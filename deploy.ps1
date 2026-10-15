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
# Dashboard\ and deploy.bat came from the two-folder layout; that daemon ran Cli\orchestrator.exe.
Stop-From 'MindAttic.Orchestrator' (Join-Path $appDir 'Dashboard')
foreach ($old in @((Join-Path $appDir 'Dashboard'), (Join-Path $appDir 'deploy.bat'))) {
    if (Test-Path $old) { Remove-Item -Recurse -Force $old; Write-Host "Removed the old $old" -ForegroundColor DarkGray }
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
    Stop-From 'orchestrator' $cliOut
    Write-Host "Publishing the CLI -> $cliOut" -ForegroundColor Cyan
    dotnet publish (Join-Path $cliDir 'MindAttic.Orchestrator.Cli.csproj') --configuration Release --output $cliOut | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Host 'Publishing the CLI FAILED.' -ForegroundColor Red; $failed = $true }
    else { Write-Host "Deployed $cliExe" -ForegroundColor Green }
}

# ── Install, as a double-click would ────────────────────────────────────────
$installed = Find-Installed
if ((Test-Path $artifact) -and ($built -or -not $installed)) {
    Write-Host 'Installing (stops a running copy and its daemon; the daemon restarts from the new build)...' -ForegroundColor Cyan
    Stop-From 'orchestrator' $cliOut   # an old-layout daemon still running from the CLI folder
    Start-Process -FilePath $artifact -ArgumentList '--install' -Wait
    $installed = Find-Installed
    if ($installed) { Write-Host "Installed $installed" -ForegroundColor Green }
    else { Write-Host 'Install FAILED; see the log under %LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\logs.' -ForegroundColor Red; $failed = $true }
} elseif (-not $failed) {
    Write-Host 'MindAttic.Orchestrator is up to date.' -ForegroundColor DarkGray
}

if ($Launch -and $installed) { Start-Process -FilePath $installed }

if ($failed) { Write-Host 'Deploy FAILED.' -ForegroundColor Red; exit 1 }
exit 0
