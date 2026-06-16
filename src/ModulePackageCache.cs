using System.Net;
using Spectre.Console;

namespace Bento;

/// <summary>
/// The summary row for the module package.
/// </summary>
public sealed record ModulePackageStatus(string Markup, bool Ok);

/// <summary>
/// What the modules stage works with. Either a directory of module files to copy into Shared/Managed or a .7z archive
/// to extract there.
/// </summary>
public sealed record ResolvedModulePackage(string Path, bool IsDirectory);

/// <summary>
/// Resolves the module package files the modules project builds against from a single --module-package source, with a
/// strict priority: a local directory, the ~/.bento/cache, download from the base URL. Downloads are cached at
/// ~/.bento/cache. Status messages should never print the URL.
/// </summary>
public static class ModulePackageCache
{
    /// <summary>
    /// The ~/.bento/cache directory holding downloaded module packages.
    /// </summary>
    public static string CacheDir => Path.Combine(BentoConfig.DefaultDir, "cache");

    /// <summary>
    /// The cache file path for a client version's module package.
    /// </summary>
    public static string CachePathFor(string clientVersion)
    {
        return Path.Combine(CacheDir, $"{clientVersion}.7z");
    }

    /// <summary>
    /// Reports where the package would come from and whether it is actually there, for the build summary: a local
    /// directory and the cache are stat'd, a URL gets an HTTP HEAD (with a headers-only GET fallback). URLs are never
    /// printed, only the file name.
    /// </summary>
    public static async Task<ModulePackageStatus> VerifyAsync(
        string? modulePackage,
        string? clientVersion,
        CancellationToken cancellationToken = default
    )
    {
        var source = ModulePackageSource.Parse(modulePackage);

        // A local directory is self-contained; stat it directly.
        if (source is { IsDirectory: true })
        {
            var dir = source.Value;
            var display = $"dir: {Markup.Escape(dir)}";
            if (!Directory.Exists(dir))
            {
                return new ModulePackageStatus($"{display} [red](missing)[/]", Ok: false);
            }

            var fileCount = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
            if (fileCount == 0)
            {
                return new ModulePackageStatus($"{display} [red](empty)[/]", Ok: false);
            }

            return new ModulePackageStatus(
                $"{display} [green](found, {fileCount} files, {Fs.FormatSize(Fs.DirectorySize(dir))})[/]",
                Ok: true
            );
        }

        // A cached <client_version>.7z satisfies both a URL source and the no-input case.
        if (clientVersion is not null)
        {
            var cachePath = CachePathFor(clientVersion);
            if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
            {
                return new ModulePackageStatus(
                    $"cache: {Markup.Escape(cachePath)} [green](found, {Fs.FormatSize(new FileInfo(cachePath).Length)})[/]",
                    Ok: true
                );
            }
        }

        if (source is not { IsUrl: true })
        {
            return new ModulePackageStatus(
                "[red]unresolved: no --module-package source or cached archive[/]",
                Ok: false
            );
        }

        if (clientVersion is null)
        {
            // Fresh mode pre-clone: the client version (and therefore the URL) isn't known yet.
            return new ModulePackageStatus("download: <client_version>.7z [grey](verified after clone)[/]", Ok: true);
        }

        var url = $"{source.Value}/{clientVersion}.7z";
        var label = $"download: {Markup.Escape(FileNameOf(url))}";
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            using var response = await ProbeAsync(http, url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var size = response.Content.Headers.ContentLength;
                var sizeNote = size is { } bytes ? $", {Fs.FormatSize(bytes)}" : string.Empty;
                return new ModulePackageStatus($"{label} [green](available{sizeNote})[/]", Ok: true);
            }

            return new ModulePackageStatus(
                $"{label} [red](HTTP {(int)response.StatusCode}, not available)[/]",
                Ok: false
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ModulePackageStatus($"{label} [red](unreachable)[/]", Ok: false);
        }
    }

    /// <summary>
    /// Sends a HEAD request, falling back to a headers-only GET when the server rejects HEAD.
    /// </summary>
    private static async Task<HttpResponseMessage> ProbeAsync(
        HttpClient http,
        string url,
        CancellationToken cancellationToken
    )
    {
        using var head = new HttpRequestMessage(HttpMethod.Head, url);
        var response = await http.SendAsync(head, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented))
        {
            return response;
        }

        response.Dispose();
        return await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    /// <summary>
    /// Extracts the file name from a URL, or a placeholder when it cannot be parsed.
    /// </summary>
    private static string FileNameOf(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Path.GetFileName(uri.LocalPath) : "<module package>";
    }

    /// <summary>
    /// Returns the package source for the modules stage, downloading into the cache if needed.
    /// </summary>
    public static async Task<ResolvedModulePackage> ResolveAsync(
        string? modulePackage,
        string clientVersion,
        Action<string> status,
        CancellationToken cancellationToken = default
    )
    {
        var source = ModulePackageSource.Parse(modulePackage);

        // A local directory always wins when provided; it never falls back.
        if (source is { IsDirectory: true })
        {
            var dir = source.Value;
            if (!Directory.Exists(dir))
            {
                throw new BentoException(
                    $"Module package directory not found: {dir}",
                    "When --module-package is a directory it must exist and contain the module package files; it does not fall back to the cache or a download."
                );
            }

            return !Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any()
                ? throw new BentoException($"Module package directory is empty: {dir}")
                : new ResolvedModulePackage(dir, IsDirectory: true);
        }

        var cachePath = CachePathFor(clientVersion);
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0)
        {
            status($"module package {clientVersion}.7z found in cache");
            return new ResolvedModulePackage(cachePath, IsDirectory: false);
        }

        var url = source is { IsUrl: true }
            ? $"{source.Value}/{clientVersion}.7z"
            : throw new BentoException(
                "No source for the module package.",
                "Pass --module-package <url|dir>, set the MODULE_PACKAGE environment variable, or save it in the Bento config."
            );

        Directory.CreateDirectory(CacheDir);
        var tempPath = cachePath + ".tmp";
        status($"downloading module package {clientVersion}.7z...");
        using (var http = new HttpClient())
        {
            http.Timeout = Timeout.InfiniteTimeSpan;
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new BentoException(
                    $"Module package download failed: HTTP {(int)response.StatusCode} for {clientVersion}.7z.",
                    "Check the --module-package URL and that the client version is published."
                );
            }

            await using var file = File.Create(tempPath);
            await response.Content.CopyToAsync(file, cancellationToken);
        }

        if (new FileInfo(tempPath).Length == 0)
        {
            File.Delete(tempPath);
            throw new BentoException($"Downloaded module package {clientVersion}.7z is empty.");
        }

        File.Move(tempPath, cachePath, overwrite: true);
        status($"module package cached at {cachePath}");
        return new ResolvedModulePackage(cachePath, IsDirectory: false);
    }
}
