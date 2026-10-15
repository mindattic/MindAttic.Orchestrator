# MindAttic.Orchestrator

One place to see and steer every agent session across the MindAttic workspace: which repos have an
agent working, which sessions are waiting on you, how much of your token budget is gone, what work
is queued, and what code and agent config has been copied or has drifted between repos.

It reads what already exists rather than adding new bookkeeping:

| Source | What it gives |
| --- | --- |
| Workspace folders (`MindAttic.Workflow`'s `Workspace`) | the repo list |
| `docs/USER_STORIES.md` per repo (`MindAttic.Workflow` parser) | open tickets |
| `\\.\pipe\mindattic-host-{provider}-{pid}` (MindAttic.Launcher) | live Launcher tabs, and a way to type into them |
| `~/.claude/projects/**/*.jsonl` (Claude Code transcripts) | per-session activity and per-response token usage, read incrementally |
| `~/.claude/claude-usage.json` (written by the shared statusline) | Claude's own 5-hour and weekly percent used and reset times |
| `~/.claude/session-context/*.json` (statusline + `context-gate.py`) | each session's context fill, whether its turn is running, and its CLI process |

## How it runs, in one screen

The goal: keep agents working all day and night, spending ~99% of every 5-hour window without ever
hitting the session limit, and never stalling on a full context. Pick what runs; it handles the rest.

| Piece | What it does | Where |
| --- | --- | --- |
| Daemon | runs at logon, refreshes every 15s: budget, pace, autopilot, context cycling | logon task `\MindAttic\Orchestrator\...`; log in Vault (below) |
| Claude's count | 5-hour percent and reset straight from Claude Code, sized safely | `~/.claude/claude-usage.json` |
| Pacing | caps new sessions so the window lands on 99% at its reset; the week is never paced | `rl-status.json` → `pace` |
| Holding agents | every Claude CLI and subagent waits before each tool call while the draw won't last, 0s once it fits | `~/.claude/pace-gate.py` (PreToolUse hook) |
| Subagent allowance | how many subagents may run at once across every CLI, worked out live from the pace (up to 16); each turn is told it, and a new subagent past it waits | `rl-status.json` → `subagent_allowance`; `settings --max-subagents 16` |
| Context cycling | past 80% context: wrap up, then `/quicksave`, `/clear`, `/quickload` typed in; a session running `/goal` is left to Claude's own compaction until `/goal clear` (a `/clear` would end the goal) | `~/.claude/context-gate.py` (prompt, tool, stop hooks) |
| Keep going | a CLI that ends its turn asking "shall I start the next one?" while its repo still has open tickets is told to carry on; one that stops again straight after without doing anything is let go (it really needs you) | `~/.claude/context-gate.py` (Stop hook); `settings --keep-going false` |
| Autopilot | each repo with an open CLI has its stories worked in parallel background agents, each in its own worktree, while the pace can afford them | `/autopilot` in any CLI; `-repo=A,B`; `/autopilot off` |

To run unattended: open a Launcher tab in each repo you want worked, then type `/autopilot` in any one
of them (`/autopilot -repo=UnityEve,MindAttic.Legion` works those repos, open or not). Each repo's next
open story (manual priority, then smallest effort first) goes to a headless background Claude (the Launcher's Claude command with `-p`; log in
`%APPDATA%\MindAttic\MindAttic.Orchestrator\autopilot`) in its own git worktree on branch `orch/<id>`. It
commits, closes its ticket there, rebases onto `origin/main` and pushes; then your checkout is fast-forwarded
(when git can do that without touching your changes) and the worktree removed. No window opens and nothing
is typed into your tabs or edited in your checkouts. How many run at once is worked out every refresh: as
many more as the spare rate pays for (what fits the 5-hour target, minus what's being drawn, over one
session's draw; agents started in the last 10 minutes count as drawing already), none while it's over, so
the count scales up and down on its own (`--repo-sessions Repo=N` caps one repo if you want); new
stories start only while the 5-hour pace can afford them, and the pace gate slows the running ones, so
the window is used to its target without hitting the limit. Repos whose CLIs open later join in; a repo
whose own CLI you're working in by hand (mid-turn, or a turn ended in the last 15 minutes) gets no new agents
until it's been left idle, and `settings --autopilot-skip GTFO.EZ,Prose` keeps repos out altogether; an
agent that ends without closing its ticket gives it back and alerts *Needs you*. `/autopilot off` ends it; `settings --autopilot false` pauses
it. `orchestrator dryrun` shows what it would start. Everything else is on by default. To check on it:
`orchestrator status` (sessions), `orchestrator budget` (percent, pace, hold), `orchestrator queue`.
The hooks are written into your global `~/.claude/settings.json` by the daemon and dashboard, and
rewritten if they go missing.

## Install

There's no setup step. `MindAttic.Orchestrator.exe` is one self-contained file (no .NET needed):
drop it anywhere and double-click it. The first run:

1. copies itself to `C:\Apps\MindAttic.Orchestrator` (per-user `%LOCALAPPDATA%\Programs` if `C:\Apps`
   isn't writable) and hands over to that copy;
2. if the workspace (default `D:\Projects\MindAttic`) is missing or empty, asks once: **build one**
   (pick an empty folder; it clones the workspace root repo, `mindattic/codex-standard`, then every repo
   listed in its `docs\workspace-repos.json`, four at a time, with a progress window) or **point at one
   that's already here**. Repos that fail (offline, no access) are listed; opening it again clones only
   what's missing;
3. registers the logon task `\MindAttic\Orchestrator\Orchestrator daemon` (the same exe with `--daemon`: headless,
   no window, no time limit) and starts it;
4. makes Start menu and desktop shortcuts, then opens the dashboard, which scans every repo and learns
   your limits from the last month of transcripts.

Every later start checks the same things and repairs only what's missing or points elsewhere, so a
moved exe, a deleted shortcut or a dead daemon fixes itself. Double-clicking a newer copy upgrades in
place (the running daemon and dashboard are stopped and restarted). `MindAttic.Orchestrator.exe --uninstall`
removes the task and shortcuts.

The daemon is what makes budgets and pacing always on: it keeps `~/.claude/rl-status.json` and the pace
(including the hold on running agents) current, installs and repairs the pace and context hooks,
cycles full contexts, notices stale assignments, and runs autopilot's background agents. Only one runs (a named mutex),
and only one process starts them (`dispatch.lock`): with the daemon running, an open
dashboard shows proposals and leaves autopilot to it.

From source:

```powershell
.\deploy.ps1          # build artifacts\MindAttic.Orchestrator.exe (+ the CLI in C:\Apps\MindAttic.Orchestrator\Cli), then install it as a double-click would
.\deploy.ps1 -Launch  # same, then open the dashboard
dotnet run --project src/MindAttic.Orchestrator.Wpf   # a dev build: never installs itself
```

Every process logs through [MindAttic.Log](../MindAttic.Log), the workspace's shared schema, to the
standalone database Vault assigns it, `%APPDATA%\MindAttic\Logging\MindAttic.Orchestrator\MindAttic.Log.db`
(`VaultPaths.LoggingDbPath`; open it with MindAttic.Log.Reader). The daemon records pace changes, holds, stale tickets, dispatches
and failed refreshes; the dashboard records dispatches, releases and crashes; the CLI records crashes.
Entries are tagged with a `Component` (`Daemon`, `Dashboard`, `Cli`) and a category.

What's been read from the transcripts is cached in `%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\transcripts.cache.json`
(each file's read offset plus its parsed usage and limit hits; a few MB, saved at most once a minute),
so a new process parses only what was written since: a CLI run takes under a second instead of ~4.
When the 7-day window moves on at midnight, what fell out of it is trimmed rather than reread. Deleting
the file is always safe; the next run rebuilds it.

The same core drives a CLI (`dotnet run --project src/MindAttic.Orchestrator.Cli -- <command>`):

| Command | |
| --- | --- |
| `status [--all] [--json]` | every repo's state, sessions, open tickets, tokens today, budget line |
| `start <Repo> [--provider] [--prompt]` | open a Launcher tab for a repo |
| `send <Repo> "text"` | type text + Enter into that repo's Launcher tab(s) |
| `budget [--block 20M] [--weekly 150M] [--weekly-reset "Mon 9:00"] [--warn 80] [--pace true] [--target 99] [--hits] [--week]` | current 5-hour block, week and pace, and Codex/Gemini usage; set limits (`0` clears back to learned, `none` drops the reset); `--hits` lists the limit hits the learned limits come from; `--week` charts the week day by day and projects it against the weekly limit |
| `queue [--groom]` / `bump <ID> [--by n]` | the cross-repo ticket queue, what's proposed next, and why (estimate, stale, too big, blocked, reserved); `--groom` lists old untouched tickets; reprioritize |
| `dispatch <ID> [--provider] [--worktree]` | start a session on a ticket |
| `release <ID> [--remove-worktree]` | put a ticket back in the queue; remove its (clean) worktree |
| `daemon [--interval 15]` | keep the budget file and pace fresh, cycle context, and run autopilot without the dashboard open |
| `settings [--max-sessions n] [--autopilot false] [--throttle-agents true] [--max-delay 300] [--provider X] [--stale-hours 12] [--quiet 22:00-07:00] [--repo-cap Repo=40] [--repo-sessions Repo=3] [--autopilot-skip A,B] [--keep-going false] [--groom-days 30] [--notify URL] [--github-intake true]` | scheduler settings |
| `audit [--latest]` | convention drift and cross-repo duplication |
| `block <ID> --by <ID>` / `reserve <ID> <tokens>` | make a ticket wait for others; set tokens aside for a ticket |
| `dryrun` | what `/autopilot` would start right now, without starting anything |
| `show <ID> [--diff]` | a ticket end to end: its row, the sessions dispatched on it, and the commits that mention it |
| `report [--weeks 4] [--csv FILE]` | uncached tokens by week and repo, by provider, and by ticket type |
| `hygiene` | closed tickets that may not be done: no Ref cited, or a later ticket says it came back |
| `intake [--dry-run]` | file GitHub issues labelled `orchestrator` and today's CI failures on main as tickets |
| `workspace export` | write `docs\workspace-repos.json` in the workspace root repo from the repos on disk (commit and push it so new machines get them) |
| `workspace build <folder> [--root URL] [--install-apps]` / `workspace install-apps` | clone the workspace root, then every repo it lists, into a folder; on a partial workspace, only what's missing |

Starting sessions needs the deployed Launcher at `C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe`.
Settings, assignments, learned limits (`limits.json`) and the last audit live in
`%APPDATA%\MindAttic\MindAttic.Orchestrator`.

## Phases

**0 · Status.** Each repo is one tile, grouped as *Needs you* (a session stopped and is waiting,
longest wait first), *Working* (a transcript grew in the last 2 minutes), *Open tickets, no session*
and *Quiet*. A toast and a taskbar flash fire when a repo goes Working → Needs you.

**1 · Session control.** Start a Claude/Codex/Gemini/Kimi tab in any repo (ticket text can pre-fill
the prompt, sent only by you) and reply to a live Launcher tab
from the detail pane.

**2 · Budget.** Usage is grouped into 5-hour blocks (the window Claude's subscription limits reset
on), starting at the first response after the previous block, rounded down to 10 minutes: that's
where Claude Code's own reported resets land. The budget metric is *uncached* tokens (input + cache
writes + output); cache reads are reported but excluded because they dominate raw totals.

The limits are **learned**. Whenever Claude Code refuses a turn it writes `You've hit your session
limit · resets 2pm (America/Chicago)` (or *weekly limit*) into the transcript. Each wall becomes one
observation: the uncached tokens in that window up to the hit, plus the exact reset time. The learned
limit is the median of the 5 most recent hits, so a raised limit takes over within a few hits. The
first run reads the last 30 days to start from the hits you already have. While you're behind a
wall, its reported reset replaces the estimated block end. A weekly hit's reset becomes the week's
reset when you haven't set one. Spending past a learned limit with no wall means the limit went up,
so it's retired until the next hit relearns it. A budget you set always overrides the learned one.
The local count misses claude.ai and other machines, which makes the learned limit err low (pacing
early) rather than high.

With a limit (set or learned) you get percent used, burn rate, the projected time you'd hit it, a
warning toast, and `~/.claude/rl-status.json` kept current for the `rl-check.py` prompt hook. With
neither, the block is compared to your busiest block in the last 7 days as context only: it never
warns, and no limit file is written. The week starts at the reset day and time you set (local time,
e.g. `Mon 9:00 AM`), else the learned one, else it's a rolling 7 days.

**Claude's own count wins.** Claude Code hands the statusline its real usage (`rate_limits`: 5-hour
and 7-day percent used and reset time); the shared statusline saves it to `~/.claude/claude-usage.json`
and the orchestrator sizes each limit from it (what the window spent by then, over the percent taken a
point high, since Claude reports whole percents, so the limit errs small and pacing never walks into
the wall). Set or learned limits apply only when there's no fresh report (none in the last hour, or
under 5%, too coarse to size by).

**Pacing** (on by default) aims to land on 99% of the 5-hour window right as it resets, instead of
hitting the wall and waiting hours. The *sustainable rate* is what's left of the target divided by
the hours to the reset. The weekly limit is shown, never paced: a week-long stretch would hold agents
for days, and you manage the week yourself; only a real weekly wall stops new work. A session's cost
is the last 30 minutes' burn divided by the sessions active in it (scaled up for any hold in force,
since a held session burns less than it will once let go), or with none running, your median
10-minute session burn over the past week. The session cap is how many such sessions fit the
sustainable rate:

