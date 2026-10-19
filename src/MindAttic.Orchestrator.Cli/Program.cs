using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

Console.OutputEncoding = System.Text.Encoding.UTF8;
OrchestratorLog.Start(args.FirstOrDefault() == "daemon" ? "Daemon" : "Cli");

var app = CliApp.Create();
try
{
    return app.Run(args);
}
catch (Exception ex) when (ex is Spectre.Console.Cli.CommandParseException or Spectre.Console.Cli.CommandRuntimeException)
{
    // A typing mistake (an unknown flag, a missing argument, a value that isn't a number) is said in one line, not
    // logged as a crash.
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return 1;
}
catch (InvalidDataException ex)
{
    // A store file that can't be read (settings.json with a hand edit's typo, or held past the retries) was left as it
    // is rather than saved over: the message says so and what to do. Not a crash (M.ORCH-T332).
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return 1;
}
catch (Exception ex)
{
    OrchestratorLog.For("Cli").Fatal(ex, "orchestrator {Args:l} crashed", string.Join(' ', args));
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}
finally
{
    OrchestratorLog.Stop();
}

/// <summary>Every command the CLI has, so tests can run them in-process (see <see cref="Ctx.StoreDirectory"/>).</summary>
internal static class CliApp
{
    public static CommandApp Create()
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            config.SetApplicationName("orchestrator");
            config.AddCommand<StatusCommand>("status").WithDescription("Every repo: state, live sessions, open tickets, tokens today.");
            config.AddCommand<StartCommand>("start").WithDescription("Open an agent tab for a repo through the Launcher.");
            config.AddCommand<SendCommand>("send").WithDescription("Type text into a live Launcher-hosted session.");
            config.AddCommand<BudgetCommand>("budget").WithDescription("Token budget for the current 5-hour block and the week, and the pace; --block/--weekly/--warn/--pace/--target set them, --hits lists learned limits.");
            config.AddCommand<QueueCommand>("queue").WithDescription("Every open ticket across the workspace, in the order the scheduler would start them.");
            config.AddCommand<BumpCommand>("bump").WithDescription("Raise (or with a negative --by, lower) a ticket's priority.");
            config.AddCommand<DispatchCommand>("dispatch").WithDescription("Start a session on a ticket (a worktree when its repo is busy).");
            config.AddCommand<ReleaseCommand>("release").WithDescription("Return a ticket to the queue; --remove-worktree also deletes its clean worktree.");
            config.AddCommand<DaemonCommand>("daemon").WithDescription("Keep the budget file, pace and hooks fresh without the dashboard open.");
            config.AddCommand<SettingsCommand>("settings").WithDescription("Show or change scheduler settings.");
            config.AddCommand<AuditCommand>("audit").WithDescription("Find drift in shared agent files and code duplicated across repos.");
            config.AddCommand<BlockCommand>("block").WithDescription("Make a ticket wait for others (--by X-T1), or --clear it.");
            config.AddCommand<NextCommand>("next").WithDescription("The next tickets a CLI running /autopilot hands its subagents (--claim: worktree, assignment and prompt).");
            config.AddCommand<ApproveCommand>("approve").WithDescription("Approve tickets for work (fixes need none); with no ids, list what's waiting for your yes. --undo takes it back.");
            config.AddCommand<ReserveCommand>("reserve").WithDescription("Set aside tokens for a ticket so background work can't spend them first (0 releases).");
            config.AddCommand<DryRunCommand>("dryrun").WithDescription("Show what /autopilot would start right now, without starting anything.");
            config.AddCommand<ShowCommand>("show").WithDescription("A ticket end to end: its row, the sessions that worked on it, and its commits (--diff for the full diff).");
            config.AddCommand<HygieneCommand>("hygiene").WithDescription("Closed tickets that may not be done: no Ref cited, or a later ticket says it came back.");
            config.AddCommand<IntakeCommand>("intake").WithDescription("File GitHub issues labelled orchestrator and CI failures on main as tickets (--dry-run to preview).");
            config.AddCommand<ReportCommand>("report").WithDescription("Uncached tokens by week, repo, provider and ticket type; --csv writes it out.");
            config.AddCommand<BillingCommand>("billing").WithDescription("Who every CLI bills (Claude Team or the Vault's API key) and each open CLI's own billing; team or api switches every CLI.");
            config.AddCommand<ClisCommand>("clis").WithDescription("Every open Claude CLI and whether /autopilot is on in it (the roster the daemon keeps in clis.json).");
            config.AddCommand<LedgerCommand>("ledger").WithDescription("Every 5-hour window graded against 99%-never-over (ok, under, blind, over) and the record so far; --csv writes it out. Exits 1 if one went over in --days.");
            config.AddBranch("workspace", w =>
            {
                w.SetDescription("The workspace as a list of repos: export it, or build it on a new machine.");
                w.AddCommand<WorkspaceExportCommand>("export").WithDescription("Write docs\\workspace-repos.json in the workspace root repo from the repos on disk.");
                w.AddCommand<WorkspaceInstallAppsCommand>("install-apps").WithDescription("Run every app's deploy.ps1 in the workspace (not the orchestrator's own).");
                w.AddCommand<WorkspaceBuildCommand>("build").WithDescription("Clone the workspace root, then every repo it lists, into a folder.");
            });
            // Let crashes reach the catch below so they're logged, not just printed.
            config.PropagateExceptions();
        });
        return app;
    }
}

