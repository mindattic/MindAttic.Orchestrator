using MindAttic.Orchestrator.Scheduling;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The heavy-process lease's half of the pace gate (M.ORCH-T1680). The agent standard says "one heavy process per machine:
/// one game player, one Unity/Unreal build, or one long soak at a time", and nothing enforced it: parallel players and engine
/// builds once grew the pagefile to 43 GB. <see cref="Python"/> is spliced into <see cref="AgentGate.Script"/>: before a shell
/// command runs, it's matched against a list of heavy commands (<see cref="DefaultPatternsJson"/>, plus the repo's own
/// <see cref="RepoPatternsFile"/>), and one that matches must take a machine-wide lease first (a <c>heavy-N</c> row of
/// orchestrator.db's <c>leases</c>, see <see cref="HeavyLease"/>). Held by someone else, the call is refused with what is
/// running and who holds it; the holder is never killed. Any error lets the call through.
/// </summary>
public static class HeavyGate
{
    /// <summary>A repo's own heavy commands, relative to its root: the same shape as <see cref="DefaultPatternsJson"/>, added
    /// to the default list, never replacing it.</summary>
    public const string RepoPatternsFile = ".agent/heavy-patterns.json";

    /// <summary>The setting the hook reads from orchestrator.db's <c>settings</c>: how many heavy processes may run at once
    /// (<see cref="Settings.OrchestratorSettings.HeavyProcessLimit"/>; 0 turns the lease off).</summary>
    public const string LimitSetting = nameof(Settings.OrchestratorSettings.HeavyProcessLimit);

    /// <summary>
    /// The default heavy commands, as data: the same shape as the test gate's <c>test-gate-patterns.json</c>. Each
    /// <c>regex</c> is a case-insensitive Python regex. <c>at</c> is <c>command</c> (the default: matched at the start of
    /// each command in a shell call, once wrappers such as quotes, <c>&amp;</c>, <c>cmd /c</c>, <c>powershell -Command</c>,
    /// <c>Start-Process</c> and an interpreter are stripped and the program's folder dropped), <c>program</c> (matched at
    /// the start of each command with the program's folder kept, for a rule about where the program lives) or
    /// <c>anywhere</c> (searched for in each whole command). It lives here, in the generated hook, rather than in the
    /// agent-standard repo's <c>shared/hooks</c>, because the pace gate is generated from this repo alone.
    /// </summary>
    public const string DefaultPatternsJson = """
        {
          "patterns": [
            { "name": "the Unity editor or a Unity batchmode build", "regex": "unity(?:\\.exe)?[\"']?(?=\\s|$)(?!\\s+hub)" },
            { "name": "a -batchmode build or player", "regex": "\\.exe[\"']?\\s[^\\n]*-batchmode\\b", "at": "anywhere" },
            { "name": "an Unreal RunUAT build", "regex": "RunUAT(?:\\.bat|\\.sh|\\.command)?(?=[\\s\"']|$)" },
            { "name": "an Unreal build script", "regex": "BatchFiles[\\\\/](?:\\w+[\\\\/])?(?:Build|Rebuild|RunUBT)\\.(?:bat|sh|command)\\b", "at": "anywhere" },
            { "name": "the Unreal editor", "regex": "(?:UnrealEditor|UE[45]Editor)(?:-[\\w-]+)?(?:\\.exe)?(?=[\\s\"']|$)" },
            { "name": "an Unreal build tool", "regex": "(?:UnrealBuildTool|AutomationTool)(?:\\.exe)?(?=[\\s\"']|$)" },
            { "name": "an Unreal build tool", "regex": "dotnet(?:\\.exe)?[\"']?\\s+\\S*(?:UnrealBuildTool|AutomationTool)\\.dll\\b" },
            { "name": "s&box", "regex": "sbox(?:-dev|-server|-standalone)?(?:\\.exe)?(?=[\\s\"']|$)" },
            { "name": "a built game player", "regex": "[^\\s\"']*(?:Builds?|Binaries[\\\\/]Win64|Windows(?:NoEditor|Client|Server)|StandaloneWindows(?:64)?|StagedBuilds[\\\\/][^\\\\/\\s\"']+)[\\\\/](?:[^\\\\/\\s\"']+[\\\\/])?[^\\\\/\\s\"']+\\.exe\\b", "at": "program" },
            { "name": "a soak runner", "regex": "[\\w.-]*soak[\\w.-]*\\.(?:ps1|sh|bat|cmd|py)\\b" },
            { "name": "a balance runner", "regex": "[\\w.-]*balance[\\w.-]*\\.(?:ps1|sh|bat|cmd|py)\\b" },
            { "name": "a harness soak (campaign/boardaudit)", "regex": "harness\\.py[\"']?\\s+(?:campaign|boardaudit|soak)\\b" }
          ]
        }
        """;

