namespace Bento.Steps;

/// <summary>
/// Builds the modules project. Extracts the module package into Shared/Managed then runs the build.
/// </summary>
public static class ModulesStep
{
    public const string Stage = "modules";

    /// <summary>
    /// Copies the module package into Shared/Managed, builds modules, and returns the Build directory.
    /// </summary>
    public static async Task<string> RunAsync(
        BuildContext ctx,
        ResolvedModulePackage modulePackage,
        BuildLogger log,
        CancellationToken cancellationToken = default
    )
    {
        var repo = ctx.Modules!;
        var projectDir = Path.Combine(repo.Path, "project");
        if (!Directory.Exists(projectDir))
        {
            throw new StageFailedException(Stage, $"The modules repo has no project/ directory: {projectDir}");
        }

        var managedDir = Path.Combine(projectDir, "Shared", "Managed");
        if (modulePackage.IsDirectory)
        {
            // Local directory source. The module files are already extracted on disk.
            log.Status(Stage, "copying local module files into Shared/Managed...");
            Fs.CopyDirectory(modulePackage.Path, managedDir);
        }
        else
        {
            // Extracts the cached archive straight into Shared/Managed, leaving no archive file behind in the tree.
            log.Status(Stage, "extracting module package into Shared/Managed...");
            await ctx.Tools.SevenZip.ExtractAsync(
                modulePackage.Path,
                managedDir,
                line => log.Line(Stage, line),
                cancellationToken
            );
        }

        log.Status(Stage, $"building modules (Release, SptVersion={ctx.Version})...");
        var exitCode = await ProcessRunner.RunAsync(
            "dotnet",
            ["build", "-c", "Release", $"-p:SptVersion={ctx.Version}"],
            projectDir,
            onLine: line => log.Line(Stage, line),
            cancellationToken: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new StageFailedException(Stage, $"Modules build failed (exit {exitCode}).");
        }

        var buildDir = Path.Combine(projectDir, "Build");
        if (!Directory.Exists(buildDir) || !Directory.EnumerateFileSystemEntries(buildDir).Any())
        {
            throw new StageFailedException(Stage, $"Modules build produced nothing at {buildDir}.");
        }

        return buildDir;
    }
}
