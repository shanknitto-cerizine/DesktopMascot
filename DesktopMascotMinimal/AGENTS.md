# DesktopMascot development instructions — Version 3

## Authority and current baseline

This file is the authoritative always-loaded source for repository-wide
development, architecture, ownership, safety, validation, approval, and Git
rules. At the start of every Codex thread, read this file and
`NativePlugin/docs/DevelopmentHandoff.md`, then verify the live Git baseline
read-only. Live Git is authoritative for branch, HEAD, upstream, working-tree,
index, and in-progress-operation state.

Authority order:

1. live Git state for Git facts;
2. this file for current operative repository rules;
3. `NativePlugin/docs/Milestones.md` for milestone status and validation
   history;
4. the relevant subsystem design document for subsystem detail;
5. `NativePlugin/docs/ProjectPrinciples.md` for future design guidance;
6. `NativePlugin/docs/DevelopmentHandoff.md` for current intent and navigation.

The current architecture baseline is **M-046**. M-047.5 is the active
Speech-specific design baseline. M-048 and M-049 through M-053 are additive
and do not advance the architecture baseline. Completed scope and validation
history are authoritative in `NativePlugin/docs/Milestones.md`.

Do not advance a baseline or begin a feature milestone without explicit scope.

## Environment and non-negotiable platform constraints

- Unity 6.3 LTS: 6000.3.20f1
- Windows x64
- Direct3D 12 only; never add a Direct3D 11 fallback or silently switch APIs
- DirectComposition
- Unity native rendering plugin
- Visual Studio Code
- CMake Presets, Ninja, and MSVC Build Tools
- UniVRM 0.131.0 / VRM 1.0
- C++20

