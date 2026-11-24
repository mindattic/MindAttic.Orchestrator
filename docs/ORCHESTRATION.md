# How orchestration works

MindAttic.Orchestrator keeps Claude agents working around the clock on the workspace's tickets, and spends each
5-hour usage window almost to its end (98% by default, adjustable) without ever reaching Claude's limit.

## Why spend almost all of every window, and never the limit

Claude's subscription meters usage in 5-hour windows. Whatever a window doesn't use is gone when it resets, so an
idle hour is lost work. Reaching the limit is worse than stopping early: every session, tab and subagent stops
mid-step until the reset, work is left half done, and nothing can run, not even a typed question. So the goal is to
land as close to the limit as possible at the moment the window resets, never on it before. The target is
`PaceTargetPercent` (98% by default, set in the dashboard's Settings; a login's own `orchestrator org limit <who> --5h <pct>` overrides it for that login). The gap above it is room for what the
orchestrator can't predict: you typing a question, an agent it didn't start, a reply bigger than usual.

## Never past the machine's RAM either

The windows aren't the only wall. Out of RAM, Windows pages to a pagefile that, system-managed, grows until the disk is
full, and only a hard restart recovers, losing everything in flight. So the memory guard (M.ORCH-T1494) is a third
cutoff beside the 5-hour and weekly ones, and like them it lets running work finish rather than start more:

- **Tight** (under `MemoryTightGb`, 4 GB free, or 80% of a fixed commit limit): every pace's subagent allowance drops to
  the ones running (`Pacer.CapByMemory`, the last step on every pace, paced or not), `orchestrator next` hands nothing out,
  no CLI starts (`orchestrator start --ignore-memory` overrides; the dashboard asks), and main-line suites wait.
- **Critical** (2 GB, 88%): also, once a minute, the newest ticket subagent is aborted through the cool-off's abort mark
  ("to free memory"): it stops at its next tool call, its work kept, its ticket first in the queue. Nothing is killed.
- **Emergency** (1 GB, 94%): every tool call is held, then refused with "end your turn", unlatched, until RAM returns.

Entered at once, left a step at a time once 2 GB (5 points) clear of the line for a minute: the flapping that made
machine health alert-only (M.ORCH-T357/T374) doesn't come back. Below Tight, new subagents start only as far as free RAM
holds at `MemoryPerSubagentGb` (1 GB) each above the line. The pace gate reads free RAM itself on each Agent call (a burst
reserves its share under a lock for the minute they load) and on every call for the emergency, so a stale daemon can't
hide a fall. `set-pagefile.ps1` fixes the pagefile at 16 GB, the backstop that keeps the disk from filling whatever happens.

## The loop

The daemon starts at logon and refreshes every 15 seconds, sooner when a turn ends or Claude reports new usage:

1. **Measure.** It reads Claude Code's own 5-hour percent and reset time (the shared statusline saves them each
   minute to `~/.claude/claude-usage.json`, keeping only the highest reading in a window, since an idle session
   carries an older, lower one), and paces straight off that live number while it's fresh, not a point of safety
   margin added on top, which used to drift ahead of Claude's own indicator (M.ORCH-T788). Past `ReportFreshMinutes`
   old -- nothing redraws the file while no interactive session is open -- the tokens the local transcripts record
   since that report are added back, so the hard cutoff still sees real spend during a quiet statusline (M.ORCH-T783).
   The window's size in tokens is learned from how far Claude's percent moved while a known number of tokens was spent.
