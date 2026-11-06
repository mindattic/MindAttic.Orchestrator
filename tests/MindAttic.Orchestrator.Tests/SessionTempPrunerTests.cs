using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SessionTempPrunerTests
{
    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("orch-claude-temp-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(root, recursive: true);

    private string Session(string project, string id, int bytes, DateTime? writtenUtc = null)
    {
        var dir = Path.Combine(root, project, id);
        Directory.CreateDirectory(Path.Combine(dir, "tasks"));
        Directory.CreateDirectory(Path.Combine(dir, "scratchpad"));
        var output = Path.Combine(dir, "tasks", "b1.output");
        File.WriteAllBytes(output, new byte[bytes]);
        if (writtenUtc is { } at)
        {
            File.SetLastWriteTimeUtc(output, at);
            foreach (var d in new[] { Path.Combine(dir, "tasks"), Path.Combine(dir, "scratchpad"), dir }) Directory.SetLastWriteTimeUtc(d, at);
        }
        return dir;
    }

    [Test]
    public void Ended_sessions_folders_are_deleted_and_running_ones_and_open_output_are_left()
    {
        var now = DateTime.UtcNow;
        var ended = Session("D--Projects-A", "11111111-1111-1111-1111-111111111111", 100);
        var running = Session("D--Projects-A", "22222222-2222-2222-2222-222222222222", 10);
        var unknownQuiet = Session("D--Projects-B", "33333333-3333-3333-3333-333333333333", 50, now.AddDays(-2));
        var unknownRecent = Session("D--Projects-B", "44444444-4444-4444-4444-444444444444", 10);
        var endedButWriting = Session("D--Projects-C", "55555555-5555-5555-5555-555555555555", 10);
        var notASession = Path.Combine(root, "D--Projects-C", "bash-edit-diff");
        Directory.CreateDirectory(notASession);
        var states = new Dictionary<string, bool?>
        {
            ["11111111-1111-1111-1111-111111111111"] = false,
            ["22222222-2222-2222-2222-222222222222"] = true,
            ["55555555-5555-5555-5555-555555555555"] = false,
        };

        (int, long) pruned;
        // A background command still writing its log holds it open.
        using (new FileStream(Path.Combine(endedButWriting, "tasks", "b1.output"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            pruned = SessionTempPruner.Prune(root, id => states.GetValueOrDefault(id));

        Assert.That(pruned, Is.EqualTo((1, 100L)));
        Assert.That(Directory.Exists(ended), Is.False);
        Assert.That(Directory.Exists(running), Is.True, "a running session keeps its folder");
        Assert.That(Directory.Exists(unknownQuiet) && Directory.Exists(unknownRecent), Is.True,
            "a session nobody recorded is kept however long it's been quiet: age never ends a session (M.ORCH-T1467)");
        Assert.That(Directory.Exists(endedButWriting), Is.True, "a log still open waits for the next look");
        Assert.That(Directory.Exists(notASession), Is.True, "only session-id folders are judged");

        Assert.That(SessionTempPruner.Prune(root, _ => false), Is.EqualTo((4, 80L)));
        Assert.That(Directory.Exists(Path.Combine(root, "D--Projects-A")), Is.False, "a project folder left empty goes too");
        Assert.That(Directory.Exists(notASession), Is.True);
    }

    [Test]
    public void A_session_is_running_while_the_cli_its_state_names_is_and_unknown_without_one()
    {
        var cycler = new ContextCycler(root);
        File.WriteAllText(Path.Combine(root, "ended.state.json"), """{"state":"idle","pid":2147483000}""");
        File.WriteAllText(Path.Combine(root, "nopid.state.json"), """{"state":"idle"}""");

        Assert.That(cycler.SessionRunning("ended"), Is.False);
        Assert.That(cycler.SessionRunning("nopid"), Is.Null);
        Assert.That(cycler.SessionRunning("never-seen"), Is.Null);
    }

    [Test]
    public void An_ended_sessions_folder_holding_a_read_only_file_is_deleted()
    {
        var dir = Session("D--Projects-A", "66666666-6666-6666-6666-666666666666", 40);
        var readOnly = Path.Combine(dir, "scratchpad", "repo", ".git", "objects", "ab", "cdef");
        Directory.CreateDirectory(Path.GetDirectoryName(readOnly)!);
        File.WriteAllBytes(readOnly, new byte[2]);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);

        Assert.That(SessionTempPruner.Prune(root, _ => false), Is.EqualTo((1, 42L)));
        Assert.That(Directory.Exists(dir), Is.False, "a read-only file isn't one in use");
    }

    [Test]
    public void A_junction_in_an_ended_sessions_folder_is_removed_without_touching_its_target()
    {
        // M.ORCH-T507: the walk went through junctions, clearing read-only flags, holding and counting files outside the folder,
        // and a file open there kept the session's folder for good.
        var dir = Session("D--Projects-A", "77777777-7777-7777-7777-777777777777", 40);
        var outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        var kept = Path.Combine(outside, "kept.bin");
        File.WriteAllBytes(kept, new byte[1000]);
        File.SetAttributes(kept, FileAttributes.ReadOnly);
        var link = Path.Combine(dir, "scratchpad", "node_modules");
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", ["/c", "mklink", "/J", link, outside])
               { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!)
            mklink.WaitForExit();
        Assume.That(Directory.Exists(Path.Combine(link)) && new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint), "a junction could be made");

        (int, long) pruned;
        using (new FileStream(Path.Combine(outside, "open.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            pruned = SessionTempPruner.Prune(root, _ => false);

        Assert.That(pruned, Is.EqualTo((1, 40L)), "only the session's own files are counted, and the open file outside doesn't keep it");
        Assert.That(Directory.Exists(dir), Is.False);
        Assert.That(File.Exists(kept) && File.GetAttributes(kept).HasFlag(FileAttributes.ReadOnly), Is.True, "the target is left as it was");
        File.SetAttributes(kept, FileAttributes.Normal);
    }

    [Test]
    public void A_dry_run_deletes_nothing_and_says_why_each_folder_would_go_or_stay()
    {
        var ended = Session("D--Projects-A", "88888888-8888-8888-8888-888888888888", 10);
        var referenced = Session("D--Projects-A", "99999999-9999-9999-9999-999999999999", 10);
        var used = Session("D--Projects-A", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 10);
        var liveness = new SessionLiveness(_ => new SessionVerdict(false, "its CLI has exited"),
            id => id.StartsWith("9999", StringComparison.Ordinal) ? "the handoff h.json" : null,
            folder => folder.EndsWith("aaaaaaaaaaaa", StringComparison.Ordinal) ? "tail.exe (pid 1) still name it on their command line" : null);

        var r = SessionTempPruner.Prune(root, liveness, dryRun: true);

        Assert.That(r.Deleted, Is.Zero);
        Assert.That(Directory.Exists(ended) && Directory.Exists(referenced) && Directory.Exists(used), Is.True, "a dry run deletes nothing");
        Assert.That(r.Folders.Single(f => f.Path == ended).Delete, Is.True);
        Assert.That(r.Folders.Single(f => f.Path == referenced).Why, Does.Contain("h.json"));
        Assert.That(r.Folders.Single(f => f.Path == used).Why, Does.Contain("tail.exe"));

        Assert.That(SessionTempPruner.Prune(root, liveness).Deleted, Is.EqualTo(1), "only the folder nothing points at or uses goes");
        Assert.That(Directory.Exists(ended), Is.False);
    }

    [Test]
    public void Liveness_comes_from_the_recorded_cli_and_unknown_is_never_ended_while_any_cli_runs()
    {
        var states = new Dictionary<string, List<SessionLiveness.State>>(StringComparer.OrdinalIgnoreCase)
        {
            ["live"] = [new(10, null)],
            ["gone"] = [new(11, null)],
            ["cleared"] = [new(11, null), new(10, null)],
        };
        var procs = new List<SessionLiveness.Proc>
        {
            new(10, "claude.exe", "claude"), new(12, "claude.exe", "claude --resume resumed"),
            new(20, "tail.exe", "tail -F C:/Users/u/AppData/Local/Temp/claude/0123456789abcdef/gone/tasks/b1.output"),
        };
        Dictionary<string, string> None() => [];
        var l = SessionLiveness.Decide(states, new HashSet<int> { 10, 12 }, procs, None);

        Assert.That(l.Running("live").Running, Is.True);
        Assert.That(l.Running("gone").Running, Is.False);
        Assert.That(l.Running("cleared").Running, Is.True, "a session of a CLI that still runs (it /clear-ed) is kept");
        Assert.That(l.Running("resumed").Running, Is.True);
        Assert.That(l.Running("never-recorded").Running, Is.Null);
        Assert.That(l.UsedBy(@"C:\Users\u\AppData\Local\Temp\claude\0123456789abcdef\gone"), Does.Contain("tail.exe"));
        Assert.That(SessionLiveness.Decide(states, new HashSet<int>(), procs, None).Running("never-recorded").Running,
            Is.False, "with no Claude CLI running at all, every session has ended");
        Assert.That(SessionLiveness.Decide(states, new HashSet<int>(), null, None).UsedBy("x"),
            Is.Not.Null, "command lines that can't be read keep the folder");
    }

    [Test]
    public void A_path_into_a_sessions_temp_folder_is_found_however_it_is_spelled() =>
        Assert.That(SessionLiveness.SessionIdsIn(
            """{"p":"C:\\Users\\u\\AppData\\Local\\Temp\\claude\\b5f60e82a367ad48\\D47BCC9F-306D-4C94-94DB-B128810B563C\\scratchpad"} /tmp/claude/c41659285a6d5e09/8e6b88c0-5c42-417b-ae3c-a490b4ec4acc/x"""),
            Is.EqualTo(new[] { "d47bcc9f-306d-4c94-94db-b128810b563c", "8e6b88c0-5c42-417b-ae3c-a490b4ec4acc" }));

    [Test]
    public void No_claude_temp_folder_prunes_nothing() =>
        Assert.That(SessionTempPruner.Prune(Path.Combine(root, "missing"), _ => false), Is.EqualTo((0, 0L)));
}
