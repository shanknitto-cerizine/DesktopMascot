# DesktopMascot development instructions — Version 2

Last architecture baseline: M-046

## Environment and non-negotiable constraints

- Unity 6.3 LTS: 6000.3.20f1
- Windows x64
- Direct3D 12 only; do not add a Direct3D 11 fallback or silently switch APIs
- DirectComposition
- Unity native rendering plugin
- Visual Studio Code
- CMake Presets, Ninja, and MSVC Build Tools
- UniVRM / VRM 1.0
- C++20

M-028 through M-046 are completed. The real animated mascot path, production
runtime foundation, native-window drag interaction, screen-bounds position
persistence, product-identity boundary, settings foundation, and the M-030
shutdown regression correction are validated. The optional Settings UI and
its full-resolution Player-preview overlay are also validated. The native
mascot context menu, including outside-click and Escape dismissal, Settings
reuse, and orderly exit, is validated. The system tray, Explorer recovery,
shared Settings/exit commands, and no-click orderly tray exit are validated.
The production Unity Player hidden-by-default lifecycle, centralized
visibility ownership, startup `DWMWA_CLOAK`, measured startup-flash quality,
and continued rendering while cloaked are validated.
Normal-runtime single-instance ownership, bounded secondary activation,
coalesced `OpenSettings` notification, existing Settings reuse, and normal
secondary exit are validated.
The normal-runtime Character Asset Foundation, single active-character
ownership, existing bundled `TEST_MODEL` reuse, structural startup
validation, and idempotent character cleanup are validated.
The Development-only Runtime VRM Import Foundation, pre-import SHA-256 and
size validation, transactional activation, bundled fallback, and ordered
runtime-import cleanup are validated.
Runtime VRM selection, dedicated atomic character-selection persistence,
Bundled-first startup restoration, SHA validation, non-fatal fallback, and
clear-on-next-start behavior are validated.
Immediate bundled-character restoration, persistence clearing after a
successful return, Standing Idle and blink resumption, and repeated
Runtime-to-Bundled switching are validated.
Normal runtime, runtime-smoke, drag-diagnostic, animated diagnostics, and
long-runtime orderly shutdown pass with continuous/region failure stages 0/0,
cleanup true, and fatal failure stage 0.

Do not downgrade Unity, treat Unity 6.5 or Unity 2022 as the active baseline,
or begin a new feature milestone without explicit scope.

## Character asset boundary

M-039 establishes the validated foundation for character selection.
`CharacterAssetManager` is the sole normal-runtime owner of active-character
selection and reference publication. It borrows the existing Scene
`TEST_MODEL` through `MascotCharacter`; it must not instantiate, destroy, move,
reimport, or persist that bundled character.

Normal runtime must complete structural character validation before creating
the runtime pipeline, Player preview, Settings host, context menu, or tray.
Missing, multiple, inactive, or structurally incomplete candidates are fatal
startup results connected to the generic managed orderly startup-abort path.
`Vrm10Instance.Runtime` may be unavailable immediately after Scene load and is
an observed readiness state, not a structural failure by itself.

Diagnostic modes retain their dedicated model-discovery paths and must not
create the production character manager. Character selection must not change
`settings.json`, `window-position.json`, Camera framing, Animator setup, the
single Camera-source Y-normalization boundary, D3D12/readback/HRGN behavior,
Player visibility ownership, or Single Instance ownership.

## Runtime VRM import boundary

M-040 is the validated architecture baseline and adds an opt-in
Development-launch import path. A launch without the
explicit development marker and absolute VRM path must retain the bundled
`TEST_MODEL`, bundled logical ID, and generation 1.

`RuntimeVrmCharacterSource` owns an import in progress and an imported root
before activation. A successful synchronous activation transfers ownership
exactly once to `CharacterAssetManager`, which remains the sole owner of
active-character selection and any active runtime-imported root.
`CharacterDescriptor` is reference-only. The bundled Scene root remains
non-owned.

Use UniVRM 0.131.0 `Vrm10.LoadPathAsync` on the Unity main thread with
`canLoadVrm0X: false` and `showMeshes: false`. Compute the content SHA-256
before import, keep the 256 MiB maximum file size as an explicit constant, and
do not persist an external path or source ID to settings or window-position
storage.

Activation must contain no `await`, yield, or frame boundary. Keep the bundled
root active until preparation succeeds; commit visibility, descriptor,
ownership, and generation as one main-thread transaction. A failed import or
activation must dispose the unactivated `RuntimeGltfInstance`, preserve the
bundled descriptor and generation, and continue normal runtime.

Release an active runtime-imported root only through
`RuntimeGltfInstance.Dispose()` after Camera, readback, region publication,
and native composition have stopped. UniVRM 0.131.0 core `LoadAsync` cannot
abort immediately; shutdown cancellation is cooperative and must not abandon
or force-terminate partially imported resources.

