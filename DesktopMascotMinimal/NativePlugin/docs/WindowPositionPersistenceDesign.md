# Window Position Persistence and Screen-Bounds Recovery

## Scope

M-031 persists only the top-left desktop position of the native
DirectComposition mascot window. It restores that position at the next normal
runtime start and recovers safely when the saved position is no longer usable.
It does not add settings UI, resizing, snapping, docking, profiles, animation
state, Android integration, or shared mascot memory.

The legacy `MascotSettings`, `SettingsStore`, `MascotApplication`, and
`Win32Window` classes target an earlier Unity Player-window prototype. Their
unversioned, non-atomic `settings.json` path is not used by the production
DirectComposition mascot window.

## Responsibility boundaries

- `WindowPositionRecord` defines a platform-local integer desktop position and
  load status.
- `WindowPositionStore` validates and atomically persists schema v1.
- `WindowsScreenBoundsService` enumerates monitor work areas and resolves
  visibility.
- `NativeMascotWindowPositionBridge` configures native startup placement and
  reads additive native drag/position status.
- `WindowPositionPersistence` performs the small amount of runtime
  orchestration: load before native window creation, observe completed drags,
  suppress duplicate writes, and perform the final orderly-shutdown check.

The native WndProc does not perform file I/O or call managed code. The screen
policy and JSON record do not own `HWND`, `HRGN`, D3D12, or COM state.

## Coordinate system

The stored `x` and `y` are the native mascot `HWND` top-left coordinates used
by `GetWindowRect`, `CreateWindowExW`, and `SetWindowPos`. They are Windows
desktop coordinates in the process/thread DPI-awareness context, represented
as signed integer physical pixels by the current Unity Windows Player.
Negative values are valid for monitors above or left of the primary monitor.

Managed work-area enumeration uses `EnumDisplayMonitors` and
`GetMonitorInfoW` in the same process, so it does not mix Unity texture-space,
logical UI coordinates, or a separately virtualized process with the native
window coordinates.

## Persisted schema and storage

The production record is:

```json
{
  "schemaVersion": 1,
  "x": 100,
  "y": 100
}
```

The path is:

```text
%LOCALAPPDATA%\DesktopMascotMinimal\window-position.json
```

`DesktopMascotMinimal` is the current Player product name and is made explicit
instead of depending on the Player working directory or the current
`DefaultCompany` placeholder. The record never contains native handles,
capture state, regions, graphics resources, or runtime pointers.

All three fields must occur exactly once and parse as 32-bit integers.
Coordinates outside `[-1,000,000, +1,000,000]` are rejected. Missing files,
malformed JSON, invalid numerics, read failures, and unknown schema versions
select a safe startup position without crashing. An unknown future schema is
not interpreted as schema v1. A corrupt or unsupported record is logged before
any later user drag can replace it.

## Atomic-write strategy

Writes use a unique temporary file in the final file's directory:

1. create and serialize the complete UTF-8 record;
2. flush the writer and file buffers, then close the file;
3. use `File.Replace` when a final record exists, or `File.Move` for the first
   record;
4. remove an uncommitted temporary file on failure.

A failed commit therefore leaves either the previous final record or no final
record. Failures are bounded warnings and do not make the rendering runtime
fatal. The last persisted coordinate is cached, so a completed drag that ends
at the unchanged position does not write again.

## Drag-completion signal

The production native mouse path publishes a monotonically increasing
completed-drag generation only when `WM_LBUTTONUP` ends an operation that
crossed the system drag threshold. The native UI thread snapshots the final
`GetWindowRect` position before publishing the generation.

Simple clicks, `WM_CANCELMODE`, capture loss, shutdown cancellation, failed
movement, and per-move `WM_MOUSEMOVE` messages do not publish a completed
generation. Managed runtime polling reads the generation and current position
only after dragging and capture are both false. It performs at most one
logical save for a newly observed generation.

The additive native APIs are:

```text
DMN_SetCompositionInitialPosition(int x, int y)
DMN_GetNativeMascotCompletedDragGeneration()
DMN_TryGetNativeMascotWindowPosition(int* x, int* y)
```

All returned positions and generations are value snapshots. No native object
ownership is transferred.

## Minimum-visible policy and recovery

