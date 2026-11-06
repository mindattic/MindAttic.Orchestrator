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
  PC), your app settings without secrets, `setup-host.ps1` and `kit.json` (your git identity, the workspace);
- makes your PC a client: stops the daemon, dashboard and gateway (CLIs already running keep going), takes the pace hooks
  out of `~/.claude/settings.json`, and points the ticket tools at a machine-local mirror of the host's ticket database
  (`MINDATTIC_TICKETS_DB`).

Do it before setting up the host. If the host's daemon started first, its own database commits and your PC's handoff
commits would conflict.

### 2. On the VM: set it up

Create a Windows 11 VM (Pro, so Remote Desktop works; a local account is simplest; 16 GB of RAM or more and a disk with room
for every repo). Adding a second virtual disk for `D:` is best, but not required. Copy the kit folder onto it (the Hyper-V
enhanced session pastes files), then from a **normal** PowerShell (not administrator):

```powershell
powershell -ExecutionPolicy Bypass -File E:\MindAttic-HostKit\setup-host.ps1 -Kit E:\MindAttic-HostKit
```

It asks for one UAC prompt, your GitHub sign-in (browser), the Windows password for automatic sign-in after restarts (kept
as an LSA secret by Sysinternals Autologon), and the Claude sign-ins. Then it:

- gives the workspace a `D:` (a blank extra disk, else `C:\MindAttic-D` mapped as `D:` at every boot, since
  `D:\Projects\MindAttic` is built into every app);
- installs git, the GitHub CLI, the .NET 10 SDK, Node.js, Python 3, PowerShell 7, Windows Terminal and Claude Code;
- fills `C:\LocalNuGet` from the kit, turns sleep off and Remote Desktop on, and opens the gateway's port (TCP 7443);
- clones the workspace and every repo, builds and installs Vault, Tickets, Launcher, Orchestrator and Scheduler, and
  installs the agent standard's global commands and commit hooks;
- runs `orchestrator machine host`, which makes it the host and prints one line for your PC (also saved on the VM's desktop
  as `Connect your PC to this host.txt`).

Every step checks first, so running it again repairs a half-finished host. The log is `C:\MindAttic-Setup\setup-host.log`.

### 3. On your PC: connect

Run the line the host printed:

```powershell
orchestrator machine connect mindattic-vm --pin <sha256> --owner-key <key>
```

The dashboard is now the host's Team Wall: every CLI on the VM, live. The owner key is sealed for your Windows user (DPAPI)
and mints a fresh gateway token whenever the old one runs low, so the link never expires. For the full dashboard, Remote
Desktop to the VM (`mstsc /v:mindattic-vm`); closing Remote Desktop leaves everything running.

## Every day

- **Newest code:** `orchestrator machine pull` pulls every repo in the workspace and refreshes the ticket mirror.
- **Where am I:** `orchestrator machine` shows this machine's role, the daemon, the data it uses and, on the host, the
  connect line.
- **Work:** type `/allocate` in a CLI on the host (through the Team Wall or Remote Desktop).
- **Restarts:** the VM signs itself in, and Scheduler starts the daemon, which starts the gateway. Your PC reconnects on its
  own.
- **Back to one machine:** `orchestrator machine standalone` (and `orchestrator db migrate` to go back to SQL Server).

## Limits

- **Tickets are the host's.** On a client, the ticket tools read a mirror of the host's database that each pull replaces.
  A ticket written on your PC is kept beside the mirror and said, but it doesn't reach the host: file tickets in a host
  CLI. Writing straight to the host's tickets from the PC is MindAttic.Tickets' part of this.
- **Secrets stay put.** Vault's keys and the year-long Claude tokens are sealed with DPAPI for one Windows user on one
  machine, so they can't be copied: setup signs the Claude logins in again, and keys the host needs (API key, Psst) are
  entered once in Vault.Desktop there.
- **Reaching the VM.** The gateway listens on every network by default. Across the internet, use a VPN such as Tailscale and
  `setup-host.ps1 -Bind <its 100.x address>`; the certificate already names the VM's addresses, and the client pins it.
- **The host uses SQLite.** The VM keeps the orchestrator's and the ticket data in the committed SQLite files, synced by its
  daemon, and needs no SQL Server.
