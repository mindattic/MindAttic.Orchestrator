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
    public static (StatusAggregator Aggregator, JsonStore Store, Snapshot Snapshot, SchedulePlan Plan) Load()
    {
        var store = new JsonStore();
        var aggregator = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Lease(store));
        var snapshot = aggregator.Refresh();
        var plan = Scheduler.Plan(snapshot, store.LoadSettings(), store.LoadAssignments());
        return (aggregator, store, snapshot, plan);
    }

    /// <summary>The status lease the daemon and dashboards share: a one-off command only writes rl-status.json while
    /// nobody else holds it, so it never overwrites the running daemon's pace. Released when the command exits.</summary>
    public static DispatchLease Lease(JsonStore store) => new(store.Directory);

    /// <summary>A ticket id as stored and compared everywhere (upper case), so "m.orch-t5" isn't silently a different ticket.</summary>
    public static string Id(string typed) => typed.Trim().ToUpperInvariant();

    public static OrchestratorSettings Settings => new JsonStore().LoadSettings();

    public static Dispatcher Dispatcher(JsonStore store) =>
        new(new SessionControl(OrchestratorOptions.Load()), store);
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
            AnsiConsole.MarkupLine($"[yellow]too big:[/] {q.Id} is {Markup.Escape(q.Estimate!.ToString())}, more than the {Formatting.Tokens((long)(plan.Pace?.RoomTokens ?? 0))} left before the reset: split it, or it waits for the reset");
        foreach (var q in plan.Stale)
            AnsiConsole.MarkupLine($"[yellow]stale:[/] {q.Id} was dispatched {Formatting.Ago(q.Assignment!.StartedUtc, DateTime.UtcNow)} but nothing has happened in {Markup.Escape(q.Assignment.Directory)} since {Formatting.Ago(q.StaleSinceUtc, DateTime.UtcNow)}; it's back in the queue");
        foreach (var f in plan.Finished)
            AnsiConsole.MarkupLine($"[aqua]done:[/] {f.TicketId} is closed{(f.Worktree ? $"; worktree {Markup.Escape(f.Directory)} can go: orchestrator release {f.TicketId} --remove-worktree" : "")}");

        var table = new Table().Border(TableBorder.Rounded).AddColumns("#", "Ticket", "Repo", "Type", "Pri", "Opened", "Summary", "");
        var i = 0;
        foreach (var q in (settings.Groom ? plan.Groom : plan.Queue).Take(settings.Top))
        {
            var note = q.BlockedBy.Count > 0 ? $"[grey]waits for {string.Join(", ", q.BlockedBy)}[/]"
                : q.Reserved > 0 && !q.IsAssigned ? $"[aqua]{Formatting.Tokens(q.Reserved)} reserved[/]"
                : q.TooBig ? "[yellow]too big for now[/]" : q.IsStale ? $"[yellow]stale since {Formatting.Ago(q.StaleSinceUtc, DateTime.UtcNow)}[/]"
                : q.IsAssigned ? $"[aqua]assigned {Formatting.Ago(q.Assignment!.StartedUtc, DateTime.UtcNow)}[/]"
                : plan.Proposals.Any(p => p.Item == q) ? "[green]next[/]" : "";
            table.AddRow((++i).ToString(), q.Id, Markup.Escape(q.Repo), q.Ticket.Type, q.Priority == 0 ? "" : q.Priority.ToString("+#;-#"),
                q.Ticket.Date, Markup.Escape(Trim(q.Ticket.Summary, 60)), note);
        }
        AnsiConsole.Write(table);
        return 0;
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}

