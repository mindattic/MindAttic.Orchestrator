using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Workflow.Tickets;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Closed tickets that may not really be done, across the workspace.</summary>
internal sealed class HygieneCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var findings = new MindAttic.Workflow.Workspace(OrchestratorOptions.Load().WorkspaceRoot).Repos()
            .Where(r => File.Exists(r.StoriesPath))
            .SelectMany(r =>
            {
                try { return TicketHygiene.Check(r.Name, StoryFile.Load(r.StoriesPath).Tickets); }
                catch (Exception ex) when (ex is IOException or InvalidOperationException) { return []; }
            })
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
