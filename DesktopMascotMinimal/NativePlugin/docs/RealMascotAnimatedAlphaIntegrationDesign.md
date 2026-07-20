# Real Mascot Animated Alpha Integration Design

## Objective

This integration diagnostic proves that the alpha channel of the actual,
animated mascot can drive both the DirectComposition image and the Win32
window region continuously. It preserves the validated D3D12 transfer,
readback, scanline merging, `SetWindowRgn`, latest-wins, and cleanup paths.
It is a diagnostic milestone, not the final interaction implementation.

## Data flow and orientation boundary

The data flow is intentionally:

```text
Actual mascot + deterministic looping Animator state
  -> Camera RenderTexture (256x256 B8G8R8A8_SRGB)
  -> explicit source-normalization Graphics.Blit
       scale=(1,-1), offset=(0,1)
  -> shared normalized transfer RenderTexture
  -> unchanged validated D3D12 copy / readback / region pipeline
  -> unchanged DirectComposition presentation pipeline
```

The Camera Y transform is a localized source-boundary correction. It does not
change the downstream D3D12 orientation contract and must not be repeated in a
shader, native copy code, alpha-region code, or the validated IMGUI preview
helper. Both DirectComposition and region readback consume the same normalized
transfer texture.

## Animation and phase validation

The scene's real mascot `Animator` and existing `Standing Idle` state are used;
no runtime animation loader is introduced. Interaction, drag, and expression
controllers are disabled for repeatability while the Animator remains enabled
at speed 1. The fractional normalized animation time is divided into four
phases. For every phase, managed code records the last valid generation,
covered pixels, bounding rectangle, and binary-mask FNV-1a hash. Native code
records the last applied generation, run counts, merged and final rectangle
counts, covered pixels, and the same binary-mask hash.

All phases must be observed and have a successfully applied, non-empty region.
At least two distinct binary mask hashes must occur, proving animated alpha
rather than generation-number-only changes.

## Update frequency and duplicate suppression

Presentation targets 30 FPS and 1200 Presents (approximately 40 seconds).
Actual animation and mask publishing remain active for at least 32 seconds.
Readback publication is limited to 4 Hz (one generation every 250 ms); region
work never runs every rendered frame.

The native FNV-1a hash uses the thresholded 256x256 binary mask. When that mask
matches the last applied mask, the generation is evaluated and counted as a
duplicate, but no `HRGN` is built and `SetWindowRgn` is not called.

## Latest-wins and ownership

The existing composition UI message path remains latest-wins: at most one
apply message is pending, and its handler snapshots the newest published
generation. Region construction and `SetWindowRgn` execute only on the
composition UI thread.

Every created `HRGN` is tracked. A failed or validation-only region is deleted
by the caller; after successful `SetWindowRgn`, ownership transfers to Windows.
At shutdown the initial region and extended style are restored, no pending
region remains, and live caller-owned region count must be zero. The invariant
is:

```text
created = caller-deleted + ownership-transferred + currently-live-owned
```

## Automated pass criteria

The run passes automatically only when the actual mascot and Animator are
available and animation time progresses; both RenderTextures and the transfer
native pointer are valid; readbacks produce non-empty masks with at least two
hashes; all four phases are observed and applied; published, evaluated, and
final applied generations agree; build plus duplicate counts equal valid
evaluations; apply failures and readback errors are zero; pending-message,
performance, HRGN, restore, GDI, Present, HRESULT, and Completed-state checks
all pass.

The diagnostic always reports `Visual verification pending: True`; automation
does not certify appearance or click behavior.

## Manual visual validation

Run the Development Player and keep another clickable application behind the
64x64 composition window. During the active interval confirm:

1. One upright mascot appears in the Player preview and one in composition.
2. Head and feet directions match; no mirrored or concatenated duplicate is
   present.
3. The actual mascot animation is visibly playing without severe clipping.
4. Transparent pixels outside the current silhouette click through.
5. Opaque body pixels block clicks.
6. The hit region follows major pose changes without lagging several phases.
7. Window-wide input blocking does not return.

## Known limitations

- The test uses the currently assigned `Standing Idle` clip; silhouette
  variation depends on that imported animation.
- A 128 alpha threshold intentionally excludes soft fringe pixels.
- Readback and region publication are diagnostic CPU operations at 4 Hz, not a
  production input-mask solution.
- Visual correctness and real-world click-through remain a manual judgment.
