using System.ComponentModel;
using System.Globalization;
using System.Text;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>Where the tokens went: by week and repo, by provider, and by ticket type.</summary>
internal sealed class ReportCommand : Command<ReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--weeks <N>")]
        [DefaultValue(4)]
        public int Weeks { get; init; } = 4;

        [CommandOption("--csv <FILE>")]
        [Description("Also write every week/repo/provider row to a CSV file.")]
        public string? Csv { get; init; }
    }

    private static DateOnly WeekOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); // Mondays

    public override int Execute(CommandContext context, Settings settings)
    {
        var (_, _, snapshot, _) = Ctx.Load();
        var from = WeekOf(DateOnly.FromDateTime(DateTime.Now)).AddDays(-7 * (Math.Max(1, settings.Weeks) - 1));
        var rows = snapshot.History.Where(d => d.Day >= from).ToList();
        var weeks = rows.Select(r => WeekOf(r.Day)).Distinct().Order().ToList();

        var byRepo = new Table().Border(TableBorder.Rounded).AddColumn("Repo");
        foreach (var w in weeks) byRepo.AddColumn($"wk {w:MM-dd}", c => c.RightAligned());
        byRepo.AddColumn("Total", c => c.RightAligned());
        foreach (var repo in rows.GroupBy(r => r.Repo).OrderByDescending(g => g.Sum(r => r.Fresh)))
            byRepo.AddRow([Markup.Escape(repo.Key), .. weeks.Select(w => Formatting.Tokens(repo.Where(r => WeekOf(r.Day) == w).Sum(r => r.Fresh))), Formatting.Tokens(repo.Sum(r => r.Fresh))]);
        AnsiConsole.MarkupLine($"[bold]Uncached tokens since {from:MMM d}[/] (input + cache writes + output)");
        AnsiConsole.Write(byRepo);

        AnsiConsole.MarkupLine("By provider: " + string.Join(" · ", rows.GroupBy(r => r.Provider).OrderByDescending(g => g.Sum(r => r.Fresh))
            .Select(g => $"{g.Key} {Formatting.Tokens(g.Sum(r => r.Fresh))}")));

        var tickets = snapshot.Costs.Tickets.Where(t => DateOnly.FromDateTime(t.LastUtc.ToLocalTime()) >= from).ToList();
        AnsiConsole.MarkupLine(tickets.Count == 0
            ? "[grey]By ticket type: no dispatched tickets in this period yet.[/]"
            : "By ticket type (dispatched work): " + string.Join(" · ", tickets.GroupBy(t => t.Type ?? "?").OrderByDescending(g => g.Sum(t => t.Fresh))
                .Select(g => $"{g.Key} {Formatting.Tokens(g.Sum(t => t.Fresh))} over {g.Count()} ticket{(g.Count() == 1 ? "" : "s")}")));

        if (settings.Csv is { } csv)
        {
            var sb = new StringBuilder("week,day,repo,provider,uncached_tokens\n");
            foreach (var r in rows)
                sb.Append(CultureInfo.InvariantCulture, $"{WeekOf(r.Day):yyyy-MM-dd},{r.Day:yyyy-MM-dd},\"{r.Repo.Replace("\"", "\"\"")}\",{r.Provider},{r.Fresh}\n");
            File.WriteAllText(csv, sb.ToString());
            AnsiConsole.MarkupLine($"Wrote {rows.Count} rows to {Markup.Escape(Path.GetFullPath(csv))}.");
        }
        return 0;
    }
}
