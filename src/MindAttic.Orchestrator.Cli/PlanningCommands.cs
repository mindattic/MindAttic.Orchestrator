using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BlockCommand : Command<BlockCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandOption("--by <TICKET>")]
        [Description("The ticket it waits for (repeatable).")]
        public string[] By { get; init; } = [];

        [CommandOption("--clear")]
        public bool Clear { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var id = Ctx.Id(settings.Ticket);
        var by = settings.By.Select(Ctx.Id).ToList();
        // Once migrated, prerequisites are 'requires' links in the ticket database, where every tool sees them.
        var root = OrchestratorOptions.Load(store).WorkspaceRoot;
        using (var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(root))
            if (db is not null && (db.Ticket(id) is null || by.Any(b => db.Ticket(b) is null)))
            {
                // A ticket of a repo still on its own USER_STORIES.md isn't in the database, so it can't be linked there: it's
                // blocked in settings.json below, which the scheduler reads for every ticket, as approve and bump do (M.ORCH-T441).
                // Checked before anything changes: an id that doesn't exist anywhere would be confirmed though nothing waits on it.
                var missing = by.Prepend(id).Where(t => MindAttic.Orchestrator.Tickets.TicketQueue.Find(db, root, t) is null)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var refused = missing.Count > 0 ? $"There's no ticket {string.Join(", ", missing)}"
                    : by.Contains(id, StringComparer.OrdinalIgnoreCase) ? $"{id} can't require itself" : null;
                if (refused is not null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(refused)}. Nothing changed.[/]"); return 1; }
                // --clear replaces what it requires, wherever it was kept.
                if (settings.Clear && db.Ticket(id) is not null)
                    foreach (var to in db.Requires(id)) db.RemoveLink(id, to, "requires");
            }
            else if (db is not null)
            {
                // Checked before anything changes: a link the database refuses (to itself, or a cycle) used to throw half-way,
                // a clear already done.
                foreach (var prerequisite in by)
                {
                    var why = prerequisite.Equals(id, StringComparison.OrdinalIgnoreCase) ? $"{id} can't require itself"
                        : db.Requires(prerequisite, transitive: true).Contains(id, StringComparer.OrdinalIgnoreCase) ? $"{prerequisite} already (indirectly) requires {id}; that would be a cycle"
                        : null;
                    if (why is not null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(why)}. Nothing changed.[/]"); return 1; }
                }
                if (settings.Clear)
                {
                    foreach (var to in db.Requires(id)) db.RemoveLink(id, to, "requires");
                    // One set by hand before the migration is still in settings.json, and the scheduler and the gates still
                    // read it there: cleared too, or "requires nothing" wasn't so (M.ORCH-T331).
                    if (store.LoadSettings().BlockedBy.ContainsKey(id))
                    {
                        var s0 = store.LoadSettingsToChange();
                        store.SaveSettings(s0 with { BlockedBy = new Dictionary<string, List<string>>(s0.BlockedBy.Where(kv => kv.Key != id), StringComparer.Ordinal) });
                    }
                }
                foreach (var prerequisite in by) db.AddLink(id, prerequisite, "requires", "orchestrator block");
                var now = db.Requires(id);
                AnsiConsole.MarkupLine(Markup.Escape(now.Count > 0 ? $"{id} requires {string.Join(", ", now)}." : $"{id} requires nothing."));
                // One set by hand before the migration still holds it in the queue: said, not left out (M.ORCH-T356).
                if (store.LoadSettings().BlockedBy.GetValueOrDefault(id) is { Count: > 0 } byHand)
                    AnsiConsole.MarkupLine(Markup.Escape($"It still waits for {string.Join(", ", byHand)} too, set by hand before the ticket database (block {id} --clear drops it)."));
                return 0;
            }
        var s = store.LoadSettingsToChange();
        var map = new Dictionary<string, List<string>>(s.BlockedBy, StringComparer.Ordinal);
        if (settings.Clear) map.Remove(id);
        // --clear --by replaces them, as on the database; --by alone adds.
        if (by.Count > 0) map[id] = [.. (map.GetValueOrDefault(id) ?? []).Concat(by).Distinct(StringComparer.Ordinal)];
        store.SaveSettings(s with { BlockedBy = map });
        AnsiConsole.MarkupLine(Markup.Escape(map.GetValueOrDefault(id) is { Count: > 0 } waits
            ? $"{id} waits for {string.Join(", ", waits)}."
            : $"{id} waits for nothing set by hand (a \"blocked by\" in its summary still counts)."));
        return 0;
    }
}

