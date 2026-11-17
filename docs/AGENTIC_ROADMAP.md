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

## The design in one page

ECC and Paperclip are two examples, not the boundary. The design also draws on durable-execution
engines (every state change is an appended event, every step can be retried safely), blackboard
systems (agents coordinate through shared written state, not chat), kanban pull (finish before
starting), and spec-driven methods (goal → spec → plan → tasks, each handed on as a document).

Five layers, each owning one question:

| Layer | Question | Made of |
|---|---|---|
| **Direction** | What are we trying to reach, and are we there? | Goal → Milestone → Job → Unit; acceptance checks; forecast |
| **Workforce** | Who is best placed to do this? | Roles, specialties, expert agents, repo teams, track record |
| **Coordination** | Who does what, when, without colliding? | Scheduler, leases, mailbox, cross-repo contracts, shared lessons |
| **Execution** | Where does it run, and what does it consume? | Workers, runtimes, resource ledger, budgets |
| **Assurance** | Is it actually done and still working? | Build gates, independent review, integration builds, milestone acceptance |

Four rules hold the whole thing together:

1. **Work is pulled toward a milestone, not pushed from a queue.** Priority comes from what the
   active milestone still needs, by its critical path. Other work still runs with spare room; it
   never outranks milestone work except for fixes.
2. **A ticket goes to the agent most likely to land it first time**, by declared specialty and by
   measured record, and is reviewed by a different agent.
3. **Done means evidence on `main`**, never "the ticket was closed": a passing build, a reviewer
   verdict, and for a milestone its acceptance checks all passing at once.
4. **Nothing is held in an agent's head.** Every handoff, decision, checkpoint and lesson is written
   to the ticket or the database, so any agent on any login or machine can carry on.

## What the field does, and what was taken from it

Surveyed 2026-11-16 (about fifty tools and frameworks; star counts and vendor claims not repeated
here). Each row is the one pattern worth having and where it lands in this roadmap.

| Source | Pattern taken | Phase |
|---|---|---|
| ECC | Role files with tools and model; reviewer verdict contract; stall escalation | 1, 3, 5 |
| Paperclip | Goal ancestry; wake queue; independent holds; adapter shape; delete only what is delivered | 2, 3, 4, 5, 7 |
| Claude Code agent teams | Self-claim of unblocked tasks; a hook that can refuse "done" | 3 |
| Claude Code workflows | Deterministic replay of finished steps; automatic wait at a usage limit | 2, 5 |
| Beads / Gas Town | Ready queue with atomic claim; a ledger outside any agent's context; merge queue that bisects to the breaking change; watchdog roles with an escalation chain | 2, 3, 9 |
| OpenAI Symphony | A proof-of-work bundle before work lands | 3 |
| Task Master | A complexity score decides how far a ticket is split | 10 |
| Kiro | Waves: concurrent inside a wave, ordered between waves | 3 |
| Spec Kit, BMAD, gstack, Superpowers | Spec → plan → tasks, each a document; a standing constitution; review by role; two-stage review (does it meet the spec, then is it good) | 2, 3 |
| Devin | Per-child budget with pause; the coordinator is woken when a child finishes | 3, 6 |
| Factory droids | Light/medium/heavy tiers routed to different models | 7 |
| Amp, Roo orchestrator | A dispatcher that holds no workspace; instructions go down, only a summary comes up | 2 |
| Aider, Anthropic's advisor pattern | Strong model plans or advises, cheaper model edits | 7 |
| Magentic-One (Microsoft Agent Framework) | Task ledger (facts, guesses, plan) and progress ledger; replan when progress stalls | 5 |
| MetaGPT, Pydantic AI | Typed documents as handoffs, checked against a schema, not chat | 2 |
| LangGraph, Mastra | Checkpoint at every step boundary; typed input required to resume | 5 |
| Temporal, Restate, DBOS | Every step journaled and safe to repeat; only the in-flight step re-runs | 2 |
| CrewAI, Google ADK | Fixed pipeline outside, model freedom only inside a step | 2 |
| A2A | Standard task states including "input required" | 7 |
| Multica, Copilot Agent HQ, Warp Oz | Agents as assignees in the tracker; several vendors' agents under one policy | 7, 10 |
| Claude Code Game Studios | Directors, leads and specialists, with a named arbiter per domain | 2, 10 |
| Cursor, Codex, Jules cloud agents | A cloud machine per task; published concurrency and spend limits | 8 |