| Pace | When | Effect |
| --- | --- | --- |
| open | every slot fits | normal |
| throttled | fewer than max fit | fewer new sessions; running ones aren't replaced as they finish |
| coast | none fit, or the 99% target is spent | no new sessions |
| over | at a limit (reported or counted) | no new sessions |

Near a reset with budget left, the sustainable rate climbs and the cap opens back up, so the leftover
gets used.

**Holding running agents.** Capping new sessions isn't enough when the ones already running (and
their subagents) draw too fast, so the orchestrator installs `~/.claude/pace-gate.py` as a global
Claude Code `PreToolUse` hook. Every Claude CLI on the machine, Launcher tab or not, subagents
included, runs it before each tool call; it waits `pace_delay_seconds` from `rl-status.json`. The
pacer sets that from the last 10 minutes' draw: if it would outrun what fits until the reset, each
call waits long enough to bring it down (`20s × (draw ÷ sustainable − 1)`, up to 5 minutes); the
moment it fits, it's 0, and a hold already waiting is let go within 5 seconds. The measured draw is
corrected for the hold already in force, so releasing doesn't just bring the burst back. A file
nobody has refreshed for 3 minutes is ignored, so a stopped daemon never slows anything.
`settings --throttle-agents false` turns holding off; `--max-delay` caps it. `pace_note` is printed into
each prompt too, so agents know to start subagents a few at a time.

