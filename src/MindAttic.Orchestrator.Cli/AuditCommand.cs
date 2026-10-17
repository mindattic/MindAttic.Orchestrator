using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Audit;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuditCommand : Command<AuditCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--latest")]
        [Description("Show the last saved report instead of scanning again.")]
        public bool Latest { get; init; }

        [CommandOption("--top <N>")]
        [DefaultValue(15)]
        public int Top { get; init; } = 15;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = OrchestratorOptions.Load();
        var runner = new AuditRunner(options, Ctx.Store());
        var report = settings.Latest ? runner.Latest() : runner.Run();
        if (report is null) { AnsiConsole.MarkupLine("[yellow]No saved audit yet; run without --latest.[/]"); return 1; }

        string Rel(string p) => Path.GetRelativePath(options.WorkspaceRoot, p);
        AnsiConsole.MarkupLine($"Audit of {report.ReposScanned} repos, {report.FilesScanned} source files, {report.Took.TotalSeconds:0.0}s ({report.RanUtc.ToLocalTime():g})");

        AnsiConsole.MarkupLine($"\n[bold]Convention drift[/] · {report.Drift.Count} file(s) differ from what most repos have");
        foreach (var d in report.Drift.Take(settings.Top))
            AnsiConsole.MarkupLine($"  {Markup.Escape(d.Repo)} [grey]{Markup.Escape(d.File)}[/]  {d.Similarity:P0} like the version in {d.CanonicalCount}/{d.Total} repos ({d.Variants} variants)");

        AnsiConsole.MarkupLine($"\n[bold]Missing[/] · {report.Missing.Count} shared file(s) absent where most agent-enabled repos have them");
        foreach (var g in report.Missing.GroupBy(m => m.File).Take(settings.Top))
            AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(g.Key)}[/] ({g.First().HaveIt}/{g.First().Eligible} have it): {Markup.Escape(string.Join(", ", g.Select(m => m.Repo)))}");

        AnsiConsole.MarkupLine($"\n[bold]Cross-repo duplication[/] · {report.Duplicates.Count(g => g.Identical)} identical, {report.Duplicates.Count(g => !g.Identical)} near-copies");
        foreach (var g in report.Duplicates.Take(settings.Top))
        {
            var kind = g.Identical ? $"[green]identical in {g.RepoCount} repos[/]" : $"[yellow]{g.Similarity:P0} alike[/]";
            AnsiConsole.MarkupLine($"  {kind}, ~{g.SharedLines} lines → [aqua]{Markup.Escape(g.SuggestedHome)}[/]");
            foreach (var f in g.Files.Take(4)) AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(Rel(f.Path))}[/]");
            if (g.Files.Count > 4) AnsiConsole.MarkupLine($"    [grey]+{g.Files.Count - 4} more[/]");
        }
        return 0;
    }
}
