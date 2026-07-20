# Composition Click-Through Diagnostics

## Scope

This diagnostic toggles whole-window click-through for the 64 by 64
DirectComposition window. It does not perform per-pixel alpha hit testing.
All Win32 window access remains on the composition UI thread.

The managed request thread stores atomic request data and posts
`WM_APP + 0x46`. The composition UI thread applies the diagnostic extended
style, calls `SetWindowPos` with
`SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE |
SWP_FRAMECHANGED`, and verifies the applied extended style.

The primary mechanism is `WS_EX_LAYERED | WS_EX_TRANSPARENT`. Enable adds
both bits to the captured initial extended style. Disable restores the exact
captured style, including any bits which predated the diagnostic. No layered
window attributes, color key, or global alpha are set.

`WM_NCHITTEST` returns `HTTRANSPARENT` only while click-through is enabled.
It remains supplemental behavior and is not treated as the cross-process
click-through mechanism.

Before start, the UI thread reads the window class style. `CS_OWNDC` or
`CS_CLASSDC` fails the diagnostic before `WS_EX_LAYERED` is applied.

The UI thread also captures the initial `GetWindowRect` result and verifies
the same position and size after each style change. A mismatch fails at
`ExtendedStyleVerificationFailed`.

The managed diagnostic saves `Application.runInBackground`, forces it to
`true` before the coroutine starts, and restores the saved value only after
composition shutdown and final logging. Visual phases are 3 seconds normal,
8 seconds enabled, and 8 seconds restored. Continuous composition is bounded
to 660 Presents at 30 FPS with a 30-second native and managed timeout.

## State values

| Value | State |
|---:|---|
| 0 | NotStarted |
| 1 | Ready |
| 2 | EnableRequested |
| 3 | EnableMessagePosted |
| 4 | EnableMessageReceived |
| 5 | Enabled |
| 6 | DisableRequested |
| 7 | DisableMessagePosted |
| 8 | DisableMessageReceived |
| 9 | Disabled |
| 10 | Completed |
| 11 | ShutdownRequested |
| 12 | Stopped |
| 13 | Failed |

## Failure-stage values

| Value | Failure stage |
|---:|---|
| 0 | None |
| 1 | CompositionNotReady |
| 2 | WindowUnavailable |
| 3 | ShutdownAlreadyRequested |
| 4 | RequestAlreadyPending |
| 5 | InvalidEnabledValue |
| 6 | PostMessageFailed |
| 7 | MessageSequenceMismatch |
| 8 | GetInitialExtendedStyleFailed |
| 9 | SetExtendedStyleFailed |
| 10 | GetAppliedExtendedStyleFailed |
| 11 | ExtendedStyleVerificationFailed |
| 12 | SetWindowPosFrameChangedFailed |
| 13 | EnableTimeout |
| 14 | DisableTimeout |
| 15 | UnexpectedRequestCount |
| 16 | UnexpectedAppliedCount |
| 17 | ShutdownFailed |
| 18 | LayeredWindowClassStyleUnsupported |
| 19 | LayeredStyleApplyFailed |
| 20 | LayeredTransparentStyleVerificationFailed |
| 21 | InitialExtendedStyleRestoreFailed |
| 22 | RunInBackgroundVerificationFailed |

The exported enabled argument is normalized to zero or one, so
`InvalidEnabledValue` is reserved and is not normally produced.
Precondition failures and `PostMessageW` failure both increment the rejected
counter. A posted request increments the matching enable or disable request
counter; a post failure does not increment the applied counter.

Initial `WS_EX_LAYERED` and `WS_EX_TRANSPARENT` states are captured. Disable
always restores the complete initial extended style rather than clearing
either bit unconditionally.