2. **Pace.** What's left of the target, divided by the hours until the reset, is the sustainable rate. The draw over
   the last few minutes, every session and subagent together, is compared with it. Above it, agents are held until it
   catches up. That is a hold, not the stop: the stop comes only at the target. The pace is always the `linear`
   profile: `PaceProfile` is hard-coded to it, and `orchestrator settings --pace-profile` and `--pace-curve-k` are
   refused ("Pace profiles are gone", M.ORCH-T706). The front-loaded curve (target x elapsed^k) is retired, and with it
   its "no new subagent or ticket past 85%" rule.

   *Front-loaded spike (M.ORCH-T399, historical).* Front-loaded ran from 2026-10-18 17:19Z with k 0.55 and target 98%, and
   nothing else changed. Results come from the pace ledger, Claude's meter readings (then `pace-ledger.jsonl` and `usage-samples.json`, now orchestrator.db's `pace_ledger` and `usage_samples`) and `pace-gate.jsonl`. The
   subagents running at each crossing come from the subagent transcripts' first and last timestamps.

   | Window (UTC) | Peak | Final | Over 98 | Limit hit | Min at target | Refused | Subagents at 85% / 95% |
   |---|---|---|---|---|---|---|---|
   | 10-18 20:20 to 10-19 01:20 | 65 | 65 | 0 | none | 0 | 4 (Agent, 16 to 27%) | not reached / not reached |
   | 10-19 01:20 to 06:20 | 95 | 95 | 0 | none | 0 | 42 (Agent; 5 past 85%) | 2 (U.SPC-T49 bug hunt, started 157 min before; a bughunt subagent, started 25 min before) / 0 |
   | 10-19 06:20 to 11:20 | 49 | 49 | 0 | none | 0 | 0 | not reached / not reached (graded Blind: meter unseen 1h55m) |
   | 10-19 14:30 to 19:30 | 96 | 96 | 0 | none | 0 | 15 (Agent, 38 to 76%) | 3 (GG-T97, GG-T100, GG-T110, all started 46 min before at about 76%) / 2 (GG-T97, GG-T110) |

   No window passed 99%. The highest peak was 96%, there was no overshoot, and no limit was hit. The 85% rule held:
   every subagent running past 85% had started before 85%, and later Agent calls were refused. Front-loaded was
   later retired (M.ORCH-T706). No window reached the 98% target. The 65% and 49% windows were short on demand, not held back:
   pacing held agents for 47 minutes and 1 minute in those windows.
3. **Steer.** The result goes into orchestrator.db's `rl-status` row (and `~/.claude/rl-status.json`, the same status
   exported for rl-check.py and the Launcher), which the hooks in every Claude CLI read, taking whichever is newer. Their
   holds and refusals go into its `gate_events` (once `pace-gate.jsonl`; a hook that finds the database held still
   appends there, and the daemon reads it in). What the status steers:
   - *Subagent allowance:* how many subagents may run at once across all CLIs. A new one waits while the allowance
     is used up, and is refused (try again once a slot frees) if it's still full after 5 minutes. It's shared fairly:
     each active CLI (one with subagents running, or one waiting for one) has `allowance ÷ active CLIs` (at least one),
     and may go past that only into places no other CLI is waiting for. A CLI over its share starts nothing new while
     another waits, so the next place that frees goes to the CLI under its share; one that wants no more lends its
     unused share. `orchestrator next --claim` likewise keeps a share for every other `/allocate` CLI with workable
     tickets of its own (`rl-status.json` → `subagents_by_cli`).
   - *Hand-started tickets (M.ORCH-T1696):* a ticket a CLI starts by hand at the user's request is claimed with
     `orchestrator claim <ID>`: the same assignment `next --claim` records (this CLI, its session and login, the repo's
     checkout), so `/allocate` and the daemon's who-works-it sync leave it alone. It's refused while another live CLI (or a
     running background agent) holds it; a claim whose CLI is gone is taken over. It ends as any claim does: the ticket
     closing, `orchestrator release <ID>`, or its CLI going away. (`tickets ticket working` lives in MindAttic.Tickets and
     doesn't call it; claim first, then work.)
   - *Hold:* each tool call waits `20s × (demand ÷ sustainable − 1)`, up to 5 minutes, while the draw is over what
     the window can sustain. It's 0 as soon as the draw fits.
   - *Stop:* at the target, every tool call is refused and every typed prompt is answered without being sent, so
     nothing more is spent until the reset. The orchestrator's own commands (`/allocate`, `/pacing`) still work.

## What may start: approval, requires and epics

Only fixes and tickets approved in the ticket database's queue are work. A ticket that `requires` another (a
`tickets ticket block` link, or "blocked by X" in its summary) waits until that ticket closes. The one exception is an
epic, a ticket labelled `epic` (M.ORCH-T1146): a requirement on an epic is met once the epic is approved (started),
not only once it closes. So an epic's children can be approved one by one and still not start until the epic itself is
approved, while any other requirement they carry, such as an earlier phase's tickets, still waits for closure. An epic
is never queued, proposed, handed out by `orchestrator next --claim` or counted as work by keep-going: while unapproved
it waits for your yes with the other proposed tickets, and once approved it just stays open until its children are done.

## The owner inbox: what waits on you (M.ORCH-T1689)

