namespace Bento.Steps;

/// <summary>
/// Fresh source acquisition. Verify the tag exists in all three repos, then shallow-clone each at the tag into a temp
/// build root and pull the server's LFS assets. All repos must be public.
/// </summary>
public static class FreshCloneStep
{
    public const string ServerUrl = "https://github.com/sp-tarkov/server-csharp.git";
    public const string ModulesUrl = "https://github.com/sp-tarkov/modules.git";
    public const string LauncherUrl = "https://github.com/sp-tarkov/launcher.git";

    private static readonly (string Name, string Url)[] Repos =
    [
        ("server", ServerUrl),
        ("modules", ModulesUrl),
        ("launcher", LauncherUrl),
    ];

    /// <summary>
    /// Verifies the tag exists in all three remote repos, throwing with the list of repos that are missing it.
    /// </summary>
    public static async Task EnsureTagExistsEverywhereAsync(string tag, Action<string> status)
    {
        var missing = new List<string>();
        await Task.WhenAll(
            Repos.Select(async repo =>
            {
                status($"checking for tag {tag} in {repo.Name}...");
                var (exitCode, stdOut, stdErr) = await ProcessRunner.CaptureAsync(
                    "git",
                    ["ls-remote", "--tags", repo.Url, tag]
                );
                if (exitCode != 0)
                {
                    throw new BentoException($"git ls-remote failed for {repo.Url}: {stdErr}");
                }

                // Match both the tag ref and its peeled (^{}) form for annotated tags.
                var found = stdOut
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Any(line =>
                        line.TrimEnd().EndsWith($"refs/tags/{tag}", StringComparison.Ordinal)
                        || line.TrimEnd().EndsWith($"refs/tags/{tag}^{{}}", StringComparison.Ordinal)
                    );
                if (!found)
                {
                    lock (missing)
                    {
                        missing.Add(repo.Name);
                    }
                }
            })
        );

        if (missing.Count > 0)
        {
            throw new BentoException(
                $"Tag '{tag}' was not found in: {string.Join(", ", missing)}.",
                "A fresh build needs the same tag in server-csharp, modules, and launcher."
            );
        }
    }

    /// <summary>
    /// Shallow-clones all three repos at the tag into a fresh temp root, pulls the server's LFS assets, then inspects
    /// each clone and reads the client version.
    /// </summary>
    public static async Task CloneAllAsync(BuildContext ctx, BuildLogger log)
    {
        var root = Path.Combine(Path.GetTempPath(), $"bento-{Path.GetRandomFileName().Replace(".", string.Empty)}");
        Directory.CreateDirectory(root);
        ctx.FreshTempRoot = root;

        await Task.WhenAll(
            Repos.Select(repo => CloneOneAsync(repo.Name, repo.Url, ctx.Tag!, Path.Combine(root, repo.Name), log))
        );

        // Handle LFS assets. Install the hooks locally, then pull.
        var serverDir = Path.Combine(root, "server");
        await RunGitAsync(serverDir, ["lfs", "install", "--local"], log, "server");
        log.Status("server", "pulling Git LFS assets...");
        await RunGitAsync(serverDir, ["lfs", "pull"], log, "server");

        ctx.Server = await Preflight.InspectRepoAsync("server", serverDir, ServerUrl);
        ctx.Modules = await Preflight.InspectRepoAsync("modules", Path.Combine(root, "modules"), ModulesUrl);
        ctx.Launcher = await Preflight.InspectRepoAsync("launcher", Path.Combine(root, "launcher"), LauncherUrl);
        ctx.ClientVersion = Preflight.ReadClientVersion(serverDir);
    }

    /// <summary>
    /// Shallow-clones one repo at the tag into directory, streaming git output to the stage log.
    /// </summary>
    private static async Task CloneOneAsync(string name, string url, string tag, string directory, BuildLogger log)
    {
        log.Status(name, $"cloning at {tag}...");
        await RunGitAsync(
            workingDirectory: null,
            ["clone", "--depth", "1", "--branch", tag, "-c", "advice.detachedHead=false", url, directory],
            log,
            name
        );
    }

    /// <summary>
    /// Runs git with output streamed to the stage log, throwing a StageFailedException on a non-zero exit.
    /// </summary>
    private static async Task RunGitAsync(string? workingDirectory, string[] arguments, BuildLogger log, string stage)
    {
        var exitCode = await ProcessRunner.RunAsync(
            "git",
            arguments,
            workingDirectory,
            onLine: line => log.Line(stage, line)
        );
        if (exitCode != 0)
        {
            throw new StageFailedException(stage, $"git {arguments[0]} failed (exit {exitCode}) for the {stage} repo.");
        }
    }
}