internal sealed class StatusCommand : Command<StatusCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("Include dormant repos with no open tickets.")]
        public bool All { get; init; }

        [CommandOption("--json")]
        [Description("Write the snapshot as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        var usage = snapshot.UsageToday;
        AnsiConsole.MarkupLine(
            $"[bold]{snapshot.Repos.Count}[/] repos · [green]{snapshot.Working} working[/] · [yellow]{snapshot.Waiting} waiting[/] · " +
            $"{snapshot.LiveHosts} hosted tabs · {snapshot.OpenTickets} open tickets · " +
            $"tokens today [bold]{Format.Tokens(usage.Total)}[/] ({Format.Tokens(usage.Fresh)} uncached, {Format.Tokens(usage.Output)} out)");
        if (snapshot.Budget is { } b) AnsiConsole.MarkupLine(BudgetCommand.Line(b, snapshot.TakenUtc));
        if (snapshot.Pace is { IsPaced: true } pace) AnsiConsole.MarkupLine(BudgetCommand.PaceLine(pace));
        if (snapshot.Billing is { } billing) BillingCommand.Write(billing, snapshot.TakenUtc);
        foreach (var x in MindAttic.Orchestrator.Activity.Watchdog.Stuck(snapshot))
            AnsiConsole.MarkupLine($"[red]looks stuck:[/] a session in {Markup.Escape(x.Repo)} is repeating {Markup.Escape(x.Loop.Describe())} since {x.Loop.SinceUtc.ToLocalTime():t}");

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Repo", "State", "Sessions", "Open", "Last active", "Tokens today");
        foreach (var r in Order(snapshot.Repos).Where(r => settings.All || r.State != RepoState.Dormant || r.OpenTickets.Count > 0))
        {
            var color = r.State switch { RepoState.Working => "green", RepoState.Waiting => "yellow", _ => "grey" };
            table.AddRow(
                Markup.Escape(r.Name),
                $"[{color}]{r.State}[/]",
                Format.Sessions(r),
                r.OpenTickets.Count == 0 ? "" : r.OpenTickets.Count.ToString(),
                Format.Ago(r.LastActivityUtc, snapshot.TakenUtc),
                r.UsageToday.Total == 0 ? "" : Format.Tokens(r.UsageToday.Total));
        }
        AnsiConsole.Write(table);
        foreach (var w in snapshot.Warnings) AnsiConsole.MarkupLine($"[red]![/] {Markup.Escape(w)}");
        return 0;
    }

    internal static IEnumerable<RepoStatus> Order(IEnumerable<RepoStatus> repos) => repos
        .OrderByDescending(r => r.State)
        .ThenByDescending(r => r.LastActivityUtc ?? DateTime.MinValue)
        .ThenByDescending(r => r.OpenTickets.Count)
        .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class StartCommand : Command<StartCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandOption("--provider <PROVIDER>")]
        [DefaultValue("Claude")]
        public string Provider { get; init; } = "Claude";

        [CommandOption("--prompt <TEXT>")]
        public string? Prompt { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var options = OrchestratorOptions.Load();
        var repo = new MindAttic.Workflow.Workspace(options.WorkspaceRoot).Find(settings.Repo);
        if (repo is null) { AnsiConsole.MarkupLine($"[red]No repo named {Markup.Escape(settings.Repo)}.[/]"); return 1; }
        if (Ctx.Provider(settings.Provider) is not { } provider) return 1;
        var error = new SessionControl(options).Start(repo.Name, repo.Path, provider, settings.Prompt);
        if (error is not null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]"); return 1; }
        AnsiConsole.MarkupLine($"Started [bold]{provider}[/] in {Markup.Escape(repo.Name)}.");
        return 0;
    }
}

