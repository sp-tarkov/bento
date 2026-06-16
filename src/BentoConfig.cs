using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bento;

/// <summary>
/// Dev-mode convenience settings persisted to ~/.bento/config.json in the user profile:  repo paths, module package
/// source, output dir, and the last-used version and build type. Never read or written in container / --no-config runs.
/// </summary>
public sealed class BentoConfig
{
    public string? ServerPath { get; set; }
    public string? ModulesPath { get; set; }
    public string? LauncherPath { get; set; }
    public string? ModulePackage { get; set; }
    public string? OutputDir { get; set; }
    public string? LastVersion { get; set; }
    public string? LastBuildType { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The ~/.bento directory that holds the config file and the module package cache.
    /// </summary>
    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bento");

    /// <summary>
    /// The config.json path inside <see cref="DefaultDir"/>.
    /// </summary>
    public static string DefaultPath => Path.Combine(DefaultDir, "config.json");

    /// <summary>
    /// Reads and deserializes the config file, or returns null when it does not exist.
    /// </summary>
    public static BentoConfig? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BentoConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new BentoException(
                $"Config file at {path} is not valid JSON: {ex.Message}",
                "Fix or delete the file; bento recreates it on the next interactive run."
            );
        }
    }

    /// <summary>
    /// Serializes the config to the given path, creating the parent directory if needed.
    /// </summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
