#Requires -Version 5.1
<#
.SYNOPSIS
    Turns a fresh Windows 11 machine (a Hyper-V VM, an HVD, a spare PC) into the MindAttic Orchestrator host, so the daemon,
    every Claude CLI, their builds and their memory live there instead of on your own PC (M.ORCH-T1473).

.DESCRIPTION
    Run it on the new machine, signed in as the Windows user the agents will run as (a local account is simplest):

        powershell -ExecutionPolicy Bypass -File <kit>\setup-host.ps1 -Kit <kit>

    where <kit> is the folder `orchestrator machine handoff --kit <folder>` wrote on your PC (C:\LocalNuGet's packages, your
    settings without secrets, and this script). It asks for administrator rights once (one UAC prompt), and for three things
    only you can give: your GitHub sign-in (a browser page), the Windows password for automatic sign-in (so the host comes
    back by itself after Windows Update restarts it), and each Claude login's sign-in (browser pages).

    Every step checks first and does only what's missing, so running it again repairs a half-finished or broken host.

      Administrator part (elevated, once):
        1. D: for the workspace: a blank extra disk becomes D:, else D: is mapped to C:\MindAttic-D at every boot (the
           workspace path D:\Projects\MindAttic is built into every MindAttic app). A DVD drive sitting on D: moves to Z:.
        2. git (+LFS), GitHub CLI, the .NET 10 SDK, Node.js LTS, Python 3, PowerShell 7 and Windows Terminal (winget).
        3. C:\LocalNuGet filled from the kit; long paths on.
        4. Never sleep or hibernate; Remote Desktop on; the gateway's port open in the firewall.
        5. Automatic sign-in at boot (Sysinternals Autologon keeps the password as an LSA secret, never in plain text).
      Your part (as you):
        6. git identity from the kit, GitHub sign-in, Claude Code.
        7. The workspace: the root repo and the core apps' repos cloned (or pulled), your settings restored.
        8. MindAttic.Vault, Tickets, Launcher, Orchestrator and Scheduler built and installed, the agent standard's global
           commands and commit hooks installed.
        9. Every other repo in the workspace cloned (orchestrator workspace build).
       10. `orchestrator machine host`: the gateway on for the network and the daemon started. It prints the one line to
           run on your PC (`orchestrator machine connect ...`), also saved on this machine's desktop.
       11. Claude sign-ins (`claude auth login` and `orchestrator login --all`), then the dashboard opens.

.PARAMETER Kit
    The host kit folder from `orchestrator machine handoff --kit` (or `orchestrator machine kit`). Without it nothing from
    C:\LocalNuGet comes across and the apps can't build.
.PARAMETER Workspace
    The workspace root. Leave it: D:\Projects\MindAttic is built into the apps.
.PARAMETER RootRemote
    The workspace root repo. Default: the kit's, else https://github.com/mindattic/codex-standard.git.
.PARAMETER Bind
    Where the gateway listens: 0.0.0.0 (every network, the default) or one address, such as a Tailscale 100.x one.
.PARAMETER SkipAutoLogon
    Don't set up automatic sign-in (then sign in yourself after every restart, or the daemon and CLIs don't run).
.PARAMETER SkipLogins
    Don't run the Claude sign-ins (do them later: claude auth login, orchestrator login --all).
.PARAMETER CoreOnly
    Clone only the root and the core apps' repos, not every repo in the workspace.
