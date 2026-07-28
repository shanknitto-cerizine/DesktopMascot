# Render-Safe Runtime Character Disposal

## Scope

M-044 adds render-safe early disposal for inactive runtime-imported characters.
It does not change character activation, the active Descriptor, generation,
persistence, Camera rendering, D3D12 transfer, readback, HRGN construction,
DirectComposition, or the final orderly-shutdown release path.

The architecture baseline remains M-043 until manual verification passes.

## Ownership and responsibility

`CharacterAssetManager` remains the sole owner of active and retired runtime
characters. Its private `RuntimeRetiredCharacterDisposalQueue` helper owns the
retired FIFO and its fence cohorts. The helper is neither a `MonoBehaviour`
nor a static owner.

`DesktopMascotRuntimePipeline` does not own queue state. Its existing
`WaitForEndOfFrame` loop only calls:

```csharp
CharacterAssetManager.PumpRetiredDisposalsAfterEndOfFrame()
```

The Scene continues to own the bundled character. `CharacterDescriptor`
remains reference-only.

## Independent disposal transaction

Activation and disposal are separate transactions:

```text
activation commit
-> previous runtime root becomes inactive
-> previous runtime handle enters the retired FIFO

later, after WaitForEndOfFrame
-> validate that the handle/root is not active
-> insert one GraphicsFence for the awaiting FIFO cohort
-> wait for GraphicsFence.passed
-> request RuntimeGltfInstance.Dispose() exactly once
-> wait until Unity confirms the captured root was destroyed
-> record Release Confirmed
-> remove the entry from the FIFO
```

Disposal never changes the active character, Descriptor, generation, saved
selection, or either character root's activation transaction. A disposed root
is never reactivated or used as rollback state.

## GraphicsFence safety boundary

The fence is created with:

```csharp
CommandBuffer.CreateGraphicsFence(
    GraphicsFenceType.AsyncQueueSynchronisation,
    SynchronisationStageFlags.PixelProcessing)
```

and the command buffer is submitted through
`Graphics.ExecuteCommandBuffer`. Submission occurs only from the Unity main
thread after the existing `WaitForEndOfFrame`, Camera normalization, native
Present pump, and readback pump for that frame.

All entries awaiting a fence at that pump share one fence cohort. Cohorts and
entries remain FIFO-ordered. `RuntimeGltfInstance.Dispose()` is requested only
after `GraphicsFence.passed` and a second ownership/activity validation.

`Dispose Requested` and `Release Confirmed` are distinct. UniVRM calls
`UnityEngine.Object.Destroy`, whose destruction is deferred; therefore a
successful `Dispose()` call does not immediately remove the queue entry. The
entry is removed only when the captured Unity root compares null on a later
pump.

## Unsupported, timeout, and failure behavior

If `SystemInfo.supportsGraphicsFence` is false, or fence creation/polling is
unavailable, early disposal is not attempted. The handle remains Manager-owned
and is released through the established post-pipeline shutdown path. This is
a supported fallback, not a runtime failure.

A pending fence times out at the first of:

- 300 frames; or
- 5 seconds of realtime.

Timeout cancels early disposal for that entry and retains it for shutdown.
There is no unbounded per-frame retry.

A safety-validation or `Dispose()` exception also retains the entry for
shutdown and is not retried every frame. The current character, generation,
persistence, Camera, and GPU pipeline continue unchanged. The exception log
contains only the exception type and a stable status.

## Queue diagnostics

The Manager publishes:

- current and maximum retired queue counts;
- fence support, cohort, pass, timeout, and fallback counts;
- Dispose Requested count;
- Release Confirmed count;
- Dispose request failure count.

The expected steady-state queue is normally zero, with a short transient of
one entry. Reaching 256 entries emits one non-fatal diagnostics warning and
does not stop activation or rendering.

Focused diagnostics also record
`Profiler.GetTotalAllocatedMemoryLong()` before, at the observed peak, and
after the switch sequence, plus the delta. These values are informational and
are not pass/fail criteria. Ownership, Dispose success, outstanding retired
entries, and Release Confirmed remain authoritative.

## Shutdown

The normal orderly-shutdown sequence remains unchanged. Early-disposal
pumping stops after the Manager shutdown barrier. After Camera, readback,
region publication, and native composition have stopped,
`ReleaseActiveOwnedCharacterAfterPipelineStop` requests disposal for the
active runtime handle and every retained timeout/unsupported/failure fallback
entry. Manager cleanup still requires active and retired ownership counts to
be zero.

## Validation

The focused Development launch performs:

```text
Bundled
-> Runtime A
-> Runtime B
-> Runtime C
-> Bundled
-> Runtime D
```

It waits for the retired queue to settle after each retirement and validates:

- exactly one active character root;
- runtime active handle count 1 while Runtime is active;
- runtime active handle count 0 while Bundled is active;
- retired queue count 0 after each settled transaction;
- Dispose request failure count 0;
- every successful early Dispose request receives Release Confirmed;
- maximum retired queue count remains bounded;
- memory samples are recorded without affecting the result.

Native C++, CMake, exports, and the native plugin ABI are unchanged.

## Known future work

- investigate UniVRM `RuntimeGltfInstance.SafeGetInitialPose()` and its static
  `PoseMap` retention behavior without modifying the package;
- determine whether UniVRM exposes a stronger explicit completion signal than
  Unity destroyed-object observation in a future package version;
- stress-test fence fallback and very large rapid-switch cohorts on additional
  D3D12 hardware.
