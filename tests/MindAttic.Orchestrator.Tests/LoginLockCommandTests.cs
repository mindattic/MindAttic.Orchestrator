using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2114: <c>orchestrator org lock/unlock/locks &lt;who&gt;</c>, mirroring <c>org allow/disallow</c>
/// (<see cref="WeeklyAllowanceTests"/>): same &lt;who&gt; resolution, same error handling and output style, built on
/// <see cref="LoginLocks"/> (M.ORCH-T2122). Unlocking a weekly-cutoff lock also prints the login's current weekly use.</summary>
public sealed class LoginLockCommandTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(4);
    private const string Erin = "ohleo199@mindattic.com";
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

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
    public void Lock_unlock_and_locks_name_a_member_any_way_and_refuse_an_unknown_one()
    {
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var store = new JsonStore(storeDir);
        OrgHelpCommand.PeopleOverride = () => [new(Erin, "Erin", [], []), new("honey@mindattic.com", "Honey", [], [])];
        OrgAllowCommand.NowOverride = () => Now;
        try
        {
            var said = Cli(out var exit, "org", "lock", "E");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com) locked by you."));
            Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Erin), Is.True);

            said = Cli(out exit, "org", "lock", "erin");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("already locked: nothing to do"));

            said = Cli(out exit, "org", "locks");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com): manual since").And.Contain("by you"));

            said = Cli(out exit, "org", "limit");
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com):").And.Contain("(locked: manual since"));

            said = Cli(out exit, "org", "unlock", "ohleo199");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com) unlocked by you."));
            Assert.That(said, Does.Not.Contain("Its week is now at"), "a manual lock's unlock doesn't print a weekly percent");
            Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Erin), Is.False);

            said = Cli(out exit, "org", "locks");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("No logins are locked."));

            said = Cli(out exit, "org", "unlock", "erin");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("isn't locked: nothing to take back"));

            said = Cli(out exit, "org", "lock", "nope");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("No member").And.Contain("\"nope\""));

            said = Cli(out exit, "org", "help");
            Assert.That(said, Does.Contain("lock <who>").And.Contain("unlock <who>").And.Contain("locks"));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
            OrgAllowCommand.NowOverride = null;
        }
    }

    [Test, NonParallelizable]
    public void Unlocking_a_weekly_cutoff_lock_prints_its_current_weekly_percent()
    {
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var store = new JsonStore(storeDir);
        OrgHelpCommand.PeopleOverride = () => [new(Erin, "Erin", [], [])];
        OrgAllowCommand.NowOverride = () => Now;
        LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonWeeklyCutoff, "its weekly cutoff", WeekReset, Now);
        UsageReportCommand.Override = _ => new UsageReport { Rows = [new UsageReportRow { Email = Erin, Name = "Erin", WeekUsed = 73.4 }] };
        try
        {
            var said = Cli(out var exit, "org", "unlock", "erin");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Erin (ohleo199@mindattic.com) unlocked by you."));
            Assert.That(said, Does.Contain("Its week is now at 73%."));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
            OrgAllowCommand.NowOverride = null;
            UsageReportCommand.Override = null;
        }
    }

    [Test]
    public void The_usage_report_says_whos_locked_in_its_notes_not_a_column()
    {
        var members = new[] { new Member { Email = Erin, Name = "Erin" } };
        var held = UsageReport.Build(members, [], [], [], new Dictionary<string, BudgetStatus>(), 90, Now);
        Assert.That(held.Notes(), Has.None.Contains("locked"));

        var since = WeeklyAllowances.When(Now);
        var locked = UsageReport.Build(members, [], [], [], new Dictionary<string, BudgetStatus>(), 90, Now,
            locks: [new LoginLock { Email = Erin, Name = "Erin", Reason = LoginLocks.ReasonManual, SinceUtc = Now, By = "you" }]);
        Assert.That(locked.Notes(), Has.Some.EqualTo($"Erin: locked (manual) since {since}."));
        Assert.That(locked.Headers, Is.EqualTo(held.Headers), "no new column");
    }
}
