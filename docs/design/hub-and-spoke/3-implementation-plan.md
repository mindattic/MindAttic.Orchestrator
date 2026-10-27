# Implementation plan: hub and spoke (M.ORCH-T1127)

The design is in `2-plan.md`.

**Ground rules for every phase:**

- `Hub.Mode = Off` is the default, and in `Off` behaviour is byte-for-byte today's.
- Each phase ships on its own. A phase may sit dark behind the setting.
- No phase removes the peer-to-peer gateway (`gateway remote add`). It keeps working beside the hub until a later,
  separate decision.
- Effort is focused engineering time for one agent-assisted developer.

---

## Phase 0: seams and contracts (no behaviour change)

**Goal.** Put an interface wherever the hub will later decide, with a Local implementation that is today's code.

**Code:**

- New `src/MindAttic.Orchestrator.Hub.Contracts` (net10.0, no dependencies, like `Term`):
  - `HubProtocol` (`Version`, `Supported`, `Negotiate`, the same shape as `TermProtocol`);
  - `HubEnvelope` (`kind`, `v`, `seq`, `ack`, `at`, `payload`: `StatusEnvelope` plus sequencing);
  - payload records: `SpokeHello`, `HubWelcome`, `Heartbeat`, `LeaseGrant`, `LeaseRenewal`, `PolicySnapshot`,
    `OpenStream`, `SessionInfo`;
  - `MeterReading` moves here, or is mirrored here, from `Status/StatusStream.cs`.
- Core seams (`Core/Hub/`), each with a `Local*` implementation:

| Seam | Today's code |
|---|---|
| `IClaims` | `Allocate.Next`, `Dispatcher` and `DispatchLease`, as they run today |
| `IPolicySource` | Settings, `UsageCutoffs`, `WeeklyAllowances`, `OrgRouting.Load` |
| `IMeterSink` | A no-op |
| `IOrgDirectory` | `MemberRoster`, `Organizations` |
| `ISessionDirectory` | `TermSessions` plus `TeamWall`'s gateway merge |

- Wiring:
  - `NextCommand` (`Cli/PlanningCommands.cs`) calls `IClaims`;
  - `AccountPacer` reads `IPolicySource`;
  - `TeamWallWindow` reads `ISessionDirectory`.
- `OrchestratorSettings.Hub`: `Mode` (`Off|Host|Join`), `Url`, `Pin`, `DeviceId`, `OfflineGraceMinutes` (120),
  `AcceptDispatch` (false). It is shown in Settings as read-only "Off" for now.

**Protocol.** Contracts only, nothing on the wire.

**Migration.** None, beyond an absent `Hub` settings block meaning `Off`.

**Tests:**

- The full existing suite passes untouched.
- Contract round-trip tests (JSON, unknown kind skipped, newer `v` skipped).
- A test that `Off` builds only `Local*` seams.

**Risks.** Seams drawn in the wrong place. Mitigate by drawing them exactly at today's function boundaries and not
refactoring the logic behind them.

**Effort.** 4 to 6 days.

## Phase 1: hub service, member sign-in, device enrollment, control link (dark)

**Goal.** A spoke can enroll with a hub and hold a live, authenticated, reconnecting control socket. Nothing is decided
remotely yet.

**Code:**

- New `src/MindAttic.Orchestrator.Hub` (ASP.NET Core, Kestrel):
  - TLS from `GatewayCertificates`, moved to a shared helper or referenced;
  - hosts MindAttic.Authentication (`AddMindAtticAuthentication` over a `HubDbContext` with the auth schema; pepper and
    KEK from `MindAttic:Vault:Security:*` on the hub machine);
  - endpoints `/health`, `/enroll/start`, `/enroll/complete`, `/device/token` and `/spoke` (WebSocket);
  - a `devices(id, member, name, public_key, enrolled_utc, last_seen_utc, version, revoked_utc)` table;
  - presence in memory, with `last_seen` persisted.
- Core `Hub/HubLink.cs`, the daemon's client:
  - the device key in CNG (`CngKey`, `AllowExport = None`, Microsoft Software or Platform KSP);
  - a challenge signature, then a token;
  - the socket with backoff and jitter (0.5 s to 60 s);
  - a heartbeat on each daemon tick;
  - a `hub_outbox` table in `orchestrator.db` (Volatile, so it never lands in the git dump).
