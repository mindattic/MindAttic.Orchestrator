using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The roster, orgs, settings, pause and reminders on the status bus (M.ORCH-T891): each change written is pushed whole,
/// as its own kind, once a process turns publishing on; the daemon keeps only its own store's for subscribers that connect later.</summary>
[NonParallelizable] // swaps AppBusClient.Send and the store's change hook, which are process-wide
public sealed class StateBusTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();
    private readonly List<StatusEnvelope> sent = [];
    private Func<StatusEnvelope, bool> wasSend = null!;

    [SetUp]
    public void SetUp()
    {
        sent.Clear();
        wasSend = AppBusClient.Send;
        AppBusClient.Send = e => { lock (sent) sent.Add(e); return true; };
    }

    [TearDown]
    public void TearDown()
    {
        StateBus.Disable();
        AppBusClient.Send = wasSend;
        temps.DeleteAll();
    }

    private JsonStore Scratch()
    {
        var dir = temps.New();
        return new JsonStore(dir, new OrchestratorDb(Path.Combine(dir, "data", "orchestrator.db")));
    }

    private T Last<T>(string kind) where T : class =>
        sent.LastOrDefault(e => e.Kind == kind)?.PayloadAs<T>() ?? throw new AssertionException($"no {kind} message was sent");

    [Test]
    public void Each_store_change_is_its_own_kind_and_any_other_is_none()
    {
        var store = Scratch();
        Assert.That(StateBus.KindOf(new StoreChange(JsonStore.SettingsName, store.Db, store)), Is.EqualTo(StateBus.Settings));
        Assert.That(StateBus.KindOf(new StoreChange(GlobalPause.StateName, store.Db, store)), Is.EqualTo(StateBus.Pause));
        Assert.That(StateBus.KindOf(new StoreChange(ReminderBook.Name, store.Db, store)), Is.EqualTo(StateBus.Reminders));
        Assert.That(StateBus.KindOf(new StoreChange(StoreChanges.Members, store.Db, null)), Is.EqualTo(StateBus.Members));
        Assert.That(StateBus.KindOf(new StoreChange(StoreChanges.Orgs, store.Db, null)), Is.EqualTo(StateBus.Orgs));
        Assert.That(StateBus.KindOf(new StoreChange(JsonStore.AssignmentsName, store.Db, store)), Is.Null);
        Assert.That(StateBus.KindOf(new StoreChange(StoreChanges.Members, store.Db, store)), Is.Null, "a state row named members is no table");
    }

    [Test]
    public void Nothing_is_pushed_until_a_process_turns_publishing_on()
    {
        var store = Scratch();
        store.SaveSettings(new OrchestratorSettings { WarnPercent = 70 });
        ReminderBook.Add(store, "call", Now.AddHours(1), null, Now);
        Assert.That(sent.Where(e => StateBus.IsStateKind(e.Kind)), Is.Empty);
    }

    [Test]
    public void Each_change_is_pushed_whole_as_it_commits_with_its_store_and_full_precision()
    {
        var store = Scratch();
        StateBus.Enable(background: false);

        store.SaveSettings(new OrchestratorSettings { WarnPercent = 70, DiskWarnGb = 12.345678901234567, PaceCurveK = 0.1 + 0.2 });
        var settings = Last<SettingsPayload>(StateBus.Settings);
        Assert.That(settings.Store, Is.EqualTo(store.Db.Path));
        Assert.That(settings.Settings.WarnPercent, Is.EqualTo(70));
        Assert.That(settings.Settings.DiskWarnGb, Is.EqualTo(12.345678901234567));
        Assert.That(settings.Settings.PaceCurveK, Is.EqualTo(0.1 + 0.2));

        store.Save(GlobalPause.StateName, new PauseState { Paused = true, SinceUtc = Now, By = "me@example.com" });
        var pause = Last<PausePayload>(StateBus.Pause);
        Assert.That((pause.Paused, pause.SinceUtc, pause.By), Is.EqualTo((true, (DateTime?)Now, "me@example.com")));

        var later = ReminderBook.Add(store, "later", Now.AddHours(2), "ryan", Now);
        var sooner = ReminderBook.Add(store, "sooner", Now.AddHours(1), null, Now);
        Assert.That(Last<RemindersPayload>(StateBus.Reminders).Pending.Select(r => r.Id), Is.EqualTo(new[] { sooner.Id, later.Id }));
        ReminderBook.Cancel(store, sooner.Id);
        Assert.That(Last<RemindersPayload>(StateBus.Reminders).Pending.Select(r => r.Text), Is.EqualTo(new[] { "later" }));

        MemberRoster.Add(store.Db, "amy@example.com", "Amy", "member", "standard", null, Now);
        MemberRoster.Add(store.Db, "bo@example.com", "Bo", "admin", "premium", null, Now);
        Assert.That(Last<MembersPayload>(StateBus.Members).Members.Select(m => m.Email), Is.EqualTo(new[] { "amy@example.com", "bo@example.com" }));
        var before = sent.Count(e => e.Kind == StateBus.Members);
        MemberRoster.Set(store.Db, "amy@example.com", "Amy", null, null, null, Now);
        Assert.That(sent.Count(e => e.Kind == StateBus.Members), Is.EqualTo(before), "a set that changes nothing pushes nothing");
        MemberRoster.Remove(store.Db, "bo@example.com");
        Assert.That(Last<MembersPayload>(StateBus.Members).Members.Select(m => m.Email), Is.EqualTo(new[] { "amy@example.com" }));

        store.Db.SaveOrg(new Organization("sales", "Sales", 1));
        store.Db.SetMember(new OrgMember("sales", "Amy", "amy@example.com"));
        var orgs = Last<OrgsPayload>(StateBus.Orgs).ToDirectory();
        Assert.That(orgs.Orgs.Select(o => o.Id), Is.EqualTo(new[] { Organization.DefaultId, "sales" }));
        Assert.That(orgs.OrgOfClaude("amy@example.com"), Is.EqualTo("sales"));

        Assert.That(sent.Where(e => StateBus.IsStateKind(e.Kind)).Select(e => e.V), Is.All.EqualTo(StateBus.Version));
    }

    [Test]
    public void A_write_never_fails_for_a_publish_that_does()
    {
        var store = Scratch();
        StateBus.Enable(background: false);
        AppBusClient.Send = _ => throw new IOException("no daemon");
        Assert.DoesNotThrow(() => store.SaveSettings(new OrchestratorSettings { WarnPercent = 60 }));
        Assert.That(store.LoadSettings().WarnPercent, Is.EqualTo(60));
    }

    [Test]
    public void The_daemon_keeps_only_its_own_store_s_state_for_subscribers_that_connect_later()
    {
        var own = Scratch();
        var other = Scratch();
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var changed = 0;
        var hub = new AppBusHub(stream, new Tickets.TicketActivityFeed()) { OwnStore = StateBus.StoreOf(own.Db), StateChanged = () => changed++ };

        hub.Take(StatusEnvelope.Of(StateBus.Pause, new PausePayload { Store = StateBus.StoreOf(own.Db), Paused = true }, StateBus.Version));
        Assert.That(stream.Retained(StateBus.Pause), Does.Contain("\"paused\":true"));
        Assert.That(changed, Is.EqualTo(1));

        hub.Take(StatusEnvelope.Of(StateBus.Pause, new PausePayload { Store = StateBus.StoreOf(other.Db), Paused = false }, StateBus.Version));
        Assert.That(stream.Retained(StateBus.Pause), Does.Contain("\"paused\":true"), "a scratch store's pause can't stand in for the daemon's");
        Assert.That(changed, Is.EqualTo(1));
    }

    [Test]
    public void A_payload_round_trips_and_names_its_store()
    {
        var store = Scratch();
        var line = StatusStream.Serialize(StatusEnvelope.Of(StateBus.Members,
            new MembersPayload { Store = StateBus.StoreOf(store.Db), AtUtc = Now, Members = [new Member { Email = "amy@example.com", Name = "Amy", JoinedUtc = Now }] },
            StateBus.Version));
        Assert.That(line, Does.Contain("\"kind\":\"members\"").And.Contain("\"store\":"));
        var envelope = StatusStream.Deserialize(line)!;
        Assert.That(StateBus.SameStore(StateBus.StoreOf(envelope), store.Db.Path.ToUpperInvariant()), Is.True);
        Assert.That(envelope.PayloadAs<MembersPayload>()!.Members.Single(), Has.Property(nameof(Member.Name)).EqualTo("Amy").And.Property(nameof(Member.JoinedUtc)).EqualTo(Now));
        Assert.That(StateBus.SameStore(null, store.Db.Path), Is.False);
    }
}