Do not downgrade Unity or treat Unity 6.5 or Unity 2022 as the active baseline.
Unity plugin headers must come from:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Data\PluginAPI
```

## Current ownership and responsibility map

Keep these boundaries separate:

- `CharacterAssetManager`: sole normal-runtime owner of active-character
  selection/reference publication and active/retired runtime-import handles;
- bundled `TEST_MODEL`: Scene-owned and borrowed, never Manager-owned;
- `RuntimeVrmCharacterSource`: an import in progress and an imported root
  before activation;
- `RuntimeCharacterSelectionController`: picker/import/restore/Bundled
  operation state, exclusion, lifetime, and shutdown barrier;
- `CharacterSelectionPersistenceManager`: sole owner of
  `character-selection.json` reads, writes, and clears;
- `SettingsManager`: sole owner of `settings.json`;
- window-position persistence: sole owner of `window-position.json`;
- `UnityPlayerWindowVisibilityController`: sole owner of exact Player HWND
  discovery and Player visibility/cloak state on Windows;
- `ISettingsPresentationHost`: common Settings presentation boundary;
- `SpeechPresentationController`: exactly one current Speech message;
- `WindowsSpeechPresentationHost`: Windows Speech presentation and its owned
  native/GPU resources;
- managed runtime orchestration, native Unity bridge, D3D12 transfer/readback,
  composition, alpha-region ownership, native interaction, persistence,
  Settings, presentation, diagnostics, and logging remain separate.

Do not create a broad owner that combines unrelated responsibilities. Use
explicit ownership, bounded state, additive APIs, and idempotent cleanup.

## Character selection, import, and persistence

### Character foundation — M-039

Normal runtime must structurally validate exactly one active, complete bundled
candidate before creating the runtime pipeline, Player preview, Settings host,
context menu, or tray. Structural failure is a fatal managed startup-abort;
temporary unavailability of `Vrm10Instance.Runtime` after Scene load is an
observed readiness state, not structural failure.

`CharacterAssetManager` borrows the existing Scene `TEST_MODEL` through
`MascotCharacter`; it must not instantiate, destroy, move, reimport, persist,
or take ownership of it. Diagnostic modes keep their dedicated discovery paths
and must not create the production character manager.

Character operations must not change Settings/ViewModel semantics,
`settings.json`, `window-position.json`, Camera framing, the single
Y-normalization boundary, D3D12/readback/HRGN behavior, Player visibility, or
Single Instance ownership.

Full contract: `NativePlugin/docs/CharacterAssetFoundationDesign.md` under
“Normal-runtime startup boundary”, “CharacterAssetManager”, and “Ownership and
cleanup” (M-039 in `Milestones.md`).

### Runtime VRM import and selection — M-040/M-041

Without an explicit Development import request, normal launch retains the
bundled logical ID and generation 1. Import uses UniVRM 0.131.0
`Vrm10.LoadPathAsync` on Unity's main thread with `canLoadVrm0X: false` and
`showMeshes: false`, after SHA-256 and the explicit 256 MiB size limit.

Activation is one synchronous main-thread transaction with no `await`, yield,
or frame boundary. Keep the current root active until preparation succeeds;
then commit visibility, descriptor, ownership, and generation exactly once.
Failure disposes an unactivated `RuntimeGltfInstance` and preserves the current
character. Ownership transfers to `CharacterAssetManager` exactly once.

The Windows picker uses managed `IFileOpenDialog` on a dedicated STA thread,
owned by the exact current-process `UnityWndClass` obtained through the Player
visibility owner. It must never use the mascot or tray owner. Only one
Character operation may run; duplicate same-content SHA skips import and
preserves root/generation. Closing Settings does not cancel an import.
Character selection is an immediate runtime operation, not a Settings
ViewModel edit; Apply, Cancel, Close, dirty state, and Restore Defaults retain
their established meanings.

External absolute VRM paths and content hashes are private. Do not log or show
an absolute path, store it in a descriptor, transfer it through Single
Instance, or persist it outside `character-selection.json`. UI may show only
the file name.

Picker and UniVRM shutdown are cooperative: reject new work after the barrier,
discard picker results returned after it, wait for in-flight import completion,
and never force-destroy a dialog, abandon imported resources, terminate an
import, or terminate a process.

Full contracts: `RuntimeVrmImportFoundationDesign.md` and
`RuntimeVrmSelectionUiDesign.md` under their input, ownership, activation,
privacy, and shutdown headings (M-040/M-041 in `Milestones.md`).

### Character persistence and bundled restoration — M-042/M-043

Keep these production files separate under
`%LOCALAPPDATA%\DesktopMascotMinimal`:

- `window-position.json`: high-frequency native mascot position and recovery;
- `settings.json`: versioned application configuration;
- `character-selection.json`: private external VRM path, record version, and
  content hash used only for startup restoration.

Do not merge, duplicate, migrate, or cross-read these stores without an
explicit migration milestone. Runtime-smoke and diagnostic modes must not read
or write production `settings.json` or `character-selection.json`.

Persist character selection only after successful activation. Persistence
failure is non-fatal and must not roll back the active root or generation.
Use a same-directory temporary file and atomic commit while preserving an old
record on replacement failure. Never auto-delete corrupt, future-version,
missing-file, unreadable, or SHA-mismatched records.

Startup publishes bundled generation 1 first and requests at most one restore
through the selection controller. Recompute SHA before import; a mismatch must
not call UniVRM. Restore failure is non-fatal and keeps bundled runtime usable.

Immediate “同梱モデルに戻す” reuses the retained Scene-owned root and descriptor.
Only `CharacterAssetManager` changes source, descriptor, generation, or handle
ownership. The switch is an atomic main-thread transaction. Validate Scene,
root, and Animator first; after success, resume `Base Layer.Standing Idle` and
blink. A failed activation changes nothing.

After a successful Bundled activation or AlreadyBundled no-op, clear only
`character-selection.json`. Clear failure is non-fatal and never rolls back
Bundled activation. Never modify or delete the external VRM. The separate
next-start-only clear command does not switch the current character.

Restore, picker, preflight, import, activation, persistence, another Character
operation, Bundled switch, and shutdown mutually exclude as defined by the
selection controller; do not queue or implement latest-wins.

Full contracts: `RuntimeCharacterPersistenceDesign.md` and
`RuntimeBundledCharacterRestorationDesign.md` (M-042/M-043 in
`Milestones.md`).

### Render-safe retired-character disposal — M-044

`CharacterAssetManager` remains the sole owner of active and retired imported
characters. Its private non-MonoBehaviour
`RuntimeRetiredCharacterDisposalQueue` owns FIFO/fence cohorts; the runtime
pipeline only pumps it after the existing `WaitForEndOfFrame`.

Dispose a retired root early only when it is inactive, is not published active
state, and its submitted `GraphicsFence.passed` is true. Keep Dispose Requested
and Release Confirmed distinct; remove ownership only after Unity destroyed-root
confirmation. Disposal must not affect active descriptor, generation,
persistence, Camera, GPU pipeline, or character activation.

Unsupported/unavailable fences, a 300-frame or 5-second timeout, safety
failure, or Dispose failure retain the handle for the post-pipeline shutdown
path without unbounded retry. Queue size 256 is a non-fatal warning. Ownership
counts and Release Confirmed, not managed-memory samples, are authoritative.
Do not modify UniVRM package sources to address its static `PoseMap`.

Full contract: `NativePlugin/docs/RuntimeRetiredCharacterDisposalDesign.md`
(M-044 in `Milestones.md`).

## Product identity, Settings, and Windows entry points

### Product identity

- internal/project/storage identity: `DesktopMascotMinimal`;
- user-facing product name: `あなたといつも`;
- stable production storage: `%LOCALAPPDATA%\DesktopMascotMinimal`.

Do not globally replace identities. Keep source identifiers, namespaces,
diagnostics, native DLL/exports, environment variables, repository paths, and
stable storage keys internal unless explicitly changed. See M-032 in
`Milestones.md`.

### Settings foundation and presentation — M-033/M-034/M-045

Settings starts closed and is opened only through tray, mascot context menu,
or Single Instance activation. `Cancel` and `Close` discard unapplied edits and
close; `Restore Defaults` edits only the ViewModel until `Apply`. Product F10
and Settings Escape shortcuts remain unsupported.

Responsibilities remain separate: `SettingsWindowController` lifecycle and
commands; `SettingsViewModel` editable state; `SettingsBinding` synchronization;
`SettingsValidation` validation; `SettingsManager` persistence. UI controls do
no filesystem work and never read/display/modify `window-position.json`.

Common Settings code must not access HWND/DWM/Windows APIs. On Windows the
Player visibility controller implements `ISettingsPresentationHost`. While
Settings is visible, suppress only the separate Player-preview Camera and draw
one completely opaque Settings-only Player surface. Production Camera,
character animation, normalized 256x256 transfer, D3D12/readback/HRGN, and
DirectComposition mascot continue unchanged. Closing restores the same preview
Camera before application cloak, with no per-frame RenderTexture allocation.

Do not scale the production transfer to the Player, use Player preview as GPU
pipeline input, resize the Player, create a native Settings window, introduce a
new UI framework, or add Android presentation in this foundation.

Full presentation contract: `NativePlugin/docs/SettingsPresentationDesign.md`;
Settings foundation/history: M-033/M-034/M-045 in `Milestones.md`.

### Mascot context menu — M-035

The native mascot popup contains exactly `設定` and `終了`; hit behavior remains
the applied `SetWindowRgn`. The native procedure publishes only the small
monotonic shared command. Settings reuses the one Settings controller; Exit
routes through managed `RequestOrderlyQuit` and never destroys a window or
terminates the process directly.

Every created `HMENU` remains native-owned until `DestroyMenu`; cancellation,
destruction, and shutdown publish no command and leave live-owned count zero.
Right-button handling must not alter left-button drag or its completed-drag
generation. The normally nonactivating mascot may become temporary foreground
owner only for `TrackPopupMenuEx`; post `WM_NULL` and restore a valid previous
foreground window afterward. Full detail: M-035 in `Milestones.md`.

### System tray — M-036/M-046 recovery

A dedicated native thread owns the ASCII-class hidden tool window, one
GUID-identified `NOTIFYICONDATAW`, popup `HMENU`, and generated `HICON`. Use
`NIM_ADD`, `NIM_SETVERSION`/`NOTIFYICON_VERSION_4`, and exactly one logical
`NIM_DELETE` during idempotent cleanup.

Tray and context menu publish the same `OpenSettings = 1` and
`RequestExit = 2` semantics. A single left click does nothing; double-click
opens Settings. The tray owner never replaces or changes mascot HWND,
activation, region, drag, Z-order, or visible surfaces.

Create and retain the owner/message loop even after initial registration
failure. Register `TaskbarCreated` first and retry at constant 200 ms for at
most 75 attempts. Success or `TaskbarCreated` recovery converges to one GUID
icon and cancels pending retry; after exhaustion the message loop remains for
later `TaskbarCreated`. Suppress retry/re-registration after the shutdown
barrier. Keep separate bounded diagnostics for add/version/retry/recovery and
current registration. Managed startup observes only; it never creates another
tray thread or changes single-start ownership.

At shutdown stop an active popup, delete the tray icon, destroy the owner
window, and destroy the owned icon before mascot-native teardown; live-owned
menu/icon counts must reach zero.

Full detail: M-036 and M-046 “Tray Startup Recovery prerequisite” in
`Milestones.md`, plus `RepositoryStructureDesign.md` under “Tray startup
recovery prerequisite”.

### Production Player visibility — M-037

`UnityPlayerWindowVisibilityController` alone discovers/caches the current
process's exact `UnityWndClass` and owns all show/cloak state. Never use
`Process.MainWindowHandle`, `Process.CloseMainWindow`, localized titles,
mascot/tray HWND, or another controller's `ShowWindow`/DWM call. Revalidate a
cached HWND before use.

Normal runtime applies startup `DWMWA_CLOAK` from
`BeforeSplashScreen`; diagnostics remain visible unless explicitly testing
visibility. Ordinary hide must cloak, never `SW_HIDE`, because the visible
rendering surface must continue `WaitForEndOfFrame`, animation, Present,
readback, region publication, and command polling with
`Application.runInBackground = true`.

Tray/context/Single Instance show requests route through the visibility owner.
Ordinary title-bar close discards Settings and cloaks; it must not post final
`WM_CLOSE`, quit, destroy, or enter shutdown. After the shutdown barrier,
reject new show requests; cleanup first, then post one exact cached
`UnityWndClass` `WM_CLOSE`, then request `Application.Quit`. Preserve startup
flash measurement. Full detail: M-037 in `Milestones.md`.

### Single instance — M-038

Normal runtime alone uses `Local\DesktopMascotMinimal.SingleInstance.v1` and
an independent auto-reset named-event pending latch. A secondary stops before
settings, persistence, mascot, tray, or runtime initialization; it only signals
bounded `OpenSettings` and exits normally. Signals coalesce. The primary
consumes on the managed main thread and routes through the visibility owner,
preserving an already-open Settings window and unapplied edits.

Close acceptance at orderly-shutdown start. Late secondaries retry boundedly,
never promote themselves in the same launch, and exit normally. Release event
and mutex only during final idempotent cleanup. Do not use process-name/title
checks, process kill, `TerminateProcess`, `ExitProcess`, `Environment.Exit`,
`SendInput`, global hooks, or another visibility owner.

Full contract: `NativePlugin/docs/SingleInstanceDesign.md` (M-038 in
`Milestones.md`).

## Rendering, native ownership, and interaction

### Validated production pipeline and single normalization

Preserve:

```text
Unity Camera
→ Camera source RenderTexture
→ exactly one Camera-source Y-normalization Blit
→ normalized transfer RenderTexture
→ Unity native render-thread callback
→ native D3D12 copy/readback
→ alpha mask → HRGN → SetWindowRgn
→ DXGI composition swap chain
→ DirectComposition mascot HWND
```

The only orientation correction is:

```csharp
Graphics.Blit(cameraSourceTexture, normalizedTransferTexture,
    new Vector2(1, -1), new Vector2(0, 1));
