[CmdletBinding()]
param(
    [switch]$NoWait,
    [switch]$AllowSecondaryLaunch,
    [ValidateRange(0, 3600)]
    [int]$OrderlyCloseAfterSeconds = 0,
    [string]$RuntimeVrmImportPath = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($NoWait -and $OrderlyCloseAfterSeconds -gt 0) {
    throw 'NoWait and OrderlyCloseAfterSeconds cannot be used together.'
}

. (Join-Path $PSScriptRoot 'ProductIdentity.ps1')

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$player = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerFileName")
$logDirectory = Join-Path $projectRoot 'NativePlugin\out'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$launchId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$log = Join-Path $logDirectory (
    "development-player-$timestamp-$launchId.log")

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
if ($runningPlayers.Count -ne 0 -and -not $AllowSecondaryLaunch) {
    $ids = ($runningPlayers | ForEach-Object { $_.Id }) -join ', '
    Write-Host (
        '[Run-DevelopmentPlayer] Development Player is already running. ' +
        "PID=$ids")
    Write-Host (
        '[Run-DevelopmentPlayer] A second Player was not started.')
    exit 1
}

$playerArguments = @(
    '-logFile',
    ('"{0}"' -f $log),
    '--desktop-mascot-development-launch')
if (-not [string]::IsNullOrWhiteSpace($RuntimeVrmImportPath)) {
    $runtimeVrmPath = [System.IO.Path]::GetFullPath(
        $RuntimeVrmImportPath)
    $playerArguments += (
        '"--runtime-vrm-import={0}"' -f $runtimeVrmPath)
}

$process = Start-Process `
    -FilePath $player `
    -ArgumentList $playerArguments `
    -PassThru
Write-Host ("Player PID: {0}" -f $process.Id)
Write-Host ("Player log: {0}" -f $log)

if ($NoWait) {
    Write-Host 'Player started without waiting.'
    return
}

if ($OrderlyCloseAfterSeconds -gt 0) {
    $exitedBeforeDeadline = $process.WaitForExit(
        $OrderlyCloseAfterSeconds * 1000)
    if (-not $exitedBeforeDeadline) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class DesktopMascotExactUnityWindowClose
{
    public delegate bool EnumWindowsCallback(IntPtr window, IntPtr state);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsCallback callback,
        IntPtr state);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr window,
        StringBuilder className,
        int maximumCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    public static bool Post(uint expectedProcessId)
    {
        var posted = false;
        EnumWindows(
            (window, state) =>
            {
                uint processId;
                GetWindowThreadProcessId(window, out processId);
                var className = new StringBuilder(64);
                GetClassName(window, className, className.Capacity);
                if (processId != expectedProcessId
                    || !string.Equals(
                        className.ToString(),
                        "UnityWndClass",
                        StringComparison.Ordinal))
                {
                    return true;
                }
                posted = PostMessage(
                    window,
                    0x0010,
                    IntPtr.Zero,
                    IntPtr.Zero);
                return false;
            },
            IntPtr.Zero);
        return posted;
    }
}
'@
        $posted = [DesktopMascotExactUnityWindowClose]::Post(
            [uint32]$process.Id)
        Write-Host (
            '[Run-DevelopmentPlayer] Exact UnityWndClass orderly close ' +
            "posted: $posted")
        if (-not $posted) {
            throw (
                'The exact current-process UnityWndClass was not found. ' +
                'No fallback or forced termination was performed.')
        }
    }
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
