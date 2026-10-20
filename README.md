# MindAttic.Orchestrator

A Windows daemon, dashboard and CLI that keeps Claude agents working the workspace's tickets around the clock and spends each 5-hour usage window to just under its limit, never over.

[![C#](https://img.shields.io/badge/C%23-.NET%2010-512BD4)](https://dotnet.microsoft.com/) [![Platform](https://img.shields.io/badge/platform-Windows%20WPF-0078D4)](https://learn.microsoft.com/dotnet/desktop/wpf/) [![License](https://img.shields.io/badge/license-all%20rights%20reserved-lightgrey)](#license) [![Status](https://img.shields.io/badge/status-in%20daily%20use-2ea44f)](docs/USER_STORIES.md)

```text
 Claude Code (every CLI, tab, subagent)                MindAttic.Orchestrator
 ---------------------------------------               -------------------------------------------
 statusline  --> ~/.claude/claude-usage.json  ---->    Daemon (WatchDog job, every 15s)
 transcripts --> ~/.claude/projects/**/*.jsonl ---->     measure: Claude's own 5-hour percent
                                                         pace:    target left / hours to reset
 hooks <------ ~/.claude/rl-status.json  <---------      steer:   allowance, hold, wind-down, stop
   pace-gate.py    (before every tool call)
   context-gate.py (prompt / turn end)                 Dashboard (WPF)         CLI (orchestrator)
                                                         status, budget, queue,  status, budget, queue,
 /autopilot in a CLI --> orchestrator next --claim       agents, settings        next, ledger, audit ...
   one subagent per ticket, each in its own git worktree, landing on main
```

## Why

- Spend almost all of every 5-hour window: unused budget is gone at the reset, so the daemon paces to land at the target (98% by default) right as the window resets.
- Never hit the wall: reaching Claude's limit stops every session mid-step, so at the target nothing more is spent, even if the daemon itself dies.
- Leave tickets running overnight: type `/autopilot` in one CLI and it works its repo's tickets through subagents, each in its own worktree, landing each on `main` and closing it.
- See every agent in one place: which repos are working, which sessions wait on you, what's queued, and how much budget is left.
- Catch drift early: shared agent files that differ between repos, and code copied across repos, are found and listed.

## Features

**Pacing and the stop**

- Reads Claude Code's own 5-hour and weekly percent every 15 seconds (sooner when a turn ends) and works out the sustainable rate to the reset.
- A subagent allowance (up to 16 at once, across every CLI) grows while the draw fits and shrinks when it runs ahead.
- A hold on every tool call in every Claude CLI (and Codex, Gemini and Kimi through their own hooks) slows a draw that runs ahead, and drops to 0 the moment it fits.
- Wind-down past 90%: turns are told to start only what can finish, and autopilot starts no new ticket until the window resets.
- A stop at the target that refuses tool calls and answers typed prompts unsent, with a fallback that still refuses at 99% if the daemon is down.
- A pace ledger that grades every finished 5-hour window `ok`, `under`, `blind` or `over`, and files a fix ticket the moment a limit is hit.

**Working tickets**

- `/autopilot` in a CLI works its own repo's tickets (or `-repo=A,B`, wildcards like `-repo=GTFO.*`, or `all`) through its own subagents.
- One queue of every open ticket across the workspace, with cost estimates learned from past tickets, blocking, token reservations and quiet hours.
- Only fixes are worked without asking; features, chores and docs wait for `orchestrator approve`.
- Repo windows: a CLI opened in a repo only edits that repo's files and closes that repo's tickets, unless you grant `/crossrepo`.
- `/handoff` winds every CLI down for an account switch and picks up after it; `/handoff off` cancels it, `orchestrator handoff status` lists what's still working.

**Dashboard**

- Repo tiles grouped as Needs you, Working, Open tickets and Quiet, with a toast and taskbar flash when a repo starts waiting on you.
- Stat tiles for the token budget, pace and tokens today, a full-width 5-hour utilization chart, and a live clock.
- An agent board listing every live CLI and background agent with its ticket, state, tokens and running time, subagents nested under it.
- Start a Claude, Codex, Gemini or Kimi tab in any repo, or reply to a live Launcher tab, from the detail pane.
- No tray icon: closing the window quits, and you pin it to Start or the taskbar to open it. Only one runs (M.ORCH-T497): opening it again brings the open one to the front, and a copy an older build left hidden in the tray is asked to exit (M.ORCH-T501).
- A Settings panel for limits, the pace target, holds, the provider and exempt commands (the subagent cap is `orchestrator settings --max-subagents`).

**Watching and safety**

- Flags a session repeating the same tool call as *looks stuck*, and a ticket whose session went quiet as *stale*.
- Samples the machine every 30 seconds and starts nothing while handles or memory run short.
- Reaps tool processes orphaned by an agent that exited, and searches running past 30 minutes.
- Deletes an ended session's `%TEMP%\claude\<project>\<session>` folder (its scratchpad and background-command logs), on start and every 10 minutes.
- Refuses whole-drive searches and `git push` to `master` before they run.
- Optional phone alerts through ntfy.sh or any plain-text webhook.

## Quick start

You need Windows and Claude Code. The released exe is self-contained, so no .NET install is needed to run it.

1. Double-click `MindAttic.Orchestrator.exe`.
2. If the workspace (default `D:\Projects\MindAttic`) is missing, pick **build one** or **point at one that's already here**.
3. The dashboard opens, scans every repo and learns your limits from the last month of transcripts.
4. In any Claude CLI inside a repo, type `/autopilot` to have it work that repo's tickets.

From source (needs the .NET 10 SDK):

```powershell
.\deploy.ps1          # build artifacts\MindAttic.Orchestrator.exe (+ the CLI in C:\Apps\MindAttic.Orchestrator\Cli), then install it as a double-click would
.\deploy.ps1 -Launch  # same, then open the dashboard
dotnet run --project src/MindAttic.Orchestrator.Wpf   # a dev build: never installs itself
```

What you should see: the dashboard's repo tiles fill in, the token budget tile shows the current 5-hour percent and pace, and `orchestrator budget` prints the same from a terminal.

## How it works

It reads what already exists rather than adding new bookkeeping:

| Source | What it gives |
| --- | --- |
| Workspace folders (`MindAttic.Workflow`'s `Workspace`) | the repo list |
| `MindAttic.Workflow\data\workflow.db` (the Workflow ticket database; each repo's `docs/USER_STORIES.md` is a view generated from it) | open tickets and their `requires` links |
| `\\.\pipe\mindattic-host-{provider}-{pid}` (MindAttic.Launcher) | live Launcher tabs, and a way to type into them |
| `~/.claude/projects/**/*.jsonl` (Claude Code transcripts) | per-session activity and per-response token usage, read incrementally |
| `~/.claude/claude-usage.json` (written by the shared statusline) | Claude's own 5-hour and weekly percent used and reset times |
| `~/.claude/session-context/*.json` (statusline + `context-gate.py`) | each session's context fill, whether its turn is running, and its CLI process |

The goal is to keep agents working all day and night, spending the target (98% by default, adjustable) of every 5-hour window without ever hitting the session limit. Pick what runs; it handles the rest. The full explanation is in [docs/ORCHESTRATION.md](docs/ORCHESTRATION.md).

| Piece | What it does | Where |
| --- | --- | --- |
| Daemon | runs at logon, refreshes every 15s: budget, pace, hooks, sweeps | WatchDog's `Orchestrator.Daemon` job (at logon, kept running); log in Vault (below) |
| Claude's count | 5-hour percent and reset straight from Claude Code, sized safely | `~/.claude/claude-usage.json` |
| Pacing | caps new sessions so the window lands on its target (98% by default) at its reset; the week is never paced | `rl-status.json` → `pace` |
| Holding agents | every Claude CLI and subagent waits before each tool call while the draw won't last, 0s once it fits. If the daemon stalls or dies (`rl-status.json` over 3 minutes old) calls are still held at the last pace it wrote until that window resets, and the daemon logs a write that keeps failing (a gate holding the file open) and any tick over a minute with where its time went | `~/.claude/pace-gate.py` (PreToolUse hook); `rl-status.json` → `pace_delay_seconds`, `window_reset` |
| Wind-down | past 90% of the 5-hour window each turn is told (once more mid-turn if it crosses the line) to start only what it can finish before the stop, and to commit and file the rest as a ticket if its task won't finish; autopilot starts no new ticket until the window resets | `rl-status.json` → `wind_down_note` |
| Never over | at the target (or a limit) the status says `stop`: every tool call, in every CLI, ones with `/pacing off` too, is held as long as a hook may wait and then refused, so usage can't pass the limit whatever the hold settings. A message you type then is answered at once by the hook (not sent, so nothing is spent; it says when typing works again), except `/autopilot`, `/pacing`, `/crossrepo`, `/handoff`, `/goal clear` (a new goal is refused) and the exempt commands. If the daemon dies or stalls (`rl-status.json` over 3 minutes old) the stop still holds: both gates fall back to Claude's own 5-hour and weekly percents (refreshed by the statusline every 60s) and refuse at 99%, and every prompt you type warns that the daemon is down. A stop once seen holds until its window resets (latched in `~/.claude/session-context/stop-latch.json`): a stalled daemon's stale file and an idle session's lower percent can't lift it, and a call already held for the pace or a subagent slot when the stop comes on is held on and refused, never let through. Below the stop, typing is never held | `rl-status.json` → `stop`, `stop_note`, `stop_until` |
| Pace ledger | every 5-hour window, once it resets, gets one permanent row graded `ok` (ended ≥95%, no hit), `under`, `blind` (Claude's meter went unseen ≥15 min while ≥2% of the window was spent) or `over` (a limit hit, or Claude reporting 100%), with its peak, end, minutes at 99%, the gate's holds and refusals and prompts answered at the stop. A new limit hit is raised the moment it's read (Error log, urgent alert, a fix ticket here with what the gate did in the 30 minutes before); a window closing over or blind does the same (one open blind ticket stands for all) | `%APPDATA%\MindAttic\MindAttic.Orchestrator\pace-ledger.jsonl`; gate events in `~/.claude/session-context/pace-gate.jsonl` (8 days); `orchestrator ledger` |
| Subagent allowance | how many subagents may run at once across every CLI, worked out live from the pace (up to 16); each turn is told it, and a new subagent past it waits, then is refused if no slot frees within 5 minutes (counted as the daemon's live count plus any started since, so a subagent whose stop never arrived doesn't hold the rest) | `rl-status.json` → `subagent_allowance`, `subagents_running`; `settings --max-subagents 16` |
| Pacing per CLI | `/pacing off` exempts one CLI (its process, subagents and `/clear` included) from every pace hold until it closes or `/pacing on` is typed; its usage still counts, so the others absorb it, and the stop at the window's target and the weekly stop still apply. `/pacing on` is every new CLI's default; a bare `/pacing` is answered by the hook (unsent) with this CLI's state | `~/.claude/session-context/unthrottled-<pid>` |
| Exempt commands | a turn typed as one of `ExemptCommands` (default `/pacing`, `/deploy`, `/run`; a saved list still naming the retired `/throttle` or `/unthrottle` reads as `/pacing`) skips every pace gate until it stops: no hold, no subagent wait, no stop refusal, and a typed one isn't answered unsent at the stop. A `Skill` call naming one goes through anywhere. Each such call is logged as `exempt` and counted, so its spend lands in the pace at once | `settings --exempt "/deploy,/run"` (`none`, `default`); the dashboard's Settings; the turn's mark in `~/.claude/session-context/<session>.state.json` → `exempt` |
| Handoff | `/handoff` (or `orchestrator handoff on`) winds every CLI down for an account switch: `orchestrator next` hands out nothing ("handing off"), keep-going sends no CLI back for work, the pace gate starts no new subagent, and the daemon returns claims no subagent started on within 7 minutes (their clean worktrees removed). Subagents already working finish, land and close; each CLI whose turn ends is told once to quicksave its repo's handoff (the quicksave goes through even at the window's stop) and say `<repo> quiet, handoff saved`, and an idle Launcher tab that hasn't saved has `/handoff` typed into it once. When the roster has nothing working for two ticks the daemon marks it quiet and sends one "All quiet, safe to switch accounts" alert, Psst text and dashboard toast. On the new account the first CLI to start (or a prompt in one signed in to another account) clears the hold, and each start gets back its repo's `/autopilot` and loads the handoff its CLI saved there, as `/quickload` does. `orchestrator handoff status` lists what's still working and who saved; `/handoff off` cancels it | `~/.claude/session-context/handoff.json`, `handoff-saved-<pid>`, `handoff-restored-<n>`; `~/.claude/commands/handoff.md` |
| Account roster | every Claude account signed in to here (email, org, plan) with its latest weekly and 5-hour used percent and resets. A `/login` adds or refreshes its row from Claude's meter; while signed in, each 1-point rise in the week refreshes it (the 5-hour percent and resets with it) and reads the meter again. An account not signed in keeps its last reading and when it was read; a passed reset reads 0% and the week's reset rolls on a week. Shown by `orchestrator accounts` (and a bare `orchestrator login`), the dashboard's Accounts card and its account menu in the upper right, which switches account (M.ORCH-T492). The daemon commits the database and its readable dump in this repo when the dump changed (every 10 minutes at most), as `workflow db sync` does for workflow.db | `data/orchestrator.db` (table `accounts`) and `data/orchestrator.sql` in the main checkout, or `MINDATTIC_ORCHESTRATOR_DB` |
| Repo windows | a CLI opened in a repo works only that repo: `pace-gate.py` refuses edits to another repo's files, `workflow ticket close`/`correct` on another repo's tickets, and `git -C <other repo>` changes. Filing a ticket in another repo (`workflow ticket new --repo X`) is always allowed, since that's how work is handed over. The workspace-root (General) CLI may work anywhere; `/crossrepo A,B` (or `all`) lets one CLI into other repos until it closes, `/crossrepo off` ends it | `~/.claude/pace-gate.py`; `~/.claude/session-context/crossrepo-<pid>` |
| No master pushes | every repo lands on `main` (the four master repos were renamed 2026-10-16), so `git push` to `master` is refused with a pointer to `HEAD:main` | `~/.claude/pace-gate.py` |
| Whole-drive guard | `find`/`rg`/`grep`/`fd` on a drive root or `~`, and a recursive `ls`/`dir`/`Get-ChildItem` there (`-R`, `/s`, or PowerShell's `-Recurse`/`-r` in any case), is refused: such a search never finishes and leaks handles until Windows freezes | `~/.claude/pace-gate.py` |
| Context | nothing ever types `/quicksave`, `/clear` or `/quickload` into a session, and no turn is told to stop for its context: a full context is left to Claude's own compaction (automatic cycling cleared a session in use, 2026-10-16, so it was removed) | — |
| Keep going | only the CLI `/autopilot` is on in, and only while its repos have workable tickets (fixes and approved ones that `orchestrator next` would hand it now: the hook asks it, as a dry run, before pushing a stop back): when its turn ends it's told to run `orchestrator next` again and carry on; one that stops again straight after without doing anything is let go. No other CLI is ever pushed onto tickets. Logged to `~/.claude/session-context/keep-going.jsonl` | `~/.claude/context-gate.py` (Stop hook) |
| Resume after the reset | a turn that ends at a window's stop (5-hour or weekly), or past the wind-down line saying it leaves work for the reset ("continue after 3:30 PM"), is recorded in its session state; once that window has reset and the pace no longer stops, the daemon types a one-line resume into the CLI's Launcher tab, or, for a CLI in a plain terminal (or with `--resume-after-reset false`), alerts you that it's waiting. Once per stop; a new prompt clears the record | `~/.claude/context-gate.py` (Stop hook), the daemon |
| Wake idle /autopilot CLIs | an /autopilot CLI let stop because `orchestrator next` handed out nothing (no pace room, its fair share in use) is idle until asked again; each tick the daemon asks next for it (a dry run), and once it would hand out a ticket, types a one-line prompt into the CLI's Launcher tab to claim and work them (or alerts you for one in a plain terminal). Not a CLI mid-turn, idle under 2 minutes, waiting on a window or running a /goal; at most once per 10 minutes per CLI | the daemon |
| Autopilot | the CLI you type it into works the tickets itself, through its own subagents: each takes one ticket in its own worktree, lands it on the repo's `main` and closes it, as many at once as the subagent allowance has room for | `/autopilot` (this CLI's repo), `-repo=A,B` (wildcards too: `-repo=GTFO.*`), `all`; `/autopilot off` (this CLI), `/autopilot off all` (every CLI); `orchestrator next --repos A,B --claim` hands out the tickets |

The hooks are written into your global `~/.claude/settings.json` by the daemon and dashboard, and rewritten if they go missing (a failed write, say while Claude Code has the file open, is retried on the next refresh). Both gates take the workspace from `MINDATTIC_WORKSPACE` (default `D:/Projects/MindAttic`) and treat `<workspace>.worktrees\<Repo>\...` as that repo's autopilot worktrees.

## Autopilot

Type `/autopilot` in a CLI. That CLI works its own repo's tickets (or `-repo=A,B`, a wildcard such as `-repo=GTFO.*` for every repo whose name starts `GTFO.`, or `all`) through its own subagents, and no other CLI is affected.

It runs `orchestrator next --repos ... --claim`, which hands it the next workable tickets (fixes, and tickets you approved, in queue order: manual priority, then smallest effort), as many as the subagent allowance has room for, each with its own git worktree from the repo's main line (branch `orch/<id>`) and a full prompt. It starts one subagent per ticket; each works only in its worktree, commits, rebases onto the repo's `main`, pushes and closes its ticket. When they return it asks for the next ones, and stops when `next` hands out nothing and says why (none left, waiting for your approval, or the pace).

While it's on, the repos named are that CLI's cross-repo exception, so its subagents may edit and close tickets there. The daemon no longer starts headless agents of its own; it still reaps ones started before, and its hourly sweep removes the worktrees of finished tickets once landed. A story whose agent was stopped at the window's stop is given back to start again after the reset (its work stays in its worktree). `/autopilot off` ends it in that CLI.

To check on it: `orchestrator status` (sessions), `orchestrator budget` (percent, pace, hold), `orchestrator queue`.

## Install

There's no setup step. `MindAttic.Orchestrator.exe` is one self-contained file: drop it anywhere and double-click it. The first run:

1. copies itself to `C:\Apps\MindAttic.Orchestrator` (per-user `%LOCALAPPDATA%\Programs` if `C:\Apps` isn't writable) and hands over to that copy;
2. if the workspace (default `D:\Projects\MindAttic`) is missing or empty, asks once: **build one** (pick an empty folder; it clones the workspace root repo, `mindattic/codex-standard`, then every repo listed in its `docs\workspace-repos.json`, four at a time, with a progress window) or **point at one that's already here**. Repos that fail (offline, no access) are listed with the `git clone` line that retries each;
3. starts the daemon (the same exe with `--daemon`: headless, no window) if it isn't running. Starting it at logon and keeping it running is [MindAttic.WatchDog](../MindAttic.WatchDog)'s `Orchestrator.Daemon` job; the exe registers no scheduled task of its own (M.ORCH-T475);
4. makes Start menu and desktop shortcuts, then opens the dashboard, which scans every repo and learns your limits from the last month of transcripts.

Every later start checks the same things and repairs only what's missing or points elsewhere, so a moved exe, a deleted shortcut or a dead daemon fixes itself. Double-clicking a newer copy upgrades in place (the running daemon and dashboard are stopped and restarted); an older one starts the installed copy instead of replacing it. A dev or test build (under `bin\` or `obj\`, inside a git checkout or worktree, a `*.worktrees` folder, or the temp folder) runs as itself and leaves the install, daemon and shortcuts alone; only `--install` installs it (`--install --force` to replace a newer installed exe). `MindAttic.Orchestrator.exe --uninstall` removes the shortcuts and hooks (and the old `\MindAttic\Orchestrator\Orchestrator daemon` task, if an older install left one); WatchDog's job is removed in WatchDog.

The daemon is what makes budgets and pacing always on: it keeps `~/.claude/rl-status.json` and the pace (including the hold on running agents) current, installs and repairs the pace and context hooks, notices stale assignments, reaps orphaned processes and sweeps landed worktrees. Only one runs (a named mutex), and only one process holds the dispatch lease (`dispatch.lock`). The same lease decides who writes `rl-status.json` and builds the status snapshot: the holder publishes each one to `status-snapshot.json`, and every other dashboard window, and one-off CLI commands like `status` and `queue`, show that instead of reading the transcripts themselves (they build their own only when nothing fresh was published in the last minute, and never overwrite the holder's `rl-status.json`). A minimized dashboard refreshes every 60s instead of every 5s (every 15s while it's the one holding the lease) and catches up the moment it's shown.

## Commands

The same core drives a CLI, deployed as `orchestrator.exe` in `C:\Apps\MindAttic.Orchestrator\Cli` (or `dotnet run --project src/MindAttic.Orchestrator.Cli -- <command>`):

| Command | |
| --- | --- |
| `status [--all] [--json]` | every repo's state, sessions, open tickets, tokens today, budget line, who's billed and each open CLI's own billing |
| `billing [team\|api]` | who every CLI bills: `Claude Team: <email> (<org>)` or `API KEY: sk-ant-...<last4>` (masked), and each open CLI's own billing; `team` or `api` switches every CLI (see [docs/ORCHESTRATION.md](docs/ORCHESTRATION.md#billing)) |
| `login [<email-or-short-name>] [--sso] [--api-key]` | bare: the account roster, the signed-in one marked and the most headroom suggested; with an account, runs `claude auth login --email <address>` (`--sso` for SSO orgs), which opens its login in the browser; `--api-key` bills every CLI to the Vault's API key (emergencies) |
| `start <Repo> [--provider] [--prompt]` | open a Launcher tab for a repo |
| `send <Repo> "text" [--no-submit]` | type text + Enter (or just the text) into that repo's Launcher tab(s) |
| `budget [--block 20M] [--weekly 150M] [--weekly-reset "Mon 9:00"] [--warn 80] [--pace true] [--target 98] [--hits] [--week]` | current 5-hour block, week and pace, and Codex/Gemini usage; set limits (`0` clears back to learned, `none` drops the reset); `--hits` lists the limit hits the learned limits come from; `--week` charts the week day by day and projects it against the weekly limit |
| `queue [--top 25] [--groom]` / `bump <ID> [--by n]` | the cross-repo ticket queue, what's proposed next, and why (estimate, stale, too big, blocked, reserved); `--groom` lists old untouched tickets; reprioritize |
| `dispatch <ID> [--provider] [--worktree]` | start a session on a ticket |
| `release <ID> [--remove-worktree]` | put a ticket back in the queue; remove its (clean) worktree |
| `daemon [--interval 15]` | keep the budget file, pace and hooks fresh without the dashboard open |
| `settings [--max-sessions n] [--autopilot false] [--throttle-agents true] [--max-delay 300] [--max-subagents 16] [--pace-profile front-loaded] [--pace-curve-k 0.55] [--provider Claude] [--stale-hours 12] [--quiet 22:00-07:00] [--repo-cap Repo=40] [--repo-sessions Repo=3] [--autopilot-skip A,B] [--hand-minutes 15] [--keep-going false] [--resume-after-reset false] [--exempt "/deploy,/run"] [--groom-days 30] [--notify URL] [--github-intake true]` | scheduler settings (`--provider` takes Claude, Codex, Gemini or Kimi) |
| `audit [--latest] [--top n]` | convention drift and cross-repo duplication |
| `approve [<ID> ...] [--undo]` | only fixes are worked without asking; every other open ticket (feature, chore, docs) waits outside the queue, and for autopilot and keep-going, until you approve it. Approval (and `bump`'s priority) lives in the Workflow database's queue, so `workflow ticket approve` and the Workflow Desktop approve the same way; settings.json's old `Approved`/`Priorities` move there once. A ticket the database doesn't hold (a repo still on its own `docs/USER_STORIES.md`) keeps them in settings.json's `StoryApproved`/`StoryPriorities` instead, which the queue reads too; they move into the database once it holds the ticket. With no ids it lists what's waiting |
| `block <ID> --by <ID> [--clear]` / `reserve <ID> <tokens>` | make a ticket wait for others (or clear what it waits for); set tokens aside for a ticket. Ticket ids may be typed in any case |
| `dryrun` / `next [--repos "A,B"] [--count n] [--claim]` | which CLIs have `/autopilot` on and what each would hand its subagents next; `next --claim` claims them (worktree, assignment, prompt) |
| `show <ID> [--diff]` | a ticket end to end: its row, the sessions dispatched on it, and the commits that mention it |
| `ledger [--days 7] [--csv FILE]` | the pace ledger: every 5-hour window graded ok/under/blind/over, the running one, and the record ("26 windows since Sep 16: 23 over, ...; last over: 9h ago"); windows before the usage samples began are known only from their limit hits. Exits 1 if a window went over in `--days` |
| `report [--weeks 4] [--csv FILE]` | uncached tokens by week and repo, by provider, and by ticket type |
| `remind "<text>" --at <time> [--to <contact>]` / `remind --list` / `remind --cancel <id>` | save a reminder the daemon texts through Psst when it's due (see [Watching](#watching)); `--at` takes `9am`, `tomorrow`, `tomorrow 8am`, `fri 5pm`, `tonight`, `in 2h`, `30m` or `2026-10-20 09:00`, local time; `--to` a Psst contact or a US number, by default you (Psst's own number). A CLI agent asked to "remind me tomorrow" runs it too |
| `hygiene` | closed tickets that may not be done: no Ref cited, or a later ticket says it came back |
| `intake [--dry-run]` | file GitHub issues labelled `orchestrator` and today's CI failures on main as tickets |
| `workspace export` | write `docs\workspace-repos.json` in the workspace root repo from the repos on disk (commit and push it so new machines get them) |
| `workspace build <folder> [--root URL] [--install-apps]` / `workspace install-apps` | clone the workspace root, then every repo it lists, into a folder; on a partial workspace, only what's missing; `install-apps` runs every other app's `deploy.ps1` |

Starting sessions needs the deployed Launcher at `C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe`.

## Configuration

All of the orchestrator's state lives in the repo's `data/orchestrator.db` (SQLite, committed with a readable `data/orchestrator.sql` dump; `MINDATTIC_ORCHESTRATOR_DB` overrides, else every process and hook, a worktree's too, uses the main checkout's copy under the default workspace, or a machine-local `orchestrator.db` in `%APPDATA%\MindAttic\MindAttic.Orchestrator` when there's no checkout there). Settings are the `settings` table, one row per setting; assignments the `assignments` table, one row each; learned limits, reminders, the last audit, the pace alarms, limit wakes and autopilot's holds are rows of `state` (what changes every tick, the holds, the stale claims and when the dashboard was last seen, is left out of the dump so it doesn't make a commit every sync). Each change is one transaction, so the daemon, the dashboard, the CLI and the hooks (Python's `sqlite3`) read and write at once with no `.lock` files; the JSON files these replaced (`settings.json`, `assignments.json`, `limits.json`, `reminders.json`, `audit.json` and the rest) are read in the first time each is used and kept beside it as `.bak`. No secrets go in it: keys and tokens stay in MindAttic.Vault (M.ORCH-T524). The history lives there too: Claude's meter readings (`usage_samples`, kept a week), the pace ledger (`pace_ledger`), usage by day (`usage_history`) and ticket costs (`ticket_costs`); the JSON files these replaced (`usage-samples.json`, `pace-ledger.jsonl`, `usage-history.json`, `ticket-costs.json`) are read in on the first run and kept beside it as `.bak`. Change them with `orchestrator settings` and `orchestrator budget`, or in the dashboard's Settings panel, which also holds the pace target, holding (`ThrottleAgents`, `MaxAgentDelaySeconds`) and the exempt commands.

Every process logs through MindAttic.Log, the workspace's shared schema, to the standalone database Vault assigns it, `%APPDATA%\MindAttic\Logging\MindAttic.Orchestrator\MindAttic.Log.db` (`VaultPaths.LoggingDbPath`; open it with MindAttic.Log.Reader). The daemon records pace changes, holds, stale tickets, dispatches and failed refreshes; the dashboard records dispatches, releases and crashes; the CLI records crashes. Entries are tagged with a `Component` (`Daemon`, `Dashboard`, `Cli`) and a category.

What's been read from the transcripts is cached in `%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\transcripts.cache.json` (each file's read offset plus its parsed usage and limit hits; a few MB, saved at most once a minute), so a new process parses only what was written since: a CLI run takes under a second instead of about 4. Within a process, a transcript that's been quiet for 10 minutes and whose size and timestamp haven't moved isn't reopened on the next refresh (every one is rechecked every 5 minutes). When the 7-day window moves on at midnight, what fell out of it is trimmed rather than reread. Deleting the file is always safe; the next run rebuilds it.

## Budget and pacing

**Budget.** Usage is grouped into 5-hour blocks (the window Claude's subscription limits reset on), starting at the first response after the previous block, rounded down to 10 minutes: that's where Claude Code's own reported resets land. The budget metric is *uncached* tokens (input + cache writes + output); cache reads are reported but excluded because they dominate raw totals.

**Claude's own count wins.** Claude Code hands the statusline its real usage (`rate_limits`: 5-hour and 7-day percent used and reset time); the shared statusline saves it to `~/.claude/claude-usage.json` and the orchestrator sizes each limit from it (what the window spent by then, over the percent taken a point high, since Claude reports whole percents, so the limit errs small and pacing never walks into the wall). Between reports the percent is the last report's plus what the transcripts recorded since, so everything is counted: what you type, every reply, subagents, background agents. A report stays in use for its whole 5-hour window; the hooks also give the statusline a 60s `refreshInterval`, so any open tab, idle or not, keeps the real percent fresh. A statusline only carries its own session's last reply, though, so a CLI waiting on subagents reports nothing new while they spend; when the report is 4 minutes old and the transcripts show tokens spent since, the daemon asks Claude's usage endpoint itself (what `/usage` shows, with Claude Code's saved sign-in, read-only) and writes the reading to the same file. A budget you set overrides it; a learned limit applies only when there's no report for the current window (or it's under 5%, too coarse to size by). The daemon recounts on its 15s tick, and sooner (at most every 10s) whenever a turn or subagent ends anywhere or a new report lands (`~/.claude/session-context/refresh.now`).

**Learned limits.** Whenever Claude Code refuses a turn it writes `You've hit your session limit · resets 2pm (America/Chicago)` (or *weekly limit*) into the transcript. Each wall becomes one observation: the uncached tokens in that window up to the hit, plus the exact reset time. The learned limit is the median of the 5 most recent hits, so a raised limit takes over within a few hits. The first run reads the last 30 days to start from the hits you already have. While you're behind a wall, its reported reset replaces the estimated block end. A weekly hit's reset becomes the week's reset when you haven't set one. Spending past a learned limit with no wall means the limit went up, so it's retired until the next hit relearns it. A budget you set always overrides the learned one.

With a limit (reported, set or learned) you get percent used, burn rate, the projected time you'd hit it, a warning toast, and `~/.claude/rl-status.json` kept current for the hooks. With none, the block is compared to your busiest block in the last 7 days as context only: it never warns, and no limit file is written. The week starts when Claude's own report says it resets, else at the reset day and time you set (local time, e.g. `Mon 9:00 AM`), else the learned one, else it's a rolling 7 days.

**Pacing** (on by default) aims to land on the target (98% by default, `budget --target` or the dashboard's Settings) of the 5-hour window right as it resets, instead of hitting the wall and waiting hours. The *sustainable rate* is what's left of the target divided by the hours to the reset. The weekly limit is shown, never paced: a week-long stretch would hold agents for days; only a real weekly wall stops new work. A session's cost is the last 30 minutes' burn divided by the sessions active in it (scaled up for any hold in force), or with none running, your median 10-minute session burn over the past week. The session cap is how many such sessions fit the sustainable rate:

| Pace | When | Effect |
| --- | --- | --- |
| open | every slot fits | normal |
| throttled | fewer than max fit | fewer new sessions; running ones aren't replaced as they finish |
| coast | none fit, or the target is spent | no new sessions |
| over | at a limit (reported or counted) | no new sessions |

Near a reset with budget left, the sustainable rate climbs and the cap opens back up, so the leftover gets used.

**Holding running agents.** Capping new sessions isn't enough when the ones already running (and their subagents) draw too fast, so the orchestrator installs `~/.claude/pace-gate.py` as a global Claude Code `PreToolUse` hook. Every Claude CLI on the machine, Launcher tab or not, subagents included, runs it before each tool call; it waits `pace_delay_seconds` from `rl-status.json`. The pacer sets that from the last 10 minutes' draw: if it would outrun what fits until the reset, each call waits `20s × (draw ÷ sustainable − 1)`, up to 5 minutes; the moment it fits, it's 0, and a hold already waiting is let go within 5 seconds. The measured draw is corrected for the hold already in force, so releasing doesn't just bring the burst back. `settings --throttle-agents false` turns holding off; `--max-delay` caps it. `pace_note` is printed into each prompt too, so agents know to start subagents a few at a time.

The same `pace-gate.py` is wired into Codex's, Gemini's and Kimi's own pre-tool-call hook too (Codex's `PreToolUse` in `~/.codex/hooks.json`, Gemini's `BeforeTool` in `~/.gemini/settings.json`, Kimi's `PreToolUse` in `~/.kimi-code/config.toml`), so a Launcher tab on any of the four providers slows down the same way. Codex shows its own one-time "hooks need review" prompt in its TUI before a freshly added hook runs there; nothing types through that for you.

**Context.** The statusline saves each session's context fill to `~/.claude/session-context/<session>.json`, and `context-gate.py` records whether its turn is running; that's all. Nothing types into a session and no turn is told to stop for its context: a full one is left to Claude's own compaction.

## Scheduler and queue

Every open ticket across all repos is in one queue: assigned tickets last, reserved ones first, then manual priority, then smallest effort (a known estimate before an unknown one, cheapest first), then fixes before features before chores before docs, then oldest. Tickets that aren't fixes and that you haven't approved wait outside it. Each refresh it proposes up to the free session slots (one per repo) under the pace's session cap. It holds when the slots are full, the pace is coasting or over, or (with pacing off or nothing to pace against) the budget passes the warn line. Proposals wait for a click; the tab opens with the ticket pre-filled and unsent, and the prompt tells the agent to finish with `/commit` and `workflow ticket close <ID> --repo <Repo>`. To work unattended, use `/autopilot` (above). A ticket for a repo that already has a session goes into a git worktree under `D:\Projects\MindAttic.worktrees\<Repo>\<ID>` on branch `orch/<id>`, so two agents never share a checkout. When the ticket closes, the queue offers to remove the worktree (git refuses if it has uncommitted changes; the branch is always kept).

A dispatched ticket stays assigned while there's any sign of life where it was dispatched: a transcript written under its directory, or a Launcher tab still open there. After `StaleAfterHours` (default 12; `settings --stale-hours`, 0 never) of neither, the session evidently ended without closing the ticket, so it goes back in the queue at its normal place, marked *stale*. Starting it again reuses its worktree, where the partial work is, and replaces the old assignment.

**Ticket costs.** A dispatched session is tied to its ticket by the prompt the scheduler writes ("Work on ticket ID (type)") or by its worktree folder, so the orchestrator learns what tickets cost: every session's uncached tokens, kept per session for 180 days in orchestrator.db's `ticket_costs`. Each queued ticket gets an estimate: the median cost of the same type in the same repo (3+ done), else the same type, else all tickets. Proposals show it (`~1.2M (median of 5 fix tickets in Prose)`). When pacing is on and the estimate is more than what's left before the reset, the ticket isn't proposed but listed as *too big for now*.

**Steering the queue.** `orchestrator block X-T2 --by X-T1` (or "blocked by X-T1" in a ticket's summary) keeps a ticket out of proposals until its blockers close. `orchestrator reserve X-T5 2M` sets tokens aside: that ticket is proposed first and may start while pacing coasts (never past a limit), and everything else paces against what's left. `settings --quiet 22:00-07:00` pauses autopilot in those hours. `settings --repo-cap Prose=40` stops proposing work in a repo once it has spent 40% of the 5-hour limit in the current block; `--repo-sessions Prose=3` lets a repo run 3 sessions at once (default 2: the repo plus a worktree). A non-Claude default provider holds at 90% of its own reported 5-hour limit. Proposals in a repo that shares duplicated code (per the last audit) with a repo that has a session say so. `queue --groom` lists open tickets over 30 days old that nobody is on. `orchestrator dryrun` prints what autopilot would start right now, without starting anything.

## Watching

The daemon flags a session that keeps repeating the identical tool call (5 of its last 12, still writing) as *looks stuck*: in its log, as a dashboard toast and in `orchestrator status`. It audits the workspace every `AuditEveryHours` (24) and reports drift or duplication that's new since the last audit.

An hour before each 5-hour reset, and again at 30 minutes, when the window is on course to end below 90% and no CLI has a ticket it could work (an open fix or approved ticket, unclaimed and unblocked, in a repo with a CLI open or that /autopilot works), it raises a *Needs you* alert: how much of the window would go unused, how many tickets wait for your approval, and where /autopilot is on. It only says so; it never starts or approves anything.

With `settings --notify https://ntfy.sh/<topic>` (or any webhook that takes a plain-text POST), the moments worth leaving the desk for go to your phone: an agent finished and is waiting on you, pacing coasts or hits a limit, a window is heading to end under target with nothing to work, a session looks stuck, a ticket went stale, the audit found something. With `settings --github-intake true` it files GitHub issues labelled `orchestrator` and CI failures on main as tickets every hour (once each, marked `GH#n` / `CI#id`; nothing is written back to GitHub).

Every 30 seconds it samples the machine (kernel handles, commit charge, free RAM): past a warn line (88% commit, under 2 GB free, 100k handles in one process) you're alerted, and past a danger line (95%, 0.75 GB, 250k) the alert is urgent. It names the worst offenders by what's short: memory users (and all Claude processes together) when it's memory, handle holders when it's handles. Going up is immediate; coming down is one level at a time and only once clear of the line (5 points of commit, 1 GB, a fifth of the handles), so it doesn't flap as agents come and go, and you're alerted once per escalation. Nothing is killed or held for it: `/autopilot` CLIs go on taking tickets.

Every tick it reads free space on the system drive and the worktree root's drive: under `DiskWarnGb` (default 20 GB; `settings --disk-warn-gb`, 0 turns it off) you're alerted, urgently under `DiskCriticalGb` (default 5 GB), with the five biggest folders in `%TEMP%` and the five biggest files under `%TEMP%\claude\<project>\<session>\` (background commands' output, scratchpad builds), so a runaway writer is named while it's still growing. It alerts once per step down and again each time free space halves since the last alert; it's back to normal once a tenth of the warn line (at least 1 GB) clear of it.

Beside the watchdog, every tick it texts each reminder that's due (`orchestrator remind`) through Psst (`psst sms <to> "Reminder: <text>"`) and marks it sent. The reminders live in one list it owns, the `reminders` row of `data/orchestrator.db`'s `state` table (was `reminders.json`), not a Task Scheduler task each as `psst sms --schedule` makes, so they're listed (`remind --list`, the Queue view), cancelled, and survive reboots: one whose time passed while the machine slept or the daemon was off is sent at the next tick and logged as late. A send Psst fails is counted, alerted once and tried again next tick; when Psst isn't installed or configured, or a reminder for you finds no number in Psst's settings, the reminders wait and you're alerted once. Sent ones are dropped from the file after 30 days.

Every 60 seconds it reaps tool processes (`find`, `rg`, `grep`, `git`, `bash`, `python`, `node`) left orphaned when their agent exited, ones still under an agent that pass 50,000 handles, and searches (`find`, `rg`, `grep`) running past 30 minutes; a long test run or build under an agent is left be. An orphan is only reaped if an earlier scan saw it (or the shell it ran in) under a live Claude CLI, so a dev server or script you started yourself is never touched. Git and `gh` calls made inside its loop give up after 2 minutes and 1 minute and never wait on a credential prompt, so a network stall can't stop pacing.

## Dashboard

Each repo is one tile, grouped as *Needs you* (a session stopped and is waiting, longest wait first), *Working* (a transcript grew in the last 2 minutes), *Open tickets, no session* and *Quiet* (shown with *Show quiet repos* or a search). A toast and a taskbar flash fire when a repo goes Working → Needs you. A full-screen loader covers the window until the first refresh finishes. `F5` refreshes, `Ctrl+F` searches repos and tickets, `Ctrl +/−` zooms.

The top row holds the stat tiles (token budget, tokens today and the rest, aligned to one size at every window width), then a full-width 5-hour utilization chart. The header shows a live clock next to Updated, Refresh and Settings. The agent board lists every live CLI and background agent (its ticket, state, tokens and running time) with its subagents nested under it. Opening it after an hour or more away starts with a *While you were away* summary. A finished ticket's *Details* shows its sessions and commits. Every ticket id the window lists (a repo's open tickets, the queue and *Next up*, a finished ticket, an agent's *Open in Workflow*) is a link that opens the ticket in Workflow Desktop (`OrchestratorOptions.WorkflowDesktopExe`, started with `--root <workspace> --ticket <ID>`) to view, approve, prioritize or close it there.

From a repo's detail pane you can start a Claude, Codex, Gemini or Kimi tab (ticket text can pre-fill the prompt, sent only by you) and reply to a live Launcher tab.

There's no tray icon: closing the window quits the dashboard (the daemon keeps running), and it's opened from its Start menu shortcut or a taskbar pin. Opening it while it's already up brings that window to the front. Usage is kept by day for a year (orchestrator.db's `usage_history`), so `report` and the week projection reach past the 7 days of transcripts.

## Audit

*Drift*: for each agent file most repos share (`.claude/commands`, hooks, skills, `.prose/commands`, `AGENTS.md`, `CLAUDE.md`), the version most repos have is canonical (repo name and path are ignored), and every other copy is listed as *drifted* or, under 30% alike, *custom*, with a diff button. *Missing*: shared files absent where 60%+ of agent-enabled repos have them.

*Duplication*: a built-in copy/paste detector (6-line fingerprints over meaningful lines; generated code, migrations, build output and vendored libraries skipped) finds identical and near-identical files across repos, clusters them, and suggests a home (`mindattic-agent-standard`, `MindAttic.Components`, `MindAttic.Helpers`, `MindAttic.Deploy`).

## Project layout

| Path | What's there |
| --- | --- |
| `src/MindAttic.Orchestrator.Core` | budget and pacing, the pace ledger, scheduling and autopilot, session discovery and control, activity readers, audit, installer and the hook scripts |
| `src/MindAttic.Orchestrator.Wpf` | the dashboard and daemon entry point (`MindAttic.Orchestrator.exe`) |
| `src/MindAttic.Orchestrator.Cli` | the `orchestrator` CLI (Spectre.Console) |
| `tests/MindAttic.Orchestrator.Tests` | NUnit tests for all of the above, including the Python hooks run for real |
| `deploy.ps1` | build the single-file exe and the CLI, then install |

It targets `net10.0-windows` and uses the MindAttic.Log, MindAttic.Vault, MindAttic.Workflow and MindAttic.Components.Wpf packages.

## Testing

```powershell
dotnet test MindAttic.Orchestrator.slnx
```

## Limitations

- Budgets and pacing are Claude's (from Claude Code's own count, else learned from its limit hits). Codex (`~/.codex/sessions`) and Gemini CLI (`~/.gemini/tmp/*/chats`) usage counts toward repo activity and per-repo totals and is shown per provider, with Codex's own 5-hour and weekly percentages; Kimi writes no local session log, so its sessions show as Launcher tabs but aren't counted.
- A session started outside the Launcher is tracked, counted and held, but `send` and the dashboard's reply box only reach Launcher tabs.
- Holding is set in the dashboard's Settings panel (or the `settings` table: `ThrottleAgents`, `MaxAgentDelaySeconds`); the CLI only has `--throttle-agents` and `--max-delay`.
- Claude Code prunes transcripts after about 30 days; the budget history window is 7 days, which always covers the current week.
- The budget file only updates while the daemon or the dashboard is running, so pacing only steers while one of them is; WatchDog's job keeps the daemon up, and opening the dashboard restarts it if it died.
- Building a workspace clones the repos; it doesn't install the other apps unless you pass `--install-apps` or run `workspace install-apps`. The repo list is only as current as the last `workspace export` that was pushed.
- No hold lasts past 5 minutes per tool call: past it the call goes ahead, held but never refused, except at the window's stop and for a subagent still waiting on a full allowance, where it's refused (the subagent is tried again once a slot frees, never started over the allowance). `/unthrottle` exempts a CLI from holds, never from that stop.
- A hold slows tool calls, not a long reply with no tool calls in it. Codex needs a one-time "trust this hook" click in its own TUI the first time its hold fires on a machine; nothing automates that.
- Learned limits are in uncached tokens, and the token-to-limit ratio shifts with the model mix, so they're estimates, used only when Claude's own count isn't available.

## Documentation

- [docs/ORCHESTRATION.md](docs/ORCHESTRATION.md): how the measure, pace and steer loop works, and what happens when something fails.
- [docs/USER_STORIES.md](docs/USER_STORIES.md): every ticket, open and closed (a view generated from the Workflow database).
- [AGENTS.md](AGENTS.md): instructions for agents working in this repo.

## License

No license file: all rights reserved. Copyright (c) 2026 MindAttic.

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [MindAttic.Workflow](https://github.com/mindattic/MindAttic.Workflow), [MindAttic.Launcher](https://github.com/mindattic/MindAttic.Launcher), [MindAttic.Log](https://github.com/mindattic/MindAttic.Log), [MindAttic.Vault](https://github.com/mindattic/MindAttic.Vault).
