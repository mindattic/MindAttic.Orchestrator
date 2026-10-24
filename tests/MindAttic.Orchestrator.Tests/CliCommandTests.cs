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
            "status", "start", "send", "budget", "queue", "dispatch", "release", "daemon", "settings", "audit",
            "next", "dryrun", "show", "report", "billing", "clis",
            "ledger", "remind", "orgs", "orgs add", "orgs member", "members", "members add", "members set", "members remove", "workspace", "workspace export", "workspace install-apps", "workspace build",
        ];
        foreach (var command in commands)
            Assert.That(Run([.. command.Split(' '), "--help"]), Is.Zero, command + ": " + output);
    }

    /// <summary>M.ORCH-T762: every ticket function is MindAttic.Tickets' (its CLI and Tickets Desktop); the orchestrator keeps only
    /// what runs work (next --claim, dispatch, the queue, release).</summary>
    [Test]
    public void The_ticket_commands_are_MindAttic_Tickets_not_the_orchestrators()
    {
        Assert.That(Run("--help"), Is.Zero, output.ToString());
        var help = output.ToString();
        foreach (var gone in new[] { "approve", "bump", "assign", "block", "hygiene", "intake", "reserve" })
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(help, $@"^\s+{gone}\s", System.Text.RegularExpressions.RegexOptions.Multiline), Is.False, gone);
        foreach (var kept in new[] { "next", "dispatch", "queue", "release", "show" })
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(help, $@"^\s+{kept}\s", System.Text.RegularExpressions.RegexOptions.Multiline), Is.True, kept);
    }

    [Test]
    public void An_unreadable_settings_json_is_an_invalid_data_exception_the_entry_point_reports_in_one_line()
    {
        // M.ORCH-T332: Program.cs says an InvalidDataException's message in one line; it used to log it as a crash.
        Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsFile, "{ oops");
        var ex = Assert.Throws<InvalidDataException>(() => Run("budget", "--block", "20M"));
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

    /// <summary>M.ORCH-T384, M.ORCH-T762: approval, priority, assignment and reservations live in the ticket database, where
    /// tickets ticket approve/priority/assign/reserve and the Tickets Desktop write them through the engine; the scheduler reads
    /// them from there, and the orchestrator's settings hold none of it.</summary>
    [Test]
    public void The_scheduler_reads_approval_priority_assignment_and_reservations_from_the_ticket_database()
    {
        var path = WorkspaceWithDb();
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            {
                db.Approve("A-T2");
                db.SetPriority("A-T2", 2);
                db.Assign("A-T2", "Ryan@Example.com");
                db.Reserve("A-T1", 2_000_000);
            }
            var source = new MindAttic.Orchestrator.Tickets.OpenTicketSource();
            source.OpenFor(new MindAttic.Tickets.Repo("A", Path.Combine(dir, "workspace", "A")), new List<string>());
            Assert.That((source.Approved.Contains("a-t2"), source.Priorities.GetValueOrDefault("A-T2")), Is.EqualTo((true, 2L)));
            Assert.That((source.Assignees.GetValueOrDefault("a-t2"), source.Reservations.GetValueOrDefault("A-T1")), Is.EqualTo(("ryan@example.com", 2_000_000L)));
            var s = Store.LoadSettings();
            Assert.That((s.Approved, s.Priorities, s.Assignees.Count, s.Reservations.Count), Is.EqualTo((null as List<string>, null as Dictionary<string, int>, 0, 0)));

            // What the scheduler plans with: the snapshot carries the database's assignments and reservations.
            var snap = new Snapshot(DateTime.UtcNow, [new RepoStatus("A", Path.Combine(dir, "workspace", "A"), RepoState.Dormant, [], [],
                [new MindAttic.Tickets.Log.Ticket("A-T1", "2026-10-01", "fix", "open", "one", "—"), new MindAttic.Tickets.Log.Ticket("A-T2", "2026-10-01", "feature", "open", "two", "—")], null, default)], [])
            {
                Approved = source.Approved, Priorities = source.Priorities, Assignees = source.Assignees, Reservations = source.Reservations,
            };
            var plan = MindAttic.Orchestrator.Scheduling.Scheduler.Plan(snap, Store.LoadSettings(), []);
            Assert.That(plan.Queue.Single(q => q.Id == "A-T2").Assignee, Is.EqualTo("ryan@example.com"));
            Assert.That(plan.Queue.Single(q => q.Id == "A-T1").Reserved, Is.EqualTo(2_000_000L));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T762: show keeps the work the orchestrator ran (its sessions); the ticket itself (row, history, commits) is
    /// MindAttic.Tickets' to show, so show points there instead of repeating it.</summary>
    [Test]
    public void Show_lists_the_sessions_run_and_points_to_Tickets_for_the_ticket_itself()
    {
        var path = WorkspaceWithDb();
        try
        {
            string filed;
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                filed = db.Add("A", "fix", "open", "filed by the pace ledger", "pace-ledger.jsonl").Id;

            Assert.That(Run("show", filed.ToLowerInvariant()), Is.Zero, output.ToString());
            var said = System.Text.RegularExpressions.Regex.Replace(output.ToString(), @"\s+", " "); // wrapped at the console's width
            Assert.That(said, Does.Contain($"tickets ticket show {filed}").And.Contain("No dispatched sessions found")
                .And.Not.Contain("filed by the pace ledger"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
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

    /// <summary>M.ORCH-T761: approval and priority live in the ticket database alone, so the orchestrator and MindAttic.Tickets
    /// always agree: what the orchestrator's settings still hold from before isn't read.</summary>
    [Test]
    public void Approvals_left_in_settings_are_not_read()
    {
        var repoDir = StoryRepo();
        Store.SaveSettings(Store.LoadSettings() with { StoryApproved = ["B-T1"], StoryPriorities = new() { ["B-T1"] = 3 } });
        var snap = new Snapshot(DateTime.UtcNow,
            [new RepoStatus("B", repoDir, RepoState.Dormant, [], [], [.. MindAttic.Tickets.Log.StoryFile.Load(Path.Combine(repoDir, "docs", "USER_STORIES.md")).Tickets.Where(t => t.Status == "open")], null, default)], []);
        var plan = MindAttic.Orchestrator.Scheduling.Scheduler.Plan(snap, Store.LoadSettings(), []);
        Assert.That((plan.Queue.Select(q => q.Id), plan.AwaitingApproval.Select(q => (q.Id, q.Priority))),
            Is.EqualTo((Array.Empty<string>(), new[] { ("B-T1", 0), ("B-T2", 0) })));
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
                Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.IsDone(Store, db.Path), Is.True, "the move is recorded");
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

    /// <summary>M.ORCH-T761: the move's marker is kept in the orchestrator's own state, never written into the ticket database's
    /// file around the MindAttic.Tickets engine; one recorded there before still counts.</summary>
    [Test]
    public void The_settings_move_is_recorded_in_the_orchestrators_state_and_an_old_marker_in_the_ticket_database_still_counts()
    {
        var path = WorkspaceWithDb();
        try
        {
            var workspace = Path.Combine(dir, "workspace");
            Store.SaveSettings(Store.LoadSettings() with { Approved = ["A-T2"] });
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.Run(Store, workspace), Is.EqualTo(new MindAttic.Orchestrator.Tickets.QueueImport.Result(1, 0)));
            Assert.That(Store.Has(MindAttic.Orchestrator.Tickets.QueueImport.DoneState), Is.True);
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM meta WHERE key = $k";
                cmd.Parameters.AddWithValue("$k", MindAttic.Orchestrator.Tickets.QueueImport.DoneKey);
                Assert.That(cmd.ExecuteScalar(), Is.EqualTo(0L), "nothing written into the ticket database's file");
            }

            // A fresh orchestrator store, and the ticket database that recorded the move before M.ORCH-T761.
            var other = new JsonStore(Path.Combine(dir, "other"), new OrchestratorDb(Path.Combine(dir, "other", "orchestrator.db")));
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.IsDone(other, path), Is.False);
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "INSERT INTO meta (key, value) VALUES ($k, '2026-01-01T00:00:00Z')";
                cmd.Parameters.AddWithValue("$k", MindAttic.Orchestrator.Tickets.QueueImport.DoneKey);
                cmd.ExecuteNonQuery();
            }
            Assert.That(MindAttic.Orchestrator.Tickets.QueueImport.IsDone(other, path), Is.True);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    /// <summary>M.ORCH-T762: the assignments, reservations and hand-set blocks the orchestrator kept in its settings move into the
    /// ticket database once it holds their tickets, without overriding what it already says, and leave the settings; the
    /// scheduler no longer reads a block left in settings.</summary>
    [Test]
    public void Assignments_reservations_and_blocks_in_settings_move_into_the_ticket_database()
    {
        var path = WorkspaceWithDb();
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
                db.Assign("A-T2", "jordan@example.com");
            Store.SaveSettings(Store.LoadSettings() with
            {
                Assignees = new() { ["A-T1"] = "Ryan@Example.com", ["A-T2"] = "ryan@example.com", ["Z-T1"] = "ryan@example.com" },
                Reservations = new() { ["A-T1"] = 2_000_000, ["Z-T1"] = 5 },
                BlockedBy = new() { ["A-T1"] = ["A-T2"], ["Z-T1"] = ["A-T1"] },
            });
            var workspace = Path.Combine(dir, "workspace");

            Assert.That(MindAttic.Orchestrator.Tickets.TicketQueue.MoveWorkIntoDatabase(Store, workspace), Is.EqualTo(4));
            using (var db = MindAttic.Tickets.Data.TicketsDb.Open(path))
            {
                Assert.That((db.Work("A-T1").Assignee, db.Work("A-T2").Assignee), Is.EqualTo(("ryan@example.com", "jordan@example.com")),
                    "the database's own assignee wins");
                Assert.That(db.Work("A-T1").Reserved, Is.EqualTo(2_000_000L));
                Assert.That(db.Requires("A-T1"), Is.EqualTo(new[] { "A-T2" }));
            }
            var s = Store.LoadSettings();
            Assert.That((s.Assignees.Keys, s.Reservations.Keys, s.BlockedBy.Keys), Is.EqualTo((new[] { "Z-T1" }, new[] { "Z-T1" }, new[] { "Z-T1" })),
                "what the database doesn't hold stays");
            Assert.That(MindAttic.Orchestrator.Tickets.TicketQueue.MoveWorkIntoDatabase(Store, workspace), Is.Zero, "once");

            // A block left in settings isn't read: only the database's 'requires' links (and "blocked by X" in a summary) are.
            var snap = new Snapshot(DateTime.UtcNow, [new RepoStatus("A", Path.Combine(workspace, "A"), RepoState.Dormant, [], [],
                [new MindAttic.Tickets.Log.Ticket("A-T1", "2026-10-01", "fix", "open", "one", "—"), new MindAttic.Tickets.Log.Ticket("A-T3", "2026-10-01", "fix", "open", "three", "—")], null, default)], []);
            var plan = MindAttic.Orchestrator.Scheduling.Scheduler.Plan(snap, new OrchestratorSettings { BlockedBy = new() { ["A-T1"] = ["A-T3"] } }, []);
            Assert.That(plan.Queue.Single(q => q.Id == "A-T1").BlockedBy, Is.Empty);
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
    public void Settings_has_no_terminal_host_every_CLI_opens_in_Windows_Terminal()
    {
        // M.ORCH-T751: the dashboard's terminal panes are gone, and "Where CLIs open" with them.
        Assert.That(Run("settings"), Is.Zero);
        Assert.That(output.ToString(), Does.Contain("CLIs open in Windows Terminal, a window per person").And.Not.Contain("panes"));
        Assert.That(typeof(OrchestratorSettings).GetProperty("TerminalHost"), Is.Null);
    }

    [Test]
    public void Settings_sets_who_cutoff_alerts_are_texted_to_and_refuses_an_unknown_recipient()
    {
        // M.ORCH-T742: Ryan texted to start with.
        var defaults = Store.LoadSettings();
        Assert.That(defaults.PsstAlertTo, Is.EqualTo("ryan"));
        var was = RemindCommand.ContactsPath;
        RemindCommand.ContactsPath = Path.Combine(Path.GetTempPath(), "no-psst-" + Guid.NewGuid().ToString("N"), "contacts.json");
        try
        {
            Assert.That(Run("settings", "--alert-to", "920-555-0101"), Is.Zero);
            var s = Store.LoadSettings();
            Assert.That(s.PsstAlertTo, Is.EqualTo("920-555-0101"));
            Assert.That(output.ToString(), Does.Contain("920-555-0101 through Psst"));

            Assert.That(Run("settings", "--alert-to", "nobody"), Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("\"nobody\" isn't a Psst contact or a US number"));
            Assert.That(Store.LoadSettings().PsstAlertTo, Is.EqualTo("920-555-0101"), "a typo saves nothing");

            Assert.That(Run("settings", "--alert-to", "none"), Is.Zero);
            s = Store.LoadSettings();
            Assert.That(s.PsstAlertTo, Is.EqualTo(""));
        }
        finally { RemindCommand.ContactsPath = was; }
    }

    [Test]
    public void Settings_refuses_the_retired_weekly_options_and_pacing_ones_and_changes_nothing()
    {
        // M.ORCH-T707: the weekly line is each member's own cutoff (org limit); M.ORCH-T706: the retired options are refused.
        var before = Store.LoadSettings();
        foreach (var locked in new[] { new[] { "--weekly-target", "90" }, new[] { "--weekly-reserve", "5" }, new[] { "--weekly-line", "70" }, new[] { "--weekly-wind-down", "4" },
                     new[] { "--weekly-aware", "true" }, new[] { "--failover", "true" }, new[] { "--throttle-agents", "true" }, new[] { "--pace-profile", "front-loaded" }, new[] { "--pace-curve-k", "0.5" } })
            Assert.That(Run(["settings", .. locked]), Is.EqualTo(1), string.Join(" ", locked));
        var after = Store.LoadSettings();
        Assert.That((after.WeeklyReservePercent, after.WeeklyAwareLinePercent, after.WeeklyAwarePacing, after.ThrottleAgents, after.FailoverToApiKey, after.PaceProfile),
            Is.EqualTo((before.WeeklyReservePercent, before.WeeklyAwareLinePercent, false, false, false, OrchestratorSettings.LinearProfile)));
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
    [TestCase("--agent-abort-after", "-1")]
    [TestCase("--agent-abort-after", "61")]
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
    public void Settings_sets_the_agent_abort_cool_off_and_prints_it()
    {
        // M.ORCH-T795: M.ORCH-T786's two settings, exposed through the CLI like every other threshold.
        var defaults = Store.LoadSettings();
        Assert.That((defaults.AgentAbort, defaults.AgentAbortAfterMinutes), Is.EqualTo((true, 3)));

        Assert.That(Run("settings", "--agent-abort", "false", "--agent-abort-after", "10"), Is.Zero);
        var s = Store.LoadSettings();
        Assert.That((s.AgentAbort, s.AgentAbortAfterMinutes), Is.EqualTo((false, 10)));
        Assert.That(output.ToString(), Does.Contain("cool-off abort off"));

        Assert.That(Run("settings", "--agent-abort", "true"), Is.Zero);
        // The line can wrap mid-number at the test console's width, so the two halves are checked apart (M.ORCH-T795).
        Assert.That(output.ToString().Replace("\n", " "), Does.Contain("cool-off abort on").And.Contain("min past the line before aborting"));
        Assert.That(Store.LoadSettings().AgentAbortAfterMinutes, Is.EqualTo(10));
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
