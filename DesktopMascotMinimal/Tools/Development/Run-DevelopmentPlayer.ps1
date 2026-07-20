[CmdletBinding()]
param(
    [switch]$NoWait
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$player = Join-Path $projectRoot (
    'Build\DevelopmentCurrent\DesktopMascotMinimal.exe')
$logDirectory = Join-Path $projectRoot 'NativePlugin\out'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $logDirectory (
    "development-player-$timestamp.log")

if (-not (Test-Path -LiteralPath $player)) {
    throw "DevelopmentCurrent Player was not found: $player"
}
if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}

$processName = [System.IO.Path]::GetFileNameWithoutExtension($player)
$runningPlayers = @(
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                [System.IO.Path]::GetFullPath($_.Path) -eq
                    [System.IO.Path]::GetFullPath($player)
            }
            catch {
                $false
            }
        })
if ($runningPlayers.Count -ne 0) {
    $ids = ($runningPlayers | ForEach-Object { $_.Id }) -join ', '
    Write-Host (
        '[Run-DevelopmentPlayer] Development Player is already running. ' +
        "PID=$ids")
    Write-Host (
        '[Run-DevelopmentPlayer] A second Player was not started.')
    exit 1
}

$process = Start-Process `
    -FilePath $player `
    -ArgumentList @('-logFile', ('"{0}"' -f $log)) `
    -PassThru
Write-Host ("Player PID: {0}" -f $process.Id)
Write-Host ("Player log: {0}" -f $log)

if ($NoWait) {
    Write-Host 'Player started without waiting.'
    return
}

$process.WaitForExit()
Write-Host ("Player exit code: {0}" -f $process.ExitCode)
if (Test-Path -LiteralPath $log) {
    Write-Host 'Important Player.log lines:'
    Select-String -LiteralPath $log -Pattern (
        'DesktopMascot|Failure stage|Present|WindowPosition|Shutdown|Exception|error'
    ) | ForEach-Object { Write-Host $_.Line }
}
exit $process.ExitCode
