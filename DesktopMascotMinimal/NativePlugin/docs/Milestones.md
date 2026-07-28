# Development Milestones

## Production-Sized Animated Silhouette Region Update Diagnostics

Status: Completed  
Visual verification: Passed

### Configuration

- Resolution: 256 × 256
- Alpha threshold: 128
- Mask publish interval: 250 ms
- Phase duration: 2000 ms
- Animation duration: 32 seconds
- Target FPS: 30
- Target Present count: 1200

### Results

- Published generations: 127
- Applied generation: 127
- Region builds: 17
- Duplicate masks skipped: 110
- Superseded generations: 0
- All four phases applied: Yes

### Performance

- Region build: min 193 µs, max 470 µs, average 319 µs
- SetWindowRgn: min 634 µs, max 1025 µs, average 872 µs
- Measured Present rate: 27.46 FPS

### Resource ownership

- HRGN created: 971
- HRGN caller-deleted: 954
- HRGN ownership transferred: 17
- HRGN live-owned at completion: 0
- Final GDI samples: 19, 19, 19, 19, 19

### Validation

- Automated diagnostics: Passed
- Visual verification: Passed
- Display and click-region synchronization: Passed
- Orientation: Passed
- Continuous Present: Passed
- Initial region and style restoration: Passed

### Notes

The final summary was logged while the diagnostic state value was 9 rather
than the documented Completed state. Failure stage remained 0 and all
automated checks passed. Align final state transition and summary timing in a
future cleanup milestone.

## M-028 — Real Mascot Animated Alpha Mask Integration Diagnostics

Status: Completed  
Visual verification: Passed

### Summary

Validated the complete animated real-mascot pipeline:

```text
Unity Camera
→ Camera RenderTexture
→ explicit Camera-source Y normalization Blit
→ shared normalized transfer RenderTexture
→ D3D12 GPU copy/readback
→ alpha mask
→ animated HRGN construction
→ SetWindowRgn
→ DirectComposition presentation
```

Camera-specific Y normalization is intentionally localized at the source
boundary. The downstream D3D12, readback, region-generation, composition, and
IMGUI preview orientation rules remain unchanged. No shader-side Y inversion
was introduced.

### Configuration

- Unity: 6000.3.20f1 LTS
- Graphics API: Direct3D12
- Resolution: 256 × 256
- Graphics format: B8G8R8A8_SRGB
- Alpha threshold: 128
- Requested FPS: 30
- Target Present count: 1200
- Alpha-mask publish interval: 250 ms
- Active animation duration: 32 seconds

### Results

- Actual mascot available: True
- Animator available: True
- Animation playing: True
- Published generation count: 126
- Applied generation: 126
- Generation evaluation count: 126
- Valid mask evaluation count: 126
- Empty or invalid mask count: 0
- Distinct binary mask hash count: 126
- Region build count: 126
- Duplicate mask skip count: 0
- Superseded generation count: 0
- Apply message post/execution/success/failure: 126/126/126/0
- Maximum pending apply message count: 1
- Maximum pending owned-region count: 0
- All four animation phases observed: True
- All four animation phases applied: True
- Readback errors: 0
- Automated diagnostics passed: True
- Visual verification passed by user

### Performance

- Region build min/max/average: 414 / 710 / 509 microseconds
- SetWindowRgn min/max/average: 439 / 1095 / 565 microseconds
- Present count: 1200
- Elapsed time: 43906 ms
- Measured FPS: 27.33
- Present HRESULT: S_OK
- Device removed HRESULT: S_OK

### Resource ownership

- HRGN created: 15815
- HRGN caller-deleted: 15689
- HRGN ownership transferred: 126
- HRGN validation objects: 126
- HRGN live-owned after cleanup: 0
- HRGN ownership invariant: Passed
- GDI initial/min/max/last: 17/19/20/19
- Final GDI samples: 19, 19, 19, 19, 19
- Stabilized GDI delta: +2
- GDI stability: Passed

### Cleanup

- Initial window region restored: True
- Initial window style restored: True
- runInBackground restored: True

### Validation

- Shared GPU/readback expected orientation match: True
- Shared GPU/readback vertically flipped orientation match: False
- Preview uses validated UV transform: True
- Managed and native representative mask hashes matched in all four phases
- Transparent areas outside the animated mascot clicked through
- Opaque mascot body areas blocked clicks
- Region followed the animated silhouette
- Preview and DirectComposition output were upright
- No duplicate or vertically concatenated mascot image was visible

### Known notes

- Duplicate suppression count was zero because all 126 sampled binary masks
  were distinct during the animation. This is valid behavior.
- The logged visual verification value remains pending by design; the user
  subsequently completed manual visual verification successfully.

## M-029 — Production Runtime Foundation and Diagnostic Hardening

Status: Completed

Visual verification: Preserved from the validated static and animated paths

### Summary

Separated the validated real-mascot Camera, D3D12 transfer, alpha-mask,
window-region, and DirectComposition path from milestone-only assertions and
completion rules. Normal Player startup now selects a reusable runtime path;
the static, animated, and short runtime-smoke diagnostics remain explicitly
selectable Development Player modes.

Camera-source Y normalization is shared and intentionally applied exactly once
at the source boundary. Downstream D3D12 copy/readback, region generation,
DirectComposition presentation, and validated preview orientation remain
unchanged. No shader-side Y inversion was introduced.

### Configuration

- Unity: 6000.3.20f1 LTS
- Graphics API: Direct3D12
- Resolution: 256 × 256
- Graphics format: B8G8R8A8_SRGB
- Alpha threshold: 128
- Alpha-mask publish interval: 250 ms
- Target runtime Present rate: 30 FPS

### Runtime foundation

- Shared Camera-source/normalized-transfer texture owner: Added
- Static and animated diagnostic auto-start in normal runtime: Disabled
- Normal runtime automatic completion timer: None
- Explicit eight-second runtime-smoke mode: Added
- Reusable pinned alpha-mask buffer: Added
- Reusable presentation CommandBuffer: Added
- Idempotent managed cleanup path: Added
- Native continuous runtime mode without diagnostic frame/time limits: Added
- Additive native runtime alpha-region entry point: Added
- Runtime log prefix: `[DesktopMascotRuntime]`

### Regression validation

- Static real-mascot automated diagnostics: Passed
- Static expected/flipped orientation match: True/False
- Static HRGN live-owned after cleanup: 0
- Static initial region/style restored: True/True
- Static readback errors: 0
- Animated real-mascot automated diagnostics: Passed
- Animated expected/flipped orientation match: True/False
- Animated published/applied generations: 126/126
- Animated all four phases observed/applied: True/True
- Animated HRGN live-owned after cleanup: 0
- Animated initial region/style restored: True/True
- Animated readback errors: 0
- Runtime-smoke Present count: 220
- Runtime-smoke mask generations/region builds: 32/32
- Runtime-smoke result: Passed
- Runtime-smoke cleanup result: True
- Present HRESULT/device removed HRESULT: S_OK/S_OK

The shared visual path was not altered after the previously passed manual
static and animated verification. The runtime smoke used that same normalized
transfer texture, D3D12 presentation, and region-update path.

### Known notes

- `d3d12: failed to query info queue interface (0x80004002)` remains a known
  non-functional Unity/device message. D3D12 copy, readback, Present, and
  device-removed validation completed successfully after it.
- The Development Player process `MainWindow` heuristic may select the native
  composition window. Automation must not treat `Process.CloseMainWindow()` as
  a reliable way to close the Unity Player once that window exists.
- Final window placement, drag interaction, settings integration, and
  production UI controls remain outside this milestone.

## M-030 — Native Mascot Window Drag Interaction

Status: Completed

Visual verification: Passed

### Summary

Added left-button dragging to the native DirectComposition mascot window.
Dragging begins only on the active `SetWindowRgn` silhouette, uses native mouse
messages and mouse capture, applies the system drag threshold, and moves only
the native mascot `HWND`. Transparent pixels retain region-based
click-through.

The validated Camera normalization, D3D12 transfer/readback, animated alpha
mask, HRGN publication, and DirectComposition presentation paths remain
unchanged. No global hook, `SendInput`, `WS_EX_TRANSPARENT`, `HTTRANSPARENT`,
layered-window API, or shader-side Y inversion was introduced.

### Native interaction

- Drag origin: `GetCursorPos` and `GetWindowRect`
- Movement threshold: `SM_CXDRAG` / `SM_CYDRAG`
- Continued movement outside the region: native `SetCapture`
- Window movement: `SetWindowPos`
- Movement flags: `SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE`
- Activation prevention: `WM_MOUSEACTIVATE` / `MA_NOACTIVATE`
- End/cancel paths: button release, capture loss, cancellation, destruction,
  shutdown, and composition failure
- Position persistence: Not implemented

### Automated validation

- Selected mode: `drag-diagnostic`
- Diagnostic move: +48 X / +32 Y
- Diagnostic state/failure stage: 4 / 0
- Native mascot HWND available: True
- Requested X/Y move succeeded: True
- Width and height unchanged: True
- Region remained applied: True
- Present continued during movement: True
- Region publication continued during movement: True
- Composition restart avoided: True
- Z-order preserved: True
- Window activation avoided: True
- Capture released: True
- Initial position restored: True
- Dragging after completion: False
- Automated diagnostics passed: True
- Cleanup result: True
- Outstanding readback after cleanup: False
- Pending owned region after cleanup: False
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0

### Regression validation

- Runtime smoke: Passed; Present count 220; mask generations/region builds
  32/32; cleanup result True
- Normal runtime: Remained active for 40.177 seconds with both auto-exit timer
  values False; Present count 1104; region publications 158; orderly cleanup
  passed
- Animated real-mascot diagnostic: Passed; Present count 1200; published and
  applied generations 126/126; all four phases observed/applied; expected and
  flipped orientation matches True/False; HRGN live-owned after cleanup 0;
  initial region/style restored True/True; readback errors 0

### Manual validation

- Transparent pixels continued to click through: Passed
- Opaque mascot body started physical dragging: Passed
- Window movement stopped immediately on button release: Passed
- Mouse capture continued dragging beyond the silhouette: Passed
- Background editor retained focus and Unity Player did not come forward:
  Passed
- Upright orientation, animation, and animated region tracking: Passed
- No duplicate/vertically concatenated image, black frame, or flicker: Passed
- Managed orderly shutdown and cleanup: Passed

### Validation status

Automated native move, runtime regressions, and all user-performed manual
interaction and visual checks passed. M-030 is complete.

### Shutdown regression correction

A later 414.903-second normal-runtime shutdown exposed region failure stage 13
(`GdiObjectLeakDetected`) and managed fatal stage 9 even though the tracked
HRGN count was zero and every cleanup invariant passed. Stage 13 was a
shutdown-time false positive: its fixed process-wide GDI delta threshold also
counted GDI objects outside the animated-region owner's tracked HRGNs. It was
not a failed region message post, a leaked owned HRGN, or a drag/capture fault.

Orderly shutdown now closes region publication before native window teardown,
rejects new generations, drains or cancels an already queued apply message,
then restores region/style before composition destruction. Runtime shutdown
uses the exact tracked-HRGN ownership invariant when process-wide GDI drift is
observed; active-runtime and diagnostic failures remain fatal. The managed
frame loop rechecks shutdown after `WaitForEndOfFrame`, and final
`Application.Quit` is issued from the next independent `Update`.

The post-fix long-runtime case included a physical drag and ran for 447.265
seconds: Present count 12293, region publications 1757, continuous/region
failure stages 0/0, pending owned region 0, GDI initial/final/delta 17/19/+2,
dragging/capture false/false, all restoration checks true, HRESULT values
S_OK/S_OK, readback errors 0, cleanup result true, and fatal failure stage 0.

## M-031 — Window Position Persistence and Screen-Bounds Recovery

Status: Completed

Visual verification: Passed

### Summary

Added a versioned, platform-local persistence boundary for the native
DirectComposition mascot window's top-left desktop position. Normal runtime
loads and validates the record before native window creation, places the
window without a visible correction jump, saves only completed native drags,
and recovers unusable positions against current monitor work areas.

The validated Camera normalization, D3D12 transfer/readback, animated alpha
mask, `HRGN` ownership, region-based click-through, native drag, composition,
and orderly-shutdown paths remain unchanged.

### Persistence and recovery

- Production path:
  `%LOCALAPPDATA%\DesktopMascotMinimal\window-position.json`
- Schema: version 1 with signed integer `x` and `y`
- Coordinate system: native `HWND` top-left desktop physical pixels
- Player DPI awareness: PerMonitorV2
- Coordinate validation range: -1,000,000 through +1,000,000
- Minimum-visible policy: 48 x 48 pixels in one monitor work area
- Monitor source: `EnumDisplayMonitors` and `GetMonitorInfoW`
- Valid negative and usable edge positions: Preserved
- Invalid/off-screen recovery: Entire window fitted inside the nearest current
  work area where possible
- Missing, corrupt, unsupported, or unreadable record: Safe default/recovery
- Write strategy: flushed same-directory temporary file followed by
  replace/move
- Duplicate unchanged-position writes: Suppressed

Normal runtime alone uses the production record. Runtime-smoke and static or
animated diagnostics disable persistence. Drag-diagnostic uses a
process-specific temporary path and did not create or alter the production
record.

### Native integration

- Initial position configured before `CreateWindowExW`: True
- Completed drag signal: Monotonically increasing generation
- Completed generation published only after thresholded `WM_LBUTTONUP`: True
- Simple click, cancellation, and capture-loss publication: Disabled
- File I/O from WndProc: None
- Per-`WM_MOUSEMOVE` writes: None
- Additive exports:
  - `DMN_SetCompositionInitialPosition`
  - `DMN_GetNativeMascotCompletedDragGeneration`
  - `DMN_TryGetNativeMascotWindowPosition`

