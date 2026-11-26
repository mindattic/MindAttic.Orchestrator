#Requires -Version 5.1
<#
.SYNOPSIS
    MindAttic.Orchestrator's run menu: 1) Redeploy  2) Run Debug  3) Run Release  (the MindAttic standard, MAT-T279),
    with the Orchestrator's busy gate and redeploy guarantees kept (MAT-T298).

.DESCRIPTION
    A wrapper over the shared deploy-lib.ps1 (Invoke-AppRun). run.bat beside it, and the shortcuts' launcher
    C:\Apps\MindAttic.Orchestrator\run.bat, are deploy-lib's Write-RunBat and open the same menu on a double-click:
      1) Redeploy     builds the dashboard and its headless daemon to R:\MindAttic\Apps\MindAttic.Orchestrator\{Debug,Release}
                      Debug   framework-dependent (the dev/test copy; it runs as itself and never installs)
                      Release self-contained single files, MindAttic.Orchestrator.Desktop.exe and MindAttic.Orchestrator.Daemon.exe
                      (the drag-and-drop app), then installs them as a double-click would (below)
      2) Run Debug    R:\...\Debug\MindAttic.Orchestrator.Desktop.exe; extra arguments pass through
      3) Run Release  R:\...\Release\MindAttic.Orchestrator.Desktop.exe: the installed copy is this build, so it opens that
    Picks combine: 13 is Deploy & Run.

    The app installs itself: MindAttic.Orchestrator.Desktop.exe is one self-contained file that installs to
    C:\Apps\MindAttic.Orchestrator (or per-user Programs), registers its logon daemon, makes Start menu and desktop shortcuts
    (to the run.bat launcher there) and opens. The Release PostBuild does what the old run.ps1 did after publishing:
      - publishes the orchestrator CLI to C:\Apps\MindAttic.Orchestrator\Cli.staging and swaps it into Cli\ file by file
        (a running orchestrator.exe is renamed aside, never fails the deploy, M.ORCH-T765);
      - publishes TermHost, Gateway and TermViewer side by side, each into its own <part>\<version>\ folder; nothing stops a
        TermHost, so every hosted CLI survives (M.ORCH-T932, M.ORCH-T1944);
      - runs `MindAttic.Orchestrator.Desktop.exe --install` from the Release folder: it stops the running dashboard and daemon
        by their exact paths and the daemon restarts from the new build;
      - writes the MindAttic Vault settings manifest (--write-settings-manifest, M.ORCH-T1308).

    The busy gate (M.ORCH-T873, M.ORCH-T1007): installing restarts the dashboard, and every unhosted CLI its Terminals panels
    run loses its state. So before anything is installed, `orchestrator busy` must say every other Claude CLI on this machine
    (any login) is idle; if any is working, or its state can't be read, nothing at all is installed (not the CLI, TermHost,
    Gateway or TermViewer either) and the deploy exits 1. The CLI running this deploy is left out. A dashboard running from a
    Debug or Release deploy folder is gated the same way before Redeploy stops it.

    It always deploys the latest checked-in code (M.ORCH-T1067): Redeploy fetches origin and builds a dedicated clean worktree
    at origin/main (..\MindAttic.worktrees\MindAttic.Orchestrator\deploy), never the checkout it was run from.

.PARAMETER FromHere
    Build this folder as it is instead of origin/main.

.PARAMETER Force
    Clean bin/obj before building, republish every side-by-side part, and let the install replace a newer installed exe.

.PARAMETER UiOnly
    Install only the dashboard: the CLI, the daemon exe, TermHost, Gateway and TermViewer are neither built nor installed, so
    their installed copies stay as they are (M.ORCH-T1323). The busy gate still runs, since installing restarts the dashboard.

.PARAMETER LauncherOnly
    Only (re)write the shortcuts' launcher, C:\Apps\MindAttic.Orchestrator\run.bat, and exit: nothing is fetched, built,
    stopped or installed. Every other run rewrites it too.

.PARAMETER IgnoreBusyClis
    Install even while other Claude CLIs are still working; they lose their state.

.PARAMETER WaitForIdle
    Instead of refusing while CLIs are working, poll until every one is idle, then install.

.PARAMETER WaitTimeoutMinutes
    With -WaitForIdle: give up (and refuse) after this many minutes; 0 waits for as long as it takes.

.PARAMETER Republish
    The old launcher's spelling of -Redeploy (C:\Apps\MindAttic.Orchestrator\run.bat from before the shared menu).

