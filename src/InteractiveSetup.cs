using Bento.Steps;
using Spectre.Console;

namespace Bento;

/// <summary>
/// Handles the first-run setup, Edit flow from the build summary, and the per-run version/build-type questions.
/// </summary>
public static class InteractiveSetup
{
    /// <summary>
    /// Shows the setup panel, walks the Edit prompts, and saves the new config file.
    /// </summary>
    public static void FirstRun(BentoConfig config, bool fresh)
    {
        AnsiConsole.Write(
            new Panel(
                new Markup(
                    "No Bento config found. Let's set one up.\nAnswers are saved to [bold]{Markup.Escape(BentoConfig.DefaultPath)}[/]."
                )
            ).Header("[bold]setup[/]")
        );
        Edit(config, fresh);
        config.Save(BentoConfig.DefaultPath);
    }

    /// <summary>
    /// Walks the saved settings. Current values become the prompt defaults.
    /// </summary>
    public static void Edit(BentoConfig config, bool fresh)
    {
        if (!fresh)
        {
            config.ServerPath = PromptRepoPath(
                "server-csharp",
                config.ServerPath,
                RepoScanner.LooksLikeServer,
                FreshCloneStep.ServerUrl
            );
            config.ModulesPath = PromptRepoPath(
                "modules",
                config.ModulesPath,
                RepoScanner.LooksLikeModules,
                FreshCloneStep.ModulesUrl
            );
            config.LauncherPath = PromptRepoPath(
                "launcher",
                config.LauncherPath,
                RepoScanner.LooksLikeLauncher,
                FreshCloneStep.LauncherUrl
            );
        }

        config.ModulePackage = PromptModulePackage(config.ModulePackage);
        config.OutputDir = AnsiConsole
            .Prompt(
                new TextPrompt<string>("Output directory:").DefaultValue(
                    config.OutputDir ?? Path.Combine(Directory.GetCurrentDirectory(), "dist")
                )
            )
            .Trim();
    }

    /// <summary>
    /// Prompts for a repo path (defaulting to a scanned guess), re-asking until the path exists and either matches the
    /// repo's marker files or is explicitly confirmed.
    /// </summary>
    public static string PromptRepoPath(string name, string? current, Func<string, bool> marker, string cloneUrl)
    {
        var fallback = current ?? RepoScanner.Guess(marker, name);
        while (true)
        {
            var prompt = new TextPrompt<string>($"Full path to the [bold]{name}[/] repo:").Validate(value =>
                Directory.Exists(value.Trim())
                    ? ValidationResult.Success()
                    : ValidationResult.Error(
                        $"[red]Directory does not exist (need it? git clone {Markup.Escape(cloneUrl)})[/]"
                    )
            );
            if (fallback is not null)
            {
                prompt.DefaultValue(fallback);
            }

            var path = Path.GetFullPath(AnsiConsole.Prompt(prompt).Trim());
            if (
                marker(path)
                || AnsiConsole.Confirm($"That doesn't look like the {name} repo... Use it anyway?", defaultValue: false)
            )
            {
                return path;
            }

            fallback = path;
        }
    }

    /// <summary>
    /// Prompts for the module package source, accepting either an http(s) base URL or an existing local directory; a
    /// URL is returned without its trailing slash.
    /// </summary>
    public static string PromptModulePackage(string? current)
    {
        var prompt = new TextPrompt<string>("Module package path:").Validate(value =>
        {
            var trimmed = value.Trim();
            return ModulePackageSource.LooksLikeUrl(trimmed) || Directory.Exists(trimmed)
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Required: a URL or an existing local directory.[/]");
        });
        if (!string.IsNullOrWhiteSpace(current))
        {
            prompt.DefaultValue(current);
        }

        var result = AnsiConsole.Prompt(prompt).Trim();
        return ModulePackageSource.LooksLikeUrl(result) ? result.TrimEnd('/') : result;
    }

    /// <summary>
    /// Prompts for the numeric X.Y.Z build version, re-asking until it validates.
    /// </summary>
    public static string PromptVersion(string? lastVersion)
    {
        var prompt = new TextPrompt<string>("SPT version for this build:").Validate(value =>
            BuildRules.IsValidVersion(value.Trim())
                ? ValidationResult.Success()
                : ValidationResult.Error(
                    "[red]Version must look like 4.1.0 (numbers only, it is stamped into AssemblyVersion)[/]"
                )
        );
        if (lastVersion is not null)
        {
            prompt.DefaultValue(lastVersion);
        }

        return AnsiConsole.Prompt(prompt).Trim();
    }

    /// <summary>
    /// Prompts for the build type with a selection list.
    /// </summary>
    public static SptBuildType PromptBuildType()
    {
        SptBuildType[] all =
        [
            SptBuildType.BleedingEdge,
            SptBuildType.BleedingEdgeMods,
            SptBuildType.Debug,
            SptBuildType.Release,
        ];

        return AnsiConsole.Prompt(
            new SelectionPrompt<SptBuildType>()
                .Title("Build type:")
                .UseConverter(type => type.ToPropertyValue())
                .AddChoices(all)
        );
    }
}