**Where this design deliberately differs.** Nearly every tool isolates each unit in its own branch,
worktree or cloud machine and merges through a pull request. Here the rule is one checkout on
`main` per machine. That removes merge queues and stale branches, and costs something real: two
agents can edit the same file at once. The design pays for it with file claims, per-machine
clones (Phase 8), a reviewer on every unit, and an integration build that bisects to the unit that
broke `main` (Phase 9).

**What the evidence says, and the rules that follow from it.**

- Many agents win on broad work that splits cleanly, and lose on work that is one chain of
  reasoning. Published studies found sequential tasks got 39–70% worse under every multi-agent
  setup, and parallel agents that each make their own choices multiply errors about four times
  more than a centrally directed team. **Rule: parallelism is across units, never inside one. One
  writer per unit; the orchestrator, not the agents, decides who does what.**
- Agents working in parallel make conflicting unstated decisions. **Rule: decisions that span
  units are written down first** (the plan, the contract unit, the constitution) and every brief
  carries them.
- A study of 1,642 failed multi-agent runs put 44% down to system design, 32% to agents
  misunderstanding each other and 24% to missing verification; adding one verification step raised
  success by about 16%. A reviewer that shares no context with the author catches the most.
  **Rule: every unit gets a clean-context review, and every job a final integration check.**
- A multi-agent run uses roughly 15 times the tokens of a chat. **Rule: a ticket small enough for
  one agent stays one unit.** Triage decides by complexity score; fan-out is for work that needs it.
- Running several models as a council has no evidence behind it for coding; a second opinion from
  a *different vendor* does. **Rule: no councils; cross-vendor review where a second vendor exists.**

## Target model

| Concept | What it is | Where it lives |
|---|---|---|
| **Role** | An agent's job description: brief, model, effort, tools, which unit kinds/paths it takes, whether it may write code | Source files `mindattic-agent-standard/shared/roles/*.md` (ECC-style frontmatter), rendered per CLI by `render-abilities.ps1`; indexed in `roles` table |
| **Agent** | A role instance a member spun up: owner, runtime adapter, state (`idle/running/paused/terminated`), heartbeat, current unit | New `agents` table in orchestrator.db |
| **Pipeline** | A template: stages, the role each stage needs, fan-out rule, gate per stage | `shared/pipelines/*.json` + `pipelines` table |
| **Job** | One run of a pipeline against a goal; owns an epic ticket | New `jobs` table (epic id, pipeline, state, goal text) |
| **Unit** | One stage's piece of work = one ticket, plus run state | New `job_units` table (ticket id, job, stage, role, state, attempt, lease, checkpoint) |
| **Goal** | A lasting aim ("ship the Unity games") | Tickets store |
| **Milestone** | A checkable point on the way to a goal, across one or more repos, with acceptance checks | New in the Tickets store (MindAttic.Tickets), shown in Tickets Desktop |
| **Specialty** | What an expert is good at: repos, `component:` labels, stack tags, paths | Role files + learned `expert_record` table |
| **Lesson** | One thing learned on a unit that would have saved time, scoped to a repo or component | `lessons` table, fed into briefs |
| **Worker** | A machine that runs agents and builds | `workers` table (Phase 8) |
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
- **Handoffs are typed documents**: spec, plan, contract, review and checkpoint each have a schema,
  and a stage can't start on a document that fails it. Agents never hand off by chat.
- **Constitution**: each repo's standing rules (its `AGENTS.md`, bible and golden rules) are one
  document every brief carries, so parallel units don't make conflicting choices.
- **The coordinator holds no workspace**: it plans, routes and reads summaries; only unit agents
  touch files. Instructions go down, a summary and the proof bundle come up.
- A `game` pipeline for the Unity projects: directors (design, art, tech) set direction, leads
  split it per discipline, specialists build; each domain has one named arbiter for disputes.
