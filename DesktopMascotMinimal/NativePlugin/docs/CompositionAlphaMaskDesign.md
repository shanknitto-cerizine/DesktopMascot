# Composition Alpha Mask Transport Diagnostics

## Scope

This diagnostic transports the alpha channel of the existing 64 x 64 Unity
composition source texture to CPU memory and publishes an immutable native
snapshot. It deliberately does not connect the snapshot to `WM_NCHITTEST`,
window styles, click-through behavior, window geometry, focus, activation, or
the DirectComposition visual tree.

## Mask contract

| Property | Value |
|---|---:|
| Width | 64 |
| Height | 64 |
| Format | One unsigned byte of alpha per pixel |
| Stride | 64 bytes |
| Byte count | 4096 bytes |
| Origin | Top-left (`x=0` left, `y=0` top) |
| Threshold | 128 |
| Hit rule | `alpha >= 128` |

The managed source is `GraphicsFormat.B8G8R8A8_SRGB`. The readback requests its
native four-byte pixels without an additional format conversion and extracts
byte offset 3 as alpha. The diagnostic pattern uses alpha values 0, 64, 128,
255, and a center rectangle at 192. The first result is compared in both
possible row orientations. Managed code performs exactly one vertical
normalization when needed; native snapshots are always top-left origin.

## Async readback policy

- API: `AsyncGPUReadback.Request(RenderTexture, mipIndex, callback)`
- Interval: 250 ms
- Maximum outstanding requests: 1
- Target completed masks: 20
- Individual request timeout: 2 seconds
- Overall timeout: 20 seconds
- No `WaitForCompletion`, `ReadPixels`, `Texture2D.Apply`, synchronous GPU wait,
  or per-mask logging

Readback latency is accepted by design. Hit testing in a later stage must use
the latest completed snapshot and must never wait for the GPU from a window
message handler.

## Ownership and lifetime

The readback request owns its returned `NativeArray<byte>`. Managed code reads
it only inside the completion callback, extracts alpha into one persistent
4096-byte pinned array, and passes that pointer to
`DMN_SubmitCompositionAlphaMask`. The pointer is valid only for that call.

Native code copies all 4096 bytes before returning. It never retains the
managed pointer. The published snapshot remains valid until a newer accepted
generation is published or diagnostics are stopped. No COM pointer or `HWND`
is part of this ABI.

## Native double buffering and thread model

Native code owns a staging snapshot and a published snapshot. Submission is:

1. Validate state, dimensions, stride, byte count, pointer, and generation.
2. Copy into staging while holding only the submit-serialization mutex.
3. Acquire the publication mutex briefly and swap staging/published vectors.
4. Publish metadata and generation through atomics.

The 4096-byte copy is not performed while holding the publication mutex.
Snapshot queries take the publication mutex briefly, so a future Composition
UI-thread hit test can read only a complete generation.

Expected callers are:

- Unity/AsyncGPUReadback callback: submit a new mask.
- Unity main thread: state and sample diagnostics.
- Future Composition UI thread: read the latest published snapshot only.

The last managed callback thread ID, native submit thread ID, and Composition
UI thread ID are recorded independently.

## Generation and rejection

Managed generations begin at 1 and increase once for each submitted mask.
Generation 0, a duplicate generation, or a generation older than the published
generation is rejected. Null data, mismatched dimensions/stride, byte-count
overflow, submission before start, and submission after shutdown are also
rejected. A rejection increments submitted/rejected counts, does not modify the
published snapshot, clears `last submit succeeded`, and records a failure
stage.

## State values

```text
0 NotStarted
1 Ready
2 WaitingForFirstMask
3 Receiving
4 SnapshotPublished
5 Validating
6 Completed
7 ShutdownRequested
8 Stopped
9 Failed
```

## Failure-stage values

```text
 0 None                         13 SnapshotUnavailable
 1 CompositionNotReady          14 SampleOutOfRange
 2 InvalidWidth                 15 UnexpectedAlphaValue
 3 InvalidHeight                16 OrientationMismatch
 4 InvalidStride                17 ThresholdMismatch
 5 ByteCountOverflow            18 UnexpectedSubmittedCount
 6 NullData                     19 UnexpectedAcceptedCount
 7 DiagnosticsNotStarted        20 UnexpectedRejectedCount
 8 ShutdownAlreadyRequested     21 ReadbackUnsupported
 9 InvalidGeneration            22 ReadbackRequestError
10 StaleGeneration              23 ReadbackTimeout
11 BufferAllocationFailed       24 ContinuousPresentFailed
12 SnapshotPublishFailed        25 ShutdownFailed
```

## Submit API contract

```cpp
int DMN_SubmitCompositionAlphaMask(
    const unsigned char* data,
    int width,
    int height,
    int stride,
    unsigned long long generation);
```

It returns 1 only after the complete snapshot is copied and published. The
input pointer is borrowed for the duration of the call. Boolean diagnostic
values use 32-bit integers and all exports use the same C ABI/Cdecl convention
as the existing plugin.

## Validation

After the twentieth accepted mask, native code checks:

- `(8,8) = 0`
- `(55,8) = 64`
- `(8,55) = 128`
- `(55,55) = 255`
- `(32,32) = 192`
- the vertically flipped interpretation does not match
- `0` and `64` miss while `128`, `192`, and `255` hit
- submitted/accepted/rejected counts are 20/20/0

Alpha comparison tolerance is two units. The published hit rule itself remains
the exact `>= 128` rule.

## Shutdown

Managed code first prevents future callback submission, waits for the single
outstanding request to finish, stops alpha-mask diagnostics, stops continuous
Present, requests Composition shutdown, observes UI-thread termination, and
restores the previous `Application.runInBackground` value. Native stop marks
shutdown requested, clears owned pixel storage under the existing locks, marks
the snapshot unavailable, and enters `Stopped`.

## Fixed-pattern WM_NCHITTEST diagnostic integration

`Fixed Alpha Pattern Pixel Hit-Test Diagnostics` may map client coordinates to
this fixed top-left snapshot and return a hit result immediately. It calls
`TryReadCompositionAlphaMaskPixel`, which takes the publication mutex only long
enough to validate metadata and read one byte. It does not start readback, wait
for GPU work, copy 4096 bytes, or mutate the mask from `WM_NCHITTEST`.

This is a diagnostics-only consumer. It is not connected to a moving mascot or
production input handling.

## Current limitations

- Fixed 64 x 64 mask; no downsampling or filtering.
- Diagnostic alpha pattern only, not mascot alpha.
- No tolerance/dilation/erosion around silhouette edges.
- Readback cadence is diagnostic (4 Hz), not per frame.
- Only the fixed-pattern pixel hit-test diagnostic consumes the snapshot.
