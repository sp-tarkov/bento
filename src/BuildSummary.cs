using Bento.Steps;
using Spectre.Console;

namespace Bento;

/// <summary>
/// The user's response to the pre-build confirmation.
/// </summary>
public enum SummaryChoice
{
    Build,
    Edit,
    Cancel,
}

/// <summary>
/// The pre-build summary. In interactive runs it doubles as the confirmation step (build/edit/cancel) and in flagged
/// runs it just prints before the build starts.
/// </summary>
public static class BuildSummary
{
    /// <summary>
    /// Renders the summary table (mode, versions, repos, package, output, compression) followed by any warnings.
    /// </summary>
    public static void Show(BuildContext ctx, ModulePackageStatus packageStatus)
    {
        AnsiConsole.WriteLine();
        var table = new Table().Border(TableBorder.None).HideHeaders();
        table.AddColumn(new TableColumn("key").RightAligned());
        table.AddColumn("value");

        table.AddRow("mode", ctx.Fresh ? "fresh (clones from GitHub)" : "dev (local working copies)");
        if (ctx.Tag is not null)
        {
            table.AddRow("tag", Markup.Escape(ctx.Tag));
        }

        table.AddRow("build type", Markup.Escape(ctx.BuildTypeProperty));
        table.AddRow("SPT version", Markup.Escape(ctx.Version));
        table.AddRow(
            "client version",
            ctx.ClientVersion is null ? "[grey]resolved after clone[/]" : Markup.Escape(ctx.ClientVersion)
        );

        if (ctx.Fresh)
        {
            table.AddRow("server", FreshRow(FreshCloneStep.ServerUrl, ctx.Tag!));
            table.AddRow("modules", FreshRow(FreshCloneStep.ModulesUrl, ctx.Tag!));
            table.AddRow("launcher", FreshRow(FreshCloneStep.LauncherUrl, ctx.Tag!));
        }
        else
        {
            table.AddRow("server", RepoRow(ctx.Server!));
            table.AddRow("modules", RepoRow(ctx.Modules!));
            table.AddRow("launcher", RepoRow(ctx.Launcher!));
        }

        table.AddRow("module package", packageStatus.Markup);
        table.AddRow("output", Markup.Escape(ctx.OutputDir));
        table.AddRow("compression", ctx.MaxCompression ? "max (-mx=9)" : "fast (-mx=1, dev build)");

        AnsiConsole.Write(new Panel(table).Header("[bold]build summary[/]"));

        if (!packageStatus.Ok)
        {
            AnsiConsole.MarkupLine(
                "[yellow]warning:[/] the module package is not available, so the modules stage will fail."
            );
        }

        foreach (var repo in new[] { ctx.Server, ctx.Modules, ctx.Launcher })
        {
            if (repo is { Dirty: true })
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]warning:[/] {repo.Name} has uncommitted changes, so the output won't match commit {repo.Commit}."
                );
            }
        }

        if (OperatingSystem.IsWindows())
        {
            AnsiConsole.MarkupLine(
                "[yellow]warning:[/] building on Windows, so the archive will not set the exec bit on the Linux executables."
            );
        }
    }

    /// <summary>
    /// Asks the user whether to build, edit the inputs, or cancel.
    /// </summary>
    public static SummaryChoice Confirm()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<SummaryChoice>()
                .Title("Proceed with this build?")
                .AddChoices(SummaryChoice.Build, SummaryChoice.Edit, SummaryChoice.Cancel)
        );
    }

    /// <summary>
    /// Formats a dev-mode repo row.
    /// </summary>
    private static string RepoRow(RepoInfo repo)
    {
        var branch = string.IsNullOrEmpty(repo.Branch) ? string.Empty : $" ({Markup.Escape(repo.Branch)})";
        var dirty = repo.Dirty ? " [yellow](dirty)[/]" : string.Empty;
        return $"{Markup.Escape(repo.Path)} [grey]@ {repo.Commit}{branch}[/]{dirty}";
    }

    /// <summary>
    /// Formats a fresh-mode repo row.
    /// </summary>
    private static string FreshRow(string url, string tag)
    {
        return $"{Markup.Escape(url)} [grey]@ {Markup.Escape(tag)}[/]";
    }
}
