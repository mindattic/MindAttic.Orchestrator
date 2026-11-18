using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Data;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>What the <c>orchestrator milestone</c> commands share: the ticket database, the check runner and how a check prints.</summary>
internal static class MilestoneCli
{
    /// <summary>Runs <paramref name="work"/> on the workspace's ticket database; 1 with the reason when it can't be opened or refuses.</summary>
    public static int WithTickets(Func<TicketsDb, int> work)
    {
        try
        {
            using var db = TicketsDb.OpenIfMigrated(OrchestratorOptions.Load().WorkspaceRoot);
            if (db is null)
            {
                AnsiConsole.MarkupLine("[red]There is no ticket database in this workspace, so there are no milestones.[/]");
                return 1;
            }
            return work(db);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or System.Data.Common.DbException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }

    /// <summary>The runner as the owner's own command uses it: main lines fetched first, and no build while RAM is low, the
    /// disk is under its floor or the heavy-process lease is taken.</summary>
    public static MilestoneAcceptance Runner()
    {
        var store = Ctx.Store();
        var s = store.LoadSettings();
        var workspace = new MindAttic.Tickets.Workspace(OrchestratorOptions.Load().WorkspaceRoot);
        return new MilestoneAcceptance(store)
        {
            RepoPath = name => workspace.Find(name)?.Path,
            WorkspaceRoot = workspace.Root,
            WorktreeRoot = s.WorktreeRoot,
            Timeout = MilestoneAcceptance.TimeoutOf(s),
            Fetch = true,
            Held = () => (MemoryGuard.Shared.Observe(s, DateTime.UtcNow) is { Level: > MemoryLevel.Ok } m ? $"RAM is low ({m.Reading.Describe()})" : null)
                ?? DiskGuard.StartHeld(s)
                ?? MilestoneAcceptance.HeavyHeld(store.Db, s.HeavyProcessLimit, s.HeavyProcessGraceSeconds, DateTime.UtcNow),
        };
    }

    /// <summary>A projected date as the owner reads it: the local day.</summary>
    public static string Day(DateTime utc) => utc.ToLocalTime().ToString("ddd yyyy-MM-dd", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Each active and accepting milestone's scope by its ID (M.ORCH-T1685); empty when the database can't say (one
    /// from before the scope lock), so a list still prints without its growth.</summary>
    public static IReadOnlyDictionary<string, MilestoneScope> Scopes(TicketsDb db)
    {
        try { return MilestoneScope.Read(db).ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException or System.Data.Common.DbException)
        {
            return new Dictionary<string, MilestoneScope>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static object Json(MilestoneAcceptance.Checked c) => new
    {
        number = c.Check.Number, kind = c.Check.Kind, description = c.Check.Description, repos = c.Check.Repos, target = c.Check.Target,
        result = c.Check.Result, ran = c.Outcome.Result is not null, recorded = c.Recorded, evidence = c.Outcome.Evidence,
    };

    public static void Print(Milestone m, IReadOnlyList<MilestoneAcceptance.Checked> checks)
    {
        AnsiConsole.MarkupLine($"[aqua]{Markup.Escape(m.Id)}[/] {Markup.Escape(m.Name)} ({Markup.Escape(m.State)})");
        if (checks.Count == 0) AnsiConsole.MarkupLine("  [grey]It has no acceptance checks.[/]");
        foreach (var c in checks)
        {
            var colour = c.Check.Result switch { MilestoneCheckResults.Pass => "green", MilestoneCheckResults.Fail => "red", _ => "yellow" };
            AnsiConsole.MarkupLine($"  #{c.Check.Number} {Markup.Escape(c.Check.Kind)} [{colour}]{Markup.Escape(c.Check.Result)}[/]{(c.Outcome.Result is null ? " [grey](not run)[/]" : "")}: {Markup.Escape(c.Check.Description)}");
            AnsiConsole.MarkupLine($"     [grey]{Markup.Escape(c.Outcome.Evidence)}[/]");
        }
    }
}

/// <summary><c>orchestrator milestone [list] [--json]</c> (M.ORCH-T1684): the active and accepting milestones, each with its
/// tickets done of total, its acceptance checks passed of total, the date the daemon last projected for it (M.ORCH-T1688), and
/// how much it has grown since it was activated with what waits for approval (M.ORCH-T1685).</summary>
internal sealed class MilestoneListCommand : Command<MilestoneListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Print it as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => MilestoneCli.WithTickets(db =>
    {
        var open = db.Milestones().Where(m => m.State is MilestoneStates.Active or MilestoneStates.Accepting).ToList();
        if (open.Count == 0)
        {
            if (settings.Json) Console.WriteLine("[]");
            else AnsiConsole.MarkupLine("[grey]No milestone is active or in acceptance.[/]");
            return 0;
        }
        var graph = db.Graph();
        var forecasts = new MilestoneForecasts(Ctx.Store()).Records();
        var scopes = MilestoneCli.Scopes(db);
        var rows = open.Select(m =>
        {
            var (done, total) = TicketsDb.MilestoneProgress(db.MilestoneMembers(m.Id, graph));
            var checks = db.MilestoneChecks(m.Id);
            return (Milestone: m, Done: done, Total: total, Passed: checks.Count(c => c.Passed), Checks: checks.Count, Forecast: forecasts.GetValueOrDefault(m.Id), Scope: scopes.GetValueOrDefault(m.Id));
        }).ToList();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows.Select(r => new
            {
                id = r.Milestone.Id, name = r.Milestone.Name, state = r.Milestone.State, repos = r.Milestone.Repos,
                ticketsDone = r.Done, tickets = r.Total, checksPassed = r.Passed, checks = r.Checks,
                projected = r.Forecast?.LatestProjectedUtc, atRisk = r.Forecast?.AtRisk,
                growth = r.Scope is not { } g ? null : new
                {
                    atActivation = g.AtActivation, added = g.Added, removed = g.Removed, now = g.Now, grown = g.Grown,
                    waiting = g.Waiting.Count, waitingTickets = g.WaitingTickets,
                },
            }), BranchesCommand.Indented));
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Milestone", "Name", "State", "Tickets", "Checks", "Grown", "Projected", "Repos");
        foreach (var r in rows)
            table.AddRow(Markup.Escape(r.Milestone.Id), Markup.Escape(r.Milestone.Name), Markup.Escape(r.Milestone.State),
                $"{r.Done}/{r.Total} done", $"{r.Passed}/{r.Checks} passed", Markup.Escape(r.Scope?.GrownText ?? ""),
                (r.Forecast?.LatestProjectedUtc is { } at ? MilestoneCli.Day(at) : "[grey]unknown[/]") + (r.Forecast?.AtRisk == true ? " [red](at risk)[/]" : ""),
                Markup.Escape(string.Join(", ", r.Milestone.Repos)));
        AnsiConsole.Write(table);
        return 0;
    });
}

/// <summary><c>orchestrator milestone check [&lt;ID&gt;] [--json]</c> (M.ORCH-T1684): runs the build and exists checks of one
/// milestone, or of every active and accepting one, and records each result with its evidence. Harness and sign-off checks are
/// listed, not run. Exit code 1 when a check ran and failed.</summary>
internal sealed class MilestoneCheckCommand : Command<MilestoneCheckCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ID]")]
        [Description("The milestone (MS-n or its name); every active and accepting milestone when left out.")]
        public string? Id { get; init; }

