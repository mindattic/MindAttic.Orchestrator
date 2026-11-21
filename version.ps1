#Requires -Version 5.1
<#
.SYNOPSIS
    Prints the Orchestrator's whole-number version (M.ORCH-T1888): "vN", the number the window title shows.
.DESCRIPTION
    N = 1 + the commits since the csproj's OrchestratorVersionBase that touch src\ - the same count the build stamps
    (MindAttic.Orchestrator.Wpf.csproj's OrchestratorVersionFromGit). Run it after a commit to say which version it was.
.PARAMETER Ref
    The commit to count to. Default: HEAD.
#>
param([string]$Ref = 'HEAD')
$ErrorActionPreference = 'Stop'
$csproj = Join-Path $PSScriptRoot 'src\MindAttic.Orchestrator.Wpf\MindAttic.Orchestrator.Wpf.csproj'
$base = ([xml](Get-Content $csproj)).Project.PropertyGroup.OrchestratorVersionBase | Where-Object { $_ } | Select-Object -First 1
$count = (git -C $PSScriptRoot rev-list --count "$base..$Ref" -- src).Trim()
if ($LASTEXITCODE -ne 0) { throw 'git rev-list failed.' }
"v$([int]$count + 1)"
