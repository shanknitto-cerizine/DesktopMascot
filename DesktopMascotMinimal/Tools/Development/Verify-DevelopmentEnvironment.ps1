[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'ProductIdentity.ps1')

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
$cmake = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe'
$ninja = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\ninja.exe'
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
$nativeSource = Join-Path $projectRoot 'NativePlugin'
$nativeBuild = Join-Path $nativeSource (
    'out\build\windows-relwithdebinfo')
$sourceDll = Join-Path $projectRoot (
    'Assets\Plugins\x86_64\DesktopMascotNative.dll')
$player = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerFileName")
$playerPluginDirectory = Join-Path $projectRoot (
    "Build\DevelopmentCurrent\$DesktopMascotDevelopmentPlayerDataDirectoryName\Plugins\x86_64")

$checks = New-Object System.Collections.Generic.List[object]
function Add-PathCheck([string]$name, [string]$path) {
    $checks.Add([pscustomobject]@{
        Name = $name
        Available = Test-Path -LiteralPath $path
        Detail = $path
    })
}

Add-PathCheck 'Unity.exe' $unity
Add-PathCheck 'cmake.exe' $cmake
Add-PathCheck 'ninja.exe' $ninja
Add-PathCheck 'vcvars64.bat' $vcvars
Add-PathCheck 'Native source directory' $nativeSource
Add-PathCheck 'Native build directory' $nativeBuild
Add-PathCheck 'Source DLL' $sourceDll
Add-PathCheck 'DevelopmentCurrent Player' $player
Add-PathCheck 'Player plugin directory' $playerPluginDirectory

$developerToolsAvailable = $false
if (Test-Path -LiteralPath $vcvars) {
    $probe = ('call "{0}" >nul && where cl && where dumpbin' -f $vcvars)
    & $env:ComSpec /d /s /c $probe
    $developerToolsAvailable = $LASTEXITCODE -eq 0
}
$checks.Add([pscustomobject]@{
    Name = 'cl.exe / dumpbin.exe environment'
    Available = $developerToolsAvailable
    Detail = 'Visual Studio x64 developer environment'
})

$checks | Format-Table -AutoSize
$missing = @($checks | Where-Object { -not $_.Available })
if ($missing.Count -ne 0) {
    Write-Error ('Development environment verification failed. Missing: {0}' -f (($missing | ForEach-Object { $_.Name }) -join ', '))
    exit 1
}
Write-Host 'Development environment verification succeeded.'
