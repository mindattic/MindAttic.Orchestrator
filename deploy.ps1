#Requires -Version 5.1
<#
.SYNOPSIS
    Builds MindAttic.Orchestrator from source and installs it the way a user would: the developer
    side of a drag-and-drop app.

.DESCRIPTION
    The app installs itself. MindAttic.Orchestrator.exe is one self-contained file: copy it anywhere
    and double-click it, and it installs to C:\Apps\MindAttic.Orchestrator (or per-user Programs when
    C:\Apps isn't writable), registers its logon daemon, makes Start menu and desktop shortcuts, finds
    the workspace (asking once if it must), and opens. Every later start repairs whatever of that is
    missing. So there is no setup script; this one only builds:

      artifacts\MindAttic.Orchestrator.exe   the drag-and-drop file (self-contained single file)
      C:\Apps\MindAttic.Orchestrator\Cli\    the orchestrator CLI, for scripting and the terminal

    then runs `artifacts\MindAttic.Orchestrator.exe --install`: exactly what a double-click does,
    minus the window. Installing over a running copy stops it (and its daemon) first; the daemon
    starts again from the new build.

    Without -Force each part is rebuilt only when a source file under its project or
    MindAttic.Orchestrator.Core (or Directory.Build.props) is newer than its output.

.PARAMETER Force
    Clean bin/obj and rebuild everything even if nothing looks changed.

.PARAMETER Launch
    Open the dashboard afterwards.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy.ps1
    powershell -ExecutionPolicy Bypass -File deploy.ps1 -Force -Launch
#>
param(
    [switch]$Force,
    [switch]$Launch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo       = $PSScriptRoot
$props      = Join-Path $repo 'Directory.Build.props'
$coreDir    = Join-Path $repo 'src\MindAttic.Orchestrator.Core'
$wpfDir     = Join-Path $repo 'src\MindAttic.Orchestrator.Wpf'
$cliDir     = Join-Path $repo 'src\MindAttic.Orchestrator.Cli'
$artifact   = Join-Path $repo 'artifacts\MindAttic.Orchestrator.exe'
$appDir     = 'C:\Apps\MindAttic.Orchestrator'
$cliOut     = Join-Path $appDir 'Cli'
$cliExe     = Join-Path $cliOut 'orchestrator.exe'
$sourceExts = @('.cs', '.csproj', '.xaml', '.json', '.props', '.targets', '.resx', '.manifest', '.ico', '.png')

function Test-Stale([string]$output, [string[]]$dirs) {
    if ($Force -or -not (Test-Path $output)) { return $true }
    $time = (Get-Item $output).LastWriteTimeUtc
    if ((Test-Path $props) -and (Get-Item $props).LastWriteTimeUtc -gt $time) { return $true }
    foreach ($dir in $dirs) {
        $newer = Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                ($sourceExts -contains $_.Extension.ToLowerInvariant()) -and
                $_.LastWriteTimeUtc -gt $time
            } | Select-Object -First 1
        if ($newer) { return $true }
    }
    return $false
}

function Stop-From([string]$name, [string]$dir) {
    foreach ($p in @(Get-Process -Name $name -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -and $_.Path.StartsWith($dir, [System.StringComparison]::OrdinalIgnoreCase) })) {
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop; [void]$p.WaitForExit(3000) } catch { }
    }
}

# Runs an installer and waits for it alone, returning its exit code. Not Start-Process -Wait: in Windows PowerShell
# 5.1 that waits for the installer's whole process tree, and --install starts the daemon (which never exits), so
# the deploy sat at 'Installing...' long after the install had finished (M.ORCH-T525).
function Invoke-Installer([string]$path, [string[]]$arguments) {
    $p = Start-Process -FilePath $path -ArgumentList $arguments -PassThru
    $null = $p.Handle   # held from the start, or ExitCode reads empty once the process has gone
    $p.WaitForExit()
    return $p.ExitCode
}

# What the install step does with the artifact: 'install'; 'newer' when the installed exe was built after it (another
# checkout installed a newer build: --install refuses the downgrade, so running it failed every deploy, M.ORCH-T554);
# 'current' when the installed copy is this build; 'none' when there's no artifact. -Force installs it anyway.
function Get-InstallStep([string]$artifactPath, [string]$installedPath, [bool]$builtNow, [bool]$forced) {
    if (-not (Test-Path $artifactPath)) { return 'none' }
    if (-not $installedPath) { return 'install' }
    if ($forced) { return 'install' }
    if ((Get-Item $installedPath).LastWriteTimeUtc -gt (Get-Item $artifactPath).LastWriteTimeUtc) { return 'newer' }
    # Also when the installed copy isn't this build: an install that failed last time is retried, not called up to date.
    if ($builtNow -or (Get-FileHash $artifactPath).Hash -ne (Get-FileHash $installedPath).Hash) { return 'install' }
    return 'current'
}

