# Running the Orchestrator on another machine

The Orchestrator, its daemon, every Claude CLI it starts, their builds and their memory can live on a separate Windows 11
machine (a Hyper-V VM, a hosted desktop, a spare PC) instead of your own PC (M.ORCH-T1473). Your PC keeps a clone of the
repos and only pulls: the agents on the host push to `main`, and `orchestrator machine pull` brings you the newest code to
try.

Each machine has a role, kept in `%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\machine.json` (never in
`orchestrator.db`, which git carries between machines). `MINDATTIC_ORCHESTRATOR_ROLE` (`host`, `client`, `standalone`)
overrides it.

| Role | Runs | Writes the committed databases |
| --- | --- | --- |
| standalone (the default) | everything, as before | yes |
| host (the VM) | the daemon, every CLI, the gateway on the network | yes: it's the one writer |
| client (your PC) | nothing: no daemon, no pace hooks, no gateway; the dashboard is the host's Team Wall | never |

## Moving to a VM: three steps

### 1. On your PC: hand off

Pull and deploy the newest MindAttic.Orchestrator (`run.ps1`), then:

```powershell
orchestrator machine handoff --kit E:\MindAttic-HostKit
```

This:

- copies the orchestrator's and the ticket database's data out of SQL Server into the committed SQLite files
  (`orchestrator db back`, `tickets db server back`) and pushes them, so the host starts from everything your PC knew;
- writes the **host kit**: `C:\LocalNuGet`'s packages (every MindAttic app restores from them, and they exist only on your
  PC), your app settings without secrets, `setup-host.ps1`, `dev-environment.json` (the curated dev tools, M.ORCH-T1489),
  `Set up this machine.cmd` (double-click it on the new machine) and `kit.json` (your git identity, the workspace);
- makes your PC a client: stops the daemon, dashboard and gateway (CLIs already running keep going), takes the pace hooks
  out of `~/.claude/settings.json`, and points the ticket tools at a machine-local mirror of the host's ticket database
  (`MINDATTIC_TICKETS_DB`).

Do it before setting up the host. If the host's daemon started first, its own database commits and your PC's handoff
commits would conflict.

### 2. On the VM: set it up

Create a Windows 11 VM (Pro, so Remote Desktop works; a local account is simplest; 16 GB of RAM or more and a disk with room
for every repo). Adding a second virtual disk for `D:` is best, but not required. Copy the kit folder onto it (the Hyper-V
enhanced session pastes files), then double-click `Set up this machine.cmd` in it, or from a **normal** PowerShell (not
administrator):

```powershell
powershell -ExecutionPolicy Bypass -File E:\MindAttic-HostKit\setup-host.ps1 -Kit E:\MindAttic-HostKit
```

It asks for one UAC prompt, your GitHub sign-in (browser), the Windows password for automatic sign-in after restarts (kept
as an LSA secret by Sysinternals Autologon), and the Claude sign-ins. Then it:

- gives the workspace a `D:` (a blank extra disk, else `C:\MindAttic-D` mapped as `D:` at every boot, since
  `D:\Projects\MindAttic` is built into every app);
- installs git, the GitHub CLI, the .NET 10 SDK, Node.js, Python 3, PowerShell 7, Windows Terminal and Claude Code;
- installs SQL Server 2025 Developer as `localhost` (unless a SQL Server is already there; you and Administrators are
  sysadmin), SQL Server 2025 LocalDB and SQL Server Management Studio 22 (M.ORCH-T1486; `-SkipSql` skips them), then
  restores the backups in `-Databases` (default: the `databases` folder beside the kit; `localhost\*.bak` onto SQL Server,
  `localdb\*.bak` onto LocalDB, any database already there left alone). 2025, because your PC's LocalDB is already 2025 and
  a backup only restores onto the same version or newer;
- restarts Windows by itself when SQL Server setup needs it (at most 3 times) and carries on after the automatic sign-in,
  with no second UAC prompt;