- First pipelines: `feature` (plan → implement ×N → review → docs), `bugfix`, `research`,
  `launch` (spec → build → copy → release notes).
- **Unit event log**: every state change is an appended `unit_events` row (who, when, from, to,
  why); `job_units` state is derived from it and each transition is safe to apply twice. This is
  what makes recovery, audit and "what happened to this unit" exact.
- **Failure ladder**: a unit that is handed back goes to the same expert once more, then a stronger
  model, then a different expert, then the owner. A unit that exhausts the ladder is quarantined
  with its history, not retried forever. The attempt counts are settings.
- `orchestrator jobs start --dry-run` prints the fan-out, the experts it would use and a token
  estimate (from `tickets ticket estimate` and `TicketCosts`) before anything is filed.
- Bounded fan-out: a planner pass creates at most `MaxUnitsPerPlan` units; more arrive as units close.

### Phase 3 — Job-centric scheduler (the "when")
- New `Scheduling/JobScheduler.cs`: ready units × idle agents matched by role, ordered by job
  priority then the existing `GraphWeight`. Login is chosen last, by room, with the existing
  `LoginRoom`/`Redelegation`. Pace becomes one admission check beside RAM, disk and role fit.
- **CLIs are a managed pool, not one-off instances.** Today a person opens a CLI on one login,
  turns `/allocate` on, and that CLI pulls for itself. In the pool model a member says how much of
  their login the pool may use; the orchestrator then decides how many CLIs each login runs, what
  role each has and which unit it takes, opens and closes them as the queue needs, and fills each
  login up to its cutoff. A person's own CLI is never taken over, and their own use still wins.
- **Capacity pool**: every login's remaining 5-hour and weekly room, per org, is one figure the
  scheduler plans against: what the pool can carry now, when each login resets, and which units
  fit where. A unit that doesn't fit any one login's remaining room is planned for the reset that
  will carry it, not started and stranded.
- **Dashboard**: the Users → CLIs view stays exactly as it is, with each CLI row gaining its role
  and current unit. Beside it, a **Work** view runs the other way: milestone → job → unit → the
  agent, login and machine carrying it. Above both, a pool bar: total room across all logins, the
  burn rate, the next resets, and what is queued against them.
- Wire it into the dormant daemon starter (`Allocate.Order`/`Tick`, `Allocate.cs:22,128-198`) and
  into `Allocate.Next` so `/allocate` CLIs and daemon-started sessions draw from the same queue.
- One admission function: the checks now spread over the CLI, the daemon and the generated hooks
  (`Setup/AgentGate.cs` → `pace-gate.py`) read one `Admission` result built from the `Pace` record
  (`Budget/Pacer.cs:21-145`), so a job can see up front which logins can carry its units.
- Atomic checkout: unit lease written under the assignments lock, same pattern as
  `Allocate.cs:848-853`.