#>
[CmdletBinding()]
param(
    [string]$Kit,
    [string]$Workspace = 'D:\Projects\MindAttic',
    [string]$RootRemote,
    [string]$Bind = '0.0.0.0',
    [switch]$SkipAutoLogon,
    [switch]$SkipLogins,
    [switch]$CoreOnly,
    # Internal: the elevated half this script starts for itself.
    [switch]$AdminPhase,
    [string]$ForUser
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$LogDir = 'C:\MindAttic-Setup'
$null = New-Item -ItemType Directory -Force -Path $LogDir
$Log = Join-Path $LogDir 'setup-host.log'
$GatewayPort = 7443
$DFolder = 'C:\MindAttic-D'
$Apps = 'C:\Apps'
$Feed = 'C:\LocalNuGet'
$CoreRepos = @('MindAttic.Vault', 'MindAttic.Tickets', 'MindAttic.Launcher', 'MindAttic.Scheduler', 'MindAttic.Orchestrator')
$OrchestratorCli = Join-Path $Apps 'MindAttic.Orchestrator\Cli\orchestrator.exe'
$OrchestratorExe = Join-Path $Apps 'MindAttic.Orchestrator\MindAttic.Orchestrator.Desktop.exe'

function Say([string]$text, [string]$color = 'Gray') {
    $line = "[{0:HH:mm:ss}] {1}" -f (Get-Date), $text
    Write-Host $line -ForegroundColor $color
    Add-Content -Path $Log -Value $line -Encoding UTF8
}
function Step([string]$text) { Say ''; Say "== $text" 'Cyan' }
function Fail([string]$text) { Say "FAILED: $text" 'Red'; throw $text }

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal $id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Update-PathFromRegistry {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = (@($machine, $user) | Where-Object { $_ }) -join ';'
}

function Has([string]$command) { [bool](Get-Command $command -ErrorAction SilentlyContinue) }

function Read-KitManifest {
    if (-not $Kit) { return $null }
    $file = Join-Path $Kit 'kit.json'
    if (Test-Path $file) { return Get-Content $file -Raw | ConvertFrom-Json }
    return $null
}

# ---------------------------------------------------------------------------------------------------------------------
# Administrator part
# ---------------------------------------------------------------------------------------------------------------------

function Ensure-Winget {
    if (Has 'winget') { return }
    Say 'winget missing: registering App Installer for this user.'
    try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe } catch { }
    Update-PathFromRegistry
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps'
    if (-not (Has 'winget') -and (Test-Path (Join-Path $local 'winget.exe'))) { $env:Path += ";$local" }
    if (-not (Has 'winget')) { Fail 'winget (App Installer) is not available. Install "App Installer" from the Microsoft Store, then run this again.' }
}

function Install-Package([string]$id, [string]$name, [string]$override) {
    $listed = winget list --id $id -e --accept-source-agreements 2>$null | Out-String
    if ($LASTEXITCODE -eq 0 -and $listed -match [regex]::Escape($id)) { Say "$name is installed."; return }
    Say "Installing $name ($id)..."
    $wargs = @('install', '--id', $id, '-e', '--silent', '--accept-source-agreements', '--accept-package-agreements', '--disable-interactivity')
    if ($override) { $wargs += @('--override', $override) }
    & winget @wargs | Out-Null
    # 0 ok; -1978335189 already installed / no newer; -1978335135 already installed.
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189 -and $LASTEXITCODE -ne -1978335135) { Say "winget couldn't install $name (exit $LASTEXITCODE); see above." 'Yellow' }
}