Runtime import must not alter Camera framing, Camera-source Y normalization,
D3D12, alpha masks, HRGN ownership, native click-through/drag,
DirectComposition, Player visibility, Single Instance, settings schema, or
position schema.

## Runtime VRM selection UI boundary

M-041 adds a Character section to the existing Settings overlay.
Character selection is an immediate runtime operation,
not a Settings ViewModel edit. Apply, Cancel, Close, dirty state, and Restore
Defaults must retain their validated meanings.

`RuntimeCharacterSelectionController` exclusively owns the picker/import
state machine, one-operation limit, duplicate rejection, operation lifetime,
and selection shutdown barrier. Settings renders its state and forwards input
only. Closing Settings does not cancel import. A same-content SHA-256 match
must skip `LoadPathAsync`, preserve generation and the active root, and report
AlreadyActive.

The Windows picker uses managed `IFileOpenDialog` on a dedicated STA thread.
Its owner is the current process's exact `UnityWndClass` obtained through
`UnityPlayerWindowVisibilityController`; never use the mascot or tray owner.
Do not log, display, persist, or place in a descriptor the absolute selected
path. UI may display only the file name. Single Instance never transfers a
VRM path or creates another picker.

`CharacterAssetManager` remains the only active-character ownership boundary.
After runtime-to-runtime replacement, the previous root is inactive and its
handle remains Manager-owned as retired until Camera and native GPU
presentation stop. Publish active/retired handle counts and release all to
zero during orderly cleanup. Runtime import failure keeps the current
character. Do not add a bundled-return command in M-041.

An open file dialog and UniVRM 0.131.0 import both use cooperative shutdown.
Reject new work after the barrier, discard picker results returned after it,
and never force-destroy the dialog or terminate an import/process.

## Runtime character-persistence boundary

M-042 uses the dedicated versioned record
`%LOCALAPPDATA%\DesktopMascotMinimal\character-selection.json`.
`CharacterSelectionPersistenceManager` is the only persistence owner. Do not
put the external VRM path or SHA into `settings.json`,
`window-position.json`, `CharacterDescriptor`, normal logs, or UI. UI may
show only the selected file name. The record does not own or copy the VRM.

Persist only after successful runtime-character activation. Persistence
failure is non-fatal and must not roll back the active root or generation.
Use a same-directory temporary file and atomic commit; if replacement is
unavailable, preserve the previous record and report a stable failure status.
Do not auto-delete corrupt, future-version, missing-file, unreadable, or
SHA-mismatched records.

Normal startup must first publish the bundled Scene character as generation 1
and then request at most one saved restore through
`RuntimeCharacterSelectionController`. Reject manual selection while restore
is pending or running. Recompute SHA-256 before import and do not call UniVRM
when it differs from the saved hash. Any restore failure keeps the bundled
runtime usable and is not a fatal startup failure.

The Settings command `次回起動時は同梱モデルを使用` clears only this
record; it does not switch or dispose the current character. Secondary
instances never initialize or modify character persistence. Close the
persistence write/clear barrier before selection/import shutdown and preserve
the M-041 active/retired ownership and cooperative-cleanup order.

## Immediate bundled-character restoration boundary

M-043 adds `同梱モデルに戻す` as a validated immediate Character operation.
Settings forwards the request to `RuntimeCharacterSelectionController`;
only `CharacterAssetManager` may change the active source, Descriptor,
generation, or runtime-handle ownership.

Reuse the Scene-owned bundled `TEST_MODEL` root and its retained generation-1
Descriptor. Do not instantiate, reload, destroy, import, or take ownership of
that bundled root. Runtime-to-Bundled replacement is one synchronous
main-thread activation transaction with no await or frame boundary. Validate
the retained root and loaded Scene before changing the current runtime root;
after activation, verify the Animator is enabled and initialized before
playing `Base Layer.Standing Idle`.

A successful switch makes the old runtime root inactive, moves its handle
from active to retired Manager ownership, publishes the retained Bundled
Descriptor, and increments generation exactly once. Dispose that retired
handle only after Camera and native GPU presentation stop. A validation or
activation failure keeps the current runtime root, Descriptor, generation,
handle counts, and persistence record unchanged.

Clear `character-selection.json` only after successful Bundled activation or
an AlreadyBundled no-op. A clear failure is non-fatal: keep Bundled active,
do not roll back generation or reactivate the old runtime root, and report
that the next-start setting could not be changed. The operation never deletes
or modifies the external VRM file. The next-start-only clear command remains
separate and preserves the current character.

Restore, picker, preflight, import, activation, persistence save/clear,
another Character operation, and shutdown exclude a Bundled request. Do not
queue it or implement latest-wins. Secondary instances only open Settings and
never start a Bundled switch. Settings dirty state, Apply, Cancel, Close,
settings persistence, and window-position persistence remain independent.

## Render-safe retired-character disposal boundary

