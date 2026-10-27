using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Setup;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class WorkspaceExportCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var root = OrchestratorOptions.Load().WorkspaceRoot;
        var manifest = WorkspaceBootstrap.Export(root);
        WorkspaceBootstrap.Save(manifest, root);
        AnsiConsole.MarkupLine($"Wrote {manifest.Repos.Count} repos to {Markup.Escape(Path.Combine(root, WorkspaceManifest.RelativePath))}.");
        AnsiConsole.MarkupLine("[grey]Commit and push it in the workspace root repo so a new machine can build from it.[/]");
        return 0;
    }
}

internal sealed class WorkspaceBuildCommand : AsyncCommand<WorkspaceBuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<FOLDER>")]
        [Description("An empty folder (or a partial workspace) to clone into.")]
        public string Folder { get; init; } = "";

        [CommandOption("--root <URL>")]
        [Description("The workspace root repo to start from.")]
        public string Root { get; init; } = WorkspaceBootstrap.DefaultRootRemote;

        [CommandOption("--install-apps")]
        [Description("Then run every app's run.ps1 (no -Launch).")]
        public bool InstallApps { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var folder = Path.GetFullPath(settings.Folder);
            var r = await WorkspaceBootstrap.BuildAsync(folder, settings.Root, new Progress<string>(line => AnsiConsole.MarkupLine(Markup.Escape(line))));
            AnsiConsole.MarkupLine($"[green]Cloned {r.Cloned}[/], {r.AlreadyThere} already there, {(r.Failed.Count == 0 ? "none failed" : $"[red]{r.Failed.Count} failed[/]")}.");
            foreach (var f in r.Failed) AnsiConsole.MarkupLine($"  [red]{Markup.Escape(f)}[/]");
            var appsFailed = settings.InstallApps ? await WorkspaceInstallAppsCommand.Run(folder) : 0;
            return r.Failed.Count == 0 && appsFailed == 0 ? 0 : 1;
        }
        // An unusable folder (no such drive, no write access) is said in one line, not a stack trace.
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}

internal sealed class WorkspaceInstallAppsCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context) =>
        await Run(OrchestratorOptions.Load().WorkspaceRoot) == 0 ? 0 : 1;

    public static async Task<int> Run(string folder)
    {
        var apps = WorkspaceBootstrap.Apps(folder);
        AnsiConsole.MarkupLine($"Installing {apps.Count} apps: {Markup.Escape(string.Join(", ", apps))}");
        var failed = await WorkspaceBootstrap.InstallAppsAsync(folder, new Progress<string>(line => AnsiConsole.MarkupLine(Markup.Escape(line))));
        foreach (var f in failed) AnsiConsole.MarkupLine($"  [red]{Markup.Escape(f)}[/]");
        AnsiConsole.MarkupLine(failed.Count == 0 ? "[green]All installed.[/]" : $"[red]{failed.Count} failed.[/]");
        return failed.Count;
    }
}
