# Roadmap: Orchestrator as a multi-agent job coordinator

## Context

Today the Orchestrator answers one question: "which login has token room, and which single ticket
should it take?" Every start decision is keyed on a login's budget (`Pace.SubagentAllowance`,
`SubagentShare.Room`, `LoginPicker`, `TicketPins`), and the only "agent profile" is sonnet-or-opus
(`ModelChoice`). There is no job, pipeline, stage, role or agent type anywhere in the code.

The goal is to turn that around: the **job** is the thing being scheduled, **role-configured agents**
that any member can spin up are the workers, pipelines fan a goal out into many units that run at
once, and a login's budget is just one admission constraint among several. It must also (a) never
exhaust disk, RAM, pagefile, handles or processes however much work is queued, and (b) pause and
resume separate units of work cleanly.

Patterns are borrowed from **ECC** (role catalog with `name/description/tools/model` frontmatter,
plan-file handoffs with a Key Files table, reviewers never write, mandatory stop conditions) and
**Paperclip** (goal ancestry on every task, atomic checkout, DB-backed wakeup queue with coalescing,
runtime adapters, pause keeps tasks assigned, budget as a scoped hard stop, durable activity log).
Everything is built natively in C#; nothing third-party is installed.

Decisions already made with the owner: one checkout on `main` stays (no branches/worktrees);
Claude first with a provider-neutral data model; borrow patterns, build native.

## What stays as it is

