using System.Security.Cryptography;

namespace Bento.Steps;

/// <summary>
/// The package result.
/// </summary>
public sealed record PackageResult(
    string ArchivePath,
    string ArchiveName,
    long SizeBytes,
    string Md5Base64,
    string ManifestPath
);

/// <summary>
/// Compresses the assembled release into the named .7z, computes the base64 MD5 hash, and writes the manifest.json.
/// </summary>
public static class PackageStep
{
    public const string Stage = "package";

    public static async Task<PackageResult> RunAsync(
        BuildContext ctx,
        IReadOnlyList<NuGetPackage> packages,
        BuildLogger log,
        CancellationToken cancellationToken = default
    )
    {
        var baseName = BuildRules.ArchiveBaseName(
            ctx.BuildType,
            ctx.Version,
            ctx.ClientVersion!,
            ctx.Server!.Commit,
            ctx.Tag,
            ctx.BuildTimeUtc
        );
        var archiveName = $"{baseName}.7z";
        var archivePath = Path.Combine(ctx.OutputDir, archiveName);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        log.Status(Stage, $"compressing {archiveName} ({(ctx.MaxCompression ? "max" : "fast")} compression)...");
        await ctx.Tools.SevenZip.CreateAsync(
            ctx.ReleaseDir,
            archivePath,
            ctx.MaxCompression,
            line => log.Line(Stage, line),
            cancellationToken
        );

        var sizeBytes = new FileInfo(archivePath).Length;
        log.Status(Stage, "hashing...");
        string md5Base64;
        await using (var stream = File.OpenRead(archivePath))
        {
            md5Base64 = Convert.ToBase64String(await MD5.HashDataAsync(stream, cancellationToken));
        }

        var manifest = new Manifest(
            BentoVersion: typeof(PackageStep).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            BuiltAt: ctx.BuildTimeUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            Mode: ctx.Fresh ? "fresh" : "dev",
            BuildType: ctx.BuildTypeProperty,
            BuildConfig: ctx.BuildConfig,
            SptVersion: ctx.Version,
            ClientVersion: ctx.ClientVersion!,
            Tag: ctx.Tag,
            Archive: new ManifestArchive(archiveName, sizeBytes, Fs.FormatSize(sizeBytes), md5Base64),
            NuGet: new ManifestNuGet(
                packages.Count > 0
                    ? packages[0].Version
                    : BuildRules.NuGetVersion(ctx.BuildType, ctx.Version, ctx.BuildTimeUtc),
                packages.Select(p => p.Id).ToList()
            ),
            Repos: new Dictionary<string, ManifestRepo>
            {
                ["server"] = ToManifestRepo(ctx, ctx.Server!),
                ["modules"] = ToManifestRepo(ctx, ctx.Modules!),
                ["launcher"] = ToManifestRepo(ctx, ctx.Launcher!),
            }
        );
        var manifestPath = Path.Combine(ctx.OutputDir, "manifest.json");
        manifest.Write(manifestPath);

        return new PackageResult(archivePath, archiveName, sizeBytes, md5Base64, manifestPath);
    }

    /// <summary>
    /// Maps a RepoInfo to its manifest entry.
    /// </summary>
    private static ManifestRepo ToManifestRepo(BuildContext ctx, RepoInfo repo)
    {
        return new ManifestRepo(ctx.Fresh ? null : repo.Path, repo.Commit, repo.Dirty);
    }
}
