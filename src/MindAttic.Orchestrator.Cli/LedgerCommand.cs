using System.ComponentModel;
using System.Globalization;
using System.Text;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>The pace ledger: every 5-hour window graded against "99%, never over", and the record so far (M.ORCH-T166).</summary>
internal sealed class LedgerCommand : Command<LedgerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--days <N>")]
        [Description("How many days of windows to list (the summary always covers the whole record).")]
        [DefaultValue(7)]
        public int Days { get; init; } = 7;

        [CommandOption("--csv <FILE>")]
        [Description("Also write every recorded window to a CSV file.")]
        public string? Csv { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var now = DateTime.UtcNow;
        var db = new OrchestratorDb(OrchestratorDb.DefaultPath(OrchestratorOptions.Load(store).WorkspaceRoot));
        db.ImportJson(store.Directory);
        var samples = db.LoadUsageSamples();
        var hits = store.LoadLimits();
        var events = PaceLedger.LoadEvents(PaceLedgerWatch.DefaultEventsPath, now - PaceLedgerWatch.KeepEvents);
        var recorded = db.LoadLedger();
        var accountAt = PaceLedger.AccountAt(new ClaudeAccount(), now);
        // Windows the daemon hasn't closed yet (it closes them a minute after their reset) are shown as they'll be recorded.
        List<LedgerWindow> rows = [.. recorded.Concat(PaceLedger.Due(recorded, samples, hits, events, now, accountAt)).OrderBy(r => r.EndUtc)];

        AnsiConsole.MarkupLine($"[bold]Pace ledger[/]: {Markup.Escape(PaceLedger.Summary(rows, now))}");
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Window").AddColumn("Grade").AddColumn("Peak", c => c.RightAligned()).AddColumn("End", c => c.RightAligned())
            .AddColumn("Uncached", c => c.RightAligned()).AddColumn("At 99%", c => c.RightAligned()).AddColumn("Held", c => c.RightAligned())
            .AddColumn("Refused", c => c.RightAligned()).AddColumn("Why");
        var from = now.AddDays(-Math.Clamp(settings.Days, 1, 36500));
        foreach (var w in rows.Where(r => r.EndUtc >= from)) table.AddRow(Row(w, null));
        if (PaceLedger.Current(samples, hits, events, now, accountAt) is { } current) table.AddRow(Row(current, "running"));
        AnsiConsole.Write(table);

        if (settings.Csv is { } csv)
        {
            var sb = new StringBuilder("start_utc,end_utc,grade,peak_percent,final_percent,uncached_tokens,samples,minutes_at_target,limit_hit_utc,weekly_hit_utc,held,held_minutes,refused,prompts_answered,blind_minutes,why,exempt\n");
            foreach (var w in rows)
                sb.Append(CultureInfo.InvariantCulture,
                    $"{w.StartUtc:O},{w.EndUtc:O},{w.Grade},{w.PeakPercent},{w.FinalPercent},{w.Fresh},{w.Samples},{w.MinutesAtTarget},{w.LimitHitUtc:O},{w.WeeklyHitUtc:O},{w.Held},{w.HeldMinutes},{w.Refused},{w.PromptsAnswered},{Math.Round(w.Blind.TotalMinutes)},\"{w.Why.Replace("\"", "\"\"")}\",{w.Exempt}\n");
            // A folder that isn't there or a file open in Excel is said in one line, not a crash (exit 2: 1 means a window went over).
            try { File.WriteAllText(csv, sb.ToString()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't write {Markup.Escape(csv)}: {Markup.Escape(ex.Message)}[/]");
                return 2;
            }
            AnsiConsole.MarkupLine($"Wrote {rows.Count} windows to {Markup.Escape(Path.GetFullPath(csv))}.");
        }
        return rows.Any(r => r.Grade == WindowGrade.Over && now - r.EndUtc <= TimeSpan.FromDays(Math.Clamp(settings.Days, 1, 36500))) ? 1 : 0;
    }

    private static string[] Row(LedgerWindow w, string? running)
    {
        var grade = running ?? w.Grade.ToString().ToLowerInvariant();
        var color = running is not null ? "blue" : w.Grade switch { WindowGrade.Over => "red", WindowGrade.Blind => "yellow", WindowGrade.Under => "grey", _ => "green" };
        return
        [
            $"{w.StartUtc.ToLocalTime():MMM d HH:mm}–{w.EndUtc.ToLocalTime():HH:mm}",
            $"[{color}]{grade}[/]",
            w.PeakPercent is { } p ? $"{p:0}%" : "–",
            w.FinalPercent is { } f ? $"{f:0}%" : "–",
            Formatting.Tokens(w.Fresh),
            w.MinutesAtTarget > 0 ? $"{w.MinutesAtTarget:0}m" : "",
            (w.Held > 0 ? $"{w.Held} ({w.HeldMinutes:0.#}m)" : "") + (w.Exempt > 0 ? $" +{w.Exempt} exempt" : ""),
            w.Refused + w.PromptsAnswered > 0 ? $"{w.Refused}" + (w.PromptsAnswered > 0 ? $" +{w.PromptsAnswered} typed" : "") : "",
            Markup.Escape(running is not null && w.Grade is WindowGrade.Ok or WindowGrade.Under ? $"at {w.FinalPercent:0}% so far" : w.Why),
        ];
    }
}
