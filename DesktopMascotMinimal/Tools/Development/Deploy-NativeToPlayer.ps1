[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'ProductIdentity.ps1')

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$sourceDll = Join-Path $projectRoot (
    'Assets\Plugins\x86_64\DesktopMascotNative.dll')
$player = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerFileName")
$destinationDll = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerDataDirectoryName\Plugins\x86_64\DesktopMascotNative.dll")

if (-not (Test-Path -LiteralPath $sourceDll)) {
    throw "Source DLL was not found: $sourceDll"
}
if (-not (Test-Path -LiteralPath $player)) {
    throw "DevelopmentCurrent Player was not found: $player"
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
    throw (
        "Development Player is running (PID: $ids). " +
        'Close it before deployment.')
}

$destinationDirectory = Split-Path -Parent $destinationDll
if (-not (Test-Path -LiteralPath $destinationDirectory)) {
    throw "Player plugin directory was not found: $destinationDirectory"
}

if (Test-Path -LiteralPath $destinationDll) {
    try {
        $lockProbe = [System.IO.File]::Open(
            $destinationDll,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $lockProbe.Dispose()
        Write-Host (
            '[Deploy-NativeToPlayer] Destination DLL lock check: Passed')
    }
    catch [System.IO.IOException] {
        throw (
            '[Deploy-NativeToPlayer] Destination DLL is locked by a ' +
            'running process.')
    }
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Copy-Item -LiteralPath $sourceDll -Destination $destinationDll -Force
$stopwatch.Stop()

$sourceHash = Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256
$destinationHash =
    Get-FileHash -LiteralPath $destinationDll -Algorithm SHA256
if ($sourceHash.Hash -ne $destinationHash.Hash) {
    throw ("DLL SHA-256 mismatch. Source={0}, Destination={1}" -f $sourceHash.Hash, $destinationHash.Hash)
}

Write-Host ("DLL deploy milliseconds: {0}" -f $stopwatch.ElapsedMilliseconds)
Write-Host ("Source: {0}" -f $sourceDll)
Write-Host ("Destination: {0}" -f $destinationDll)
Write-Host ("SHA-256: {0}" -f $sourceHash.Hash)
