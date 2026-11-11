# Hub and spoke: org members on one central Orchestrator (M.ORCH-T1127)

Status: research spike, plan and implementation plan, 2026-11-11. Nothing here is built. The phase tickets are listed in
§9 and wait for approval.

**The ask** (the owner, 2026-10-27): the Team Wall works, but every member is signed in on one computer. Let members
connect their own accounts to MindAttic.Orchestrator.Desktop from their own machines and work their tickets where
everyone's CLIs are visible and shareable. Keep today's setup working exactly as it is. Grow toward a central hub
("wheel") with spokes.

**Why this doc again.** This is the second pass. The first pass (27 Oct, `docs/design/hub-and-spoke/`, commit
e2443513) planned a hand-built hub. That plan's epic (M.ORCH-T1145) and its 18 tickets were closed as wont-do the same
day and moved to **MindAttic.Relay** (epic M.RLY-T2, `MindAttic.Relay/docs/design/relay.md`), which rebuilds the same
design on NATS + JetStream. None of Relay is built yet. Since then, three pieces shipped that change the picture, plus one doc:

- **Machine roles** (M.ORCH-T1473, `docs/REMOTE_HOST.md`): `standalone` (default), `host` (runs the daemon, every CLI
  and the gateway on the network) and `client` (runs nothing; its dashboard is the host's Team Wall). The owner key is
  sealed with DPAPI on the client and mints fresh gateway tokens, so the link never expires.
- **Tickets through the gateway** (M.ORCH-T1479): a `tickets` shim on the client sends every command to the host's
  gateway `POST /tickets`, which runs the real CLI next to the host's database. Owner's interact token only.
- **Branch builds on the host** (M.ORCH-T1501, first slice, off by default): the host builds each `origin/work/*`
  branch in its own worktree (`Core/Scheduling/BranchBuilds.cs`); the local test run, sign-off and merge are still to
  come. The owner's own machine stays on `main`.
- Plus `docs/TAILSCALE.md`: the documented way to reach a home PC without opening ports.

So there are now two shapes of "remote" in the code base: a **central host with thin clients** (shipped, one owner),
and **federated spokes on Relay** (designed, not built). This doc compares them and recommends doing both, in that
order. It does not repeat the first pass's detail; read `docs/design/hub-and-spoke/1-research-spike.md` for the code
inventory and prior art, and `relay.md` §10 for how the federated plan maps onto Relay.

## 1. Goals and non-goals

**Goals**

1. A member signs in from their own PC with their own identity, not the owner's key.
2. Everyone's CLIs in the org show on everyone's Team Wall; sharing is opt-in per session, as now.
3. A member works their own tickets in their own CLIs, and can let a teammate watch or type, with an expiring grant
   and their consent.
4. One ticket queue for the org, with no double claims.
5. **Today's single-machine setup is unchanged**: `standalone` stays the default, and in it the new code is dead code.
6. Each phase ships on its own and is useful on its own.

**Non-goals**

- Moving or copying a Claude credential, `setup-token` token or the Vault API key off the machine that holds it (DPAPI
  already forbids it).
- End-to-end encryption that hides masked terminal bytes from the hub. The hub is org infrastructure.
- Browser or phone viewers (Remote Desktop over Tailscale and `/remote-control` cover phones).
- Several hubs, federation between orgs, or a highly available hub. One hub, backed up.
- Syncing working trees. Git stays the integration point.
- Throttling machines. Only the hard cutoffs (usage, and the memory guard) stop work.
- Non-Windows spokes (TermHost is ConPTY only).
- The hub turning `/allocate` on in anyone's CLI.

## 2. What exists today

| Piece | What it gives a multi-member setup | Gap |
|---|---|---|
| TermHost + relay protocol (`Term/TermProtocol.cs`, v2) | Detached CLIs, replay ring, several viewers, watch/interact Hello | Machine-local registry |
| Gateway (`Gateway/GatewayApp.cs`): `/health`, `/auth/token`, `/tickets`, `/sessions`, `/sessions/{id}/stream` | TLS with SPKI pinning, WebSocket streams, masking before bytes leave (`StreamMasker`), grants with expiry rechecked every 10 s, audit of counts never content | An inbound listener per machine; `/tickets` is owner-only; member tokens are hand-minted by the owner |
| ACL (`TermGatewayAcl`, `term_grants`) | Same-org watch by default, interact by grant | Decided from the host's own `orchestrator.db` |
| Machine roles (`Core/Setup/MachineRole.cs`) | One host, one client, one writer of the committed DBs | Exactly one client identity: the owner |
| Team Wall (`TeamWall`, `GatewayClient`, TermViewer `--remote`) | Merges local sessions with remote gateways | A mesh: each viewer adds each gateway by hand |
| Members and orgs (`members`, `organizations`, `org_members`) | Who is in the org, which Claude logins are whose | No credential to sign in with |
| Pacing (`AccountPacer`, `rl-status.json`, hooks) | Per-login cutoffs, enforced in milliseconds, offline | Single machine |
| Claims (`Allocate`, `DispatchLease`) | One feeder per machine | A pid lease means nothing on another machine |
| DB git sync (daemon, every 10 min) | The only cross-machine data path | Unsafe with two writers; already off on a client |
| Relay (design only) | Leaf nodes dialled out, per-spoke JetStream domain, KV for presence/policy/leases, auth callout for grants | Not built; 10 foundation tickets before any hub value |

## 3. The hard questions

### 3.1 Where do a member's CLIs run?

This decides everything else.

- **On the central host.** That is today: every org login lives in `~/.claude-accounts/<email>` on one machine. The
  member's PC only shows and types. Cheapest, and it already half works. But every member's CLI runs on one box, as
  one Windows user, unless we isolate them (§3.4).
- **On the member's own machine.** Their repos, their RAM, their Claude sign-in and DPAPI scope stay theirs. The hub
  must then merge presence, streams, tickets and policy from many machines. That is the Relay plan.

Both are legitimate and an org will want both: a small team sharing one big host, and a member with their own
workstation. The design has to let the two coexist on one Team Wall.

### 3.2 Transport

| Option | For | Against |
|---|---|---|
| **Gateway as it is** (HTTPS + WebSocket, one inbound listener on the host) | Shipped; pinning, masking, grants, audit all work; clients dial the host | Only the host listens; a member machine that runs CLIs would need its own inbound port (the mesh) |
| **Hand-built hub socket** (first pass: control WebSocket + per-stream sockets, outbox, seq/ack) | Full control, reuses `TermBridge` | We write reconnect, sequencing, outbox and replay ourselves; superseded by the Relay decision |
| **Relay** (NATS leaf nodes dialled out, JetStream, KV, auth callout) | No inbound port on spokes; offline buffering per spoke; durable replay; per-subject permissions; one bus for every app | A server per machine; the leaf/JetStream-domain sourcing is the most intricate part and is unproven (M.RLY-T10 prototypes it) |
| SignalR / gRPC | Typed, managed reconnect | Re-litigated and rejected in both earlier passes (proxy trouble, head-of-line blocking, one more framework) |

Reachability is the same in every option: put the host on a tailnet (`docs/TAILSCALE.md`, `setup-host.ps1 -Bind
<100.x>`), and use a real certificate where possible (`tailscale cert`), else the self-signed one with its pin carried
in the connect line.

### 3.3 Identity and auth per member

- Today the gateway knows one subject that matters: the **owner**. Member tokens exist but are minted by hand.
- Short term (central host): the host mints a **member-scoped key** per member (`machine invite`), the same mechanism
  as the owner key, carried in a connect line and sealed with DPAPI on the member's PC. Tokens minted from it are
  short-lived, carry the member's email, and are checked against `org_members` on every request. Revocation is one
  command and closes open streams within the 10 s recheck.
- Long term (federated): MindAttic.Authentication sign-in on the hub (password + TOTP) and a **device** enrollment per
  machine, with a non-exportable key and short-lived Relay credentials (M.RLY-T11, T15). The member key is exchanged
  for a device enrollment when a client becomes a spoke, so a member signs in once.
- Two identities stay separate: the **member** (a person) and the **device** (a machine acting for them). A Claude
  login is something a member owns; the hub trusts reports about a login only from its owner's devices.

### 3.4 Security of exposing CLIs and terminals

Remote input into a Claude CLI is remote code execution on the machine it runs on, by design. The rules from the
gateway carry over unchanged, and two new ones are needed.

Kept:

- **Masking at the source** (`SecretMasker`, Vault values) before any byte leaves.
- **Opt-in share and pause per session** (`TermBroadcast`); the owner's local "no" beats any grant.
- **Watch by default; interact only by an expiring grant**, rechecked every 10 s.
- **Audit counts, never content.**
- **No compression on interactive streams** (attacker-chosen input next to secrets is the CRIME/BREACH shape,
  T934/T935).

New:

- **Session ownership follows the login, not the Windows user.** On a shared host, the "owner" of a session is the
  member whose Claude login it runs on. Only they get interact by default.
- **Owner consent for interact.** The first use of each grant raises a toast on the session owner's screen; no answer
  in 30 s means watch only. A compromised hub, or an over-broad grant, can't type into a CLI whose owner hasn't said
  yes.
- **Per-member Windows users on a shared host.** Today every login runs as one Windows user, so any CLI can read every
  other login's `.credentials.json` and decrypt every DPAPI secret on the box. That is acceptable while the owner is the
  only one typing. Once other members type into CLIs on the host, each member's CLIs must run under their own Windows
  account (own profile, own DPAPI scope, pipes ACL'd to that user and the daemon). ConPTY needs no interactive desktop,
  and `setup-token` tokens avoid the browser sign-in, so this is feasible. Until it is on, non-owner interact is
  refused unless an admin sets an explicit "trusted org" Vault setting that says what it means.
- **The `/tickets` endpoint runs a process on the host.** For members it takes an allowlist of verbs, records the
  member as the author, and leaves owner-only verbs (approvals, settings) owner-only.

### 3.5 What each spoke shares

| Shared with the hub | Never leaves the spoke |
|---|---|
| Presence: machine, member, CLIs (repo, login, `/allocate` on/off) | Claude credentials, `setup-token` tokens, the Vault and its key |
| Session list with masked titles (no command line, cwd, config dir or pipe, as `/sessions` already omits) | Unmasked terminal bytes |
| Meter readings per login (percent and resets) | The working tree (git carries code) |
| Masked terminal frames, only for shared sessions with a live viewer | Share, pause, consent and kill decisions (made locally) |
| Ticket transitions and lease renewals | Hook enforcement (stays local and offline) |
| Audit records | |

### 3.6 Failure modes

| Failure | Central host (phases 1-2) | Federated spoke (phases 3-5) |
|---|---|---|
| Host/hub down or asleep | Members lose everything: their CLIs live on it. Mitigate: no sleep, Autologon, Scheduler restarts (already in `setup-host.ps1`); later a dedicated box | Spoke CLIs, hooks and pacing carry on; only remote visibility and new claims stop; JetStream catches up on reconnect |
| Member PC offline | Their CLIs keep running on the host; they reconnect and resume | Their spoke's leases ride the offline grace, then requeue |
| Network partition with work in flight | n/a (one machine) | Lease fencing (KV revision) refuses a stale completion; git is the real integration point |
| Revoked member mid-stream | Stream closed within 10 s (existing recheck) | Relay connection dropped (auth callout) |
| Version skew | Gateway protocol negotiates (TermProtocol 1/2); client refuses clearly when too old | N and N-1 contracts at the hub; older spokes keep running locally |
| Host overload (RAM) | Memory guard's hard cutoffs (4/2/1 GB) stop new work; size the host for members × CLIs | Load spreads to members' machines |
| Two DB writers | Impossible: the host is the one writer, clients never write | Only the hub git-syncs `data/*.db`; spokes' sync is off |

### 3.7 Bandwidth

- The first pass measured about 36 KB/s for 12 working CLIs, with bursts to about 360 KB/s, for watched streams. Frames
  only flow while someone watches; an idle Team Wall tile costs its session list, not bytes.
- An attach costs one replay of TermHost's ring, once.
- Ticket commands through `/tickets` are small JSON; the mirror refresh on `machine pull` is the largest periodic
  transfer (the ticket DB file).
- Through a hub relay, watching adds one round trip of latency; under ~100 ms is fine for typing.
- All of this is trivial on a home uplink. The constraint is the host's RAM and CPU, not the network.

### 3.8 Pacing

Unchanged in the central-host shape: every login is still on one machine, so `AccountPacer` and the hooks work as
today. In the federated shape, enforcement stays on each spoke (the hooks need an answer in milliseconds, offline), and
policy and the allowance split move to the hub (M.RLY-T27, T28). Claude's percent is account-wide, so the hard stop is
correct on every machine even with no hub.

### 3.9 Branch builds (M.ORCH-T1501)

T1501 builds work branches on the host and sends the test run to "the local machine". With members, "local" becomes
"the machine of the member whose ticket the branch carries": their spoke runs the tests and their sign-off merges. A
member without a spoke falls back to the owner's machine. This belongs to the last phase, after cross-machine dispatch.

## 4. Options

| | A. Central host, many clients | B. Peer mesh (today's `gateway remote add`) | C. Federated spokes on Relay | D. Hand-built hub (first pass) |
|---|---|---|---|---|
| Where CLIs run | Host | Each machine | Each machine (and the host) | Each machine |
| New infrastructure | None | None | NATS server per machine, hub service | Hub service |
| Builds on | T1473/T1479 roles, gateway | Gateway | Relay epic (≥10 foundation tickets) | Superseded design |
| Time to first value | Weeks | Already here | Months | Months |
| Inbound ports | Host only | Every machine | Hub only | Hub only |
| Shared ticket queue | Yes (one DB on the host) | No | Yes (TIX replica + leases) | Yes |
| Members' own machines' RAM used | No | Yes | Yes | Yes |
| Biggest risk | One box is a single point of failure, and credential separation on it | N² setup, no shared queue | Leaf/JetStream sourcing is intricate and unproven | Rewrites what Relay gives free |

## 5. Recommendation

**Do A now, C next, and make A's host become C's hub.**

- **A first** because it is mostly built. The host already runs every login's CLIs, owns the one ticket database, and
  serves a Team Wall and a tickets shim to one client. Generalising "one owner client" to "one client per member" with
  per-member keys, session ownership by login, consent and per-member Windows users delivers goals 1 to 4 for members
  who share a host, with no new infrastructure.
- **C second** because some members will want their CLIs on their own machine, and Relay is the accepted plumbing for
  that. The Orchestrator phases here are the integration on top of the Relay hub tickets (M.RLY-T13 to T30), not a
  copy of them.
- **B stays** as it is, beside both, as every earlier plan required.
- **D is not revived.** Where it disagrees with Relay, Relay wins (as M.ORCH-T1145 already says).
- The host of A is the hub of C: same machine, same member identities (exchanged for device enrollments), same grant
  and consent rules, one Team Wall listing host-run and spoke-run CLIs together.

## 6. Architecture (recommended end state)

```
          Hub machine (today's host)                        Member PC (client)       Member PC (spoke)
 +--------------------------------------------+            +----------------+       +----------------------+
 | Orchestrator daemon + gateway (:7443)      |<--wss------| Dashboard =    |       | Daemon, TermHost x N |
 | TermHost x N, one Windows user per member  |            | Team Wall,     |       | Gateway (local)      |
 | Ticket DB (one writer), DB git sync        |            | tickets shim   |       | Relay leaf  ---------+--+
 | Relay hub server + Orchestrator.Hub        |<-----------------------------------------------leaf, wss :443 |
 |   (auth, devices, presence, leases, policy)|                                     | Tickets.db replica   |
 +--------------------------------------------+                                     +----------------------+
```

- **standalone** (default): exactly today. No listener on the network, no Relay link, local everything.
- **host**: today's host, plus member keys (phase 1), member CLIs under member Windows users (phase 2), and the Relay
  hub (phase 3).
- **client**: today's client, but for any member (phase 1), with interact on their own CLIs (phase 2).
- **spoke** (phase 3): a member machine running its own CLIs, joined over Relay.

## 7. Phased implementation plan

Rules for every phase: `standalone` behaviour is unchanged and pinned by the phase 0 tests; each phase ships alone and
may sit dark; the peer gateway is never removed; every new tunable is a Vault setting.

### Phase 0: topology seam and standalone guard (no behaviour change)

- One topology read point over `MachineRole` (standalone, host, client; member client and spoke reserved). Every
  role-dependent branch reads it: daemon start, pace hooks, DB git sync, gateway bind, tickets shim, branch builds.
- Uses M.RLY-T13's Local seams if they have landed; does not wait for them.
- Tests pin standalone: no network listener, hooks installed, git sync on, local `Tickets.db`, no shim, no branch
  builds, no new process or socket. Host and client keep REMOTE_HOST.md behaviour.

### Phase 1: members connect to the central host (watch)

- `orchestrator machine invite <member>` mints a member-scoped key and a connect line; `machine members`,
  `machine revoke <member>`.
- A member's client shows every shared session in their org, watch only (existing same-org ACL).
- `/tickets` accepts member tokens for allowlisted verbs, authored as the member, audited per member.
- Host dashboard shows connected members.

### Phase 2: members work their own CLIs on the host

- Session owner = the member whose login it runs on; interact on own sessions by default; others by grant.
- Start and stop CLIs on one's own logins from the client; never on another member's.
- Owner consent toast on first use of each grant; watch only after 30 s.
- Per-member Windows users on the host; non-owner interact refused without them unless "trusted org" is set.
- No deflate on interactive streams.

### Phase 3: federated spokes join the host (Relay)

- Requires the Relay foundation (M.RLY-T3 to T7, T10, T11) and hub tickets T15 to T23.
- The host becomes the Relay hub plus Orchestrator.Hub; `machine join` turns a client into a spoke; the member key
  becomes a device enrollment.
- Team Wall merges gateway sessions (host) and Relay sessions (spokes) under one grant and consent model.

### Phase 4: one ticket queue across spokes

- Requires M.RLY-T24 to T26.
- Spokes use the TIX replica and `tix.cmd.claim` leases instead of the shim and the mirror; a lost lease stops that
  ticket's subagent and keeps its worktree; only the hub git-syncs the DBs. Clients keep the shim.

### Phase 5: policy, dispatch, branch builds, operations

- Requires M.RLY-T27 to T30.
- Central pacing policy and allowance split; opt-in remote dispatch; T1501 test runs on the owning member's spoke; hub
  backup, move to a dedicated box with pin rotation, revocation UI, version-skew and chaos tests, with the
  week-unattended goal (M.ORCH-T1611) and the memory guard in mind.

**Order.** 0 → 1 → 2 delivers the ask for members sharing one host. 3 → 4 → 5 follow the Relay epic and can't start
before its foundation lands. Phases 1 and 2 don't depend on Relay at all.

## 8. Risks and open questions

- **Relay's leaf/JetStream-domain sourcing** is unproven; phases 3 to 5 wait on M.RLY-T10's prototype. If it fails,
  phases 1 and 2 still stand, and the fallback for phase 3 is an outbound gateway dial (the first pass's `HubDialer`).
- **Per-member Windows users on one host** (phase 2) is the largest unknown in the near-term track: launching TermHost
  under another account, ACLs on its pipes, and the Launcher's assumptions about one profile. Spike it at the start of
  phase 2.
- **Host sizing.** Members × CLIs × builds on one box. The memory guard protects it; the owner sizes it.
- **The `/tickets` allowlist** must track the tickets CLI's verbs; a new verb is owner-only until added.
- **Org membership is entered by hand.** Phase 1 trusts `org_members`; a real sign-in arrives with phase 3.

## 9. Phase tickets

Filed in MindAttic.Orchestrator, each a follow-up of M.ORCH-T1127, each requiring the phase before it, waiting for
approval:

| Phase | Ticket | Also requires |
|---|---|---|
| 0: topology seam and standalone guard | M.ORCH-T1639 | (related: M.RLY-T13) |
| 1: members connect to the central host (watch) | M.ORCH-T1640 | |
| 2: members work their own CLIs on the host | M.ORCH-T1641 | |
| 3: federated spokes join the host (Relay) | M.ORCH-T1642 | M.RLY-T15, M.RLY-T23 |
| 4: one ticket queue across spokes | M.ORCH-T1643 | M.RLY-T25, M.RLY-T26 |
| 5: policy, dispatch, branch builds, operations | M.ORCH-T1644 | M.RLY-T30 |