### Focused automated validation

- No saved file/default position: Passed
- Exact valid position restoration: Passed
- Negative multi-monitor-style coordinates: Passed
- Far off-screen recovery/full-window visibility: Passed
- Valid 48 x 48 edge placement accepted unchanged: Passed
- Removed-monitor-style recovery/full-window visibility: Passed
- Oversized-window deterministic safe fallback: Passed
- Corrupt data recovery: Passed
- Unsupported schema recovery: Passed
- Simulated atomic commit failure preserved previous record: Passed
- One completed generation produced one logical write: Passed
- Cancelled/active and duplicate generations produced no write: Passed
- Save/load restart simulation: Passed
- Native diagnostic completed drag generation: 1
- Isolated diagnostic write count: 1
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic automated result: Passed

### Build and regression validation

- Native RelWithDebInfo configure/build: Passed
- Unity Development Player incremental build: Passed
- Asset/Player native DLL SHA-256:
  `AA60C5392E5C72D4280620A3B88E31F30E25DD72147F30C42BB6EBCBBECE03B7`
- Asset/Player DLL hash match: True
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke result and cleanup: Passed/True
- Real animated Present count: 1200
- Real animated published/applied generations: 126/126
- Real animated all phases observed/applied: True/True
- Real animated expected/flipped orientation: True/False
- Real animated HRGN live-owned after cleanup: 0
- Real animated initial region/style restored: True/True
- Real animated readback errors: 0
- Normal runtime remained active without an auto-exit timer: 108.111 seconds
- Normal runtime Present/region publication counts: 2971/425
- Normal runtime continuous/region failure stages: 0/0
- Normal runtime cleanup result/fatal stage: True/0
- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Native dragging/capture after stop: False/False
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Managed orderly Application.Quit requested: True

### Manual validation

- Real normal-runtime drag and exact restart restoration: Passed
- Intentional valid edge placement preserved: Passed
- Far off-screen record recovered to a discoverable position: Passed
- Second-monitor restoration when available: Passed
- Focus avoidance and transparent click-through preserved: Passed
- Opaque mascot dragging preserved: Passed
- Startup remained free of correction jump or flicker: Passed
- Orderly shutdown and absence of a residual native window: Passed

### Screen-bounds recovery correction

Manual validation found that recovery of a far off-screen record could clamp
the 256 x 256 window to only 48 x 48 visible pixels. The window rectangle met
the minimum-visible acceptance rule, but that retained corner could contain
only transparent mascot pixels and leave the mascot visually undiscoverable.
Changing the record to `(100,100)` confirmed that persistence and native
window creation were functioning and isolated the failure to recovery
placement.

The minimum-visible rule remains the acceptance policy for an already-valid
saved position, so intentional partial edge placement and valid negative
coordinates remain unchanged. Only positions that fail acceptance enter
recovery. Recovery now fits the entire mascot window inside the nearest
current work area where possible. If the window is larger than that work area,
the oversized axis deterministically uses the work-area top-left. No alpha
mask, `HRGN`, D3D12, animation, native drag, schema, or production-path change
was introduced.

The user subsequently confirmed the corrected far off-screen recovery and all
remaining M-031 manual checks. M-031 is therefore complete.

Post-correction automated validation passed the focused M-031 suite,
drag-diagnostic, runtime-smoke, and real-animated diagnostic. The normal
runtime remained active with both auto-exit timers false, then completed all
orderly-shutdown cleanup invariants after a `WM_CLOSE` targeted specifically
to the Unity Player window. The process required a second orderly `WM_CLOSE`
after cleanup before exiting; no `Process.CloseMainWindow` or forced
termination was used.

## M-032 — Product Identity Boundary

Status: Completed

Visual verification: Passed

### Summary

Established three explicit identities without globally renaming the project:

- User-facing product identity: `あなたといつも`
- Internal engineering identity: `DesktopMascotMinimal`
- Stable production-storage identity: `DesktopMascotMinimal`

Unity now reports the user-facing name through `Application.productName`, the
Development Player is generated as `あなたといつも.exe`, and the Unity-managed
Player window uses the same title. Internal source identifiers, namespaces,
diagnostics, log prefixes, environment variables, CMake target, native DLL,
native exports, repository, and Unity project directory remain unchanged.

The M-031 production record remains
`%LOCALAPPDATA%\DesktopMascotMinimal\window-position.json`. No migration,
schema change, move, deletion, or Japanese-named production settings directory
was introduced.

### Configuration and window identity

- Unity: 6000.3.20f1 LTS
- Unity company name: `DefaultCompany` (unchanged)
- Unity product name: `あなたといつも`
- Development Player:
  `Build/DevelopmentCurrent/あなたといつも.exe`
- Development Player data directory:
  `Build/DevelopmentCurrent/あなたといつも_Data`
- Unity Player HWND class/title: `UnityWndClass` / `あなたといつも`
- Native mascot HWND class/title:
  `DesktopMascotDirectCompositionDiagnosticWindow` / empty
- HWND technical selection depends on class and process, not the localized
  title alone

### Build-script boundary

A small Development-only PowerShell identity file centrally supplies the
display name, executable name, and `_Data` directory name. Windows PowerShell
5.1 does not reliably parse a non-ASCII literal in a BOM-less UTF-8 script, so
the display name is constructed from Unicode code points in that ASCII-only
file. The resulting .NET strings and paths are exactly `あなたといつも`,
`あなたといつも.exe`, and `あなたといつも_Data`.

Build, launch, stop, native deployment, and environment-verification scripts
consume that boundary. Process discovery derives the process name from the
configured executable and verifies its full path where a running process can
be confused with another executable. Existing internal build directory names
remain unchanged.

### Automated validation

- Unity Development Player build: Passed
- Unity company/product log: `DefaultCompany` / `あなたといつも`
- PowerShell parser errors across updated scripts: 0
- Unicode executable and `_Data` paths: Passed
- Actual launched process path matched the configured Japanese executable:
  True
- Stale legacy executable selected by launch automation: False
- Native DLL Assets/Player SHA-256 match: True
- M-031 focused position tests: Passed
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic automated result: Passed
- Diagnostic persistence remained isolated: True
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke result and cleanup: Passed/True
- Real-animated Present count: 1200
- Real-animated published/applied generations: 123/123
- Real-animated all phases observed/applied: True/True
- Real-animated expected/flipped orientation: True/False
- Real-animated HRGN live-owned after cleanup: 0
- Real-animated readback errors: 0
- Normal runtime loaded the existing ASCII production position record: True
- Normal runtime auto-exit timers: False/False
- Japanese `%LOCALAPPDATA%\あなたといつも` settings directory created: False

### Shutdown regression

Normal runtime remained active for more than 60 seconds without diagnostic or
smoke completion, automatic quit, presentation failure, or region-update
failure. A `WM_CLOSE` targeted by process and `UnityWndClass` initiated the
managed orderly cleanup. The automation path required a second orderly
`WM_CLOSE` after cleanup before the process exited. `Process.CloseMainWindow`
and forced termination were not used.

- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Native dragging/capture after stop: False/False
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Cleanup result/fatal failure stage: True/0
- Residual Player or native mascot process: None

### Manual validation

- Executable name and visible Unity Player title: `あなたといつも`
- Garbled Japanese product text: None
- Native mascot presentation and animation: Passed
- Transparent click-through and opaque dragging: Passed
- Native mascot remained non-activating: Passed
- Drag/quit/restart position restoration: Passed
- Japanese-named production settings directory created: False
- Orderly shutdown and absence of a residual mascot window: Passed

The Unity Player surface opened larger than the previously remembered window
and its own view appeared static. This was not a native mascot regression: the
new product identity used the configured default Player size rather than the
old identity's remembered size, while the authoritative Camera-to-RenderTexture
to DirectComposition mascot window continued to present and animate normally.

All required automated and user-performed manual checks passed. M-032 is
complete.

## M-033 — Settings Foundation

Status: Completed

Visual verification: Passed

### Summary

Added a versioned production settings boundary without adding a settings UI,
context menu, tray icon, or other user-visible feature. The new settings
foundation is the single configuration source for future application
preferences, while M-031 window-position persistence remains authoritative
for native mascot placement.

The two production records intentionally coexist:

- `%LOCALAPPDATA%\DesktopMascotMinimal\settings.json` stores application
  configuration.
- `%LOCALAPPDATA%\DesktopMascotMinimal\window-position.json` stores
  high-frequency, platform-local window position and recovery state.

`SettingsManager` does not read, write, migrate, or duplicate the position
record. Camera normalization, D3D12 transfer/readback, animated alpha masks,
`HRGN` ownership, `SetWindowRgn`, click-through, native dragging, product
identity, and orderly shutdown remain unchanged.

### Schema and architecture

The only settings schema members introduced by this milestone are:

```json
{
  "schemaVersion": 1,
  "settingsVersion": 1,
  "firstRunCompleted": false
}
```

- `SettingsManager`: Runtime initialization, load, save, and current-value API
- `SettingsStore`: Path management and flushed same-directory atomic writes
- `SettingsSerializer`: Strict JSON member/type/schema/version validation
- `SettingsDefaults`: Current schema/version constants and default generation

Missing settings are written atomically and reloaded. Invalid JSON, negative
or unsupported schemas, unsupported settings versions, missing members, and
type mismatches recover to defaults and atomically replace the invalid
record. Startup keeps safe in-memory defaults if filesystem recovery fails;
settings failures do not escape as startup exceptions.

Normal runtime alone uses the production settings path. Runtime-smoke,
drag-diagnostic, real-static, real-animated, and other explicit diagnostic
modes use process- and mode-specific temporary paths. The focused settings
suite also uses and removes its own temporary directory.

### Focused automated validation

- Missing settings/default creation and reload: Passed
- Existing valid settings/exact value load: Passed
- Invalid JSON recovery: Passed
- Negative schema recovery: Passed
- Future unsupported schema recovery: Passed
- Missing required member recovery: Passed
- Type mismatch recovery: Passed
- Future settings-version startup recovery: Passed
- Simulated partial atomic commit preserved the previous record: Passed
- Failed-write temporary file cleanup: Passed
- Atomic replacement of an existing valid record: Passed
- Production-path isolation: Passed
- Focused settings suite aggregate result: Passed

The drag-diagnostic run also passed all focused M-031 position-persistence and
screen-bounds tests. The production `window-position.json` SHA-256 and
timestamp remained unchanged throughout diagnostic isolation checks.

### Build and regression validation

- Unity Development Player incremental build: Passed
- Native source and exports changed: No
- Native rebuild required: No
- Known Unity warning:
  `TransparentWindowController.borderless` CS0414 only
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic automated result: Passed
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke result and cleanup: Passed/True
- Real-animated Present count: 1200
- Real-animated published/applied generations: 126/126
- Real-animated all phases observed/applied: True/True
- Real-animated expected/flipped orientation: True/False
- Real-animated HRGN live-owned after cleanup: 0
- Real-animated readback errors: 0
- Production settings existed before first normal launch: False
- First normal launch initialization: DefaultsCreated
- Second normal launch initialization: Loaded
- Production settings SHA-256 remained stable across the second launch: True
- Production settings temporary files after commit: 0
- Existing production position SHA-256/timestamp remained unchanged: True
- Normal runtime auto-exit timers: False/False
- Normal runtime remained active without forbidden completion or failure:
  More than 77 seconds

### Shutdown regression

Normal runtime remained active without diagnostic completion, smoke
completion, automatic quit, presentation failure, or region-update failure.
A `WM_CLOSE` targeted by process and `UnityWndClass` initiated
`Application.wantsToQuit` and the managed orderly-cleanup path. As in the
previous validated regression, automation sent a second orderly `WM_CLOSE`
after cleanup to let the Player process exit. `Process.CloseMainWindow` and
forced termination were not used.

Both the first-launch and second-launch normal-runtime checks reported:

- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Cleanup result/fatal failure stage: True/0
- Residual Player process: None

### Manual validation

The user confirmed all of the following in normal use:

- First launch creates
  `%LOCALAPPDATA%\DesktopMascotMinimal\settings.json`.
- Second launch loads normally.
- The previously saved native mascot position is still restored.
- `window-position.json` is preserved and remains independent.
- A completed mascot drag updates only `window-position.json`.
- No duplicate production settings file or Japanese-named storage directory
  is created.
- No new user-visible behavior is present.

All required automated and user-performed manual checks passed. M-033 is
complete.

## M-034 — Settings UI Foundation

Status: Completed

Visual verification: Passed

### Summary

Added the first optional production Settings UI framework without adding a
tray icon, context menu, character switching, or future settings. Normal
runtime creates the controller in a closed state and behaves as before if the
window is never opened. The normal Unity Player opens or closes the window
with `F10`; `Escape` closes it and discards unapplied edits.

Only `firstRunCompleted` is exposed as a checkbox. The M-033 schema and
persistence design are unchanged, and `window-position.json` remains
independent and absent from the Settings UI.

### UI architecture and editing model

- `SettingsWindowController`: Initialization, open/close lifecycle, keyboard
  entry point, optional IMGUI window, and Apply/Cancel/Restore Defaults/Close
  commands
- `SettingsViewModel`: Editable copy, applied baseline, dirty tracking,
  cancellation, defaults, and applied-state update
- `SettingsBinding`: Synchronization between the checkbox and ViewModel plus
  validation-aware Apply eligibility
- `SettingsValidation`: Reusable validation boundary; the current Boolean is
  valid by construction while schema/version compatibility is still checked
- `SettingsManager`: Sole persistence owner

The editing flow is:

```text
SettingsManager.Current
→ editable SettingsViewModel copy
→ SettingsBinding
→ SettingsValidation
→ Apply
→ SettingsManager.TrySave
```

Controls perform no filesystem operations. `TrySave` serializes and
atomically writes the candidate settings, then updates
`SettingsManager.Current` only after a successful write. `Restore Defaults`
changes only the ViewModel; persistence requires a subsequent Apply. Cancel,
Close, and Escape restore the applied baseline before closing.

### Focused automated validation

- Controller initialized closed: Passed
- Window open state: Passed
- Initial checkbox value: Passed
- Editing and dirty transition: Passed
- Apply enabled only while valid and dirty: Passed
- Cancel discarded changes: Passed
- Apply persisted the candidate and cleared dirty state: Passed
- Reload preserved the applied value: Passed
- Restore Defaults changed only the ViewModel before Apply: Passed
- Restore Defaults persisted only after Apply: Passed
- Reusable valid/invalid validation paths: Passed
- Window close state: Passed
- Diagnostic storage remained outside the production directory: Passed
- M-033 focused settings suite: Passed
- M-031 focused position-persistence suite: Passed

### Build and regression validation

- Unity Development Player incremental build: Passed
- Native source and exports changed: No
- Native rebuild required: No
- Known Unity warning:
  `TransparentWindowController.borderless` CS0414 only
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic automated result: Passed
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke result and cleanup: Passed/True
- Real-animated Present count: 1200
- Real-animated published/applied generations: 126/126
- Real-animated all phases observed/applied: True/True
- Real-animated expected/flipped orientation: True/False
- Real-animated HRGN live-owned after cleanup: 0
- Real-animated readback errors: 0
- Normal runtime settings initialization: Loaded
- Normal runtime settings window initial state: Closed
- Normal runtime auto-exit timers: False/False
- Normal runtime remained active without forbidden completion or failure:
  More than 75 seconds
- Production settings SHA-256/timestamp unchanged while UI remained closed:
  True
- Production position SHA-256/timestamp unchanged: True
- Production settings temporary files after normal shutdown: 0

### Shutdown regression

Normal runtime remained active with the Settings UI unopened and without
diagnostic completion, smoke completion, automatic quit, presentation
failure, or region-update failure. A `WM_CLOSE` targeted by process and
`UnityWndClass` initiated the managed orderly-cleanup path. The Player exited
after that first orderly request; `Process.CloseMainWindow` and forced
termination were not used.

- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Native dragging/capture after stop: False/False
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Cleanup result/fatal failure stage: True/0
- Residual Player process: None

### Manual validation

The user completed the final manual validation after the full-quality Player
preview correction:

- `F10` opened, closed, and reopened an interactive Settings window: Passed
- The checkbox reflected the current `firstRunCompleted` value: Passed
- Apply saved the changed value and disabled again after success: Passed
- Cancel discarded changes and removed only Settings: Passed
- Close discarded changes and removed only Settings: Passed
- Escape discarded changes and removed only Settings: Passed
- Restore Defaults remained unsaved until Apply: Passed
- Restart preserved the last applied value: Passed
- No stale Settings image remained after closing: Passed
- Player mascot quality matched the pre-M-034 appearance: Passed
- Mascot remained visible and animated behind Settings: Passed
- DirectComposition mascot remained unchanged: Passed
- Native mascot position persistence and validated interaction remained
  unchanged: Passed

### Cancel/Close stale-surface correction

Initial manual validation failed because Cancel changed the controller's
logical state to closed, but the last IMGUI window image remained visible and
non-interactive. The mascot Camera renders to a RenderTexture rather than
clearing the Unity Player backbuffer, so stopping IMGUI drawing did not erase
the last window pixels. This was a Player-surface lifecycle issue rather than
a ViewModel, dirty-state, or persistence failure.

Cancel and Close now release IMGUI hot/keyboard control, return immediately
from the window callback, and schedule four opaque Player-surface Repaints.
Opening the window cancels pending cleanup and rebuilds the editable copy from
`SettingsManager.Current`. The correction does not change the settings or
position schemas, native mascot pipeline, DirectComposition surface, or
persisted values.

The corrected Unity Development Player build passed. Focused M-034, M-033,
and M-031 tests, drag-diagnostic, runtime-smoke, and real-animated diagnostics
all passed again. Post-correction normal runtime remained active for more
than 70 seconds with the Settings UI unopened, both auto-exit timers false,
and no forbidden completion or failure. It then satisfied every orderly
shutdown invariant; automation required the established second targeted
`WM_CLOSE` after cleanup before process exit.

Manual validation remains pending and must repeat Cancel, Close, reopen, and
artifact checks against the corrected Player.

### Player presentation overlay correction

The first stale-surface correction also failed manual validation. Its opaque
full-Player background removed the stale Settings pixels, but it covered the
Player's mascot and left only the magenta background behind the UI. This was
not accepted as an overlay and M-034 remained pending.

Investigation confirmed that the production Camera is intentionally redirected
to `CameraSourceTexture`. It continues rendering there, and the explicit
source-boundary Blit continues updating `NormalizedTransferTexture`, but the
Camera no longer refreshes the Player backbuffer directly. Consequently,
stopping IMGUI cannot restore an old backbuffer, and an opaque full-screen
draw hides the mascot.

The frame-count cleanup and opaque full-screen draw were removed. The normal
runtime now exposes the already-existing normalized transfer texture and its
normalized frame number through a read-only Player-presentation boundary.
`SettingsWindowController` owns a continuous IMGUI Player preview: it clears
only the aspect-fitted preview viewport to the Camera's original background,
draws the latest normalized mascot texture with the existing validated
IMGUI-only UV transform, and then draws Settings on top. When Settings closes,
the next ordinary Repaint draws the next mascot frame without the window.
There is no cleanup timer or stale-frame special case.

This Player preview does not create or normalize another RenderTexture. It
does not change `Camera.targetTexture`, the single Camera-source normalization
Blit, the normalized texture consumed by D3D12, or any native composition,
readback, alpha-mask, `HRGN`, click-through, drag, persistence, or shutdown
behavior.

The corrected focused test uses a presentation source to verify frame
advancement while open and closed. A runtime diagnostic also exercised the
real normalized transfer texture and reported:

- Fresh Player frame before Settings opened: True
- Fresh Player frames while Settings was open: True
- Fresh Player frames after Cancel: True
- Fresh Player frames after Close: True
- Reopened Settings interactive: True
- Open/closed presentation frame advances: 8/25
- Runtime Player-presentation diagnostic: Passed

The Unity build, M-034 and M-033 focused tests, M-031 position tests,
drag-diagnostic, runtime-smoke, real-animated, and normal-runtime regressions
all passed again. Normal runtime remained active for more than 74 seconds
with both auto-exit timers false and no forbidden completion or failure, then
satisfied every orderly-shutdown invariant. Manual validation remains
pending for the actual visual overlay, Cancel, Close, Escape, and F10 reopen
behavior.

### Full-quality Player preview correction

The normalized-texture overlay correction also failed manual validation.
Although it kept the mascot visible and removed stale Settings pixels, it
scaled the native-transfer 256 x 256 `NormalizedTransferTexture` to the full
Player surface. The resulting Player mascot was visibly pixelated. M-034
therefore remains pending.

The pre-M-034 presentation was traced to the original source Camera rendering
at Player resolution before the production pipeline redirected that Camera to
the 256 x 256 `CameraSourceTexture`. The correction now creates a separate
Player-only Camera before that redirection. It copies the source Camera
configuration, follows its transform, and renders directly to the Player
backbuffer at Player resolution. `SettingsWindowController` now draws only
the Settings IMGUI window over this continuously updating Camera output; it
does not clear or redraw the full Player surface.

The Player-only Camera does not allocate a preview RenderTexture. It is
disabled and destroyed during managed orderly cleanup. It neither changes
the production Camera's `targetTexture` nor feeds D3D12, readback, alpha-mask,
`HRGN`, or DirectComposition processing. The validated native path remains:

```text
Camera
→ CameraSourceTexture
→ one Camera-source Y normalization
→ 256 x 256 NormalizedTransferTexture
→ D3D12 / readback / alpha mask / HRGN / DirectComposition
```

The corrected Unity Development Player and focused runtime diagnostics
reported:

- Fresh Player frame before Settings opened: True
- Fresh Player frames while Settings was open: True
- Fresh Player frames after Cancel: True
- Fresh Player frames after Escape: True
- Fresh Player frames after Close: True
- Reopened Settings interactive: True
- Open/closed presentation frame advances: 8/32
- Player preview resolution: 1920 x 1080
- Full-quality Player resolution: True
- Native transfer remains 256 x 256: True
- Preview RenderTexture allocations: 0
- Player-preview cleanup result: True
- M-034 focused tests: Passed
- M-033 settings tests: Passed
- M-031 position tests: Passed
- Drag diagnostic: Passed

Post-correction runtime-smoke passed with Present count 220, 32 mask
generations, 32 region builds, native continuous/region failure stages 0/0,
readback errors 0, and cleanup True. Real-animated passed with Present count
1200, 126 valid and applied mask generations, all four animation phases
observed and applied, no live-owned `HRGN`, expected orientation match True,
vertically flipped match False, and readback errors 0.

Normal runtime remained active for more than 187 seconds with both auto-exit
timers false and no diagnostic completion, smoke completion, quit timer,
presentation failure, region-update failure, or exception. A targeted
`WM_CLOSE` initiated managed orderly cleanup. No outstanding readback,
pending owned regions 0, all initial region/style, `Camera.targetTexture`,
and `runInBackground` restorations True, Player-preview cleanup True, Present
and device-removed HRESULTs `S_OK`, readback errors 0, cleanup True, and fatal
failure stage 0 were confirmed. As in the established shutdown automation,
a second targeted `WM_CLOSE` was needed after cleanup for the Player process
to exit; `Process.CloseMainWindow` and forced termination were not used.

The user subsequently completed all required manual checks successfully.
M-034 is complete.

## M-035 — Native Mascot Context Menu

Status: Completed

Visual verification: Passed

### Summary

Added a native Win32 popup menu to the existing DirectComposition mascot
`HWND`. Right-click interaction is delivered only through the existing
window and its currently applied `SetWindowRgn`; transparent pixels remain
click-through and no separate alpha hit test was introduced.

The menu contains exactly:

- `設定`
- `終了`

Japanese labels are wide-character literals expressed with Unicode escapes.
`CreatePopupMenu`, `AppendMenuW`, `TrackPopupMenuEx`, and `DestroyMenu` own the
complete popup lifecycle. A shutdown-only UI-thread message calls `EndMenu`
when orderly shutdown begins while a popup is active.

For correct Win32 popup dismissal, the non-activating mascot window becomes
the temporary foreground owner immediately before `TrackPopupMenuEx`.
`WM_NULL` is posted after tracking returns, and the previously foreground
window is restored when it is still valid. This temporary ownership is limited
to the modal popup lifetime and does not change the normal non-activating
mascot-window contract.

No system tray icon, About dialog, character switching, settings-schema
expansion, or Settings UI redesign was added.

### Native-to-managed command boundary

The native menu publishes one of two stable commands into a single pending
slot:

```text
OpenSettings = 1
RequestExit = 2
```

Each accepted selection increments a monotonic generation. Managed polling
atomically consumes the command once; repeated polling cannot duplicate it.
An occupied slot, an invalid or cancelled selection, or a selection after the
shutdown barrier creates no managed command.

`OpenSettings` reuses the existing `SettingsWindowController`. If it is
already open, it remains the same interactive controller and no second
component is created. The Player is located by current process ID and
`UnityWndClass`, not by the localized title, then restored and foregrounded
only after an explicit `設定` selection.

`RequestExit` calls
`DesktopMascotRuntimePipeline.RequestOrderlyQuit("native mascot context-menu
exit selected")`. It does not call `DestroyWindow`, `ExitProcess`,
`TerminateProcess`, `Environment.Exit`, or any forced process termination.

### Ownership and interaction invariants

- HMENU creation/destruction/live-owned diagnostic: 1/1/0
- Cancelled selection published no command: True
- Shutdown rejected new commands: True
- One selection/one consumption: True
- Repeated polling duplicated no command: True
- Menu activity changed completed-drag generation: False
- Menu activity changed position persistence: False
- Menu activity changed settings persistence: False
- Existing Settings controller reused: True
- Orderly-exit route available: True
- Left-button drag implementation changed: No
- `SetWindowRgn` hit ownership changed: No

### Build and automated validation

- Native RelWithDebInfo CMake/Ninja/MSVC build: Passed
- Unity Development Player incremental build: Passed
- New C++ warnings/errors: None
- Unity warnings: Existing
  `TransparentWindowController.borderless` CS0414 only
- Focused context-menu diagnostics: Passed
- M-034 focused Settings UI tests: Passed
- M-034 Player-presentation diagnostics: Passed
- M-033 settings tests: Passed
- M-031 position tests: Passed
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic result: Passed
- Context-menu `RequestExit` published/consumed generation: 2/2
- Context-menu orderly-exit shutdown reason observed: True
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke cleanup/failure stages: True/0/0
- Real-animated Present count: 1200
- Real-animated published/applied generations: 126/126
- Real-animated all four phases observed/applied: True/True
- Real-animated HRGN live-owned/readback errors: 0/0
- Assets/Player native DLL SHA-256 match: True
- `git diff --check`: Passed

New native exports were verified with `dumpbin /exports`. `dumpbin
/dependents` contains only Release CRT dependencies (`MSVCP140.dll`,
`VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`, and Universal CRT API sets); no
Debug CRT dependency is present.

### Normal runtime and orderly shutdown

