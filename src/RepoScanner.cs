namespace Bento;

/// <summary>
/// Filesystem guesses for repo locations, used as interactive prompt defaults. Looks for marker files near the working
/// directory, where the three repos typically sit as siblings under the SPT workspace dir with Bento alongside them.
/// </summary>
public static class RepoScanner
{
    /// <summary>
    /// Reports whether dir holds the server-csharp repo, by its solution or core library.
    /// </summary>
    public static bool LooksLikeServer(string dir)
    {
        return File.Exists(Path.Combine(dir, "server-csharp.slnx"))
            || Directory.Exists(Path.Combine(dir, "Libraries", "SPTarkov.Server.Core"));
    }

    /// <summary>
    /// Reports whether dir holds the modules repo, by its project/Shared directory.
    /// </summary>
    public static bool LooksLikeModules(string dir)
    {
        return Directory.Exists(Path.Combine(dir, "project", "Shared"));
    }

    /// <summary>
    /// Reports whether dir holds the launcher repo, by its project/SPTarkov.Launcher directory.
    /// </summary>
    public static bool LooksLikeLauncher(string dir)
    {
        return Directory.Exists(Path.Combine(dir, "project", "SPTarkov.Launcher"));
    }

    /// <summary>
    /// Scans the search roots for a repo matching the marker, returning the first hit or null.
    /// </summary>
    public static string? Guess(Func<string, bool> marker, string preferredName)
    {
        // A directory with the conventional name wins, otherwise any marker match.
        foreach (var root in SearchRoots())
        {
            var preferred = Path.Combine(root, preferredName);
            if (Directory.Exists(preferred) && marker(preferred))
            {
                return preferred;
            }
        }

        foreach (var root in SearchRoots())
        {
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    if (marker(directory))
                    {
                        return directory;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip unreadable roots.
            }
        }

        return null;
    }

    /// <summary>
    /// Yields the working directory and up to two of its ancestors as scan roots.
    /// </summary>
    private static IEnumerable<string> SearchRoots()
    {
        var current = Directory.GetCurrentDirectory();
        yield return current;
        var parent = Directory.GetParent(current)?.FullName;
        if (parent is null)
        {
            yield break;
        }

        yield return parent;
        if (Directory.GetParent(parent)?.FullName is { } grandParent)
        {
            yield return grandParent;
        }
    }
}
