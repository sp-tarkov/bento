namespace Bento;

/// <summary>
/// Everything the build steps need.
/// </summary>
public sealed class BuildContext
{
    public required bool Fresh { get; init; }
    public required SptBuildType BuildType { get; init; }
    public required string Version { get; init; }
    public string? Tag { get; init; }
    public required string OutputDir { get; init; }
    public string? ModulePackage { get; set; }
    public required DateTime BuildTimeUtc { get; init; }
    public required ToolSet Tools { get; init; }

    public string? DevServerPath { get; init; }
    public string? DevModulesPath { get; init; }
    public string? DevLauncherPath { get; init; }

    public RepoInfo? Server { get; set; }
    public RepoInfo? Modules { get; set; }
    public RepoInfo? Launcher { get; set; }
    public string? ClientVersion { get; set; }
    public string? FreshTempRoot { get; set; }

    /// <summary>
    /// The parsed module package source (URL or directory), or null when none is set.
    /// </summary>
    public ModulePackageSource? PackageSource => ModulePackageSource.Parse(ModulePackage);

    /// <summary>
    /// The MSBuild configuration (Release or Debug) this build type compiles under.
    /// </summary>
    public string BuildConfig => BuildRules.BuildConfigFor(BuildType);

    /// <summary>
    /// The all-caps build type string passed to the builds as -p:SptBuildType.
    /// </summary>
    public string BuildTypeProperty => BuildType.ToPropertyValue();

    /// <summary>
    /// Use LZMA2 -mx=9 for releases and all fresh builds, or -mx=1 for anything else.
    /// </summary>
    public bool MaxCompression => Fresh || BuildType == SptBuildType.Release;

    /// <summary>
    /// Scratch directory under the output dir where the release tree is assembled.
    /// </summary>
    public string StagingDir => Path.Combine(OutputDir, ".staging");

    /// <summary>
    /// The assembled release tree inside the staging dir.
    /// </summary>
    public string ReleaseDir => Path.Combine(StagingDir, "release");

    /// <summary>
    /// Per-stage log directory under the output dir.
    /// </summary>
    public string LogDir => Path.Combine(OutputDir, "logs");
}
