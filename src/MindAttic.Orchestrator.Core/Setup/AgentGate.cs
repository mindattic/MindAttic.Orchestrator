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

    /// <summary>Python shared by both hooks: the claude.exe process the hook runs under (through the
    /// shell between), so a CLI can be typed into or exempted by process, not by session.</summary>
    private const string CliPid = """
        def cli_pid():
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
                    if procs.get(pid, (0, ""))[1].startswith("claude.exe"):  # or claude.exe.old.N, once the updater renames it
                        return pid
            except Exception:
                pass
            return None
        """;

    // The context gate acts on these when they're typed (UserPromptSubmit); the files make them real slash commands.
    internal const string UnthrottleCommand = """
        ---
        description: Run this CLI unthrottled (no pace holds) until it closes, for when something has to get done fast.
        ---
        MindAttic.Orchestrator's context gate has exempted this CLI process from pace holds, subagents included, until the CLI closes or you run /throttle. Its usage still counts toward the 5-hour pace, so other CLIs absorb the slowdown. Reply with one short line confirming this CLI is now unthrottled, then carry on with whatever was asked.
        """;

    internal const string ThrottleCommand = """
        ---
        description: Put this CLI back under the orchestrator's pace holds after /unthrottle.
        ---
        MindAttic.Orchestrator's context gate has put this CLI process back under pace holds. Reply with one short line confirming this CLI is paced again.
        """;

    internal const string AutopilotCommand = """
        ---
        description: Work the stories of every repo with an open CLI (or -repo=a,b) in parallel background agents, each in its own worktree, paced to use the 5-hour window without hitting the limit. /autopilot off stops it.
        argument-hint: "[on|off] [-repo=a,b,c]"
        ---
        The user ran /autopilot $ARGUMENTS. MindAttic.Orchestrator's context gate has already recorded it; there is nothing for you to set up, and nothing for you to work on because of it.

        If the arguments start with "off": reply with one short line confirming autopilot is off; the orchestrator types no new stories into any CLI (agents already working finish their stories).

        Otherwise reply with one short line confirming autopilot is on for the repos that have an open Claude CLI (or, with -repo=, the repos named; name them), then end your turn. From then on the orchestrator works each of those repos' next open story (manual priority first, then smallest effort) in background agents of their own, each in its own git worktree: no window, nothing typed into any CLI, so every tab and checkout stays free for you. As many run at once as each repo's session limit and the pace allow; each commits, closes its ticket and lands its work on main, then the next starts. New stories only start while the 5-hour pace can afford them and the pace gate slows running agents, so the window gets used without hitting the limit. Repos whose CLIs open later join in; an agent that ends without closing its ticket gives it back and you get a "Needs you"; /autopilot off starts nothing new.
        """;

    internal const string CrossRepoCommand = """
        ---
        description: Let this CLI work other repos too (the one exception to "a repo's window works only that repo"). /crossrepo off ends it.
        argument-hint: "<Repo,Repo|all|off>"
        ---
        The user ran /crossrepo $ARGUMENTS. MindAttic.Orchestrator's context gate has recorded it for this CLI process, until it closes.

        If the arguments are empty or "off": reply with one short line confirming this window is back to its own repo only.

        Otherwise reply with one short line confirming this window may now also edit and close tickets in the repos named ("all" means any repo), then carry on with whatever was asked.
        """;

    internal static string Script => $$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's pace gate (written by the orchestrator; edits are overwritten).
        # Holds each tool call for rl-status.json's pace_delay_seconds so agents slow down instead of
        # hitting the usage limit. Re-reads the file while waiting: a lifted hold ends at once.
        import json, sys, time
        from datetime import datetime
        from pathlib import Path

        {{CliPid}}

        STATUS = Path.home() / ".claude" / "rl-status.json"
        STALE = {{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}
        MAX_HOLD = {{MaxHoldSeconds}}

        def status():
            try:
                s = json.loads(STATUS.read_text())
                if s.get("source") != "MindAttic.Orchestrator":
                    return {}
                at = datetime.fromisoformat(s["timestamp"].replace("Z", ""))
                return s if (datetime.utcnow() - at).total_seconds() <= STALE else {}
            except Exception:
                return {}

        def delay():
            try:
                return float(status().get("pace_delay_seconds", 0))
            except Exception:
                return 0

        try:
            data = json.load(sys.stdin)
        except Exception:
            data = {}
        tool = data.get("tool_name")

        # A repo's window works only that repo (MAO-T95): its own folder and worktrees, and its own tickets. Another
        # repo's files and tickets belong to that repo's window, autopilot, or the General (workspace-root) session.
        import os, re, sqlite3
        WS = Path(os.environ.get("MINDATTIC_WORKSPACE", "D:/Projects/MindAttic"))
        WT = WS.parent / (WS.name + ".worktrees")

        def repo_of(path):
            try:
                p = Path(path).resolve()
            except Exception:
                return None
            for base, in_ws in ((WS, True), (WT, False)):
                try:
                    rel = p.relative_to(base.resolve())
                except Exception:
                    continue
                if not rel.parts:
                    return None
                name = rel.parts[0]
                if in_ws and not (base / name / ".git").exists():
                    return None
                return name
            return None

        def ticket_repo(ticket_id):
            try:
                db = sqlite3.connect(str(WS / "MindAttic.Workflow" / "data" / "workflow.db"))
                row = db.execute("SELECT repo FROM tickets WHERE id = ? COLLATE NOCASE", (ticket_id,)).fetchone()
                db.close()
                return row[0] if row else None
            except Exception:
                return None

        def refuse(why):
            print(json.dumps(dict(hookSpecificOutput=dict(hookEventName="PreToolUse", permissionDecision="deny", permissionDecisionReason=why))))
            raise SystemExit(0)

        # The one exception is explicit, per CLI and until it closes: /crossrepo <A,B|all> (the context gate writes the mark).
        def allowed_here():
            pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
            mark = Path.home() / ".claude" / "session-context" / ("crossrepo-" + str(pid))
            try:
                return {r.strip().lower() for r in mark.read_text().split(",") if r.strip()} if pid else set()
            except Exception:
                return set()

        here = repo_of(data.get("cwd") or "")
        if here:
            args = data.get("tool_input") or {}
            excepted = None
            def other(r):
                global excepted
                if not r or r.lower() == here.lower():
                    return False
                if excepted is None:
                    excepted = allowed_here()
                return "all" not in excepted and r.lower() not in excepted
            if tool in ("Edit", "Write", "MultiEdit", "NotebookEdit"):
                target = args.get("file_path") or args.get("notebook_path") or ""
                if other(repo_of(target)):
                    refuse(f"This window is for {here}; {target} belongs to {repo_of(target)}. Work {repo_of(target)} in its own window "
                           f"(or autopilot, or the General session), or file a ticket for it: workflow ticket new --repo {repo_of(target)} ...")
            if tool in ("Bash", "PowerShell"):
                command = str(args.get("command") or "")
                # Only a segment that IS a workflow ticket command counts: the words inside a message or a string don't.
                for segment in re.split(r"[;\n|]|&&", command):
                    if not re.match(r"\s*(?:&\s*)?['\"]?workflow(?:\.exe)?['\"]?\s+ticket\s+(new|close|correct)\b", segment):
                        continue
                    action = re.match(r"\s*(?:&\s*)?['\"]?workflow(?:\.exe)?['\"]?\s+ticket\s+(new|close|correct)\s*(.*)", segment).group(2)
                    named = re.search(r"--repo[\s=]+['\"]?([^\s'\"]+)", action)
                    owners = [named.group(1)] if named else []
                    # The ticket a close/correct acts on is its first argument; IDs in --summary or --ref are just text.
                    first = re.match(r"['\"]?([A-Z][A-Z0-9]*-T\d+)\b", action.strip())
                    owners += [ticket_repo(first.group(1))] if first else []
                    for owner in owners:
                        if other(owner):
                            refuse(f"This window is for {here}; that ticket belongs to {owner}. Only {here}'s tickets are worked here: "
                                   f"leave {owner}'s to its own window, autopilot or the General session.")
                for target in re.findall(r"\bgit\s+-C\s+['\"]?([^'\"\s]+)", command):
                    if other(repo_of(target)) and re.search(r"\b(commit|push|add|rm|mv|reset|checkout|merge|rebase|stash)\b", command):
                        refuse(f"This window is for {here}; git changes in {repo_of(target)} belong to that repo's window.")

        # A search of a whole drive never finishes in time and, orphaned when its agent stops, leaks handles until
        # Windows can't draw (found live: two `find /` held 660k handles and froze the desktop). Refuse it up front.
        import re
        cmd = str((data.get("tool_input") or {}).get("command") or "")
        ROOT_SEARCH = (r"(?:^|[;&|(])\s*(?:\"[^\"]*[\\/]|'[^']*[\\/]|[^\s\"';&|]*[\\/])?(find|fd|rg|grep|ls|dir|Get-ChildItem|gci)(?:\.exe)?[\"']?"
                       r"(?=\s)[^|;&\n]*?\s[\"']?(/|/[a-zA-Z]|[A-Za-z]:[\\/]?|~|\$HOME|\$env:USERPROFILE)[\"']?(?=\s|$|[;&|)])")
        def root_search(c):
            if not re.search(ROOT_SEARCH, c, re.I):
                return False
            # Listing a root is harmless; only a recursive listing walks the whole drive.
            return not re.match(r"^\s*(ls|dir|Get-ChildItem|gci)\b", c, re.I) or bool(re.search(r"(\s-\w*R\w*\b|\s/s\b|-Recurse)", c))
        if tool in ("Bash", "PowerShell") and root_search(cmd):
            print(json.dumps(dict(hookSpecificOutput=dict(hookEventName="PreToolUse", permissionDecision="deny",
                permissionDecisionReason="Searching a whole drive or the filesystem root is blocked: it never finishes and leaks handles until "
                "Windows freezes. Search the specific folder instead (a repo under D:\\Projects\\MindAttic, or a known install folder), "
                "or use the Glob/Grep tools with a path."))))
            raise SystemExit(0)

        # /unthrottle exempts one CLI (its process, so subagents and /clear included) until it closes.
        MARKS = Path.home() / ".claude" / "session-context"
        if MARKS.exists() and any(MARKS.glob("unthrottled-*")):
            pid = cli_pid()
            if pid is not None and (MARKS / ("unthrottled-" + str(pid))).exists():
                raise SystemExit(0)

        start = time.monotonic()
        # A new subagent waits while the allowance the pace works out is used up (all CLIs together); the
        # wait counts toward the same MAX_HOLD, and past it the subagent starts anyway: held, never refused.
        if tool in ("Agent", "Task"):
            SUB = MARKS / "subagents"
            while time.monotonic() - start < MAX_HOLD:
                allowed = status().get("subagent_allowance")
                if allowed is None or (sum(1 for _ in SUB.iterdir()) if SUB.exists() else 0) < allowed:
                    break
                time.sleep(5)
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
        import json, re, sys
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

        {{{CliPid}}}

        try:
            data = json.load(sys.stdin)
        except Exception:
            sys.exit(0)
        sid, event = data.get("session_id"), data.get("hook_event_name")
        # Every running subagent has a mark, so the pace gate can hold new ones past the allowance.
        if event in ("SubagentStart", "SubagentStop"):
            if data.get("agent_id"):
                sub = DIR / "subagents"
                sub.mkdir(parents=True, exist_ok=True)
                mark = sub / re.sub(r"[^A-Za-z0-9_.-]", "_", str(data["agent_id"]))
                if event == "SubagentStart":
                    mark.write_text(str(cli_pid() or ""))
                elif mark.exists():
                    mark.unlink()
            sys.exit(0)
        if not sid or data.get("agent_id"):
            sys.exit(0)  # subagents finish inside their parent's turn
        DIR.mkdir(parents=True, exist_ok=True)
        state_file = DIR / (sid + ".state.json")
        state = load(state_file)
        now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        if event == "Stop":
            state.update(state="idle", at=now, pid=cli_pid(), cwd=data.get("cwd"))
        elif event == "UserPromptSubmit":
            state.update(state="busy", at=now, warned=False, cwd=data.get("cwd"))
            typed = (data.get("prompt") or "").strip()
            prompt = typed.lower()
            # /autopilot [on|off] [-repo=a,b]: every open repo CLI (or those repos' CLIs) works its own stories until off.
            if prompt.startswith("/autopilot"):
                order = DIR / "autopilot.order"
                args = typed[len("/autopilot"):].strip()
                if re.match(r"off\b", args, re.I):
                    if order.exists():
                        order.unlink()
                else:
                    m = re.search(r"-{1,2}repos?\s*[=:]?\s*\[?([^\]]*)\]?", args, re.I)
                    order.write_text(",".join(r.strip() for r in m.group(1).replace(" ", ",").split(",") if r.strip()) if m else "")
            # /crossrepo <A,B|all> lets this CLI work other repos too (the explicit exception to MAO-T95); /crossrepo off ends it.
            if prompt.startswith("/crossrepo"):
                pid = cli_pid()
                if pid is not None:
                    mark = DIR / ("crossrepo-" + str(pid))
                    args = typed[len("/crossrepo"):].strip()
                    if not args or re.match(r"off\b", args, re.I):
                        if mark.exists():
                            mark.unlink()
                    else:
                        mark.write_text(",".join(r.strip() for r in args.replace(" ", ",").split(",") if r.strip()))
            if prompt.startswith("/unthrottle") or prompt.startswith("/throttle"):
                pid = cli_pid()
                if pid is not None:
                    mark = DIR / ("unthrottled-" + str(pid))
                    if prompt.startswith("/unthrottle"):
                        mark.write_text(now)
                    elif mark.exists():
                        mark.unlink()
            # Each turn starts knowing how many subagents the pace allows right now.
            try:
                s = json.loads((Path.home() / ".claude" / "rl-status.json").read_text())
                fresh = (datetime.now(timezone.utc) - datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00"))).total_seconds() <= {{{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}}
                if s.get("source") == "MindAttic.Orchestrator" and fresh and s.get("subagent_note"):
                    print(json.dumps({"hookSpecificOutput": {"hookEventName": "UserPromptSubmit", "additionalContext": s["subagent_note"]}}))
            except Exception:
                pass
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
        Directory.CreateDirectory(Path.Combine(dir, "commands"));
        foreach (var (path, body) in new[]
        {
            (script, Script), (contextScript, ContextScript(contextPercent)),
            (Path.Combine(dir, "commands", "unthrottle.md"), UnthrottleCommand), (Path.Combine(dir, "commands", "throttle.md"), ThrottleCommand),
            (Path.Combine(dir, "commands", "autopilot.md"), AutopilotCommand),
            (Path.Combine(dir, "commands", "crossrepo.md"), CrossRepoCommand),
        })
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
        foreach (var hookEvent in new[] { "PostToolUse", "Stop", "UserPromptSubmit", "SubagentStart", "SubagentStop" })
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

    public static string CodexDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    public static string GeminiDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");
    public static string KimiConfigPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kimi-code", "config.toml");

    /// <summary>
    /// Wires the same <c>pace-gate.py</c> into Codex's, Gemini's and Kimi's own pre-tool-call hook, so the
    /// pacer's hold slows their tool calls too, not just Claude's: Codex's <c>PreToolUse</c> in
    /// <c>~/.codex/hooks.json</c>, Gemini's <c>BeforeTool</c> in <c>~/.gemini/settings.json</c>, Kimi's
    /// <c>PreToolUse</c> in <c>~/.kimi-code/config.toml</c>. The script's own CLI-walk and /unthrottle check
    /// only ever match a <c>claude.exe</c> parent, so under these three it just falls through to the plain
    /// wait on <c>pace_delay_seconds</c>, same as it gives Claude. Codex still shows its own one-time "hooks
    /// need review" prompt in its TUI before a freshly added hook runs the first time; nothing types through
    /// that for you, since a mis-keyed approval there is worse than leaving it for a click. Returns null
    /// until <see cref="Ensure"/> has written <paramref name="claudeDir"/>'s <c>pace-gate.py</c>.
    /// </summary>
    public static string? EnsureOtherProviders(string? claudeDir = null, string? codexDir = null, string? geminiDir = null, string? kimiConfigPath = null)
    {
        var script = Path.Combine(claudeDir ?? ClaudeDir, ScriptName);
        if (!File.Exists(script)) return null;
        var command = Command(script);
        var did = new List<string>();

        if (MergeJsonHook(Path.Combine(codexDir ?? CodexDir, "hooks.json"), "PreToolUse", command, HookTimeoutSeconds) is { } c) did.Add("Codex: " + c);
        if (MergeJsonHook(Path.Combine(geminiDir ?? GeminiDir, "settings.json"), "BeforeTool", command, HookTimeoutSeconds * 1000) is { } g) did.Add("Gemini: " + g);
        if (AppendKimiHook(kimiConfigPath ?? KimiConfigPath, command, HookTimeoutSeconds)) did.Add("Kimi: wired PreToolUse into config.toml");

        return did.Count > 0 ? string.Join(", ", did) : null;
    }

    /// <summary>Adds <paramref name="hookEvent"/> to the JSON hook file at <paramref name="path"/> (created fresh if
    /// missing), reusing <see cref="AddHook"/>'s idempotent merge; null when already wired in or the file is
    /// someone else's unreadable JSON.</summary>
    private static string? MergeJsonHook(string path, string hookEvent, string command, int timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject? root;
        try { root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : new JsonObject(); }
        catch (JsonException) { return null; }
        if (root is null || !AddHook(root, hookEvent, ScriptName, command, timeout)) return null;
        var temp = path + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
        return $"wired {hookEvent} into {Path.GetFileName(path)}";
    }

    /// <summary>Appends a <c>[[hooks]]</c> table to Kimi's <c>config.toml</c> (a plain text append: Kimi's own
    /// config has no JSON equivalent and no library here parses TOML); false when <paramref name="command"/>
    /// is already in the file.</summary>
    private static bool AppendKimiHook(string path, string command, int timeoutSeconds)
    {
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        if (existing.Contains(command, StringComparison.Ordinal)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var block = $"[[hooks]]\nevent = \"PreToolUse\"\ncommand = '{command}'\ntimeout = {timeoutSeconds}\n";
        File.WriteAllText(path, (existing.Length > 0 ? existing.TrimEnd('\r', '\n') + "\n\n" : "") + block);
        return true;
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
