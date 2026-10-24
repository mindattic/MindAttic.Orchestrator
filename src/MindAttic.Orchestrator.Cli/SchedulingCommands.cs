using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

internal static class Ctx
{
    public static (StatusAggregator Aggregator, JsonStore Store, Snapshot Snapshot, SchedulePlan Plan) Load(int? cli = null)
    {
        cli ??= CallingCli();
        var store = Ctx.Store();
        var aggregator = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Lease(store));
        var snapshot = OnOwnLogin(aggregator.Refresh(), CurrentLogin is { } seam ? seam() : LoginOfCli(cli) ?? new AccountDirs().Current());
        var plan = Scheduler.Plan(snapshot, store.LoadSettings(), store.LoadAssignments());
        return (aggregator, store, snapshot, plan);
    }

    /// <summary>The calling CLI, as the pace gate finds it: MINDATTIC_CLI_PID, else the claude.exe this runs under (M.ORCH-T195).</summary>
    internal static int? CallingCli() => int.TryParse(Environment.GetEnvironmentVariable("MINDATTIC_CLI_PID"), out var envPid) && envPid > 0 ? envPid
        : MindAttic.Orchestrator.Sessions.ProcessTree.ClaudeAncestor(Environment.ProcessId);

    /// <summary>The login the CLI with process id <paramref name="cli"/> runs on, from its session's state (M.ORCH-T673), for a command
    /// (its recorded login is authoritative over this process's environment); null when it's on the global login or isn't known.</summary>
    internal static AccountDir? LoginOfCli(int? cli)
    {
        if (cli is not { } pid) return null;
        try { return new AccountDirs().Of(new MindAttic.Orchestrator.Sessions.ContextCycler().LoginOfCli()(pid)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>The per-CLI login this command runs under (its CLI's CLAUDE_CONFIG_DIR); tests set their own.</summary>
    internal static Func<AccountDir?>? CurrentLogin { get; set; }

    /// <summary>
    /// <paramref name="snapshot"/> as a CLI on <paramref name="login"/>'s own login sees it (M.ORCH-T559): its account's budget and
    /// pace in place of the global login's, so <c>/allocate</c>'s <c>next</c>, <c>dryrun</c> and <c>queue</c> hand out what that
    /// account's allowance and window have room for. Unchanged on the global login. An account the snapshot hasn't paced yet
    /// (signed in moments ago) is planned unpaced, as with no daemon, never on the global login's room; its hooks still stop
    /// it at Claude's own 99%.
    /// </summary>
    internal static Snapshot OnOwnLogin(Snapshot snapshot, AccountDir? login)
    {
        if (login is null) return snapshot;
        var own = snapshot.Accounts.FirstOrDefault(a => a.Email.Equals(login.Email, StringComparison.OrdinalIgnoreCase));
        return snapshot with { Budget = own?.Budget, Pace = own?.Pace, Utilization = own?.Utilization, Account = own?.Account };
    }

    /// <summary>Where the store lives; null is the user's own (%APPDATA%). Tests point it at a temp folder.</summary>
    public static string? StoreDirectory { get; set; }

    /// <summary>The settings store every command reads and writes (<see cref="StoreDirectory"/>).</summary>
    public static JsonStore Store() => new(StoreDirectory);

    /// <summary>The status lease the daemon and dashboards share: a one-off command only writes rl-status.json while
    /// nobody else holds it, so it never overwrites the running daemon's pace. Released when the command exits.</summary>
    public static DispatchLease Lease(JsonStore store) => new(store.Directory, db: store.Db);

    /// <summary>A ticket id as stored and compared everywhere (upper case), so "m.orch-t5" isn't silently a different ticket.</summary>
    public static string Id(string typed) => typed.Trim().ToUpperInvariant();

    public static OrchestratorSettings Settings => Ctx.Store().LoadSettings();

    public static Dispatcher Dispatcher(JsonStore store) =>
        new(new SessionControl(OrchestratorOptions.Load()), store);

    /// <summary>A typed --provider as its known name, or null (said in red) when the Launcher wouldn't start it: an unknown
    /// one opens a tab that closes at once, while the ticket would read as assigned.</summary>
    public static string? Provider(string typed)
    {
        if (SessionControl.Providers.FirstOrDefault(p => p.Equals(typed.Trim(), StringComparison.OrdinalIgnoreCase)) is { } known) return known;
        AnsiConsole.MarkupLine($"[red]No provider \"{Markup.Escape(typed)}\"; use one of {Markup.Escape(string.Join(", ", SessionControl.Providers))}.[/]");
        return null;
    }
}

internal sealed class QueueCommand : Command<QueueCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--top <N>")]
        [DefaultValue(25)]
        public int Top { get; init; } = 25;

        [CommandOption("--groom")]
        [Description("Only old open tickets nobody is on: re-prioritize them, or close them as wont-do.")]
        public bool Groom { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (_, _, _, plan) = Ctx.Load();
        AnsiConsole.MarkupLine($"{plan.Queue.Count} open tickets · {plan.Occupied} of {plan.Slots} session slots in use" +
            (plan.Cap < plan.Slots ? $" · pacing allows {plan.Cap}" : ""));
        if (plan.Hold is not null) AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(plan.Hold)}[/]");
        foreach (var p in plan.Proposals)
            AnsiConsole.MarkupLine($"[green]next:[/] {p.Item.Id} in {Markup.Escape(p.Item.Repo)} [grey]({Markup.Escape(p.Reason)})[/]  → orchestrator dispatch {p.Item.Id}");
        if (plan.Groom.Count > 0)
            AnsiConsole.MarkupLine($"[grey]{plan.Groom.Count} open tickets are over {Ctx.Settings.GroomAfterDays} days old with nobody on them: `orchestrator queue --groom` lists them.[/]");
        foreach (var q in plan.TooBig)
            AnsiConsole.MarkupLine($"[yellow]too big:[/] {q.Id} is {Markup.Escape(q.Estimate!.ToString())}, more than the {Formatting.Tokens((long)(plan.RoomForRest ?? 0))} left before the reset{(plan.RoomForRest < plan.Pace?.RoomTokens ? " that isn't reserved" : "")}: split it, or it waits for the reset");
        foreach (var q in plan.Stale)
            AnsiConsole.MarkupLine($"[yellow]stale:[/] {q.Id} was dispatched {Formatting.Ago(q.Assignment!.StartedUtc, DateTime.UtcNow)} but nothing has happened in {Markup.Escape(q.Assignment.Directory)} since {Formatting.Ago(q.StaleSinceUtc, DateTime.UtcNow)}; it's back in the queue");
        foreach (var f in plan.Finished)
            AnsiConsole.MarkupLine($"[aqua]done:[/] {f.TicketId} is closed{(f.Worktree ? $"; worktree {Markup.Escape(f.Directory)} can go: orchestrator release {f.TicketId} --remove-worktree" : "")}");
        // Went dark mid-task (M.ORCH-T748): apart from an ordinary open ticket, at once rather than at the next session's start.
        var orphaned = WriteOrphans(Ctx.Store()).Where(r => r.Open).ToDictionary(r => r.TicketId, StringComparer.OrdinalIgnoreCase);

        var table = new Table().Border(TableBorder.Rounded).AddColumns("#", "Ticket", "Repo", "Type", "Pri", "Opened", "Summary", "");
        var i = 0;
        foreach (var q in (settings.Groom ? plan.Groom : plan.Queue).Take(settings.Top))
        {
            var note = orphaned.TryGetValue(q.Id, out var orphan) ? (orphan.State == OrphanRecord.NeedsYou ? "[red]orphaned: needs you[/]" : "[red]orphaned[/]")
                : q.BlockedBy.Count > 0 ? $"[grey]waits for {string.Join(", ", q.BlockedBy)}[/]"
                : q.Reserved > 0 && !q.IsAssigned ? $"[aqua]{Formatting.Tokens(q.Reserved)} reserved[/]"
                : q.TooBig ? "[yellow]too big for now[/]" : q.IsStale ? $"[yellow]stale since {Formatting.Ago(q.StaleSinceUtc, DateTime.UtcNow)}[/]"
                : q.IsAssigned ? $"[aqua]assigned {Formatting.Ago(q.Assignment!.StartedUtc, DateTime.UtcNow)}[/]"
                : plan.Proposals.Any(p => p.Item == q) ? "[green]next[/]" : "";
            table.AddRow((++i).ToString(), Markup.Escape(q.Id), Markup.Escape(q.Repo), Markup.Escape(q.Ticket.Type), q.Priority == 0 ? "" : q.Priority.ToString("+#;-#"),
                Markup.Escape(q.Ticket.Date), Markup.Escape(Trim(q.Ticket.Summary, 60)), note);
        }
        AnsiConsole.Write(table);
        return 0;
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    /// <summary>Says each ticket that went dark mid-task and what became of it (M.ORCH-T748): waiting or given up on in red,
    /// redispatched in grey. Returns the records.</summary>
    internal static IReadOnlyList<OrphanRecord> WriteOrphans(JsonStore store)
    {
        var records = OrphanWatch.Load(store);
        foreach (var r in records.OrderByDescending(r => r.Open).ThenBy(r => r.TicketId, StringComparer.OrdinalIgnoreCase))
            AnsiConsole.MarkupLine(r.Open ? $"[red]orphaned:[/] {Markup.Escape(OrphanWatch.Describe(r, DateTime.UtcNow))}"
                : $"[grey]redispatched: {Markup.Escape(OrphanWatch.Describe(r, DateTime.UtcNow))}[/]");
        return records;
    }
}

internal sealed class DispatchCommand : Command<DispatchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--provider <PROVIDER>")]
        public string? Provider { get; init; }

        [CommandOption("--worktree")]
        [Description("Force a worktree even when the repo is free.")]
        public bool Worktree { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (_, store, _, plan) = Ctx.Load();
        var item = plan.Queue.FirstOrDefault(q => q.Id.Equals(settings.Ticket, StringComparison.OrdinalIgnoreCase));
        // An unapproved one gone stale is still in the queue, and waits for your yes like the rest (M.ORCH-T394).
        if (item is null or { IsAssigned: false, MayWork: false } && plan.AwaitingApproval.FirstOrDefault(q => q.Id.Equals(settings.Ticket, StringComparison.OrdinalIgnoreCase)) is { } waiting)
        {
            // Open, but only fixes are worked without your yes: say that, not that the ticket doesn't exist.
            AnsiConsole.MarkupLine($"[yellow]{waiting.Id} waits for your approval: tickets ticket approve {waiting.Id} (or approve it in Tickets Desktop)[/]");
            return 1;
        }
        if (item is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(settings.Ticket)} isn't an open ticket.[/]"); return 1; }
        if (item.IsAssigned) { AnsiConsole.MarkupLine($"[yellow]{item.Id} is already assigned; release it first.[/]"); return 1; }

        if (Ctx.Provider(settings.Provider ?? store.LoadSettings().DefaultProvider) is not { } provider) return 1;
        var r = Ctx.Dispatcher(store).Dispatch(item, provider, settings.Worktree || item.NeedsWorktree);
        AnsiConsole.MarkupLine(r.Ok ? Markup.Escape(r.Message) : $"[red]{Markup.Escape(r.Message)}[/]");
        return r.Ok ? 0 : 1;
    }
}

internal sealed class ReleaseCommand : Command<ReleaseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--remove-worktree")]
        public bool RemoveWorktree { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var a = store.LoadAssignments().FirstOrDefault(x => x.TicketId.Equals(settings.Ticket, StringComparison.OrdinalIgnoreCase));
        // An orphaned ticket released is yours (M.ORCH-T748): the daemon no longer redispatches or holds it back.
        var wasOrphan = OrphanWatch.Forget(store, settings.Ticket);
        if (wasOrphan) AnsiConsole.MarkupLine($"{Markup.Escape(settings.Ticket)} is no longer tracked as orphaned; its worktree is left as it is.");
        if (a is null && wasOrphan) return 0;
        if (a is null) { AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(settings.Ticket)} isn't assigned.[/]"); return 1; }
        var dispatcher = Ctx.Dispatcher(store);
        if (!settings.RemoveWorktree)
        {
            dispatcher.Release(a.TicketId, OrchestratorOptions.Load().WorkspaceRoot);
            AnsiConsole.MarkupLine($"{a.TicketId} is back in the queue.");
            return 0;
        }
        var repo = new MindAttic.Tickets.Workspace(OrchestratorOptions.Load().WorkspaceRoot).Find(a.Repo);
        if (repo is null) { AnsiConsole.MarkupLine($"[red]Repo {Markup.Escape(a.Repo)} not found.[/]"); return 1; }
        var r = dispatcher.RemoveWorktree(a, repo.Path);
        AnsiConsole.MarkupLine(r.Ok ? Markup.Escape(r.Message) : $"[red]{Markup.Escape(r.Message)}[/]");
        return r.Ok ? 0 : 1;
    }
}

internal sealed class DaemonCommand : Command<DaemonCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--interval <SECONDS>")]
        [DefaultValue(15)]
        public int Interval { get; init; } = 15;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCtrlC = (_, e) => { e.Cancel = true; stop.Cancel(); };
        Console.CancelKeyPress += onCtrlC;
        AnsiConsole.MarkupLine($"[grey]Ctrl+C to stop. Log: {Markup.Escape(OrchestratorLog.DbPath)}[/]");
        try
        {
            new Daemon(Ctx.Store(), (level, line) =>
            {
                var color = level switch { Serilog.Events.LogEventLevel.Error => "red", Serilog.Events.LogEventLevel.Warning => "yellow", _ => "default" };
                AnsiConsole.MarkupLine($"[grey]{DateTime.Now:T}[/] [{color}]{Markup.Escape(line)}[/]");
            }).Run(TimeSpan.FromSeconds(Math.Max(5, settings.Interval)), stop.Token);
        }
        finally { Console.CancelKeyPress -= onCtrlC; } // a Ctrl+C during shutdown would cancel a disposed source
        return 0;
    }
}

