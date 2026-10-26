# T923 relay prototype (M.ORCH-T923)

Phase 0 of the spike in `docs/spikes/cli-host-viewer.md` (M.ORCH-T922). A throwaway: it is not in
`MindAttic.Orchestrator.slnx` and touches nothing of the real app.

- `TermHostStub` is a console exe. It owns one CLI on a ConPTY, keeps a 4 MB replay ring and the sticky terminal modes,
  and serves them on the named pipe `mindattic-termproto-<user>-<id>` (current user only, one viewer at a time).
- `RelayViewer` is a WPF exe: a bare `Microsoft.Terminal.Wpf` `TerminalControl` fed by a `RelayConnection`
  (`ITerminalConnection`) over that pipe. Ctrl+V and Shift+Insert paste through the app's `TerminalPaste.Payload`.

## Build

```powershell
cd D:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T923\prototypes\T923-relay
dotnet build TermHostStub\TermHostStub.csproj
dotnet build RelayViewer\RelayViewer.csproj
```

## Run

Start the host detached (prints its pid and returns; the CLI keeps living without any viewer):

```powershell
.\TermHostStub\bin\Debug\net10.0-windows\TermHostStub.exe --launch --id p1 -- claude
```

Other options: `--cwd <dir>` (the CLI's working directory), `--linger <seconds>` (how long the host stays after the CLI
exits; default 60). With no command after `--` the host runs `pwsh`. Its log is
`%LOCALAPPDATA%\MindAttic\termproto\p1.log`.

Open the viewer, type `p1` in the Session id box and press Connect (Disconnect closes the pipe and leaves the host running):

```powershell
.\RelayViewer\bin\Debug\net10.0-windows\RelayViewer.exe
```

## Manual acceptance checklist

1. Launch the host with claude, open the viewer, connect.
2. Typing, arrows and Home/End reach Claude. `/status` can be navigated with Left and Right. Esc and Ctrl+C reach Claude.
3. A multi-line paste arrives bracketed and doesn't submit early.
4. Resizing makes Claude reflow, and cols x rows match.
5. Close the viewer mid-answer, reopen it and connect: the screen is restored, the answer kept going, and the half-typed
   input is still there.
6. `taskkill /F` the viewer: the host pid is still alive, and a new viewer reattaches.
7. Exit claude: the viewer shows the exit, and the host leaves after its linger time.

## Protocol

`u8 kind | u32 length (LE) | payload`. Host to viewer: Replay (cols, rows, mode flags, ring data, then re-asserted modes),
Live, Output, Modes, Exit. Viewer to host: Hello (cols, rows), Input, Resize. The exact layouts are in
`TermHostStub\Protocol.cs`.
