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
    public void No_claude_temp_folder_prunes_nothing() =>
        Assert.That(SessionTempPruner.Prune(Path.Combine(root, "missing"), _ => false, DateTime.UtcNow), Is.EqualTo((0, 0L)));
}