The final normal-runtime regression remained active for more than 186 seconds
with the native menu enabled, both auto-exit timers false, and no diagnostic
completion, smoke completion, automatic quit, presentation failure,
region-update failure, or exception.

A process-ID plus `UnityWndClass` targeted `WM_CLOSE` entered the existing
managed orderly-shutdown path. A second targeted `WM_CLOSE` was required
after cleanup for the established Player-process exit behavior.
`Process.CloseMainWindow` and forced termination were not used.

- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Player Preview Camera cleanup: True
- Native dragging/capture after stop: False/False
- Popup menu live-owned count: 0
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Cleanup result/fatal failure stage: True/0
- Final Present/region publication counts: 5135/734
- Production settings hash/timestamp unchanged: True
- Production position hash/timestamp unchanged: True
- Residual Player process: None

An earlier test-harness attempt used `Process.MainWindowHandle`, which
selected the native mascot `HWND` instead of `UnityWndClass` and therefore
correctly produced a region failure after directly closing the native window.
That failing log is retained. The final regression uses the required
process/class identification and passed all shutdown invariants.

### Popup dismissal correction

The first manual validation found that outside clicks and Escape did not
dismiss the popup. The mascot `HWND` uses the validated non-activating window
contract and was passed to `TrackPopupMenuEx` without first becoming the
foreground popup owner. Consequently, the modal menu did not reliably receive
the dismissal input expected by Win32.

The correction temporarily assigns foreground ownership to the mascot window
for the `TrackPopupMenuEx` lifetime, posts `WM_NULL` after the call returns,
then restores the previous foreground window when it remains valid. A cancelled
menu still publishes no native-to-managed command.

Post-correction validation:

- Native RelWithDebInfo build: Passed
- Unity Development Player build: Passed
- Assets/Player native DLL SHA-256 match: True
- Focused context-menu diagnostics: Passed
- HMENU created/destroyed/live-owned: 1/1/0
- Drag, position, Settings, and Player-presentation diagnostics: Passed
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke cleanup/failure stages: True/0/0
- Normal runtime remained active before close: 81 seconds or more
- Normal-runtime auto-exit timers: False/False
- Normal-runtime Present/region publication counts: 4829/690
- Normal-runtime readback/pending owned region: None/0
- Normal-runtime region/style restoration: True/True
- Normal-runtime Camera targetTexture/runInBackground restoration: True/True
- Normal-runtime Present/device removed HRESULT: S_OK/S_OK
- Normal-runtime native continuous/region failure stages: 0/0
- Normal-runtime popup live-owned count: 0
- Normal-runtime cleanup/fatal failure stage: True/0
- `git diff --check`: Passed

The user subsequently confirmed that outside-click and Escape dismissal both
close the menu without executing a command. The corrected menu can be reopened
normally, and the remaining M-035 manual checks had already passed.

### Manual validation

Manual validation covered:

1. Right-clicking the visible mascot opens the menu.
2. The menu contains only `設定` and `終了`.
3. Japanese text is not garbled.
4. Transparent space still clicks through and opens no menu.
5. Clicking outside or pressing Escape dismisses the menu without action.
6. `設定` opens the existing Settings UI.
7. Settings remains full-quality and interactive.
8. Repeated `設定` selection creates no duplicate Settings window.
9. Cancel, Close, Escape, Apply, and Restore Defaults still work.
10. Ordinary left-button mascot dragging remains unchanged.
11. Menu use does not alter the saved mascot position.
12. `終了` performs orderly shutdown.
13. No Player, native mascot window, popup, or process remains afterward.
14. Restart restores the previous position and applied settings.
15. DirectComposition animation, click-through, and visual quality remain
    unchanged.

The user completed the required manual checks successfully. M-035 is complete.

## M-036 — System Tray Integration

Status: Completed

Visual verification: Passed

### Summary

Added one Windows notification-area icon to normal runtime as a second entry
point for the existing `設定` and `終了` actions. The M-035 mascot context menu
remains unchanged. Single left-click has no action; left double-click opens
Settings.

### Native architecture and ownership

`NativeTrayIcon` owns a dedicated native thread and an ASCII-class-name hidden
`WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` owner window. The DirectComposition
mascot `HWND` is not used as the permanent tray owner.

The icon uses `NOTIFYICONDATAW`, a stable application GUID,
`Shell_NotifyIconW(NIM_ADD)`, `NIM_SETVERSION` with
`NOTIFYICON_VERSION_4`, and a logical single `NIM_DELETE` on cleanup. The
tooltip is constructed from UTF-16 escapes as `あなたといつも`.

No suitable product icon existed in the repository. M-036 therefore creates a
simple 32 × 32 cyan/yellow/pink application-owned icon with
`CreateDIBSection` and `CreateIconIndirect`. Temporary GDI bitmaps are deleted
immediately; the resulting owned `HICON` is destroyed exactly once. This is a
temporary product icon, not a generic system warning/error/question icon.

Each popup `HMENU` is created and destroyed on the tray thread. The owner uses
the foreground-owner pattern around `TrackPopupMenuEx`, posts `WM_NULL`, and
restores the previous foreground window. Cancellation publishes no command.

### Explorer recovery

The owner registers `TaskbarCreated`. Each notification re-adds the same GUID
identity, restores `NOTIFYICON_VERSION_4` and the tooltip, and does not create
a second logical icon. Re-registration is rejected once shutdown begins or
after the owner is destroyed.

Focused diagnostics performed two controlled Explorer-loss simulations by
removing the current Shell entry and posting `TaskbarCreated`. Both produced
one re-registration, one logical icon, and preserved command behavior.
Physical Explorer restart remains part of manual validation.

### Shared command routing

Both native entry points publish through the M-035 single pending slot and
monotonic generation:

```text
mascot context menu ─┐
                     ├─ shared native application command
system tray menu ────┘   ├─ OpenSettings = 1
                         └─ RequestExit = 2
```

The recorded source (`MascotContextMenu = 1`, `SystemTray = 2`) is diagnostic
metadata only. Managed consumption remains exactly once per generation.
`OpenSettings` reuses the single M-034 `SettingsWindowController` and existing
Player activation path. `RequestExit` enters
`DesktopMascotRuntimePipeline.RequestOrderlyQuit`; no forced termination was
added.

M-034 Cancel and Close remain unchanged during M-036: both discard unapplied
edits and close Settings. No About dialog, character switching, settings
schema expansion, or Settings UI redesign was added.

### Focused automated validation

- Tray startup/owner/icon registered: Passed
- Initial `NIM_ADD` request count: 1
- `NIM_SETVERSION` after initial add and two recoveries: 3
- Controlled `TaskbarCreated` re-registration count: 2
- Tooltip configuration: Passed
- OpenSettings one publication/one consumption: Passed
- Command source for tray publication: 2
- Repeated polling produced no duplicate: Passed
- Existing/already-open Settings controller reused: Passed
- Cancelled selection produced no command: Passed
- Orderly-exit route available and reached: Passed
- Shutdown rejected tray diagnostic publication: Passed
- Logical deletion request during each lifecycle: 1
- Cleanup called twice safely: Passed
- Owner create/destroy per lifecycle: 1/1
- Live owned HMENU/HICON after cleanup: 0/0
- Menu action changed drag generation: False
- Menu action changed position/settings persistence: False
- Production storage isolation: Passed
- M-035 context-menu diagnostics: Passed
- M-034 Settings and Player-presentation diagnostics: Passed
- M-033 settings tests: Passed
- M-031 position tests: Passed
- Drag diagnostic state/failure stage: 4/0
- System-tray command orderly-shutdown route: Passed
- Focused Player log:
  `NativePlugin/out/development-player-20260725-012400.log`

### Build and regression validation

- Native RelWithDebInfo CMake/Ninja/MSVC configure/build: Passed
- New native warnings/errors: None
- Unity Development Player build: Passed
- Unity warning: existing
  `TransparentWindowController.borderless` CS0414 only
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke cleanup/failure stages: True/0/0
- Runtime-smoke log:
  `NativePlugin/out/development-player-20260725-012607.log`
- Real-animated Present count: 1200
- Real-animated generations/applied/all phases: 126/126/True
- Real-animated HRGN live-owned/readback errors: 0/0
- Real-animated log:
  `NativePlugin/out/development-player-20260725-012624.log`
- Normal runtime remained active before close: more than 75 seconds
- Normal runtime auto-exit timers: False/False
- Normal runtime unexpected completion/failure/re-registration loop: None
- Normal runtime final Present/region publications: 2527/361
- Normal runtime log:
  `NativePlugin/out/development-player-20260725-012727.log`
- Assets/Player DLL SHA-256:
  `A91FB7E4C3F18CC377C8DA8AD8441741CEA431ADE2620E16C4C96D160CF29EA8`
- Assets/Player DLL hash match: True
- Production settings hash/timestamp unchanged: True
- Production position hash/timestamp unchanged: True
- `dumpbin /exports` new tray/application-command exports: Present
- `dumpbin /dependents` Debug CRT dependency: None
- New native dependency: `SHELL32.dll`
- `git diff --check`: Passed

### Shutdown contract

Managed orderly shutdown first rejects new tray commands, cancels an active
popup, deletes the Shell icon, destroys the hidden owner, and releases the
owned icon. Existing context-menu, drag, region, composition, Camera, and
managed cleanup then continue in their established order.

The focused shutdown reported:

- Tray cleanup/icon removed/owner destroyed/popup closed: True/True/True/True
- Tray live owned menu/icon counts: 0/0
- No outstanding readback/pending owned region: True/0
- Region/style restored: True/True
- Camera targetTexture/runInBackground restored: True/True
- Present/device removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Cleanup result/fatal failure stage: True/0
- Tray delete requests: 1
- Tray owner created/destroyed: 1/1
- Residual Player process: None

### Manual validation

The user confirmed the requested tray appearance, tooltip, menu labels,
dismissal, Settings reuse, Explorer restart recovery, absence of residual
resources/processes, and preservation of mascot context-menu, drag,
click-through, animation, visual quality, applied settings, and saved
position. The initial orderly-exit result required the corrections recorded
below.

### Tray-exit foreground-loss correction

The first manual validation found that selecting tray `終了` completed native
and managed cleanup but left the Player surface visible until it was clicked.
The cleanup log showed every invariant passing and the final managed quit
request being reached.

Root cause: final `Application.Quit()` execution was deferred to the next
`Update()`. Tray interaction left the Player unfocused, and cleanup correctly
restored the previous `Application.runInBackground = false` value before that
next frame. The deferred quit therefore did not execute until a click returned
focus and allowed another Player update.

The correction calls `Application.Quit()` directly at the end of the same
orderly-shutdown coroutine, after all restoration, cleanup evaluation, and
final logging. It does not move or weaken any native cleanup step and does not
change tray, context-menu, Settings, drag, persistence, D3D12, HRGN, or
DirectComposition behavior.

Post-correction validation:

- Unity Development Player build: Passed
- New warnings/errors: None
- M-036 focused tray diagnostics: Passed
- M-035 context-menu diagnostics: Passed
- M-034 Settings and Player-presentation diagnostics: Passed
- M-033 settings and M-031 position diagnostics: Passed
- Drag diagnostic state/failure stage: 4/0
- Shared tray command source/exit reason: 2/system tray exit selected
- Tray cleanup/icon removed/owner destroyed/popup closed: True/True/True/True
- Tray live-owned HMENU/HICON: 0/0
- Existing orderly cleanup/fatal failure stage: True/0
- Diagnostic process exited without an additional click or external close:
  True
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke process exited without a follow-up frame trigger: True
- Focused log:
  `NativePlugin/out/development-player-20260725-014747.log`
- Runtime-smoke log:
  `NativePlugin/out/development-player-20260725-014802.log`

The first correction required another manual tray `終了` verification, which
exposed the remaining Player-window wake issue documented below.

### Tray-exit Player-window wake correction

The first foreground-loss correction still failed manual validation. The
actual log proved that the tray command was consumed with source 2, orderly
cleanup completed, `Application.Quit()` was called, and Unity advanced through
Physics and Input System shutdown. The visible Player window nevertheless
waited for another native window message before disappearing.

The corrected final step now finds the current process's visible window by
exact `UnityWndClass` class name and posts one normal `WM_CLOSE` after all
orderly cleanup invariants have passed. `Application.Quit()` is then requested
as before. This only wakes and closes the already-cleaned Unity Player surface;
it does not use `Process.CloseMainWindow`, force termination, process kill, a
localized title, or the native mascot/tray owner window.

Post-correction validation:

- Unity Development Player build: Passed
- New warnings/errors: None
- M-036/M-035 focused diagnostics: Passed
- M-034/M-033/M-031 regressions: Passed
- Drag diagnostic state/failure stage: 4/0
- Tray command source/shutdown reason: 2/system tray exit selected
- Managed orderly `UnityWndClass` `WM_CLOSE` posted: True
- Existing cleanup/fatal failure stage: True/0
- Tray icon/owner/popup/live HMENU/HICON cleanup: Passed
- Focused process exited without a click or external close: True
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke `UnityWndClass` `WM_CLOSE` posted: True
- Runtime-smoke exited normally: True
- Focused log:
  `NativePlugin/out/development-player-20260725-020045.log`
- Runtime-smoke log:
  `NativePlugin/out/development-player-20260725-020104.log`

The user subsequently confirmed that tray `終了` removes the tray icon,
mascot, Player surface, and process without clicking the Player. The final
manual log is
`NativePlugin/out/development-player-20260725-020145.log`; it records command
source 2, `system tray exit selected`, cleanup true, fatal failure stage 0,
and a successful orderly `UnityWndClass` `WM_CLOSE` post. No Player process
remained. M-036 is complete.