.PARAMETER Launch
    The old launcher's spelling of -RunRelease.

.EXAMPLE
    .\run.ps1                              the menu
    .\run.ps1 -Redeploy                    build and install, open nothing (what /deploy runs)
    .\run.ps1 -Redeploy -RunRelease        Deploy & Run
    .\run.ps1 -Redeploy -UiOnly -WaitForIdle
    .\run.ps1 -Redeploy -DryRun            print what would be built and launched, do nothing
    .\run.ps1 -LauncherOnly
#>
param(
    [switch]$Redeploy,
    [switch]$RunDebug,
    [switch]$RunRelease,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$IgnoreBusyClis,
    [switch]$WaitForIdle,
    [int]$WaitTimeoutMinutes = 0,
    [int]$BusyPollSeconds = 30,
    [switch]$FromHere,
    [switch]$LauncherOnly,
    [switch]$UiOnly,
    [switch]$Republish,
    [switch]$Launch,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$AppArgs = @()
)

$ErrorActionPreference = 'Stop'
$Redeploy = [switch]($Redeploy -or $Republish)
$RunRelease = [switch]($RunRelease -or $Launch)

function Find-DeployLib {
    if ($env:MINDATTIC_DEPLOY_LIB -and (Test-Path -LiteralPath $env:MINDATTIC_DEPLOY_LIB)) { return $env:MINDATTIC_DEPLOY_LIB }
    $roots = @()
    $d = $PSScriptRoot
    while ($d) { $roots += $d; $d = Split-Path -Parent $d }
    $roots += 'D:\Projects\MindAttic'
    foreach ($r in $roots) {
        foreach ($rel in 'MindAttic.Agency\shared\deploy-lib.ps1', 'mindattic-agent-standard\shared\deploy-lib.ps1') {
            $p = Join-Path $r $rel
            if (Test-Path -LiteralPath $p) { return $p }
        }
    }
    throw 'deploy-lib.ps1 not found: clone MindAttic.Agency beside this repo, or set MINDATTIC_DEPLOY_LIB to its path.'
}

. (Find-DeployLib)

