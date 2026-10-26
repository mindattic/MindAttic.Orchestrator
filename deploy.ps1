#Requires -Version 5.1
<#
.SYNOPSIS
    Builds MindAttic.Orchestrator from source and installs it the way a user would: the developer
    side of a drag-and-drop app.

.DESCRIPTION
    The app installs itself. MindAttic.Orchestrator.Desktop.exe is one self-contained file: copy it anywhere
    and double-click it, and it installs to C:\Apps\MindAttic.Orchestrator (or per-user Programs when
    C:\Apps isn't writable), registers its logon daemon, makes Start menu and desktop shortcuts, finds
    the workspace (asking once if it must), and opens. Every later start repairs whatever of that is
    missing. So there is no setup script; this one only builds:

      artifacts\MindAttic.Orchestrator.Desktop.exe   the drag-and-drop file (self-contained single file)
      C:\Apps\MindAttic.Orchestrator\Cli\    the orchestrator CLI, for scripting and the terminal

    then runs `artifacts\MindAttic.Orchestrator.Desktop.exe --install`: exactly what a double-click does,
    minus the window. Installing over a running copy stops it (and its daemon) first; the daemon
    starts again from the new build.

    Without -Force each part is rebuilt only when a source file under its project or
    MindAttic.Orchestrator.Core (or Directory.Build.props) is newer than its output.

    Installing restarts the dashboard, and every CLI its Terminals panels host loses its state. So before the install stops
    anything it asks `orchestrator busy` whether any Claude CLI on this machine (any login) is still working: mid-turn, a
    subagent running, an /allocate claim in flight, or holding on a prompt. If any is, or its state can't be read, the deploy
    lists them and exits 1 without stopping anything (M.ORCH-T873). The CLI running this deploy is left out.

.PARAMETER Force
    Clean bin/obj and rebuild everything even if nothing looks changed.

.PARAMETER Launch
    Open the dashboard afterwards.

.PARAMETER IgnoreBusyClis
    Install even while other Claude CLIs are still working; they lose their state.

.PARAMETER WaitForIdle
    Instead of refusing while CLIs are working, poll until every one is idle, then install.

.PARAMETER WaitTimeoutMinutes
    With -WaitForIdle: give up (and refuse) after this many minutes; 0 waits for as long as it takes.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File deploy.ps1
    powershell -ExecutionPolicy Bypass -File deploy.ps1 -Force -Launch
    powershell -ExecutionPolicy Bypass -File deploy.ps1 -WaitForIdle
#>
param(
    [switch]$Force,
    [switch]$Launch,
    [switch]$IgnoreBusyClis,
    [switch]$WaitForIdle,
    [int]$WaitTimeoutMinutes = 0,
    [int]$BusyPollSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo       = $PSScriptRoot
$props      = Join-Path $repo 'Directory.Build.props'
$coreDir    = Join-Path $repo 'src\MindAttic.Orchestrator.Core'
$wpfDir     = Join-Path $repo 'src\MindAttic.Orchestrator.Wpf'
$cliDir     = Join-Path $repo 'src\MindAttic.Orchestrator.Cli'
$termDir    = Join-Path $repo 'src\MindAttic.Orchestrator.Term'
$termHostDir = Join-Path $repo 'src\MindAttic.Orchestrator.TermHost'
$termWpfDir = Join-Path $repo 'src\MindAttic.Orchestrator.Term.Wpf'
$artifact   = Join-Path $repo 'artifacts\MindAttic.Orchestrator.Desktop.exe'
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

# Matches the process name exactly (never a wildcard), so MindAttic.Orchestrator.TermHost is never caught: running CLIs keep their
# host across a redeploy (M.ORCH-T932). Callers pass only the dashboard and CLI names; never a TermHost.
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

# The newest installed exe of a side-by-side part, under C:\Apps\MindAttic.Orchestrator\<Name>\<version>\ (TermHost M.ORCH-T932,
# Gateway M.ORCH-T967), or $null. Folders are named <major.minor.patch>-<yyyyMMddHHmmss>, so the newest sorts last by version,
# then stamp (TermHostInstalls.Newest and GatewayInstalls.Newest do the same).
function Get-NewestInstalled([string]$Name, [string]$ExeName) {
    $root = Join-Path $appDir $Name
    if (-not (Test-Path -LiteralPath $root)) { return $null }
    Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+(-\d{14})?$' -and (Test-Path -LiteralPath (Join-Path $_.FullName $ExeName)) } |
        Sort-Object @{ Expression = { [version](($_.Name -split '-')[0]) } }, @{ Expression = { ($_.Name -split '-', 2)[1] } } |
        Select-Object -Last 1 | ForEach-Object { Join-Path $_.FullName $ExeName }
}

function Find-Installed {
    # The pre-M.ORCH-T844 name last: an install still holding only that is upgraded by the --install below.
    @((Join-Path $appDir 'MindAttic.Orchestrator.Desktop.exe'),
      (Join-Path $env:LOCALAPPDATA 'Programs\MindAttic.Orchestrator\MindAttic.Orchestrator.Desktop.exe'),
      (Join-Path $appDir 'MindAttic.Orchestrator.exe'),
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

# Whether the install may go ahead (M.ORCH-T873): it restarts the dashboard, and every unhosted CLI its Terminals panels run
# loses its state (M.ORCH-T928: a CLI inside a TermHost survives the redeploy, is listed as hosted and never blocks it). `orchestrator busy` (the CLI just published, or the installed one) lists every Claude CLI still working on any login and
# exits 1 if any is, 0 when all are idle; the CLI running this deploy is left out (its process tree). Any other outcome (no CLI,
# one too old to have `busy`, a crash) is unknown, and unknown is never taken for idle. -WaitForIdle polls instead of refusing.
function Test-ClisIdle {
    if ($IgnoreBusyClis) {
        Write-Host 'Not checking for working Claude CLIs (-IgnoreBusyClis): any unhosted CLI still working loses its state (hosted ones are unaffected).' -ForegroundColor DarkYellow
        return $true
    }
    if (-not (Test-Path $cliExe)) {
        Write-Host "Can't tell whether any Claude CLI is still working: $cliExe isn't there. -IgnoreBusyClis installs anyway." -ForegroundColor Red
        return $false
    }
    $busyArgs = @('busy')
    if ($WaitForIdle) {
        $busyArgs += @('--wait', '--poll', [string][Math]::Max(5, $BusyPollSeconds))
        if ($WaitTimeoutMinutes -gt 0) { $busyArgs += @('--timeout', [string]$WaitTimeoutMinutes) }
        Write-Host 'Waiting until every other unhosted Claude CLI is idle (-WaitForIdle)...' -ForegroundColor Cyan
    } else {
        Write-Host 'Checking that no other unhosted Claude CLI is still working (CLIs hosted in a TermHost survive the deploy)...' -ForegroundColor Cyan
    }
    $code = $null
    try { & $cliExe @busyArgs | Out-Host; $code = $LASTEXITCODE }
    catch { Write-Host "orchestrator busy failed: $($_.Exception.Message)" -ForegroundColor Red }
    if ($code -eq 0) { return $true }
    if ($code -eq 1) {
        Write-Host 'Deploy refused: the unhosted CLIs above are still working (or their state can''t be read), and installing restarts the dashboard, so each of them would lose its state. CLIs hosted in a TermHost are unaffected and don''t count. Nothing was stopped. Run it again once they are done, with -WaitForIdle to wait for them, or with -IgnoreBusyClis to install anyway.' -ForegroundColor Red
    } else {
        Write-Host "Deploy refused: couldn't tell whether any Claude CLI is still working (orchestrator busy exited $code). Nothing was stopped; -IgnoreBusyClis installs anyway." -ForegroundColor Red
    }
    return $false
}

function Exit-Refused {
    if ($Launch -and (Find-Installed)) { Start-Process -FilePath (Find-Installed) }
    exit 1
}

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
    start "" "%~dp0MindAttic.Orchestrator.Desktop.exe"
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
if (Test-Stale $artifact @($wpfDir, $coreDir, $termWpfDir)) {
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

# ── Side-by-side parts: TermHost (M.ORCH-T932), Gateway (M.ORCH-T967), TermViewer (M.ORCH-T960) ──
# Each build goes into its own <part>\<version>\ folder and is never copied over another: running CLIs keep the host binary they
# started on, new sessions start on the newest. Nothing here stops a TermHost, and nothing deletes an old folder: the installer and
# the daemon do that once no process runs from it. A build whose code matches the newest installed one adds no folder.
# The Gateway may be stopped and restarted by its daemon supervisor (that only drops remote viewers). The TermViewer exe
# (M.ORCH-T960, the standalone viewer the daemon opens for each hosted CLI while the dashboard is down) is never stopped here: an
# open viewer keeps its folder, and the daemon starts the newest.
$gatewayDir = Join-Path $repo 'src\MindAttic.Orchestrator.Gateway'
$termViewerDir = Join-Path $repo 'src\MindAttic.Orchestrator.TermViewer'

function Get-TreeHash([string]$dir) {
    $base = (Get-Item -LiteralPath $dir).FullName.TrimEnd([char]92) + ''
    (Get-ChildItem -LiteralPath $dir -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($base.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash
    }) -join '|'
}

function Publish-SideBySide([string]$Name, [string]$ProjectDir, [string]$ExeName, [string[]]$StaleDirs) {
    $root = Join-Path $appDir $Name
    $newest = Get-NewestInstalled $Name $ExeName
    if (-not ($Force -or (-not $newest) -or (Test-Stale $newest $StaleDirs))) { return }
    $stage = Join-Path $appDir "$Name.staging"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    Write-Host "Publishing $Name -> $root" -ForegroundColor Cyan
    dotnet publish (Join-Path $ProjectDir "MindAttic.Orchestrator.$Name.csproj") --configuration Release --output $stage | Out-Host
    $stageExe = Join-Path $stage $ExeName
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $stageExe)) { Write-Host "Publishing $Name FAILED." -ForegroundColor Red; $script:failed = $true }
    else {
        # Every published file, not just the exe's dll: a change only in a library it uses must install too.
        $same = $false
        if ($newest -and -not $Force) { $same = (Get-TreeHash (Split-Path $newest)) -eq (Get-TreeHash $stage) }
        if ($same) {
            # Touched, so the next deploy doesn't republish the same code just because the source files are newer.
            (Get-Item $newest).LastWriteTimeUtc = [DateTime]::UtcNow
            Write-Host "$Name is unchanged; the newest installed version stays." -ForegroundColor DarkGray
        } else {
            $ver = [version](Get-Item $stageExe).VersionInfo.FileVersion
            $folder = '{0}.{1}.{2}-{3}' -f $ver.Major, $ver.Minor, [Math]::Max(0, $ver.Build), [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
            $target = Join-Path $root $folder
            try {
                New-Item -ItemType Directory -Force $root | Out-Null
                if (Test-Path -LiteralPath $target) { throw "$target already exists; a version folder is never overwritten." }
                # Moved into place whole, so a start never sees a half-copied version.
                Move-Item -LiteralPath $stage -Destination $target -ErrorAction Stop
                Write-Host "Installed $Name $folder" -ForegroundColor Green
            } catch { Write-Host "Installing $Name failed: $($_.Exception.Message)" -ForegroundColor Red; $script:failed = $true }
        }
    }
    Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
}

Publish-SideBySide 'TermHost' $termHostDir 'MindAttic.Orchestrator.TermHost.exe' @($termHostDir, $termDir)
Publish-SideBySide 'Gateway' $gatewayDir 'MindAttic.Orchestrator.Gateway.exe' @($gatewayDir, $termDir)
Publish-SideBySide 'TermViewer' $termViewerDir 'MindAttic.Orchestrator.TermViewer.exe' @($termViewerDir, $termWpfDir, $termDir, $coreDir)

# ── Install, as a double-click would ────────────────────────────────────────
$installed = Find-Installed
$step = Get-InstallStep $artifact $installed $built ([bool]$Force)
if ($step -eq 'newer') {
    Write-Host ("The installed {0} ({1:yyyy-MM-dd HH:mm}) is newer than this build ({2:yyyy-MM-dd HH:mm}), so it's left as it is; -Force installs this build over it." -f
        $installed, (Get-Item $installed).LastWriteTime, (Get-Item $artifact).LastWriteTime) -ForegroundColor DarkGray
} elseif ($step -eq 'install') {
    # Nothing is stopped before every other CLI is known to be done (M.ORCH-T873).
    if (-not (Test-ClisIdle)) { Exit-Refused }

    # ── Earlier layouts ─────────────────────────────────────────────────────
    # Dashboard\ came from the two-folder layout; that daemon ran Cli\orchestrator.exe.
    Stop-From 'MindAttic.Orchestrator' (Join-Path $appDir 'Dashboard')
    $oldDashboard = Join-Path $appDir 'Dashboard'
    if (Test-Path $oldDashboard) { Remove-Item -Recurse -Force $oldDashboard; Write-Host "Removed the old $oldDashboard" -ForegroundColor DarkGray }

    Write-Host 'Installing (stops a running copy and its daemon; the daemon restarts from the new build)...' -ForegroundColor Cyan
    # An old-layout daemon still running from the CLI folder; only it: a CLI call in flight (an /allocate CLI's
    # `orchestrator next --claim`) is left to finish, as a deploy that doesn't republish the CLI must.
    Get-CimInstance Win32_Process -Filter "Name='orchestrator.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($cliOut, [System.StringComparison]::OrdinalIgnoreCase) -and $_.CommandLine -match '\sdaemon\b' } |
        ForEach-Object { try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { } }
    $installArgs = @('--install')
    if ($Force) { $installArgs += '--force' }
    if ($IgnoreBusyClis) { $installArgs += '--ignore-busy' }
    # The installer checks again, just before it stops anything, and exits 3 when a CLI started working since (M.ORCH-T873).
    while ($true) {
        $exitCode = Invoke-Installer $artifact $installArgs
        if ($exitCode -ne 3) { break }
        Write-Host 'The installer refused: a Claude CLI started working since the check. Nothing was stopped.' -ForegroundColor DarkYellow
        if (-not $WaitForIdle -or -not (Test-ClisIdle)) {
            if (-not $WaitForIdle) { $null = Test-ClisIdle }   # lists who, for the refusal
            Exit-Refused
        }
    }
    $installed = Find-Installed
    if ($exitCode -eq 0 -and $installed) { Write-Host "Installed $installed" -ForegroundColor Green }
    else { Write-Host 'Install FAILED; see its log, %APPDATA%\MindAttic\Logging\MindAttic.Orchestrator\MindAttic.Log.db (MindAttic.Log.Reader opens it).' -ForegroundColor Red; $failed = $true }
} elseif (-not $failed) {
    Write-Host 'MindAttic.Orchestrator is up to date.' -ForegroundColor DarkGray
}

if ($Launch -and $installed) { Start-Process -FilePath $installed }

# ── The sibling repo's deploy ───────────────────────────────────────────────
# Deploying Orchestrator also deploys Tickets (its deploy.ps1 does the same back here).
# MINDATTIC_DEPLOY_CHAIN stops Orchestrator -> Tickets -> Orchestrator from looping.
if (-not $env:MINDATTIC_DEPLOY_CHAIN) {
    $ticketsDeploy = Join-Path $repo '..\MindAttic.Tickets\deploy.ps1'
    if (Test-Path $ticketsDeploy) {
        Write-Host "Deploying MindAttic.Tickets too ($ticketsDeploy)..." -ForegroundColor Cyan
        $chainArgs = @{}
        if ($Force) { $chainArgs['Force'] = $true }
        # Every Orchestrator launch runs this deploy.ps1 (via deploy.bat's -Launch); without -SkipCli that reinstalled
        # the tickets CLI global tool on every single launch, undoing M.TIX-T126's fix for slow launches (M.ORCH-T832).
        # A deliberate full deploy without -Launch still reinstalls Tickets' CLI normally.
        if ($Launch) { $chainArgs['SkipCli'] = $true }
        $env:MINDATTIC_DEPLOY_CHAIN = '1'
        try {
            # Tickets' deploy.ps1 has no exit code convention of its own (unlike this script): it signals failure by
            # throwing (it runs under $ErrorActionPreference = 'Stop' too), so that's the only thing to catch here.
            & $ticketsDeploy @chainArgs
            Write-Host 'MindAttic.Tickets deployed.' -ForegroundColor Green
        } catch {
            Write-Host "MindAttic.Tickets deploy FAILED: $($_.Exception.Message)" -ForegroundColor Red
            $failed = $true
        } finally {
            Remove-Item Env:\MINDATTIC_DEPLOY_CHAIN -ErrorAction SilentlyContinue
        }
    } else {
        Write-Host "MindAttic.Tickets checkout not found at $ticketsDeploy; skipping its deploy." -ForegroundColor DarkYellow
    }
}

if ($failed) { Write-Host 'Deploy FAILED.' -ForegroundColor Red; exit 1 }
exit 0