        [CommandOption("--json")]
        [Description("Print the results as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => MilestoneCli.WithTickets(db =>
    {
        List<Milestone> chosen;
        if (settings.Id is { } id)
        {
            var m = db.Milestone(id) ?? throw new KeyNotFoundException($"There is no milestone {id}.");
            if (m.State is not (MilestoneStates.Active or MilestoneStates.Accepting))
                throw new InvalidOperationException($"{m.Id} is {m.State}; checks run only for an active or accepting milestone.");
            chosen = [m];
        }
        else chosen = [.. db.Milestones().Where(m => m.State is MilestoneStates.Active or MilestoneStates.Accepting)];
        if (chosen.Count == 0)
        {
            if (settings.Json) Console.WriteLine("[]");
            else AnsiConsole.MarkupLine("[grey]No milestone is active or in acceptance, so there is nothing to check.[/]");
            return 0;
        }
        var runner = MilestoneCli.Runner();
        var failed = false;
        var json = new List<object>();
        foreach (var m in chosen)
        {
            var checks = runner.Run(db, m);
            failed |= checks.Any(c => c.Outcome.Failed);
            if (settings.Json) json.Add(new { id = m.Id, name = m.Name, state = m.State, checks = checks.Select(MilestoneCli.Json) });
            else MilestoneCli.Print(m, checks);
        }
        if (settings.Json) Console.WriteLine(JsonSerializer.Serialize(json, BranchesCommand.Indented));
        return failed ? 1 : 0;
    });
}

/// <summary><c>orchestrator milestone accept &lt;ID&gt; --by &lt;WHO&gt; [--json]</c> (M.ORCH-T1684): the owner asking for every
/// check. An active milestone moves into acceptance; its build and exists checks run; its harness checks would run if anything
/// could run one (nothing can yet, so they stay unknown); its sign-off checks are recorded as passed by <c>--by</c>. Only when
/// every check has passed is the milestone reached; otherwise it stays in acceptance and what hasn't passed is listed. It never
/// forces: that stays <c>tickets milestone accept --force</c>.</summary>
internal sealed class MilestoneAcceptCommand : Command<MilestoneAcceptCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The milestone (MS-n or its name).")]
        public string Id { get; init; } = "";