/// <summary>Only fixes are worked without asking; any other ticket waits for your yes here.</summary>
internal sealed class ApproveCommand : Command<ApproveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[TICKETS]")]
        [Description("Ticket ids to approve; none lists every ticket waiting for approval.")]
        public string[] Tickets { get; init; } = [];

        [CommandOption("--undo")]
        [Description("Take the approval back: the tickets wait again (one already started finishes).")]
        public bool Undo { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Tickets.Length == 0)
        {
            var (_, _, _, plan) = Ctx.Load();
            AnsiConsole.MarkupLine($"{plan.AwaitingApproval.Count} tickets wait for your approval (fixes need none):");
            foreach (var q in plan.AwaitingApproval)
                AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(q.Id)}[/] {Markup.Escape(q.Repo)} ({Markup.Escape(q.Ticket.Type)}): {Markup.Escape(q.Ticket.Summary)}");
            AnsiConsole.MarkupLine("[grey]orchestrator approve <ID> [[<ID> ...]] lets them be worked.[/]");
            return 0;
        }
        var store = Ctx.Store();
        // Once each: the same ticket named twice (in any case) was a repeated key that crashed approve (M.ORCH-T456).
        var ids = settings.Tickets.Select(Ctx.Id).Distinct(StringComparer.Ordinal).ToList();
        // Approval lives in the ticket database's queue, where tickets ticket approve and the Tickets Desktop write it too and
        // the scheduler reads it (M.ORCH-T384); a ticket it doesn't hold (a repo still on its own USER_STORIES.md, or no database
        // yet) is approved in settings.json, which the scheduler reads beside it (M.ORCH-T441).
        var root = OrchestratorOptions.Load(store).WorkspaceRoot;
        using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(root);
        var found = ids.ToDictionary(id => id, id => MindAttic.Orchestrator.Tickets.TicketQueue.Find(db, root, id));
        // A typo would be "approved" while the ticket you meant goes on waiting, so only ids of real tickets count. Taking back
        // one that settings.json holds for a ticket gone since still works.
        var held = store.LoadSettings().StoryApproved;
        if (found.Where(f => f.Value is null && !(settings.Undo && held.Contains(f.Key, StringComparer.OrdinalIgnoreCase))).Select(f => f.Key).ToList()
            is { Count: > 0 } unknown)
        {
            AnsiConsole.MarkupLine($"[red]No ticket {Markup.Escape(string.Join(", ", unknown))}; nothing was changed.[/]");
            return 1;
        }
        // Checked before anything changes: only an open ticket can be approved.
        if (!settings.Undo && ids.Where(id => found[id]!.Ticket.Status != MindAttic.Tickets.Log.TicketRules.Open).ToList() is { Count: > 0 } shut)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(string.Join(", ", shut))} {(shut.Count == 1 ? "isn't" : "aren't")} open; nothing was changed.[/]");
            return 1;
        }
        foreach (var id in ids)
            MindAttic.Orchestrator.Tickets.TicketQueue.SetApproval(store, db, id, !settings.Undo);
        AnsiConsole.MarkupLine(Markup.Escape(settings.Undo
            ? $"{string.Join(", ", ids)} wait for approval again."
            : $"{string.Join(", ", ids)} approved: they join the queue in their order."));
        return 0;
    }
}

