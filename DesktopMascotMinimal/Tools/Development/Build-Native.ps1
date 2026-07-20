[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$nativeDirectory = Join-Path $projectRoot 'NativePlugin'
$sourceDll = Join-Path $projectRoot (
    'Assets\Plugins\x86_64\DesktopMascotNative.dll')
$cmake = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe'
$vcvars = 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'

foreach ($path in @($nativeDirectory, $cmake, $vcvars)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path was not found: $path"
    }
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Push-Location $nativeDirectory
try {
    $command = ('call "{0}" && "{1}" --preset windows-relwithdebinfo && "{1}" --build --preset windows-relwithdebinfo' -f $vcvars, $cmake)
    & $env:ComSpec /d /s /c $command
    if ($LASTEXITCODE -ne 0) {
        throw "Native configure/build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
    $stopwatch.Stop()
}

if (-not (Test-Path -LiteralPath $sourceDll)) {
    throw "Native build succeeded but the DLL was not found: $sourceDll"
}

$item = Get-Item -LiteralPath $sourceDll
$hash = Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256
Write-Host ("Native build milliseconds: {0}" -f $stopwatch.ElapsedMilliseconds)
Write-Host ("DLL: {0}" -f $item.FullName)
Write-Host ("LastWriteTime: {0:o}" -f $item.LastWriteTime)
Write-Host ("SHA-256: {0}" -f $hash.Hash)
