using System.Text.Json;

namespace Bento;

/// <summary>
/// The external tools a build depends on.
/// </summary>
public sealed record ToolSet(string DotnetVersion, SevenZip SevenZip);

/// <summary>
/// A repository's identity.
/// </summary>
public sealed record RepoInfo(string Name, string Path, string Branch, string Commit, bool Dirty);

/// <summary>
/// Checks that run before build. Required external tools, repository validity, Git LFS pull of assets, and reading the
/// compatible client version.
/// </summary>
public static class Preflight
{
    private const string LfsPointerSignature = "version https://git-lfs";

    /// <summary>
    /// Verifies dotnet, git (and LFS) run, and locates 7-Zip, returning the resolved tool set or throwing a hinted
    /// error for whatever is missing.
    /// </summary>
    public static async Task<ToolSet> CheckToolsAsync(bool needLfs)
    {
        var dotnetVersion = await CheckToolAsync(
            ".NET SDK",
            "dotnet",
            ["--version"],
            PlatformHint(
                "Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0",
                "Install the .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
            )
        );
        await CheckToolAsync(
            "git",
            "git",
            ["--version"],
            PlatformHint(
                "Install git: https://git-scm.com/download/win",
                "Install git via your package manager (e.g. apt-get install git)."
            )
        );
        if (needLfs)
        {
            await CheckToolAsync(
                "Git LFS",
                "git",
                ["lfs", "version"],
                PlatformHint(
                    "Install Git LFS: https://git-lfs.com",
                    "Install Git LFS via your package manager (e.g. apt-get install git-lfs)."
                )
            );
        }

        var sevenZip =
            await SevenZip.LocateAsync()
            ?? throw new BentoException(
                "7-Zip was not found (tried 7z, 7za and the default install locations).",
                PlatformHint(
                    "Install 7-Zip from https://www.7-zip.org or put 7z.exe on PATH.",
                    "Install p7zip (e.g. apt-get install p7zip-full)."
                )
            );

        return new ToolSet(dotnetVersion, sevenZip);
    }

    /// <summary>
    /// Picks the Windows or Linux variant of an install hint based on the OS.
    /// </summary>
    private static string PlatformHint(string windows, string linux) => OperatingSystem.IsWindows() ? windows : linux;

    /// <summary>
    /// Confirms the path is a git repository and returns its name, absolute path, short commit, and dirty flag; throws
    /// with a clone hint when the path is missing or not a repo.
    /// </summary>
    public static async Task<RepoInfo> InspectRepoAsync(string name, string path, string cloneUrl)
    {
        if (!Directory.Exists(path))
        {
            throw new BentoException(
                $"The {name} repository was not found at: {path}",
                $"Clone it first: git clone {cloneUrl} \"{path}\""
            );
        }

        var (exitCode, commit, stdErr) = await ProcessRunner.CaptureAsync(
            "git",
            ["rev-parse", "--short", "HEAD"],
            path
        );
        if (exitCode != 0)
        {
            throw new BentoException(
                $"'{path}' does not look like a git repository: {stdErr}",
                $"Point bento at a real clone of {cloneUrl}"
            );
        }

        var (_, status, _) = await ProcessRunner.CaptureAsync("git", ["status", "--porcelain"], path);
        var (_, branch, _) = await ProcessRunner.CaptureAsync("git", ["rev-parse", "--abbrev-ref", "HEAD"], path);
        return new RepoInfo(name, System.IO.Path.GetFullPath(path), branch, commit, status.Length > 0);
    }

    /// <summary>
    /// Detects unhydrated Git LFS content in the server assets: throws when items.json is still a small LFS pointer.
    /// </summary>
    public static void CheckServerLfs(string serverPath)
    {
        var marker = Path.Combine(
            serverPath,
            "Libraries",
            "SPTarkov.Server.Assets",
            "SPT_Data",
            "database",
            "templates",
            "items.json"
        );

        // THe real content is much larger than a pointer file... skip when the marker is missing or already large.
        if (!File.Exists(marker) || new FileInfo(marker).Length > 1024)
        {
            return;
        }

        using var reader = new StreamReader(marker);
        var buffer = new char[LfsPointerSignature.Length];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        if (new string(buffer, 0, read).StartsWith(LfsPointerSignature, StringComparison.Ordinal))
        {
            throw new BentoException(
                "server-csharp's large assets are Git LFS pointer files, not real content.",
                $"Run: git lfs install --local && git lfs pull (in {serverPath})"
            );
        }
    }

    /// <summary>
    /// Reads compatibleTarkovVersion from the server's core.json and returns the trailing client build number.
    /// </summary>
    public static string ReadClientVersion(string serverPath)
    {
        var coreJsonPath = Path.Combine(
            serverPath,
            "Libraries",
            "SPTarkov.Server.Assets",
            "SPT_Data",
            "configs",
            "core.json"
        );
        if (!File.Exists(coreJsonPath))
        {
            throw new BentoException(
                $"core.json not found at {coreJsonPath}",
                "Is the server-csharp checkout complete?"
            );
        }

        using var document = JsonDocument.Parse(File.ReadAllText(coreJsonPath));
        if (
            !document.RootElement.TryGetProperty("compatibleTarkovVersion", out var property)
            || property.GetString() is not { Length: > 0 } fullVersion
        )
        {
            throw new BentoException($"{coreJsonPath} has no compatibleTarkovVersion value.");
        }

        return BuildRules.ClientVersionFrom(fullVersion);
    }

    /// <summary>
    /// Runs a tool's probe command, throwing a hinted BentoException when it is missing or exits non-zero; returns the
    /// command's standard output.
    /// </summary>
    private static async Task<string> CheckToolAsync(
        string displayName,
        string fileName,
        string[] arguments,
        string hint
    )
    {
        (int ExitCode, string StdOut, string StdErr) result;
        try
        {
            result = await ProcessRunner.CaptureAsync(fileName, arguments);
        }
        catch (BentoException)
        {
            throw new BentoException($"Required tool '{displayName}' was not found.", hint);
        }

        if (result.ExitCode != 0)
        {
            throw new BentoException(
                $"Required tool '{displayName}' failed its check (exit {result.ExitCode}): {result.StdErr}",
                hint
            );
        }

        return result.StdOut;
    }
}
