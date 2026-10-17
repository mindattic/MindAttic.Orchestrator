using System.Diagnostics;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The process helpers the sessions code leans on: parent chains, the headless agent's command line and start times.</summary>
public sealed class ProcessHelperTests
{
    private string dir = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-process-helpers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    // ---- ProcessTree.IsAncestor ----

    private static readonly Dictionary<int, int> Chain = new()
    {
        [100] = 4,   // the Launcher, started by System
        [200] = 100, // a terminal tab
        [300] = 200, // the shell in it
        [400] = 300, // claude.exe
        [900] = 4,   // something unrelated
    };

    [Test]
    public void An_ancestor_is_found_anywhere_up_the_chain()
    {
        Assert.That(ProcessTree.IsAncestor(300, 400, Chain), Is.True, "direct parent");
        Assert.That(ProcessTree.IsAncestor(100, 400, Chain), Is.True, "three steps up");
        Assert.That(ProcessTree.IsAncestor(4, 400, Chain), Is.True, "the root");
    }

    [Test]
    public void A_process_outside_the_chain_or_below_it_is_not_an_ancestor()
    {
        Assert.That(ProcessTree.IsAncestor(900, 400, Chain), Is.False, "a sibling branch");
        Assert.That(ProcessTree.IsAncestor(400, 100, Chain), Is.False, "a descendant isn't an ancestor");
        Assert.That(ProcessTree.IsAncestor(400, 400, Chain), Is.False, "a process isn't its own ancestor");
    }

    [Test]
    public void A_missing_parent_ends_the_walk_without_a_match()
    {
        // 300's parent (200) already exited: it isn't in the snapshot, so nothing above it can be reached.
        var parents = new Dictionary<int, int> { [400] = 300, [300] = 200, [100] = 4 };
        Assert.That(ProcessTree.IsAncestor(100, 400, parents), Is.False);
        Assert.That(ProcessTree.IsAncestor(300, 400, parents), Is.True, "what is still there is found");
        Assert.That(ProcessTree.IsAncestor(100, 12345, parents), Is.False, "a pid not in the snapshot at all");
        Assert.That(ProcessTree.IsAncestor(100, 400, new Dictionary<int, int>()), Is.False, "an empty snapshot");
    }

    [Test]
    public void A_parent_of_zero_ends_the_walk()
    {
        var parents = new Dictionary<int, int> { [400] = 0 };
        Assert.That(ProcessTree.IsAncestor(0, 400, parents), Is.False, "pid 0 means no parent, not the Idle process");
    }

    [Test]
    public void A_pid_cycle_ends_instead_of_looping_forever()
    {
        // Pids get reused, so a stale parent link can point back down the chain.
        var parents = new Dictionary<int, int> { [400] = 300, [300] = 200, [200] = 400 };
        var task = Task.Run(() => ProcessTree.IsAncestor(100, 400, parents));
        Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True, "the walk finished");
        Assert.That(task.Result, Is.False);
        Assert.That(ProcessTree.IsAncestor(200, 400, parents), Is.True, "an ancestor inside the cycle is still found");

