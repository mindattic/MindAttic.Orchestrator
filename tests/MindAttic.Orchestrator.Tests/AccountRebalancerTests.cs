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
    private readonly OrchestratorSettings settings = new();

    [SetUp]
    public void SetUp()
    {
        sent = [];
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
        };
        var picker = new LoginPicker(dirs, () => onOwn.Values.Select(v => (string?)v).ToList());
        return new AccountRebalancer(mover, picker) { Usage = (a, _) => usage.GetValueOrDefault(a?.Email ?? "") };
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
}
