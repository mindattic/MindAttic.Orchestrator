---
layer: bible
status: living
---
# MindAttic.Orchestrator — Project Bible

> Single source of truth for what the Orchestrator IS, is NOT, and the rules that keep it coherent.
> README says how to build and run it; this says how to think about it. The finish line lives in
> `docs/RELEASE.md`.

## 1. The one sentence {#ORCH-§1}
A Windows daemon, dashboard and CLI that keeps Claude agents working the workspace's tickets around the clock and
spends each 5-hour usage window to just under its limit, never over.

## 2. The product promise {#ORCH-§2}
- Spend almost all of every 5-hour window: pace so the window lands on its target (98% by default) at its reset.
- Never hit the wall: at the target every tool call is refused, and the hooks still refuse at 99% if the daemon is down.
- `/allocate` in one CLI works its repo's tickets through subagents, all in the repo's own checkout on `main`, landing
  each on `main` and closing it.
- One place to see every agent, login, queue and budget; remote use from a phone over Tailscale + RDP
  (docs/TAILSCALE.md).
- The machine is never exhausted: processes, scratch, disk and RAM the Orchestrator starts are owned and reclaimed.

## 3. What it is NOT {#ORCH-§3}
- Not the ticket store: MindAttic.Tickets owns tickets; approving, prioritizing and showing a ticket are
  `tickets ticket ...` and Tickets Desktop (M.ORCH-T762).
- Not a multi-machine hub: member machines as Relay spokes (hub phases 3-5) are out of scope (docs/RELEASE.md §5).
- Not a test runner on its own initiative: suites run only on the owner's `/test`.
- Not a throttle that protects the machine by guessing: the only holds are the pace hold, the hard cutoffs and the
  hard exhaustion floors (disk, RAM).
- Not a branch or worktree manager: one checkout on `main`.

## 4. Architecture canon {#ORCH-§4}
```text
 Claude Code (CLIs, tabs, subagents)           MindAttic.Orchestrator
 statusline -> ~/.claude/claude-usage.json -->  Daemon (Scheduler job, every 15s)
 transcripts -> ~/.claude/projects/**.jsonl ->    measure -> pace -> steer
 hooks <- ~/.claude/rl-status.json <----------   Dashboard (WPF)   CLI (orchestrator)
   pace-gate / context-gate (AgentGate)          TermHost (ConPTY) -> TermViewer / Gateway
 /allocate -> orchestrator next --claim  ------>  MindAttic.Tickets (Tickets.db)
```

### 4.1 Projects
| Project | Role |
|---|---|
| `MindAttic.Orchestrator.Core` | budget and pacing, pace ledger, scheduling and allocate, sessions, activity, audit, health, installer, the hook scripts (`Setup/AgentGate.cs`) |
| `MindAttic.Orchestrator.Wpf` | the dashboard (`MindAttic.Orchestrator.Desktop.exe`) |
| `MindAttic.Orchestrator.Daemon` | the headless daemon: keeps budget, pace and hooks current |
| `MindAttic.Orchestrator.Cli` | the `orchestrator` CLI (Spectre.Console) |
| `MindAttic.Orchestrator.Term`, `.TermHost`, `.Term.Wpf`, `.TermViewer` | hosted CLIs on ConPTY that outlive the dashboard, and their viewers |
| `MindAttic.Orchestrator.Gateway` | team gateway serving hosted CLIs over TLS; off by default |
| `tests/MindAttic.Orchestrator.Tests` | NUnit tests, including the Python hooks run for real |

### 4.2 Domain model
Login (an account's token pool), window (5-hour and weekly), pace target and cutoff, allowance (subagent slots),
hold and stop, ticket claim and lease, hosted session (TermHost), resource ledger entry, health report, milestone.

### 4.3 Key services
`Budget/Pacer.cs` (pace and allowance), `Setup/AgentGate.cs` (generates the pace and context gate hooks),
`Activity/MemoryGuard.cs` and `Activity/DiskGuard.cs` (exhaustion floors), `Activity/ResourceLedger.cs` and
`Activity/LedgerSweeper.cs` (ownership and reclaim), `Health/HealthThresholds.cs` (grades), `LogPruner.cs`
(log growth), `Settings/OrchestratorVaultSettings.cs` (the Vault-backed settings).

## 5. The Laws {#ORCH-§5}
Inherits `MindAttic.HouseRules.md` by reference.
1. {#ORCH-LAW-1} Hard cutoffs only: 98% of the 5-hour window and 90% of the week (per login, settings), never over.
   The API key is never used automatically; a person's own use wins.
2. {#ORCH-LAW-2} Tickets are the only work store. A job is an epic, a unit is a ticket, order is `requires` links.
   Containers (epics, features, split stories) are never dispatched as work.
3. {#ORCH-LAW-3} Agents start only when a member starts them; nothing self-enables in a fresh CLI.
4. {#ORCH-LAW-4} One checkout on `main`; a landing commits only its own ticket's paths and never reverts others' work.
5. {#ORCH-LAW-5} Every tunable number is a Vault setting (Vault.Desktop's Orchestrator tab and `orchestrator
   settings`), never a const, so it can be dialed live.
6. {#ORCH-LAW-6} Verification inside work is build-only; tests run only on the owner's `/test`.
7. {#ORCH-LAW-7} Everything the Orchestrator creates (processes, scratch, logs) is registered and reclaimed; delete
   only what is proven delivered.
8. {#ORCH-LAW-8} Redeploy only through `run.ps1`, which obeys the busy gate; a TermHost is never stopped, so hosted
   CLIs survive.

## 6. Verified state {#ORCH-§6}
- In daily use (README badge). Pacing, the stop, `/allocate`, per-CLI logins, lock and move, health probes.
- Resource ownership epic (M.ORCH-T1673): Job Objects, resource ledger and sweeper, startup orphan sweep, disk floor,
  heavy-process lease, growth caps, all landed.
- Week-long growth bounded (M.ORCH-T1611, cb9bc66b): hourly prune of MindAttic.Log and finished TermHost logs.
- Pacer tunables moved to Vault settings (M.ORCH-T2336, d41ae8cf).
- Recent bughunts of Core and AgentGate landed (M.ORCH-T2312..T2346).

## 7. Active frontier {#ORCH-§7}
`docs/RELEASE.md`: the milestone and its epics. Longer-range ideas: `docs/AGENTIC_ROADMAP.md` (out of scope until the
milestone is reached), `docs/research/hub-and-spoke.md`.

## 8. Quality bar {#ORCH-§8}
A change is done when the solution builds on `main`, the change is committed with its ticket ID, the version
(`.\version.ps1`) is reported, and any new number it introduces is a Vault setting. Docs that describe the behaviour
(README, docs/*.md) are updated in the same change.

## 9. Glossary {#ORCH-§9}
- **Pace target**: the share of a 5-hour window to spend by its reset (98% default).
- **Allowance**: how many subagents may run at once across every CLI.
- **Hold / stop**: a delayed tool call / a refused one.
- **Login**: one Claude account's token pool; each CLI runs on one.
- **TermHost**: the process that owns a hosted CLI on a ConPTY.
- **SIDEBAR**: `MindAttic.Orchestrator.SIDEBAR`, a second checkout of this repo; it has no charter of its own.