        [CommandOption("--by <WHO>")]
        [Description("Who accepts it: recorded on its sign-off checks and on the milestone when it's reached.")]
        public string? By { get; init; }

        [CommandOption("--json")]
        [Description("Print the result as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.By))
        {
            AnsiConsole.MarkupLine("[red]Say who accepts the milestone: orchestrator milestone accept <ID> --by <who>.[/]");
            return 1;
        }
        return MilestoneCli.WithTickets(db =>
        {
            var result = MilestoneCli.Runner().Accept(db, settings.Id, settings.By);
            var harness = result.Unmet.Any(c => c.Check.Kind == MilestoneCheckKinds.Harness);
            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    id = result.Milestone.Id, name = result.Milestone.Name, state = result.Milestone.State, reached = result.Reached,
                    checks = result.Checks.Select(MilestoneCli.Json),
                }, BranchesCommand.Indented));
                return result.Reached ? 0 : 1;
            }
            MilestoneCli.Print(result.Milestone, result.Checks);
            if (result.Reached)
            {
                AnsiConsole.MarkupLine($"[green]{Markup.Escape(result.Milestone.Id)} is reached: every check passed. Accepted by {Markup.Escape(settings.By.Trim())}.[/]");
                return 0;
            }
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(result.Milestone.Id)} stays in acceptance: {result.Unmet.Count} of {result.Checks.Count} checks have not passed ({Markup.Escape(string.Join(", ", result.Unmet.Select(c => $"#{c.Check.Number} {c.Check.Kind} {c.Check.Result}")))}).[/]");
            if (harness)
                AnsiConsole.MarkupLine("[yellow]A harness check can't be run here: there is no harness runner yet. Record its result by hand with tickets milestone, then accept again.[/]");
            return 1;
        });
    }
}

