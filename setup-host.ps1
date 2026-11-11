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
        6. SQL Server 2022 Developer (the localhost instance, you and Administrators as sysadmin), SQL Server LocalDB and
           SQL Server Management Studio, each only if missing (M.ORCH-T1486).
        6a. The dev environment: every package in dev-environment.json (Visual Studio with its workloads, Unity Hub and the
           editor, Notepad++, ArtCraft, the SDKs, runtimes, DirectX and redistributables; M.ORCH-T1489), each only if missing.
      Restarts: when Windows or SQL Server setup needs a restart, setup restarts the machine itself and carries on after
      the automatic sign-in (a RunOnce entry starts it again; the administrator part re-runs through an elevated
      scheduled task, \MindAttic\Setup\Admin, so no second UAC prompt). At most 3 restarts; all of it is removed when done.
      Your part (as you):
        6b. The database backups (-Databases, else the kit folder's sibling "databases") restored onto the instance each
           came from: databases\localhost\*.bak onto SQL Server, databases\localdb\*.bak onto LocalDB. A database that's
           already there is left alone.
        6c. git identity from the kit, GitHub sign-in, Claude Code.
        7. The workspace: the root repo and the core apps' repos cloned (or pulled), your settings restored.
        8. MindAttic.Vault, Tickets, Launcher, Orchestrator and Scheduler built and installed, the agent standard's global
           commands and commit hooks installed, and a desktop icon, "Rebuild MindAttic dev environment", that runs this
           setup again (the workspace's copy, with this run's options) to repair or update the machine with one double-click.
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
.PARAMETER Databases
    The folder of database backups to restore (localhost\*.bak, localdb\*.bak). Default: "databases" beside the kit folder.
.PARAMETER SkipSql
    Don't install SQL Server, LocalDB or SSMS, and don't restore backups.
.PARAMETER DevEnvironment
    The dev-environment manifest to install. Default: the dev-environment.json beside this script (the workspace's newest
    when run from the desktop icon), else the kit's.
.PARAMETER SkipDevEnvironment
    Don't install the dev environment (Visual Studio, Unity, editors, SDKs, redistributables).
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
    [string]$Databases,
    [switch]$SkipSql,
    [string]$DevEnvironment,
    [switch]$SkipDevEnvironment,
    # Internal: the elevated half this script starts for itself, and the run that resumes after a restart.
    [switch]$AdminPhase,
    [string]$ForUser,
    [switch]$Resumed
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
$SqlDir = Join-Path $LogDir 'sql'
$StateFile = Join-Path $LogDir 'state.json'
$ResumeFile = Join-Path $LogDir 'resume.cmd'
$SetupTaskPath = '\MindAttic\Setup\'
$AdminTaskName = 'Admin'
$RunOnceKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\RunOnce'
$RunOnceName = 'MindAtticHostSetup'
$MaxRestarts = 3
$RestartExitCode = 3010
# Microsoft's own SQL Server 2025 bootstrappers: the InstallerUrls of winget's Microsoft.SQLServer.2025.Developer (Standard
# Developer) and .Express packages, 17.0.1000.7 (M.ORCH-T1576). These were the 2022 ones, whose Developer bootstrapper no longer
# starts, and whose LocalDB (16.x) Ensure-LocalDb never takes as installed (it wants 17+), so every run tried it again.
# The Orchestrator daemon's daily SQL Server check (M.ORCH-T1488) keeps the six lines below fresh from winget: a newer version or
# bootstrapper URL of the same package is written here, committed and pushed, and the admin is texted. A newer major (another
# year's package) is only told, never written: the rest of this script's SQL Server steps name 2025 and its 170 folder.
$SseiDevPackage = 'Microsoft.SQLServer.2025.Developer'
$SseiDevVersion = '17.0.1000.7'
$SseiDevUrl = 'https://download.microsoft.com/download/77dc60d3-0139-4dad-83c8-bb52ab22db01/SQL2025-SSEI-StdDev.exe'
$SseiExprPackage = 'Microsoft.SQLServer.2025.Express'
$SseiExprVersion = '17.0.1000.7'
$SseiExprUrl = 'https://download.microsoft.com/download/7ab8f535-7eb8-4b16-82eb-eca0fa2d38f3/SQL2025-SSEI-Expr.exe'

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

# ---------------------------------------------------------------------------------------------------------------------
# Restarts: resume after sign-in, at most $MaxRestarts times (M.ORCH-T1486)
# ---------------------------------------------------------------------------------------------------------------------

function Get-SetupState {
    try { if (Test-Path $StateFile) { return Get-Content $StateFile -Raw | ConvertFrom-Json } } catch { }
    [pscustomobject]@{ Restarts = 0 }
}

function Save-SetupState($state) { $state | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8 }

function Test-RestartPending {
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') { return $true }
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') { return $true }
    $renames = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations
    return [bool]$renames
}

# The arguments this run was started with, for the run that resumes it (the internal ones left out).
function Get-ScriptArgs {
    $a = @()
    if ($Kit) { $a += "-Kit `"$Kit`"" }
    if ($Databases) { $a += "-Databases `"$Databases`"" }
    if ($Workspace -ne 'D:\Projects\MindAttic') { $a += "-Workspace `"$Workspace`"" }
    if ($RootRemote) { $a += "-RootRemote `"$RootRemote`"" }
    if ($Bind -ne '0.0.0.0') { $a += "-Bind `"$Bind`"" }
    # Only one given on the command line: the default is resolved afresh by each run (the desktop icon's takes the workspace's).
    if ($script:DevEnvironmentArg) { $a += "-DevEnvironment `"$script:DevEnvironmentArg`"" }
    foreach ($s in @(@('SkipAutoLogon', $SkipAutoLogon), @('SkipLogins', $SkipLogins), @('CoreOnly', $CoreOnly), @('SkipSql', $SkipSql), @('SkipDevEnvironment', $SkipDevEnvironment))) {
        if ($s[1]) { $a += "-$($s[0])" }
    }
    $a -join ' '
}

# Every sign-in until setup is done starts it again (HKCU RunOnce, through a short .cmd: RunOnce skips long commands).
function Set-ResumeAtSignIn {
    $self = $script:SelfPath
    Set-Content -Path $ResumeFile -Encoding ASCII -Value @(
        '@echo off',
        "powershell.exe -NoProfile -NoExit -ExecutionPolicy Bypass -File `"$self`" $(Get-ScriptArgs) -Resumed"
    )
    Set-ItemProperty -Path $RunOnceKey -Name $RunOnceName -Value "cmd.exe /c `"$ResumeFile`""
}

function Clear-Resume {
    Remove-ItemProperty -Path $RunOnceKey -Name $RunOnceName -ErrorAction SilentlyContinue
    Remove-Item $ResumeFile, $StateFile -Force -ErrorAction SilentlyContinue
}

# Administrator part, before a restart: the run after it starts this part through a task that runs elevated without a UAC
# prompt (RunLevel Highest, as you), so an unattended resume isn't stopped at a prompt nobody sees. The part removes the
# task again once it finishes without needing a restart. Only for a user who is an administrator (Highest elevates no one else).
function Register-AdminTask {
    # This part runs elevated: as $ForUser themselves when they approved the prompt with their own (administrator) account.
    $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $admins = @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction SilentlyContinue | ForEach-Object Name)
    if ($me -ne $ForUser -and $admins -notcontains $ForUser) { Say "$ForUser isn't an administrator: after the restart, approve the UAC prompt (or sign in as an administrator) to carry on." 'Yellow'; return }
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$script:SelfPath`" -AdminPhase -ForUser `"$ForUser`" $(Get-ScriptArgs)"
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
    $principal = New-ScheduledTaskPrincipal -UserId $ForUser -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 4)
    Register-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
}

function Unregister-AdminTask {
    if (Get-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName -Confirm:$false
    }
}

# User part: runs the administrator part, through the elevated task when a restart left one, else with one UAC prompt.
# Returns its exit code ($RestartExitCode: restart now).
function Invoke-AdminPart {
    $task = Get-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName -ErrorAction SilentlyContinue
    if ($task) {
        try {
            Step 'Administrator part (through the elevated setup task, no prompt)'
            Start-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName
            Start-Sleep -Seconds 3
            while ((Get-ScheduledTask -TaskPath $SetupTaskPath -TaskName $AdminTaskName -ErrorAction SilentlyContinue).State -in 'Running', 'Queued') { Start-Sleep -Seconds 5 }
            $info = Get-ScheduledTaskInfo -TaskPath $SetupTaskPath -TaskName $AdminTaskName -ErrorAction SilentlyContinue
            return $(if ($info) { [int]$info.LastTaskResult } else { 0 }) # gone = it finished and removed itself
        }
        catch { Say "The setup task couldn't be started ($($_.Exception.Message)); asking for administrator rights instead." 'Yellow' }
    }
    Step 'Asking for administrator rights (one prompt)'
    $adminArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$script:SelfPath`" -AdminPhase -ForUser `"$env:USERDOMAIN\$env:USERNAME`" $(Get-ScriptArgs)"
    $admin = Start-Process powershell -Verb RunAs -ArgumentList $adminArgs -Wait -PassThru
    return $admin.ExitCode
}

# Administrator part: counts the restart and says whether to take it (false once $MaxRestarts are used up: carry on).
function Request-Restart([string]$why) {
    $state = Get-SetupState
    if ([int]$state.Restarts -ge $MaxRestarts) {
        Say "$why But setup has restarted $MaxRestarts times already; carrying on without restarting." 'Yellow'
        return $false
    }
    $state.Restarts = [int]$state.Restarts + 1
    Save-SetupState $state
    Say "$why Restart $($state.Restarts) of at most ${MaxRestarts}: setup carries on by itself after the automatic sign-in." 'Yellow'
    $script:RestartNeeded = $true
    return $true
}

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
    if (-not (Has 'winget')) {
        # No Store on this image (a sandbox, a stripped VM or HVD): App Installer and its dependencies from winget's own release.
        Say 'Installing winget from github.com/microsoft/winget-cli (no Microsoft Store here)...'
        $dl = Join-Path $LogDir 'winget'
        $null = New-Item -ItemType Directory -Force -Path $dl
        $base = 'https://github.com/microsoft/winget-cli/releases/latest/download'
        Invoke-WebRequest "$base/DesktopAppInstaller_Dependencies.zip" -OutFile "$dl\deps.zip" -UseBasicParsing
        Expand-Archive "$dl\deps.zip" -DestinationPath "$dl\deps" -Force
        $arch = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
        foreach ($dep in Get-ChildItem "$dl\deps" -Recurse -Include *.appx, *.msix | Where-Object { $_.FullName -match "\\$arch\\" -or $_.Name -match "_$arch" }) {
            try { Add-AppxPackage -Path $dep.FullName -ErrorAction Stop } catch { Say "  dependency $($dep.Name): $($_.Exception.Message)" 'Yellow' }
        }
        Invoke-WebRequest "$base/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle" -OutFile "$dl\winget.msixbundle" -UseBasicParsing
        Add-AppxPackage -Path "$dl\winget.msixbundle"
        if (Test-Path (Join-Path $local 'winget.exe')) { $env:Path += ";$local" }
    }
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

function Get-Download([string]$url, [string]$name) {
    $null = New-Item -ItemType Directory -Force -Path $SqlDir
    $file = Join-Path $SqlDir $name
    if (-not (Test-Path $file)) { Say "  downloading $name"; Invoke-WebRequest $url -OutFile $file -UseBasicParsing }
    return $file
}

# Microsoft's bootstrapper fetching install media into $SqlDir\<folder> (one at a time: two at once save nothing).
function Get-SqlMedia([string]$bootstrapper, [string]$mediaType, [string]$folder, [string]$filter) {
    $target = Join-Path $SqlDir $folder
    $found = Get-ChildItem $target -Recurse -Filter $filter -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.FullName }
    Say "  downloading the $mediaType media (this can take a while)"
    $p = Start-Process $bootstrapper -ArgumentList "/ACTION=Download /MEDIAPATH=`"$target`" /MEDIATYPE=$mediaType /QUIET" -Wait -PassThru
    $found = Get-ChildItem $target -Recurse -Filter $filter -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) { Fail "The SQL Server bootstrapper didn't download the $mediaType media (exit $($p.ExitCode))." }
    return $found.FullName
}

# SQL Server 2025 Developer as the default instance (localhost), Windows sign-in, you and Administrators sysadmin, started
# at boot. 2025 because Microsoft's SQL Server 2022 Developer bootstrapper no longer starts (it crashes fetching its manifest),
# and a 2022 server or LocalDB restores onto 2025 (never the other way).
function Ensure-SqlServer {
    if (Get-Service 'MSSQLSERVER' -ErrorAction SilentlyContinue) {
        Say 'SQL Server (localhost) is installed.'
        Set-Service 'MSSQLSERVER' -StartupType Automatic
        if ((Get-Service 'MSSQLSERVER').Status -ne 'Running') { Start-Service 'MSSQLSERVER' }
        return
    }
    if ((Test-RestartPending) -and (Request-Restart 'Windows has a restart pending, and SQL Server setup won''t run until it happens.')) { return }
    Say 'Installing SQL Server 2025 Developer (localhost)...'
    $ssei = Get-Download $SseiDevUrl 'SQL2025-SSEI-StdDev.exe'
    $iso = Get-SqlMedia $ssei 'ISO' 'developer' '*.iso'
    $mounted = Mount-DiskImage -ImagePath $iso -PassThru
    try {
        $drive = ($mounted | Get-Volume).DriveLetter
        $sysadmins = "`"$ForUser`" `"BUILTIN\Administrators`""
        $setupArgs = "/Q /ACTION=Install /FEATURES=SQLENGINE /INSTANCENAME=MSSQLSERVER /SQLSVCACCOUNT=`"NT Service\MSSQLSERVER`" " +
            "/SQLSYSADMINACCOUNTS=$sysadmins /SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=1 /UPDATEENABLED=False /IACCEPTSQLSERVERLICENSETERMS"
        $p = Start-Process "${drive}:\setup.exe" -ArgumentList $setupArgs -Wait -PassThru
    }
    finally { Dismount-DiskImage -ImagePath $iso | Out-Null }
    $summary = 'C:\Program Files\Microsoft SQL Server\170\Setup Bootstrap\Log\Summary.txt'
    switch ($p.ExitCode) {
        0 { Say 'SQL Server installed.' 'Green'; Remove-Item (Split-Path $iso -Parent) -Recurse -Force -ErrorAction SilentlyContinue }
        3010 { Say 'SQL Server installed.' 'Green'; Remove-Item (Split-Path $iso -Parent) -Recurse -Force -ErrorAction SilentlyContinue; $null = Request-Restart 'SQL Server setup asks for a restart.' }
        default {
            if ((Test-RestartPending) -and (Request-Restart "SQL Server setup stopped (exit $($p.ExitCode)), and Windows has a restart pending.")) { return }
            Say "SQL Server setup failed (exit $($p.ExitCode)); see $summary. Run this script again to retry." 'Yellow'
        }
    }
}

function Find-SqlLocalDb {
    Get-ChildItem 'C:\Program Files\Microsoft SQL Server\*\Tools\Binn\SqlLocalDB.exe' -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.FullName -replace '^.*Microsoft SQL Server\\(\d+)\\.*$', '$1') } -Descending | Select-Object -First 1
}

# SQL Server 2025 LocalDB ((localdb)\MSSQLLocalDB, where Prose, IdiotProof and the other apps keep their databases).
function Ensure-LocalDb {
    $exe = Find-SqlLocalDb
    if ($exe -and $exe.FullName -match '\\(1[7-9]|[2-9]\d)\d\\') { Say "SQL Server LocalDB is installed ($($exe.FullName))."; return }
    Say 'Installing SQL Server 2025 LocalDB...'
    $ssei = Get-Download $SseiExprUrl 'SQL2025-SSEI-Expr.exe'
    $msi = Get-SqlMedia $ssei 'LocalDB' 'localdb' 'SqlLocalDB.msi'
    $p = Start-Process 'msiexec.exe' -ArgumentList "/i `"$msi`" /qn /norestart IACCEPTSQLLOCALDBLICENSETERMS=YES /l*v `"$SqlDir\localdb-install.log`"" -Wait -PassThru
    switch ($p.ExitCode) {
        0 { Say 'LocalDB installed.' 'Green' }
        3010 { Say 'LocalDB installed.' 'Green'; $null = Request-Restart 'LocalDB setup asks for a restart.' }
        default { Say "LocalDB setup failed (exit $($p.ExitCode)); see $SqlDir\localdb-install.log." 'Yellow' }
    }
}

function Ensure-Sql {
    if ($SkipSql) { return }
    Step 'SQL Server, LocalDB and Management Studio'
    Ensure-SqlServer
    if ($script:RestartNeeded) { return }
    Ensure-LocalDb
    if ($script:RestartNeeded) { return }
    Install-Package 'Microsoft.SQLServerManagementStudio.22' 'SQL Server Management Studio 22'
    # The full instance reads the backups as its service account.
    if ($Databases -and (Test-Path $Databases)) { & icacls $Databases /grant 'NT SERVICE\MSSQLSERVER:(OI)(CI)RX' /T /C /Q | Out-Null }
}

# The dev environment (M.ORCH-T1489): every package in the manifest, each only if missing. Visual Studio's workloads go in
# through its Override (the installer's own --add arguments).
function Ensure-DevEnvironment {
    if ($SkipDevEnvironment) { return }
    Step 'Dev environment'
    if (-not $DevEnvironment -or -not (Test-Path $DevEnvironment)) { Say 'No dev-environment.json (in the kit or beside this script); the dev environment is skipped.' 'Yellow'; return }
    $packages = @((Get-Content $DevEnvironment -Raw | ConvertFrom-Json).Packages | Where-Object { $_.Id })
    Say "$($packages.Count) package(s) from $DevEnvironment (Visual Studio alone can take an hour)."
    foreach ($p in $packages) {
        $name = if ($p.Name) { $p.Name } else { $p.Id }
        Install-Package $p.Id $name $p.Override
    }
    Update-PathFromRegistry
    if (Test-RestartPending) { $null = Request-Restart 'The dev environment''s installers ask for a restart.' }
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

    Ensure-Sql
    if ($script:RestartNeeded) { Register-AdminTask; return }
    Ensure-DevEnvironment
    if ($script:RestartNeeded) { Register-AdminTask; return }
    Unregister-AdminTask
    Say 'Administrator part done.' 'Green'
}

# ---------------------------------------------------------------------------------------------------------------------
# User part
# ---------------------------------------------------------------------------------------------------------------------

function Invoke-Native([string]$what, [scriptblock]$block) {
    & $block
    if ($LASTEXITCODE -ne 0) { Fail "$what (exit $LASTEXITCODE)" }
}

function Invoke-Sql([string]$server, [string]$sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$server;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=60"
    $c.Open()
    try {
        $cmd = $c.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $sql
        $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); return ,$t
    }
    finally { $c.Close() }
}

# Restores each backup onto $server, its files in $dataDir, unless a database of that name is already there.
function Restore-Backups([string]$server, [object[]]$files, [string]$dataDir) {
    foreach ($bak in $files) {
        $db = $bak.BaseName
        # Quoted for T-SQL: a path or name with ' (C:\Users\O'Brien) or ] broke the statement.
        $bakPath = $bak.FullName -replace "'", "''"
        $dbName = $db -replace '\]', ']]'
        try {
            if ((Invoke-Sql $server "SELECT COUNT(*) AS n FROM sys.databases WHERE name = N'$($db -replace "'", "''")'").Rows[0].n -gt 0) { Say "  $db is already on $server."; continue }
            $moves = foreach ($f in (Invoke-Sql $server "RESTORE FILELISTONLY FROM DISK = N'$bakPath'").Rows) {
                $suffix = if ($f.Type -eq 'L') { '_log.ldf' } elseif ([int]$f.FileId -gt 2) { "_$($f.FileId).ndf" } else { '.mdf' }
                "MOVE N'$($f.LogicalName -replace "'", "''")' TO N'$(("$dataDir\$db$suffix") -replace "'", "''")'"
            }
            Say "  restoring $db onto $server..."
            $null = Invoke-Sql $server "RESTORE DATABASE [$dbName] FROM DISK = N'$bakPath' WITH $($moves -join ', '), RECOVERY" # checksums in the backup are verified anyway
        }
        catch { Say "  $db couldn't be restored onto ${server}: $($_.Exception.Message)" 'Yellow' }
    }
}

# The backups from your PC (databases\localhost\*.bak, databases\localdb\*.bak; a flat folder: Orchestrator and Tickets to
# localhost, the rest to LocalDB), each onto the instance it came from.
function Restore-Databases {
    if ($SkipSql -or -not $Databases) { return }
    Step "Databases from $Databases"
    $all = @(Get-ChildItem $Databases -Recurse -Filter *.bak -ErrorAction SilentlyContinue)
    if (-not $all) { Say 'No .bak files there.'; return }
    $full = @($all | Where-Object { $_.Directory.Name -eq 'localhost' -or ($_.DirectoryName -eq $Databases -and $_.BaseName -in 'MindAttic.Orchestrator', 'MindAttic.Tickets') })
    $local = @($all | Where-Object { $full -notcontains $_ })
    if ($full) {
        try {
            $dataDir = (Invoke-Sql 'localhost' "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260)) AS p").Rows[0].p.TrimEnd('\')
            Restore-Backups 'localhost' $full $dataDir
        }
        catch { Say "SQL Server (localhost) isn't reachable, so its backups wait: $($_.Exception.Message)" 'Yellow' }
    }
    if ($local) {
        $exe = Find-SqlLocalDb
        if (-not $exe) { Say 'LocalDB isn''t installed, so its backups wait.' 'Yellow' }
        else {
            & $exe.FullName create MSSQLLocalDB -s 2>&1 | Out-Null # makes the default instance if it isn't there yet, and starts it
            Restore-Backups '(localdb)\MSSQLLocalDB' $local $env:USERPROFILE
        }
    }
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

# The desktop icon that runs this setup again (M.ORCH-T1489): the workspace's copy of this script, which pulls bring up to
# date, with this run's options. Every step checks first, so a double-click repairs or updates the dev environment.
function Ensure-DesktopIcon {
    $script = Join-Path $Workspace 'MindAttic.Orchestrator\setup-host.ps1'
    if (-not (Test-Path $script)) { $script = $script:SelfPath }
    $desktop = [Environment]::GetFolderPath('Desktop')
    $link = Join-Path $desktop 'Rebuild MindAttic dev environment.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = "-NoProfile -NoExit -ExecutionPolicy Bypass -File `"$script`" $(Get-ScriptArgs)".TrimEnd()
    $shortcut.WorkingDirectory = Split-Path $script -Parent
    $shortcut.Description = 'Installs or repairs the MindAttic development environment and host (setup-host.ps1).'
    $shortcut.IconLocation = if (Test-Path $OrchestratorExe) { "$OrchestratorExe,0" } else { "$($shortcut.TargetPath),0" }
    $shortcut.Save()
    Say "Desktop icon: $link" 'Green'
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

$script:SelfPath = $MyInvocation.MyCommand.Path
$script:RestartNeeded = $false
if ($Kit) { $Kit = (Resolve-Path $Kit).Path }
if (-not $Databases -and $Kit) {
    $beside = Join-Path (Split-Path $Kit -Parent) 'databases'
    if (Test-Path $beside) { $Databases = $beside }
}
if ($Databases) { $Databases = (Resolve-Path $Databases).Path }
if ($DevEnvironment -and (Test-Path $DevEnvironment)) { $DevEnvironment = (Resolve-Path $DevEnvironment).Path }
$script:DevEnvironmentArg = $DevEnvironment
if (-not $DevEnvironment) {
    $DevEnvironment = @((Join-Path $PSScriptRoot 'dev-environment.json'), $(if ($Kit) { Join-Path $Kit 'dev-environment.json' })) |
        Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if ($AdminPhase) {
    try {
        Invoke-AdminPhase
        if ($script:RestartNeeded) { exit $RestartExitCode }
        exit 0
    }
    catch { Say $_.Exception.Message 'Red'; exit 1 }
}

Say "MindAttic Orchestrator host setup on $env:COMPUTERNAME as $env:USERDOMAIN\$env:USERNAME (log: $Log)" 'Cyan'
if ($Resumed) { Say "Carrying on after a restart (restarts so far: $((Get-SetupState).Restarts))." 'Cyan' }
$manifest = Read-KitManifest
if ($manifest) { Say "Kit from $($manifest.SourceMachine), made $($manifest.CreatedUtc)." }
if (Test-Admin) {
    Say 'Running elevated: the apps and the daemon would be installed for the administrator. Run this from a normal (non-admin) PowerShell.' 'Yellow'
}

# Any restart from here on (setup's own, or yours) carries on at the next sign-in, until setup is done.
Set-ResumeAtSignIn

# The administrator part runs elevated in its own window (one UAC prompt); everything else runs as you, so the daemon, the
# CLIs and their files belong to you, not to the administrator.
$adminExit = Invoke-AdminPart
if ($adminExit -eq $RestartExitCode) {
    Step 'Restarting'
    Say 'Restarting in 60 seconds to finish installing. Setup carries on by itself after the sign-in (shutdown /a cancels).' 'Yellow'
    & shutdown.exe /r /t 60 /c 'MindAttic host setup: restarting to finish installing; it carries on after sign-in.'
    exit 0
}
if ($adminExit -ne 0) { Fail "The administrator part failed (exit $adminExit); see $Log. Run this again to retry." }
Update-PathFromRegistry

Restore-Databases
Ensure-Identity $manifest
Ensure-Claude
Ensure-Workspace $manifest
Ensure-Apps
Ensure-DesktopIcon
Ensure-AllRepos
Become-Host
Ensure-Logins

Clear-Resume
Step 'Done'
Say 'This machine is the MindAttic Orchestrator host. The dashboard opens now; /allocate in its CLIs works the tickets.' 'Green'
Say 'On your PC: pull, deploy MindAttic.Orchestrator (run.ps1), then run the connect line above. From then on `orchestrator machine pull` brings you the newest code.' 'Green'
if (Test-Path $OrchestratorExe) { Start-Process $OrchestratorExe }
