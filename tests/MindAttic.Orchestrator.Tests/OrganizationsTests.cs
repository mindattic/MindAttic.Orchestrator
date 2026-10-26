using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Organizations above people (M.ORCH-T655): an org owns people, a person owns logins (each on its LLM provider), a
/// login no org names is the default org's, and each org sees only its own members' CLIs, agents, pace and claims.</summary>
public sealed class OrganizationsTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromMinutes(2), WaitingWindow = TimeSpan.FromMinutes(30);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static OrgDirectory SalesAndAccounting() => new(
        [new Organization("sales", "Sales", 2), new Organization("accounting", "Accounting", 1)],
        [new OrgMember("sales", "Jordan", "jordan@example.com"),
         new OrgMember("sales", "Jordan", "jordan", Providers.Codex),
         new OrgMember("accounting", "Amy", "amy@example.com"),
         new OrgMember("gone", "Old", "old@example.com")]);

    [Test]
    public void With_no_org_added_there_is_one_default_org_holding_every_login()
    {
        var single = OrgDirectory.Single;
        Assert.That(single.HasOrgs, Is.False);
        Assert.That(single.Orgs.Single().Id, Is.EqualTo(Organization.DefaultId));
        Assert.That(single.OrgOfClaude("anyone@example.com"), Is.EqualTo(Organization.DefaultId));
        Assert.That(single.OrgOfClaude(null), Is.EqualTo(Organization.DefaultId), "the global login too");
    }

    [Test]
    public void A_login_is_its_members_org_and_any_other_the_default_ones()
    {
        var orgs = SalesAndAccounting();
        Assert.That(orgs.Orgs.Select(o => o.Id), Is.EqualTo(new[] { "default", "accounting", "sales" }), "the default first, then by position");
        Assert.That(orgs.OrgOfClaude("JORDAN@example.com "), Is.EqualTo("sales"));
        Assert.That(orgs.OrgOf(Providers.Codex, "Jordan"), Is.EqualTo("sales"), "a login on another LLM");
        Assert.That(orgs.OrgOfClaude("ryan@example.com"), Is.EqualTo(Organization.DefaultId));
        Assert.That(orgs.OrgOfClaude("old@example.com"), Is.EqualTo(Organization.DefaultId), "a member of an org since removed");
        Assert.That(orgs.OrgOfTab(LoginTab.ApiKey(hasKey: true)), Is.EqualTo(Organization.DefaultId), "the Vault's key is the workspace's");
        Assert.That(orgs.PeopleOf("sales").Single().Person, Is.EqualTo("Jordan"));
        Assert.That(orgs.PeopleOf("sales").Single().Logins, Has.Count.EqualTo(2), "a person owns several logins");
        Assert.That(orgs.ClaudeEmails, Is.EquivalentTo(new[] { "jordan@example.com", "amy@example.com", "old@example.com" }));
        Assert.That(OrgScope.Summary(orgs, "sales", 1), Is.EqualTo("1 person · 1 login · 1 Codex"));
    }

    [Test]
    public void Logins_and_ids_are_kept_normalized()
    {
        Assert.That(OrgMember.NormalizeLogin(Providers.Claude, "global"), Is.EqualTo(""));
        Assert.That(OrgMember.NormalizeLogin(Providers.Claude, " Amy@Example.com"), Is.EqualTo("amy@example.com"));
        Assert.That(OrgMember.NormalizeProvider("codex"), Is.EqualTo(Providers.Codex));
        Assert.That(OrgMember.NormalizeProvider("Mistral"), Is.EqualTo("Mistral"), "any other LLM as given");
        Assert.That(Organization.IdFor("  Sales East! "), Is.EqualTo("sales-east"));
    }

    [Test]
    public void A_repos_tickets_are_its_mapped_orgs_or_the_default_ones_and_a_pinned_ticket_its_assignees()
    {
        // M.ORCH-T753: dispatch scoped per org.
        var orgs = new OrgDirectory([new Organization("sales", "Sales", 1)],
            [new OrgMember("sales", "Jordan", "jordan@example.com"), new OrgMember("sales", "Ryan", "")]); // the global login, as the store keeps it
        var routing = new OrgRouting(orgs, new Dictionary<string, string> { ["crm"] = "sales", ["Old"] = "gone" }, "ryan@example.com");
        QueueItem Item(string repo, string? assignee = null) =>
            new(repo, $@"D:\W\{repo}", new Ticket($"{repo}-T1", "2026-10-01", "fix", "open", "x", ""), 0, null, false) { Assignee = assignee };

        Assert.That(routing.OrgOfRepo("CRM"), Is.EqualTo("sales"), "a repo named in any case");
        Assert.That(routing.OrgOfRepo("Old"), Is.EqualTo(Organization.DefaultId), "mapped to an org since removed: the default's");
        Assert.That(routing.OrgOfRepo("Other"), Is.EqualTo(Organization.DefaultId), "a repo not set is the default org's");
        Assert.That(routing.OrgOfLogin(null), Is.EqualTo("sales"), "the global login, placed as global");
        Assert.That(routing.OrgOfLogin("Ryan@example.com"), Is.EqualTo("sales"), "the global login by its own email");
        Assert.That(routing.OrgOfLogin("amy@example.com"), Is.EqualTo(Organization.DefaultId));
        Assert.That(routing.MayTake(Item("crm"), "jordan@example.com"), Is.True);
        Assert.That(routing.MayTake(Item("crm"), "amy@example.com"), Is.False, "another org's login");
        Assert.That(routing.MayTake(Item("Other"), "jordan@example.com"), Is.False);
        Assert.That(routing.MayTake(Item("Other", assignee: "jordan@example.com"), "jordan@example.com"), Is.True, "a pin is its user's whatever the repo");
        Assert.That(routing.OrgOf(Item("crm", assignee: "amy@example.com")), Is.EqualTo(Organization.DefaultId));
        Assert.That(OrgRouting.Single.MayTake(Item("crm"), "amy@example.com"), Is.True, "one org: everything as before");
        Assert.That(new OrgRouting(OrgDirectory.Single, routing.RepoOrgs).OrgOfRepo("crm"), Is.EqualTo(Organization.DefaultId));
    }

    [Test]
    public void The_orgs_and_their_members_round_trip_through_the_store()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        Assert.That(db.LoadOrgs().HasOrgs, Is.False);

        db.SaveOrg(new Organization("sales", "Sales", 1));
        db.SetMember(new OrgMember("sales", "Jordan", "Jordan@Example.com"));
        db.SetMember(new OrgMember("sales", "Jordan", "jordan-gem", "gemini"));
        db.SetMember(new OrgMember(Organization.DefaultId, "Ryan", "global"));
        Assert.Throws<ArgumentException>(() => db.SetMember(new OrgMember("nowhere", "X", "x@example.com")));

        var orgs = db.LoadOrgs();
        Assert.That(orgs.Orgs.Select(o => o.Name), Is.EqualTo(new[] { "MindAttic", "Sales" }));
        Assert.That(orgs.OrgOfClaude("jordan@example.com"), Is.EqualTo("sales"));
        Assert.That(orgs.OrgOf(Providers.Gemini, "jordan-gem"), Is.EqualTo("sales"));
        Assert.That(orgs.MembersOf(Organization.DefaultId).Single().Login, Is.EqualTo(""), "the global login, placed");
        Assert.That(db.Dump(), Does.Contain("INSERT INTO organizations").And.Contain("INSERT INTO org_members"), "history worth committing");

        // A login moves rather than joining a second org; renaming keeps the id.
        db.SaveOrg(new Organization("accounting", "Accounting", 2));
        db.SetMember(new OrgMember("accounting", "Jordan", "jordan@example.com"));
        db.SaveOrg(new Organization(Organization.DefaultId, "Head office"));
        orgs = db.LoadOrgs();
        Assert.That(orgs.OrgOfClaude("jordan@example.com"), Is.EqualTo("accounting"));
        Assert.That(orgs.Orgs[0].Name, Is.EqualTo("Head office"));

        Assert.That(db.RemoveOrg("sales"), Is.True);
        Assert.That(db.LoadOrgs().OrgOf(Providers.Gemini, "jordan-gem"), Is.EqualTo(Organization.DefaultId), "its logins are the default org's again");
        Assert.Throws<ArgumentException>(() => db.RemoveOrg(Organization.DefaultId));
        Assert.That(db.RemoveMember(Providers.Claude, "JORDAN@example.com"), Is.True);
        Assert.That(db.RemoveMember(Providers.Claude, "jordan@example.com"), Is.False);
    }

    [Test]
    public void A_version_12_database_gains_the_org_tables_keeping_what_it_held_and_is_one_org()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        using (var c = db.Open())
        {
            Sql.Run(c, null, "INSERT INTO accounts (id, email) VALUES ('acct', 'a@example.com');");
            OrchestratorDb.Execute(c, LeaseTests.DropStep13 + "PRAGMA user_version = 12;");
        }
        var again = new OrchestratorDb(db.Path);
        Assert.That(again.LoadOrgs().HasOrgs, Is.False, "everything that was here is the one default org");
        Assert.That(again.LoadOrgs().Orgs.Single().Name, Is.EqualTo("MindAttic"));
        using var connection = again.Open();
        Assert.That(Sql.Scalar(connection, null, "PRAGMA user_version;"), Is.EqualTo((long)OrchestratorDb.Version));
        Assert.That(AccountRoster.Load(again).Single().Email, Is.EqualTo("a@example.com"));
    }

    private static AgentBoardRow Agent(string session, string repo, string? own) =>
        new(session, repo, null, null, Now.AddHours(-1), Now, AgentState.Working, "", false, default, []) { OwnLogin = own };

    /// <summary>Repo A: the global login's CLI (the default org's), Jordan's (Sales) and Amy's (Accounting), each working.</summary>
    private static Snapshot Workspace()
    {
        var budget = BudgetCalculator.Compute([], new OrchestratorSettings(), Now, Now.Date);
        Pace P(string why) => new(PaceMode.Open, 4, 1, 1, null, null, null, why);
        var a = new RepoStatus("A", @"D:\W\A", RepoState.Working,
            [new HostSession("Claude", 1, "p1", @"D:\W\A"), new HostSession("Claude", 2, "p2", @"D:\W\A") { OwnLogin = "jordan@example.com" },
             new HostSession("Claude", 3, "p3", @"D:\W\A") { OwnLogin = "amy@example.com" }],
            [new TranscriptSession("g1", @"D:\W\A", Now.AddMinutes(-1), new TokenUsage(1, 0, 0, 1), null),
             new TranscriptSession("j1", @"D:\W\A", Now.AddMinutes(-1), new TokenUsage(5, 0, 0, 5), null),
             new TranscriptSession("a1", @"D:\W\A", Now.AddMinutes(-20), new TokenUsage(7, 0, 0, 7), null)],
            [], Now.AddMinutes(-1), new TokenUsage(13, 0, 0, 13));
        return new Snapshot(Now, [a], [])
        {
            Budget = budget, Pace = P("global"), Account = "acct-global",
            Agents = [Agent("g1", "A", null), Agent("j1", "A", "jordan@example.com"), Agent("a1", "A", "amy@example.com")],
            OwnLogins = new Dictionary<string, string> { ["j1"] = "jordan@example.com", ["a1"] = "amy@example.com" },
            Accounts = [new AccountPace("jordan@example.com", @"C:\x\j", "acct-j", budget, P("jordan"), null, 1),
                        new AccountPace("amy@example.com", @"C:\x\a", "acct-a", budget, P("amy"), null, 1)],
        };
    }

    [Test]
    public void An_org_sees_only_its_members_clis_agents_and_pace()
    {
        var all = Workspace();
        var orgs = SalesAndAccounting();

        var sales = OrgScope.For(all, orgs, "sales", WorkingWindow, WaitingWindow);
        Assert.That(sales.Repos[0].Sessions.Select(s => s.SessionId), Is.EqualTo(new[] { "j1" }));
        Assert.That(sales.Repos[0].Hosts.Select(h => h.ProcessId), Is.EqualTo(new[] { 2 }));
        Assert.That(sales.Agents.Select(x => x.SessionId), Is.EqualTo(new[] { "j1" }));
        Assert.That(sales.Accounts.Select(x => x.Email), Is.EqualTo(new[] { "jordan@example.com" }));
        Assert.That((sales.Pace, sales.Account), Is.EqualTo(((Pace?)null, (string?)null)), "the global login is another org's: its meter isn't Sales'");

        var accounting = OrgScope.For(all, orgs, "accounting", WorkingWindow, WaitingWindow);
        Assert.That(accounting.Repos[0].State, Is.EqualTo(RepoState.Waiting), "Amy's CLI went quiet; the others' work isn't Accounting's");

        var home = OrgScope.For(all, orgs, Organization.DefaultId, WorkingWindow, WaitingWindow);
        Assert.That(home.Agents.Select(x => x.SessionId), Is.EqualTo(new[] { "g1" }));
        Assert.That(home.Pace, Is.SameAs(all.Pace), "the global login is the default org's");

        // A person's tab cut from its org's view is that login's alone, as before.
        var jordan = LoginScope.For(sales, "jordan@example.com", WorkingWindow, WaitingWindow);
        Assert.That(jordan.Pace!.Note, Is.EqualTo("jordan"));

        Assert.That(OrgScope.For(all, OrgDirectory.Single, Organization.DefaultId, WorkingWindow, WaitingWindow), Is.SameAs(all), "one org: nothing changes");
    }

    [Test]
    public void An_orgs_queue_lists_every_unclaimed_ticket_and_only_its_own_claims()
    {
        var all = Workspace();
        var orgs = SalesAndAccounting();
        QueueItem Item(string id, Assignment? a) => new("A", @"D:\W\A", new Ticket(id, "2026-10-01", "fix", "open", "x", "—"), 0, a, false);
        var open = Item("A-T1", null);
        var jordans = Item("A-T2", new Assignment("A-T2", "A", @"D:\W\A", "Claude", Now, true) { CliSession = "j1" });
        var globals = Item("A-T3", new Assignment("A-T3", "A", @"D:\W\A", "Claude", Now, true));

        Assert.That(new[] { open, jordans, globals }.Where(q => OrgScope.Lists(all, orgs, "sales", q)).Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "A-T2" }));
        Assert.That(new[] { open, jordans, globals }.Where(q => OrgScope.Lists(all, orgs, Organization.DefaultId, q)).Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "A-T3" }));
        Assert.That(new[] { open, jordans, globals }.All(q => OrgScope.Lists(all, OrgDirectory.Single, Organization.DefaultId, q)), Is.True);
    }
}
