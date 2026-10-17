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
   the last few minutes, every session and subagent together, is compared with it.
3. **Steer.** The result goes into `~/.claude/rl-status.json`, which the hooks in every Claude CLI read:
   - *Subagent allowance:* how many subagents may run at once across all CLIs. A new one waits while the allowance
     is used up, and is refused (try again once a slot frees) if it's still full after 5 minutes.
   - *Hold:* each tool call waits `20s × (demand ÷ sustainable − 1)`, up to 5 minutes, while the draw is over what
     the window can sustain. It's 0 as soon as the draw fits.
   - *Wind-down:* past 90% of the limit, agents are told to start only what they can finish, and to commit and
     file the rest as a ticket. Autopilot only hands out tickets whose cost estimate fits what's left.
   - *Stop:* at the target, every tool call is refused and every typed prompt is answered without being sent, so
     nothing more is spent until the reset. The orchestrator's own commands (`/autopilot`, `/throttle`) still work.

## Scaling up and down

The same arithmetic both speeds up and slows down:

- **Up:** when the draw is under the sustainable rate (a quiet start to a window, or agents finishing), the spare
  rate pays for more subagents, measured by what one typically draws, and holds drop to zero. `/autopilot` in a CLI
  claims as many tickets as the allowance has room for, each in its own git worktree, and hands each to a subagent.
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
- **The machine runs short** of handles or memory: autopilot starts nothing until it recovers.
