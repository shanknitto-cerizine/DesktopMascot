# Repository Structure Design

Milestone: M-046 — Repository Structure Cleanup

Status: Completed

Architecture baseline: M-046

## Purpose

M-046 aligns physical file placement with the responsibility boundaries
validated through M-045. The repository cleanup itself does not change runtime
behavior, serialized field names, namespaces, persistence schemas, diagnostic
mode names, rendering, ownership, or shutdown contracts. The explicitly
approved prerequisite fix, Tray Startup Recovery Robustness, adds bounded tray
recovery diagnostics and behavior without changing existing export names,
command IDs, ownership, visibility, or orderly-shutdown contracts.

## Unity source layout

Production and shared runtime code lives under:

```text
Assets/_Project/Runtime/
  Core/
  Character/
    Selection/
  Persistence/
    CharacterSelection/
    WindowPosition/
  Presentation/
    Settings/
  Settings/
    Foundation/
  Platform/
    Windows/
      CharacterSelection/
      Desktop/
      Interaction/
      Position/
      Presentation/
      SingleInstance/
  Legacy/
```

- `Core` owns bootstrap, runtime orchestration, configuration, and the Camera
  source pipeline.
- `Character` owns active/runtime character behavior and selection.
- `Persistence` contains the dedicated character-selection and window-position
  records and stores.
- `Settings/Foundation` contains the settings defaults, serialization, store,
  and persistence manager.
- `Presentation/Settings` contains platform-neutral Settings state, binding,
  validation, view, and presentation interfaces.
- `Platform/Windows` contains HWND, tray, context-menu, visibility,
  single-instance, native-position, native-interaction, and the Windows file
  picker integration.
- `Legacy` retains older sample-scene helpers whose deletion is not yet proven
  safe. It is not a location for new production features.

Focused diagnostics live separately:

```text
Assets/_Project/Diagnostics/
  Runtime/
  Presentation/
  Character/
  Persistence/
  Windows/
```

Editor-only code lives under `Assets/_Project/Editor`. The bundled duplicate
`Assets/_Project/Scripts/TEST_MODEL.vrm` remains in place because its removal
or relocation is outside this structural milestone and redistribution or user
asset assumptions must not be made.

No assembly definition was introduced. Existing namespaces, type names,
partial declarations, serialized fields, and public APIs remain unchanged.

## Unity GUID and serialized-reference policy

Every moved Unity asset was moved with its existing `.meta` sidecar. The file
contents and existing GUID values are unchanged. Scene-bound MonoBehaviours
continue to resolve through those GUIDs, including:

- `MascotCharacter`
- `MascotExpressionController`
- `NativeWindowController`
- `TopMostWindowController`
- `TransparentWindowController`
- `MascotDragController`
- `MascotInteractionController`

The managed and Windows partial definitions of
`UnityPlayerWindowVisibilityController` remain together under the same
Windows presentation responsibility.

New responsibility folders have new folder-only GUIDs. These GUIDs do not
replace or regenerate any moved asset GUID.

## Native plugin boundary

The validated native layout remains unchanged:

```text
NativePlugin/
  include/
  src/
  unity/
  docs/
  out/
  CMakeLists.txt
  CMakePresets.json
```

`include`, `src`, copied Unity headers, CMake inputs, DLL name, exports, ABI,
and output location are source/build contracts. `docs` is documentation.
`out` is generated build and diagnostic evidence and must never become a
source dependency.

A broad native source split into Bridge, Composition, D3D12, Region,
Interaction, Tray, and Diagnostics remains future work. M-046 does not take
that risk because the current source list and include graph are already
validated and physically moving them would add no product value.

## Development tools boundary

The canonical commands remain directly available at
`Tools/Development/*.ps1`, especially:

```text
Tools/Development/Build-Native.ps1
Tools/Development/Build-UnityIncremental.ps1
Tools/Development/Run-DevelopmentPlayer.ps1
```

Their fixed paths, `Build/DevelopmentCurrent` contract, executable naming,
`Assets/Plugins/x86_64/DesktopMascotNative.dll`, and `NativePlugin/out` log
contract remain unchanged. Internal Build/Launch/Diagnostics/Validation
subfolders may be considered later only with thin compatibility wrappers and
dedicated validation.

## Fixed paths intentionally preserved

- `Assets/_Project/Scenes/MascotMain.unity`
- `Assets/_Project/Scripts/TEST_MODEL.vrm`
- `Assets/Plugins/x86_64/DesktopMascotNative.dll`
- `NativePlugin/CMakeLists.txt`
- `NativePlugin/CMakePresets.json`
- `NativePlugin/out`
- `Tools/Development/*.ps1`
- production persistence under `%LOCALAPPDATA%\DesktopMascotMinimal`