M-044 is validated. Its render-safe retired-character disposal contract
remains authoritative within the current M-045 architecture baseline.
`CharacterAssetManager` remains the sole owner of
active and retired runtime-imported characters. Its private
`RuntimeRetiredCharacterDisposalQueue` helper owns the FIFO and GraphicsFence
cohorts; the runtime pipeline only pumps that Manager-owned queue after its
existing `WaitForEndOfFrame`.

Retired roots may be disposed early only after they are inactive, are not the
published active root or handle, and their submitted
`GraphicsFence.passed` is true. Keep Dispose Requested and Release Confirmed
as separate states. Removal from retired ownership requires Unity destroyed
root confirmation. Disposal is independent from activation and must never
change the active Descriptor, generation, persistence, Camera, or GPU
pipeline, or reactivate a disposed character.

If GraphicsFence is unsupported or unavailable, or if it does not pass within
300 frames or 5 seconds, retain the handle for the established post-pipeline
shutdown release path. A safety or Dispose failure also falls back to
shutdown without unbounded per-frame retry. Queue growth at 256 entries is a
non-fatal diagnostics warning. Managed-memory samples are informational only;
ownership counts, Dispose success, outstanding retired entries, and Release
Confirmed are authoritative.

Do not move queue ownership into a MonoBehaviour, static owner, selection
controller, or runtime pipeline. Do not alter UniVRM package sources to
address `RuntimeGltfInstance.SafeGetInitialPose()` static `PoseMap`; that is
future investigation.

## Product identity boundary

- Internal engineering and project name: `DesktopMascotMinimal`
- User-facing product name: `あなたといつも`
- Persistent production-storage identity:
  `%LOCALAPPDATA%\DesktopMascotMinimal`
- Do not globally replace one identity with another. Select the name according
  to responsibility and audience; keep source identifiers, namespaces,
  diagnostics, native DLL/export names, environment variables, repository
  paths, and stable storage keys internal unless a milestone explicitly
  changes their contract.

## Settings and runtime-state persistence

Keep the production files below
`%LOCALAPPDATA%\DesktopMascotMinimal` separate:

- `window-position.json` is authoritative for native mascot position,
  screen-bounds recovery, and completed-drag persistence.
- `settings.json` is authoritative for versioned application configuration
  and future user preferences.
- `character-selection.json` is authoritative only for the saved external
  VRM path, record version, and content hash used by startup restoration.

Position is high-frequency platform runtime state; settings are lower-frequency
application configuration; character selection is private external-source
state. Do not merge, duplicate, migrate, or cross-read these files without an
explicit migration milestone. Runtime-smoke and all diagnostic modes must not
read or write production `settings.json` or `character-selection.json`.

## Settings UI boundary

The optional M-034 settings window is hosted by the normal Unity Player. It
starts closed and must not affect normal runtime when it is never opened.
Settings may be opened only from the system tray, the native mascot context
menu, or the existing Single Instance activation route. `Cancel` and `Close`
discard unapplied edits and close the window. Product keyboard shortcuts,
including `F10` and Settings `Escape`, are intentionally unsupported.

Keep these UI responsibilities separate:

- `SettingsWindowController`: optional window lifecycle and commands;
- `SettingsViewModel`: editable copy and dirty state;
- `SettingsBinding`: controls-to-ViewModel synchronization;
- `SettingsValidation`: reusable validation and Apply eligibility;
- `SettingsManager`: the only settings persistence owner.

UI controls must never perform filesystem operations. `Restore Defaults`
changes only the ViewModel until the user selects `Apply`. Settings UI must
not read, display, or modify `window-position.json`. Do not add a tray icon,
context menu, or future settings without an explicit milestone.

M-045 supersedes the original M-034 Player-overlay presentation contract.
While Settings is visible, stop only the separate Player-preview Camera and
draw one completely opaque Settings surface across the Player. The production
Camera, character animation, D3D12 transfer, readback, masks, regions, and
DirectComposition mascot must continue. Closing Settings restores the same
Player-preview Camera before the established application-cloak path.

Do not scale the 256 x 256 `NormalizedTransferTexture` to the Player surface
or use the Player preview as input to D3D12, readback, masks, regions, or
DirectComposition. The Player-preview Camera normally renders directly to the
Player backbuffer and owns no preview RenderTexture. Its lifecycle and the
opaque IMGUI Settings surface must not clear or alter the production Camera's
`targetTexture`, native transfer textures, or the single Camera-source
Y-normalization boundary.

## Cross-platform Settings presentation boundary

M-045 is validated. Its cross-platform Settings presentation contract remains
authoritative within the current M-046 architecture baseline. Common Settings
state, validation, binding, and Unity UI must remain independent from
platform-window operations. `ISettingsPresentationHost` is the boundary for
show, close, bring-to-front, visibility, and presentation-state requests.

On Windows, `UnityPlayerWindowVisibilityController` implements that boundary
and remains the sole owner of exact `UnityWndClass` discovery and
`DWMWA_CLOAK`. The common `SettingsWindowController` must not access HWND,
DWM, or Windows APIs. Tray, mascot context menu, and Single Instance Settings
requests route through the presentation host.

