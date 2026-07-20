# Fixed Alpha Pattern Window Region Diagnostics

## Scope

This is Method B following the recorded Method A failure. It converts the
published, fixed 64 x 64 top-left-origin alpha snapshot into an `HRGN` and
applies it to the composition window on the Composition UI thread.

```text
threshold = 128
alpha < 128  = outside the window region
alpha >= 128 = inside the window region
```

It does not implement time-varying region updates, production mascot input,
dragging, mouse capture, hooks, synthetic input, or an additional fallback.

## Fixed pattern

| Region | Coordinate | Alpha | `PtInRegion` | Expected Notepad click |
|---|---:|---:|---:|---:|
| Top-left | `(8,8)` | 0 | false | true |
| Top-right | `(55,8)` | 64 | false | true |
| Bottom-left | `(8,55)` | 128 | true | false |
| Bottom-right | `(55,55)` | 255 | true | false |
| Center | `(32,32)` | 192 | true | false |

The actual click target is the 64 x 64 composition window at screen position
`(100,100)`. The Unity Player's 8x display is only a guide.

## Region generation

The UI thread copies one immutable 4096-byte published snapshot under the
alpha-publication mutex. It scans each row and detects contiguous
`alpha >= 128` runs. Each run becomes:

```cpp
CreateRectRgn(runStartX, y, runEndXExclusive, y + 1);
```

Runs are combined into the final region using `CombineRgn(..., RGN_OR)`.
The implementation does not create one region per pixel. The diagnostic
reports scanline-run count, covered pixels, excluded pixels, region type, and
published generation.

## UI-thread ownership

The following execute only in composition-window messages posted with
`PostMessageW`:

- `CreateRectRgn`
- `CombineRgn`
- `GetWindowRgn`
- `SetWindowRgn`
- `PtInRegion`
- `SetWindowPos`
- `RedrawWindow`
- extended-style reads, changes, and restoration

While the Method B region is active, `WM_NCHITTEST` returns `HTCLIENT` for
points that Windows delivers to the window. Region-excluded points do not
target the composition window. Method A's per-pixel handler is not active.

## `SetWindowRgn` ownership

After `SetWindowRgn(window, region, TRUE)` succeeds, Windows owns `region` and
the plugin does not call `DeleteObject` on it. If the call fails, the plugin
deletes the region itself.

Before applying the diagnostic region, the UI thread captures and classifies
the initial region as `ERROR`, `NULLREGION`, `SIMPLEREGION`, or
`COMPLEXREGION`. Per the Win32 contract, `ERROR` means either no window region
or an error. The diagnostic clears LastError before the call and treats
`ERROR` with no LastError as the normal "no initial region" state. A non-null
initial region is retained as a plugin-owned copy.

At stop:

- no initial region: `SetWindowRgn(window, nullptr, TRUE)`
- existing initial region: pass a duplicate of the saved copy to
  `SetWindowRgn`

The duplicate transfers to Windows. The retained copy is used to verify the
restoration with `GetWindowRgn` and `EqualRgn`, then deleted.

## Extended style

The diagnostic requires that the initial style does not contain
`WS_EX_TRANSPARENT`. It records the exact initial style and temporarily uses:

```text
diagnostic style = initial style | WS_EX_LAYERED
```

It never uses `SetLayeredWindowAttributes`, `UpdateLayeredWindow`, color key,
or global alpha. Stop restores and verifies the exact initial extended style.

## State

```text
0 NotStarted
1 WaitingForComposition
2 WaitingForMask
3 ApplyRequested
4 VisualTestRunning
5 Completed
6 RestoreRequested
7 Stopped
8 Failed
```

Failure stages are defined in
`CompositionWindowRegionDiagnostics.h`. Diagnostics include region applied,
region types, scanline rectangles, covered/excluded pixels, representative
`PtInRegion` values, request state, last result/error, generation, region
restoration, and style restoration.

## Visual verification

During the 30-second test:

1. Place Notepad behind the actual 64 x 64 window.
2. Click the top-left and top-right excluded areas. Notepad should receive the
   clicks.
3. Click bottom-left, bottom-right, and center. Notepad should not receive the
   clicks.
4. Record clipping, edge flicker, and any effect on DirectComposition output.

Native `PtInRegion` results validate geometry but cannot prove delivery to a
different process. Cross-process delivery remains a user visual result.

## Shutdown

The controller restores region and style, logs the final diagnostic state,
allows bounded continuous Present to complete or stop, requests composition
shutdown, observes the stopped state, restores `runInBackground`, waits at
least one frame for final logging, and only then calls `Application.Quit`.

If excluded pixels still do not reach Notepad, this stage stops without
implementing another approach.

## First automated run

The first Classification B Player run completed with:

```text
Region applied: True
Region type: COMPLEXREGION
Region rectangle count: 40
Region covered pixel count: 2176
Region excluded pixel count: 1920
Initial region type: ERROR (no region, LastError 0)
Top-left in region: False
Top-right in region: False
Bottom-left in region: True
Bottom-right in region: True
Center in region: True
WS_EX_TRANSPARENT active: False
Failure stage: 0
Initial region restored: True
Initial extended style restored: True
Present count: 1200
Last Present HRESULT: 0x00000000
Device removed reason: 0x00000000
Continuous Present successful: True
Shutdown complete: True
UI thread stopped: True
runInBackground restored: True
```

The separate-process Notepad click result, visible clipping, and edge-flicker
result remain manual observations and are not inferred from these counters.
