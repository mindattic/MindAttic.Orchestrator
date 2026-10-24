using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>/org and /organization (M.ORCH-T686): who &lt;who&gt; names, the command files AgentGate installs, and
/// <c>orchestrator org</c>'s help and refusals.</summary>
public sealed class OrgCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    // The organization as it is today, Ryan's login named "Ryan DeBraal" (so its badge is RD) and Jordan's "Jordan".
    private static readonly IReadOnlyList<OrgPerson> Org =
    [
        new("ryandebraal@mindattic.com", "Ryan DeBraal", ["Ryan DeBraal"], ["RD"]),
        new("shampoo1232@mindattic.com", "Amy", ["shampoo1232"], ["SH"]),
        new("ohleo199@mindattic.com", "Erin", [], []),
        new("honey@mindattic.com", "Honey", [], []),
        new("pdftransferjr@mindattic.com", "Jordan", ["JJ"], ["JJ"]),
    ];

    private static string? One(string who) => MemberLookup.Match(Org, who) is [var only] ? only.Email : null;

    [TestCase("ohleo199@mindattic.com", "ohleo199@mindattic.com", TestName = "By email")]
    [TestCase("OHLEO199@MindAttic.com", "ohleo199@mindattic.com", TestName = "By email, any case")]
    [TestCase("ohleo199", "ohleo199@mindattic.com", TestName = "By the part before the @")]
    [TestCase("Ryan DeBraal", "ryandebraal@mindattic.com", TestName = "By name")]
    [TestCase("ryan  debraal", "ryandebraal@mindattic.com", TestName = "By name, any case and spacing")]
    [TestCase("Ryan", "ryandebraal@mindattic.com", TestName = "By first name")]
    [TestCase("erin", "ohleo199@mindattic.com", TestName = "By a one-word name")]
    [TestCase("JJ", "pdftransferjr@mindattic.com", TestName = "By login alias")]
    [TestCase("RD", "ryandebraal@mindattic.com", TestName = "By initials")]
    [TestCase("rd", "ryandebraal@mindattic.com", TestName = "By initials, any case")]
    [TestCase("E", "ohleo199@mindattic.com", TestName = "By a one-letter initial")]
    [TestCase("shampoo1232", "shampoo1232@mindattic.com", TestName = "By the alias that's the part before the @")]
    public void A_member_is_named_any_way(string who, string email) => Assert.That(One(who), Is.EqualTo(email));

    [Test]
    public void Nobody_or_a_partial_name_matches_no_one()
    {
        Assert.That(MemberLookup.Match(Org, "Bob"), Is.Empty);
        Assert.That(MemberLookup.Match(Org, "Ry"), Is.Empty, "exact only: no prefix guesses");
        Assert.That(MemberLookup.Match(Org, "  "), Is.Empty);
    }

    [Test]
    public void A_name_two_members_share_lists_them_both()
    {
        IReadOnlyList<OrgPerson> two = [.. Org, new("ed@mindattic.com", "Ed", [], [])];
        Assert.That(MemberLookup.Match(two, "E").Select(p => p.Email), Is.EquivalentTo(new[] { "ohleo199@mindattic.com", "ed@mindattic.com" }));
        IReadOnlyList<OrgPerson> ryans = [.. Org, new("ryan.other@mindattic.com", "Ryan Other", [], [])];
        Assert.That(MemberLookup.Match(ryans, "ryan").Select(p => p.Email), Is.EquivalentTo(new[] { "ryandebraal@mindattic.com", "ryan.other@mindattic.com" }));
        Assert.That(MemberLookup.Match(ryans, "Ryan DeBraal").Single().Email, Is.EqualTo("ryandebraal@mindattic.com"), "the whole name is still one");
    }

    [Test]
    public void One_member_named_several_ways_at_once_is_still_one()
    {
        // Honey: her name, first name and the part before the @ are all "honey".
        Assert.That(MemberLookup.Match(Org, "honey"), Has.Count.EqualTo(1));
        Assert.That(Org[0].NameInitials, Is.EqualTo("RD"));
        Assert.That(Org[2].NameInitials, Is.EqualTo("E"));
    }

    [Test]
    public void People_are_the_members_and_anyone_else_signed_in_here()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "data", "orchestrator.db"));
        MemberRoster.Add(db, "ohleo199@mindattic.com", "Erin", "member", "premium", null, Now);
        using (var c = db.Open())
            Sql.Run(c, null, "INSERT INTO accounts (id, email) VALUES ('acct', 'guest@mindattic.com');");
        var dirs = new AccountDirs(Path.Combine(temps.New(), "none"), Path.Combine(temps.New(), ".claude"), Path.Combine(temps.New(), ".claude.json"));

        var people = MemberLookup.People(db, dirs);
        Assert.That(people.Select(p => (p.Email, p.Name)), Is.EqualTo(new (string, string?)[] { ("ohleo199@mindattic.com", "Erin"), ("guest@mindattic.com", null) }));
        Assert.That(MemberLookup.Match(people, "guest").Single().Email, Is.EqualTo("guest@mindattic.com"));
    }

    [Test]
    public void Ensure_installs_org_and_organization_and_they_only_run_the_cli()
    {
        var home = temps.New();
        var claude = Path.Combine(home, ".claude");
        var did = AgentGate.Ensure(claude);
        Assert.That(did, Does.Contain("wrote org.md").And.Contain("wrote organization.md"));
        var commands = Path.Combine(claude, "commands");
        foreach (var (file, name) in new[] { ("org.md", "org"), ("organization.md", "organization") })
        {
            var text = File.ReadAllText(Path.Combine(commands, file));
            Assert.That(text, Is.EqualTo(file == "org.md" ? AgentGate.OrgCommand : AgentGate.OrganizationCommand));
            Assert.That(text, Does.StartWith("---").And.Contain($"/{name} $ARGUMENTS was asked for"));
            Assert.That(text, Does.Not.Contain("disable-model-invocation"), "the model runs it");
            Assert.That(text, Does.Contain("run `orchestrator org $ARGUMENTS`").And.Contain("in one code block"));
            Assert.That(text, Does.Contain("`! orchestrator org $ARGUMENTS`"), "login is the user's to run, in their terminal");
            // The only thing it runs is the orchestrator's CLI.
            var commandsRun = System.Text.RegularExpressions.Regex.Matches(text, "`!? ?([^`]+)`").Select(m => m.Groups[1].Value).ToList();
            Assert.That(commandsRun, Is.Not.Empty.And.All.StartsWith("orchestrator org "));
        }
        Assert.That(AgentGate.Ensure(claude), Is.Null, "in place: nothing written again");
        Assert.That(AgentGateRemoval.Remove(claude, Path.Combine(home, ".codex"), Path.Combine(home, ".gemini"), Path.Combine(home, ".kimi-code", "config.toml")), Does.Contain("deleted commands/org.md").And.Contain("deleted commands/organization.md"));
    }

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

    [Test, NonParallelizable] // AnsiConsole.Console and OrgHelpCommand.PeopleOverride are process-wide
    public void Org_bare_or_help_lists_the_subcommands()
    {
        foreach (var args in new[] { new[] { "org" }, ["org", "help"] })
        {
            var said = Cli(out var exit, args);
            Assert.That(exit, Is.Zero, said);
            foreach (var sub in new[] { "usage", "login <who>", "members", "start <repo> <who>", "help" })
                Assert.That(said, Does.Contain(sub));
        }
    }

    [Test, NonParallelizable]
    public void Org_login_and_start_refuse_an_ambiguous_or_unknown_member_and_list_who_it_could_be()
    {
        OrgHelpCommand.PeopleOverride = () => [.. Org, new("ed@mindattic.com", "Ed", [], [])];
        try
        {
            var said = Cli(out var exit, "org", "login", "E");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("could be more than one member").And.Contain("Erin (ohleo199@mindattic.com)").And.Contain("Ed (ed@mindattic.com)")
                .And.Not.Contain("Amy"));

            said = Cli(out exit, "org", "start", "MindAttic.Orchestrator", "Bob");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("No member is \"Bob\"").And.Contain("Amy (shampoo1232@mindattic.com)"), "every member listed");
        }
        finally { OrgHelpCommand.PeopleOverride = null; }
    }

    [Test]
    public void An_org_is_chosen_by_its_id_or_name_in_any_case_else_the_dashboards_else_the_default()
    {
        // M.ORCH-T755
        var orgs = new OrgDirectory([new Organization("sales", "Sales East", 1)], []);
        Assert.That(OrgSelection.Choose(orgs, "SALES", null, out var error)!.Id, Is.EqualTo("sales"));
        Assert.That(OrgSelection.Choose(orgs, " sales east ", null, out error)!.Id, Is.EqualTo("sales"), "by name");
        Assert.That(OrgSelection.Choose(orgs, "mindattic", "sales", out error)!.Id, Is.EqualTo(Organization.DefaultId), "--org over the dashboard's");
        Assert.That(OrgSelection.Choose(orgs, null, "sales", out error)!.Id, Is.EqualTo("sales"), "the dashboard's");
        Assert.That(OrgSelection.Choose(orgs, null, "removed", out error)!.Id, Is.EqualTo(Organization.DefaultId), "one since removed: the default");
        Assert.That(OrgSelection.Choose(orgs, null, null, out error)!.Id, Is.EqualTo(Organization.DefaultId));
        Assert.That(OrgSelection.Choose(orgs, "nope", "sales", out error), Is.Null);
        Assert.That(error, Does.Contain("No org is \"nope\"").And.Contain("Sales East (sales)"));
        Assert.That(OrgSelection.Choose(OrgDirectory.Single, "MindAttic", null, out error)!.Id, Is.EqualTo(Organization.DefaultId), "one org: --org still names it");
    }

    [Test, NonParallelizable]
    public void Org_commands_work_on_the_chosen_org_and_adding_a_member_places_their_login_in_it()
    {
        // M.ORCH-T755: two orgs, Erin's login in Sales and everyone else's in the default one (MindAttic).
        var previousStore = Ctx.StoreDirectory;
        Ctx.StoreDirectory = temps.New();
        OrgHelpCommand.PeopleOverride = () => Org;
        LoginCommand.DirsOverride = () => new AccountDirs(Path.Combine(temps.New(), "none"), Path.Combine(temps.New(), ".claude"), Path.Combine(temps.New(), ".claude.json"));
        try
        {
            var store = Ctx.Store();
            store.Db.SaveOrg(new Organization("sales", "Sales", 1));
            store.Db.SetMember(new OrgMember("sales", "Erin", "ohleo199@mindattic.com"));
            var db = MembersCommand.Db();
            MemberRoster.Add(db, "ohleo199@mindattic.com", "Erin", "member", "premium", null, Now);
            MemberRoster.Add(db, "shampoo1232@mindattic.com", "Amy", "member", "premium", null, Now);

            // Nothing chosen in the dashboard: the default org, so Erin is another org's.
            var said = Cli(out var exit, "org", "login", "Erin");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("is in Sales, not MindAttic: add --org sales"));
            said = Cli(out exit, "org", "start", "MindAttic.Orchestrator", "Erin", "--org", "nope");
            Assert.That(exit, Is.EqualTo(1));
            Assert.That(said, Does.Contain("No org is \"nope\""));

            said = Cli(out exit, "org", "limit");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Amy").And.Not.Contain("ohleo199"));
            said = Cli(out exit, "org", "limit", "--org", "SALES");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("ohleo199").And.Not.Contain("Amy"));

            said = Cli(out exit, "org", "members", "--org", "Sales");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("ohleo199@mindattic.com").And.Not.Contain("shampoo1232"));

            // The org chosen in the dashboard is the one worked on.
            OrgSelection.Remember(store, "sales");
            said = Cli(out exit, "org", "members");
            Assert.That(said, Does.Contain("ohleo199@mindattic.com").And.Not.Contain("shampoo1232"));
            said = Cli(out exit, "org", "members", "add", "new@example.com", "--name", "Newt", "--role", "member", "--tier", "standard");
            Assert.That(exit, Is.Zero, said);
            Assert.That(said, Does.Contain("Their login is in Sales"));
            var orgs = store.Db.LoadOrgs();
            Assert.That(orgs.OrgOfClaude("new@example.com"), Is.EqualTo("sales"));
            Assert.That(orgs.MembersOf("sales").Single(m => m.Login == "new@example.com").Person, Is.EqualTo("Newt"));
            said = Cli(out exit, "org", "members", "remove", "ohleo199@mindattic.com", "--org", "default");
            Assert.That(exit, Is.EqualTo(1), said);
            Assert.That(said, Does.Contain("is in Sales, not MindAttic"));
            Assert.That(MemberRoster.Load(db).Select(m => m.Email), Does.Contain("ohleo199@mindattic.com"), "refused: still a member");

            // usage: only the org's rows.
            UsageReportCommand.Override = _ => new UsageReport
            {
                GeneratedUtc = Now,
                Rows = [new UsageReportRow { Name = "Erin", Email = "ohleo199@mindattic.com" }, new UsageReportRow { Name = "Amy", Email = "shampoo1232@mindattic.com" }],
            };
            var stdout = new StringWriter();
            var realOut = Console.Out;
            Console.SetOut(stdout);
            try { Cli(out exit, "org", "usage"); }
            finally { Console.SetOut(realOut); }
            Assert.That(exit, Is.Zero);
            Assert.That(stdout.ToString(), Does.Contain("Erin").And.Not.Contain("Amy"));
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            OrgHelpCommand.PeopleOverride = null;
            LoginCommand.DirsOverride = null;
            UsageReportCommand.Override = null;
        }
    }
}
