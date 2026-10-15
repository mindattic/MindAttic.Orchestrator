using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The pace gate: a Claude Code PreToolUse hook in <c>~/.claude/settings.json</c> that holds each tool
/// call for the <c>pace_delay_seconds</c> the pacer writes into <c>rl-status.json</c>, so running agents
/// slow down while the burn won't last until the reset, instead of running into the wall. It re-reads the
/// file while it waits, so a hold lifted mid-wait ends at once, and ignores a file nobody refreshes.
/// </summary>
public static class AgentGate
{
    public const string ScriptName = "pace-gate.py";

    /// <summary>Claude Code's limit on the hook; above <see cref="MaxHoldSeconds"/> so a hold is never cut off as a failure.</summary>
    public const int HookTimeoutSeconds = 330;

    public const int MaxHoldSeconds = 300;

    public static string ClaudeDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    internal static string Script => $$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's pace gate (written by the orchestrator; edits are overwritten).
        # Holds each tool call for rl-status.json's pace_delay_seconds so agents slow down instead of
        # hitting the usage limit. Re-reads the file while waiting: a lifted hold ends at once.
        import json, time
        from datetime import datetime
        from pathlib import Path

        STATUS = Path.home() / ".claude" / "rl-status.json"
        STALE = {{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}
        MAX_HOLD = {{MaxHoldSeconds}}

        def delay():
            try:
                s = json.loads(STATUS.read_text())
                if s.get("source") != "MindAttic.Orchestrator":
                    return 0
                at = datetime.fromisoformat(s["timestamp"].replace("Z", ""))
                if (datetime.utcnow() - at).total_seconds() > STALE:
                    return 0
                return float(s.get("pace_delay_seconds", 0))
            except Exception:
                return 0

        start = time.monotonic()
        while True:
            waited = time.monotonic() - start
            left = min(delay(), MAX_HOLD) - waited
            if left <= 0:
                break
            time.sleep(min(left, 5))
        """.Replace("\r\n", "\n");

    public const string ContextScriptName = "context-gate.py";

    /// <summary>
    /// The context gate: records each session's turn (busy on a prompt, idle on Stop) next to the
    /// context fill the statusline saves, and once a turn tells the main agent its context is past
    /// <paramref name="percent"/> so it wraps up and lets its subagents finish. The orchestrator then
    /// cycles it (<see cref="Sessions.ContextCycler"/>).
    /// </summary>
    internal static string ContextScript(int percent) => $$$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's context gate (written by the orchestrator; edits are overwritten).
        # Records each session's turn state and warns the main agent once per turn when its context is
        # past the cycle point, so it finishes cleanly before the orchestrator saves, clears and reloads it.
        import json, sys
        from datetime import datetime, timezone
        from pathlib import Path

        DIR = Path.home() / ".claude" / "session-context"
        PERCENT = {{{percent}}}

        def load(path):
            try:
                return json.loads(path.read_text())
            except Exception:
                return {}

        def save(path, value):
            tmp = path.with_suffix(".tmp")
            tmp.write_text(json.dumps(value))
            tmp.replace(path)

        def cli_pid():
            # The claude process this hook runs under (via the shell between), so the orchestrator can
            # type into its console when it isn't in a Launcher tab.
            try:
                import ctypes, os
                from ctypes import wintypes
                class Entry(ctypes.Structure):
                    _fields_ = [("dwSize", wintypes.DWORD), ("cntUsage", wintypes.DWORD), ("th32ProcessID", wintypes.DWORD),
                                ("th32DefaultHeapID", ctypes.c_size_t), ("th32ModuleID", wintypes.DWORD), ("cntThreads", wintypes.DWORD),
                                ("th32ParentProcessID", wintypes.DWORD), ("pcPriClassBase", ctypes.c_long), ("dwFlags", wintypes.DWORD),
                                ("szExeFile", ctypes.c_char * 260)]
                k = ctypes.windll.kernel32
                k.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
                snap = k.CreateToolhelp32Snapshot(2, 0)
                procs, e = {}, Entry()
                e.dwSize = ctypes.sizeof(Entry)
                ok = k.Process32First(snap, ctypes.byref(e))
                while ok:
                    procs[e.th32ProcessID] = (e.th32ParentProcessID, e.szExeFile.decode(errors="ignore").lower())
                    ok = k.Process32Next(snap, ctypes.byref(e))
                k.CloseHandle(snap)
                pid = os.getpid()
                for _ in range(8):
                    pid = procs.get(pid, (0, ""))[0]
                    if not pid:
                        return None
                    if procs.get(pid, (0, ""))[1] == "claude.exe":
                        return pid
            except Exception:
                pass
            return None

        try:
            data = json.load(sys.stdin)
        except Exception:
            sys.exit(0)
        sid, event = data.get("session_id"), data.get("hook_event_name")
        if not sid or data.get("agent_id"):
            sys.exit(0)  # subagents finish inside their parent's turn
        DIR.mkdir(parents=True, exist_ok=True)
        state_file = DIR / (sid + ".state.json")
        state = load(state_file)
        now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        if event == "Stop":
            state.update(state="idle", at=now, pid=cli_pid())
        elif event == "UserPromptSubmit":
            state.update(state="busy", at=now, warned=False)
        elif event == "PostToolUse":
            used = load(DIR / (sid + ".json")).get("used_percentage") or 0
            if state.get("warned") or used < PERCENT:
                sys.exit(0)
            state["warned"] = True
            print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext":
                "Context is " + str(used) + "% full. Finish the step you're on, let running subagents finish and "
                "start no new ones, then end your turn with a short status. The orchestrator will /quicksave, "
                "/clear and /quickload this session so the work carries on with a fresh context."}}))
        else:
            sys.exit(0)
        save(state_file, state)
        """.Replace("\r\n", "\n");

    /// <summary>Writes the scripts and registers their hooks when anything is missing or out of date.
    /// Returns what it did, or null when everything was already in place.</summary>
    public static string? Ensure(string? claudeDir = null, int contextPercent = 80)
    {
        var dir = claudeDir ?? ClaudeDir;
        Directory.CreateDirectory(dir);
        var did = new List<string>();

        var script = Path.Combine(dir, ScriptName);
        var contextScript = Path.Combine(dir, ContextScriptName);
        foreach (var (path, body) in new[] { (script, Script), (contextScript, ContextScript(contextPercent)) })
        {
            if (File.Exists(path) && File.ReadAllText(path) == body) continue;
            File.WriteAllText(path, body);
            did.Add("wrote " + Path.GetFileName(path));
        }

        var settingsPath = Path.Combine(dir, "settings.json");
        JsonObject? root;
        try { root = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject : new JsonObject(); }
        catch (JsonException) { root = null; }
        if (root is null) return did.Count > 0 ? string.Join(", ", did) : null; // unreadable: leave someone else's file alone
        var changed = AddHook(root, "PreToolUse", ScriptName, Command(script), HookTimeoutSeconds);
        foreach (var hookEvent in new[] { "PostToolUse", "Stop", "UserPromptSubmit" })
            changed |= AddHook(root, hookEvent, ContextScriptName, Command(contextScript), 10);
        if (changed)
        {
            var temp = settingsPath + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, settingsPath, overwrite: true);
            did.Add("registered the pace and context gate hooks");
        }
        return did.Count > 0 ? string.Join(", ", did) : null;
    }

    internal static string Command(string script) => $"python \"{script.Replace('\\', '/')}\"";

    /// <summary>Adds a <paramref name="hookEvent"/> entry unless one already runs <paramref name="scriptName"/>; true when it changed <paramref name="root"/>.</summary>
    internal static bool AddHook(JsonObject root, string hookEvent, string scriptName, string command, int timeout)
    {
        var hooks = root["hooks"] as JsonObject ?? (JsonObject)(root["hooks"] = new JsonObject());
        var entries = hooks[hookEvent] as JsonArray ?? (JsonArray)(hooks[hookEvent] = new JsonArray());
        foreach (var hook in entries.OfType<JsonObject>().SelectMany(m => (m["hooks"] as JsonArray ?? []).OfType<JsonObject>()))
        {
            if (hook["command"]?.GetValue<string>() is { } c && c.Contains(scriptName, StringComparison.OrdinalIgnoreCase))
            {
                if (c == command && hook["timeout"]?.GetValue<int>() == timeout) return false;
                hook["command"] = command;
                hook["timeout"] = timeout;
                return true;
            }
        }
        entries.Add(new JsonObject
        {
            ["matcher"] = "",
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = timeout }),
        });
        return true;
    }
}
