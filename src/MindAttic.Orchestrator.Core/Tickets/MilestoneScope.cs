using System.Globalization;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>An addition waiting for a locked milestone's owner (M.ORCH-T1685): the ticket or epic, when it was proposed
/// (null when the time can't be read) and by whom (null when nobody was named).</summary>
public sealed record MilestoneWaiting(string Item, DateTime? SinceUtc, string? By);

/// <summary>
/// The scope of one active or accepting milestone (M.ORCH-T1685, the store's lock is M.TIX-T347): what waits for its owner's
/// approval, and how much it has grown since it was first activated. <see cref="AtActivation"/>, <see cref="Added"/>,
/// <see cref="Removed"/> and <see cref="Now"/> are member tickets as <c>TicketsDb.MilestoneGrowth</c> counts them;
/// <see cref="WaitingTickets"/> the tickets the waiting additions would bring.
/// </summary>
public sealed record MilestoneScope(string Id, string Name, string State, int AtActivation, int Added, int Removed, int Now, int WaitingTickets,
    IReadOnlyList<MilestoneWaiting> Waiting)
{
    /// <summary>How many tickets it has grown by since activation; negative when it shrank.</summary>
    public int Grown => Now - AtActivation;

    /// <summary>The growth in a few words, "+3, 2 waiting": empty when it hasn't changed and nothing waits.</summary>
    public string GrownText => string.Join(", ", new[]
    {
        Grown == 0 ? null : Grown.ToString("+0;-0", CultureInfo.InvariantCulture),
        WaitingTickets > 0 ? $"{WaitingTickets} waiting" : Waiting.Count > 0 ? $"{Waiting.Count} waiting (no new tickets)" : null,
    }.Where(p => p is not null));

    /// <summary>Every active and accepting milestone's scope, in milestone order. Reads the ticket graph only when there is one,
    /// and not at all when <paramref name="graph"/> is given: the caller's own read of it (M.ORCH-T1938, the whole tickets and
    /// links tables read once per refresh, not once per caller).</summary>
    public static IReadOnlyList<MilestoneScope> Read(TicketsDb db, TicketGraph? graph = null)
    {
        var locked = db.Milestones().Where(m => m.IsLocked).ToList();
        if (locked.Count == 0) return [];
        graph ??= db.Graph();
        var pending = db.MilestonePending().ToLookup(p => p.MilestoneId, StringComparer.OrdinalIgnoreCase);
        return [.. locked.Select(m => Of(m, db.MilestoneGrowth(m.Id, graph), [.. pending[m.Id]]))];
    }

    /// <summary>As <see cref="Read"/>, but a read that fails (a database from before the scope lock, or one locked) gives
    /// <paramref name="last"/>, the read before it, or none: never a failed ticket read.</summary>
    internal static IReadOnlyList<MilestoneScope> ReadOr(TicketsDb db, IReadOnlyList<MilestoneScope>? last = null, TicketGraph? graph = null)
    {
        try { return Read(db, graph); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or KeyNotFoundException or System.Data.Common.DbException)
        {
            return last ?? [];
        }
    }

    /// <summary>One milestone's scope from what the store says of it; <paramref name="growth"/> is null for one never activated.</summary>
    public static MilestoneScope Of(Milestone milestone, MilestoneGrowth? growth, IReadOnlyList<MilestonePendingAddition> pending) =>
        new(milestone.Id, milestone.Name, milestone.State, growth?.AtActivation ?? 0, growth?.Added ?? 0, growth?.Removed ?? 0, growth?.Now ?? 0,
            growth?.PendingTickets ?? 0, [.. pending.Select(p => new MilestoneWaiting(p.Item, At(p.At), string.IsNullOrWhiteSpace(p.By) ? null : p.By.Trim()))]);

    /// <summary>A store timestamp (<c>yyyy-MM-ddTHH:mm:ssZ</c>) as a time; null when it isn't one.</summary>
    public static DateTime? At(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
}
