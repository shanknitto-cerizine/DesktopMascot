[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$lockPath = Join-Path $projectRoot 'Temp\DevelopmentBuild.lock'
$statusPath = Join-Path $projectRoot (
    'Temp\DevelopmentBuild.status.json')
$log = Join-Path $projectRoot (
    'NativePlugin\out\unity-development-incremental-build.log')

$state = 'NotStarted'
$detail = $null
if (Test-Path -LiteralPath $statusPath) {
    try {
        $status = Get-Content -Raw -LiteralPath $statusPath |
            ConvertFrom-Json
        $state = [string]$status.state
        $detail = $status
    }
    catch {
        $state = 'Unknown'
    }
}

if (Test-Path -LiteralPath $lockPath) {
    $state = 'Running'
}
elseif (Test-Path -LiteralPath $log) {
    $tail = Get-Content -LiteralPath $log -Tail 120
    if ($tail -match 'Build Finished, Result: Success') {
        $state = 'Succeeded'
    }
    elseif ($tail -match (
        'Build Finished, Result: Failure|Failed to build player')) {
        $state = 'Failed'
    }
    elseif ($state -eq 'Running') {
        $state = 'Unknown'
    }
}

Write-Host (
    '[Get-UnityIncrementalBuildStatus] State: {0}' -f $state)
Write-Host (
    '[Get-UnityIncrementalBuildStatus] Build lock present: {0}' -f
    (Test-Path -LiteralPath $lockPath))
Write-Host (
    '[Get-UnityIncrementalBuildStatus] Build log: {0}' -f $log)
if ($null -ne $detail) {
    Write-Host (
        '[Get-UnityIncrementalBuildStatus] Script PID: {0}' -f
        $detail.pid)
    Write-Host (
        '[Get-UnityIncrementalBuildStatus] Unity PID: {0}' -f
        $detail.unityPid)
    Write-Host (
        '[Get-UnityIncrementalBuildStatus] Exit code: {0}' -f
        $detail.exitCode)
    Write-Host (
        '[Get-UnityIncrementalBuildStatus] Player timestamp: {0}' -f
        $detail.playerLastWriteTime)
}

if ($state -eq 'Failed' -or $state -eq 'Unknown') {
    exit 1
}
