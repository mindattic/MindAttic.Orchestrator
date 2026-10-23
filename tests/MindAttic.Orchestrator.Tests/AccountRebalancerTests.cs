using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Rebalance when an account runs low (M.ORCH-T608): a CLI on a login past its 5-hour pace target or weekly stop line
/// moves to a free signed-in login with more headroom, through the Launcher's restart-resume. Every folder is a scratch one,
/// and nothing is sent to a real tab.</summary>
public sealed class AccountRebalancerTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 16, 0, 0, DateTimeKind.Utc);
    private static readonly HostSession Tab = new("Claude", 77, "mindattic-host-claude-77", @"D:\Projects\Prose");
    private static readonly HostSession Tab2 = new("Claude", 78, "mindattic-host-claude-78", @"D:\Projects\Ideas");
    private readonly TempDirs temps = new();
    private AccountDirs dirs = null!;
    private List<(HostSession Host, string Message)> sent = [];
    private Dictionary<string, string> onOwn = new(StringComparer.Ordinal);
    private List<CliBilling> live = [];
    private Dictionary<string, ReportedUsage?> usage = new(StringComparer.OrdinalIgnoreCase); // "" is the global login
    // The rebalancing tests leave the API-key failover (M.ORCH-T664) off; its own tests turn it on.
    private readonly OrchestratorSettings settings = new() { FailoverToApiKey = false };
    private readonly OrchestratorSettings failover = new();
    private List<(string ConfigPath, bool Approve)> keyChoices = [];
    private bool hasKey = true;
    private string fallbackMode = BillingModes.Api;
    private JsonStore? store;
    private Dictionary<string, List<UsageSample>> samples = new(StringComparer.OrdinalIgnoreCase); // "" is the global login

    [SetUp]
    public void SetUp()
    {
        sent = [];
        keyChoices = [];
        hasKey = true;
        fallbackMode = BillingModes.Api;
        store = null;
        samples = new(StringComparer.OrdinalIgnoreCase);
        onOwn = new(StringComparer.Ordinal);
        usage = new(StringComparer.OrdinalIgnoreCase);
        var dir = temps.New();
        Directory.CreateDirectory(Path.Combine(dir, ".claude"));
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        live = [new CliBilling(4242, "sess-1", @"D:\Projects\Prose", "subscription", Tab)];
    }

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private AccountRebalancer Rebalancer()
    {
        var mover = new AccountMove(dirs)
        {
            Send = (h, m) => { sent.Add((h, m)); return Task.FromResult(true); },
            GlobalSignedIn = () => true,
            OnOwnLogin = () => onOwn,
            LiveOverride = (_, _) => live,
            NameOf = d => d.Length == 0 ? "Ryan" : "Ryan of " + Path.GetFileName(d),
        };
        var picker = new LoginPicker(dirs, () => onOwn.Values.Select(v => (string?)v).ToList());
        return new AccountRebalancer(mover, picker)
        {
            Usage = (a, _) => usage.GetValueOrDefault(a?.Email ?? ""),
            HasApiKey = () => hasKey,
            FallbackMode = () => fallbackMode,
            SetKeyChoice = (path, approve) => { keyChoices.Add((path, approve)); return null; },
            Store = store,
            Samples = a => samples.GetValueOrDefault(a?.Email ?? "") ?? [],
            GlobalAccount = () => null,
        };
    }

    private AccountDir SignedIn(string email)
    {
        var d = dirs.For(email);
        Directory.CreateDirectory(d.Directory);
        File.WriteAllText(d.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "u-" + email, emailAddress = email } }));
        return d;
    }

    private static ReportedUsage Used(double fiveHour, double week = 0) =>
        new(new ReportedWindow(fiveHour, Now.AddHours(2)), new ReportedWindow(week, Now.AddDays(3)), Now);

    private static string ConfigDirOf(string message) => (string)JsonNode.Parse(message)!["configDir"]!;

    private static string Field(string message, string name) => JsonNode.Parse(message)![name]!.ToString();

    [Test]
    public void A_cli_on_a_login_spent_to_its_pace_target_moves_to_the_free_login_with_the_most_headroom()
    {
        SignedIn("a@x.com");
        var b = SignedIn("b@x.com");
        usage[""] = Used(99);
        usage["a@x.com"] = Used(60);
        usage["b@x.com"] = Used(10);

        var moves = Rebalancer().Tick(null, [], [], settings, Now);

        Assert.That(moves, Has.Count.EqualTo(1));
        Assert.That(moves[0].Moved, Is.True);
        Assert.That(moves[0].Line, Does.Contain("b@x.com").And.Contain("5-hour window is at 99%"));
        Assert.That(sent.Select(s => ConfigDirOf(s.Message)), Is.EqualTo(new[] { b.Directory }));
    }

    [Test]
    public void A_cli_on_the_fallback_login_at_its_weekly_stop_line_moves_too()
    {
        var b = SignedIn("b@x.com");
        usage[""] = Used(20, week: 96);
        usage["b@x.com"] = Used(20, week: 30);

        var moves = Rebalancer().Tick(null, [], [], settings, Now);

        Assert.That(moves.Single().Line, Does.Contain("to b@x.com").And.Contain("week is at 96% (stops at 95%)"));
        Assert.That(ConfigDirOf(sent.Single().Message), Is.EqualTo(b.Directory));
    }

    [Test]
    public void A_pace_at_the_limit_counts_as_low()
    {
        SignedIn("b@x.com");
        var over = new Pace(PaceMode.Over, 0, 0, 0, null, "5-hour", Now.AddHours(1), "At the 5-hour limit.") { Stop = true };

        Assert.That(Rebalancer().Tick(over, [], [], settings, Now).Single().Moved, Is.True);
    }

    [Test]
    public void Nothing_moves_while_the_login_has_room_or_the_setting_is_off()
    {
        SignedIn("b@x.com");
        usage[""] = Used(50);
        Assert.That(Rebalancer().Tick(null, [], [], settings, Now), Is.Empty, "not low");

        usage[""] = Used(99);
        Assert.That(Rebalancer().Tick(null, [], [], settings with { RebalanceLogins = false }, Now), Is.Empty, "off");
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void A_login_with_a_cli_or_one_low_itself_is_never_a_target()
    {
        var b = SignedIn("b@x.com");
        SignedIn("c@x.com");
        live.Add(new CliBilling(5151, "sess-2", @"D:\Projects\Ideas", "subscription", Tab2));
        onOwn["sess-2"] = b.Directory;       // b has a CLI
        usage[""] = Used(99);
        usage["b@x.com"] = Used(0);
        usage["c@x.com"] = Used(10, week: 97); // c is past its stop line

        Assert.That(Rebalancer().Tick(null, [], [], settings, Now), Is.Empty);
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void A_target_barely_better_than_the_source_is_not_worth_a_restart()
    {
        SignedIn("b@x.com");
        usage[""] = Used(20, week: 95);          // headroom 0
        usage["b@x.com"] = Used(20, week: 90);   // headroom 5: under MinGain

        Assert.That(Rebalancer().Tick(null, [], [], settings, Now), Is.Empty);
    }

    [Test]
    public void A_moved_cli_waits_out_the_cool_down_and_its_target_stays_taken_until_it_arrives()
    {
        SignedIn("b@x.com");
        usage[""] = Used(99);
        usage["b@x.com"] = Used(10);
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Tick(null, [], [], settings, Now).Single().Moved, Is.True);

        // Still on the global login (its turn hasn't ended), with a second CLI there too: neither goes anywhere.
        live.Add(new CliBilling(5151, "sess-2", @"D:\Projects\Ideas", "subscription", Tab2));
        Assert.That(rebalancer.Tick(null, [], [], settings, Now.AddMinutes(5)), Is.Empty);
        Assert.That(sent, Has.Count.EqualTo(1));

        // Past the cool-down the first one is free to move again (it never arrived), and b is free again.
        Assert.That(rebalancer.Tick(null, [], [], settings, Now + AccountRebalancer.CoolDown + TimeSpan.FromMinutes(1)), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_cli_outside_the_launcher_or_billing_the_api_is_left_alone()
    {
        SignedIn("b@x.com");
        usage[""] = Used(99);
        live = [new CliBilling(1, "s-plain", @"D:\Projects\Prose", "subscription", null),
                new CliBilling(2, "s-api", @"D:\Projects\Ideas", "api-key", Tab2)];

        Assert.That(Rebalancer().Tick(null, [], [], settings, Now), Is.Empty);
    }

    [Test]
    public void The_daemons_moves_reach_the_dashboard_through_the_store()
    {
        var store = new JsonStore(Path.Combine(temps.New(), "store"));
        AccountRebalancer.Record(store, "first", Now);
        AccountRebalancer.Record(store, "second", Now.AddMinutes(1));

        Assert.That(AccountRebalancer.Since(store, Now).Select(m => m.Text), Is.EqualTo(new[] { "second" }));
        Assert.That(AccountRebalancer.Since(store, Now.AddMinutes(-1)).Select(m => m.Text), Is.EqualTo(new[] { "first", "second" }));
    }

    // Emergency failover to the API key (M.ORCH-T664).

    [Test]
    public void A_low_login_with_no_other_room_fails_over_to_the_api_key_for_that_cli_alone()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(20, week: 98);
        var rebalancer = Rebalancer();

        var moves = rebalancer.Tick(null, [], [], failover, Now);

        Assert.That(moves.Single().Moved, Is.False, "asked, not yet counted");
        Assert.That(moves[0].Asked, Is.True);
        var message = sent.Single().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "billing"), Is.EqualTo(BillingModes.Api));
        Assert.That(ConfigDirOf(message), Is.EqualTo(a.Directory), "it keeps its login; only its billing changes");
        Assert.That(Field(message, "now"), Is.EqualTo("true"), "past its own login's line: at once (M.ORCH-T681)");
        Assert.That(keyChoices, Is.EqualTo(new[] { (a.ConfigPath, true) }), "the key is approved in that login's config only");
        Assert.That(rebalancer.Failovers.Single().FromEmail, Is.EqualTo("a@x.com"));

        // Seen on the key: counted and toasted, then left there: not failed over again, nor asked to move.
        live = [live[0] with { Auth = "api-key" }];
        moves = rebalancer.Tick(null, [], [], failover, Now.AddMinutes(1));
        Assert.That(moves.Single().Moved, Is.True);
        Assert.That(moves[0].Line, Does.Contain("Prose moved to the API key: Ryan of a@x.com's week at 98%"));
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(2)), Is.Empty);
        Assert.That(sent, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_login_with_room_is_still_preferred_to_the_api_key()
    {
        var b = SignedIn("b@x.com");
        usage[""] = Used(99);
        usage["b@x.com"] = Used(10);
        var rebalancer = Rebalancer();

        Assert.That(rebalancer.Tick(null, [], [], failover, Now).Single().Moved, Is.True);
        Assert.That(Field(sent.Single().Message, "reason"), Is.EqualTo("account"));
        Assert.That(ConfigDirOf(sent.Single().Message), Is.EqualTo(b.Directory));
        Assert.That(keyChoices, Is.Empty);
        Assert.That(rebalancer.Failovers, Is.Empty);
    }

    [Test]
    public void A_mid_turn_limit_error_resumes_on_the_api_key_at_once()
    {
        SignedIn("b@x.com");
        usage[""] = Used(99);
        usage["b@x.com"] = Used(10);
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Tick(null, [], [], failover, Now).Single().Moved, Is.True, "moved toward b at its idle point");

        // Before it gets there a limit error ends its turn: b is taken (it's on its way), and it can't wait out the cool-down.
        var hit = new LimitHit(Now.AddMinutes(2), LimitKind.Session, Now.AddHours(1));
        TranscriptSession[] sessions = [new("sess-1", live[0].Cwd!, Now.AddMinutes(2), default, null) { Hits = [hit] }];
        var moves = rebalancer.Tick(null, [], [], failover, Now.AddMinutes(3), sessions);

        Assert.That(moves.Single().Asked, Is.True);
        var message = sent.Last().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "now"), Is.EqualTo("true"));
        Assert.That(ConfigDirOf(message), Is.EqualTo(""), "the global login it's on");
        Assert.That(rebalancer.Failovers.Single().UntilUtc, Is.EqualTo(hit.ResetUtc));

        live = [live[0] with { Auth = "api-key" }];
        moves = rebalancer.Tick(null, [], [], failover, Now.AddMinutes(4), sessions);
        Assert.That(moves.Single().Line, Does.Contain("Prose CLI moved to API key: Ryan's 5-hour limit was hit").And.Contain("at once"));
    }

    [Test]
    public void A_failed_over_cli_moves_back_to_its_login_after_the_reset()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(99);
        store = new JsonStore(Path.Combine(temps.New(), "store"));
        Assert.That(Rebalancer().Tick(null, [], [], failover, Now).Single().Asked, Is.True);
        live = [live[0] with { Auth = "api-key" }];

        // The daemon restarts before the reset: the failover is remembered, and nothing moves back while the login's spent.
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Failovers.Single().SessionId, Is.EqualTo("sess-1"));
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(1)).Single().Moved, Is.True, "seen on the key");
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddHours(1)), Is.Empty);

        // Past the reset, with a fresh window: asked back onto its login, and the key rejected there.
        var after = Now.AddHours(2).AddMinutes(1);
        usage["a@x.com"] = new ReportedUsage(new ReportedWindow(3, after.AddHours(5)), new ReportedWindow(40, after.AddDays(3)), after);
        var moves = rebalancer.Tick(null, [], [], failover, after);
        Assert.That(moves.Single().Asked, Is.True);
        Assert.That(rebalancer.Failovers, Has.Count.EqualTo(1), "not counted until it bills its login");

        // Restarted billing its login again: counted.
        live = [new CliBilling(4343, "sess-1", @"D:\Projects\Prose", "subscription", Tab)];
        moves = rebalancer.Tick(null, [], [], failover, after.AddMinutes(5));
        Assert.That(moves.Single().Moved, Is.True);
        Assert.That(moves[0].Line, Does.Contain("Prose CLI moved back to Ryan of a@x.com").And.Contain("billed only the gap (2h 1m)"));
        var message = sent.Last().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "billing"), Is.EqualTo(BillingModes.Team));
        Assert.That(ConfigDirOf(message), Is.EqualTo(a.Directory));
        Assert.That(keyChoices.Last(), Is.EqualTo((a.ConfigPath, false)));
        Assert.That(rebalancer.Failovers, Is.Empty);
        Assert.That(AccountRebalancer.LoadFailovers(store), Is.Empty);
    }

    [Test]
    public void No_vault_key_or_the_setting_off_leaves_it_waiting()
    {
        usage[""] = Used(99);
        hasKey = false;
        var moves = Rebalancer().Tick(null, [], [], failover, Now);
        Assert.That(moves.Single().Moved, Is.False);
        Assert.That(moves[0].Line, Does.Contain("no Claude key in the MindAttic Vault"));

        hasKey = true;
        Assert.That(Rebalancer().Tick(null, [], [], settings, Now), Is.Empty);
        Assert.That(sent, Is.Empty);
    }

    // Past a login's line, straight to the fallback, the API key (M.ORCH-T681).

    /// <summary>Taken through a failover to the key: asked, then seen on it.</summary>
    private IReadOnlyList<Rebalanced> FailOver(AccountRebalancer rebalancer, OrchestratorSettings with, DateTime at)
    {
        Assert.That(rebalancer.Tick(null, [], [], with, at).Single().Asked, Is.True);
        live = [live[0] with { Auth = "api-key" }];
        return rebalancer.Tick(null, [], [], with, at.AddSeconds(30));
    }

    [Test]
    public void At_98_percent_of_the_5_hour_window_the_cli_moves_to_the_key_at_once_with_no_other_login_preferred()
    {
        var a = SignedIn("a@x.com");
        SignedIn("b@x.com");                 // free, with all its room: never a target for a@x.com's CLI
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(98);
        usage["b@x.com"] = Used(0);
        var rebalancer = Rebalancer();

        var moves = FailOver(rebalancer, failover, Now);

        var message = sent.Single().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "billing"), Is.EqualTo(BillingModes.Api));
        Assert.That(Field(message, "now"), Is.EqualTo("true"), "at its next tool call, not its next idle point");
        Assert.That(ConfigDirOf(message), Is.EqualTo(a.Directory), "it keeps its login and its session");
        Assert.That(Field(message, "session"), Is.EqualTo("sess-1"));
        Assert.That(moves.Single().Moved, Is.True);
        Assert.That(moves[0].Line, Does.Contain("Prose moved to the API key: Ryan of a@x.com's 5-hour window at 98%"));
    }

    [Test]
    public void At_the_weekly_target_from_the_settings_the_cli_moves_to_the_key()
    {
        var a = SignedIn("a@x.com");
        SignedIn("b@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(20, week: 90);
        usage["b@x.com"] = Used(0, week: 0);

        // The line is the setting's (100 minus the weekly reserve): at the default reserve of 5, 90% isn't past it.
        Assert.That(Rebalancer().Tick(null, [], [], failover, Now), Is.Empty);
        Assert.That(sent, Is.Empty);

        var reserve10 = failover with { WeeklyReservePercent = 10 };
        var moves = FailOver(Rebalancer(), reserve10, Now);

        Assert.That(Field(sent.Single().Message, "billing"), Is.EqualTo(BillingModes.Api));
        Assert.That(ConfigDirOf(sent.Single().Message), Is.EqualTo(a.Directory), "not b@x.com's login, though it has room");
        Assert.That(moves.Single().Line, Does.Contain("Prose moved to the API key: Ryan of a@x.com's week at 90%"));
    }

    [Test]
    public void Nothing_moves_when_the_fallback_is_not_the_api_key_and_it_is_said_plainly_once()
    {
        var a = SignedIn("a@x.com");
        SignedIn("b@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(99);
        usage["b@x.com"] = Used(0);
        fallbackMode = BillingModes.Team;
        var rebalancer = Rebalancer();

        var moves = rebalancer.Tick(null, [], [], failover, Now);

        Assert.That(moves.Single().Moved, Is.False);
        Assert.That(moves[0].NeedsYou, Is.True);
        Assert.That(moves[0].Line, Does.Contain("the fallback isn't the API key").And.Contain("Claude Team"));
        Assert.That(sent, Is.Empty, "neither to the key nor to another person's login");
        Assert.That(keyChoices, Is.Empty);
        Assert.That(rebalancer.Tick(null, [], [], failover, Now + AccountRebalancer.CoolDown + TimeSpan.FromMinutes(1)), Is.Empty, "said once");
    }

    [Test]
    public void A_failover_the_tab_took_but_dropped_is_not_counted_and_is_asked_again()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(99);
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Tick(null, [], [], failover, Now).Single().Asked, Is.True);

        // An old tab restarted it on its login without the key: not on the key, said, and the key rejected again.
        live = [new CliBilling(4343, "sess-1", @"D:\Projects\Prose", "subscription", Tab)];
        var moves = rebalancer.Tick(null, [], [], failover, Now.AddSeconds(30));
        var dropped = moves.Single(m => m.NeedsYou);
        Assert.That(dropped.Moved, Is.False);
        Assert.That(dropped.Line, Does.Contain("didn't move to the API key").And.Contain("Close and reopen this tab"));
        Assert.That(moves.Any(m => m.Moved), Is.False);
        Assert.That(rebalancer.Failovers, Is.Empty);
        Assert.That(keyChoices.Last(), Is.EqualTo((a.ConfigPath, false)));

        // A tab that never restarts it is dropped too, once the time for it is up.
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(1)), Is.Empty, "not asked again at once");
        var again = rebalancer.Tick(null, [], [], failover, Now.AddSeconds(30) + AccountRebalancer.BackRetry);
        Assert.That(again.Single().Asked, Is.True, "asked again after the retry");
        Assert.That(sent, Has.Count.EqualTo(2));
        var late = rebalancer.Tick(null, [], [], failover, Now.AddSeconds(30) + AccountRebalancer.BackRetry + AccountRebalancer.ConfirmWithin);
        Assert.That(late.Single(m => m.NeedsYou).Line, Does.Contain("never restarted"));
    }

    [Test]
    public void A_move_back_the_tab_dropped_keeps_it_on_the_key_and_is_said()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(99);
        var rebalancer = Rebalancer();
        Assert.That(FailOver(rebalancer, failover, Now).Single().Moved, Is.True);

        var after = Now.AddHours(2).AddMinutes(1);
        usage["a@x.com"] = new ReportedUsage(new ReportedWindow(3, after.AddHours(5)), new ReportedWindow(40, after.AddDays(3)), after);
        Assert.That(rebalancer.Tick(null, [], [], failover, after).Single().Asked, Is.True);

        // Restarted, still on the key: it stays booked on it, the key approved again, and you're told.
        live = [new CliBilling(4343, "sess-1", @"D:\Projects\Prose", "api-key", Tab)];
        var moves = rebalancer.Tick(null, [], [], failover, after.AddMinutes(1));
        Assert.That(moves.Single().NeedsYou, Is.True);
        Assert.That(moves[0].Line, Does.Contain("didn't move back").And.Contain("Close and reopen this tab"));
        Assert.That(rebalancer.Failovers, Has.Count.EqualTo(1));
        Assert.That(keyChoices.Last(), Is.EqualTo((a.ConfigPath, true)));
    }

    // A member using Claude outside the orchestrator (M.ORCH-T682): their login's CLIs stay on it, held to a lower ceiling.

    private static readonly DateTime Window = Now.AddMinutes(25), Week = Now.AddDays(3);

    /// <summary>a@x.com's readings: a past window that taught it 100k tokens a point (and 0.2 week points per 5-hour point),
    /// then one every 5 minutes of the running window (resetting 25 minutes from now) from <paramref name="from"/>.</summary>
    private void Readings(DateTime from, double[] percents, long[] fresh, double[]? week = null)
    {
        var past = Now.AddHours(-1);
        var list = new List<UsageSample>
        {
            new(past.AddHours(-4), 0, past, 0) { WeekPercent = 30, WeekResetUtc = Week },
            new(past.AddHours(-1), 10, past, 1_100_000) { WeekPercent = 32, WeekResetUtc = Week },
        };
        for (var i = 0; i < percents.Length; i++)
            list.Add(new(from.AddMinutes(5 * i), percents[i], Window, fresh[i]) { WeekPercent = week?[i] ?? 40, WeekResetUtc = Week });
        samples["a@x.com"] = list;
    }

    private static long[] Idle(int n) => [.. Enumerable.Repeat(500_000L, n)];

    private AccountDir OnA()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(26, week: 41);
        return a;
    }

    private (OrchestratorSettings Settings, string? Why) PacedAs(AccountRebalancer rebalancer, string email = "a@x.com") =>
        OutsideUseWatch.Apply(rebalancer.Outside.Logins, email, failover);

    [Test]
    public void A_meter_rise_the_clis_explain_changes_nothing()
    {
        OnA();
        // 10 points in half an hour, and the CLIs spent 1M (10 points at 100k a point) of it.
        Readings(Now.AddMinutes(-30), [20, 21, 23, 25, 27, 29, 30], [0, 100_000, 300_000, 500_000, 700_000, 900_000, 1_000_000]);
        var rebalancer = Rebalancer();

        Assert.That(rebalancer.Tick(null, [], [], failover, Now), Is.Empty);
        Assert.That(rebalancer.Outside.InUse("a@x.com"), Is.False);

        // Two points with nothing spent here is the meter's rounding at each end, not someone: still nothing.
        Readings(Now.AddMinutes(-25), [30, 30, 31, 31, 31, 32], Idle(6));
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(5)), Is.Empty);
        Assert.That(sent, Is.Empty);
        Assert.That(PacedAs(rebalancer).Settings.PaceTargetPercent, Is.EqualTo(98), "the full target");
        Assert.That(rebalancer.Outside.Logins.Single().Windows.Single().Points, Is.Zero, "the window is recorded, with no outside use");
    }

    [Test]
    public void An_unexplained_rise_holds_that_logins_clis_to_90_percent_without_moving_them_and_restores_after_the_quiet_period()
    {
        OnA();
        var b = SignedIn("b@x.com");
        usage["b@x.com"] = Used(0);
        live.Add(new CliBilling(5151, "sess-2", @"D:\Projects\Ideas", "subscription", Tab2));
        onOwn["sess-2"] = b.Directory;
        // Six points in 25 minutes while the CLIs here spent nothing: someone's on claude.ai.
        Readings(Now.AddMinutes(-25), [20, 20, 22, 23, 25, 26], Idle(6));
        var rebalancer = Rebalancer();

        var told = rebalancer.Tick(null, [], [], failover, Now).Single();
        Assert.That(told.Title, Is.EqualTo("Ryan of a@x.com is using Claude"));
        Assert.That(told.Line, Does.Contain("the meter rose 6 points in 25 min; the CLIs here spent 0 of them")
            .And.Contain("Ryan of a@x.com's CLIs are held to 90% of this window").And.Contain("back to 98% about 20 minutes after"));
        Assert.That(sent, Is.Empty, "its CLIs stay on its login");
        var (paced, why) = PacedAs(rebalancer);
        Assert.That(paced.PaceTargetPercent, Is.EqualTo(90));
        Assert.That(why, Does.Contain("Held to 90% of this window").And.Contain("Ryan of a@x.com is using Claude outside the orchestrator"));
        Assert.That(PacedAs(rebalancer, "b@x.com").Settings.PaceTargetPercent, Is.EqualTo(98), "only the login whose meter shows it");

        // Its pace stops the CLIs at the lowered ceiling: that's the pace slowing them, not its line, so nothing moves.
        var stopped = new Pace(PaceMode.Over, 0, 0, 0, null, "5-hour", Window, "At the ceiling.") { Stop = true };
        var paces = new[] { new AccountPace("a@x.com", "", null, null!, stopped, null, 1) };
        Assert.That(rebalancer.Tick(null, paces, [], failover, Now.AddMinutes(10)), Is.Empty);
        Assert.That(sent, Is.Empty);

        // Twenty minutes with nothing new: the full target is back.
        var ended = rebalancer.Tick(null, [], [], failover, Now.AddMinutes(20)).Single();
        Assert.That(ended.Title, Is.EqualTo("Ryan of a@x.com stopped using Claude"));
        Assert.That(ended.Line, Does.Contain("back to 98% of this window"));
        Assert.That(PacedAs(rebalancer).Settings.PaceTargetPercent, Is.EqualTo(98));
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void A_heavy_outside_rate_lowers_the_ceiling_further_but_never_below_what_is_spent()
    {
        OnA();
        // Twenty points in 25 minutes, 25 minutes before the reset: at that rate they'd need 20 more, plus the headroom.
        Readings(Now.AddMinutes(-25), [20, 24, 28, 32, 36, 40], Idle(6));
        var rebalancer = Rebalancer();

        Assert.That(rebalancer.Tick(null, [], [], failover, Now).Single().Line, Does.Contain("held to 78% of this window"));
        Assert.That(PacedAs(rebalancer).Settings.PaceTargetPercent, Is.EqualTo(78));

        // At 90 points spent, the same rate can't take it under what's spent.
        Readings(Now.AddMinutes(-25), [70, 74, 78, 82, 86, 90], Idle(6));
        var heavy = Rebalancer();
        heavy.Tick(null, [], [], failover, Now);
        Assert.That(PacedAs(heavy).Settings.PaceTargetPercent, Is.EqualTo(90));
    }

    [Test]
    public void Use_seen_again_keeps_the_ceiling_down()
    {
        OnA();
        Readings(Now.AddMinutes(-25), [20, 20, 22, 23, 25, 26], Idle(6));
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Tick(null, [], [], failover, Now).Single().Title, Is.Not.Null);

        // Two more points ten minutes on, with nothing spent here: still them.
        Readings(Now.AddMinutes(-25), [20, 20, 22, 23, 25, 26, 27, 28], Idle(8));
        rebalancer.Tick(null, [], [], failover, Now.AddMinutes(10));
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(20)).Where(m => m.Title is not null), Is.Empty, "seen 10 minutes ago");
        Assert.That(rebalancer.Outside.InUse("a@x.com"), Is.True);
    }

    [Test]
    public void Each_members_outside_use_is_recorded_per_window_and_week_with_suggested_reserves()
    {
        OnA();
        store = new JsonStore(Path.Combine(temps.New(), "store"));
        Readings(Now.AddMinutes(-25), [20, 20, 22, 23, 25, 26], Idle(6), week: [40, 40, 40.5, 41, 41, 42.5]);
        Rebalancer().Tick(null, [], [], failover, Now);

        var stats = OutsideUseWatch.Stats(OutsideUseWatch.Load(store), failover).Single();
        Assert.That(stats.Key, Is.EqualTo("a@x.com"));
        Assert.That(stats.Name, Is.EqualTo("Ryan of a@x.com"));
        Assert.That(stats.InUse, Is.True);
        Assert.That(stats.HeldTo, Is.EqualTo(90));
        Assert.That(stats.WindowCount, Is.EqualTo(1));
        Assert.That(stats.PerWindow, Is.EqualTo(6).Within(1e-9));
        Assert.That(stats.PerWeek, Is.EqualTo(2.5).Within(1e-9));
        Assert.That(stats.FiveHourReserve, Is.EqualTo(6));
        Assert.That(stats.WeeklyReserve, Is.EqualTo(3));
        Assert.That(stats.Text, Does.Contain("held to 90% of this window").And.Contain("6% a window, 2.5% a week on average")
            .And.Contain("5-hour reserve 6% (pace target 94%), weekly reserve 3%, outside-use ceiling 90%"));
    }

}
