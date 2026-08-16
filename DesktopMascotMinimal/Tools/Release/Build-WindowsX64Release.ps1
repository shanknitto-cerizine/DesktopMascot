[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectSettingsPath = Join-Path $projectRoot 'ProjectSettings/ProjectSettings.asset'
$expectedProductName = [regex]::Unescape('\u3042\u306a\u305f\u3068\u3044\u3064\u3082')
$expectedCompanyName = 'Ceritizine_poc'
$expectedVersion = '0.1.0'
$settingsText = Get-Content -LiteralPath $projectSettingsPath -Raw

function Get-ProjectSettingValue([string]$Name) {
    $match = [regex]::Match($settingsText, "(?m)^\s*$($Name):\s*(.+?)\s*$")
    if (-not $match.Success) { throw "ProjectSettings is missing $Name." }
    return $match.Groups[1].Value.Trim('"')
}

$productName = [regex]::Unescape((Get-ProjectSettingValue 'productName'))
$companyName = Get-ProjectSettingValue 'companyName'
$version = Get-ProjectSettingValue 'bundleVersion'
if ($productName -ne $expectedProductName -or $companyName -ne $expectedCompanyName -or $version -ne $expectedVersion) {
    throw 'ProjectSettings metadata does not match the approved M-058 release identity.'
}

$releaseVersionRoot = Join-Path $projectRoot "Build/Release/$version"
$artifactName = "$productName-$version-windows-x64"
$artifactRoot = Join-Path $releaseVersionRoot $artifactName
$zipPath = Join-Path $releaseVersionRoot "$artifactName.zip"
if ((Test-Path -LiteralPath $artifactRoot) -or (Test-Path -LiteralPath $zipPath)) {
    throw "Release output already exists; refusing to overwrite version $version."
}
if (Test-Path -LiteralPath $releaseVersionRoot) {
    $priorEntries = Get-ChildItem -LiteralPath $releaseVersionRoot -Force |
        Where-Object { $_.Name -ne 'unity-release-build.log' }
    if ($priorEntries) {
        throw "Release output already exists; refusing to overwrite version $version."
    }
}

$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unityExe -PathType Leaf)) {
    throw 'Unity 6000.3.20f1 was not found at the canonical path.'
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'Tools/Development/Build-Native.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Canonical native Release build failed.' }

$playerPath = Join-Path $artifactRoot "$productName.exe"
$logPath = Join-Path $releaseVersionRoot 'unity-release-build.log'
$previousOutput = $env:DESKTOP_MASCOT_RELEASE_OUTPUT
try {
    $env:DESKTOP_MASCOT_RELEASE_OUTPUT = $playerPath
    $unityProcess = Start-Process -FilePath $unityExe -ArgumentList @(
        '-batchmode', '-nographics', '-quit', '-projectPath', $projectRoot,
        '-executeMethod', 'DesktopMascotReleaseBuild.BuildWindowsX64Release',
        '-logFile', $logPath) -Wait -PassThru
    if ($unityProcess.ExitCode -ne 0) {
        throw "Unity Release build failed. See $logPath"
    }
}
finally {
    $env:DESKTOP_MASCOT_RELEASE_OUTPUT = $previousOutput
}
Remove-Item -LiteralPath (Join-Path $artifactRoot 'release-build-receipt.json') -Force

$notices = Join-Path $artifactRoot 'Notices'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Release/Notices/TEST_MODEL-PROVENANCE.txt') -Destination $notices
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Release/Notices/UniVRM-UniGLTF-MIT.txt') -Destination $notices
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Release/Notices/THIRD-PARTY-NOTICES.txt') -Destination $notices
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/Release/Notices/Microsoft.AI.DirectML-1.13.1-NOTICE.txt') -Destination $notices
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/_Project/Runtime/Presentation/Speech/Fonts/OFL.txt') -Destination (Join-Path $notices 'NotoSansCJKjp-OFL.txt')

$inferenceNotices = Get-ChildItem -Path (Join-Path $projectRoot 'Library/PackageCache/com.unity.ai.inference@*/Third Party Notices.md') -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $inferenceNotices) { throw 'Required Unity AI Inference third-party notice was not found.' }
Copy-Item -LiteralPath $inferenceNotices.FullName -Destination (Join-Path $notices 'Unity-AI-Inference-Third-Party-Notices.txt')

$modelPath = Join-Path $projectRoot 'Assets/_Project/Models/VRM/TEST_MODEL.vrm'
$modelHash = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
$nativeHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'Assets/Plugins/x86_64/DesktopMascotNative.dll') -Algorithm SHA256).Hash
$unityDirectMlPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PlaybackEngines\WindowsStandaloneSupport\External\DirectML\x64\DirectML.dll'
$unityDirectMlHash = (Get-FileHash -LiteralPath $unityDirectMlPath -Algorithm SHA256).Hash
$releaseMetadata = [ordered]@{
    schemaVersion = 1
    productName = $productName
    companyName = $companyName
    version = $version
    platform = 'windows-x64'
    unityVersion = '6000.3.20f1'
    graphicsApi = 'Direct3D12'
    bundledModel = [ordered]@{
        path = 'Assets/_Project/Models/VRM/TEST_MODEL.vrm'
        sha256 = $modelHash
    }
    nativeDllSha256 = $nativeHash
    directMl = [ordered]@{
        component = 'Microsoft.AI.DirectML'
        version = '1.13.1'
        sha256 = $unityDirectMlHash
    }
}
$releaseMetadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $artifactRoot 'release.json') -Encoding utf8
@"
$productName $version portable Windows x64 release candidate

Run $productName.exe from this folder. No installer or updater is included.
This Player requires Windows x64 with Direct3D 12 support.
Persistent application data remains under %LOCALAPPDATA%\DesktopMascotMinimal.
See Notices for third-party licenses and TEST_MODEL provenance.
"@ | Set-Content -LiteralPath (Join-Path $artifactRoot 'README.txt') -Encoding utf8

$manifestPath = Join-Path $artifactRoot 'manifest.sha256'
Get-ChildItem -LiteralPath $artifactRoot -File -Recurse |
    Where-Object { $_.FullName -ne $manifestPath } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        $relative = $_.FullName.Substring($artifactRoot.Length + 1).Replace('\', '/')
        "$hash  $relative"
    } | Set-Content -LiteralPath $manifestPath -Encoding utf8

& (Join-Path $PSScriptRoot 'Verify-WindowsX64ReleaseArtifact.ps1') -ArtifactRoot $artifactRoot -ProductName $productName -SourceNativeDll (Join-Path $projectRoot 'Assets/Plugins/x86_64/DesktopMascotNative.dll') -UnityDirectMlDll $unityDirectMlPath
if ($LASTEXITCODE -ne 0) { throw 'Release artifact validation failed.' }

Compress-Archive -LiteralPath $artifactRoot -DestinationPath $zipPath -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $zipEntries = $zipArchive.Entries.FullName | ForEach-Object {
        $_.Replace('\', '/')
    }
    $requiredZipDirectMl = "$artifactName/DirectML.dll"
    $requiredZipNotice =
        "$artifactName/Notices/Microsoft.AI.DirectML-1.13.1-NOTICE.txt"
    if (($zipEntries -notcontains $requiredZipDirectMl) -or
        ($zipEntries -notcontains $requiredZipNotice)) {
        throw 'Release ZIP is missing DirectML.dll or its dedicated notice.'
    }
}
finally {
    $zipArchive.Dispose()
}
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$zipHash | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
Write-Host "Release artifact created: $zipPath"
