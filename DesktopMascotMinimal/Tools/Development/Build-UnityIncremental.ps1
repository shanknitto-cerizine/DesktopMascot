[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'ProductIdentity.ps1')

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
$player = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerFileName")
$logDirectory = Join-Path $projectRoot 'NativePlugin\out'
$log = Join-Path $logDirectory 'unity-development-incremental-build.log'
$tempDirectory = Join-Path $projectRoot 'Temp'
$lockPath = Join-Path $tempDirectory 'DevelopmentBuild.lock'
$statusPath = Join-Path $tempDirectory 'DevelopmentBuild.status.json'
$lockStream = $null
$unityProcess = $null
$startedAt = [DateTimeOffset]::Now
$exitCode = -1

function Write-BuildStatus(
    [string]$state,
    [Nullable[DateTimeOffset]]$finishedAt,
    [int]$resultCode) {
    $playerLastWriteTime = $null
    if (Test-Path -LiteralPath $player) {
        $playerLastWriteTime =
            (Get-Item -LiteralPath $player).LastWriteTime.ToString('o')
    }
    $status = [ordered]@{
        state = $state
        pid = $PID
        unityPid = if ($null -ne $unityProcess) {
            $unityProcess.Id
        } else {
            $null
        }
        startedAt = $startedAt.ToString('o')
        finishedAt = if ($null -ne $finishedAt) {
            ([DateTimeOffset]$finishedAt).ToString('o')
        } else {
            $null
        }
        exitCode = $resultCode
        playerPath = $player
        playerLastWriteTime = $playerLastWriteTime
        logPath = $log
        scriptPath = $PSCommandPath
    }
    $status | ConvertTo-Json | Set-Content `
        -LiteralPath $statusPath `
        -Encoding utf8
}

if (-not (Test-Path -LiteralPath $unity)) {
    throw "Unity.exe was not found: $unity"
}
if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}
if (-not (Test-Path -LiteralPath $tempDirectory)) {
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null
}

if (Test-Path -LiteralPath $lockPath) {
    $existingPid = 0
    try {
        $lockData = Get-Content -Raw -LiteralPath $lockPath |
            ConvertFrom-Json
        $existingPid = [int]$lockData.pid
    }
    catch {
        Write-Warning (
            '[Build-UnityIncremental] Existing build lock could not be ' +
            'parsed; treating it as stale.')
    }
    if ($existingPid -gt 0 -and
        $null -ne (
            Get-Process -Id $existingPid -ErrorAction SilentlyContinue)) {
        throw (
            '[Build-UnityIncremental] Another incremental build is ' +
            "already running. PID=$existingPid")
    }
    Write-Warning (
        '[Build-UnityIncremental] Reclaiming stale build lock.')
    Remove-Item -LiteralPath $lockPath -Force
}

try {
    $lockStream = [System.IO.File]::Open(
        $lockPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    $lockContent = [ordered]@{
        pid = $PID
        startTime = $startedAt.ToString('o')
        scriptPath = $PSCommandPath
    } | ConvertTo-Json -Compress
    $lockBytes = [System.Text.Encoding]::UTF8.GetBytes($lockContent)
    $lockStream.Write($lockBytes, 0, $lockBytes.Length)
    $lockStream.Flush()
    Write-Host '[Build-UnityIncremental] Build lock acquired.'
    Write-BuildStatus 'Running' $null -1

    if (Test-Path -LiteralPath $log) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        Move-Item -LiteralPath $log -Destination (
            Join-Path $logDirectory (
                "unity-development-incremental-build-$timestamp.log"))
    }

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-timestamps',
        '-projectPath',
        ('"{0}"' -f $projectRoot),
        '-buildTarget',
        'Win64',
        '-buildWindows64Player',
        ('"{0}"' -f $player),
        '-logFile',
        ('"{0}"' -f $log)
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $unityProcess = Start-Process `
        -FilePath $unity `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden
    Write-BuildStatus 'Running' $null -1
    $unityProcess.WaitForExit()
    $stopwatch.Stop()
    $exitCode = $unityProcess.ExitCode

    Write-Host (
        "Unity incremental build milliseconds: {0}" -f
        $stopwatch.ElapsedMilliseconds)
    Write-Host ("Unity exit code: {0}" -f $exitCode)
    Write-Host ("Player: {0}" -f $player)
    Write-Host ("Build log: {0}" -f $log)

    if (Test-Path -LiteralPath $log) {
        Select-String -LiteralPath $log -Pattern (
            'Build Successful|Build Finished, Result|warning CS|error CS|' +
            'Shader error|Failed to build player'
        ) | ForEach-Object { Write-Host $_.Line }
    }
    if ($exitCode -ne 0) {
        throw "Unity incremental build failed with exit code $exitCode."
    }
    if (-not (Test-Path -LiteralPath $player)) {
        throw (
            'Unity returned success but the Player was not found: ' +
            $player)
    }
    Write-BuildStatus 'Succeeded' ([DateTimeOffset]::Now) $exitCode
}
catch {
    Write-BuildStatus 'Failed' ([DateTimeOffset]::Now) $exitCode
    throw
}
finally {
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }
    if (Test-Path -LiteralPath $lockPath) {
        Remove-Item -LiteralPath $lockPath -Force
    }
    Write-Host '[Build-UnityIncremental] Build lock released.'
}