The same `pace-gate.py` is wired into Codex's, Gemini's and Kimi's own pre-tool-call hook too (Codex's
`PreToolUse` in `~/.codex/hooks.json`, Gemini's `BeforeTool` in `~/.gemini/settings.json`, Kimi's
`PreToolUse` in `~/.kimi-code/config.toml`), so a Launcher tab on any of the four providers slows down
the same way (MAO-T50). Codex shows its own one-time "hooks need review" prompt in its TUI before a
freshly added hook runs there; nothing types through that for you.

**Context cycling.** The statusline also saves each session's context fill to
`~/.claude/session-context/<session>.json`, and a second hook, `context-gate.py` (on prompt, tool and
stop), records whether its turn is running and, once past 80% (`ContextCyclePercent`), tells the main
agent to finish its step, let its subagents finish and end the turn. When that turn has ended, the
daemon types `/quicksave`, waits for it, then `/clear` and `/quickload` into the session's Launcher tab,
so it carries on from the handoff with a fresh context. A CLI in a plain terminal is typed into through its
console (the hook records its process); a Claude desktop-app session has no console, so you get an alert instead.

**3 · Scheduler.** Every open ticket across all repos in one queue: manual priority, then fixes
before features before chores before docs, then oldest. Each refresh it proposes up to the free
session slots (one per repo) under the pace's session cap. It holds when the slots are full, the pace
is coasting or over, or (with pacing off or nothing to pace against) the budget passes the warn line.
Proposals wait for a click; the tab opens with the ticket pre-filled and unsent, and the prompt tells
the agent to finish with `/commit` and `workflow ticket close <ID> --repo <Repo>`. To work unattended,
`/autopilot` (above) works stories in background agents instead of opening tabs. A ticket for a repo that already has a session
goes into a git worktree under `D:\Projects\MindAttic.worktrees\<Repo>\<ID>` on branch `orch/<id>`, so
two agents never share a checkout. When the ticket closes, the queue offers to remove the worktree
(git refuses if it has uncommitted changes; the branch is always kept).

