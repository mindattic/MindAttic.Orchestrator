using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Uninstall strips the gate hooks the installer registered, and only those (M.ORCH-T439); temp folders stand in for
/// ~/.claude, ~/.codex, ~/.gemini and Kimi's config.toml.</summary>
public sealed class AgentGateRemovalTests
{
    private string root = "";
    private string Claude => Path.Combine(root, ".claude");
    private string Codex => Path.Combine(root, ".codex");
    private string Gemini => Path.Combine(root, ".gemini");
    private string Kimi => Path.Combine(root, ".kimi-code", "config.toml");
    private string Settings => Path.Combine(Claude, "settings.json");

    [SetUp]
    public void Make() => root = Path.Combine(Path.GetTempPath(), "orch-ungate-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void Clean()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(path))!;

    private string? Remove() => AgentGateRemoval.Remove(Claude, Codex, Gemini, Kimi);

    private static string UserHook(string command) =>
        $$"""{ "matcher": "Bash", "hooks": [ { "type": "command", "command": "{{command}}" } ] }""";

    [Test]
    public void Uninstall_strips_the_installers_hooks_and_keeps_the_users_own_and_every_other_setting()
    {
        Directory.CreateDirectory(Claude);
        File.WriteAllText(Settings, $$"""
            {
              "model": "opus",
              "statusLine": { "type": "command", "command": "python my-statusline.py" },
              "env": { "FOO": "bar" },
              "hooks": {
                "PreToolUse": [ {{UserHook("python my-guard.py")}} ],
                "Notification": [ {{UserHook("python notify.py")}} ]
              }
            }
            """);
        Assert.That(AgentGate.Ensure(Claude), Does.Contain("registered"));
        Assert.That(AgentGate.EnsureOtherProviders(Claude, Codex, Gemini, Kimi), Does.Contain("Codex").And.Contain("Gemini").And.Contain("Kimi"));
        Assert.That(File.ReadAllText(Settings), Does.Contain(AgentGate.ScriptName).And.Contain(AgentGate.ContextScriptName), "precondition: installed");

        var did = Remove();

        Assert.That(did, Does.Contain("Claude").And.Contain("Codex").And.Contain("Gemini").And.Contain("Kimi"));
        var text = File.ReadAllText(Settings);
        Assert.That(text, Does.Not.Contain(AgentGate.ScriptName).And.Not.Contain(AgentGate.ContextScriptName));
        var s = Json(Settings);
        Assert.That((string?)s["model"], Is.EqualTo("opus"));
        Assert.That((string?)s["env"]!["FOO"], Is.EqualTo("bar"));
        Assert.That((string?)s["statusLine"]!["command"], Is.EqualTo("python my-statusline.py"), "the statusline is the user's: kept");
        var pre = s["hooks"]!["PreToolUse"]!.AsArray();
        Assert.That(pre, Has.Count.EqualTo(1), "the user's own PreToolUse hook stays");
        Assert.That((string?)pre[0]!["hooks"]![0]!["command"], Is.EqualTo("python my-guard.py"));
        Assert.That((string?)s["hooks"]!["Notification"]![0]!["hooks"]![0]!["command"], Is.EqualTo("python notify.py"));
        foreach (var only in new[] { "PostToolUse", "Stop", "UserPromptSubmit", "SubagentStart", "SubagentStop", "SessionStart" })
            Assert.That(s["hooks"]![only], Is.Null, $"{only} held only the context gate, so it goes");

        Assert.That(File.ReadAllText(Path.Combine(Codex, "hooks.json")), Does.Not.Contain(".py"));
        Assert.That(File.ReadAllText(Path.Combine(Gemini, "settings.json")), Does.Not.Contain(".py"));
        Assert.That(File.Exists(Kimi) ? File.ReadAllText(Kimi) : "", Does.Not.Contain(AgentGate.ScriptName));

        Assert.That(File.Exists(Path.Combine(Claude, AgentGate.ScriptName)), Is.False, "the scripts go with their hooks");
        Assert.That(File.Exists(Path.Combine(Claude, AgentGate.ContextScriptName)), Is.False);
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "allocate.md")), Is.False);
        Assert.That(Remove(), Is.Null, "nothing of the orchestrator's left: a second uninstall does nothing");
    }

    [Test]
    public void A_hook_group_shared_with_a_user_hook_keeps_the_users_hook()
    {
        Directory.CreateDirectory(Claude);
        var gate = AgentGate.Command(Path.Combine(Claude, AgentGate.ScriptName));
        File.WriteAllText(Settings, $$"""
            { "hooks": { "PreToolUse": [ { "matcher": "", "hooks": [
                { "type": "command", "command": "{{gate.Replace("\"", "\\\"")}}", "timeout": 330 },
                { "type": "command", "command": "python mine.py" } ] } ] } }
            """);

        Assert.That(Remove(), Does.Contain("Claude"));

        var inner = Json(Settings)["hooks"]!["PreToolUse"]![0]!["hooks"]!.AsArray();
        Assert.That(inner, Has.Count.EqualTo(1));
        Assert.That((string?)inner[0]!["command"], Is.EqualTo("python mine.py"));
    }

    [Test]
    public void Settings_with_no_gate_hooks_or_unreadable_ones_are_not_rewritten()
    {
        Directory.CreateDirectory(Claude);
        const string mine = "{\n  \"hooks\": { \"PreToolUse\": [] },\n  \"model\": \"opus\"\n}";
        File.WriteAllText(Settings, mine);
        Directory.CreateDirectory(Gemini);
        const string broken = "{ \"hooks\": { oops pace-gate.py";
        File.WriteAllText(Path.Combine(Gemini, "settings.json"), broken);

        Assert.That(Remove(), Is.Null);
        Assert.That(File.ReadAllText(Settings), Is.EqualTo(mine), "not ours to touch: byte for byte");
        Assert.That(File.ReadAllText(Path.Combine(Gemini, "settings.json")), Is.EqualTo(broken), "someone else's unreadable file is left alone");
    }

    [Test]
    public void Kimi_loses_only_the_gate_table_and_keeps_its_other_config()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Kimi)!);
        const string before = "model = \"k2\"\n\n[[hooks]]\nevent = \"PostToolUse\"\ncommand = 'python mine.py'\n";
        File.WriteAllText(Kimi, before);
        Directory.CreateDirectory(Claude);
        File.WriteAllText(Path.Combine(Claude, AgentGate.ScriptName), "# stand-in");
        Assert.That(AgentGate.EnsureOtherProviders(Claude, Codex, Gemini, Kimi), Does.Contain("Kimi"));
        File.AppendAllText(Kimi, "\n[ui]\ntheme = \"dark\"\n");

        Assert.That(AgentGateRemoval.RemoveKimiHooks(Kimi), Is.True);

        Assert.That(File.ReadAllText(Kimi), Is.EqualTo(before + "\n[ui]\ntheme = \"dark\"\n"));
        Assert.That(AgentGateRemoval.RemoveKimiHooks(Kimi), Is.False, "nothing left to remove");
    }

    [Test]
    public void Kimi_keeps_the_comment_above_the_table_that_follows_the_gate()
    {
        // M.ORCH-T503: the gate's table was taken to run up to the next '[', so a comment above that table went with it.
        Directory.CreateDirectory(Path.GetDirectoryName(Kimi)!);
        const string before = "model = \"k2\"\n";
        File.WriteAllText(Kimi, before);
        Directory.CreateDirectory(Claude);
        File.WriteAllText(Path.Combine(Claude, AgentGate.ScriptName), "# stand-in");
        Assert.That(AgentGate.EnsureOtherProviders(Claude, Codex, Gemini, Kimi), Does.Contain("Kimi"));
        File.AppendAllText(Kimi, "\n# my UI settings\n[ui]\ntheme = \"dark\"\n");

        Assert.That(AgentGateRemoval.RemoveKimiHooks(Kimi), Is.True);

        Assert.That(File.ReadAllText(Kimi), Is.EqualTo(before + "\n# my UI settings\n[ui]\ntheme = \"dark\"\n"));
    }

    [Test]
    public void Uninstall_keeps_a_commented_gemini_settings_json_as_the_user_wrote_it()
    {
        // M.ORCH-T442: the removal parsed with comments skipped and wrote the result back, so every // comment went.
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Gemini);
        var gemini = Path.Combine(Gemini, "settings.json");
        const string original = "{\n    // my theme\n    \"theme\": \"Dracula\", /* dark */\n    \"ui\": { \"hideBanner\": true } // keep the banner off\n}\n";
        File.WriteAllText(gemini, original);
        Assert.That(AgentGate.EnsureOtherProviders(Claude, Codex, Gemini, Kimi), Does.Contain("Gemini"));
        Assert.That(File.ReadAllText(gemini), Does.Contain(AgentGate.ScriptName), "precondition: installed");

        Assert.That(Remove(), Does.Contain("Gemini: removed the gate hooks from settings.json, keeping its comments"));
        Assert.That(File.ReadAllText(gemini), Is.EqualTo(original), "install then uninstall gives back the user's file byte for byte");
    }

    [Test]
    public void Uninstall_cuts_only_the_gate_out_of_a_commented_gemini_hook_list()
    {
        Directory.CreateDirectory(Gemini);
        var gemini = Path.Combine(Gemini, "settings.json");
        File.WriteAllText(gemini, """
            {
              "hooks": {
                // mine runs first
                "BeforeTool": [
                  { "matcher": "", "hooks": [ { "type": "command", "command": "gemini-own.sh" } ] }, // my own
                  { "matcher": "", "hooks": [ { "type": "command", "command": "python \"C:/x/pace-gate.py\"", "timeout": 330000 } ] }
                ],
                "AfterAgent": [
                  { "matcher": "", "hooks": [ { "type": "command", "command": "python \"C:/x/context-gate.py\" gemini" } ] }
                ]
              } /* end of hooks */
            }
            """);

        Assert.That(Remove(), Does.Contain("Gemini: removed the gate hooks from settings.json, keeping its comments"));
        var text = File.ReadAllText(gemini);
        Assert.That(text, Does.Contain("// mine runs first").And.Contain("// my own").And.Contain("/* end of hooks */"));
        Assert.That(text, Does.Not.Contain(".py"));
        var hooks = JsonNode.Parse(text, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!["hooks"]!;
        Assert.That(hooks["BeforeTool"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That((string?)hooks["BeforeTool"]![0]!["hooks"]![0]!["command"], Is.EqualTo("gemini-own.sh"));
        Assert.That(hooks["AfterAgent"], Is.Null, "an event left empty goes");
        Assert.That(Remove(), Is.Null, "nothing left to remove");
    }

    [Test]
    public void The_scripts_stay_while_a_settings_file_left_alone_still_runs_them()
    {
        // M.ORCH-T451: python exits 2 on a missing script, which Claude and Gemini take as a blocking hook: deleting the scripts
        // under a hook uninstall couldn't take out refused every call in that CLI.
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Gemini);
        var gemini = Path.Combine(Gemini, "settings.json");
        var unreadable = "{ \"theme\": \"a\", \"theme\": \"b\", \"hooks\": { \"BeforeTool\": [ { \"matcher\": \"\", \"hooks\": [ { \"type\": \"command\", " +
                         "\"command\": \"python \\\"C:/x/pace-gate.py\\\"\" } ] } ] } }";
        File.WriteAllText(gemini, unreadable);

        var did = Remove();

        Assert.That(File.ReadAllText(Settings), Does.Not.Contain(AgentGate.ScriptName), "Claude's hooks still come out");
        Assert.That(File.ReadAllText(gemini), Is.EqualTo(unreadable));
        Assert.That(File.Exists(Path.Combine(Claude, AgentGate.ScriptName)) && File.Exists(Path.Combine(Claude, AgentGate.ContextScriptName)), Is.True);
        Assert.That(did, Does.Contain("kept pace-gate.py and context-gate.py: Gemini still runs them"));

        File.WriteAllText(gemini, "{}");
        Assert.That(Remove(), Does.Contain("deleted " + AgentGate.ScriptName), "once that hook is out, they go");
        Assert.That(File.Exists(Path.Combine(Claude, AgentGate.ScriptName)), Is.False);
    }

    [Test]
    public void A_command_file_the_user_rewrote_is_kept()
    {
        AgentGate.Ensure(Claude);
        var allocate = Path.Combine(Claude, "commands", "allocate.md");
        File.WriteAllText(allocate, "my own allocate");

        Remove();

        Assert.That(File.ReadAllText(allocate), Is.EqualTo("my own allocate"));
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "throttle.md")), Is.False);
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "pacing.md")), Is.False);
    }

    [Test]
    public void The_retired_throttle_command_files_go_when_still_ours_and_stay_when_rewritten()
    {
        // M.ORCH-T483: /pacing replaced /unthrottle and /throttle; an install that wrote them loses them on the next Ensure.
        var commands = Path.Combine(Claude, "commands");
        Directory.CreateDirectory(commands);
        var (unthrottle, ours) = AgentGate.RetiredCommands[0];
        File.WriteAllText(Path.Combine(commands, unthrottle), ours);
        File.WriteAllText(Path.Combine(commands, "throttle.md"), "my own throttle");

        Assert.That(AgentGate.Ensure(Claude), Does.Contain("deleted commands/unthrottle.md"));

        Assert.That(File.Exists(Path.Combine(commands, unthrottle)), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(commands, "throttle.md")), Is.EqualTo("my own throttle"));
        Assert.That(File.Exists(Path.Combine(commands, "pacing.md")), Is.True);
    }
}