Everything waiting on the owner is one list, so nothing stalls unseen: `orchestrator inbox` (`--json` for scripts), and
the dashboard's Inbox button in the menu bar, which shows the count and opens the list. A ticket's id opens it in Tickets
Desktop, where it is approved or edited; every row carries the command that answers it. What it lists today:

| Kind | What | Answer |
| --- | --- | --- |
| Orphaned | A ticket whose CLI or tab went dark and that the daemon gave up redispatching, or that has waited a stale period (`StaleAfterHours`) for its redispatch | `orchestrator release <ID>` |
| Waiting on you | An open ticket labelled `waiting-on-owner` | `tickets ticket label remove <ID> waiting-on-owner` |
| Stale | A stale claim nobody picked up for another stale period after it went stale | `orchestrator release <ID>` |
| Approve | An open ticket that isn't a fix and isn't approved | `tickets ticket approve <ID>` |
| Sign in | A login whose meter can't be read until its person signs in again | `orchestrator login --account <email>` |
| Consent | On a member's PC, someone asking to type into one of their CLIs on the host | `orchestrator machine consent <id> allow\|deny` |

A ticket two kinds name is listed once, by the first in that order. The list is built from what a tick already has (the
snapshot, the plan, the `orphaned-tickets` and `meter-probe` state rows); it runs no query of its own. Each kind is one
provider in `OwnerInbox.Providers` (`Core/Status/OwnerInbox.cs`): a later kind (an escalation, a quarantined unit, a
milestone acceptance) is one more provider there.

**The alert.** Once a minute the daemon compares the list with the items it has already seen (the `owner-inbox-alerted`
state row) and texts the alert recipient through Psst about the new ones: one text per look, naming up to three and
counting the rest. An item is alerted once and never again, across restarts and daemons, and one that leaves the list
and comes back within 90 days stays quiet. The first look only records what is already waiting, so a backlog isn't
texted. The Vault's "Text new inbox items" setting (`InboxAlerts`, on by default) turns the texts off; items are still
recorded as seen, so turning it back on texts no backlog. Consent requests are not texted (the daemon doesn't ask the
host for them; the dashboard already shows a toast for each).

## Plans: ideas that aren't tickets yet (M.ORCH-T1826)

A plan is a half-formed idea, or a feature that isn't ready for an implementation ticket: a markdown file saved under
`docs\plans` at the workspace root or in any repo. The dashboard's Plans button in the menu bar (the lightbulb, after
the Inbox) says how many there are and opens the list, newest first. Each card shows:

| Part | Where it comes from |
| --- | --- |
| Title | The file's first `# ` heading, else its file name. It is a link that opens the file |
| Repo and age | The repo it was saved in (the workspace folder's name for the root), and when it was last written |
| Status | What follows `Status:` on the line that starts with it, when the file has one |
| Summary | The first paragraph that is not a heading or the status |
| Show in folder | Selects the file in Explorer |

The list is read with each refresh by `PlanCatalog` (`Core/Status/PlanCatalog.cs`) from the repos the snapshot already
lists; it keeps no state of its own, and only the head of each file is read. Three Vault settings (Dashboard group) dial
it: "Plans folder" (`PlansFolder`, `docs\plans`), "Plan summary length (characters)" (`PlanSummaryMaxChars`, 200) and
"Plan file head read (KB)" (`PlanHeadKb`, 8).

## Ticket attribution: flagged, not blocked (M.ORCH-T1520)

MINDATTIC_AGENT.md's GOLDEN RULE says every request is a ticket, filed before any code is written for
it. Nothing here enforces that as a hard rule against a live session, and that's a deliberate choice, not
an oversight:

- A session's ticket is still only a heuristic guess (`Activity/TicketAttribution.cs`'s `FromPrompt`/
  `FromWorktree`), not a hard foreign key: it can come back `null` even while the session is genuinely
  working, and nothing stops that session from continuing.
- The obvious place to enforce it harder is the pace gate (`AgentGate.cs`/`pace-gate.py`), the
  `PreToolUse` hook already gating every tool call for every login, org-wide, with no staged rollout and
  no feature flag to fall back on. Extending that single ~3,700-line generated-Python file to also refuse
  a tool call for missing ticket attribution was considered and rejected for a first cut: a bug there
  would hold every CLI's every tool call at once, which is a far worse failure than an occasional
  ticket-less session.