`Assets/_Project/Editor/DesktopMascotSetup.cs` contains an older fixed sample
Scene path (`Assets/_Project/Scenes/DesktopMascot.unity`). It is preserved
unchanged because correcting or deleting that helper would be a behavior
change rather than a pure move.

The diagnostic shaders remain directly under `Assets/_Project/Resources`.
Their names are loaded through Unity `Resources.Load`; moving them into the
new diagnostics tree would require a runtime resource-path change and is
therefore outside M-046.

## Files not deleted

M-046 does not delete Scene or Prefab assets, VRMs, plugin binaries or symbols,
tracked native output, logs cited by milestone history, production
persistence, or older diagnostic assets. Ambiguous or mixed-responsibility
files are retained and documented rather than guessed to be obsolete.

## Future cleanup candidates

- Prove whether the Legacy sample-scene helpers and the old Editor Scene path
  are still required, then address them in a dedicated change.
- Resolve the duplicate bundled VRM only after asset provenance, references,
  and redistribution rules are verified.
- Consider native source responsibility subdirectories with an explicit CMake
  and export regression milestone.
- Consider internal development-tool subdirectories while preserving the
  canonical root-level wrappers.
- Add assembly definitions only as a separate compile-boundary design; M-046
  intentionally does not introduce them.

## Tray startup recovery prerequisite

Normal-runtime validation exposed an existing race between tray startup and
Explorer notification-area readiness. The previous native startup path
terminated the tray thread when the first `NIM_ADD` or `NIM_SETVERSION` call
failed, so the already-created owner window could not receive
`TaskbarCreated` and recover.

The approved prerequisite fix keeps the hidden owner window,
`TaskbarCreated` registration, and native message loop alive after an initial
registration failure. Registration retry uses native constants:

- interval: 200 ms;
- maximum attempts: 75;
- bounded retry window: approximately 15 seconds.

A successful retry cancels the pending retry window and publishes one
registered GUID icon. `TaskbarCreated` also performs the same idempotent
registration path. Shutdown suppresses pending or future attempts. The native
thread, owner window, icon, menu, and exactly-one logical `NIM_DELETE`
ownership contracts remain unchanged.

Managed startup calls the native start boundary once and observes registration
at a bounded 200 ms interval for at most 20 seconds. It does not create another
native thread, retry every frame, or change normal-runtime initialization
order. Diagnostics distinguish `NIM_ADD` and `NIM_SETVERSION` attempts,
successes, and last errors and also report `TaskbarCreated`, retry count,
exhaustion, shutdown suppression, final result, and current registration.

## Validation contract

Validation must prove:

- moved `.cs` and `.meta` files remain byte-identical;
- Scene script GUIDs still resolve;
- no old source path is used as a runtime/build dependency;
- Unity Development Player build has no compile error or new warning;
- Settings, Player visibility, tray, context menu, Single Instance,
  persistence, diagnostic modes, VRM selection/import, M-044 disposal, and
  orderly shutdown retain their M-045 behavior;
- no generated artifact or runtime log is treated as source.

## Final validation state

The structural, native, managed, and manual validation has passed:

- all moved tracked `.cs` and `.meta` blobs are byte-identical to M-045;
- all project Scene MonoScript GUIDs resolve after the move;
- there are no missing `.cs.meta` files, duplicate asset GUIDs, or current
  build/document references to the old source paths;
- `git diff --check` passes;
- Unity Development Player build passes with only the established
  `TransparentWindowController.borderless` CS0414 warning;
- runtime-smoke, drag-diagnostic, real-animated, Settings, Player visibility,
  context menu, focused tray, Single Instance, all three persistence groups,
  Runtime VRM selection/import, and M-044 disposal diagnostics pass.
- native CMake configure and Ninja RelWithDebInfo build pass;
- no existing native export was removed and Debug CRT dependencies are absent;
- Assets and Development Player native DLL SHA-256 values match;
- focused tray recovery validates repeated `TaskbarCreated` handling and
  deterministic initial-registration-loss recovery without duplicate icons;
- normal runtime reports tray startup and cleanup `True`, one owner
  created/destroyed, one logical delete, and live-owned menu/icon counts `0/0`;
- Present and device-removed HRESULTs are `S_OK/S_OK`, readback errors and
  continuous/region failure stages are `0` and `0/0`;
- pending owned regions and active/retired runtime handles reach `0` and
  `0/0`, aggregate cleanup is `True`, and fatal failure stage is `0`;
- the user verified one tray icon, one continuously animated mascot, Settings
  opening and interaction from the tray, orderly tray exit, and no remaining
  Player, mascot, or tray surface.

M-046 is complete and advances the architecture baseline to M-046.
