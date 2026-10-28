# Spike: members' own machines on one Orchestrator (M.ORCH-T1127)

**Question.** Today every org login (`~/.claude-accounts/<email>`) runs on one machine. How do members connect from their
own machines to one central Orchestrator, work their own tickets, and see and share each other's CLIs, while the
single-machine setup keeps working exactly as it does now?

**Short answer.** About half of it already exists. TermHost, the relay protocol, the Gateway and the Team Wall already
let one machine watch another's CLIs, with grants, masking and audit. What is missing is a **centre**: one place that owns
identity, the ticket queue and claims, the org's policy and the merged view. Machines also have to reach that centre
without opening inbound ports. Read `2-plan.md` for the design and `3-implementation-plan.md` for the phases.

---

## 1. What exists today, and how far it goes

| Piece | Where | Already remote-capable? |
|---|---|---|
| **TermHost**: one detached process per CLI that owns its ConPTY, replay ring and headless VT screen (libghostty-vt) | `src/MindAttic.Orchestrator.TermHost` (`TermSession`, `ViewerPolicy`, `Replay/HeadlessScreen`) | Yes, locally. Several viewers, size ownership, watch/interact Hello (protocol 2) |
| **Session registry**: one `terms\<id>.json` per host, plus the `term-session` bus kind | `Term/TermSessions.cs`, `TermSessionRecord`, `TermBus` | Machine-local only (`%LOCALAPPDATA%`, pid liveness) |
| **Relay protocol**: `u8 kind \| u32 len \| payload`; Hello, Replay, Output, Size, Input, TakeSize, Typing, Paused | `Term/TermProtocol.cs` (`Version` 2, `Supported` [1,2], `Negotiate`) | Yes: the same frames run over a pipe or a WebSocket |
| **Viewer**: a bare `TerminalControl` driven by `RelayConnection` over any frame `Stream` | `Term.Wpf/RelayConnection.cs`, `RelayTerminal`; `Core/Sessions/WebSocketFrameStream.cs` | Yes |
| **Gateway**: Kestrel, TLS, WebSocket bridge from a local pipe to remote viewers | `Gateway/GatewayApp.cs` (`/health`, `/auth/token`, `/sessions`, `/sessions/{id}/stream`), `TermBridge` | **Yes, peer to peer. It is an inbound listener on each owner's machine** |
| **Gateway tokens**: `v1.<payload>.<sig>`, HMAC-SHA256 under the owner's `signing.key`; member tokens minted by the owner | `Term/TermGatewayTokens.cs`, `Gateway/GatewayTokens.cs`, `Cli/GatewayCommand.cs` | The owner hand-carries each token. There is no member sign-in |
| **ACL**: active member, same-org watch by default, `term_grants` raise access to interact with an expiry; rechecked every 10 s | `Term/TermGatewayAcl.cs` (`acl.json`), `Gateway/GatewayGrants.cs`, `Core/Sessions/TermGrants.cs`, `Cli/GatewayAclCommands.cs` | Decided on the owner's machine from that machine's `orchestrator.db` snapshot |
| **Broadcast hygiene**: opt-in share, `SecretMasker`/`StreamMasker` (token shapes plus Vault values), pause, "being watched" | `Term/TermBroadcast.cs`, `Gateway/StreamMasker.cs`, `VaultSecrets.cs`, `Term/TermWatchers.cs` | Yes. Masking runs **before bytes leave the owner's machine** |
| **Certificates**: thumbprint, PFX, or a self-signed dev certificate with SHA-256 pinning | `Gateway/GatewayCertificates.cs` | Yes. The pin is passed out of band |
| **Audit**: attach, detach, refused, input counts; never content | `Gateway/GatewayAudit.cs` → `term_audit` (migration 16) | Stored on the owner's machine |
| **Team Wall**: merges the local registry with each remote gateway's `/sessions`; read-only tiles; TermViewer pop-out | `Wpf/TeamWallWindow.cs`, `TeamWallTile.cs`, `MainWindow.TeamWall.cs`; `Core/Sessions/TeamWall.cs`, `GatewayClient.cs`; `TermViewer/RemoteViewer.cs` | Yes, but as a **mesh**: each viewer adds each teammate's gateway by hand (`gateway remote add`) |
| **Daemon**: a 15 s tick that measures, paces, steers, dispatches, supervises the gateway, and git-syncs the DBs | `Core/Daemon.cs`, `Sessions/GatewaySupervisor.cs` | Machine-local. Its status bus and inbox are `CurrentUserOnly` named pipes (`Status/StatusStream.cs`, `StatusInbox.cs`) |
| **Envelope convention**: `{kind, v, at, pid, payload}`; unknown kinds and newer versions are skipped | `Status/StatusEnvelope.cs`, `AppBus`, `StateBus` (members, orgs, settings) | Already transport-agnostic, so it can be reused on the wire |
| **Pacing**: per-login budget, pace and allowance from that login's own `claude-usage.json`; hard cutoffs 98% (5 h) and 90% (week) | `Budget/AccountPacer.cs`, `Pacer.cs`, `UsageCutoff.cs`, `ClaudeMeterProbe.cs`; hooks read `<configDir>/rl-status.json` | Local. The hooks need an answer in milliseconds, so it must stay local |
| **Meter reading** as a record | `Status/StatusStream.cs` `MeterReading` (email, 5 h and weekly percent, resets, source) | Ready to send over the wire |
| **Members and orgs** | `members`, `organizations`, `org_members` (`Budget/MemberRoster.cs`, `Sessions/Organizations.cs`, `OrgRouting.cs`) | Data only. A member is entered by hand and holds no secret to sign in with |
| **Claims**: `orchestrator next --claim` → `Allocate.Next` → `Dispatcher`, recorded in `assignments`; one feeder per machine via `DispatchLease` (`leases` row, pid plus start time) | `Cli/PlanningCommands.cs`, `Scheduling/Allocate.cs`, `Dispatcher.cs`, `DispatchLease.cs` | **Single machine.** A lease is a pid on this box, and a claim has no expiry |
| **Ticket DB**: `TicketsDb` on SQLite or the SQL Server store of record; `data/Tickets.db` is always a complete copy for the hooks; `ticket_work(assignee, reserved, login, session, since)` | `MindAttic.Tickets/Data/TicketsDb*.cs`, `TicketsServer.cs` | SQL Server is `localhost` with a Windows sign-in. Nothing remote |
| **Git sync of the DBs**: `data/orchestrator.db` and `.sql` every 10 min, `data/Tickets.db` the same way | `Daemon.SyncOrchestratorDbIfDue`, `SyncTicketsDbIfDue` | The only cross-machine sharing today, and **unsafe with two writers** (binary SQLite merge) |
| **Credentials**: per-login folders; `setup-token` long-lived tokens sealed with DPAPI in the Vault; the Vault API key read at launch | `Sessions/AccountDirs.cs`, `LoginTokens.cs`, `Billing/ApiKeyPerson.cs`, `BillingSwitch.cs` | Bound to this Windows user by design (DPAPI) |

