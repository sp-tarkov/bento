namespace Bento;

/// <summary>
/// Locates and drives the 7z executable to extract and create .7z archives.
/// </summary>
public sealed class SevenZip
{
    public string ExePath { get; }

    private SevenZip(string exePath)
    {
        ExePath = exePath;
    }

    /// <summary>
    /// Finds a working 7z/7za on PATH (and the default Windows install locations), or returns null when none responds.
    /// </summary>
    public static async Task<SevenZip?> LocateAsync()
    {
        var candidates = new List<string> { "7z", "7za" };
        if (OperatingSystem.IsWindows())
        {
            candidates.Add(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe")
            );
            candidates.Add(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe")
            );
        }

        foreach (var candidate in candidates)
        {
            try
            {
                var (exitCode, _, _) = await ProcessRunner.CaptureAsync(candidate, ["i"]);
                if (exitCode == 0)
                {
                    return new SevenZip(candidate);
                }
            }
            catch (BentoException)
            {
                // Not found, try next.
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts an archive into destinationDir (overwriting), throwing on a non-zero exit.
    /// </summary>
    public async Task ExtractAsync(
        string archivePath,
        string destinationDir,
        Action<string> onLine,
        CancellationToken cancellationToken = default
    )
    {
        Directory.CreateDirectory(destinationDir);
        var exitCode = await ProcessRunner.RunAsync(
            ExePath,
            ["x", archivePath, $"-o{destinationDir}", "-aoa", "-y"],
            onLine: onLine,
            cancellationToken: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new BentoException($"7z failed to extract '{archivePath}' (exit code {exitCode}).");
        }
    }

    /// <summary>
    /// Compresses the contents of <paramref name="sourceDir"/> (not the dir itself) into the archive.
    /// </summary>
    public async Task CreateAsync(
        string sourceDir,
        string archivePath,
        bool maxCompression,
        Action<string> onLine,
        CancellationToken cancellationToken = default
    )
    {
        var level = maxCompression ? "-mx=9" : "-mx=1";
        var exitCode = await ProcessRunner.RunAsync(
            ExePath,
            ["a", level, "-m0=lzma2", archivePath, "."],
            workingDirectory: sourceDir,
            onLine: onLine,
            cancellationToken: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new BentoException($"7z failed to create '{archivePath}' (exit code {exitCode}).");
        }
    }
}
