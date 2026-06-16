using Spectre.Console;

namespace Bento.Steps;

/// <summary>
/// Builds the release tree under the staging dir: merges the server, launcher, and modules outputs, prunes the
/// builds, sets Linux exec bits, and copies over the static assets.
/// </summary>
public static class AssembleStep
{
    public const string Stage = "assemble";

    private static readonly string[] PruneFileNames =
    [
        "web.config",
        "SPT.Server.staticwebassets.endpoints.json",
        "SPT.Server.Linux.staticwebassets.endpoints.json",
    ];

    public static void Run(
        BuildContext ctx,
        ServerArtifacts server,
        string modulesBuildDir,
        string launcherBuildDir,
        BuildLogger log
    )
    {
        Fs.DeleteDirectory(ctx.StagingDir);
        var release = ctx.ReleaseDir;
        var sptDir = Path.Combine(release, "SPT");
        Directory.CreateDirectory(sptDir);

        log.Status(Stage, "merging server artifacts...");
        Fs.CopyDirectory(server.LinuxPublishDir, sptDir, ExcludePdb);
        Fs.CopyDirectory(server.WinPublishDir, sptDir, ExcludePdb);

        log.Status(Stage, "merging launcher artifacts...");
        Fs.CopyDirectory(launcherBuildDir, sptDir);

        // Modules land at the release root, not under SPT/.
        log.Status(Stage, "merging modules artifacts...");
        Fs.CopyDirectory(modulesBuildDir, release);

        log.Status(Stage, "pruning build files...");
        var pruned = 0;
        foreach (var file in Directory.EnumerateFiles(release, "*", SearchOption.AllDirectories))
        {
            if (PruneFileNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
            {
                File.Delete(file);
                pruned++;
            }
        }

        foreach (var file in Directory.EnumerateFiles(release, "*.xml", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
            pruned++;
        }

        log.Line(Stage, $"pruned {pruned} files");

        // Sets the execute bit on the Linux server and launcher binaries (skipped on Windows).
        if (!OperatingSystem.IsWindows())
        {
            foreach (var binaryName in new[] { "SPT.Server.Linux", "SPT.Launcher.Linux" })
            {
                foreach (var file in Directory.EnumerateFiles(release, binaryName, SearchOption.AllDirectories))
                {
                    File.SetUnixFileMode(
                        file,
                        File.GetUnixFileMode(file)
                            | UnixFileMode.UserExecute
                            | UnixFileMode.GroupExecute
                            | UnixFileMode.OtherExecute
                    );
                }
            }
        }

        log.Status(Stage, "overlaying static assets...");
        Fs.CopyDirectory(StaticAssetsDir(), release);
    }

    /// <summary>
    /// Prints the release tree. Include top-level directories (with file counts and sizes) then files.
    /// </summary>
    public static void PrintReleaseTree(string releaseDir)
    {
        AnsiConsole.WriteLine();
        var tree = new Tree($"[bold]release[/] [grey]({Fs.FormatSize(Fs.DirectorySize(releaseDir))})[/]");
        foreach (var directory in Directory.EnumerateDirectories(releaseDir).Order())
        {
            var fileCount = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count();
            tree.AddNode(
                $"{Markup.Escape(Path.GetFileName(directory))}/ [grey]({fileCount} files, {Fs.FormatSize(Fs.DirectorySize(directory))})[/]"
            );
        }

        foreach (var file in Directory.EnumerateFiles(releaseDir).Order())
        {
            tree.AddNode(Markup.Escape(Path.GetFileName(file)));
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Returns the assets/ directory next to the bento binary, throwing when it is missing.
    /// </summary>
    private static string StaticAssetsDir()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "assets");
        if (!Directory.Exists(directory))
        {
            throw new BentoException(
                $"Static assets not found at {directory}.",
                "The assets/ directory is missing. What did you do!?"
            );
        }

        return directory;
    }

    /// <summary>
    /// Copy predicate that excludes .pdb files.
    /// </summary>
    private static bool ExcludePdb(string relativePath)
    {
        return relativePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);
    }
}