**Verdict.** The terminal plane is about 80% done. The control plane (identity, tickets, policy, presence) is 0%
done for more than one machine. The gateway's design choices hold up and should be kept: mask at the source, owner
consent, read-only by default, grants with an expiry, and an audit that records counts but never content. Its topology
should not be kept: an inbound listener per machine with hand-carried tokens and pins.

## 2. The hard problems

### 2.1 Identity and auth per member

- Today a member is a row in `members` with no credential. Gateway tokens are minted by the *owner* and handed over by
  hand (`gateway token <who>`).
- The hub needs a real sign-in. The prior spike already chose the way (`docs/spikes/cli-host-viewer.md` § Authentication):
  host **MindAttic.Authentication** (Argon2id, TOTP, lockout, audit) in a central web app. That app needs an EF
  `DbContext` and `MindAttic:Vault:Security:*`, and a central hub can have both.
- Two identities are separate, and the design must not mix them up:
  - the **member** is a person, signed in to the hub;
  - the **device** is a spoke machine. It is enrolled once by a signed-in member and holds a non-exportable key.
- A spoke acts *as its device, on behalf of its member*.
- A Claude login (`org_members.login`) is something a member *owns*. The hub can't verify Claude credentials it never
  sees, so it trusts a spoke's report only for logins that an admin mapped to that spoke's member.

