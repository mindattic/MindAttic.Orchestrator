# How orchestration works

MindAttic.Orchestrator keeps Claude agents working around the clock on the workspace's tickets, and spends each
5-hour usage window almost to its end (98% by default, adjustable) without ever reaching Claude's limit.

## Why spend almost all of every window, and never the limit

Claude's subscription meters usage in 5-hour windows. Whatever a window doesn't use is gone when it resets, so an
idle hour is lost work. Reaching the limit is worse than stopping early: every session, tab and subagent stops
mid-step until the reset, work is left half done, and nothing can run, not even a typed question. So the goal is to
land as close to the limit as possible at the moment the window resets, never on it before. The target is
`PaceTargetPercent` (98% by default, set in the dashboard's Settings). The gap above it is room for what the
orchestrator can't predict: you typing a question, an agent it didn't start, a reply bigger than usual.

## The loop

The daemon starts at logon and refreshes every 15 seconds, sooner when a turn ends or Claude reports new usage:

1. **Measure.** It reads Claude Code's own 5-hour percent and reset time (the shared statusline saves them each
   minute to `~/.claude/claude-usage.json`, keeping only the highest reading in a window, since an idle session
   carries an older, lower one). It adds a point for safety, plus the tokens the local transcripts record since that
   report. The window's size in tokens is learned from how far Claude's percent moved while a known number of
   tokens was spent.
2. **Pace.** What's left of the target, divided by the hours until the reset, is the sustainable rate. The draw over
   the last few minutes, every session and subagent together, is compared with it. That is the `linear` pace profile,
   the default. The `front-loaded` profile (`orchestrator settings --pace-profile front-loaded`, or the dashboard's
   Settings) instead follows the curve target × elapsed^k (`--pace-curve-k`, 0.55 by default): the rate is whatever
   reaches that curve 30 minutes from now, so it spends faster early and eases off. In the last 30 minutes it matches
   the linear rate, and the curve never aims past the target. Above the curve, agents are held until it catches up.
   That is a hold, not the stop: the stop comes only at the target. Past 85% it starts no new subagent or ticket. The
   profile is read on every check, so a switch mid-window applies to the next call; going back to linear spreads
   what's left evenly again. The utilization chart draws the active profile's curve.

   *Front-loaded spike (M.ORCH-T399).* Front-loaded ran from 2026-10-18 17:19Z with k 0.55 and target 98%, and
   nothing else changed. Results come from `pace-ledger.jsonl`, `usage-samples.json` and `pace-gate.jsonl`. The
   subagents running at each crossing come from the subagent transcripts' first and last timestamps.

   | Window (UTC) | Peak | Final | Over 98 | Limit hit | Min at target | Refused | Subagents at 85% / 95% |
   |---|---|---|---|---|---|---|---|
   | 10-18 20:20 to 10-19 01:20 | 65 | 65 | 0 | none | 0 | 4 (Agent, 16 to 27%) | not reached / not reached |
   | 10-19 01:20 to 06:20 | 95 | 95 | 0 | none | 0 | 42 (Agent; 5 past 85%) | 2 (U.SPC-T49 bug hunt, started 157 min before; a bughunt subagent, started 25 min before) / 0 |
   | 10-19 06:20 to 11:20 | 49 | 49 | 0 | none | 0 | 0 | not reached / not reached (graded Blind: meter unseen 1h55m) |
   | 10-19 14:30 to 19:30 | 96 | 96 | 0 | none | 0 | 15 (Agent, 38 to 76%) | 3 (GG-T97, GG-T100, GG-T110, all started 46 min before at about 76%) / 2 (GG-T97, GG-T110) |

   No window passed 99%. The highest peak was 96%, there was no overshoot, and no limit was hit. The 85% rule held:
   every subagent running past 85% had started before 85%, and later Agent calls were refused. Front-loaded stays as
   an option. No window reached the 98% target. The 65% and 49% windows were short on demand, not held back:
   pacing held agents for 47 minutes and 1 minute in those windows.
3. **Steer.** The result goes into `~/.claude/rl-status.json`, which the hooks in every Claude CLI read:
   - *Subagent allowance:* how many subagents may run at once across all CLIs. A new one waits while the allowance
     is used up, and is refused (try again once a slot frees) if it's still full after 5 minutes. It's shared fairly:
     each active CLI (one with subagents running, or one waiting for one) has `allowance ÷ active CLIs` (at least one),
     and may go past that only into places no other CLI is waiting for. A CLI over its share starts nothing new while
     another waits, so the next place that frees goes to the CLI under its share; one that wants no more lends its
     unused share. `orchestrator next --claim` likewise keeps a share for every other `/autopilot` CLI with workable
     tickets of its own (`rl-status.json` → `subagents_by_cli`).
   - *Hold:* each tool call waits `20s × (demand ÷ sustainable − 1)`, up to 5 minutes, while the draw is over what
     the window can sustain. It's 0 as soon as the draw fits.
   - *Wind-down:* past 90% of the limit, agents are told to start only what they can finish, and to commit and
     file the rest as a ticket. Autopilot starts no new ticket until the window resets.
   - *Stop:* at the target, every tool call is refused and every typed prompt is answered without being sent, so
     nothing more is spent until the reset. The orchestrator's own commands (`/autopilot`, `/pacing`) still work.

## Scaling up and down

The same arithmetic both speeds up and slows down:

- **Up:** when the draw is under the sustainable rate (a quiet start to a window, or agents finishing), the spare
  rate pays for more subagents, measured by what one typically draws, and holds drop to zero. `/autopilot` in a CLI
  claims as many tickets as its fair share of the allowance has room for, each in its own git worktree, and hands each to a subagent.
- **Down:** when the draw runs ahead, the allowance falls below the number running, so no new subagent starts and the
  extras drain as they finish. Holds stretch every tool call, so running agents slow down without stopping.
- **Near the reset**, the hours left shrink faster than the budget left, so the sustainable rate rises and the
  window's last stretch is spent instead of wasted.

The week is shown but never paced: spending each 5-hour window to its target is the goal, and only a weekly limit
Claude actually reports stops new work.

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
   dates the key in `~/.claude/session-context/api-key-set-aside.json` (by a hash, never the key), so a CLI started on it
   still reads as billing it until it restarts. The daemon puts the choice back in line if it drifts from the mode (a
   "yes" answered in a CLI's prompt).
2. **Writes the mode** to `~/.claude/session-context/billing.json`, where the Launcher reads it to start each CLI and the
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
- **What the Launcher does:**
  1. Act only if `cliPid` is still the CLI this tab runs; otherwise drop the request.
  2. Wait for the CLI's next idle point: its turn has ended and it's waiting for input (the context gate's
     `~/.claude/session-context/<session>.state.json` says `"state": "idle"`, or the host's own idle detection). Never
     mid-turn, and never while a subagent of it runs.
  3. Re-read `billing.json`. If its `mode` is no longer the request's `billing` (switched back meanwhile), drop the
     request.
  4. End the CLI and start `claude --resume <session>` in the same tab and folder, with the environment for the mode:
     `team` with `ANTHROPIC_API_KEY` removed; `api` with `ANTHROPIC_API_KEY` set to the Vault's `Claude` key (Claude Code
     uses it because the switch approved it).
  5. A request for a session already restarting, or already restarted for the same mode, is a no-op: the orchestrator may
     send one again after a daemon restart.
- **Launching new CLIs:** every CLI the Launcher starts reads `billing.json` the same way (no file: keep today's rule, no
  key while a claude.ai login is signed in).
