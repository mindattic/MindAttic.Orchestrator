---
layer: release
status: living
---
# MindAttic.Orchestrator — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A Windows daemon, WPF dashboard and CLI that keeps Claude agents working the workspace's tickets around the clock
(`/allocate`) and spends each 5-hour usage window to just under its limit, never over. It is for the owner and the
org's logins on the owner's PC. `MindAttic.Orchestrator.SIDEBAR` is a second checkout of this repo and has no charter
of its own.

## 2. The milestone: MindAttic.Orchestrator 1.0, a week unattended {#milestone}
- **Milestone:** MS-23, "MindAttic.Orchestrator 1.0: a week unattended"
- **Published means:** an internal tool deployed by `run.ps1 -Redeploy` to `R:\MindAttic\Apps\MindAttic.Orchestrator\{Debug,Release}`,
  started at logon by the Scheduler's `Orchestrator.Daemon` job, reached remotely over Tailscale + RDP
  (docs/TAILSCALE.md).
- **The natural stopping point:** the Release build runs seven days with `/allocate` on and nobody at the desk,
  with no crash, no RAM blowup and no lost or reverted landing. Allocation hands out only leaf tasks, the pace hooks
  read state correctly under contention, every tunable number is a Vault setting, and the old `C:\Apps` deploy is
  gone. That is the owner's standing goal (2026-11-11) and the point where the tool can be left alone; everything in
  `docs/AGENTIC_ROADMAP.md` beyond it is a new milestone.

## 3. Done when (acceptance checks)
1. build — `MindAttic.Orchestrator.slnx` builds clean on main.
2. exists — `R:\MindAttic\Apps\MindAttic.Orchestrator\Release` holds the deployed dashboard and daemon, and
   `C:\Apps\MindAttic.Orchestrator` no longer exists.
3. signoff — the owner leaves `/allocate` running for a week, checks in only over Tailscale + RDP, and finds every
   landed ticket intact on main; `orchestrator health --week` (M.ORCH-T2367) shows no unexpected daemon exit and no
   low-RAM cutoff hit over those seven days.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | M.ORCH-T2350 Unattended allocation correctness | only leaf work dispatched, safe landings, robust pace hooks | M.ORCH-T2354 Dispatch only leaf work (990), M.ORCH-T2355 Safe shared-checkout landing (980), M.ORCH-T2356 Pace hook robustness (970) |
| 800 | M.ORCH-T2351 Week-long stability | Vault tunables, no dead dashboard work, week-run evidence | M.ORCH-T2357 Tunables as Vault settings (890), M.ORCH-T2358 Lean dashboard (880), M.ORCH-T2359 Week-run evidence (870) |
| 700 | M.ORCH-T2352 Deploy on R: | deploy on the shared deploy-lib to R:, consumers moved, C: removed | M.ORCH-T2360 R: layout and run menu (790), M.ORCH-T2361 Consumers moved, C: removed (780) |
| 600 | M.ORCH-T2353 Loose ends | Launcher handoff of model and agent id, duplication audit cap | M.ORCH-T2362 Launcher handoff (690), M.ORCH-T2363 Audit accuracy (680) |

Tasks: M.ORCH-T2131 (990); M.ORCH-T2132 (980); M.ORCH-T2347 (970); M.ORCH-T2364, T2365, T2366 (890); M.ORCH-T768
(880); M.ORCH-T2367 (870); M.ORCH-T2250, T2251 (790); M.ORCH-T2252, T2253 (780); M.ORCH-T2058, T2061 (690);
M.ORCH-T2335 (680).

## 5. Out of scope (not before this milestone)
- Hub-and-spoke phases 3-5 (member machines as Relay spokes, TIX replica, central pacing policy) — remote use is
  Tailscale + RDP; docs/research/hub-and-spoke.md stays as research.
- The app bus on MindAttic.Relay — owned by MindAttic.Relay's milestone MS-8 (M.RLY-T82); the named-pipe bus stays.
- Roadmap phases 1-3 and 5-11 in docs/AGENTIC_ROADMAP.md: roles, jobs and pipelines, the job scheduler and wakeup
  queue, pause/resume, governance, other providers, off-host and cloud workers, expert agents, cross-repo jobs.
- Open-ended bughunt sweeps; a confirmed bug is filed as its own task instead.
- Passive "wait for a live occurrence" watches, and team billing administration.

## 6. Current verified state
- Pacing, the stop, `/allocate`, per-CLI logins, lock and move, health probes: in daily use (README).
- Resource ownership (M.ORCH-T1673, all children closed): Job Objects, resource ledger and sweeper, startup orphan
  sweep, disk floor, heavy-process lease, growth caps.
- Low-RAM cutoffs: `Core/Activity/MemoryGuard.cs`; disk floor: `Core/Activity/DiskGuard.cs`.
- Week-long log growth bounded (M.ORCH-T1611, cb9bc66b).
- Pacer tunables are Vault settings (M.ORCH-T2336, d41ae8cf).
- Bughunts of Core and AgentGate landed (M.ORCH-T2312..T2346, e.g. 0345d5c7, 64162dbf, a81bce72).

## 7. Decision record
- 2026-11-27 — charter drafted from README, docs/AGENTIC_ROADMAP.md, docs/HEALTH.md and the open tickets; Legion
  vote: **Approve**, 7 of 9 (plurality, quorum reached). Dissent: one voter thought the out-of-scope list named
  things unrelated to the Orchestrator's core; another asked that M.ORCH-T2131 be kept, which it already is.
- 2026-11-27 — M.ORCH-T1201 and M.ORCH-T1923..T1925 closed as duplicates of the Relay tickets (M.RLY-T82, T44,
  T45), at the Relay triage's request.
- Triage (M.ORCH-T2348): 40 open (39 Orchestrator + 1 SIDEBAR, excluding this job and M.ORCH-T2349) → done 5,
  duplicate 5, wont-do 19, kept 11, new tasks 4 (plus 4 epics and 10 features). M.ORCH-T2349 (MAT-T321) left
  untouched by instruction.