/// <summary><c>orchestrator milestone forecast [&lt;ID&gt;] [--json]</c> (M.ORCH-T1688): the forecast of one milestone, or of
/// every active and accepting one, worked out now: tickets and tokens left, the projected date and what it rests on, and
/// whether it is at risk and why. It reads the first forecast the daemon recorded and writes nothing.</summary>
internal sealed class MilestoneForecastCommand : Command<MilestoneForecastCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ID]")]
        [Description("The milestone (MS-n or its name); every active and accepting milestone when left out.")]
        public string? Id { get; init; }

        [CommandOption("--json")]
        [Description("Print the forecasts as JSON.")]
        public bool Json { get; init; }
    }

    /// <summary>The token cost in words: the sum, how much of it is the measured average, and what couldn't be valued.</summary>
    internal static string Cost(MilestoneForecast.Result f) =>
        f.Complete ? "none"
        : f.Unvalued == f.RemainingTickets ? "unknown (no estimates and no measured cost per ticket)"
        : $"~{Formatting.Tokens(f.RemainingTokens)} tokens"
            + (f.FromAverage > 0 ? $" ({f.FromAverage} of {f.RemainingTickets} at the measured average)" : "")
            + (f.Unvalued > 0 ? $", not counting {f.Unvalued} with no estimate" : "");

    public override int Execute(CommandContext context, Settings settings) => MilestoneCli.WithTickets(db =>
    {
        Milestone? only = null;
        if (settings.Id is { } id)
        {
            only = db.Milestone(id) ?? throw new KeyNotFoundException($"There is no milestone {id}.");
            if (only.State is not (MilestoneStates.Active or MilestoneStates.Accepting))
                throw new InvalidOperationException($"{only.Id} is {only.State}; only an active or accepting milestone has a forecast.");
        }
        var rows = new MilestoneForecasts(Ctx.Store()).Read(db, DateTime.UtcNow, only);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows.Select(r => new
            {
                id = r.Milestone.Id, name = r.Milestone.Name, state = r.Milestone.State,
                remainingTickets = r.Forecast.RemainingTickets, remainingTokens = r.Forecast.RemainingTokens,
                estimatedAtAverage = r.Forecast.FromAverage, notValued = r.Forecast.Unvalued,
                ticketsPerDay = r.Forecast.TicketsPerDay, basis = r.Forecast.Basis, projected = r.Forecast.ProjectedUtc,
                firstProjected = r.Record?.FirstProjectedUtc, firstProjectedAt = r.Record?.FirstAtUtc,
                atRisk = r.Forecast.AtRisk, reasons = r.Forecast.Reasons,
            }), BranchesCommand.Indented));
            return 0;
        }
        if (rows.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No milestone is active or in acceptance, so there is nothing to forecast.[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Milestone", "Name", "Left", "Token cost", "Projected", "First forecast", "At risk");
        foreach (var r in rows)
        {
            var f = r.Forecast;
            table.AddRow(Markup.Escape(r.Milestone.Id), Markup.Escape(r.Milestone.Name),
                $"{f.RemainingTickets} ticket{(f.RemainingTickets == 1 ? "" : "s")}", Markup.Escape(Cost(f)),
                f.Complete ? "[grey]tickets done[/]" : f.ProjectedUtc is { } at ? MilestoneCli.Day(at) : "[yellow]unknown[/]",
                r.Record?.FirstProjectedUtc is { } first ? MilestoneCli.Day(first) : "[grey]not recorded yet[/]",
                f.AtRisk ? $"[red]yes[/]: {Markup.Escape(f.Reason)}" : "no");
        }
        AnsiConsole.Write(table);
        foreach (var r in rows)
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(r.Milestone.Id)}: {Markup.Escape(r.Forecast.Basis)}[/]");
        return 0;
    });
}

