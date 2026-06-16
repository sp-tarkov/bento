using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Tests for the tag, version and archive-name rules in BuildRules.
/// </summary>
[TestFixture]
public class BuildRulesTests
{
    private static readonly DateTime BuildTime = new(2026, 6, 12, 14, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Maps a tag to its build type via the suffix rules.
    /// </summary>
    [TestCase("4.1.0", ExpectedResult = SptBuildType.Release)]
    [TestCase("v4.1.0", ExpectedResult = SptBuildType.Release)]
    [TestCase("4.1.0-BE-20260101", ExpectedResult = SptBuildType.BleedingEdge)]
    [TestCase("4.1.0-be-20260101", ExpectedResult = SptBuildType.BleedingEdge)]
    [TestCase("4.1.0-BEM-20260101", ExpectedResult = SptBuildType.BleedingEdgeMods)]
    [TestCase("4.1.0-bem", ExpectedResult = SptBuildType.BleedingEdgeMods)]
    [TestCase("4.1.0-rc1", ExpectedResult = SptBuildType.Debug)]
    [TestCase("nightly", ExpectedResult = SptBuildType.Debug)]
    [TestCase("4.1.0-BETA", ExpectedResult = SptBuildType.BleedingEdge)] // -BETA contains -BE, treated as bleeding edge.
    public SptBuildType BuildTypeFromTag(string tag)
    {
        return BuildRules.BuildTypeFromTag(tag);
    }

    /// <summary>
    /// Derives the version from a tag by stripping a leading v and any suffix.
    /// </summary>
    [TestCase("4.1.0", ExpectedResult = "4.1.0")]
    [TestCase("v4.1.0", ExpectedResult = "4.1.0")]
    [TestCase("4.1.0-BE-20260101", ExpectedResult = "4.1.0")]
    [TestCase("4.1.0-BEM-20260101", ExpectedResult = "4.1.0")]
    public string VersionFromTag(string tag)
    {
        return BuildRules.VersionFromTag(tag);
    }

    /// <summary>
    /// Maps each build type to the MSBuild configuration it compiles under.
    /// </summary>
    [TestCase(SptBuildType.Release, ExpectedResult = "Release")]
    [TestCase(SptBuildType.BleedingEdgeMods, ExpectedResult = "Release")]
    [TestCase(SptBuildType.Debug, ExpectedResult = "Debug")]
    [TestCase(SptBuildType.BleedingEdge, ExpectedResult = "Debug")]
    public string BuildConfig(SptBuildType type)
    {
        return BuildRules.BuildConfigFor(type);
    }

    /// <summary>
    /// Maps build types to their all-caps -p:SptBuildType property strings.
    /// </summary>
    [TestCase(SptBuildType.Release, ExpectedResult = "RELEASE")]
    [TestCase(SptBuildType.BleedingEdgeMods, ExpectedResult = "BLEEDINGEDGEMODS")]
    public string PropertyValue(SptBuildType type)
    {
        return type.ToPropertyValue();
    }

    /// <summary>
    /// Takes the last dot-segment of compatibleTarkovVersion as the client version.
    /// </summary>
    [TestCase("0.16.9.40743", ExpectedResult = "40743")]
    [TestCase("1.2.3", ExpectedResult = "3")]
    public string ClientVersion(string compatibleTarkovVersion)
    {
        return BuildRules.ClientVersionFrom(compatibleTarkovVersion);
    }

    /// <summary>
    /// Parses build-type strings case-insensitively into the enum.
    /// </summary>
    [TestCase("RELEASE", ExpectedResult = SptBuildType.Release)]
    [TestCase("release", ExpectedResult = SptBuildType.Release)]
    [TestCase("BleedingEdge", ExpectedResult = SptBuildType.BleedingEdge)]
    [TestCase("BLEEDINGEDGEMODS", ExpectedResult = SptBuildType.BleedingEdgeMods)]
    public SptBuildType? ParseBuildType(string value)
    {
        return BuildRules.ParseBuildType(value);
    }

    /// <summary>
    /// Returns null for an unrecognized build-type string.
    /// </summary>
    [Test]
    public void ParseBuildTypeRejectsUnknownValues()
    {
        Assert.That(BuildRules.ParseBuildType("nope"), Is.Null);
    }

    /// <summary>
    /// Accepts only strictly numeric X.Y.Z versions. Suffixed or short ones are rejected.
    /// </summary>
    [TestCase("4.1.0", ExpectedResult = true)]
    [TestCase("4.1", ExpectedResult = false)]
    [TestCase("4.1.0-dev", ExpectedResult = false)]
    [TestCase("4.1.0-BE", ExpectedResult = false)]
    [TestCase("4.1.0-rc.1", ExpectedResult = false)]
    [TestCase("4.1.0+20260612", ExpectedResult = false)]
    [TestCase("abc", ExpectedResult = false)]
    public bool IsValidVersion(string version)
    {
        return BuildRules.IsValidVersion(version);
    }

    /// <summary>
    /// A release packs under the plain SPT version.
    /// </summary>
    [Test]
    public void NuGetVersionReleaseIsPlain()
    {
        Assert.That(BuildRules.NuGetVersion(SptBuildType.Release, "4.1.0", BuildTime), Is.EqualTo("4.1.0"));
    }

    /// <summary>
    /// Every non-release build type packs under a pre-release version stamped with the UTC build time.
    /// </summary>
    [TestCase(SptBuildType.Debug)]
    [TestCase(SptBuildType.BleedingEdge)]
    [TestCase(SptBuildType.BleedingEdgeMods)]
    public void NuGetVersionNonReleaseIsTimestampedPreRelease(SptBuildType type)
    {
        Assert.That(BuildRules.NuGetVersion(type, "4.1.0", BuildTime), Is.EqualTo("4.1.0-pre.202606121430"));
    }

    /// <summary>
    /// A release archive name omits the build type and trailing suffix.
    /// </summary>
    [Test]
    public void ReleaseArchiveNameHasNoTypeOrSuffix()
    {
        var name = BuildRules.ArchiveBaseName(SptBuildType.Release, "4.1.0", "40743", "abc1234", "4.1.0", BuildTime);
        Assert.That(name, Is.EqualTo("SPT-4.1.0-40743-abc1234"));
    }

    /// <summary>
    /// A tag with two dashes contributes its trailing segment to the archive name.
    /// </summary>
    [Test]
    public void TwoDashTagContributesItsTrailingSegment()
    {
        var name = BuildRules.ArchiveBaseName(
            SptBuildType.BleedingEdge,
            "4.1.0",
            "40743",
            "abc1234",
            "4.1.0-BE-CustomText",
            BuildTime
        );
        Assert.That(name, Is.EqualTo("SPT-BLEEDINGEDGE-4.1.0-40743-abc1234-CUSTOMTEXT"));
    }

    /// <summary>
    /// A single-dash tag falls back to the build date in the archive name.
    /// </summary>
    [Test]
    public void SingleDashTagFallsBackToBuildDate()
    {
        var name = BuildRules.ArchiveBaseName(
            SptBuildType.BleedingEdge,
            "4.1.0",
            "40743",
            "abc1234",
            "4.1.0-BE",
            BuildTime
        );
        Assert.That(name, Is.EqualTo("SPT-BLEEDINGEDGE-4.1.0-40743-abc1234-20260612"));
    }

    /// <summary>
    /// A dev build with no tag uses the build date in the archive name.
    /// </summary>
    [Test]
    public void DevBuildWithoutTagUsesBuildDate()
    {
        var name = BuildRules.ArchiveBaseName(SptBuildType.Debug, "4.1.0", "40743", "abc1234", tag: null, BuildTime);
        Assert.That(name, Is.EqualTo("SPT-DEBUG-4.1.0-40743-abc1234-20260612"));
    }
}