- Instead, the dashboard sidebar (`MainViewModel.LiveActivity.cs`) flags a live session that's gone past
  `TicketlessFlagAfter` (15 minutes) with no ticket attributed: its activity card (M.ORCH-T1519, the same
  card shown for a session with no ticket at all) turns up a warning line, and the first time a session
  crosses the line it also raises a toast, the same shape as the existing "Looks stuck" toast. Both are
  UI-only: nothing is refused, held or blocked.
- This is a first cut, not the final word: a hard block is still worth building once there's a safe way to
  stage or flag-gate a pace-gate change (or once ticket attribution is itself a hard foreign key, which
  would remove the need for a guess-based gate entirely). Re-evaluate before assuming "flag only" is
  permanent.

## Scaling up and down

The same arithmetic both speeds up and slows down:

- **Up:** when the draw is under the sustainable rate (a quiet start to a window, or agents finishing), the spare
  rate pays for more subagents, measured by what one typically draws, and holds drop to zero. `/allocate` in a CLI
  claims as many tickets as its fair share of the allowance has room for and hands each to a subagent. Every one works
  It warms up: one subagent the minute `/allocate` turned on in it, one more each minute after, so CLIs opened just after
  it claim their share before it fills every place; a freed place goes to the waiting CLI furthest under its share.
- **Down:** when the draw runs ahead, the allowance falls below the number running, so no new subagent starts and the
  extras drain as they finish. Holds stretch every tool call, so running agents slow down without stopping.
- **Near the reset**, the hours left shrink faster than the budget left, so the sustainable rate rises and the
  window's last stretch is spent instead of wasted.

The week is shown but never paced (weekly-aware pacing is hard-coded off, M.ORCH-T706): spending each 5-hour window to
its target is the goal. The week has a hard cutoff per login instead: 90% of it by default (`WeeklyStopPercent`, 100
minus the 10-point weekly reserve), or the login's own `orchestrator org limit <who> --weekly <pct>` (M.ORCH-T707).
Reaching it locks that login (see "Login lock" below).

## When something fails

- **The daemon dies or stalls** (`rl-status.json` over 3 minutes old): both hooks fall back to Claude's own percent
  and refuse at 99%, and every typed prompt warns that the daemon is down.
- **An agent dies:** tool processes it left behind (searches, shells) are killed within a minute, and its ticket goes
  back to the queue for you.
- **The machine runs short** of handles or memory: you're alerted with who's using it, and nothing is held (heavy
  load is the point). The one freeze ever seen came from leaked handles (two orphaned `find /` runs, 660k handles),
  and that cause is stopped at its source: whole-drive searches are refused and runaway or orphaned agent tool
  processes are reaped. Memory commit has sat at 97-100% many times under full load without an agent crashing
  (M.ORCH-T374).

## Login lock (M.ORCH-T2108)

A locked login can't open a CLI, take work or keep running CLIs until it is unlocked. Nothing unlocks it by time, not
even the weekly reset. The API key is never locked; every real login is, the global one included.

**How a login gets locked and unlocked**

| Way | What it does |
| --- | --- |
| Manual lock | Right-click the login's tab, Lock; or `orchestrator org lock <who>` (M.ORCH-T2114, T2118). Reason `manual` |
| Weekly cutoff | When the login's week reaches its weekly cutoff (90% by default, or its `org limit --weekly`), the daemon locks it on the next tick, `LoginLocks.AutoLock` from `AccountRebalancer.CheckCutoffs` (M.ORCH-T2126). Reason `weekly-cutoff`. It fires whatever an `org allow` says: reaching 100% of the week stays impossible, and a lock wins over an allow |
| Unlock | Right-click, Unlock; or `orchestrator org unlock <who>`. Only the owner unlocks. A weekly-cutoff lock asks first in the dashboard, showing the week's current use, and `org unlock` prints it |

An owner Unlock holds for the rest of that weekly window, whatever the lock's reason: the unlock records the login's
current weekly reset (`LoginLockBook.UnlockedInWeek`), and the same window's cutoff does not lock it again. The next
weekly window's cutoff does. Two readings of one reset within an hour count as the same window.

**What a lock blocks**

- *Picks:* `AccountDirs.SignedIn` leaves a locked login out, so the Auto pick for a new tab, `LoginPicker` and the
  rebalancer never choose it.
- *Dispatch:* a ticket assigned to the locked login's person is held, not started, with "is locked (<reason> since
  <time>): orchestrator org unlock <who>" (`Dispatcher`, M.ORCH-T2124).