- `Daemon`: `HubLinkSupervisor`, shaped like `GatewaySupervisor`, starts and stops the link to match `Hub.Mode`.
- A `HubSupervisor` runs the hub process in `Host` mode. It is separate from the daemon, so a daemon redeploy never
  drops the hub.
- CLI `Cli/HubCommands.cs`:
  - `orchestrator hub host [--bind --port]`;
  - `orchestrator hub join <url>` (opens the enrollment page; the pin is learned, not typed);
  - `orchestrator hub status`, `orchestrator hub leave`;
  - `orchestrator hub devices [--revoke <id>]`.
- Dashboard: a hub status chip (Off, Connected, Offline since…).

**Protocol.** Control protocol v1: `hello` → `welcome`, then `heartbeat` and `ack`, then `bye`. The hub refuses
`protocol < N-1` with `update-required`.

**Migration.**

- New hub DB: SQLite by default, or SQL Server through the `DATA_STORES.md` pattern with `hub db migrate`.
- On the spoke: an `orchestrator.db` migration adds `hub_outbox`.

**Tests:**

- Hub integration tests with `WebApplicationFactory`: enroll, token, connect, revoke mid-session (the socket closes
  with 1008), a version refusal.
- `HubLink` reconnect against a hub that is killed and restarted.
- With `Off`, no socket and no process are created (asserted).

**Risks.**

- MindAttic.Authentication assumes a browser app, but it is browser-based here too (enrollment).
- CNG keys on machines without a TPM fall back to the software KSP. That is fine.
- Hosting the hub on a member's desktop means it sleeps, so document that the hub machine should not sleep.

**Effort.** 2 to 3 weeks.

## Phase 2: presence and meters (read-only org view)

**Goal.** From any joined machine, see every member, their machines, their CLIs and their meters. No remote control.

**Code:**

- Each spoke heartbeat carries:
  - `CliRoster` rows (pid, repo, login, `/allocate` on or off);
  - `TermSessionRecord`s with titles masked through `SecretMasker` (no command line, cwd, config dir or pipe, as
    `/sessions` already leaves them out);
  - `MeterReading`s;
  - `AgentBoard` summaries (ticket and phase).
- The hub merges per login and stores `usage_samples` and `pace_ledger` rows tagged with the device.
- Hub endpoints `GET /org/presence` and `GET /org/usage`.
- `IOrgDirectory` and `ISessionDirectory` get Hub implementations in `Join` and `Host`.
- Dashboard: remote people's tabs show read-only, badged with their machine. `/org usage` (`Cli/OrgCommands.cs`) prints
  every member's table across machines.
- The hub pushes members and orgs as the existing `StateBus` kinds (`members`, `orgs`).
- Hub-to-spoke login mapping: a report for a login that isn't in the member's `org_members` rows is dropped and audited.

**Protocol.** Heartbeat payload v1. Meter readings collapse to the latest per login in the outbox.

**Migration.**

- `members`, `organizations` and `org_members` are copied once into the hub DB from the host machine's
  `orchestrator.db` (`hub import-org`).
- In `Join`, a spoke's local tables become a cache.

**Tests:**

- Merge rules: the freshest reading wins; the highest within a window; a stale reading is ignored.
- An unmapped login is dropped.
- An outbox replay after an offline period has no duplicates (idempotency id).
- The dashboard view model shows a remote person read-only.

**Risks.** The roster drifting between hub and spoke. The hub is the store of record, and the spoke tables are
overwritten from `welcome` and the deltas.

**Effort.** ~2 weeks.

## Phase 3: terminal streams through the hub (Team Wall at org scale)

**Goal.** Watch any shared CLI in the org from any joined machine, with no inbound port on spokes. Interact works with a
grant and owner consent.

**Code:**

- Gateway outbound mode:
  - `Gateway/HubDialer.cs` receives `open-stream` (by way of the daemon's `HubLink`, or the gateway's own control
    socket; decide in the phase) and dials `wss://hub/streams/attach/{streamId}`;
  - it wraps that socket in the existing `TermBridge` and `StreamMasker` path, unchanged.
