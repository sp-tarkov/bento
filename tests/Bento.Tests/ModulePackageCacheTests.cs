using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Tests for ModulePackageCache.VerifyAsync across its possible package sources.
/// </summary>
[TestFixture]
public class ModulePackageCacheTests
{
    /// <summary>
    /// A non-empty local package directory verifies as available.
    /// </summary>
    [Test]
    public async Task LocalDirectoryWithFilesIsOk()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bento-pkg-{Path.GetRandomFileName()}");
        try
        {
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "FakeUnity.dll"), "fake dll");
            var status = await ModulePackageCache.VerifyAsync(dir, "40743");
            Assert.Multiple(() =>
            {
                Assert.That(status.Ok, Is.True);
                Assert.That(status.Markup, Does.Contain("found"));
            });
        }
        finally
        {
            Fs.DeleteDirectory(dir);
        }
    }

    /// <summary>
    /// A missing local package directory verifies as unavailable.
    /// </summary>
    [Test]
    public async Task LocalDirectoryThatIsMissingIsNotOk()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bento-nope-{Path.GetRandomFileName()}");
        var status = await ModulePackageCache.VerifyAsync(dir, "40743");
        Assert.Multiple(() =>
        {
            Assert.That(status.Ok, Is.False);
            Assert.That(status.Markup, Does.Contain("missing"));
        });
    }

    /// <summary>
    /// An empty local package directory verifies as unavailable.
    /// </summary>
    [Test]
    public async Task LocalDirectoryThatIsEmptyIsNotOk()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bento-empty-{Path.GetRandomFileName()}");
        try
        {
            Directory.CreateDirectory(dir);
            var status = await ModulePackageCache.VerifyAsync(dir, "40743");
            Assert.Multiple(() =>
            {
                Assert.That(status.Ok, Is.False);
                Assert.That(status.Markup, Does.Contain("empty"));
            });
        }
        finally
        {
            Fs.DeleteDirectory(dir);
        }
    }

    /// <summary>
    /// With no local package, domain, or cache, the package verifies as unresolved.
    /// </summary>
    [Test]
    public async Task NoSourceAtAllIsNotOk()
    {
        var status = await ModulePackageCache.VerifyAsync(null, "00000-bento-test");
        Assert.Multiple(() =>
        {
            Assert.That(status.Ok, Is.False);
            Assert.That(status.Markup, Does.Contain("unresolved"));
        });
    }

    /// <summary>
    /// A URL source with no client version yet defers verification until after the clone.
    /// </summary>
    [Test]
    public async Task FreshModeWithUrlButNoClientVersionDefersVerification()
    {
        var status = await ModulePackageCache.VerifyAsync("https://example.test", null);
        Assert.Multiple(() =>
        {
            Assert.That(status.Ok, Is.True);
            Assert.That(status.Markup, Does.Contain("verified after clone"));
        });
    }
}
