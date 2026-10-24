using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Tickets;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// The work run on a ticket: the sessions the orchestrator dispatched on it. The ticket itself (its row, links, history and the
/// commits that mention it) is MindAttic.Tickets' to show (<c>tickets ticket show</c>, Tickets Desktop), so it isn't repeated here
/// (M.ORCH-T762).
/// </summary>
internal sealed class ShowCommand : Command<ShowCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var options = OrchestratorOptions.Load(store);
        var id = Ctx.Id(settings.Ticket);
        AnsiConsole.MarkupLine($"[grey]The ticket itself (its row, links, history and commits): tickets ticket show {Markup.Escape(id)}, or open it in Tickets Desktop.[/]");
        var sessions = TicketReplay.Sessions(id, options.ClaudeProjectsDir, store.LoadSettings().WorktreeRoot);
        AnsiConsole.MarkupLine(sessions.Count == 0 ? "[grey]No dispatched sessions found for it (only sessions the orchestrator started are tied to a ticket).[/]"
            : $"[bold]{sessions.Count} session{(sessions.Count == 1 ? "" : "s")}[/] worked {Markup.Escape(id)}, {Formatting.Tokens(sessions.Sum(s => s.Fresh))} uncached");
        foreach (var s in sessions)
        {
            AnsiConsole.MarkupLine($"\n[aqua]{s.StartUtc.ToLocalTime():ddd MM-dd h:mm tt} – {s.EndUtc.ToLocalTime():h:mm tt}[/] · {Formatting.Tokens(s.Fresh)} · [grey]{Markup.Escape(s.File)}[/]");
            foreach (var p in s.Prompts.Take(8)) AnsiConsole.MarkupLine($"  [bold]>[/] {Markup.Escape(p)}");
            if (s.Prompts.Count > 8) AnsiConsole.MarkupLine($"  [grey]… {s.Prompts.Count - 8} more prompts[/]");
            if (s.LastReply.Length > 0) AnsiConsole.MarkupLine($"  [grey]last reply:[/] {Markup.Escape(s.LastReply)}");
        }
        return 0;
    }
}