/// <summary><c>orchestrator milestone pending [&lt;ID&gt;] [--json]</c> (M.ORCH-T1685): the tickets and epics waiting to join an
/// active or accepting milestone (one milestone's, or every milestone's), oldest first, each with who proposed it and when.</summary>
internal sealed class MilestonePendingCommand : Command<MilestonePendingCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ID]")]
        [Description("The milestone (MS-n or its name); every milestone when left out.")]
        public string? Id { get; init; }

        [CommandOption("--json")]
        [Description("Print the waiting additions as JSON: milestone, item, proposedAt, proposedBy.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => MilestoneCli.WithTickets(db =>
    {
        var pending = db.MilestonePending(string.IsNullOrWhiteSpace(settings.Id) ? null : settings.Id);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(pending.Select(p => new
            {
                milestone = p.MilestoneId, item = p.Item, proposedAt = MilestoneScope.At(p.At), proposedBy = p.By,
            }), BranchesCommand.Indented));
            return 0;
        }
        if (pending.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing is waiting to join a milestone.[/]");
            return 0;
        }
        var now = DateTime.UtcNow;
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Milestone", "Item", "Proposed by", "Since", "Approve");
        foreach (var p in pending)
            table.AddRow(Markup.Escape(p.MilestoneId), Markup.Escape(p.Item), Markup.Escape(p.By ?? ""),
                MilestoneScope.At(p.At) is { } at ? Markup.Escape(Format.Ago(at, now)) : "",
                $"[aqua]{Markup.Escape($"orchestrator milestone approve {p.MilestoneId} {p.Item} --by <WHO>")}[/]");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]Decline one with orchestrator milestone decline <ID> <ITEM> --by <WHO> [[--reason <TEXT>]].[/]");
        return 0;
    });
}

/// <summary>What <c>orchestrator milestone approve</c> and <c>decline</c> take: the milestone, what waits, and who answers.</summary>
internal class MilestoneAdditionSettings : CommandSettings
{
    [CommandArgument(0, "<ID>")]
    [Description("The milestone (MS-n or its name).")]
    public string Id { get; init; } = "";

    [CommandArgument(1, "<ITEM>")]
    [Description("The ticket or epic waiting to join it.")]
    public string Item { get; init; } = "";

    [CommandOption("--by <WHO>")]
    [Description("Who answers: recorded in the milestone's history.")]
    public string? By { get; init; }
}

/// <summary><c>orchestrator milestone approve &lt;ID&gt; &lt;ITEM&gt; --by &lt;WHO&gt;</c> (M.ORCH-T1685): the owner approves a
/// waiting addition, which becomes a member of the milestone.</summary>
internal sealed class MilestoneApproveCommand : Command<MilestoneAdditionSettings>
{
    public override int Execute(CommandContext context, MilestoneAdditionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.By))
        {
            AnsiConsole.MarkupLine("[red]Say who approves the addition: orchestrator milestone approve <ID> <ITEM> --by <who>.[/]");
            return 1;
        }
        return MilestoneCli.WithTickets(db =>
        {
            var m = db.Milestone(settings.Id) ?? throw new KeyNotFoundException($"There is no milestone {settings.Id}.");
            db.ApproveMilestoneAddition(m.Id, settings.Item, settings.By);
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(settings.Item.Trim())} is on {Markup.Escape(m.Id)} ({Markup.Escape(m.Name)}) now. Approved by {Markup.Escape(settings.By.Trim())}.[/]");
            return 0;
        });
    }
}

/// <summary><c>orchestrator milestone decline &lt;ID&gt; &lt;ITEM&gt; --by &lt;WHO&gt; [--reason &lt;TEXT&gt;]</c> (M.ORCH-T1685):
/// the owner declines a waiting addition, which stops waiting and never was a member.</summary>
internal sealed class MilestoneDeclineCommand : Command<MilestoneDeclineCommand.Settings>
{
    public sealed class Settings : MilestoneAdditionSettings
    {
        [CommandOption("--reason <TEXT>")]
        [Description("Why it is declined: recorded in the milestone's history.")]
        public string? Reason { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.By))
        {
            AnsiConsole.MarkupLine("[red]Say who declines the addition: orchestrator milestone decline <ID> <ITEM> --by <who>.[/]");
            return 1;
        }
        return MilestoneCli.WithTickets(db =>
        {
            var m = db.Milestone(settings.Id) ?? throw new KeyNotFoundException($"There is no milestone {settings.Id}.");
            db.DeclineMilestoneAddition(m.Id, settings.Item, settings.By, settings.Reason);
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(settings.Item.Trim())} is declined for {Markup.Escape(m.Id)} ({Markup.Escape(m.Name)}): it no longer waits. Declined by {Markup.Escape(settings.By.Trim())}.[/]");
            return 0;
        });
    }
}

