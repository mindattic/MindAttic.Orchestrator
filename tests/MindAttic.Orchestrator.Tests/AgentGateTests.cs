using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>AgentGate.Ensure and EnsureOtherProviders against temp folders standing in for ~/.claude, ~/.codex,
/// ~/.gemini and Kimi's config.toml (M.ORCH-T141).</summary>
public sealed class AgentGateTests
{
    private string root = "";
    private string Claude => Path.Combine(root, ".claude");
    private string Codex => Path.Combine(root, ".codex");
    private string Gemini => Path.Combine(root, ".gemini");
    private string Kimi => Path.Combine(root, ".kimi-code", "config.toml");
    private string Settings => Path.Combine(Claude, "settings.json");

    [SetUp]
    public void Make() => root = Path.Combine(Path.GetTempPath(), "orch-gate-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void Clean()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static JsonNode Json(string path) => JsonNode.Parse(File.ReadAllText(path))!;

    private string? Others() => AgentGate.EnsureOtherProviders(Claude, Codex, Gemini, Kimi);

    [Test]
    public void A_changed_script_body_is_rewritten_and_an_unchanged_one_is_not()
    {
        Assert.That(AgentGate.Ensure(Claude), Does.Contain("wrote pace-gate.py").And.Contain("wrote context-gate.py")
            .And.Contain("registered the pace and context gate hooks"));
        var pace = Path.Combine(Claude, AgentGate.ScriptName);
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(pace, stamp);

        Assert.That(AgentGate.Ensure(Claude), Is.Null, "everything in place: nothing done");
        Assert.That(File.GetLastWriteTimeUtc(pace), Is.EqualTo(stamp), "an unchanged script isn't even rewritten");

        File.WriteAllText(pace, "# someone edited this\n");
        Assert.That(AgentGate.Ensure(Claude), Is.EqualTo("wrote pace-gate.py"), "only the edited script is put back");
        Assert.That(File.ReadAllText(pace), Is.EqualTo(AgentGate.Script));

        File.Delete(Path.Combine(Claude, "commands", "autopilot.md"));
        Assert.That(AgentGate.Ensure(Claude), Is.EqualTo("wrote autopilot.md"), "a missing command is written back");
        // Since M.ORCH-T164 nothing works tickets in background agents: /autopilot mustn't tell the CLI something carries on.
        Assert.That(AgentGate.AutopilotCommand, Does.Not.Contain("background agents of their own").And.Contain("this CLI's own subagents or nobody"));
    }

    [Test]
    public void An_unreadable_settings_json_is_left_alone()
    {
        Directory.CreateDirectory(Claude);
        const string broken = "{ \"hooks\": { oops, this isn't json";
        File.WriteAllText(Settings, broken);

        var ex = Assert.Throws<IOException>(() => AgentGate.Ensure(Claude), "not reported as done, so the daemon retries")!;

        Assert.That(File.ReadAllText(Settings), Is.EqualTo(broken), "someone else's file isn't touched");
        Assert.That(ex.Message, Does.Contain("aren't registered").And.Contain("wrote pace-gate.py"), "the scripts are still written");
        Assert.That(File.Exists(Path.Combine(Claude, "pace-gate.py")), Is.True);
        Assert.That(File.Exists(Settings + ".tmp"), Is.False);
        Assert.Throws<IOException>(() => AgentGate.Ensure(Claude), "and it's said again next time, never claimed done");

        File.WriteAllText(Settings, "[1, 2]");
        Assert.Throws<IOException>(() => AgentGate.Ensure(Claude), "JSON that isn't an object is left alone too");
        Assert.That(File.ReadAllText(Settings), Is.EqualTo("[1, 2]"));

        File.WriteAllText(Settings, "{}");
        Assert.That(AgentGate.Ensure(Claude), Does.Contain("registered"), "once it's readable the hooks go in");
    }

    [Test]
    public void A_users_own_statusLine_refreshInterval_is_kept_and_a_missing_one_is_added()
    {
        Directory.CreateDirectory(Claude);
        File.WriteAllText(Settings, """{ "statusLine": { "type": "command", "command": "mine.py", "refreshInterval": 5 }, "theme": "dark" }""");
        AgentGate.Ensure(Claude);
        var s = Json(Settings);
        Assert.That((int)s["statusLine"]!["refreshInterval"]!, Is.EqualTo(5));
        Assert.That((string)s["statusLine"]!["command"]!, Is.EqualTo("mine.py"));
        Assert.That((string)s["theme"]!, Is.EqualTo("dark"), "the rest of the user's settings survive");

        File.WriteAllText(Settings, """{ "statusLine": { "type": "command", "command": "mine.py" } }""");
        Assert.That(AgentGate.Ensure(Claude), Does.Contain("registered"));
        Assert.That((int)Json(Settings)["statusLine"]!["refreshInterval"]!, Is.EqualTo(AgentGate.StatusLineRefreshSeconds));

        File.WriteAllText(Settings, "{}");
        AgentGate.Ensure(Claude);
        Assert.That(Json(Settings)["statusLine"], Is.Null, "no statusline of yours, none invented");
    }

    [Test]
    public void The_hooks_are_merged_once_beside_the_users_own()
    {
        Directory.CreateDirectory(Claude);
        File.WriteAllText(Settings, """
            { "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "mine.sh" } ] } ] } }
            """);
        AgentGate.Ensure(Claude);
        AgentGate.Ensure(Claude);
        var pre = Json(Settings)["hooks"]!["PreToolUse"]!.AsArray();
        Assert.That(pre.Count, Is.EqualTo(2), "yours and the pace gate, once");
        Assert.That(pre.ToJsonString(), Does.Contain("mine.sh").And.Contain(AgentGate.ScriptName));
        Assert.That(pre.ToJsonString(), Does.Contain($"\"timeout\":{AgentGate.HookTimeoutSeconds}"));
    }

    [Test]
    public void The_context_percent_and_keep_going_flag_reach_the_written_script()
    {
        AgentGate.Ensure(Claude, contextPercent: 72, keepGoing: false);
        var context = File.ReadAllText(Path.Combine(Claude, AgentGate.ContextScriptName));
        Assert.That(context, Does.Contain("\nPERCENT = 72\n").And.Contain("\nKEEP_GOING = False\n"));

        Assert.That(AgentGate.Ensure(Claude, contextPercent: 72, keepGoing: true), Is.EqualTo("wrote context-gate.py"), "turning keep-going on rewrites it");
        context = File.ReadAllText(Path.Combine(Claude, AgentGate.ContextScriptName));
        Assert.That(context, Does.Contain("\nPERCENT = 72\n").And.Contain("\nKEEP_GOING = True\n"));
        Assert.That(context, Does.Not.Contain("\r\n"), "written with plain newlines");
    }

    [Test]
    public void Other_providers_wait_for_the_claude_scripts()
    {
        Assert.That(Others(), Is.Null);
        Assert.That(Directory.Exists(Codex) || Directory.Exists(Gemini) || File.Exists(Kimi), Is.False, "nothing written before the pace gate exists");
    }

    [Test]
    public void Kimis_config_toml_append_is_idempotent_and_keeps_whats_there()
    {
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Path.GetDirectoryName(Kimi)!);
        File.WriteAllText(Kimi, "model = \"k2\"\r\n\r\n");

        Assert.That(Others(), Does.Contain("Kimi: wired PreToolUse into config.toml"));
        var once = File.ReadAllText(Kimi);
        var command = AgentGate.Command(Path.Combine(Claude, AgentGate.ScriptName));
        Assert.That(once, Does.StartWith("model = \"k2\"\n\n[[hooks]]\nevent = \"PreToolUse\"\n"));
        Assert.That(once, Does.Contain($"command = '{command}'\ntimeout = {AgentGate.HookTimeoutSeconds}\n"));

        Assert.That(Others(), Is.Null.Or.Not.Contain("Kimi"), "already wired");
        Assert.That(File.ReadAllText(Kimi), Is.EqualTo(once), "no second block");
    }