## M-037 — Production Player Window Visibility Lifecycle

Status: Completed

Visual verification: Passed

### Summary

Normal runtime now treats the existing Unity Player as an on-demand Settings
host. At `RuntimeInitializeLoadType.BeforeSplashScreen`, the visibility owner
finds the current process's exact `UnityWndClass` and applies the application
cloak before scene and native mascot initialization. The DirectComposition
mascot, animation, D3D12 transfer/readback, alpha-region updates, command
polling, tray icon, and position persistence continue while it is hidden.
Explicit diagnostic modes retain their existing visible Player behavior.

The native mascot `設定` command, tray `設定` command, and tray double-click all
use one managed visibility owner. It finds and caches only the current
process's exact `UnityWndClass`, restores a minimized Player, recovers an
inaccessible Player rectangle to a current monitor work area, shows it, and
reuses the existing single `SettingsWindowController`.

`Cancel`, `Close`, `Escape`, and focused `F10` discard unapplied edits, close
Settings, and hide only the Player. `Apply` and `Restore Defaults` keep
Settings and the Player visible. Ordinary hiding uses an application-owned
DWM cloak; it does not post `WM_CLOSE`, destroy a window, or request
application exit.

### Ownership and shutdown boundary

`UnityPlayerWindowVisibilityController`, split only into its general and
Windows-specific partial source files, solely owns normal-runtime Player
visibility. It owns exact PID/class discovery, cached-handle validation,
show/restore, monitor recovery, DWM cloak/uncloak, and the existing final
orderly-close post. No other source calls `ShowWindow` or
`DwmSetWindowAttribute`.

The shutdown barrier rejects new show requests immediately. Final orderly
shutdown remains the M-036 path: all native and managed cleanup invariants are
evaluated first, then one `WM_CLOSE` is posted to the exact cached
`UnityWndClass` and `Application.Quit()` is requested. No
`Process.MainWindowHandle`, `Process.CloseMainWindow`, process kill, localized
title lookup, native mascot destruction, or tray-owner substitution is used
for ordinary visibility changes.

`Application.runInBackground` remains enabled for the active runtime and is
restored only during final cleanup. The Player-preview Camera, production
Camera target, normalized 256 × 256 transfer texture, D3D12 resources, HRGN
ownership, native mascot window, tray window, context menu, drag contract, and
persistence schemas retain their previous ownership.

### Present occlusion correction

Testing the hidden Player exposed that `IDXGISwapChain::Present` can return
`DXGI_STATUS_OCCLUDED`, which is a successful status rather than a device or
presentation failure. The continuous composition path previously treated
every result other than `S_OK` as fatal failure stage 21.

The native path now treats only `DXGI_STATUS_OCCLUDED` as a temporary,
non-fatal result, emits one bounded debug message, completes the same frame
ownership/state bookkeeping, and retries on later frames. Other failing
HRESULT values retain the established failure handling. No export or native
ownership contract changed.

### Focused automated validation

- Exact current-PID `UnityWndClass` discovery: Passed
- BeforeSplashScreen startup cloak/one lookup: Passed
- Player Flash Measured: 602 ms
- Player rendering surface retained after cloak: Passed
- Startup hide performed once and idempotently: Passed
- Open shows and reuses the existing Settings controller: Passed
- Cancel/Close/Escape/focused F10 close and hide: Passed
- Minimized Player restore: Passed
- Apply and Restore Defaults remain visible: Passed
- Visibility transitions write no persistence: Passed
- Hidden tray and mascot commands reopen Settings: Passed
- Shutdown barrier rejects new show requests: Passed
- Ordinary hide posts no `WM_CLOSE`: Passed
- M-036 system-tray diagnostics: Passed
- M-035 context-menu diagnostics: Passed
- M-034 Settings UI and Player-presentation diagnostics: Passed
- M-033 settings diagnostics: Passed
- M-031 position diagnostics: Passed
- M-030 drag state/failure stage: 4/0
- Focused log:
  `NativePlugin/out/development-player-20260725-081532.log`

### Build and regression validation

- Native RelWithDebInfo CMake/Ninja/MSVC build: Passed
- Unity Development Player build: Passed
- New native or managed warnings/errors: None
- Existing warning:
  `TransparentWindowController.borderless` CS0414
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke cleanup/failure stages: True/0/0
- Runtime-smoke log:
  `NativePlugin/out/development-player-20260725-081546.log`
- Real-animated Present count: 1200
- Real-animated generations/applied/all phases: 126/126/True
- Real-animated live-owned HRGN/readback errors: 0/0
- Real-animated log:
  `NativePlugin/out/development-player-20260725-070732.log`
- Assets/Player DLL SHA-256:
  `378F09BBDC87ED5D3173781A2DE9D7C1762469D26346E522D2BE860BE8292FEB`
- Assets/Player DLL hash match: True
- DLL architecture: x64
- `dumpbin /dependents` Debug CRT dependency: None

### Hidden-runtime soak and orderly shutdown

Normal runtime was launched with no mascot-mode argument. The Player was
hidden after native presentation startup and the exact hidden
`UnityWndClass` remained discoverable. The runtime remained active for
137838 ms before the user selected tray `終了`.

- Production/smoke auto-exit timers: False/False
- Final Present count: 4124
- Final region publication count: 552
- Production settings file unchanged: True
- Production position file unchanged during the final soak: True
- No outstanding readback/pending owned region: True/0
- Initial region/style restored: True/True
- `Camera.targetTexture`/`Application.runInBackground` restored: True/True
- Present/device-removed HRESULT: S_OK/S_OK
- Readback errors: 0
- Native continuous/region failure stages: 0/0
- Tray cleanup: True
- Cleanup result/fatal failure stage: True/0
- Residual Player process: None
- Hidden-runtime log:
  `NativePlugin/out/development-player-20260725-065358.log`

### Manual validation

The user verified hidden-by-default normal runtime, continued mascot
animation and click-through while cloaked, native dragging, Settings
open/reuse from the native entry points, Cancel/Close/Escape/F10 hiding only
the Player, Apply/Restore Defaults visibility behavior, focus and duplicate
window behavior, persistence preservation, and orderly exit. No remaining
interaction issue was reported.

### Manual animation failure and rendering-surface correction

The first manual validation found that the mascot animated while Settings and
the Player were visible but froze after the Player was hidden. The original
implementation used `ShowWindow(SW_HIDE)`. On the tested Unity 6000.3.20f1
Player, hiding the rendering surface stopped production of
`WaitForEndOfFrame`; `Application.runInBackground = true` did not override
that rendering-surface behavior. Camera normalization, render-event
submission, Present, readback, and region publication therefore stopped
together.

Ordinary hide now uses `DwmSetWindowAttribute(DWMWA_CLOAK)` instead. The
Player remains `WS_VISIBLE` to the Unity rendering lifecycle but is not
visible to the user. Showing Settings removes the application cloak. The
focused diagnostic verifies that logical hiding retains the rendering
surface. Final orderly `WM_CLOSE`, native ownership, persistence, Settings,
drag, context-menu, tray, D3D12, readback, HRGN, and DirectComposition
contracts are unchanged.

The user subsequently confirmed that, while the Player is cloaked, mascot
animation continues and click-through and native dragging remain correct.
This part of manual validation passed. Overall visual verification remains
passed after the startup-flash quality correction below.

### Startup Player Visibility Quality

The original M-037 path waited for the first native Present and alpha-region
publication before hiding the Player. The corrected path applies the
application cloak from
`RuntimeInitializeLoadType.BeforeSplashScreen`. It performs an exact
current-PID `UnityWndClass` lookup and cloaks immediately; both measured
normal-runtime launches succeeded on the first lookup.

Measurement is defined as elapsed wall-clock time from Windows process
creation to successful `DWMWA_CLOAK` verification:

- First corrected measurement: 703 ms
- Final-build measurement: 602 ms
- Manual-validation launch measurement: 592 ms
- Final startup cloak lookup count: 1
- Final startup cloak result: True
- Rendering surface retained after cloak: True
- Final measurement log:
  `NativePlugin/out/development-player-20260725-081842.log`

A strict 0 ms guarantee is not possible from managed Unity code. Unity creates
and may present its native Player window while starting the engine and managed
runtime; the earliest supported project callback used here runs only at
`BeforeSplashScreen`. The measured interval therefore includes unmanaged
engine startup before project code can execute and is an upper bound on
actual visible time—the Player window is not necessarily visible for the
entire process-to-cloak interval.

The final measured hidden run continued for 29183 ms after startup and
reported 802 Presents and 115 region publications. Cleanup was true, Present
and device-removed HRESULTs were `S_OK`, readback errors were 0, continuous
and region failure stages were 0/0, and all Camera, run-in-background, region,
style, readback, tray, and owned-region shutdown invariants passed.

Manual validation now includes:

- Player Flash Visual Verification: Passed
- Player is not displayed for a visibly long duration during startup: Passed
- Animation/click-through/drag while cloaked: Passed
- Remaining Settings, focus, recovery, and hidden/visible shutdown checks:
  Passed

The user reported the final startup flash was within an acceptable range and
that all remaining manual checks passed. The final Player exited through the
managed orderly path. Its log records no outstanding readback, pending owned
region 0, region/style and Camera/run-in-background restoration, Present and
device-removed HRESULTs `S_OK`, readback errors 0, continuous/region failure
stages 0/0, cleanup true, and fatal failure stage 0. M-037 is complete.

## M-038 — Single Instance Enforcement

Status: Completed

Visual verification: Passed

### Summary

Normal runtime now acquires one stable session-local internal mutex before
production Settings, window-position, mascot, tray, or runtime
initialization. A secondary normal-runtime launch does not create another
resident runtime. It signals one `OpenSettings` request to the existing
primary and exits through `Application.Quit(0)`.

The primary consumes the request on the Unity managed main thread, routes
Player display exclusively through
`UnityPlayerWindowVisibilityController`, and reuses the existing
`SettingsWindowController`. An already-open Settings ViewModel is not
reinitialized, so unapplied edits are retained.

Explicit diagnostic modes remain outside production single-instance
ownership.

### Ownership and notification design

- Mutex:
  `Local\DesktopMascotMinimal.SingleInstance.v1`
