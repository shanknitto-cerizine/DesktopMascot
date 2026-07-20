# Fixed Alpha Pattern Pixel Hit-Test Diagnostics

## Scope

This diagnostic connects the already-published 64 x 64 top-left-origin alpha
snapshot to the DirectComposition window's `WM_NCHITTEST` handler. It tests
Win32 method A only:

```text
diagnostic extended style = initial extended style | WS_EX_LAYERED
WS_EX_TRANSPARENT         = absent
alpha < 128               = HTTRANSPARENT
alpha >= 128              = HTCLIENT
```

It does not implement method B, moving mascot input, mouse capture, dragging,
click handlers, topmost behavior, focus changes, window geometry changes,
swap-chain changes, or DirectComposition visual-tree changes.

## Recorded Method A result

The user completed the cross-process visual test with Notepad behind the
64 x 64 composition window. Method A failed:

```text
Method A: Failed

WS_EX_LAYERED: True
WS_EX_TRANSPARENT: False

Top-left alpha 0:
Native expected result = HTTRANSPARENT
Background Notepad received click = False

Top-right alpha 64:
Native expected result = HTTRANSPARENT
Background Notepad received click = False

Bottom-left alpha 128:
Native expected result = HTCLIENT
Background Notepad received click = False

Bottom-right alpha 255:
Native expected result = HTCLIENT
Background Notepad received click = False

Center alpha 192:
Native expected result = HTCLIENT
Background Notepad received click = False
```

The pixel-alpha classification worked, but `HTTRANSPARENT` did not provide
cross-process click-through. Method A is not a successful click-through
solution. `WS_EX_TRANSPARENT` must not be added permanently as a workaround.
This is consistent with the Win32 `WM_NCHITTEST` contract: `HTTRANSPARENT`
continues the search through underlying windows belonging to the same thread.

## Fixed pattern

| Region | Representative coordinate | Alpha | Result |
|---|---:|---:|---|
| Top-left | `(8,8)` | 0 | `HTTRANSPARENT` |
| Top-right | `(55,8)` | 64 | `HTTRANSPARENT` |
| Bottom-left | `(8,55)` | 128 | `HTCLIENT` |
| Bottom-right | `(55,55)` | 255 | `HTCLIENT` |
| Center 16 x 16 | `(32,32)` | 192 | `HTCLIENT` |

The center is rendered after the quadrants. The real composition alpha remains
unchanged. Unity's main Player window displays a separate 8x guide with region
labels during the 30-second visual test.

## Coordinates and snapshot synchronization

`WM_NCHITTEST` extracts signed screen coordinates with `GET_X_LPARAM` and
`GET_Y_LPARAM`, then calls `ScreenToClient` on the Composition UI thread. Client
coordinates and the published mask both use top-left origin.

`TryReadCompositionAlphaMaskPixel` holds the publication mutex only while it
checks immutable metadata and reads one alpha byte plus threshold/generation.
There is no vector allocation, 4096-byte copy, managed call, GPU readback, GPU
wait, sleep, or file I/O in the hit-test path.

If no published snapshot is available, the handler increments
`mask-unavailable count` and returns `HTCLIENT`. Coordinates outside 64 x 64
increment `outside-client count` and return `HTNOWHERE`. A `ScreenToClient`
failure records its Win32 error and failure stage before falling back to normal
window processing.

## Window style policy

The Composition UI thread reads and saves the complete initial extended style.
It also reads the window class style and rejects `CS_OWNDC` or `CS_CLASSDC`.
An initially present `WS_EX_TRANSPARENT` is rejected without removing it.

Application uses:

```cpp
SetWindowLongPtrW(
    window,
    GWL_EXSTYLE,
    initialExtendedStyle | WS_EX_LAYERED);
SetWindowPos(
    window,
    nullptr,
    0, 0, 0, 0,
    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER |
    SWP_NOACTIVATE | SWP_FRAMECHANGED);
```

