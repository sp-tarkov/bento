using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Bento;

/// <summary>
/// The available command-line options for a build. Validate runs before the command executes.
/// </summary>
public sealed class BuildSettings : CommandSettings
{
    [CommandOption("--fresh")]
    [Description(
        "Clone all three repos from GitHub at --tag into a temp dir and build there. Ignores --server/--modules/--launcher. Requires --tag."
    )]
    public bool Fresh { get; init; }

    [CommandOption("--tag <TAG>")]
    [Description(
        "Git tag to checkout for build. It must exist in all three repos. A build that doesn't use --fresh also derives the version and build type from this value."
    )]
    public string? Tag { get; init; }

    [CommandOption("--version <VERSION>")]
    [Description("SPT version of the build (e.g. 4.1.0).")]
    public string? Version { get; init; }

    [CommandOption("--build-type <TYPE>")]
    [Description("RELEASE, DEBUG, BLEEDINGEDGE, or BLEEDINGEDGEMODS.")]
    public string? BuildType { get; init; }

    [CommandOption("--server <DIR>")]
    [Description("Path to the local server-csharp repo. Not needed for --fresh builds.")]
    public string? ServerPath { get; init; }

    [CommandOption("--modules <DIR>")]
    [Description("Path to the local modules repo. Not needed for --fresh builds.")]
    public string? ModulesPath { get; init; }

    [CommandOption("--launcher <DIR>")]
    [Description("Path to the local launcher repo. Not needed for --fresh builds.")]
    public string? LauncherPath { get; init; }

    [CommandOption("--module-package <URL_OR_DIR>")]
    [Description(
        "Module package source: an http(s) base URL that serves <client_version>.7z (downloaded and cached), or a local directory of module files copied into Shared/Managed. Overrides MODULE_PACKAGE and config."
    )]
    public string? ModulePackage { get; init; }

    [CommandOption("--output <DIR>")]
    [Description("Where the .7z, manifest.json and logs land.")]
    public string? Output { get; init; }

    [CommandOption("--no-config")]
    [Description(
        "Ignore the user config file (automatic inside containers); every required value must come from flags or environment variables, and nothing prompts."
    )]
    public bool NoConfig { get; init; }

    /// <summary>
    /// Rejects a malformed --build-type or --version, a --module-package with a non-http(s) URL scheme, and --fresh
    /// without --tag, before the command body runs.
    /// </summary>
    public override ValidationResult Validate()
    {
        if (BuildType is not null && BuildRules.ParseBuildType(BuildType) is null)
        {
            return ValidationResult.Error("--build-type must be RELEASE, DEBUG, BLEEDINGEDGE, or BLEEDINGEDGEMODS.");
        }

        if (Version is not null && !BuildRules.IsValidVersion(Version))
        {
            return ValidationResult.Error("--version must look like 4.1.0");
        }

        if (
            ModulePackage is not null
            && ModulePackage.Contains("://")
            && !ModulePackageSource.LooksLikeUrl(ModulePackage)
        )
        {
            return ValidationResult.Error("--module-package must be an http(s):// URL or a local directory path.");
        }

        if (Fresh && Tag is null)
        {
            return ValidationResult.Error("--fresh requires --tag (fresh builds clone the repos at a tag).");
        }

        return ValidationResult.Success();
    }
}
