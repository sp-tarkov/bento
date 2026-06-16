namespace Bento;

/// <summary>
/// Filesystem helpers: recursive copy with exclusions, robust recursive delete, and sizing.
/// </summary>
public static class Fs
{
    /// <summary>
    /// Recursive copy with overwrite; <paramref name="exclude"/> filters by relative path.
    /// </summary>
    public static void CopyDirectory(string sourceDir, string destinationDir, Func<string, bool>? exclude = null)
    {
        var source = new DirectoryInfo(sourceDir);
        if (!source.Exists)
        {
            throw new BentoException($"Expected directory does not exist: {sourceDir}");
        }

        Directory.CreateDirectory(destinationDir);
        foreach (var file in source.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file.FullName);
            if (exclude?.Invoke(relative) == true)
            {
                continue;
            }

            var target = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            file.CopyTo(target, overwrite: true);
        }
    }

    /// <summary>
    /// Recursively deletes a directory, retrying once after clearing read-only attributes.
    /// </summary>
    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Clears the read-only attribute that git object stores and some shipped DLLs carry (which blocks recursive
            // delete on Windows), then retries.
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>
    /// Total byte size of every file under a directory, recursively.
    /// </summary>
    public static long DirectorySize(string path)
    {
        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
    }

    /// <summary>
    /// Formats a byte count as a megabyte string (e.g. 12.34 MB).
    /// </summary>
    public static string FormatSize(long bytes)
    {
        return $"{bytes / 1024d / 1024d:0.00} MB";
    }
}
