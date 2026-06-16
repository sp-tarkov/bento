namespace Bento;

/// <summary>
/// Whether a --module-package value is a download base URL or a local directory.
/// </summary>
public enum ModulePackageKind
{
    Url,
    Directory,
}

/// <summary>
/// A parsed --module-package value: an http base URL that serves client_version.7z archives to download, or a local
/// directory of module files to copy into Shared/Managed. The kind is decided purely by an https prefix.
/// </summary>
public sealed record ModulePackageSource(string Value, ModulePackageKind Kind)
{
    public bool IsUrl => Kind == ModulePackageKind.Url;
    public bool IsDirectory => Kind == ModulePackageKind.Directory;

    /// <summary>
    /// Classifies a raw value into a URL (trailing slash trimmed) or a normalized directory path, returning null when
    /// the value is null or blank.
    /// </summary>
    public static ModulePackageSource? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        return LooksLikeUrl(trimmed)
            ? new ModulePackageSource(trimmed.TrimEnd('/'), ModulePackageKind.Url)
            : new ModulePackageSource(Path.GetFullPath(trimmed), ModulePackageKind.Directory);
    }

    /// <summary>
    /// True when the value starts with an http:// or https:// scheme.
    /// </summary>
    public static bool LooksLikeUrl(string value)
    {
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
