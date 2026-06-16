using System.Globalization;
using System.Text.RegularExpressions;

namespace Bento;

/// <summary>
/// The four release channels SPT builds for.
/// </summary>
public enum SptBuildType
{
    Release,
    Debug,
    BleedingEdge,
    BleedingEdgeMods,
}

/// <summary>
/// Maps tags to build types, validates versions, derives the client version, and constructs archive names.
/// </summary>
public static partial class BuildRules
{
    /// <summary>
    /// Matches an all-caps plain semver tag, optionally V-prefixed (e.g. V4.1.0).
    /// </summary>
    [GeneratedRegex(@"^V?\d+\.\d+\.\d+$")]
    private static partial Regex PlainSemverUpper();

    // Matches a strictly numeric X.Y.Z version. The three repos stamp this into AssemblyVersion and FileVersion, which
    // accept only numeric major.minor.build.revision segments, so a suffix like 4.1.0-dev is rejected.
    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex Semver();

    /// <summary>
    /// Maps a git tag to its build type.
    /// </summary>
    public static SptBuildType BuildTypeFromTag(string tag)
    {
        // Tests the uppercased tag for the -BEM/-BE substrings.
        var upper = tag.ToUpperInvariant();
        if (upper.Contains("-BEM"))
        {
            return SptBuildType.BleedingEdgeMods;
        }

        if (upper.Contains("-BE"))
        {
            return SptBuildType.BleedingEdge;
        }

        return PlainSemverUpper().IsMatch(upper) ? SptBuildType.Release : SptBuildType.Debug;
    }

    /// <summary>
    /// Parses a build-type string (case-insensitive) into an enum value, or null when unrecognized.
    /// </summary>
    public static SptBuildType? ParseBuildType(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "RELEASE" => SptBuildType.Release,
            "DEBUG" => SptBuildType.Debug,
            "BLEEDINGEDGE" => SptBuildType.BleedingEdge,
            "BLEEDINGEDGEMODS" => SptBuildType.BleedingEdgeMods,
            _ => null,
        };
    }

    /// <summary>
    /// The all-caps value passed to the server build as -p:SptBuildType.
    /// </summary>
    public static string ToPropertyValue(this SptBuildType type)
    {
        return type switch
        {
            SptBuildType.Release => "RELEASE",
            SptBuildType.Debug => "DEBUG",
            SptBuildType.BleedingEdge => "BLEEDINGEDGE",
            SptBuildType.BleedingEdgeMods => "BLEEDINGEDGEMODS",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    /// <summary>
    /// Maps a build type to the MSBuild configuration it builds under (Release or Debug).
    /// </summary>
    public static string BuildConfigFor(SptBuildType type)
    {
        return type is SptBuildType.Release or SptBuildType.BleedingEdgeMods ? "Release" : "Debug";
    }

    /// <summary>
    /// Tag prefix up to the first dash, minus any leading v.
    /// </summary>
    public static string VersionFromTag(string tag)
    {
        var version = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        var dash = version.IndexOf('-');
        return dash < 0 ? version : version[..dash];
    }

    /// <summary>
    /// Reports whether the version is a strictly numeric X.Y.Z.
    /// </summary>
    public static bool IsValidVersion(string version)
    {
        return Semver().IsMatch(version);
    }

    /// <summary>
    /// Last dot-segment of core.json's compatibleTarkovVersion.
    /// </summary>
    public static string ClientVersionFrom(string compatibleTarkovVersion)
    {
        return compatibleTarkovVersion[(compatibleTarkovVersion.LastIndexOf('.') + 1)..];
    }

    /// <summary>
    /// The version stamped onto the server NuGet packages. A release uses the plain SPT version; every other build type
    /// uses a pre-release version stamped with the UTC build time so same-day builds stay unique and sort correctly.
    /// </summary>
    public static string NuGetVersion(SptBuildType type, string version, DateTime buildTimeUtc)
    {
        return type == SptBuildType.Release
            ? version
            : $"{version}-pre.{buildTimeUtc.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Builds the archive base name (no extension) per the release and non-release naming rules.
    /// </summary>
    public static string ArchiveBaseName(
        SptBuildType type,
        string version,
        string clientVersion,
        string serverCommit,
        string? tag,
        DateTime buildTimeUtc
    )
    {
        if (type == SptBuildType.Release)
        {
            return $"SPT-{version}-{clientVersion}-{serverCommit}";
        }

        // Non-release build types maybe have a trailing segment as the filename suffix (4.0.3-BEM-SUFFIX) otherwise the
        // build date is appended to the end of the filename.
        string? tagSuffix = null;
        if (tag is not null)
        {
            var upper = tag.ToUpperInvariant();
            if (upper.Count(c => c == '-') >= 2)
            {
                tagSuffix = upper[(upper.LastIndexOf('-') + 1)..];
            }
        }

        var trailing = tagSuffix ?? buildTimeUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"SPT-{type.ToPropertyValue()}-{version}-{clientVersion}-{serverCommit}-{trailing}";
    }
}
