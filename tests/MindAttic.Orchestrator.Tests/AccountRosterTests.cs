using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The per-account usage roster (M.ORCH-T490): every account signed in to, its latest 5h and weekly percents and
/// resets, refreshed on each sign-in and each point the week moves, rolled to 0 once a reset passes.</summary>
[NonParallelizable] // one test sets MINDATTIC_ORCHESTRATOR_DB, which is process-wide
public sealed class AccountRosterTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(3);
    private static readonly DateTime FiveReset = Now.AddHours(2);

    private readonly TempDirs temps = new();
    private string dir = null!;
    private OrchestratorDb db = null!;
    private ClaudeAccount account = null!;

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        db = new OrchestratorDb(Path.Combine(dir, "repo", "data", "orchestrator.db"));
        account = new ClaudeAccount { ConfigPath = Path.Combine(dir, ".claude.json"), StatePath = Path.Combine(dir, "session-context", ClaudeAccount.FileName) };
    }

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private void SignIn(string id, string email, DateTime stamp)
    {
        var path = Path.Combine(dir, ".claude.json");
        File.WriteAllText(path, $$$"""{"oauthAccount":{"accountUuid":"{{{id}}}","organizationUuid":"org","emailAddress":"{{{email}}}","organizationName":"MindAttic","organizationType":"claude_team","userRateLimitTier":"default_claude_max_5x"}}""");
        File.SetLastWriteTimeUtc(path, stamp);
    }

    private static ReportedUsage Reading(double five, double week, DateTime at, DateTime? fiveReset = null, DateTime? weekReset = null) =>
        new(new ReportedWindow(five, fiveReset ?? FiveReset), new ReportedWindow(week, weekReset ?? WeekReset), at);

    private bool Observe(ReportedUsage? reading, DateTime now)
    {
        var signIn = account.Read(now)!;
        return AccountRoster.Observe(db, signIn, account.Profile, reading, now);
    }

    private AccountRow Row(string id) => AccountRoster.Load(db).Single(r => r.Account == id + "/org");

    [Test]
    public void A_new_sign_in_adds_its_row_with_who_it_is_and_the_meter()
    {
        SignIn("a", "a@mindattic.com", Now);
        Observe(Reading(12, 75, Now), Now);

        var row = Row("a");
        Assert.That(row.Email, Is.EqualTo("a@mindattic.com"));
        Assert.That(row.Org, Is.EqualTo("MindAttic"));
        Assert.That(row.Plan, Is.EqualTo("Team · Max 5x"));
        Assert.That((row.WeekPercent, row.WeekResetUtc, row.FiveHourPercent, row.FiveHourResetUtc, row.ReadUtc),
            Is.EqualTo(((double?)75, (DateTime?)WeekReset, (double?)12, (DateTime?)FiveReset, (DateTime?)Now)));
        Assert.That(row.WeekLeftAt(Now), Is.EqualTo(25));
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("INSERT INTO accounts (id, email, org, plan, five_hour_percent")
            .And.Contain("'a/org', 'a@mindattic.com', 'MindAttic', 'Team · Max 5x', 12, ").And.Contain(", 75, "),
            "the readable dump beside the database is rewritten with the change");
    }

    [Test]
    public void Accounts_signed_in_before_the_roster_show_up_from_the_sign_in_record()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(account.StatePath)!);
        string E(DateTime t) => ((t - DateTime.UnixEpoch).TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        File.WriteAllText(account.StatePath,
            "{\"spans\":[{\"account\":\"b/org\",\"from\":" + E(Now.AddDays(-2)) + ",\"to\":" + E(Now.AddDays(-1)) + "}," +
            "{\"account\":\"a/org\",\"from\":" + E(Now.AddDays(-1)) + ",\"to\":null}]," +
            "\"confirmed\":{\"b/org\":{\"five_hour\":" + E(Now.AddHours(-20)) + ",\"seven_day\":" + E(Now.AddDays(1)) + "}," +
            "\"c/org\":{\"five_hour\":" + E(Now.AddHours(-30)) + ",\"seven_day\":" + E(Now.AddDays(-2)) + "}}}");
        SignIn("a", "a@mindattic.com", Now.AddDays(-1));
        Observe(Reading(12, 75, Now), Now);

        Assert.That(AccountRoster.Load(db, "a/org").Select(r => r.Account), Is.EqualTo(new[] { "a/org", "b/org", "c/org" }), "signed in first");
        var b = Row("b");
        Assert.That(b.Email, Is.Null, "its email is only known once it's signed in to");
        Assert.That(b.WeekResetAt(Now), Is.EqualTo(Now.AddDays(1)));
        Assert.That(b.WeekUsedAt(Now), Is.Null, "never read");
        Assert.That(b.FiveHourUsedAt(Now), Is.EqualTo(0), "its 5-hour window reset since");
        Assert.That(Row("c").WeekUsedAt(Now), Is.EqualTo(0), "its week reset since");
        Assert.That(Row("c").WeekResetAt(Now), Is.EqualTo(Now.AddDays(5)), "the next weekly reset, a week on");
    }

    [Test]
    public void The_row_is_refreshed_on_each_point_the_week_moves_and_the_meter_is_read_then()
    {
        SignIn("a", "a@mindattic.com", Now);
        Assert.That(Observe(Reading(12, 75, Now), Now), Is.False, "a first reading is the meter's own, just taken");

        Assert.That(Observe(Reading(14, 75.6, Now.AddMinutes(5)), Now.AddMinutes(5)), Is.False, "under a point: no new reading");
        Assert.That((Row("a").WeekPercent, Row("a").FiveHourPercent, Row("a").ReadUtc), Is.EqualTo(((double?)75, (double?)14, (DateTime?)Now.AddMinutes(5))),
            "the 5-hour percent follows the meter; the week keeps its step's figure (M.ORCH-T871)");

        Assert.That(Observe(Reading(17, 76, Now.AddMinutes(10)), Now.AddMinutes(10)), Is.True, "a point: read the meter again");
        Assert.That((Row("a").WeekPercent, Row("a").FiveHourPercent, Row("a").ReadUtc),
            Is.EqualTo(((double?)76, (double?)17, (DateTime?)Now.AddMinutes(10))), "the 5-hour percent comes with it");

        Assert.That(Observe(Reading(17, 76, Now.AddMinutes(11)), Now.AddMinutes(11)), Is.False, "once updated, it's not moved again");

        // A new 5-hour window is taken at once, the week unmoved.
        Observe(Reading(1, 76, Now.AddHours(3), fiveReset: Now.AddHours(7)), Now.AddHours(3));
        Assert.That((Row("a").FiveHourPercent, Row("a").FiveHourResetUtc), Is.EqualTo(((double?)1, (DateTime?)Now.AddHours(7))));
    }

    [Test]
    public void The_five_hour_percent_follows_the_meter_at_full_precision_between_weekly_steps()
    {
        SignIn("a", "a@mindattic.com", Now);
        Observe(Reading(12, 75, Now), Now);

        Assert.That(Observe(Reading(12.25, 75.2, Now.AddMinutes(1)), Now.AddMinutes(1)), Is.False, "no meter re-read for a 5-hour rise");
        Assert.That((Row("a").FiveHourPercent, Row("a").WeekPercent), Is.EqualTo(((double?)12.25, (double?)75)), "unrounded; the week keeps its step");

        var stamp = File.GetLastWriteTimeUtc(db.DumpPath);
        Observe(Reading(12.25, 75.3, Now.AddMinutes(2)), Now.AddMinutes(2));
        Observe(Reading(11, 75.3, Now.AddMinutes(3)), Now.AddMinutes(3));
        Assert.That((Row("a").FiveHourPercent, Row("a").ReadUtc), Is.EqualTo(((double?)12.25, (DateTime?)Now.AddMinutes(1))),
            "the same figure, or a stale lower one in the same window, writes nothing");
        Assert.That(File.GetLastWriteTimeUtc(db.DumpPath), Is.EqualTo(stamp));

        // The week still steps from its last refresh, not from each 5-hour write.
        Assert.That(Observe(Reading(13, 76, Now.AddMinutes(4)), Now.AddMinutes(4)), Is.True);
        Assert.That((Row("a").FiveHourPercent, Row("a").WeekPercent), Is.EqualTo(((double?)13, (double?)76)));
    }

    [Test]
    public void A_passed_reset_rolls_to_zero_without_signing_in()
    {
        SignIn("a", "a@mindattic.com", Now);
        Observe(Reading(40, 90, Now), Now);
        SignIn("b", "b@mindattic.com", Now.AddHours(1));
        Observe(Reading(5, 20, Now.AddHours(1), Now.AddHours(5), Now.AddDays(6)), Now.AddHours(1));

        var a = Row("a");
        Assert.That((a.WeekUsedAt(Now.AddHours(1)), a.FiveHourUsedAt(Now.AddHours(1))), Is.EqualTo(((double?)90, (double?)40)), "kept while not signed in");

        var later = FiveReset.AddMinutes(1);
        Assert.That(a.FiveHourUsedAt(later), Is.EqualTo(0));
        Assert.That(a.FiveHourResetAt(later), Is.Null, "the next window starts at its next use");
        Assert.That(a.WeekUsedAt(later), Is.EqualTo(90));

        var nextWeek = WeekReset.AddHours(1);
        Assert.That((a.WeekUsedAt(nextWeek), a.WeekLeftAt(nextWeek)), Is.EqualTo(((double?)0, (double?)100)));
        Assert.That(a.WeekResetAt(nextWeek), Is.EqualTo(WeekReset.AddDays(7)));
        Assert.That(a.ReadUtc, Is.EqualTo(Now), "its last reading keeps its time");
        Assert.That(AccountRoster.Line(a, "b/org", nextWeek), Does.Contain("week 0% used, 100% left"));
    }

    [Test]
    public void Switching_back_to_a_known_account_refreshes_its_row_and_keeps_the_others()
    {
        SignIn("a", "a@mindattic.com", Now);
        Observe(Reading(40, 90, Now), Now);
        SignIn("b", "b@mindattic.com", Now.AddHours(1));
        // Just after the /login, before the new account's meter is read: nothing of a's is written as b's.
        Observe(null, Now.AddHours(1));
        Assert.That(Row("b").ReadUtc, Is.Null);
        Observe(Reading(5, 20, Now.AddHours(1), Now.AddHours(5), Now.AddDays(6)), Now.AddHours(1));

        SignIn("a", "a@mindattic.com", Now.AddHours(2));
        // The same percents as a's last reading: still taken as this sign-in's reading.
        Observe(Reading(40, 90, Now.AddHours(2)), Now.AddHours(2));

        var a = Row("a");
        Assert.That((a.WeekPercent, a.ReadUtc, a.SignedInUtc), Is.EqualTo(((double?)90, (DateTime?)Now.AddHours(2), (DateTime?)Now.AddHours(2))));
        var b = Row("b");
        Assert.That((b.Email, b.WeekPercent, b.FiveHourPercent, b.ReadUtc), Is.EqualTo(("b@mindattic.com", (double?)20, (double?)5, (DateTime?)Now.AddHours(1))));
        Assert.That(AccountRoster.Load(db), Has.Count.EqualTo(2));
    }

    [Test]
    public void Every_opener_and_the_daemon_sync_use_the_one_state_database()
    {
        // M.ORCH-T535: the status and the pace ledger opened the db under the configured workspace root while the store, the
        // leases and the hooks opened StatePath, so a root elsewhere (or no default checkout) split the state in two.
        var before = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, null);
            var live = Path.GetFullPath(OrchestratorDb.StatePath(JsonStore.LiveDirectory));
            Assert.That(Path.GetFullPath(OrchestratorDb.LivePath), Is.EqualTo(live));
            var elsewhere = Path.Combine(dir, "ws");
            var store = new JsonStore(Path.Combine(dir, "store"), db);
            Assert.That(new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = elsewhere }, store).Db.Path, Is.EqualTo(live));
            Assert.That(new PaceLedgerWatch(store, elsewhere, (_, _) => { }, (_, _, _) => { }).Db.Path, Is.EqualTo(live));
        }
        finally { Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, before); }
        Assert.That(OrchestratorDb.RepoOf(@"C:\ws\MindAttic.Orchestrator\data\orchestrator.db"), Is.EqualTo(@"C:\ws\MindAttic.Orchestrator"));
        Assert.That(OrchestratorDb.RepoOf(@"C:\Users\me\AppData\Roaming\MindAttic\MindAttic.Orchestrator\orchestrator.db"), Is.Null,
            "the machine-local fallback has no repo to commit it to");
    }

    [Test]
    public void The_database_lives_in_the_main_checkout_unless_the_environment_names_another()
    {
        var before = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar);
        try
        {
            Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, null);
            Assert.That(OrchestratorDb.DefaultPath(@"C:\ws"), Is.EqualTo(@"C:\ws\MindAttic.Orchestrator\data\orchestrator.db"));
            Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, @"C:\elsewhere\o.db");
            Assert.That(OrchestratorDb.DefaultPath(@"C:\ws"), Is.EqualTo(@"C:\elsewhere\o.db"));
        }
        finally { Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, before); }
        Assert.That(db.DumpPath, Is.EqualTo(Path.Combine(dir, "repo", "data", "orchestrator.sql")));
    }

    [Test]
    public void The_database_is_versioned_and_keeps_no_side_files()
    {
        using (var connection = db.Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo((long)OrchestratorDb.Version));
            command.CommandText = "PRAGMA journal_mode;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo("delete"), "the .db file alone holds every write, so git commits it whole");
        }
        Assert.That(File.Exists(db.Path + "-wal") || File.Exists(db.Path + "-journal"), Is.False);
        // Opened again, nothing is migrated twice.
        Assert.That(AccountRoster.Load(db), Is.Empty);
    }

    [Test]
    public void Plans_read_from_the_sign_in()
    {
        Assert.That(AccountProfile.PlanOf("claude_team", "default_claude_max_5x"), Is.EqualTo("Team · Max 5x"));
        Assert.That(AccountProfile.PlanOf("claude_max", "default_claude_max_20x"), Is.EqualTo("Max 20x"));
        Assert.That(AccountProfile.PlanOf("claude_pro", null), Is.EqualTo("Pro"));
        Assert.That(AccountProfile.PlanOf(null, null), Is.Null);
    }
}
