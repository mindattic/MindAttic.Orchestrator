#Requires -Version 5.1
<#
.SYNOPSIS
    Builds everything a new user's PC needs into one downloadable zip, and with -Publish puts it on GitHub Releases
    (M.ORCH-T1547, M.ORCH-T1548).

.DESCRIPTION
    A fresh PC can't build MindAttic from source (the MindAttic.* packages live only in this PC's C:\LocalNuGet), so the zip
    carries built apps:

      MindAttic.Orchestrator\   the dashboard (self-contained Desktop exe), Cli\, and TermHost\, Gateway\, TermViewer\ each in
                                its <version>-<yyyyMMddHHmmss>\ folder, published from this checkout as run.ps1 publishes them
      MindAttic.Launcher\       the deployed Launcher (C:\Apps\MindAttic.Launcher), which starts every CLI
      Tickets\                  the installed tickets tool (dotnet tool store), run through a tickets.cmd shim
      install.ps1, Install.cmd  put each where the apps look for it and open the dashboard, whose Welcome window does the rest

    Never packed: run.bat and repo.path (this machine's paths), *.stale-* and *.pdb.

.PARAMETER Tag
    The release's tag. Default: release-<yyyyMMdd-HHmm> (the apps' own versions stay whole-number, HOUSE-LAW-1).

.PARAMETER Publish
    Also create the GitHub release (gh release create) with the zip attached. Without it, only the zip is made.
#>
param(
    [string]$Tag = ('release-' + (Get-Date -Format 'yyyyMMdd-HHmm')),
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'

$out = Join-Path $PSScriptRoot 'artifacts\release'
$stage = Join-Path $out 'stage'
# Only what's on origin/main ships, never this checkout's uncommitted work: an export of it (git archive), not a worktree.
$source = Join-Path $out 'source'
if (Test-Path $source) { Remove-Item -Recurse -Force $source }
New-Item -ItemType Directory -Force $source | Out-Null
git -C $PSScriptRoot fetch --quiet origin main
if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }
$commit = (git -C $PSScriptRoot rev-parse origin/main).Trim()
$tarball = Join-Path $out 'source.tar'
git -C $PSScriptRoot archive --format=tar --output $tarball origin/main
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
& (Join-Path $env:SystemRoot 'System32\tar.exe') -xf $tarball -C $source
if ($LASTEXITCODE -ne 0) { throw 'Extracting the source failed.' }
Remove-Item -Force $tarball
Write-Host "Building origin/main ($($commit.Substring(0, 8)))" -ForegroundColor Cyan
$src = Join-Path $source 'src'
$app = Join-Path $stage 'MindAttic.Orchestrator'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $app | Out-Null
$workspace = Split-Path -Parent $PSScriptRoot
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')

function Invoke-Publish([string]$Project, [string]$Output, [string[]]$Extra = @()) {
    Write-Host "Publishing $Project -> $Output" -ForegroundColor Cyan
    dotnet publish (Join-Path $src "$Project\$Project.csproj") --configuration Release --output $Output @Extra | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publishing $Project failed." }
}

Invoke-Publish 'MindAttic.Orchestrator.Wpf' $app @('--runtime', 'win-x64', '--self-contained', 'true', '/p:PublishSingleFile=true',
    '/p:IncludeNativeLibrariesForSelfExtract=true', '/p:EnableCompressionInSingleFile=true', '/p:DebugType=None')
