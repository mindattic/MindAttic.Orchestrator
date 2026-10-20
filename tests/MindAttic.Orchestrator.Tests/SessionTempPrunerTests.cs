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
            pruned = SessionTempPruner.Prune(root, id => states.GetValueOrDefault(id), now);

        Assert.That(pruned, Is.EqualTo((2, 150L)));
        Assert.That(Directory.Exists(ended) || Directory.Exists(unknownQuiet), Is.False);
        Assert.That(Directory.Exists(running), Is.True, "a running session keeps its folder");
        Assert.That(Directory.Exists(unknownRecent), Is.True, "a session nobody recorded is kept while it's recently written");
        Assert.That(Directory.Exists(endedButWriting), Is.True, "a log still open waits for the next look");
        Assert.That(Directory.Exists(notASession), Is.True, "only session-id folders are judged");

        Assert.That(SessionTempPruner.Prune(root, _ => false, now), Is.EqualTo((3, 30L)));
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

        Assert.That(SessionTempPruner.Prune(root, _ => false, DateTime.UtcNow), Is.EqualTo((1, 42L)));
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
            pruned = SessionTempPruner.Prune(root, _ => false, DateTime.UtcNow);

        Assert.That(pruned, Is.EqualTo((1, 40L)), "only the session's own files are counted, and the open file outside doesn't keep it");
        Assert.That(Directory.Exists(dir), Is.False);
        Assert.That(File.Exists(kept) && File.GetAttributes(kept).HasFlag(FileAttributes.ReadOnly), Is.True, "the target is left as it was");
        File.SetAttributes(kept, FileAttributes.Normal);
    }

    [Test]
    public void No_claude_temp_folder_prunes_nothing() =>
        Assert.That(SessionTempPruner.Prune(Path.Combine(root, "missing"), _ => false, DateTime.UtcNow), Is.EqualTo((0, 0L)));
}
