using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1528: a background shell's mark counts while its CLI is alive with a shell of its own started around the mark.</summary>
public sealed class BackgroundJobsTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MarkedAt = Now.AddHours(-1);

    private static BackgroundJobs.Mark Mark(int? cli = 100, DateTime? at = null) => new("b1", cli, "s1", at ?? MarkedAt, "Run the study");

    /// <summary>A Launcher above claude.exe 100, with an MCP server under cmd (300) beside whatever a test adds.</summary>
    private static Dictionary<int, (int Parent, string Exe)> Tree(params (int Pid, int Parent, string Exe)[] extra)
    {
        var tree = new Dictionary<int, (int, string)>
        {
            [50] = (1, "MindAttic.Launcher.exe"),
            [100] = (50, "claude.exe"),
            [300] = (100, "cmd.exe"),
        };
        foreach (var (pid, parent, exe) in extra) tree[pid] = (parent, exe);
        return tree;
    }

    private static Func<int, DateTime?> Starts(params (int Pid, DateTime At)[] starts) =>
        pid => starts.Where(s => s.Pid == pid).Select(s => (DateTime?)s.At).FirstOrDefault();

    [Test]
    public void A_job_whose_shell_still_runs_under_its_cli_counts()
    {
        var tree = Tree((200, 100, "bash.exe"), (201, 200, "dotnet.exe"));
        Assert.That(BackgroundJobs.Judge(Mark(), tree, Starts((200, MarkedAt.AddSeconds(-2))), Now), Is.True);
        Assert.That(BackgroundJobs.Judge(Mark(), Tree((200, 100, "powershell.exe")), Starts((200, MarkedAt.AddSeconds(-2))), Now), Is.True, "a PowerShell job too");
    }

    [Test]
    public void A_job_whose_shell_has_ended_does_not_count()
    {
        // Only the MCP server's cmd is left under the CLI, started about then: cmd is never a job's shell.
        Assert.That(BackgroundJobs.Judge(Mark(), Tree(), Starts((300, MarkedAt)), Now), Is.False);
        // A shell started well after the mark is a later call's, not the job's.
        Assert.That(BackgroundJobs.Judge(Mark(), Tree((200, 100, "bash.exe")), Starts((200, MarkedAt.AddMinutes(20))), Now), Is.False);
        // A shell under another CLI isn't this one's.
        var other = Tree((400, 1, "claude.exe"), (200, 400, "bash.exe"));
        Assert.That(BackgroundJobs.Judge(Mark(), other, Starts((200, MarkedAt)), Now), Is.False);
    }

    [Test]
    public void A_job_whose_cli_closed_or_whose_mark_names_none_or_is_too_old_does_not_count()
    {
        var tree = Tree((200, 100, "bash.exe"));
        var starts = Starts((200, MarkedAt));
        Assert.That(BackgroundJobs.Judge(Mark(cli: 999), tree, starts, Now), Is.False, "its CLI is gone");
        Assert.That(BackgroundJobs.Judge(Mark(cli: null), tree, starts, Now), Is.False, "no CLI to tie it to");
        Assert.That(BackgroundJobs.Judge(Mark(at: Now.AddHours(-49)), tree, Starts((200, Now.AddHours(-49))), Now), Is.False, "past its life");
    }

    [Test]
    public void Without_a_process_tree_a_mark_inside_its_life_counts()
    {
        Assert.That(BackgroundJobs.Judge(Mark(), null, _ => null, Now), Is.True);
        Assert.That(BackgroundJobs.Judge(Mark(at: Now.AddHours(-49)), null, _ => null, Now), Is.False);
    }

    [Test]
    public void A_mark_is_read_as_the_context_gate_writes_it()
    {
        var mark = BackgroundJobs.Parse("b1", """{"cli": 4242, "session_id": "s1", "at": "2026-11-07T21:33:05Z", "what": "Run"}""", Now)!;
        Assert.That(mark, Is.EqualTo(new BackgroundJobs.Mark("b1", 4242, "s1", new DateTime(2026, 11, 7, 21, 33, 5, DateTimeKind.Utc), "Run")));
        Assert.That(BackgroundJobs.Parse("b1", """{"cli": null}""", Now)!.Cli, Is.Null);
        Assert.That(BackgroundJobs.Parse("b1", """{"cli": "77"}""", Now)!.AtUtc, Is.EqualTo(Now), "no at: the file's write time");
        Assert.That(BackgroundJobs.Parse("b1", """{"cli": 4""", Now), Is.Null, "half written");
    }
}
