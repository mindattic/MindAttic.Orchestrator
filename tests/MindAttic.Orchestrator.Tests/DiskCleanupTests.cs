using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1449, M.ORCH-T1467: under the --disk-cleanup-gb line, ended sessions' temp goes by liveness, never age, and
/// a running session's scratch is only named.</summary>
public sealed class DiskCleanupTests
{
    [Test]
    public void Only_ended_sessions_folders_go_and_claude_codes_own_caches_and_running_sessions_stay()
    {
        var temp = Path.Combine(Path.GetTempPath(), "orch-clean-" + Guid.NewGuid().ToString("N"));
        try
        {
            string Session(string id)
            {
                var dir = Path.Combine(temp, "claude", "0123456789abcdef", id);
                Directory.CreateDirectory(Path.Combine(dir, "scratchpad", "tree"));
                File.WriteAllBytes(Path.Combine(dir, "scratchpad", "tree", "f.bin"), new byte[1024]);
                return dir;
            }
            var ended = Session("11111111-1111-1111-1111-111111111111");
            var running = Session("22222222-2222-2222-2222-222222222222");
            var cache = Path.Combine(temp, "claude", "bundled-skills", "2.1.316");
            Directory.CreateDirectory(cache);
            var liveness = SessionLiveness.From(id => id.StartsWith('1') ? false : true);

            var dry = DiskCleanup.Run(liveness, dryRun: true, logPath: null, tempDir: temp);
            Assert.That(dry.Deleted.Select(d => d.Path), Is.EqualTo(new[] { ended }));
            Assert.That(dry.FreedBytes, Is.EqualTo(1024));
            Assert.That(Directory.Exists(ended), Is.True, "a dry run deletes nothing");

            var r = DiskCleanup.Run(liveness, dryRun: false, logPath: null, tempDir: temp);
            Assert.That(r.FreedBytes, Is.EqualTo(1024));
            Assert.That(Directory.Exists(ended), Is.False);
            Assert.That(Directory.Exists(running) && Directory.Exists(cache), Is.True);
        }
        finally { try { Directory.Delete(temp, recursive: true); } catch (IOException) { } }
    }

    [Test]
    public void A_kept_sessions_big_scratch_is_only_named() =>
        Assert.That(DiskCleanup.LiveScratch([new TempFolderVerdict(@"C:\nowhere\s", "s", 100, Delete: false, "kept: running")]), Is.Empty,
            "under the naming size, or not there, nothing is named");
}