- Scope: current Windows session; `Global\` is not used
- Ownership: native `SingleInstanceCoordinator`
- Startup acquisition: zero-time wait; `WAIT_OBJECT_0` and
  `WAIT_ABANDONED` establish the primary
- Notification: independent session-local named activation, readiness, and
  shutdown events
- Activation event: auto-reset pending latch
- Secondary retry bound: 80 attempts at 25 ms
- Managed receiver: `SingleInstanceController.Update`
- Settings route:
  `SingleInstanceController`
  → `UnityPlayerWindowVisibilityController`
  → existing `SettingsWindowController`

The first notification prototype used a dedicated `HWND_MESSAGE`. Automated
multi-process execution showed that isolated Window Stations/desktops shared
the session-local mutex but could not discover one another's windows. The
named-event channel removes that UI-desktop dependency and remains independent
from the tray owner, mascot window, and Unity Player window.

Repeated `SetEvent` calls while activation is already pending remain one
signaled auto-reset event. The focused diagnostic additionally publishes ten
equivalent requests without consumption and confirms one pending generation;
after consumption, a later request creates the next generation. The observed
focused counts were received/published/consumed/coalesced/rejected
`12/2/2/9/1`, with maximum pending count 1.

### Startup and shutdown policy

One `BeforeSplashScreen` gate first preserves the M-037 exact
current-process `UnityWndClass` startup cloak and then performs
single-instance acquisition. A secondary is rejected before production
Settings and position objects are constructed. Defensive `BeforeSceneLoad`
and `AfterSceneLoad` guards prevent later runtime creation.

The single-instance acceptance barrier closes at the beginning of orderly or
emergency shutdown. Readiness is reset and shutdown is signaled before the
existing composition, tray, region, Camera, and managed cleanup. A secondary
arriving during shutdown performs only bounded retry and exits; it does not
promote itself during that launch. Final idempotent cleanup destroys the
notification channel and releases the mutex before the established final
`UnityWndClass` close and `Application.Quit`.

No process-name detection, localized-title lookup,
`Process.MainWindowHandle`, `Process.CloseMainWindow`, process kill,
`TerminateProcess`, `ExitProcess`, `Environment.Exit`, `SendInput`, or global
hook was added.

### Build and binary validation

- Native RelWithDebInfo CMake/Ninja/MSVC build: Passed
- Unity Development Player build: Passed
- New native warnings/errors: None
- New managed warnings/errors: None
- Existing warning:
  `TransparentWindowController.borderless` CS0414
- Assets/Player DLL SHA-256:
  `366FCC7014825DE88DF026B0B95A5B6A5359CC13EB0072D59BF87BF0CD5D0E98`
- Assets/Player DLL hash match: True
- `dumpbin /exports`: all M-038 initialization, shutdown, activation,
  state, counter, and focused-diagnostic exports present
- `dumpbin /dependents`: release
  `MSVCP140.dll`, `VCRUNTIME140.dll`, and `VCRUNTIME140_1.dll`; no Debug CRT
- `git diff --check`: Passed

### Focused and multi-process validation

- Native pending-request coalescing focused diagnostic: Passed
- Existing open dirty Settings preserved during secondary activation: Passed
- M-037 exact visibility routing and shutdown rejection: Passed
- One secondary detected the primary: Passed
- Secondary activation signal sent: True
- Primary activation consumed and Settings routed: True
- Secondary exit code: 0
- Secondary exited normally: True
- Five simultaneous secondaries signaled successfully: 5/5
- Five simultaneous secondaries exited normally: 5/5
- Unique per-launch logs produced: 5/5
- Remaining Player process after secondary burst: primary only
- No duplicate production runtime component was created by a secondary
- Production `settings.json` hash/stamp unchanged: True
- Production `window-position.json` hash/stamp unchanged: True
- Abnormal-owner recovery mutex acquisition: Passed

Relevant logs:

- Primary/one-secondary:
  `NativePlugin/out/development-player-20260726-144745-637.log`
- Successful secondary:
  `NativePlugin/out/development-player-20260726-144753-262.log`
- Five-secondary integration primary:
  `NativePlugin/out/development-player-20260726-150838-368-3012b84c.log`
- Five-secondary logs:
  `NativePlugin/out/development-player-20260726-150841-494-8879165b.log`
  and the four adjacent unique launch logs from the same second

### Regression validation

- M-031 position-persistence focused tests: Passed
- M-033 settings focused tests: Passed
- M-034 Settings UI and Player-presentation focused tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 Player-visibility focused tests: Passed
- Drag diagnostic state/failure stage: 4/0
- Drag diagnostic automated result: Passed
- Runtime-smoke Present/mask/region counts: 220/32/32
- Runtime-smoke cleanup/fatal failure stage: True/0
- Real-animated Present/generations/applied: 1200/126/126
- Real-animated all phases observed/applied: True/True
- Real-animated live-owned HRGN/readback errors: 0/0
- Real-animated automated result: Passed

Regression logs:

- Focused and drag:
  `NativePlugin/out/development-player-20260726-145658-561-38eab8a0.log`
- Runtime-smoke:
  `NativePlugin/out/development-player-20260726-145715-538-f24998c1.log`
- Real-animated:
  `NativePlugin/out/development-player-20260726-145738-302-0175b642.log`

### Normal-runtime validation environment note

The automated launcher runs its normal Player in an isolated desktop without
an Explorer notification area. In that environment the production tray
reports startup false, although the M-036 focused tray lifecycle test passes.
This is a launcher-environment limitation rather than a production fallback;
tray requirements were not weakened.

A normal runtime remained active for at least 60 seconds without diagnostic
completion, smoke completion, automatic quit, presentation failure, or
region-update failure. A separate orderly-close run used only current PID plus
exact `UnityWndClass` `WM_CLOSE` from the same launch desktop. It recorded:

- single-instance shutdown barrier closed: True
- notification channel created/destroyed: 1/1
- single-instance failure stage: 0
- single-instance validation passed: True
- no outstanding readback/pending owned region: True/0
- initial region/style restored: True/True
- `Camera.targetTexture`/`Application.runInBackground` restored: True/True
- Present/device-removed HRESULT: `S_OK`/`S_OK`
- readback errors: 0
- continuous/region failure stages: 0/0
- single-instance cleanup result: True
- fatal failure stage: 0
- process exit code: 0

The isolated-desktop tray startup prevented the aggregate production cleanup
boolean from being used as tray evidence in that run. Final tray presence,
tray exit, full aggregate cleanup, and visible Settings behavior therefore
remain part of the required user-desktop manual validation.

Normal orderly-close log:
`NativePlugin/out/development-player-20260726-150730-682-196326bf.log`

### Manual validation

The user launched the Player directly on the interactive desktop and verified:

- one mascot and one tray icon after the first launch;
- no additional mascot or tray icon after a second launch;
- the second launch opens the existing Settings window;
- an already-open Settings window is reused without losing unapplied edits;
- Cancel, Close, Escape, and F10 preserve single-instance behavior;
- repeated rapid launches converge to one primary process;
- click-through, dragging, animation, Present, readback, and region updates
  continue when the Player is cloaked;
- tray `終了` performs full orderly cleanup with no residual process;
- a new launch after exit becomes primary.

All required manual visual and interaction checks passed. M-038 is complete,
and the architecture baseline advances to M-038.

## M-039 — Character Asset Foundation

Status: Completed

Visual verification: Passed

Architecture baseline: M-039

### Summary

M-039 introduces a normal-runtime-only boundary for discovering and publishing
the active bundled character without changing the rendered character or any
validated downstream path.

The intended route is:

```text
Scene bundled TEST_MODEL
→ CharacterAssetManager
→ read-only CharacterDescriptor
→ existing Animator / Vrm10Instance / Camera-visible Scene root
→ existing D3D12 mascot pipeline
```

The Scene instance is borrowed. No character GameObject is instantiated,
destroyed, moved, or reimported. Diagnostics retain their existing independent
Camera and Animator discovery.

### Startup and ownership

Normal-runtime structural character validation runs at the beginning of
`AfterSceneLoad`, before the runtime pipeline, Player preview, Settings,
context menu, and tray are created. The manager requires exactly one active
`MascotCharacter` with an `Animator`, Animator Controller, and
`Vrm10Instance`.

`Vrm10Instance.Runtime` readiness is observed through the descriptor but is
not an initialization failure because UniVRM may populate it after Scene
load.

The bundled logical ID is
`desktop-mascot.bundled-default`; GameObject name and `GetInstanceID` are not
selection identities. The latter is diagnostics-only evidence that the
existing Scene instance remains active.

Repeated production-manager acquisition and initialization reuse one manager,
one descriptor, and generation 1. Shutdown closes activation, cleanup is
idempotent, and the production manager cannot be recreated after the barrier.

If initialization fails, the generic startup-abort route releases the already
acquired M-038 primary resource and requests managed orderly exit without
creating partial runtime surfaces.

### Known notes

Two byte-identical `TEST_MODEL.vrm` files currently exist:

- used by the Scene:
  `Assets/_Project/Models/VRM/TEST_MODEL.vrm`;
- unused duplicate:
  `Assets/_Project/Scripts/TEST_MODEL.vrm`.

Both have SHA-256
`140DEC9A688716150C49E3C5FEFFAFC42682B64BC26DC54E49843DF9C411C4DF`
but distinct Unity GUIDs. M-039 intentionally leaves both files unchanged.

Detailed design:
`NativePlugin/docs/CharacterAssetFoundationDesign.md`

### Focused validation

- diagnostic mode production manager separated: True
- bundled Scene candidate count: 1
- active root is the existing `TEST_MODEL`: True
- logical bundled ID is stable: True
- Animator reference matches `MascotCharacter`: True
- `Vrm10Instance` reference matches `MascotCharacter`: True
- repeated initialization preserves descriptor/generation 1: True
- missing candidate failure: True
- multiple candidate failure: True
- missing required reference failure: True
- failed initialization blocks runtime-surface creation: True
- cleanup idempotent: True
- activation rejected after shutdown: True
- character foundation persistence: None
- focused diagnostics passed: True

Focused and drag log:
`NativePlugin/out/development-player-20260726-183851-215-6331a74b.log`

### Build and regression validation

- Unity Development Player build: Passed
- New managed warnings/errors: None
- Existing warning:
  `TransparentWindowController.borderless` CS0414
- Native CMake/Ninja build: Not required; no native source, CMake, export, or
  binary contract changed
- M-031 position focused tests: Passed
- M-033 Settings focused tests: Passed
- M-034 Settings UI and full-resolution presentation tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 visibility focused tests: Passed
- M-038 single-instance focused test: Passed
- one secondary detected primary/signaled/exited normally: Passed
- primary consumed generation 1 and routed existing Settings: Passed
- runtime-smoke Present/mask/region counts: `220/32/32`
- runtime-smoke cleanup/fatal failure stage: `True/0`
- real-animated Present/generations/applied: `1200/126/126`
- real-animated all phases observed/applied: `True/True`
- real-animated HRGN live-owned/readback errors: `0/0`
- real-animated orientation expected/flipped: `True/False`
- real-animated automated diagnostics: Passed
- `git diff --check`: Passed

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260726-183927-771-275f5350.log`
- real-animated:
  `NativePlugin/out/development-player-20260726-183948-161-0d3daee2.log`
- successful secondary:
  `NativePlugin/out/development-player-20260726-184058-918-4189e03a.log`

### Normal-runtime validation

The final normal runtime remained active for 65 seconds without an automatic
completion timer, presentation failure, region-update failure, or fatal
failure. It used exactly one production manager and the existing bundled
root:

- character initialization/root/source/generation:
  `Success/TEST_MODEL/BundledScene/1`
- Animator/`Vrm10Instance` available: `True/True`
- production manager count: 1
- final Present count: 1699
- final region publication count: 243
- runtime elapsed: 61837 ms
- character shutdown barrier: closed
- character cleanup result: True
- no outstanding readback/pending owned region: `True/0`
- initial region/style restored: `True/True`
- Camera target/run-in-background restored: `True/True`
- Single Instance channel created/destroyed: `1/1`
- Single Instance cleanup: True
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`
- fatal failure stage: 0
- process exit code: 0

As in M-038, the automated launch desktop has no Explorer notification area,
so production tray startup and the aggregate cleanup boolean were False in
this run. Tray owner/icon/menu cleanup invariants and the dedicated M-036
focused lifecycle passed. This environment limitation does not change the
interactive-desktop tray contract.

Final orderly normal-runtime log:
`NativePlugin/out/development-player-20260726-184549-840-a5c38539.log`

An earlier combined 65-second primary-plus-secondary run correctly routed
Settings, so its first exact `UnityWndClass` close performed the validated
M-037 ordinary Settings-close/CLOAK action rather than shutdown. The isolated
launcher could not post a second cross-desktop close, and that test process
was removed before the independent final orderly run above. It is not used as
shutdown evidence.

### Manual validation

The user confirmed that all required manual checks passed:

- the same bundled `TEST_MODEL` remains visible;
- position, size, orientation, and visual quality are unchanged;
- animation continues normally;
- transparent pixels click through;
- the mascot body remains draggable;
- completed drag position is restored after restart;
- Settings, context menu, and tray behavior remain correct;
- a secondary launch opens the existing Settings without adding another
  mascot or tray icon;
- tray exit leaves no Player, mascot, tray, or process behind;
- a subsequent launch succeeds.

M-039 is complete. Visual verification passed and the architecture baseline
advances to M-039.

## M-040 — Runtime VRM Import Foundation

Status: Completed

Visual verification: Passed

Architecture baseline: M-040

### Summary

M-040 adds one explicitly requested Development-launch path from an absolute
external VRM 1.0 path through UniVRM 0.131.0 and the M-039
`CharacterAssetManager`:

```text
Development path
→ RuntimeVrmCharacterSource
→ Vrm10.LoadPathAsync
→ hidden imported root validation/preparation
→ synchronous CharacterAssetManager activation
→ existing Camera / D3D12 / alpha / HRGN / DirectComposition pipeline
```

An argument-free launch remains `BundledScene`, logical bundled ID, and
generation 1. No VRM path or source ID is added to `settings.json`,
`window-position.json`, or another persistent schema.

Detailed design:
`NativePlugin/docs/RuntimeVrmImportFoundationDesign.md`

### Import and ownership

- UniVRM package version: 0.131.0
- API: `Vrm10.LoadPathAsync`
- VRM 0.x migration: disabled with `canLoadVrm0X: false`
- import visibility: `showMeshes: false`
- maximum file size constant: 256 MiB
- SHA-256 computed before import: True
- pre-activation owner: `RuntimeVrmCharacterSource`
- post-activation owner: `CharacterAssetManager`
- descriptor owns imported resources: False
- bundled Scene root remains borrowed: True
- release API: `RuntimeGltfInstance.Dispose()`

The imported Animator receives the existing bundled Animator Controller after
retaining its previous reference. Runtime-only character, expression,
interaction, drag, and collider components are prepared while the root is
inactive. The collider is derived from finite imported renderer bounds.

The activation commit has no await, yield, or frame boundary. Successful
activation hides the bundled root, shows the imported renderers, publishes
the RuntimeImported descriptor, transfers ownership, and increments the
generation once. Failure keeps the bundled descriptor and generation and
disposes the unactivated import.

### Focused and integration validation

- focused path and 256 MiB boundary tests: Passed
- valid VRM 1.0 Import status: Success
- preparation status: Success
- ownership transferred exactly once: True
- active source type: RuntimeImported
- active generation: 2
- imported Animator available: True
- imported `Vrm10Instance` available: True
- runtime import validation: Passed
- missing-file status: FileNotFound
- missing-file fallback source/generation: BundledScene/1
- invalid import leaves runtime pipeline running: True
- successful imported-root release after pipeline stop: True
- source cleanup result: True
- runtime import shutdown wait exceeded: False

Successful import log:
`NativePlugin/out/development-player-20260726-202432-849-0390ea2f.log`

Missing-file rollback log:
`NativePlugin/out/development-player-20260726-202534-667-fa2d4c05.log`

### Known notes

The installed UniVRM 0.131.0 source states that the current
`Vrm10Importer.LoadAsync` implementation cannot abort immediately.
Cancellation rejects new work and is observed before and after core import,
but shutdown must cooperatively wait for an import in the middle of
Unity-object creation to settle. The 30-second threshold records a failure;
it does not force-abort or abandon resources.

M-040 deliberately does not add automatic arbitrary-model scaling, a file
picker, character switching, persistence, Addressables, AssetBundles, or a
return-to-bundled command after successful activation.

### Build and regression validation

- Unity Development Player build: Passed
- new managed warnings/errors: None
- existing warning:
  `TransparentWindowController.borderless` CS0414
- native CMake/Ninja build: Not required; native source, CMake, exports, and
  binary contracts are unchanged
- M-031 position focused tests: Passed
- M-033 Settings focused tests: Passed
- M-034 Settings UI/full-resolution presentation tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 Player visibility focused tests: Passed
- M-038 Single Instance focused tests: Passed
- M-039 character focused tests: Passed
- drag diagnostic: Passed
- runtime-smoke Present/mask/region: `220/32/32`
- runtime-smoke cleanup/fatal stage: `True/0`
- real-animated Present/generations/applied: `1200/126/126`
- real-animated phases observed/applied: `True/True`
- real-animated HRGN live-owned/readback errors: `0/0`
- normal argument-free source/generation: `BundledScene/1`
- normal Present/region publication: `326/47`
- normal cleanup/fatal stage: `True/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260726-202602-890-3159cb8a.log`
- drag and focused M-031 through M-039:
  `NativePlugin/out/development-player-20260726-202624-397-042ec0e4.log`
- real-animated:
  `NativePlugin/out/development-player-20260726-202637-307-1df77d9c.log`
- argument-free normal runtime:
  `NativePlugin/out/development-player-20260726-202739-131-7f3d5433.log`

### Manual validation

The user reported manual validation passed. The visual check confirmed:

- argument-free launch still showing the bundled `TEST_MODEL`;
- explicit external VRM launch showing one correctly activated model; because
  the available external test file contains the same model as the bundled
  asset, source selection was confirmed by the `RuntimeImported` diagnostic,
  generation 2, and imported Animator/`Vrm10Instance` results rather than by a
  visual character-identity difference;
- correct orientation, framing, animation, and expressions;
- click-through and native drag following the imported silhouette;
- drag position persistence;
- Settings, context menu, tray, Player visibility, and Single Instance;
- invalid VRM fallback to the bundled model;
- no imported model or process remaining after orderly exit;
- a subsequent argument-free launch succeeding.

M-040 is complete. Visual verification passed and the architecture baseline
advances to M-040.

## M-041 — Runtime VRM Selection UI Foundation

Status: Completed

Visual verification: Passed

Architecture baseline: M-041

### Summary

M-041 adds a Character section to the normal-runtime Settings overlay. A
managed Windows Common Item Dialog selects a VRM 1.0 file, and the existing
M-040 preflight, UniVRM import, preparation, activation, rollback, ownership,
and cleanup path performs the runtime switch.

Character selection is independent from persistent Settings editing. It does
not change the Settings or position schema and stores no external path.

Detailed design:
`NativePlugin/docs/RuntimeVrmSelectionUiDesign.md`

### Ownership and duplicate handling

- simultaneous picker/import operations: 1
- duplicate requests: Rejected
- same SHA-256 behavior: `LoadPathAsync` skipped
- same SHA generation/root change: None
- active root after activation: 1
- previous runtime handle: inactive retired Manager ownership
- active and retired release point: after native/Camera pipeline stop
- bundled-return UI: Not added

### Focused and integration validation

- Unity Development Player build: Passed
- new managed warnings/errors: None
- existing warning:
  `TransparentWindowController.borderless` CS0414
- managed STA `IFileOpenDialog` boundary compiled: Passed
- M-040 initial runtime import: Passed
- same SHA skip/generation/root unchanged: Passed
- consecutive successful activation count: 3
- final active generation: 4
- active root count after switches: 1
- active/retired/total handles before shutdown: `1/2/3`
- active/retired handles after pipeline stop: `0/0`
- failure preserves active root/generation/handles: Passed
- picker cancellation/duplicate/shutdown-result discard: Passed
- focused dialog shutdown wait: 52 ms
- production selection controller count after focused test: 1
- Present/device removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- cleanup/fatal stage: `True/0`
- continuous/region failure stages: `0/0`

Three-switch, failure, picker shutdown, and orderly-shutdown log:
`NativePlugin/out/development-player-20260726-214956-820-79972e08.log`

### Regression validation

- M-031 position persistence focused tests: Passed
- M-033 Settings focused tests: Passed
- M-034 Settings UI/full-resolution presentation tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 Player visibility focused tests: Passed
- M-038 Single Instance focused tests: Passed
- M-039 character focused tests: Passed
- M-040 runtime-import integration: Passed
- runtime-smoke Present/mask/region: `220/32/32`
- drag diagnostic Present/region: `84/13`
- real-animated Present/generations/applied: `1200/126/126`
- real-animated phases observed/applied: `True/True`
- real-animated HRGN live-owned/readback errors: `0/0`
- argument-free normal Present/region publication: `327/47`
- normal active/retired handles after release: `0/0`
- normal cleanup/fatal stage: `True/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`
- `git diff --check`: Passed

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260726-215041-226-1233f1f3.log`
- drag and focused M-031 through M-040:
  `NativePlugin/out/development-player-20260726-215059-428-b0288c80.log`