function Ensure-DDrive {
    $optical = Get-CimInstance Win32_Volume -Filter "DriveLetter='D:'" -ErrorAction SilentlyContinue
    if ($optical -and $optical.DriveType -eq 5) {
        $free = @('Z:', 'Y:', 'X:', 'W:') | Where-Object { -not (Get-CimInstance Win32_Volume -Filter "DriveLetter='$_'") } | Select-Object -First 1
        Say "Moving the DVD drive from D: to $free."
        $optical | Set-CimInstance -Property @{ DriveLetter = $free }
    }
    if (Test-Path 'D:\') { Say 'D: is there.'; return }

    $raw = Get-Disk | Where-Object { $_.PartitionStyle -eq 'RAW' -and -not $_.IsBoot -and -not $_.IsSystem } | Select-Object -First 1
    if ($raw) {
        Say "Formatting the blank disk $($raw.Number) ($([math]::Round($raw.Size / 1GB)) GB) as D:."
        Initialize-Disk -Number $raw.Number -PartitionStyle GPT
        New-Partition -DiskNumber $raw.Number -UseMaximumSize -DriveLetter D | Format-Volume -FileSystem NTFS -NewFileSystemLabel 'Projects' -Confirm:$false | Out-Null
        return
    }
    # No spare disk: D: is C:\MindAttic-D, mapped by Windows at every boot (a persistent subst); the user part maps it for
    # this sign-in too, so no restart is needed now.
    $null = New-Item -ItemType Directory -Force -Path $DFolder
    $key = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices'
    $want = "\??\$DFolder"
    if ((Get-ItemProperty -Path $key -Name 'D:' -ErrorAction SilentlyContinue).'D:' -ne $want) {
        Set-ItemProperty -Path $key -Name 'D:' -Value $want
        Say "D: will be $DFolder at every boot (no spare disk to give it)."
    }
}

function Grant-UserModify([string]$path) {
    $null = New-Item -ItemType Directory -Force -Path $path
    if ($ForUser) { & icacls $path /grant "${ForUser}:(OI)(CI)M" /T /C /Q | Out-Null }
}

function Invoke-AdminPhase {
    Step 'Administrator part'
    Ensure-DDrive

    Step 'Tools (winget)'
    Ensure-Winget
    Install-Package 'Git.Git' 'git'
    Install-Package 'GitHub.cli' 'GitHub CLI'
    Install-Package 'Microsoft.DotNet.SDK.10' '.NET 10 SDK'
    Install-Package 'OpenJS.NodeJS.LTS' 'Node.js LTS'
    Install-Package 'Python.Python.3.13' 'Python 3' '/quiet InstallAllUsers=1 PrependPath=1 Include_launcher=1'
    Install-Package 'Microsoft.PowerShell' 'PowerShell 7'
    Install-Package 'Microsoft.WindowsTerminal' 'Windows Terminal'
    Update-PathFromRegistry
    if (Has 'git') {
        & git config --system core.longpaths true 2>$null
        & git lfs install --system 2>$null | Out-Null
    }
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' -Name 'LongPathsEnabled' -Value 1 -Type DWord

    Step 'Folders and packages'
    Grant-UserModify $Apps
    Grant-UserModify $Feed
    if (Test-Path $DFolder) { Grant-UserModify $DFolder }
    if ($Kit -and (Test-Path (Join-Path $Kit 'LocalNuGet'))) {
        $copied = 0
        foreach ($pkg in Get-ChildItem (Join-Path $Kit 'LocalNuGet') -Filter *.nupkg) {
            $target = Join-Path $Feed $pkg.Name
            if (-not (Test-Path $target) -or (Get-Item $target).Length -ne $pkg.Length) { Copy-Item $pkg.FullName $target -Force; $copied++ }
        }
        Say "C:\LocalNuGet: $copied package(s) copied from the kit."
    }
    else { Say 'No kit LocalNuGet folder: the MindAttic apps need it to build (orchestrator machine kit <folder> on your PC).' 'Yellow' }

    Step 'Always on'
    & powercfg /change standby-timeout-ac 0 | Out-Null
    & powercfg /change hibernate-timeout-ac 0 | Out-Null
    & powercfg /hibernate off 2>$null | Out-Null
    Say 'Never sleeps or hibernates.'
    try {
        Set-ItemProperty -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' -Name 'fDenyTSConnections' -Value 0
        Enable-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue
        Say 'Remote Desktop on (to see the full dashboard: mstsc /v:<this machine>).'
    }
    catch { Say "Remote Desktop couldn't be turned on (Windows 11 Home has none): $($_.Exception.Message)" 'Yellow' }
    $rule = 'MindAttic Orchestrator Gateway'
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Action Allow -Protocol TCP -LocalPort $GatewayPort | Out-Null
    }
    Say "Firewall: inbound TCP $GatewayPort (the gateway) allowed."

    if (-not $SkipAutoLogon) {
        Step 'Automatic sign-in'
        $winlogon = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
        if ($winlogon.AutoAdminLogon -eq '1' -and $winlogon.DefaultUserName) {
            Say "Already signs in as $($winlogon.DefaultUserName) at boot."
        }
        else {
            Install-Package 'Microsoft.Sysinternals.Autologon' 'Sysinternals Autologon'
            Update-PathFromRegistry
            $tool = Get-Command 'Autologon64.exe', 'Autologon.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $tool) { $tool = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter 'Autologon64.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 }
            if (-not $tool) { Say 'Autologon not found; set automatic sign-in yourself (netplwiz), or the host stops working after a restart.' 'Yellow' }
            else {
                $user, $domain = if ($ForUser -match '\\') { $ForUser.Split('\')[1], $ForUser.Split('\')[0] } else { $ForUser, $env:COMPUTERNAME }
                $secure = Read-Host "Windows password for $ForUser (for automatic sign-in after restarts; kept as an LSA secret)" -AsSecureString
                $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
                $path = if ($tool -is [IO.FileInfo]) { $tool.FullName } else { $tool.Source }
                & $path /accepteula $user $domain $plain | Out-Null
                $plain = $null
                Say "Signs in as $ForUser at boot."
            }
        }
    }
    Say 'Administrator part done.' 'Green'
}

# ---------------------------------------------------------------------------------------------------------------------
# User part
# ---------------------------------------------------------------------------------------------------------------------

function Invoke-Native([string]$what, [scriptblock]$block) {
    & $block
    if ($LASTEXITCODE -ne 0) { Fail "$what (exit $LASTEXITCODE)" }
}

function Ensure-Identity($manifest) {
    Step 'git and GitHub'
    if (-not (& git config --global user.name) -and $manifest.GitUserName) { & git config --global user.name $manifest.GitUserName }
    if (-not (& git config --global user.email) -and $manifest.GitUserEmail) { & git config --global user.email $manifest.GitUserEmail }
    & git config --global core.longpaths true
    & git config --global pull.ff only
    & gh auth status 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Say 'Sign in to GitHub (a browser page opens; use the account that can push to the mindattic repos).' 'Yellow'
        Invoke-Native 'gh auth login' { gh auth login --hostname github.com --git-protocol https --web }
    }
    & gh auth setup-git | Out-Null
    Say "GitHub: $((& gh api user --jq .login) 2>$null)"
}

