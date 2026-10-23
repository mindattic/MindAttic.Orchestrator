using System.Text.Json;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T694: a login with less than its weekly reserve left (past the weekly target, 100 minus the reserve) takes no ticket
/// and its CLIs stop until its weekly reset, unless the user lets that one login past with <c>org allow</c>; <c>org disallow</c>
/// and the weekly reset end it. Saved in the database, logged, shown in the usage report.
/// </summary>
public sealed class WeeklyAllowanceTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(4);
    private const string Erin = "ohleo199@mindattic.com";

    // The live value: a 10% weekly reserve, so the week's target is 90%.
    private static readonly OrchestratorSettings Settings = new() { BlockBudget = 1_000_000, WeeklyReservePercent = 10 };

    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static BudgetStatus Budget(double weekPercent, OrchestratorSettings settings, DateTime? now = null)
    {
        var at = now ?? Now;
        var reported = new ReportedUsage(new ReportedWindow(20, at.AddHours(3)), new ReportedWindow(weekPercent, WeekReset), at.AddMinutes(-1));
        UsageEvent[] events = [new(at.AddHours(-1), new TokenUsage(0, 0, 0, 10_000))];
        return BudgetCalculator.Compute(events, settings, at, at.Date, TimeZoneInfo.Utc, reported: reported);
    }

    private static WeeklyAllowance Allowed(DateTime? until = null) =>
        new() { Email = Erin, Name = "Erin", SinceUtc = Now.AddMinutes(-5), UntilUtc = until ?? WeekReset, WeekResetUtc = WeekReset };

    private static AccountPace Account(BudgetStatus b, OrchestratorSettings s, DateTime now) =>
        new(Erin, @"C:\x\" + Erin, "acct", b, Pacer.Compute(b, 1, null, s, now), null, 1);

    [Test]
    public void A_login_under_its_weekly_reserve_takes_no_ticket_and_its_clis_stop()
    {
        var b = Budget(92, Settings);
        var p = Pacer.Compute(b, 1, null, Settings, Now);

        Assert.That(Settings.WeeklyStopPercent, Is.EqualTo(90), "the target comes from the weekly reserve");
        Assert.That((b.WeeklyStopped(Now), p.Stop, p.Governing), Is.EqualTo((true, true, "weekly")), "its CLIs stop until the reset");
        Assert.That(p.ResetUtc, Is.EqualTo(WeekReset));
        Assert.That(LoginRoom.Of(Account(b, Settings, Now), Settings).Open, Is.False, "nothing is redelegated to it");

        var plan = Scheduler.Plan(new Snapshot(Now, [], []) { Budget = b, Pace = p }, Settings, []);
        var (handed, why) = Autopilot.Next(plan, Settings, new JsonStore(temps.New()), ["*"], null, claim: true, Now);
        Assert.That(handed, Is.Empty);
        Assert.That(why, Does.Contain("weekly"));
    }

    [Test]
    public void Allowed_the_login_takes_work_past_the_target_until_its_reset()
    {
        var (s, why) = WeeklyAllowances.Apply([Allowed()], Erin, Settings, Now);
        var b = Budget(92, s);
        var p = Pacer.Compute(b, 1, null, s, Now);

        Assert.That(why, Does.Contain("Allowed past the 90% weekly target by you"));
        Assert.That((s.WeeklyStopPercent, b.WeeklyStopPercent, b.WeeklyStopped(Now), p.Stop), Is.EqualTo((100, 100, false, false)));
        var room = LoginRoom.Of(Account(b, s, Now), s);
        Assert.That((room.Open, room.Headroom), Is.EqualTo((true, 8.0)), "its week up to the wall is its room");

        // Only that one login: anyone else is still held to the target.
        Assert.That(WeeklyAllowances.Apply([Allowed()], "honey@mindattic.com", Settings, Now).Settings.WeeklyStopPercent, Is.EqualTo(90));
        Assert.That(WeeklyAllowances.StopFor([Allowed()], null, Settings, Now), Is.EqualTo(90));
    }

    [Test]
    public void The_allowance_reaches_the_gate_through_that_logins_status()
    {
        var path = Path.Combine(temps.New(), "rl-status.json");
        var (s, _) = WeeklyAllowances.Apply([Allowed()], Erin, Settings, Now);
        var b = Budget(92, s);
        new RateLimitStatusWriter(path) { UtcNow = () => Now, Db = null }.Write(b, Pacer.Compute(b, 1, null, s, Now));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        Assert.That(doc.RootElement.GetProperty("weekly_stop_percent").GetInt32(), Is.EqualTo(100), "the hooks' weekly line for that login");
        Assert.That(doc.RootElement.TryGetProperty("stop", out _), Is.False, "nothing is refused");
    }

    [Test]
    public void The_weekly_reset_or_its_time_ends_it()
    {
        Assert.That(WeeklyAllowances.For([Allowed()], Erin, WeekReset.AddMinutes(-1)), Is.Not.Null);
        Assert.That(WeeklyAllowances.For([Allowed()], Erin, WeekReset), Is.Null, "ends at the weekly reset");
        Assert.That(WeeklyAllowances.For([Allowed(until: WeekReset.AddDays(3))], Erin, WeekReset.AddMinutes(1)), Is.Null,
            "never into the next week, whatever --until said");
        Assert.That(WeeklyAllowances.For([Allowed(until: Now.AddHours(2))], Erin, Now.AddHours(2)), Is.Null, "an --until time ends it there");

        var after = WeekReset.AddMinutes(1);
        var (s, why) = WeeklyAllowances.Apply([Allowed()], Erin, Settings, after);
        Assert.That((s.WeeklyStopPercent, why), Is.EqualTo((90, (string?)null)), "back to the target in the new week");
    }

    [Test]
    public void Allow_disallow_and_expiry_are_saved_in_the_database_and_logged()
    {
        var store = new JsonStore(temps.New());
        var (allowance, line) = WeeklyAllowances.Allow(store, Erin, "Erin", WeekReset, WeekReset, 90, Now);

        Assert.That(line, Does.StartWith("Erin (ohleo199@mindattic.com) allowed past the 90% weekly target by you until").And.EndWith("(its weekly reset)."));
        Assert.That(WeeklyAllowances.Active(new JsonStore(store.Directory), Now).Single().Email, Is.EqualTo(Erin), "read back from the database");
        Assert.That(allowance.EndsUtc, Is.EqualTo(WeekReset));

        Assert.That(WeeklyAllowances.Disallow(store, Erin, "Erin", 90, Now.AddHours(1)), Does.Contain("no longer allowed past the 90% weekly target: taken back by you"));
        Assert.That(WeeklyAllowances.Active(store, Now.AddHours(1)), Is.Empty);
        Assert.That(WeeklyAllowances.Disallow(store, Erin, "Erin", 90, Now.AddHours(1)), Is.Null, "nothing left to take back");

        WeeklyAllowances.Allow(store, Erin, "Erin", WeekReset, WeekReset, 90, Now.AddHours(2));
        Assert.That(WeeklyAllowances.Prune(store, Now.AddHours(3)), Is.Empty, "still in force");
        Assert.That(WeeklyAllowances.Prune(store, WeekReset.AddMinutes(1)).Single(), Does.Contain("no longer allowed past the weekly target: its week reset"));
        var book = WeeklyAllowances.Load(store);
        Assert.That(book.Allowances, Is.Empty);
        Assert.That(book.Log.Select(l => l.Text).ToList(), Has.Count.EqualTo(4).And.Some.Contains("taken back").And.Some.Contains("its week reset"));
    }

    [Test]
    public void The_usage_report_says_who_is_allowed_in_its_notes_not_a_column()
    {
        var members = new[] { new Member { Email = Erin, Name = "Erin" } };
        var logins = new[] { new LoginReading(Erin, "acct", null, new ReportedUsage(new ReportedWindow(20, Now.AddHours(3)), new ReportedWindow(93, WeekReset), Now.AddMinutes(-2))) };
        var held = UsageReport.Build(members, [], logins, [], new Dictionary<string, BudgetStatus>(), 90, Now);
        Assert.That(held.Notes(), Has.Some.Contains("At or past the 90% weekly target").And.Some.Contains("Erin"));

        var allowed = UsageReport.Build(members, [], logins, [], new Dictionary<string, BudgetStatus>(), 90, Now, allowances: [Allowed()]);
        var until = WeekReset.ToLocalTime().ToString("ddd h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(allowed.Notes(), Has.Some.EqualTo($"Erin: allowed past the target by you until {until}."));
        Assert.That(allowed.Notes(), Has.None.Contains("At or past the 90% weekly target"), "it takes work: not listed as held");
        Assert.That(allowed.Headers, Is.EqualTo(held.Headers), "no new column");
    }

    [Test]
    public void The_rebalancer_leaves_an_allowed_logins_cli_on_it_instead_of_failing_it_over()
    {
        var dir = temps.New();
        Directory.CreateDirectory(Path.Combine(dir, ".claude"));
        var dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        var erin = dirs.For(Erin);
        Directory.CreateDirectory(erin.Directory);
        File.WriteAllText(erin.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "u-erin", emailAddress = Erin } }));
        var sent = new List<string>();
        var onOwn = new Dictionary<string, string>(StringComparer.Ordinal) { ["sess-1"] = erin.Directory };
        var tab = new HostSession("Claude", 77, "mindattic-host-claude-77", @"D:\Projects\Prose");
        var live = new List<CliBilling> { new(4242, "sess-1", @"D:\Projects\Prose", "subscription", tab) };
        IReadOnlyList<WeeklyAllowance> allowances = [];
        AccountRebalancer Rebalancer() => new(new AccountMove(dirs)
        {
            Send = (_, m) => { sent.Add(m); return Task.FromResult(true); },
            GlobalSignedIn = () => true, OnOwnLogin = () => onOwn, LiveOverride = (_, _) => live, NameOf = _ => "Erin",
        }, new LoginPicker(dirs, () => onOwn.Values.Select(v => (string?)v).ToList()))
        {
            Usage = (a, _) => a is null ? null : new ReportedUsage(new ReportedWindow(20, Now.AddHours(2)), new ReportedWindow(92, WeekReset), Now),
            HasApiKey = () => true, FallbackMode = () => Billing.BillingModes.Api, SetKeyChoice = (_, _) => null,
            Samples = _ => [], GlobalAccount = () => null, GlobalEmail = () => null, AllowancesOverride = () => allowances,
        };
        var failover = Settings with { FailoverToApiKey = true };

        allowances = [Allowed()];
        Assert.That(Rebalancer().Tick(null, [], [], failover, Now), Is.Empty, "allowed: its CLI keeps working on its login");
        Assert.That(sent, Is.Empty);

        allowances = [];
        Assert.That(Rebalancer().Tick(null, [], [], failover, Now).Single().Asked, Is.True, "held to the target: past it, it goes to the API key");
        Assert.That(sent, Has.Count.EqualTo(1));
    }

    [Test]
    public void The_login_picker_offers_an_allowed_login_for_a_new_tab()
    {
        var usage = new ReportedUsage(new ReportedWindow(20, Now.AddHours(2)), new ReportedWindow(92, WeekReset), Now);
        var dir = temps.New();
        var dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        var erin = dirs.For(Erin);
        Assert.That(LoginPicker.Choose([erin], new HashSet<string>(), _ => usage, Now, 90), Is.Null, "past the target: no headroom");
        Assert.That(LoginPicker.Choose([erin], new HashSet<string>(), _ => usage, Now, 90,
            e => WeeklyAllowances.StopFor([Allowed()], e, Settings, Now)), Is.EqualTo(erin));
    }

    // ---- orchestrator org allow / disallow -------------------------------------------------------------------------------

    private static string Cli(out int exit, params string[] args)
    {
        var output = new StringWriter();
        var previous = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No,
        });
        AnsiConsole.Console.Profile.Width = 240;
        try { exit = CliApp.Create().Run(args); }
        finally { AnsiConsole.Console = previous; }
        return output.ToString();
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory, AnsiConsole.Console and the org overrides are process-wide
    public void Org_allow_and_disallow_name_a_member_any_way_and_default_to_the_weekly_reset()
    {
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var store = new JsonStore(storeDir);
        store.SaveSettings(store.LoadSettings() with { WeeklyReservePercent = 10 });
        OrgHelpCommand.PeopleOverride = () => [new(Erin, "Erin", [], []), new("honey@mindattic.com", "Honey", [], [])];
        OrgAllowCommand.NowOverride = () => Now;
        DateTime? reset = WeekReset;
        OrgAllowCommand.WeekResetOverride = (_, _) => reset;
        try
        {
            var said = Cli(out var exit, "org", "allow", "E");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com) allowed past the 90% weekly target by you until").And.Contain("(its weekly reset)"));
            Assert.That(WeeklyAllowances.Active(store, Now).Single().EndsUtc, Is.EqualTo(WeekReset));

            said = Cli(out exit, "org", "allow", "erin", "--until", "2099-01-01 00:00");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("ends at its weekly reset"), "never past the reset");
            Assert.That(WeeklyAllowances.Active(store, Now).Single().EndsUtc, Is.EqualTo(WeekReset));

            said = Cli(out exit, "org", "allow", "erin", "--until", "someday");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("--until is \"reset\" or a local time"));

            said = Cli(out exit, "org", "disallow", "ohleo199");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("no longer allowed past the 90% weekly target: taken back by you"));
            Assert.That(WeeklyAllowances.Active(store, Now), Is.Empty);

            reset = null;
            said = Cli(out exit, "org", "allow", "Honey");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("weekly reset isn't known here").And.Contain("--until"));

            said = Cli(out exit, "org", "help");
            Assert.That(said, Does.Contain("allow <who> [--until <reset|time>]").And.Contain("disallow <who>"));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
            OrgAllowCommand.NowOverride = null;
            OrgAllowCommand.WeekResetOverride = null;
        }
    }

    [Test]
    public void An_until_time_is_read_as_local_time()
    {
        var local = Now.ToLocalTime();
        var at = OrgAllowCommand.ParseUntil(local.AddHours(3).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture), Now);
        Assert.That(at, Is.EqualTo(new DateTime(local.AddHours(3).Ticks - local.AddHours(3).Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Local).ToUniversalTime()));
        var earlier = OrgAllowCommand.ParseUntil(local.AddHours(-1).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), Now);
        Assert.That(earlier, Is.GreaterThan(Now), "a time already past today is tomorrow's");
        var tuesday = OrgAllowCommand.ParseUntil("Tue 5:00 PM", Now)!.Value.ToLocalTime();
        Assert.That((tuesday.DayOfWeek, tuesday.Hour, tuesday.Minute), Is.EqualTo((DayOfWeek.Tuesday, 17, 0)));
        Assert.That(tuesday, Is.GreaterThan(local));
        Assert.That(OrgAllowCommand.ParseUntil("someday", Now), Is.Null);
    }
}