While Settings is visible, the Unity Player renders an opaque Settings-only
surface. Only the separate Player-preview Camera may be suppressed; the
production Camera, character root, animation, 256 x 256 normalized transfer,
D3D12/readback/HRGN, and DirectComposition mascot must continue unchanged.
Closing Settings restores the same Player-preview Camera without per-frame
RenderTexture allocation.

Do not implement Player resizing, a native Settings window, Android
presentation, or a new UI framework as part of this foundation stage.

## Native mascot context-menu boundary

M-035 adds a native popup menu to the existing DirectComposition mascot
`HWND`. The menu contains exactly `設定` and `終了`. Its hit behavior remains
owned by the currently applied `SetWindowRgn`; do not add separate alpha hit
testing or make transparent pixels interactive.

The native window procedure may publish only a small monotonic command value.
Managed code polls and consumes each generation once. `設定` reuses the single
M-034 `SettingsWindowController` and may identify and activate the Player by
process ID plus `UnityWndClass`. `終了` must route through
`DesktopMascotRuntimePipeline.RequestOrderlyQuit`; it must not destroy the
native window or terminate the process directly.

Every `HMENU` created for the popup remains native-owned until
`DestroyMenu`. Popup cancellation, window destruction, and shutdown publish
no command and must leave the live-owned menu count at zero. Right-button menu
handling must not alter the validated left-button drag contract or create a
completed-drag generation. System-tray integration remains outside M-035.

Because the mascot `HWND` is normally non-activating, popup tracking may make
it the temporary foreground owner only for the `TrackPopupMenuEx` lifetime so
outside clicks and Escape dismiss reliably. Post `WM_NULL` after tracking and
restore the previously foreground window when valid; do not turn this into
persistent mascot-window activation.

## System tray boundary

M-036 adds a Windows notification-area entry point to normal runtime without
changing the M-035 mascot context menu. A dedicated native thread owns an
ASCII-class-name hidden tool window, one GUID-identified `NOTIFYICONDATAW`,
the popup `HMENU`, and the dynamically generated application `HICON`. It must
use `NIM_ADD`, `NIM_SETVERSION` with `NOTIFYICON_VERSION_4`, and exactly one
logical `NIM_DELETE` during idempotent cleanup.

The tray callback and mascot context menu publish the same `OpenSettings = 1`
and `RequestExit = 2` values through one generation-based pending-command
boundary. Command source is diagnostic metadata only and must not change
semantics. Tray `設定` reuses the M-034 controller and Player activation path;
tray `終了` uses managed orderly shutdown. Single left-click has no action and
left double-click opens Settings.

Handle the registered `TaskbarCreated` message by restoring the GUID icon,
version, and tooltip once per notification. Never re-register after the
shutdown barrier. Stop tracking an active popup, delete the tray icon, destroy
the owner window, and destroy the owned icon before existing native teardown.
The tray owner must never replace the mascot `HWND`, affect its activation,
region, drag contract, Z-order, or create another visible application surface.

M-046 validates bounded tray startup recovery. Create the hidden owner window,
register `TaskbarCreated`, and keep the native message loop alive even when
the initial `NIM_ADD` or `NIM_SETVERSION` attempt fails. Retry registration at
the constant 200 ms interval for at most 75 attempts; do not use an unbounded
retry, busy loop, or per-frame managed retry. A successful retry or
`TaskbarCreated` recovery must converge to one registered GUID icon and cancel
the pending retry window. Keep the owner window and message loop available
after retry exhaustion so a later `TaskbarCreated` notification can still
recover the icon. Suppress all retry and re-registration work after the
shutdown barrier.

Keep separate diagnostics for `NIM_ADD` and `NIM_SETVERSION` attempts,
successes, and last errors, plus `TaskbarCreated`, retry, exhaustion,
shutdown-suppression, final-result, and current-registration state. Managed
startup may observe the native recovery state with a bounded interval, but
must not create a second native tray thread or change the established
`startAttempted` ownership contract.

## Production Player visibility boundary

M-037 is the validated architecture baseline for production Player
visibility. In normal runtime, the existing Unity Player becomes an on-demand
Settings host. Apply the initial
application cloak from `RuntimeInitializeLoadType.BeforeSplashScreen` as soon
as the current process's exact `UnityWndClass` is discoverable. Keep
runtime-smoke and explicit diagnostic Players visible unless a focused test
opts into visibility testing.

`UnityPlayerWindowVisibilityController`, including its Windows-specific
partial implementation, is the sole owner of Player visibility state. No
other controller, helper, diagnostic, or runtime component may call
`ShowWindow`, apply/remove `DWMWA_CLOAK`, or independently classify Player
visibility. Identify and cache the Player only by current process ID plus
exact `UnityWndClass`; revalidate the cached `HWND` before use. Do not use
`Process.MainWindowHandle`, `Process.CloseMainWindow`, localized titles, the
mascot `HWND`, or the tray owner. Native mascot and tray Settings commands
must route through this controller before reusing the single
`SettingsWindowController`.