internal sealed class ReserveCommand : Command<ReserveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TICKET>")]
        public string Ticket { get; init; } = "";

        [CommandArgument(1, "<TOKENS>")]
        [Description("Uncached tokens to set aside, like 2M; 0 releases the reservation.")]
        public string Tokens { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!TokenText.TryParse(settings.Tokens, out var tokens)) { AnsiConsole.MarkupLine("[red]Tokens: a number like 2M, 500k or 0.[/]"); return 1; }
        var store = Ctx.Store();
        var id = Ctx.Id(settings.Ticket);
        // Releasing one goes through whatever became of its ticket.
        if (tokens > 0 && Ctx.RefuseUnknown(store, [id])) return 1;
        var s = store.LoadSettingsToChange();
        var map = new Dictionary<string, long>(s.Reservations, StringComparer.Ordinal);
        if (tokens <= 0) map.Remove(id); else map[id] = tokens;
        store.SaveSettings(s with { Reservations = map });
        AnsiConsole.MarkupLine(Markup.Escape(tokens > 0
            ? $"{id} has {Formatting.Tokens(tokens)} reserved: it's proposed first, and everything else paces against what's left after it."
            : $"{id}'s reservation is released."));
        return 0;
    }
}

/// <summary>Which CLIs have /allocate on, and what each would hand its subagents next, without claiming anything.</summary>
internal sealed class DryRunCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var (_, store, _, plan) = Ctx.Load();
        var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler();
        var clis = cycler.AutopilotClis();
        if (clis.Count == 0) { AnsiConsole.MarkupLine("[grey]/allocate is off in every CLI; type /allocate (optionally -repo=A,B) in the CLI that should work the tickets.[/]"); return 0; }
        foreach (var (pid, repos) in clis)
        {
            AnsiConsole.MarkupLine($"/allocate is on in CLI {pid} for {Markup.Escape(string.Join(", ", repos))}:");
            var (handed, why) = Autopilot.Next(plan, store.LoadSettings(), store, repos, null, claim: false, DateTime.UtcNow,
                self: cycler.LatestSessionOf(pid), handingOff: MindAttic.Orchestrator.Sessions.Handoff.Holding(cycler.Directory),
                cli: pid, pilots: clis);
            foreach (var h in handed) AnsiConsole.MarkupLine($"  next: {Markup.Escape(h.Item.Id)} in {Markup.Escape(h.Item.Repo)}: {Markup.Escape(h.Item.Ticket.Summary)}");
            if (why is not null) AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(why)}[/]");
        }
        AnsiConsole.MarkupLine("[grey]Each CLI hands these to its own subagents (orchestrator next --claim). Nothing was claimed.[/]");
        return 0;
    }
}

