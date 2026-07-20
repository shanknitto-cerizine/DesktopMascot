# Animated Alpha Mask Window Region Update Diagnostics

## Scope

This diagnostic extends the accepted fixed `SetWindowRgn` method to a
time-varying 64 x 64 alpha mask. It deliberately does not add production
mascot input, dragging, hooks, synthetic input, multiple windows, or
production-size region optimization.

Method A (`WM_NCHITTEST` returning `HTTRANSPARENT`) remains rejected because
the prior cross-process test did not deliver clicks to the process behind the
composition window. Method B is retained:

```text
Unity RenderTexture alpha
  -> AsyncGPUReadback (at most one outstanding request)
  -> managed 4096-byte alpha buffer
  -> native double-buffered published snapshot
  -> generation polling
  -> HRGN build and SetWindowRgn on the Composition UI thread
```

## Pattern and timing

The shader produces an exact 32 x 32 alpha-255 square on an alpha-0
background. The automated geometry initially used two-second phases. The
visual test now holds each phase for eight seconds so a person can reliably
position and click the actual 64 x 64 window:

| Phase | Opaque quadrant |
|---:|---|
| 0 | top-left |
| 1 | top-right |
| 2 | bottom-right |
| 3 | bottom-left |

On the D3D12 `Graphics.Blit` path used here, the shader input UV is vertically
inverted relative to the displayed composition texture. The shader therefore
defines displayed top as `uv.y < 0.5`. A synchronized screen capture with the
window region temporarily removed proved that the previous `uv.y >= 0.5`
condition displayed Phase 2 at top-right while the HRGN correctly occupied
bottom-right.

One cycle is 32 seconds. The visual test lasts 32 seconds (one complete
cycle).
The managed readback interval and native minimum region-update interval are
both 250 ms. `SetWindowRgn` is never called from each Unity frame, Present, or
render event.

The threshold is inclusive:

```text
alpha < 128  = outside the region
alpha >= 128 = inside the region
```

Every valid phase therefore covers 1024 pixels and excludes 3072.

## Generation model

Four generation values are kept distinct:

- published: latest immutable alpha snapshot
- requested: generation visible when the UI request is posted
- build: generation copied and examined by the UI thread
- applied: generation represented by the current window geometry

Polling posts only when `published > applied`, the 250 ms interval elapsed,
and no request is pending. At most one request is outstanding. The UI handler
copies the latest snapshot, so intermediate generations use a latest-wins
policy. Gaps are counted as skipped published generations and are not
failures.

### Counter semantics migration

The accepted animated run exposed an ambiguous legacy counter:

```text
DMN_GetAnimatedWindowRegionApplyRequestCount
```

Code inspection showed that it counted successful
`PostMessage(kAnimatedWindowRegionApplyMessage)` calls. It did not count
`SetWindowRgn` calls, region builds, or published generations. The legacy
export remains available for binary compatibility and keeps that historical
meaning.

New code and logs use the following unambiguous exports:

| Export | Increment point |
|---|---|
| `DMN_GetAnimatedWindowRegionGenerationEvaluationCount` | after a UI handler copies a valid published snapshot |
| `DMN_GetAnimatedWindowRegionApplyMessagePostCount` | after a successful apply-message `PostMessage` |
| `DMN_GetAnimatedWindowRegionApplyExecutionCount` | on entry to the UI apply handler |
| `DMN_GetAnimatedWindowRegionDuplicateMaskSkipCount` | when an evaluated hash equals the last applied hash |
| `DMN_GetAnimatedWindowRegionSupersededGenerationCount` | generations skipped by latest-wins snapshot selection |

Consequently:

```text
legacy ApplyRequestCount == ApplyMessagePostCount
ApplyMessagePostCount != SetWindowRgn execution count
GenerationEvaluationCount != published generation count
```

The old `SkippedGenerationCount` and `DuplicateSkipCount` exports also remain
as compatibility aliases. Managed diagnostics have moved to the precise
names.

The mask uses FNV-1a 64-bit change detection. If a newer generation has the
same contents as the applied mask, no new `HRGN` is built and no
`SetWindowRgn` call is made. The generation is still consumed and the
duplicate-skip counter advances. Correctness is also guarded by covered-pixel
and representative-point validation rather than relying only on the hash.

## Snapshot synchronization

The alpha transport owns staging and published vectors. Submission copies to
staging and swaps under its publication mutex. The Composition UI thread uses
`TryCopyCompositionAlphaMaskSnapshot` to copy exactly 4096 bytes while that
mutex is held, then releases the lock before any GDI work.

The animated transport start entry point disables the old fixed-pattern
sample validation at submission 20. Dimensions, stride, threshold,
generation monotonicity, double buffering, and all other transport checks
remain active.

## Region construction and validation

