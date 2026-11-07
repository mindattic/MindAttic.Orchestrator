#Requires -Version 5.1
<#
.SYNOPSIS
    Fixes C:\pagefile.sys at a set size so it can never grow to fill the disk (M.ORCH-T1493).

.DESCRIPTION
    A system-managed pagefile grows whenever commit charge nears the commit limit. When the CLIs, their subagents and builds
    use up the RAM, it keeps growing until C: is full, and only a hard restart gets the machine back. With a fixed size the
    commit limit is fixed (RAM + pagefile): at worst one process fails an allocation, the disk never fills, and the
    Orchestrator's memory guard has a stable commit percent to scale work back against.

    Asks for administrator rights (one UAC prompt). Takes effect after a restart.

        powershell -ExecutionPolicy Bypass -File set-pagefile.ps1            # 16 GB on C:
        powershell -ExecutionPolicy Bypass -File set-pagefile.ps1 -SizeGb 24
        powershell -ExecutionPolicy Bypass -File set-pagefile.ps1 -Restore   # back to system-managed

.PARAMETER SizeGb
    The pagefile's fixed size (minimum = maximum). Default 16.

.PARAMETER Drive
    The drive the pagefile lives on. Default C:.

.PARAMETER Restore
    Hand the pagefile back to Windows (system-managed, unbounded).
#>
param(
    [ValidateRange(1, 256)][int]$SizeGb = 16,
    [ValidatePattern('^[A-Za-z]:$')][string]$Drive = 'C:',
    [switch]$Restore
)
$ErrorActionPreference = 'Stop'

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-SizeGb', $SizeGb, '-Drive', $Drive)
    if ($Restore) { $argList += '-Restore' }
    $p = Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $p.ExitCode
}

$cs = Get-CimInstance Win32_ComputerSystem
if ($Restore) {
    Set-CimInstance -InputObject $cs -Property @{ AutomaticManagedPagefile = $true }
    Write-Host 'Pagefile is system-managed again. Restart Windows for it to take effect.'
    exit 0
}

if ($cs.AutomaticManagedPagefile) {
    Set-CimInstance -InputObject $cs -Property @{ AutomaticManagedPagefile = $false }
}

$sizeMb = $SizeGb * 1024
$name = "$($Drive.ToUpper())\pagefile.sys"
$setting = Get-CimInstance Win32_PageFileSetting | Where-Object { $_.Name -ieq $name }
if (-not $setting) {
    $setting = New-CimInstance -ClassName Win32_PageFileSetting -Property @{ Name = $name }
}
Set-CimInstance -InputObject $setting -Property @{ InitialSize = [uint32]$sizeMb; MaximumSize = [uint32]$sizeMb }

Get-CimInstance Win32_PageFileSetting | Where-Object { $_.Name -ine $name } | ForEach-Object {
    Write-Host "Removing the other pagefile $($_.Name) so only $name remains."
    Remove-CimInstance -InputObject $_
}

$ramGb = [math]::Round($cs.TotalPhysicalMemory / 1GB, 1)
Write-Host "Pagefile fixed at $SizeGb GB on $Drive (commit limit about $([math]::Round($ramGb + $SizeGb)) GB with $ramGb GB RAM)."
Write-Host 'Restart Windows for it to take effect.'
