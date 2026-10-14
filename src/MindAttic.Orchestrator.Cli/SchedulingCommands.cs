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
        var aggregator = new StatusAggregator(new OrchestratorOptions(), store, new RateLimitStatusWriter());
        var snapshot = aggregator.Refresh();
        var plan = Scheduler.Plan(snapshot, store.LoadSettings(), store.LoadAssignments());
        return (aggregator, store, snapshot, plan);
    }

    public static Dispatcher Dispatcher(JsonStore store) =>
        new(new SessionControl(new OrchestratorOptions()), store);
}

internal sealed class QueueCommand : Command<QueueCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--top <N>")]
        [DefaultValue(25)]
        public int Top { get; init; } = 25;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (_, _, _, plan) = Ctx.Load();
        AnsiConsole.MarkupLine($"{plan.Queue.Count} open tickets · {plan.Occupied} of {plan.Slots} session slots in use");
        if (plan.Hold is not null) AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(plan.Hold)}[/]");
        foreach (var p in plan.Proposals)
            AnsiConsole.MarkupLine($"[green]next:[/] {p.Item.Id} in {Markup.Escape(p.Item.Repo)} [grey]({Markup.Escape(p.Reason)})[/]  → orchestrator dispatch {p.Item.Id}");
        foreach (var f in plan.Finished)
            AnsiConsole.MarkupLine($"[aqua]done:[/] {f.TicketId} is closed{(f.Worktree ? $"; worktree {Markup.Escape(f.Directory)} can go: orchestrator release {f.TicketId} --remove-worktree" : "")}");

        var table = new Table().Border(TableBorder.Rounded).AddColumns("#", "Ticket", "Repo", "Type", "Pri", "Opened", "Summary", "");
        var i = 0;
        foreach (var q in plan.Queue.Take(settings.Top))
        {
            var note = q.IsAssigned ? $"[aqua]assigned {Formatting.Ago(q.Assignment!.StartedUtc, DateTime.UtcNow)}[/]"
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
        var priorities = new Dictionary<string, int>(s.Priorities, StringComparer.Ordinal);
        var next = priorities.GetValueOrDefault(settings.Ticket) + settings.By;
        if (next == 0) priorities.Remove(settings.Ticket); else priorities[settings.Ticket] = next;
        store.SaveSettings(s with { Priorities = priorities });
        AnsiConsole.MarkupLine($"{settings.Ticket} priority is now {next:+#;-#;0}.");
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
        var repo = new MindAttic.Workflow.Workspace(new OrchestratorOptions().WorkspaceRoot).Find(a.Repo);
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
        var store = new JsonStore();
        var aggregator = new StatusAggregator(new OrchestratorOptions(), store, new RateLimitStatusWriter());
        var auto = new AutoDispatch(Ctx.Dispatcher(store));
        AnsiConsole.MarkupLine($"[grey]orchestrator daemon: every {settings.Interval}s; Ctrl+C to stop.[/]");
        string? lastHold = null;
        while (true)
        {
            var snapshot = aggregator.Refresh();
            var s = store.LoadSettings();
            var plan = Scheduler.Plan(snapshot, s, store.LoadAssignments());
            if (plan.Hold != lastHold && plan.Hold is not null)
                AnsiConsole.MarkupLine($"[grey]{DateTime.Now:T}[/] [yellow]{Markup.Escape(plan.Hold)}[/]");
            lastHold = plan.Hold;
            if (auto.Tick(plan, s, DateTime.UtcNow) is { } r)
                AnsiConsole.MarkupLine($"[grey]{DateTime.Now:T}[/] {(r.Ok ? "" : "[red]")}{Markup.Escape(r.Message)}{(r.Ok ? "" : "[/]")}");
            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(5, settings.Interval)));
        }
    }
}

internal sealed class SettingsCommand : Command<SettingsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--max-sessions <N>")]
        public int? MaxSessions { get; init; }

        [CommandOption("--auto-dispatch <ON>")]
        [Description("true or false.")]
        public bool? AutoDispatch { get; init; }

        [CommandOption("--provider <PROVIDER>")]
        public string? Provider { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = new JsonStore();
        var s = store.LoadSettings();
        s = s with
        {
            MaxConcurrentSessions = settings.MaxSessions ?? s.MaxConcurrentSessions,
            AutoDispatch = settings.AutoDispatch ?? s.AutoDispatch,
            DefaultProvider = settings.Provider ?? s.DefaultProvider,
        };
        if (settings.MaxSessions is not null || settings.AutoDispatch is not null || settings.Provider is not null) store.SaveSettings(s);

        AnsiConsole.MarkupLine($"settings: {Markup.Escape(Path.Combine(store.Directory, "settings.json"))}");
        AnsiConsole.MarkupLine($"  max sessions {s.MaxConcurrentSessions} · auto-dispatch {(s.AutoDispatch ? "[green]on[/]" : "off")} · provider {s.DefaultProvider}");
        AnsiConsole.MarkupLine($"  5h budget {(s.BlockBudget is { } b ? Formatting.Tokens(b) : "none (peak-relative)")} · weekly {(s.WeeklyBudget is { } w ? Formatting.Tokens(w) : "none")} · week resets {(WeeklyReset.TryParse(s.WeeklyReset, out var r) && r is { } wr ? wr.Display : "rolling 7 days")} · warn at {s.WarnPercent}%");
        AnsiConsole.MarkupLine($"  worktrees under {Markup.Escape(s.WorktreeRoot)} · {s.Priorities.Count} manual priorities");
        return 0;
    }
}
