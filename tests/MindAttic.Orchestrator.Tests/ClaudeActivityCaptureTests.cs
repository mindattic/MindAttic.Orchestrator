using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Claude's hook-side activity capture (M.ORCH-T961): the redacting target extractor shared by both hooks
/// (<see cref="AgentGate.ActivityPython"/>) turns one tool call into (tool, verb, short target) without ever passing on a raw
/// input, and the context gate and pace gate keep a Claude session's record in session-context/activity/&lt;session&gt;.json in
/// T964's format: working on a prompt or a tool call, idle at a turn's end, asking on a permission prompt; the pace gate only
/// over a record the context gate made Claude's; any error writes nothing and never fails the hook.
/// <para>Every token here is a fake built at run time, so no secret-shaped literal sits in the source.</para>
/// </summary>
[NonParallelizable] // spawns python
public sealed class ClaudeActivityCaptureTests
{
    private string dir = "";
    private string Claude => Path.Combine(dir, ".claude");
    private string Activity => Path.Combine(Claude, "session-context", AgentGate.ActivityDirName);
    private string Repo => Path.Combine(dir, "ws", "MindAttic.Orchestrator");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "activity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Repo, ".git"));
    }

    [TearDown]
    public void TearDown()
    {
        for (var attempt = 0; ; attempt++)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); return; }
            catch (IOException) when (attempt < 10) { Thread.Sleep(300); }
            catch (UnauthorizedAccessException) when (attempt < 10) { Thread.Sleep(300); }
        }
    }

    /// <summary>A fake GitHub token, built from a seed.</summary>
    private static string Token()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var random = new Random(23);
        return "ghp_7Qk" + new string(Enumerable.Range(0, 33).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
    }

    // ---- The extractor, run on its own ----

    /// <summary>Runs <see cref="AgentGate.ActivityPython"/>'s <c>activity_target</c> over (tool, input) pairs.</summary>
    private string[][] Extract(params (string Tool, object Input)[] calls)
    {
        Directory.CreateDirectory(dir);
        var script = Path.Combine(dir, "extract.py");
        File.WriteAllText(script, "import json, os, re, sys\nfrom pathlib import Path\n"
            + "def repo_of(path, worktrees=True):\n    return None\n"
            + "def cli_pid(names=(\"claude.exe\",)):\n    return None\n"
            + AgentGate.ActivityPython + "\n"
            + "cases = json.loads(sys.stdin.buffer.read().decode(\"utf-8\"))\n"
            + "sys.stdout.buffer.write(json.dumps([list(activity_target(t, a)) for t, a in cases]).encode(\"utf-8\"))\n");
        var psi = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(script);
        using var p = Process.Start(psi)!;
        p.StandardInput.BaseStream.Write(JsonSerializer.SerializeToUtf8Bytes(calls.Select(c => new object[] { c.Tool, c.Input })));
        p.StandardInput.Close();
        var output = new StreamReader(p.StandardOutput.BaseStream, System.Text.Encoding.UTF8).ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return JsonSerializer.Deserialize<string[][]>(output)!;
    }

    private string[] One(string tool, object input) => Extract((tool, input))[0];

    [TestCase("Read", "Reading")]
    [TestCase("Write", "Writing")]
    [TestCase("Edit", "Editing")]
    [TestCase("MultiEdit", "Editing")]
    public void A_file_tool_shows_the_file_base_name_never_its_path(string tool, string verb) =>
        Assert.That(One(tool, new { file_path = @"C:\Users\someone\private\AgentGate.cs", old_string = "x", content = "y" }),
            Is.EqualTo(new[] { tool, verb, "AgentGate.cs" }));

    [Test]
    public void A_notebook_edit_shows_the_notebook_base_name() =>
        Assert.That(One("NotebookEdit", new { notebook_path = "/home/someone/work/model.ipynb", new_source = "x" }),
            Is.EqualTo(new[] { "NotebookEdit", "Editing", "model.ipynb" }));

    [TestCase("Bash")]
    [TestCase("PowerShell")]
    public void A_shell_call_without_a_description_shows_its_program_only(string tool)
    {
        var token = Token();
        var cases = new (string Command, string Program)[]
        {
            ($"curl -H \"Authorization: Bearer {token}\" https://api.example.com/x", "curl"),
            ($"C:\\Tools\\psql.exe postgres://user:hunter2@db/x {token}", "psql.exe"),
            ($"gh pr list --token {token}", "gh"),
        };
        foreach (var (command, program) in cases)
        {
            var got = One(tool, new { command });
            Assert.That(got[0], Is.EqualTo(tool), command);
            Assert.That(got[1], Is.EqualTo("Running"), command);
            Assert.That(got[2], Does.Not.Contain(token).And.Not.Contain("hunter2").And.Not.Contain("Authorization"), command);
            Assert.That(got[2], Is.EqualTo(program), command);
        }
    }

    [Test]
    public void A_shell_call_with_a_description_shows_it_masked_and_never_the_command()
    {
        var token = Token();
        var got = One("Bash", new { command = $"curl --api-key {token} https://x", description = $"Call the API with {token}\nsecond line" });
        Assert.That(got, Is.EqualTo(new[] { "Bash", "", "Call the API with " + SecretShapes.ActivityMask }));
    }

    [Test]
    public void A_web_fetch_shows_its_host_only()
    {
        var token = Token();
        Assert.That(One("WebFetch", new { url = $"https://user:hunter2@api.example.com:8443/x?access_token={token}", prompt = "read it" }),
            Is.EqualTo(new[] { "WebFetch", "Reading", "api.example.com" }));
    }

    [Test]
    public void A_search_shows_its_pattern_or_query_masked()
    {
        var token = Token();
        var got = Extract(("Grep", new { pattern = token, path = "C:/x" }), ("Glob", new { pattern = "**/*.cs" }),
                          ("WebSearch", new { query = $"why does {token} fail" }));
        Assert.That(got[0], Is.EqualTo(new[] { "Grep", "Searching for", SecretShapes.ActivityMask }));
        Assert.That(got[1], Is.EqualTo(new[] { "Glob", "Finding", "**/*.cs" }));
        Assert.That(got[2][2], Does.Not.Contain(token).And.StartWith("why does "));
    }

    [Test]
    public void An_mcp_tool_shows_its_own_name_only_and_never_its_input()
    {
        var token = Token();
        Assert.That(One("mcp__claude_ai_Docs__batch", new { secret = token }), Is.EqualTo(new[] { "mcp", "Using", "batch" }));
    }

    [Test]
    public void A_subagent_shows_its_description_never_its_prompt() =>
        Assert.That(One("Agent", new { description = "Fix the pace gate", prompt = "a long private brief", subagent_type = "general-purpose" }),
            Is.EqualTo(new[] { "Agent", "Starting a subagent:", "Fix the pace gate" }));

    [Test]
    public void A_skill_shows_its_name_never_its_arguments() =>
        Assert.That(One("Skill", new { skill = "commit", args = "private words" }), Is.EqualTo(new[] { "Skill", "Using", "/commit" }));

    [Test]
    public void Another_tool_shows_its_name_and_no_target() =>
        Assert.That(One("TodoWrite", new { todos = new[] { new { content = "private plan" } } }), Is.EqualTo(new[] { "TodoWrite", "Using", "" }));

    [Test]
    public void A_target_is_cut_to_80_characters()
    {
        var got = One("Bash", new { command = "x", description = string.Concat(Enumerable.Repeat("word ", 40)) });
        Assert.That(got[2].Length, Is.LessThanOrEqualTo(80));
        Assert.That(got[2], Does.EndWith("…"));
    }

    [Test]
    public void A_missing_or_odd_input_falls_back_without_failing()
    {
        var got = Extract(("Read", new { }), ("Bash", new { command = "   " }), ("WebFetch", new { url = "not a url" }));
        Assert.That(got[0], Is.EqualTo(new[] { "Read", "Reading", "" }));
        Assert.That(got[1], Is.EqualTo(new[] { "Bash", "Running", "" }));
        Assert.That(got[2], Is.EqualTo(new[] { "WebFetch", "Reading", "" }));
    }

    [Test]
    public void Both_hooks_carry_the_one_extractor()
    {
        var context = AgentGate.ContextScript(AgentGate.DefaultContextPercent);
        Assert.That(context, Does.Contain(AgentGate.ActivityPython));
        Assert.That(AgentGate.Script, Does.Contain(AgentGate.ActivityPython));
        Assert.That(AgentGate.Script, Does.Contain("activity_record(\"claude\", data, \"PreToolUse\", require=\"claude\")"));
    }

    // ---- The hooks, end to end ----

    private void Hook(string script, object input, params string[] args)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(Path.Combine(Claude, script));
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["USERPROFILE"] = dir;
        psi.Environment["APPDATA"] = dir;
        psi.Environment["LOCALAPPDATA"] = dir;
        psi.Environment["MINDATTIC_CLI_PID"] = "4242";
        psi.Environment["MINDATTIC_WORKSPACE"] = Path.Combine(dir, "ws");
        psi.Environment["MINDATTIC_ORCHESTRATOR"] = Path.Combine(dir, "no-orchestrator.exe");
        psi.Environment["MINDATTIC_FAKE_FREE_RAM_GB"] = "64";
        psi.Environment.Remove("MINDATTIC_TICKETS_DB");
        psi.Environment.Remove("MINDATTIC_WORKFLOW_DB");
        psi.Environment.Remove("CLAUDE_CONFIG_DIR");
        using var p = Process.Start(psi)!;
        p.StandardInput.BaseStream.Write(JsonSerializer.SerializeToUtf8Bytes(input));
        p.StandardInput.Close();
        _ = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        Assert.That(p.ExitCode, Is.Zero, script);
    }

    private JsonObject? Record(string session = "sess-1") =>
        File.Exists(Path.Combine(Activity, session + ".json")) ? JsonNode.Parse(File.ReadAllText(Path.Combine(Activity, session + ".json"))) as JsonObject : null;

    private object Payload(string hookEvent, object? extra = null)
    {
        var node = new JsonObject { ["session_id"] = "sess-1", ["cwd"] = Repo, ["hook_event_name"] = hookEvent };
        if (extra is not null)
            foreach (var (k, v) in JsonSerializer.SerializeToNode(extra)!.AsObject())
                node[k] = v?.DeepClone();
        return node;
    }

    [Test]
    public void A_claude_session_is_recorded_through_its_turn()
    {
        AgentGate.Ensure(Claude);
        var token = Token();

        Hook(AgentGate.ContextScriptName, Payload("UserPromptSubmit", new { prompt = "Work on ticket M.ORCH-T961 now" }));
        var rec = Record()!;
        Assert.That((string?)rec["provider"], Is.EqualTo("claude"));
        Assert.That((string?)rec["state"], Is.EqualTo("working"));
        Assert.That((string?)rec["ticket"], Is.EqualTo("M.ORCH-T961"));
        Assert.That((string?)rec["repo"], Is.EqualTo("MindAttic.Orchestrator"));
        Assert.That((int?)rec["pid"], Is.EqualTo(4242));
        Assert.That(rec.ToJsonString(), Does.Not.Contain("Work on ticket"), "never the prompt");

        Hook(AgentGate.ScriptName, Payload("PreToolUse", new { tool_name = "Bash", tool_input = new { command = $"curl -H \"Authorization: Bearer {token}\" https://x" } }));
        rec = Record()!;
        Assert.That((string?)rec["tool"], Is.EqualTo("Bash"));
        Assert.That((string?)rec["target"], Is.EqualTo("curl"));
        Assert.That(rec.ToJsonString(), Does.Not.Contain(token));

        Hook(AgentGate.ContextScriptName, Payload("PostToolUse", new { tool_name = "Edit", tool_input = new { file_path = @"D:\x\AgentGate.cs" }, tool_response = new { } }));
        Assert.That((string?)Record()!["target"], Is.EqualTo("AgentGate.cs"));

        Hook(AgentGate.ContextScriptName, Payload("Notification", new { notification_type = "permission_prompt", message = "Claude needs your permission to use Bash" }));
        rec = Record()!;
        Assert.That((string?)rec["state"], Is.EqualTo("asking"));
        Assert.That(rec.ContainsKey("tool"), Is.False);

        Hook(AgentGate.ContextScriptName, Payload("Stop"));
        rec = Record()!;
        Assert.That((string?)rec["state"], Is.EqualTo("idle"));
        Assert.That((string?)rec["ticket"], Is.EqualTo("M.ORCH-T961"), "the ticket stays with the session");
        Assert.That((int?)rec["seq"], Is.EqualTo(5));
    }

    [Test]
    public void The_pace_gate_writes_only_over_a_record_the_context_gate_made_claudes()
    {
        AgentGate.Ensure(Claude);
        // No record yet: it can't tell a Claude session from Codex's, Gemini's or Kimi's, which run it unnamed.
        Hook(AgentGate.ScriptName, Payload("PreToolUse", new { tool_name = "Read", tool_input = new { file_path = "a.cs" } }));
        Assert.That(Record(), Is.Null);

        // A Codex session's record (the context gate named it) is left to the context gate.
        Hook(AgentGate.ContextScriptName, new { session_id = "codex-1", cwd = Repo, hook_event_name = "PostToolUse", tool_name = "shell",
            tool_input = new { command = new[] { "bash", "-lc", "git status" } } }, "codex");
        var before = Record("codex-1")!.ToJsonString();
        Hook(AgentGate.ScriptName, new { session_id = "codex-1", cwd = Repo, hook_event_name = "PreToolUse", tool_name = "shell", tool_input = new { command = new[] { "ls" } } });
        Assert.That(Record("codex-1")!.ToJsonString(), Is.EqualTo(before));
        Assert.That((string?)Record("codex-1")!["provider"], Is.EqualTo("codex"));
    }

    [Test]
    public void A_compaction_is_not_a_turn_ending()
    {
        AgentGate.Ensure(Claude);
        Hook(AgentGate.ContextScriptName, Payload("UserPromptSubmit", new { prompt = "go" }));
        Hook(AgentGate.ContextScriptName, Payload("SessionStart", new { source = "compact" }));
        Assert.That((string?)Record()!["state"], Is.EqualTo("working"));
        Hook(AgentGate.ContextScriptName, Payload("SessionStart", new { source = "startup" }));
        Assert.That((string?)Record()!["state"], Is.EqualTo("idle"));
    }

    [Test]
    public void Any_error_writes_nothing_and_never_fails_either_hook()
    {
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Path.GetDirectoryName(Activity)!);
        File.WriteAllText(Activity, "not a folder");
        Hook(AgentGate.ContextScriptName, Payload("UserPromptSubmit", new { prompt = "go" }));
        Hook(AgentGate.ScriptName, Payload("PreToolUse", new { tool_name = "Read", tool_input = new { file_path = "a.cs" } }));
        Assert.That(File.ReadAllText(Activity), Is.EqualTo("not a folder"));
    }
}