### 2.2 Where the CLIs physically run

- **On each member's machine.** That machine is the only place that has their repos, their Claude sign-in, their
  Windows session (which `claude /login` needs for its browser) and their DPAPI scope.
- Running members' CLIs on the hub would mean copying everyone's OAuth tokens to one box. It would also break "a
  person's own Claude use always wins" (their local use and the hub's would share one meter with no local arbiter), and
  it would make the hub a code-execution host for everyone.
- So the hub runs **no member CLIs**. The hub machine may also be a spoke, which is today's machine exactly.

### 2.3 Claude logins and credentials never leave the member's machine

- `~/.claude-accounts/<email>` (`.credentials.json`, `.claude.json`), the `LoginTokens` Vault bucket and `setup-token`
  tokens stay on the spoke. DPAPI enforces that already.
- The hub receives only derived facts:
  - meter percents and resets;
  - signed-in yes/no;
  - session metadata, with titles masked.
- Anything the hub asks a spoke to do (start a CLI on login X, move a CLI) is carried out by the spoke's own Launcher
  and `TermLauncher`, with its own credentials.

### 2.4 The Vault API key

- The key (`LlmCredentialStore`, provider `Claude`) and the API-key person (`ApiKeyPerson`, `api-key@mindattic.vault`)
  bill money, not a seat.
- Copying the key to every spoke multiplies how widely it is exposed. Sending it from the hub at launch makes the hub a
  key-distribution server.
- **Recommendation:** the key stays only on spokes designated as **key holders**, which is today the hub machine.
- A remote member's failover (`AccountRebalancer`, T664/T681) becomes "reassign the ticket to the API-key person on a
  key-holder spoke", not "restart the member's CLI on the key".
- `ApiSpend` is reported by the key holder.

### 2.5 Pacing and cutoffs from spokes' meters

- The hooks' hard stop (`AgentGate`, reading `rl-status.json`) must answer in milliseconds and must keep working
  offline. So **enforcement stays on the spoke**.
- **Policy** belongs centrally:
  - per-member cutoffs (`org limit`), allowances (`org allow`) and `PaceTargetPercent`;
  - org routing.
- **Merging** belongs centrally too, when one Claude account is used from two machines. That is common: a laptop and a
  desktop. Claude's meter is account-wide, so the two spokes see the same percent. The subagent allowance is the only
  per-machine split that needs the hub, and it uses the same `allowance ÷ active CLIs` share rule that `SubagentShare`
  uses today.
- **Failure mode:** an offline spoke keeps enforcing the last policy it received. With none, it uses the defaults (98 /
  90, `UsageCutoffs.Default*`). Claude's own percent is account-wide, so the hard stop stays correct even with no hub.

### 2.6 The ticket DB, claims and leases

- A claim today is an `assignments` row plus `ticket_work.login/session`, written by whichever CLI ran
  `next --claim`. Only one feeder runs per machine, through the `DispatchLease` pid lease.
- Across machines, two spokes can claim the same ticket, and a pid lease means nothing on another machine.
- Options:
  - point every spoke at the SQL Server store of record directly;
  - put a hub API in front of it;
  - keep git-synced SQLite.
- Direct SQL exposes the database across NAT and puts credentials on every spoke. It also gives no per-member
  authorization, and `TicketsDb`'s write lock is a single-database lock. Git-synced SQLite can't merge binary writes.
  **So use the hub API.**
- Claims become **leases**: `{ticket, member, device, login, session, leaseId, expiresUtc, epoch}`.
  - A spoke renews its leases on every heartbeat.
  - A lease that expires is not reassigned until an **offline grace** passes. The spoke may still be working: its CLI
    runs locally, and git is the real integration point.
  - The `epoch` (a fencing token) lets the hub reject a stale spoke's "done", or its renewal of a lease the hub has
    already reassigned.
- Hooks and Tickets Desktop keep reading a local `Tickets.db` **replica**, which the hub feeds.
- Only the hub git-syncs the DB files. The spokes' `SyncTicketsDbIfDue` and `SyncOrchestratorDbIfDue` must turn off,
  or two machines will commit `data/Tickets.db` over each other.

