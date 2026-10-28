# Spike: a structured activity feed beside the real terminals (M.ORCH-T937)

Phase 4 of the CLI-host spike ([cli-host-viewer.md](cli-host-viewer.md), M.ORCH-T922, sections 6 and 8). The terminal
mirror (TermHost and the team gateway) shows pixels. The Team Wall (M.ORCH-T936) and phones also want a compact,
structured "what is each CLI doing right now" feed. This evaluates three sources for it.

## Recommendation (read this first)

**Use (a): our own hooks, extended, and carried on the status bus as a new `activity` message kind.** It is the only
source that works for every CLI we run, needs no extra process or relay, costs no tokens, is already inside our trust
boundary, and is the one we can redact before anything leaves the machine. Stream-json (b) cannot observe the real
interactive CLIs, so it only fits a different product (headless workers). Remote Control (c) has no machine-readable
surface for us and routes through Anthropic. Keep (c) as the user-facing phone option, which is what it is already for.

Estimated effort: about 1 week, five tickets (section 6). The first three are the core; Codex and Gemini coverage is a
separate ticket because their hook sets are thinner.

## 1. What we have today (from the code)

- **Hooks.** `AgentGate.cs` installs two Python hooks into `~/.claude/settings.json`. The pace gate
  (`pace-gate.py`) runs on `PreToolUse` for every tool call and sees `tool_name`, `tool_input`, `session_id`, `cwd`,
  and `agent_id` (set when a subagent made the call). The context gate (`context-gate.py`) runs on `UserPromptSubmit`,
  `PostToolUse`, `Stop`, `SubagentStart`, `SubagentStop` and `Notification`. It keeps a per-session state file
  (busy/idle, `asking`, `transcript`, `pid`, `cwd`, ...). `asking` is set on a `permission_prompt` or
  `elicitation_dialog` Notification and on an `AskUserQuestion` PreToolUse (M.ORCH-T790).
- **Daemon.** The daemon reads those files plus transcripts and builds `Snapshot.Agents`, a list of `AgentBoardRow`
  (`Activity/AgentBoard.cs`): `SessionId`, `Repo`, `TicketId`, `TicketType`, `State` (Working, Waiting, HeldByPace),
  `StateDetail`, the asking flag, `Tokens` (`TokenUsage`), `Subagents`, `Claims`, `Login`, and `Doing` with `DoingAtUtc`.
- **`Doing` comes from the transcript tail, not the hooks.** `TranscriptReader` takes the latest `tool_use` block and
  `ToolCallText.Describe` turns it into words ("Editing Foo.cs", "Reading Foo.cs", "Searching for 'x'"). So the
  current-tool field is as fresh as the daemon's next look at the transcript, not as fresh as the hook.
- **Bus.** Since M.ORCH-T872/T879/T891 the daemon pushes state on the status pipe (NDJSON envelope
  `{kind, v, at, pid, payload}`, `docs/app-bus.md`, `Core/Status/AppBus.cs`). `Agents` travels inside the snapshot
  figures (`SnapshotFigures.Agents`), so a consumer gets the whole board per snapshot, not per-event deltas.
- **Other providers.** `AgentGate.cs` wires the pace gate into Codex's `PreToolUse` (`~/.codex/hooks.json`) and
  Gemini's `BeforeTool` (`~/.gemini/settings.json`), and the context gate into Codex's `Stop` and Gemini's
  `AfterAgent`, passing the provider on the command line. The context gate exits at once for non-Claude providers
  (`if provider != "Claude": sys.exit(0)`), so today it records nothing for them.

## 2. The three sources

### (a) Our hooks, extended, plus the bus

| Aspect | Finding |
|---|---|
| Fields | Current tool and its input (so file, command, URL, pattern) at `PreToolUse`; completion at `PostToolUse`; busy/idle at prompt and Stop; `asking` from Notification; subagent start/stop with `agent_id`; ticket from the claim record (already on the board row); `transcript_path` for deeper reads. **Tokens** are not in hook payloads: they stay a transcript read in the daemon (`TokenUsage`), which is fine for a feed that refreshes tokens every few seconds. **Last message summary** is not a hook field either (Stop has the transcript path, not the text); derive it from the transcript tail on Stop only, truncated, or omit it from v1. |
| Latency | Event-driven: a hook fires before and after each tool call. A local write plus one best-effort inbox line (the T892 statusline already does this with a 50 ms connect and all failures swallowed) puts the update on the bus in tens of milliseconds, versus the transcript poll today. Hook cost is the concern: the gates are Python processes started per call, so extra work in them must stay in the existing process and be a single small write. |
| Codex, Gemini | Partly. Both already run our pre-tool hook (`PreToolUse`, `BeforeTool`) and a stop-equivalent (`Stop`, `AfterAgent`), so "which tool, which target, finished" is available. Missing: prompt-submit and post-tool equivalents are not wired, so state is coarser. Needs per-provider payload mapping (field names differ). Kimi has the pace hook only. |
| Privacy | Strongest. Redaction runs in our own code before a byte is published, and we choose the fields. Tool inputs are the risk: a Bash command or URL can hold a token. v1 sends a short target, never the raw input (section 4). |
| Effort | Lowest: about a week. Extend two existing scripts, add one bus kind and one consumer. |