        var self = new Dictionary<int, int> { [400] = 400 };
        Assert.That(ProcessTree.IsAncestor(100, 400, self), Is.False, "a process listed as its own parent");
    }

    [Test]
    public void The_live_snapshot_never_calls_a_process_its_own_ancestor()
    {
        Assert.That(ProcessTree.IsAncestor(Environment.ProcessId, Environment.ProcessId), Is.False);
        Assert.That(ProcessTree.IsAncestor(-1, Environment.ProcessId), Is.False);
    }

    // ---- BackgroundAgent.CommandLine ----

    [Test]
    public void The_command_line_quotes_prompt_and_log_paths_with_spaces()
    {
        var line = BackgroundAgent.CommandLine(@"C:\Users\Ryan D\App Data\A-T1.prompt.txt", @"C:\Users\Ryan D\App Data\A-T1.log", "claude --model x");
        Assert.That(line, Is.EqualTo("""claude --model x -p < "C:\Users\Ryan D\App Data\A-T1.prompt.txt" > "C:\Users\Ryan D\App Data\A-T1.log" 2>&1"""));
    }

    [Test]
    public void The_prompt_text_never_reaches_the_command_line()
    {
        // The prompt goes into a file and comes in on stdin, so its spaces, quotes and ampersands can't break cmd.
        var line = BackgroundAgent.CommandLine(@"C:\l\p.prompt.txt", @"C:\l\p.log", "claude");
        Assert.That(line, Does.StartWith("claude -p < "));
        Assert.That(line, Does.EndWith(" 2>&1"));
        Assert.That(line.Count(c => c == '"'), Is.EqualTo(4), "exactly the two quoted paths");
    }

    [Test]
    public void Without_a_command_the_line_uses_the_launchers_claude_command()
    {
        var line = BackgroundAgent.CommandLine(@"C:\l\p.prompt.txt", @"C:\l\p.log");
        Assert.That(line, Does.StartWith("claude").IgnoreCase);
        Assert.That(line, Does.Contain(" -p < \"C:\\l\\p.prompt.txt\""));
    }

    // ---- BackgroundAgent.ClaudeCommand ----

    private string Settings(string json)
    {
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Test]
    public void The_launchers_claude_run_command_is_used_trimmed()
    {
        var path = Settings("""{"agentProviders":[{"key":"Gemini","runCommand":"gemini -y"},{"key":"Claude","runCommand":"  claude --model claude-opus-5  "}]}""");
        Assert.That(BackgroundAgent.ClaudeCommand(path), Is.EqualTo("claude --model claude-opus-5"));
    }

    [Test]
    public void A_missing_settings_file_falls_back()
    {
        Assert.That(BackgroundAgent.ClaudeCommand(Path.Combine(dir, "nope.json")), Is.EqualTo(BackgroundAgent.FallbackCommand));
        Assert.That(BackgroundAgent.ClaudeCommand(Path.Combine(dir, "no-such-dir", "settings.json")), Is.EqualTo(BackgroundAgent.FallbackCommand));
    }

    [TestCase("")]
    [TestCase("{ not json")]
    [TestCase("""{"agentProviders":[{"key":"Claude","runCommand":"claude""")]
    public void Invalid_json_falls_back(string json)
    {
        Assert.That(BackgroundAgent.ClaudeCommand(Settings(json)), Is.EqualTo(BackgroundAgent.FallbackCommand));
    }

    [TestCase("""{"agentProviders":[{"key":"Claude","runCommand":"codex --yolo"}]}""", TestName = "a non-claude command")]
    [TestCase("""{"agentProviders":[{"key":"Claude","runCommand":"   "}]}""", TestName = "a blank command")]
    [TestCase("""{"agentProviders":[{"key":"Claude","runCommand":""}]}""", TestName = "an empty command")]
    [TestCase("""{"agentProviders":[{"key":"Claude","runCommand":42}]}""", TestName = "a command that isn't a string")]
    [TestCase("""{"agentProviders":[{"key":"Claude"}]}""", TestName = "no runCommand")]
    [TestCase("""{"agentProviders":[{"key":"Codex","runCommand":"claude --x"}]}""", TestName = "only another provider's key")]
    [TestCase("""{"agentProviders":{"key":"Claude","runCommand":"claude --x"}}""", TestName = "providers not an array")]
    [TestCase("""{"other":1}""", TestName = "no providers")]
    [TestCase("""[1,2,3]""", TestName = "a root that isn't an object")]
    public void Anything_but_a_claude_command_falls_back(string json)
    {
        Assert.That(BackgroundAgent.ClaudeCommand(Settings(json)), Is.EqualTo(BackgroundAgent.FallbackCommand));
    }

    [Test]
    public void The_claude_prefix_is_matched_case_insensitively()
    {
        var path = Settings("""{"agentProviders":[{"key":"Claude","runCommand":"Claude.exe --x"}]}""");
        Assert.That(BackgroundAgent.ClaudeCommand(path), Is.EqualTo("Claude.exe --x"));
    }

    // ---- BackgroundAgent.StartTimeOf ----

    [Test]
    public void StartTimeOf_is_null_for_a_dead_pid()
    {
        using var p = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c exit 0") { UseShellExecute = false, CreateNoWindow = true })!;
        var pid = p.Id;
        p.WaitForExit();
        p.Dispose(); // drop our handle so the pid is really gone

        Assert.That(BackgroundAgent.StartTimeOf(pid), Is.Null);
        Assert.That(BackgroundAgent.StartTimeOf(-1), Is.Null, "an invalid pid");
        Assert.That(BackgroundAgent.IsRunning(pid), Is.False);
    }

    [Test]
    public void StartTimeOf_a_live_process_is_its_utc_start()
    {
        using var me = Process.GetCurrentProcess();
        var start = BackgroundAgent.StartTimeOf(Environment.ProcessId);
        Assert.That(start, Is.Not.Null);
        Assert.That(start!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(BackgroundAgent.SameStart(start.Value, me.StartTime.ToUniversalTime()), Is.True);
    }

    // ---- Foreground ----

    [Test]
    public async Task Restoring_focus_to_no_window_returns_at_once()
    {
        var sw = Stopwatch.StartNew();
        await Foreground.RestoreAsync(IntPtr.Zero, TimeSpan.FromSeconds(30));
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "no wait when there's nothing to give focus back to");
    }
}