- Hub `StreamRelay`:
  - matches the viewer socket to the spoke socket by `streamId` (a 10 s rendezvous timeout);
  - splices binary messages without re-encoding;
  - enforces the level (it drops Input, Resize and TakeSize for watchers, as `TermBridge` does);
  - rechecks access every 10 s and closes with 1008 on revoke.
- Grants move to the hub:
  - `term_grants` and `term_audit` live in the hub DB;
  - `orchestrator gateway grant|revoke|grants|audit` call the hub when joined;
  - the spoke's `acl.json` (`TermGatewayAcl`) is written from the hub's pushes, so the gateway's own check stays in
    place as defence in depth;
  - each `open-stream` carries the hub's signature over `{streamId, session, viewer, level, expires}`, which the spoke
    verifies with the hub key it pinned at enrollment.
- Owner consent for interact:
  - on the first use of each grant, the spoke's dashboard toasts "<viewer> wants to type into <tab>: Allow for <grant
    span> / Deny", with the answer kept for that grant;
  - with no answer in 30 s, the viewer gets watch only.
- Compression: offered to watchers only. Interactive remote streams negotiate no deflate (the CRIME/BREACH note in
  T934 and T935).
- Team Wall: `ISessionDirectory` (Hub) lists `/org/sessions`. `GatewayClient` gains a hub base URL, and the tiles and
  TermViewer `--remote` get a `--hub` form. Peer remotes are still listed beside them.

**Protocol.**

- Control: `open-stream` and `close-stream`.
- Stream sockets: TermHost frames unchanged (TermProtocol 1 and 2), one frame per message.
- The hub never synthesizes frames, except `Paused`, which already comes only from the gateway.

**Migration.** `orchestrator hub import-grants` copies the host's `term_grants` once. Existing peer-to-peer tokens are
untouched.

**Tests:**

- Relay splice end to end in-process: hub, a fake spoke and a stub TermHost from the `--unlisted` smoke pattern.
- A watcher's input dropped at the hub *and* at the spoke.
- An unshared session answers 404 through the hub.
- A pause mid-stream gives a `Paused` frame.
- A revoke closes within 10 s.
- A consent timeout falls back to watch.
- A wrong signature on `open-stream` is refused by the spoke.
- No byte of a fake `sk-ant-` token reaches the viewer (the T935 smoke test, through the hub).

**Risks.**

