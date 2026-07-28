[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'ProductIdentity.ps1')

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$player = [System.IO.Path]::GetFullPath(
    (Join-Path $projectRoot (
        "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerFileName")))
$processName = [System.IO.Path]::GetFileNameWithoutExtension($player)

$players = @(
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                [System.IO.Path]::GetFullPath($_.Path) -eq $player
            }
            catch {
                $false
            }
        })

if ($players.Count -eq 0) {
    Write-Host (
        '[Stop-DevelopmentPlayer] No Development Player process is running.')
    exit 0
}

foreach ($process in $players) {
    Write-Host (
        '[Stop-DevelopmentPlayer] Player process found. PID={0}' -f
        $process.Id)
    $closeRequested = $process.CloseMainWindow()
    Write-Host (
        '[Stop-DevelopmentPlayer] CloseMainWindow requested: {0}' -f
        $closeRequested)
    if (-not $closeRequested) {
        throw (
            'Development Player PID={0} did not accept normal close. ' +
            'No forced termination was performed.' -f $process.Id)
    }
    $exited = $process.WaitForExit(15000)
    Write-Host (
        '[Stop-DevelopmentPlayer] Player exited normally: {0}' -f $exited)
    if (-not $exited) {
        throw (
            'Development Player PID={0} did not exit within 15 seconds. ' +
            'No forced termination was performed.' -f $process.Id)
    }
}

$remaining = @(
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object {
            try {
                [System.IO.Path]::GetFullPath($_.Path) -eq $player
            }
            catch {
                $false
            }
        })
$released = $remaining.Count -eq 0
Write-Host (
    '[Stop-DevelopmentPlayer] Player lock released: {0}' -f $released)
if (-not $released) {
    throw 'Development Player process still exists after normal close.'
}
