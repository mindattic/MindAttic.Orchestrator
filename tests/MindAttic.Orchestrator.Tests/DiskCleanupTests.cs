using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1449: under the --disk-cleanup-gb line, agent-made temp goes oldest first, and nothing else does.</summary>
public sealed class DiskCleanupTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc);
    private const long Mb = 1024 * 1024, GbBytes = 1024 * Mb;

    private static TempEntry E(string name, long bytes, TimeSpan age) => new($@"C:\T\claude\p\s\{name}", name, bytes, Now - age);

    private static SessionTemp S(string id, bool live, TimeSpan age, TempEntry[]? entries = null, TempEntry[]? scratch = null) =>
        new($@"C:\T\claude\p\{id}", id, live, Now - age, entries ?? [], scratch ?? []);

    [Test]
    public void An_ended_quiet_session_goes_but_never_its_task_output()
    {
        var s = S("a", live: false, TimeSpan.FromHours(3),
            [E("scratchpad", 5 * GbBytes, TimeSpan.FromHours(3)), E("tasks", 2 * GbBytes, TimeSpan.FromHours(3))]);
        var c = DiskCleanup.Candidates([s], Now);
        Assert.That(c.Select(x => x.Path), Is.EqualTo(new[] { @"C:\T\claude\p\s\scratchpad" }));
        Assert.That(c[0].Reason, Does.Contain("ended session"));
    }

    [Test]
    public void An_ended_session_written_lately_is_left_alone()
    {
        var s = S("a", live: false, TimeSpan.FromMinutes(10), [E("scratchpad", 5 * GbBytes, TimeSpan.FromMinutes(10))]);
        Assert.That(DiskCleanup.Candidates([s], Now), Is.Empty);
    }

    [Test]
    public void A_running_session_loses_only_big_scratch_untouched_for_two_hours()
    {
        var s = S("a", live: true, TimeSpan.FromMinutes(1),
            [E("scratchpad", 9 * GbBytes, TimeSpan.FromMinutes(1)), E("tasks", GbBytes, TimeSpan.FromHours(5))],
            [E("oldtree", 3 * GbBytes, TimeSpan.FromHours(3)), E("hottree", 3 * GbBytes, TimeSpan.FromMinutes(30)), E("note.md", Mb, TimeSpan.FromHours(9))]);
        var c = DiskCleanup.Candidates([s], Now);
        Assert.That(c.Select(x => Path.GetFileName(x.Path)), Is.EqualTo(new[] { "oldtree" }));
    }

    [Test]
    public void Candidates_are_oldest_first_and_the_plan_stops_once_enough_is_freed()
    {
        var old = S("a", live: false, TimeSpan.FromDays(2), [E("x", 4 * GbBytes, TimeSpan.FromDays(2))]);
        var mid = S("b", live: false, TimeSpan.FromDays(1), [E("y", 4 * GbBytes, TimeSpan.FromDays(1))]);
        var young = S("c", live: false, TimeSpan.FromHours(1), [E("z", 4 * GbBytes, TimeSpan.FromHours(1))]);
        var c = DiskCleanup.Candidates([young, mid, old], Now);
        Assert.That(c.Select(x => Path.GetFileName(x.Path)), Is.EqualTo(new[] { "x", "y", "z" }));
        Assert.That(DiskCleanup.Plan(c, 5 * GbBytes).Count, Is.EqualTo(2));
        Assert.That(DiskCleanup.Plan(c, 0), Is.Empty);
    }

    [TestCase(35, 30, 0L)]
    [TestCase(30, 30, 0L)]
    [TestCase(1, 0, 0L)]
    [TestCase(25, 30, 15 * GbBytes)]
    public void Need_is_what_reaches_the_line_plus_the_margin(double free, double line, long need) =>
        Assert.That(DiskCleanup.Need(free, line), Is.EqualTo(need).Within(GbBytes / 100));

    [Test]
    public void Only_session_id_folders_are_scanned_so_claude_codes_own_caches_stay()
    {
        var root = Path.Combine(Path.GetTempPath(), "orch-clean-" + Guid.NewGuid().ToString("N"));
        try
        {
            var session = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(root, "proj", session, "scratchpad", "tree"));
            Directory.CreateDirectory(Path.Combine(root, "bundled-skills", "2.1.316"));
            File.WriteAllBytes(Path.Combine(root, "proj", session, "scratchpad", "tree", "f.bin"), new byte[1024]);
            var found = DiskCleanup.Scan(root, new HashSet<string> { session });
            Assert.That(found.Select(f => f.SessionId), Is.EqualTo(new[] { session }));
            Assert.That(found[0].Live, Is.True);
            Assert.That(found[0].Scratch.Single().Bytes, Is.EqualTo(1024));
        }
        finally { try { Directory.Delete(root, recursive: true); } catch (IOException) { } }
    }
}