### (b) stream-json, headless mode and the Agent SDK

| Aspect | Finding |
|---|---|
| Fields | Rich: `system` init (session id, model, tools), `assistant` messages with text and `tool_use` blocks (tool, full input), `user` tool results, and a final `result` with usage, cost and duration. Tokens, tool and last message are all present natively. Permission requests surface as SDK permission callbacks, not as a TUI prompt. |
| Latency | Streaming, effectively real time (per token with partial-message streaming). Best of the three for latency and for the last-message text. |
| Codex, Gemini | No single format. Codex has `codex exec --json` and Gemini has its own `--output-format stream-json`, with different event schemas, so it is three adapters. They are also headless modes, which brings the next point. |
| The blocking problem | `claude -p` is **a different process from the interactive TUI**. It cannot be attached to a running interactive session, and the live CLIs in the dashboard are interactive. So stream-json can only describe sessions that we *start* headless (allocate background agents, scripted runs), and would need those to run through our wrapper. Our hooks and statusline behave differently there (cli-host-viewer.md section 6). The user's terminals are exactly what the feed is for, and stream-json cannot see them. |
| Privacy | Worst by default: full tool inputs and results, file contents and command output are in the stream. We would have to filter every event; the volume makes missing something likely. |
| Effort | Highest: a runner or SDK host, an event parser per provider, and a migration of live work off the TUI. Only worthwhile if we later move automated agents (allocate) to headless anyway, in which case the board could read their stream for free as a bonus. |

### (c) `/remote-control`

| Aspect | Finding |
|---|---|
| Fields | Everything in the conversation, shown by claude.ai/code and the mobile app: messages, tool calls, and permission prompts that can be answered from the phone. |
| Latency | Good, but through Anthropic's relay. |
| Our access | None as a feed. It is a viewer product, not an API: there is no local stream for the daemon or dashboard to subscribe to, and no way to aggregate many sessions into our wall. Aggregating would mean scraping a web UI or an unsupported protocol. |
| Codex, Gemini | No. Claude only. |
| Privacy | Content goes through Anthropic's relay, and the account must have the feature enabled (off by default on Team plans until an admin enables it). We cannot mask anything; it is all-or-nothing per session. |
| Effort | Zero to use as is; unbounded to build a feed from. |

Verdict on (c): it already does its job, a phone window onto one session, and it replaced MindAttic.Mobile. It is the
right answer to "let me answer a permission prompt from my phone", and the wrong source for the wall.

## 3. Comparison

| | (a) hooks + bus | (b) stream-json / SDK | (c) Remote Control |
|---|---|---|---|
| Sees the real interactive CLIs | Yes | **No** (headless only) | Yes (one at a time) |
| Current tool / target | Yes | Yes | Yes (UI only) |
| Ticket | Yes (claims) | Only if we inject it | No |
| Asking for permission | Yes (T790) | Via SDK callback | Yes (UI only) |
| Tokens | Daemon read of transcript | Native | No |
| Last message | Optional, from transcript on Stop | Native | UI only |
| Latency | Tens of ms | Real time | Real time, via relay |
| Codex and Gemini | Partly (thin hook sets) | Three adapters | No |
| Redaction in our hands | Yes | Filter everything | No |
| Effort | ~1 week | Weeks | Not buildable as a feed |

## 4. Privacy and redaction

The feed goes to the Team Wall (other people) and phones (a network hop through the Phase 3 gateway). Rules:

1. **Never publish raw `tool_input`.** The extractor emits a short `target` of at most 80 characters and a fixed
   per-tool shape: file tools send the base name (as `ToolCallText` already does), Grep and Glob send a pattern,
   WebFetch sends the host only, MCP tools send the tool name only, Agent sends its description.
2. **Bash is the leak.** `ToolCallText.Describe` today falls back to `Running <first line of the command>` when there
   is no `description`. Commands carry tokens (`curl -H "Authorization: ..."`, `--api-key`, env assignments, connection
   strings). The feed must send the `description` when present, otherwise only the program name (first word), never
   arguments. This also fixes the same leak in the dashboard's own board; it is its own ticket.
