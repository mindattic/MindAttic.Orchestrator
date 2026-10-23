using System.ComponentModel;
using System.Globalization;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator report</c> (M.ORCH-T685): the usage report, one row per member (<see cref="UsageReport"/>): 5-hour and
/// weekly use with their resets, points left to the weekly target, how old each reading is and whether it's accurate, and
/// the lines under it. <c>--detail</c> adds the CLIs' spend on each login against the member's outside use, and the API key's;
/// <c>--since</c> adds the spend since a day; <c>--csv</c> and <c>--json</c> print it raw; <c>--md</c> or <c>--html</c> write it to share.
/// </summary>
internal sealed class UsageReportCommand : Command<UsageReportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--since <DATE>")]
        [Description("Also count each login's CLI spend (and the API key's) since this day: 2026-10-01.")]
        public string? Since { get; init; }

        [CommandOption("--detail")]
        [Description("Also print the second table: where each reading came from, the CLIs' spend today and this week, outside use, and the API key's spend.")]
        public bool Detail { get; init; }

        [CommandOption("--csv")]
        [Description("Print the report as CSV instead (raw values, one row per member, then the API key).")]
        public bool Csv { get; init; }

        [CommandOption("--json")]
        [Description("Print the report as JSON instead.")]
        public bool Json { get; init; }

        [CommandOption("--md <FILE>")]
        [Description("Also write the report as a Markdown file to share.")]
        public string? Markdown { get; init; }

        [CommandOption("--html <FILE>")]
        [Description("Also write the report as an HTML page to share.")]
        public string? Html { get; init; }

        public override ValidationResult Validate() =>
            Csv && Json ? ValidationResult.Error("Pick one of --csv and --json.")
            : Since is { } s && ParseDay(s) is null ? ValidationResult.Error($"\"{s}\" isn't a day: use 2026-10-01.")
            : ValidationResult.Success();
    }

    /// <summary>The report to print; tests build their own (never this machine's logins).</summary>
    internal static Func<DateOnly?, UsageReport>? Override { get; set; }

    internal static DateOnly? ParseDay(string text) =>
        DateOnly.TryParseExact(text.Trim(), ["yyyy-MM-dd", "yyyy-M-d", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public override int Execute(CommandContext context, Settings settings)
    {
        var since = settings.Since is { } s ? ParseDay(s) : null;
        var report = Override?.Invoke(since) ?? Load(since);
        // Plain Console for the raw formats: piped to a file or another tool as is, no markup or wrapping.
        if (settings.Csv) Console.Write(report.Csv());
        else if (settings.Json) Console.WriteLine(report.Json());
        else Write(report, settings.Detail);
        var failed = false;
        foreach (var path in new[] { settings.Markdown, settings.Html }.OfType<string>())
        {
            try
            {
                report.WriteFile(path);
                if (!settings.Csv && !settings.Json) AnsiConsole.MarkupLine($"Wrote {Markup.Escape(Path.GetFullPath(path))}.");
            }
            // A folder that can't be made or a file held open is said in one line, not a crash.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't write {Markup.Escape(path)}: {Markup.Escape(ex.Message)}[/]");
                failed = true;
            }
        }
        return failed ? 1 : 0;
    }

    /// <summary>Prints the report's plain text (the main table and its notes; with <paramref name="detail"/> the spend table too),
    /// for any command that shows it. Plain Console, not AnsiConsole: the table's lines are never wrapped at the console's width.</summary>
    internal static void Write(UsageReport report, bool detail) => Console.Write(report.PlainText(detail));

    /// <summary>This machine's report: the roster brought up to date first (as <c>accounts</c> does), then a status refresh for
    /// the usage history and each login's budget.</summary>
    internal static UsageReport Load(DateOnly? since)
    {
        AccountsCommand.Load();
        var store = Ctx.Store();
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh();
        var now = DateTime.UtcNow;
        OrchestratorSettings settings;
        try { settings = store.LoadSettings(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { settings = new OrchestratorSettings(); }
        return UsageReport.Gather(new OrchestratorDb(OrchestratorDb.LivePath), snapshot, new AccountDirs(), settings, now, since,
            ReportedUsage.Load(now), ReportedUsage.DefaultDb);
    }
}