    [Test]
    public void A_profile_path_with_an_apostrophe_is_written_to_kimis_config_as_a_valid_toml_string_and_a_broken_entry_mended()
    {
        // M.ORCH-T326: 'python "C:/Users/O'Brien/..."' ended the literal string at the apostrophe.
        root = Path.Combine(Path.GetTempPath(), "orch-gate-O'Brien-" + Guid.NewGuid().ToString("N"));
        AgentGate.Ensure(Claude);
        var command = AgentGate.Command(Path.Combine(Claude, AgentGate.ScriptName));
        var valid = "command = \"" + command.Replace("\"", "\\\"") + "\"\n";

        Assert.That(Others(), Does.Contain("Kimi: wired PreToolUse into config.toml"));
        Assert.That(File.ReadAllText(Kimi), Does.Contain(valid));
        var once = File.ReadAllText(Kimi);
        Others();
        Assert.That(File.ReadAllText(Kimi), Is.EqualTo(once), "already wired: no second block");

        File.WriteAllText(Kimi, $"model = \"k2\"\n\n[[hooks]]\nevent = \"PreToolUse\"\ncommand = '{command}'\ntimeout = 5\n");
        Assert.That(Others(), Does.Contain("Kimi"));
        Assert.That(File.ReadAllText(Kimi), Is.EqualTo($"model = \"k2\"\n\n[[hooks]]\nevent = \"PreToolUse\"\n{valid}timeout = 5\n"), "the broken entry is mended in place");
    }

