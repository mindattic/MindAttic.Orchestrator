using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The milestone steward, read and applied (M.ORCH-T1686). <see cref="Read"/> gathers what <see cref="MilestoneSteward.Plan"/>
/// needs from the ticket database and writes nothing. <see cref="Apply"/> carries a plan out through the ticket library alone:
/// a proposal is filed open in its repo (approved by nobody), labelled <see cref="MilestoneSteward.Label"/> and its check's
/// marker, and added to the milestone, where the scope lock (M.TIX-T347) keeps it waiting for the owner. The library has no way
/// to reopen a closed ticket (tickets are append-only: only a close can be written), so a reopen is never carried out: it's
/// listed for the owner, as a report is. <see cref="RunDue"/> is the daemon's pass, at most once every <see cref="Every"/> for
/// each milestone in the state it is in (<see cref="StateName"/>).
/// </summary>
public sealed class MilestoneStewards(JsonStore store)
{
    /// <summary>The store's record of when each milestone was last stewarded and in which state, by milestone ID.</summary>
    public const string StateName = "milestone-steward";

    /// <summary>How often the daemon stewards a milestone that stays in the same state.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromDays(1);

    /// <summary>Why a reopen isn't carried out.</summary>
    public const string CannotReopen = "The ticket store can't reopen a closed ticket (tickets are append-only), so it is listed for the owner instead.";

    /// <summary>When a milestone was last stewarded, and the state it was in then.</summary>
    public sealed class Ran
    {
        /// <summary>Gets or sets the state.</summary>
        public string State { get; set; } = "";
        /// <summary>Gets or sets when.</summary>
        public DateTime AtUtc { get; set; }
    }

    /// <summary>What became of one action: whether it changed anything, the ticket it filed, and what to say of it.</summary>
    public sealed record Outcome(StewardAction Action, bool Applied, string Text, string? Filed = null, bool Failed = false);

    private bool mayHaveRecords = true;

    /// <summary>The active and accepting milestones: one read of the ticket database.</summary>
    public static IReadOnlyList<Milestone> Locked(TicketsDb db) => [.. db.Milestones().Where(m => m.IsLocked)];

    /// <summary>The plan for <paramref name="milestones"/>. Nothing is read for none, and nothing is ever written.</summary>
    public static IReadOnlyList<StewardAction> Read(TicketsDb db, IReadOnlyList<Milestone> milestones)
    {
        if (milestones.Count == 0) return [];
        var graph = db.Graph();
        var tickets = db.Tickets();
        var statuses = tickets.GroupBy(t => t.Ticket.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Ticket.Status, StringComparer.OrdinalIgnoreCase);
        var scopes = milestones.Select(m => new MilestoneSteward.Scope(m, db.MilestoneChecks(m.Id),
            [.. db.MilestoneMembers(m.Id, graph).Where(x => x.IsTicket).Select(x => x.Id)])).ToList();
        return MilestoneSteward.Plan(scopes, statuses, db.AllLabels(), TicketHygiene.Check(tickets));
    }

    /// <summary>Carries <paramref name="plan"/> out. An action the ticket database refuses is said so and the rest still run.</summary>
    public static IReadOnlyList<Outcome> Apply(TicketsDb db, IReadOnlyList<StewardAction> plan)
    {
        var done = new List<Outcome>();
        foreach (var action in plan)
        {
            try
            {
                done.Add(action.Kind switch
                {
                    StewardActionKind.Propose => Propose(db, action),
                    StewardActionKind.Reopen => new Outcome(action, false, $"{action.Text} {CannotReopen}"),
                    _ => new Outcome(action, false, action.Text),
                });
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or KeyNotFoundException
                or System.Data.Common.DbException)
            {
                done.Add(new Outcome(action, false, $"{action.Text} It could not be done: {ex.Message}", Failed: true));
            }
        }
        return done;
    }

