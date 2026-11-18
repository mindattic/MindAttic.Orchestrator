using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Telling a forced <c>/quicksave</c> (the Save button's Quick Save All, M.ORCH-T1769; formerly Save all &amp;
/// close, M.ORCH-T1724) apart from a stale one already sitting in a repo's handoff (<see cref="QuicksaveCapture"/>).</summary>
public sealed class QuicksaveCaptureTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    [Test]
    public void TryReadCode_is_null_when_the_file_is_missing() =>
        Assert.That(QuicksaveCapture.TryReadCode(Path.Combine(temp.New(), "agent-session.json"), DateTime.UtcNow), Is.Null);

    [Test]
    public void TryReadCode_reads_a_save_written_at_or_after_the_baseline()
    {
        var dir = temp.New();
        var path = Path.Combine(dir, "agent-session.json");
        var baseline = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(path, $"{{\"code\":\"lively-oxen\",\"updatedAt\":\"{baseline:O}\"}}");
        Assert.That(QuicksaveCapture.TryReadCode(path, baseline), Is.EqualTo("lively-oxen"));
    }

    [Test]
    public void TryReadCode_is_null_for_a_save_written_before_the_baseline()
    {
        var dir = temp.New();
        var path = Path.Combine(dir, "agent-session.json");
        var before = new DateTime(2026, 11, 17, 7, 59, 0, DateTimeKind.Utc);
        File.WriteAllText(path, $"{{\"code\":\"stale-save\",\"updatedAt\":\"{before:O}\"}}");
        Assert.That(QuicksaveCapture.TryReadCode(path, before.AddMinutes(1)), Is.Null, "a save from before the baseline isn't the one just asked for");
    }

    [Test]
    public void TryReadCode_is_null_for_malformed_json()
    {
        var dir = temp.New();
        var path = Path.Combine(dir, "agent-session.json");
        File.WriteAllText(path, "not json");
        Assert.That(QuicksaveCapture.TryReadCode(path, DateTime.UtcNow), Is.Null);
    }

    [Test]
    public void SessionPath_is_the_repos_dot_agent_handoff_file() =>
        Assert.That(QuicksaveCapture.SessionPath(@"C:\Repo"), Is.EqualTo(Path.Combine(@"C:\Repo", ".agent", "agent-session.json")));
}