    /// <summary>The environment variable naming a JSON file of <c>[pid, started epoch, command line]</c> rows the hook takes
    /// as the machine's processes instead of the real ones, so its lease liveness can be run against a made-up machine.</summary>
    public const string FakeProcessesEnvVar = "MINDATTIC_FAKE_PROCESSES";

    /// <summary>The environment variable that overrides <see cref="LimitSetting"/> for one hook run.</summary>
    public const string LimitEnvVar = "MINDATTIC_HEAVY_LIMIT";

    /// <summary>
    /// The Python spliced into the pace gate. It needs <c>json</c>, <c>os</c>, <c>re</c>, <c>time</c> and <c>Path</c>, and
    /// from the script around it <c>ORCH_DB</c>, <c>db_connect</c>, <c>orch_settings</c>, <c>cli_pid</c> and
    /// <c>process_started</c>. <c>heavy_gate(data)</c> returns the refusal for a tool call, or None to let it through
    /// (holding a lease when the command is heavy).
    /// </summary>
    internal static string Python => Template
        .Replace("__HEAVY_DEFAULT_PATTERNS__", DefaultPatternsJson.Replace("\r\n", "\n"))
        .Replace("__HEAVY_REPO_FILE__", RepoPatternsFile)
        .Replace("__HEAVY_PREFIX__", HeavyLease.Prefix)
        .Replace("__HEAVY_GRACE__", HeavyLease.GraceSeconds.ToString())
        .Replace("__HEAVY_UNKNOWN_MAX__", HeavyLease.UnknownMaxSeconds.ToString())
        .Replace("__HEAVY_SLACK__", HeavyLease.SlackSeconds.ToString())
        .Replace("__HEAVY_LIMIT_SETTING__", LimitSetting)
        .Replace("__HEAVY_LIMIT_DEFAULT__", Settings.OrchestratorSettings.DefaultHeavyProcessLimit.ToString())
        .Replace("__HEAVY_LIMIT_ENV__", LimitEnvVar)
        .Replace("__HEAVY_FAKE_ENV__", FakeProcessesEnvVar)
        .Replace("\r\n", "\n");

