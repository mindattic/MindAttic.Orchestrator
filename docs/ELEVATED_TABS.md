# Elevated (Administrator) CLI tabs

M.ORCH-T1656 (for M.LNCH-T125's "Open Command Prompt (Admin)" and "Open PowerShell (Admin)").

## How a tab gets elevated

The dashboard never runs elevated, so an elevated tab always runs under a TermHost:

1. A `PaneRequest` with `Elevated = true` becomes a `TermLaunchSpec` with `Elevated = true`. `TerminalsViewModel.Open`
   refuses it when no TermHost is available rather than running it unelevated in-process.
2. `TermLauncher.StartElevated` shell-executes `MindAttic.Orchestrator.TermHost.exe --launch ...` with the `runas` verb,
   which shows the UAC prompt. The arguments are the same ones an unelevated launch passes.
3. The elevated `--launch` process respawns the host detached (the normal `SpawnDetached` path). The respawn inherits the
   elevated token, so the long-lived host and the CLI on its ConPTY are elevated too.
4. `ShellExecuteEx` can't redirect stdout, so the `{"id","pid","pipe"}` line isn't read. The dashboard waits on the session
   registry instead, for the Vault setting **Elevated tab start wait (s)** (`ElevatedTermWaitSeconds`, default 120). A
   declined prompt (Win32 1223) is reported as "Elevation was cancelled".
5. An elevated spec can't carry `Environment` variables, because `UseShellExecute` can't set them. Nothing is put on the
   command line in their place.

The unelevated launch path (`TermLauncher.Start`, `CurrentUserPipeFactory`) is unchanged.

## The elevated host's pipe

An unelevated host uses .NET's `PipeOptions.CurrentUserOnly`. That option doesn't work for an elevated host, for three
reasons:

- In an elevated token, the *owner* SID is `BUILTIN\Administrators`, so the pipe would be owned by, and grant access only
  to, Administrators. The dashboard's unelevated token holds Administrators only as a deny-only group.
- The viewer's own `CurrentUserOnly` client check requires the pipe's owner to be its token owner (the user), so it would
  refuse the pipe.
- The pipe's default High mandatory label blocks a Medium caller from opening it for write (no-write-up).

So an elevated host creates every pipe instance with `ElevatedPipeFactory`, which applies its security descriptor at creation:

```
O:<user>G:<user>D:P(A;;GA;;;<user>)S:(ML;;NW;;;ME)
```

- **Owner and group**: the user's own SID (the token *user*, never the token owner).
- **DACL**: protected, with one ACE: the user's SID, full access. There are no Administrators, SYSTEM, Everyone or
  logon-session ACEs. The elevated host runs as that same user SID, so it needs nothing more, and no other account (another
  admin included) can open the pipe. Remote clients are rejected (`PIPE_REJECT_REMOTE_CLIENTS`).
- **Label**: Medium with no-write-up. The ticket asked for a low-integrity label, but the viewer it needs to let in runs at
  Medium, and that is enough. A Low label would also let a Low-integrity process of this user (a sandboxed browser or
  document renderer) type into an Administrator shell.

## The trade-off

While an elevated tab is open, any unelevated process running as this user can attach to its pipe and type into an
Administrator shell. That's the purpose of the feature (the dashboard is one such process), but it removes, for that one
session, the barrier UAC normally puts between a user's Medium and High processes. Microsoft doesn't treat UAC as a
security boundary, but the prompt does add friction, and an open elevated tab takes it away from anything already running
as the user. The exposure is limited to one user, one session and that session's lifetime. Other users, Low-integrity
processes and remote clients are still refused, and every unelevated tab keeps today's pipe. Elevated tabs open only on an
explicit ask (the Launcher's "(Admin)" items).

Known gap: an elevated host's own bus announcement (`TermBus`, a `CurrentUserOnly` client of the dashboard's inbox) fails
the same owner check in reverse. The dashboard picks up the tab from the registry reread instead (`TermResyncSeconds`).
