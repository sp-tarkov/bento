using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Tests for how ModulePackageSource classifies a raw --module-package value.
/// </summary>
[TestFixture]
public class ModulePackageSourceTests
{
    /// <summary>
    /// Null, empty, and whitespace values parse to no source.
    /// </summary>
    [Test]
    public void BlankValuesParseToNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModulePackageSource.Parse(null), Is.Null);
            Assert.That(ModulePackageSource.Parse(""), Is.Null);
            Assert.That(ModulePackageSource.Parse("   "), Is.Null);
        });
    }

    /// <summary>
    /// An http(s) value (any case) is a URL with its trailing slash trimmed.
    /// </summary>
    [Test]
    public void HttpValuesParseAsUrls()
    {
        var lower = ModulePackageSource.Parse("https://example.test/packages/");
        var upper = ModulePackageSource.Parse("HTTP://example.test/packages");
        Assert.Multiple(() =>
        {
            Assert.That(lower!.Kind, Is.EqualTo(ModulePackageKind.Url));
            Assert.That(lower.Value, Is.EqualTo("https://example.test/packages"));
            Assert.That(upper!.IsUrl, Is.True);
        });
    }

    /// <summary>
    /// A non-URL value is a directory, normalized to a full path.
    /// </summary>
    [Test]
    public void PlainPathParsesAsDirectory()
    {
        var source = ModulePackageSource.Parse("some/managed/dir");
        Assert.Multiple(() =>
        {
            Assert.That(source!.Kind, Is.EqualTo(ModulePackageKind.Directory));
            Assert.That(source.Value, Is.EqualTo(Path.GetFullPath("some/managed/dir")));
        });
    }
}
