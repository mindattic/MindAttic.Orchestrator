using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The save-state slots and what a Quick Load types (M.ORCH-T1869).</summary>
[TestFixture]
public sealed class SaveStateTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "savestate-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    private static RestoreTab Tab(string directory = @"D:\ws\Repo", IReadOnlyList<string>? allocate = null, IReadOnlyList<string>? crossrepo = null, string? code = null) =>
        new() { Request = new PaneRequest("RD: Repo", directory), Allocate = allocate, Crossrepo = crossrepo, HandoffCode = code };

    [Test]
    public void The_newest_save_is_the_latest_and_old_ones_beyond_keep_go()
    {
        var store = new SaveStateStore(dir);
        var t0 = new DateTime(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < SaveStateStore.Keep + 2; i++)
            store.Save(new RestoreState { SavedUtc = t0.AddMinutes(i), Pid = i, Tabs = [Tab()] });
        Assert.That(store.Saves(), Has.Count.EqualTo(SaveStateStore.Keep));
        Assert.That(store.Latest()?.Pid, Is.EqualTo(SaveStateStore.Keep + 1));
    }

    [Test]
    public void A_load_takes_the_newer_of_the_save_and_the_autosave()
    {
        var save = new RestoreState { SavedUtc = new DateTime(2026, 11, 20, 1, 0, 0, DateTimeKind.Utc), Pid = 1, Tabs = [Tab()] };
        var autosave = save with { SavedUtc = save.SavedUtc.AddHours(1), Pid = 2 };
        Assert.That(SaveStateStore.Pick(save, autosave)?.Pid, Is.EqualTo(2));
        Assert.That(SaveStateStore.Pick(save, autosave with { Tabs = [] })?.Pid, Is.EqualTo(1), "an autosave with no tabs is skipped");
        Assert.That(SaveStateStore.Pick(null, null), Is.Null);
    }

    [Test]
    public void Allocate_comes_back_as_it_was()
    {
        Assert.That(SaveStateStore.AllocateCommand(Tab()), Is.Null);
        Assert.That(SaveStateStore.AllocateCommand(Tab(allocate: ["Repo"])), Is.EqualTo("/allocate"));
        Assert.That(SaveStateStore.AllocateCommand(Tab(allocate: ["*"])), Is.EqualTo("/allocate all"));
        Assert.That(SaveStateStore.AllocateCommand(Tab(allocate: ["A", "B"])), Is.EqualTo("/allocate -repo=A,B"));
    }

    [Test]
    public void Crossrepo_comes_back_as_it_was()
    {
        Assert.That(SaveStateStore.CrossrepoCommand(Tab()), Is.Null);
        Assert.That(SaveStateStore.CrossrepoCommand(Tab(crossrepo: ["A", "B"])), Is.EqualTo("/crossrepo A,B"));
        Assert.That(SaveStateStore.CrossrepoCommand(Tab(crossrepo: ["*"])), Is.EqualTo("/crossrepo all"));
    }

    [Test]
    public void A_restored_cli_loads_its_handoff_or_carries_on()
    {
        Assert.That(SaveStateStore.ResumePrompt(Tab(code: "lively-oxen")), Is.EqualTo("/quickload lively-oxen"));
        Assert.That(SaveStateStore.ResumePrompt(Tab()), Does.Contain("Continue exactly where you left off"));
    }

    [Test]
    public void The_new_fields_survive_a_round_trip()
    {
        var state = new RestoreState { Tabs = [Tab(allocate: ["A"], crossrepo: ["B"], code: "c") with { Subagents = 3 }] };
        var back = SessionRestoreStore.Deserialize(SessionRestoreStore.Serialize(state))!.Tabs[0];
        Assert.That(back.HandoffCode, Is.EqualTo("c"));
        Assert.That(back.Allocate, Is.EqualTo(new[] { "A" }));
        Assert.That(back.Crossrepo, Is.EqualTo(new[] { "B" }));
        Assert.That(back.Subagents, Is.EqualTo(3));
    }
}
