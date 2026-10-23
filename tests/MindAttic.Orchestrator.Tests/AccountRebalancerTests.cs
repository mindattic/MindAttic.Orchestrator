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
    private JsonStore? store;

    [SetUp]
    public void SetUp()
    {
        sent = [];
        keyChoices = [];
        hasKey = true;
        store = null;
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
            SetKeyChoice = (path, approve) => { keyChoices.Add((path, approve)); return null; },
            Store = store,
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
    public void A_cli_on_a_login_at_its_weekly_stop_line_moves_too()
    {
        var a = SignedIn("a@x.com");
        var b = SignedIn("b@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(20, week: 96);
        usage["b@x.com"] = Used(20, week: 30);

        var moves = Rebalancer().Tick(null, [], [], settings, Now);

        Assert.That(moves.Single().Line, Does.Contain("from a@x.com to b@x.com").And.Contain("week is at 96% (stops at 95%)"));
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
        var a = SignedIn("a@x.com");
        SignedIn("b@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(20, week: 95);   // headroom 0
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

        Assert.That(moves.Single().Moved, Is.True);
        Assert.That(moves[0].Line, Does.Contain("Prose CLI moved to API key: Ryan of a@x.com's week is at 98%"));
        var message = sent.Single().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "billing"), Is.EqualTo(BillingModes.Api));
        Assert.That(ConfigDirOf(message), Is.EqualTo(a.Directory), "it keeps its login; only its billing changes");
        Assert.That(Field(message, "now"), Is.EqualTo("false"), "no limit error: it waits for its idle point");
        Assert.That(keyChoices, Is.EqualTo(new[] { (a.ConfigPath, true) }), "the key is approved in that login's config only");
        Assert.That(rebalancer.Failovers.Single().FromEmail, Is.EqualTo("a@x.com"));

        // On the key, it's left there: not failed over again, nor asked to move.
        live = [live[0] with { Auth = "api-key" }];
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddMinutes(1)), Is.Empty);
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

        Assert.That(moves.Single().Line, Does.Contain("Prose CLI moved to API key: Ryan's 5-hour limit was hit").And.Contain("at once"));
        var message = sent.Last().Message;
        Assert.That(Field(message, "reason"), Is.EqualTo("failover"));
        Assert.That(Field(message, "now"), Is.EqualTo("true"));
        Assert.That(ConfigDirOf(message), Is.EqualTo(""), "the global login it's on");
        Assert.That(rebalancer.Failovers.Single().UntilUtc, Is.EqualTo(hit.ResetUtc));
    }

    [Test]
    public void A_failed_over_cli_moves_back_to_its_login_after_the_reset()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        usage["a@x.com"] = Used(99);
        store = new JsonStore(Path.Combine(temps.New(), "store"));
        Assert.That(Rebalancer().Tick(null, [], [], failover, Now).Single().Moved, Is.True);
        live = [live[0] with { Auth = "api-key" }];

        // The daemon restarts before the reset: the failover is remembered, and nothing moves back while the login's spent.
        var rebalancer = Rebalancer();
        Assert.That(rebalancer.Failovers.Single().SessionId, Is.EqualTo("sess-1"));
        Assert.That(rebalancer.Tick(null, [], [], failover, Now.AddHours(1)), Is.Empty);

        // Past the reset, with a fresh window: back on its login, billing it again, and the key rejected there.
        var after = Now.AddHours(2).AddMinutes(1);
        usage["a@x.com"] = new ReportedUsage(new ReportedWindow(3, after.AddHours(5)), new ReportedWindow(40, after.AddDays(3)), after);
        var moves = rebalancer.Tick(null, [], [], failover, after);

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
}