`Cancel` and `Close` retain their discard/close semantics and then
application-cloak only the Player with
`DwmSetWindowAttribute(DWMWA_CLOAK)`. Do not use `ShowWindow(SW_HIDE)`:
Unity 6000.3.20f1 can stop producing `WaitForEndOfFrame` when its rendering
surface is hidden. `Apply` and `Restore Defaults` do not cloak it. A cloaked,
unfocused Player must retain its `WS_VISIBLE` rendering surface and keep
animation, Present, readback, region publication, and command polling active
through the production `Application.runInBackground = true` lifetime. Do not
restore that value during ordinary hiding.

Ordinary hide must not use `SW_HIDE`, post `WM_CLOSE`, call
`Application.Quit`, destroy a window, or enter shutdown. Once the
orderly-shutdown barrier begins, reject
new show requests and preserve the M-036 final sequence: cleanup first, one
exact cached `UnityWndClass` `WM_CLOSE`, then `Application.Quit`. The active
normal-runtime title-bar close is intercepted through the existing managed
quit boundary to discard Settings and hide the Player; after shutdown begins,
the final close must pass through.

Measure startup visibility from Windows process creation time through
successful application cloak and log `Player Flash Measured: <ms>`. The
quality target is no visibly long Player display. A true zero-millisecond
guarantee is not expected because Unity creates the native window and starts
the managed runtime before `BeforeSplashScreen` callbacks can execute; retain
the measured value and manual flash result in `Milestones.md`.

## Single-instance boundary

Normal runtime uses the stable session-local internal mutex
`Local\DesktopMascotMinimal.SingleInstance.v1`. The primary process exclusively
owns the mutex and the independent named-event notification channel. Explicit
diagnostic modes do not participate in production single-instance ownership.

A secondary normal-runtime process must stop before settings, position,
mascot, tray, or runtime initialization. It may only signal the primary's
`OpenSettings` event with a short bounded retry and then exit normally. The
primary consumes the signal on the managed main thread and routes it through
`UnityPlayerWindowVisibilityController` to the existing single
`SettingsWindowController`. An already-open Settings window and unapplied
edits must be preserved.

The activation event is an auto-reset pending latch: multiple signals received
before consumption converge to one logical `OpenSettings` request rather than
an unbounded generation backlog. Single-instance notification must remain
independent of the tray owner, mascot window, Player window, product display
name, and persistent files.

Close the single-instance acceptance barrier at the beginning of orderly
shutdown. A secondary arriving after that barrier uses only bounded retry,
does not promote itself during the same launch, and exits normally. Release
the notification channel and mutex only during final idempotent cleanup.
Abnormal owner termination must allow the next launch to acquire abandoned or
released mutex ownership.

Do not use process-name checks, localized titles, `Process.MainWindowHandle`,
`Process.CloseMainWindow`, process kill, `TerminateProcess`, `ExitProcess`,
`Environment.Exit`, `SendInput`, or global hooks for single-instance behavior.
Only `UnityPlayerWindowVisibilityController` may show/uncloak the Player.

## Validated production pipeline

Preserve this pipeline:

```text
Unity Camera
→ Camera source RenderTexture
→ exactly one explicit Camera-source Y-normalization Blit
→ normalized transfer RenderTexture
→ Unity native render-thread callback
→ native D3D12 copy/readback
→ alpha mask
→ HRGN construction
→ SetWindowRgn
→ DXGI composition swap chain
→ DirectComposition native mascot window
```

Camera orientation is corrected exactly once at the Camera-source boundary:

```csharp
Graphics.Blit(
    cameraSourceTexture,
    normalizedTransferTexture,
    new Vector2(1, -1),
    new Vector2(0, 1));
```

Downstream texture transfer, D3D12 copy, readback, masks, regions, composition,
and validated previews must not perform another Y inversion. Shader-side Y
inversion is prohibited. Do not move or redesign this normalization boundary
without a dedicated regression milestone.

## Speech presentation boundary

M-048 is validated as an additive implementation under the M-046 architecture
baseline and M-047.5 design baseline. Its corrected full-body framing,
170 x 64 center-aligned card, 12/11-pixel Japanese text, split manual close
checks, diagnostic Character-selection integration, and orderly Speech
shutdown are visually verified. Shared
Speech data contains display-only `SpeakerData` and `SpeechMessage` values.
`SpeechPresentationController` owns exactly one current message, has no queue,
and is unaware of event, rule, script, conversation-pack, or AI-provider
types. A same-ID Show coalesces, a different ID returns Busy, and a matching
presentation generation plus first-close-wins governs click and timeout
closure.

Keep the Speech rendering path independent:

```text
TMP Speech view
→ dedicated 170×64 B8G8R8A8_SRGB RenderTexture
→ render event 9
→ independent Speech D3D12 copy and composition swap chain
→ independent owned top-level Speech HWND
```

Never composite Speech into the mascot's 256×256 transfer, add another
Camera-source Y normalization, reuse the mascot alpha mask/HRGN, parent Speech
under a Character root, or expose HWND/HRGN/D3D12 types through the shared
presentation interface. `WindowsSpeechPresentationHost` is the Windows
boundary. The Speech HWND is nonactivating and owned by the mascot HWND, but
owns its own DComp target/visual/swap chain, copy objects, and rounded input
HRGN. Unity device, queue, fence, factory, composition device, and source
texture remain borrowed and must not be released.

Anchor resolution observes `CharacterAssetManager` generation without taking
Character ownership. Prefer humanoid hips/leg bones, fall back to current
Renderer bounds, and discard old references on generation changes. Native
positioning starts from the actual mascot `GetWindowRect`, reads the current
applied silhouette with `GetWindowRgnBox`, and aligns the Speech-card center
to that silhouette center. Work-area clamping applies only the smallest
necessary horizontal correction; it uses no activation or Z-order change and
never persists a Speech position.

Close the Speech shutdown barrier and drain/release Speech resources before
mascot composition teardown. Speech cleanup is idempotent and must succeed for
never-shown and partial initialization. Speech failure is isolated from the
mascot runtime, but it fails `message-window-diagnostic`. Do not add a
conversation pack, schema, provider, AI integration, persistence, variable
timeout, queue, or production greeting without a later explicit milestone.

## Conversation domain contract

M-049 establishes only the platform-neutral Conversation Domain Contracts
Foundation. `ConversationEvent`, `ConversationRequest`, and
`ConversationResponse` are immutable managed data and own no Character,
Speech, Unity, native, filesystem, network, or GPU resource.

The domain uses stable lowercase ASCII `ConversationLogicalId` values for
trigger, character, response, and speaker identity, and a distinct
caller-supplied `ConversationRequestId` for evaluation correlation. Do not
trim, lowercase, normalize, or generate either identifier in the domain.
Expected invalid input must use stable non-localized validation results and
must not create a valid domain object or throw an expected exception.

The only M-049 `ConversationPresentationIntent` is `CharacterUtterance`.
It expresses character intent, not acceptance by Speech or any other
presentation. `SpeechShowResult.Busy` remains M-048 presentation semantics;
do not add Busy, queue, priority, retry, scheduling, timestamp, sequence,
animation, expression, audio, Text SE, provider, pack, persistence, or
platform fields to the M-049 domain types.

Domain source may use basic `System` types only. It must not reference
UnityEngine, MonoBehaviour, GameObject, Camera, RenderTexture, Win32, HWND,
HRGN, D3D12, DirectComposition, DXGI, Android lifecycle, AI SDKs,
CharacterDescriptor, SpeechMessage, filesystem paths, or VRM paths. It has no
Update/LateUpdate, polling, worker, timer, network, filesystem, history,
queue, retry, cache, or native/GPU resource.

The built-in trigger taxonomy is stable logical IDs, not an enum. M-049 does
not create a registry, aliases, wildcard matching, hierarchy, pack-defined
trigger loading, production event wiring, Rule/Script Engine, Conversation
Pack, Speech adapter, AI provider, typing, Text SE, Settings, persistence, or
schema change. See `NativePlugin/docs/ConversationDomainDesign.md` and
`NativePlugin/docs/ProjectPrinciples.md`.

## Transparency and pixel interaction

- Transparency uses a premultiplied-alpha DirectComposition composition swap
  chain.
- `SetWindowRgn`, generated from the alpha mask, defines pixel-level native
  interaction.
- Transparent pixels outside the active HRGN click through; opaque mascot
  pixels receive native window input.
- Do not replace region-based behavior with `WS_EX_TRANSPARENT`.
- Do not use `HTTRANSPARENT` as the primary implementation.
- Do not use `UpdateLayeredWindow`, `SetLayeredWindowAttributes`, or
  `WS_EX_LAYERED` color-key transparency.
- `DwmExtendFrameIntoClientArea` is not the primary transparency solution.

## Native mascot-window drag contract

Dragging belongs to the DirectComposition mascot `HWND` and starts from native
left-button input inside the active region. Preserve the validated behavior:

- record origins with `GetCursorPos` and `GetWindowRect`;
- classify dragging with `SM_CXDRAG` / `SM_CYDRAG`;
- use `SetCapture` and release capture on every completion, cancellation,
  destruction, failure, and shutdown path;
- move only the mascot `HWND`;
- use `SetWindowPos` with no resize, activation, or Z-order change;
- keep the mascot window non-activating.

Do not use global mouse hooks, `SendInput`, or movement of the Unity Player
window as a substitute. Position persistence, snapping, bounds recovery,
resizing, and context menus do not belong in the drag layer unless a later
milestone explicitly adds them.

