using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
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
/// tickets done of total, its acceptance checks passed of total, and the date the daemon last projected for it (M.ORCH-T1688).</summary>
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
        var rows = open.Select(m =>
        {
            var (done, total) = TicketsDb.MilestoneProgress(db.MilestoneMembers(m.Id, graph));
            var checks = db.MilestoneChecks(m.Id);
            return (Milestone: m, Done: done, Total: total, Passed: checks.Count(c => c.Passed), Checks: checks.Count, Forecast: forecasts.GetValueOrDefault(m.Id));
        }).ToList();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows.Select(r => new
            {
                id = r.Milestone.Id, name = r.Milestone.Name, state = r.Milestone.State, repos = r.Milestone.Repos,
                ticketsDone = r.Done, tickets = r.Total, checksPassed = r.Passed, checks = r.Checks,
                projected = r.Forecast?.LatestProjectedUtc, atRisk = r.Forecast?.AtRisk,
            }), BranchesCommand.Indented));
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Milestone", "Name", "State", "Tickets", "Checks", "Projected", "Repos");
        foreach (var r in rows)
            table.AddRow(Markup.Escape(r.Milestone.Id), Markup.Escape(r.Milestone.Name), Markup.Escape(r.Milestone.State),
                $"{r.Done}/{r.Total} done", $"{r.Passed}/{r.Checks} passed",
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
