#Requires -Version 5.1
<#
.SYNOPSIS
    Packs the deployed MindAttic.Orchestrator into a zip a new user downloads, and with -Publish puts it on GitHub Releases
    (M.ORCH-T1547).

.DESCRIPTION
    The Desktop exe installs only itself; the CLI, TermHost, Gateway and TermViewer it runs live beside it in versioned
    folders that run.ps1 deploys. So a release is the install folder itself: run .\run.ps1 first (it builds and deploys
    origin/main), then this script stages, from C:\Apps\MindAttic.Orchestrator (or -From):

      MindAttic.Orchestrator.Desktop.exe, Cli\, and the newest TermHost\<v>\, Gateway\<v>\ and TermViewer\<v>\
      install.ps1 / Install.cmd   copy it all into the install folder and open the dashboard (its Welcome window does the rest)

    run.bat (it holds this machine's paths) and older versions are left out. The zip is
    artifacts\release\MindAttic.Orchestrator-<tag>.zip.

.PARAMETER From
    The deployed install to pack. Default: C:\Apps\MindAttic.Orchestrator.

.PARAMETER Tag
    The release's tag. Default: release-<yyyyMMdd-HHmm> (the app's own version stays whole-number, HOUSE-LAW-1).

.PARAMETER Publish
    Also create the GitHub release (gh release create) with the zip attached. Without it, only the zip is made.
#>
param(
    [string]$From = 'C:\Apps\MindAttic.Orchestrator',
    [string]$Tag = ('release-' + (Get-Date -Format 'yyyyMMdd-HHmm')),
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'

$desktop = Join-Path $From 'MindAttic.Orchestrator.Desktop.exe'
if (-not (Test-Path $desktop)) { throw "No deployed Orchestrator at $From. Run .\run.ps1 first." }

$out = Join-Path $PSScriptRoot 'artifacts\release'
$stage = Join-Path $out 'MindAttic.Orchestrator'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

Copy-Item $desktop $stage
Copy-Item (Join-Path $From 'Cli') (Join-Path $stage 'Cli') -Recurse
foreach ($part in 'TermHost', 'Gateway', 'TermViewer') {
    $newest = Get-ChildItem (Join-Path $From $part) -Directory -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    if (-not $newest) { throw "No $part under $From. Run .\run.ps1 first." }
    New-Item -ItemType Directory -Force (Join-Path $stage $part) | Out-Null
    Copy-Item $newest.FullName (Join-Path $stage "$part\$($newest.Name)") -Recurse
}

# The installer a new user runs: the same folder the Desktop exe installs to (Core's Installer: C:\Apps when writable, else
# per-user Programs), then the dashboard, whose Welcome window walks them through tools, GitHub, workspace and Claude.
@'
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
Get-ChildItem $here -Recurse -File | Unblock-File
$target = 'C:\Apps\MindAttic.Orchestrator'
try { New-Item -ItemType Directory -Force $target | Out-Null; [IO.File]::WriteAllText((Join-Path $target '.write-test'), ''); Remove-Item (Join-Path $target '.write-test') }
catch { $target = Join-Path $env:LOCALAPPDATA 'Programs\MindAttic.Orchestrator'; New-Item -ItemType Directory -Force $target | Out-Null }
Write-Host "Installing MindAttic Orchestrator into $target" -ForegroundColor Cyan
Get-Process 'MindAttic.Orchestrator.Desktop', 'MindAttic.Orchestrator' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$target\*" } | Stop-Process -Force
foreach ($item in Get-ChildItem $here) {
    if ($item.Name -in 'install.ps1', 'Install.cmd') { continue }
    Copy-Item $item.FullName (Join-Path $target $item.Name) -Recurse -Force
}
Start-Process (Join-Path $target 'MindAttic.Orchestrator.Desktop.exe')
Write-Host 'Done: the dashboard is opening.' -ForegroundColor Green
'@ | Set-Content -Encoding UTF8 (Join-Path $stage 'install.ps1')
"@powershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`"`r`n@pause" |
    Set-Content -Encoding ASCII (Join-Path $stage 'Install.cmd')

$zip = Join-Path $out "MindAttic.Orchestrator-$Tag.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Write-Host "Zipping $stage -> $zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("Built {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green

if (-not $Publish) { Write-Host "Not published. To publish: .\release.ps1 -Publish -Tag $Tag"; return }

$notes = @"
Unzip anywhere and double-click **Install.cmd**. It installs into C:\Apps\MindAttic.Orchestrator (or your own Programs
folder) and opens the dashboard; its Welcome window installs the tools (git, the GitHub CLI, Claude Code, the .NET 10
runtimes), signs you in to GitHub and Claude, and sets up the workspace.

To drive it from your phone over Tailscale and Remote Desktop, see
[docs/TAILSCALE.md](https://github.com/mindattic/MindAttic.Orchestrator/blob/main/docs/TAILSCALE.md).
"@
gh release create $Tag $zip --repo mindattic/MindAttic.Orchestrator --title "MindAttic Orchestrator $Tag" --notes $notes --target main
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed.' }
Write-Host "Published $Tag" -ForegroundColor Green
