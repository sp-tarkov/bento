using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Validation tests for the BuildSettings command options.
/// </summary>
[TestFixture]
public class BuildSettingsTests
{
    /// <summary>
    /// --fresh without --tag fails validation.
    /// </summary>
    [Test]
    public void FreshRequiresTag()
    {
        var settings = new BuildSettings { Fresh = true };
        Assert.That(settings.Validate().Successful, Is.False);
    }

    /// <summary>
    /// --module-package accepts an http(s) base URL.
    /// </summary>
    [Test]
    public void ModulePackageAcceptsUrls()
    {
        var settings = new BuildSettings { ModulePackage = "https://example.test/packages" };
        Assert.That(settings.Validate().Successful, Is.True);
    }

    /// <summary>
    /// --module-package accepts a local directory path.
    /// </summary>
    [Test]
    public void ModulePackageAcceptsLocalPaths()
    {
        var settings = new BuildSettings { ModulePackage = @"C:\packages\managed" };
        Assert.That(settings.Validate().Successful, Is.True);
    }

    /// <summary>
    /// --module-package rejects a non-http(s) URL scheme.
    /// </summary>
    [Test]
    public void ModulePackageRejectsOtherSchemes()
    {
        var settings = new BuildSettings { ModulePackage = "ftp://example.test/packages" };
        Assert.That(settings.Validate().Successful, Is.False);
    }

    /// <summary>
    /// --version accepts only strictly numeric X.Y.Z.
    /// </summary>
    [Test]
    public void VersionMustBeNumericXyz()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                new BuildSettings { Version = "4.1" }
                    .Validate()
                    .Successful,
                Is.False
            );
            Assert.That(
                new BuildSettings { Version = "4.1.0-dev" }
                    .Validate()
                    .Successful,
                Is.False
            );
            Assert.That(
                new BuildSettings { Version = "4.1.0" }
                    .Validate()
                    .Successful,
                Is.True
            );
        });
    }

    /// <summary>
    /// --build-type accepts only recognized values.
    /// </summary>
    [Test]
    public void BuildTypeMustBeKnown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                new BuildSettings { BuildType = "nope" }
                    .Validate()
                    .Successful,
                Is.False
            );
            Assert.That(
                new BuildSettings { BuildType = "release" }
                    .Validate()
                    .Successful,
                Is.True
            );
        });
    }
}
