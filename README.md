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

## Run

```powershell
dotnet run --project src/MindAttic.Orchestrator.Wpf   # the dashboard: Repos, Queue and Audit views + Settings
```

The same core drives a CLI (`dotnet run --project src/MindAttic.Orchestrator.Cli -- <command>`):

| Command | |
| --- | --- |
| `status [--all] [--json]` | every repo's state, sessions, open tickets, tokens today, budget line |
| `start <Repo> [--provider] [--prompt]` | open a Launcher tab for a repo |
| `send <Repo> "text"` | type text + Enter into that repo's Launcher tab(s) |
| `budget [--block 20M] [--weekly 150M] [--weekly-reset "Mon 9:00"] [--warn 80]` | current 5-hour block and week; set limits (`0` clears, `none` makes the week rolling) |
| `queue` / `bump <ID> [--by n]` | the cross-repo ticket queue and what's proposed next; reprioritize |
| `dispatch <ID> [--provider] [--worktree]` | start a session on a ticket |
| `release <ID> [--remove-worktree]` | put a ticket back in the queue; remove its (clean) worktree |
| `daemon [--interval 15]` | keep the budget file fresh and, if enabled, auto-dispatch without the dashboard open |
| `settings [--max-sessions n] [--auto-dispatch true] [--provider X]` | scheduler settings |
| `audit [--latest]` | convention drift and cross-repo duplication |

Starting sessions needs the deployed Launcher at `C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe`.
Settings, assignments and the last audit live in `%APPDATA%\MindAttic\MindAttic.Orchestrator`.

## Phases

**0 · Status.** Each repo is one tile, grouped as *Needs you* (a session stopped and is waiting,
longest wait first), *Working* (a transcript grew in the last 2 minutes), *Open tickets, no session*
and *Quiet*. A toast and a taskbar flash fire when a repo goes Working → Needs you.

**1 · Session control.** Start a Claude/Codex/Gemini/Kimi tab in any repo (ticket text can pre-fill
the prompt, never auto-sent) and reply to a live Launcher tab from the detail pane.

**2 · Budget.** Usage is grouped into 5-hour blocks (the window Claude's subscription limits reset
on; the same convention as community tools like ccusage, since the real limit isn't exposed). The
budget metric is *uncached* tokens (input + cache writes + output); cache reads are reported but
excluded because they dominate raw totals. With a 5-hour budget set you get percent used, burn rate,
the projected time you'd hit it, a warning toast, and `~/.claude/rl-status.json` kept current for the
`rl-check.py` prompt hook. With no budget set, the block is compared to your busiest block in the
last 7 days as context only: it never warns, and no limit file is written. The week starts at the reset day and
time you set (local time, e.g. `Mon 9:00 AM`, matching when Claude's weekly limit restarts); with no
reset set it's a rolling 7 days.

**3 · Scheduler.** Every open ticket across all repos in one queue: manual priority, then fixes
before features before chores before docs, then oldest. Each refresh it proposes up to the free
session slots (one per repo), and holds when the slots are full or the budget passes the warn line.
Proposals wait for a click by default; *auto-dispatch* (Settings) opens one per minute by itself.
Either way the tab opens with the ticket pre-filled and unsent, and the prompt tells the agent to
finish with `workflow ticket close <ID> --repo <Repo>`. A ticket for a repo that already has a session
goes into a git worktree under `D:\Projects\MindAttic.worktrees\<Repo>\<ID>` on branch `orch/<id>`, so
two agents never share a checkout. When the ticket closes, the queue offers to remove the worktree
(git refuses if it has uncommitted changes; the branch is always kept).

**4 · Audit.** *Drift*: for each agent file most repos share (`.claude/commands`, hooks, skills,
`.prose/commands`, `AGENTS.md`, `CLAUDE.md`), the version most repos have is canonical (repo name and
path are ignored), and every other copy is listed as *drifted* or, under 30% alike, *custom*, with a
diff button. *Missing*: shared files absent where 60%+ of agent-enabled repos have them.
*Duplication*: a built-in copy/paste detector (6-line fingerprints over meaningful lines; generated
code, migrations, build output and vendored libraries skipped) finds identical and near-identical
files across repos, clusters them, and suggests a home (`mindattic-agent-standard`,
`MindAttic.Components`, `MindAttic.Helpers`, `MindAttic.Deploy`).

## Limits

- Token usage and budgets are Claude Code only; Codex/Gemini/Kimi sessions show as Launcher tabs but
  aren't in the totals.
- A session started outside the Launcher is tracked and counted but can't be typed into.
- Claude Code prunes transcripts after about 30 days; the budget history window is 7 days, which
  always covers the current week.
- The budget file only updates while the dashboard or `orchestrator daemon` is running.