- Start order when a slot frees (from Paperclip's run queue): work a person asked for directly,
  then other units, then recovered units; within a tier, in-progress first, then priority, then
  age. Every Nth start goes to the unit that has waited longest, so nothing starves under a deep
  queue. N and the waiting age are settings.
- Wakeup queue with coalescing (a `wakeups` state row) replaces ad hoc wakes in `AllocateWaker`.
- Soft file claims: the plan's Key Files feed the commit gate's existing per-path record so disjoint
  units are preferred first; nothing waits because of overlap.
- **Waves**: units with no unmet prerequisite form a wave and run together; the next wave starts as
  its prerequisites close.
- **Proof before close**: a unit closes only with its bundle attached to the ticket: the commit on
  `origin/main`, the build result, the reviewer's verdict, and what was checked by hand if
  anything. The commit gate's existing close check refuses a close without it.
- **Two-stage review**: first "does it do what the unit's spec says", then "is it good". A unit
  that fails the first never reaches the second.
- **Per-unit budget**: each unit carries a token ceiling from its estimate; passing it pauses that
  unit for the failure ladder, not the whole job.
- Stage gates: build-only verify; `DiffReview` generalised into a `reviewer` stage.
- Reviewer contract (from ECC's `code-reviewer`): a verdict of `approve`, `warning` or `block`; each
  blocking finding names the exact line and the failure scenario; only findings the reviewer is
  confident in are reported, and zero findings is a valid result. A `block` hands the unit back to
  an implementer with the findings as its brief. Planner and reviewer roles get read-only tools.

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
- **Delete only what is proven delivered** (from Paperclip's workspace reaper): a unit's scratch is
  removed once its commit is on `origin/main`, or once it was handed back with a checkpoint; anything
  else waits out a retention window. Each new sweep ships with a dry-run mode that logs what it
  would delete, and every retention window is a setting.
- **Startup orphan sweep**: persist `ProcessReaper`'s seen-under-agent set so orphans born while the
  daemon was down are reaped.
- **Hard floors as admission**: below the disk floor, reclaim first, then hold new units until
  space returns (the disk floor is `DiskGuard`, M.ORCH-T1679: `settings --disk-hold-gb`). RAM levels stay as they are in
  `MemoryGuard`.
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
- Holds are independent (from Paperclip's agent lifecycle): a manual pause, a budget stop and a
  resource floor are tracked separately, so resuming by hand never clears a budget or resource hold.
  Agents pass through `pausing` and `resuming` so the dashboard shows a request that hasn't landed.
- Restart report: when the daemon restarts it lists every unit that was running as adopted (agent
  still alive), finished while down, or lost. Lost units go back to `ready` with their checkpoint
  and are named in the log and dashboard; nothing is dropped silently.
- Stopped-job check (from Paperclip's task watchdog): a job with open units but nothing running,
  queued or waking gets one review unit, keyed by a fingerprint of the stopped state so the same
  stop is never reviewed twice.
- Stall rules (from ECC's `loop-operator`), built on the existing `LoopDetector` and `Watchdog`: a
  unit is paused and escalated to the owner when it shows no progress across two checkpoints, fails
  twice with the same error, or drifts past its job's budget. It resumes only after its build
  passes. The counts are settings.
- A login reaching its hard cutoff pauses only that login's agents; their units move to other
  agents of the same role, or wait for the reset using the existing resume-after-reset wake.

### Phase 6 — Governance and visibility
- Per-job and per-role spend from `ticket_costs`; optional job budget as a hard stop that pauses the
  job only.
- Durable activity log of agent/job state changes on the app bus (`Status/AppBus.cs`).
- Jobs view in the dashboard: pipeline as stages, units per stage, agents on them; every unit ID
  links to Tickets Desktop.
- Role and pipeline definitions are versioned files, so a bad change is a revert.

### Phase 7 — Many models, each doing what it does best
- **Capability table as data**: for each kind of work (planning, large implementation, review,
  long reading, UI, bulk edits, image and asset generation) the models allowed and preferred, with
  price. Shipped with a starting set and corrected by this workspace's own record, never by a
  benchmark number hard-coded in C#. Model names and prices change monthly; the table is a setting.
- **Record per model**: the Phase 10 track record is kept per model as well as per expert, so
  routing learns which model lands which component first time, at what cost.
- **Three routing patterns, in order of evidence**:
  1. *Planner and editor*: a strong model plans and advises, a cheaper one writes.
  2. *Escalate on failure*: the failure ladder's "stronger model" step.
  3. *Cross-vendor review*: the reviewer runs on a different vendor from the author when one is
     signed in.
- **Tiers**: triage marks each unit light, medium or heavy; light units go to the cheapest model
  allowed for that kind of work.
- **Runtimes drive each vendor's own unmodified CLI under each person's own sign-in** (Claude Code,
  Codex, Kimi, agy). The Orchestrator starts and watches them; it does not sit between a vendor and
  its login. Work that can't be done that way (image generation, a model with no CLI) uses an API
  key from the Vault, on the owner's say-so only.
- **Check each vendor's plan terms before turning a runtime on.** As read on 2026-11-16: Anthropic
  allows a person's own sign-in to the unmodified Claude Code and says plan limits assume ordinary
  individual use; OpenAI allows plan sign-in for approved partner tools only; Google treats plan
  sign-in through third-party tools as a breach and recommends API keys. Each runtime carries a
  `TestEnvironment` note of the terms it was checked against and the date.
- Units report in A2A's task states (`submitted`, `working`, `input-required`, `completed`) so a
  runtime that needs a person shows up in the owner inbox instead of hanging.
- `IAgentRuntime` follows Paperclip's adapter shape: `Execute` (returns usage, cost and session
  state), `TestEnvironment` (info/warn/error checks, each marked retryable or not) and a session
  codec that saves and restores the runtime's session between wakes, with a way to drop a session
  that can no longer be resumed.
- `IAgentRuntime` adapters for Codex, Kimi and agy, with a readiness check per adapter; extend the
  per-member login model beyond Claude accounts. Roles and pipelines need no change.

### Phase 8 — Off-host workers (move the load, don't just survive it)

Phases 4 and 5 stop the host PC from falling over; this phase takes the load off it. Today
`docs/REMOTE_HOST.md` can move *everything* to one other machine (`host`/`client`/`standalone`), but
that is one machine, always on, and all or nothing. It needs Phase 1 (agent identity), Phase 3
(leases in the database) and Phase 5 (checkpoints); 8a can start as soon as those land.

**8a — Many workers instead of one host**
- Split the control plane (daemon, databases, dashboard: light) from **workers** (any machine that
  runs CLIs and builds: this PC, the existing host VM, a spare PC, a cloud VM).
- `workers` table: name, kind (`local`/`owned`/`cloud`), capabilities (`windows`, `gpu`, `unity`,
  `unreal`, `dotnet`, ...), measured RAM and cores, cost per hour, state
  (`off/starting/ready/draining/stopped`), heartbeat.
- A worker runs the pieces that already exist: TermHost, the gateway (TCP 7443, pinned certificate,
  member keys) and the pace hooks. `setup-host.ps1` and the host kit become `setup-worker.ps1`.
  Reached over Tailscale (`docs/TAILSCALE.md`).
- Placement in `JobScheduler`: capability first, then cost. Planning, review, docs and research
  units need no Windows toolchain; engine builds and game players need a labelled worker.
- A setting says what this PC takes: nothing, light units only, or everything. Set to nothing, the
  PC is only the dashboard.
- Code still lands on `main`: each worker has its own clone, rebases onto `origin/main` and pushes
  one ticket per commit, as the host does today. The one-checkout rule holds per machine. Soft file
  claims move from the commit gate's per-machine files into the control plane's database so
  workers see each other's.
- Main-line tests and `/test` run on a worker, not the PC.

**8b — Where the workers come from, cheapest first**
1. **Claude cloud sessions**: the agent runs in Anthropic's own sandbox on the same subscription, so
   there is no compute bill at all. A `claude_cloud` runtime for light units on repos that build on
   Linux. Needs a spike first: Windows-only targets and `C:\LocalNuGet` packages won't build there.
2. **Owned hardware**: a spare PC or Hyper-V VM as an always-on worker. No marginal cost.
3. **Rented, flat rate**: a dedicated server at a fixed monthly price. Cheapest per hour when it is
   busy most of the day.
4. **Rented, by the hour**: Azure Spot VMs that run only while there is queue. An 8-core, 32 GB
   `D8as_v5` listed at about $0.34/hour on demand and about $0.07/hour spot in November 2026
   (likely Linux rates; Windows costs more). Eight hours a day on spot is roughly $17 a month plus
   the disk, against roughly $250 always on. The price table is a setting, refreshed from Azure's
   published prices, never hard-coded.

**8c — Elastic cloud workers that scale to zero**
- A small fixed pool of named VMs that are started and deallocated, not created from an image each
  time: Claude tokens and Vault keys are sealed with DPAPI per machine and user, and a deallocated
  VM keeps its disk and so its sign-ins.
- Start a cloud worker when ready units it could take have waited longer than a set time and no
  owned worker has room. Drain and deallocate it after a set idle time. A deallocated VM costs only
  its disk.
- Spot eviction uses the Phase 5 path: on Azure's eviction notice the worker checkpoints; with the
  deallocate eviction policy the disk survives, so the unit resumes there when the VM returns, or
  is restarted elsewhere from `origin/main` after its lease expires.

**8d — Cloud spend can't run away**
- A monthly cloud budget is a hard cutoff like the token cutoffs: at the limit every cloud worker
  drains and deallocates and work falls back to owned workers. Never raised automatically.
- An estimate is reserved before a worker starts; cost shows per job and per worker in the dashboard.
- Two independent guards: the daemon deallocates any cloud worker holding no lease for a set time,
  and each VM carries an Azure-side auto-shutdown and budget alert, so a dead control plane can't
  leave machines running.

### Phase 9 — Goals and milestones (reaching something, not just closing tickets)

The ticket store has epics, stories, priorities, estimates and `requires` links, but nothing that
says "this set of work, across these repos, adds up to a finished thing". That is why work can
crank for weeks without arriving. Most of this phase needs only today's queue, so it can start early.

- **Milestones in the Tickets store** (MindAttic.Tickets' own tickets): name, goal, repos, order,
  state (`planned/active/accepting/reached/dropped`), acceptance checks. Epics and tickets link to
  one with `part-of`. `tickets milestone new|list|show|activate|accept`; a Milestones view in
  Tickets Desktop.
- **Acceptance checks are evidence, not ticket counts.** Each is one of: a build that passes on
  `main` in named repos; a file or package that exists; a harness run that reaches a named state;
  the owner's sign-off. Build and existence checks run on their own. Test, soak and play checks run
  only when the owner runs `orchestrator milestone accept`, which is the owner asking for them.
- **Milestone-first ordering** in `Scheduler.Plan`: fixes, then units on an active milestone by
  critical path (the longest remaining `requires` chain, from the existing `GraphWeight`), then
  everything else. Ordering only: spare room always takes other work.
- **Scope lock**: once a milestone is active, adding units to it needs the owner's approval, and the
  dashboard shows how much it has grown since activation. Everything else asked for meanwhile is
  still filed as a ticket, off the milestone.
- **Milestone steward** role: on a schedule it compares each acceptance check with what is on
  `main` and proposes the missing units (integration, packaging, polish, the parts nobody filed).
  It also reads `tickets ticket hygiene` and reopens closed tickets that aren't done.
- **Integration unit**: every job ends with one that builds the whole result on `main` in every
  repo it touched. A job isn't done until it passes.
- **Bisect to the breaker**: when an integration or main-line build goes red, the commits since the
  last green build are bisected to the unit that broke it; that unit is reopened with the failure
  as its brief, and everything else carries on.
- **Forecast**: remaining estimate against measured throughput (`tickets ticket stats`) gives each
  milestone a projected date and token cost, and an at-risk flag when the projection slips.
- **Owner inbox**: approvals, escalations, quarantined units and milestone acceptances in one
  dashboard list, with a Psst alert for each new one.

### Phase 10 — Expert agents (the right ticket to the right agent)

- **Specialty on top of role**: an expert is a role plus a specialty, e.g. `implementer` for Unity
  gameplay, WPF dashboards, Python hooks, SQL, or s&box. Declared in the role file as repos,
  `component:` labels, stack tags and path globs.
- **Knowledge pack**: each expert's brief carries its repo's `AGENTS.md` and bible, the component's
  docs, and the lessons recorded for that repo and component, within a size limit.
- **Lessons**: at close, each unit records one line on what would have saved time, scoped to its
  repo and component. A lesson seen in two or more repos is promoted workspace-wide. Stored in
  `lessons`; no background observer process.
- **Triage role**: every new ticket gets a component, type, estimate, a complexity score and likely
  prerequisites (`tickets ticket suggest-links`) before it is routed. The score decides the
  treatment: a simple ticket stays one unit for one agent; only a complex one is split or becomes a
  job.
- **Ledgers for long units**: an expert on a heavy unit keeps a task ledger (facts, open guesses,
  plan) and a progress ledger on the ticket. When the progress ledger stops moving, the unit is
  replanned by a planner, not retried as it was.
- **Routing score** per unit and expert: declared match (repo, component, type, the plan's key
  files) plus track record for that component: first-pass rate (closed with no hand-back and no
  reviewer block), build failures, cost against estimate, later reopen. The weights are settings
  that correct themselves from results. `tickets ticket assign` and `divvy` stay as the manual
  override.
- **Model and effort follow the record**: a specialty that keeps bouncing on sonnet moves to opus
  for that component; one that lands cleanly moves down. Replaces keyword matching in `ModelChoice`.
- **Independent review**: a unit's reviewer is never its implementer, and where possible is on a
  different login.
- **Ask an expert**: an agent that hits another component's territory messages that component's
  expert through the mailbox, with a deadline, instead of guessing.
- **Coverage view**: components with no expert, and experts with a poor record, listed in the
  dashboard.

### Phase 11 — Jobs across repos

- **One job, many repos**: the job's epic lives in its lead repo; each repo's share is its own unit
  tickets in that repo's log, linked `part-of` and `requires` across repos.
- **Repo graph**: which repo consumes which (project and package references, `C:\LocalNuGet`),
  built by scanning the workspace. It orders units (library first, then its consumers) and names
  the repos an integration build must cover.
- **Contract first**: a change that crosses a repo boundary starts with a contract unit (the API,
  package version or schema both sides will build against). The provider's unit lands and publishes
  before the consumers' units become ready.
- **Repo teams**: each repo gets standing experts spun up on demand, and a repo lead that owns its
  triage and its integration build. A unit is always worked by an agent whose session is in that
  repo, so "a repo's window works only that repo" still holds; crossing repos is done by routing,
  not by one CLI reaching across. `/crossrepo` stays the explicit exception.
- **Cross-repo integration gate**: after units land in several repos, each dependent repo is built
  against the new packages. A failure files a fix ticket in the repo that broke.
- **Workspace board**: every active milestone and job across all repos in one dashboard view.

## Build order

The phases are numbered by topic, not by order. Recommended order, with each row runnable in
parallel:

| Step | Phases | Why now |
|---|---|---|
| 1 | 4 (resource ownership), 9 (milestones, ordering, steward) | Both work on today's queue and fix today's two biggest problems: the host falling over, and work not arriving |
| 2 | 1 (roles, agent identity, mailbox), 10 (triage, declared specialties) | The workforce the rest depends on |
| 3 | 2 (jobs, event log, failure ladder), 3 (job scheduler) | Many units at once, routed by expertise |
| 4 | 5 (pause/resume), 8a (many workers) | Load moves off the PC; work survives anything |
| 5 | 11 (cross-repo jobs), 10 (learned routing) | Needs real job history to learn from |
| 6 | 6 (governance), 8b–8d (cloud), 7 (other providers) | Scale and cost |

## Verification

- Each ticket: `dotnet build` of the solution, nothing more.
- Phase 2/3 end-to-end: start a `feature` job on a scratch repo with two agents on two logins and
  watch units fan out, claim, land and close in the dashboard and in Tickets Desktop.
- Phase 4: kill a hosted CLI mid-build and confirm its children are gone and its scratch dir is
  deleted on the next tick; restart the daemon with a planted orphan and confirm the startup sweep.
- Phase 5: pause a running job in both modes, restart the daemon, resume, and confirm each unit
  continues from its checkpoint on a different agent.
- Phase 8: set this PC to take nothing, start a job, and confirm its units run on a second worker
  and land on `main` while the PC's RAM stays flat; let a cloud worker go idle and confirm it
  deallocates; set the cloud budget below current spend and confirm every cloud worker drains.
- Phase 9: activate a milestone with one unmet build check, confirm its units jump the queue, the
  steward proposes the missing unit, and the milestone only reads `reached` once every check passes.
- Phase 10: file tickets in three components and confirm each goes to its declared expert, and
  that a unit handed back twice climbs the failure ladder.
- Phase 11: run a job that changes MindAttic.Helpers and one consumer, and confirm the consumer's
  unit waits for the published package and the integration gate builds both.
- The full test suite runs once per phase, only on the owner's `/test`.
