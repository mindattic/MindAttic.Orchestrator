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
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "autopilot.md")), Is.False);
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
    public void A_command_file_the_user_rewrote_is_kept()
    {
        AgentGate.Ensure(Claude);
        var autopilot = Path.Combine(Claude, "commands", "autopilot.md");
        File.WriteAllText(autopilot, "my own autopilot");

        Remove();

        Assert.That(File.ReadAllText(autopilot), Is.EqualTo("my own autopilot"));
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "throttle.md")), Is.False);
    }
}