Stop restores `initialExtendedStyle` exactly and verifies it by reading the
style again. All style and class APIs execute only in handlers reached through
`PostMessageW`.

## State values

```text
0 NotStarted
1 WaitingForComposition
2 WaitingForMask
3 ApplyingWindowStyle
4 ReadyForVisualTest
5 VisualTestRunning
6 Completed
7 RestoreRequested
8 Stopped
9 Failed
```

## Result values

```text
0 None
1 Transparent
2 Opaque
3 OutsideClient
4 MaskUnavailable
5 CoordinateConversionFailed
```

## Failure-stage values

```text
 0 None                              9 VerifyLayeredStyleFailed
 1 CompositionNotReady              10 ScreenToClientFailed
 2 WindowUnavailable                11 SnapshotReadFailed
 3 AlphaMaskUnavailable             12 UnexpectedAlphaValue
 4 InvalidThreshold                 13 UnexpectedHitTestResult
 5 ExistingTransparentStyleUnsupported
 6 LayeredWindowClassStyleUnsupported
 7 GetInitialExtendedStyleFailed    14 RestoreInitialStyleFailed
 8 ApplyLayeredStyleFailed          15 ContinuousPresentFailed
                                    16 DiagnosticsTimeout
                                    17 ShutdownFailed
```

## Counters

- total hit tests
- transparent results
- opaque results
- outside-client results
- mask-unavailable results
- `ScreenToClient` failures
- last client x/y
- last alpha
- last result
- last published generation

No debug string is emitted per hit test.

## Visual procedure

1. Place Notepad behind the 64 x 64 window at the logged screen position.
2. Enter several lines so caret movement is visible.
3. During the 30-second guide, click top-left and top-right several times.
   The caret should move.
4. Click bottom-left, bottom-right, and center. The caret should not move.
5. Confirm Present continues when Unity is inactive and there is no black
   frame, major flicker, window movement, resize, or focus acquisition.

Both transparent and opaque counters must be nonzero for complete coverage.
Missing clicks are logged as `Visual verification incomplete`, not a code
failure. Whether method A crosses the process boundary is a manual result; the
implementation must not silently enable `WS_EX_TRANSPARENT`.

## Shutdown

The managed controller marks the visual test completed, posts the restore
request, waits for exact style restoration, stops alpha transport, allows the
bounded continuous Present diagnostic to complete or safely stops it, requests
Composition shutdown, observes UI-thread termination, restores
`Application.runInBackground`, and exits the Player. Composition teardown also
restores the style defensively on the UI thread.

## Timeout-safe development workflow

- `Stop-DevelopmentPlayer.ps1` targets only the configured Player path,
  requests `CloseMainWindow`, waits up to 15 seconds, and never force-kills by
  default.
- `Deploy-NativeToPlayer.ps1` opens the destination DLL briefly with
  `FileShare.None` before copying, without changing file content.
- `Build-UnityIncremental.ps1` keeps an exclusive
  `Temp/DevelopmentBuild.lock`, reclaims only stale locks, writes
  `Temp/DevelopmentBuild.status.json`, and releases the lock in `finally`.
- `Get-UnityIncrementalBuildStatus.ps1` reports
  `NotStarted`, `Running`, `Succeeded`, `Failed`, or `Unknown` from the lock,
  status, and dedicated log.
- `Run-DevelopmentPlayer.ps1` refuses a second instance of the configured
  Player.

If an external command times out, do not launch another Unity build or kill
Unity. Query the status script and inspect the dedicated log and artifact
timestamps until success or failure is explicit.

## Current limitations

- Fixed 64 x 64 diagnostic pattern and threshold 128 only.
- No time-varying snapshot validation.
- No production mascot or drag/input integration.
- Cross-process `HTTRANSPARENT` was empirically confirmed not to work for the
  separate Notepad process in this configuration.
- Method B is documented separately in
  `CompositionWindowRegionDesign.md`.