# Copies a published folder over an installed one without failing on files a running process holds (M.ORCH-T765: publishing
# straight into Cli\ failed with MSB3027 whenever a hook, /allocate or `orchestrator review` call had orchestrator.exe loaded).
# A loaded exe or DLL can't be overwritten but can be renamed, so a locked file is renamed aside (<name>.old-<id>) and the new
# one copied in; the aside copies are deleted here on a later deploy, once the processes holding them have exited.
function Remove-AsideFiles([string]$dir) {
    if (-not (Test-Path -LiteralPath $dir)) { return }
    foreach ($f in @(Get-ChildItem -LiteralPath $dir -Recurse -File -Filter '*.old-*' -ErrorAction SilentlyContinue)) {
        if ($f.Name -match '\.old-[0-9a-f]{8}$') { try { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop } catch { } }
    }
}

function Copy-Published([string]$from, [string]$to) {
    Remove-AsideFiles $to
    New-Item -ItemType Directory -Force $to | Out-Null
    $root = (Get-Item -LiteralPath $from).FullName.TrimEnd([char]92, [char]47)
    $pairs = @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; Target = (Join-Path $to $_.FullName.Substring($root.Length + 1)) }
    })
    # Pass 1: move every installed file a running process holds out of the way, before anything new lands. If one can't be
    # moved the moves are undone and this throws, so Cli\ stays the old, whole install.
    $moved = New-Object System.Collections.ArrayList
    try {
        foreach ($p in $pairs) {
            if (-not (Test-Path -LiteralPath $p.Target)) { continue }
            $locked = $false
            try { $fs = [System.IO.File]::Open($p.Target, 'Open', 'Write', 'None'); $fs.Dispose() } catch { $locked = $true }
            if (-not $locked) { continue }
            $aside = $p.Target + '.old-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
            Rename-Item -LiteralPath $p.Target -NewName (Split-Path $aside -Leaf) -ErrorAction Stop
            [void]$moved.Add(@($p.Target, $aside))
        }
    } catch {
        foreach ($m in $moved) { try { Move-Item -LiteralPath $m[1] -Destination $m[0] -ErrorAction Stop } catch { } }
        throw
    }
    # Pass 2: copy everything in. A file that fails to copy after being moved aside is put back; its name is never left empty.
    foreach ($p in $pairs) {
        New-Item -ItemType Directory -Force (Split-Path $p.Target) | Out-Null
        try { Copy-Item -LiteralPath $p.Source -Destination $p.Target -Force -ErrorAction Stop }
        catch {
            foreach ($m in $moved) { if ($m[0] -eq $p.Target -and -not (Test-Path -LiteralPath $m[0])) { try { Move-Item -LiteralPath $m[1] -Destination $m[0] -ErrorAction Stop } catch { } } }
            throw
        }
    }
}

