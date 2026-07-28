# Real Mascot Static Alpha Mask Integration Diagnostics

## Scope and classification

This Classification B milestone connects the actual scene-rendered mascot to
the validated DirectComposition and static Window Region paths. It does not
introduce a synthetic silhouette, production animation updates, adaptive
region cadence, dragging integration, or new input mechanisms.

## Render source

The scene `Main Camera` temporarily renders the existing VRM mascot into a
256 x 256 camera-source `RenderTexture` with
`GraphicsFormat.B8G8R8A8_SRGB`, one sample, and a transparent black clear
color. HDR and MSAA are disabled for the diagnostic. The original target,
clear mode, clear color, HDR, and MSAA values are restored during cleanup.

The camera-source texture is copied once with `Graphics.Blit` into the
separate no-depth transport texture used by D3D12 copy, alpha readback, and
preview. The verified Windows D3D12 Camera target path requires
`scale=(1,-1)` and `offset=(0,1)` for this copy. This is the coordinate-system
boundary between the Camera target and the already validated transport
texture; it is not a shader-side inversion. The Player IMGUI pass paints its
diagnostic background before the preview so that the last backbuffer image
from before camera redirection cannot appear beneath the preview.

All scene Animators are paused and the mascot expression, interaction, and
drag controllers are disabled temporarily. Their previous state is restored
before Player exit. The actual camera output is passed directly to the
existing continuous render-event path; no diagnostic material or procedural
shader is used.

## Texture orientation

The D3D12 transfer and GPU readback prerequisites must retain expected
orientation matching. No shader-side Y inversion or unconditional readback
row inversion is added. `DesktopMascotValidatedPreview` remains the only
place that applies the validated IMGUI UV transform.

## Alpha mask contract

The managed controller performs one `AsyncGPUReadback` of the actual mascot
RenderTexture and extracts byte 3 from each BGRA pixel. Alpha values greater
than or equal to 128 are covered. It records covered, transparent, and total
pixels, coverage percentage, and the covered-pixel bounding rectangle.

An empty mask fails safely. A full mask is reported by the mask statistics,
but does not fail native region construction solely for being full; the
milestone automated integration check requires both covered and transparent
pixels.

## Region construction and UI-thread apply

`DMN_StartRealMascotStaticAlphaDiagnostics` selects the existing 256 x 256
scanline/run-merging implementation in real-content mode. Synthetic
representative-point checks are skipped because the actual mascot outline is
asset-dependent. Empty-mask, run, merge, `GetRegionData`, timing, ownership,
and post-apply `GetWindowRgn` checks remain active.

Exactly one region is built and transferred through `SetWindowRgn` on the
composition UI thread. Successful transfer gives Windows ownership of the
applied `HRGN`; temporary, validation, restoration, and failed-transfer
objects remain caller-owned and are deleted by the plugin.

## Presentation and lifetime

The DirectComposition loop targets 30 FPS and 600 Presents, yielding roughly
20 to 22 seconds for visual inspection. Alpha-mask publication stops after
the static snapshot. At completion the original region and extended style
are restored, the composition window is destroyed, the UI thread is joined,
and five process GDI samples are taken 100 ms apart.

The documented native state is:

```text
9 = Completed
```

The final summary is emitted only after state 9 and native cleanup are
observed.

## Automated success

- the real mascot RenderTexture exists and has a non-zero native pointer
- format and dimensions are `B8G8R8A8_SRGB`, 256 x 256
- GPU readback succeeds and validated orientation remains correct
- the thresholded mask is neither empty nor full
- the covered bounding rectangle has positive dimensions
- one non-empty region is built and successfully applied
- 600 Presents complete with `S_OK` and no device removal
- `HRGN` ownership invariants hold and live-owned count reaches zero
- the initial region and extended style restore
- five final GDI samples stabilize with no continuing growth
- managed failure and readback-error counts remain zero

Visual click delivery and visible clipping are deliberately not marked
successful by automation.

## Visual verification

Place another application behind the actual composition window at `(100,100)`.
Confirm that the actual mascot and Unity preview are upright, the mascot is
not vertically mirrored, transparent background pixels pass clicks, central
opaque mascot pixels block clicks, and the clickable region follows the real
outline rather than the full 256 x 256 rectangle. Confirm that no unexpected
hair, limb, clothing, or outline clipping is visible.

The Unity IMGUI preview is a reference only; click tests target the
DirectComposition window.

## Limitations

- one static alpha snapshot at threshold 128
- no edge dilation, erosion, hysteresis, or adaptive threshold
- no per-frame or pose-driven region update
- real material alpha quality is observed but not modified
- visual verification remains pending until user confirmation

This milestone does not start a later adaptive-update or live-animation
milestone automatically.