    [Test]
    public void Existing_codex_and_gemini_hooks_survive()
    {
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Codex);
        Directory.CreateDirectory(Gemini);
        File.WriteAllText(Path.Combine(Codex, "hooks.json"), """
            { "hooks": { "PreToolUse": [ { "matcher": "", "hooks": [ { "type": "command", "command": "codex-own.sh", "timeout": 3 } ] } ] } }
            """);
        File.WriteAllText(Path.Combine(Gemini, "settings.json"), """
            { "theme": "GitHub", "hooks": { "BeforeTool": [ { "matcher": "", "hooks": [ { "type": "command", "command": "gemini-own.sh" } ] } ] } }
            """);

        var did = Others();
        Assert.That(did, Does.Contain("Codex: wired PreToolUse into hooks.json").And.Contain("Codex: wired Stop into hooks.json")
            .And.Contain("Gemini: wired BeforeTool into settings.json").And.Contain("Gemini: wired AfterAgent into settings.json"));

        var codex = Json(Path.Combine(Codex, "hooks.json"))["hooks"]!;
        Assert.That(codex["PreToolUse"]!.AsArray().Count, Is.EqualTo(2));
        Assert.That(codex["PreToolUse"]!.ToJsonString(), Does.Contain("codex-own.sh").And.Contain(AgentGate.ScriptName));
        Assert.That((string)codex["Stop"]![0]!["hooks"]![0]!["command"]!,
            Is.EqualTo(AgentGate.Command(Path.Combine(Claude, AgentGate.ContextScriptName)) + " codex"), "the context gate is told it runs under Codex");

        var gemini = Json(Path.Combine(Gemini, "settings.json"));
        Assert.That((string)gemini["theme"]!, Is.EqualTo("GitHub"));
        Assert.That(gemini["hooks"]!["BeforeTool"]!.AsArray().Count, Is.EqualTo(2));
        Assert.That(gemini["hooks"]!["BeforeTool"]!.ToJsonString(), Does.Contain("gemini-own.sh").And.Contain($"\"timeout\":{AgentGate.HookTimeoutSeconds * 1000}"),
            "Gemini's timeout is in milliseconds");
        Assert.That(gemini["hooks"]!["AfterAgent"]!.ToJsonString(), Does.Contain(AgentGate.ContextScriptName));

        Assert.That(Others(), Is.Null, "a second run changes nothing");
        Assert.That(Json(Path.Combine(Codex, "hooks.json"))["hooks"]!["PreToolUse"]!.AsArray().Count, Is.EqualTo(2));
    }

    [Test]
    public void An_unreadable_codex_or_gemini_file_is_left_alone()
    {
        AgentGate.Ensure(Claude);
        Directory.CreateDirectory(Codex);
        File.WriteAllText(Path.Combine(Codex, "hooks.json"), "not json {");

        var did = Others();
        Assert.That(File.ReadAllText(Path.Combine(Codex, "hooks.json")), Is.EqualTo("not json {"));
        Assert.That(did, Does.Not.Contain("Codex").And.Contain("Gemini: wired BeforeTool"), "the others still get wired");
    }
}
