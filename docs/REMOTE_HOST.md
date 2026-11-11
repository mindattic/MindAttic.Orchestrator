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
  waiting for a local test run (`--json` for scripts). Branches exist only on the host: your PC stays on main. The local
  test run, its sign-off and the merge to main are still to come.
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
