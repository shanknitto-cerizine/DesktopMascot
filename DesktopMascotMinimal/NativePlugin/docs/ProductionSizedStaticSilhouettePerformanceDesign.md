# Production-Sized Static Silhouette Performance Diagnostics

## Scope and classification

This is a Classification B diagnostic for one static 256 x 256 procedural
silhouette. It measures region complexity, construction/application time,
continuous presentation, and GDI/`HRGN` lifecycle behavior. It does not use
a mascot asset, Live2D, animation, dragging, or the production window.

The existing shape is scaled by four in each dimension. Its alpha values
remain 0 for the background, 64 for the soft outer band, and 255 for the
inner silhouette. The test threshold is 128.

## Fixed run configuration

```text
composition window: 256 x 256 at (100,100)
RenderTexture: 256 x 256
requested target FPS: 30
effective target FPS: 30
target Present count: 1200
expected Present duration: approximately 40 seconds
visual verification interval: 30 seconds
maximum diagnostic timeout: 55 seconds
```

The controller explicitly supplies 30 FPS without changing defaults used by
other diagnostics. Requested and effective FPS must match. A measured
average of 25 through 35 FPS is accepted because this is a scheduler-level
diagnostic rather than a frame-pacing precision test.

## Mask orientation and representative points

On this D3D12 RenderTexture path, `Graphics.Blit` supplies vertically
inverted UVs, so shader UV `y=0` already corresponds to the displayed top.
`AsyncGPUReadback` is normalized independently by the managed orientation
check before native region publication. Exactly one readback is published
as generation 1.

| Point | Coordinate | Expected |
|---|---:|---:|
| body center | `(124,152)` | hit |
| long left ear | `(60,32)` | hit |
| mirrored right ear | `(192,32)` | miss |
| bottom-right tail | `(228,172)` | hit |
| vertically mirrored tail | `(228,80)` | miss |
| transparent top-right corner | `(248,8)` | miss |
| transparent bottom-left corner | `(8,248)` | miss |

All seven expectations are checked in the published alpha data, the
pre-apply `HRGN`, and a caller-owned copy returned by `GetWindowRgn`.

## Region generation and complexity

Each row is scanned into exact half-open alpha runs. Identical horizontal
runs in consecutive rows are merged vertically. The completed rectangles
are combined with `RGN_OR`. This preserves the existing algorithm while
changing only resolution.

The diagnostic records:

- raw scanline run count and raw runs per row
- vertically merged input rectangle count and rectangles per row
- Windows-normalized `GetRegionData` rectangle count and rectangles per row
- `GetRegionData` byte size and region type
- covered/excluded pixels and covered percentage
- merge reduction count and percentage

Merged input rectangles and final `GetRegionData` rectangles are different
quantities. Windows may normalize the combined region, so equality is not a
success condition. Both must be nontrivial, and merged count must not exceed
raw run count.

## Timing boundaries and thresholds

`QueryPerformanceCounter` measures total native region construction and
`SetWindowRgn` separately. Last, minimum, maximum, and average infrastructure
is retained; a single static build normally makes these values identical.

```text
total region build <= 10,000 microseconds
SetWindowRgn <= 5,000 microseconds
```

The total build measurement includes row scanning, vertical merge, `HRGN`
creation/combination, representative checks, pixel accounting, and
`GetRegionData`. `SetWindowRgn` excludes construction and post-apply
`GetWindowRgn` validation. Detailed phase timings remain deferred because
the total and apply boundaries are sufficient for this milestone.

## Counter semantics

The static publication expects one evaluated generation, one build, one
posted apply message, one UI-thread execution, and one successful apply.

```text
success + failure == execution
execution == posted apply messages
build == post == execution == success == 1
failure == 0
```

Legacy ambiguous counter labels are not used by the production-sized log.

## GDI lifecycle sampling

`GetGuiResources(GetCurrentProcess(), GR_GDIOBJECTS)` is recorded initially,
after region build, after `SetWindowRgn`, after region/style restoration,
after composition window destruction, after UI thread join, and after native
diagnostic cleanup.

