# Development Milestones

## Production-Sized Animated Silhouette Region Update Diagnostics

Status: Completed  
Visual verification: Passed

### Configuration

- Resolution: 256 × 256
- Alpha threshold: 128
- Mask publish interval: 250 ms
- Phase duration: 2000 ms
- Animation duration: 32 seconds
- Target FPS: 30
- Target Present count: 1200

### Results

- Published generations: 127
- Applied generation: 127
- Region builds: 17
- Duplicate masks skipped: 110
- Superseded generations: 0
- All four phases applied: Yes

### Performance

- Region build: min 193 µs, max 470 µs, average 319 µs
- SetWindowRgn: min 634 µs, max 1025 µs, average 872 µs
- Measured Present rate: 27.46 FPS

### Resource ownership

- HRGN created: 971
- HRGN caller-deleted: 954
- HRGN ownership transferred: 17
- HRGN live-owned at completion: 0
- Final GDI samples: 19, 19, 19, 19, 19

### Validation

- Automated diagnostics: Passed
- Visual verification: Passed
- Display and click-region synchronization: Passed
- Orientation: Passed
- Continuous Present: Passed
- Initial region and style restoration: Passed

### Notes

The final summary was logged while the diagnostic state value was 9 rather
than the documented Completed state. Failure stage remained 0 and all
automated checks passed. Align final state transition and summary timing in a
future cleanup milestone.

## M-028 — Real Mascot Animated Alpha Mask Integration Diagnostics

Status: Completed  
Visual verification: Passed

### Summary

Validated the complete animated real-mascot pipeline:

```text
Unity Camera
→ Camera RenderTexture
→ explicit Camera-source Y normalization Blit
→ shared normalized transfer RenderTexture
→ D3D12 GPU copy/readback
→ alpha mask
→ animated HRGN construction
→ SetWindowRgn
→ DirectComposition presentation
```

Camera-specific Y normalization is intentionally localized at the source
boundary. The downstream D3D12, readback, region-generation, composition, and
IMGUI preview orientation rules remain unchanged. No shader-side Y inversion
was introduced.

### Configuration

- Unity: 6000.3.20f1 LTS
- Graphics API: Direct3D12
- Resolution: 256 × 256
- Graphics format: B8G8R8A8_SRGB
- Alpha threshold: 128
- Requested FPS: 30
- Target Present count: 1200
- Alpha-mask publish interval: 250 ms
- Active animation duration: 32 seconds

### Results

- Actual mascot available: True
- Animator available: True
- Animation playing: True
- Published generation count: 126
- Applied generation: 126
- Generation evaluation count: 126
- Valid mask evaluation count: 126
- Empty or invalid mask count: 0
- Distinct binary mask hash count: 126
- Region build count: 126
- Duplicate mask skip count: 0
- Superseded generation count: 0
- Apply message post/execution/success/failure: 126/126/126/0
- Maximum pending apply message count: 1
- Maximum pending owned-region count: 0
- All four animation phases observed: True
- All four animation phases applied: True
- Readback errors: 0
- Automated diagnostics passed: True
- Visual verification passed by user

### Performance

- Region build min/max/average: 414 / 710 / 509 microseconds
- SetWindowRgn min/max/average: 439 / 1095 / 565 microseconds
- Present count: 1200
- Elapsed time: 43906 ms
- Measured FPS: 27.33
- Present HRESULT: S_OK
- Device removed HRESULT: S_OK

### Resource ownership

- HRGN created: 15815
- HRGN caller-deleted: 15689
- HRGN ownership transferred: 126
- HRGN validation objects: 126
- HRGN live-owned after cleanup: 0
- HRGN ownership invariant: Passed
- GDI initial/min/max/last: 17/19/20/19
- Final GDI samples: 19, 19, 19, 19, 19
- Stabilized GDI delta: +2
- GDI stability: Passed

### Cleanup

- Initial window region restored: True
- Initial window style restored: True
- runInBackground restored: True

### Validation

- Shared GPU/readback expected orientation match: True
- Shared GPU/readback vertically flipped orientation match: False
- Preview uses validated UV transform: True
- Managed and native representative mask hashes matched in all four phases
- Transparent areas outside the animated mascot clicked through
- Opaque mascot body areas blocked clicks
- Region followed the animated silhouette
- Preview and DirectComposition output were upright
- No duplicate or vertically concatenated mascot image was visible

### Known notes

- Duplicate suppression count was zero because all 126 sampled binary masks
  were distinct during the animation. This is valid behavior.
- The logged visual verification value remains pending by design; the user
  subsequently completed manual visual verification successfully.
