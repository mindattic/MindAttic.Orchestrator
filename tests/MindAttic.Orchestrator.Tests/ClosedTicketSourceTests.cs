using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The Queue's Closed and Won't do tabs (M.ORCH-T849): the recently closed tickets each person had assigned.</summary>
public sealed class ClosedTicketSourceTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private const string Ryan = "ryan@example.com";

    private static (string, Ticket) T(string id, string status, string opened = "2026-09-01") => ("A", new Ticket(id, opened, "fix", status, "x", ""));

    private static TicketEvent Closed(string id, string at, string status = "closed") => new(id, at, "status", "open", status);

    [Test]
    public void Lists_tickets_closed_within_the_window_newest_first_and_leaves_older_ones_out()
    {
        var source = new ClosedTicketSource();
        var events = new Dictionary<string, IReadOnlyList<TicketEvent>>
        {
            ["A-T1"] = [Closed("A-T1", "2026-10-24T10:00:00Z")],
            ["A-T2"] = [Closed("A-T2", "2026-10-01T10:00:00Z")],
            ["A-T3"] = [Closed("A-T3", "2026-10-25T09:00:00Z", TicketRules.WontDo)],
        };
        var assignees = new Dictionary<string, string> { ["A-T1"] = Ryan, ["A-T2"] = Ryan, ["A-T3"] = Ryan };
        var list = source.Recent([T("A-T1", TicketRules.Closed), T("A-T2", TicketRules.Closed), T("A-T3", TicketRules.WontDo)], assignees,
            id => events[id], Now);
        Assert.That(list.Select(c => c.Id), Is.EqualTo(new[] { "A-T3", "A-T1" }));
        Assert.That(list[1].ClosedUtc, Is.EqualTo(new DateTime(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc)));
        Assert.That(list[0].AsItem().Assignee, Is.EqualTo(Ryan));
    }

    [Test]
    public void Reads_each_tickets_history_once()
    {
        var source = new ClosedTicketSource();
        var reads = 0;
        var assignees = new Dictionary<string, string> { ["A-T1"] = Ryan };
        IReadOnlyList<TicketEvent> History(string id) { reads++; return [Closed(id, "2026-10-24T10:00:00Z")]; }
        source.Recent([T("A-T1", TicketRules.Closed)], assignees, History, Now);
        source.Recent([T("A-T1", TicketRules.Closed)], assignees, History, Now);
        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public void Bounds_the_history_reads_of_one_call()
    {
        var source = new ClosedTicketSource();
        var reads = 0;
        var many = Enumerable.Range(1, ClosedTicketSource.MaxHistoryReads + 20).Select(i => T($"A-T{i}", TicketRules.Closed)).ToList();
        var assignees = many.ToDictionary(t => t.Item2.Id, _ => Ryan);
        source.Recent(many, assignees, id => { reads++; return []; }, Now);
        Assert.That(reads, Is.EqualTo(ClosedTicketSource.MaxHistoryReads));
    }

    [Test]
    public void The_close_time_is_the_last_close_in_the_history()
    {
        IReadOnlyList<TicketEvent> events =
        [
            Closed("A-T1", "2026-10-01T10:00:00Z"),
            new("A-T1", "2026-10-02T10:00:00Z", "status", "closed", "open"),
            Closed("A-T1", "2026-10-20T10:00:00Z"),
        ];
        Assert.That(ClosedTicketSource.ClosedAt(events), Is.EqualTo(new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc)));
        Assert.That(ClosedTicketSource.ClosedAt([]), Is.Null);
    }
}
