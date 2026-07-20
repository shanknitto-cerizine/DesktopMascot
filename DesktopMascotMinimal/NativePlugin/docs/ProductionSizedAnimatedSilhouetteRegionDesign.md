# Production-Sized Animated Silhouette Region Update Diagnostics

## Scope

Classification B diagnostic for a procedural 256 x 256 silhouette. It tests
4 Hz alpha publication, 0.5 Hz shape changes, latest-wins region updates,
duplicate suppression, region complexity/timing, GDI/HRGN lifetime, and
continuous 30 FPS presentation. Production assets, VRM/Live2D animation, and
per-frame region updates remain excluded.

## Phases and timing

The run uses threshold 128, a 250 ms publication interval, a 2000 ms phase
duration, 32 seconds of animation, and 1200 Presents. Phase 0 is neutral
(cyan), phase 1 raises the left arm (green), phase 2 raises the tail
(yellow), and phase 3 squashes the body and moves the legs outward
(magenta). Four cycles produce approximately 128 generations and normally
16 unique builds; the 32-second boundary may produce a seventeenth.

Each publication is a real GPU readback. Equal consecutive masks are
identified by FNV-1a hash and advance applied generation without rebuilding
an HRGN. The invariant is evaluation = build + duplicate for normally
processed messages; skipped generation gaps are separately reported as
superseded generations.

## Orientation

The shader retains the validated D3D12 `Graphics.Blit` orientation and does
not flip Y. Managed readback determines raw row order once, normalizes to
top-left rows, and native code consumes those rows unchanged. The
DirectComposition window and Window Region therefore use the validated
orientation. Only `DesktopMascotValidatedPreview` flips IMGUI texture UVs.

Displayed generation correlation is not directly instrumented. Synchronism
is instead checked with phase-specific mask/HRGN representative points and
by visual comparison of phase color and geometry.

## Region construction and pending policy

Native code scans exact horizontal runs and vertically merges identical
ranges before combining them with `RGN_OR`. `GetRegionData` records the
Windows-normalized rectangle count. Only one UI apply message can be pending.
At 4 Hz, construction occurs in the UI handler from the newest alpha
snapshot, so there is no built-HRGN replacement slot; replacement and
superseded-built counters are zero by design.

Each applied phase validates common body/left-ear/transparent-corner points
plus neutral/raised arm, low/raised tail, normal upper body, and outer-leg
points before apply and on a `GetWindowRgn` copy after apply.

## Ownership and performance

Every created HRGN is classified as caller-deleted, transferred to Windows,
or live-owned. Validation copies are an exclusive subset of caller-deleted
handles. Successful `SetWindowRgn` transfers ownership; failures remain
caller-owned. Final live-owned count must be zero and:

```text
created = caller-deleted + transferred + live-owned
```

Per-phase raw/merged/final rectangle and covered-pixel values are retained.
Overall QPC statistics retain last/min/max/average build and `SetWindowRgn`
times. Each build must be at most 10,000 us and each apply at most 5,000 us;
average targets are 2,000 us.

GDI counts record initial, animation minimum/maximum/last, restoration, and
final stabilized samples. Maximum animation GDI must be no more than initial
+5. Five final samples at 100 ms intervals must be stable within one object,
with median delta at most +2.

## Visual verification

Place Notepad behind the actual composition window at `(100,100)` and observe
at least one cycle. Confirm neutral, raised-left-arm, raised-tail, and squash
geometry; phase colors; blocking at new opaque points and pass-through at old
transparent points; left ear always at upper-left; legs at the bottom; no
orientation mismatch, visible phase lag, full-rectangle/full-transparent
flash, major flicker, black frame, move, resize, Z-order change, or focus
acquisition. IMGUI preview is only a visual reference and is not the click
target.

## Limitations

The diagnostic uses one UI-thread build/apply pipeline, so a prebuilt pending
HRGN replacement is not exercised at 4 Hz. Displayed GPU generation is not
directly tagged. Repeated-run leak trend and real mascot masks remain future
work. Success does not start Adaptive Region Update Rate or Real Mascot
Static Alpha Mask Integration automatically.