Get-ChildItem $app -File | Where-Object Name -ne 'MindAttic.Orchestrator.Desktop.exe' | Remove-Item -Force
Invoke-Publish 'MindAttic.Orchestrator.Cli' (Join-Path $app 'Cli') @("-p:MindAtticRoot=$workspace", '/p:DebugType=None')
foreach ($part in 'TermHost', 'Gateway', 'TermViewer') {
    $project = "MindAttic.Orchestrator.$part"
    $version = ([xml](Get-Content (Join-Path $src "$project\$project.csproj"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $version) { $version = '1.0.0' }
    Invoke-Publish $project (Join-Path $app "$part\$version-$stamp") @('/p:DebugType=None')
}

$launcher = 'C:\Apps\MindAttic.Launcher'
if (-not (Test-Path (Join-Path $launcher 'MindAttic.Launcher.exe'))) { throw "No deployed Launcher at $launcher (run MindAttic.Launcher\run.ps1)." }
Write-Host "Copying the Launcher from $launcher" -ForegroundColor Cyan
robocopy $launcher (Join-Path $stage 'MindAttic.Launcher') /E /NFL /NDL /NJH /NJS /NP /XF run.bat repo.path *.pdb *.stale-* | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying the Launcher failed.' }

$toolStore = Join-Path $env:USERPROFILE '.dotnet\tools\.store\mindattic.tickets.cli'
$tickets = Get-ChildItem $toolStore -Recurse -Filter 'tickets.dll' -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
if (-not $tickets) { throw "No installed tickets tool under $toolStore." }
Write-Host "Copying the tickets tool from $($tickets.DirectoryName)" -ForegroundColor Cyan
robocopy $tickets.DirectoryName (Join-Path $stage 'Tickets') /E /NFL /NDL /NJH /NJS /NP /XF *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying the tickets tool failed.' }

# The installer a new user runs. The Orchestrator goes where its Desktop exe installs itself (Core's Installer: C:\Apps when
# writable, else per-user Programs); the Launcher where the Orchestrator looks for it (OrchestratorOptions.LauncherExe); the
# tickets tool beside the Orchestrator with a shim on the user's PATH. Then the dashboard opens on its Welcome window.
@'
param([string]$Apps = 'C:\Apps', [switch]$DryRun) # -DryRun: copy only; no PATH change, no dashboard
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
Get-ChildItem $here -Recurse -File | Unblock-File
try { New-Item -ItemType Directory -Force $Apps | Out-Null; $probe = Join-Path $Apps '.write-test'; [IO.File]::WriteAllText($probe, ''); Remove-Item $probe }
catch { $Apps = Join-Path $env:LOCALAPPDATA 'Programs'; Write-Host "C:\Apps isn't writable: installing under $Apps (the Launcher's path then needs setting in the Orchestrator's settings)." -ForegroundColor Yellow }
$orchestrator = Join-Path $Apps 'MindAttic.Orchestrator'
Get-Process 'MindAttic.Orchestrator.Desktop', 'MindAttic.Orchestrator', 'MindAttic.Launcher' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$Apps\*" } | Stop-Process -Force
foreach ($name in 'MindAttic.Orchestrator', 'MindAttic.Launcher') {
    Write-Host "Installing $name into $(Join-Path $Apps $name)" -ForegroundColor Cyan
    robocopy (Join-Path $here $name) (Join-Path $Apps $name) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed." }
}
$ticketsDir = Join-Path $orchestrator 'Tickets'
robocopy (Join-Path $here 'Tickets') $ticketsDir /E /NFL /NDL /NJH /NJS /NP | Out-Null
$bin = Join-Path $orchestrator 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
"@dotnet `"$ticketsDir\tickets.dll`" %*" | Set-Content -Encoding ASCII (Join-Path $bin 'tickets.cmd')
"#!/bin/sh`nexec dotnet `"$($ticketsDir -replace '\\','/')/tickets.dll`" `"`$@`"" | Set-Content -Encoding ASCII (Join-Path $bin 'tickets')
if ($DryRun) { Write-Host "Dry run: installed into $Apps; PATH and the dashboard left alone." -ForegroundColor Green; return }
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (($userPath -split ';') -notcontains $bin) { [Environment]::SetEnvironmentVariable('Path', ($bin + ';' + $userPath).TrimEnd(';'), 'User') }
Start-Process (Join-Path $orchestrator 'MindAttic.Orchestrator.Desktop.exe')
Write-Host 'Done: the dashboard is opening on its Welcome window.' -ForegroundColor Green
'@ | Set-Content -Encoding UTF8 (Join-Path $stage 'install.ps1')
"@powershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`"`r`n@pause" |
    Set-Content -Encoding ASCII (Join-Path $stage 'Install.cmd')

$zip = Join-Path $out "MindAttic.Orchestrator-$Tag.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Write-Host "Zipping $stage -> $zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -Recurse -Force $source
Write-Host ("Built {0} ({1:N0} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green

if (-not $Publish) { Write-Host "Not published. To publish: .\release.ps1 -Publish -Tag $Tag"; return }

$notes = @"
Unzip anywhere and double-click **Install.cmd**. It installs the Orchestrator and the Launcher into C:\Apps, puts ``tickets``
on your PATH, and opens the dashboard. Its Welcome window installs the tools (git, the GitHub CLI, Claude Code, Python, the .NET 10
runtimes), signs you in to GitHub and Claude, and sets up a workspace: an empty one for your own repos, or MindAttic's.

To drive it from your phone over Tailscale and Remote Desktop, see
[docs/TAILSCALE.md](https://github.com/mindattic/MindAttic.Orchestrator/blob/main/docs/TAILSCALE.md).
"@
gh release create $Tag $zip --repo mindattic/MindAttic.Orchestrator --title "MindAttic Orchestrator $Tag" --notes $notes --target $commit
if ($LASTEXITCODE -ne 0) { throw 'gh release create failed.' }
Write-Host "Published $Tag" -ForegroundColor Green