internal sealed class SettingsCommand : Command<SettingsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--max-sessions <N>")]
        public int? MaxSessions { get; init; }

        [CommandOption("--allocate <ON>")]
        [Description("false pauses every CLI where /allocate is on (their marks stay); true lets them go again.")]
        public bool? Allocate { get; init; }

        [CommandOption("--throttle-agents <ON>")]
        [Description("true: while the burn won't last until the reset, running Claude agents wait before each tool call (up to --max-delay seconds).")]
        public bool? ThrottleAgents { get; init; }

        [CommandOption("--max-delay <SECONDS>")]
        public int? MaxDelay { get; init; }

        [CommandOption("--max-subagents <N>")]
        [Description("The most subagents running at once across every CLI; under it the pace decides, live (0 holds every new one).")]
        public int? MaxSubagents { get; init; }

        [CommandOption("--agent-abort <ON>")]
        [Description("true (default): when a login's live 5-hour trend stays past its pace line with new subagents already slowed for --agent-abort-after minutes, the orchestrator aborts just enough of that login's ticket subagents to bend it back under, releasing their claims and putting their tickets first in the queue; false leaves the cool-off to slow new starts only.")]
        public bool? AgentAbort { get; init; }

        [CommandOption("--agent-abort-after <MINUTES>")]
        [Description("How long the new-start cool-off gets to bend a login's trend back under before --agent-abort steps in, 0 to 60 (default 3).")]
        public int? AgentAbortAfterMinutes { get; init; }

        [CommandOption("--provider <PROVIDER>")]
        public string? Provider { get; init; }

        [CommandOption("--pace-profile <PROFILE>")]
        [Description("linear (default): what's left of the target spread evenly to the reset; front-loaded: aim at target x elapsed^k (--pace-curve-k), faster early, and no new subagent or ticket past 85%. Applies at the next pace check, mid-window too.")]
        public string? PaceProfile { get; init; }

        [CommandOption("--pace-curve-k <K>")]
        [Description("The front-loaded curve's exponent, 0.1 to 1 (default 0.55; lower front-loads more, 1 is a straight line).")]
        public double? PaceCurveK { get; init; }

        [CommandOption("--quiet <WINDOWS>")]
        [Description("Local times /allocate pauses, comma-separated: \"22:00-07:00,12:00-13:00\"; \"none\" clears.")]
        public string? Quiet { get; init; }

        [CommandOption("--repo-cap <REPO=PERCENT>")]
        [Description("Most of the 5-hour limit one repo may spend per block, like Prose=40 (0 clears). Repeatable.")]
        public string[] RepoCap { get; init; } = [];

        [CommandOption("--repo-sessions <REPO=N>")]
        [Description("Sessions one repo may run at once, like Prose=3 (0 clears). Repeatable.")]
        public string[] RepoSessions { get; init; } = [];

        [CommandOption("--keep-going <ON>")]
        [Description("true (default): a CLI that typed /allocate and ends its turn while its repos have workable tickets is told to carry on; false lets it stop.")]
        public bool? KeepGoing { get; init; }

        [CommandOption("--resume-after-reset <ON>")]
        [Description("true (default): once the window a CLI stopped for resets, a resume prompt is typed into its Launcher tab; false only alerts you that it's waiting.")]
        public bool? ResumeAfterReset { get; init; }

        [CommandOption("--orphan-redispatches <N>")]
        [Description("How many times a ticket whose CLI or tab exited mid-task (orphaned) is redispatched, handed back to its repo's /allocate CLI or started again in a new tab in its worktree, before it's left for you as one that keeps dying (default 3; 0 only shows it as orphaned).")]
        public int? OrphanRedispatches { get; init; }

        [CommandOption("--orphan-quiet <MINUTES>")]
        [Description("Minutes a dispatch's CLI or tab must be gone, with nothing written in its worktree, before it counts as orphaned (default 10).")]
        public int? OrphanQuiet { get; init; }

        [CommandOption("--tab-blink-tokens <N>")]
        [Description("The dashboard's person tabs: a busy login's green play arrow blinks once for every N tokens it spends, uncached (default 1000; 1 to 10000000).")]
        public int? TabBlinkTokens { get; init; }

        [CommandOption("--auto-login <ON>")]
        [Description("true (default): a Claude tab started without a login chosen (dashboard Auto, start without --account, Queue starts, accounts pick) goes on a signed-in per-CLI login no CLI runs on yet, most headroom first, else the global login; false: always the global login.")]
        public bool? AutoLogin { get; init; }

        [CommandOption("--rebalance <ON>")]
        [Description("true (default): a CLI whose login crosses its 5-hour pace target or weekly stop line is moved by the daemon to a signed-in per-CLI login with no CLI and more headroom, at its next idle point, and you're toasted; false leaves every CLI on its login.")]
        public bool? Rebalance { get; init; }

        [CommandOption("--failover <ON>")]
        [Description("true (default): a CLI whose login runs out with no other login to rebalance it onto is moved onto the Vault's API key on its own (the billing mode unchanged), resuming its session, and moved back once its login resets; false leaves it waiting for the reset.")]
        public bool? Failover { get; init; }

        [CommandOption("--weekly-reserve <PERCENT>")]
        [Description("The share of each account's week kept back, 0 to 50 (default 5): every CLI on an account stops at 100 minus this of that account's week (95% by default), each login on its own.")]
        public int? WeeklyReserve { get; init; }

        [CommandOption("--weekly-target <PERCENT>")]
        [Description("The weekly target, 50 to 100 (default 95): 100 minus the weekly reserve, so this sets --weekly-reserve to 100 minus it. Every CLI on an account stops there, and weekly-aware pacing lands each login's week on it.")]
        public int? WeeklyTarget { get; init; }

        [CommandOption("--weekly-aware <ON>")]
        [Description("true: weekly-aware pacing, per login: from its take-in line (--weekly-line, 75% by default) of its week a 5-hour window is held only when the week, spent as it's being spent, would reach the weekly target (100 minus --weekly-reserve, 95% by default) before its reset, and then only as far as needed, so the week lands on the target as it resets; scarce room goes to higher-priority tickets first, a ticket its login can't fit goes to another signed-in login with room (else the fallback login), and the week stops at the target; false (default): only the 5-hour target paces.")]
        public bool? WeeklyAware { get; init; }

        [CommandOption("--weekly-line <PERCENT>")]
        [Description("Weekly-aware pacing's take-in line: from this percent of a login's week (default 75) its 5-hour windows are capped by the week. Below the weekly target.")]
        public int? WeeklyLine { get; init; }

        [CommandOption("--weekly-wind-down <POINTS>")]
        [Description("Weekly-aware pacing's wind-down margin, 0 to 20 (default 3): this many points under the weekly target, while there's no plan for the week, only what can finish starts.")]
        public int? WeeklyWindDown { get; init; }
        [CommandOption("--alert-to <CONTACT>")]
        [Description("Who an urgent owner alert (a login reaching its own cutoff) is texted to through Psst: a Psst contact or a US number (default ryan); \"none\" stops it.")]
        public string? AlertTo { get; init; }

        [CommandOption("--hand-minutes <N>")]
        [Description("Kept for older setups; it has no effect now: /allocate hands tickets to the typing CLI's own subagents, each in its own worktree.")]
        public int? HandMinutes { get; init; }

        [CommandOption("--allocate-skip <REPOS>")]
        [Description("Repos /allocate never starts agents in, comma-separated: \"GTFO.EZ,Prose\"; \"none\" clears.")]
        public string? AllocateSkip { get; init; }

        [CommandOption("--exempt <COMMANDS>")]
        [Description("Slash commands whose turns skip every pace gate (no hold, no subagent wait, no stop), comma-separated: \"/deploy,/run\"; \"none\" clears, \"default\" restores /pacing,/deploy,/run.")]
        public string? Exempt { get; init; }

        [CommandOption("--github-intake <ON>")]
        [Description("true: hourly, file GitHub issues labelled 'orchestrator' and CI failures on main as tickets.")]
        public bool? GitHubIntake { get; init; }

        [CommandOption("--notify <URL>")]
        [Description("Where alerts go off the desktop (an ntfy.sh topic URL for phone pushes, or any webhook); \"none\" stops them.")]
        public string? Notify { get; init; }

        [CommandOption("--groom-days <N>")]
        public int? GroomDays { get; init; }

        [CommandOption("--stale-hours <N>")]
        [Description("Hours with no activity where a ticket was dispatched before it goes back in the queue (0 never).")]
        public int? StaleHours { get; init; }

        [CommandOption("--disk-warn-gb <GB>")]
        [Description("Free GB on the system or worktree drive under which the daemon alerts, naming what fills %TEMP% (default 20; 0 turns it off).")]
        public double? DiskWarnGb { get; init; }

        [CommandOption("--disk-critical-gb <GB>")]
        [Description("Free GB under which that alert is urgent (default 5).")]
        public double? DiskCriticalGb { get; init; }

        [CommandOption("--model <REPO_OR_TICKET=MODEL>")]
        [Description("The model /allocate starts a repo's (or one ticket's) subagents on, overriding the pick: Prose=opus, M.ORCH-T12=sonnet, GTFO.*=sonnet; =auto clears. A ticket's wins over its repo's. Repeatable.")]
        public string[] Model { get; init; } = [];

        [CommandOption("--review-lines <N>")]
        [Description("Lines changed past which a Sonnet ticket's diff gets one Opus review before it lands (default 300; 0 reviews every one, -1 none for size).")]
        public int? ReviewLines { get; init; }

        [CommandOption("--risky-files <GLOBS>")]
        [Description("Files whose change by a Sonnet ticket always gets that review, comma-separated wildcards: \"*AgentGate*.cs,*/Budget/Pacer.cs\"; \"default\" restores the gates, pacing and git landing; \"none\" clears.")]
        public string? RiskyFiles { get; init; }
    }

    /// <summary>Why a retired option is refused (M.ORCH-T706): the owner's usage rules are hard-coded, so these can't be set.</summary>
    internal static string? LockedRule(Settings settings) =>
        settings.WeeklyAware is not null ? "--weekly-aware is locked off: no weekly-aware pacing, ever (the owner's rule, M.ORCH-T706)."
        : settings.Failover is not null ? "--failover is locked off: API billing is for emergencies and only the owner switches it on (M.ORCH-T706)."
        : settings.ThrottleAgents is not null ? "--throttle-agents is locked off: hard cutoffs only, agents are never held (M.ORCH-T706)."
        : settings.PaceProfile is not null || settings.PaceCurveK is not null ? "Pace profiles are gone: hard cutoffs only, no front-loaded or throttled curve (M.ORCH-T706)."
        : null;

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.StaleHours is < 0) { AnsiConsole.MarkupLine("[red]--stale-hours can't be negative.[/]"); return 1; }
        if (settings.MaxSessions is < 1) { AnsiConsole.MarkupLine("[red]--max-sessions is at least 1.[/]"); return 1; }
        if (settings.GroomDays is < 1) { AnsiConsole.MarkupLine("[red]--groom-days is at least 1.[/]"); return 1; }
        string? provider = null;
        if (settings.Provider is { } typed &&
            (provider = MindAttic.Orchestrator.Sessions.SessionControl.Providers.FirstOrDefault(p => p.Equals(typed, StringComparison.OrdinalIgnoreCase))) is null)
        {
            AnsiConsole.MarkupLine($"[red]No provider \"{Markup.Escape(typed)}\"; use one of {Markup.Escape(string.Join(", ", MindAttic.Orchestrator.Sessions.SessionControl.Providers))}.[/]");
            return 1;
        }
        if (LockedRule(settings) is { } locked) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(locked)}[/]"); return 1; }
        var store = Ctx.Store();
        // Shown as defaults when settings.json can't be read, but never saved over it (that would wipe everything in it).
        var readable = store.TryLoad<OrchestratorSettings>("settings", out var s);
        s = s with
        {
            MaxConcurrentSessions = settings.MaxSessions ?? s.MaxConcurrentSessions,
            Allocate = settings.Allocate ?? s.Allocate,
            DefaultProvider = provider ?? s.DefaultProvider,
            StaleAfterHours = settings.StaleHours ?? s.StaleAfterHours,
            GroomAfterDays = settings.GroomDays ?? s.GroomAfterDays,
        };
        if (settings.MaxDelay is < 0 or > MindAttic.Orchestrator.Setup.AgentGate.MaxHoldSeconds) { AnsiConsole.MarkupLine($"[red]--max-delay is 0 to {MindAttic.Orchestrator.Setup.AgentGate.MaxHoldSeconds}.[/]"); return 1; }
        s = s with
        {
            MaxAgentDelaySeconds = settings.MaxDelay ?? s.MaxAgentDelaySeconds,
            MaxSubagents = settings.MaxSubagents ?? s.MaxSubagents,
        };
        if (settings.MaxSubagents is < 0) { AnsiConsole.MarkupLine("[red]--max-subagents can't be negative.[/]"); return 1; }
        if (settings.DiskWarnGb is { } dw && !(dw >= 0) || settings.DiskCriticalGb is { } dc && !(dc >= 0)) { AnsiConsole.MarkupLine("[red]--disk-warn-gb and --disk-critical-gb can't be negative.[/]"); return 1; }
        if (settings.DiskWarnGb is not null || settings.DiskCriticalGb is not null)
        {
            s = s with { DiskWarnGb = settings.DiskWarnGb ?? s.DiskWarnGb, DiskCriticalGb = settings.DiskCriticalGb ?? s.DiskCriticalGb };
            if (s.DiskWarnGb > 0 && s.DiskCriticalGb > s.DiskWarnGb) { AnsiConsole.MarkupLine("[red]--disk-critical-gb is at most --disk-warn-gb.[/]"); return 1; }
        }
        var changed = settings.GroomDays is not null || settings.GitHubIntake is not null || settings.MaxDelay is not null || settings.MaxSubagents is not null
            || settings.DiskWarnGb is not null || settings.DiskCriticalGb is not null;
        if (settings.GitHubIntake is { } intake) s = s with { GitHubIntake = intake };
        if (settings.Notify is { } notify)
        {
            var off = notify.Equals("none", StringComparison.OrdinalIgnoreCase);
            if (!off && !(Uri.TryCreate(notify, UriKind.Absolute, out var u) && u.Scheme is "https" or "http")) { AnsiConsole.MarkupLine("[red]--notify takes an http(s) URL, or none.[/]"); return 1; }
            s = s with { NotifyUrl = off ? null : notify };
            changed = true;
        }
        if (settings.Quiet is { } quiet)
        {
            var windows = quiet.Equals("none", StringComparison.OrdinalIgnoreCase) ? [] : quiet.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            if (windows.FirstOrDefault(w => !QuietHours.TryParse(w, out _)) is { } bad) { AnsiConsole.MarkupLine($"[red]Couldn't read \"{Markup.Escape(bad)}\"; use 22:00-07:00.[/]"); return 1; }
            s = s with { QuietHours = windows };
            changed = true;
        }
        if (settings.HandMinutes is { } hand)
        {
            if (hand < 0) { AnsiConsole.MarkupLine("[red]--hand-minutes can't be negative.[/]"); return 1; }
            s = s with { AllocateHandMinutes = hand };
            changed = true;
        }
        if (settings.KeepGoing is { } keep)
        {
            s = s with { KeepGoing = keep };
            changed = true;
        }
        if (settings.ResumeAfterReset is { } resume)
        {
            s = s with { ResumeAfterReset = resume };
            changed = true;
        }
        if (settings.OrphanRedispatches is { } redispatches)
        {
            if (redispatches is < 0 or > 20) { AnsiConsole.MarkupLine("[red]--orphan-redispatches is 0 to 20.[/]"); return 1; }
            s = s with { OrphanRedispatches = redispatches };
            changed = true;
        }
        if (settings.OrphanQuiet is { } orphanQuiet)
        {
            if (orphanQuiet is < 1 or > 1440) { AnsiConsole.MarkupLine("[red]--orphan-quiet is 1 to 1440 minutes.[/]"); return 1; }
            s = s with { OrphanQuietMinutes = orphanQuiet };
            changed = true;
        }
        if (settings.TabBlinkTokens is { } blink)
        {
            if (blink is < OrchestratorSettings.MinTabBlinkTokens or > OrchestratorSettings.MaxTabBlinkTokens) { AnsiConsole.MarkupLine($"[red]--tab-blink-tokens is {OrchestratorSettings.MinTabBlinkTokens} to {OrchestratorSettings.MaxTabBlinkTokens}.[/]"); return 1; }
            s = s with { TabBlinkTokens = blink };
            changed = true;
        }
        if (settings.AutoLogin is { } autoLogin)
        {
            s = s with { AutoLogin = autoLogin };
            changed = true;
        }
        if (settings.Rebalance is { } rebalance)
        {
            s = s with { RebalanceLogins = rebalance };
            changed = true;
        }
        if (settings.AgentAbort is { } agentAbort)
        {
            s = s with { AgentAbort = agentAbort };
            changed = true;
        }
        if (settings.AgentAbortAfterMinutes is { } agentAbortAfter)
        {
            if (agentAbortAfter is < 0 or > 60) { AnsiConsole.MarkupLine("[red]--agent-abort-after is 0 to 60.[/]"); return 1; }
            s = s with { AgentAbortAfterMinutes = agentAbortAfter };
            changed = true;
        }
        if (settings.WeeklyReserve is { } weeklyReserve)
        {
            if (weeklyReserve is < 0 or > OrchestratorSettings.MaxWeeklyReservePercent) { AnsiConsole.MarkupLine($"[red]--weekly-reserve is 0 to {OrchestratorSettings.MaxWeeklyReservePercent}.[/]"); return 1; }
            s = s with { WeeklyReservePercent = weeklyReserve };
            changed = true;
        }
        if (settings.WeeklyTarget is not null && settings.WeeklyReserve is not null && settings.WeeklyTarget != 100 - settings.WeeklyReserve)
        {
            AnsiConsole.MarkupLine("[red]--weekly-target is 100 minus --weekly-reserve; give one of them.[/]");
            return 1;
        }
        if (settings.AlertTo is { } alertTo)
        {
            var off = alertTo.Trim().Length == 0 || alertTo.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);
            if (!off && !MindAttic.Orchestrator.Reminders.PsstClient.Addressable(alertTo, RemindCommand.ContactsPath))
            {
                AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(alertTo)}\" isn't a Psst contact or a US number, so Psst would refuse it. Add it with: psst contacts add <name> <phone>[/]");
                return 1;
            }
            s = s with { PsstAlertTo = off ? "" : alertTo.Trim() };
            changed = true;
        }
        if (settings.WeeklyTarget is not null || settings.WeeklyReserve is not null || settings.WeeklyLine is not null || settings.WeeklyWindDown is not null)
        {
            // The weekly and 5-hour cutoffs are per user, and org limit is the only way to change one (M.ORCH-T707).
            AnsiConsole.MarkupLine("[red]The weekly line is each member's own cutoff now (90% of the week, 98% of the 5-hour window by default): orchestrator org limit <who> --weekly <pct> --5h <pct>.[/]");
            return 1;
        }
        if (settings.AllocateSkip is { } skip)
        {
            s = s with { AllocateSkip = skip.Equals("none", StringComparison.OrdinalIgnoreCase) ? [] : [.. skip.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)] };
            changed = true;
        }
        if (settings.Exempt is { } exempt)
        {
            s = s with
            {
                ExemptCommands = exempt.Trim().ToLowerInvariant() switch
                {
                    "none" => [],
                    "default" => [.. OrchestratorSettings.DefaultExemptCommands],
                    _ => OrchestratorSettings.ParseCommands(exempt),
                },
            };
            changed = true;
        }
        foreach (var (opt, isCap) in settings.RepoCap.Select(o => (o, true)).Concat(settings.RepoSessions.Select(o => (o, false))))
        {
            var parts = opt.Split('=', 2);
            // A repo named, and a cap that's a share of the limit (over 100% it would never apply) (M.ORCH-T335).
            if (parts.Length != 2 || parts[0].Trim().Length == 0 || !int.TryParse(parts[1], out var n) || n < 0 || (isCap && n > 100))
            {
                AnsiConsole.MarkupLine($"[red]Use REPO={(isCap ? "PERCENT (0-100)" : "NUMBER")}, not \"{Markup.Escape(opt)}\".[/]");
                return 1;
            }
            parts[0] = parts[0].Trim();
            var map = new Dictionary<string, int>(isCap ? s.RepoBudgetPercent : s.RepoMaxSessions, StringComparer.Ordinal);
            // One entry per repo whatever the casing: "prose=0" clears a "Prose" cap, and "prose=1" replaces it.
            foreach (var same in map.Keys.Where(k => k.Equals(parts[0], StringComparison.OrdinalIgnoreCase)).ToList()) map.Remove(same);
            if (n != 0) map[parts[0]] = n;
            s = isCap ? s with { RepoBudgetPercent = map } : s with { RepoMaxSessions = map };
            changed = true;
        }
        // M.ORCH-T563: which model a repo's or a ticket's subagents run on, and when a Sonnet ticket's diff gets its Opus review.
        foreach (var opt in settings.Model)
        {
            var parts = opt.Split('=', 2);
            if (parts.Length != 2 || parts[0].Trim().Length == 0 || ModelChoice.Parse(parts[1]) is not { } model)
            {
                AnsiConsole.MarkupLine($"[red]Use REPO=MODEL or TICKET=MODEL with sonnet, opus or auto, not \"{Markup.Escape(opt)}\".[/]");
                return 1;
            }
            var key = parts[0].Trim();
            var map = new Dictionary<string, string>(s.ModelOverrides, StringComparer.Ordinal);
            foreach (var same in map.Keys.Where(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList()) map.Remove(same);
            if (model != ModelChoice.Auto) map[key] = model;
            s = s with { ModelOverrides = map };
            changed = true;
        }
        if (settings.ReviewLines is { } reviewLines)
        {
            if (reviewLines < -1) { AnsiConsole.MarkupLine("[red]--review-lines is -1 (none for size), 0 (every one) or more.[/]"); return 1; }
            s = s with { ReviewDiffLines = reviewLines };
            changed = true;
        }
        if (settings.RiskyFiles is { } risky)
        {
            s = s with
            {
                RiskyFiles = risky.Trim().ToLowerInvariant() switch
                {
                    "none" => [],
                    "default" => [.. OrchestratorSettings.DefaultRiskyFiles],
                    _ => [.. risky.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct()],
                },
            };
            changed = true;
        }
        changed |= settings.MaxSessions is not null || settings.Allocate is not null || settings.Provider is not null || settings.StaleHours is not null;
        if (changed && !readable)
        {
            AnsiConsole.MarkupLine($"[red]The settings in {Markup.Escape(store.Describe(JsonStore.SettingsName))} couldn't be read, so nothing was saved over them. Fix or remove {Markup.Escape(Path.Combine(store.Directory, "settings.json"))} if it's still there, and try again.[/]");
            return 1;
        }
        if (changed) store.SaveSettings(s);
        // Only once it's saved: a later option refused, or a settings.json that can't be written over, saves nothing.
        if (settings.Notify is not null && s.NotifyUrl is { } url &&
            new Notifier().SendAsync(url, "MindAttic Orchestrator", "Alerts will arrive here.").GetAwaiter().GetResult())
            AnsiConsole.MarkupLine("[green]Sent a test alert.[/]");

        var clis = new MindAttic.Orchestrator.Sessions.ContextCycler().AllocateClis();
        AnsiConsole.MarkupLine($"settings: {Markup.Escape(store.Describe(JsonStore.SettingsName))}");
        AnsiConsole.MarkupLine($"  max sessions {s.MaxConcurrentSessions} · provider {Markup.Escape(s.DefaultProvider)} · allocate " +
            (clis.Count == 0 ? "off in every CLI (type /allocate in the CLI that should work tickets)"
                : (s.Allocate ? "[green]on[/] in " : "[yellow]paused[/] (--allocate true resumes it) in ") +
                  Markup.Escape(string.Join("; ", clis.Select(c => $"CLI {c.Key} for {string.Join(", ", c.Value)}")))) +
            " · a new CLI starts with it off until /allocate is typed there" +
            (s.KeepGoing ? " · /allocate CLIs keep going through their workable tickets without asking" : " · /allocate CLIs may stop between tickets") +
            (s.ResumeAfterReset ? " · a CLI stopped for a window is told to carry on once it resets" : " · you're alerted when a CLI stopped for a window could carry on after its reset") +
            (s.OrphanRedispatches > 0 ? $" · a ticket whose CLI or tab exited mid-task is orphaned after {s.OrphanQuietMinutes} quiet min and redispatched in its worktree, up to {s.OrphanRedispatches} times"
                : $" · a ticket whose CLI or tab exited mid-task is shown as orphaned after {s.OrphanQuietMinutes} quiet min, not redispatched") +
            (s.AutoLogin ? " · a new Claude tab goes on a free per-CLI login (one CLI per account), else the global login" : " · a new Claude tab uses the global login unless given --account") +
            (s.RebalanceLogins ? " · a CLI on a login that runs low moves to a free login with more headroom" : " · a CLI stays on its login when it runs low") +
            " · a CLI whose login runs out waits for its reset (API billing is only ever switched on by you)" +
            " · CLIs open in Windows Terminal, a window per person" +
            $" · a busy person tab's play arrow blinks once per {s.TabBlinkTokens:N0} tokens");
        AnsiConsole.MarkupLine($"  5h budget {(s.BlockBudget is { } b ? Formatting.Tokens(b) : "none (Claude's report, else learned from limit hits)")} · weekly {(s.WeeklyBudget is { } w ? Formatting.Tokens(w) : "none (Claude's report, else learned)")} · week resets {(WeeklyReset.TryParse(s.WeeklyReset, out var r) && r is { } wr ? wr.Display + " unless Claude reports another" : "as Claude reports, else learned, else rolling 7 days")} · warn at {s.WarnPercent}%" +
            $" · weekly reserve {s.WeeklyReservePercent}% (each account's CLIs stop at {s.WeeklyStopPercent}% of its week) · no weekly-aware pacing, ever");
        AnsiConsole.MarkupLine($"  pacing {(s.Pacing ? $"[green]on[/], aiming for {s.PaceTargetPercent}% of each limit by its reset" : "off: the warn line holds new work")}" +
            " · running agents never held" +
            $" · subagents: as many as the pace allows, at most {s.MaxSubagents}" +
            (s.AgentAbort ? $" · cool-off abort [green]on[/]: {s.AgentAbortAfterMinutes} min past the line before aborting subagents to bend it back under"
                : " · cool-off abort off: the new-start cool-off only slows new subagents"));
        AnsiConsole.MarkupLine("  cutoff alerts " +
            (!string.IsNullOrWhiteSpace(s.PsstAlertTo) ? $"text {Markup.Escape(s.PsstAlertTo!)} through Psst (--alert-to)" : "text nobody (--alert-to)"));
        AnsiConsole.MarkupLine("  exempt from pacing (their turns skip every gate, spend still counted): " +
            (s.ExemptCommands.Count > 0 ? Markup.Escape(string.Join(", ", s.ExemptCommands)) : "none"));
        AnsiConsole.MarkupLine($"  worktrees under {Markup.Escape(s.WorktreeRoot)} · " +
            (s.StaleAfterHours > 0 ? $"assignments go stale after {s.StaleAfterHours}h quiet" : "assignments never go stale") + " · " +
            (s.DiskWarnGb > 0 ? $"alerts under {s.DiskWarnGb:0.#} GB free disk (urgent under {Math.Min(s.DiskCriticalGb, s.DiskWarnGb):0.#} GB)" : "disk space not watched"));
        AnsiConsole.MarkupLine($"  quiet hours {(s.QuietHours.Count > 0 ? string.Join(", ", s.QuietHours) : "none")} · {s.MaxSessionsPerRepo} sessions per repo" +
            Markup.Escape(string.Concat(s.RepoMaxSessions.Select(kv => $", {kv.Key} {kv.Value}"))) +
            (s.RepoBudgetPercent.Count > 0 ? " · repo caps " + Markup.Escape(string.Join(", ", s.RepoBudgetPercent.Select(kv => $"{kv.Key} {kv.Value}%"))) : "") +
            $" · groom after {s.GroomAfterDays} days");
        AnsiConsole.MarkupLine("  models: Sonnet for fixes, failed tests and tightly specified changes, Opus for features, design and wide changes" +
            (s.ModelOverrides.Count > 0 ? " · overrides " + Markup.Escape(string.Join(", ", s.ModelOverrides.Select(kv => $"{kv.Key} {kv.Value}"))) : "") +
            " · a Sonnet diff gets one Opus review " + (s.ReviewDiffLines >= 0 ? $"past {s.ReviewDiffLines} lines or " : "") +
            (s.RiskyFiles.Count > 0 ? $"touching {s.RiskyFiles.Count} risky file patterns" : "never for files (no risky files set)"));
        return 0;
    }
}
