using System.Text.Json;
using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Tests for how the build manifest serializes to JSON, the contract workflows parse.
/// </summary>
[TestFixture]
public class ManifestTests
{
    /// <summary>
    /// The NuGet section serializes under the lowercase "nuget" key the workflow reads, not the camelCase "nuGet"
    /// the naming policy would otherwise produce.
    /// </summary>
    [Test]
    public void NuGetSectionUsesLowercaseKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bento-manifest-{Guid.NewGuid():N}.json");
        try
        {
            NewManifest().Write(path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            Assert.Multiple(() =>
            {
                Assert.That(doc.RootElement.TryGetProperty("nuget", out _), Is.True, "expected a \"nuget\" key");
                Assert.That(doc.RootElement.TryGetProperty("nuGet", out _), Is.False, "did not expect a \"nuGet\" key");
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Manifest NewManifest()
    {
        return new Manifest(
            BentoVersion: "0.1.0",
            BuiltAt: "2026-06-20T03:19:45Z",
            Mode: "fresh",
            BuildType: "BLEEDINGEDGE",
            BuildConfig: "Debug",
            SptVersion: "4.1.0",
            ClientVersion: "40743",
            Tag: "4.1.0-BE-WorkflowTest",
            Archive: new ManifestArchive("SPT.7z", 1, "1 B", "aaaa"),
            NuGet: new ManifestNuGet("4.1.0-pre.202606200319", ["SPTarkov.Common", "SPTarkov.Server.Core"]),
            Repos: new Dictionary<string, ManifestRepo> { ["server"] = new(null, "8b066d0", false) }
        );
    }
}
