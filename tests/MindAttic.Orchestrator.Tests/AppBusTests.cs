using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Data;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The app bus (M.ORCH-T879): the ticket database's changes read as bus events, the hub's routing and the presences.</summary>
public sealed class AppBusTests
{
    [TestCase("created", null, "open", TicketEvents.Filed)]
    [TestCase("approval", "proposed", "approved", TicketEvents.Approved)]
    [TestCase("approval", "approved", "proposed", TicketEvents.Unapproved)]
    [TestCase("approval", "proposed", "rejected", TicketEvents.Rejected)]
    [TestCase("working", null, "a@b.com (s1)", TicketEvents.Claimed)]
    [TestCase("working", "a@b.com (s1)", null, TicketEvents.Released)]
    [TestCase("status", "open", "closed", TicketEvents.Closed)]
    [TestCase("status", "open", "wont-do", TicketEvents.WontDo)]
    [TestCase("status", "closed", "open", TicketEvents.Reopened)]
    [TestCase("summary", "old", "new", TicketEvents.Reworded)]
    [TestCase("assignee", null, "a@b.com", TicketEvents.Assigned)]
    [TestCase("priority", "0", "5", TicketEvents.Prioritized)]
    [TestCase("link", null, "x", TicketEvents.Linked)]
    [TestCase("description", null, "body", TicketEvents.Changed)]
    public void A_database_change_reads_as_its_bus_event(string field, string? old, string? value, string expected) =>
        Assert.That(TicketActivityFeed.EventOf(field, old, value), Is.EqualTo(expected));

    [TestCase("a@b.com (s1)", "a@b.com", "s1")]
    [TestCase("a@b.com", "a@b.com", null)]
    [TestCase("s1", null, "s1")]
    [TestCase(null, null, null)]
    public void Who_works_a_ticket_splits_into_login_and_session(string? who, string? login, string? session) =>
        Assert.That(TicketActivityFeed.ParseWorker(who), Is.EqualTo((login, session)));

    [Test]
    public void A_claim_carries_its_worker_and_the_feed_number()
    {
        var change = TicketActivityFeed.Of(new ActivityEvent(42, "M.ORCH-T1", "MindAttic.Orchestrator", "Do it", "2026-10-25T12:00:00Z", "working", null, "a@b.com (s1)"));
        Assert.That(change, Has.Property(nameof(TicketBusEvent.Event)).EqualTo(TicketEvents.Claimed)
            .And.Property(nameof(TicketBusEvent.Login)).EqualTo("a@b.com")
            .And.Property(nameof(TicketBusEvent.Session)).EqualTo("s1")
            .And.Property(nameof(TicketBusEvent.Seq)).EqualTo(42)
            .And.Property(nameof(TicketBusEvent.Source)).EqualTo("tickets-db"));
        Assert.That(change.AtUtc, Is.EqualTo(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void An_envelope_round_trips_with_the_contract_s_field_names()
    {
        var line = StatusStream.Serialize(StatusEnvelope.Of(AppBus.OpenTicket, new OpenTicketRequest { Id = "M.TIX-T158", From = AppBus.OrchestratorDesktop }, AppBus.Version));
        Assert.That(line, Does.Contain("\"kind\":\"open-ticket\"").And.Contain("\"id\":\"M.TIX-T158\"").And.Contain("\"from\":\"orchestrator-desktop\""));
        Assert.That(StatusStream.Deserialize(line)?.PayloadAs<OpenTicketRequest>()?.Id, Is.EqualTo("M.TIX-T158"));
    }

    [Test]
    public void The_hub_keeps_each_app_s_presence_for_subscribers_that_connect_later()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new TicketActivityFeed());
        hub.Take(StatusEnvelope.Of(AppBus.Presence, new AppPresence { App = AppBus.TicketsDesktop, Pid = 7, Handles = [AppBus.OpenTicket] }));
        hub.Take(StatusEnvelope.Of(AppBus.Presence, new AppPresence { App = AppBus.OrchestratorDesktop, Pid = 8, Handles = [AppBus.ShowWorker] }));
        Assert.That(stream.Retained(AppBusHub.PresenceKey(AppBus.TicketsDesktop)), Does.Contain("tickets-desktop"));
        Assert.That(stream.Retained(AppBusHub.PresenceKey(AppBus.OrchestratorDesktop)), Does.Contain("orchestrator-desktop"));
    }

    [Test]
    public void A_ticket_change_brings_the_daemon_s_plan_forward_and_an_unreadable_one_is_dropped()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var changed = 0;
        var hub = new AppBusHub(stream, new TicketActivityFeed()) { TicketChanged = () => changed++ };
        hub.Take(StatusEnvelope.Of(AppBus.Ticket, new TicketBusEvent { Id = "M.ORCH-T1", Event = TicketEvents.Claimed, Source = "orchestrator" }));
        hub.Take(StatusEnvelope.Of(AppBus.Ticket, new { }));
        Assert.That(changed, Is.EqualTo(1));
    }

    [Test]
    public void A_presence_counts_only_while_its_process_lives_and_for_what_it_handles()
    {
        var alive = true;
        var book = new AppPresences { StartOf = _ => alive ? DateTime.UtcNow.AddHours(-1) : null };
        book.Note(new AppPresence { App = AppBus.TicketsDesktop, Pid = 7, Handles = [AppBus.OpenTicket] });
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket), Is.Not.Null);
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.ShowWorker), Is.Null);
        Assert.That(book.Listening(AppBus.OrchestratorDesktop, AppBus.ShowWorker), Is.Null);
        alive = false;
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket), Is.Null);
        alive = true;
        book.Clear();
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket), Is.Null);
    }

    [Test]
    public void A_presence_whose_pid_a_later_process_took_counts_for_nothing()
    {
        // M.ORCH-T1010: Tickets.Desktop said it at 10:00 and exited; its pid now belongs to a process started at 11:00.
        var said = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
        DateTime? started = said.AddHours(1);
        var book = new AppPresences { StartOf = _ => started };
        book.Note(new AppPresence { App = AppBus.TicketsDesktop, Pid = 7, Handles = [AppBus.OpenTicket], AtUtc = said });
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket), Is.Null);
        started = said.AddMinutes(-5);
        Assert.That(book.Listening(AppBus.TicketsDesktop, AppBus.OpenTicket), Is.Not.Null, "the process that said it, still running");
    }

    [Test]
    public void Publishing_with_no_daemon_says_so_without_throwing()
    {
        var was = AppBusClient.Send;
        try
        {
            AppBusClient.Send = _ => false;
            Assert.That(AppBusClient.PublishTicket("M.ORCH-T1", TicketEvents.Released), Is.False);
            AppBusClient.Send = _ => throw new IOException("boom");
            Assert.That(AppBusClient.PublishTicket("M.ORCH-T1", TicketEvents.Released), Is.False);
        }
        finally { AppBusClient.Send = was; }
    }
}