### 2.7 Live terminal streaming and sharing

- This is already solved peer to peer. The open question is the path.
  - **Hub relay.** Viewer → hub → spoke. It works across NAT, and the hub enforces grants centrally. The hub sees
    masked bytes.
  - **Direct.** Viewer → spoke gateway, as today. It needs reachability.
- **Keep the decisions where they are safe:**
  - **masking stays on the spoke**: only it holds the Vault values that `VaultSecrets` masks;
  - **share, pause and kill stay on the spoke** (`broadcast.json`): the owner's local "no" beats any hub grant;
  - **grants and audit move to the hub**, with the spoke re-checking a signed decision as defence in depth.
- **Interact** is remote input into a CLI that can run tools on the owner's machine. That is remote code execution by
  design. It needs:
  - an explicit grant with an expiry (as now);
  - owner presence: a local consent toast the first time each grant is used ("Erin wants to type into U.SPC. Allow 1 h?");
  - the existing "being watched" banner.
- Compression and secrets: `permessage-deflate` beside attacker-chosen input is the CRIME/BREACH shape (already flagged
  under T934/T935). Turn compression off for interactive remote streams. Keep it for watchers.

### 2.8 NAT, firewalls and transport

- Members are behind home NATs, so **spokes must dial out**. The hub is the only listener (TCP 443, or 7443 on a
  tailnet).
- Transport options:

| Option | For | Against |
|---|---|---|
| Raw WebSocket (what the gateway uses) | We already have the frame codec, `WebSocketFrameStream`, `TermBridge`, TLS pinning and deflate. Passes proxies on 443 | We write our own request and response correlation and reconnect logic |
| SignalR | Reconnect, groups, MessagePack, .NET on both ends | A hub-method model over our binary frames; its reconnect replays nothing; one more framework |
| gRPC bidi streaming | Typed contracts, HTTP/2 multiplexing, deadlines | Awkward through corporate proxies; HTTP/2 head-of-line blocking when many terminals share one connection; a new toolchain (protobuf) |