```

Never invert downstream or in a shader, and never move this boundary without a
dedicated regression milestone. See `ProductionRuntimeFoundationDesign.md`
under “Single Camera-source normalization rule” and
`D3D12TextureTransferDesign.md`.

### Transparency, D3D12/COM, and HRGN

Transparency uses a premultiplied-alpha DirectComposition swap chain.
`SetWindowRgn` from the alpha mask defines pixel interaction. Do not replace it
with `WS_EX_TRANSPARENT`, primary `HTTRANSPARENT`, `UpdateLayeredWindow`,
`SetLayeredWindowAttributes`, `WS_EX_LAYERED` color-keying, or primary
`DwmExtendFrameIntoClientArea`.

Use RAII and owned `ComPtr` only for plugin-owned COM resources. Never
`AddRef`, `Release`, or place Unity-borrowed D3D12 objects in owning `ComPtr`
unless ownership is explicitly transferred. Submit GPU work only through Unity
render-thread callbacks. Preserve resource states, ordering, synchronization,
and existing device/swap-chain/composition ownership. Inspect the copied Unity
6000.3.20f1 headers; never guess an `IUnityGraphicsD3D12` API.

Every `HRGN` has one owner. Before successful `SetWindowRgn`, the caller owns
and must delete on failure/cancellation; after success Windows owns it and the
caller must not delete it. Release failed, superseded, and cancelled queued
regions; pending owned count reaches zero at shutdown. Direct counters and
restoration invariants outrank process-wide GDI samples. Never hide an
active-runtime region failure as shutdown cancellation.

### Native mascot drag — M-030

Drag belongs to the mascot HWND and begins from native left-button input inside
the active region. Record `GetCursorPos`/`GetWindowRect` origins, classify with
`SM_CXDRAG`/`SM_CYDRAG`, use and release `SetCapture` on every completion,
cancellation, destruction, failure, and shutdown path, and move only the
mascot via nonactivating/no-Z-order/no-resize `SetWindowPos`.

Do not use global hooks, `SendInput`, or move the Player. Persistence, snapping,
bounds recovery, resizing, and menus remain outside drag unless explicitly
scoped. Full contract: `NativeMascotWindowDragDesign.md`.

## Speech and Conversation

### Speech presentation — M-047.5/M-048

Shared Speech data is display-only. `SpeechPresentationController` owns one
current message, has no queue, coalesces same-ID Show, returns Busy for a
different ID, and uses matching generation plus first-close-wins for click and
timeout closure.

Speech remains an independent path: dedicated 170x64 RenderTexture, render
event 9, independent D3D12 copy/composition swap chain, and independent owned
nonactivating top-level Speech HWND. Never composite into mascot transfer,
reuse mascot alpha/HRGN, add Y inversion, parent it to a Character root, or
expose native/GPU types through shared presentation interfaces. Unity device,
queue, fence, factory, composition device, and source texture are borrowed and
must not be released.

Anchoring observes Character generation without ownership, discards stale
references, starts from actual mascot window/silhouette, minimally clamps
horizontally, and never persists position or changes activation/Z-order.
Close the Speech barrier and drain/release Speech before mascot composition
teardown. Cleanup is idempotent for never-shown/partial initialization; Speech
failure is isolated except `message-window-diagnostic` must fail.

Do not add queue, production greeting, pack, provider, AI, persistence,
variable timeout, animation, expression, audio, typing, or Text SE without an
explicit milestone. Full contract: `SpeechPresentationDesign.md`; validation
history: M-048 in `Milestones.md`.

### Conversation contracts — M-049 through M-053

Conversation domain values are immutable, platform-neutral managed data and
own no Character, Speech, Unity, native, filesystem, network, or GPU resource.
Logical IDs are stable lowercase ASCII supplied by callers; request IDs are
distinct caller-supplied correlation values. Never trim, normalize, lowercase,
or generate either. Expected invalid input uses stable non-localized validation
results and does not create a valid object or throw an expected exception.

`CharacterUtterance` is intent, not Speech acceptance; Speech Busy stays
presentation semantics. Domain source uses only basic `System` types and has no
Unity/platform/presentation/provider/path dependency, polling, worker, timer,
I/O, history, queue, retry, or cache. Trigger taxonomy remains stable logical
IDs, not an enum.

M-049 through M-053 do not authorize production wiring, Rule/Script Engine,
Conversation Pack, Speech adapter, provider/AI/networking, persistence,
scheduling, collection policy, typing, Text SE, animation, expression, or
audio. Full contracts:

- `ConversationDomainDesign.md`
- `ConversationEvaluationResultDesign.md`
- `LocalConversationEvaluatorDesign.md`
- `LocalConversationResponseEntryDesign.md`
- `SingleEntryLocalConversationEvaluatorDesign.md`
- `ProjectPrinciples.md`

## Runtime modes and orderly shutdown

Normal runtime is unlimited and exits only through managed orderly shutdown.
Diagnostics are explicit and must not auto-start or leak completion criteria
into production. Supported Development modes are:

```text
runtime-smoke
real-static-diagnostic
real-animated-diagnostic
drag-diagnostic
message-window-diagnostic
```

Select with `DESKTOP_MASCOT_MODE` or `--desktop-mascot-mode=<mode>`; no marker
means normal `runtime`.

Close acceptance barriers at shutdown start: Single Instance, tray/context
commands, Settings show, persistence writes/clears, Character selection/import,
Speech, and new render/region work. Cooperative picker/import work must finish
without abandonment. Drain Speech before mascot composition. Stop Camera,
readback, region publication, and native GPU presentation before final active/
retired runtime-character release.

Preserve idempotent core ordering:

1. stop new alpha-mask generations and region-apply posts;
2. cancel/drain queued and in-flight region work under HRGN ownership;
3. cancel drag and release capture;
4. restore initial window region/style;
5. stop composition and destroy native windows/resources in owned order;
6. after GPU presentation stops, release active/retired imported handles only
   through `RuntimeGltfInstance.Dispose()`;
7. restore `Camera.targetTexture` and `Application.runInBackground`;
8. finish managed/native cleanup without accessing destroyed state;
9. perform the one final Player `WM_CLOSE`, then `Application.Quit`.

Required safe-shutdown evidence includes continuous/region failure stage 0,
no outstanding readback, pending owned regions 0, restored region/style/Camera/
background state, no drag/capture ownership, Present/device-removed `S_OK`,
readback errors 0, active/retired imported handles 0, cleanup true, and fatal
failure stage 0. Treat cancellation as non-fatal only after the relevant
barrier and ownership/restoration proof; never suppress an active failure.

## Repository structure and coding boundary — M-046

Production managed code belongs under `Assets/_Project/Runtime`, focused
diagnostics under `Assets/_Project/Diagnostics`, and Editor-only code under
`Assets/_Project/Editor`. Within Runtime keep Core, Character, Persistence,
Settings, Presentation, and Platform responsibilities separate. Windows-only
HWND, visibility, tray, context-menu, single-instance, position, and native
interaction code belongs under `Runtime/Platform/Windows`; common contracts
and ViewModels remain outside it. `Runtime/Legacy` must not become a dependency
of new production work.

Move Unity assets with their `.meta` sidecars. Preserve GUIDs, namespaces, type
names, serialized field names, partial-class pairing, public APIs, diagnostic
mode names, and fixed paths unless explicitly changed. Do not add an `.asmdef`
as incidental cleanup.

Keep `NativePlugin/include`, `NativePlugin/src`, copied Unity headers, CMake,
native output contracts, and canonical `Tools/Development/*.ps1` paths stable.
`NativePlugin/docs` is documentation. `NativePlugin/out` is generated output
and retained evidence, never a source dependency. Full current layout and
no-move decisions: `RepositoryStructureDesign.md`.

Inspect relevant source/docs before editing, preserve unrelated user changes,
make incremental scoped changes, preserve validated paths, and build after
structural changes. Never begin unrelated features or rewrite historical
milestone results.

## Development handoff and mandatory milestone workflow

`DevelopmentHandoff.md` records current intent only. Update it only for
milestone completion, next-milestone selection, material blocker change,
deliberate handoff, or architecture/design-baseline change. Model selection
and thread lifecycle belong there.

Before implementation:

- inspect relevant source and current documentation;
- report the smallest implementation plan;
- identify affected validated invariants;
- define automated and manual validation requirements.

During implementation, make incremental scoped changes, preserve prior paths,
avoid unrelated features, and never claim success from compilation alone.

Before milestone completion, require as applicable: native and Unity
Development Player builds; `dumpbin` when exports change; `git diff --check`;
focused diagnostics, normal-runtime and affected regressions; orderly-shutdown
proof; and required user-performed visual/interaction verification.

Until the user reports required verification:

```text
Status: Automated Validation Passed
Visual verification: Pending
```

Only after the user actually reports success:

```text
Status: Completed
Visual verification: Passed
```

Never claim user-performed validation without the user's report.

## Logging, builds, generated artifacts, and warning

Do not delete logs during an active milestone/regression. Preserve the latest
failure, first passing log after its fix, and final logs cited by
`Milestones.md`. Do not reorganize `NativePlugin/out` until completion. Avoid
per-frame logging; prefer bounded failure-site/final summaries. Never log
private user content or future shared memory without explicit diagnostic need.

Canonical repository-root builds are:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Development\Build-Native.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Development\Build-UnityIncremental.ps1
```

The direct debug preset remains valid from `NativePlugin` in a Visual Studio
Developer PowerShell: `cmake --preset windows-debug`, then
`cmake --build --preset windows-debug`. Unity consumes
`Assets/Plugins/x86_64/DesktopMascotNative.dll`.

Source/docs are authoritative. DLL, PDB, ILK, Ninja output, Player builds, and
runtime persistence are generated; never edit them manually. Running Players
may lock the DLL; close them and compare Assets/Player timestamps or hashes
when diagnosing stale binaries. Do not invent build commands.

Known warning:

```text
TransparentWindowController.borderless
CS0414: assigned but its value is never used
```

Do not globally suppress it. Before removing/renaming a possibly serialized
field, inspect Scene/prefab serialization, diagnostic use, and migration need.
Do not block an unrelated completed milestone for this known non-functional
warning.

## Approval request language

When explicit approval is required for a terminal, Git, filesystem, build, or
other consequential operation, write the user-facing request in Japanese. It
must state:

- the operation;
- why it is needed;
- files or repository state that may change;
- related operations that will not be performed.

Keep technical identifiers unchanged where clearer. Japanese wording must not
weaken, bypass, or imply approval. This controls Codex-authored wording only,
not fixed client UI or global settings.

## Git authorization, safety, and final staged audit

Execution approval and repository authorization are separate. Terminal or
auto-review approval never authorizes commit, push, force-push, tag, rebase,
merge, history rewrite, branch change, or destructive cleanup. Commit and push
each require explicit task scope. Force push is prohibited unless separately
and explicitly authorized. Do not commit or tag automatically, rewrite
history, discard unrelated changes, or add generated output/logs unless that
specific artifact is intentionally tracked.

Inspect `git status` and relevant diffs before broad edits. At a milestone
completion/commit gate, and whenever untracked intended files, moves/deletes,
generated-artifact risk, multi-phase work, or release/push preparation require
complete review:

1. verify Git safety and the intended file set;
2. require a clean index before changing it;
3. if the index is not clean, inspect and stop for direction;
4. temporarily stage only the exact intended files;
5. run `git diff --cached --check`;
6. inspect staged name-status and the complete staged diff;
7. reject secrets, generated artifacts, and unintended files;
8. for audit-only work, immediately unstage exactly the files staged for the
   audit, leaving prior index state untouched;
9. commit only after the gate passes and explicit commit authorization exists.

Use `git diff --check` as the earlier tracked-worktree check. Approval requests
for temporary staging must follow the Japanese approval-language rule.

## Future direction and documentation map

Future shared domain/brain state, settings/persistence, synchronization,
renderer, and platform integration remain separate. Keep screen position,
native handles, and capture platform-local; use versioned persistence through
dedicated services. Do not couple shared state to Win32/D3D12/DirectComposition
or Android lifecycle, add Android dependencies, or implement network sync
before an explicit milestone. These principles do not authorize work. See
`NativePlugin/docs/ProjectPrinciples.md`.

Primary current detail/history documents:

- `NativePlugin/docs/Milestones.md`
- `NativePlugin/docs/README.md`
- `NativePlugin/docs/ProductionRuntimeFoundationDesign.md`
- `NativePlugin/docs/D3D12TextureTransferDesign.md`
- `NativePlugin/docs/NativeMascotWindowDragDesign.md`
- `NativePlugin/docs/WindowPositionPersistenceDesign.md`
- `NativePlugin/docs/SingleInstanceDesign.md`
- `NativePlugin/docs/CharacterAssetFoundationDesign.md`
- `NativePlugin/docs/RuntimeVrmImportFoundationDesign.md`
- `NativePlugin/docs/RuntimeVrmSelectionUiDesign.md`
- `NativePlugin/docs/RuntimeCharacterPersistenceDesign.md`
- `NativePlugin/docs/RuntimeBundledCharacterRestorationDesign.md`
- `NativePlugin/docs/RuntimeRetiredCharacterDisposalDesign.md`
- `NativePlugin/docs/SettingsPresentationDesign.md`
- `NativePlugin/docs/RepositoryStructureDesign.md`
- `NativePlugin/docs/SpeechPresentationDesign.md`
- `NativePlugin/docs/ConversationDomainDesign.md`
- `NativePlugin/docs/ConversationEvaluationResultDesign.md`
- `NativePlugin/docs/LocalConversationEvaluatorDesign.md`
- `NativePlugin/docs/LocalConversationResponseEntryDesign.md`
- `NativePlugin/docs/SingleEntryLocalConversationEvaluatorDesign.md`
- `NativePlugin/docs/ProjectPrinciples.md`
