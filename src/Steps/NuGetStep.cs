namespace Bento.Steps;

/// <summary>
/// A packed NuGet package.
/// </summary>
public sealed record NuGetPackage(string Id, string Version, string File);

/// <summary>
/// Packs the server libraries into .nupkg files under the output dir. Bento only produces the packages; pushing them to
/// a feed is left to the orchestrating workflow, so no API key ever enters the build.
/// </summary>
public static class NuGetStep
{
    public const string Stage = "nuget";

    // The packable server libraries. dotnet pack emits a .nupkg per packable project in the solution; only these are
    // recorded, matching the packages the release workflow publishes.
    private static readonly string[] PackageIds =
    [
        "SPTarkov.Common",
        "SPTarkov.DI",
        "SPTarkov.Reflection",
        "SPTarkov.Server.Assets",
        "SPTarkov.Server.Web",
        "SPTarkov.Server.Core",
    ];

    /// <summary>
    /// Packs the server solution into NuGetDir, prunes any .nupkg the orchestrating workflow does not publish, then
    /// returns the recorded packages.
    /// </summary>
    public static async Task<IReadOnlyList<NuGetPackage>> RunAsync(
        BuildContext ctx,
        BuildLogger log,
        CancellationToken cancellationToken = default
    )
    {
        var repo = ctx.Server!;
        var version = BuildRules.NuGetVersion(ctx.BuildType, ctx.Version, ctx.BuildTimeUtc);
        Directory.CreateDirectory(ctx.NuGetDir);

        log.Status(Stage, $"packing {version} ({ctx.BuildConfig})...");
        string[] arguments = ["pack", "-o", ctx.NuGetDir, "-c", ctx.BuildConfig, $"-p:Version={version}"];
        var exitCode = await ProcessRunner.RunAsync(
            "dotnet",
            arguments,
            repo.Path,
            onLine: line => log.Line(Stage, line),
            cancellationToken: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new StageFailedException(Stage, $"NuGet pack failed (exit {exitCode}).");
        }

        var packages = new List<NuGetPackage>();
        foreach (var id in PackageIds)
        {
            var file = Path.Combine(ctx.NuGetDir, $"{id}.{version}.nupkg");
            if (File.Exists(file))
            {
                packages.Add(new NuGetPackage(id, version, file));
            }
            else
            {
                log.Line(Stage, $"expected package not produced: {Path.GetFileName(file)}");
            }
        }

        PruneExtras(ctx.NuGetDir, packages, log);

        return packages;
    }

    // dotnet pack emits a .nupkg per packable project. Remove anything that is not a recorded package so the output
    // directory matches the packages the workflow publishes.
    private static void PruneExtras(string nugetDir, IReadOnlyList<NuGetPackage> packages, BuildLogger log)
    {
        var keep = packages.Select(p => Path.GetFullPath(p.File)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(nugetDir))
        {
            if (keep.Contains(Path.GetFullPath(path)))
            {
                continue;
            }

            File.Delete(path);
            log.Line(Stage, $"pruned {Path.GetFileName(path)}");
        }
    }
}