    private const string Template = """
        # The heavy-process lease (M.ORCH-T1680): one game player, engine build or long soak per machine at a time (parallel
        # ones once grew the pagefile to 43 GB). A shell command that matches a heavy pattern must hold a machine-wide lease,
        # a heavy-N row of orchestrator.db's leases, before it runs. The row's holder is JSON: who took it (session, subagent,
        # CLI pid), the command, and, once found, the processes running it. A lease is free once those are gone (each pid
        # checked with its start time), so a crashed holder never blocks the machine. Nothing here kills anything, and any
        # error lets the call through.
        HEAVY_DEFAULT = json.loads(r'''__HEAVY_DEFAULT_PATTERNS__''')
        HEAVY_PREFIX = "__HEAVY_PREFIX__"
        HEAVY_GRACE = __HEAVY_GRACE__              # seconds a lease just taken counts as held before its process must be found
        HEAVY_UNKNOWN_MAX = __HEAVY_UNKNOWN_MAX__  # ... and how long it counts while the machine's processes can't be listed
        HEAVY_SLACK = __HEAVY_SLACK__              # a lease's process may have started this many seconds before its lease's time

        def heavy_repo_root(cwd):
            try:
                p = Path(cwd).resolve() if cwd else None
                while p and p != p.parent:
                    if (p / ".git").exists():
                        return p
                    p = p.parent
            except Exception:
                pass
            return None

        def heavy_patterns(cwd):
            # (name, compiled regex, at) for the default list and the repo's own .agent/heavy-patterns.json, which adds to it.
            sources = [HEAVY_DEFAULT]
            root = heavy_repo_root(cwd)
            if root:
                try:
                    sources.append(json.loads((root / "__HEAVY_REPO_FILE__").read_text(encoding="utf-8-sig")))
                except Exception:
                    pass
            out = []
            for source in sources:
                for p in (source.get("patterns") or []) if isinstance(source, dict) else []:
                    try:
                        out.append((p.get("name") or p["regex"], re.compile(p["regex"], re.I), p.get("at", "command")))
                    except Exception:
                        continue  # one bad pattern never disables the rest
            return out

        # What stands in front of the program a command really runs (as the test gate strips them, MAT-T121).
        HEAVY_WRAPPERS = [
            r"[\"'`(){}]+",                                         # quotes, subshells, script blocks
            r"&\s*",                                                # PowerShell call operator
            r"\.\s+(?=\S)",                                         # dot-sourcing
            r"(?:call|time|exec|nohup|start(?:\s+/\w+)*)\s+",
            r"(?:Start-Process|saps)(?:\s+-(?:FilePath|NoNewWindow|Wait|PassThru)\b)*\s+",
            r"env(?:\s+-\S+)*\s+",
            r"[A-Za-z_][A-Za-z0-9_]*=(?:\"[^\"]*\"|'[^']*'|\S*)\s+",  # VAR=x cmd
            r"\$env:[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?:\"[^\"]*\"|'[^']*'|\S*)\s*",
            r"cmd(?:\.exe)?\s+/[ckCK]\s+",
            r"(?:powershell|pwsh)(?:\.exe)?(?:\s+-(?:NoProfile|nop|NoLogo|NonInteractive|noni|NoExit|Sta|Mta)\b"
            r"|\s+-(?:ExecutionPolicy|ep|ex|WindowStyle|w|WorkingDirectory|wd|OutputFormat|of|InputFormat|if|Version|v)\s+\S+)*"
            r"(?:\s+-(?:c|command|file|f)\b)?\s+(?=[^\s-])",
            r"(?:python[\d.]*|py|node|ruby|perl)(?:\.exe)?(?:\s+-(?!m\b)[A-Za-z]+\b)*\s+(?=[^\s-])",  # interpreter running a script
            r"(?:ba|z|da)?sh(?:\.exe)?(?:\s+-\w*c\w*|\s+-[a-z]+)*\s+(?=[\"'])",
            r"(?:ba|z|da)?sh(?:\.exe)?\s+(?=\S+\.sh\b)",
            r"(?:npx|pnpx|bunx)(?:\.cmd)?(?:\s+-\S+)*\s+",
            r"(?:yarn\s+dlx|pnpm\s+(?:exec|dlx)|npm\s+exec(?:\s+--)?|uv\s+run|poetry\s+run|pipenv\s+run|hatch\s+run|dotnet\s+tool\s+run)\s+",
        ]
        HEAVY_WRAPPER_RX = re.compile(r"\s*(?:" + "|".join(HEAVY_WRAPPERS) + r")", re.I)

        def heavy_segments(command):
            # Each command a shell call runs, wrappers stripped: (the program's folder dropped, the program's folder kept).
            # A heredoc's or here-string's body is data (a file's text, a commit message), not commands.
            c = re.sub(r"<<-?\s*([\"']?)(\w+)\1[^\n]*\n.*?\n\s*\2\s*(?=\n|$)", "\n", command, flags=re.S)
            c = re.sub(r"@([\"'])\s*\n.*?\n\1@", "\n", c, flags=re.S)
            for raw in re.split(r"[;\n|]|&&|\$\(|\b(?:then|do|else)\b", c):
                seg = raw
                for _ in range(12):
                    m = HEAVY_WRAPPER_RX.match(seg)
                    if not m or m.end() == 0:
                        break
                    seg = seg[m.end():]
                seg = seg.strip()
                if not seg:
                    continue
                full = seg
                # Drop the program's folder: C:\x\Unity.exe -> Unity.exe, ./tools/soak.ps1 -> soak.ps1, even quoted with spaces.
                m = re.match(r"(?:[A-Za-z]:)?(?:[^\s\\/\"']*[\\/]|[^\\/\"'\n]*[\\/](?=[^\"'\n]*[\"']))+", seg)
                if m and m.end() < len(seg):
                    seg = seg[m.end():]
                yield seg, full

        def heavy_match(command, patterns):
            # (name, regex, at) of the first heavy pattern the command runs, or None.
            for seg, full in heavy_segments(str(command or "")):
                for name, rx, at in patterns:
                    if rx.search(full) if at == "anywhere" else rx.match(full) if at == "program" else rx.match(seg):
                        return name, rx.pattern, at
            return None

        def heavy_command(tool_input):
            if not isinstance(tool_input, dict):
                return None
            for key in ("command", "cmd", "script"):
                v = tool_input.get(key)
                if isinstance(v, list):
                    v = " ".join(str(x) for x in v)
                if isinstance(v, str) and v.strip():
                    return v
            return None

        def heavy_fake():
            # A made-up machine for trying the lease out: [pid, started epoch, command line] rows; None on a real one.
            path = os.environ.get("__HEAVY_FAKE_ENV__")
            if not path:
                return None
            try:
                return [(int(p[0]), float(p[1]), str(p[2])) for p in json.loads(Path(path).read_text(encoding="utf-8-sig"))]
            except Exception:
                return []

        def heavy_alive(pid, started):
            # Whether the process that had this pid when the lease was written still runs: a reused pid started at another time.
            try:
                if pid is None:
                    return False
                fake = heavy_fake()
                if fake is not None:
                    return any(p[0] == int(pid) and (started is None or abs(p[1] - float(started)) < 1) for p in fake)
                at = process_started(int(pid))
                return at is not None and (started is None or abs(at - float(started)) < 1)
            except Exception:
                return False

        def heavy_processes():
            # Every process on the machine as (pid, started epoch, command line); None when they can't be listed. A second
            # or two of PowerShell, so only asked once a lease is past its grace with no process known for it.
            fake = heavy_fake()
            if fake is not None:
                return fake
            try:
                import subprocess
                ps = ("Get-CimInstance Win32_Process | ForEach-Object { if ($_.CreationDate -and $_.CommandLine) { "
                      "'{0}|{1}|{2}' -f $_.ProcessId, ([DateTimeOffset]$_.CreationDate).ToUnixTimeMilliseconds(), "
                      "($_.CommandLine -replace '\\s+', ' ') } }")
                done = subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-Command", ps], stdin=subprocess.DEVNULL,
                                      stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=20, creationflags=0x08000000)
                if done.returncode != 0:
                    return None
                rows = []
                for line in done.stdout.decode("utf-8", errors="replace").splitlines():
                    parts = line.split("|", 2)
                    if len(parts) == 3 and parts[0].strip().isdigit() and parts[1].strip().lstrip("-").isdigit():
                        rows.append((int(parts[0]), int(parts[1]) / 1000.0, parts[2]))
                return rows or None
            except Exception:
                return None

        def heavy_state(h, now, alive=None, processes=None):
            # ("live" | "free", the processes found for it or None). Live while a process it's known to run is alive. Before
            # one is known ("starting"): while the gate that took it still runs (it may hold the call for the pace), then for
            # HEAVY_GRACE seconds while its CLI is alive; after that the machine's processes are asked for one running the
            # lease's command that started since the lease did: found, the lease is that process's; none, the call is over.
            alive = alive or heavy_alive
            if any(alive(p[0], p[1]) for p in (h.get("procs") or [])):
                return "live", None
            if h.get("phase") != "starting":
                return "free", None
            if h.get("gate") and alive(h.get("gate"), h.get("gate_started")):
                return "live", None
            if h.get("cli") and not alive(h.get("cli"), h.get("cli_started")):
                return "free", None
            at = float(h.get("at") or 0)
            if now - at < HEAVY_GRACE:
                return "live", None
            listed = (processes or heavy_processes)()
            if listed is None:
                return ("live" if now - at < HEAVY_UNKNOWN_MAX else "free"), None
            try:
                pattern = [(h.get("pattern"), re.compile(h["regex"], re.I), h.get("mode") or "command")]
            except Exception:
                return "free", None
            found = [[int(pid), float(started)] for pid, started, line in listed
                     if started >= at - HEAVY_SLACK and int(pid) != os.getpid() and heavy_match(line, pattern)]
            return ("live", found) if found else ("free", None)

        def heavy_limit():
            # How many heavy processes may run at once; 0 turns the lease off.
            v = os.environ.get("__HEAVY_LIMIT_ENV__")
            if v is None or not str(v).strip():
                v = orch_settings().get("__HEAVY_LIMIT_SETTING__", __HEAVY_LIMIT_DEFAULT__)
            return int(v)

        def heavy_rows(db):
            out = []
            for name, raw in db.execute("SELECT name, holder FROM leases WHERE name LIKE ? ORDER BY name", (HEAVY_PREFIX + "%",)).fetchall():
                try:
                    h = json.loads(raw)
                except Exception:
                    h = None
                out.append((name, raw, h if isinstance(h, dict) else {}))
            return out

        def heavy_short(text, limit=160):
            text = re.sub(r"\s+", " ", str(text or "")).strip()
            return text if len(text) <= limit else text[:limit - 3] + "..."

        def heavy_who(h):
            who = []
            if h.get("repo"):
                who.append("repo " + str(h["repo"]))
            if h.get("login"):
                who.append("login " + str(h["login"]))
            if h.get("session"):
                who.append("session " + str(h["session"])[:8])
            who.append(("subagent " + heavy_short(h.get("agent_type") or h.get("agent"), 40)) if h.get("agent") else "its main agent")
            if h.get("cli"):
                who.append("CLI pid " + str(h["cli"]))
            return ", ".join(who)

        def heavy_refusal(rows, limit, now):
            running = []
            for _, _, h in rows:
                minutes = int(max(0.0, now - float(h.get("at") or now)) // 60)
                running.append((h.get("pattern") or "a heavy process") + ": `" + heavy_short(h.get("command")) + "` (lease taken " +
                               ("under a minute" if minutes < 1 else str(minutes) + " min") + " ago; held by " + heavy_who(h) + ")")
            return ("HEAVY-PROCESS LEASE (M.ORCH-T1680): " + ("one heavy process runs" if limit == 1 else str(limit) + " heavy processes run") +
                    " on this machine at a time (a game player, a Unity/Unreal/s&box build or editor, a long soak): parallel ones "
                    "once grew the pagefile to 43 GB. Running now: " + "; ".join(running) + ". This command wasn't started. Don't "
                    "kill or interrupt what's running, and don't ask the user: do other work, check back on a background timer "
                    "every minute or two (`orchestrator clis` shows who holds the lease), and run this again once it has ended.")

        HEAVY_TAKEN = [None]  # (name, holder JSON) of the lease this gate took
        def heavy_touch():
            # The gate is done with the call (it may have held it for minutes): the lease's grace runs from now.
            taken = HEAVY_TAKEN[0]
            if not taken:
                return
            try:
                import sqlite3
                h = json.loads(taken[1])
                h["at"] = time.time()
                h["gate"] = None
                db = sqlite3.connect(ORCH_DB, timeout=2, isolation_level=None)
                try:
                    db.execute("UPDATE leases SET holder = ?, taken_at = ? WHERE name = ? AND holder = ?", (json.dumps(h), h["at"], taken[0], taken[1]))
                finally:
                    db.close()
            except Exception:
                pass

        def heavy_take(data, command, cwd, hit, limit):
            # Takes a lease for this call, or returns the refusal when every one is held by someone else.
            if not os.path.isfile(ORCH_DB):
                return None
            now = time.time()
            cli = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
            cli = int(cli) if cli else None
            session = str(data.get("session_id") or data.get("sessionId") or data.get("conversationId") or ("cli-" + str(cli)))
            agent = str(data.get("agent_id") or data.get("agentId") or "")
            db = db_connect(2, isolation_level=None)
            try:
                # Which leases are over, and which found their process, is worked out before the write transaction: listing
                # the machine's processes takes a second or two, and the database isn't held for it.
                listing = []
                def processes():
                    if not listing:
                        listing.append(heavy_processes())
                    return listing[0]
                verdicts = [(name, raw, h) + heavy_state(h, now, processes=processes) for name, raw, h in heavy_rows(db)]
                # Who is asking, read before the transaction too: the account's record may itself be written through the database.
                root = heavy_repo_root(cwd)
                login = None
                try:
                    login = account_tag()
                except Exception:
                    pass
                holder = dict(session=session, agent=agent, agent_type=str(data.get("agent_type") or data.get("subagent_type") or ""),
                              cli=cli, cli_started=process_started(cli) if cli else None,
                              gate=os.getpid(), gate_started=process_started(os.getpid()),
                              repo=root.name if root else "", cwd=str(cwd or ""), login=str(login or ""),
                              tool=str(data.get("tool_name") or ""), command=str(command)[:2000],
                              pattern=hit[0], regex=hit[1], mode=hit[2], phase="starting", at=now, procs=[])
                refusal = None
                db.execute("BEGIN IMMEDIATE")
                try:
                    for name, raw, h, state, found in verdicts:
                        if state == "free":
                            db.execute("DELETE FROM leases WHERE name = ? AND holder = ?", (name, raw))
                        elif found:
                            bound = dict(h, phase="running", gate=None, procs=found)
                            db.execute("UPDATE leases SET holder = ?, pid = ?, started = ? WHERE name = ? AND holder = ?",
                                       (json.dumps(bound), found[0][0], found[0][1], name, raw))
                    rows = heavy_rows(db)  # a row another gate wrote meanwhile is one just taken: held
                    mine = [r for r in rows if str(r[2].get("session") or "") == session and str(r[2].get("agent") or "") == agent]
                    if not mine and len(rows) >= limit:
                        refusal = heavy_refusal(rows, limit, now)
                    else:
                        if mine:
                            # The same agent's next heavy command (a harness driving the player it started): its own lease
                            # is kept, with what it already runs still counted.
                            name = mine[0][0]
                            holder["procs"] = list(mine[0][2].get("procs") or [])
                        else:
                            used = {r[0] for r in rows}
                            name = next(HEAVY_PREFIX + str(i) for i in range(1, limit + 1) if HEAVY_PREFIX + str(i) not in used)
                        text = json.dumps(holder)
                        db.execute("INSERT OR REPLACE INTO leases (name, holder, pid, started, taken_at) VALUES (?, ?, ?, ?, ?)",
                                   (name, text, cli or os.getpid(), holder["cli_started"] if cli else holder["gate_started"], now))
                        HEAVY_TAKEN[0] = (name, text)
                    db.execute("COMMIT")
                except Exception:
                    HEAVY_TAKEN[0] = None
                    try:
                        db.execute("ROLLBACK")
                    except Exception:
                        pass
                    raise
            finally:
                db.close()
            if HEAVY_TAKEN[0]:
                import atexit
                atexit.register(heavy_touch)
            return refusal

        def heavy_gate(data):
            # The refusal for this tool call, or None to let it through. Any error lets it through.
            try:
                if not isinstance(data, dict):
                    return None
                command = heavy_command(data.get("tool_input"))
                if not command:
                    return None
                cwd = data.get("cwd") or os.getcwd()
                hit = heavy_match(command, heavy_patterns(cwd))
                if not hit:
                    return None
                limit = heavy_limit()
                if limit <= 0:
                    return None
                return heavy_take(data, command, cwd, hit, limit)
            except Exception:
                return None
        """;
}