- **Pick raw WebSocket with two kinds of socket:**
  - one long-lived **control** socket per spoke, carrying versioned JSON envelopes (`StatusEnvelope`'s shape);
  - one **stream** socket per watched session, opened by the spoke *on demand* when the hub asks, carrying
    TermHost frames unchanged.
- One socket per stream avoids head-of-line blocking and reuses `TermBridge` almost verbatim.
- TLS: the hub uses a real certificate where it can (a Tailscale `tailscale cert`, or Let's Encrypt on a public name),
  otherwise the dev certificate. Spokes **pin** the hub's SPKI hash, learned at enrollment, so a pin is never typed in.

### 2.9 Offline spokes and reconnection

- A spoke must never depend on the hub to *keep working*. Its CLIs, hooks and local pacing run on, and only remote
  visibility and new claims stop.
- The spoke keeps an **outbox** (a SQLite table) of meter readings, ticket transitions, lease renewals and audit
  records. Each item carries an idempotency id and is replayed on reconnect. Meter readings are collapsed to the
  latest per login.
- Reconnect backs off (0.5 s up to 60 s, with jitter). The spoke resumes with
  `hello {deviceId, lastAckedSeq, leases[]}`, and the hub answers with the leases it still honours.
- The hub shows a spoke as online, stale (no heartbeat for over 45 s) or offline, with when it was last seen.

### 2.10 Security and abuse

- **Threats:**
  - a stolen device key;
  - a malicious member using interact to type into someone's CLI;
  - a spoke lying about meters (which only affects its own logins) or claiming tickets outside its org;
  - a compromised hub, which could send interact frames to every spoke.
- **Mitigations:**
  - device keys are non-exportable (CNG/TPM where available) and revocable at the hub;
  - access tokens last 15 minutes;
  - every spoke action is authorized against its member's org and role;
  - interact needs owner consent on the spoke, so **a hub compromise can't type into a CLI the owner hasn't approved
    locally**;
  - every frame of input is counted in the audit;
  - rate limits on enrollment and sign-in (`/auth/token` already returns 429).
- Masking is best effort. Sharing stays opt-in per session (`TermBroadcast`), as now.

### 2.11 Versioning between hub and spokes

- Members upgrade at different times. Reuse the patterns that already exist:
  - `TermProtocol.Negotiate`, which picks the highest version both sides speak;
  - envelope `kind` + `v`, where unknown kinds and newer versions are skipped;
  - `TermHostInstalls`' side-by-side binaries.
- The hub supports the current control protocol and the one before it (N and N-1). A spoke older than that gets a
  clear "update to join" refusal and keeps running locally.
- Capabilities are announced in `hello` (`["meters","streams","leases","dispatch"]`), so a phase can ship without a
  flag day.

## 3. Options compared

### Where the hub runs

| Option | For | Against | Verdict |
|---|---|---|---|
| **On one member's machine** (today's main PC) | No new infrastructure; it already has SQL Server, the Vault, both DBs and the API key | Its sleep, reboot or redeploy takes the hub down; it needs an inbound port (a tailnet makes this easy) | **First home**, behind Tailscale. The hub is its own process, so it can move later |
| **Dedicated always-on server** (mini PC or VM on the tailnet) | Uptime; no member's desktop is in the critical path | One more box to run and patch; SQL Server and the Vault have to live there too | **Target home** once more than about 3 members join |
| **Cloud relay or hosted hub** (Azure App Service or a container) | Reachable from anywhere with no VPN; managed TLS | Org data and (masked) terminal bytes leave the premises; cost; the Vault and SQL move to the cloud | **Only** for members who can't join the tailnet, as a relay in front of the same hub |

### Spokes push or hub pulls

- **Spokes push** over their outbound control socket: meters, presence, sessions and ticket changes as they happen. The
  hub never opens a connection to a spoke.
- **The hub pulls only through that same socket.** It sends requests such as "open stream X" or "start ticket T on
  login L", and the spoke answers or dials back. This is the only shape that works behind NAT, and it is what GitHub's
  runners, Teleport agents and Tailscale's DERP fallback all do.

## 4. Prior art worth copying

| System | What to copy |
|---|---|
| **The existing Gateway** (first) | Mask at the source; owner opt-in share and pause; read-only by default; grants with an expiry; access rechecked every 10 s; audit with counts, never content; certificate pinning; one frame per WebSocket message. Keep `TermBridge` and `StreamMasker` as they are and move them behind an outbound dial |
| **GitHub Actions self-hosted runners** | Enrollment: a short-lived registration token, the runner makes its own key pair, then signed JWTs for its access tokens. **Outbound long-poll only**. Jobs are leased and renewed, and a runner that goes quiet loses its job after a timeout. This is almost exactly our spoke and ticket lease |
| **Teleport** | Reverse-tunnel agents; **moderated sessions** (a session needs a moderator present) as the model for interact consent; per-session audit |
| **tmate / upterm** | Separate read-only and read-write join paths; identity-pinned joiners (`--github-user`); a relay for NAT |
| **VS Live Share** | The host owns everything and guests only stream; read-only by default; the host approves each guest; "follow" mode; a cloud relay with direct LAN as an optimization |
| **Tailscale** | A central coordination server for identity and policy, while data moves peer to peer, with a relay (DERP) only as fallback. Our hub is the coordination server, and a later phase can let a viewer go direct to a reachable spoke |
| **Kubernetes kubelet** | Node heartbeats as Lease objects, and node conditions (Ready, Unknown). Workloads keep running on a node that loses the control plane |

## 5. Conclusions for the plan

1. The hub is a new process, `MindAttic.Orchestrator.Hub`. It is **not** the daemon: the daemon is redeployed often
   and is per-user and per-machine. The hub owns identity, devices, the ticket store of record, leases, policy,
   presence, the grants and audit, and the stream relay.
2. The spoke is today's machine, unchanged, plus one link: a `HubLink` in the daemon for control, and the gateway in
   **outbound** mode for streams.
3. Enforcement (hooks, masking, share and pause, consent) stays on the spoke. Decisions and merged views live on the
   hub.
4. Single-machine mode is "hub off". The seams are pass-throughs to today's code, so nothing changes until a setting
   is flipped.