    private static Outcome Propose(TicketsDb db, StewardAction action)
    {
        var ticket = db.Add(action.Repo!, action.Type!, TicketRules.Open, action.Summary!, "", description: action.Description);
        db.AddLabels(ticket.Id, [MilestoneSteward.Label, action.Marker!]);
        var added = db.AddToMilestone(action.MilestoneId, [ticket.Id], by: MilestoneSteward.By);
        var joins = added.Pending.Count > 0 ? $"it waits for the owner's approval to join {action.MilestoneId}" : $"it is on {action.MilestoneId}";
        return new Outcome(action, true,
            $"Filed {ticket.Id} ({action.Type}) in {action.Repo} for check #{action.Check} of {action.MilestoneId}; {joins}.", ticket.Id);
    }

    /// <summary>
    /// The daemon's pass: stewards each active and accepting milestone not stewarded in its present state within
    /// <see cref="Every"/>; a line for each ticket filed, each action the database refused, and one for a milestone with
    /// something for its owner to look at. With no such milestone it's one read of the ticket database (and, once, the store's
    /// record cleared). Nothing at all when the ticket database or the store can't be read.
    /// </summary>
    public IReadOnlyList<MilestoneAcceptance.Line> RunDue(Func<TicketsDb?> openTickets, DateTime nowUtc)
    {
        var lines = new List<MilestoneAcceptance.Line>();
        try
        {
            using var db = openTickets();
            if (db is null) return lines;
            var locked = Locked(db);
            if (locked.Count == 0 && !mayHaveRecords) return lines;
            if (!store.TryLoad<Dictionary<string, Ran>>(StateName, out var loaded)) return lines;
            var state = new Dictionary<string, Ran>(loaded, StringComparer.OrdinalIgnoreCase);
            mayHaveRecords = locked.Count > 0;
            if (locked.Count == 0 && state.Count == 0) return lines;

            var due = Due(locked, state, nowUtc);
            var gone = state.Keys.Where(id => !locked.Any(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToList();
            if (due.Count == 0 && gone.Count == 0) return lines;
            var outcomes = Apply(db, Read(db, due));
            foreach (var id in gone) state.Remove(id);
            foreach (var m in due) state[m.Id] = new Ran { State = m.State, AtUtc = nowUtc };
            store.Save(StateName, state);

            foreach (var o in outcomes.Where(o => o.Applied || o.Failed))
                lines.Add(new MilestoneAcceptance.Line(o.Failed, $"milestone steward: {o.Text}"));
            foreach (var m in due)
            {
                var listed = outcomes.Where(o => !o.Applied && !o.Failed && o.Action.MilestoneId.Equals(m.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                if (listed.Count > 0)
                    lines.Add(new MilestoneAcceptance.Line(true,
                        $"milestone steward: {m.Id} has {listed.Count} thing{(listed.Count == 1 ? "" : "s")} for its owner to look at ({Counts(listed)}); orchestrator milestone steward {m.Id} --dry-run lists them"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or System.Data.Common.DbException) { }
        return lines;
    }

    /// <summary>The milestones of <paramref name="locked"/> to steward now: never stewarded, stewarded in another state, or
    /// last stewarded <see cref="Every"/> ago or longer.</summary>
    internal static IReadOnlyList<Milestone> Due(IReadOnlyList<Milestone> locked, IReadOnlyDictionary<string, Ran> state, DateTime nowUtc) =>
        [.. locked.Where(m => !state.TryGetValue(m.Id, out var ran) || ran.State != m.State || nowUtc - ran.AtUtc >= Every)];

    private static string Counts(IReadOnlyList<Outcome> listed)
    {
        var reopen = listed.Count(o => o.Action.Kind == StewardActionKind.Reopen);
        var other = listed.Count - reopen;
        return string.Join(", ", new[]
        {
            reopen > 0 ? $"{reopen} closed but come back" : null,
            other > 0 ? $"{other} reported" : null,
        }.Where(p => p is not null));
    }
}
