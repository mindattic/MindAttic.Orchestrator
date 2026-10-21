using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Log;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Closed tickets that may not really be done, across the workspace.</summary>
internal sealed class HygieneCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var root = OrchestratorOptions.Load(Ctx.Store()).WorkspaceRoot;
        // Each repo's tickets from the ticket database when it holds the repo, else from its own USER_STORIES.md (M.ORCH-T461):
        // the generated view lags the database until something renders it, so a ticket closed or filed since was judged as it was.
        using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(root);
        var inDb = db?.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var fromDb = db is null ? [] : db.Tickets().GroupBy(t => t.Repo, StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => TicketHygiene.Check(g.Key, [.. g.Select(x => x.Ticket)]));
        var findings = fromDb.Concat(new MindAttic.Tickets.Workspace(root).Repos()
            .Where(r => !inDb.Contains(r.Name) && File.Exists(r.StoriesPath))
            .SelectMany(r =>
            {
                try { return TicketHygiene.Check(r.Name, [.. WorkspaceTickets.All([r]).Select(x => x.Ticket)]); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { return []; }
            }))
            .ToList();
        if (findings.Count == 0) { AnsiConsole.MarkupLine("[green]Every closed fix and feature cites its proof, and none came back.[/]"); return 0; }

        var table = new Table().Border(TableBorder.Rounded).AddColumns("Repo", "Ticket", "Summary", "Why");
        foreach (var f in findings)
            table.AddRow(Markup.Escape(f.Repo), f.TicketId, Markup.Escape(f.Summary.Length > 60 ? f.Summary[..59] + "…" : f.Summary), Markup.Escape(f.Why));
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{findings.Count} closed tickets to double-check.[/]");
        return 0;
    }
}