/// <summary>The next tickets a CLI running /allocate hands to its subagents: each with its worktree and full prompt when claimed.</summary>
internal sealed class NextCommand : Command<NextCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--repos <REPOS>")]
        [Description("Repos to take tickets from, comma-separated (also --repo), wildcards allowed (\"GTFO.*\"); \"*\" for every repo. Default: the repo of the current folder.")]
        public string? Repos { get; init; }

        [CommandOption("--repo <REPOS>")]
        public string? Repo { get; init; }

        [CommandOption("--count <N>")]
        [Description("How many to hand out (default: as many as the pace's subagent allowance has room for).")]
        public int? Count { get; init; }

        [CommandOption("--claim")]
        [Description("Claim them: make each its worktree, record it as assigned, and print the subagent's prompt.")]
        public bool Claim { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (_, store, snapshot, plan) = Ctx.Load();
        IReadOnlyList<string> repos = (settings.Repos ?? settings.Repo) is { } list
            ? [.. list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
            : Autopilot.RepoFor(Environment.CurrentDirectory, snapshot) is { } here ? [here.Name] : [];
        if (repos.Count == 0) { Console.Error.WriteLine("No repo here; name them with --repos A,B (or \"*\")."); return 1; }
        // The calling CLI, as the pace gate finds it: MINDATTIC_CLI_PID, else the claude.exe this runs under (M.ORCH-T195).
        var cli = int.TryParse(Environment.GetEnvironmentVariable("MINDATTIC_CLI_PID"), out var envPid) && envPid > 0 ? envPid
            : MindAttic.Orchestrator.Sessions.ProcessTree.ClaudeAncestor(Environment.ProcessId);
        var cycler = new MindAttic.Orchestrator.Sessions.ContextCycler();
        var unthrottled = cli is { } pid && cycler.IsUnthrottled(pid);
        // What the calling CLI filed itself isn't held from its own subagents; what anyone else (its subagents included) filed
        // and is fixing is (M.ORCH-T255).
        var self = cli is { } me ? cycler.LatestSessionOf(me) : null;
        var (handed, why) = Autopilot.Next(plan, store.LoadSettings(), store, repos, settings.Count, settings.Claim, DateTime.UtcNow,
            unthrottled: unthrottled, self: self, handingOff: MindAttic.Orchestrator.Sessions.Handoff.Holding(cycler.Directory),
            cli: cli, pilots: cycler.AutopilotClis());
        foreach (var h in handed)
        {
            Console.WriteLine($"=== {h.Item.Id} ({h.Item.Repo}, {h.Item.Ticket.Type}): {h.Item.Ticket.Summary}");
            if (h.Worktree is not null) Console.WriteLine($"worktree: {h.Worktree}");
            if (h.Prompt is not null) Console.WriteLine("prompt:\n" + h.Prompt + "\n");
        }
        if (why is not null) Console.WriteLine($"Nothing handed out: {why}.");
        // M.ORCH-T529: a CLI held two claims unstarted to avoid merge conflicts while the window went under its target.
        else if (settings.Claim && handed.Count > 0) Console.WriteLine(Autopilot.StartAllLine(handed.Count));
        return 0;
    }
}

/// <summary>File GitHub issues labelled "orchestrator" and today's CI failures on main as tickets, once.</summary>
internal sealed class IntakeCommand : Command<IntakeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--dry-run")]
        [Description("Show what would be filed without writing any USER_STORIES.md.")]
        public bool DryRun { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var filed = Daemon.Intake(OrchestratorOptions.Load().WorkspaceRoot, settings.DryRun).ToList();
        foreach (var (repo, t) in filed)
            AnsiConsole.MarkupLine($"{(settings.DryRun ? "would file" : "filed")} [bold]{t.Id}[/] in {Markup.Escape(repo)} ({t.Type}): {Markup.Escape(t.Summary)}");
        AnsiConsole.MarkupLine(filed.Count == 0 ? "[grey]Nothing new on GitHub (issues labelled 'orchestrator', CI failures on main in the last day).[/]"
            : $"[grey]{filed.Count} ticket{(filed.Count == 1 ? "" : "s")}{(settings.DryRun ? " would be filed; nothing was written" : " filed")}.[/]");
        return 0;
    }
}

internal static class TokenText
{
    /// <summary>The most tokens that can be typed: a quadrillion, far past any limit, with room for thousands of them summed.</summary>
    public const double MaxTokens = 1e15;

    /// <summary>20000000, 20M, 1.5B, 500k.</summary>
    public static bool TryParse(string text, out long value)
    {
        value = 0;
        var t = text.Trim().Replace(",", "").Replace("_", "");
        if (t.Length == 0) return false;
        var scale = char.ToUpperInvariant(t[^1]) switch { 'K' => 1e3, 'M' => 1e6, 'B' => 1e9, _ => 1 };
        if (scale != 1) t = t[..^1];
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n < 0) return false;
        // Under one token would round to 0, which clears a budget or releases a reservation; only a plain 0 means that.
        if (n * scale is > 0 and < 1) return false;
        // Past a quadrillion is a typo, not a budget: it was taken as long.MaxValue, and two such reservations overflowed the
        // scheduler's sum of them, so every plan threw (M.ORCH-T458).
        if (n * scale > MaxTokens) return false;
        value = (long)(n * scale);
        return true;
    }
}
