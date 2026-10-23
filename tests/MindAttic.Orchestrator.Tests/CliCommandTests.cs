using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The CLI's commands run in-process through Spectre's CommandApp against a temp store, so what a typed command
/// saves (or refuses to save) is checked end to end, never touching the user's own settings.</summary>
[NonParallelizable] // Ctx.StoreDirectory and AnsiConsole.Console are process-wide
public sealed class CliCommandTests
{
    private string dir = "";
    private string? previousStore;
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;

    private JsonStore Store => new(dir);
    private string SettingsFile => Path.Combine(dir, "settings.json");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-cli-" + Guid.NewGuid().ToString("N"));
        previousStore = Ctx.StoreDirectory;
        Ctx.StoreDirectory = dir;
        previousConsole = AnsiConsole.Console;
        output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
    }

    [TearDown]
    public void TearDown()
    {
        Ctx.StoreDirectory = previousStore;
        AnsiConsole.Console = previousConsole;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private int Run(params string[] args) => CliApp.Create().Run(args);

    [Test]
    public void A_missing_argument_is_a_runtime_exception_the_entry_point_reports_in_one_line()
    {
        // Program.cs catches parse and runtime exceptions as typing mistakes; anything else is logged as a crash.
        Assert.Throws<Spectre.Console.Cli.CommandRuntimeException>(() => Run("dispatch"));
    }

    [Test]
    public void A_malformed_connection_string_is_a_bad_option_that_is_never_echoed()
    {
        // M.ORCH-T716: SqlConnectionStringBuilder's ArgumentException escaped db migrate, so the crash handler logged the
        // whole command line, password and all.
        Assert.That(Run("db", "migrate", "--connection", "Server=x;Password=hunter2;Bogus"), Is.EqualTo(2));
        Assert.That(output.ToString(), Does.Contain("--connection").And.Not.Contain("hunter2"));
    }

    [Test]
    public void The_crash_log_names_only_the_command_never_its_options()
    {
        Assert.That(CliApp.CommandName(["db", "migrate", "--connection", "Password=hunter2"]), Is.EqualTo("db migrate"));
        Assert.That(CliApp.CommandName(["send", "M.ORCH-T1", "secret text"]), Is.EqualTo("send"));
        Assert.That(CliApp.CommandName(["--help"]), Is.EqualTo("(no command)"));
    }

    [Test]
    public void The_full_help_renders_without_a_markup_error()
    {
        // M.ORCH-T496: remind's description had an unescaped '[--to <contact>]', which Spectre read as markup, so
        // 'orchestrator --help' crashed with "Could not find color or style '--to'".
        Assert.That(Run("--help"), Is.Zero, output.ToString());
        var help = string.Concat(output.ToString().Where(c => !char.IsWhiteSpace(c))); // help wraps at the console's width
        Assert.That(help, Does.Contain("[--to<contact>]").And.Contain("remind"));
    }

    [Test]
    public void Every_commands_help_renders_without_a_markup_error()
    {
        string[] commands =
        [
            "status", "start", "send", "budget", "queue", "bump", "dispatch", "release", "daemon", "settings", "audit",
            "block", "next", "approve", "reserve", "dryrun", "show", "hygiene", "intake", "report", "billing", "clis",
            "ledger", "remind", "members", "members add", "members set", "members remove", "workspace", "workspace export", "workspace install-apps", "workspace build",
        ];
        foreach (var command in commands)
            Assert.That(Run([.. command.Split(' '), "--help"]), Is.Zero, command + ": " + output);
    }

    [Test]
    public void An_unreadable_settings_json_is_an_invalid_data_exception_the_entry_point_reports_in_one_line()
    {
        // M.ORCH-T332: Program.cs says an InvalidDataException's message in one line; it used to log it as a crash.
        Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsFile, "{ oops");
        var ex = Assert.Throws<InvalidDataException>(() => Run("reserve", "M.ORCH-T5", "2M"));
        Assert.That(ex!.Message, Does.Contain("couldn't be read").And.Contain("Fix or remove"));
        Assert.That(File.ReadAllText(SettingsFile), Is.EqualTo("{ oops"));
    }

    [Test]
    public void Audit_says_in_one_line_when_its_report_cant_be_saved_and_still_shows_it()
    {
        // M.ORCH-T467: store.Save's IOException reached Program.cs as a crash (stack trace, Fatal log); only the daemon caught it.
        var workspace = Path.Combine(dir, "workspace");
        Directory.CreateDirectory(workspace);
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        using (StoreFaults.Unwritable(Store, "audit"))
            Assert.That(Run("audit"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Audit of 0 repos").And.Contain("Couldn't save this report"));
    }

    [Test]
    public void Audit_latest_says_an_unreadable_audit_json_couldnt_be_read_not_that_there_is_none()
    {
        StoreFaults.Garble(Store, "audit", "{ oops");
        Assert.That(Run("audit", "--latest"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("couldn't be read").And.Not.Contain("No saved audit yet"));
    }

    [Test]
    public void Dispatch_and_start_take_only_a_provider_the_launcher_knows()
    {
        Assert.That(Ctx.Provider("claude"), Is.EqualTo("Claude"), "any casing, as its own name");
        Assert.That(Ctx.Provider("codx"), Is.Null, "a typo would open a tab that dies at once while the ticket reads as assigned");
        Assert.That(output.ToString(), Does.Contain("No provider \"codx\""));
    }

    [Test]
    public void The_store_directory_is_injectable()
    {
        Assert.That(Ctx.Store().Directory, Is.EqualTo(dir));
    }

    /// <summary>A workspace with repo A (open A-T1 fix, A-T2 feature, A-T3 closed feature) imported into its own ticket database; the
    /// database's path. Never the real workspace's.</summary>
    private string WorkspaceWithDb()
    {
        var workspace = Path.Combine(dir, "workspace");
        var repoDir = Path.Combine(workspace, "A");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
        File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | one | — |\n| A-T2 | 2026-10-01 | feature | open | two | — |\n| A-T3 | 2026-10-01 | feature | closed | three | x |\n");
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        var path = MindAttic.Tickets.Data.TicketsDb.DefaultPath(workspace);
        using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            db.Import([new MindAttic.Tickets.Repo("A", repoDir)]);
        return path;
    }

    /// <summary>M.ORCH-T384: priority and approval live in the ticket database's queue, where tickets ticket approve and the
    /// Tickets Desktop write them too, not in settings.json, which the scheduler alone read.</summary>
    [Test]
    public void Bump_and_approve_write_the_ticket_databases_queue_not_settings()
    {
        var path = WorkspaceWithDb();
        try
        {
            Assert.That(Run("bump", "a-t2", "--by", "2"), Is.Zero);
            Assert.That(output.ToString(), Does.Contain("A-T2 priority is now +2"));
            Assert.That(Run("approve", "a-t2"), Is.Zero);
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueState("A-T2"), Is.EqualTo(("approved", 2L)));
            // And what the scheduler reads: the snapshot's approvals and priorities come from that queue.
            var source = new MindAttic.Orchestrator.Tickets.OpenTicketSource();
            source.OpenFor(new MindAttic.Tickets.Repo("A", Path.Combine(dir, "workspace", "A")), new List<string>());
            Assert.That((source.Approved.Contains("a-t2"), source.Priorities.GetValueOrDefault("A-T2")), Is.EqualTo((true, 2L)));
            Assert.That((Store.LoadSettings().Approved, Store.LoadSettings().Priorities), Is.EqualTo((null as List<string>, null as Dictionary<string, int>)));

            Assert.That(Run("bump", "A-T2", "--by", "-2"), Is.Zero);
            Assert.That(Run("approve", "A-T2", "--undo"), Is.Zero);
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueState("A-T2"), Is.EqualTo(("proposed", 0L)));

            Assert.That(Run("approve", "A-T3"), Is.EqualTo(1), "a closed ticket isn't approved");
            Assert.That(output.ToString(), Does.Contain("A-T3 isn't open"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T456: the same ticket named twice (in any case) is approved once, not a crash that approves nothing.</summary>
    [Test]
    public void Approve_takes_the_same_ticket_named_twice()
    {
        var path = WorkspaceWithDb();
        try
        {
            Assert.That(Run("approve", "A-T2", "a-t2"), Is.Zero, output.ToString());
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueState("A-T2").Item1, Is.EqualTo("approved"));
            Assert.That(output.ToString(), Does.Contain("A-T2 approved"));
            Assert.That(Run("approve", "A-T2", "A-T2", "--undo"), Is.Zero, output.ToString());
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueState("A-T2").Item1, Is.EqualTo("proposed"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T461: show and hygiene read the ticket database, not the USER_STORIES.md view, which lags it until
    /// something renders it: a ticket the orchestrator filed itself (the pace ledger's, GitHub intake's) read as "No ticket".</summary>
    [Test]
    public void Show_and_hygiene_read_a_ticket_the_view_does_not_have_yet_from_the_database()
    {
        var path = WorkspaceWithDb();
        StoryRepo();
        try
        {
            string filed, closed;
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            {
                // Added as PaceLedgerWatch and GitHubIntake add them: straight into the database, the view not rendered.
                filed = db.Add("A", "fix", "open", "filed by the pace ledger", "pace-ledger.jsonl").Id;
                closed = db.Add("A", "fix", "closed", "closed with nothing cited", "—").Id;
            }
            var view = File.ReadAllText(Path.Combine(dir, "workspace", "A", "docs", "USER_STORIES.md"));
            Assert.That(view, Does.Not.Contain(filed).And.Not.Contain(closed), "the view hasn't been rendered since");

            Assert.That(Run("show", filed.ToLowerInvariant()), Is.Zero, output.ToString());
            Assert.That(output.ToString(), Does.Contain("filed by the pace ledger").And.Not.Contain("No ticket"));
            Assert.That(Run("show", "B-T1"), Is.Zero, "a story-file repo's ticket still shows: " + output);
            Assert.That(output.ToString(), Does.Contain("B-T1"));

            Assert.That(Run("hygiene"), Is.Zero, output.ToString());
            Assert.That(output.ToString(), Does.Contain(closed));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Bump_and_approve_without_a_ticket_database_change_nothing()
    {
        var workspace = Path.Combine(dir, "workspace");
        Directory.CreateDirectory(workspace);
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        Assert.That((Run("bump", "M.ORCH-T5"), Run("approve", "M.ORCH-T5")), Is.EqualTo((1, 1)), "no repo has that ticket");
        Assert.That(output.ToString(), Does.Contain("No ticket M.ORCH-T5"));
        Assert.That((Store.LoadSettings().Approved, Store.LoadSettings().Priorities), Is.EqualTo((null as List<string>, null as Dictionary<string, int>)));
        Assert.That((Store.LoadSettings().StoryApproved, Store.LoadSettings().StoryPriorities), Is.EqualTo((new List<string>(), new Dictionary<string, long>())));
    }

    /// <summary>Repo <paramref name="name"/> on its own USER_STORIES.md (open B-T1 feature, B-T2 docs, B-T3 closed chore), not
    /// imported into any database.</summary>
    private string StoryRepo(string name = "B")
    {
        var repoDir = Path.Combine(dir, "workspace", name);
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
        File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
            $"---\ncode: {name}\n---\n\n# {name}\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            $"| {name}-T1 | 2026-10-01 | feature | open | one | - |\n| {name}-T2 | 2026-10-01 | docs | open | two | - |\n| {name}-T3 | 2026-10-01 | chore | closed | three | x |\n");
        return repoDir;
    }

    /// <summary>M.ORCH-T441: ticket_queue only takes tickets in the ticket database, so a repo still on its own USER_STORIES.md
    /// had approve and bump refuse every ticket ("No ticket"), and its features, chores and docs waited for approval forever.
    /// They're kept in settings.json for such a ticket now, where the scheduler reads them beside the database's queue.</summary>
    [Test]
    public void Approve_and_bump_work_for_a_repo_still_on_its_own_story_file()
    {
        var path = WorkspaceWithDb();
        var repoDir = StoryRepo();
        try
        {
            Assert.That(Run("approve", "b-t1"), Is.Zero, output.ToString());
            Assert.That(Run("bump", "B-T1", "--by", "3"), Is.Zero, output.ToString());
            Assert.That(output.ToString(), Does.Contain("B-T1 priority is now +3"));
            Assert.That(Run("reserve", "B-T2", "1M"), Is.Zero, "reserve takes it too");
            var s = Store.LoadSettings();
            Assert.That((s.StoryApproved, s.StoryPriorities), Is.EqualTo((new List<string> { "B-T1" }, new Dictionary<string, long> { ["B-T1"] = 3 })));
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueStates().Keys, Has.None.StartsWith("B-"), "nothing of B's in the database's queue");

            // What the scheduler makes of it: B-T1 is work, B-T2 still waits.
            var snap = new Snapshot(DateTime.UtcNow,
                [new RepoStatus("B", repoDir, RepoState.Dormant, [], [], [.. MindAttic.Tickets.Log.StoryFile.Load(Path.Combine(repoDir, "docs", "USER_STORIES.md")).Tickets.Where(t => t.Status == "open")], null, default)], []);
            var plan = MindAttic.Orchestrator.Scheduling.Scheduler.Plan(snap, Store.LoadSettings(), []);
            Assert.That((plan.Queue.Select(q => (q.Id, q.Priority)), plan.AwaitingApproval.Select(q => q.Id)),
                Is.EqualTo((new[] { ("B-T1", 3) }, new[] { "B-T2" })));

            Assert.That(Run("approve", "B-T3"), Is.EqualTo(1), "a closed ticket isn't approved");
            Assert.That(output.ToString(), Does.Contain("B-T3 isn't open"));
            Assert.That(Run("approve", "B-T9"), Is.EqualTo(1), "a typo isn't approved");
            Assert.That(output.ToString(), Does.Contain("No ticket B-T9"));

            Assert.That(Run("approve", "B-T1", "--undo"), Is.Zero);
            Assert.That(Run("bump", "B-T1", "--by", "-3"), Is.Zero);
            Assert.That((Store.LoadSettings().StoryApproved, Store.LoadSettings().StoryPriorities), Is.EqualTo((new List<string>(), new Dictionary<string, long>())));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Approve_works_before_the_workspace_has_a_ticket_database()
    {
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = Path.Combine(dir, "workspace") });
        StoryRepo();
        Assert.That(Run("approve", "B-T2"), Is.Zero, output.ToString());
        Assert.That(Store.LoadSettings().StoryApproved, Is.EqualTo(new List<string> { "B-T2" }));
    }

    /// <summary>M.ORCH-T441: once the repo is brought into the database, its settings.json approvals and priorities move into the
    /// database's queue (so tickets ticket approve --undo and the Tickets Desktop can take them back) and leave settings.json.</summary>
    [Test]
    public void Story_file_approvals_move_into_the_database_once_it_holds_the_ticket()
    {
        var path = WorkspaceWithDb();
        var repoDir = StoryRepo();
        try
        {
            Store.SaveSettings(Store.LoadSettings() with
            {
                StoryApproved = ["B-T1", "C-T1"],
                StoryPriorities = new() { ["B-T2"] = 4, ["C-T1"] = 2 },
            });
            var workspace = Path.Combine(dir, "workspace");
            Assert.That(MindAttic.Orchestrator.Tickets.TicketQueue.MoveIntoDatabase(Store, workspace), Is.Zero, "B isn't in the database yet");

            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                db.Import([new MindAttic.Tickets.Repo("B", repoDir)]);
            Assert.That(MindAttic.Orchestrator.Tickets.TicketQueue.MoveIntoDatabase(Store, workspace), Is.EqualTo(2));
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That((db.QueueState("B-T1").Approval, db.QueueState("B-T2")), Is.EqualTo(("approved", ("proposed", 4L))));
            var s = Store.LoadSettings();
            Assert.That((s.StoryApproved, s.StoryPriorities), Is.EqualTo((new List<string> { "C-T1" }, new Dictionary<string, long> { ["C-T1"] = 2 })),
                "what the database still doesn't hold stays");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T384: settings.json's approvals and priorities move into the database's queue once, without overriding what
    /// the database already says, and leave settings.json.</summary>
    [Test]
    public void Settings_approvals_and_priorities_move_into_the_database_once_and_leave_settings()
    {
        var path = WorkspaceWithDb();
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path)) db.SetPriority("A-T1", 4);
            Store.SaveSettings(Store.LoadSettings() with
            {
                Approved = ["a-t2", "A-T3", "A-T99"],
                Priorities = new() { ["A-T1"] = 9, ["A-T2"] = 3, ["A-T99"] = 1 },
            });
            var raw = StoreFaults.SettingsText(Store);
            Assert.That(raw, Does.Contain("\"Approved\""));

            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.Run(Store, Path.Combine(dir, "workspace")),
                Is.EqualTo(new MindAttic.Orchestrator.Tickets.QueueImport.Result(1, 1)), "A-T2 approved and prioritized; the closed A-T3, the unknown A-T99 and A-T1's own priority left");
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That((db.QueueState("A-T1"), db.QueueState("A-T2"), db.QueueState("A-T3").Approval),
                    Is.EqualTo((("proposed", 4L), ("approved", 3L), "proposed")));
            Assert.That(StoreFaults.SettingsText(Store), Does.Not.Contain("\"Approved\"").And.Not.Contain("\"Priorities\""), "dropped from the settings");
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.Run(Store, Path.Combine(dir, "workspace")), Is.Null, "once");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T389: once the move is made, a settings.json saved again with the old fields (by a process on an older
    /// build) is not imported again, so a ticket un-approved in the database since stays un-approved; the fields are dropped.</summary>
    [Test]
    public void Settings_rewritten_with_the_old_fields_after_the_move_do_not_reapprove_a_ticket_unapproved_in_the_database()
    {
        var path = WorkspaceWithDb();
        try
        {
            var workspace = Path.Combine(dir, "workspace");
            Store.SaveSettings(Store.LoadSettings() with { Approved = ["A-T2"], Priorities = new() { ["A-T2"] = 3 } });
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.Run(Store, workspace), Is.EqualTo(new MindAttic.Orchestrator.Tickets.QueueImport.Result(1, 1)));
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            {
                Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.IsDone(db.Path), Is.True, "the move is recorded in the database");
                db.Unapprove("A-T2");
                db.SetPriority("A-T2", 0);
            }

            // An older process saves its stale settings.json back, old fields and all.
            Store.SaveSettings(Store.LoadSettings() with { Approved = ["A-T2"], Priorities = new() { ["A-T2"] = 3 } });
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.Run(Store, workspace), Is.Null, "not imported again");
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.QueueState("A-T2"), Is.EqualTo(("proposed", 0L)));
            Assert.That(StoreFaults.SettingsText(Store), Does.Not.Contain("\"Approved\"").And.Not.Contain("\"Priorities\""), "dropped again");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Reserve_stores_the_ticket_id_upper_cased_and_0_releases_it()
    {
        Assert.That(Run("reserve", "m.orch-t7", "2M"), Is.Zero);
        Assert.That(Store.LoadSettings().Reservations, Is.EqualTo(new Dictionary<string, long> { ["M.ORCH-T7"] = 2_000_000 }));

        Assert.That(Run("reserve", "m.orch-t7", "0"), Is.Zero);
        Assert.That(Store.LoadSettings().Reservations, Is.Empty);
    }

    [Test]
    public void Reserve_refuses_tokens_it_cannot_read()
    {
        Assert.That(Run("reserve", "m.orch-t7", "lots"), Is.EqualTo(1));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

    [Test]
    public void Block_stores_both_ids_upper_cased()
    {
        // A workspace with no ticket database, so block keeps its prerequisites in settings (never the real workspace's).
        var workspace = Path.Combine(dir, "workspace");
        Directory.CreateDirectory(workspace);
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });

        Assert.That(Run("block", "m.orch-t9", "--by", "m.orch-t8", "--by", "M.Orch-T8"), Is.Zero);
        Assert.That(Store.LoadSettings().BlockedBy["M.ORCH-T9"], Is.EqualTo(new[] { "M.ORCH-T8" }));

        Assert.That(Run("block", "M.ORCH-T9", "--clear", "--by", "M.ORCH-T7"), Is.Zero);
        Assert.That(Store.LoadSettings().BlockedBy["M.ORCH-T9"], Is.EqualTo(new[] { "M.ORCH-T7" }), "--clear --by replaces, as on the database");

        Assert.That(Run("block", "M.ORCH-T9", "--clear"), Is.Zero);
        Assert.That(Store.LoadSettings().BlockedBy, Does.Not.ContainKey("M.ORCH-T9"));
    }

    [Test]
    public void Block_on_the_database_refuses_a_missing_id_itself_or_a_cycle_before_changing_anything()
    {
        var workspace = Path.Combine(dir, "workspace");
        var repoDir = Path.Combine(workspace, "A");
        Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
        Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
        File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | one | — |\n| A-T2 | 2026-10-01 | fix | open | two | — |\n| A-T3 | 2026-10-01 | fix | open | three | — |\n");
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        var path = MindAttic.Tickets.Data.TicketsDb.DefaultPath(workspace);
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            {
                db.Import([new MindAttic.Tickets.Repo("A", repoDir)]);
                db.AddLink("A-T1", "A-T2", "requires");
                db.AddLink("A-T3", "A-T1", "requires");
            }
            foreach (var (by, said) in new[] { ("A-T99", "no ticket A-T99"), ("A-T1", "can't require itself"), ("A-T3", "would be a cycle") })
            {
                Assert.That(Run("block", "A-T1", "--clear", "--by", by), Is.EqualTo(1), by);
                Assert.That(output.ToString(), Does.Contain(said));
            }
            Assert.That(Run("block", "A-T27", "--by", "A-T2"), Is.EqualTo(1), "the ticket blocked has to exist too");
            Assert.That(output.ToString(), Does.Contain("no ticket A-T27"));
            Assert.That((Run("bump", "A-T99"), Run("reserve", "A-T99", "2M")), Is.EqualTo((1, 1)), "a typo isn't bumped or reserved either");
            Assert.That((Store.LoadSettings().Priorities, Store.LoadSettings().Reservations.Count), Is.EqualTo((null as Dictionary<string, int>, 0)));
            Assert.That((Run("bump", "a-t2"), Run("reserve", "A-T2", "2M"), Run("reserve", "A-T99", "0")), Is.EqualTo((0, 0, 0)));
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.Requires("A-T1"), Is.EqualTo(new[] { "A-T2" }), "nothing changed: the --clear wasn't half-done");

            // M.ORCH-T331: a prerequisite set by hand before the migration is still in settings.json; --clear clears it too.
            Store.SaveSettings(Store.LoadSettings() with { BlockedBy = new() { ["A-T1"] = ["A-T3"], ["A-T2"] = ["A-T3"] } });
            Assert.That(Run("block", "A-T1"), Is.Zero);
            Assert.That(output.ToString(), Does.Contain("It still waits for A-T3 too"), "M.ORCH-T356: the hand-set one is said");
            Assert.That(Run("block", "A-T1", "--clear"), Is.Zero);
            Assert.That(Store.LoadSettings().BlockedBy.Keys, Is.EqualTo(new[] { "A-T2" }), "only the cleared ticket's");
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                Assert.That(db.Requires("A-T1"), Is.Empty);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Block_keeps_a_ticket_of_a_repo_still_on_its_story_file_in_settings()
    {
        // M.ORCH-T504: once the database existed, block only looked there, so a ticket that approve and bump find was "no ticket".
        var workspace = Path.Combine(dir, "workspace");
        string Repo(string name, string rows)
        {
            var repoDir = Path.Combine(workspace, name);
            Directory.CreateDirectory(Path.Combine(repoDir, ".git"));
            Directory.CreateDirectory(Path.Combine(repoDir, "docs"));
            File.WriteAllText(Path.Combine(repoDir, "docs", "USER_STORIES.md"),
                $"---\ncode: {name}\n---\n\n# {name}\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" + rows);
            return repoDir;
        }
        var a = Repo("A", "| A-T1 | 2026-10-01 | fix | open | one | — |\n");
        Repo("B", "| B-T1 | 2026-10-01 | fix | open | one | — |\n| B-T2 | 2026-10-01 | fix | open | two | — |\n");
        Store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = workspace });
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(MindAttic.Tickets.Data.TicketsDb.DefaultPath(workspace)))
                db.Import([new MindAttic.Tickets.Repo("A", a)]);

            Assert.That(Run("block", "b-t2", "--by", "B-T1", "--by", "A-T1"), Is.Zero);
            Assert.That(Store.LoadSettings().BlockedBy["B-T2"], Is.EqualTo(new[] { "B-T1", "A-T1" }));
            Assert.That(Run("block", "B-T2", "--by", "B-T9"), Is.EqualTo(1), "one that exists nowhere is still refused");
            Assert.That(output.ToString(), Does.Contain("no ticket B-T9"));
            Assert.That(Run("block", "B-T2", "--by", "B-T2"), Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("can't require itself"));
            Assert.That(Store.LoadSettings().BlockedBy["B-T2"], Is.EqualTo(new[] { "B-T1", "A-T1" }), "nothing changed");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Settings_rejects_an_unknown_provider_and_saves_nothing()
    {
        Assert.That(Run("settings", "--provider", "Clippy"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("No provider \"Clippy\""));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

    [Test]
    public void Settings_terminal_host_chooses_where_CLIs_open_and_refuses_a_typo()
    {
        Assert.That(Run("settings", "--terminal-host", "Orchestrator"), Is.Zero);
        Assert.That(Store.LoadSettings().TerminalHost, Is.EqualTo(OrchestratorSettings.OrchestratorHost));
        Assert.That(output.ToString(), Does.Contain("CLIs open as panes inside the Orchestrator"));
        Assert.That(Run("settings", "--terminal-host", "tmux"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("No terminal host \"tmux\""));
        Assert.That(Store.LoadSettings().TerminalHost, Is.EqualTo(OrchestratorSettings.OrchestratorHost), "a typo saves nothing");
        Assert.That(Run("settings", "--terminal-host", "wt"), Is.Zero);
        Assert.That(Store.LoadSettings().EmbeddedTerminals, Is.False);
    }

    [Test]
    public void Settings_sets_who_is_watched_for_outside_use_and_who_is_texted_and_refuses_an_unknown_recipient()
    {
        // M.ORCH-T702: Jordan's login watched and Ryan texted to start with.
        var defaults = Store.LoadSettings();
        Assert.That((defaults.OutsideUsePsstLogins, defaults.OutsideUsePsstTo), Is.EqualTo((new List<string> { "pdftransferjr@mindattic.com" }, "ryan")));
        var was = RemindCommand.ContactsPath;
        RemindCommand.ContactsPath = Path.Combine(Path.GetTempPath(), "no-psst-" + Guid.NewGuid().ToString("N"), "contacts.json");
        try
        {
            Assert.That(Run("settings", "--psst-outside", "Jordan, amy@x.com", "--psst-outside-to", "920-555-0101"), Is.Zero);
            var s = Store.LoadSettings();
            Assert.That((s.OutsideUsePsstLogins, s.OutsideUsePsstTo), Is.EqualTo((new List<string> { "Jordan", "amy@x.com" }, "920-555-0101")));
            // The console wraps the line, so its words are checked apart.
            Assert.That(output.ToString(), Does.Contain("920-555-0101 through Psst").And.Contain("Jordan, amy@x.com starts using it"));

            Assert.That(Run("settings", "--psst-outside-to", "nobody"), Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("\"nobody\" isn't a Psst contact or a US number"));
            Assert.That(Store.LoadSettings().OutsideUsePsstTo, Is.EqualTo("920-555-0101"), "a typo saves nothing");

            Assert.That(Run("settings", "--psst-outside", "none", "--psst-outside-to", "none"), Is.Zero);
            s = Store.LoadSettings();
            Assert.That((s.OutsideUsePsstLogins.Count, s.OutsideUsePsstTo), Is.EqualTo((0, "")));
            Assert.That(Run("settings", "--psst-outside", "default"), Is.Zero);
            Assert.That(Store.LoadSettings().OutsideUsePsstLogins, Is.EqualTo(new[] { "pdftransferjr@mindattic.com" }));
        }
        finally { RemindCommand.ContactsPath = was; }
    }

    [Test]
    public void Settings_sets_weekly_pacings_target_line_and_wind_down_and_refuses_bad_ones()
    {
        // M.ORCH-T680: the weekly target is 100 minus the reserve; the line must sit below it; the margin is 0 to 20.
        Assert.That(Run("settings", "--weekly-aware", "true", "--weekly-target", "90", "--weekly-line", "70", "--weekly-wind-down", "4"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That((s.WeeklyAwarePacing, s.WeeklyReservePercent, s.WeeklyAwareLinePercent, s.WeeklyWindDownMargin, s.WeeklyAwareWindDown),
            Is.EqualTo((true, 10, 70, 4, 86)));
        Assert.That(Run("settings", "--weekly-line", "90"), Is.EqualTo(1), "the line isn't below the target");
        Assert.That(Run("settings", "--weekly-reserve", "35"), Is.EqualTo(1), "a 65% target would sit under the 70% line");
        Assert.That(Run("settings", "--weekly-wind-down", "21"), Is.EqualTo(1));
        Assert.That(Run("settings", "--weekly-target", "45"), Is.EqualTo(1));
        var after = Store.LoadSettings();
        Assert.That((after.WeeklyReservePercent, after.WeeklyAwareLinePercent, after.WeeklyWindDownMargin), Is.EqualTo((10, 70, 4)), "nothing refused was saved");
    }

    [Test]
    public void Settings_stores_a_known_provider_in_its_own_casing()
    {
        Assert.That(Run("settings", "--provider", "codex"), Is.Zero);
        Assert.That(Store.LoadSettings().DefaultProvider, Is.EqualTo("Codex"));
    }

    [TestCase("--max-sessions", "-1")]
    [TestCase("--max-sessions", "0")]
    [TestCase("--groom-days", "0")]
    [TestCase("--stale-hours", "-1")]
    [TestCase("--max-subagents", "-1")]
    [TestCase("--hand-minutes", "-1")]
    public void Settings_rejects_out_of_range_numbers_and_saves_nothing(string option, string value)
    {
        Assert.That(Run("settings", option, value), Is.EqualTo(1));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

    [TestCase("--repo-sessions", "Prose")]
    [TestCase("--repo-sessions", "Prose=x")]
    [TestCase("--repo-sessions", "Prose=-1")]
    [TestCase("--repo-cap", "Prose=")]
    [TestCase("--repo-cap", "Prose=150")] // M.ORCH-T335: a share of the limit over 100% never applies
    [TestCase("--repo-cap", "=40")]
    [TestCase("--repo-sessions", " =3")]
    public void Settings_rejects_a_bad_REPO_equals_N_and_saves_nothing(string option, string value)
    {
        Assert.That(Run("settings", option, value), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Use REPO="));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

    [Test]
    public void A_repo_cap_applies_and_clears_whatever_the_casing_typed()
    {
        Assert.That(Run("settings", "--repo-sessions", "Prose=2", "--repo-cap", "prose=40"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That((OrchestratorSettings.ForRepo(s.RepoMaxSessions, "Prose", 9), OrchestratorSettings.ForRepo(s.RepoBudgetPercent, "Prose", 0)),
            Is.EqualTo((2, 40)), "prose=40 caps Prose");
        Assert.That(OrchestratorSettings.ForRepo(s.RepoMaxSessions, "Other", 9), Is.EqualTo(9));

        Assert.That(Run("settings", "--repo-sessions", "PROSE=0", "--repo-cap", "Prose=30"), Is.Zero);
        s = Store.LoadSettings();
        Assert.That((s.RepoMaxSessions.Count, s.RepoBudgetPercent.Single()), Is.EqualTo((0, new KeyValuePair<string, int>("Prose", 30))),
            "PROSE=0 cleared Prose; Prose=30 replaced prose");
    }

    [Test]
    public void Settings_escapes_a_bracket_in_saved_values_when_it_prints_them()
    {
        // Unescaped, "[odd" would be read as a markup tag and the command would crash after saving.
        Assert.That(Run("settings", "--repo-sessions", "[odd=3", "--repo-cap", "[cap]=40"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That(s.RepoMaxSessions["[odd"], Is.EqualTo(3));
        Assert.That(s.RepoBudgetPercent["[cap]"], Is.EqualTo(40));
        Assert.That(output.ToString(), Does.Contain("[odd 3").And.Contain("[cap] 40%"));
    }

    [Test]
    public void Budget_takes_token_counts_as_the_readme_writes_them()
    {
        // Read as 20M, not refused by the option's type before the command runs (the --warn mistake stops it short of saving).
        Assert.That(Run("budget", "--block", "20M", "--weekly", "1.5B", "--warn", "0"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--warn is a percent"));
        Assert.That(Run("budget", "--weekly", "lots"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--weekly: a number like"));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

    [TestCase("0")]
    [TestCase("101")]
    [TestCase("-5")]
    public void Budget_rejects_warn_outside_1_to_100_and_saves_nothing(string warn)
    {
        Assert.That(Run("budget", "--warn", warn), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("--warn is a percent from 1 to 100"));
        Assert.That(Store.Has(JsonStore.SettingsName), Is.False);
    }

}