- *Moves:* `AccountMove` refuses to move a CLI onto a locked login (dashboard Move to, `orchestrator move`, rebalance),
  naming the lock.
- *Explicit opens:* `orchestrator start --account`, `org start`, `machine start --login` and the dashboard's Start on that
  login's tab refuse with the same text ("Not started: <email> is locked."). A CLI already running can still be
  reattached to.

**What happens to CLIs already running on it.** They are not killed. The login's pace becomes a stop with no subagent
allowance (`AccountPacer.LockedPace`): each CLI finishes its current step, every further tool call is refused and typed
prompts are answered unsent, the same as at a 5-hour cutoff. The note reads "<email>'s login is locked (its weekly
cutoff): nothing runs on it until it's unlocked (orchestrator org unlock <email>)." Unlocking lets them go on.

**What you see**

- A red lock badge on the login's tab and tree node, and the activity word "Locked (by you)" or "Locked at its weekly
  cutoff" (M.ORCH-T2119). The right-click menu shows Unlock while it is locked.
- `orchestrator org locks` lists every locked login with why and since; `org usage` and `org limit` mark a locked
  login. Each lock and unlock is logged (the last 200 kept) in the `login-locks` state row of `data/orchestrator.db`,
  and the lock book syncs to other dashboards.
- The weekly crossing alert, once per window, an urgent alert plus a Psst text: "<name> reached its weekly cutoff
  (<n>% of the week, cutoff <c>%) and is now locked: right-click Unlock, or orchestrator org unlock <who>; to change
  the cutoff: orchestrator org limit <who> --weekly <pct>." For a login with an active `org allow` it adds "(a lock
  wins over its org allow)" before the colon.

## What guarantees cleanup: Job Objects

A process tree the orchestrator starts is owned by a Windows Job Object with kill-on-close
(`MindAttic.Orchestrator.Term/JobObject.cs`). Windows ends every process in the job, children and grandchildren
included, the moment the job's last handle closes, and the only handle is held by the owner. So cleanup no longer
depends on anyone walking parent pids in time.

- **Hosted CLIs (M.ORCH-T1674).** Each TermHost starts its CLI inside its own job, from the CLI's first instruction
  (`PROC_THREAD_ATTRIBUTE_JOB_LIST`), so nothing it starts (shells, builds, node, dotnet, git) can be born outside it.
  The job closes when the CLI exits, and when the host exits, is stopped (`machine stop`, a closed tab) or is killed:
  whatever was left of the tree ends with it. The host itself and the pseudoconsole's OpenConsole are not in the job.
  Elevated hosts and hosts under a member's own account make their job the same way.
- **Commands the daemon runs (M.ORCH-T1675).** `ProcessRunner` runs every command (`dotnet`, `git`, `gh`, `tickets`)
  in its own job. At the timeout the whole job is ended, not only the processes still reachable by parent pid, and if
  the daemon itself dies mid-run Windows closes the job and the run's tree goes with it. Builds and tests
  (`ProcessRunner.Contained`: branch builds, main's tests, a branch's test run) also end whatever the command left
  running once it exited (MSBuild nodes, testhost, VBCSCompiler), so nothing holds the worktree's files or the output
  pipes. Other commands' leftovers are let go once the command has exited. The process joins its job right after it
  starts, not at creation, so a child born in those first moments would be missed; `Kill(entireProcessTree: true)`
  still runs at the timeout as the fallback.