The existing scanline-run algorithm is reused. For every row, each contiguous
`alpha >= 128` run is converted to:

```cpp
CreateRectRgn(runStartX, y, runEndXExclusive, y + 1);
```

Runs are joined with `CombineRgn(..., RGN_OR)`. The current diagnostic may
report 32 input scanline rectangles even if GDI simplifies the resulting
region. Both `SIMPLEREGION` and `COMPLEXREGION` are acceptable when the
representative points and pixel counts are correct.

Native validation samples quadrant centers `(16,16)`, `(48,16)`, `(48,48)`,
and `(16,48)`. Exactly one must be inside. `(32,32)` is recorded separately;
it is not used as a boundary assertion.

## Thread and ownership rules

The Composition UI thread performs style capture, initial-region capture,
snapshot copy, hash, `HRGN` construction, `PtInRegion`, `SetWindowRgn`, and
restoration. No GPU wait, sleep, file I/O, or managed call occurs there.

`SetWindowRgn` ownership is strict:

- success: Windows owns the submitted `HRGN`; the plugin does not delete it
- failure: ownership did not transfer; the plugin deletes it
- a saved initial region remains plugin-owned
- restoration submits a duplicate, which transfers to Windows on success
- the saved copy is deleted after restoration or teardown

Because the pending message carries no `HRGN`, message-post failure,
diagnostic cancellation, and UI shutdown cannot strand an in-flight region
handle.

## Style and initial-region semantics

The exact initial extended style is recorded. The diagnostic style is:

```text
initial extended style | WS_EX_LAYERED
```

`WS_EX_TRANSPARENT` is not enabled. Stop restores and verifies the exact
initial style.

Initial region state is reported independently from the raw Win32 return:

```text
0 Unknown
1 None
2 Empty
3 Simple
4 Complex
5 QueryFailed
```

`GetWindowRgn` returning `ERROR` with LastError still zero is the normal
`None` case, not an error. A real query failure is `QueryFailed`.

## GDI leak monitoring

The UI thread records `GetGuiResources(GetCurrentProcess(), GR_GDIOBJECTS)`
at initialization, observes a peak while building/applying regions, and
records the final count after restoration and releasing the saved region.
The accepted final delta is at most +2. Counts must not grow in proportion to
the number of updates.

## Managed lifecycle and visual test

The standalone Windows Player controller:

1. preserves and enables `Application.runInBackground`
2. waits for the existing D3D12/composition/readback prerequisites
3. starts the continuous 64 x 64 composition path (35 FPS scheduling target;
   the first 30 FPS run reached only 1373 Presents before the 50-second
   native limit, so the diagnostic keeps a small scheduling margin)
4. renders the four-phase premultiplied-alpha shader
5. performs one AsyncGPUReadback at most every 250 ms
6. normalizes the readback to top-left origin
7. publishes monotonically increasing alpha generations
8. starts and polls animated region updates
9. validates all four native phase geometries for 32 seconds
10. restores region and style
11. waits for the bounded continuous Present diagnostic
12. stops alpha transport, composition, and the UI thread
13. restores `runInBackground`, emits final logs, yields one managed frame,
    and quits (it does not wait for `WaitForEndOfFrame` after graphics
    shutdown)

The actual click target is the 64 x 64 composition window at the position
shown in `Player.log` and the Player guide. Place Notepad behind that window.
The opaque quadrant must block the click; transparent quadrants must deliver
it. After a phase change, the previous opaque quadrant must become
click-through and the new opaque quadrant must block clicks. The enlarged
Unity Player guide is not the click target.

The guide scale is calculated from the current Player width and height and is
capped at 8x. The instruction area and a bottom margin are reserved first, so
all four guide quadrants remain visible in a shorter Player window.

## Timeout-safe build workflow

This is Classification B because C# and shader assets changed:

```powershell
.\Tools\Development\Stop-DevelopmentPlayer.ps1
.\Tools\Development\Verify-DevelopmentEnvironment.ps1
.\Tools\Development\Build-Native.ps1
.\Tools\Development\Build-UnityIncremental.ps1
```

If the Unity build command reaches its caller timeout, do not run it again or
kill Unity. Inspect the build lock, dedicated build log, and
`Get-UnityIncrementalBuildStatus.ps1` until the state becomes `Succeeded` or
`Failed`. Before launch, deployment requires an unlocked destination DLL and
matching SHA-256 hashes for the Assets and Player copies. A second Player
instance is never launched.

## Current limitations

- Only the exact 64 x 64 binary-alpha diagnostic mask is supported.
- Region construction is intentionally unoptimized beyond scanline runs.
- Native geometry validation cannot prove cross-process click delivery;
  Notepad verification remains visual/manual.
- The test does not move, resize, focus, activate, or change the Z-order of
  the composition window.
- Production-size masks and a real mascot silhouette are explicitly deferred.
