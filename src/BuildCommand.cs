using Bento.Steps;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Bento;

/// <summary>
/// The single bento command. Detects the runtime environment, resolves all build inputs, runs the build stages, and
/// reports the build results. Throws BentoException.
/// </summary>
public sealed class BuildCommand : AsyncCommand<BuildSettings>
{
    /// <summary>Program entry point. Runs the build and converts a BentoException into exit code 1.</summary>
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        BuildSettings settings,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await RunAsync(settings, RuntimeEnv.Detect(settings.NoConfig));
        }
        catch (BentoException ex)
        {
            AnsiConsole.MarkupLine($"[red]error:[/] {Markup.Escape(ex.Message)}");
            if (ex.Hint is not null)
            {
                AnsiConsole.MarkupLine($"[yellow]hint:[/] {Markup.Escape(ex.Hint)}");
            }

            return 1;
        }
    }

    /// <summary>
    /// Loads config, runs preflight, resolves the build inputs, then executes the clone/build/assemble/package pipeline
    /// and cleans up on success.
    /// </summary>
    private static async Task<int> RunAsync(BuildSettings settings, RuntimeEnv env)
    {
        AnsiConsole.Write(new FigletText("Bento").Color(Color.Cyan));
        AnsiConsole.MarkupLine("[grey]builds and packages SPT[/]");
        AnsiConsole.WriteLine();

        var configPath = BentoConfig.DefaultPath;
        var loaded = env.UseConfig ? BentoConfig.Load(configPath) : null;
        var config = loaded ?? new BentoConfig();

        var tools = await Preflight.CheckToolsAsync(needLfs: settings.Fresh);
        if (!tools.DotnetVersion.StartsWith("10."))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]warning:[/] active .NET SDK is {Markup.Escape(tools.DotnetVersion)}, Bento requires .NET 10."
            );
        }

        if (env.Interactive && loaded is null)
        {
            InteractiveSetup.FirstRun(config, settings.Fresh);
        }

        BuildContext ctx;
        while (true)
        {
            ctx = await ResolveAndInspectAsync(settings, env, config, tools);
            var packageStatus = await ModulePackageCache.VerifyAsync(ctx.ModulePackage, ctx.ClientVersion);

            BuildSummary.Show(ctx, packageStatus);
            if (!env.Interactive)
            {
                break;
            }

            var choice = BuildSummary.Confirm();
            if (choice == SummaryChoice.Build)
            {
                break;
            }

            if (choice == SummaryChoice.Cancel)
            {
                AnsiConsole.MarkupLine("[grey]canceled, nothing was built[/]");
                return 0;
            }

            config.LastVersion = ctx.Version;
            config.LastBuildType = ctx.BuildTypeProperty;
            InteractiveSetup.Edit(config, settings.Fresh);
        }

        if (env.UseConfig)
        {
            config.LastVersion = ctx.Version;
            config.LastBuildType = ctx.BuildTypeProperty;
            config.Save(configPath);
        }

        Directory.CreateDirectory(ctx.OutputDir);
        AnsiConsole.WriteLine();
        using var log = new BuildLogger(ctx.LogDir, env.Interactive);

        try
        {
            if (ctx.Fresh)
            {
                await FreshCloneStep.CloneAllAsync(ctx, log);
            }

            // Resolves (and downloads, if needed) the module package before the build stages.
            var modulePackage = await ModulePackageCache.ResolveAsync(
                ctx.ModulePackage,
                ctx.ClientVersion!,
                message => log.Status(ModulesStep.Stage, message)
            );

            var (server, modulesBuild, launcherBuild) = await RunBuildStagesAsync(
                ctx,
                modulePackage,
                log,
                env.Interactive
            );

            AssembleStep.Run(ctx, server, modulesBuild, launcherBuild, log);
            AssembleStep.PrintReleaseTree(ctx.ReleaseDir);

            var packages = await NuGetStep.RunAsync(ctx, log);
            var result = await PackageStep.RunAsync(ctx, packages, log);

            Fs.DeleteDirectory(ctx.StagingDir);
            if (ctx.FreshTempRoot is not null)
            {
                Fs.DeleteDirectory(ctx.FreshTempRoot);
            }

            PrintSuccess(result);
            return 0;
        }
        catch
        {
            if (ctx.FreshTempRoot is not null && Directory.Exists(ctx.FreshTempRoot))
            {
                AnsiConsole.MarkupLine($"[grey]temp clones kept for debugging: {Markup.Escape(ctx.FreshTempRoot)}[/]");
            }

            if (Directory.Exists(ctx.StagingDir))
            {
                AnsiConsole.MarkupLine($"[grey]staging tree kept for debugging: {Markup.Escape(ctx.StagingDir)}[/]");
            }

            AnsiConsole.MarkupLine($"[grey]stage logs: {Markup.Escape(ctx.LogDir)}[/]");
            throw;
        }
    }

    /// <summary>
    /// Resolves every build input from flags, tag, config and prompts (in that order), inspects the repos (verifies the
    /// tag exists everywhere for fresh builds), and returns a populated BuildContext.
    /// </summary>
    private static async Task<BuildContext> ResolveAndInspectAsync(
        BuildSettings settings,
        RuntimeEnv env,
        BentoConfig config,
        ToolSet tools
    )
    {
        // Priority for version & build type: flag > tag > prompt.
        var buildType = settings.BuildType is not null ? BuildRules.ParseBuildType(settings.BuildType) : null;
        var version = settings.Version;

        if (settings.Tag is not null)
        {
            buildType ??= BuildRules.BuildTypeFromTag(settings.Tag);
            if (version is null)
            {
                version = BuildRules.VersionFromTag(settings.Tag);
                if (!BuildRules.IsValidVersion(version))
                {
                    throw new BentoException(
                        $"Could not derive a version from tag '{settings.Tag}'.",
                        "Pass --version explicitly."
                    );
                }
            }
        }

        if (version is null)
        {
            if (!env.Interactive)
            {
                throw new BentoException(
                    "No version provided.",
                    "Pass --version <x.y.z> or --tag <tag> (if the tag includes the version)."
                );
            }

            // Uses the valid saved version as the prompt default.
            var lastVersion = config.LastVersion is { } saved && BuildRules.IsValidVersion(saved) ? saved : null;
            version = InteractiveSetup.PromptVersion(lastVersion);
        }

        if (buildType is null)
        {
            if (!env.Interactive)
            {
                throw new BentoException(
                    "No build type provided.",
                    "Pass --build-type <type> or --tag <tag> (if the tag includes the build type)."
                );
            }

            buildType = InteractiveSetup.PromptBuildType();
        }

        var output = settings.Output ?? (env.UseConfig ? config.OutputDir : null);
        if (output is null)
        {
            if (!env.Interactive)
            {
                throw new BentoException("No output directory provided.", "Pass --output <dir>.");
            }

            output = Path.Combine(Directory.GetCurrentDirectory(), "dist");
        }
        output = Path.GetFullPath(output);
        if (env.UseConfig)
        {
            config.OutputDir ??= output;
        }

        // Module package source priority: flag > env var > config
        var modulePackage = settings.ModulePackage;
        if (string.IsNullOrWhiteSpace(modulePackage))
        {
            var fromEnv = Environment.GetEnvironmentVariable("MODULE_PACKAGE");
            modulePackage =
                !string.IsNullOrWhiteSpace(fromEnv) ? fromEnv
                : env.UseConfig ? config.ModulePackage
                : null;
        }

        // Repo paths (dev mode)
        string? serverPath = null;
        string? modulesPath = null;
        string? launcherPath = null;
        if (settings.Fresh)
        {
            if ((settings.ServerPath ?? settings.ModulesPath ?? settings.LauncherPath) is not null)
            {
                AnsiConsole.MarkupLine("[yellow]warning:[/] --fresh ignores --server/--modules/--launcher.");
            }
        }
        else
        {
            serverPath = ResolveRepoPath(
                settings.ServerPath,
                config.ServerPath,
                "server-csharp",
                "--server",
                RepoScanner.LooksLikeServer,
                FreshCloneStep.ServerUrl,
                env,
                path => config.ServerPath = path
            );
            modulesPath = ResolveRepoPath(
                settings.ModulesPath,
                config.ModulesPath,
                "modules",
                "--modules",
                RepoScanner.LooksLikeModules,
                FreshCloneStep.ModulesUrl,
                env,
                path => config.ModulesPath = path
            );
            launcherPath = ResolveRepoPath(
                settings.LauncherPath,
                config.LauncherPath,
                "launcher",
                "--launcher",
                RepoScanner.LooksLikeLauncher,
                FreshCloneStep.LauncherUrl,
                env,
                path => config.LauncherPath = path
            );
        }

        var ctx = new BuildContext
        {
            Fresh = settings.Fresh,
            BuildType = buildType.Value,
            Version = version,
            Tag = settings.Tag,
            OutputDir = output,
            ModulePackage = modulePackage,
            BuildTimeUtc = DateTime.UtcNow,
            Tools = tools,
            DevServerPath = serverPath,
            DevModulesPath = modulesPath,
            DevLauncherPath = launcherPath,
        };

        if (ctx.Fresh)
        {
            // A fresh build has no client version (and thus no cache key) before the clone. A local directory works
            // immediately and a URL is verified after the clone; only a totally absent source is a problem up front.
            if (ctx.ModulePackage is null)
            {
                if (env.Interactive)
                {
                    config.ModulePackage = InteractiveSetup.PromptModulePackage(config.ModulePackage);
                    ctx.ModulePackage = config.ModulePackage;
                }

                if (ctx.ModulePackage is null)
                {
                    throw new BentoException(
                        "Fresh builds need a module package source.",
                        "Pass --module-package <url|dir> or set MODULE_PACKAGE."
                    );
                }
            }

            await FreshCloneStep.EnsureTagExistsEverywhereAsync(
                ctx.Tag!,
                message => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]")
            );
        }
        else
        {
            ctx.Server = await Preflight.InspectRepoAsync("server", serverPath!, FreshCloneStep.ServerUrl);
            ctx.Modules = await Preflight.InspectRepoAsync("modules", modulesPath!, FreshCloneStep.ModulesUrl);
            ctx.Launcher = await Preflight.InspectRepoAsync("launcher", launcherPath!, FreshCloneStep.LauncherUrl);
            Preflight.CheckServerLfs(ctx.Server.Path);
            ctx.ClientVersion = Preflight.ReadClientVersion(ctx.Server.Path);

            if (ctx.ModulePackage is not null || File.Exists(ModulePackageCache.CachePathFor(ctx.ClientVersion)))
            {
                return ctx;
            }

            if (env.Interactive)
            {
                config.ModulePackage = InteractiveSetup.PromptModulePackage(config.ModulePackage);
                ctx.ModulePackage = config.ModulePackage;
            }

            if (ctx.ModulePackage is null)
            {
                throw new BentoException(
                    "No way to obtain the module package.",
                    $"Pass --module-package <url|dir>, set MODULE_PACKAGE, or pre-seed the cache at {ModulePackageCache.CachePathFor(ctx.ClientVersion)}."
                );
            }
        }

        return ctx;
    }

    /// <summary>
    /// Resolves one repo path from flag, config or prompt, saves a newly-prompted path back to config, and warns when
    /// the resolved path lacks the repo's marker files.
    /// </summary>
    private static string ResolveRepoPath(
        string? flagValue,
        string? configValue,
        string displayName,
        string flagName,
        Func<string, bool> marker,
        string cloneUrl,
        RuntimeEnv env,
        Action<string> saveToConfig
    )
    {
        var path = flagValue ?? (env.UseConfig ? configValue : null);
        if (path is null)
        {
            if (!env.Interactive)
            {
                throw new BentoException($"No path for the {displayName} repository.", $"Pass {flagName} <dir>.");
            }

            path = InteractiveSetup.PromptRepoPath(displayName, current: null, marker, cloneUrl);
            saveToConfig(path);
        }

        path = Path.GetFullPath(path);
        if (!marker(path))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]warning:[/] {Markup.Escape(path)} doesn't look like the {displayName} repo."
            );
        }

        return path;
    }

    /// <summary>
    /// Runs the server, modules and launcher stages concurrently (under a live progress display when interactive), then
    /// fetches their results, throwing if any failed.
    /// </summary>
    private static async Task<(ServerArtifacts Server, string ModulesBuild, string LauncherBuild)> RunBuildStagesAsync(
        BuildContext ctx,
        ResolvedModulePackage modulePackage,
        BuildLogger log,
        bool interactive
    )
    {
        Task<ServerArtifacts> serverTask = null!;
        Task<string> modulesTask = null!;
        Task<string> launcherTask = null!;

        void StartStages()
        {
            serverTask = ServerStep.RunAsync(ctx, log);
            modulesTask = ModulesStep.RunAsync(ctx, modulePackage, log);
            launcherTask = LauncherStep.RunAsync(ctx, log);
        }

        if (interactive)
        {
            await AnsiConsole
                .Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn { Alignment = Justify.Left },
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                )
                .StartAsync(async progress =>
                {
                    var tasks = new Dictionary<string, ProgressTask>
                    {
                        [ServerStep.Stage] = progress.AddTask(StageLabel(ServerStep.Stage, "waiting...")),
                        [ModulesStep.Stage] = progress.AddTask(StageLabel(ModulesStep.Stage, "waiting...")),
                        [LauncherStep.Stage] = progress.AddTask(StageLabel(LauncherStep.Stage, "waiting...")),
                    };

                    log.StatusSink = (stage, message) =>
                    {
                        if (tasks.TryGetValue(stage, out var task))
                        {
                            task.Description = StageLabel(stage, message);
                            // Eases the bar toward 90% on each milestone, leaving headroom until the stage settles.
                            task.Value += (90 - task.Value) * 0.4;
                        }
                    };

                    try
                    {
                        StartStages();
                        await Task.WhenAll(
                            TrackStageAsync(ServerStep.Stage, serverTask, tasks),
                            TrackStageAsync(ModulesStep.Stage, modulesTask, tasks),
                            TrackStageAsync(LauncherStep.Stage, launcherTask, tasks)
                        );
                    }
                    finally
                    {
                        log.StatusSink = null;
                    }
                });
        }
        else
        {
            StartStages();
            try
            {
                await Task.WhenAll(serverTask, modulesTask, launcherTask);
            }
            catch
            {
                // Swallow failures.
            }
        }

        var failures = new List<string>();
        var server = Harvest(serverTask, ServerStep.Stage, failures, log);
        var modulesBuild = Harvest(modulesTask, ModulesStep.Stage, failures, log);
        var launcherBuild = Harvest(launcherTask, LauncherStep.Stage, failures, log);
        if (failures.Count > 0)
        {
            throw new BentoException($"Build stage(s) failed: {string.Join(", ", failures)}.");
        }

        return (server!, modulesBuild!, launcherBuild!);
    }

    private const int StageLabelWidth = 44;

    /// <summary>
    /// Awaits a stage, then settles its progress bar. A successful stage fills to 100% and its timer stops. A failed
    /// stage's bar freezes where it stopped.
    /// </summary>
    private static async Task TrackStageAsync(string stage, Task work, IReadOnlyDictionary<string, ProgressTask> tasks)
    {
        try
        {
            await work;
        }
        catch
        {
            // Swallow failures.
        }

        var task = tasks[stage];
        if (work.IsCompletedSuccessfully)
        {
            task.Value = task.MaxValue;
        }

        task.StopTask();
    }

    /// <summary>
    /// Formats a stage's progress line to a fixed width so the bar columns don't jitter.
    /// </summary>
    private static string StageLabel(string stage, string message)
    {
        var text = $"{stage}: {message}";
        text = text.Length > StageLabelWidth ? text[..(StageLabelWidth - 1)] + "..." : text.PadRight(StageLabelWidth);
        return Markup.Escape(text);
    }

    /// <summary>
    /// Returns a completed stage's result, or records the stage as failed and prints its error and returns null.
    /// </summary>
    private static T? Harvest<T>(Task<T> task, string stage, List<string> failures, BuildLogger log)
        where T : class
    {
        if (task.IsCompletedSuccessfully)
        {
            return task.Result;
        }

        failures.Add(stage);
        var exception = task.Exception?.GetBaseException();
        AnsiConsole.MarkupLine($"[red]{stage} failed:[/] {Markup.Escape(exception?.Message ?? "unknown error")}");
        foreach (var line in log.Tail(stage, 25))
        {
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(line)}[/]");
        }
        AnsiConsole.MarkupLine($"  [grey]full log: {Markup.Escape(log.LogPath(stage))}[/]");
        return null;
    }

    /// <summary>
    /// Prints the success panel (archive path, size, MD5, manifest).
    /// </summary>
    private static void PrintSuccess(PackageResult result)
    {
        AnsiConsole.WriteLine();
        var grid = new Grid();
        grid.AddColumn(new GridColumn().RightAligned());
        grid.AddColumn();
        grid.AddRow("[green]archive:[/]", Markup.Escape(result.ArchivePath));
        grid.AddRow("[green]size:[/]", Fs.FormatSize(result.SizeBytes));
        grid.AddRow("[green]md5/b64:[/]", Markup.Escape(result.Md5Base64));
        grid.AddRow("[green]manifest:[/]", Markup.Escape(result.ManifestPath));
        AnsiConsole.Write(new Panel(grid).Header("[bold green]build complete[/]"));
        AnsiConsole.WriteLine();
    }
}
