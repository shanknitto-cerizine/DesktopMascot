[CmdletBinding()]
param(
    [switch]$NoWait
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Build-Native.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
& (Join-Path $PSScriptRoot 'Deploy-NativeToPlayer.ps1')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
if ($NoWait) {
    & (Join-Path $PSScriptRoot 'Run-DevelopmentPlayer.ps1') -NoWait
}
else {
    & (Join-Path $PSScriptRoot 'Run-DevelopmentPlayer.ps1')
}
exit $LASTEXITCODE
