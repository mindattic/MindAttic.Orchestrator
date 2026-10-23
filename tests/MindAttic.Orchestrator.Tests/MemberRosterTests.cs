using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The organization's members in the database (M.ORCH-T670): the members table made by its migration step on a new
/// database and an existing one, add/set/remove through the database and the dump, and <c>orchestrator members</c>.</summary>
public sealed class MemberRosterTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private OrchestratorDb Scratch() => new(Path.Combine(temps.New(), "data", "orchestrator.db"));

    private static List<string> Columns(OrchestratorDb db)
    {
        using var c = db.Open();
        return Sql.Read(c, null, "PRAGMA table_info(members);", r => r.GetString(1));
    }

    [Test]
    public void A_new_database_has_the_members_table()
    {
        Assert.That(Columns(Scratch()), Is.EqualTo(new[] { "email", "name", "role", "tier", "status", "joined_at", "updated_at" }));
    }

    [Test]
    public void An_existing_database_gets_the_members_table_and_keeps_what_it_held()
    {
        var db = Scratch();
        using (var c = db.Open())
        {
            Sql.Run(c, null, "INSERT INTO accounts (id, email) VALUES ('acct', 'a@example.com');");
            // Back to how a version-10 database was: no members table.
            OrchestratorDb.Execute(c, LeaseTests.DropStep11 + "PRAGMA user_version = 10;");
        }

        Assert.That(Columns(new OrchestratorDb(db.Path)), Has.Count.EqualTo(7));
        using var again = new OrchestratorDb(db.Path).Open();
        Assert.That(Sql.Scalar(again, null, "PRAGMA user_version;"), Is.EqualTo((long)OrchestratorDb.Version));
        Assert.That(AccountRoster.Load(db).Single().Email, Is.EqualTo("a@example.com"), "what it held is kept");
    }

    [Test]
    public void Add_set_and_remove_round_trip_through_the_database_and_the_dump()
    {
        var db = Scratch();
        var added = MemberRoster.Add(db, " New.Person@Example.com ", "New Person", "Admin", "Premium", null, Now);
        Assert.That((added.Email, added.Role, added.Tier, added.Status), Is.EqualTo(("new.person@example.com", "admin", "premium", "active")));
        MemberRoster.Add(db, "b@example.com", null, "primary owner", "standard", "pending", Now);

        var read = new OrchestratorDb(db.Path);
        Assert.That(MemberRoster.Load(read).Select(m => m.Email), Is.EqualTo(new[] { "b@example.com", "new.person@example.com" }), "by email");
        Assert.That(MemberRoster.Find(read, "NEW.PERSON@example.com"), Is.EqualTo(added));
        Assert.That(MemberRoster.Find(read, "b@example.com")!.Role, Is.EqualTo("primary-owner"));
        var dump = File.ReadAllText(db.DumpPath);
        Assert.That(dump, Does.Contain("-- members\nINSERT INTO members (email, name, role, tier, status, joined_at, updated_at) VALUES ('b@example.com', NULL, 'primary-owner', 'standard', 'pending',"));
        Assert.That(dump.IndexOf("'b@example.com'", StringComparison.Ordinal), Is.LessThan(dump.IndexOf("'new.person@example.com'", StringComparison.Ordinal)), "in key order");

        Assert.Throws<InvalidOperationException>(() => MemberRoster.Add(db, "new.person@example.com", null, "member", "premium", null, Now), "already a member");
        Assert.Throws<ArgumentException>(() => MemberRoster.Add(db, "c@example.com", null, "boss", "premium", null, Now));
        Assert.Throws<ArgumentException>(() => MemberRoster.Add(db, "c@example.com", null, "member", "gold", null, Now));
        Assert.Throws<ArgumentException>(() => MemberRoster.Add(db, "c@example.com", null, "member", "premium", "gone", Now));
        Assert.Throws<ArgumentException>(() => MemberRoster.Add(db, "not an email", null, "member", "premium", null, Now));

        var later = Now.AddHours(1);
        var set = MemberRoster.Set(db, "b@example.com", "Bee", null, "premium", "active", later);
        Assert.That((set.Name, set.Role, set.Tier, set.Status, set.JoinedUtc, set.UpdatedUtc), Is.EqualTo(("Bee", "primary-owner", "premium", "active", Now, later)));
        Assert.That(MemberRoster.Find(new OrchestratorDb(db.Path), "b@example.com"), Is.EqualTo(set));
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("('b@example.com', 'Bee', 'primary-owner', 'premium', 'active',"));
        Assert.That(MemberRoster.Set(db, "b@example.com", null, "Primary-Owner", null, null, later.AddHours(1)).UpdatedUtc, Is.EqualTo(later), "nothing changed: not written");
        Assert.That(MemberRoster.Set(db, "b@example.com", "", null, null, null, later).Name, Is.Null, "an empty name clears it");
        Assert.Throws<InvalidOperationException>(() => MemberRoster.Set(db, "nobody@example.com", "X", null, null, null, later));

        Assert.That(MemberRoster.Remove(db, "B@example.com"), Is.True);
        Assert.That(MemberRoster.Remove(db, "b@example.com"), Is.False);
        Assert.That(MemberRoster.Load(new OrchestratorDb(db.Path)).Select(m => m.Email), Is.EqualTo(new[] { "new.person@example.com" }));
        Assert.That(File.ReadAllText(db.DumpPath), Does.Not.Contain("b@example.com"));
    }

    [Test]
    public void The_members_command_adds_sets_lists_and_removes_members_in_the_live_database()
    {
        // The per-test scratch database (OrchestratorDbPerTest) is the live one here: never the repo's.
        var output = new StringWriter();
        var previous = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No,
        });
        AnsiConsole.Console.Profile.Width = 240; // one line per row
        try
        {
            int Run(params string[] args) => CliApp.Create().Run(args);
            var noLogins = () => new AccountDirs(Path.Combine(temps.New(), "none"), Path.Combine(temps.New(), ".claude"), Path.Combine(temps.New(), ".claude.json")); // never the real ~/.claude-accounts
            LoginCommand.DirsOverride = noLogins;
            var db = new OrchestratorDb(OrchestratorDb.LivePath);
            Assert.That(db.Path, Does.Not.Contain(Path.Combine("MindAttic.Orchestrator", "data")), "a scratch database");
            using (var c = db.Open())
                Sql.Run(c, null, "INSERT INTO accounts (id, email, signed_in_at) VALUES ('acct', 'Signed@Example.com', 1761200000);");

            Assert.That(Run("members", "add", "signed@example.com", "--name", "Sig Ned", "--role", "owner", "--tier", "premium"), Is.Zero, output.ToString());
            Assert.That(Run("members", "add", "invited@example.com", "--role", "member", "--tier", "standard", "--status", "pending"), Is.Zero, output.ToString());
            Assert.That(Run("members", "add", "x@example.com", "--role", "member"), Is.EqualTo(1), "no tier");
            Assert.That(Run("members", "add", "x@example.com", "--role", "chief", "--tier", "premium"), Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("isn't a role"));
            Assert.That(Run("members", "set", "invited@example.com", "--name", "In Vited", "--status", "active"), Is.Zero, output.ToString());
            Assert.That(Run("members", "set", "nobody@example.com", "--name", "N"), Is.EqualTo(1));

            output.GetStringBuilder().Clear();
            Assert.That(Run("members"), Is.Zero, output.ToString());
            var list = output.ToString();
            Assert.That(list, Does.Contain("Sig Ned").And.Contain("signed@example.com").And.Contain("owner").And.Contain("In Vited").And.Contain("standard"));
            var signedLine = list.Split('\n').Single(l => l.Contains("signed@example.com", StringComparison.Ordinal));
            var invitedLine = list.Split('\n').Single(l => l.Contains("invited@example.com", StringComparison.Ordinal));
            Assert.That(signedLine, Does.Contain("yes"), "signed in here: the accounts table, by email");
            Assert.That(invitedLine, Does.Contain("no"));

            Assert.That(signedLine, Does.Contain("fallback login"));

            // M.ORCH-T671: a member signed in only through its own folder under ~/.claude-accounts reads as signed in, via its own login.
            var accountsRoot = Path.Combine(temps.New(), ".claude-accounts");
            var ownDirs = new AccountDirs(accountsRoot, Path.Combine(accountsRoot, "..", ".claude"), Path.Combine(accountsRoot, "..", ".claude.json"));
            var honey = ownDirs.For("honey@example.com");
            Directory.CreateDirectory(honey.Directory);
            File.WriteAllText(honey.ConfigPath, """{"oauthAccount":{"accountUuid":"acct-honey","emailAddress":"honey@example.com"}}""");
            LoginCommand.DirsOverride = () => ownDirs;
            try
            {
                Assert.That(Run("members", "add", "honey@example.com", "--role", "member", "--tier", "standard"), Is.Zero, output.ToString());
                output.GetStringBuilder().Clear();
                Assert.That(Run("members"), Is.Zero, output.ToString());
                var lines = output.ToString().Split((char)10);
                var honeyLine = lines.Single(l => l.Contains("honey@example.com", StringComparison.Ordinal));
                Assert.That(honeyLine, Does.Contain("yes").And.Contain("own login").And.Not.Contain("fallback"));
                Assert.That(lines.Single(l => l.Contains("signed@example.com", StringComparison.Ordinal)), Does.Not.Contain("own login"));
                Assert.That(Run("members", "remove", "honey@example.com"), Is.Zero, output.ToString());
            }
            finally { LoginCommand.DirsOverride = noLogins; }

            Assert.That(Run("members", "remove", "invited@example.com"), Is.Zero, output.ToString());
            Assert.That(Run("members", "remove", "invited@example.com"), Is.EqualTo(1));
            Assert.That(MemberRoster.Load(db).Select(m => m.Email), Is.EqualTo(new[] { "signed@example.com" }));
            Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("'signed@example.com', 'Sig Ned', 'owner', 'premium', 'active'"));
            Assert.That(Run("members", "--help"), Is.Zero, output.ToString());
            Assert.That(Run("members", "add", "--help"), Is.Zero, output.ToString());
        }
        finally { AnsiConsole.Console = previous; LoginCommand.DirsOverride = null; }
    }
}