# The main checkout, from git's common dir: run from a worktree, the launcher and the deploy folders still name the real repo.
function Get-MainCheckout {
    $common = $null
    try { $common = (& git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir 2>$null) } catch { $common = $null }
    if ($common -and $LASTEXITCODE -eq 0) { return (Split-Path -Parent ($common.Trim() -replace '/', '\')) }
    return $PSScriptRoot
}

$mainCheckout = Get-MainCheckout
$appDir       = 'C:\Apps\MindAttic.Orchestrator'
$cliOut       = Join-Path $appDir 'Cli'
$cliExe       = Join-Path $cliOut 'orchestrator.exe'
$cliStage     = Join-Path $appDir 'Cli.staging'
$deployTree   = Join-Path (Split-Path -Parent (Split-Path -Parent $mainCheckout)) "MindAttic.worktrees\$(Split-Path -Leaf $mainCheckout)\deploy"
# What Redeploy builds: a clean worktree at origin/main (M.ORCH-T1067), or with -FromHere this folder as it is.
$repo         = if ($FromHere) { $PSScriptRoot } else { $deployTree }
$props        = Join-Path $repo 'Directory.Build.props'
$coreDir      = Join-Path $repo 'src\MindAttic.Orchestrator.Core'
$wpfDir       = Join-Path $repo 'src\MindAttic.Orchestrator.Wpf'
$cliDir       = Join-Path $repo 'src\MindAttic.Orchestrator.Cli'
$termDir      = Join-Path $repo 'src\MindAttic.Orchestrator.Term'
$termHostDir  = Join-Path $repo 'src\MindAttic.Orchestrator.TermHost'
$termWpfDir   = Join-Path $repo 'src\MindAttic.Orchestrator.Term.Wpf'
$daemonDir    = Join-Path $repo 'src\MindAttic.Orchestrator.Daemon'
$gatewayDir   = Join-Path $repo 'src\MindAttic.Orchestrator.Gateway'
$termViewerDir = Join-Path $repo 'src\MindAttic.Orchestrator.TermViewer'
$sourceExts   = @('.cs', '.csproj', '.xaml', '.json', '.props', '.targets', '.resx', '.manifest', '.ico', '.png')
$failed       = $false
$treeReady    = $false

# ── The shortcuts' launcher (MAT-T67, MAT-T298) ─────────────────────────────
# The app points its desktop and Start menu shortcuts at C:\Apps\MindAttic.Orchestrator\run.bat when it's there
# (Installer.ShortcutTarget): deploy-lib's standard run.bat, carrying the main checkout's run.ps1 by full path, so a
# double-click opens this menu. Rewritten only when it differs (cmd reads a running batch file by offset).
function Write-Launcher {
    $launcher = Write-RunBat -Repo $mainCheckout -Path (Join-Path $appDir 'run.bat') -RunScript (Join-Path $mainCheckout 'run.ps1') -Title 'MindAttic Orchestrator'
    # The launcher was deploy.bat until MAT-T68: remove it, and move any shortcut still pointing at it onto run.bat (in a
    # normal window: the menu needs answering). The app's own shortcut repair (Installer.EnsureShortcuts) does the same.
    $stale = Join-Path $appDir 'deploy.bat'
    if (Test-Path -LiteralPath $stale) {
        Remove-Item -LiteralPath $stale -Force -ErrorAction SilentlyContinue
        Write-Host "Removed the old $stale" -ForegroundColor DarkGray
    }
    try {
        $shell = New-Object -ComObject WScript.Shell
        foreach ($lnk in @((Join-Path ([Environment]::GetFolderPath('Programs')) 'MindAttic\MindAttic Orchestrator.lnk'),
                           (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'MindAttic Orchestrator.lnk'))) {
            if (-not (Test-Path -LiteralPath $lnk)) { continue }
            $link = $shell.CreateShortcut($lnk)
            if ($link.TargetPath -ieq $stale) {
                $link.TargetPath = $launcher
                $link.WindowStyle = 1
                $link.Save()
                Write-Host "Pointed $lnk at $launcher" -ForegroundColor DarkGray
            }
        }
    } catch { }
}

Write-Launcher
if ($LauncherOnly) { exit 0 }

# Moves the deploy worktree to origin/main (M.ORCH-T1067), once per run: the checkout this script runs from may be stale,
# diverged or full of someone's uncommitted work. -Force also cleans bin/obj there.
function Initialize-BuildTree {
    if ($script:treeReady) { return }
    if (-not $FromHere) {
        & git -C $mainCheckout fetch --quiet origin main
        if ($LASTEXITCODE -ne 0) { throw 'git fetch origin main failed.' }
        if (-not (Test-Path (Join-Path $deployTree '.git'))) {
            & git -C $mainCheckout worktree prune
            & git -C $mainCheckout worktree add --detach --force $deployTree origin/main | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Couldn't make the deploy worktree at $deployTree." }
        } else {
            & git -C $deployTree reset --quiet --hard origin/main
            if ($LASTEXITCODE -ne 0) { throw "Couldn't move the deploy worktree to origin/main." }
            & git -C $deployTree clean -fdq   # keeps ignored bin/obj, so unchanged parts aren't rebuilt
        }
        Write-Host "Deploying origin/main: $(& git -C $deployTree log -1 --format='%h %s')"
    }
    if ($Force) {
        foreach ($d in @($coreDir, $wpfDir, $cliDir, $daemonDir)) {
            foreach ($sub in 'bin', 'obj') {
                $path = Join-Path $d $sub
                if (Test-Path $path) {
                    try { Remove-Item -Recurse -Force $path -ErrorAction Stop }
                    catch { Write-Host "  (could not fully remove $path : $($_.Exception.Message))" -ForegroundColor DarkYellow }
                }
            }
        }
    }
    $script:treeReady = $true
}

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
        # A folder with no build stamp (1.0.0, which the pattern and TermHostInstalls.Parse accept) sorts first among its version:
        # indexing the missing stamp threw under strict mode and aborted the whole deploy (M.ORCH-T1008).
        Sort-Object @{ Expression = { [version](($_.Name -split '-')[0]) } }, @{ Expression = { $parts = @($_.Name -split '-', 2); if ($parts.Count -gt 1) { $parts[1] } else { '' } } } |
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

# Whether the install may go ahead (M.ORCH-T873): it restarts the dashboard, and every unhosted CLI its Terminals panels run
# loses its state (M.ORCH-T928: a CLI inside a TermHost survives the redeploy, is listed as hosted and never blocks it). `orchestrator busy` (the CLI just published, or the installed one) lists every Claude CLI still working on any login and
# exits 1 if any is, 0 when all are idle; the CLI running this deploy is left out (its process tree). Any other outcome (no CLI,
# one too old to have `busy`, a crash) is unknown, and unknown is never taken for idle. -WaitForIdle polls instead of refusing.
function Test-ClisIdle([string]$BusyExe = $cliExe) {
    if ($IgnoreBusyClis) {
        Write-Host 'Not checking for working Claude CLIs (-IgnoreBusyClis): any unhosted CLI still working loses its state (hosted ones are unaffected).' -ForegroundColor DarkYellow
        return $true
    }
    if (-not (Test-Path $BusyExe)) {
        Write-Host "Can't tell whether any Claude CLI is still working: $BusyExe isn't there. -IgnoreBusyClis installs anyway." -ForegroundColor Red
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
    # The CLI writes UTF-8 (its table has box-drawing characters). Piped, PowerShell 5.1 decodes a native exe's stdout with
    # [Console]::OutputEncoding, the OEM code page by default, which turned the table into mojibake (M.ORCH-T1418).
    $savedEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
        & $BusyExe @busyArgs | Out-Host; $code = $LASTEXITCODE
    }
    catch { Write-Host "orchestrator busy failed: $($_.Exception.Message)" -ForegroundColor Red }
    finally { try { [Console]::OutputEncoding = $savedEncoding } catch { } }
    if ($code -eq 0) { return $true }
    if ($code -eq 1) {
        Write-Host 'Deploy refused: the unhosted CLIs above are still working (or their state can''t be read), and installing restarts the dashboard, so each of them would lose its state. CLIs hosted in a TermHost are unaffected and don''t count. Nothing was stopped. Run it again once they are done, with -WaitForIdle to wait for them, or with -IgnoreBusyClis to install anyway.' -ForegroundColor Red
    } else {
        Write-Host "Deploy refused: couldn't tell whether any Claude CLI is still working (orchestrator busy exited $code). Nothing was stopped; -IgnoreBusyClis installs anyway." -ForegroundColor Red
    }
    return $false
}

# Starts something that outlives this script (the dashboard). A hosted CLI runs inside a kill-on-close job (M.ORCH-T1674),
# and Start-Process can't ask to leave it, so a dashboard a deploy started from a CLI ended with that CLI's host. The
# deployed CLI's `spawn` starts it outside the job instead (M.ORCH-T1691). A CLI from before that verb (a first deploy),
# or none, answers non-zero without starting anything, and Start-Process does it as before.
function Start-Outliving([string]$path) {
    if (Test-Path $cliExe) {
        try {
            & $cliExe spawn -- $path *> $null
            if ($LASTEXITCODE -eq 0) { return }
        } catch { }
    }
    Start-Process -FilePath $path
}

function Exit-Refused {
    if ($RunRelease -and (Find-Installed)) { Start-Outliving (Find-Installed) }
    exit 1
}

function Get-TreeHash([string]$dir) {
    $base = (Get-Item -LiteralPath $dir).FullName.TrimEnd([char]92) + ''
    (Get-ChildItem -LiteralPath $dir -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($base.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash
    }) -join '|'
}

# ── Side-by-side parts: TermHost (M.ORCH-T932), Gateway (M.ORCH-T967), TermViewer (M.ORCH-T960) ──
# Each build goes into its own <part>\<version>\ folder and is never copied over another: running CLIs keep the host binary they
# started on, new sessions start on the newest. Nothing here stops a TermHost, and nothing deletes an old folder: the installer and
# the daemon do that once no process runs from it. A build whose code matches the newest installed one adds no folder.
# The Gateway may be stopped and restarted by its daemon supervisor (that only drops remote viewers). The TermViewer exe
# (M.ORCH-T960, the standalone viewer the daemon opens for each hosted CLI while the dashboard is down) is never stopped here: an
# open viewer keeps its folder, and the daemon starts the newest.
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

function Install-StagedCli {
    if (-not $script:cliStaged) { return }
    $swapFailed = $false
    try { Copy-Published $cliStage $cliOut } catch { Write-Host "Swapping in the CLI failed: $($_.Exception.Message)" -ForegroundColor Red; $swapFailed = $true }
    Remove-Item -Recurse -Force $cliStage -ErrorAction SilentlyContinue
    $script:cliStaged = $false
    if ($swapFailed) { Write-Host 'Publishing the CLI FAILED.' -ForegroundColor Red; $script:failed = $true }
    else {
        # The apphost keeps its old time when only Core's method bodies changed (the CLI isn't recompiled), so Core would
        # read newer than it on every later deploy, which would republish it and stop running CLIs each time.
        (Get-Item $cliExe).LastWriteTimeUtc = [DateTime]::UtcNow
        Write-Host "Deployed $cliExe" -ForegroundColor Green
    }
}

# ── PreBuild, before each config's build ────────────────────────────────────
# The build tree first. Then: Publish-AppConfig stops whatever runs from the deploy folder before swapping the new build in, so a
# dashboard run from there (Run Debug, or a Release copy that didn't hand over) passes the busy gate first, like an install.
# -UiOnly's Release deploy carries the installed daemon exe, so --install's daemon copy changes nothing.
function Invoke-PreBuild([string]$Config, [string]$Out) {
    Initialize-BuildTree
    $target = $Out -replace '\.staging$', ''
    $running = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -and $_.Path.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase) } catch { $false } })
    if ($running.Count -and -not (Test-ClisIdle)) { Exit-Refused }
    $installedDaemon = Join-Path $appDir 'MindAttic.Orchestrator.Daemon.exe'
    if ($UiOnly -and $Config -eq 'Release' -and (Test-Path $installedDaemon)) { Copy-Item -LiteralPath $installedDaemon -Destination $Out -Force }
}

