using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A CLI that stopped for a window is told to carry on once it resets, or you're told it's waiting (M.ORCH-T256).</summary>
public sealed class LimitWakerTests
{
    private string dir = "";
    private ContextCycler cycler = null!;
    private JsonStore store = null!;
    private readonly List<(string Pipe, string Text)> typed = [];
    private static readonly DateTime Now = new(2026, 10, 18, 11, 15, 0, DateTimeKind.Utc);
    private static readonly HostSession Tab = new("Claude", 900, "mindattic-host-claude-900", @"D:\Projects\MindAttic\Prose");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "waker-" + Guid.NewGuid().ToString("N"));
        cycler = new ContextCycler(Path.Combine(dir, "session-context"));
        store = new JsonStore(Path.Combine(dir, "store"));
        Directory.CreateDirectory(cycler.Directory);
        typed.Clear();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private LimitWaker Waker(bool typingWorks = true, Func<int, bool>? alive = null) => new(cycler, store)
    {
        IsAlive = alive ?? (_ => true),
        HostOf = (pid, hosts) => pid == 42 ? hosts.FirstOrDefault() : null,
        Submit = (pipe, text) => { lock (typed) typed.Add((pipe, text)); return Task.FromResult(typingWorks); },
    };

    private void State(string sid, int pid, string state, DateTime at, DateTime? until, string window = "5-hour", string cwd = @"D:\Projects\MindAttic\Prose")
    {
        var body = new Dictionary<string, object?> { ["state"] = state, ["at"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"), ["pid"] = pid, ["cwd"] = cwd };
        if (until is { } u)
            body["limit_stop"] = new Dictionary<string, object> { ["until"] = new DateTimeOffset(u).ToUnixTimeSeconds(), ["window"] = window, ["at"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ") };
        File.WriteAllText(Path.Combine(cycler.Directory, sid + ".state.json"), JsonSerializer.Serialize(body));
    }

    private static Pace Open => new(PaceMode.Open, 4, 1, 0, null, "5-hour", Now.AddHours(5), "On pace.");

    [Test]
    public void A_held_database_is_not_taken_for_no_remembered_wakes()
    {
        // M.ORCH-T541: a busy load read as an empty list, so the stop was said again and the next save overwrote the memory.
        State("s1", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5));
        Assert.That(Waker().Tick(Open, [Tab], type: true, Now), Has.Count.EqualTo(1));
        State("s2", 43, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5), cwd: @"D:\Projects\MindAttic\Other");

        var before = OrchestratorDb.BusyTimeoutMs;
        OrchestratorDb.BusyTimeoutMs = 100;
        var restarted = Waker();
        try
        {
            using var held = store.Db.Open();
            OrchestratorDb.Execute(held, "BEGIN EXCLUSIVE;");
            Assert.That(restarted.Tick(Open, [Tab], type: true, Now.AddMinutes(1)), Is.Empty, "nothing woken without the remembered wakes");
            OrchestratorDb.Execute(held, "ROLLBACK;");
        }
        finally { OrchestratorDb.BusyTimeoutMs = before; }

        Assert.That(restarted.Tick(Open, [Tab], type: true, Now.AddMinutes(2)).Select(w => w.SessionId), Is.EqualTo(new[] { "s2" }));
        Assert.That(Waker().Tick(Open, [Tab], type: true, Now.AddMinutes(3)), Is.Empty, "both remembered across a restart");
        Assert.That(typed, Has.Count.EqualTo(1), "s1 typed once; s2 isn't in a tab, only said");
    }

    [Test]
    public void Once_the_window_resets_a_cli_in_a_launcher_tab_is_typed_a_resume_once()
    {
        State("s1", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5));
        var w = Waker().Tick(Open, [Tab], type: true, Now).Single();
        Assert.That(w.Typed, Is.True);
        Assert.That(w.Line, Does.Contain("told the CLI in Prose").And.Contain("5-hour window reset"));
        Assert.That(typed.Single().Pipe, Is.EqualTo(Tab.PipeName));
        Assert.That(typed.Single().Text, Does.Contain("The 5-hour window you stopped for reset").And.Not.Contain("\n").And.Not.Contain("\""),
            "one line, nothing wt or the prompt box would split");

        Assert.That(Waker().Tick(Open, [Tab], type: true, Now.AddMinutes(1)), Is.Empty, "once per stop, a restart included (the store remembers it)");
        Assert.That(typed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Nothing_is_woken_before_the_reset_while_the_pace_stops_or_winds_down_or_when_busy_closed_or_not_held()
    {
        State("early", 42, "idle", Now.AddHours(-1), until: Now.AddSeconds(-20));
        State("busy", 43, "busy", Now.AddHours(-1), until: Now.AddMinutes(-5));
        State("plain", 44, "idle", Now.AddHours(-1), until: null);
        State("gone", 45, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5));
        var waker = Waker(alive: pid => pid != 45);
        Assert.That(waker.Tick(Open, [Tab], true, Now), Is.Empty, "inside the grace, busy, never held, or its CLI closed");
        Assert.That(waker.Tick(Open with { Stop = true }, [Tab], true, Now.AddMinutes(2)), Is.Empty, "the pace still stops: the reset isn't in yet");
        Assert.That(waker.Tick(Open with { WindDown = true }, [Tab], true, Now.AddMinutes(2)), Is.Empty);
        Assert.That(typed, Is.Empty);
    }

    [Test]
    public void Only_the_newest_session_of_a_cli_counts_and_a_stop_long_past_is_left_alone()
    {
        State("old", 42, "idle", Now.AddHours(-3), until: Now.AddMinutes(-5));
        State("new", 42, "idle", Now.AddHours(-2), until: null); // /clear'd since: the session on screen isn't waiting
        State("stale", 50, "idle", Now.AddHours(-12), until: Now.AddHours(-7));
        Assert.That(Waker().Tick(Open, [Tab], true, Now), Is.Empty);
    }

    [Test]
    public void A_session_cleared_into_but_without_a_turn_yet_isnt_typed_the_stop_of_the_one_before()
    {
        // M.ORCH-T516: the SessionStart hook writes no 'at', so the cleared-into session lost to the stopped one by turn time.
        State("old", 42, "idle", Now.AddHours(-3), until: Now.AddMinutes(-5));
        File.SetLastWriteTimeUtc(Path.Combine(cycler.Directory, "old.state.json"), Now.AddHours(-3));
        File.WriteAllText(Path.Combine(cycler.Directory, "new.state.json"), JsonSerializer.Serialize(new { pid = 42, cwd = @"D:\Projects\MindAttic\Prose", auth = "subscription" }));
        File.SetLastWriteTimeUtc(Path.Combine(cycler.Directory, "new.state.json"), Now.AddHours(-1));
        Assert.That(Waker().Tick(Open, [Tab], true, Now), Is.Empty);
        Assert.That(typed, Is.Empty);
    }

    [Test]
    public void A_weekly_stop_after_a_five_hour_one_wakes_it_when_the_week_lifts_not_hours_later_forgotten()
    {
        State("s1", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5));
        var waker = Waker();
        Assert.That(waker.Tick(Open with { Stop = true, Governing = "weekly" }, [Tab], true, Now.AddMinutes(-6)), Is.Empty, "the week holds it from before the reset");
        Assert.That(waker.Tick(Open with { Stop = true, Governing = "weekly" }, [Tab], true, Now.AddHours(7)), Is.Empty, "the week holds it");
        Assert.That(waker.Tick(Open, [Tab], true, Now.AddHours(7).AddMinutes(1)).Single().Typed, Is.True, "the week lifted: woken");

        State("w1", 42, "idle", Now.AddMinutes(1), until: Now.AddHours(30), window: "weekly");
        Assert.That(waker.Tick(Open, [Tab], true, Now.AddHours(30).AddMinutes(2)).Single().Line, Does.Contain("weekly window reset"));
    }

    [Test]
    public void A_stop_already_forgotten_isnt_brought_back_by_a_later_windows_wind_down()
    {
        State("s1", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5));
        var waker = Waker();
        Assert.That(waker.Tick(Open, [Tab], true, Now.AddHours(7)), Is.Empty, "the daemon was down through it: forgotten");
        Assert.That(waker.Tick(Open with { WindDown = true }, [Tab], true, Now.AddHours(8)), Is.Empty);
        Assert.That(waker.Tick(Open, [Tab], true, Now.AddHours(8).AddMinutes(1)), Is.Empty, "the next window's wind-down began long after its reset");
        Assert.That(typed, Is.Empty);
    }

    [Test]
    public void A_cli_in_a_plain_terminal_with_the_setting_off_or_whose_tab_wont_take_it_is_said_to_you()
    {
        State("plain", 77, "idle", Now.AddHours(-1), until: Now.AddMinutes(-5), cwd: @"D:\Projects\MindAttic\Prose");
        var w = Waker().Tick(Open, [Tab], true, Now).Single();
        Assert.That((w.Typed, w.Line), Is.EqualTo((false,
            $"The 5-hour window reset at {Now.AddMinutes(-5).ToLocalTime():h:mm tt}: the CLI in Prose stopped for it at {Now.AddHours(-1).ToLocalTime():h:mm tt} and is still idle. " +
            "Tell it to carry on (it isn't in a Launcher tab the orchestrator can type into).")));

        State("off", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-4));
        Assert.That(Waker().Tick(Open, [Tab], type: false, Now).Single().Line, Does.Contain("resume-after-reset is off"));
        Assert.That(typed, Is.Empty, "off: nothing is typed");

        State("fails", 42, "idle", Now.AddHours(-1), until: Now.AddMinutes(-3));
        Assert.That(Waker(typingWorks: false).Tick(Open, [Tab], true, Now).Single().Line, Does.Contain("typing into its tab failed"));
    }

    [Test]
    public void A_stop_or_reset_on_another_day_is_said_with_its_weekday()
    {
        State("w1", 77, "idle", Now.AddDays(-3), until: Now.AddMinutes(-5), window: "weekly");
        var stopped = Now.AddDays(-3).ToLocalTime().ToString("ddd h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(Waker().Tick(Open, [Tab], true, Now).Single().Line, Does.Contain($"stopped for it at {stopped} "),
            "a weekly stop days back isn't read as today's");
        var reset = new LimitStop("5-hour", Now.AddHours(-26), Now.AddHours(-30));
        Assert.That(LimitWaker.ResumePrompt(reset, Now), Does.Contain(Now.AddHours(-26).ToLocalTime().ToString("ddd h:mm tt", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public void Only_one_daemon_holds_the_wake_lease_and_it_isnt_autopilots()
    {
        using var first = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory, LimitWaker.LeaseName);
        using var second = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory, LimitWaker.LeaseName);
        using var dispatch = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory);
        Assert.That((first.TryAcquire(), second.TryAcquire(), dispatch.TryAcquire()), Is.EqualTo((true, false, true)),
            "a second daemon doesn't type the resume again; an open dashboard holding autopilot's lease doesn't stop the wakes");
    }

    [Test]
    public void The_daemon_alerts_only_for_a_cli_it_couldnt_type_into()
    {
        var alerts = new List<string>();
        var daemon = new Daemon(store) { AlertSink = (_, message, _) => alerts.Add(message) };
        var stop = new LimitStop("5-hour", Now, Now.AddHours(-1));
        daemon.ReportWakes([new LimitWaker.Wake("a", "Prose", stop, true, "told"), new LimitWaker.Wake("b", "Prose", stop, false, "waiting")]);
        Assert.That(alerts, Is.EqualTo(new[] { "waiting" }));
    }
}
