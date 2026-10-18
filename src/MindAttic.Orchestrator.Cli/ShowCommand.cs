using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Workflow.Tickets;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>A ticket end to end: its row, the sessions that worked on it, and the commits that mention it.</summary>
internal sealed class ShowCommand : Command<ShowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--diff")]
        [Description("Show each commit's full diff, not just the files it touched.")]
        public bool Diff { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var options = OrchestratorOptions.Load(store);
        var id = Ctx.Id(settings.Ticket);
        // The Workflow database when it holds the ticket, else a story-file repo's USER_STORIES.md (M.ORCH-T461): the generated view
        // lags the database until something renders it, so a ticket the orchestrator filed itself (the pace ledger's, GitHub
        // intake's) read as "No ticket" for up to the daemon's next db sync, or for good with the daemon stopped.
        MindAttic.Orchestrator.Tickets.TicketQueue.Found? hit;
        using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(options.WorkspaceRoot))
            hit = TicketQueue.Find(db, options.WorkspaceRoot, id);
        if (hit is null) { AnsiConsole.MarkupLine($"[red]No ticket {Markup.Escape(id)} in the workspace.[/]"); return 1; }
        var repoPath = new MindAttic.Workflow.Workspace(options.WorkspaceRoot).Find(hit.Repo)?.Path ?? "";

        var t = hit.Ticket;
        AnsiConsole.MarkupLine($"[bold]{t.Id}[/] in {Markup.Escape(hit.Repo)} · {t.Type} · [bold]{t.Status}[/] · opened {t.Date}");
        AnsiConsole.MarkupLine($"  {Markup.Escape(t.Summary)}");
        if (t.Ref.Trim() is { Length: > 0 } r && r != "—") AnsiConsole.MarkupLine($"  [grey]Ref: {Markup.Escape(r)}[/]");

        var sessions = TicketReplay.Sessions(id, options.ClaudeProjectsDir, store.LoadSettings().WorktreeRoot);
        AnsiConsole.MarkupLine(sessions.Count == 0 ? "\n[grey]No dispatched sessions found for it (only sessions the orchestrator started are tied to a ticket).[/]"
            : $"\n[bold]{sessions.Count} session{(sessions.Count == 1 ? "" : "s")}[/], {Formatting.Tokens(sessions.Sum(s => s.Fresh))} uncached");
        foreach (var s in sessions)
        {
            AnsiConsole.MarkupLine($"\n[aqua]{s.StartUtc.ToLocalTime():ddd MM-dd h:mm tt} – {s.EndUtc.ToLocalTime():h:mm tt}[/] · {Formatting.Tokens(s.Fresh)} · [grey]{Markup.Escape(s.File)}[/]");
            foreach (var p in s.Prompts.Take(8)) AnsiConsole.MarkupLine($"  [bold]>[/] {Markup.Escape(p)}");
            if (s.Prompts.Count > 8) AnsiConsole.MarkupLine($"  [grey]… {s.Prompts.Count - 8} more prompts[/]");
            if (s.LastReply.Length > 0) AnsiConsole.MarkupLine($"  [grey]last reply:[/] {Markup.Escape(s.LastReply)}");
        }

        var commits = TicketReplay.Commits(id, repoPath, settings.Diff);
        AnsiConsole.MarkupLine(commits.Length == 0 ? "\n[grey]No commits mention it.[/]" : "\n[bold]Commits[/]");
        if (commits.Length > 0) AnsiConsole.WriteLine(commits);
        return 0;
    }
}
