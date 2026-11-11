using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The activity bus kind (M.ORCH-T962): per-session coalescing, the snapshot kept for subscribers that connect, the end of a
/// silent session, and the activity record read as its payload.</summary>
public sealed class ActivityFeedTests
{
    private static readonly DateTime T0 = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static ActivityPayload Pic(string session, long seq, string state = ActivityStates.Working, string? target = null) =>
        new() { Session = session, Provider = "codex", Repo = "Repo", State = state, Tool = target is null ? null : "Edit", Target = target, Seq = seq };

    [Test]
    public void The_first_picture_goes_out_at_once_and_a_busy_session_s_next_ones_are_held_to_the_latest()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        Assert.That(c.Offer(Pic("s1", 1, target: "A.cs"), T0), Is.Not.Null);
        Assert.That(c.Offer(Pic("s1", 2, target: "B.cs"), T0.AddMilliseconds(100)), Is.Null);
        Assert.That(c.Offer(Pic("s1", 3, target: "C.cs"), T0.AddMilliseconds(200)), Is.Null);
        Assert.That(c.Due(T0.AddMilliseconds(500)), Is.Empty);
        var due = c.Due(T0.AddSeconds(1));
        Assert.That(due.Select(p => p.Target), Is.EqualTo(new[] { "C.cs" }));
        Assert.That(c.Due(T0.AddSeconds(5)), Is.Empty, "a held picture goes out once");
    }

    [Test]
    public void Each_session_has_its_own_gap()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        Assert.That(c.Offer(Pic("s1", 1, target: "A.cs"), T0), Is.Not.Null);
        Assert.That(c.Offer(Pic("s2", 1, target: "B.cs"), T0.AddMilliseconds(10)), Is.Not.Null);
    }

    [Test]
    public void A_picture_after_the_gap_goes_out_at_once()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        c.Offer(Pic("s1", 1, target: "A.cs"), T0);
        Assert.That(c.Offer(Pic("s1", 2, target: "B.cs"), T0.AddSeconds(2))?.Target, Is.EqualTo("B.cs"));
    }

    [Test]
    public void A_reordered_or_unchanged_picture_is_dropped()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.Zero };
        c.Offer(Pic("s1", 5, target: "A.cs"), T0);
        Assert.That(c.Offer(Pic("s1", 4, target: "Old.cs"), T0.AddSeconds(1)), Is.Null, "a lower seq");
        Assert.That(c.Offer(Pic("s1", 6, target: "A.cs") with { AtUtc = T0.AddSeconds(2) }, T0.AddSeconds(2)), Is.Null, "only seq and time differ");
        Assert.That(c.Offer(Pic("s1", 7, ActivityStates.Idle), T0.AddSeconds(3)), Is.Not.Null);
    }

    [Test]
    public void A_newer_held_picture_that_returns_to_the_sent_one_is_not_sent_again()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        c.Offer(Pic("s1", 1, target: "A.cs"), T0);
        c.Offer(Pic("s1", 2, target: "B.cs"), T0.AddMilliseconds(100));
        c.Offer(Pic("s1", 3, target: "A.cs"), T0.AddMilliseconds(200));
        Assert.That(c.Due(T0.AddSeconds(2)), Is.Empty);
        Assert.That(c.NextDue(T0.AddSeconds(2)), Is.Null);
    }

    [Test]
    public void Next_due_says_how_long_until_a_held_picture_may_go()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        Assert.That(c.NextDue(T0), Is.Null);
        c.Offer(Pic("s1", 1, target: "A.cs"), T0);
        c.Offer(Pic("s1", 2, target: "B.cs"), T0.AddMilliseconds(300));
        Assert.That(c.NextDue(T0.AddMilliseconds(300)), Is.EqualTo(TimeSpan.FromMilliseconds(700)));
    }

    [Test]
    public void The_snapshot_holds_the_latest_sent_picture_of_every_session()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(1) };
        c.Offer(Pic("s1", 1, target: "A.cs"), T0);
        c.Offer(Pic("s2", 1, target: "X.cs"), T0.AddMilliseconds(10));
        c.Offer(Pic("s1", 2, target: "B.cs"), T0.AddMilliseconds(20));
        Assert.That(c.Snapshot().Select(p => (p.Session, p.Target)), Is.EqualTo(new[] { ("s1", "A.cs"), ("s2", "X.cs") }), "a held picture isn't in it yet");
        c.Due(T0.AddSeconds(1));
        Assert.That(c.Snapshot().Select(p => (p.Session, p.Target)), Is.EquivalentTo(new[] { ("s1", "B.cs"), ("s2", "X.cs") }));
    }

    [Test]
    public void An_end_goes_out_at_once_and_forgets_the_session_but_an_end_nobody_saw_begin_is_dropped()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.FromSeconds(10) };
        Assert.That(c.Offer(Pic("old", 9, ActivityStates.Ended), T0), Is.Null);
        c.Offer(Pic("s1", 1, target: "A.cs"), T0);
        Assert.That(c.Offer(Pic("s1", 2, ActivityStates.Ended), T0.AddMilliseconds(1))?.State, Is.EqualTo(ActivityStates.Ended));
        Assert.That(c.Snapshot(), Is.Empty);
    }

    [Test]
    public void A_silent_session_ends_only_once_its_cli_is_gone()
    {
        var c = new ActivityCoalescer { Every = TimeSpan.Zero };
        var alive = true;
        c.Offer(Pic("s1", 3, target: "A.cs"), T0, cliPid: 42);
        c.Offer(Pic("s2", 1, ActivityStates.Idle), T0.AddMinutes(9));
        Assert.That(c.Expire(T0.AddMinutes(11), TimeSpan.FromMinutes(10), _ => alive), Is.Empty, "its pid still runs");
        alive = false;
        var ended = c.Expire(T0.AddMinutes(11), TimeSpan.FromMinutes(10), _ => alive);
        Assert.That(ended, Has.Count.EqualTo(1));
        Assert.That(ended[0], Has.Property(nameof(ActivityPayload.Session)).EqualTo("s1")
            .And.Property(nameof(ActivityPayload.State)).EqualTo(ActivityStates.Ended)
            .And.Property(nameof(ActivityPayload.Target)).Null
            .And.Property(nameof(ActivityPayload.Seq)).EqualTo(4));
        Assert.That(c.Snapshot().Select(p => p.Session), Is.EqualTo(new[] { "s2" }), "s2 spoke within the window");
    }

    [Test]
    public void The_hub_keeps_each_session_s_latest_activity_for_subscribers_that_connect_and_forgets_an_ended_one()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new TicketActivityFeed());
        hub.Activity.Every = TimeSpan.FromSeconds(1);
        hub.OfferActivity(Pic("s1", 1, target: "A.cs"), T0);
        hub.OfferActivity(Pic("s1", 2, target: "B.cs"), T0.AddMilliseconds(100));
        Assert.That(stream.Retained(AppBusHub.ActivityKey("s1")), Does.Contain("A.cs").And.Contain("\"kind\":\"activity\"").And.Contain("\"v\":1"));
        hub.FlushActivity(T0.AddSeconds(1));
        Assert.That(stream.Retained(AppBusHub.ActivityKey("s1")), Does.Contain("B.cs"));
        hub.OfferActivity(Pic("s1", 3, ActivityStates.Ended), T0.AddSeconds(1.1));
        Assert.That(stream.Retained(AppBusHub.ActivityKey("s1")), Is.Null);
    }

    [Test]
    public void An_activity_line_on_the_inbox_goes_through_the_same_coalescing()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new TicketActivityFeed());
        hub.Take(StatusEnvelope.Of(AppBus.Activity, Pic("s9", 1, target: "Inbox.cs"), AppBus.ActivityVersion));
        hub.Take(StatusEnvelope.Of(AppBus.Activity, new { }, AppBus.ActivityVersion));
        Assert.That(stream.Retained(AppBusHub.ActivityKey("s9")), Does.Contain("Inbox.cs"));
        Assert.That(hub.Activity.Snapshot(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Published_activity_is_masked_cut_and_keeps_only_its_own_fields()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new TicketActivityFeed()) { MaskActivityText = t => t.Replace("sekrit", "***") };
        var clean = hub.CleanActivity(new ActivityPayload
        {
            Session = " s1 ", Provider = "Codex", State = "Working", Tool = "mcp__tickets__show",
            Target = "sekrit " + new string('x', 200), Verb = "  ", Subagents = 0,
        });
        Assert.That(clean.Session, Is.EqualTo("s1"));
        Assert.That(clean.Provider, Is.EqualTo("codex"));
        Assert.That(clean.State, Is.EqualTo(ActivityStates.Working));
        Assert.That(clean.Tool, Is.EqualTo("mcp"));
        Assert.That(clean.Target, Does.StartWith("*** ").And.Length.EqualTo(ActivityPayload.MaxTarget));
        Assert.That(clean.Verb, Is.Null);
        Assert.That(clean.Subagents, Is.Null);
    }

    [Test]
    public void By_default_a_token_in_a_target_is_masked_by_the_shared_masker()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new TicketActivityFeed());
        const string token = "ghp_0123456789abcdefghijklmnopqrstuvwxyzAB";
        Assert.That(hub.CleanActivity(new ActivityPayload { Session = "s1", State = ActivityStates.Working, Target = "push " + token }).Target,
            Does.Not.Contain(token));
    }

    [Test]
    public void An_activity_record_reads_as_its_payload_and_its_cli_s_pid()
    {
        const string json = """
            {"session":"019a-abc","provider":"codex","seq":12,"atUtc":"2026-11-11T12:00:00.120Z","repo":"MindAttic.Orchestrator",
             "pid":4321,"state":"working","tool":"apply_patch","verb":"Editing","target":"AppBus.cs","ticket":"M.ORCH-T962","raw":"never"}
            """;
        var (activity, pid) = ActivityFeedPublisher.Parse(json)!.Value;
        Assert.That(pid, Is.EqualTo(4321));
        Assert.That(activity, Has.Property(nameof(ActivityPayload.Session)).EqualTo("019a-abc")
            .And.Property(nameof(ActivityPayload.Seq)).EqualTo(12)
            .And.Property(nameof(ActivityPayload.Target)).EqualTo("AppBus.cs")
            .And.Property(nameof(ActivityPayload.Ticket)).EqualTo("M.ORCH-T962"));
        Assert.That(activity.AtUtc, Is.EqualTo(new DateTime(2026, 11, 11, 12, 0, 0, 120, DateTimeKind.Utc)));
        Assert.That(StatusStream.Serialize(StatusEnvelope.Of(AppBus.Activity, activity, AppBus.ActivityVersion)), Does.Not.Contain("never"));
    }

    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("""{"state":"working"}""")]
    [TestCase("""{"session":"s1"}""")]
    public void A_file_that_isn_t_an_activity_record_reads_as_nothing(string json) =>
        Assert.That(ActivityFeedPublisher.Parse(json), Is.Null);

    [Test]
    public void At_the_start_an_old_record_of_a_gone_cli_isn_t_sent_but_a_live_one_is()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-test-activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
            var hub = new AppBusHub(stream, new TicketActivityFeed());
            File.WriteAllText(Path.Combine(dir, "old.json"), """{"session":"old","state":"idle","seq":1,"atUtc":"2026-11-11T10:00:00Z","pid":1}""");
            File.WriteAllText(Path.Combine(dir, "live.json"), """{"session":"live","state":"working","seq":1,"atUtc":"2026-11-11T10:00:00Z","pid":2}""");
            File.WriteAllText(Path.Combine(dir, "new.json"), """{"session":"new","state":"working","seq":1,"atUtc":"2026-11-11T11:59:00Z"}""");
            var feed = new ActivityFeedPublisher(hub, dir) { Alive = pid => pid == 2, Now = () => T0 };
            feed.Scan(T0, starting: true);
            Assert.That(hub.Activity.Snapshot().Select(p => p.Session), Is.EquivalentTo(new[] { "live", "new" }));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