3. **Mask by shape as a second layer.** Run the T935 masker (token-shaped strings: `sk-...`, `ghp_...`, JWTs, long
   base64 or hex, `key=value` for key-like names) over every string field before publishing, so a pattern or description
   cannot smuggle one out.
4. **Paths.** Send the base name; the repo is already shown. Never send the user's profile path.
5. **Opt-out and scope.** A per-session "private" flag (and the T935 pause-broadcast) makes the feed send only
   `state`. Local consumers (the dashboard of the same user) may subscribe to the same kind; the gateway forwards it to
   remote watchers only under the T934 ACL.
6. **No prompts, no results.** Never send the user's prompt text or tool output. The last-message summary is off by
   default and, when enabled, is masked and cut to 120 characters.

## 5. The `activity` message, for docs/app-bus.md

A new kind, v1. It is the *current picture of one session*, not a log: the latest message for a `session` replaces the
last, so a missed message loses nothing (the same rule as the T891 state kinds). It fits the existing envelope.

```json
{"kind":"activity","v":1,"at":"2026-10-26T12:00:00Z","pid":1234,"payload":{
  "session":"6f1c...","provider":"claude","repo":"MindAttic.Orchestrator","login":"alice@example.com",
  "state":"working","tool":"Edit","target":"AgentGate.cs","verb":"Editing",
  "ticket":"M.ORCH-T937","subagents":1,"seq":412,"atUtc":"2026-10-26T12:00:00.120Z"}}
```

| Field | Meaning |
|---|---|
| `session` | The CLI's session id (the key; a newer message replaces the older). |
| `provider` | `claude`, `codex`, `gemini` or `kimi`. |
| `repo`, `login` | The repo, and the login's email. `login` is omitted when the session is private. |
| `state` | `working` (a tool call or turn in progress), `idle` (turn ended, waiting for you), `asking` (blocked on a permission prompt or question; the reason is never sent), `held` (the pace gate is holding it), `ended` (the CLI exited). |
| `tool` | The tool name (`Edit`, `Bash`, ...). MCP tools are collapsed to `mcp`. Absent when idle. |
| `target` | Short, masked, at most 80 characters: a file base name, a host, a pattern, or a Bash description. Never a raw command. |
| `verb` | An optional human phrase (`Editing`), as `ToolCallText` produces. |
| `ticket` | The ticket this session works, from its claim; absent when none. |
| `subagents` | How many subagents it has running. |
| `summary` | Optional. The masked last-message summary, at most 120 characters, sent on `idle` only and only when enabled. |
| `tokens` | Optional. `{"in":N,"out":N}` for the session so far, sent at most every 5 seconds. |
| `seq` | A per-session counter, so a consumer can drop a reordered message. |
| `atUtc` | When the hook saw it. |

Behaviour: the hook writes the latest picture to a small per-session file (the context gate already keeps per-session
state), then sends one best-effort line to the inbox. The daemon coalesces to at most 2 messages a second per session,
keeps the latest `activity` per session (like `app-present`), and replays them all on connect, so a late viewer is
complete at once. A session with no message for 10 minutes and a dead pid is sent as `ended` once and dropped. A
subscriber skips the kind if it doesn't read it, so older apps are unaffected.

## 6. Plan and follow-up tickets (filed unapproved, each a follow-up of M.ORCH-T937)

- M.ORCH-T961: Hook-side capture in the context gate and pace gate, with the redacting target extractor, written to a
  per-session activity file.
- M.ORCH-T962: The `activity` kind in `AppBus.cs` and `AppBusHub`, with coalescing and an activity snapshot on connect,
  and section 5 added to `docs/app-bus.md`.
- M.ORCH-T963: Masking for activity targets, shared with the T935 masker, and the `ToolCallText` Bash fix.
- M.ORCH-T964: Codex and Gemini coverage: wire the missing hook events and map their payloads.
- M.ORCH-T965: Dashboard consumer: a compact per-CLI activity strip for the Team Wall (T936) and a phone-width view.

## 7. Risks

- **Hook cost.** Each hook is a Python process. Keep the capture inside the existing process, one file write and one
  inbox line, never a new process or a blocking wait; failures must not hold a tool call (the pace gate is on the
  critical path of every call).
- **Provider drift.** Codex and Gemini hook payloads are not under our control; the extractor must tolerate missing
  fields and fall back to `state` only.
- **Fields we cannot get from hooks.** Tokens and last message stay transcript reads in the daemon; v1 treats them as
  optional so the feed ships without them.
- **Unverified claims.** This spike read our code and the existing spike doc. Provider CLI formats for stream-json,
  `codex exec --json` and Gemini's output mode, and Remote Control's current plan gating, come from the docs as known
  at the time and were not exercised here (no CLI was started). Check them before building anything on (b) or (c).
