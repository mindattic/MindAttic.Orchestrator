---
codex: 1
project: MindAttic.Orchestrator
code: MAO
layer: stories
status: living
updated: 2026-10-13
---

# MindAttic.Orchestrator — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| MAO-T1 | 2026-10-13 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| MAO-T2 | 2026-10-13 | feature | closed | Status dashboard (WPF on MindAttic.Components.Wpf) + CLI: repos grouped Needs you / Working / Open tickets / Quiet from Workflow tickets, Launcher host pipes and Claude transcripts; toast + taskbar flash when an agent finishes a turn | TranscriptReaderTests, MatchingTests |
| MAO-T3 | 2026-10-13 | feature | closed | Session control: start a Claude/Codex/Gemini/Kimi Launcher tab for any repo (prompt pre-filled, never sent) and type replies into a live Launcher tab | MatchingTests.Start_arguments_escape_semicolons_for_windows_terminal |
| MAO-T4 | 2026-10-13 | feature | closed | Token budget: 5-hour usage blocks from transcripts, uncached-token budgets per block and per 7 days, burn rate + projected limit time, keeps ~/.claude/rl-status.json current for rl-check.py | BudgetTests |
| MAO-T5 | 2026-10-13 | feature | closed | Cross-repo scheduler: one ticket queue across all repos (priority, fix>feature>chore>docs, oldest), proposals up to free slots, budget hold, worktree per ticket when the repo is busy, opt-in paced auto-dispatch | SchedulerTests |
| MAO-T6 | 2026-10-13 | feature | closed | Audit: drift in shared .claude/.prose agent files (majority version is canonical), files most repos have but some lack, and built-in cross-repo copy/paste detection with suggested homes | AuditTests |
| MAO-T7 | 2026-10-13 | fix | closed | Live transcripts looked idle because Windows reports stale mtimes for files held open; read today's transcripts by real length and stamp growth as activity | TranscriptReaderTests.First_read_keeps_the_files_own_timestamp_but_later_growth_counts_as_now |
| MAO-T8 | 2026-10-13 | fix | closed | With no budget set, the current block counted as its own peak and raised a false 100% alarm and limit projection; peak-relative mode is now context only and writes no limit file | BudgetTests.Beating_your_peak_is_context_not_an_alarm |
| MAO-T9 | 2026-10-13 | chore | open | Manually verify the paths that act on real sessions: Start, Send, queue Start/dispatch into a worktree, Remove worktree, and auto-dispatch (not exercised against live agents yet) | — |
| MAO-T10 | 2026-10-13 | fix | open | Assignments never expire: if a dispatched session ends without closing its ticket, the ticket stays assigned and out of the queue forever; release stale assignments (no transcript or tab activity for N hours) and surface them | — |
| MAO-T11 | 2026-10-13 | feature | open | Weekly budget is a rolling 7 days, but Claude's weekly limit resets at a fixed time; add a weekly reset anchor setting so the weekly percent restarts when the real week does | — |
| MAO-T12 | 2026-10-13 | feature | open | Keep rl-status.json and auto-dispatch running without the dashboard open: run 'orchestrator daemon' at logon (scheduled task) or from the Launcher | — |
| MAO-T13 | 2026-10-13 | chore | open | Ship like the other apps: deploy.ps1 publishing the dashboard and CLI to C:\Apps\MindAttic.Orchestrator, plus a Launcher menu entry to open it | — |
| MAO-T14 | 2026-10-13 | feature | open | Token usage for Codex, Gemini and Kimi sessions (read their local session logs) so budgets and per-repo totals cover every provider, not just Claude Code | — |
| MAO-T15 | 2026-10-13 | fix | open | First refresh reads 7 days of transcripts (~3s); persist reader offsets and per-file usage between runs so startup is instant | — |
| MAO-T16 | 2026-10-13 | chore | open | Act on audit findings: move statusline.ps1 (25 repos), quickload-on-do.ps1 (22 identical + drifted copies) and tools/build-readme.ps1 (20) into mindattic-agent-standard and have install.ps1 point repos at the shared copy | — |
| MAO-T17 | 2026-10-13 | feature | open | Audit: schedule a daily run and toast when new drift or duplication appears, instead of only on demand | — |
