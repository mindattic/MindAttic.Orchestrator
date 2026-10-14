using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("orchestrator");
    config.AddCommand<StatusCommand>("status").WithDescription("Every repo: state, live sessions, open tickets, tokens today.");
    config.AddCommand<StartCommand>("start").WithDescription("Open an agent tab for a repo through the Launcher.");
    config.AddCommand<SendCommand>("send").WithDescription("Type text into a live Launcher-hosted session.");
    config.AddCommand<BudgetCommand>("budget").WithDescription("Token budget for the current 5-hour block and the week; --block/--weekly/--warn set limits.");
    config.AddCommand<QueueCommand>("queue").WithDescription("Every open ticket across the workspace, in the order the scheduler would start them.");
    config.AddCommand<BumpCommand>("bump").WithDescription("Raise (or with a negative --by, lower) a ticket's priority.");
    config.AddCommand<DispatchCommand>("dispatch").WithDescription("Start a session on a ticket (a worktree when its repo is busy).");
    config.AddCommand<ReleaseCommand>("release").WithDescription("Return a ticket to the queue; --remove-worktree also deletes its clean worktree.");
    config.AddCommand<DaemonCommand>("daemon").WithDescription("Keep the budget file fresh and, when auto-dispatch is on, start proposals.");
    config.AddCommand<SettingsCommand>("settings").WithDescription("Show or change scheduler settings.");
    config.AddCommand<AuditCommand>("audit").WithDescription("Find drift in shared agent files and code duplicated across repos.");
});
return app.Run(args);

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
        var snapshot = new StatusAggregator(new OrchestratorOptions(), rateLimitFile: new RateLimitStatusWriter()).Refresh();
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
        if (snapshot.Budget is { } b) AnsiConsole.MarkupLine(BudgetCommand.Line(b));

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
        var options = new OrchestratorOptions();
        var repo = new MindAttic.Workflow.Workspace(options.WorkspaceRoot).Find(settings.Repo);
        if (repo is null) { AnsiConsole.MarkupLine($"[red]No repo named {Markup.Escape(settings.Repo)}.[/]"); return 1; }
        var error = new SessionControl(options).Start(repo.Name, repo.Path, settings.Provider, settings.Prompt);
        if (error is not null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]"); return 1; }
        AnsiConsole.MarkupLine($"Started [bold]{settings.Provider}[/] in {Markup.Escape(repo.Name)}.");
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
        var snapshot = new StatusAggregator(new OrchestratorOptions()).Refresh();
        var repo = snapshot.Repos.FirstOrDefault(r => r.Name.Equals(settings.Repo, StringComparison.OrdinalIgnoreCase));
        if (repo is null || repo.Hosts.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No Launcher-hosted session for {Markup.Escape(settings.Repo)}.[/]");
            return 1;
        }
        var sent = 0;
        foreach (var h in repo.Hosts)
            if (await SessionControl.SendAsync(h.PipeName, settings.Text, !settings.NoSubmit)) sent++;
        AnsiConsole.MarkupLine($"Delivered to {sent} of {repo.Hosts.Count} session(s).");
        return sent > 0 ? 0 : 1;
    }
}

internal sealed class BudgetCommand : Command<BudgetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--block <TOKENS>")]
        [Description("Uncached tokens per 5-hour block (0 clears it: measure against your peak block).")]
        public long? Block { get; init; }

        [CommandOption("--weekly <TOKENS>")]
        [Description("Uncached tokens per week (0 clears it).")]
        public long? Weekly { get; init; }

        [CommandOption("--weekly-reset <WHEN>")]
        [Description("When the week resets, local time: \"Mon 9:00\", \"Tue 5pm\". \"none\" makes the week a rolling 7 days.")]
        public string? WeeklyReset { get; init; }

        [CommandOption("--warn <PERCENT>")]
        public int? Warn { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = new JsonStore();
        WeeklyReset? reset = null;
        if (settings.WeeklyReset is not null && !WeeklyReset.TryParse(settings.WeeklyReset, out reset))
        {
            AnsiConsole.MarkupLine($"[red]Couldn't read \"{Markup.Escape(settings.WeeklyReset)}\"; use a day and a time, like \"Mon 9:00\" or \"Tue 5pm\".[/]");
            return 1;
        }
        if (settings.Block is not null || settings.Weekly is not null || settings.Warn is not null || settings.WeeklyReset is not null)
        {
            var s = store.LoadSettings();
            store.SaveSettings(s with
            {
                BlockBudget = settings.Block is { } bl ? (bl > 0 ? bl : null) : s.BlockBudget,
                WeeklyBudget = settings.Weekly is { } w ? (w > 0 ? w : null) : s.WeeklyBudget,
                WarnPercent = settings.Warn ?? s.WarnPercent,
                WeeklyReset = settings.WeeklyReset is null ? s.WeeklyReset : reset?.ToString(),
            });
        }

        var b = new StatusAggregator(new OrchestratorOptions(), store, new RateLimitStatusWriter()).Refresh().Budget!;
        AnsiConsole.MarkupLine(Line(b));
        if (b.CurrentBlock is { } block)
            AnsiConsole.MarkupLine($"  block {block.StartUtc.ToLocalTime():t}–{block.EndUtc.ToLocalTime():t} · burning {Format.Tokens((long)b.FreshPerHour)}/h" +
                (b.ProjectedLimitUtc is { } p ? $" · [red]limit at this pace: {p.ToLocalTime():t}[/]" : ""));
        AnsiConsole.MarkupLine($"  today {Format.Tokens(b.Today.Fresh)} uncached ({Format.Tokens(b.Today.CacheRead)} cache reads)");
        AnsiConsole.MarkupLine($"  {WeekLine(b)}");
        return 0;
    }

    public static string WeekLine(BudgetStatus b)
    {
        var used = $"{Format.Tokens(b.Week.Fresh)} uncached" + (b.WeeklyPercent is { } wp ? $" ({wp:0}% of weekly)" : "");
        return b.WeekResetUtc is { } next
            ? $"this week (since {b.WeekStartUtc.ToLocalTime():ddd h:mm tt}) {used} · resets {next.ToLocalTime():ddd h:mm tt}"
            : $"last 7 days {used} [grey]· rolling: set the real reset with --weekly-reset \"Mon 9:00\"[/]";
    }

    public static string Line(BudgetStatus b)
    {
        if (b.CurrentBlock is not { } block) return "[grey]budget: no active 5-hour block[/]";
        var used = $"{Format.Tokens(block.Usage.Fresh)} uncached this block, resets {block.EndUtc.ToLocalTime():t}";
        if (b.BlockLimitIsPeak)
            return $"[aqua]this block is at {b.BlockPercent:0}% of your heaviest block in 7 days[/] ({used}) [grey]· no budget set: `orchestrator budget --block <tokens>`[/]";
        var color = b.Level switch { BudgetLevel.Over => "red", BudgetLevel.Warn => "yellow", _ => "green" };
        return $"[{color}]budget: {b.BlockPercent:0}% of your 5h budget[/] ({Format.Tokens(block.Usage.Fresh)} of {Format.Tokens(b.BlockLimit)}; {used})";
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