A dispatched ticket stays assigned while there's any sign of life where it was dispatched: a transcript
written under its directory, or a Launcher tab still open there. After `StaleAfterHours` (default 12;
`settings --stale-hours`, 0 never) of neither, the session evidently ended without closing the ticket,
so it goes back in the queue at its normal place, marked *stale*. The queue, `orchestrator queue` and
the daemon log show it. Starting it again reuses its worktree, where the partial work is, and replaces
the old assignment.

**Ticket costs.** A dispatched session is tied to its ticket by the prompt the scheduler writes ("Work on
ticket ID (type)") or by its worktree folder, so the orchestrator learns what tickets cost: every
session's uncached tokens, kept per session for 180 days in `ticket-costs.json`. Each queued ticket
gets an estimate: the median cost of the same type in the same repo (3+ done), else the same type, else
all tickets. Proposals show it (`~1.2M (median of 5 fix tickets in Prose)`). When pacing is on and the
estimate is more than what's left before the reset, the ticket isn't proposed but listed as *too big
for now*: split it, or let it wait for the reset rather than run out of room halfway.

**Steering the queue.** `orchestrator block X-T2 --by X-T1` (or "blocked by X-T1" in a ticket's summary)
keeps a ticket out of proposals until its blockers close. `orchestrator reserve X-T5 2M` sets tokens aside:
that ticket is proposed first and may start while pacing coasts (never past a limit), and everything
else paces against what's left. `settings --quiet 22:00-07:00` pauses autopilot in those hours.
`settings --repo-cap Prose=40` stops proposing work in a repo once it has spent 40% of the 5-hour limit
in the current block; `--repo-sessions Prose=3` lets a repo run 3 sessions at once (default 2: the repo
plus a worktree). A non-Claude default provider holds at 90% of its own reported 5-hour limit. Proposals
in a repo that shares duplicated code (per the last audit) with a repo that has a session say so, since
two agents fixing the same copied code is how conflicting edits happen. `queue --groom` lists open
tickets over 30 days old that nobody is on. `orchestrator dryrun` prints what autopilot would start
right now, without starting anything.

**Watching.** The daemon also flags a session that keeps repeating the identical tool call (5 of its last
12, still writing) as *looks stuck*: in its log, as a dashboard toast and in `orchestrator status`. It
audits the workspace every `AuditEveryHours` (24) and reports drift or duplication that's new since the
last audit. With `settings --notify https://ntfy.sh/<topic>` (or any webhook that takes a plain-text
POST), the moments worth leaving the desk for go to your phone: an agent finished and is waiting on you,
pacing coasts or hits a limit, a session looks stuck, a ticket went stale, the audit found something.
With `settings --github-intake true` it files GitHub issues labelled `orchestrator` and CI failures on
main as tickets every hour (once each, marked `GH#n` / `CI#id`; nothing is written back to GitHub).

**Dashboard extras.** A tray icon shows budget, pace and who needs you; its menu opens the dashboard,
pauses or resumes autopilot, starts the next proposal, jumps to a waiting repo, and shows an
always-on-top HUD strip. Minimizing hides the window to the tray. Opening it after an hour or more
away starts with a *While you were away* summary. A finished ticket's *Details* shows its sessions and
commits. Usage is kept by day for a year (`usage-history.json`), so `report` and the week projection
reach past the 7 days of transcripts.

**4 · Audit.** *Drift*: for each agent file most repos share (`.claude/commands`, hooks, skills,
`.prose/commands`, `AGENTS.md`, `CLAUDE.md`), the version most repos have is canonical (repo name and
path are ignored), and every other copy is listed as *drifted* or, under 30% alike, *custom*, with a
diff button. *Missing*: shared files absent where 60%+ of agent-enabled repos have them.
*Duplication*: a built-in copy/paste detector (6-line fingerprints over meaningful lines; generated
code, migrations, build output and vendored libraries skipped) finds identical and near-identical
files across repos, clusters them, and suggests a home (`mindattic-agent-standard`,
`MindAttic.Components`, `MindAttic.Helpers`, `MindAttic.Deploy`).

## Limits

- Budgets and pacing are Claude's (from Claude Code's own count, else learned from its limit hits). Codex
  (`~/.codex/sessions`) and Gemini CLI (`~/.gemini/tmp/*/chats`) usage counts toward repo activity and
  per-repo totals and is shown per provider, with Codex's own 5-hour/weekly percentages; Kimi writes no
  local session log, so its sessions show as Launcher tabs but aren't counted.
- A session started outside the Launcher is tracked, counted, held and context-cycled, but `send` and
  the dashboard's reply box only reach Launcher tabs.
- The context cycle point (80%) and holding are set in the dashboard's Settings panel (or
  `settings.json`: `ContextCyclePercent`, `ContextCycle`, `ThrottleAgents`, `MaxAgentDelaySeconds`);
  the CLI only has `--throttle-agents` and `--max-delay`.
- Claude Code prunes transcripts after about 30 days; the budget history window is 7 days, which
  always covers the current week.
- The budget file only updates while the daemon or the dashboard is running, so pacing only steers
  while one of them is; the logon task keeps the daemon up, and opening the dashboard restarts it if it died.
- Building a workspace clones the repos; it doesn't install the other apps (Launcher etc.; MAO-T44).
  The repo list is only as current as the last `workspace export` that was pushed.
- A hold slows tool calls, not a long reply with no tool calls in it. Codex needs a one-time "trust
  this hook" click in its own TUI the first time its hold fires on a machine; nothing automates that.
- Context cycling can't reach Claude desktop-app sessions (no console to type into); they get an alert.
- Learned limits are in uncached tokens, and the token-to-limit ratio shifts with the model mix, so
  they're estimates, used only when Claude's own count isn't available.
