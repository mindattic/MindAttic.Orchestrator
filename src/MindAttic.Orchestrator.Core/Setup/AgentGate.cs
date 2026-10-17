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

    public const int StatusLineRefreshSeconds = 60;

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

    /// <summary>
    /// Python shared by both hooks (needs <c>json</c>, <c>os</c>, <c>time</c>, <c>datetime</c> and <c>Path</c>): the never-over line
    /// without the orchestrator. While the daemon keeps <c>rl-status.json</c> fresh it decides the stop; if it dies or
    /// stalls, the hooks used to treat the stale file as "no pacing", so nothing stopped a window passing the limit.
    /// Claude Code's own 5-hour percent (the shared statusline saves it every minute) stands in, and a stale file is
    /// reported as the daemon being down (M.ORCH-T167).
    /// <para>A stop, once seen, is latched until its window resets (<see cref="StopLatchName"/>, M.ORCH-T196). Every open
    /// session's statusline rewrites <c>claude-usage.json</c> each minute with its own last percent, so an idle one turned
    /// Claude's 99% back into 85% once a minute and every call held at the stop went through; and a daemon that stalled
    /// right after writing its stop let the stop lapse with its file. Usage in a window never goes down, so neither can.</para>
    /// </summary>
    internal static string ClaudeUsagePython => $$"""
        STOP_PERCENT = {{StopPercent}}
        LATCH = Path.home() / ".claude" / "session-context" / "{{StopLatchName}}"
        def read_json(path):
            # A file being replaced can't be opened for a moment on Windows: that's a retry, not a missing file.
            for _ in range(20):
                try:
                    return json.loads(path.read_text())
                except PermissionError:
                    time.sleep(0.05)
            raise PermissionError(str(path))
        def latched(by):
            # The stop latched by "claude" or "orchestrator" for a window that hasn't reset; None otherwise.
            try:
                l = read_json(LATCH).get(by)
                if l and float(l["until"]) > time.time():
                    return l
            except Exception:
                pass
            return None
        def latch(by, until, **fields):
            # Records (or with until=None, clears) a stop seen for its window, so it holds until the reset whatever comes next.
            try:
                every = read_json(LATCH) if LATCH.exists() else {}
            except Exception:
                every = {}
            old = every.get(by)
            if until is None:
                if old is None:
                    return
                every.pop(by)
            else:
                entry = dict(fields, until=float(until))
                if old and old.get("until") == entry["until"] and float(old.get("percent") or 0) > float(entry.get("percent") or 0):
                    entry["percent"] = old["percent"]
                if old == entry:
                    return
                every[by] = entry
            tmp = LATCH.with_name(LATCH.name + "." + str(os.getpid()) + ".tmp")
            try:
                LATCH.parent.mkdir(parents=True, exist_ok=True)
                tmp.write_text(json.dumps(every))
                for _ in range(20):
                    try:
                        os.replace(tmp, LATCH)
                        return
                    except PermissionError:
                        time.sleep(0.05)
            except Exception:
                pass
            try:
                tmp.unlink()
            except Exception:
                pass
        def claude_stop():
            # (percent, reset epoch) when Claude itself reports this window at the stop line, now or earlier in the same
            # window (an idle session's later, lower report doesn't lift it); None otherwise.
            try:
                w = read_json(Path.home() / ".claude" / "claude-usage.json")["rate_limits"]["five_hour"]
                if float(w["used_percentage"]) >= STOP_PERCENT and float(w["resets_at"]) > time.time():
                    latch("claude", float(w["resets_at"]), percent=float(w["used_percentage"]))
                    return float(w["used_percentage"]), float(w["resets_at"])
            except Exception:
                pass
            l = latched("claude")
            return (float(l.get("percent") or STOP_PERCENT), float(l["until"])) if l else None
        def claude_stop_note(stop):
            reset = datetime.fromtimestamp(stop[1]).strftime("%I:%M %p").lstrip("0")
            return ("Claude reports " + str(int(stop[0])) + "% of the 5-hour window used and MindAttic.Orchestrator isn't running to pace it, "
                    "so tool calls are refused until it resets at " + reset + ". End your turn now.")
        ORCHESTRATOR_STOP = "The 5-hour window is at its target: tool calls are refused until it resets. End your turn now."
        def orchestrator_stop(s):
            # The orchestrator's stop note, from its fresh rl-status.json s ({} when stale or unreadable) or latched from an
            # earlier one until the window resets, so a daemon that stalls at the stop can't lift it; None when not stopped.
            # A fresh file without the stop is the orchestrator lifting it, and ends the latch.
            if s:
                if not s.get("stop"):
                    latch("orchestrator", None)
                    return None
                note = s.get("stop_note") or ORCHESTRATOR_STOP
                try:
                    until = datetime.fromisoformat(str(s["stop_until"]).replace("Z", "+00:00")).timestamp()
                except Exception:
                    old = latched("orchestrator")
                    until = float(old["until"]) if old else time.time() + {{StopLatchFallbackSeconds}}
                latch("orchestrator", until, note=note)
                return note
            l = latched("orchestrator")
            return (l.get("note") or ORCHESTRATOR_STOP) if l else None
        def daemon_down():
            # Seconds since the orchestrator last wrote rl-status.json, once that's past what the gates trust; else None.
            try:
                s = json.loads((Path.home() / ".claude" / "rl-status.json").read_text())
                if s.get("source") != "MindAttic.Orchestrator":
                    return None
                age = time.time() - datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00")).timestamp()
                return age if age > {{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}} else None
            except Exception:
                return None
        """;

    /// <summary>The percent of the 5-hour window at which every tool call is refused, whoever is counting.</summary>
    public const int StopPercent = 99;

    /// <summary>Where the hooks latch a stop they've seen until its window resets (in <c>~/.claude/session-context</c>).</summary>
    public const string StopLatchName = "stop-latch.json";

    /// <summary>A stop from an orchestrator that didn't say when its window resets is latched this long.</summary>
    public const int StopLatchFallbackSeconds = 600;

    /// <summary>Python shared by both hooks (needs <c>os</c> and <c>Path</c>): the workspace and which repo a folder is in,
    /// so the repo-window guard and keep-going can't disagree about it.</summary>
    private const string WorkspacePython = """
        WS = Path(os.environ.get("MINDATTIC_WORKSPACE", "D:/Projects/MindAttic"))
        WT = WS.parent / (WS.name + ".worktrees")

        def repo_of(path, worktrees=True):
            # A repo checkout under the workspace, or (with worktrees) one of autopilot's worktrees of it; None elsewhere.
            try:
                p = Path(path).resolve()
            except Exception:
                return None
            for base, in_ws in ((WS, True), (WT, False)):
                if not (in_ws or worktrees):
                    continue
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
        """;

    /// <summary>Python shared by both hooks (needs <c>json</c>, <c>os</c>, <c>re</c> and <c>Path</c>): the commands exempt from
    /// orchestration pacing (<see cref="Settings.OrchestratorSettings.ExemptCommands"/>), read live from the orchestrator's
    /// settings.json so an edit applies to the next prompt, and which of them a prompt or Skill names (M.ORCH-T198).</summary>
    internal static string ExemptPython => $$"""
        EXEMPT_DEFAULT = [{{string.Join(", ", Settings.OrchestratorSettings.DefaultExemptCommands.Select(c => "\"" + c + "\""))}}]
        def exempt_commands():
            try:
                names = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text()).get("ExemptCommands")
            except Exception:
                names = None
            if not isinstance(names, list):
                names = EXEMPT_DEFAULT
            return {str(n).strip().lstrip("/").lower() for n in names if str(n).strip().lstrip("/")}
        def exempt_prompt(text):
            # The exempt command a typed prompt starts with ("/deploy now" -> "/deploy"), or None.
            m = re.match(r"\s*/([^\s/]+)(?:\s|$)", text or "")
            return "/" + m.group(1).lower() if m and m.group(1).lower() in exempt_commands() else None
        def exempt_skill(name):
            return bool(name) and str(name).strip().lstrip("/").lower() in exempt_commands()
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
        description: This CLI works the open tickets of its own repo (or -repo=A,B, wildcards like -repo=GTFO.*, or "all") through its own subagents, each in its own worktree, landing on main and closing the ticket, until none are left. /autopilot off stops it in this CLI; /autopilot off all in every CLI.
        argument-hint: "[off|off all|all] [-repo=A,B | -repo=GTFO.*]"
        ---
        The user ran /autopilot $ARGUMENTS in this CLI. MindAttic.Orchestrator's context gate has recorded it for this CLI only (no other CLI is affected), and while it's on, the repos named (a wildcard such as GTFO.* names every repo it matches) are this CLI's cross-repo exception so your subagents can work and close tickets there.

        If the arguments start with "off": reply with one short line confirming autopilot is off in this CLI (with -repo, just for those repos; "off all": in every CLI). Subagents already working finish their tickets. Then end your turn.

        Otherwise you work the tickets yourself, through subagents, now:
        1. Run `orchestrator next --repos "<repos>" --claim`, where <repos> is the -repo list as typed (wildcards included), * for all, or this CLI's own repo when none are named; keep the quotes so no shell expands a wildcard. It claims the next workable tickets (fixes and tickets the user approved, in queue order, as many as the pace allows right now), makes each its own git worktree, and prints each ticket's id and a full prompt.
        2. Start one subagent per ticket it printed, all in one message, each with exactly the prompt printed for it (a general-purpose subagent, in the foreground). Each works only in its worktree, commits, lands on the repo's main line and closes its ticket.
        3. When they've returned, say in one line per ticket what happened, and go back to step 1.
        4. Stop when `orchestrator next` hands out nothing (it says why: none left, waiting for approval, or the pace), and say so in one short line. Don't start anything else on your own. From then on the orchestrator works each of those repos' next open story (manual priority first, then smallest effort) in background agents of their own, each in its own git worktree: no window, nothing typed into any CLI, so every tab and checkout stays free for you. As many run at once as each repo's session limit and the pace allow; each commits, closes its ticket and lands its work on main, then the next starts. New stories only start while the 5-hour pace can afford them and the pace gate slows running agents, so the window gets used without hitting the limit. Repos whose CLIs open later join in; an agent that ends without closing its ticket gives it back and you get a "Needs you"; /autopilot off starts nothing new.
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
        import json, os, sys, time
        from datetime import datetime
        from pathlib import Path

        {{CliPid}}

        {{ClaudeUsagePython}}

        STATUS = Path.home() / ".claude" / "rl-status.json"
        STALE = {{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}
        # MINDATTIC_MAX_HOLD shortens the longest hold so tests can run the held-then-refused paths in seconds.
        try:
            MAX_HOLD = float(os.environ.get("MINDATTIC_MAX_HOLD") or {{MaxHoldSeconds}})
        except ValueError:
            MAX_HOLD = {{MaxHoldSeconds}}

        def status():
            try:
                s = read_json(STATUS)
                if s.get("source") != "MindAttic.Orchestrator":
                    return {}
                at = datetime.fromisoformat(s["timestamp"].replace("Z", ""))
                return s if (datetime.utcnow() - at).total_seconds() <= STALE else {}
            except Exception:
                return {}

        def delay():
            # The pace's hold. While the daemon is stalled or down (its file stale), the last pace it wrote still holds
            # until the window it was worked out for resets (M.ORCH-T205: the daemon stalled for 4 minutes under heavy
            # load at 88% and every call ran unheld until 96%); past that reset nothing is known, so nothing is held.
            try:
                s = status()
                if s:
                    return float(s.get("pace_delay_seconds", 0))
                s = read_json(STATUS)
                if s.get("source") != "MindAttic.Orchestrator" or not s.get("window_reset"):
                    return 0
                until = datetime.fromisoformat(str(s["window_reset"]).replace("Z", "+00:00")).timestamp()
                return float(s.get("pace_delay_seconds", 0)) if time.time() < until else 0
            except Exception:
                return 0

        # Every hold of a second or more and every refusal at the stop goes to the pace ledger's event log (M.ORCH-T166),
        # so a window that went over can be traced to the calls around it.
        def gate_log(decision, held):
            try:
                (Path.home() / ".claude" / "session-context").mkdir(parents=True, exist_ok=True)
                with open(Path.home() / ".claude" / "session-context" / "{{GateLogName}}", "a", encoding="utf-8") as log:
                    log.write(json.dumps({"at": datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"), "decision": decision, "held": round(held, 1),
                                          "tool": tool, "percent": status().get("percent_used")}) + "\n")
            except Exception:
                pass

        try:
            data = json.load(sys.stdin)
        except Exception:
            data = {}
        tool = data.get("tool_name")

        # A repo's window works only that repo (M.ORCH-T95): its own folder and worktrees, and its own tickets. Another
        # repo's files and tickets belong to that repo's window, autopilot, or the General (workspace-root) session.
        import fnmatch, os, re, sqlite3
        {{WorkspacePython}}

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
            # /crossrepo's repos, and the repos /autopilot is working in this CLI ("*" for all): its subagents edit and close there.
            pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
            found = set()
            for kind in ("crossrepo-", "autopilot-"):
                try:
                    found |= {r.strip().lower() for r in (Path.home() / ".claude" / "session-context" / (kind + str(pid))).read_text().split(",") if r.strip()} if pid else set()
                except Exception:
                    pass
            return {"all" if r == "*" else r for r in found}

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
                # A name may be a wildcard (/autopilot -repo=GTFO.*): every repo it matches is excepted.
                return "all" not in excepted and not any(fnmatch.fnmatchcase(r.lower(), p) for p in excepted)
            if tool in ("Edit", "Write", "MultiEdit", "NotebookEdit"):
                target = args.get("file_path") or args.get("notebook_path") or ""
                if other(repo_of(target)):
                    refuse(f"This window is for {here}; {target} belongs to {repo_of(target)}. Work {repo_of(target)} in its own window "
                           f"(or autopilot, or the General session), or file a ticket for it: workflow ticket new --repo {repo_of(target)} ...")
            if tool in ("Bash", "PowerShell"):
                command = str(args.get("command") or "")
                # Only a segment that IS a workflow ticket command counts: the words inside a message or a string don't.
                for segment in re.split(r"[;\n|]|&&", command):
                    called = re.match(r"\s*(?:&\s*)?['\"]?workflow(?:\.exe)?['\"]?\s+ticket\s+(close|correct)\b\s*(.*)", segment)
                    if not called:
                        continue  # filing a new ticket in another repo is how work is handed to it, so `new` is never refused
                    action = called.group(2)
                    named = re.search(r"--repo[\s=]+['\"]?([^\s'\"]+)", action)
                    owners = [named.group(1)] if named else []
                    # The ticket a close/correct acts on is its first argument; IDs in --summary or --ref are just text.
                    first = re.match(r"['\"]?((?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)\b", action.strip())
                    owners += [ticket_repo(first.group(1))] if first else []
                    for owner in owners:
                        if other(owner):
                            refuse(f"This window is for {here}; that ticket belongs to {owner}. Only {here}'s tickets are worked here: "
                                   f"leave {owner}'s to its own window, autopilot or the General session.")
                for target in re.findall(r"\bgit\s+-C\s+['\"]?([^'\"\s]+)", command):
                    if other(repo_of(target)) and re.search(r"\b(commit|push|add|rm|mv|reset|checkout|merge|rebase|stash)\b", command):
                        refuse(f"This window is for {here}; git changes in {repo_of(target)} belong to that repo's window.")

        # Every repo lands on main (the master repos were renamed 2026-10-16): a push to master would only recreate a stray
        # branch on GitHub that triggers nothing (found live: an agent told to push main to a master repo made a stray main).
        if tool in ("Bash", "PowerShell"):
            for segment in re.split(r"[;\n|]|&&", str((data.get("tool_input") or {}).get("command") or "")):
                if re.match(r"\s*(?:&\s*)?['\"]?git(?:\.exe)?['\"]?\s[^\n]*\bpush\b", segment) and re.search(r"(?:\s|:)master(?![\w./-])", segment) and not re.search(r"--delete\b", segment):
                    refuse("Every MindAttic repo's main line is main now (renamed from master 2026-10-16); pushing master would "
                           "recreate a stray branch. Push main instead: git push origin HEAD:main.")

        # A search of a whole drive never finishes in time and, orphaned when its agent stops, leaks handles until
        # Windows can't draw (found live: two `find /` held 660k handles and froze the desktop). Refuse it up front.
        import re
        cmd = str((data.get("tool_input") or {}).get("command") or "")
        ROOT_SEARCH = (r"(?:^|[;&|(\n])\s*(?:\"[^\"]*[\\/]|'[^']*[\\/]|[^\s\"';&|]*[\\/])?(find|fd|rg|grep|ls|dir|Get-ChildItem|gci)(?:\.exe)?[\"']?"
                       r"(?=\s)[^|;&\n]*?\s[\"']?(/|/[a-zA-Z]|[A-Za-z]:[\\/]?|~|\$HOME|\$env:USERPROFILE)[\"']?(?=\s|$|[;&|)])")
        # A heredoc's or here-string's body is data (a script, a file's text), not commands: `open(DIR / x)` in a Python
        # heredoc read as `dir /` (M.ORCH-T168). Each line of what's left starts a command, so `cd x` then `find /` is caught.
        def commands_only(c):
            c = re.sub(r"<<-?\s*([\"']?)(\w+)\1[^\n]*\n.*?\n\s*\2\s*(?=\n|$)", "", c, flags=re.S)
            return re.sub(r"@([\"'])\s*\n.*?\n\1@", "", c, flags=re.S)
        def root_search(c, powershell):
            c = commands_only(c)
            # Each command naming a root is judged on its own: `ls && find / ...` is a search, `find . ; ls /` isn't.
            for m in re.finditer(ROOT_SEARCH, c, re.I):
                name = m.group(1).lower()
                if name not in ("ls", "dir", "get-childitem", "gci"):
                    return True
                # Listing a root is harmless; only a recursive listing walks the whole drive. PowerShell's parameters ignore
                # case (-recurse, -r), but bash's `ls -r` only reverses the order.
                segment = re.split(r"[;&|\n]", c[m.start(1):], maxsplit=1)[0]
                if powershell or name in ("get-childitem", "gci"):
                    if re.search(r"\s-r(?:e(?:c(?:u(?:r(?:s(?:e)?)?)?)?)?)?\b|\s/s\b", segment, re.I):
                        return True
                elif re.search(r"(\s-\w*R\w*\b|\s/s\b)", segment):
                    return True
            return False
        if tool in ("Bash", "PowerShell") and root_search(cmd, tool == "PowerShell"):
            print(json.dumps(dict(hookSpecificOutput=dict(hookEventName="PreToolUse", permissionDecision="deny",
                permissionDecisionReason="Searching a whole drive or the filesystem root is blocked: it never finishes and leaks handles until "
                "Windows freezes. Search the specific folder instead (a repo under D:\\Projects\\MindAttic, or a known install folder), "
                "or use the Glob/Grep tools with a path."))))
            raise SystemExit(0)

        # Exempt commands (M.ORCH-T198): a turn typed as one of them (the context gate marks its session state) and a Skill
        # call naming one skip every pacing gate below: no pace delay, no subagent hold, no stop refusal. Each is logged as
        # "exempt" and the daemon is told to recount, so the spend still lands in the pace at once.
        {{ExemptPython}}
        def exempt_turn():
            sid = re.sub(r"[^A-Za-z0-9_.-]", "_", str(data.get("session_id") or ""))
            if not sid:
                return False
            for _ in range(20):
                try:
                    return bool(json.loads((Path.home() / ".claude" / "session-context" / (sid + ".state.json")).read_text()).get("exempt"))
                except PermissionError:
                    time.sleep(0.05)
                except Exception:
                    return False
            return False
        if exempt_turn() or (tool == "Skill" and exempt_skill((data.get("tool_input") or {}).get("skill"))):
            gate_log("exempt", 0)
            try:
                (Path.home() / ".claude" / "session-context" / "refresh.now").write_text(datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%S.%fZ"))
            except Exception:
                pass
            raise SystemExit(0)

        # Never past the limit, whatever else is set (/unthrottle included): at the window's target a call is held as long
        # as a hook may wait, in case the reset or a lower count lifts it, then refused rather than let through.
        start = time.monotonic()
        slot = None
        def release():
            # A subagent slot whose call never runs is given back at once, not left to expire.
            try:
                if slot is not None:
                    slot.unlink()
            except Exception:
                pass
        # The orchestrator's stop, or Claude's own percent at the line: a dead daemon can't switch the stop off (M.ORCH-T167),
        # and neither a stalled one nor an idle session's lower report lifts a stop already seen (latched, M.ORCH-T196).
        def stop_note():
            note = orchestrator_stop(status())
            if note:
                return note
            own = claude_stop()
            return claude_stop_note(own) if own else None
        def hold_at_stop():
            # Checked before every other hold and through each of them, so a call held for the pace delay or a subagent slot
            # when the stop comes on is held on and refused, never let through once its wait ends (M.ORCH-T196: in the
            # minutes after the 99% stop, calls already held each went through when their own wait ran out).
            if stop_note() is None:
                return
            while time.monotonic() - start < MAX_HOLD and stop_note() is not None:
                time.sleep(5)
            note = stop_note()
            if note is not None:
                release()
                gate_log("refused", time.monotonic() - start)
                refuse(note)
        hold_at_stop()

        # /unthrottle exempts one CLI (its process, so subagents and /clear included) from holds until it closes.
        MARKS = Path.home() / ".claude" / "session-context"
        if MARKS.exists() and any(MARKS.glob("unthrottled-*")):
            pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
            if pid is not None and (MARKS / ("unthrottled-" + str(pid))).exists():
                raise SystemExit(0)

        # A new subagent waits while the allowance the pace works out is used up (all CLIs together); the wait counts toward
        # the same MAX_HOLD, and past it the call is refused, never let through over the allowance (M.ORCH-T201: letting it
        # start anyway put 7 subagents on an allowance of 1, and a longer hold can't slow them: the cap stays under the
        # 5-minute prompt-cache TTL subagents use).
        # Agent calls sent in one message run this together, before any SubagentStart has marked one, so the check
        # and the slot it lets through are one step under a lock (M.ORCH-T197: ~20 got past an allowance of 1-2).
        # The slot counts until the context gate's SubagentStart turns it into the subagent's mark, or it expires.
        if tool in ("Agent", "Task"):
            SUB = MARKS / "subagents"
            SLOTS = MARKS / "{{SlotDirName}}"
            LOCK = MARKS / "{{SlotDirName}}.lock"
            def running(s):
                # The daemon's count leaves out marks whose stop never came; only marks newer than its count are added to it.
                marks = list(SUB.iterdir()) if SUB.exists() else []
                if s.get("subagents_running") is None:
                    return len(marks)
                at = datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00")).timestamp()
                return int(s["subagents_running"]) + sum(1 for m in marks if m.stat().st_mtime > at)
            def slots():
                # Slots let through but not started yet; one whose start never came (the call was refused) is dropped.
                held = 0
                for f in (SLOTS.iterdir() if SLOTS.exists() else []):
                    try:
                        if time.time() - f.stat().st_mtime > {{SlotLifeSeconds}}:
                            f.unlink()
                        else:
                            held += 1
                    except Exception:
                        pass
                return held
            def locked():
                # One pace gate at a time decides; a lock left by a killed gate is broken after a few seconds.
                MARKS.mkdir(parents=True, exist_ok=True)
                give_up = time.monotonic() + 15
                while time.monotonic() < give_up:
                    try:
                        os.close(os.open(str(LOCK), os.O_CREAT | os.O_EXCL | os.O_WRONLY))
                        return True
                    except FileExistsError:
                        try:
                            if time.time() - LOCK.stat().st_mtime > 10:
                                LOCK.unlink()
                        except Exception:
                            pass
                    except Exception:
                        return False
                    time.sleep(0.05)
                return False
            def unlock():
                try:
                    LOCK.unlink()
                except Exception:
                    pass
            def take():
                try:
                    SLOTS.mkdir(parents=True, exist_ok=True)
                    path = SLOTS / (str(os.environ.get("MINDATTIC_CLI_PID") or cli_pid() or 0) + "-" + str(time.time_ns()) + "-" + str(os.getpid()))
                    path.write_text("")
                    return path
                except Exception:
                    return None
            while True:
                has_lock = locked()
                try:
                    s = status()
                    allowed = s.get("subagent_allowance")
                    try:
                        busy = running(s) + slots() if allowed is not None else 0
                        go = allowed is None or busy < allowed
                    except Exception:
                        go = True
                    if go:
                        slot = take()
                        break
                finally:
                    if has_lock:
                        unlock()
                if time.monotonic() - start >= MAX_HOLD:
                    gate_log("refused", time.monotonic() - start)
                    refuse("The subagent allowance is full (" + str(busy) + " running or starting, " + str(allowed) + " allowed across every CLI), "
                           "so this subagent wasn't started. Don't start it now: work on in this turn, or end it, and try again after one "
                           "of the running subagents finishes and a slot frees.")
                time.sleep(5)
                hold_at_stop()
        try:
            while True:
                hold_at_stop()
                waited = time.monotonic() - start
                left = min(delay(), MAX_HOLD) - waited
                if left <= 0:
                    break
                if slot is not None:
                    try:
                        os.utime(slot)  # still held, not abandoned: other gates drop a slot past its life
                    except Exception:
                        pass
                time.sleep(min(left, 5))
            hold_at_stop()  # the last word before the call runs
        except BaseException:
            release()  # killed, failed or refused while held: the call doesn't run
            raise
        if slot is not None:
            try:
                os.utime(slot)  # its life starts when the call is let through, not when the slot was taken
            except Exception:
                pass
        if time.monotonic() - start >= 1:
            gate_log("held", time.monotonic() - start)
        """.Replace("\r\n", "\n");

    /// <summary>Where the pace gate keeps a slot for each Agent call it let through until its SubagentStart marks it
    /// (in <c>~/.claude/session-context</c>), so calls sent together can't all pass one free place (M.ORCH-T197).</summary>
    public const string SlotDirName = "subagent-slots";

    /// <summary>A slot whose SubagentStart hasn't come this long after its call was let through was never used.</summary>
    public const int SlotLifeSeconds = 120;

    /// <summary>The pace gate's event log in <c>~/.claude/session-context</c>: one JSON line per hold or refusal.</summary>
    public const string GateLogName = "pace-gate.jsonl";

    public const string ContextScriptName = "context-gate.py";

    /// <summary>
    /// The context gate: records each session's turn (busy on a prompt, idle on Stop) next to the
    /// context fill the statusline saves, and once a turn tells the main agent its context is past
    /// <paramref name="percent"/> so it wraps up and lets its subagents finish. The orchestrator then
    /// cycles it (<see cref="Sessions.ContextCycler"/>). With <paramref name="keepGoing"/>, a turn that ends asking whether
    /// to go on while its repo still has open tickets is told to carry on (<see cref="Settings.OrchestratorSettings.KeepGoing"/>).
    /// </summary>
    internal static string ContextScript(int percent, bool keepGoing = true) => $$$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's context gate (written by the orchestrator; edits are overwritten).
        # Records each session's turn state and warns the main agent once per turn when its context is
        # past the cycle point, so it finishes cleanly before the orchestrator saves, clears and reloads it.
        import json, os, re, sys, time
        from datetime import datetime, timezone
        from pathlib import Path

        DIR = Path.home() / ".claude" / "session-context"
        PERCENT = {{{percent}}}
        KEEP_GOING = {{{(keepGoing ? "True" : "False")}}}
        {{{WorkspacePython}}}

        # A turn ending on one of these is asking for a go-ahead the orchestrator gives itself.
        ASKS = re.compile(r"\b(shall i|should i|do you want|want me to|would you like|ready for me to|ok(?:ay)? to (?:start|continue|proceed|go)|"
                          r"let me know (?:if|when|whether)|(?:go ahead|proceed|continue|carry on|keep going)\?)", re.I)

        def autopilot_repos(pid):
            # What /autopilot typed in THIS CLI turned on (its repos, or "*"): it works them through its own subagents.
            # Never another CLI's: one you're using is never pushed onto tickets because its repo was named elsewhere.
            try:
                return [n.strip() for n in (DIR / ("autopilot-" + str(pid))).read_text().split(",") if n.strip()] if pid else []
            except Exception:
                return []

        def workable(repos):
            # The open fixes and approved tickets of repos (names or wildcards such as GTFO.*; "*": every repo), in ticket order.
            try:
                import fnmatch, sqlite3
                db = sqlite3.connect(str(WS / "MindAttic.Workflow" / "data" / "workflow.db"))
                try:
                    rows = db.execute("SELECT id, type, repo, summary FROM tickets WHERE status = 'open' ORDER BY ordinal").fetchall()
                except sqlite3.OperationalError:  # a database from before tickets had summaries
                    rows = [r + (None,) for r in db.execute("SELECT id, type, repo FROM tickets WHERE status = 'open' ORDER BY ordinal").fetchall()]
                # A ticket waiting on another open one isn't handed out by `orchestrator next`, so it isn't work to push on
                # to either: a 'requires' link, "blocked by X" in its summary, or settings' BlockedBy (as the scheduler reads them).
                open_ids = {r[0].upper() for r in rows}
                waits = {}
                try:
                    for f, t in db.execute("SELECT from_id, to_id FROM links WHERE kind = 'requires'").fetchall():
                        waits.setdefault(f.upper(), set()).add(t.upper())
                except Exception:
                    pass
                db.close()
                # Only fixes and tickets the user approved are work (orchestrator approve); the rest wait for their yes.
                try:
                    settings = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text())
                    approved = {a.upper() for a in settings.get("Approved") or []}
                except Exception:
                    settings, approved = {}, set()
                for f, ts in (settings.get("BlockedBy") or {}).items():
                    waits.setdefault(f.upper(), set()).update(t.upper() for t in ts or [])
                def blocked(r):
                    on = waits.get(r[0].upper(), set()) | {m.upper() for m in re.findall(r"blocked by ((?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)", r[3] or "", re.I)}
                    return any(b != r[0].upper() and b in open_ids for b in on)
                # A ticket `orchestrator next --claim` already handed to a subagent is being worked: it isn't left (M.ORCH-T188).
                try:
                    claimed = {(a.get("TicketId") or "").upper() for a in json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" /
                               "MindAttic.Orchestrator" / "assignments.json").read_text()) or []}
                except Exception:
                    claimed = set()
                named = {r.lower() for r in repos}
                return [r[0] for r in rows if any(fnmatch.fnmatchcase((r[2] or "").lower(), n) for n in named) and
                        ((r[1] or "").lower() == "fix" or r[0].upper() in approved) and r[0].upper() not in claimed and not blocked(r)]
            except Exception:
                return []

        def no_subagent_room(limits, pid):
            # The pace's subagent allowance is used up (all CLIs together), so `orchestrator next` would hand out nothing:
            # pushing a stop back would only loop (M.ORCH-T188). An unthrottled CLI isn't held to it, as `next` isn't.
            try:
                allowed = limits.get("subagent_allowance")
                if allowed is None or (pid and (DIR / ("unthrottled-" + str(pid))).exists()):
                    return False
                sub = DIR / "subagents"
                marks = list(sub.iterdir()) if sub.exists() else []
                if limits.get("subagents_running") is None:
                    running = len(marks)
                else:
                    at = datetime.fromisoformat(limits["timestamp"].replace("Z", "+00:00")).timestamp()
                    running = int(limits["subagents_running"]) + sum(1 for m in marks if m.stat().st_mtime > at)
                return running >= int(allowed)
            except Exception:
                return False

        def keep_going(repos, ids, sid, why, insisted, provider="Claude"):
            repo = ", ".join(repos)
            # Logs it to keep-going.jsonl (the daemon reports it); the reason to give the agent, or None when it's let stop.
            try:
                DIR.mkdir(parents=True, exist_ok=True)
                with open(DIR / "keep-going.jsonl", "a", encoding="utf-8") as log:
                    log.write(json.dumps({"at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "repo": repo, "session": sid,
                                          "provider": provider, "why": why, "open": len(ids), "action": "let stop" if insisted else "kept going"}) + "\n")
            except Exception:
                pass
            if insisted:
                return None
            return ("Don't stop: /autopilot is on in this CLI for " + repo + ", and " + str(len(ids)) + " workable ticket" +
                    ("" if len(ids) == 1 else "s") + " remain (" + ", ".join(ids[:6]) + ("..." if len(ids) > 6 else "") + "). "
                    "Run `orchestrator next --repos \"" + ",".join(repos) + "\" --claim` and hand each ticket it prints to a subagent with the "
                    "prompt it prints (several at once, in one message), then repeat. End your turn only when it hands out nothing, "
                    "or you're blocked on a decision only the user can make, and then say plainly what it is.")

        def orchestrator_status():
            # rl-status.json while the orchestrator keeps it fresh; {} when it's stale or someone else's.
            try:
                s = json.loads((Path.home() / ".claude" / "rl-status.json").read_text())
                age = (datetime.now(timezone.utc) - datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00"))).total_seconds()
                return s if s.get("source") == "MindAttic.Orchestrator" and age <= {{{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}} else {}
            except Exception:
                return {}

        def poke():
            # A turn (or a subagent) just ended, so its reply's tokens are in its transcript: the daemon recounts now
            # instead of on its next tick, so even a reply with no tool calls is in the pace within a second or two.
            try:
                DIR.mkdir(parents=True, exist_ok=True)
                (DIR / "refresh.now").write_text(datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ"))
            except Exception:
                pass

        # Typed at the window's stop, these still go through: they're how you steer the orchestrator itself.
        CONTROL = re.compile(r"/(autopilot|crossrepo|throttle|unthrottle|goal)\b", re.I)

        def goal_finished(transcript, goal):
            # A /goal ends on its own once its condition holds: the session's last entry is then the stop summary naming it
            # (an unmet goal keeps the turn going, so more follows). The summary lands after this hook's own Stop, so it's
            # checked later: on the next prompt here, and by the orchestrator's cycler (M.ORCH-T109).
            try:
                lines = Path(transcript).read_text(encoding="utf-8", errors="ignore").splitlines()[-60:]
            except Exception:
                return False
            for line in reversed(lines):
                try:
                    m = json.loads(line)
                except Exception:
                    continue
                if m.get("type") in ("assistant", "user", "system"):
                    return m.get("subtype") == "stop_hook_summary" and any(h.get("promptText") == goal for h in m.get("hookInfos") or [])
            return False

        def last_said(data):
            if data.get("last_assistant_message"):
                return str(data["last_assistant_message"])
            try:
                lines = Path(data.get("transcript_path") or "").read_text(encoding="utf-8", errors="ignore").splitlines()
            except Exception:
                return ""
            for line in reversed(lines[-200:]):
                try:
                    m = json.loads(line)
                except Exception:
                    continue
                if m.get("type") == "assistant":
                    parts = (m.get("message") or {}).get("content") or []
                    text = " ".join(p.get("text", "") for p in parts if isinstance(p, dict) and p.get("type") == "text")
                    if text.strip():
                        return text
            return ""

        # Hooks fire together (parallel tool calls, a Stop racing a SubagentStop). A read or replace Windows refuses while
        # another hook has the file open is retried: read as empty, the next save would wipe the session's goal and pid.
        def load(path):
            for _ in range(20):
                try:
                    return json.loads(path.read_text())
                except PermissionError:
                    time.sleep(0.05)
                except Exception:
                    return {}
            return {}

        def save(path, value):
            tmp = path.with_name(path.name + "." + str(os.getpid()) + ".tmp")
            tmp.write_text(json.dumps(value))
            for _ in range(20):
                try:
                    tmp.replace(path)
                    return
                except PermissionError:
                    time.sleep(0.05)
            try:
                tmp.unlink()
            except Exception:
                pass

        {{{CliPid}}}

        {{{ClaudeUsagePython}}}

        {{{ExemptPython}}}

        def my_pid():
            return os.environ.get("MINDATTIC_CLI_PID") or cli_pid()

        try:
            data = json.load(sys.stdin)
        except Exception:
            sys.exit(0)
        sid, event = data.get("session_id"), data.get("hook_event_name")
        # Codex (its Stop hook) and Gemini (AfterAgent) run this too, named on the command line: only keep-going applies
        # to them (their sessions aren't Claude's to cycle), and their own stop_hook_active guards the second stop.
        provider = sys.argv[1].capitalize() if len(sys.argv) > 1 else "Claude"
        if provider != "Claude":
            sys.exit(0)  # /autopilot runs in a Claude CLI; a Codex or Gemini CLI is never pushed onto tickets
        # Every running subagent has a mark, so the pace gate can hold new ones past the allowance.
        if event in ("SubagentStart", "SubagentStop"):
            if event == "SubagentStop":
                poke()
            if data.get("agent_id"):
                sub = DIR / "subagents"
                sub.mkdir(parents=True, exist_ok=True)
                mark = sub / re.sub(r"[^A-Za-z0-9_.-]", "_", str(data["agent_id"]))
                if event == "SubagentStart":
                    mark.write_text(str(cli_pid() or ""))
                    # The mark now counts it, so the slot the pace gate kept for its call is let go: this CLI's oldest. Never
                    # another CLI's: a subagent that took no slot (an /unthrottle'd CLI, an exempt turn) would free a place
                    # still held for that CLI's pending call; a slot left over expires.
                    try:
                        mine = str(my_pid() or 0) + "-"
                        held = sorted(f for f in (DIR / "{{{SlotDirName}}}").iterdir() if f.name.startswith(mine))
                        if held:
                            held[0].unlink()
                    except Exception:
                        pass
                elif mark.exists():
                    mark.unlink()
            sys.exit(0)
        if event == "Stop":
            poke()
        if not sid or data.get("agent_id"):
            sys.exit(0)  # subagents finish inside their parent's turn
        if event == "UserPromptSubmit":
            # At the window's stop a typed message is answered at once by this, not by the model: nothing is spent, so
            # the limit is never passed even by your own prompts. The orchestrator's control commands still go through.
            limits = orchestrator_status()
            if orchestrator_stop(limits) and not limits.get("stop"):
                limits = dict(limits, stop=True)  # latched from before the daemon stalled (M.ORCH-T196)
            own = None if limits.get("stop") else claude_stop()
            if own:
                limits = dict(limits, stop=True, percent_used=own[0], reset_time=datetime.fromtimestamp(own[1]).strftime("%I:%M %p").lstrip("0"))
            # So do the exempt commands (M.ORCH-T198): their turns skip every Orchestrator gate.
            if limits.get("stop") and not CONTROL.match((data.get("prompt") or "").strip()) and not exempt_prompt(data.get("prompt")):
                try:
                    DIR.mkdir(parents=True, exist_ok=True)
                    with open(DIR / "{{{GateLogName}}}", "a", encoding="utf-8") as log:
                        log.write(json.dumps({"at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "decision": "prompt-answered",
                                              "held": 0, "tool": None, "percent": limits.get("percent_used")}) + "\n")
                except Exception:
                    pass
                print(json.dumps({"decision": "block", "reason": "Not sent, so no tokens were spent: the 5-hour window is at its target "
                    "(MindAttic.Orchestrator never lets it pass the limit). Typing works again when it resets at " +
                    str(limits.get("reset_time") or "the end of the window") + ". /autopilot, /throttle and /goal still go through."}))
                sys.exit(0)
        DIR.mkdir(parents=True, exist_ok=True)
        state_file = DIR / (sid + ".state.json")
        state = load(state_file)
        now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        if event == "Stop":
            state.pop("exempt", None)  # an exempt command's turn ends here, and with it the exemption (M.ORCH-T198)
            # The orchestrator keeps development going without you: a turn that ends asking whether to go on, or that
            # closed a ticket and stopped, while its repo still has open tickets is told to carry on. One that stops
            # again straight after without doing anything is let go (something there really needs you), and so is one
            # past the cycle point. Each is logged to keep-going.jsonl, which the daemon reports.
            used = load(DIR / (sid + ".json")).get("used_percentage") or 0
            insisted = state.get("nudged") and not state.get("worked")
            why = None
            limits = orchestrator_status()
            # At the window's target nothing more may run, so a stop is never pushed back to work: the orchestrator's stop,
            # latched or fresh, or Claude's own percent at the line with the daemon down, as a typed prompt is held to.
            stopped = bool(limits.get("stop")) or orchestrator_stop(limits) is not None or claude_stop() is not None
            # Only a CLI that typed /autopilot is kept going, on its own repos, while they have workable tickets.
            repos = autopilot_repos(my_pid())
            if repos and KEEP_GOING and not stopped and not state.get("goal"):
                why = "autopilot"
            ids = workable(repos) if why and not no_subagent_room(limits, my_pid()) else []
            reason = keep_going(repos, ids, sid, why, insisted) if ids else None
            if reason and limits.get("wind_down_note"):
                reason += " " + limits["wind_down_note"]
            if reason:
                state.update(nudged=True, worked=False)
                state.pop("closed", None)
                save(state_file, state)
                print(json.dumps({"decision": "block", "reason": reason}))
                sys.exit(0)
            for k in ("nudged", "worked", "closed"):
                state.pop(k, None)
            state.update(state="idle", at=now, pid=cli_pid(), cwd=data.get("cwd"))
        elif event == "UserPromptSubmit":
            for k in ("nudged", "worked", "closed"):
                state.pop(k, None)
            if state.get("goal") and goal_finished(state.get("transcript"), state["goal"]):
                state.pop("goal", None)
            if data.get("transcript_path"):
                state["transcript"] = data["transcript_path"]
            state.update(state="busy", at=now, warned=False, cwd=data.get("cwd"))
            # A turn typed as an exempt command (/deploy, /run, ...) skips every pace gate until it stops (M.ORCH-T198).
            exempt = exempt_prompt(data.get("prompt"))
            if exempt:
                state["exempt"] = exempt
            else:
                state.pop("exempt", None)
            typed = (data.get("prompt") or "").strip()
            prompt = typed.lower()
            # /autopilot [off|all] [-repo=A,B]: THIS CLI works those repos' tickets through its own subagents (its own
            # repo when none are named) until /autopilot off here. Nothing is marked for any other CLI. A name may be a
            # wildcard (-repo=GTFO.*), kept as typed and matched wherever the repos are read (M.ORCH-T165).
            if prompt.startswith("/autopilot") and re.search(r"-{1,2}repos\b", typed, re.I):
                # The -repos alias is gone (it read as a different switch); say so rather than guess what was meant.
                print(json.dumps({"decision": "block", "reason": "/autopilot takes -repo=A,B (wildcards too: -repo=GTFO.*); -repos is gone. "
                    "Nothing changed: type it again with -repo."}))
                sys.exit(0)
            if prompt.startswith("/autopilot"):
                pid = my_pid()
                mark = DIR / ("autopilot-" + str(pid))
                args = typed[len("/autopilot"):].strip()
                names = autopilot_repos(pid)
                mine = repo_of(data.get("cwd") or "")
                m = re.search(r"-{1,2}repo\b\s*[=:]?\s*\[?([^\]]*)\]?", args, re.I)
                named = [r.strip() for r in m.group(1).replace(" ", ",").split(",") if r.strip() and r.strip().lower() not in ("on", "off", "this", "all")] if m else ([mine] if mine else [])
                lower = {n.lower() for n in named}
                if re.match(r"off\b", args, re.I):
                    names = [] if re.match(r"off\s+all\b", args, re.I) or not m else [n for n in names if n.lower() not in lower]
                elif re.match(r"all\b", args, re.I):
                    names = ["*"]
                else:
                    names = [n for n in names if n != "*" and n.lower() not in lower] + named
                try:
                    (DIR / "autopilot.order").unlink()  # the old workspace-wide list: nothing reads it any more
                except Exception:
                    pass
                if re.match(r"off\s+all\b", args, re.I):
                    for other in DIR.glob("autopilot-*"):  # off everywhere: every CLI's
                        try:
                            other.unlink()
                        except Exception:
                            pass
                if pid and names:
                    mark.write_text(",".join(names))
                elif mark.exists():
                    mark.unlink()
            # /goal <condition> runs turn after turn until its condition holds; cycling would end it, so the session is
            # marked and left to Claude's own compaction until /goal clear (M.ORCH-T93).
            if re.match(r"/goal\b", prompt):
                args = typed[len("/goal"):].strip()
                if not args or re.match(r"(clear|off|stop)\b", args, re.I):
                    state.pop("goal", None)
                else:
                    state["goal"] = args
            # /crossrepo <A,B|all> lets this CLI work other repos too (the explicit exception to M.ORCH-T95); /crossrepo off ends it.
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
            # Each turn starts knowing how many subagents the pace allows right now, and whether the window is winding down.
            s = orchestrator_status()
            notes = [n for n in (s.get("subagent_note"), s.get("stop_note") or s.get("wind_down_note")) if n]
            state["wind_down_told"] = bool(s.get("wind_down_note") or s.get("stop_note"))
            out = {"hookSpecificOutput": {"hookEventName": "UserPromptSubmit", "additionalContext": " ".join(notes)}} if notes else {}
            # A dead or stalled daemon is said to you, on every prompt, until it's back (M.ORCH-T167): nothing is paced
            # meanwhile except Claude's own 99% stop.
            down = daemon_down()
            if down is not None:
                out["systemMessage"] = ("MindAttic.Orchestrator hasn't updated the pace for " + str(int(down // 60)) + " min: its daemon "
                    "isn't running, so agents aren't being paced (only Claude's own " + str(STOP_PERCENT) + "% stop still holds). "
                    "Start it: C:\\Apps\\MindAttic.Orchestrator\\MindAttic.Orchestrator.exe")
            if out:
                print(json.dumps(out))
        elif event == "PostToolUse":
            touched = False
            # Running `orchestrator next` only asks for work: when it hands out nothing, stopping again is insisting (M.ORCH-T188).
            asked = re.search(r"\borchestrator(?:\.exe)?['\"]?\s+next\b", str((data.get("tool_input") or {}).get("command") or ""))
            if state.get("nudged") and not state.get("worked") and not asked:
                state["worked"] = touched = True  # it carried on after being told to
            # A ticket closed this turn: stopping right after it, with more open, is a stop keep-going catches.
            closed = re.search(r"\bworkflow(?:\.exe)?['\"]?\s+ticket\s+close\s+['\"]?((?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)",
                               str((data.get("tool_input") or {}).get("command") or ""))
            if closed and closed.group(1) not in state.get("closed", []):
                state["closed"] = state.get("closed", []) + [closed.group(1)]
                touched = True
            if touched:
                save(state_file, state)
            said = []
            # A turn that crosses the window's wind-down line (a long autopilot run has only the one prompt) hears it once.
            if not state.get("wind_down_told"):
                s = orchestrator_status()
                if s.get("wind_down_note"):
                    state["wind_down_told"] = True
                    said.append(s["wind_down_note"])
            # No context warning any more: nothing quicksaves, clears or reloads a session for it (2026-10-16), so a turn
            # is never told to stop for its context; Claude's own compaction handles a full one.
            if not said:
                sys.exit(0)
            print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": " ".join(said)}}))
        else:
            sys.exit(0)
        save(state_file, state)
        """.Replace("\r\n", "\n");

    /// <summary>Writes the scripts and registers their hooks when anything is missing or out of date.
    /// Returns what it did, or null when everything was already in place.</summary>
    public static string? Ensure(string? claudeDir = null, int contextPercent = 80, bool keepGoing = true)
    {
        var dir = claudeDir ?? ClaudeDir;
        Directory.CreateDirectory(dir);
        var did = new List<string>();

        var script = Path.Combine(dir, ScriptName);
        var contextScript = Path.Combine(dir, ContextScriptName);
        Directory.CreateDirectory(Path.Combine(dir, "commands"));
        foreach (var (path, body) in new[]
        {
            (script, Script), (contextScript, ContextScript(contextPercent, keepGoing)),
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
        // Claude's own usage reaches the orchestrator only through a statusline redraw; an idle tab redraws on this
        // timer too, so the real percent stays fresh while any tab is open (M.ORCH-T125).
        if (root["statusLine"] is JsonObject statusLine && statusLine["refreshInterval"] is null)
        {
            statusLine["refreshInterval"] = StatusLineRefreshSeconds;
            changed = true;
        }
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

        var codexHooks = Path.Combine(codexDir ?? CodexDir, "hooks.json");
        var geminiSettings = Path.Combine(geminiDir ?? GeminiDir, "settings.json");
        if (MergeJsonHook(codexHooks, "PreToolUse", ScriptName, command, HookTimeoutSeconds) is { } c) did.Add("Codex: " + c);
        if (MergeJsonHook(geminiSettings, "BeforeTool", ScriptName, command, HookTimeoutSeconds * 1000) is { } g) did.Add("Gemini: " + g);
        if (AppendKimiHook(kimiConfigPath ?? KimiConfigPath, command, HookTimeoutSeconds)) did.Add("Kimi: wired PreToolUse into config.toml");
        // Keep development going: Codex's Stop and Gemini's AfterAgent take the same "carry on" as Claude's Stop. Kimi
        // has no end-of-turn hook, so its CLIs can stop to ask (README says so).
        var contextScript = Path.Combine(claudeDir ?? ClaudeDir, ContextScriptName);
        if (File.Exists(contextScript))
        {
            if (MergeJsonHook(codexHooks, "Stop", ContextScriptName, Command(contextScript) + " codex", 10) is { } cs) did.Add("Codex: " + cs);
            if (MergeJsonHook(geminiSettings, "AfterAgent", ContextScriptName, Command(contextScript) + " gemini", 10_000) is { } ga) did.Add("Gemini: " + ga);
        }

        return did.Count > 0 ? string.Join(", ", did) : null;
    }

    /// <summary>Adds <paramref name="hookEvent"/> to the JSON hook file at <paramref name="path"/> (created fresh if
    /// missing), reusing <see cref="AddHook"/>'s idempotent merge; null when already wired in or the file is
    /// someone else's unreadable JSON.</summary>
    private static string? MergeJsonHook(string path, string hookEvent, string scriptName, string command, int timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonObject? root;
        try { root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : new JsonObject(); }
        catch (JsonException) { return null; }
        if (root is null || !AddHook(root, hookEvent, scriptName, command, timeout)) return null;
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
            if (hook["command"] is JsonValue cv && cv.TryGetValue<string>(out var c) && c.Contains(scriptName, StringComparison.OrdinalIgnoreCase))
            {
                if (c == command && hook["timeout"] is JsonValue tv && tv.TryGetValue<int>(out var t) && t == timeout) return false;
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