- **What may outlive its CLI (M.ORCH-T1691).** Only a process started with `CREATE_BREAKAWAY_FROM_JOB`, which the job
  allows. One helper does that, `Breakaway.Start` (`MindAttic.Orchestrator.Term/Breakaway.cs`), and everything the
  orchestrator starts to outlive its caller goes through it, because a deploy or a command run from a hosted CLI would
  otherwise lose what it started the moment that CLI's host ended:
  - a detached TermHost (`Native.SpawnDetached`), so a CLI opened by another CLI outlives it;
  - the daemon (`Installer.StartDaemon`: an install, `machine` setup, the dashboard finding none running);
  - the dashboard, when an install restarts it, when a copy hands over to the installed one, and when `run.ps1 -Launch`
    starts it;
  - the gateway (`GatewaySupervisor`);
  - TermViewer windows (fallback viewers, pop-outs, the team wall's);
  - the Windows Terminal window of a started CLI or of the Launcher's menu (`SessionControl.Start`, `OpenLauncher`);
  - Tickets Desktop (a ticket opened from a command or the dashboard).

  If a job the caller is in forbids breakaway, Windows refuses with access denied and the process is started the
  ordinary way, inside the job. The helper passes no handles on, so the started process never holds a script's output
  pipe. A PowerShell script can't ask for the flag (`Start-Process` has no way to), so `run.ps1` starts the dashboard
  with `orchestrator spawn [--cwd <dir>] [--no-window] [--] <program> [arguments...]`, which starts the program through
  the same helper and exits (0: started, 1: couldn't, 2: bad command line). A CLI from before that verb (a first deploy)
  answers non-zero without starting anything, and the script falls back to `Start-Process` as before.
- **What stays contained, on purpose.** Everything else a hosted CLI starts: its shells, builds, tests and tools, and
  whatever a command waits on (`claude` sign-ins and token mints, `git`, `schtasks`, the Launcher's `run.ps1` and
  commit-all, a TermHost's short `--launch` step). So do `/allocate`'s background agents (`BackgroundAgent`), the
  sign-in page's private browser window (it is only needed while the sign-in command runs) and Explorer, VS Code or
  Notepad opened from the dashboard, which is itself outside any CLI's job. Nothing long-lived runs through
  `ProcessRunner`: every run is waited on to its exit or its timeout, and `ProcessRunner.Contained` is only `dotnet`
  builds and tests (branch builds, main's tests, a branch's test run).
- **When there is no job.** If Windows refuses the job, or the CLI won't start in it, the host logs why and starts the
  CLI without one; `MINDATTIC_TERM_JOB=off` in the host's environment switches it off. The tree is then found by
  parent pid as before (`ProcessTree`, `ProcessReaper`, `TermHostKill`), which also still covers CLIs that run in the
  dashboard's own process and hosts started before this change.

## Billing

Every Claude CLI bills one of two things, set by one switch (M.ORCH-T436): the **Claude Team** login
(`pdftransferjr@mindattic.com`, org MindAttic), for everyday tokens, or the **API key** in the MindAttic Vault keyring
(`LlmCredentialStore`, provider `Claude`), for emergencies only. The switch is the dashboard's account menu (its last
entry, "Use API key (emergencies)"; any account in it bills Claude Team), `orchestrator billing team|api`,
`orchestrator login --api-key`, or a `/login` to a claude.ai account: the last sign-in decides, so a /login to another
account than the one signed in when the API key was chosen sets the mode back to Claude Team. The dashboard's upper-right corner (the signed-in
email, or "API key"; its tooltip has the rest) and `orchestrator status` say who's billed (`Claude Team: <email> (<org>)`, or `API KEY: sk-ant-...<last4>`, never more of the
key) and what each open CLI bills.

The account menu (M.ORCH-T492) lists every account in the roster (`data/orchestrator.db`): seat (premium or standard),
weekly % used and left and its reset, the 5-hour % and its reset, and when it was last read, the signed-in one marked and
the one with the most of its week left starred. Picking another runs `claude auth login --email <address>` (`--sso` for an
Enterprise org), which opens the browser on that account's login; the dashboard follows the switch once it lands
(M.ORCH-T486). `orchestrator login <email-or-short-name>` does the same from a terminal, and a bare `orchestrator login`
prints the same table.

A switch:

1. **Sets Claude Code's choice of the key.** The Vault key's last 20 characters go into `customApiKeyResponses.approved`
   (API key) or `.rejected` (Claude Team) in `~/.claude.json`, out of the other list. Only that value's text changes;
   every other byte of the file is kept, the old file is kept beside it as `~/.claude.json.billing-backup`, and the new one
   is put in place whole, re-read first so a write Claude Code made meanwhile is never lost. Going to Claude Team also
   dates the key in `~/.claude/session-context/api-key-set-aside.json` (by a hash, never the key; the hooks read it into `data/orchestrator.db`'s `api-key-set-aside` state row and keep it as .bak, M.ORCH-T528), so a CLI started on it
   still reads as billing it until it restarts. The daemon puts the choice back in line if it drifts from the mode (a
   "yes" answered in a CLI's prompt).
2. **Writes the mode** to `data/orchestrator.db`'s `billing` state row and exports it to `~/.claude/session-context/billing.json` (M.ORCH-T528), where the Launcher reads it to start each CLI and the
   context gate reads it to tell each CLI at its start what it bills:

   ```jsonc
   {
     "mode": "team",                 // "team" (Claude Team) or "api" (the Vault key)
     "since": 1760803200.0,          // epoch seconds: when this mode was switched to
     "login": "<accountUuid>/<organizationUuid>",  // the claude.ai login signed in then; a /login to another sets "team"
     "by": "dashboard",              // "dashboard", "cli" or "login"
     "requested": ["<session id>"],  // CLIs whose Launcher tab took a restart request for this mode
     "refused": ["<session id>"],    // CLIs whose tab had no control pipe to take one
     "approved_at": 1760803200.0     // when the switch approved the key (null if it already was): CLIs started before
   }                                 //   then settled on the login at their start
   ```

3. **Asks the Launcher to restart each open hosted CLI that bills the other thing**, and flags every one outside the
   Launcher (a plain terminal, an IDE) as needing a manual restart: in the dashboard, `orchestrator status`, the daemon's
   log and a Needs-you alert. What a CLI bills is what its context gate recorded (`auth` in its session's
   `.state.json`: `subscription` is Claude Team, `api-key` the key); one billing a cloud provider or a gateway is left
   alone. The daemon asks again every 5 minutes a tab that couldn't be asked, and never asks a tab twice for one switch.

### The Launcher's control pipe (for M.LNCH-T38)

The request goes to the host, never into the CLI, on a pipe of its own beside the input pipe:

- **Name:** `\.\pipe\mindattic-control-{provider}-{host pid}`: the provider lowercased (`claude`) and the
  `MindAttic.Launcher.exe host` process id, the same pair as its input pipe `mindattic-host-{provider}-{pid}`. It's
  outside the `mindattic-host-` prefix so the orchestrator's tab scan never takes it for a tab. A Launcher without it
  (before M.LNCH-T38) simply isn't there: the orchestrator's 1-second connect fails and the CLI is flagged for a manual
  restart, rather than a request being typed into the prompt box.
- **Transport:** the orchestrator connects (`PipeDirection.Out`), writes one message and closes, as with the input pipe.
  The server reads the connection to its end. Nothing is sent back.
- **Message:** one UTF-8 JSON object on one line, ending in `\n`:

  ```json
  {"type":"restart-resume","reason":"billing","billing":"api","session":"<claude session id>","cliPid":4242,"cwd":"D:\Projects\Prose","requestedAt":"2026-10-18T16:00:00Z"}
  ```

  `type` is the only field every request has; ignore a `type` you don't know. `billing` is the mode the CLI is to bill
  (`team` or `api`), `session` the Claude session to resume, `cliPid` the claude.exe the request is about, `cwd` the
  folder it runs in.

  **Move to account (M.ORCH-T607, M.LNCH-T52):** the dashboard's "Move to" menu and `orchestrator move <repo|pid>
  --account <email>|--global` send the same request with `"reason":"account"`, a `configDir` and no `billing`:

  ```json
  {"type":"restart-resume","reason":"account","configDir":"C:\Users\me\.claude-accounts\b@x.com","session":"<claude session id>","cliPid":4242,"cwd":"D:\Projects\Prose","requestedAt":"2026-10-22T16:00:00Z"}
  ```

  `configDir` is the login to restart under: a per-CLI login's folder becomes `CLAUDE_CONFIG_DIR`, `""` is the global
  login (`CLAUDE_CONFIG_DIR` removed). The tab keeps it for every later restart, and a request without one (a billing
  restart) keeps the login the CLI is on. Only signed-in logins are offered, never the one the CLI is already on.

  **A move carries the CLI's subagents and locks its work (M.ORCH-T637).** The moment a move is sent (from any of the
  above, the rebalancer included) the CLI and every ticket it or its subagents hold (its `orchestrator next --claim`
  claims, and the ticket it was started on) are locked in `data/orchestrator.db`'s `account-moves` state row: the
  dashboard shows each as "Moving to <name>…" with Move to, Start, Use as prompt, Send, Go to tab, Release and Remove
  worktree (an old one) off; `orchestrator next` (and the daemon's nudges) never hand them out, claim them or put them to another CLI,
  and the moving CLI itself claims nothing; a second move of it is refused. Its running subagents are stopped at once
  (Esc typed into its tab interrupts its turn, so the restart doesn't wait for them); their tickets stay claimed and their
  uncommitted changes are kept. When the CLI shows up on the new login (its session, the same Launcher tab,
  or a CLI of its repo started since the move), the daemon puts the claims on the CLI as it is now and types into its tab a
  prompt to start their subagents again where they were (the repo's own checkout on main), under the new account. A move the tab refused unlocks at
  once; one that doesn't land within 30 minutes (or whose CLI ended and didn't come back within 5) unlocks with an alert,
  the claims staying with the old owner, whose CLI is told to start them again if it's still running.

  **Rebalance (M.ORCH-T608):** the daemon sends the same request on its own when a CLI's login runs low (Claude's meter
  puts its 5-hour window at that login's 5-hour cutoff or its week at its weekly cutoff, or its pace is at a limit) and a
  signed-in per-CLI login has no CLI and at least 10 points more headroom. The CLI isn't moved again for 30 minutes, its
  target stays taken until it arrives, and a login that's low itself is never a target. A locked login is
  never a target either (`AccountDirs.SignedIn` leaves it out). Setting `RebalanceLogins` (on by default;
  `orchestrator settings --rebalance false`) turns it off.

  **Automatic failover to the API key is retired (M.ORCH-T664, M.ORCH-T681, hard-coded off by M.ORCH-T706):**
  `FailoverToApiKey` is always false and `orchestrator settings --failover` is refused ("--failover is locked off").
  The daemon never moves a CLI onto the Vault's API key by itself; only the owner switches API billing on
  (`orchestrator billing api`). What happens at a login's line now:

  - *5-hour cutoff* (`PaceTargetPercent`, 98%, or the login's `org limit --5h`): the pace says stop. Every tool call of
    that login's CLIs is refused and typed prompts are answered unsent until the window resets. The CLI is not moved
    to another person's login (each person's remainder stays theirs) and not onto the key. The owner gets one alert
    and one Psst text per crossing (`CutoffAlerts`, M.ORCH-T742).
  - *Weekly cutoff:* the login is locked as described under "Login lock".
  - *Low login with room elsewhere:* the rebalance above moves the CLI to a signed-in login of the same org with
    more headroom.
  - *A CLI already on the key from an earlier failover* (the `api-failovers` state row): with failover off, the daemon
    sends the move-back at once, the same `"reason":"failover"` request with `"billing":"team"` and `now: false`, so it
    returns to its login and stops billing the key. Only that request is still sent; the message shape is the one
    below.

  ```json
  {"type":"restart-resume","reason":"failover","billing":"team","configDir":"","now":false,"session":"<claude session id>","cliPid":4242,"cwd":"D:\Projects\Prose","requestedAt":"2026-10-23T16:00:00Z"}
  ```

  `billing` is for this CLI alone: `billing.json`'s mode is left as it is, so the Launcher must not drop a failover
  request whose `billing` differs from it (step 3). `configDir` is the login it's on. A move-back is counted only once the
  CLI's session state shows it billing the login; a tab that took the request but did not restart the CLI is toasted as
  Needs you ("close and reopen this tab") and asked again 5 minutes later.

  **No outside-use detection (retired):** an earlier version lowered a login's 5-hour pace ceiling by guessing at the
  member's own use of Claude elsewhere, from the meter's rise the CLIs here couldn't explain. Removed (owner's rule): it
  moved the pace target off `PaceTargetPercent` on a noisy guess, which a member could watch drift down and back up for no
  reason they could see. The hard cutoffs (`PaceTargetPercent`, `org limit`) are the only thing that moves a login's target.
- **What the Launcher does:**
  1. Act only if `cliPid` is still the CLI this tab runs; otherwise drop the request.
  2. Wait for the CLI's next idle point: its turn has ended and it's waiting for input (the context gate's
     `~/.claude/session-context/<session>.state.json` says `"state": "idle"`, or the host's own idle detection). Never
     mid-turn, and never while a subagent of it runs.
  3. Re-read `billing.json`. If its `mode` is no longer the request's `billing` (switched back meanwhile), drop the
     request. A `"reason":"failover"` request is never dropped for this: its `billing` is the CLI's own.
  4. End the CLI and start `claude --resume <session>` in the same tab and folder, with the environment for the mode:
     `team` with `ANTHROPIC_API_KEY` removed; `api` with `ANTHROPIC_API_KEY` set to the Vault's `Claude` key (Claude Code
     uses it because the switch approved it).
  5. A request for a session already restarting, or already restarted for the same mode, is a no-op: the orchestrator may
     send one again after a daemon restart.
- **Launching new CLIs:** every CLI the Launcher starts reads `billing.json` the same way (no file: keep today's rule, no
  key while a claude.ai login is signed in).