- real-animated:
  `NativePlugin/out/development-player-20260726-215224-150-df0256a7.log`
- argument-free normal runtime:
  `NativePlugin/out/development-player-20260726-215334-899-fedb4a0d.log`

### Manual-validation correction

The first manual pass found that selecting Settings from the tray or mascot
menu did not restore keyboard input after focus had moved to another
application while the Settings overlay was already open. The visible-state
short circuit treated the request as redundant and skipped the Windows
interactive activation call.

The corrected route reuses the same open `SettingsWindowController`, preserves
its ViewModel and dirty state, and calls the centralized
`UnityPlayerWindowVisibilityController` interactive activation path again.
Focused Settings, Player visibility, context-menu, tray, drag, and normal
orderly-shutdown regressions pass. The user subsequently verified that tray
and mascot Settings commands restore the existing overlay after focus moves
to another application.

Correction logs:

- focused Settings/visibility/context-menu/tray/drag:
  `NativePlugin/out/development-player-20260726-223632-167-db52ece7.log`
- argument-free normal orderly shutdown:
  `NativePlugin/out/development-player-20260726-223655-130-bf932b18.log`

### Known notes

Retired imported roots retain their runtime-imported resources until orderly
shutdown. This is intentional in M-041 to preserve the M-040 rule that owned
runtime roots are released only after Camera and native GPU presentation
stop. Render-safe early retirement is future work.

UniVRM 0.131.0 import cannot abort immediately. An open Common Item Dialog is
also closed cooperatively: shutdown rejects new work, discards a returned
selection, records the wait, and continues after the user closes the dialog.

### Manual validation

The user reported manual validation passed. The visual and interaction checks
confirmed:

- the managed file picker opens from the Settings Character section;
- picker cancellation leaves the active character and Settings edits intact;
- a selected VRM 1.0 activates as the single visible animated character;
- repeated selection does not create duplicate active characters;
- Settings remains reusable through Apply, Cancel, Close, Escape, and F10;
- after focus moves to another application, both tray and mascot Settings
  commands restore the existing interactive Settings overlay;
- click-through, native drag, animation, and existing presentation remain
  correct;
- orderly exit releases the active runtime handle and leaves no Player,
  mascot window, or tray icon;
- a later argument-free launch intentionally starts with the bundled model,
  because M-041 does not persist the external VRM path.

Manual validation and orderly-exit log:
`NativePlugin/out/development-player-20260726-223735-574-9f8994f3.log`

M-041 is complete. Visual verification passed and the architecture baseline
advances to M-041.

## M-042 — Runtime Character Persistence and Startup Restoration

Status: Completed

Visual verification: Passed

Architecture baseline: M-042

### Summary

M-042 adds a dedicated versioned character-selection record at
`%LOCALAPPDATA%\DesktopMascotMinimal\character-selection.json`. A successful
manual Runtime VRM activation is followed by an atomic record save. A later
normal launch first publishes the bundled Scene character as generation 1,
then validates the stored path and current SHA-256 before using the existing
M-040/M-041 import, preparation, and activation transaction.

`settings.json` and `window-position.json` are unchanged.
`CharacterSelectionPersistenceManager` is the only character-persistence
owner; `CharacterAssetManager` remains the only active/retired runtime-root
owner, and `CharacterDescriptor` remains path-free and reference-only.

Detailed design:
`NativePlugin/docs/RuntimeCharacterPersistenceDesign.md`

### Record and atomic commit

- record schema/selection version: `1/1`
- record members:
  `schemaVersion`, `selectionVersion`, `absolutePath`, `contentSha256`
- maximum record size: 256 KiB
- maximum accepted path length: 32767 UTF-16 characters
- initial commit: same-directory temporary file plus `File.Move`
- replacement commit: same-directory temporary file plus `File.Replace`
- replacement fallback when `File.Replace` fails or is unavailable: None
- failed commit behavior: previous valid record preserved; activation remains
  active; failure is non-fatal
- directory initialization failure:
  `PersistenceUnavailable`, bundled runtime remains usable
- corrupt, unsupported, missing-file, and SHA-mismatched records:
  preserved without automatic deletion

Normal logs expose stable statuses, exception types, and path presence only.
They do not expose the selected absolute path, SHA value, or
`exception.Message`. The Settings Character section displays only the file
name.

### Startup and selection behavior

- safe initial source/generation: `BundledScene/1`
- successful saved restore source/generation: `RuntimeImported/2`
- restore request count per controller: at most 1
- manual selection while restore is pending/running: Rejected
- SHA mismatch: detected after preflight hash, before
  `Vrm10.LoadPathAsync`
- SHA-mismatch import invocation count: 0
- restore failure: non-fatal bundled fallback
- persistence-unavailable startup: runtime remains usable
- same path/SHA selection: import, generation change, root change, and
  redundant persistence write suppressed
- same content explicitly selected through a different path: persisted path
  may be atomically updated without import or generation change
- `次回起動時は同梱モデルを使用`: removes only the selection record;
  current root and generation remain unchanged

### Focused and integration validation

- missing record and directory initialization: Passed
- valid record save/reload including Japanese path: Passed
- invalid JSON, blank path, missing/duplicate/unknown member, wrong type,
  schema/selection version, path/extension, SHA validation: Passed
- oversized path and record rejection: Passed
- invalid/future record preservation: Passed
- atomic initial create and `File.Replace`: Passed
- simulated commit failure, previous record preservation, and temporary-file
  cleanup: Passed
- duplicate-write suppression and alternate-path update: Passed
- `Directory.CreateDirectory` failure classification: Passed
- shutdown save/clear rejection and idempotent cleanup: Passed
- production path isolation from Settings and position: Passed
- startup matching-hash restore: Passed
- SHA mismatch skips import and preserves active character/record: Passed
- manual selection rejected during restore: Passed
- persistence failure remains non-fatal after activation: Passed
- persistence unavailable keeps runtime usable: Passed
- clear affects next startup only: Passed
- M-041 same-SHA skip: Passed
- M-041 consecutive successful activations: 3
- active root count after switching: 1
- active/retired/total handles before shutdown: `1/2/3`
- active/retired handles after cleanup: `0/0`
- selection/import failure preserves active root/generation/handles: Passed

Focused persistence plus M-031/M-033 through M-041 drag regression log:
`NativePlugin/out/development-player-20260726-235505-671-ce34e397.log`

Successful restore, SHA mismatch, unavailable/commit-failure/clear, same-SHA,
three-switch, and ownership log:
`NativePlugin/out/development-player-20260727-001329-155-186101d8.log`

### Build and regression validation

- Unity Development Player build: Passed
- new managed warnings/errors: None
- existing warning:
  `TransparentWindowController.borderless` CS0414
- native CMake/Ninja/dumpbin: Not required; native C++, CMake, exports, and
  binary contracts are unchanged
- M-031 position focused tests: Passed
- M-033 Settings focused tests: Passed
- M-034 Settings UI/full-resolution presentation tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 Player visibility focused tests: Passed
- M-038 Single Instance focused tests: Passed
- M-039 Character Asset focused tests: Passed
- M-040 Runtime VRM Import: Passed
- M-041 Runtime Character Selection: Passed
- runtime-smoke Present/mask/region: `220/32/32`
- runtime-smoke cleanup/fatal stage: `True/0`
- drag Present/region: `85/13`
- drag cleanup/fatal stage: `True/0`
- real-animated Present/generations/applied: `1200/126/126`
- real-animated phases observed/applied: `True/True`
- real-animated HRGN live-owned/readback errors: `0/0`
- argument-free normal source/generation: `BundledScene/1`
- argument-free normal Present/region: `244/35`
- normal active/retired handles after cleanup: `0/0`
- normal cleanup/fatal stage: `True/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260727-001423-078-3531f6d5.log`
- real-animated:
  `NativePlugin/out/development-player-20260727-001445-904-d94e82cb.log`
- argument-free normal orderly shutdown:
  `NativePlugin/out/development-player-20260727-001826-933-269c50de.log`

### Shutdown and invariants

The character-persistence barrier closes before selection/import shutdown.
New saves and clears are rejected after the barrier. Persistence cleanup is
idempotent and does not dispose character roots.

Final normal-runtime invariants:

- no outstanding readback: True
- pending owned region count: 0
- initial region/style restored: `True/True`
- `Camera.targetTexture` restored: True
- `Application.runInBackground` restored: True
- active/retired runtime handles: `0/0`
- Player preview, character, selection, persistence, tray, and Single
  Instance cleanup: True
