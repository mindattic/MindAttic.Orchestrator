using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Save all &amp; close (M.ORCH-T1724): its own one-shot pending file (<see cref="SaveAllCloseStore"/>, separate
/// from the crash-recovery session-restore file) and telling a forced <c>/quicksave</c> apart from a stale one already
/// sitting in a repo's handoff (<see cref="QuicksaveCapture"/>).</summary>
public sealed class SaveAllCloseTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static PaneRequest Pane(string directory) => new("RD: Repo [Claude]", directory) { Repo = "Repo", Provider = "Claude" };

    [Test]
    public void Saves_and_loads_its_tabs_and_codes()
    {
        var store = new SaveAllCloseStore(temp.New());
        var state = new SaveAllCloseState
        {
            State = new RestoreState
            {
                SavedUtc = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc), Reason = "save-all-close", Pid = 42,
                Tabs = [new RestoreTab { Request = Pane(@"C:\Repo1"), SessionId = "s1", Login = "a@b.com" },
                        new RestoreTab { Request = Pane(@"C:\Repo2"), Login = "b@c.com" }],
            },
            Codes = ["lively-oxen", null],
        };
        Assert.That(store.Save(state), Is.True);
        var back = store.Load();
        Assert.That(back, Is.Not.Null);
        Assert.That(back!.State.Tabs, Has.Count.EqualTo(2));
        Assert.That(back.Codes[0], Is.EqualTo("lively-oxen"));
        Assert.That(back.Codes[1], Is.Null);
        Assert.That(Directory.GetFiles(store.Directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Load_is_null_when_nothing_was_saved() => Assert.That(new SaveAllCloseStore(temp.New()).Load(), Is.Null);

    [Test]
    public void Delete_removes_it_and_a_second_delete_is_not_an_error()
    {
        var store = new SaveAllCloseStore(temp.New());
        store.Save(new SaveAllCloseState { State = new RestoreState { Tabs = [new RestoreTab { Request = Pane(@"C:\Repo") }] } });
        Assert.That(File.Exists(store.PendingPath), Is.True);
        store.Delete();
        Assert.That(File.Exists(store.PendingPath), Is.False);
        Assert.DoesNotThrow(() => store.Delete());
    }

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