function Ensure-Claude {
    Step 'Claude Code'
    Update-PathFromRegistry
    $userBin = Join-Path $env:USERPROFILE '.local\bin'
    if (Test-Path $userBin) { $env:Path = "$userBin;$env:Path" }
    if (Has 'claude') { Say "Claude Code $(& claude --version 2>$null)"; return }
    Say 'Installing Claude Code (native installer)...'
    try { Invoke-Expression (Invoke-RestMethod 'https://claude.ai/install.ps1') } catch { Say "Native installer failed: $($_.Exception.Message); trying npm." 'Yellow' }
    if (Test-Path $userBin) { $env:Path = "$userBin;$env:Path" }
    if (-not (Has 'claude')) { & npm install -g '@anthropic-ai/claude-code' | Out-Null; Update-PathFromRegistry }
    if (-not (Has 'claude')) { Fail 'Claude Code could not be installed.' }
}

function Clone-Or-Pull([string]$remote, [string]$path) {
    if (Test-Path (Join-Path $path '.git')) {
        & git -C $path pull --ff-only -q 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Say "  $(Split-Path $path -Leaf): couldn't fast-forward (local changes?); left as it is." 'Yellow' }
        return
    }
    Say "  cloning $remote"
    Invoke-Native "git clone $remote" { git clone -q $remote $path }
}