# ── PostBuild: the Release build is the drag-and-drop exe; install it ───────
function Invoke-PostBuild([string]$Config, [string]$Out) {
    # The sidecar names the commit actually built (the deploy worktree's), not the checkout's.
    Write-PublishSidecar -Repo $repo -Target $Out -Mode $(if ($Config -eq 'Release') { 'release' } else { 'dev' }) -Config $Config -Kind 'dotnet'
    if ($Config -ne 'Release') { return }
    $artifact = Join-Path $Out 'MindAttic.Orchestrator.Desktop.exe'
    $daemonArtifact = Join-Path $Out 'MindAttic.Orchestrator.Daemon.exe'

    # ── The CLI ─────────────────────────────────────────────────────────────
    # Published to a staging folder here and swapped in only after the busy gate below (M.ORCH-T1007): a refused deploy installs
    # nothing, so no new CLI runs against the old daemon and dashboard. The swap goes file by file: a running CLI call (hooks,
    # /allocate, review) holds its DLLs and must neither fail the deploy nor leave Cli\ half-installed (M.ORCH-T765).
    $script:cliStaged = $false
    if ($UiOnly) { Write-Host '-UiOnly: the CLI and the daemon exe are left as installed.' -ForegroundColor DarkGray }
    elseif (Test-Stale $cliExe @($cliDir, $coreDir, $termDir)) {
        if (Test-Path $cliStage) { Remove-Item -Recurse -Force $cliStage }
        Write-Host "Publishing the CLI -> $cliStage" -ForegroundColor Cyan
        # Sibling repos (MindAttic.Helpers) resolve against the workspace, not this deploy worktree (M.ORCH-T1313).
        $workspace = Split-Path -Parent $mainCheckout
        dotnet publish (Join-Path $cliDir 'MindAttic.Orchestrator.Cli.csproj') --configuration Release --output $cliStage "-p:MindAtticRoot=$workspace" | Out-Host
        if ($LASTEXITCODE -eq 0 -and (Test-Path (Join-Path $cliStage 'orchestrator.exe'))) { $script:cliStaged = $true }
        else {
            Remove-Item -Recurse -Force $cliStage -ErrorAction SilentlyContinue
            Write-Host 'Publishing the CLI FAILED.' -ForegroundColor Red; $script:failed = $true
        }
    }

    # ── The busy gate, before anything is installed (M.ORCH-T873, M.ORCH-T1007) ─
    # Installing restarts the dashboard: while another CLI is still working nothing at all is installed (not the CLI, the TermHost,
    # the Gateway or the TermViewer either), so a refused deploy leaves the old build whole. The staged CLI answers when there is one.
    $installed = Find-Installed
    $step = Get-InstallStep $artifact $installed $true ([bool]$Force)
    # A daemon-only change (Program.cs, say) still installs: the dashboard exe is copied over unchanged and the daemon exe takes the new
    # build (M.ORCH-T1944). As is an install that has no daemon exe yet, or one that isn't this build.
    if ($step -eq 'current' -and -not $UiOnly -and (Test-Path $daemonArtifact)) {
        $installedDaemon = Join-Path (Split-Path $installed) 'MindAttic.Orchestrator.Daemon.exe'
        if (-not (Test-Path $installedDaemon) -or
            (Get-FileHash $daemonArtifact).Hash -ne (Get-FileHash $installedDaemon).Hash) { $step = 'install' }
    }
    if ($step -eq 'install') {
        $busyExe = if ($script:cliStaged) { Join-Path $cliStage 'orchestrator.exe' } else { $cliExe }
        if (-not (Test-ClisIdle $busyExe)) {
            Remove-Item -Recurse -Force $cliStage -ErrorAction SilentlyContinue
            Exit-Refused
        }
    }
    Install-StagedCli

    if ($UiOnly) { Write-Host '-UiOnly: TermHost, Gateway and TermViewer are left as installed.' -ForegroundColor DarkGray }
    else {
        Publish-SideBySide 'TermHost' $termHostDir 'MindAttic.Orchestrator.TermHost.exe' @($termHostDir, $termDir)
        Publish-SideBySide 'Gateway' $gatewayDir 'MindAttic.Orchestrator.Gateway.exe' @($gatewayDir, $termDir)
        Publish-SideBySide 'TermViewer' $termViewerDir 'MindAttic.Orchestrator.TermViewer.exe' @($termViewerDir, $termWpfDir, $termDir, $coreDir)
    }

    # ── Install, as a double-click would ────────────────────────────────────
    if ($step -eq 'newer') {
        Write-Host ("The installed {0} ({1:yyyy-MM-dd HH:mm}) is newer than this build ({2:yyyy-MM-dd HH:mm}), so it's left as it is; -Force installs this build over it." -f
            $installed, (Get-Item $installed).LastWriteTime, (Get-Item $artifact).LastWriteTime) -ForegroundColor DarkGray
    } elseif ($step -eq 'install') {
        # Every other CLI was known to be done before anything was installed (the gate above, M.ORCH-T1007); the installer checks
        # again just before it stops anything.

        # ── Earlier layouts ─────────────────────────────────────────────────
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
        # Without Run Release the installer starts a dashboard it stopped again (M.ORCH-T1030); with it, the menu's Run Release
        # opens it (the Release exe hands over to the installed copy, which is this build).
        if ($RunRelease) { $installArgs += '--no-dashboard-restart' }
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
        else { Write-Host 'Install FAILED; see its log, %APPDATA%\MindAttic\Logging\MindAttic.Orchestrator\MindAttic.Log.db (MindAttic.Log.Reader opens it).' -ForegroundColor Red; $script:failed = $true }
    } elseif (-not $script:failed) {
        Write-Host 'MindAttic.Orchestrator is up to date.' -ForegroundColor DarkGray
    }

    # ── MindAttic Vault settings (LAMP, M.ORCH-T1308) ─────────────────────
    # Imports today's settings from the orchestrator's database into %LOCALAPPDATA%\MindAttic\Orchestrator\settings.json (once)
    # and writes settings.manifest.json beside it, so MindAttic.Vault.Desktop shows the Orchestrator tab. The installed CLI
    # does it (the daemon also does on its start); a CLI from before LAMP doesn't know the switch, which only says so.
    if (-not $script:failed -and (Test-Path $cliExe)) {
        Write-Host 'Writing the MindAttic Vault settings manifest...' -ForegroundColor Cyan
        & $cliExe --write-settings-manifest
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  (--write-settings-manifest exited $LASTEXITCODE; the daemon imports the settings on its next start)" -ForegroundColor DarkYellow
        }
    }

    if ($script:failed) {
        # A refused or failed install leaves the installed copy as it was; Run Release still opens it, as the old -Launch did.
        if ($RunRelease -and (Find-Installed)) { Start-Outliving (Find-Installed) }
        throw 'Deploy FAILED.'
    }
}

# Debug: the dashboard and daemon framework-dependent. Release: each a self-contained single file, the daemon beside the dashboard
# where --install looks for it (Installer.InstallDaemonCopy, M.ORCH-T1944). -UiOnly builds the dashboard alone.
$projects = @(Join-Path $wpfDir 'MindAttic.Orchestrator.Wpf.csproj')
if (-not $UiOnly) { $projects += Join-Path $daemonDir 'MindAttic.Orchestrator.Daemon.csproj' }
$Override = @{
    Exe       = 'MindAttic.Orchestrator.Desktop.exe'
    Plan      = @{ Kind = 'dotnet'; Projects = $projects }
    PreBuild  = { param($Config, $Out) Invoke-PreBuild $Config $Out }
    PostBuild = { param($Config, $Out) Invoke-PostBuild $Config $Out }
}

Invoke-AppRun -Repo $mainCheckout -RepoName 'MindAttic.Orchestrator' -Redeploy:$Redeploy -RunDebug:$RunDebug -RunRelease:$RunRelease `
    -DryRun:$DryRun -AppArgs $AppArgs -Override $Override
