namespace Bento.Steps;

/// <summary>
/// The server's win-x64 and linux-x64 publish output directories.
/// </summary>
public sealed record ServerArtifacts(string WinPublishDir, string LinuxPublishDir);

/// <summary>
/// Publishes the server for win-x64 and linux-x64.
/// </summary>
public static class ServerStep
{
    public const string Stage = "server";

    private static readonly string[] Platforms = ["linux-x64", "win-x64"];

    /// <summary>
    /// Publishes the server once per platform with the version/build metadata stamped in, then returns the two publish
    /// directories after confirming they exist.
    /// </summary>
    public static async Task<ServerArtifacts> RunAsync(
        BuildContext ctx,
        BuildLogger log,
        CancellationToken cancellationToken = default
    )
    {
        var repo = ctx.Server!;

        // Publishes platforms one at a time (both share the obj/ directory, so concurrent publishes race).
        foreach (var platform in Platforms)
        {
            log.Status(Stage, $"publishing {platform} ({ctx.BuildConfig})...");
            string[] arguments =
            [
                "publish",
                "./SPTarkov.Server/SPTarkov.Server.csproj",
                "-c",
                ctx.BuildConfig,
                "-f",
                "net10.0",
                "-r",
                platform,
                "-p:IncludeNativeLibrariesForSelfExtract=true",
                "-p:PublishSingleFile=false",
                "--self-contained",
                "false",
                $"-p:SptBuildType={ctx.BuildTypeProperty}",
                $"-p:SptVersion={ctx.Version}",
                $"-p:SptBuildTime={ctx.BuildTimeUtc:yyyyMMdd}",
                $"-p:SptCommit={repo.Commit}",
                "-p:IsPublish=true",
            ];
            var exitCode = await ProcessRunner.RunAsync(
                "dotnet",
                arguments,
                repo.Path,
                onLine: line => log.Line(Stage, line),
                cancellationToken: cancellationToken
            );
            if (exitCode != 0)
            {
                throw new StageFailedException(Stage, $"Server publish for {platform} failed (exit {exitCode}).");
            }
        }

        var artifacts = new ServerArtifacts(PublishDir("win-x64"), PublishDir("linux-x64"));
        foreach (var directory in new[] { artifacts.WinPublishDir, artifacts.LinuxPublishDir })
        {
            if (!Directory.Exists(directory))
            {
                throw new StageFailedException(Stage, $"Expected publish output missing: {directory}");
            }
        }

        return artifacts;

        // The publish output directory for one platform.
        string PublishDir(string platform)
        {
            return Path.Combine(repo.Path, "SPTarkov.Server", "bin", ctx.BuildConfig, "net10.0", platform, "publish");
        }
    }
}