function Ensure-Workspace($manifest) {
    Step "Workspace $Workspace"
    if (-not (Test-Path 'D:\') -and (Test-Path $DFolder)) {
        & subst D: $DFolder
        Say "D: mapped to $DFolder for this sign-in (Windows maps it at every boot from now on)."
    }
    if (-not (Test-Path 'D:\')) { Fail 'There is no D: drive. Run this script again (it maps one), or add a second disk.' }
    $root = if ($RootRemote) { $RootRemote } elseif ($manifest.RootRemote) { $manifest.RootRemote } else { 'https://github.com/mindattic/codex-standard.git' }
    $parent = Split-Path $Workspace -Parent
    $null = New-Item -ItemType Directory -Force -Path $parent
    if (-not (Test-Path (Join-Path $Workspace '.git'))) {
        if ((Test-Path $Workspace) -and (Get-ChildItem $Workspace -Force | Select-Object -First 1)) {
            Fail "$Workspace exists, isn't empty and isn't the workspace repo. Move it aside and run this again."
        }
        Say "  cloning the workspace root ($root)"
        Invoke-Native 'git clone (workspace root)' { git clone -q $root $Workspace }
    }
    else { Clone-Or-Pull $root $Workspace }

    $listFile = Join-Path $Workspace 'docs\workspace-repos.json'
    $repos = if (Test-Path $listFile) { (Get-Content $listFile -Raw | ConvertFrom-Json).Repos } else { @() }
    foreach ($name in $CoreRepos) {
        $entry = $repos | Where-Object { $_.Name -eq $name } | Select-Object -First 1
        $remote = if ($entry) { $entry.Remote } else { "https://github.com/mindattic/$name.git" }
        Clone-Or-Pull $remote (Join-Path $Workspace $name)
    }
}

function Restore-Settings {
    if (-not $Kit) { return }
    $pairs = @(
        @{ From = 'settings\Global.settings.json'; To = Join-Path $env:APPDATA 'MindAttic\Global\settings.json' },
        @{ From = 'settings\Orchestrator.settings.json'; To = Join-Path $env:LOCALAPPDATA 'MindAttic\Orchestrator\settings.json' }
    )
    foreach ($p in $pairs) {
        $from = Join-Path $Kit $p.From
        if ((Test-Path $from) -and -not (Test-Path $p.To)) {
            $null = New-Item -ItemType Directory -Force -Path (Split-Path $p.To -Parent)
            Copy-Item $from $p.To
            Say "Settings restored: $($p.To)"
        }
    }
}

function Install-App([string]$repo, [string]$script = 'run.ps1') {
    $path = Join-Path (Join-Path $Workspace $repo) $script
    if (-not (Test-Path $path)) { Say "  $repo has no $script; skipped." 'Yellow'; return }
    Say "  $repo ($script)..."
    & powershell -NoProfile -ExecutionPolicy Bypass -File $path *>> $Log
    if ($LASTEXITCODE -ne 0) { Fail "$repo's $script failed (exit $LASTEXITCODE); the log is $Log." }
}

function Ensure-Apps {
    Step 'Core apps (build and install)'
    Restore-Settings
    Install-App 'MindAttic.Vault'
    Install-App 'MindAttic.Tickets'
    Install-App 'MindAttic.Launcher'
    Install-App 'MindAttic.Orchestrator'
    # After the Orchestrator: its Orchestrator.Daemon job starts the installed exe at once (and at every sign-in from now on).
    Install-App 'MindAttic.Scheduler' 'install.ps1'
    Update-PathFromRegistry
    if (-not (Test-Path $OrchestratorCli)) { Fail "The Orchestrator CLI isn't at $OrchestratorCli after its run.ps1." }

    Step 'Agent standard'
    $standard = Join-Path $Workspace 'mindattic-agent-standard'
    foreach ($s in 'install.ps1', 'install-commit-hooks.ps1') {
        $file = Join-Path $standard $s
        if (Test-Path $file) { & powershell -NoProfile -ExecutionPolicy Bypass -File $file -WorkspaceRoot $Workspace *>> $Log; Say "  $s done." }
    }
}

function Ensure-AllRepos {
    if ($CoreOnly) { return }
    Step 'Every other repo (orchestrator workspace build)'
    & $OrchestratorCli workspace build $Workspace
    if ($LASTEXITCODE -ne 0) { Say 'Some repos failed to clone (listed above); run this script again to retry them.' 'Yellow' }
    $standard = Join-Path $Workspace 'mindattic-agent-standard\install-commit-hooks.ps1'
    if (Test-Path $standard) { & powershell -NoProfile -ExecutionPolicy Bypass -File $standard -WorkspaceRoot $Workspace *>> $Log }
}

function Become-Host {
    Step 'Host role'
    $out = & $OrchestratorCli machine host --bind $Bind 2>&1 | Out-String
    Write-Host $out
    Add-Content -Path $Log -Value ($out -replace '--owner-key \S+', '--owner-key ***') -Encoding UTF8
    $line = ($out -split "`r?`n" | Where-Object { $_ -match 'orchestrator machine connect' } | Select-Object -First 1)
    if ($line) {
        $desktop = [Environment]::GetFolderPath('Desktop')
        $file = Join-Path $desktop 'Connect your PC to this host.txt'
        @(
            'Run this on your PC (after pulling and deploying the newest MindAttic.Orchestrator there):',
            '',
            $line.Trim(),
            '',
            'It holds this host''s owner key: keep it to yourself, and delete this file once your PC is connected.'
        ) | Set-Content -Path $file -Encoding UTF8
        Say "Saved to $file" 'Green'
    }
    else { Say 'The gateway is not up yet; `orchestrator machine` prints the connect line once it is.' 'Yellow' }
}

function Ensure-Logins {
    if ($SkipLogins) { return }
    Step 'Claude sign-ins'
    & claude auth status 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Say 'Sign the global Claude login in (a browser page opens).' 'Yellow'
        & claude auth login
    }
    Say "Signing in every member's own login (orchestrator login --all); skip any you don't want here." 'Yellow'
    & $OrchestratorCli login --all
}

# ---------------------------------------------------------------------------------------------------------------------

if ($AdminPhase) {
    try { Invoke-AdminPhase; exit 0 } catch { Say $_.Exception.Message 'Red'; exit 1 }
}

Say "MindAttic Orchestrator host setup on $env:COMPUTERNAME as $env:USERDOMAIN\$env:USERNAME (log: $Log)" 'Cyan'
if ($Kit) { $Kit = (Resolve-Path $Kit).Path }
$manifest = Read-KitManifest
if ($manifest) { Say "Kit from $($manifest.SourceMachine), made $($manifest.CreatedUtc)." }

# The administrator part runs elevated in its own window (one UAC prompt); everything else runs as you, so the daemon, the
# CLIs and their files belong to you, not to the administrator.
$self = $MyInvocation.MyCommand.Path
$adminArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$self`"", '-AdminPhase', '-ForUser', "`"$env:USERDOMAIN\$env:USERNAME`"")
if ($Kit) { $adminArgs += @('-Kit', "`"$Kit`"") }
if ($SkipAutoLogon) { $adminArgs += '-SkipAutoLogon' }
if (Test-Admin) {
    Say 'Running elevated: the apps and the daemon would be installed for the administrator. Run this from a normal (non-admin) PowerShell.' 'Yellow'
}
Step 'Asking for administrator rights (one prompt)'
$admin = Start-Process powershell -Verb RunAs -ArgumentList $adminArgs -Wait -PassThru
if ($admin.ExitCode -ne 0) { Fail "The administrator part failed; see $Log." }
Update-PathFromRegistry

Ensure-Identity $manifest
Ensure-Claude
Ensure-Workspace $manifest
Ensure-Apps
Ensure-AllRepos
Become-Host
Ensure-Logins

Step 'Done'
Say 'This machine is the MindAttic Orchestrator host. The dashboard opens now; /allocate in its CLIs works the tickets.' 'Green'
Say 'On your PC: pull, deploy MindAttic.Orchestrator (run.ps1), then run the connect line above. From then on `orchestrator machine pull` brings you the newest code.' 'Green'
if (Test-Path $OrchestratorExe) { Start-Process $OrchestratorExe }
