using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Round-trip and error-handling tests for loading and saving BentoConfig.
/// </summary>
[TestFixture]
public class BentoConfigTests
{
    /// <summary>
    /// Saving then loading a config preserves every value.
    /// </summary>
    [Test]
    public void RoundTripsAllValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bento-test-{Path.GetRandomFileName()}", "config.json");
        try
        {
            var config = new BentoConfig
            {
                ServerPath = @"C:\SPT\server-csharp",
                ModulesPath = @"C:\SPT\modules",
                LauncherPath = @"C:\SPT\launcher",
                ModulePackage = "https://example.test",
                OutputDir = @"C:\SPT\bento\dist",
                LastVersion = "4.1.0",
                LastBuildType = "DEBUG",
            };
            config.Save(path);

            var loaded = BentoConfig.Load(path);
            Assert.That(loaded, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(loaded!.ServerPath, Is.EqualTo(config.ServerPath));
                Assert.That(loaded.ModulesPath, Is.EqualTo(config.ModulesPath));
                Assert.That(loaded.LauncherPath, Is.EqualTo(config.LauncherPath));
                Assert.That(loaded.ModulePackage, Is.EqualTo(config.ModulePackage));
                Assert.That(loaded.OutputDir, Is.EqualTo(config.OutputDir));
                Assert.That(loaded.LastVersion, Is.EqualTo(config.LastVersion));
                Assert.That(loaded.LastBuildType, Is.EqualTo(config.LastBuildType));
            });
        }
        finally
        {
            Fs.DeleteDirectory(Path.GetDirectoryName(path)!);
        }
    }

    /// <summary>
    /// Load returns null when the config file does not exist.
    /// </summary>
    [Test]
    public void LoadReturnsNullWhenMissing()
    {
        Assert.That(BentoConfig.Load(Path.Combine(Path.GetTempPath(), "nope", "config.json")), Is.Null);
    }

    /// <summary>
    /// Load throws a BentoException when the file is not valid JSON.
    /// </summary>
    [Test]
    public void LoadThrowsBentoExceptionOnGarbage()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{not json");
            Assert.Throws<BentoException>(() => BentoConfig.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