## D3D12 and COM ownership

- Use RAII for owned native resources and `Microsoft::WRL::ComPtr` for owned
  COM interfaces.
- Never `AddRef`, `Release`, or place Unity-owned D3D12 objects into an owning
  `ComPtr` unless the Unity API explicitly transfers ownership.
- Never execute GPU work directly from the Unity C# main thread. Graphics
  commands must run through Unity render-thread callbacks.
- Preserve validated resource-state tracking, command ordering, and copy
  synchronization.
- Do not recreate the D3D12 device, swap chain, or composition system for
  ordinary interaction changes.
- Inspect the copied Unity 6000.3.20f1 headers before using a Unity graphics
  interface. Never guess an `IUnityGraphicsD3D12` version or method.

## HRGN ownership

- Every created `HRGN` must have exactly one clear owner.
- Before a successful `SetWindowRgn`, the caller owns the region and must
  delete it on failure or cancellation.
- After a successful `SetWindowRgn`, Windows owns that region; the caller must
  not delete it.
- Failed, superseded, or cancelled queued operations must release every
  caller-owned region.
- Pending owned-region count must reach zero during orderly shutdown.
- Process-wide GDI-object count is supporting evidence, not direct proof of an
  HRGN leak. Direct ownership counters and restoration invariants take
  precedence.
- Do not broadly suppress GDI or region failures, and never suppress an
  active-runtime failure as a shutdown cancellation.

## Runtime, diagnostics, and mode selection

Normal runtime has no automatic completion timer and must exit through managed
orderly shutdown. Diagnostics are opt-in and must not auto-start in production
runtime or leak their completion criteria into it.

Supported explicit Development Player modes are:

```text
DESKTOP_MASCOT_MODE=runtime-smoke
DESKTOP_MASCOT_MODE=real-static-diagnostic
DESKTOP_MASCOT_MODE=real-animated-diagnostic
DESKTOP_MASCOT_MODE=drag-diagnostic
DESKTOP_MASCOT_MODE=message-window-diagnostic
```

The command-line equivalent is `--desktop-mascot-mode=<mode>`. With no explicit
mode, `runtime` starts. `runtime-smoke` is explicitly selected and time-limited;
normal runtime is unlimited.

## Orderly shutdown contract

Preserve this ordering and keep cleanup idempotent:

1. stop accepting new alpha-mask region generations;
2. stop posting new region-apply messages;
3. cancel or drain queued and in-flight region work under the HRGN ownership
   rules;
4. cancel native dragging and release capture when owned;
5. restore the initial window region and style;
6. stop composition and destroy the native window;
7. restore `Camera.targetTexture`;
8. restore `Application.runInBackground`;
9. complete managed and native cleanup without accessing destroyed state.

Expected orderly-shutdown invariants are:

- continuous failure stage 0 and region failure stage 0;
- no outstanding readback;
- pending owned-region count 0;
- initial region and initial style restored;
- `Camera.targetTexture` and `Application.runInBackground` restored;
- dragging false and mouse capture not owned;
- Present HRESULT and device-removed HRESULT both `S_OK`;
- readback errors 0, cleanup true, and fatal failure stage 0.

An expected shutdown cancellation may be non-fatal only after the shutdown
barrier has closed new work and ownership/restoration invariants prove safe
cleanup. Do not clear or suppress failures that first occurred while the
runtime was active.

## Responsibility boundaries and coding rules

Keep these responsibilities separate:

- Camera source normalization;
- managed runtime orchestration;
- native Unity bridge;
- D3D12 transfer and readback;
- composition window and swap chain;
- animated alpha-region ownership;
- native interaction;
- bounded logging and diagnostics;
- settings and persistence.

Avoid broad manager classes that combine unrelated responsibilities. Prefer
additive APIs, explicit ownership, idempotent cleanup, incremental changes,
source inspection before editing, and coherent complete-file changes over
disconnected snippets. Preserve existing user changes and previously validated
paths. Build after structural changes.

## Repository structure boundary

M-046 is validated and is the current architecture baseline. It reorganizes
source placement while preserving the M-045 product responsibility
boundaries. Production managed code lives under
`Assets/_Project/Runtime`, focused diagnostics under
`Assets/_Project/Diagnostics`, and Editor-only code under
`Assets/_Project/Editor`.

Within Runtime, keep core orchestration, Character, Persistence, Settings,
Presentation, and Platform responsibilities physically separate. Windows-only
HWND, visibility, tray, context-menu, single-instance, position, and native
interaction code belongs under `Runtime/Platform/Windows`. Platform-neutral
Settings presentation contracts and ViewModel behavior remain outside that
Windows boundary. Older sample-scene helpers retained under `Runtime/Legacy`
must not become dependencies of new production work.