/// <summary><c>orchestrator milestone steward [&lt;ID&gt;] [--dry-run] [--json]</c> (M.ORCH-T1686): the steward's pass over one
/// milestone, or over every active and accepting one. It files one ticket for each failed build or exists check that no open
/// ticket covers and puts it forward for the milestone (a pending addition the owner approves), and lists the milestone's
/// closed tickets that came back or cite no evidence. With <c>--dry-run</c> it prints the plan and changes nothing.</summary>
internal sealed class MilestoneStewardCommand : Command<MilestoneStewardCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ID]")]
        [Description("The milestone (MS-n or its name); every active and accepting milestone when left out.")]
        public string? Id { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print what the steward would do and change nothing.")]
        public bool DryRun { get; init; }

        [CommandOption("--json")]
        [Description("Print the actions as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => MilestoneCli.WithTickets(db =>
    {
        IReadOnlyList<Milestone> chosen;
        if (settings.Id is { } id)
        {
            var m = db.Milestone(id) ?? throw new KeyNotFoundException($"There is no milestone {id}.");
            if (!m.IsLocked) throw new InvalidOperationException($"{m.Id} is {m.State}; the steward looks only at an active or accepting milestone.");
            chosen = [m];
        }
        else chosen = MilestoneStewards.Locked(db);

        var plan = MilestoneStewards.Read(db, chosen);
        IReadOnlyList<MilestoneStewards.Outcome> outcomes = settings.DryRun
            ? [.. plan.Select(a => new MilestoneStewards.Outcome(a, false,
                a.Kind == StewardActionKind.Reopen ? $"{a.Text} {MilestoneStewards.CannotReopen}" : a.Text))]
            : MilestoneStewards.Apply(db, plan);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(outcomes.Select(o => new
            {
                action = o.Action.Kind.ToString().ToLowerInvariant(), milestone = o.Action.MilestoneId, check = o.Action.Check,
                ticket = o.Filed ?? o.Action.TicketId, because = o.Action.Because, repo = o.Action.Repo, type = o.Action.Type,
                summary = o.Action.Summary, marker = o.Action.Marker, applied = o.Applied, failed = o.Failed, dryRun = settings.DryRun, text = o.Text,
            }), BranchesCommand.Indented));
            return outcomes.Any(o => o.Failed) ? 1 : 0;
        }
        if (chosen.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No milestone is active or in acceptance, so the steward has nothing to do.[/]");
            return 0;
        }
        if (outcomes.Count == 0)
        {
            AnsiConsole.MarkupLine($"[green]The steward has nothing to do for {Markup.Escape(string.Join(", ", chosen.Select(m => m.Id)))}: every failed check has an open ticket, and no closed ticket on it is in doubt.[/]");
            return 0;
        }
        if (settings.DryRun) AnsiConsole.MarkupLine("[grey]Dry run: nothing is changed.[/]");
        foreach (var o in outcomes)
        {
            var (colour, word) = o switch
            {
                { Failed: true } => ("red", "failed"),
                { Applied: true } => ("green", "done"),
                { Action.Kind: StewardActionKind.Propose } => ("aqua", "would propose"),
                { Action.Kind: StewardActionKind.Reopen } => ("yellow", "came back"),
                _ => ("yellow", "report"),
            };
            AnsiConsole.MarkupLine($"[aqua]{Markup.Escape(o.Action.MilestoneId)}[/] [{colour}]{word}[/]: {Markup.Escape(o.Text)}");
            if (settings.DryRun && o.Action.Kind == StewardActionKind.Propose)
                AnsiConsole.MarkupLine($"     [grey]{Markup.Escape($"{o.Action.Repo} {o.Action.Type}, labels {MilestoneSteward.Label} and {o.Action.Marker}: {o.Action.Summary}")}[/]");
        }
        return outcomes.Any(o => o.Failed) ? 1 : 0;
    });
}
