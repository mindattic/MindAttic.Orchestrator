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

    [Test, NonParallelizable] // Ctx.StoreDirectory and the org overrides are process-wide
    public void Org_start_refuses_a_locked_login_the_same_way_start_account_does()
    {
        // M.ORCH-T2124: org start is orchestrator start --account underneath (OrgStartCommand), so a locked login is refused
        // before a tab opens, naming the lock, exactly as --account does on its own.
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var workspace = Path.Combine(storeDir, "workspace");
        var repoDir = Path.Combine(workspace, "A");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
        File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n");
        var store = new JsonStore(storeDir);
        store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        var path = MindAttic.Tickets.Data.TicketsDb.DefaultPath(workspace);
        using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path)) db.Import([new MindAttic.Tickets.Repo("A", repoDir)]);
        OrgHelpCommand.PeopleOverride = () => [new(Erin, "Erin", [], [])];
        LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonManual, "you", null, Now);
        try
        {
            var said = Cli(out var exit, "org", "start", "A", "erin");
            Assert.That(exit, Is.EqualTo(1), said);
            Assert.That(said, Does.Contain($"{Erin} is locked (manual since").And.Contain($"orchestrator org unlock {Erin}"));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
        }
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory is process-wide
    public void Machine_start_refuses_a_locked_login_before_asking_the_host()
    {
        // M.ORCH-T2124: checked locally, before the host is even asked.
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        LoginLocks.Lock(new JsonStore(storeDir), Erin, "Erin", LoginLocks.ReasonManual, "you", null, Now);
        try
        {
            var said = Cli(out var exit, "machine", "start", "SomeRepo", "--login", Erin);
            Assert.That(exit, Is.EqualTo(1), said);
            Assert.That(said, Does.Contain($"{Erin} is locked (manual since").And.Contain($"orchestrator org unlock {Erin}"));
        }
        finally { Ctx.StoreDirectory = previousStore; }
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory and the org overrides are process-wide
    public void Org_lock_refuses_the_api_key_instead_of_calling_it_already_locked()
    {
        // M.ORCH-T2142: LoginLocks.Lock never locks the API key and returns null, which read as "already locked", exit 0.
        var previousStore = Ctx.StoreDirectory;
        Ctx.StoreDirectory = temps.New();
        OrgHelpCommand.PeopleOverride = () => [new(MindAttic.Orchestrator.Billing.ApiKeyPerson.Email, "API key", [], [])];
        OrgAllowCommand.NowOverride = () => Now;
        try
        {
            var said = Cli(out var exit, "org", "lock", "API", "key");
            Assert.That(exit, Is.EqualTo(1), said);
            Assert.That(said, Does.Contain("can't be locked").And.Not.Contain("already locked"));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
            OrgAllowCommand.NowOverride = null;
        }
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory is process-wide
    public void Machine_start_refuses_a_login_that_isnt_an_email_instead_of_checking_the_global_one()
    {
        // M.ORCH-T2142: a --login that didn't normalize fell back to the PC's global login for the lock check.
        var previousStore = Ctx.StoreDirectory;
        Ctx.StoreDirectory = temps.New();
        try
        {
            var said = Cli(out var exit, "machine", "start", "SomeRepo", "--login", "not an email");
            Assert.That(exit, Is.EqualTo(1), said);
            Assert.That(said, Does.Contain("\"not an email\" isn't a login's email."));
        }
        finally { Ctx.StoreDirectory = previousStore; }
    }

    [Test, NonParallelizable] // Ctx.StoreDirectory and AnsiConsole.Console are process-wide
    public void Release_waiting_on_owner_parks_a_ticket_typed_in_lower_case()
    {
        // M.ORCH-T2142: the ID went to the ticket database as typed, so a lower-case one released, then "isn't in the ticket database".
        var previousStore = Ctx.StoreDirectory;
        var storeDir = temps.New();
        Ctx.StoreDirectory = storeDir;
        var workspace = Path.Combine(storeDir, "workspace");
        var repoDir = Path.Combine(workspace, "A");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
        File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-11-01 | fix | open | one | — |\n");
        var store = new JsonStore(storeDir);
        store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        store.SaveAssignments([new Assignment("A-T1", "A", repoDir, "Claude", Now, false)]);
        var path = MindAttic.Tickets.Data.TicketsDb.DefaultPath(workspace);
        using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path)) db.Import([new MindAttic.Tickets.Repo("A", repoDir)]);
        try
        {
            var said = Cli(out var exit, "release", "a-t1", "--waiting-on-owner");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("A-T1 is parked as waiting on the owner"));
            using var check = MindAttic.Tickets.Data.TicketsDb.Open(path);
            Assert.That(check.Labels("A-T1"), Does.Contain(MindAttic.Orchestrator.Tickets.OpenTicketSource.WaitingOnOwnerLabel));
        }
        finally { Ctx.StoreDirectory = previousStore; }
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
