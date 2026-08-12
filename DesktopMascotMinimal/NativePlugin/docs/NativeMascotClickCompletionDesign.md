# Native Mascot Click Completion Signal Foundation

## Scope

M-055 adds one additive native snapshot: a monotonic completed-click generation.
It is a signal foundation only. It does not create a production observer or
connect a click to Conversation, Character, Speech, persistence, Unity input,
raycasts, a queue, callback, event bus, or request ID.

## Native ownership and publication

`NativeMascotWindowDrag.cpp` remains the sole native pointer-operation owner.
It owns `std::atomic<std::uint64_t> g_completedClickGeneration`; no managed
code owns a native handle, capture, or interaction resource.

`EndPointerOperation` has an internal-only completion classification:

- `None`: no eligible pending press ended;
- `NonDragClick`: an eligible press ended without crossing the existing drag
  threshold;
- `Drag`: an eligible press ended after crossing that threshold.

Only the authoritative normal `WM_LBUTTONUP` path publishes a click. It adds
exactly one to the completed-click generation for `NonDragClick`. A drag uses
the unchanged completed-drag publication path and does not publish a click.
Cancellation, capture loss, shutdown, input/capture/movement failure, stray
release, and a press outside the applied region publish neither a click nor a
synthetic completion.

Existing capture release, threshold classification, movement flags, final
drag-position snapshot, and completed-drag generation behavior are unchanged.
The click generation is reset to zero only by the existing native interaction
lifecycle reset before a new lifecycle begins; enabling, disabling, polling,
character changes, and any later consumer action never reset it.

## Snapshot and managed boundary

`GetNativeMascotCompletedClickGeneration()` and C export
`DMN_GetNativeMascotCompletedClickGeneration()` return a non-destructive
atomic snapshot. They do not consume, reset, queue, callback, or transfer
ownership.

Windows-only `NativeMascotClickCompletionBridge` imports only that export and
returns its value. It stores no observed generation, pending debt, native
handle, or resource and has no Conversation, Character, Speech, or
Persistence dependency.

## Diagnostic-only multiple-click model

The `drag-diagnostic` focused pure diagnostic verifies future-consumer math
without adding a production consumer:

```text
delta = unchecked(current - observed)
```

The diagnostic model retains all delta as pending debt, drains at most 64 per
frame, retains the remainder, never coalesces or drops completion, detects
pending-count overflow as failure, and relies on unsigned modulo arithmetic
for native `uint64` wrap. This diagnostic model is not a runtime owner or
observer cursor.

## Manual verification hold

`DESKTOP_MASCOT_CLICK_MANUAL_HOLD=hold` extends only the existing
`drag-diagnostic` Development Player path. After the existing runtime, region,
context-menu, and tray readiness checks, it logs initial completed-click and
completed-drag generations plus dragging, capture-owned, and native drag
failure snapshots. It then remains alive and logs each change to those values.

The hold path does not call the deterministic drag move/completion diagnostic
or `RequestOrderlyQuit`; it waits for physical input and subsequent explicit
orderly exit through the established diagnostic/tray cleanup path. With the
environment value absent or different, `drag-diagnostic` retains its existing
three-second deterministic completion and auto-quit behavior. This is a
diagnostic-only environment control and does not affect `runtime`.

## Validation and manual verification

Automated evidence includes native/header/export parity, focused delta/debt
tests, native and Unity builds, export inspection, existing drag diagnostics,
position-persistence and context-menu regressions, dependency/prohibited-
reference audit, duplicate-GUID/meta audit, `git diff --check`, and required
runtime/orderly-shutdown regressions.

Automated Validation: Passed

Physical/Manual Verification: Passed

The required physical checks passed: one opaque-region click incremented only
the click generation; one physical drag incremented only completed-drag and
preserved position persistence; and multiple transparent-region clicks passed
through without capture or either completion publication. Each check completed
through the established orderly tray-exit path with native drag failure stage
zero and no reported visual or interaction anomaly.

Remaining Manual Verification: None

Final Repository/Staged Audit: Passed

M-055 implementation and required validation complete.

Commit: `d1222948e55a3279b2552dfae7e81c00656ca440`

Commit message: `M-055 establish native mascot click completion signal`

Push / Remote Protection: Completed