internal sealed class BumpCommand : Command<BumpCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--by <N>")]
        [DefaultValue(1)]
        public int By { get; init; } = 1;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = new JsonStore();
        var s = store.LoadSettings();
        var id = Ctx.Id(settings.Ticket);
        var priorities = new Dictionary<string, int>(s.Priorities, StringComparer.Ordinal);
        var next = priorities.GetValueOrDefault(id) + settings.By;
        if (next == 0) priorities.Remove(id); else priorities[id] = next;
        store.SaveSettings(s with { Priorities = priorities });
        AnsiConsole.MarkupLine($"{Markup.Escape(id)} priority is now {next:+#;-#;0}.");
        return 0;
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
        if (item is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(settings.Ticket)} isn't an open ticket.[/]"); return 1; }
        if (item.IsAssigned) { AnsiConsole.MarkupLine($"[yellow]{item.Id} is already assigned; release it first.[/]"); return 1; }

        var r = Ctx.Dispatcher(store).Dispatch(item, settings.Provider ?? store.LoadSettings().DefaultProvider, settings.Worktree || item.RepoBusy);
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
        var store = new JsonStore();
        var a = store.LoadAssignments().FirstOrDefault(x => x.TicketId.Equals(settings.Ticket, StringComparison.OrdinalIgnoreCase));
        if (a is null) { AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(settings.Ticket)} isn't assigned.[/]"); return 1; }
        var dispatcher = Ctx.Dispatcher(store);
        if (!settings.RemoveWorktree)
        {
            dispatcher.Release(a.TicketId);
            AnsiConsole.MarkupLine($"{a.TicketId} is back in the queue.");
            return 0;
        }
        var repo = new MindAttic.Workflow.Workspace(OrchestratorOptions.Load().WorkspaceRoot).Find(a.Repo);
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
        AnsiConsole.MarkupLine($"[grey]Ctrl+C to stop. Log: {Markup.Escape(OrchestratorLog.Directory)}[/]");
        try
        {
            new Daemon(new JsonStore(), (level, line) =>
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

        [CommandOption("--autopilot <ON>")]
        [Description("false pauses every CLI where /autopilot is on (their marks stay); true lets them go again.")]
        public bool? Autopilot { get; init; }

        [CommandOption("--throttle-agents <ON>")]
        [Description("true: while the burn won't last until the reset, running Claude agents wait before each tool call (up to --max-delay seconds).")]
        public bool? ThrottleAgents { get; init; }

        [CommandOption("--max-delay <SECONDS>")]
        public int? MaxDelay { get; init; }

        [CommandOption("--max-subagents <N>")]
        [Description("The most subagents running at once across every CLI; under it the pace decides, live (0 holds every new one).")]
        public int? MaxSubagents { get; init; }

        [CommandOption("--provider <PROVIDER>")]
        public string? Provider { get; init; }

        [CommandOption("--quiet <WINDOWS>")]
        [Description("Local times autopilot pauses, comma-separated: \"22:00-07:00,12:00-13:00\"; \"none\" clears.")]
        public string? Quiet { get; init; }

        [CommandOption("--repo-cap <REPO=PERCENT>")]
        [Description("Most of the 5-hour limit one repo may spend per block, like Prose=40 (0 clears). Repeatable.")]
        public string[] RepoCap { get; init; } = [];

        [CommandOption("--repo-sessions <REPO=N>")]
        [Description("Sessions one repo may run at once, like Prose=3 (0 clears). Repeatable.")]
        public string[] RepoSessions { get; init; } = [];

        [CommandOption("--keep-going <ON>")]
        [Description("true (default): a CLI that ends its turn asking whether to go on while its repo has open tickets is told to carry on; false lets it ask.")]
        public bool? KeepGoing { get; init; }

        [CommandOption("--hand-minutes <N>")]
        [Description("Minutes after a repo's own CLI last ended a turn that autopilot still leaves the repo to you (default 15; 0: only while mid-turn).")]
        public int? HandMinutes { get; init; }

        [CommandOption("--autopilot-skip <REPOS>")]
        [Description("Repos /autopilot never starts agents in, comma-separated: \"GTFO.EZ,Prose\"; \"none\" clears.")]
        public string? AutopilotSkip { get; init; }

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
    }

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
        var store = new JsonStore();
        var s = store.LoadSettings();
        s = s with
        {
            MaxConcurrentSessions = settings.MaxSessions ?? s.MaxConcurrentSessions,
            Autopilot = settings.Autopilot ?? s.Autopilot,
            DefaultProvider = provider ?? s.DefaultProvider,
            StaleAfterHours = settings.StaleHours ?? s.StaleAfterHours,
            GroomAfterDays = settings.GroomDays ?? s.GroomAfterDays,
        };
        if (settings.MaxDelay is < 0 or > MindAttic.Orchestrator.Setup.AgentGate.MaxHoldSeconds) { AnsiConsole.MarkupLine($"[red]--max-delay is 0 to {MindAttic.Orchestrator.Setup.AgentGate.MaxHoldSeconds}.[/]"); return 1; }
        s = s with
        {
            ThrottleAgents = settings.ThrottleAgents ?? s.ThrottleAgents,
            MaxAgentDelaySeconds = settings.MaxDelay ?? s.MaxAgentDelaySeconds,
            MaxSubagents = settings.MaxSubagents ?? s.MaxSubagents,
        };
        if (settings.MaxSubagents is < 0) { AnsiConsole.MarkupLine("[red]--max-subagents can't be negative.[/]"); return 1; }
        var changed = settings.GroomDays is not null || settings.GitHubIntake is not null || settings.ThrottleAgents is not null || settings.MaxDelay is not null || settings.MaxSubagents is not null;
        if (settings.GitHubIntake is { } intake) s = s with { GitHubIntake = intake };
        if (settings.Notify is { } notify)
        {
            var off = notify.Equals("none", StringComparison.OrdinalIgnoreCase);
            if (!off && !(Uri.TryCreate(notify, UriKind.Absolute, out var u) && u.Scheme is "https" or "http")) { AnsiConsole.MarkupLine("[red]--notify takes an http(s) URL, or none.[/]"); return 1; }
            s = s with { NotifyUrl = off ? null : notify };
            changed = true;
            if (!off && new Notifier().SendAsync(notify, "MindAttic Orchestrator", "Alerts will arrive here.").GetAwaiter().GetResult())
                AnsiConsole.MarkupLine("[green]Sent a test alert.[/]");
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
            s = s with { AutopilotHandMinutes = hand };
            changed = true;
        }
        if (settings.KeepGoing is { } keep)
        {
            s = s with { KeepGoing = keep };
            changed = true;
        }
        if (settings.AutopilotSkip is { } skip)
        {
            s = s with { AutopilotSkip = skip.Equals("none", StringComparison.OrdinalIgnoreCase) ? [] : [.. skip.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)] };
            changed = true;
        }
        foreach (var (opt, isCap) in settings.RepoCap.Select(o => (o, true)).Concat(settings.RepoSessions.Select(o => (o, false))))
        {
            var parts = opt.Split('=', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var n) || n < 0) { AnsiConsole.MarkupLine($"[red]Use REPO=NUMBER, not \"{Markup.Escape(opt)}\".[/]"); return 1; }
            var map = new Dictionary<string, int>(isCap ? s.RepoBudgetPercent : s.RepoMaxSessions, StringComparer.Ordinal);
            if (n == 0) map.Remove(parts[0]); else map[parts[0]] = n;
            s = isCap ? s with { RepoBudgetPercent = map } : s with { RepoMaxSessions = map };
            changed = true;
        }
        if (changed) store.SaveSettings(s);
        if (settings.MaxSessions is not null || settings.Autopilot is not null || settings.Provider is not null || settings.StaleHours is not null)
            store.SaveSettings(s);

        var clis = new MindAttic.Orchestrator.Sessions.ContextCycler().AutopilotClis();
        AnsiConsole.MarkupLine($"settings: {Markup.Escape(Path.Combine(store.Directory, "settings.json"))}");
        AnsiConsole.MarkupLine($"  max sessions {s.MaxConcurrentSessions} · provider {Markup.Escape(s.DefaultProvider)} · autopilot " +
            (clis.Count == 0 ? "off in every CLI (type /autopilot in the CLI that should work tickets)"
                : "[green]on[/] in " + Markup.Escape(string.Join("; ", clis.Select(c => $"CLI {c.Key} for {string.Join(", ", c.Value)}")))) +
            $", leaving a repo to you {(s.AutopilotHandMinutes > 0 ? $"until its CLI has been idle {s.AutopilotHandMinutes} min" : "only while its CLI is mid-turn")}" +
            (s.KeepGoing ? " · CLIs keep going through their open tickets without asking" : " · CLIs may stop to ask between tickets"));
        AnsiConsole.MarkupLine($"  5h budget {(s.BlockBudget is { } b ? Formatting.Tokens(b) : "none (learned from limit hits)")} · weekly {(s.WeeklyBudget is { } w ? Formatting.Tokens(w) : "none (learned)")} · week resets {(WeeklyReset.TryParse(s.WeeklyReset, out var r) && r is { } wr ? wr.Display : "learned, or rolling 7 days")} · warn at {s.WarnPercent}%");
        AnsiConsole.MarkupLine($"  pacing {(s.Pacing ? $"[green]on[/], aiming for {s.PaceTargetPercent}% of each limit by its reset" : "off: the warn line holds new work")}" +
            (s.ThrottleAgents ? $" · running agents held up to {s.MaxAgentDelaySeconds}s per tool call when the burn won't last" : " · running agents never held") +
            $" · subagents: as many as the pace allows, at most {s.MaxSubagents}");
        AnsiConsole.MarkupLine($"  worktrees under {Markup.Escape(s.WorktreeRoot)} · {s.Priorities.Count} manual priorities · " +
            (s.StaleAfterHours > 0 ? $"assignments go stale after {s.StaleAfterHours}h quiet" : "assignments never go stale"));
        AnsiConsole.MarkupLine($"  quiet hours {(s.QuietHours.Count > 0 ? string.Join(", ", s.QuietHours) : "none")} · {s.MaxSessionsPerRepo} sessions per repo" +
            Markup.Escape(string.Concat(s.RepoMaxSessions.Select(kv => $", {kv.Key} {kv.Value}"))) +
            (s.RepoBudgetPercent.Count > 0 ? " · repo caps " + Markup.Escape(string.Join(", ", s.RepoBudgetPercent.Select(kv => $"{kv.Key} {kv.Value}%"))) : "") +
            (s.Reservations.Count > 0 ? " · reserved " + Markup.Escape(string.Join(", ", s.Reservations.Select(kv => $"{kv.Key} {Formatting.Tokens(kv.Value)}"))) : "") +
            $" · groom after {s.GroomAfterDays} days");
        return 0;
    }
}