- dragging/capture after stop: `False/False`
- Present/device removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`
- fatal failure stage: 0
- exact cached `UnityWndClass` orderly close: Posted once
- `Application.Quit`: Requested through the managed orderly path

### Known notes

- The persisted absolute path is required for restoration but remains private
  record-only data.
- A temporarily unavailable external drive or network share falls back to the
  bundled character; M-042 does not search for a moved file.
- UniVRM 0.131.0 import still cannot abort immediately.
- Retired runtime roots retain resources until orderly shutdown under the
  validated M-041 ownership contract.
- M-042 does not copy, move, modify, or delete the external VRM.

### Manual validation

The user reported that all manual visual and interaction checks passed. The
validation confirmed:

- startup without a saved character selection displays the bundled model;
- selecting an external VRM creates the dedicated character-selection record
  without coupling it to Settings or window-position persistence;
- orderly exit and restart automatically restore the saved runtime model;
- startup remains Bundled-first by design, so the bundled model can be visible
  briefly while the saved path, file contents, SHA-256, UniVRM import, and
  activation complete;
- the restored runtime model becomes the only visible model, with no duplicate
  or stale bundled model remaining;
- orientation, image quality, animation, transparent click-through, opaque
  drag, Settings, F10/Escape, tray, and context-menu behavior remain correct;
- moved-file and changed-content cases fall back safely to the bundled model
  and allow explicit reselection;
- clearing the saved selection affects the next startup rather than switching
  the current runtime model immediately;
- the following startup uses the bundled model after the selection is cleared;
- final tray exit leaves no Player, mascot window, or tray icon.

M-042 is complete. Visual verification passed and the architecture baseline
advances to M-042.

## M-043 — Immediate Bundled Character Restoration

Status: Completed

Visual verification: Passed

Architecture baseline: M-043

### Summary

M-043 adds an immediate `同梱モデルに戻す` operation to the existing
Settings Character section. The command follows the existing ownership
boundaries:

```text
SettingsWindowController
→ RuntimeCharacterSelectionController
→ CharacterAssetManager.TryActivateBundledCharacter
→ synchronous Runtime-to-Bundled activation transaction
→ CharacterSelectionPersistenceManager.TryClear
→ stable UI result
```

The existing Scene `TEST_MODEL` root and its generation-1 Descriptor are
reused. No bundled model is instantiated, reloaded, imported, destroyed, or
owned by the Manager. The previous runtime-imported root becomes inactive and
its handle moves to retired ownership until orderly shutdown.

Detailed design:
`NativePlugin/docs/RuntimeBundledCharacterRestorationDesign.md`

### Activation and persistence behavior

- Bundled validation checks retained references and loaded Scene before
  changing the current root.
- Runtime root deactivation, Bundled root activation, Descriptor publication,
  ownership retirement, and generation increment occur synchronously without
  an await, yield, or frame boundary.
- Animator active/initialized state and `Base Layer.Standing Idle` are checked
  after root activation and before `Animator.Play`.
- `MascotExpressionController` restarts automatic blinking through one
  duplicate-safe initialization/resume path.
- activation failure keeps the prior runtime root, generation, ownership, and
  persistence record.
- persistence clear runs only after activation success or AlreadyBundled.
- clear failure is non-fatal and does not roll Bundled activation back.
- AlreadyBundled changes no root, Descriptor, generation, or handle.
- the existing next-start-only clear operation remains as a secondary UI
  action.
- neither operation deletes, modifies, or copies the external VRM file.

### Focused validation

M-043 focused log:
`NativePlugin/out/development-player-20260727-013516-200-7eaa1893.log`

- validation failure preserved current Runtime character: True
- Runtime-to-Bundled activation: Passed
- source/generation/active/retired handles:
  `BundledScene/5/0/3`
- active character root count: 1
- Scene loaded/Animator initialized/blink running: `True/True/True`
- persistence clear failure remained non-fatal: True
- AlreadyBundled clear/no-op: Passed
- repeated AlreadyBundled no-op: Passed
- busy/shutdown requests rejected: `True/True`
- settings/position/external VRM unchanged: `True/True/True`
- combined M-039 through M-043 selection validation: Passed
- final active/retired runtime handles after shutdown: `0/0`

The first focused attempt correctly exposed that `Animator.HasState` is not a
valid inactive-root structural check. The final implementation checks the
Controller structurally while inactive, then checks Animator initialization
and Standing Idle after activation and before playback. The failed attempt
preserved the current runtime character and all shutdown invariants.

### Build and regression validation

- Unity Development Player build: Passed
- new managed warnings/errors: None
- existing warning:
  `TransparentWindowController.borderless` CS0414
- native CMake/Ninja/dumpbin: Not required; native source, CMake, exports,
  D3D12, HRGN, and DirectComposition contracts are unchanged
- M-031 position focused tests: Passed
- M-033 Settings focused tests: Passed
- M-034 Settings UI/full-resolution presentation tests: Passed
- M-035 context-menu focused tests: Passed
- M-036 tray focused tests: Passed
- M-037 Player visibility focused tests: Passed
- M-038 Single Instance focused tests: Passed
- M-039 Character Asset focused tests: Passed
- M-040 Runtime VRM Import: Passed
- M-041 Runtime Character Selection: Passed
- M-042 persistence and startup restoration: Passed
- runtime-smoke Present/mask/region: `220/32/32`
- drag Present/region: `85/13`
- real-animated Present/generations/applied: `1200/125/125`
- real-animated all phases observed/applied: `True/True`
- real-animated readback errors/failure stage: `0/0`
- 60-second normal-runtime Present/region: `1563/224`
- normal-runtime auto-exit/smoke timer: `False/False`
- continuous/region failure stages: `0/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- final cleanup/fatal failure stage: `True/0`

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260727-012708-102-eef9064c.log`
- drag and focused managed regressions:
  `NativePlugin/out/development-player-20260727-012726-571-65cc322c.log`
- real-animated:
  `NativePlugin/out/development-player-20260727-012741-109-5104862f.log`
- 60-second normal-runtime orderly shutdown:
  `NativePlugin/out/development-player-20260727-012937-764-65563efd.log`

### Shutdown invariants

- no outstanding readback: True
- pending owned region count: 0
- initial region/style restored: `True/True`
- `Camera.targetTexture` restored: True
- `Application.runInBackground` restored: True
- active/retired runtime handles after release: `0/0`
- Player preview, character, selection, persistence, tray, and Single
  Instance cleanup: True
- dragging/capture after stop: `False/False`
- Present/device removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`
- fatal failure stage: 0
- exact cached `UnityWndClass` orderly close: Posted once
- `Application.Quit`: Requested through the managed orderly path

### Manual validation

The user completed the required manual verification successfully:

- select an external VRM and then use `同梱モデルに戻す`;
- the existing bundled model appears immediately and only one model is
  visible;
- Standing Idle and automatic blinking continue;
- orientation, size, image quality, Player preview, click-through, and native
  drag remain correct;
- the current-model label changes to `同梱モデル`;
- `character-selection.json` is removed after a successful return;
- `settings.json`, `window-position.json`, and the external VRM are unchanged
  by the command;
- restart remains on the bundled model;
- external selection and a second bundled return still work;
- F10, context menu, tray, Apply, Cancel, Close, and Escape retain their
  existing behavior;
- final tray exit leaves no Player, native mascot, or tray icon.

M-043 is complete. Visual verification passed and the architecture baseline
advances to M-043.

## M-044 — Render-Safe Runtime Character Disposal

Status: Completed

Automated validation: Passed

Visual verification: Passed

Architecture baseline: M-043

### Summary

M-044 replaces shutdown-only retention of inactive runtime-imported
characters with a Manager-owned, render-safe early-disposal transaction:

```text
retired FIFO
-> post-WaitForEndOfFrame safety validation
-> GraphicsFence cohort
-> GraphicsFence.passed
-> Dispose Requested
-> Unity root destruction observed
-> Release Confirmed
-> queue removal
```

`CharacterAssetManager` remains the sole runtime-character owner.
`RuntimeRetiredCharacterDisposalQueue` is its private non-MonoBehaviour
helper. The runtime pipeline only calls the Manager pump from the existing
end-of-frame loop. Activation, active Descriptor, generation, persistence,
Camera, D3D12, readback, HRGN, DirectComposition, and the final shutdown
release contract are unchanged.

Detailed design:
`NativePlugin/docs/RuntimeRetiredCharacterDisposalDesign.md`

### Settings input policy correction

While M-044 remained pending manual verification, the product interaction
policy was simplified. Settings is opened through the system tray, the native
mascot context menu, or Single Instance activation, and is closed through its
own `Cancel` or `Close` controls. Product keyboard shortcuts are not part of
the supported UI, so the temporary Windows F10 fallback, F10 diagnostics, and
Settings Escape handling were removed. Historical milestone entries retain
their original validation record; this policy change does not alter the M-044
GraphicsFence, retired-queue, disposal, release-confirmation, or character
ownership results.

Post-removal regression validation:

- Unity Development Player incremental build: Passed
- Settings Cancel/Close and Player visibility focused tests: Passed
- tray and native context-menu Settings reuse: Passed
- tray orderly exit: Passed
- Single Instance activation opened the existing Settings: Passed
- secondary exited normally: Passed
- M-044 Runtime A/B/C -> Bundled -> Runtime D validation: Passed
- M-044 fence cohorts/passed/timeouts/fallback: `3/3/0/0`
- M-044 Dispose requested/release confirmed/failure: `3/3/0`
- active/retired runtime handles after cleanup: `0/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors and continuous/region failure stages: `0`, `0/0`
- cleanup and tray cleanup: `True/True`

Post-removal logs:

- Settings, visibility, tray, context menu, and drag:
  `NativePlugin/out/development-player-20260728-215050-145-28463e2d.log`
- Runtime VRM selection and render-safe disposal:
  `NativePlugin/out/development-player-20260728-215135-123-15a23f39.log`
- Single Instance primary:
  `NativePlugin/out/development-player-20260728-215254-668-cd3a334e.log`
- Single Instance secondary:
  `NativePlugin/out/development-player-20260728-215408-254-fbedb71e.log`

### Safety and fallback

- fence cohorts use `AsyncQueueSynchronisation` and `PixelProcessing`;
- a retired handle is validated again after its fence passes;
- Dispose Requested and Release Confirmed are separate counters;
- the root remains queued until Unity's deferred destruction is observed;
- unsupported/unavailable GraphicsFence automatically retains the handle for
  shutdown and is not a failure;
- 300 frames or 5 seconds without a passed fence cancels early disposal and
  retains the handle for shutdown;
- safety and Dispose failures do not affect the current character, generation,
  persistence, Camera, or GPU pipeline and are not retried every frame;
- a retired queue count of 256 emits a non-fatal warning;
- managed-memory values are informational only.

### Focused validation

Focused M-044 log:
`NativePlugin/out/development-player-20260727-215501-599-70bfe2bd.log`

The Development launch exercised:

```text
Bundled
-> Runtime A
-> Runtime B
-> Runtime C
-> Bundled
-> Runtime D
```

- active character root after switching: 1
- Runtime active handle: 1
- retired queue after each settled transaction: 0
- GraphicsFence supported: True
- fence cohorts/passed/timeouts/unsupported fallback: `3/3/0/0`
- Dispose requested/release confirmed/failure: `3/3/0`
- current/maximum retired queue count: `0/1`
- render-safe disposal validation: Passed
- combined runtime-selection validation: Passed
- managed memory before: `247934383`
- managed memory peak: `313044702`
- managed memory after: `268012514`
- managed memory delta: `20078131`

Memory measurements are bytes from
`Profiler.GetTotalAllocatedMemoryLong()` and are not acceptance criteria.

### Build and validation state

- Unity Development Player incremental build: Passed
- new managed warnings/errors: None
- existing warning:
  `TransparentWindowController.borderless` CS0414
- native CMake/Ninja/dumpbin: Not required; native source, CMake, exports,
  D3D12 implementation, and native ABI are unchanged
- runtime-smoke Present/mask/region: `220/32/32`
- runtime-smoke cleanup/fatal stage: `True/0`
- real-animated Present/generations/applied: `1200/126/126`
- real-animated all phases observed/applied: `True/True`
- real-animated readback errors/failure stage: `0/0`
- drag-diagnostic Present/region: `85/13`
- drag/context menu/tray/position/Settings diagnostics: Passed
- drag cleanup/fatal stage: `True/0`
- argument-free normal runtime elapsed milliseconds: `61842`
- argument-free normal Present/region: `1700/243`
- normal runtime active/retired handles after release: `0/0`
- normal runtime cleanup/fatal stage: `True/0`
- Present/device-removed HRESULT: `S_OK/S_OK`
- readback errors: 0
- continuous/region failure stages: `0/0`
- no outstanding readback: True
- pending owned region count: 0
- initial region/style restored: `True/True`
- `Camera.targetTexture` and `Application.runInBackground` restored:
  `True/True`
- Tray cleanup/icon removal/owner destruction: `True/True/True`
- manual visual and interaction verification: Passed

Regression logs:

- runtime-smoke:
  `NativePlugin/out/development-player-20260727-215057-774-eaeace79.log`
- drag and focused managed regressions:
  `NativePlugin/out/development-player-20260727-220153-044-2cb977a2.log`
- real-animated:
  `NativePlugin/out/development-player-20260727-215200-577-54fc69c0.log`
- argument-free normal orderly shutdown:
  `NativePlugin/out/development-player-20260727-220219-876-49f0db56.log`

### Known notes and future work

- unsupported or timed-out fence entries intentionally remain allocated until
  orderly shutdown;
- the queue warning is diagnostic and non-fatal;
- UniVRM `RuntimeGltfInstance.SafeGetInitialPose()` uses a static `PoseMap`.
  M-044 does not modify package sources; retention behavior remains a future
  investigation.

### Manual verification

The user completed final manual verification and confirmed:

- Settings opens through the system tray and closes through both `Cancel` and
  `Close`;
- the native mascot context menu opens the existing Settings surface;
- Settings remains open while its in-window controls are used;
- external VRM and bundled-character switching both work;
- Settings remains reusable after character switching;
- Single Instance activation opens the existing Settings surface;
- tray `終了` completes orderly shutdown;
- no Unity Player process or native mascot window remains after exit.

The final validated product interaction policy has no F10 or Settings Escape
shortcut. Settings opens through the tray, mascot context menu, or Single
Instance activation and closes through its in-window `Cancel` or `Close`
controls.

M-044 is complete. Automated validation and user visual verification passed.
The next architecture baseline advances to M-044.
