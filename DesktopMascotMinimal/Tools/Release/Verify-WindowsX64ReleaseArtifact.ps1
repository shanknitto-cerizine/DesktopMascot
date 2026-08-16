[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ArtifactRoot,
    [Parameter(Mandatory = $true)][string]$ProductName,
    [Parameter(Mandatory = $true)][string]$SourceNativeDll,
    [Parameter(Mandatory = $true)][string]$UnityDirectMlDll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$SourceNativeDll = [IO.Path]::GetFullPath($SourceNativeDll)
$UnityDirectMlDll = [IO.Path]::GetFullPath($UnityDirectMlDll)
if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) {
    throw "Artifact root is missing: $ArtifactRoot"
}
if (-not (Test-Path -LiteralPath $SourceNativeDll -PathType Leaf)) {
    throw "Source native DLL is missing: $SourceNativeDll"
}
if (-not (Test-Path -LiteralPath $UnityDirectMlDll -PathType Leaf)) {
    throw "Unity DirectML DLL is missing: $UnityDirectMlDll"
}

$allowedTopLevel = @(
    "$ProductName.exe", 'UnityPlayer.dll', 'UnityCrashHandler64.exe',
    ($ProductName + '_Data'), 'MonoBleedingEdge', 'D3D12', 'DirectML.dll',
    'Notices',
    'README.txt', 'release.json', 'manifest.sha256'
)
$actualTopLevel = Get-ChildItem -LiteralPath $ArtifactRoot -Force |
    ForEach-Object { $_.Name }
$unknownTopLevel = $actualTopLevel | Where-Object { $_ -notin $allowedTopLevel }
if ($unknownTopLevel) {
    throw "Unknown mandatory top-level artifact dependency: $($unknownTopLevel -join ', ')"
}

$requiredPaths = @(
    "$ProductName.exe", 'UnityPlayer.dll', ($ProductName + '_Data'),
    'DirectML.dll', 'Notices', 'README.txt', 'release.json', 'manifest.sha256'
)
foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactRoot $relativePath))) {
        throw "Required artifact item is missing: $relativePath"
    }
}

$manifestPath = Join-Path $ArtifactRoot 'manifest.sha256'
foreach ($line in Get-Content -LiteralPath $manifestPath) {
    if ($line -notmatch '^(?<hash>[0-9A-F]{64})  (?<relative>.+)$') {
        throw "Invalid manifest row: $line"
    }
    $manifestTarget = Join-Path $ArtifactRoot $matches.relative
    if (-not (Test-Path -LiteralPath $manifestTarget -PathType Leaf)) {
        throw "Manifest item is missing: $($matches.relative)"
    }
    $manifestHash = (Get-FileHash -LiteralPath $manifestTarget -Algorithm SHA256).Hash
    if ($manifestHash -ne $matches.hash) {
        throw "Manifest SHA-256 mismatch: $($matches.relative)"
    }
}

$forbiddenPattern = '\.(pdb|ilk|lib|exp|mdb|log)$|(^|\\)(DoNotShip|Logs?)($|\\)|(^|\\)(settings|window-position|character-selection)\.json$|DesktopMascotMinimal'
$forbidden = Get-ChildItem -LiteralPath $ArtifactRoot -Force -Recurse |
    Where-Object { $_.FullName.Substring($ArtifactRoot.Length + 1) -match $forbiddenPattern }
$unexpectedMap = Get-ChildItem -LiteralPath $ArtifactRoot -Force -Recurse -Filter '*.map' |
    Where-Object {
        $_.FullName.Substring($ArtifactRoot.Length + 1).Replace('\', '/') -notmatch
            '^MonoBleedingEdge/etc/mono/(2\.0|4\.0|4\.5)/settings\.map$'
    }
if ($unexpectedMap) {
    $forbidden += $unexpectedMap
}
if ($forbidden) {
    throw "Forbidden artifact content: $($forbidden.FullName -join ', ')"
}

$dataDirectoryName = "$ProductName" + '_Data'
$artifactDirectMlDll = Join-Path $ArtifactRoot 'DirectML.dll'
$unityDirectMlHash = (Get-FileHash -LiteralPath $UnityDirectMlDll -Algorithm SHA256).Hash
$artifactDirectMlHash = (Get-FileHash -LiteralPath $artifactDirectMlDll -Algorithm SHA256).Hash
if ($unityDirectMlHash -ne $artifactDirectMlHash) {
    throw 'DirectML DLL SHA-256 parity failed between Unity installation and Release Player.'
}
if ($artifactDirectMlHash -ne '5AB77CC5DB8E1544D386FD28586598317DA8DCBEF098FB86D8D8A60E739E0E5D') {
    throw 'DirectML DLL is not the approved Microsoft.AI.DirectML 1.13.1 binary.'
}
$playerNativeDll = Get-ChildItem -LiteralPath (Join-Path $ArtifactRoot "$dataDirectoryName/Plugins/x86_64") -Filter DesktopMascotNative.dll -File -ErrorAction Stop
$sourceHash = (Get-FileHash -LiteralPath $SourceNativeDll -Algorithm SHA256).Hash
$playerHash = (Get-FileHash -LiteralPath $playerNativeDll.FullName -Algorithm SHA256).Hash
if ($sourceHash -ne $playerHash) {
    throw 'Native DLL SHA-256 parity failed between Assets and Release Player.'
}

$dumpbin = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\*\bin\Hostx64\x64\dumpbin.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $dumpbin) { throw 'dumpbin.exe was not found for native binary validation.' }
$headers = [string]::Join([Environment]::NewLine, (& $dumpbin /headers $playerNativeDll.FullName))
if ($LASTEXITCODE -ne 0 -or $headers -notmatch 'machine \(x64\)') {
    throw 'Native DLL is not a valid x64 binary.'
}
$exports = [string]::Join([Environment]::NewLine, (& $dumpbin /exports $playerNativeDll.FullName))
if ($LASTEXITCODE -ne 0 -or $exports -notmatch 'DMN_IsSpeechPresentationReady' -or $exports -notmatch 'DMN_BeginSpeechPresentationShutdown') {
    throw 'Native DLL release export validation failed.'
}
$dependencies = [string]::Join([Environment]::NewLine, (& $dumpbin /dependents $playerNativeDll.FullName))
if ($LASTEXITCODE -ne 0 -or $dependencies -match 'MSVCP140D|VCRUNTIME140D|ucrtbased') {
    throw 'Native DLL has a Debug CRT or dependency validation failed.'
}

Write-Host "Release artifact validation passed. Native SHA-256: $playerHash; DirectML SHA-256: $artifactDirectMlHash"
