# Static Complex Silhouette Window Region Diagnostics

## Scope

This Classification B diagnostic validates one reproducible 64 x 64 static
silhouette. It does not use a production mascot asset, a production-sized
window, Live2D, dragging, or a new input architecture.

```text
static premultiplied-alpha shader
  -> Unity B8G8R8A8_SRGB RenderTexture
  -> one AsyncGPUReadback
  -> top-left-origin 4096-byte alpha snapshot
  -> vertically merged scanline rectangles
  -> HRGN
  -> SetWindowRgn on the Composition UI thread
```

## Shape and orientation markers

The shader combines ellipses, rectangles, thick line segments, and two
triangles into a head, body, arms, legs, ears, and tail. The deliberately
asymmetric markers are a long upper-left ear and a stepped lower-right tail.
Shader UV `y=0` represents displayed top on the verified D3D12
`Graphics.Blit` path. Managed readback accepts exactly one of native row order
and vertically flipped row order, then publishes top-left-origin rows.

| Point | Coordinate | Expected |
|---|---:|---:|
| body center | `(31,38)` | hit |
| long left ear | `(15,8)` | hit |
| mirrored ear | `(48,8)` | miss |
| bottom-right tail | `(57,43)` | hit |
| vertically mirrored tail | `(57,20)` | miss |
| transparent corner | `(2,2)` | miss |

The normalized mask, pre-apply `HRGN`, and a post-apply `GetWindowRgn` copy
must agree at all six points.

## Alpha and thresholds

Background alpha is 0, core alpha is 255, and the soft head/body/tail band is
64. `alpha >= threshold` is included. The automated run uses threshold 128.
The native entry point also accepts threshold 32 for a later comparison that
includes the alpha-64 band.

## Scanline runs and vertical merge

Every row becomes exact `[xStart,xEndExclusive)` runs. A run extends
vertically only when the next row contains the identical x range. Partial
overlaps, splits, and joins are never merged. Completed rectangles are
combined with `RGN_OR`.

Raw scanline run count and merged rectangle count are recorded separately.
Before ownership transfer, `GetRegionData` records `nCount` and byte size;
`GetRgnBox` records the region type. A query failure fails the diagnostic.

## Counter semantics and invariants

- published generation: generation copied from the immutable snapshot
- generation evaluation: valid snapshot copied for evaluation
- region build: completed `HRGN` build
- apply message post: successful UI `PostMessage`
- apply execution: UI handler entry
- apply success/failure: `SetWindowRgn` result only
- duplicate mask skip: evaluated generation equal to the applied mask
- superseded generation: generations omitted by latest-wins coalescing

The static run expects:

```text
build = post = execution = success = 1
failure = duplicate = superseded = 0
success + failure = execution
```

## Timing and synchronization

`QueryPerformanceCounter` measures region construction separately from
`SetWindowRgn`. Last/min/max/average build microseconds and last/max apply
microseconds are exported. A sub-microsecond `SetWindowRgn` result may be
reported as zero.

Readback and snapshot publication occur through Unity callbacks. Style,
region, `GetRegionData`, `SetWindowRgn`, and restoration work occurs on the
Composition UI thread. No GPU wait or managed callback occurs there.

## Ownership and GDI diagnostics

- successful `SetWindowRgn`: Windows owns the submitted `HRGN`
- failed `SetWindowRgn`: the plugin deletes it
- saved initial regions and `GetWindowRgn` validation copies are plugin-owned
- restoration submits a duplicate and deletes the saved copy

Initial GDI count is sampled before the build, pre-shutdown count after
region/style restoration, and post-shutdown count after Composition COM
objects are released. Counts are process-wide. A run expects pre-shutdown
delta at most +2 and post-shutdown delta at most zero; if the latter varies,
repeated-run growth is evaluated without normalizing the measurement.

## Visual verification

The actual click target is the 64 x 64 composition window at `(100,100)`;
the 8x Unity Player preview is not a click target. For 30 seconds, place
Notepad behind it and verify:

- silhouette pixels block clicks
- transparent pixels pass clicks
- the upper-left ear and lower-right tail match blocked geometry
- mirrored ear and vertically mirrored tail positions are transparent
- no inversion, clipping, major flicker, black frame, move, resize, Z-order
  change, or focus acquisition

Continuous presentation uses a per-run target of 1200 Presents. Automated
geometry cannot prove cross-process delivery, so success remains “automated
diagnostics passed; visual verification pending” until user confirmation.

## Current limitations

- only 64 x 64 and automatic threshold 128 are tested
- production-sized region performance is not measured
- GDI values are process-wide
- production mascot assets, Live2D, dragging, and production sizing remain
  deferred

## Recorded baseline and handoff

The completed 64 x 64 run recorded 80 raw scanline runs, 48 vertically
merged input rectangles, 57 rectangles in the Windows-normalized region,
1575 covered pixels, and 2521 excluded pixels. Region construction took
174 microseconds and `SetWindowRgn` took 344 microseconds in that run.

GDI counts are process-wide. The observed post-shutdown delta of +2 alone
does not demonstrate an `HRGN` leak. The production-sized diagnostic adds
lifecycle sampling, a stabilized median, and explicit tracking of every
diagnostic-owned `HRGN`.

The next diagnostic scales only the same procedural static shape to 256 x
256. Animated 256 x 256 updates remain a separate later stage.