After join and native cleanup, managed code takes five samples 100 ms apart.
The median is the stabilized final value.

```text
stable = max(samples) - min(samples) <= 1
final delta = median(samples) - initial
accepted final delta <= 2
```

The absolute GDI value is process-wide and may include unrelated Unity
objects. A +2 delta is not, by itself, an `HRGN` leak. Repeated-run growth is
not tested in this one-run architecture.

## Owned HRGN accounting

All region handles created by this diagnostic pass through tracked helpers.
Counters distinguish created handles, caller-deleted handles, ownership
transferred to Windows, initial-region restoration copies, and handles still
owned by the plugin.

On successful `SetWindowRgn`, Windows owns the handle and caller deletion is
forbidden. On failure, the plugin deletes it. Temporary `GetWindowRgn` and
combine-part handles remain caller-owned and are deleted. At completion,
plugin-owned live handle count must be zero.

No demonstrated leak requires:

```text
final stabilized GDI delta <= 2
stable samples = true
plugin-owned live HRGN count = 0
```

## Shutdown order

The diagnostic restores the original window region and extended style,
waits for the continuous Present target, stops alpha-mask publication,
requests composition shutdown, destroys the composition window, joins the UI
thread, closes native synchronization handles, and samples stabilized GDI
counts. `Application.runInBackground` is restored before Player quit.

## Automated success conditions

- state completed and native/managed failure stages are zero
- dimensions 256 x 256 and threshold 128
- requested/effective FPS both 30
- generation and counter invariants hold
- all seven representative points match in every validation layer
- covered plus excluded pixels equals 65536
- total build and `SetWindowRgn` thresholds pass
- original region and extended style restore
- final five GDI samples are stable and median delta is at most +2
- plugin-owned live `HRGN` count is zero
- exactly 1200 Presents complete normally
- last Present and device-removed results are `S_OK`
- measured Present rate is between 25 and 35 FPS
- composition window is destroyed, UI thread joins, and background-run state
  restores

Automated geometry cannot prove cross-process click delivery, so successful
automation is reported with "visual verification pending".

## Visual verification

Place Notepad behind the actual 256 x 256 composition window at `(100,100)`.
The Unity preview is not the click target. Its IMGUI texture coordinates are
vertically corrected for visual comparison with the composition window; this
preview-only correction does not modify the RenderTexture, alpha readback, or
native region. During the 30-second interval:

The shader, GPU/readback path, DirectComposition output, and Window Region
orientation are validated and must not receive an additional Y flip. Static
and animated diagnostics share `DesktopMascotValidatedPreview`; only that
IMGUI helper applies the preview UV transform.

- inner silhouette must block clicks
- transparent background must pass clicks
- upper-left ear and lower-right tail must match visible geometry
- mirrored ear and mirrored tail locations must remain transparent
- no major outline gap, hole, flicker, or black frame may appear
- no unexpected move, resize, Z-order change, or focus acquisition may occur

The Player exits after the 1200-Present run and cleanup, so the window may
remain after the visual interval ends.

## Native and managed connection

`DMN_StartProductionSizedStaticSilhouetteDiagnostics` validates fixed
dimensions, threshold, target FPS, and Present count, then reuses the proven
static-region implementation. Production-prefixed getters expose
configuration, counters, timing, lifecycle, and `HRGN` accounting. Existing
ABI exports remain unchanged.

The independent managed controller is:

```text
Assets/_Project/Diagnostics/Presentation/
DesktopMascotProductionSizedStaticSilhouetteDiagnostics.cs
```

The procedural shader is reused with a 256 x 256 target. No production asset
or animated mask is introduced.

## Limitations and next stage

- only one static generation at threshold 128 is measured
- detailed scan/merge/combine timing phases are not separated
- GDI values are process-wide
- repeated-run leak trend is not tested
- visual click delivery requires user confirmation
- no production mascot sizing or content is involved

Only after automated and visual results are accepted, the smallest next
stage is Production-Sized Animated Silhouette Region Update Diagnostics.
This diagnostic does not start that stage automatically.
