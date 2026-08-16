# M-058 — Portable Windows x64 Release Candidate Foundation

## Purpose

M-058 makes the already validated normal-runtime product path reproducibly
packageable as a portable Windows x64 Release Candidate. It does not alter the
Conversation, Speech, Character, Settings, native rendering, or persistence
architectures.

## Canonical build input

- Unity Editor: 6000.3.20f1.
- Build entry: Tools/Release/Build-WindowsX64Release.ps1.
- Editor build entry: DesktopMascotReleaseBuild.BuildWindowsX64Release.
- Scene: the one enabled Editor Build Settings Scene,
  Assets/_Project/Scenes/MascotMain.unity.
- Target/options: StandaloneWindows64 and BuildOptions.None.
- Graphics API: exactly Direct3D12.
- Metadata source of truth: ProjectSettings/ProjectSettings.asset:
  あなたといつも, Ceritizine_poc, and 0.1.0.

The legacy DesktopMascotSetup.BuildWindows delegates to this entry and owns no
Release Scene or Player build policy.

## Artifact contract

The build refuses to overwrite Build/Release/0.1.0. Its output consists of the
portable folder, its versioned ZIP, and a ZIP SHA-256 sidecar. The portable
folder contains the Unity Player, release.json, manifest.sha256, README.txt,
and Notices/. The validator rejects unknown top-level dependencies and PDB,
ILK, log, DoNotShip, source, legacy product identity, and persisted-user-data
content. It verifies the Player native DLL is x64, has required Speech exports,
has no Debug CRT dependency, and exactly matches
Assets/Plugins/x86_64/DesktopMascotNative.dll by SHA-256.

The D3D12 artifact allowlist includes Unity's signed DirectML.dll companion:
Microsoft.AI.DirectML 1.13.1, SHA-256
5AB77CC5DB8E1544D386FD28586598317DA8DCBEF098FB86D8D8A60E739E0E5D.
Validation requires byte-identical parity with the Unity Windows Standalone
Support source and packages its dedicated Microsoft license notice.

## Bundled model and notices

Assets/_Project/Models/VRM/TEST_MODEL.vrm and its existing GUID
b2c4fdc826a0d7e4aa1efd906ba4780d are the authoritative tracked build input.
The .gitignore exception is intentionally limited to those two files. The
byte-identical obsolete Assets/_Project/Scripts/TEST_MODEL.vrm pair is removed
only after hash equality and zero serialized-reference checks; no Scene
reference or GUID is rewritten.

Notices includes model provenance, Noto OFL, UniVRM/UniGLTF MIT, an index, and
required Unity runtime package notices. Missing notice input is a build stop
condition.

## Release-only behavior

Non-Development Players ignore diagnostic mode markers and the direct
Development VRM-import marker, entering normal runtime. Development Players
retain the existing diagnostic behavior. Existing v1 persistence remains at
%LOCALAPPDATA%/DesktopMascotMinimal and is neither migrated nor bundled.

## Validation and stop conditions

Automated validation comprises source/build-input checks, canonical native
build, Unity Release build, artifact allow/deny validation, manifest and ZIP
hash generation, and native binary parity. User manual verification remains
required for clean first launch, orderly exit/restart, persistence
compatibility, bundled/imported Character, M-057 click-to-Speech including
Busy/NoMatch, Settings, drag/click, Single Instance, tray recovery, and no
residual process/window/icon.

Stop and return to design if the model GUID or Scene reference changes, the
duplicate hash or reference audit fails, native source/export redesign is
needed, a required artifact dependency or notice is unknown, Release CRT
portability fails, a M-057 regression appears, or scope must expand.

## Recorded validation result

Automated Validation, Manual Verification, and Visual Verification passed for
the `あなたといつも-0.1.0-windows-x64.zip` Release artifact. The user verified a
separate-folder ZIP expansion; Release launch, mascot and tray presentation;
existing external-character restoration; bundled-character restoration;
M-057 click-to-Speech, Busy drop, card close/re-show, drag classification,
Settings, restart persistence, Single Instance routing, Explorer tray
recovery, and orderly shutdown with no residual process/window/icon.

The user also performed clean-first-launch evidence by temporarily renaming
`%LOCALAPPDATA%\DesktopMascotMinimal`, verified bundled `TEST_MODEL`, no
unexpected Settings or abnormal Unity window, click-to-Speech, position save,
orderly exit, and restart restoration, then restored the original persistence
and reconfirmed its selected Character, position, click-to-Speech, and exit.
`%LOCALAPPDATA%\DesktopMascotMinimal_clean_M058` is retained as user-owned
temporary verification evidence and is not to be deleted by M-058.

Clean VM/another Windows account, long-duration soak, sleep/resume, and a full
Windows-version matrix are deferred final-product-acceptance work, not M-058
completion criteria. The Final Repository / Staged Audit passed; M-058 is
complete. Commit and push remain separately unauthorized.
