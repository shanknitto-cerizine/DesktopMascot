# Production Runtime Foundation Design

## Objective

This milestone separates the validated real-mascot rendering path from its
milestone diagnostics without redesigning the native plugin. Normal runtime
keeps the actual mascot animated, presents continuously, and publishes an
alpha-derived `SetWindowRgn` region at a bounded interval. Diagnostic
statistics, four-phase validation, fixed Present counts, IMGUI diagnostic
labels, and automatic test completion remain outside the runtime component.

## Runtime and diagnostic boundary

`DesktopMascotCameraSourcePipeline` owns the production-relevant Camera source
and normalized transfer textures. It is shared by the real static diagnostic,
the real animated diagnostic, and `DesktopMascotRuntimePipeline`.

`DesktopMascotRuntimePipeline` owns normal runtime scheduling, native start and
stop requests, one reusable managed mask, one reusable presentation
`CommandBuffer`, and lifecycle cleanup. It does not contain phase assertions or
milestone pass/fail counters.

The existing diagnostic behaviours remain available, but are opt-in through
the following Development Player modes:

```text
DESKTOP_MASCOT_MODE=real-static-diagnostic
DESKTOP_MASCOT_MODE=real-animated-diagnostic
DESKTOP_MASCOT_MODE=runtime-smoke
```

The equivalent command-line form is
`--desktop-mascot-mode=<mode>`. With no explicit mode, normal `runtime` starts
and does not contain an automatic quit timer.

## Texture ownership and data flow

The managed Camera pipeline owns both temporary `RenderTexture` objects:

```text
Unity Camera
  -> cameraSourceTexture (256x256, B8G8R8A8_SRGB, depth)
  -> explicit normalization Graphics.Blit
  -> normalizedTransferTexture (256x256, B8G8R8A8_SRGB, no depth)
  -> unchanged D3D12 copy/readback
  -> alpha mask -> HRGN -> SetWindowRgn
  -> unchanged DirectComposition presentation
```

It saves and restores `Camera.targetTexture`, clear mode, background colour,
HDR, and MSAA. Disposal is idempotent and releases only managed-owned
RenderTextures.

## Single Camera-source normalization rule

Camera-source normalization is intentionally performed exactly once per
rendered update at the boundary between the two RenderTextures:

```csharp
Graphics.Blit(
    cameraSourceTexture,
    normalizedTransferTexture,
    new Vector2(1, -1),
    new Vector2(0, 1));
```

This source correction does not alter downstream D3D12, readback,
region-generation, DirectComposition, or validated IMGUI preview orientation.
No shader-side inversion was introduced. Native copy and region code remain
unchanged.

## Configuration

`DesktopMascotRuntimeConfiguration` groups the initial runtime values:

- texture width: 256
- texture height: 256
- format: `B8G8R8A8_SRGB`
- alpha threshold: 128
- region publication interval: 250 ms
- target Present rate: 30 FPS

The current native path is validated specifically for 256x256 and threshold
128. Invalid dimensions, threshold, interval, or FPS fail safely before native
runtime work starts. Diagnostics retain their separate duration and Present
count configuration.

## Runtime lifecycle

1. Wait for the already validated composition, destination texture, and
   orientation prerequisites.
2. Create and attach the Camera-source texture pipeline.
3. Normalize once and acquire the normalized transfer native pointer.
4. Start continuous composition with the existing render event.
5. Start animated alpha-mask publication.
6. Present at the configured rate and request one readback at most every
   250 ms.
7. Reuse the same pinned 256x256 mask array for every publication.
8. Start the additive native runtime-region entry point after the first valid,
   non-empty mask.
9. Continue until the application requests shutdown.

The `runtime-smoke` mode follows the same path for eight seconds and then
explicitly requests orderly shutdown. This timer is not present in normal
runtime mode.

## Cleanup lifecycle

Shutdown is idempotent and shared by normal completion, startup failure,
runtime failure, `Application.wantsToQuit`, `OnDisable`, `OnDestroy`, and the
smoke test.

The orderly path:

1. stops new readbacks and waits for an outstanding callback;
2. completes and stops region publication;
3. waits for the composition UI thread to restore the initial HRGN and style;
4. stops alpha-mask publication;
5. stops continuous composition;
6. requests composition-thread shutdown and finalizes it;
7. makes no further native calls after native shutdown;
8. releases the command buffer and pinned mask;
9. restores the Camera and releases both RenderTextures;
10. restores `Application.runInBackground`.

`Application.wantsToQuit` temporarily delays process exit until the orderly
path completes. Emergency `OnDisable`/`OnDestroy` handling is guarded against
double release and does not free a mask while its readback is pending.

The native side retains its established ownership contract: caller-owned
regions are deleted by the plugin, successful `SetWindowRgn` ownership is
transferred to Windows, no pending owned region remains after cleanup, and the
initial window region and style are restored.

## Development-build guardrails

Development builds assert that:

- source and transfer RenderTextures are distinct;
- normalization is not requested twice for one rendered update;
- region and composition receive the normalized transfer texture;
- shader-side inversion remains disabled.

Real-mascot diagnostic previews still use
`DesktopMascotValidatedPreview`; the runtime component has no diagnostic IMGUI
preview. These checks are not expensive per-frame validation in non-development
builds.

## Allocation audit

The steady-state runtime mask path uses one mask array, one pinned handle, and
one reusable command buffer. It does not allocate a new full-size array,
collection, or unchanged log message for each 250 ms sample. The animated
diagnostic retains its fixed arrays and representative hash collection because
those values are required by its final validation; it does not allocate a new
full-size mask per sample.

## Known limitations

- Runtime mode disables the diagnostic Present target and 50-second overall
  timeout. Its diagnostic counters remain 32-bit and would wrap only after
  multiple years at the initial 30 FPS rate.
- Runtime alpha readback remains a diagnostic-era CPU readback at 4 Hz; it is
  not yet an optimized production hit-test transport.
- Configuration is intentionally limited to the currently validated 256x256,
  threshold-128 path.
- The Unity/device message
  `d3d12: failed to query info queue interface (0x80004002)` is documented and
  not treated as a functional failure. Validated D3D12 copy, readback, Present,
  and device-removed checks continue successfully after it.
- Final user-facing window placement, drag interaction, settings integration,
  and production UI controls are outside this milestone.
- A process-level `CloseMainWindow()` heuristic can select the native
  composition window instead of the Unity Player window. Development
  automation must target the Unity window explicitly or use the orderly
  managed quit path; closing only the composition window invalidates the
  runtime surface without requesting application shutdown.
