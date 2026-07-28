# Native Mascot Window Drag Design

## Scope

M-030 adds left-button dragging to the existing DirectComposition mascot
window. The native mascot `HWND` owns the interaction because it already owns
the Win32 message loop, the active `SetWindowRgn` shape, and the window whose
position must change. Managed per-frame cursor polling, global mouse hooks,
`SendInput`, and movement of the Unity Player window are not used.

M-030 itself did not add position persistence, resizing, settings UI, context
menus, animation switching, or click forwarding to Unity. M-031 later adds
position persistence as a separate service without moving file I/O or
screen-bounds policy into this drag layer.

## Interaction with the active window region

The composition window continues to use `SetWindowRgn`. Pixels outside the
active region do not belong to the native window and continue to click through
to the application behind it. On `WM_LBUTTONDOWN`, the drag handler also reads
the current region with `GetWindowRgn` and checks the client point with
`PtInRegion`; a pointer operation begins only when this explicit check passes,
the runtime drag feature is enabled, and shutdown has not started.

No `WS_EX_TRANSPARENT`, `HTTRANSPARENT`, layered-window API, or global input
hook is involved.

## Drag-state lifecycle

The state is local to the native composition-window subsystem and records:

- whether a left-button operation is pending;
- whether it crossed the system drag threshold;
- cursor screen coordinates at the start and at the latest applied move;
- the mascot window rectangle at the start;
- whether shutdown or cancellation is in progress;
- diagnostic counters and the last applied window coordinates.

On an eligible `WM_LBUTTONDOWN`, `GetCursorPos` and `GetWindowRect` establish
the immutable drag origins. The handler then calls `SetCapture`. A simple click
remains a pending pointer operation and does not move the window unless the
cursor delta reaches `GetSystemMetrics(SM_CXDRAG)` or
`GetSystemMetrics(SM_CYDRAG)`.

After the threshold is crossed, each `WM_MOUSEMOVE` reads screen coordinates
with `GetCursorPos`, calculates the delta from the original cursor position,
and applies that delta to the original window position. Repeated coordinates
are ignored.

The operation ends on `WM_LBUTTONUP`, `WM_CANCELMODE`, `WM_CAPTURECHANGED`,
window destruction, runtime shutdown, or composition failure. Capture is
released only when the mascot window owns it. State clearing and capture
cleanup are idempotent, and shutdown disables new operations before the window
is destroyed.

## Window movement, Z-order, and activation

Only the native mascot window is moved. `SetWindowPos` uses:

- `SWP_NOSIZE` to retain width and height;
- `SWP_NOZORDER` to retain the existing Z-order contract;
- `SWP_NOACTIVATE` to avoid activating the mascot or Unity Player.

The swap chain, DirectComposition device, visual tree, content, window region,
and presentation runtime are not recreated. `WM_MOUSEACTIVATE` returns
`MA_NOACTIVATE` while native dragging is enabled. The existing non-activating
window style remains otherwise unchanged.

## Mouse capture ownership

The native UI thread acquires capture after an eligible opaque-region press.
This allows the drag to continue when the cursor leaves the animated
silhouette. Button release or any cancellation path relinquishes capture. No
managed object owns capture, and no queued polling operation retains the
`HWND` after shutdown.

## Managed/runtime integration

The production runtime enables native drag handling only after the native
presentation and animated alpha-region publication path have started. It
disables dragging before normal native shutdown and also does so on emergency
cleanup. Normal runtime remains unlimited; enabling interaction adds no
completion condition or per-frame log.

The startup log identifies the selected runtime mode. Normal shutdown adds
elapsed time, final Present count, and final region-publication count as
summaries without changing shutdown criteria.

## Diagnostic strategy

The explicit Development Player mode is `drag-diagnostic`, selected with
either `DESKTOP_MASCOT_MODE=drag-diagnostic` or
`--desktop-mascot-mode=drag-diagnostic`. It is independent of normal runtime.

The diagnostic posts private messages to the native composition window. On
its UI thread it records the initial rectangle, neighboring Z-order windows,
foreground window, Present count, published region generation, and initial
DirectComposition commit count. It then performs one deterministic
`SetWindowPos` move by `(48, 32)` with the production movement flags. It does
not synthesize input or move the global cursor.

After three seconds the diagnostic checks that:

- the requested X/Y movement occurred and size did not change;
- an active window region remains applied;
- Present and alpha-region publication continued;
- the composition tree was not restarted;
- Z-order and foreground activation did not change;
- dragging ended and capture is not owned;
- the initial window position was restored.

The managed diagnostic logs the additive native state/counter API and requests
the existing managed orderly shutdown. The normal cleanup log then verifies
readback, HRGN ownership, window region/style restoration, Present/device
status, Camera target restoration, and `runInBackground` restoration.

All diagnostic state exports are atomic snapshots. Returned integers and
booleans carry no ownership. The native mascot `HWND` remains native-owned and
is exposed only as an availability result, not as an owning handle.

## Limitations and deferred work

- A click on the mascot does not yet invoke a Unity reaction.
- Position persistence and screen-bounds recovery are defined separately by
  `WindowPositionPersistenceDesign.md`; this drag layer exposes only its
  completed-generation and current-position snapshots.
- There is no resizing, snapping, multi-monitor policy, settings UI, context
  menu, or animation-selection behavior.
- The automated move validates window invariants without synthesizing the
  physical mouse/capture gesture. Opaque/transparent hit behavior, smooth
  physical dragging, capture outside the silhouette, focus retention, and
  visual stability still require the documented manual verification.
