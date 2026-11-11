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

/// <summary>Writes the generated context-gate.py over its version-controlled copies (M.ORCH-T1605): AgentGate's script is
/// the one source, and these exports are made only on purpose, then committed in their own repos.</summary>
internal sealed class WorkspaceExportContextGateCommand : Command<WorkspaceExportContextGateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--check")]
        [Description("Only compare: exit 1 when a copy differs from the generated script, writing nothing.")]
        public bool Check { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        List<(string Path, bool Differed)> copies;
        try { copies = AgentGate.ExportSharedContextGate(OrchestratorOptions.Load().WorkspaceRoot, settings.Check); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't export context-gate.py: {Markup.Escape(e.Message)}[/]");
            return 1;
        }
        if (copies.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No shared copy of context-gate.py's folder is in the workspace.[/]");
            return 1;
        }
        foreach (var (path, differed) in copies)
            AnsiConsole.MarkupLine(Markup.Escape(path) + ": " + (!differed ? "up to date" : settings.Check ? "[yellow]differs[/]" : "[green]written[/]"));
        if (!settings.Check && copies.Any(c => c.Differed))
            AnsiConsole.MarkupLine("[grey]Commit each in its own repo (MindAttic.Agency, and the workspace root for the mirror).[/]");
        return settings.Check && copies.Any(c => c.Differed) ? 1 : 0;
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
