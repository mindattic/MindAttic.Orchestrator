using System.Globalization;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>A closed or won't-do ticket assigned to someone (M.ORCH-T849), with when it closed (null when its history doesn't say).</summary>
public sealed record ClosedTicket(string Repo, Ticket Ticket, string Assignee, DateTime? ClosedUtc)
{
    public string Id => Ticket.Id;

    /// <summary>As a queue item, so a person's Queue lists it with the same row as an open one.</summary>
    public QueueItem AsItem() => new(Repo, "", Ticket, 0, null, false) { Assignee = Assignee };
}

/// <summary>
/// The recently closed tickets each person had assigned (M.ORCH-T849), for the Closed and Won't do tabs of their Queue: the
/// ticket database's closed and won't-do tickets with an assignee, closed within <see cref="Window"/>. When a ticket closed is
/// its history's last status change, read once per ticket and kept (a ticket's close time doesn't change unless it's reopened,
/// which its status shows), and at most <see cref="MaxHistoryReads"/> histories are read per call, newest tickets first, so a
/// read on refresh stays cheap however many tickets were ever closed. Not thread-safe: one read at a time.
/// </summary>
public sealed class ClosedTicketSource
{
    /// <summary>How far back the Closed tab reaches.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>The most ticket histories one read looks up; the rest wait for the next read.</summary>
    public const int MaxHistoryReads = 100;

    private readonly Dictionary<string, (string Status, DateTime? At)> closedAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The closed tickets assigned to anyone in the workspace at <paramref name="workspaceRoot"/>, closed after
    /// <paramref name="nowUtc"/> less <see cref="Window"/>, newest first; none when the workspace has no ticket database.</summary>
    public IReadOnlyList<ClosedTicket> Read(string? workspaceRoot, DateTime nowUtc)
    {
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return [];
        var assignees = OpenTicketSource.WorkState.Of(db).Assignees;
        if (assignees.Count == 0) return [];
        var candidates = db.Tickets()
            .Where(t => t.Ticket.Status != TicketRules.Open && assignees.ContainsKey(t.Ticket.Id))
            .OrderByDescending(t => t.Ticket.Date, StringComparer.Ordinal).ThenByDescending(t => t.Ticket.Number ?? 0)
            .ToList();
        return Recent(candidates, assignees, id => db.History(id), nowUtc);
    }

    /// <summary>The candidates closed within the window (<see cref="Read"/>, with the database's parts given).</summary>
    internal IReadOnlyList<ClosedTicket> Recent(IReadOnlyList<(string Repo, Ticket Ticket)> candidates, IReadOnlyDictionary<string, string> assignees,
        Func<string, IReadOnlyList<TicketEvent>> history, DateTime nowUtc)
    {
        var cutoff = nowUtc - Window;
        var reads = 0;
        var list = new List<ClosedTicket>();
        foreach (var (repo, ticket) in candidates)
        {
            // Opened inside the window, it closed inside it too: no history needed to know it belongs.
            var opened = OpenedUtc(ticket);
            if (!closedAt.TryGetValue(ticket.Id, out var known) || known.Status != ticket.Status)
            {
                if (reads >= MaxHistoryReads)
                {
                    if (opened >= cutoff) list.Add(new(repo, ticket, assignees[ticket.Id], null));
                    continue;
                }
                reads++;
                known = (ticket.Status, ClosedAt(history(ticket.Id)));
                closedAt[ticket.Id] = known;
            }
            var when = known.At ?? opened;
            if (when is { } w && w < cutoff) continue;
            list.Add(new(repo, ticket, assignees[ticket.Id], known.At));
        }
        return [.. list.OrderByDescending(c => c.ClosedUtc ?? OpenedUtc(c.Ticket) ?? DateTime.MinValue)];
    }

    /// <summary>When the ticket's status last changed to closed or won't-do, from its history; null when it never says.</summary>
    internal static DateTime? ClosedAt(IReadOnlyList<TicketEvent> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.Field == "status" && e.New is TicketRules.Closed or TicketRules.WontDo) return ParseUtc(e.At);
        }
        return null;
    }

    private static DateTime? OpenedUtc(Ticket ticket) => ParseUtc(ticket.Date);

    private static DateTime? ParseUtc(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