function Find-Installed {
    @((Join-Path $appDir 'MindAttic.Orchestrator.exe'),
      (Join-Path $env:LOCALAPPDATA 'Programs\MindAttic.Orchestrator\MindAttic.Orchestrator.exe')) |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ($Force) {
    foreach ($d in @($coreDir, $wpfDir, $cliDir)) {
        foreach ($sub in 'bin', 'obj') {
            $path = Join-Path $d $sub
            if (Test-Path $path) {
                try { Remove-Item -Recurse -Force $path -ErrorAction Stop }
                catch { Write-Host "  (could not fully remove $path : $($_.Exception.Message))" -ForegroundColor DarkYellow }
            }
        }
    }
}

# ── Earlier layouts ─────────────────────────────────────────────────────────
# Dashboard\ came from the two-folder layout; that daemon ran Cli\orchestrator.exe.
Stop-From 'MindAttic.Orchestrator' (Join-Path $appDir 'Dashboard')
$oldDashboard = Join-Path $appDir 'Dashboard'
if (Test-Path $oldDashboard) { Remove-Item -Recurse -Force $oldDashboard; Write-Host "Removed the old $oldDashboard" -ForegroundColor DarkGray }

# ── The shortcuts' launcher ─────────────────────────────────────────────────
# Written before the install, so the app it starts points the desktop and Start menu shortcuts here (Installer.ShortcutTarget):
# every open redeploys when the source is newer, then opens the dashboard. With the checkout gone it opens the installed exe.
$launcher = Join-Path $appDir 'deploy.bat'
$launcherText = @"
@echo off
title MindAttic Orchestrator
rem Generated by $repo\deploy.ps1 - edits here are overwritten.
set "DEPLOY_PS1=$repo\deploy.ps1"
if not exist "%DEPLOY_PS1%" (
    echo Source repo not found at "%DEPLOY_PS1%" - opening the installed copy without redeploying.
    start "" "%~dp0MindAttic.Orchestrator.exe"
    exit /b 0
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_PS1%" -Launch
if errorlevel 1 (
    echo Redeploy failed - the installed copy was opened instead.
    pause
)
"@
New-Item -ItemType Directory -Force $appDir | Out-Null
if (-not (Test-Path $launcher) -or (Get-Content -Raw $launcher) -ne $launcherText) {
    Set-Content -LiteralPath $launcher -Value $launcherText -Encoding ascii -NoNewline
    Write-Host "Wrote $launcher" -ForegroundColor DarkGray
}

$failed = $false

# ── The drag-and-drop exe ───────────────────────────────────────────────────
$built = $false
if (Test-Stale $artifact @($wpfDir, $coreDir)) {
    Write-Host "Publishing MindAttic.Orchestrator -> $artifact" -ForegroundColor Cyan
    dotnet publish (Join-Path $wpfDir 'MindAttic.Orchestrator.Wpf.csproj') --configuration Release --runtime win-x64 `
        --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=true /p:DebugType=None --output (Split-Path $artifact) | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $artifact)) { Write-Host 'Publishing the app FAILED.' -ForegroundColor Red; $failed = $true }
    else { $built = $true; Write-Host ("Built {0} ({1:N0} MB)" -f $artifact, ((Get-Item $artifact).Length / 1MB)) -ForegroundColor Green }
}

# ── The CLI ─────────────────────────────────────────────────────────────────
if (Test-Stale $cliExe @($cliDir, $coreDir)) {
    # Published to a staging folder, then swapped in file by file: a running CLI call (hooks, /allocate, review) holds its
    # DLLs and must neither fail the deploy nor leave Cli\ half-installed (M.ORCH-T765).
    $cliStage = Join-Path $appDir 'Cli.staging'
    if (Test-Path $cliStage) { Remove-Item -Recurse -Force $cliStage }
    Write-Host "Publishing the CLI -> $cliOut" -ForegroundColor Cyan
    dotnet publish (Join-Path $cliDir 'MindAttic.Orchestrator.Cli.csproj') --configuration Release --output $cliStage | Out-Host
    $swapFailed = $false
    if ($LASTEXITCODE -eq 0) {
        try { Copy-Published $cliStage $cliOut } catch { Write-Host "Swapping in the CLI failed: $($_.Exception.Message)" -ForegroundColor Red; $swapFailed = $true }
    }
    Remove-Item -Recurse -Force $cliStage -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0 -or $swapFailed) { Write-Host 'Publishing the CLI FAILED.' -ForegroundColor Red; $failed = $true }
    else {
        # The apphost keeps its old time when only Core's method bodies changed (the CLI isn't recompiled), so Core would
        # read newer than it on every later deploy, which would republish it and stop running CLIs each time.
        (Get-Item $cliExe).LastWriteTimeUtc = [DateTime]::UtcNow
        Write-Host "Deployed $cliExe" -ForegroundColor Green
    }
}

# ── Install, as a double-click would ────────────────────────────────────────
$installed = Find-Installed
$step = Get-InstallStep $artifact $installed $built ([bool]$Force)
if ($step -eq 'newer') {
    Write-Host ("The installed {0} ({1:yyyy-MM-dd HH:mm}) is newer than this build ({2:yyyy-MM-dd HH:mm}), so it's left as it is; -Force installs this build over it." -f
        $installed, (Get-Item $installed).LastWriteTime, (Get-Item $artifact).LastWriteTime) -ForegroundColor DarkGray
} elseif ($step -eq 'install') {
    Write-Host 'Installing (stops a running copy and its daemon; the daemon restarts from the new build)...' -ForegroundColor Cyan
    # An old-layout daemon still running from the CLI folder; only it: a CLI call in flight (an /autopilot CLI's
    # `orchestrator next --claim`) is left to finish, as a deploy that doesn't republish the CLI must.
    Get-CimInstance Win32_Process -Filter "Name='orchestrator.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($cliOut, [System.StringComparison]::OrdinalIgnoreCase) -and $_.CommandLine -match '\sdaemon\b' } |
        ForEach-Object { try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { } }
    $exitCode = Invoke-Installer $artifact $(if ($Force) { @('--install', '--force') } else { @('--install') })
    $installed = Find-Installed
    if ($exitCode -eq 0 -and $installed) { Write-Host "Installed $installed" -ForegroundColor Green }
    else { Write-Host 'Install FAILED; see its log, %APPDATA%\MindAttic\Logging\MindAttic.Orchestrator\MindAttic.Log.db (MindAttic.Log.Reader opens it).' -ForegroundColor Red; $failed = $true }
} elseif (-not $failed) {
    Write-Host 'MindAttic.Orchestrator is up to date.' -ForegroundColor DarkGray
}

if ($Launch -and $installed) { Start-Process -FilePath $installed }

if ($failed) { Write-Host 'Deploy FAILED.' -ForegroundColor Red; exit 1 }
exit 0
