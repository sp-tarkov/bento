using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bento;

/// <summary>
/// A repo's entry in the manifest. The path (in dev mode), commit hash, and dirty flag.
/// </summary>
public sealed record ManifestRepo(string? Path, string Commit, bool Dirty);

/// <summary>
/// The archive's entry in the manifest. The name, byte size, display size, and base64 MD5.
/// </summary>
public sealed record ManifestArchive(string Name, long SizeBytes, string SizeDisplay, string Md5Base64);

/// <summary>
/// The record of what went into the build, written as manifest.json next to the archive. This is Bento's CI interface;
/// workflows parse it instead of Bento emitting CI-specific outputs.
/// </summary>
public sealed record Manifest(
    string BentoVersion,
    string BuiltAt,
    string Mode,
    string BuildType,
    string BuildConfig,
    string SptVersion,
    string ClientVersion,
    string? Tag,
    ManifestArchive Archive,
    Dictionary<string, ManifestRepo> Repos
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the manifest to the given path as JSON.
    /// </summary>
    public void Write(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
