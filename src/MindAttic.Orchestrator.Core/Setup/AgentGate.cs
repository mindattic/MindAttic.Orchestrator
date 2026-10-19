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
    /// <para>All of that is one account's usage (<see cref="Budget.ClaudeAccount"/>). Only what's tied to the account signed in
    /// now counts: a latch is kept per account, a <c>claude-usage.json</c> reading must be that account's once another has been
    /// signed in, and an orchestrator status from before the switch is the old account's. A /login to an account with room
    /// left used to stay locked out at the old one's weekly stop until it reset; going back to it brings its stop back.</para>
    /// <para>A CLI billed elsewhere (an API key, a gateway, a cloud provider: <c>session_auth</c>) spends none of the
    /// subscription's windows, so no stop or pace holds it.</para>
    /// </summary>
    internal static string ClaudeUsagePython => $$"""
        STOP_PERCENT = {{StopPercent}}
        LATCH = Path.home() / ".claude" / "session-context" / "{{StopLatchName}}"
        ACCOUNT = Path.home() / ".claude" / "session-context" / "{{Budget.ClaudeAccount.FileName}}"
        HISTORY_KEPT = {{(int)Budget.ClaudeAccount.HistoryKept.TotalSeconds}}
        def read_json(path):
            # A file being replaced can't be opened for a moment on Windows: that's a retry, not a missing file.
            for _ in range(20):
                try:
                    return json.loads(path.read_text())
                except PermissionError:
                    time.sleep(0.05)
            raise PermissionError(str(path))
        def write_json(path, value):
            tmp = path.with_name(path.name + "." + str(os.getpid()) + ".tmp")
            try:
                path.parent.mkdir(parents=True, exist_ok=True)
                tmp.write_text(json.dumps(value))
                for _ in range(20):
                    try:
                        os.replace(tmp, path)
                        return
                    except PermissionError:
                        time.sleep(0.05)
            except Exception:
                pass
            try:
                tmp.unlink()
            except Exception:
                pass
        def claude_config():
            try:
                config = json.loads((Path.home() / ".claude.json").read_text(encoding="utf-8", errors="ignore"))
                return config if isinstance(config, dict) else {}
            except Exception:
                return {}
        def config_account(config):
            # Keyed as ClaudeAccount.Current: a Claude login by account and organization, an API key as "{{Budget.ClaudeAccount.ApiKey}}"; None signed out.
            text = lambda o, k: o.get(k) if isinstance(o, dict) and isinstance(o.get(k), str) and o.get(k) else None
            oauth = config.get("oauthAccount")
            if text(oauth, "accountUuid"):
                return text(oauth, "accountUuid") + ("/" + text(oauth, "organizationUuid") if text(oauth, "organizationUuid") else "")
            if text(config, "primaryApiKey"):
                return "{{Budget.ClaudeAccount.ApiKey}}"
            responses = config.get("customApiKeyResponses")
            approved = responses.get("approved") if isinstance(responses, dict) else None
            return "{{Budget.ClaudeAccount.ApiKey}}" if isinstance(approved, list) and approved else None
        _account = []
        def account():
            # (the account signed in, or the last one while signed out; the epochs [(from, to)] it was signed in; the record).
            # A move to another account closes the last span and opens one for it; the daemon keeps the same record, so whichever
            # sees a switch first dates it for both. ("", [], {}) when no account was ever seen.
            if _account:
                return _account[0]
            result = ("", [], {})
            try:
                signed = config_account(claude_config())
                try:
                    seen = read_json(ACCOUNT) if ACCOUNT.exists() else {}
                except Exception:
                    seen = {}
                spans = [dict(x) for x in seen.get("spans") or [] if isinstance(x, dict) and x.get("account") and x.get("from") is not None]
                if not spans and seen.get("account") and seen.get("since"):
                    spans = [{"account": seen["account"], "from": float(seen["since"]), "to": None}]  # recorded before spans were kept
                current = spans[-1]["account"] if spans else None
                changed = False
                if signed and current and "/" not in current and signed.startswith(current + "/"):
                    # Recorded before the organization was part of the key: the same sign-in, so keep its dates.
                    for x in spans:
                        if x["account"] == current:
                            x["account"] = signed
                    current, changed = signed, True
                if signed and signed != current:
                    now = time.time()
                    if spans:
                        spans[-1]["to"] = now
                    spans.append({"account": signed, "from": now, "to": None})
                    current, changed = signed, True
                # A closed span ages out on every read, not only at the next switch, or one /login counts as a switch for good.
                kept = [x for x in spans if x.get("to") is None or time.time() - float(x["to"]) <= HISTORY_KEPT]
                if kept and len(kept) != len(spans):
                    spans, changed = kept, True
                if changed:
                    seen.update(account=current, since=spans[-1]["from"], spans=spans)
                    write_json(ACCOUNT, seen)
                if current:
                    result = (current, [(float(x["from"]), None if x.get("to") is None else float(x["to"])) for x in spans if x["account"] == current], seen)
            except Exception:
                pass
            _account.append(result)
            return result
        def switched_at():
            # When the sign-in last moved to the current account (epoch; 0 when no account was ever seen).
            spans = account()[1]
            return spans[-1][0] if spans else 0.0
        def before_switch(stamp, to_the_second=False):
            # Whether an ISO time (a report's or a status file's) is from before the sign-in moved to the current account; never
            # before the first account seen (its first sight isn't a move from anything). rl-status.json's timestamp is cut to
            # the second, so the status the daemon writes in the second of the switch isn't taken for an older one (M.ORCH-T325).
            current, _, seen = account()
            since = switched_at()
            if not since or all(x.get("account") == current for x in seen.get("spans") or []):
                return False
            try:
                return datetime.fromisoformat(str(stamp).replace("Z", "+00:00")).timestamp() < (int(since) if to_the_second else since)
            except Exception:
                return True
        def owns(usage):
            # Whether a claude-usage.json reading is the current account's (ClaudeAccount.Owns). Every open CLI's statusline
            # restamps its own last reply, so after a /login the others keep writing the old account's percent as new. Once
            # another account has been signed in, only a reading the daemon's meter probe tagged with this account, or one whose
            # windows were already confirmed for it, counts.
            current, _, seen = account()
            if usage.get("account"):
                return usage["account"] == current
            if not current or all(x.get("account") == current for x in seen.get("spans") or []):
                return True
            confirmed = seen.get("confirmed") if isinstance(seen.get("confirmed"), dict) else {}
            mine = confirmed.get(current) if isinstance(confirmed.get(current), dict) else None
            if not mine:
                return False
            limits = usage.get("rate_limits") or {}
            reset = lambda w: float(w["resets_at"]) if isinstance(w, dict) and w.get("resets_at") is not None else None
            same = lambda a, b: a is not None and b is not None and abs(float(a) - float(b)) < 90
            five, week = reset(limits.get("five_hour")), reset(limits.get("seven_day"))
            if same(five, mine.get("five_hour")) and (week is None or mine.get("seven_day") is None or same(week, mine["seven_day"])):
                return True
            return same(week, mine.get("seven_day")) and not any(same(week, (c or {}).get("seven_day")) for a, c in confirmed.items() if a != current and isinstance(c, dict))
        def latched(by):
            # The stop latched by "claude", "claude-week" or "orchestrator" for the current account, for a window that hasn't
            # reset; None otherwise. Going back to an account brings its stop back while its window runs.
            try:
                every = read_json(LATCH)
                l = every.get(by + "@" + account()[0])
                if l is None:
                    # Latched before latches were kept per account: the current account's if dated from its current span.
                    l = every.get(by)
                    if l and (l.get("account") or float(l.get("epoch") or 0) != switched_at()):
                        l = None
                    elif l:
                        # Moved to the account's key, so it still holds once this account is left and come back to.
                        every[by + "@" + account()[0]] = dict(l, account=account()[0])
                        every.pop(by)
                        write_json(LATCH, every)
                if l and float(l["until"]) > time.time():
                    return l
            except Exception:
                pass
            return None
        def latch(by, until, **fields):
            # Records (or with until=None, clears) a stop seen for the current account's window, so it holds until the reset
            # whatever comes next. Latches whose window has reset are dropped.
            try:
                every = read_json(LATCH) if LATCH.exists() else {}
            except Exception:
                every = {}
            key = by + "@" + account()[0]
            old = every.get(key)
            legacy = every.get(by)
            if legacy and not legacy.get("account") and float(legacy.get("epoch") or 0) == switched_at():
                old = old or legacy
                every.pop(by)
            else:
                legacy = None
            if until is None:
                if old is None:
                    return
                every.pop(key, None)
            else:
                entry = dict(fields, until=float(until), account=account()[0], epoch=switched_at())
                if old and old.get("until") == entry["until"] and float(old.get("percent") or 0) > float(entry.get("percent") or 0):
                    entry["percent"] = old["percent"]
                if old == entry and not legacy:
                    return
                every[key] = entry
            now = time.time()
            write_json(LATCH, {k: v for k, v in every.items() if isinstance(v, dict) and float(v.get("until") or 0) > now})
        def claude_stop():
            # (percent, reset epoch, window) when Claude itself reports a window at the stop line, now or earlier in the same
            # window (an idle session's later, lower report doesn't lift it); None otherwise. The week counts as much as the
            # 5 hours: a daemon that's down mustn't let a week at 99% run into a wall that locks you out for days.
            windows = (("five_hour", "claude", "5-hour"), ("seven_day", "claude-week", "weekly"))
            try:
                usage = read_json(Path.home() / ".claude" / "claude-usage.json")
                limits = usage["rate_limits"] if owns(usage) and (usage.get("account") or not before_switch(usage.get("at"))) else {}
                for key, by, name in windows:
                    w = limits.get(key)
                    if w and float(w["used_percentage"]) >= STOP_PERCENT and float(w["resets_at"]) > time.time():
                        latch(by, float(w["resets_at"]), percent=float(w["used_percentage"]))
                        return float(w["used_percentage"]), float(w["resets_at"]), name
            except Exception:
                pass
            for key, by, name in windows:
                l = latched(by)
                if l:
                    return float(l.get("percent") or STOP_PERCENT), float(l["until"]), name
            return None
        def flag(value):
            return bool(value) and str(value).strip().lower() not in ("0", "false", "no", "off")
        KEY_SET_ASIDE = Path.home() / ".claude" / "session-context" / "{{ApiKeySetAsideName}}"
        def key_mark(key):
            # The record of a set-aside key names it by a hash of the 20 characters Claude Code keeps, never the key.
            import hashlib
            return hashlib.sha256(key[-20:].encode("utf-8")).hexdigest()[:16]
        def process_started(pid):
            # When a process started (epoch), or None when that can't be told.
            try:
                import ctypes
                from ctypes import wintypes
                k = ctypes.windll.kernel32
                k.OpenProcess.restype = wintypes.HANDLE
                k.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
                k.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
                k.CloseHandle.argtypes = [wintypes.HANDLE]
                h = k.OpenProcess(0x1000, False, int(pid))  # PROCESS_QUERY_LIMITED_INFORMATION
                if not h:
                    return None
                try:
                    t = [wintypes.FILETIME() for _ in range(4)]
                    if not k.GetProcessTimes(h, *[ctypes.byref(x) for x in t]):
                        return None
                    return ((t[0].dwHighDateTime << 32) | t[0].dwLowDateTime) / 1e7 - 11644473600
                finally:
                    k.CloseHandle(h)
            except Exception:
                return None
        def still_on_set_aside_key(key):
            # The key was set aside while this CLI already ran on it (M.ORCH-T434): Claude Code settled on the key at its start,
            # so it's what this CLI bills until it's closed and reopened.
            try:
                at = read_json(KEY_SET_ASIDE).get(key_mark(key)) if KEY_SET_ASIDE.exists() else None
                if at is None:
                    return False
                pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
                started = process_started(pid) if pid else None
                return started is not None and started < float(at)
            except Exception:
                return False
        BILLING = Path.home() / ".claude" / "session-context" / "{{Billing.BillingModeFile.FileName}}"
        def billing_mode():
            # The billing mode the orchestrator's switch set (M.ORCH-T436): {"mode": "team"|"api", "since", "login", ...}; {} when
            # no switch was ever made, and the last sign-in alone decides, as before.
            try:
                mode = read_json(BILLING) if BILLING.exists() else {}
                return mode if isinstance(mode, dict) and mode.get("mode") in ("{{Billing.BillingModes.Team}}", "{{Billing.BillingModes.Api}}") else {}
            except Exception:
                return {}
        def approved_since_start():
            # The switch to the API key approved the key after this CLI started: Claude Code settled on the login at its start, so
            # that's what it bills until it's restarted.
            try:
                at = billing_mode().get("approved_at")
                if at is None:
                    return False
                pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
                started = process_started(pid) if pid else None
                return started is not None and started < float(at)
            except Exception:
                return False
        def auth_sources(cwd=None):
            # (environment, settings files): this CLI's own environment over the settings' env blocks, and the settings read.
            settings = []
            places = [Path.home() / ".claude" / "settings.json", Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "ClaudeCode" / "managed-settings.json"]
            if cwd:
                places += [Path(cwd) / ".claude" / "settings.json", Path(cwd) / ".claude" / "settings.local.json"]
            for place in places:
                try:
                    found = json.loads(place.read_text(encoding="utf-8", errors="ignore"))
                    if isinstance(found, dict):
                        settings.append(found)
                except Exception:
                    pass
            env = {}
            for found in settings:
                if isinstance(found.get("env"), dict):
                    env.update({k: str(v) for k, v in found["env"].items()})
            env.update(os.environ)
            return env, settings
        CLOUDS = (("CLAUDE_CODE_USE_BEDROCK", "Amazon Bedrock"), ("CLAUDE_CODE_USE_VERTEX", "Google Vertex AI"),
                  ("CLAUDE_CODE_USE_FOUNDRY", "Microsoft Foundry"), ("CLAUDE_CODE_USE_ANTHROPIC_AWS", "Claude Platform on AWS"),
                  ("CLAUDE_CODE_USE_MANTLE", None))
        def billing(cwd=None):
            # (auth, source): what this CLI is billed through, by Claude Code's own precedence (code.claude.com/docs/en/iam,
            # "Authentication precedence"): a cloud provider, then ANTHROPIC_AUTH_TOKEN (a gateway), ANTHROPIC_API_KEY,
            # apiKeyHelper, CLAUDE_CODE_OAUTH_TOKEN, an Anthropic profile, then the /login in ~/.claude.json. Hooks run inside
            # claude.exe and see its environment, settings' env blocks included; the settings files are read too. Outside
            # claude.exe (no CLAUDECODE: Codex, Gemini, Kimi) it's "subscription", paced as ever. CLAUDE_CODE_OAUTH_TOKEN is a
            # subscription whose account can't be told from the token: it's paced as the config's account. source says how:
            # the cloud's variable, "env-p" (-p's key), "env" (an approved key), "env-set-aside" (one set aside since this CLI
            # started on it), "login", "token", "helper", the profile, or None.
            if os.environ.get("CLAUDECODE") != "1":
                return "{{Sessions.ContextCycler.SubscriptionAuth}}", None
            env, settings = auth_sources(cwd)
            for variable, _ in CLOUDS:
                if flag(env.get(variable)):
                    return "cloud", variable
            if env.get("ANTHROPIC_AUTH_TOKEN"):
                return "gateway", None
            config = claude_config()
            key = env.get("ANTHROPIC_API_KEY")
            if key:
                # -p (and the SDK) always uses it; interactively only once approved (Claude Code remembers the key's last 20
                # characters). A rejected key, or one still waiting for its answer, leaves the login in charge, except in a CLI
                # that started on it before it was set aside; an approved one leaves it too in a CLI that started before the
                # billing switch approved it (M.ORCH-T436).
                responses = config.get("customApiKeyResponses") if isinstance(config.get("customApiKeyResponses"), dict) else {}
                if str(os.environ.get("CLAUDE_CODE_ENTRYPOINT") or "").startswith("sdk"):
                    return "api-key", "env-p"
                listed = lambda k: responses.get(k) if isinstance(responses.get(k), list) else []
                if key[-20:] in listed("approved") and not approved_since_start():
                    return "api-key", "env"
                if key[-20:] in listed("rejected") and still_on_set_aside_key(key):
                    return "api-key", "env-set-aside"
            if any(found.get("apiKeyHelper") for found in settings):
                return "helper", None
            if env.get("CLAUDE_CODE_OAUTH_TOKEN"):
                return "{{Sessions.ContextCycler.SubscriptionAuth}}", "token"
            if env.get("ANTHROPIC_PROFILE") or (env.get("ANTHROPIC_FEDERATION_RULE_ID") and env.get("ANTHROPIC_ORGANIZATION_ID")):
                return "profile", env.get("ANTHROPIC_PROFILE")
            # A Console login (primaryApiKey, with its oauthAccount) bills the API, not a subscription.
            console = isinstance(config.get("primaryApiKey"), str) and config.get("primaryApiKey")
            return ("api-key" if console or config_account(config) == "{{Budget.ClaudeAccount.ApiKey}}" else "{{Sessions.ContextCycler.SubscriptionAuth}}"), "login"
        def session_auth(cwd=None):
            return billing(cwd)[0]
        def reset_text(epoch):
            # The clock alone today; with the weekday on any other day, even under 24 hours off (M.ORCH-T407).
            at = datetime.fromtimestamp(epoch)
            return at.strftime("%a %I:%M %p" if at.date() != datetime.now().date() else "%I:%M %p").replace(" 0", " ").lstrip("0")
        def claude_stop_note(stop, running=False):
            # running: the daemon's status is fresh (it just hasn't stopped yet), so the note doesn't say it's down.
            window = stop[2] if len(stop) > 2 else "5-hour"
            return ("Claude reports " + str(int(stop[0])) + "% of the " + window + " window used" +
                    ("" if running else " and MindAttic.Orchestrator isn't running to pace it") + ", "
                    "so tool calls are refused until it resets at " + reset_text(stop[1]) + ". End your turn now.")
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
                latch("orchestrator", until, note=note, window=s.get("stop_window"), reset_time=s.get("reset_time"))
                return note
            l = latched("orchestrator")
            return (l.get("note") or ORCHESTRATOR_STOP) if l else None
        def lapsed(s):
            # A status worked out before its window reset holds nothing in the new one, though it's still fresh for up to a tick:
            # past stop_until its stop, past window_reset its holds, wind-down and subagent allowance (M.ORCH-T387).
            def passed(key):
                try:
                    return datetime.fromisoformat(str(s[key]).replace("Z", "+00:00")).timestamp() <= time.time()
                except Exception:
                    return False
            if s.get("stop") and passed("stop_until"):
                s = {k: v for k, v in s.items() if k not in ("stop", "stop_note", "stop_until", "stop_window")}
            if passed("window_reset"):
                s = {k: v for k, v in s.items() if k not in ("pace_delay_seconds", "pace_note", "wind_down_note", "subagent_allowance", "subagent_note")}
            return s
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

    /// <summary>When the context gate set each <c>ANTHROPIC_API_KEY</c> aside for a claude.ai login (in <c>~/.claude/session-context</c>,
    /// keyed by a hash of the key's last 20 characters): a CLI started before then still bills the key until it's reopened.</summary>
    public const string ApiKeySetAsideName = "api-key-set-aside.json";

    /// <summary>
    /// Python for the context gate (needs <c>json</c>, <c>os</c>, <c>re</c>, <c>time</c>, <c>Path</c> and <see cref="ClaudeUsagePython"/>):
    /// the last sign-in decides what a CLI bills (M.ORCH-T434). With a claude.ai login in <c>~/.claude.json</c> (<c>oauthAccount</c>,
    /// no <c>primaryApiKey</c>), an <c>ANTHROPIC_API_KEY</c> in a CLI's environment that was approved before the /login would
    /// go on winning in every CLI started with it (Claude Code's precedence puts it above the login), so it's set aside the way
    /// Claude Code records a "no" to it: its last 20 characters move from <c>customApiKeyResponses.approved</c> to
    /// <c>.rejected</c>. A Console login, or no claude.ai login at all, leaves the key alone: the API is what was chosen. Each
    /// CLI is told at its start what it bills, a warning when that's the key rather than the claude.ai login.
    /// <para>The billing switch (<see cref="Billing.BillingSwitch"/>, M.ORCH-T436) is followed: with its mode on the API key the
    /// key is what was chosen, so it's left approved and a CLI on the login is the one warned, until a /login to another
    /// claude.ai account than the one signed in at the switch, which sets the mode back to Claude Team.</para>
    /// </summary>
    private const string BillingPython = """
        def top_level_value(text, name):
            # (start, end, value) of a top-level key's value in a JSON object's text (the last, as JSON.parse keeps); None if absent.
            dec, ws = json.JSONDecoder(), re.compile(r"[ \t\r\n]*")
            i = ws.match(text, 0).end()
            if text[i:i + 1] != "{":
                return None
            i = ws.match(text, i + 1).end()
            found = None
            while i < len(text) and text[i] != "}":
                key, i = dec.raw_decode(text, i)
                i = ws.match(text, i).end()
                if text[i:i + 1] != ":":
                    return None
                i = ws.match(text, i + 1).end()
                value, end = dec.raw_decode(text, i)
                if key == name:
                    found = (i, end, value)
                i = ws.match(text, end).end()
                if text[i:i + 1] == ",":
                    i = ws.match(text, i + 1).end()
            return found
        def dumped_like(value, text, start, end):
            # value as JSON laid out as the text it replaces was: Claude Code's two-space indent at that depth, its line endings.
            old = text[start:end]
            if "\n" not in old:
                return json.dumps(value, ensure_ascii=False, separators=(", ", ": ") if ": " in old else (",", ":"))
            newline = "\r\n" if "\r\n" in old else "\n"
            line = text[text.rfind("\n", 0, start) + 1:start]
            indent = line[:len(line) - len(line.lstrip(" \t"))]
            inner = re.search(r"\n([ \t]*)\S", old)
            step = inner.group(1)[len(indent):] if inner and len(inner.group(1)) > len(indent) else "  "
            lines = json.dumps(value, ensure_ascii=False, indent=step).split("\n")
            return lines[0] + "".join(newline + indent + l for l in lines[1:])
        def set_aside_key(key):
            # Moves the key's last 20 characters from customApiKeyResponses.approved to .rejected in ~/.claude.json; True when it
            # did. That's Claude Code's own file, large and rewritten often: only that field's text changes, the rest is kept byte
            # for byte, and it's put in place whole, re-read just before so a write Claude Code made meanwhile is never lost.
            path, tail = Path.home() / ".claude.json", key[-20:]
            for _ in range(5):
                raw = None
                for _ in range(20):
                    try:
                        raw = path.read_bytes()
                        break
                    except PermissionError:
                        time.sleep(0.05)
                if raw is None:
                    return False
                bom = raw.startswith(b"\xef\xbb\xbf")
                text = raw[3 if bom else 0:].decode("utf-8")
                span = top_level_value(text, "customApiKeyResponses")
                if not span or not isinstance(span[2], dict) or not isinstance(span[2].get("approved"), list) or tail not in span[2]["approved"]:
                    return False
                value = dict(span[2])
                rejected = value.get("rejected") if isinstance(value.get("rejected"), list) else []
                value["approved"] = [x for x in value["approved"] if x != tail]
                value["rejected"] = rejected + ([] if tail in rejected else [tail])
                out = text[:span[0]] + dumped_like(value, text, span[0], span[1]) + text[span[1]:]
                json.loads(out)
                tmp = path.with_name(path.name + "." + str(os.getpid()) + ".tmp")
                try:
                    tmp.write_bytes((b"\xef\xbb\xbf" if bom else b"") + out.encode("utf-8"))
                    for _ in range(20):
                        try:
                            if path.read_bytes() != raw:
                                break  # Claude Code wrote it meanwhile: start over from its new text
                            os.replace(tmp, path)
                            return True
                        except PermissionError:
                            time.sleep(0.05)
                    else:
                        return False
                finally:
                    try:
                        tmp.unlink()
                    except Exception:
                        pass
            return False
        def plan_name(oauth):
            # The plan the config names (organizationType "claude_team": Claude Team); None when it names none.
            kind = oauth.get("organizationType") if isinstance(oauth.get("organizationType"), str) else ""
            return "Claude " + " ".join(w.capitalize() for w in kind[len("claude_"):].split("_") if w) if kind.startswith("claude_") and len(kind) > len("claude_") else None
        def billing_check(cwd=None):
            # (auth, line, warn) for a CLI starting: one [Billing] line naming what it bills. A claude.ai login sets an approved
            # ANTHROPIC_API_KEY aside first; a CLI already running on it is warned to close and reopen. With the billing switch
            # on the API key (M.ORCH-T436) the key is what was chosen: it's left approved, and a CLI on the login is warned
            # instead; until a /login to another claude.ai account than the one signed in then, which switches back to Claude Team.
            config = claude_config()
            text = lambda o, k: o.get(k) if isinstance(o, dict) and isinstance(o.get(k), str) and o.get(k) else None
            oauth = config.get("oauthAccount") if isinstance(config.get("oauthAccount"), dict) else {}
            claude_ai = bool(text(oauth, "accountUuid")) and not text(config, "primaryApiKey")
            mode = billing_mode()
            if mode.get("mode") == "api" and claude_ai and config_account(config) != mode.get("login"):
                mode = dict(mode, mode="team", since=time.time(), login=config_account(config), by="login", requested=[], refused=[], approved_at=None)
                write_json(BILLING, mode)
            api_mode = mode.get("mode") == "api"
            auth, source = billing(cwd)
            env, _ = auth_sources(cwd)
            who = (text(oauth, "emailAddress") or text(oauth, "accountUuid") or "") + (" (" + text(oauth, "organizationName") + ")" if text(oauth, "organizationName") else "")
            plan = plan_name(oauth) if claude_ai else None
            login = (plan or "the claude.ai login") + ": " + who
            key = env.get("ANTHROPIC_API_KEY") or ""
            keyed = "the API key in ANTHROPIC_API_KEY (…" + key[-4:] + ")"
            if auth == "api-key" and source in ("env", "env-p", "env-set-aside") and claude_ai and not api_mode:
                setaside = source == "env-set-aside"
                if source in ("env", "env-p"):
                    responses = config.get("customApiKeyResponses") if isinstance(config.get("customApiKeyResponses"), dict) else {}
                    if isinstance(responses.get("approved"), list) and key[-20:] in responses["approved"]:
                        try:
                            # Dated first, so this CLI (started before it) reads as still on the key the moment the file changes.
                            marks = read_json(KEY_SET_ASIDE) if KEY_SET_ASIDE.exists() else {}
                            marks[key_mark(key)] = time.time()
                            write_json(KEY_SET_ASIDE, marks)
                        except Exception:
                            pass
                        try:
                            setaside = set_aside_key(key)
                        except Exception:
                            setaside = False
                    elif source == "env-p":
                        setaside = True  # nothing approved to set aside: only -p uses it
                line = "[Billing] Warning: this CLI bills " + keyed + ", not " + login + ", which is signed in."
                if source == "env-p":
                    line += (" -p always uses ANTHROPIC_API_KEY when it's set: remove it from the environment that starts this CLI so it bills " +
                             (plan or "the claude.ai login") + ".")
                elif setaside:
                    line += " The key has now been set aside: close and reopen this CLI so it bills " + (plan or "the claude.ai login") + "."
                else:
                    line += (" The key couldn't be set aside in ~/.claude.json: turn off \"Use custom API key\" in /config, then close and "
                             "reopen this CLI so it bills " + (plan or "the claude.ai login") + ".")
                return auth, line, True
            if auth == "cloud":
                name = dict(CLOUDS).get(source)
                line = "[Billing] This CLI bills " + (name + " (" + source + ")" if name else "the cloud provider " + str(source) + " selects") + "."
            elif auth == "gateway":
                url = env.get("ANTHROPIC_BASE_URL")
                line = "[Billing] This CLI bills the LLM gateway " + ("at " + url + " " if url else "") + "through ANTHROPIC_AUTH_TOKEN."
            elif auth == "helper":
                line = "[Billing] This CLI bills the API key from apiKeyHelper."
            elif auth == "profile":
                line = "[Billing] This CLI bills the Anthropic profile " + (source + "." if source else "set by the federation variables.")
            elif auth == "api-key" and source in ("env", "env-p", "env-set-aside"):
                line = "[Billing] This CLI bills " + keyed + "."
                if api_mode:
                    line += (" Billing is set to the API key, for emergencies only: 'orchestrator billing team' (or the dashboard's toggle)"
                             " switches every CLI back to Claude Team.")
            elif auth == "api-key":
                line = ("[Billing] This CLI bills the Anthropic Console API: " + who + "." if text(config, "primaryApiKey") and who
                        else "[Billing] This CLI bills the Anthropic Console API." if text(config, "primaryApiKey") else "[Billing] This CLI bills an approved Anthropic API key.")
            elif source == "token":
                line = "[Billing] This CLI bills the Claude subscription in CLAUDE_CODE_OAUTH_TOKEN."
            elif claude_ai and api_mode:
                line = ("[Billing] Warning: billing is set to the API key, but this CLI bills " + login + ". A Launcher tab restarts it"
                        " at its next idle point; outside the Launcher, close it and reopen it with ANTHROPIC_API_KEY set to the Vault key.")
                return auth, line, True
            elif claude_ai:
                line = "[Billing] This CLI bills " + login + "."
            else:
                line = "[Billing] This CLI isn't signed in to anything it can bill: run /login."
            return auth, line, False
        """;

    /// <summary>A stop from an orchestrator that didn't say when its window resets is latched this long.</summary>
    public const int StopLatchFallbackSeconds = 600;

    /// <summary>Python shared by both hooks (needs <c>os</c> and <c>Path</c>): the workspace and which repo a folder is in,
    /// so the repo-window guard and keep-going can't disagree about it.</summary>
    private const string WorkspacePython = """
        # The folders the orchestrator is set to (settings.json's WorkspaceRoot and WorktreeRoot), not always the default ones
        # (M.ORCH-T409); MINDATTIC_WORKSPACE still overrides both, its worktrees beside it as before.
        def orchestrator_roots():
            if os.environ.get("MINDATTIC_WORKSPACE"):
                return (os.environ["MINDATTIC_WORKSPACE"], None)
            try:
                s = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text(encoding="utf-8-sig"))
                return (s.get("WorkspaceRoot") or None, s.get("WorktreeRoot") or None) if isinstance(s, dict) else (None, None)
            except Exception:
                return (None, None)
        _ws_root, _wt_root = orchestrator_roots()
        WS = Path(_ws_root or "D:/Projects/MindAttic")
        WT = Path(_wt_root) if _wt_root else WS.parent / (WS.name + ".worktrees")
        # The Workflow database the orchestrator and the workflow CLI open: MINDATTIC_WORKFLOW_DB when set (M.ORCH-T398).
        WORKFLOW_DB = os.environ.get("MINDATTIC_WORKFLOW_DB") or str(WS / "MindAttic.Workflow" / "data" / "workflow.db")

        def repo_of(path, worktrees=True):
            # A repo checkout under the workspace, or (with worktrees) one of autopilot's worktrees of it; None elsewhere.
            try:
                # Git Bash's /d/Projects/... is D:/Projects/... (Windows Python would read it as a folder on this drive).
                p = Path(re.sub(r"^/([a-zA-Z])(?=/|$)", r"\1:", str(path))).resolve()
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
                names = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text(encoding="utf-8-sig")).get("ExemptCommands")
            except Exception:
                names = None
            if not isinstance(names, list):
                names = EXEMPT_DEFAULT
            # /throttle and /unthrottle are gone: a list still naming them means /pacing, which replaced them (M.ORCH-T483).
            names = {str(n).strip().lstrip("/").lower() for n in names if str(n).strip().lstrip("/")}
            return {"pacing" if n in ("throttle", "unthrottle") else n for n in names}
        def exempt_prompt(text):
            # The exempt command a typed prompt starts with ("/deploy now" -> "/deploy"), or None.
            m = re.match(r"\s*/([^\s/]+)(?:\s|$)", text or "")
            return "/" + m.group(1).lower() if m and m.group(1).lower() in exempt_commands() else None
        def exempt_skill(name):
            return bool(name) and str(name).strip().lstrip("/").lower() in exempt_commands()
        """;

    /// <summary>Python shared by both hooks (needs <c>json</c>, <c>os</c>, <c>re</c>, <c>time</c>, <c>Path</c> and <c>repo_of</c>):
    /// the per-CLI <c>crossrepo-&lt;pid&gt;</c> and <c>autopilot-&lt;pid&gt;</c> marks. The context gate writes them for a typed
    /// /crossrepo or /autopilot, the pace gate for the same command called through the Skill tool, but only when the user's
    /// own latest prompt asked for it (M.ORCH-T435): both are the user's choice, never the model's.</summary>
    internal const string MarksPython = """
        SESSION_MARKS = Path.home() / ".claude" / "session-context"
        # What the user's typed prompt asks for by name ("use crossrepo for X", "turn auto-pilot on", or the command itself).
        ASKS_FOR = {"crossrepo": re.compile(r"\bcross[\s_-]?repos?\b", re.I), "autopilot": re.compile(r"\bauto[\s_-]?pilot\b|/work\b", re.I),
                    "handoff": re.compile(r"\bhand[\s_-]?off\b", re.I)}
        def asked_for(prompt):
            return sorted(k for k, r in ASKS_FOR.items() if r.search(prompt or ""))
        def session_state(sid):
            sid = re.sub(r"[^A-Za-z0-9_.-]", "_", str(sid or ""))
            if not sid:
                return {}
            for _ in range(20):
                try:
                    return json.loads((SESSION_MARKS / (sid + ".state.json")).read_text()) or {}
                except PermissionError:
                    time.sleep(0.05)
                except Exception:
                    return {}
            return {}
        def autopilot_repos(pid):
            # What /autopilot turned on in THIS CLI (its repos, or "*"): it works them through its own subagents.
            # Never another CLI's: one you're using is never pushed onto tickets because its repo was named elsewhere.
            try:
                return [n.strip() for n in (SESSION_MARKS / ("autopilot-" + str(pid))).read_text(encoding="utf-8", errors="replace").split(",") if n.strip()] if pid else []
            except Exception:
                return []
        def set_autopilot(pid, args, cwd):
            # /autopilot [off|all] [-repo=A,B]: THIS CLI works those repos' tickets through its own subagents (its own
            # repo when none are named) until /autopilot off here. Nothing is marked for any other CLI. A name may be a
            # wildcard (-repo=GTFO.*), kept as typed and matched wherever the repos are read (M.ORCH-T165).
            # Returns why nothing changed, or None once it's recorded.
            import fnmatch
            if re.search(r"-{1,2}repos\b", args, re.I):
                # The -repos alias is gone (it read as a different switch); say so rather than guess what was meant.
                return "/autopilot takes -repo=A,B (wildcards too: -repo=GTFO.*); -repos is gone. Nothing changed: type it again with -repo."
            SESSION_MARKS.mkdir(parents=True, exist_ok=True)
            mark = SESSION_MARKS / ("autopilot-" + str(pid))
            names = autopilot_repos(pid)
            mine = repo_of(cwd or "")
            m = re.search(r"-{1,2}repo\b\s*[=:]?\s*\[?([^\]]*)\]?", args, re.I)
            named = [r.strip() for r in m.group(1).replace(" ", ",").split(",") if r.strip() and r.strip().lower() not in ("on", "off", "this", "all")] if m else ([mine] if mine else [])
            lower = {n.lower() for n in named}
            if re.match(r"off\b", args, re.I):
                # A repo this CLI covers only through "*" or a wildcard can't be switched off on its own: removing its name
                # would change nothing and it would go on being worked, so say so and change nothing.
                covered = [(n, p) for n in named if m for p in names
                           if p.lower() != n.lower() and fnmatch.fnmatchcase(n.lower(), p.lower())]
                if covered and not re.match(r"off\s+all\b", args, re.I):
                    n, p = covered[0]
                    return (n + " is worked here through /autopilot " + ("all" if p == "*" else "-repo=" + p) + ", so it can't be switched "
                            "off on its own. Nothing changed: type /autopilot off, then /autopilot -repo= with the repos you do want.")
                names = [] if re.match(r"off\s+all\b", args, re.I) or not m else [n for n in names if n.lower() not in lower]
            elif re.match(r"all\b", args, re.I):
                names = ["*"]
            else:
                names = [n for n in names if n != "*" and n.lower() not in lower] + named
            try:
                (SESSION_MARKS / "autopilot.order").unlink()  # the old workspace-wide list: nothing reads it any more
            except Exception:
                pass
            if re.match(r"off\s+all\b", args, re.I):
                for each in SESSION_MARKS.glob("autopilot-*"):  # off everywhere: every CLI's, kept off past the default
                    try:
                        (SESSION_MARKS / ("no" + each.name)).write_text("", encoding="utf-8")
                        each.unlink()
                    except Exception:
                        pass
            off = SESSION_MARKS / ("noautopilot-" + str(pid))
            if pid and names:
                mark.write_text(",".join(names), encoding="utf-8")
                if off.exists():
                    off.unlink()
            else:
                if mark.exists():
                    mark.unlink()
                if pid and re.match(r"off\b", args, re.I):
                    off.write_text("", encoding="utf-8")
            return None
        def autopilot_by_default(pid, cwd):
            # /autopilot is on by default (M.ORCH-T476): a Claude CLI with no mark gets one for its own repo, unless /autopilot
            # off was typed in it (its noautopilot mark), AutopilotByDefault is false, or the repo is in AutopilotSkip.
            if not pid or (SESSION_MARKS / ("autopilot-" + str(pid))).exists() or (SESSION_MARKS / ("noautopilot-" + str(pid))).exists():
                return
            try:
                s = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text(encoding="utf-8-sig"))
            except Exception:
                s = {}
            if s.get("AutopilotByDefault") is False:
                return
            mine = repo_of(cwd or "")
            if not mine or mine.lower() in {(x or "").lower() for x in s.get("AutopilotSkip") or []}:
                return
            SESSION_MARKS.mkdir(parents=True, exist_ok=True)
            (SESSION_MARKS / ("autopilot-" + str(pid))).write_text(mine, encoding="utf-8")
        def set_crossrepo(pid, args):
            # /crossrepo <A,B|all> lets this CLI work other repos too (the explicit exception to M.ORCH-T95); /crossrepo off ends it.
            if not pid:
                return None
            SESSION_MARKS.mkdir(parents=True, exist_ok=True)
            mark = SESSION_MARKS / ("crossrepo-" + str(pid))
            if not args or re.match(r"off\b", args, re.I):
                if mark.exists():
                    mark.unlink()
            else:
                mark.write_text(",".join(r.strip() for r in args.replace(" ", ",").split(",") if r.strip()), encoding="utf-8")
            return None
        # /handoff (M.ORCH-T481): one mark for every CLI (Sessions.Handoff reads and writes the same file). While it's winding down
        # or quiet nothing new starts; each CLI quicksaves (handoff-saved-<pid>); the first start after the switch marks it resumed.
        HANDOFF = SESSION_MARKS / "handoff.json"
        # The shared runner's save, as /quicksave runs it.
        QUICKSAVE = re.compile(r"prose-agent\.ps1['\"]?\s+(?:-Command\s+)?save\b", re.I)
        def utc_stamp():
            return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        def handoff():
            for _ in range(20):
                try:
                    h = json.loads(HANDOFF.read_text(encoding="utf-8"))
                    return h if isinstance(h, dict) and h.get("state") in ("winding", "quiet", "resumed") else None
                except PermissionError:
                    time.sleep(0.05)
                except Exception:
                    return None
            return None
        def handing_off():
            # The mark while it holds new work back (winding down, or quiet until the switch); None otherwise.
            h = handoff()
            return h if h and h.get("state") in ("winding", "quiet") else None
        def write_handoff(h):
            SESSION_MARKS.mkdir(parents=True, exist_ok=True)
            tmp = HANDOFF.with_name(HANDOFF.name + "." + str(os.getpid()) + ".tmp")
            tmp.write_text(json.dumps(h, indent=2), encoding="utf-8")
            for _ in range(20):
                try:
                    os.replace(tmp, HANDOFF)
                    return
                except PermissionError:
                    time.sleep(0.05)
        def clear_handoff(mark_too=True):
            # /handoff off, or a new handoff starting: the mark (unless kept) and every CLI's saved and restored marks go.
            for f in list(SESSION_MARKS.glob("handoff-saved-*")) + list(SESSION_MARKS.glob("handoff-restored-*")) + ([HANDOFF] if mark_too else []):
                try:
                    f.unlink()
                except Exception:
                    pass
        def start_handoff(by, cwd=None):
            # Starts a handoff unless one already holds, recording every CLI's /autopilot (on with its repos, or typed off) by the
            # repo and folder it runs in, so the CLI that starts there on the new account gets it back. True when it started one.
            if handing_off():
                return False
            clear_handoff(False)
            cwds = {}
            for f in SESSION_MARKS.glob("*.state.json"):
                try:
                    s, at = json.loads(f.read_text()), f.stat().st_mtime
                except Exception:
                    continue
                if isinstance(s, dict) and s.get("pid") and (str(s["pid"]) not in cwds or at > cwds[str(s["pid"])][0]):
                    cwds[str(s["pid"])] = (at, s.get("cwd"))
            if by and cwd:
                cwds[str(by)] = (float("inf"), cwd)  # the CLI starting it is where it's typed now
            clis = {}
            for f in SESSION_MARKS.glob("autopilot-*"):
                pid = f.name[len("autopilot-"):]
                try:
                    if pid.isdigit():
                        clis[pid] = {"repos": ",".join(n.strip() for n in f.read_text(encoding="utf-8", errors="replace").split(",") if n.strip()), "off": False}
                except Exception:
                    pass
            for f in SESSION_MARKS.glob("noautopilot-*"):
                pid = f.name[len("noautopilot-"):]
                if pid.isdigit() and pid not in clis:
                    clis[pid] = {"repos": "", "off": True}
            entries = []
            for pid in sorted(clis, key=int):
                cwd = cwds.get(pid, (0, None))[1]
                entries.append(dict(pid=int(pid), repo=repo_of(cwd) if cwd else None, cwd=cwd, **clis[pid]))
            try:
                signed = account()[0]
            except Exception:
                signed = ""
            write_handoff({"state": "winding", "started": utc_stamp(), "by": int(by) if str(by or "").isdigit() else None,
                           "account": signed, "quiet_at": None, "resumed_at": None, "autopilot": entries})
            return True
        def handoff_saved(pid):
            return bool(pid) and (SESSION_MARKS / ("handoff-saved-" + str(pid))).exists()
        def mark_handoff_saved(pid, repo):
            if pid:
                SESSION_MARKS.mkdir(parents=True, exist_ok=True)
                (SESSION_MARKS / ("handoff-saved-" + str(pid))).write_text(repo or "", encoding="utf-8")
        def handoff_save_told(repo):
            # What a CLI is told when its turn ends during a handoff without having saved.
            return ("A handoff is under way (/handoff): every CLI is winding down for an account switch, and nothing new starts. Quicksave "
                    "this repo's handoff now, exactly as /quicksave does: compose Task, Decisions, State and Next for what this CLI was doing "
                    "and run the shared runner's save from this CLI's folder (& <workspace root>\\mindattic-agent-standard\\prose-agent.ps1 save "
                    "-RepoRoot (Get-Location) -Task '...' -Decisions '...' -State '...' -Next '...'). Then say exactly \"" + repo +
                    " quiet, handoff saved\" and end your turn. Start no ticket or subagent.")
        """;

    // The context gate acts on these when they're typed (UserPromptSubmit), and the pace gate when the model calls them through the
    // Skill tool after the user's own prompt asked for them (M.ORCH-T435); the files make them real slash commands.
    internal const string PacingCommand = """
        ---
        description: "/pacing off runs this CLI without pace holds until it closes or /pacing on (the default for every new CLI); a bare /pacing says which this CLI is on. The 99% and weekly stops still apply."
        argument-hint: "[on|off]"
        ---
        /pacing $ARGUMENTS was typed in this CLI, and MindAttic.Orchestrator's context gate has recorded it for this CLI process only. With off: no pace holds (subagents included) until the CLI closes or /pacing on is typed; its usage still counts toward the 5-hour pace, so other CLIs absorb it, and the stop at the window's target and the weekly stop still apply. With on: this CLI is held to the pace again. Reply with one short line confirming which, then carry on with whatever was asked.
        """;

    /// <summary>The /unthrottle and /throttle command files earlier versions wrote, word for word: /pacing replaced them
    /// (M.ORCH-T483), so a copy still exactly ours is deleted, and one the user rewrote is left alone.</summary>
    internal static readonly (string File, string Body)[] RetiredCommands =
    [
        ("unthrottle.md", """
            ---
            description: Run this CLI unthrottled (no pace holds) until it closes, for when something has to get done fast.
            ---
            MindAttic.Orchestrator's context gate has exempted this CLI process from pace holds, subagents included, until the CLI closes or you run /throttle. Its usage still counts toward the 5-hour pace, so other CLIs absorb the slowdown. Reply with one short line confirming this CLI is now unthrottled, then carry on with whatever was asked.
            """),
        ("throttle.md", """
            ---
            description: Put this CLI back under the orchestrator's pace holds after /unthrottle.
            ---
            MindAttic.Orchestrator's context gate has put this CLI process back under pace holds. Reply with one short line confirming this CLI is paced again.
            """),
    ];

    /// <summary>Deletes a retired command file in <paramref name="commandsDir"/> that's still exactly ours; returns what went.</summary>
    internal static IEnumerable<string> DeleteRetiredCommands(string commandsDir)
    {
        foreach (var (file, body) in RetiredCommands)
        {
            var path = Path.Combine(commandsDir, file);
            bool gone;
            try { gone = File.Exists(path) && File.ReadAllText(path) == body && TryDeleteFile(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { gone = false; }
            if (gone) yield return "deleted commands/" + file;
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try { File.Delete(path); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    internal static readonly string WorkCommand = AutopilotCommand
        .Replace("description: This CLI works", "description: The same as /autopilot. This CLI works")
        .Replace("/autopilot $ARGUMENTS is running", "/work $ARGUMENTS (the same as /autopilot $ARGUMENTS) is running");

    internal const string AutopilotCommand = """
        ---
        description: This CLI works the open tickets of its own repo (or -repo=A,B, wildcards like -repo=GTFO.*, or "all") through its own subagents, each in its own worktree, landing on main and closing the ticket, until none are left. /autopilot off stops it in this CLI; /autopilot off all in every CLI.
        argument-hint: "[off|off all|all] [-repo=A,B | -repo=GTFO.*]"
        ---
        /autopilot $ARGUMENTS is running in this CLI. MindAttic.Orchestrator's gates record it for this CLI only (no other CLI is affected) when the user typed it, or when you called it through the Skill tool because the user's own latest prompt asked for autopilot; a Skill call the user didn't ask for is refused and records nothing, so never start it on your own. Once recorded, while it's on, the repos named (a wildcard such as GTFO.* names every repo it matches) are this CLI's cross-repo exception so your subagents can work and close tickets there.

        If the arguments start with "off": reply with one short line confirming autopilot is off in this CLI (with -repo, just for those repos; "off all": in every CLI). Subagents already working finish their tickets. Then end your turn.

        Otherwise you work the tickets yourself, through subagents, now:
        1. Run `orchestrator next --repos "<repos>" --claim`, where <repos> is the -repo list as typed (wildcards included), * for all, or this CLI's own repo when none are named; keep the quotes so no shell expands a wildcard. It claims the next workable tickets (fixes and tickets the user approved, in queue order, as many as the pace allows right now), makes each its own git worktree, and prints each ticket's id and a full prompt.
        2. Start one subagent per ticket it printed, all in one message, each with exactly the prompt printed for it (a general-purpose subagent, in the foreground). Each works only in its worktree, commits, lands on the repo's main line and closes its ticket.
        3. When they've returned, say in one line per ticket what happened, and go back to step 1.
        4. Stop when `orchestrator next` hands out nothing (it says why: none left, waiting for approval, or the pace), and say so in one short line. Don't start anything else on your own. Nothing works these tickets in the background: it's this CLI's own subagents or nobody. Autopilot stays on here, so when this turn ends while the repos still have workable tickets and the pace has room, the orchestrator sends this CLI back to step 1; /autopilot off stops that.
        """;

    internal const string HandoffCommand = """
        ---
        description: Wind every CLI down for an account switch and pick up after it. Nothing new starts, what's working finishes, lands and closes, each CLI quicksaves its repo's handoff, and you're told when it's all quiet. /handoff off cancels it; /handoff status lists what's still working.
        argument-hint: "[off|status]"
        ---
        /handoff $ARGUMENTS is running in this CLI. MindAttic.Orchestrator's gates record it for every CLI when the user typed it, or when you called it through the Skill tool because the user's own latest prompt asked for a handoff; a Skill call the user didn't ask for is refused and records nothing, so never start it on your own.

        If the arguments start with "off" or "cancel": reply with one short line confirming the handoff is cancelled and new work starts again. Then end your turn.

        If the arguments are "status": run `orchestrator handoff status` and show what it prints. Then end your turn.

        Otherwise a handoff is under way in every CLI: `orchestrator next` hands out nothing, autopilot sends no CLI back for work, no new subagent starts, and tickets claimed for a subagent that never started go back to the queue. In this CLI:
        1. Start no ticket or subagent. If subagents of yours are still working, let them finish, land and close their tickets.
        2. Then quicksave this repo's handoff exactly as /quicksave does: compose Task, Decisions, State and Next for what this CLI was doing, and run the shared runner's save from this CLI's folder: `& <workspace root>\mindattic-agent-standard\prose-agent.ps1 save -RepoRoot (Get-Location) -Task '...' -Decisions '...' -State '...' -Next '...'` (the workspace root is the folder this repo sits in; single-quote the strings, doubling any ' inside).
        3. Then say exactly "<repo> quiet, handoff saved", <repo> being this CLI's repo, and end your turn.

        The orchestrator's daemon says "All quiet, safe to switch accounts" once nothing is working in any CLI. On the new account each CLI's start loads its handoff (as /quickload does) and gets its /autopilot back; memories are local files, so they carry over.
        """;

    internal const string CrossRepoCommand = """
        ---
        description: Let this CLI work other repos too (the one exception to "a repo's window works only that repo"). /crossrepo off ends it.
        argument-hint: "<Repo,Repo|all|off>"
        ---
        /crossrepo $ARGUMENTS is running in this CLI. MindAttic.Orchestrator's gates record it for this CLI process, until it closes, when the user typed it, or when you called it through the Skill tool because the user's own latest prompt asked for cross-repo work; a Skill call the user didn't ask for is refused and records nothing, so never start it on your own.

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
                if s.get("source") != "MindAttic.Orchestrator" or before_switch(s["timestamp"], True):
                    return {}
                at = datetime.fromisoformat(s["timestamp"].replace("Z", ""))
                return lapsed(s) if (datetime.utcnow() - at).total_seconds() <= STALE else {}
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
                if s.get("source") != "MindAttic.Orchestrator" or not s.get("window_reset") or before_switch(s.get("timestamp"), True):
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
            # UTF-8 as Claude Code writes it, not the ANSI code page Python reads a pipe in on Windows (M.ORCH-T392).
            data = json.loads(sys.stdin.buffer.read().decode("utf-8", errors="replace"))
        except Exception:
            data = {}
        tool = data.get("tool_name")

        # A repo's window works only that repo (M.ORCH-T95): its own folder and worktrees, and its own tickets. Another
        # repo's files and tickets belong to that repo's window, autopilot, or the General (workspace-root) session.
        import fnmatch, os, re, sqlite3
        {{WorkspacePython}}

        def ticket_repo(ticket_id):
            try:
                db = sqlite3.connect(WORKFLOW_DB)
                row = db.execute("SELECT repo FROM tickets WHERE id = ? COLLATE NOCASE", (ticket_id,)).fetchone()
                db.close()
                return row[0] if row else None
            except Exception:
                return None

        def refuse(why):
            print(json.dumps(dict(hookSpecificOutput=dict(hookEventName="PreToolUse", permissionDecision="deny", permissionDecisionReason=why))))
            raise SystemExit(0)

        # /crossrepo and /autopilot called through the Skill tool (M.ORCH-T435): the model starts them when the user asks in words
        # ("use crossrepo for X"), and the guard below refused every edit for want of the mark a typed command writes. They're
        # the user's choice, never the model's: recorded exactly as typed only when the user's latest prompt asked for it (the
        # context gate notes that), refused otherwise. Switching off is always safe, so it always goes through.
        {{MarksPython}}
        if tool == "Skill":
            called = str((data.get("tool_input") or {}).get("skill") or "").strip().lstrip("/").lower().split(":")[-1]
            called = "autopilot" if called == "work" else called  # /work is /autopilot's alias
            if called == "handoff":
                # /handoff (M.ORCH-T481) through the Skill tool: the user's to start, like /autopilot; off and status always go through.
                given = str((data.get("tool_input") or {}).get("args") or "").strip()
                if re.match(r"(off|cancel)\b", given, re.I):
                    clear_handoff()
                elif not re.match(r"status\b", given, re.I):
                    if "handoff" not in (session_state(data.get("session_id")).get("asked") or []):
                        refuse("/handoff is the user's to start, never yours: their latest prompt didn't ask for a handoff, so nothing was "
                               "recorded. Ask the user to type /handoff themselves.")
                    start_handoff(os.environ.get("MINDATTIC_CLI_PID") or cli_pid(), data.get("cwd"))
                raise SystemExit(0)
            if called in ("crossrepo", "autopilot"):
                given = str((data.get("tool_input") or {}).get("args") or "").strip()
                off = bool(re.match(r"off\b", given, re.I)) or (called == "crossrepo" and not given)
                if not off and called not in (session_state(data.get("session_id")).get("asked") or []):
                    refuse(f"/{called} is the user's to switch on, never yours: their latest prompt didn't ask for it, so nothing was "
                           f"recorded. Ask the user to type " + ("/crossrepo <repos>" if called == "crossrepo" else "/autopilot") + " themselves.")
                pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
                why = set_autopilot(pid, given, data.get("cwd") or "") if called == "autopilot" else set_crossrepo(pid, given)
                if why:
                    refuse(why)
                raise SystemExit(0)

        # While a /handoff winds every CLI down for an account switch, no new subagent starts: the ones working finish (M.ORCH-T481).
        if tool in ("Agent", "Task") and handing_off():
            refuse("A handoff is under way (/handoff): every CLI is winding down for an account switch, so no new subagent starts. "
                   "Let the ones already working finish, then quicksave this repo's handoff as /handoff says, and end your turn.")

        # The one exception is explicit, per CLI and until it closes: /crossrepo <A,B|all> (the context gate writes the mark).
        def allowed_here():
            # /crossrepo's repos, and the repos /autopilot is working in this CLI ("*" for all): its subagents edit and close there.
            pid = os.environ.get("MINDATTIC_CLI_PID") or cli_pid()
            found = set()
            for kind in ("crossrepo-", "autopilot-"):
                try:
                    found |= {r.strip().lower() for r in (Path.home() / ".claude" / "session-context" / (kind + str(pid))).read_text(encoding="utf-8", errors="replace").split(",") if r.strip()} if pid else set()
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
                    # Any case, as Windows runs it and the ticket lookup matches it (M.ORCH-T468).
                    called = re.match(r"\s*(?:&\s*)?['\"]?workflow(?:\.exe)?['\"]?\s+ticket\s+(close|correct)\b\s*(.*)", segment, re.I)
                    if not called:
                        continue  # filing a new ticket in another repo is how work is handed to it, so `new` is never refused
                    action = called.group(2)
                    named = re.search(r"--repo[\s=]+['\"]?([^\s'\"]+)", action)
                    owners = [named.group(1)] if named else []
                    # The ticket a close/correct acts on is its first argument; IDs in --summary or --ref are just text.
                    first = re.match(r"['\"]?((?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)\b", action.strip(), re.I)
                    owners += [ticket_repo(first.group(1))] if first else []
                    for owner in owners:
                        if other(owner):
                            refuse(f"This window is for {here}; that ticket belongs to {owner}. Only {here}'s tickets are worked here: "
                                   f"leave {owner}'s to its own window, autopilot or the General session.")
                # Only a write that `git -C` itself runs in the other repo: reading it is fine, and a word in a message, a file
                # name or this repo's own git on the same line isn't that repo's change.
                for segment in re.split(r"[;\n|]|&&", command):
                    for target in re.findall(r"\bgit\s+-C\s+['\"]?([^'\"\s]+)['\"]?(?:\s+-c\s+\S+|\s+--[\w-]+(?:=\S+)?)*\s+"
                                         r"(?:commit|push|pull|add|rm|mv|reset|restore|checkout|switch|merge|rebase|cherry-pick|revert|am|apply|clean|tag|branch|worktree|stash(?!\s+(?:list|show)\b))\b", segment):
                        if other(repo_of(target)):
                            refuse(f"This window is for {here}; git changes in {repo_of(target)} belong to that repo's window.")

        # Every repo lands on main (the master repos were renamed 2026-10-16): a push to master would only recreate a stray
        # branch on GitHub that triggers nothing (found live: an agent told to push main to a master repo made a stray main).
        if tool in ("Bash", "PowerShell"):
            for segment in re.split(r"[;\n|]|&&", str((data.get("tool_input") or {}).get("command") or "")):
                # push as git's own subcommand (after its global options), never the word inside a commit message
                if re.match(r"\s*(?:&\s*)?['\"]?git(?:\.exe)?['\"]?(?:\s+(?:-C\s+(?:\"[^\"]*\"|'[^']*'|\S+)|-c\s+\S+|--?[\w-]+(?:=\S+)?))*\s+push\b", segment) and re.search(r"(?:\s|:|\+|refs/heads/|['\"])master(?![\w./-])", segment) and not re.search(r"--delete\b", segment):
                    refuse("Every MindAttic repo's main line is main now (renamed from master 2026-10-16); pushing master would "
                           "recreate a stray branch. Push main instead: git push origin HEAD:main.")

        # A search of a whole drive never finishes in time and, orphaned when its agent stops, leaks handles until
        # Windows can't draw (found live: two `find /` held 660k handles and froze the desktop). Refuse it up front.
        import re
        cmd = str((data.get("tool_input") or {}).get("command") or "")
        ROOT_SEARCH = (r"(?:^|[;&|(\n])\s*(?:\"[^\"]*[\\/]|'[^']*[\\/]|[^\s\"';&|]*[\\/])?(find|fd|rg|grep|ls|dir|Get-ChildItem|gci|where)(?:\.exe)?[\"']?"
                       r"(?=\s)[^|;&\n]*?\s[\"']?(/|/[a-zA-Z]|[A-Za-z]:[\\/]?|~|\$HOME|\$env:USERPROFILE)[\"']?(?=\s|$|[;&|)])")
        # A heredoc's or here-string's body is data (a script, a file's text), not commands: `open(DIR / x)` in a Python
        # heredoc read as `dir /` (M.ORCH-T168). Each line of what's left starts a command, so `cd x` then `find /` is caught.
        def commands_only(c):
            c = re.sub(r"<<-?\s*([\"']?)(\w+)\1[^\n]*\n.*?\n\s*\2\s*(?=\n|$)", "", c, flags=re.S)
            c = re.sub(r"@([\"'])\s*\n.*?\n\1@", "", c, flags=re.S)
            # What cmd /c or powershell -c runs is a command too: `cmd /c dir C:\ /s` walks the drive all the same (M.ORCH-T362).
            return re.sub(r"\b(?:cmd(?:\.exe)?\s+/[ck]|(?:powershell|pwsh)(?:\.exe)?(?:\s+-(?!c\b|command\b)\w+(?:\s+(?!-)\S+)?)*\s+-c(?:ommand)?)\s+[\"']?",
                          "\n", c, flags=re.I)
        def arguments(segment):
            return [t[1:-1] if len(t) > 1 and t[0] == t[-1] and t[0] in "\"'" else t for t in re.findall(r"\"[^\"]*\"|'[^']*'|\S+", segment)][1:]
        def root_search(c, powershell):
            c = commands_only(c)
            # Each command naming a root is judged on its own: `ls && find / ...` is a search, `find . ; ls /` isn't.
            for m in re.finditer(ROOT_SEARCH, c, re.I):
                name = m.group(1).lower()
                segment = re.split(r"[;&|\n]", c[m.start(1):], maxsplit=1)[0]
                if name == "where":
                    # where /r <dir> walks <dir>: only a root after /r is a whole-drive search (M.ORCH-T362).
                    if re.search(r"\s/r\s+[\"']?(?:[A-Za-z]:[\\/]?|/|~)[\"']?(?=\s|$)", segment, re.I):
                        return True
                    continue
                # Only a path argument naming a root counts, never a switch or a search pattern (M.ORCH-T364): in PowerShell
                # find is find.exe (/i is its case switch, and it reads only the files it's given), cmd's dir takes /s and
                # /b, and grep's, rg's and fd's first argument is what they look for.
                if powershell and name == "find":
                    continue
                args = arguments(segment)
                paths = [a for a in args if not a.startswith("-") and not (name == "dir" and re.fullmatch(r"/[A-Za-z?]", a))]
                if name in ("grep", "rg", "fd") and not any(a in ("-e", "-f", "--regexp", "--file") or a.startswith(("--regexp=", "--file=")) for a in args):
                    paths = paths[1:]
                unix = not powershell and name in ("find", "fd", "rg", "grep", "ls")
                def is_root(a):
                    return bool(re.fullmatch(r"/|[A-Za-z]:[\\/]?|~|\$HOME|\$env:USERPROFILE", a, re.I) or (unix and re.fullmatch(r"/[A-Za-z]", a)))
                # `dir /s C:\x.exe` looks for x.exe in every folder of the drive.
                def drive_pattern(a):
                    return name == "dir" and bool(re.fullmatch(r"[A-Za-z]:[\\/][^\\/]*[.*?][^\\/]*", a))
                if not any(is_root(a) or drive_pattern(a) for a in paths):
                    continue
                if name not in ("ls", "dir", "get-childitem", "gci"):
                    return True
                # Listing a root is harmless; only a recursive listing walks the whole drive. PowerShell's parameters ignore
                # case (-recurse, -r), but bash's `ls -r` only reverses the order.
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

        # During a handoff the quicksave itself always goes through, at the window's stop too: a handoff is often for the account
        # that's run out, and every CLI's work would be lost to the switch (M.ORCH-T481).
        if tool in ("Bash", "PowerShell") and QUICKSAVE.search(cmd) and handing_off():
            gate_log("exempt", 0)
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

        # A CLI billed to an API key, a gateway or a cloud provider spends none of the subscription's windows: no stop, latch,
        # pace hold or subagent allowance is its to obey.
        if session_auth(data.get("cwd")) != "{{Sessions.ContextCycler.SubscriptionAuth}}":
            raise SystemExit(0)

        # Never past the limit, whatever else is set (/pacing off included): at the window's target a call is held as long
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
            s = status()
            note = orchestrator_stop(s)
            if note:
                return note
            own = claude_stop()
            return claude_stop_note(own, running=bool(s)) if own else None
        def hold_at_stop():
            # Checked before every other hold and through each of them, so a call held for the pace delay or a subagent slot
            # when the stop comes on is held on and refused, never let through once its wait ends (M.ORCH-T196: in the
            # minutes after the 99% stop, calls already held each went through when their own wait ran out).
            if stop_note() is None:
                return
            while time.monotonic() - start < MAX_HOLD and stop_note() is not None:
                if slot is not None:
                    try:
                        os.utime(slot)  # still held: other gates drop a slot past its life, and the call would go through uncounted
                    except Exception:
                        pass
                time.sleep(5)
            note = stop_note()
            if note is not None:
                release()
                gate_log("refused", time.monotonic() - start)
                refuse(note)
        hold_at_stop()

        # /pacing off exempts one CLI (its process, so subagents and /clear included) from holds until it closes or /pacing on (M.ORCH-T483).
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
            def mark_ns(m):
                # A mark whose subagent stops (or the daemon prunes) while it's counted is gone, not a reason to stop
                # counting: then the allowance didn't hold at all (M.ORCH-T366).
                try:
                    return m.stat().st_mtime_ns
                except OSError:
                    return -1
            def running(s):
                # The daemon's count leaves out marks whose stop never came; only marks newer than its count are added to it.
                marks = list(SUB.iterdir()) if SUB.exists() else []
                if s.get("subagents_running") is None:
                    return len(marks)
                # Newer than the newest mark the count saw, to the file time's own precision (M.ORCH-T320: against the
                # whole-second timestamp, up to a minute old, marks it had counted were counted again); an older daemon
                # writes only the timestamp.
                if s.get("subagents_seen_through_ns") is not None:
                    seen = int(s["subagents_seen_through_ns"])
                    return int(s["subagents_running"]) + sum(1 for m in marks if mark_ns(m) > seen)
                at = datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00")).timestamp()
                return int(s["subagents_running"]) + sum(1 for m in marks if mark_ns(m) / 1e9 > at)
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
    /// context fill the statusline saves, so the orchestrator can cycle a session past <paramref name="percent"/>
    /// once its turn is over (<see cref="Sessions.ContextCycler"/>). With <paramref name="keepGoing"/>, a turn that ends in a CLI that typed
    /// /autopilot while its repos still have workable tickets is told to carry on (<see cref="Settings.OrchestratorSettings.KeepGoing"/>).
    /// </summary>
    internal static string ContextScript(int percent, bool keepGoing = true) => $$$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's context gate (written by the orchestrator; edits are overwritten).
        # Records each session's turn state, which the orchestrator reads to save, clear and reload a session
        # whose context is past the cycle point once its turn is over.
        import json, os, re, sys, time
        from datetime import datetime, timezone
        from pathlib import Path

        DIR = Path.home() / ".claude" / "session-context"
        PERCENT = {{{percent}}}
        KEEP_GOING = {{{(keepGoing ? "True" : "False")}}}
        {{{WorkspacePython}}}
        {{{MarksPython}}}

        # A turn ending on one of these is asking for a go-ahead the orchestrator gives itself.
        ASKS = re.compile(r"\b(shall i|should i|do you want|want me to|would you like|ready for me to|ok(?:ay)? to (?:start|continue|proceed|go)|"
                          r"let me know (?:if|when|whether)|(?:go ahead|proceed|continue|carry on|keep going)\?)", re.I)

        def workable(repos, sid=None):
            # The open fixes and approved tickets of repos (names or wildcards such as GTFO.*; "*": every repo), in ticket order.
            try:
                import fnmatch, sqlite3
                db = sqlite3.connect(WORKFLOW_DB)
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
                # Only fixes and tickets the user approved are work; the rest wait for their yes. Approval lives in the database's
                # queue (orchestrator approve, workflow ticket approve, the Workflow Desktop), as the scheduler reads it (M.ORCH-T384).
                try:
                    approved = {a.upper() for (a,) in db.execute("SELECT ticket_id FROM ticket_queue WHERE approval = 'approved'").fetchall()}
                except Exception:
                    approved = set()
                db.close()
                try:
                    settings = json.loads((Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "settings.json").read_text(encoding="utf-8-sig"))
                except Exception:
                    settings = {}
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
                # A claim gone stale is back in the queue, and `orchestrator next` hands it out again: it's work. Only the
                # running daemon's list counts (it rewrites it each minute): one left by a daemon that stopped may name a
                # ticket claimed afresh since.
                # What `orchestrator next` itself holds back isn't work to push on to either: autopilot paused, quiet hours,
                # a skipped repo, a ticket another live session or subagent is working, a stale claim whose agent still runs.
                if settings.get("Autopilot") is False or handing_off():
                    return []
                skip = {(x or "").lower() for x in settings.get("AutopilotSkip") or []}
                holds = {}
                try:
                    hf = Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "{{{Daemon.AutopilotHoldsName}}}.json"
                    if time.time() - hf.stat().st_mtime <= {{{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}}:
                        holds = json.loads(hf.read_text()) or {}
                except Exception:
                    holds = {}
                if holds.get("Quiet"):
                    return []
                held = {(w.get("TicketId") or "").upper() for w in holds.get("Worked") or []
                        if not (w.get("Filed") and w.get("AgentId") is None and sid and w.get("SessionId") == sid)}
                held |= {(i or "").upper() for i in holds.get("LivePilot") or []}
                try:
                    stale = Path(os.environ.get("APPDATA", "")) / "MindAttic" / "MindAttic.Orchestrator" / "{{{Daemon.StaleTicketsName}}}.json"
                    if time.time() - stale.stat().st_mtime > {{{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}}:
                        raise ValueError("no daemon")
                    claimed -= {(i or "").upper() for i in json.loads(stale.read_text()) or []} - held
                except Exception:
                    pass
                claimed |= held
                named = {r.lower() for r in repos}
                return [r[0] for r in rows if any(fnmatch.fnmatchcase((r[2] or "").lower(), n) for n in named) and (r[2] or "").lower() not in skip and
                        ((r[1] or "").lower() == "fix" or r[0].upper() in approved) and r[0].upper() not in claimed and not blocked(r)]
            except Exception:
                return []

        def story_file_repos(repos):
            # Whether a repo named (or matched by a wildcard) keeps its tickets in its own docs/USER_STORIES.md, not the
            # Workflow database: workable() can't see those (their approvals live in settings' StoryApproved since
            # M.ORCH-T441), so `orchestrator next`, which reads them, is asked instead (M.ORCH-T445).
            try:
                import fnmatch, sqlite3
                try:
                    db = sqlite3.connect(WORKFLOW_DB)
                    try:
                        held = {(r or "").lower() for (r,) in db.execute("SELECT DISTINCT repo FROM tickets").fetchall()}
                    finally:
                        db.close()
                except Exception:
                    held = set()
                named = {r.lower() for r in repos}
                return any(d.name.lower() not in held and any(fnmatch.fnmatchcase(d.name.lower(), n) for n in named) and
                           (d / "docs" / "USER_STORIES.md").is_file() for d in WS.iterdir() if d.is_dir())
            except Exception:
                return False

        def no_subagent_room(limits, pid):
            # The pace's subagent allowance is used up (all CLIs together), so `orchestrator next` would hand out nothing:
            # pushing a stop back would only loop (M.ORCH-T188). An unthrottled CLI isn't held to it, as `next` isn't.
            try:
                allowed = limits.get("subagent_allowance")
                if allowed is None or (pid and (DIR / ("unthrottled-" + str(pid))).exists()):
                    return False
                sub = DIR / "subagents"
                marks = list(sub.iterdir()) if sub.exists() else []
                def mark_ns(m):
                    # Gone while counted (its stop came): not counted, rather than the whole check giving up (M.ORCH-T366).
                    try:
                        return m.stat().st_mtime_ns
                    except OSError:
                        return -1
                if limits.get("subagents_running") is None:
                    running = len(marks)
                elif limits.get("subagents_seen_through_ns") is not None:
                    # Only marks newer than the newest the daemon's count saw (M.ORCH-T320), as the pace gate counts.
                    seen = int(limits["subagents_seen_through_ns"])
                    running = int(limits["subagents_running"]) + sum(1 for m in marks if mark_ns(m) > seen)
                else:
                    at = datetime.fromisoformat(limits["timestamp"].replace("Z", "+00:00")).timestamp()
                    running = int(limits["subagents_running"]) + sum(1 for m in marks if mark_ns(m) / 1e9 > at)
                return running >= int(allowed)
            except Exception:
                return False

        def next_hands_out(repos, pid):
            # What `orchestrator next` itself would hand this CLI now (a dry run: nothing is claimed), so a stop is pushed back
            # only for tickets it will get. The files workable() reads trail it: a ticket another live session filed a moment
            # ago is held by next at once, but by the daemon's holds only after its next tick, and every stop until then was
            # sent back for a ticket next refused (M.ORCH-T385). None when next can't be asked (not installed, failed, too slow):
            # then workable()'s own count stands.
            try:
                import shutil, subprocess
                exe = os.environ.get("MINDATTIC_ORCHESTRATOR") or shutil.which("orchestrator") or r"C:\Apps\MindAttic.Orchestrator\Cli\orchestrator.exe"
                if not Path(exe).exists():
                    return None
                env = dict(os.environ)
                if pid:
                    env["MINDATTIC_CLI_PID"] = str(pid)  # this CLI, so what it filed itself stays its own (M.ORCH-T328)
                r = subprocess.run([exe, "next", "--repos", ",".join(repos)], capture_output=True, text=True, encoding="utf-8",
                                   errors="ignore", timeout=6, env=env, stdin=subprocess.DEVNULL)
                ids = re.findall(r"^=== (\S+) \(", r.stdout or "", re.M)
                return ids if r.returncode == 0 and (ids or "Nothing handed out" in (r.stdout or "")) else None
            except Exception:
                return None

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

        def restore_autopilot(pid, cwd, h):
            # The /autopilot a CLI in this repo had before the switch, given back to this one (each recorded CLI's to one new CLI,
            # the one in the same folder first): "off" when it was typed off, the command when it was on, None when none was recorded.
            mine = repo_of(cwd or "")
            if not pid or not mine:
                return None
            entries = [e for e in h.get("autopilot") or [] if isinstance(e, dict)]
            same = lambda e: os.path.normcase(os.path.normpath(str(e.get("cwd") or ""))) == os.path.normcase(os.path.normpath(str(cwd)))
            for i in sorted(range(len(entries)), key=lambda i: 0 if same(entries[i]) else 1):
                e = entries[i]
                if str(e.get("repo") or "").lower() != mine.lower():
                    continue
                try:
                    os.close(os.open(str(DIR / ("handoff-restored-" + str(i))), os.O_CREAT | os.O_EXCL | os.O_WRONLY))
                except FileExistsError:
                    continue  # another CLI starting here already took it
                on, off = DIR / ("autopilot-" + str(pid)), DIR / ("noautopilot-" + str(pid))
                if e.get("off"):
                    off.write_text("", encoding="utf-8")
                    if on.exists():
                        on.unlink()
                    return "off"
                if e.get("repos"):
                    on.write_text(str(e["repos"]), encoding="utf-8")
                    if off.exists():
                        off.unlink()
                    return "/autopilot " + ("all" if e["repos"] == "*" else "-repo=" + str(e["repos"]))
            return None

        def load_handoff(cwd, h):
            # The handoff this folder's CLI quicksaved for the switch, loaded as /quickload loads it (the shared runner's load, which
            # archives it); None when none was saved here since the handoff began, or it can't be loaded in time.
            try:
                import subprocess
                session = Path(cwd) / ".prose" / "agent-session.json"
                saved = str(json.loads(session.read_text(encoding="utf-8-sig")).get("updatedAt") or "")
                at = lambda t: datetime.fromisoformat(re.sub(r"(\.\d{6})\d+", r"\1", str(t)).replace("Z", "+00:00")).timestamp()
                if at(saved) < at(h.get("started")):
                    return None
                runner = WS / "mindattic-agent-standard" / "prose-agent.ps1"
                if not runner.exists():
                    return None
                r = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(runner), "load", "-RepoRoot", str(cwd)],
                                   capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=7, stdin=subprocess.DEVNULL)
                return r.stdout.strip() if r.returncode == 0 and r.stdout.strip() else None
            except Exception:
                return None

        def orchestrator_status():
            # rl-status.json while the orchestrator keeps it fresh; {} when it's stale or someone else's. Read as the pace gate
            # reads it, retried while the daemon is replacing it: once, a Stop then saw no status at all (M.ORCH-T363).
            try:
                s = read_json(Path.home() / ".claude" / "rl-status.json")
                age = (datetime.now(timezone.utc) - datetime.fromisoformat(s["timestamp"].replace("Z", "+00:00"))).total_seconds()
                fresh = age <= {{{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}} and not before_switch(s["timestamp"], True)
                return lapsed(s) if s.get("source") == "MindAttic.Orchestrator" and fresh else {}
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

        # A turn ending past the wind-down line that says it leaves work for the reset ("continue after 3:30 PM", "once the
        # limit resets"): it's waiting on the window, not done (M.ORCH-T256).
        LEFT_FOR_RESET = re.compile(r"\b(?:resets?|limit|wind[- ]?down|5-hour|weekly|continue\s+(?:after|once|when)|after\s+\d{1,2}(?::\d{2})?\s*[ap]\.?m\b)", re.I)

        def iso_epoch(text):
            try:
                return datetime.fromisoformat(str(text).replace("Z", "+00:00")).timestamp()
            except Exception:
                return None

        def limit_held(limits, data, now):
            # {"until", "window", "at"} when this turn ended held by a window: at its stop (the orchestrator's, fresh or latched,
            # or Claude's own line), or past the wind-down line saying it leaves work for after the reset; None otherwise. The
            # daemon types a resume into the CLI (or asks you to) once that window has reset (M.ORCH-T256: a CLI that stopped
            # at 94% sat idle past the reset until its author noticed).
            until = window = None
            if limits.get("stop"):
                until, window = iso_epoch(limits.get("stop_until")), limits.get("stop_window")
            if until is None:
                l = latched("orchestrator")
                if l:
                    until, window = float(l["until"]), window or l.get("window")
            if until is None:
                own = claude_stop()
                if own:
                    until, window = own[1], own[2]
            if until is None and limits.get("wind_down_note") and LEFT_FOR_RESET.search(last_said(data)):
                until, window = iso_epoch(limits.get("window_reset")), "5-hour"
            if until is None or until <= time.time():
                return None
            return {"until": until, "window": window or "5-hour", "at": now}

        # Typed at the window's stop, these still go through: they're how you steer the orchestrator itself.
        # /goal goes through at the stop only to clear one: a new goal there would run turn after turn its refused calls can't finish.
        CONTROL = re.compile(r"/(autopilot|work|crossrepo|pacing|handoff)\b|/goal\s*(?:(?:clear|off|stop|reset|none|cancel)\s*)?$", re.I)

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

        {{{BillingPython}}}

        {{{ExemptPython}}}

        def my_pid():
            return os.environ.get("MINDATTIC_CLI_PID") or cli_pid()

        try:
            # UTF-8 as Claude Code writes it, not the ANSI code page Python reads a pipe in on Windows (M.ORCH-T392).
            data = json.loads(sys.stdin.buffer.read().decode("utf-8", errors="replace"))
        except Exception:
            sys.exit(0)
        if not isinstance(data, dict):
            sys.exit(0)
        sid, event = data.get("session_id"), data.get("hook_event_name")
        # Codex (its Stop hook) and Gemini (AfterAgent) run this too, named on the command line: nothing here applies to
        # them (their sessions aren't Claude's to cycle, and only a Claude CLI that typed /autopilot is kept going).
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
                    # A CLI billed to an API key, a gateway or a cloud provider takes no slot and spends none of the
                    # subscription's windows: its subagents mustn't fill the subscription's allowance either.
                    if session_auth(data.get("cwd")) != "{{{Sessions.ContextCycler.SubscriptionAuth}}}":
                        sys.exit(0)
                    mark.write_text(str(cli_pid() or ""))
                    # The mark now counts it, so the slot the pace gate kept for its call is let go: this CLI's oldest. Never
                    # another CLI's: a subagent that took no slot (a CLI with /pacing off, an exempt turn) would free a place
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
        if event == "SessionStart":
            # Every CLI is told at its start what it bills (M.ORCH-T434), as Prose checks its hub first; recorded in the session's
            # state for the daemon and the dashboard. Nothing here may hold up or fail the start: on any error it says nothing.
            try:
                auth, line, warn = billing_check(data.get("cwd"))
            except Exception:
                sys.exit(0)
            handoff_note = None
            try:
                DIR.mkdir(parents=True, exist_ok=True)
                state_file = DIR / (sid + ".state.json")
                state = load(state_file)
                state.update(auth=auth, billing=line, billing_warn=warn, billing_account=config_account(claude_config()) or "",
                             pid=state.get("pid") or cli_pid(), cwd=state.get("cwd") or data.get("cwd"))
                save(state_file, state)
                # After a /handoff (M.ORCH-T481): the first start once it's quiet clears the hold (new work starts again), and each
                # start gets back its repo's /autopilot and loads the handoff its CLI saved there, before the default could switch it.
                h = handoff()
                if h and h.get("state") == "quiet":
                    h = dict(h, state="resumed", resumed_at=utc_stamp())
                    write_handoff(h)
                if h and h.get("state") == "resumed":
                    restored = restore_autopilot(my_pid(), data.get("cwd"), h)
                    loaded = load_handoff(data.get("cwd"), h)
                    said = ["[Handoff] Picked up after the account switch."]
                    if restored == "off":
                        said.append("/autopilot stays off in this CLI, as it was before.")
                    elif restored:
                        said.append(restored + " is back on in this CLI, as it was before.")
                    if loaded:
                        said.append("This CLI's handoff is loaded, as /quickload loads it: confirm in one line what you're resuming, then pick up its "
                                    "Task, honour its Decisions and continue from its Next without re-asking anything settled.\n" + loaded)
                    if len(said) > 1:
                        handoff_note = " ".join(said)
                autopilot_by_default(state["pid"], data.get("cwd"))
            except Exception:
                pass
            if handoff_note:
                print(json.dumps({"systemMessage": line + "\n" + handoff_note.split("\n")[0],
                                  "hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": line + "\n" + handoff_note}}))
                sys.exit(0)
            print(json.dumps({"systemMessage": line, "hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": line}}))
            sys.exit(0)
        # Recorded in the session's state, so the daemon leaves a CLI billed elsewhere out of the subscription's budget; and
        # nothing the subscription's windows decide holds it here.
        auth = session_auth(data.get("cwd"))
        paced = auth == "{{{Sessions.ContextCycler.SubscriptionAuth}}}"
        if event == "UserPromptSubmit" and paced:
            # At the window's stop a typed message is answered at once by this, not by the model: nothing is spent, so
            # the limit is never passed even by your own prompts. The orchestrator's control commands still go through.
            limits = orchestrator_status()
            if orchestrator_stop(limits) and not limits.get("stop"):
                # Latched from before the daemon stalled (M.ORCH-T196): with its window and reset, so a weekly stop says so.
                l = latched("orchestrator") or {}
                limits = dict(limits, stop=True, stop_window=limits.get("stop_window") or l.get("window"),
                              reset_time=limits.get("reset_time") or l.get("reset_time"))
            own = None if limits.get("stop") else claude_stop()
            if own:
                limits = dict(limits, stop=True, percent_used=own[0], reset_time=reset_text(own[1]), stop_window=own[2])
            # So do the exempt commands (M.ORCH-T198): their turns skip every Orchestrator gate.
            if limits.get("stop") and not CONTROL.match((data.get("prompt") or "").strip()) and not exempt_prompt(data.get("prompt")):
                try:
                    DIR.mkdir(parents=True, exist_ok=True)
                    with open(DIR / "{{{GateLogName}}}", "a", encoding="utf-8") as log:
                        log.write(json.dumps({"at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "decision": "prompt-answered",
                                              "held": 0, "tool": None, "percent": limits.get("percent_used")}) + "\n")
                except Exception:
                    pass
                print(json.dumps({"decision": "block", "reason": "Not sent, so no tokens were spent: the " + str(limits.get("stop_window") or "5-hour") + " window is at its target "
                    "(MindAttic.Orchestrator never lets it pass the limit). Typing works again when it resets at " +
                    str(limits.get("reset_time") or "the end of the window") + ". /autopilot, /pacing and /goal clear still go through."}))
                sys.exit(0)
        DIR.mkdir(parents=True, exist_ok=True)
        state_file = DIR / (sid + ".state.json")
        state = load(state_file)
        now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        if event == "Stop":
            state.pop("exempt", None)  # an exempt command's turn ends here, and with it the exemption (M.ORCH-T198)
            # The orchestrator keeps development going without you: a turn that ends asking whether to go on, or that
            # closed a ticket and stopped, while its repo still has open tickets is told to carry on. One that stops
            # again straight after without doing anything is let go (something there really needs you). Each is logged
            # to keep-going.jsonl, which the daemon reports.
            insisted = state.get("nudged") and not state.get("worked")
            why = None
            limits = orchestrator_status()
            # At the window's target nothing more may run, so a stop is never pushed back to work: the orchestrator's stop,
            # latched or fresh, or Claude's own percent at the line with the daemon down, as a typed prompt is held to.
            stopped = paced and (bool(limits.get("stop")) or orchestrator_stop(limits) is not None or claude_stop() is not None)
            # A handoff (M.ORCH-T481): no CLI is sent back for work; one whose turn ends without having quicksaved is told to, once.
            winding = handing_off()
            if winding and winding.get("state") == "winding" and not handoff_saved(my_pid()) and not state.get("handoff_told"):
                state["handoff_told"] = True
                save(state_file, state)
                print(json.dumps({"decision": "block", "reason": handoff_save_told(repo_of(data.get("cwd") or "") or Path(str(data.get("cwd") or ".")).name)}))
                sys.exit(0)
            # Only a CLI that typed /autopilot is kept going, on its own repos, while they have workable tickets.
            repos = [] if winding else autopilot_repos(my_pid())
            if repos and KEEP_GOING and not stopped and not state.get("goal"):
                why = "autopilot"
            # Past the wind-down line `orchestrator next` starts nothing new before the reset (M.ORCH-T254), unless this CLI is
            # unthrottled: pushing the stop back would only spend a turn being told so.
            wound = bool(limits.get("wind_down_note")) and not (DIR / ("unthrottled-" + str(my_pid()))).exists()
            eligible = bool(why) and not wound and not no_subagent_room(limits, my_pid())
            ids = workable(repos, sid) if eligible else []
            if ids or (eligible and story_file_repos(repos)):
                # Only what next would hand out: the count above is the cheap first look, next has the last word. A repo on
                # its own USER_STORIES.md has tickets the count can't see, so next is asked for it even when the count is empty.
                handed = next_hands_out(repos, my_pid())
                if handed is not None:
                    ids = handed
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
            state.update(state="idle", at=now, pid=cli_pid(), cwd=data.get("cwd"), auth=auth)
            # Held by a window (its stop, or the wind-down with work left for the reset): the daemon wakes it once that window
            # resets. Any other end of a turn isn't waiting on anything.
            held = limit_held(limits, data, now) if paced and (stopped or limits.get("wind_down_note")) else None
            if held:
                state["limit_stop"] = held
            else:
                state.pop("limit_stop", None)
            if stopped and state.get("goal"):
                # /goal's own Stop hook sends the turn back while its condition isn't met, and at the stop every tool call is
                # refused, so it never is: each round would spend a turn past the line. continue:false outranks its block.
                save(state_file, state)
                # The stop's own window: a weekly wall says so, as the refusal of a typed prompt does.
                own = None if limits.get("stop_window") else claude_stop()
                window = limits.get("stop_window") or (latched("orchestrator") or {}).get("window") or (own[2] if own else None) or "5-hour"
                print(json.dumps({"continue": False, "stopReason": "The " + str(window) + " window is at its target, so /goal can't go on before it "
                    "resets; the goal stays set (/goal clear drops it)."}))
                sys.exit(0)
        elif event == "UserPromptSubmit":
            # A new prompt (yours, or the daemon's resume after the reset) is the CLI going on: it's no longer waiting on a window.
            for k in ("nudged", "worked", "closed", "limit_stop", "handoff_told"):
                state.pop(k, None)
            if state.get("goal") and goal_finished(state.get("transcript"), state["goal"]):
                state.pop("goal", None)
            if data.get("transcript_path"):
                state["transcript"] = data["transcript_path"]
            # The CLI's process id from its first prompt on: `orchestrator next` finds the calling CLI's own session by it, and
            # a CLI whose every stop is pushed back (an /autopilot run) never reaches the Stop that used to record it.
            state.update(state="busy", at=now, warned=False, cwd=data.get("cwd"), pid=cli_pid(), auth=auth)
            # A turn typed as an exempt command (/deploy, /run, ...) skips every pace gate until it stops (M.ORCH-T198).
            exempt = exempt_prompt(data.get("prompt"))
            if exempt:
                state["exempt"] = exempt
            else:
                state.pop("exempt", None)
            typed = (data.get("prompt") or "").strip()
            prompt = typed.lower()
            # Whether this prompt asks for cross-repo work or autopilot: the pace gate lets the model start either through the
            # Skill tool only then, never on its own (M.ORCH-T435). Each prompt replaces the last one's note.
            asked = asked_for(typed)
            if asked:
                state["asked"] = asked
            else:
                state.pop("asked", None)
            # On by default, so a CLI open before the default (or before its SessionStart hook) gets it here; a typed /autopilot
            # off below then removes it and keeps it off.
            try:
                autopilot_by_default(my_pid(), data.get("cwd") or "")
            except Exception:
                pass
            # /autopilot (set_autopilot): THIS CLI works those repos' tickets through its own subagents until /autopilot off here.
            # /work is its alias; not /workflow or /worktree.
            command = "/autopilot" if prompt.startswith("/autopilot") else "/work" if re.match(r"/work\b", prompt) else None
            if command:
                why = set_autopilot(my_pid(), typed[len(command):].strip(), data.get("cwd") or "")
                if why:
                    print(json.dumps({"decision": "block", "reason": why}))
                    sys.exit(0)
            # /handoff (M.ORCH-T481) winds every CLI down for an account switch; /handoff off cancels it; /handoff status only asks.
            # Its turn quicksaves, so it skips every pace gate as an exempt command's does, at the window's stop too.
            handing = None
            if re.match(r"/handoff\b", prompt):
                args = typed[len("/handoff"):].strip()
                if re.match(r"(off|cancel)\b", args, re.I):
                    clear_handoff()
                elif not re.match(r"status\b", args, re.I):
                    start_handoff(my_pid(), data.get("cwd"))
                    state["exempt"] = "/handoff"
            else:
                handing = handing_off()
                # Quiet, and this CLI is now signed in to another account (a /login rather than a restart): the switch is made.
                try:
                    if handing and handing.get("state") == "quiet" and handing.get("account") and account()[0] and account()[0] != handing["account"]:
                        write_handoff(dict(handing, state="resumed", resumed_at=utc_stamp()))
                        handing = None
                except Exception:
                    pass
            # /goal <condition> runs turn after turn until its condition holds; cycling would end it, so the session is
            # marked and left to Claude's own compaction until /goal clear (M.ORCH-T93).
            if re.match(r"/goal\b", prompt):
                args = typed[len("/goal"):].strip()
                # Claude Code clears a goal on any of these; a bare /goal only shows its status, and leaves it running.
                # Only the word alone: "/goal clear all lint warnings" sets a new goal in Claude Code, as it does here.
                if re.fullmatch(r"(clear|off|stop|reset|none|cancel)", args, re.I):
                    state.pop("goal", None)
                elif args:
                    state["goal"] = args
            # /crossrepo <A,B|all> lets this CLI work other repos too (the explicit exception to M.ORCH-T95); /crossrepo off ends it.
            if prompt.startswith("/crossrepo"):
                set_crossrepo(my_pid(), typed[len("/crossrepo"):].strip())
            # /pacing off runs THIS CLI (its process, so subagents and /clear included) without pace holds until it closes or
            # /pacing on; on is every new CLI's default. A bare /pacing says which, answered here unsent (M.ORCH-T483).
            # The stop at the window's target and the weekly stop hold either way.
            if re.match(r"/pacing\b", prompt):
                pid = my_pid()
                arg = prompt[len("/pacing"):].strip()
                mark = DIR / ("unthrottled-" + str(pid)) if pid else None
                if mark is not None and arg == "off":
                    mark.write_text(now)
                elif mark is not None and arg == "on":
                    if mark.exists():
                        mark.unlink()
                else:
                    if mark is None:
                        why = "Pacing: this CLI's process couldn't be found, so its pacing can't be read or changed."
                    elif mark.exists():
                        why = ("Pacing is off in this CLI: no pace holds until it closes or you type /pacing on. "
                               "The stop at the window's target and the weekly stop still apply.")
                    else:
                        why = "Pacing is on in this CLI (the default): its tool calls are held to the pace. /pacing off lifts the holds until it closes."
                    if arg:
                        why = "/pacing takes on or off; nothing changed. " + why
                    print(json.dumps({"decision": "block", "reason": why}))
                    sys.exit(0)
            # Each turn starts knowing how many subagents the pace allows right now, and whether the window is winding down.
            s = orchestrator_status()
            notes = [n for n in (s.get("subagent_note"), s.get("stop_note") or s.get("wind_down_note")) if n]
            if handing:
                notes.append("A handoff is under way (/handoff): every CLI is winding down for an account switch, so start no ticket or "
                             "subagent; when this turn's work is done, quicksave this repo's handoff as /handoff says. /handoff off cancels it.")
            state["wind_down_told"] = bool(s.get("wind_down_note") or s.get("stop_note"))
            out = {"hookSpecificOutput": {"hookEventName": "UserPromptSubmit", "additionalContext": " ".join(notes)}} if notes else {}
            # A CLI that started before its SessionStart hook was registered is checked on its first prompt instead, and told
            # only when it's billing the key rather than the claude.ai login (M.ORCH-T434).
            # After a /login to another account the line from its start names the old one: the first prompt since says the new
            # line, once (M.ORCH-T486). A state from before the account was recorded beside the line just records it.
            signed = None
            try:
                signed = config_account(claude_config()) or ""
            except Exception:
                pass
            if "billing" not in state:
                try:
                    _, line, warn = billing_check(data.get("cwd"))
                    state.update(billing=line, billing_warn=warn, billing_account=signed)
                    if warn:
                        out["systemMessage"] = line
                        told = out.setdefault("hookSpecificOutput", {"hookEventName": "UserPromptSubmit", "additionalContext": ""})
                        told["additionalContext"] = (told["additionalContext"] + " " + line).strip()
                except Exception:
                    pass
            elif signed is not None and state.get("billing_account") is None:
                state["billing_account"] = signed
            elif signed is not None and state.get("billing_account") != signed:
                try:
                    auth, line, warn = billing_check(data.get("cwd"))
                    was = state.get("billing")
                    state.update(auth=auth, billing=line, billing_warn=warn, billing_account=signed)
                    if line != was:
                        out["systemMessage"] = line
                        told = out.setdefault("hookSpecificOutput", {"hookEventName": "UserPromptSubmit", "additionalContext": ""})
                        told["additionalContext"] = (told["additionalContext"] + " " + line).strip()
                except Exception:
                    pass
            # A dead or stalled daemon is said to you, on every prompt, until it's back (M.ORCH-T167): nothing is paced
            # meanwhile except Claude's own 99% stop.
            down = daemon_down()
            if down is not None:
                out["systemMessage"] = ((out["systemMessage"] + " ") if out.get("systemMessage") else "") + ("MindAttic.Orchestrator hasn't updated the pace for " + str(int(down // 60)) + " min: its daemon "
                    "isn't running, so agents aren't being paced (only Claude's own " + str(STOP_PERCENT) + "% stop still holds). "
                    "Start it: C:\\Apps\\MindAttic.Orchestrator\\MindAttic.Orchestrator.exe")
            if out:
                print(json.dumps(out))
        elif event == "PostToolUse":
            touched = False
            # The quicksave a handoff asks for: this CLI's saved (M.ORCH-T481).
            try:
                if QUICKSAVE.search(str((data.get("tool_input") or {}).get("command") or "")) and handing_off():
                    mark_handoff_saved(my_pid(), repo_of(data.get("cwd") or "") or Path(str(data.get("cwd") or ".")).name)
            except Exception:
                pass
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
    /// Returns what it did, or null when everything was already in place. Throws <see cref="IOException"/> when
    /// <c>settings.json</c> can't be read (it's left alone), so a caller tries again rather than taking the hooks as in place.</summary>
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
            (Path.Combine(dir, "commands", "pacing.md"), PacingCommand),
            (Path.Combine(dir, "commands", "autopilot.md"), AutopilotCommand),
            (Path.Combine(dir, "commands", "work.md"), WorkCommand),
            (Path.Combine(dir, "commands", "crossrepo.md"), CrossRepoCommand),
            (Path.Combine(dir, "commands", "handoff.md"), HandoffCommand),
        })
        {
            if (File.Exists(path) && File.ReadAllText(path) == body) continue;
            WriteReplacing(path, body);
            did.Add("wrote " + Path.GetFileName(path));
        }
        did.AddRange(DeleteRetiredCommands(Path.Combine(dir, "commands")));

        var settingsPath = Path.Combine(dir, "settings.json");
        JsonObject? root;
        try { root = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath), documentOptions: StrictKeys) as JsonObject : new JsonObject(); }
        catch (JsonException) { root = null; }
        // Unreadable (mid-rewrite by Claude Code, or broken): someone else's file is left alone, but it's said, not
        // reported as done, so the daemon tries again next tick instead of never registering the hooks (M.ORCH-T376).
        if (root is null)
            throw new IOException($"{settingsPath} isn't a JSON object it can read, so the pace and context gate hooks aren't registered; it's left as it is"
                + (did.Count > 0 ? $" ({string.Join(", ", did)})" : ""));
        var changed = AddHook(root, "PreToolUse", ScriptName, Command(script), HookTimeoutSeconds);
        foreach (var hookEvent in new[] { "PostToolUse", "Stop", "UserPromptSubmit", "SubagentStart", "SubagentStop", "SessionStart" })
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
            WriteReplacing(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
    /// <c>PreToolUse</c> in <c>~/.kimi-code/config.toml</c>. The script's own CLI-walk and /pacing off check
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
        // Codex's Stop and Gemini's AfterAgent run the context gate too, but it leaves them be: only a Claude CLI that typed
        // /autopilot is kept going. Kimi has no end-of-turn hook at all.
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
    /// someone else's unreadable JSON. A file with comments is edited in place so they survive, or, when that can't be
    /// done reliably, left alone with a note saying how to add the hook by hand (M.ORCH-T442).</summary>
    private static string? MergeJsonHook(string path, string hookEvent, string scriptName, string command, int timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = File.Exists(path) ? File.ReadAllText(path) : null;
        JsonObject? root;
        try { root = text is not null ? JsonNode.Parse(text, documentOptions: CommentedStrictKeys) as JsonObject : new JsonObject(); }
        catch (JsonException) { return null; }
        if (root is null || !AddHook(root, hookEvent, scriptName, command, timeout)) return null;
        if (text is not null && CommentedHookSplice.HasComments(text))
        {
            if (CommentedHookSplice.Splice(text, root, hookEvent, scriptName, command, timeout) is not { } spliced)
                return CommentedHookSplice.ByHand(Path.GetFileName(path), hookEvent, scriptName, command, timeout);
            WriteReplacing(path, spliced);
            return $"wired {hookEvent} into {Path.GetFileName(path)}, keeping its comments";
        }
        WriteReplacing(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return $"wired {hookEvent} into {Path.GetFileName(path)}";
    }

    /// <summary>A repeated key (a hand edit; JavaScript keeps the last one) is a JsonException at parse, which the readers catch:
    /// by default JsonNode only throws an ArgumentException on first access, which escaped every daemon tick (M.ORCH-T427).</summary>
    private static readonly JsonDocumentOptions StrictKeys = new() { AllowDuplicateProperties = false };

    /// <summary>Gemini CLI reads its settings.json with comments stripped: one with a // comment was taken as unreadable and
    /// skipped without a word, so Gemini's tool calls ran ungated (M.ORCH-T428). Its comments are kept: see <see cref="CommentedHookSplice"/>.</summary>
    private static readonly JsonDocumentOptions CommentedStrictKeys = new() { AllowDuplicateProperties = false, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Puts <paramref name="text"/> in place whole: a hook started mid-write would run half a script (a SyntaxError
    /// Claude Code shrugs off, so that call goes ungated), and the temp file is this writer's own, so the dashboard and the
    /// daemon registering at once can't move or overwrite each other's.</summary>
    private static void WriteReplacing(string path, string text)
    {
        var temp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    /// <summary>Appends a <c>[[hooks]]</c> table to Kimi's <c>config.toml</c> (a plain text append: Kimi's own
    /// config has no JSON equivalent and no library here parses TOML); false when <paramref name="command"/>
    /// is already in the file.</summary>
    private static bool AppendKimiHook(string path, string command, int timeoutSeconds)
    {
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        // A TOML literal string ('...') can't hold an apostrophe (a profile like C:\Users\O'Brien): then it's a basic string,
        // and an entry an earlier version wrote broken that way is mended in place (M.ORCH-T326).
        var value = TomlString(command);
        var broken = $"command = '{command}'";
        if (command.Contains('\'') && existing.Contains(broken, StringComparison.Ordinal))
        {
            File.WriteAllText(path, existing.Replace(broken, "command = " + value, StringComparison.Ordinal));
            return true;
        }
        if (existing.Contains(command, StringComparison.Ordinal) || existing.Contains(value, StringComparison.Ordinal)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var block = $"[[hooks]]\nevent = \"PreToolUse\"\ncommand = {value}\ntimeout = {timeoutSeconds}\n";
        File.WriteAllText(path, (existing.Length > 0 ? existing.TrimEnd('\r', '\n') + "\n\n" : "") + block);
        return true;
    }

    /// <summary><paramref name="text"/> as a TOML string: literal ('...') unless it holds an apostrophe, else basic ("...")
    /// with its backslashes and double quotes escaped.</summary>
    internal static string TomlString(string text) =>
        !text.Contains('\'') ? $"'{text}'" : "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

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