- installs the dev environment in `dev-environment.json` (M.ORCH-T1489): Visual Studio 2026 and 2022 with their workloads,
  Unity Hub and the Unity 6 editor, the Epic launcher, Notepad++, VS Code, ArtCraft, the .NET/Node/Python/Rust/Java SDKs,
  the database tools, DirectX and the redistributables (`-SkipDevEnvironment` skips it; edit the file to change the list);
- fills `C:\LocalNuGet` from the kit, turns sleep off and Remote Desktop on, and opens the gateway's port (TCP 7443);
- clones the workspace and every repo, builds and installs Vault, Tickets, Launcher, Orchestrator and Scheduler, and
  installs the agent standard's global commands and commit hooks;
- runs `orchestrator machine host`, which makes it the host and prints one line for your PC (also saved on the VM's desktop
  as `Connect your PC to this host.txt`).

Every step checks first, so running it again repairs a half-finished host: the desktop icon it leaves,
`Rebuild MindAttic dev environment`, runs it again with one double-click. The log is `C:\MindAttic-Setup\setup-host.log`.

### 3. On your PC: connect

Run the line the host printed:

```powershell
orchestrator machine connect mindattic-vm --pin <sha256> --owner-key <key>
```

The dashboard is now the host's Team Wall: every CLI on the VM, live (the host shares every session with you, `orchestrator
broadcast share all`). The owner key is sealed for your Windows user (DPAPI) and mints a fresh gateway token whenever the
old one runs low, so the link never expires. For the full dashboard, Remote Desktop to the VM (`mstsc /v:mindattic-vm`);
closing Remote Desktop leaves everything running.

`tickets` on your PC now runs on the host (M.ORCH-T1479): `connect` puts a `tickets` shim first on your user PATH
(`tickets.cmd` for cmd and PowerShell, and a script for Git Bash, which Claude Code's Bash tool uses). It hands every command
to the host's gateway, which runs the real tickets CLI there, in the same workspace folder you typed it in. So a ticket you
or a local Claude CLI file on the PC lands in the host's database at once, and every read is the host's, live. A
`--description-file` is read on the PC and sent along. Tickets Desktop on the PC shows the mirror.

## Every day

- **Newest code:** `orchestrator machine pull` pulls every repo in the workspace and refreshes the ticket mirror.
- **Where am I:** `orchestrator machine` shows this machine's role, the daemon, the data it uses and, on the host, the
  connect line.
- **Work:** type `/allocate` in a CLI on the host (through the Team Wall or Remote Desktop).
- **Restarts:** the VM signs itself in, and Scheduler starts the daemon, which starts the gateway. Your PC reconnects on its
  own.
- **Work branches (in progress, off by default; M.ORCH-T1501):** with the Vault setting *Build work branches (remote host)*
  on, the host's daemon builds each `origin/work/*` branch in a worktree of its own (at most *Work-branch builds at once*),
  apart from the others and from main, and `orchestrator branches` shows each one; `--ready` lists the ones built clean and
  waiting for a local test run (`--json` for scripts). Branches exist only on the host: your PC stays on main.
  `orchestrator branches report <repo> <branch> <head> --pass|--fail [--note "<what failed>"]` records a local run of one
  head (M.ORCH-T1637). A failing run sends the branch back to the work unit that produced it, the ticket its name carries
  (`work/<ticket>`): the ticket gets a note with the failures and is handed out again, to keep iterating on that same
  branch, while every other branch goes on building and being tested. Once it pushes a new head, the host builds it and
  queues it for a local run again; `--returned` lists the branches back with their work unit. A passing run signs that
  head off (M.ORCH-T1636): at its next look the host merges it into main by itself, in a worktree of its own
  (`<WorktreeRoot>\<repo>\merge-main`, never the repo's checkout), pushes main and deletes the branch on origin (kept if it
  moved after the sign-off; a head that moved isn't merged, it needs its own run). A merge that conflicts with main is
  logged and not tried again until a new head is signed off. Then `orchestrator branches ff [<repo>...]` on your PC
  fast-forwards main (only a checkout on main, only a fast-forward). `orchestrator branches test [<branch>...] [--repo
  <name>]` on your PC (M.ORCH-T1635) runs the full suite of some or all queued branches, one at a time, each at the head
  the host built, in a detached worktree (`<WorktreeRoot>\<repo>\local-tests`; your checkout stays on main), and reports
  pass or fail per head back to the host through its gateway (`POST /branches`, the owner's token only), the same record
  `branches report` writes; a run that couldn't complete isn't reported, and `--list` only shows what would run. The PC's
  periodic fetch job is still to come.
- **Back to one machine:** `orchestrator machine standalone` (and `orchestrator db migrate` to go back to SQL Server).

## Limits

- **Tickets are the host's.** The `tickets` command reaches the host through the gateway, so it needs the VM on. Anything
  that opens the ticket database directly on the PC (Tickets Desktop, the libraries) reads the mirror each pull refreshes,
  and an edit made there is kept beside the mirror (and said) but doesn't reach the host: make edits through `tickets` or on
  the host.
- **Secrets stay put.** Vault's keys and the year-long Claude tokens are sealed with DPAPI for one Windows user on one
  machine, so they can't be copied: setup signs the Claude logins in again, and keys the host needs (API key, Psst) are
  entered once in Vault.Desktop there.
- **Reaching the VM.** The gateway listens on every network by default. Across the internet, use a VPN such as Tailscale and
  `setup-host.ps1 -Bind <its 100.x address>`; the certificate already names the VM's addresses, and the client pins it.
- **The host uses SQLite.** The VM keeps the orchestrator's and the ticket data in the committed SQLite files, synced by its
  daemon, and needs no SQL Server.

## Org members on the host (hub phase 1)

Other org members can connect their own PCs to the same host (M.ORCH-T1640; the plan is
`docs/research/hub-and-spoke.md`). Each gets a key of their own, never the owner key, and connects as themselves.

On the host:

```powershell
orchestrator machine invite erin        # any way of naming an active member: email, name, initials...
orchestrator machine members            # who is invited, when the gateway last saw them, who is connected now
orchestrator machine revoke erin        # cut them off at once
```

`invite` needs an active member (`orchestrator members`), mints their member key (only its SHA-256 is kept, in the
gateway's `members.json`; a new invite replaces an older key) and prints the line for their PC:

```powershell
orchestrator machine connect mindattic-vm --pin <sha256> --member erin@example.com --member-key mk1_...
```

Send it to them privately. On their PC it works like the owner's connect: the PC becomes a client (no daemon, no pace
hooks; its own data is not pushed anywhere), the key is sealed with DPAPI and mints short-lived tokens whose subject is
the member's email, and `tickets` runs on the host through the shim.

- **Watch only.** Their Team Wall lists the host's shared CLIs of their own org (same-org watch, the existing gateway
  ACL); a member of another org sees none. Nobody types into anyone's CLI yet.
- **Tickets as themselves.** Their `tickets` may run `ticket show`, `list`, `new`, `note` and `close`, and `close` only
  on a ticket one of their own logins works or is assigned. A note's author is their email. Options that reach a file on
  the host (`--root`, `--output`, `--description-file`) or name another author are refused, and so is every other verb
  (approvals, priorities, assignments, the database): those stay the owner's. Each call is in the gateway audit
  (`orchestrator gateway audit`), per member, by verb, never its text.
- **Revoking** takes effect within a second: their key and every token minted for them are refused, and their open
  streams close (1008) within 10 seconds.
- The host's dashboard shows a connected-members count in its menu bar; the Vault's *Member counts as connected for
  (min)* sets how long after the gateway last saw a member they still count.

## Members working their own CLIs on the host (hub phase 2)

Off by default (M.ORCH-T1641). With the Vault's *Members work their own CLIs (host)* (`HubMemberSessions`) off, or on
any machine that isn't the host, nothing in this section exists: the gateway decides exactly as in phase 1, and its new
routes answer 404. Turn it on, on the host, and:

- **A CLI's owner is the member whose Claude login it runs on** (their email, or a login `orchestrator orgs member`
  placed under their name). Their Team Wall lists their own CLIs even outside their org, with an `owner` field.
- **Typing needs isolation.** A member types into a CLI on the host only when it runs under their own Windows account
  (below), or when the Vault's *Trusted org* (`HubTrustedOrg`) says the org accepts that every CLI runs as the host's
  one Windows user. That user can read every login's `.credentials.json` and decrypt every DPAPI secret on the host, so
  under "trusted org" anyone who can type into a CLI can reach all of them. Without either, members watch, their own
  CLIs included. The same rule caps the owner's interact grants: on a CLI that may not be typed into, a grant counts as
  watch.
- **Consent.** The host owner's interact grant (`orchestrator gateway grant <who> interact`) on a *member's* CLI takes
  effect only once that member says yes. The viewer's first attach asks them; it shows as a toast on their dashboard
  (every *Consent check every (s)*), and `orchestrator machine consents` lists the waiting requests. They answer with
  `orchestrator machine consent <id> allow|deny`. No answer within *Consent wait (s)* (30 by default) leaves the viewer
  watching; their next attach asks again. Once allowed, the viewer's next attach types. An answer lasts as long as that
  grant (a new or extended grant is asked about again) and is held in the gateway's memory, so a restarted gateway asks
  again. A grant on a CLI no member owns needs no consent (the owner made it on their own CLI).
- **Start and stop from the member's PC:** `orchestrator machine start <repo> [--login <email>]` starts a CLI on one of
  their own logins (never another member's; the host runs `orchestrator start <repo> --account <login>`), and
  `orchestrator machine stop <session>` stops one of their own. Both need isolation or trusted org, as typing does.
- **No deflate on interactive streams** while it's on: typed input beside secrets on one compressed stream is the
  CRIME/BREACH shape. Watch streams stay compressed.
- Every start, stop, consent request and answer is in the gateway audit, by who and what, never content.

### Per-member Windows users

With *Per-member Windows users (host)* (`HubMemberWindowsUsers`) on as well, each member's CLIs are meant to run under
their own Windows account: own profile, own DPAPI scope, own `%USERPROFILE%\.claude-accounts`. The Orchestrator never
creates, changes or deletes a Windows account, group, ACL or credential; an admin does the following once per member, on
the host, by hand:

1. Make the account, a standard (not admin) local user with a strong password, for example in an elevated PowerShell:
   `New-LocalUser -Name erin-cli -FullName "Erin (Orchestrator CLIs)" -Password (Read-Host -AsSecureString)`. Don't add
   it to Administrators or Remote Desktop Users.
2. Give it read and execute on the workspace and the installed tools, and modify on that member's own repos only, for
   example `icacls D:\Projects\MindAttic /grant "erin-cli:(OI)(CI)RX"`, then `(OI)(CI)M` on the repos they work. Never
   on another member's `.claude-accounts` folder, the gateway's data folder or `owner.key`.
3. Sign in once as that account (or `runas /profile /user:erin-cli cmd`) so Windows makes its profile, then sign its
   Claude login in there with `claude setup-token`, so the token lives in that profile, sealed with that account's DPAPI.
4. Record the mapping: `orchestrator machine isolate erin erin-cli` (it only stores the name and looks the account up;
   `--clear` forgets it). `orchestrator machine isolate` refuses the host's own user, which would isolate nothing.

The daemon then lists, in the gateway's snapshot, each invited member whose account exists; one whose account is missing
gets no typing and no starting, never a fallback to the host's user.

**Not built yet.** Launching a TermHost under the member's account (it needs that account's credential or a small
service, and its named pipe ACL'd to that account and the daemon) is left out of this build: every CLI still runs as the
host's user, so per-member isolation alone never lets a member type, and `machine start` answers that starting under
their own account isn't built (501). Until it is, use *trusted org* only where it's true. The remaining piece is its own
ticket.