- **Tickets remain the unit of work and the only work store.** A job is an epic; a unit is a ticket
  `part-of` it; ordering is the existing `requires` links. No second work database. This keeps
  "every code change has a ticket", Tickets Desktop as the only ticket UI, the commit gate, and the
  approval gate (`Snapshot.MayWork`: parts of an unapproved epic don't run) all working unchanged.
- Hard cutoffs only (98% of 5h, 90% of week), API key never automatic, a person's own use wins.
- Agents start only when a member starts them; nothing self-enables in a fresh CLI.
- Verification inside pipelines is build-only; a test stage runs only on the owner's `/test`.
- Overlap is never a reason to hold a unit (file claims order and warn, they don't block).
- No throttles to protect the machine: the only holds are hard exhaustion floors, after reclaim.
- Every number below is a setting (Settings modal + CLI) that self-corrects within owner bounds.

## Target model

| Concept | What it is | Where it lives |
|---|---|---|
| **Role** | An agent's job description: brief, model, effort, tools, which unit kinds/paths it takes, whether it may write code | Source files `mindattic-agent-standard/shared/roles/*.md` (ECC-style frontmatter), rendered per CLI by `render-abilities.ps1`; indexed in `roles` table |
| **Agent** | A role instance a member spun up: owner, runtime adapter, state (`idle/running/paused/terminated`), heartbeat, current unit | New `agents` table in orchestrator.db |
| **Pipeline** | A template: stages, the role each stage needs, fan-out rule, gate per stage | `shared/pipelines/*.json` + `pipelines` table |
| **Job** | One run of a pipeline against a goal; owns an epic ticket | New `jobs` table (epic id, pipeline, state, goal text) |
| **Unit** | One stage's piece of work = one ticket, plus run state | New `job_units` table (ticket id, job, stage, role, state, attempt, lease, checkpoint) |
| **Adapter** | How an agent is actually run | `IAgentRuntime` in Core; first two: Claude subagent (today's `/allocate` handout) and Claude session (`orchestrator org start`) |

Unit states: `pending → ready → leased → running → (pausing → paused) → done | handed-back | failed`.
A lease has an expiry renewed by the agent's heartbeat; an expired lease returns the unit to `ready`
with its last checkpoint.

## Phases

Each phase is its own epic with one ticket per bullet, landed on `main` one ticket per commit.

### Phase 0 — File the roadmap
- Save this roadmap as `docs/AGENTIC_ROADMAP.md`; file one epic per phase below and print each
  ticket draft for approval (non-fix work is never queued unapproved).

### Phase 1 — Roles and agents (the "who")
- Role source format + first catalog: `planner`, `architect`, `implementer`, `reviewer`,
  `build-fixer`, `doc-writer`, `researcher`, `release`; business-side roles (`analyst`, `copywriter`,
  `designer`) as data, not code. Reviewer roles are read-only by tool list.
- `roles` and `agents` tables (append to `Migrations` in `Settings/OrchestratorDb.cs`), mirrored by
  `OrchestratorServer.cs`.
- **Stable agent identity**: an agent id that outlives PIDs, `/clear` and restarts.
  `SessionControl.Start` (`Sessions/SessionControl.cs:24-44`) returns it instead of only an error
  string, and it is stamped on `TermSessionRecord`, `Assignment` and `session_states`.
- Roles reach every login for free: `agents/` is already symlinked from each
  `~/.claude-accounts/<email>` to `~/.claude` (`AccountDirs.cs:102`), so one rendered
  `agents/<role>.md` (brief, tools, model, effort) serves all members.
- `orchestrator agents new|list|pause|resume|retire` (new branch in `Cli/Program.cs`, modelled on
  `branches`); `/agent <role>` ability in the standard.
- **Agent mailbox**: an `agent_messages` table (to-agent, from, unit, body, read) for durable,
  addressed handoffs between roles. Delivered through the existing `CliNudges` file on the next
  prompt, and by the wakeup queue for an idle agent. Replaces model-addressed `SendMessage` for
  pipeline traffic, and works for non-Claude runtimes later.
- `Handout` (`Scheduling/Allocate.cs:548`) and `Assignment` (`OrchestratorSettings.cs:849`) gain
  `role`; `ModelChoice.For` becomes the fallback when a role names no model.
- Dashboard: an Agents panel under each user (role, state, unit, last heartbeat).

### Phase 2 — Jobs and pipelines (the "what")
- `jobs`, `job_units`, `pipelines` tables; `orchestrator jobs start <pipeline> "<goal>"`, `list`,
  `show`, `cancel`.
- Planner stage output is a plan file with a Key Files table; the orchestrator turns it into unit
  tickets (`part-of` the job's epic, `requires` between stages, stage/role labels) through the
  Tickets store, so the whole job is visible in Tickets Desktop.
- Goal ancestry: every unit's handout prompt carries job goal → stage → unit, built in `Briefing.cs`.
- First pipelines: `feature` (plan → implement ×N → review → docs), `bugfix`, `research`,
  `launch` (spec → build → copy → release notes).
- Bounded fan-out: a planner pass creates at most `MaxUnitsPerPlan` units; more arrive as units close.

### Phase 3 — Job-centric scheduler (the "when")
- New `Scheduling/JobScheduler.cs`: ready units × idle agents matched by role, ordered by job
  priority then the existing `GraphWeight`. Login is chosen last, by room, with the existing
  `LoginRoom`/`Redelegation`. Pace becomes one admission check beside RAM, disk and role fit.
- Wire it into the dormant daemon starter (`Allocate.Order`/`Tick`, `Allocate.cs:22,128-198`) and
  into `Allocate.Next` so `/allocate` CLIs and daemon-started sessions draw from the same queue.
- One admission function: the checks now spread over the CLI, the daemon and the generated hooks
  (`Setup/AgentGate.cs` → `pace-gate.py`) read one `Admission` result built from the `Pace` record
  (`Budget/Pacer.cs:21-145`), so a job can see up front which logins can carry its units.
- Atomic checkout: unit lease written under the assignments lock, same pattern as
  `Allocate.cs:848-853`.
- Wakeup queue with coalescing (a `wakeups` state row) replaces ad hoc wakes in `AllocateWaker`.
- Soft file claims: the plan's Key Files feed the commit gate's existing per-path record so disjoint
  units are preferred first; nothing waits because of overlap.
- Stage gates: build-only verify; `DiffReview` generalised into a `reviewer` stage.

### Phase 4 — Resource ownership (never run out)
- **Job Objects**: every hosted CLI's process tree goes in a Windows Job Object with
  kill-on-close (`TermHost/Native.cs`, `TermSession.cs`); daemon-run builds too (`ProcessRunner.cs`).
  This replaces name-list orphan hunting in `ProcessReaper` as the primary guarantee.
- **Resource ledger**: `resources` table (kind, path/pid, owning unit/agent/session, lease expiry).
  Anything the orchestrator creates is registered before it exists and released when its unit ends.
  Each unit gets its own scratch dir handed to the agent as `ORCH_SCRATCH`.
- **Sweeper**: on daemon start and every tick, delete ledger entries whose owner is dead or
  terminal. Extends `SessionTempPruner`; "unknown owner, kept forever" no longer applies to
  orchestrator-owned things. Also sweeps crash leftovers (`orchestrator-review-*`,
  `mindattic-token-check-*`) and stale `bin/obj` in the `main-tests` and branch worktrees.
- **Startup orphan sweep**: persist `ProcessReaper`'s seen-under-agent set so orphans born while the
  daemon was down are reaped.
- **Hard floors as admission**: below the disk floor, reclaim first, then hold new units until
  space returns (today low disk only alerts). RAM levels stay as they are in `MemoryGuard`.
- **Heavy-process lease**: the documented "one game player / engine build / soak per machine" rule
  enforced by a hook pattern list plus a machine-wide row in `leases`.
- **Growth caps**: rotate `disk-cleanup.log`; age-prune and `VACUUM` orchestrator.db; make transcript
  retention a setting; prune closed jobs' unit rows into a compact history table.

### Phase 5 — Pause, resume and recovery
- `orchestrator jobs pause|resume <job|unit|agent|role|all>` and matching dashboard buttons.
- Two modes: **drain** (finish the current unit, take no new one) and **checkpoint** (stop at the
  next tool call via the existing abort mark in `AgentAbort.cs`).
- Checkpoint = a note on the unit's ticket + a `job_units.checkpoint` row: done so far, dirty paths
  (from the commit gate's record), next step. Same shape as a quicksave handoff, so any agent of the
  role can resume from it on any login.
- The checkpoint is structured (unit, agent id, Claude session id, claims, live subagents), unlike
  today's free-prose `.agent/agent-session.json`; `prose-agent.ps1` gains those fields (protocol 1.3).
- **Daemon-driven resume**: a paused or idle agent is woken into its own session
  (`claude --resume <sid>`, the path `AccountMove.cs` and `SessionRestore.cs` already use) rather than
  a fresh `claude -p`; today's wakers (`AllocateWaker`, `LimitWaker`) can only nudge a CLI that is
  already being prompted.
- Lease expiry and CLI death both land on the same path: unit back to `ready` with its checkpoint
  (folds in `OrphanWatch`).
- A login reaching its hard cutoff pauses only that login's agents; their units move to other
  agents of the same role, or wait for the reset using the existing resume-after-reset wake.

### Phase 6 — Governance and visibility
- Per-job and per-role spend from `ticket_costs`; optional job budget as a hard stop that pauses the
  job only.
- Durable activity log of agent/job state changes on the app bus (`Status/AppBus.cs`).
- Jobs view in the dashboard: pipeline as stages, units per stage, agents on them; every unit ID
  links to Tickets Desktop.
- Role and pipeline definitions are versioned files, so a bad change is a revert.

### Phase 7 — Other providers
- `IAgentRuntime` adapters for Codex, Kimi and agy, with a readiness check per adapter; extend the
  per-member login model beyond Claude accounts. Roles and pipelines need no change.

## Verification

- Each ticket: `dotnet build` of the solution, nothing more.
- Phase 2/3 end-to-end: start a `feature` job on a scratch repo with two agents on two logins and
  watch units fan out, claim, land and close in the dashboard and in Tickets Desktop.
- Phase 4: kill a hosted CLI mid-build and confirm its children are gone and its scratch dir is
  deleted on the next tick; restart the daemon with a planted orphan and confirm the startup sweep.
- Phase 5: pause a running job in both modes, restart the daemon, resume, and confirm each unit
  continues from its checkpoint on a different agent.
- The full test suite runs once per phase, only on the owner's `/test`.