- Latency through the relay: plus one RTT. For typing, that is fine under ~100 ms.
- Hub bandwidth: about 36 KB/s for 12 working CLIs, with bursts to ~360 KB/s (the spike's estimates), which is
  trivial.
- A later "direct when reachable" optimization (the Tailscale-style peer path) stays possible, because the peer gateway
  still exists.

**Effort.** 2 to 3 weeks.

## Phase 4: shared tickets and leases

**Goal.** Every joined spoke works the same queue with no double claims. Tickets Desktop and the hooks keep working from
a local replica.

**Code:**

- Hub Tickets API over `TicketsDb`:
  - `GET /tickets?since=<cursor>` (the replica feed);
  - `POST /tickets/{id}/notes|status|assign|approve`;
  - authorized by role and org.
- The hub's `IClaims` implementation:
  - `Allocate.Next` runs **on the hub**, with the spoke's reported `LoginRoom`s and allocate CLIs from the heartbeat;
  - it writes `leases(ticket, member, device, login, session, lease_id, epoch, expires_utc, grace_until_utc)`,
    `assignments` and `ticket_work` in one transaction (`TicketsDb`'s write lock).
- Spoke:
  - `NextCommand` in `Join` sends `claim` through `HubLink`, a local request over the daemon's inbox, with a 10 s
    timeout and a plain "hub unreachable: nothing claimed" message;
  - worktree creation and `Dispatcher` stay local;
  - renewals ride on the heartbeat;
  - the ticket-close path sends `complete {leaseId, epoch, commit}`.
- Replica: the spoke applies the feed into `data/Tickets.db` through `TicketsDb` (the file the hooks read with stdlib
  sqlite3). Its local writes are refused in `Join` unless they come from the replica writer.
- Git sync: `Daemon.SyncTicketsDbIfDue` and `SyncOrchestratorDbIfDue` are skipped when `Mode = Join`. Only the hub
  machine syncs.
- Cross-repo work in MindAttic.Tickets (its own ticket):
  - a `TicketsDb` remote-writer hook, so the `tickets` CLI and Tickets Desktop write through the hub API when
    `Join` is configured (read from the Orchestrator's hub settings or its own);
  - otherwise they are read-only with a clear message.

**Protocol.**

- `claim`, `lease-grant`, `renew` (on the heartbeat), `complete`, `release`, `lease-lost`.
- `replica-delta {cursor, rows}`.

**Lease rules (from `2-plan.md` §4.3):**

- A lease lives 3 heartbeats.
- An expired lease is held for the offline grace (`Hub.OfflineGraceMinutes`) before the ticket is queued again.
- A stale epoch is refused.
- A spoke told `lease-lost` stops that ticket's subagent the way `AgentAbort` does, and keeps its worktree.

**Migration.**

- `hub import-tickets` points the hub's `TicketsDb` at the host's SQL Server database. Same machine, so it is a
  config change, not a copy.
- Joined spokes' `data/Tickets.db` is rebuilt from the feed on first join, and the old file is kept as `.prejoin`.

**Tests:**

- Two fake spokes claim at once and get no duplicates.
- A spoke going offline keeps its lease through the grace, then the ticket is requeued.
- A stale-epoch complete is refused.
- The replica converges after deltas are missed.
- Hooks read the replica (the existing hook tests point at a replica file).
- `Off` and `Host` still claim locally with today's semantics, and the existing `Allocate` tests are unchanged.

**Risks.** **The highest of any phase.**

- Concurrency bugs in claims.
- Tickets Desktop writing to a replica.
- Moving git-sync ownership.

Mitigations: ship dark; run `Host` mode on the main machine alone for a week, where leases are local but go through the
hub; then add one remote spoke.

**Effort.** 3 to 4 weeks, plus about 1 week in MindAttic.Tickets.

## Phase 5: central policy and cross-machine dispatch

**Goal.** The org's pacing policy is set once and holds on every spoke. Work can be dispatched to a member's machine
that has opted in.

**Code:**

- Policy: `org limit`, `org allow`, `PaceTargetPercent`, `RepoOrgs` and the weekly reserve are written at the hub (the
  CLI and Settings modal call the hub when joined) and pushed as `PolicySnapshot`.
- `IPolicySource` (Hub) feeds `AccountPacer` and `TicketPins`.
- With no snapshot, the spoke uses the defaults (`UsageCutoffs.Default*`).
- Allowance split: when one login is live on more than one device, the hub divides the login's subagent allowance by
  active CLIs across the devices (the `SubagentShare` rule) and pushes `allowanceShare`. `AccountPacer` writes its
  share into `rl-status.json`.
- Remote dispatch:
  - the hub sends `dispatch {ticket, login, prompt}` only to spokes with `Hub.AcceptDispatch = true`, a setting the
    member sets on their own machine;
  - the spoke runs the existing `Dispatcher` and Launcher path on its own login;
  - the hub never turns `/allocate` on in any CLI.
- Rebalance and failover:
  - `AccountRebalancer` decisions that cross machines become hub reassignments of *tickets*, never moves of a CLI to
    another person's login (T681's rule);
  - API-key failover reassigns the ticket to the API-key person on a key-holder spoke (`Hub.KeyHolder = true`, set by
    an admin);
  - `ApiSpend` is reported by that spoke.

**Protocol.** `policy` v1, `allowance-share`, `dispatch` and `dispatch-result`.

**Migration.** `hub import-policy` copies `usage-cutoffs`, allowances and settings once.

**Tests:**

- The policy applies after a push.
- An offline spoke falls back to the last policy, then to the defaults.
- Two devices on one login split the allowance and never exceed it together.
- Dispatch is refused to a spoke that didn't opt in.
- Failover never targets another person's login.

**Risks.** Policy lag. Hard stops use Claude's own account-wide percent locally, so lag can only delay a *raised*
allowance, never break a cutoff.

**Effort.** 2 to 3 weeks.

## Phase 6: hardening, deployment and the dedicated hub

**Goal.** Run the hub off a member's desktop and operate it safely.

**Code and ops:**

- `deploy.ps1` publishes `Hub`, and the gateway too (its publish was left out of T928).
- Hub backup: the SQL Server and SQLite pattern in `DATA_STORES.md`.
- A runbook for moving the hub to a dedicated tailnet box. Spokes re-pin by `hub join --rotate-pin`, signed by the old
  key.
- An optional cloud relay in front of the hub for members off the tailnet (Tailscale Funnel or an Azure container).
  Same protocol.
- Rate limits on `/enroll` and `/device/token`.
- A device revocation UI in the dashboard.
- A hub audit viewer.
- Version-skew tests: spoke N-1 against hub N.
- Chaos tests: hub kill, spoke sleep, clock skew.

**Effort.** 1 to 2 weeks.

## Order and total

The phases run in order 0 → 1 → 2 → 3 → 4 → 5 → 6.

- Phases 2 and 3 can run in parallel once 1 lands.
- Phase 3 alone already delivers "everyone's CLIs visible and shareable" across machines.
- Phase 4 delivers "work their tickets" across machines.

Total: about 12 to 17 weeks.

## Proposed tickets (not filed; awaiting approval)

| # | Phase | Ticket summary |
|---|---|---|
| 1 | 0 | Hub seams: `IClaims`, `IPolicySource`, `IMeterSink`, `IOrgDirectory`, `ISessionDirectory` with Local implementations over today's code, plus `OrchestratorSettings.Hub` (Mode `Off` by default); no behaviour change |
| 2 | 0 | `MindAttic.Orchestrator.Hub.Contracts`: a dependency-free library with `HubProtocol`, `HubEnvelope` and the payload records, with round-trip and version-skip tests |
| 3 | 1 | `MindAttic.Orchestrator.Hub` service: Kestrel TLS, MindAttic.Authentication sign-in, a devices table, `/enroll`, `/device/token` and a `/spoke` WebSocket with hello, welcome and heartbeat |
| 4 | 1 | Spoke `HubLink`: a CNG device key, a signed-challenge token, a reconnecting control socket, a `hub_outbox`, and a `HubLinkSupervisor` in the daemon |
| 5 | 1 | `orchestrator hub host\|join\|status\|leave\|devices` and a dashboard hub chip; a `HubSupervisor` runs the hub in Host mode |
| 6 | 2 | Presence and meters: spokes push CLIs, masked sessions, meter readings and agent summaries; the hub merges them per login and serves `/org/presence` and `/org/usage` |
| 7 | 2 | Org view in the dashboard and `/org usage` across machines; remote people read-only; the hub is the store of record for members and orgs (`hub import-org`) |
| 8 | 3 | Gateway outbound mode (`HubDialer`) and the hub `StreamRelay`: per-stream outbound sockets spliced to viewers, no inbound port on spokes |
| 9 | 3 | Grants and audit at the hub, with signed `open-stream` decisions re-checked by the spoke's gateway; `gateway grant\|revoke\|audit` go to the hub when joined |
| 10 | 3 | Owner consent for interact: a first-use toast per grant on the owner's machine, watch-only on timeout; no deflate on interactive remote streams |
| 11 | 3 | Team Wall and TermViewer through the hub (`--hub`), listing org sessions beside peer remotes |
| 12 | 4 | Hub Tickets API and replica feed; spokes keep `data/Tickets.db` as a hub-fed replica for the hooks; DB git sync only on the hub |
| 13 | 4 | Ticket leases: claim, renew, complete and release with epoch fencing and offline grace; `orchestrator next --claim` goes through the hub when joined |
| 14 | 4 | (MindAttic.Tickets) Write through the hub API when the machine has joined a hub; read-only with a clear message otherwise |
| 15 | 5 | Central policy push (cutoffs, allowances, pace target, routing) with offline fallback to the last policy, then the defaults |
| 16 | 5 | Per-login allowance split across devices when one Claude login is live on several machines |
| 17 | 5 | Opt-in remote dispatch (`Hub.AcceptDispatch`) plus cross-machine rebalance and API-key failover to key-holder spokes only |
| 18 | 6 | Hub deploy, backup, pin rotation, revocation UI, rate limits, an optional cloud relay, and version-skew and chaos tests |