At least `48 x 48` pixels of the 256 x 256 mascot window must intersect one
current monitor work area. The policy is bounded by the window dimensions if a
future smaller window is validated.

- Acceptance and recovery are intentionally different operations. A saved
  position meeting the minimum-visible policy on any work area is accepted and
  preserved exactly, including valid negative and deliberately partial edge
  coordinates.
- A saved position that does not meet the policy enters recovery. The nearest
  current work area is selected from the saved top-left coordinate, and the
  position is clamped so the entire mascot window is inside that work area
  whenever the work area can contain it.
- If the mascot window is wider or taller than the selected work area, the
  oversized axis uses the work-area top-left. This deterministic fallback
  keeps a usable portion visible and avoids constructing an inverted clamp
  range.
- If no saved position is usable, the primary work area and the production
  default `(100,100)` use the same visually safe recovery clamp.
- If monitor enumeration itself is unavailable, the existing default remains
  the non-fatal fallback.

Work areas, rather than the full virtual-screen rectangle, account for taskbar
and reserved desktop bounds. Already-valid deliberate edge placement is not
forced on-screen. Full-window clamping applies only after a position has
failed acceptance and entered recovery; it does not inspect alpha masks,
animated bounds, `HRGN`, D3D12 readback, or composition state.

## Runtime and diagnostic isolation

- `runtime`: reads and writes the production `%LOCALAPPDATA%` record.
- `runtime-smoke`: persistence is disabled.
- `drag-diagnostic`: uses a process-specific path below
  `Application.temporaryCachePath`; it never mutates the production record.
- static and animated diagnostic modes: persistence is disabled.

The drag diagnostic runs focused missing/valid/negative/off-screen/partial,
corrupt/schema, atomic-failure, drag-generation, and restart-simulation tests
against temporary records. Its deterministic native move publishes one
diagnostic completed generation through the same bounded signal, while the
diagnostic later restores its initial native position.

## Startup ordering

1. `DesktopMascotRuntimeBootstrap.SelectMode` selects the persistence policy
   during `BeforeSceneLoad`.
2. Normal runtime loads and validates schema v1.
3. Current monitor work areas resolve the accepted/default/recovered position.
4. `DMN_SetCompositionInitialPosition` records the resolved position before
   the composition thread starts.
5. The native UI thread passes that position directly to `CreateWindowExW`.
6. DirectComposition is initialized and the window is shown with
   `SW_SHOWNOACTIVATE`.

The correction occurs before the first show and therefore does not require a
visible post-start placement jump or focus activation.

## Shutdown ordering

The M-030 shutdown barrier and HRGN ownership order remain authoritative:

1. managed runtime stops new mask/readback publication;
2. native dragging is disabled, an active operation is cancelled, and capture
   is allowed to release;
3. only a newly completed generation with dragging/capture both false is
   considered for the final save;
4. existing region work drains or cancels and region/style restoration
   completes;
5. composition shutdown destroys the native window;
6. managed Camera and `runInBackground` state are restored.

The position is never queried after native composition teardown and an
incomplete drag is never saved. Persistence failure remains independent from
HRGN, Present, device, and cleanup failure stages.

## Validation

Focused automated cases cover:

- missing record and default selection;
- exact valid restoration;
- valid negative multi-monitor-style coordinates;
- accepted 48 x 48 partially visible edge placement without coordinate change;
- far off-screen recovery with the full window inside the selected work area;
- removed-monitor-style recovery to the nearest current work area;
- deterministic oversized-window recovery without an invalid clamp range;
- corrupt JSON and unsupported schema;
- simulated atomic commit failure preserving the prior record;
- one logical write for one new completed generation, with no write for a
  cancelled/active or duplicate generation;
- save/load restart simulation;
- the existing drag diagnostic and orderly cleanup invariants.

Regression validation must also cover runtime-smoke, real animated diagnostic,
normal runtime without an auto-exit timer, native/Unity builds, exports, DLL
hash equality, and orderly shutdown.

Manual completion requires a real normal-runtime drag, orderly quit and
restart restoration, usable edge placement, off-screen recovery, and (when
available) second-monitor restoration. Focus avoidance, transparent
click-through, opaque dragging, startup stability, and absence of a residual
native window must remain intact.