Move Unity assets only with their existing `.meta` sidecars. Preserve GUIDs,
namespaces, type names, serialized field names, partial-class pairing, public
APIs, diagnostic mode names, and fixed paths unless a dedicated milestone
changes them. Do not introduce an assembly definition as an incidental
cleanup.

Keep `NativePlugin/include`, `NativePlugin/src`, copied Unity headers, CMake
files, and native output contracts stable. `NativePlugin/docs` is
documentation; `NativePlugin/out` is generated output and retained diagnostic
evidence, never a source dependency. Keep the canonical
`Tools/Development/*.ps1` entry points at their current paths.

See `NativePlugin/docs/RepositoryStructureDesign.md` for the current layout,
intentional no-move decisions, and future cleanup candidates.

## Mandatory milestone workflow

Before implementation:

- inspect the relevant source and current documentation;
- report the smallest implementation plan;
- identify affected validated invariants;
- define automated and manual validation requirements.

During implementation:

- make incremental, scoped changes;
- preserve earlier validated paths;
- do not begin unrelated features;
- do not rewrite historical milestone results;
- do not claim success from compilation alone.

Before marking a milestone complete, require as applicable:

- native build and Unity Development Player build pass;
- `dumpbin` export validation when exports changed;
- `git diff --check` passes;
- required automated diagnostics and normal-runtime regression pass;
- affected earlier diagnostics pass;
- orderly-shutdown invariants pass;
- required manual visual or interaction checks are reported by the user.

Until required user verification occurs, use:

```text
Status: Automated Validation Passed
Visual verification: Pending
```

Only after the user actually reports success use:

```text
Status: Completed
Visual verification: Passed
```

Never claim user-performed validation without the user's report.

## Logging and development history

- Do not delete logs while a milestone or regression investigation is active.
- Preserve the latest failing log, the first passing log after its fix, and
  final regression logs referenced by `Milestones.md`.
- Do not reorganize `NativePlugin/out` during an active investigation. Archive
  or remove redundant logs only after milestone completion.
- Avoid continuous per-frame logging unless explicitly required. Prefer
  one-shot failure-site logs and final summaries.
- Do not log private user content, conversation history, or future shared
  mascot memory without an explicit diagnostic requirement.
- `NativePlugin/out` must never become a source-code dependency.

## Build commands and generated artifacts

Canonical development commands from the repository root are:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Development\Build-Native.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Development\Build-UnityIncremental.ps1
```

The direct debug preset remains valid from `NativePlugin` in a Visual Studio
Developer PowerShell:

```powershell
cmake --preset windows-debug
cmake --build --preset windows-debug
```

Native output consumed by Unity is:

```text
Assets/Plugins/x86_64/DesktopMascotNative.dll
```

Source and documentation are authoritative. DLL, PDB, ILK, Ninja output, and
Player builds are generated artifacts; never edit them manually. Running
Players may lock `DesktopMascotNative.dll`. When diagnosing stale binaries,
close the Player and compare the Assets and Player DLL timestamps or hashes.
Do not invent unverified build commands.

## Git policy

- Inspect `git status` and relevant diffs before broad edits.
- Do not commit or create tags automatically.
- Do not force-push or rewrite history.
- Do not discard or overwrite unrelated user changes.
- Do not add generated build output or runtime logs to source control unless
  the repository intentionally tracks that specific artifact.

## Warning policy

The known warning is:

```text
TransparentWindowController.borderless
CS0414: assigned but its value is never used
```

Do not suppress it globally. Before removing or renaming a potentially
serialized Unity field, inspect scene and prefab serialization, older
diagnostic use, and migration needs. Treat warnings intentionally, but do not
block an unrelated completed milestone for this known non-functional warning.

## Future cross-platform direction

A future goal may include Windows and Android mascots sharing settings,
durable mascot state, and memory. Conceptually separate future shared logic
into:

- shared domain/brain state;
- settings and persistence;
- synchronization;
- platform renderer;
- platform integration.

Do not couple shared mascot state to DirectComposition, Win32 `HWND`, `HRGN`,
D3D12, Android Overlay, or Android lifecycle APIs. Keep screen position,
native handles, and capture state platform-local. Give persisted data a schema
version and route persistence through a dedicated interface or service instead
of scattered direct file writes.

Do not add Android dependencies to the Windows runtime before an explicit
Android milestone, and do not implement network synchronization yet. This is
architectural direction, not a current deliverable.

## Repository layout and detailed documentation

Native sources remain organized as:

```text
NativePlugin/
├── CMakeLists.txt
├── CMakePresets.json
├── include/DesktopMascotNative/
├── src/
└── unity/
```

Unity plugin headers must come from:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PluginAPI
```

Keep this file focused on non-negotiable constraints, validated boundaries,
workflow, and completion rules. Detailed implementation design and history
remain in:

- `NativePlugin/docs/ProductionRuntimeFoundationDesign.md`
- `NativePlugin/docs/NativeMascotWindowDragDesign.md`
- `NativePlugin/docs/Milestones.md`