internal sealed class SendCommand : AsyncCommand<SendCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandArgument(1, "<TEXT>")]
        public string Text { get; init; } = "";

        [CommandOption("--no-submit")]
        [Description("Type the text without pressing Enter.")]
        public bool NoSubmit { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var snapshot = new StatusAggregator(OrchestratorOptions.Load()).Refresh();
        var repo = snapshot.Repos.FirstOrDefault(r => r.Name.Equals(settings.Repo, StringComparison.OrdinalIgnoreCase));
        if (repo is null || repo.Hosts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No Launcher-hosted session for {Markup.Escape(settings.Repo)}.[/]");
            return 1;
        }
        var sent = 0;
        foreach (var h in repo.Hosts)
            // Text and Enter sent together read as a paste and leave the prompt unsent: Enter goes as a keystroke of its own.
            if (settings.NoSubmit ? await SessionControl.SendAsync(h.PipeName, settings.Text, submit: false)
                                  : await SessionControl.SubmitAsync(h.PipeName, settings.Text)) sent++;
        AnsiConsole.MarkupLine($"Delivered to {sent} of {repo.Hosts.Count} session(s).");
        return sent > 0 ? 0 : 1;
    }
}

internal sealed class BudgetCommand : Command<BudgetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--block <TOKENS>")]
        [Description("Uncached tokens per 5-hour block (0 clears it: Claude's reported limit, else the learned one, else your peak block).")]
        public string? Block { get; init; }

        [CommandOption("--weekly <TOKENS>")]
        [Description("Uncached tokens per week (0 clears it).")]
        public string? Weekly { get; init; }

        [CommandOption("--weekly-reset <WHEN>")]
        [Description("When the week resets, local time: \"Mon 9:00\", \"Tue 5pm\". \"none\" clears it. Claude's own reported reset comes first; then this, the learned one, or a rolling 7 days.")]
        public string? WeeklyReset { get; init; }

        [CommandOption("--warn <PERCENT>")]
        public int? Warn { get; init; }

        [CommandOption("--pace <ON>")]
        [Description("true or false: cap sessions so spend lands on the target at the reset instead of hitting the limit.")]
        public bool? Pace { get; init; }

        [CommandOption("--target <PERCENT>")]
        [Description("How much of each limit pacing aims to use by the reset (default 98).")]
        public int? Target { get; init; }

        [CommandOption("--week")]
        [Description("The week day by day, and where it ends at this pace.")]
        public bool Week { get; init; }

        [CommandOption("--hits")]
        [Description("List the limit hits the learned limits come from.")]
        public bool Hits { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        WeeklyReset? reset = null;
        if (settings.WeeklyReset is not null && !WeeklyReset.TryParse(settings.WeeklyReset, out reset))
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read \"{Markup.Escape(settings.WeeklyReset)}\"; use a day and a time, like \"Mon 9:00\" or \"Tue 5pm\".[/]");
            return 1;
        }
        if (settings.Target is < 1 or > 100) { AnsiConsole.MarkupLine("[red]--target is a percent from 1 to 100.[/]"); return 1; }
        if (settings.Warn is < 1 or > 100) { AnsiConsole.MarkupLine("[red]--warn is a percent from 1 to 100.[/]"); return 1; }
        // Token counts read as the README, the Settings panel and `reserve` take them: 20M, 500k, 1.5B, or a plain number.
        long? blockTokens = null, weeklyTokens = null;
        if (settings.Block is { } bt) { if (!TokenText.TryParse(bt, out var v)) { AnsiConsole.MarkupLine("[red]--block: a number like 20M, 500k or 0.[/]"); return 1; } blockTokens = v; }
        if (settings.Weekly is { } wt) { if (!TokenText.TryParse(wt, out var v)) { AnsiConsole.MarkupLine("[red]--weekly: a number like 150M, 500k or 0.[/]"); return 1; } weeklyTokens = v; }
        if (settings.Block is not null || settings.Weekly is not null || settings.Warn is not null || settings.WeeklyReset is not null ||
            settings.Pace is not null || settings.Target is not null)
        {
            var s = store.LoadSettingsToChange();
            store.SaveSettings(s with
            {
                BlockBudget = blockTokens is { } bl ? (bl > 0 ? bl : null) : s.BlockBudget,
                WeeklyBudget = weeklyTokens is { } w ? (w > 0 ? w : null) : s.WeeklyBudget,
                WarnPercent = settings.Warn ?? s.WarnPercent,
                WeeklyReset = settings.WeeklyReset is null ? s.WeeklyReset : reset?.ToString(),
                Pacing = settings.Pace ?? s.Pacing,
                PaceTargetPercent = settings.Target ?? s.PaceTargetPercent,
            });
        }

        // Built here (not the daemon's published copy) so a budget just changed above shows at once.
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh(DateTime.UtcNow);
        var b = snapshot.Budget!;
        AnsiConsole.MarkupLine(Line(b, snapshot.TakenUtc));
        if (b.CurrentBlock is { } block) AnsiConsole.MarkupLine(BlockLine(b, block, snapshot.TakenUtc));
        AnsiConsole.MarkupLine($"  today {Format.Tokens(b.Today.Fresh)} uncached ({Format.Tokens(b.Today.CacheRead)} cache reads)");
        AnsiConsole.MarkupLine($"  {WeekLine(b)}");
        AnsiConsole.MarkupLine($"  limits: {Markup.Escape(SourceLine(b))}");
        foreach (var p in snapshot.ProviderUsage.Where(p => p.Provider != Providers.Claude))
            AnsiConsole.MarkupLine($"  {p.Provider}: {Format.Tokens(p.Today.Fresh)} uncached today, {Format.Tokens(p.Week.Fresh)} this week" +
                string.Concat(p.Limits.OrderBy(l => l.Window).Select(l => $" · {l.Window} {l.UsedPercent:0}% (its own count, resets {l.ResetsUtc.ToLocalTime():ddd h:mm tt})")));
        if (snapshot.Pace is { } pace) AnsiConsole.MarkupLine($"  {PaceLine(pace)}");
        if (settings.Hits) WriteHits(b.Calibration);
        if (settings.Week) WriteWeek(Burndown.Compute(b, snapshot.History, snapshot.TakenUtc, snapshot.Account));
        return 0;
    }

    /// <summary>The block's span and burn rate; a time on another day (a block begun before midnight, a reset or limit after it) names its weekday (M.ORCH-T437).</summary>
    public static string BlockLine(BudgetStatus b, UsageBlock block, DateTime nowUtc) =>
        $"  block {Formatting.ResetTime(block.StartUtc, nowUtc)}–{Formatting.ResetTime(block.EndUtc, nowUtc)} · burning {Format.Tokens((long)b.RecentFreshPerHour)}/h now, {Format.Tokens((long)b.FreshPerHour)}/h this block" +
        (b.ProjectedLimitUtc is { } p ? $" · [red]limit at this pace: {Formatting.ResetTime(p, nowUtc)}[/]" : "");

    public static string PaceLine(Pace pace)
    {
        var color = pace.Mode switch { PaceMode.Open => "green", PaceMode.Throttled => "yellow", PaceMode.Coast or PaceMode.Over => "red", _ => "grey" };
        return $"[{color}]pace: {pace.Key}[/] {Markup.Escape(pace.Note)}" + (pace.Advise ? " [grey](running agents are asked to conserve)[/]" : "");
    }

    public static string SourceLine(BudgetStatus b)
    {
        string Of(LimitSource? source, long? limit, LimitKind kind, bool outdated, long? old) => source switch
        {
            LimitSource.Manual => $"{Format.Tokens(limit ?? 0)} (set)",
            LimitSource.Reported => $"{Format.Tokens(limit ?? 0)} (from Claude's own usage percent)",
            LimitSource.Learned => $"{Format.Tokens(limit ?? 0)} (learned from {b.Calibration?.Basis(kind)} limit hits)",
            _ when outdated => $"relearning (already past the {Format.Tokens(old ?? 0)} learned earlier, with no wall)",
            _ => "none",
        };
        return $"5h {Of(b.BlockLimitIsPeak ? null : b.BlockLimitSource, b.BlockLimit, LimitKind.Session, b.BlockLimitOutdated, b.Calibration?.Learned5h)} · " +
               $"weekly {Of(b.WeeklyLimitSource, b.WeeklyLimit, LimitKind.Weekly, b.WeeklyLimitOutdated, b.Calibration?.LearnedWeekly)}";
    }

    private static void WriteWeek(Burndown w)
    {
        var max = Math.Max(1, w.Days.Max(d => d.Fresh));
        foreach (var (day, fresh) in w.Days)
            AnsiConsole.MarkupLine($"  {day:ddd MM-dd} {new string('█', (int)Math.Round(30.0 * fresh / max)),-30} {Format.Tokens(fresh)}");
        var pace = $"{Format.Tokens((long)(w.PerHour * 24))}/day";
        AnsiConsole.MarkupLine(w switch
        {
            { Limit: null } => $"  [grey]{Format.Tokens(w.Used)} this week at {pace}; no weekly limit known yet, so no projection[/]",
            { RunsOut: true } => $"  [red]at {pace} the weekly limit ({Format.Tokens(w.Limit!.Value)}) runs out {w.ExhaustedUtc!.Value.ToLocalTime():ddd h:mm tt}, before the {w.ResetUtc!.Value.ToLocalTime():ddd h:mm tt} reset[/]",
            _ => $"  [green]at {pace} the week ends with room to spare[/] ({Format.Tokens(w.Used)} of {Format.Tokens(w.Limit!.Value)})",
        });
    }

    private static void WriteHits(LimitCalibration? calibration)
    {
        if (calibration is null || calibration.Observations.Count == 0) { AnsiConsole.MarkupLine("  [grey]no limit hits recorded yet[/]"); return; }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Limit", "Hit", "Reset", "Uncached used", "");
        foreach (var o in calibration.Observations.OrderByDescending(o => o.HitUtc))
            table.AddRow(o.Kind.ToString(), $"{o.HitUtc.ToLocalTime():ddd MM-dd h:mm tt}", $"{o.ResetUtc.ToLocalTime():ddd MM-dd h:mm tt}",
                Format.Tokens(o.FreshUsed), o.Covered ? "" : "[grey]partial: transcripts start later[/]");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"  [grey]learned = median of the {LimitCalibration.RecentHits} most recent complete hits of each kind[/]");
    }

    public static string WeekLine(BudgetStatus b)
    {
        var used = $"{Format.Tokens(b.Week.Fresh)} uncached" + (b.WeeklyPercent is { } wp ? $" ({wp:0}% of weekly)" : "");
        return b.WeekResetUtc is { } next
            ? $"this week (since {b.WeekStartUtc.ToLocalTime():ddd h:mm tt}) {used} · resets {next.ToLocalTime():ddd h:mm tt}"
            : $"last 7 days {used} [grey]· rolling: set the real reset with --weekly-reset \"Mon 9:00\"[/]";
    }

    /// <param name="nowUtc">When the times are read: one on another day names its weekday (M.ORCH-T437).</param>
    public static string Line(BudgetStatus b, DateTime nowUtc)
    {
        if (b.CurrentBlock is not { } block) return "[grey]budget: no active 5-hour block[/]";
        var used = $"{Format.Tokens(block.Usage.Fresh)} uncached this block, resets {Formatting.ResetTime(block.EndUtc, nowUtc)}";
        if (b.BlockLimitIsPeak)
            return $"[aqua]this block is at {b.BlockPercent:0}% of your heaviest block in 7 days[/] ({used}) [grey]· no budget set: `orchestrator budget --block <tokens>`[/]";
        var color = b.Level switch { BudgetLevel.Over => "red", BudgetLevel.Warn => "yellow", _ => "green" };
        var what = b.BlockLimitSource switch { LimitSource.Learned => "learned 5h limit", LimitSource.Reported => "5h limit (Claude's count)", _ => "5h budget" };
        var wall = b.AtLimitUntilUtc is { } until ? $" [red]· at the {(b.AtLimitKind == LimitKind.Weekly ? "weekly" : "5-hour")} limit until {Formatting.ResetTime(until, nowUtc)}[/]" : "";
        return $"[{color}]budget: {b.BlockPercent:0}% of your {what}[/] ({Format.Tokens(block.Usage.Fresh)} of {Format.Tokens(b.BlockLimit)}; {used}){wall}";
    }
}

internal static class Format
{
    public static string Tokens(long n) => Formatting.Tokens(n);

    public static string Ago(DateTime? utc, DateTime nowUtc) => Formatting.Ago(utc, nowUtc);

    public static string Sessions(RepoStatus r)
    {
        var parts = new List<string>();
        if (r.Hosts.Count > 0) parts.Add(string.Join(", ", r.Hosts.GroupBy(h => h.Provider).Select(g => $"{g.Count()} {g.Key} tab")));
        var recent = r.Sessions.Count(s => s.LastWriteUtc >= DateTime.UtcNow.AddMinutes(-30));
        if (recent > 0) parts.Add($"{recent} recent");
        return string.Join(" · ", parts);
    }
}
