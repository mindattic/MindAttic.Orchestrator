# Features reference (M.ORCH-T2150)

Commands and options below are as `src\MindAttic.Orchestrator.Cli\Program.cs` registers them. `orchestrator <command> --help`
always prints the live list. Ticket IDs say where a behaviour came from.

Contents: [Team gateway](#team-gateway) · [Broadcast](#broadcast) · [Milestones](#milestones) · [Branches](#branches) ·
[Named agents](#named-agents) · [Mail](#mail) · [Inbox](#inbox) · [Health probes](#health-probes) ·
[Dock, undock and quickload-all](#dock-undock-and-quickload-all) · [pace-watch](#pace-watch)

## Team gateway

What it is for: letting teammates (or your own other PC) watch, and when you allow it type into, the CLIs hosted on this
machine, over HTTPS. A separate process, `MindAttic.Orchestrator.Gateway` (M.ORCH-T933). Off by default.

Turn it on and off:

```
orchestrator gateway                 show settings, whether it runs, the certificate pin
orchestrator gateway on              the daemon starts it within a tick
orchestrator gateway off             the daemon stops the one it started
```

You never start the process yourself. `gateway on` saves the setting and the daemon starts, restarts or stops the gateway to
match it.

Options (each saved to settings; "none" clears the ones that take a value):

| Option | Default | Meaning |
|---|---|---|
| `--bind <ip>` | `127.0.0.1` | Listen address. Loopback exposes nothing; use a Tailscale/VPN address or `0.0.0.0` for a team. |
| `--port <n>` | `7443` | HTTPS port, 1 to 65535. |
| `--cert-path <pfx>` | none | A PFX file; its password is read from `MINDATTIC_GATEWAY_CERT_PASSWORD`. |
| `--cert-thumbprint <hex>` | none | A certificate in CurrentUser\My or LocalMachine\My. |
| `--dev-cert-names <a,b>` | none | Extra DNS names or IPs for the dev certificate (a Tailscale name or 100.x address). |
| `--owner <identity>` | the Windows user | The machine owner, the subject of its tokens. |
| `--token-minutes <n>` | `15` | Token lifetime, 1 to 60. |

With neither `--cert-path` nor `--cert-thumbprint` it serves a self-signed dev certificate. Clients pin its SHA-256;
`orchestrator gateway` prints the pin of the running one.

Who may do what:

```
orchestrator gateway grant <who> watch|interact [--session <id>] [--for 2h]
orchestrator gateway revoke <who> [--session <id>]
orchestrator gateway grants [--all]
orchestrator gateway audit [--since 2h|<date>] [--limit N]
orchestrator gateway token <who> [--scope watch|interact] [--minutes N]
```

- `<who>` is a member by email, the part before the @, name, first name, login alias or initials. They must already be in
  the members table (`orchestrator members add <email>`).
- Members of the same org may watch by default. Interacting needs a grant. `--for` takes `30m`, `2h`, `1d`, up to `30d`;
  interact defaults to 1 hour, watch never expires unless `--for` is given.
- A grant or revoke is written to the gateway's snapshot at once. A stream that lost its access closes within seconds
  (the gateway rechecks every 10 seconds).
- `audit` shows who attached, detached and typed, as counts. Content is never recorded. Default 200 newest records.
- `token` prints a bearer token for a member to hand them. Without `--scope` it is interact when they hold a live interact
  grant, else watch. Without `--minutes` it lasts 365 days; with it, 1 to 60 minutes.

Watching a teammate's gateway (the dashboard's Team Wall, M.ORCH-T936):

```
orchestrator gateway remote add <name> <url> --pin <sha256> [--person <who>] [--token <token>]
orchestrator gateway remote remove <name>
orchestrator gateway remote list [--check]
```

`add` asks for the token when it is left out; it is stored sealed with DPAPI for this Windows user. `list --check` also asks
each gateway for its sessions. Remote entries never restart your own gateway.

Endpoints the process serves: `GET /health`, `POST /auth/token`, `GET /sessions`, `/sessions/{id}/stream` (WebSocket),
`POST /sessions` and `DELETE /sessions/{id}` (member-started CLIs on a host), `GET /consents`, `POST /consents/{id}`,
`POST /tickets` and `POST /branches` (the owner's own client). Its own CLI, for a hand run:
`MindAttic.Orchestrator.Gateway [serve] | token | owner-key | fingerprint`, with `--data-dir` (default
`%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\gateway`) and a repeatable `--dev-cert-name`.

Limits:

- Tokens: asking `POST /auth/token` takes the owner key or a member's own key. Failed attempts are throttled per address
  (HTTP 429); wrong keys are logged and audited.
- `POST /tickets` takes the owner's interact token, or an active member's interact token for an allowlisted set of verbs
  run as that member. `POST /branches` takes only the owner's interact token and only the `branches` verbs.
- Secrets in streams are masked (token shapes, plus the Vault's values once loaded). See [Broadcast](#broadcast).
- Running a host for members on their own PCs is in [docs/REMOTE_HOST.md](REMOTE_HOST.md) and
  [docs/TAILSCALE.md](TAILSCALE.md).

## Broadcast

What it is for: choosing what the gateway shows remote viewers. Nothing is shared by default (M.ORCH-T935).

```
orchestrator broadcast                          every hosted session: shared, masked, paused, who is watching
orchestrator broadcast share|unshare <session|all>
orchestrator broadcast pause|resume <session|all>
orchestrator broadcast mask|unmask <session>
```

- `<session>` is a hosted session's id or the start of one (it must match exactly one). `all` works for share, unshare,
  pause and resume. `share all` means "share my sessions"; `pause all` stops every broadcast.
- Masking of secrets on remote streams is on by default. `unmask` turns it off for one session; the table shows it in red.
- The choices live in `broadcast.json`; a running gateway applies a change within a second. If the file can't be written,
  the command exits 1.
- A viewer still needs a grant (see the gateway). Sharing alone grants nothing.

## Milestones

What it is for: a named goal made of tickets plus acceptance checks. A milestone is reached only when every check passes
(M.ORCH-T1684). It reads the workspace's ticket database; with none it says there are no milestones. `<ID>` is `MS-n` or the
milestone's name.

```
orchestrator milestone [list] [--json]
orchestrator milestone forecast [<ID>] [--json]
orchestrator milestone check [<ID>]
orchestrator milestone accept <ID> --by <who> [--json]
orchestrator milestone pending [<ID>] [--json]
orchestrator milestone approve <ID> <ITEM> --by <who>
orchestrator milestone decline <ID> <ITEM> --by <who> [--reason <text>]
orchestrator milestone steward [<ID>] [--dry-run] [--json]
```

| Command | What it does |
|---|---|
| `list` (bare) | The active and accepting milestones: tickets done of total, checks passed of total, the date last projected. |
| `forecast` | Tickets and tokens left, a projected date from tickets closed per day, and an at-risk mark (the date slipped past its first forecast, or scope grows faster than it closes) (M.ORCH-T1688). |
| `check` | Runs the build and exists checks (every active and accepting milestone when no ID) and records each result with its evidence. Exits 1 when a check ran and failed. |
| `accept` | The owner asking for everything: moves an active milestone into acceptance, runs the build and exists checks, records its sign-off checks as passed by `--by`, and marks it reached only when every check passed. Never forces. |
| `pending` | Tickets and epics waiting to join an active or accepting milestone (M.ORCH-T1685). Its scope is locked, so each waits for the owner. |
| `approve` / `decline` | Answers one waiting addition. Approve makes it a member; decline stops it waiting (it never was a member). `--by` and `--reason` are recorded in the milestone's history. |
| `steward` | Files one ticket for each failed build or exists check that no open ticket covers and puts it forward for the milestone; lists closed tickets that came back or cite no evidence. `--dry-run` prints the plan and changes nothing (M.ORCH-T1686). |

Limits:

- Builds run in a worktree under the settings' `WorktreeRoot`, time out at the branch builds' limit
  (`BranchBuildsTimeoutMinutes`), and are held while RAM is low, the disk is under its floor or the heavy-process lease is
  taken. Main lines are fetched first.
- `check` runs only build and exists checks. Other kinds (sign-off) are recorded only by `accept`.
- The daemon also runs the checks and forecasts on its own tick; the inbox shows additions and growth waiting on you.

## Branches

What it is for: the remote-host flow, where agents on a host push `work/<ticket>` branches, the host builds them in
isolation, and you test them on your PC before they merge (M.ORCH-T1501, T1634 to T1637, T1661). The builds are off unless
the Vault's `RemoteBranchBuilds` is on. Details of the setup are in [docs/REMOTE_HOST.md](REMOTE_HOST.md).

```
orchestrator branches [--ready] [--returned] [--json]
orchestrator branches pushed <repo> <branch> <head>
orchestrator branches fetch [--show] [--json]
orchestrator branches test [<branch>...] [--repo <name>] [--list] [--timeout <min>] [--json]
orchestrator branches report <repo> <branch> <head> --pass|--fail [--note "<text>"] [--json]
orchestrator branches ff [<repo>...]
```

| Command | Where | What it does |
|---|---|---|
| bare | host | Each `origin/work/*` branch and its state: waiting to build, building, fails to build, built and ready to test, passed or failed its local run. `--ready` lists the built clean at a head not yet tested (the queue); `--returned` those whose run failed. |
| `pushed` | host | The agent's last step. Marks the branch's ticket as awaiting its test: it stays open, is not handed out again, and the host closes it when it merges the branch into main. A failed run, failed build or merge conflict hands it back. |
| `fetch` | PC | Pulls the host's ready queue through its gateway and keeps it here. Runs no test. The Scheduler job runs it every `BranchQueueFetchMinutes` (default 10) while `RemoteBranchBuilds` is on. `--show` prints the last fetch without asking the host. |
| `test` | PC | Runs the full suite of the queued branches (all, or the ones named by branch, `work/<name>` or ticket, or `--repo`), each at the head the host built, in a detached worktree so this checkout stays on main. Reports pass/fail per head back to the host. `--list` only lists. `--timeout` defaults to the main-line suite's limit. |
| `report` | either | Records a run. `<head>` is the full hash or at least 7 characters. A fail sends the branch back to its work unit with the `--note` to iterate on the same branch; no other branch waits on it. |
| `ff` | PC | After the host merged a signed-off branch into main: fast-forwards main in the named repos (every workspace repo when none). A checkout off main, or with local commits or changes in the way, is left as it is and named. |

Limits: `fetch`, `test` and `report` on a PC go through the gateway's `POST /branches`, which accepts only the owner's
interact token. Only the branches verbs are allowed there.

## Named agents

What it is for: giving an agent a stable id, a role, a login and a state, so mail (below) and later roadmap phases can
address it (M.ORCH-T1744, [docs/AGENTIC_ROADMAP.md](AGENTIC_ROADMAP.md) Phase 1). Making one starts no work.

```
orchestrator agents [list] [--all] [--role <role>] [--owner <email>] [--json]
orchestrator agents new <role> [--owner <email>] [--runtime claude] [--unit <ID>] [--id <agt-...>] [--force] [--json]
orchestrator agents pause <id>
orchestrator agents resume <id>
orchestrator agents retire <id>
```

- An id is `agt-` and 16 hex digits. States: idle, running, paused, terminated.
- `new` writes an idle agent. The role must be in the roles table or a file name under
  `mindattic-agent-standard\shared\roles` (analyst, architect, build-fixer, copywriter, designer, doc-writer, implementer,
  planner, release, researcher, reviewer); `--force` takes another. With no catalog found, it warns and skips the check.
- `--owner` is the login's account email. Default is this CLI's own login (`CLAUDE_CONFIG_DIR`); on the global login you
  must pass it. `--runtime` defaults to `claude`. `--id` reuses an id a started CLI already carries as `MINDATTIC_AGENT_ID`.
- `pause` works from idle or running; `resume` only from paused (back to idle); `retire` from any live state. A retired row
  is kept so the id still names it, and its unit is cleared. `list` hides retired agents unless `--all`.

The `/agent` skill (M.ORCH-T2065, `mindattic-agent-standard\shared\commands\agent.md`) is the same thing from inside a
CLI: `/agent <role> [--owner EMAIL]` runs `agents new`, `/agent list|pause|resume|retire <id>` run the rest, and a bare
`/agent` lists. It never adds `--force` unless you asked for a role outside the catalog.

## Mail

What it is for: durable, addressed handoffs between agents by their ids (M.ORCH-T1745).

```
orchestrator mail [list] [<agent>] [--all] [--everyone] [--limit N] [--json]
orchestrator mail send <agent> "<body>" [--unit <ID>] [--from <who>] [--force] [--json]
```

- `send` refuses an id that is not an agent (`--force` sends anyway). `--from` defaults to this CLI's agent
  (`MINDATTIC_AGENT_ID`), else its login, else `user`.
- `list` shows unread mail for this CLI's own agent, or everyone's when the CLI is not an agent. `--all` adds read mail,
  `--everyone` lists every agent's, `--limit` is 50 by default (newest first); bodies are cut at 80 characters in the table.
- Delivery: every 15 seconds the daemon writes each live agent's unread mail to its CLI, and the prompt hook adds it to the
  agent's next turn, then the messages are marked read. A file the hook dropped unread after 20 minutes is left again. An
  idle agent with mail also gets a wake queued (M.ORCH-T2067).
- Limit: delivery needs a running Claude CLI whose session names the agent. Mail for an agent with no live CLI waits.

## Inbox

What it is for: everything waiting on you, right now, each with the command that answers it (M.ORCH-T1689). Read-only.

```
orchestrator inbox [--json]
```

Item kinds: tickets to approve, tickets parked as waiting on owner, stale tickets, orphaned tickets (their CLI or tab went
dark mid-task), logins to sign in again, consent requests (on a member's PC, from the host), milestone additions and
milestone growth. The table columns are Kind, Item, Repo, Since, What, Do. `--json` writes kind, id, title, repo,
sinceUtc, action, ticketId, milestoneId and milestoneItem. With nothing waiting it prints "Nothing is waiting on you."
The daemon texts you through Psst when a new item appears, once per item.

## Health probes

What it is for: reading the machine, the daemon's links and Azure SQL, grading each reading, and publishing a health report
on the app bus. The contract, grades, wire format and reuse choices are in [docs/HEALTH.md](HEALTH.md); this is only the
command line.

```
orchestrator probe machine|links|azure [--once] [--json] [--interval <s>]
```

- `machine`: free RAM, commit charge, kernel handles and each fixed drive's free space.
- `links`: status stream and ticket-database latency, and the daemon's tick time.
- `azure` (alias `azure-sql`): each Azure SQL database's state, load and storage; `az` not signed in reads degraded.
- `--once` runs one check and exits with the Nagios code: 0 ok, 1 warn, 2 critical, 3 the probe could not run. Without it
  the probe loops until Ctrl+C. `--interval` is 1 to 86400 seconds, default the health sample setting (30). `--json`
  prints health+json instead of the summary line.
- A probe only reads and reports. Lines for degraded and unhealthy are the `orchestrator settings --health-*` settings.

## Dock, undock and quickload-all

What it is for: managing the dashboard's tabs for hosted CLIs without restarting them.

```
orchestrator undock [<session>]
orchestrator dock [<session>]
orchestrator quickload-all [--wait <seconds>]
```

- `undock` marks a hosted session undocked and asks any running dashboard to drop its tab. The CLI keeps running headless
  under its TermHost (M.ORCH-T1448). `dock` clears the mark and re-announces the session so the dashboard reattaches it
  now; otherwise it would notice within about 45 seconds. Neither kills or relaunches the CLI.
- `<session>` is a hosted session's id or the start of one (`orchestrator broadcast` lists them). With none, the command
  means this CLI's own session, from `MINDATTIC_TERM_ID`; a CLI not started through the Launcher has none and gets an
  error. The `/dock` and `/undock` skills call these with no argument.
- Both are best effort: with no dashboard running nothing happens and nothing is lost.
- `quickload-all` (M.ORCH-T2070, the `/quickload all` skill) asks the running dashboard to run Quick Load All: the newest
  save state restored, closed tabs reopened, and every CLI resumed on its repo's latest quicksave. It waits for the
  dashboard's report (`--wait`, 600 seconds by default) and prints how many CLIs resumed. Exit 1 when no dashboard runs, and
  2 when the report did not arrive in time (the dashboard keeps going).

## pace-watch

What it is for: seeing pacing's real effect on tool calls outside the chat. It prints the pace gate's calculated delay and
every hold or refusal as it happens.

```
orchestrator pace-watch [--interval <seconds>]
```

- The delay is `pace_delay_seconds` from `~\.claude\rl-status.json`, the number `pace-gate.py` sleeps for before each tool
  call. It prints when the value changes; "unknown (no daemon yet)" means the file is missing or unreadable.
- Held and refused gate events print with time, tool and account. Only holds of a second or more are logged.
- `--interval` defaults to 1 and is clamped to 1 to 60 seconds. Ctrl+C stops it. It changes nothing.
- The pacing model itself is in [docs/ORCHESTRATION.md](ORCHESTRATION.md).
