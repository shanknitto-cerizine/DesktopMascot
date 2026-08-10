# Minimum Speech Presentation Foundation

Status: M-048 completed; automated and visual verification passed

Architecture baseline: M-046

Design baseline: M-047.5

## Responsibility flow

```text
SpeechMessage (platform-neutral display data)
→ SpeechPresentationController (one current message and generation)
→ SpeechAnchorResolver (active-character observation only)
→ SpeechPresentationView (TMP + dedicated 170×64 RenderTexture)
→ ISpeechPresentationHost
→ WindowsSpeechPresentationHost
→ Unity render event 9
→ SpeechPresentation.cpp
→ independent Speech HWND + D3D12 copy + DirectComposition
```

This is an additive subsystem. It does not composite into the mascot's
256×256 transfer texture, use its alpha readback or HRGN, change Camera-source
Y normalization, or become a child of the active Character root. The fixed
M-048 greeting is created once in the focused diagnostic and is not embedded
in native code. Conversation packs, rules, scripts, AI providers, schemas,
serialization, and persistence remain future work.

## Managed domain and lifecycle

`SpeechMessage` contains only `MessageId`, `Speaker`, `Body`, `Theme`,
`ClosePolicy`, and `Transition`. `SpeakerData` contains only the logical
speaker ID, display name, and decoration. No Windows handle, D3D12 type,
provider type, command, URL, priority, timeout, or persistence field crosses
this boundary.

`SpeechPresentationController` lives once on the persistent runtime owner. It
has only `Hidden` and `Visible` states and no queue. A valid Show from Hidden
increments the presentation generation. A Show of the current MessageId is
coalesced; another ID returns Busy. Only the matching generation can close.
Click and timeout use one `closeClaimedGeneration`, so the first close wins;
duplicates and stale callbacks do not change visible state. The fixed timeout
is 12 seconds.

The M-048 product demo is limited to the explicit Development mode
`message-window-diagnostic`. Normal runtime does not show an unsolicited
greeting. A later provider can use the same `TryShow(SpeechMessage)` boundary
without changing presentation ownership.

## View and font ownership

`SpeechPresentationView` owns one persistent GameObject tree, one Speech-only
Camera, speaker/body TextMeshPro elements, and one
`B8G8R8A8_SRGB` 170×64 RenderTexture. They are allocated once, reused for
every Show, and destroyed deterministically. No RenderTexture is allocated per
frame. The dark card, narrow accent, separate name line, and clipped-side
layout form an asymmetric floating card. Transparent pixels outside the card
remain outside native input.

The Japanese font is **Noto Sans CJK JP Regular** from the official
`notofonts/noto-cjk` repository. It is licensed under SIL Open Font License
1.1, which permits application redistribution when the license and notices
are retained; modified font versions must follow the OFL naming and license
conditions. M-048 ships the unmodified OTF and its official `OFL.txt`:

- source: `https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese`;
- font SHA-256:
  `68A3FC98800B2A27B371F2FB79991DAF3633BD89309D4FFAA6946FD587F375B5`;
- license SHA-256:
  `6A73F9541C2DE74158C0E7CF6B0A58EF774F5A780BF191F2D7EC9CC53EFE2BF2`;
- generated project asset:
  `Resources/DesktopMascotSpeech/NotoSansCJKjp-Regular SDF.asset`.

The Editor generator creates the Dynamic TMP asset reproducibly. Runtime
loads that project asset and never generates a font asset from the OTF.

## Anchor resolution and positioning

The resolver borrows the active Descriptor and observes
`CharacterAssetManager.ActiveCharacterGeneration`; it owns neither. A
generation change discards old Animator/bone references and binds again.
Primary positioning blends Hips, both UpperLegs, and both LowerLegs. Missing
humanoid bones fall back to current active-root Renderer bounds. The projected
anchor is expressed in normalized 256-space only as a platform-neutral
position hint.

Native placement always starts from the actual mascot `GetWindowRect`.
It reads the current applied mascot silhouette with `GetWindowRgnBox`; this is
a read-only Speech positioning input and does not alter mascot HRGN ownership
or generation. The card horizontal center is aligned to the bounding-box
center of that currently applied silhouette. This replaces the visually
left-heavy fixed 31-pixel side-overlap candidate. The vertical anchor remains
unchanged: the card top is biased upward by one eighth of normalized mascot
space from the upper/lower-leg blend so its visible edge begins around the
waist/thigh area. If the centered rectangle exceeds the current monitor work
area, the smallest required clamp correction is applied; no side flip or
activation occurs. DPI is observed from the mascot HWND. Position is
updated only when the resolved anchor or mascot window changes and
`SetWindowPos` uses `SWP_NOACTIVATE | SWP_NOZORDER`. Speech position is never
persisted.

For `message-window-diagnostic` visual comparison only, the bundled
character remains at scale 1 and the existing perspective Camera keeps its
position. The earlier 37.87-degree comparison filled the alpha silhouette to
both vertical edges and is retained only as the pre-adjustment screenshot.
The current diagnostic target projects aggregate active-character Renderer
bounds to 260 pixels, producing an approximately 224-pixel visible silhouette
for the bundled reference and roughly 16 pixels of head/foot safety margin in
the unchanged 256 x 256 mascot surface. The computed FOV remains diagnostic
state and is restored with the original Camera state. Normal runtime Camera
framing is never changed. This margin-based full-body reference, rather than
a current-model edge fit, is the M-048 Speech UI comparison baseline; future
VRM coverage must validate hats, ears, long hair, skirts, and boots against
the same full-body/no-clipping invariant. The compact 136 x 56
candidate measured 53.1% x 21.9% of reference mascot height. The earlier
156 x 64 candidate measured 69.6% x 28.6% against the final 224-pixel
full-body baseline. Manual validation found its 10-pixel body text too small.

The final bundled-reference measurement computes a 48.32-degree FOV and an
alpha region `x=73, y=17, width=85, height=224`. Thus the actual full body has
17 pixels above and 15 below. A 12-pixel body-font trial required about 180
pixels for the longer unwrapped Japanese line and could not fit a 156-pixel
surface without clipping or an unreadably compressed line interval. The
correction therefore uses an 11-pixel body font and a 170 x 64 card. Its body
line requires 165.01 pixels, fits the 166-pixel body rect in exactly two lines,
and leaves the 12-pixel speaker line separate. The corrected card is 75.9%
wide and 28.6% high relative to the 224-pixel full-body baseline. At an
unclamped position its center difference from the 85-pixel silhouette box is
zero and their bounding-box horizontal intersection is 85 pixels.

The current Camera isolation is intentionally diagnostic-only. Before Player
Preview creation, `message-window-diagnostic` stores the production Camera
culling mask and excludes dedicated Speech Layer 30. The preview copies that
isolated mask, while the Speech Camera renders only Layer 30. Runtime cleanup
restores and validates the exact original mask idempotently; `OnDestroy` is a
fallback for partial initialization and exceptions. Normal runtime does not
apply this diagnostic isolation. Before Speech becomes a normal-runtime
feature, a permanent production Camera isolation contract must be introduced
and validated rather than enabling this diagnostic path unconditionally.

## Windows HWND, input, and Z-order

The Speech window is an independent owned top-level `WS_POPUP`, created on
the existing native composition UI thread with `WS_EX_TOOLWINDOW`,
`WS_EX_NOACTIVATE`, and `WS_EX_NOREDIRECTIONBITMAP`. Its owner is exactly the
mascot HWND. It is shown with `SW_SHOWNOACTIVATE`; it neither uses the Player
visibility controller nor activates Player or mascot.

A Speech-only rounded-card HRGN is created once. Before successful
`SetWindowRgn`, native code owns and deletes it on failure. After success,
Windows owns it until replacement or window destruction. The shadow is not in
the region. `WM_LBUTTONUP` publishes only the current presentation generation;
managed code consumes it once. Hiding uses `SW_HIDE` only on the Speech HWND,
so its transparent/outside area and the hidden window cannot steal mascot or
background input. The mascot HRGN and drag contract are untouched.

## D3D12 and DirectComposition ownership

The Speech window owns its DComp target and visual, composition swap chain,
back-buffer references, command allocator, command list, and the associated
COM reference lifetimes. The DirectComposition swap chain uses
`DXGI_ALPHA_MODE_PREMULTIPLIED`.

Unity's Speech Camera target is vertically inverted when its D3D12 resource
is copied byte-for-byte into the composition swap chain. The independent
Speech DirectComposition visual therefore applies one vertical transform
(`scaleY = -1`, translated by the fixed Speech height) at the Windows
presentation boundary. This is not a Camera-source normalization Blit, does
not use a shader, and does not alter or duplicate the mascot Camera-source
Y-normalization contract.

The Unity `ID3D12Device`, Direct command queue, frame fence, DXGI factory, and
composition device are borrowed and are never released or placed into owning
`ComPtr`s. The Unity RenderTexture resource passed to render event 9 is also
borrowed and never destroyed by native code. Managed code uses one reusable
`CommandBuffer`; the event payload is the texture pointer value and no
heap-backed transient payload is retained.

On the render thread, the Speech subsystem validates the source as 170×64
`B8G8R8A8_SRGB`, obtains the current Speech back buffer, records a
`CopyResource`, and submits through `IUnityGraphicsD3D12v8::ExecuteCommandList`.
Unity's resource-state array declares the source as `COPY_SOURCE` before and
after and the destination as `PRESENT` before and after. The Unity frame fence
is borrowed to detect completion. Only after completion does the UI thread
call `Present`. A 2-second in-flight timeout fails and hides the Speech
subsystem without changing the mascot pipeline. Present and device-removed
HRESULTs are retained for diagnostics.

## Shutdown and failures

The managed shutdown barrier rejects new Shows and stops timeout, click,
anchor, and render-event work. Native cleanup then drains an in-flight copy,
removes Speech input, releases Speech-owned D3D12/DComp resources, and destroys
the Speech HWND on the native UI thread before existing mascot composition
teardown. Managed cleanup then releases the CommandBuffer, TMP view,
RenderTexture, and Controller reference. Every stage is idempotent and also
supports never-shown and partial-initialization paths.

Speech failure is isolated: it records a stable failure stage and makes the
focused diagnostic fail, but does not stop the mascot runtime merely because
Speech cannot initialize or copy. Aggregate orderly cleanup includes the
native Speech cleanup result.

## Focused diagnostic and known limits

`message-window-diagnostic` covers initialization, fixed Japanese content,
coalescing, Busy rejection, click and timeout closes, first-close-wins, stale
generation rejection, more than 20 repeated Show/Hide cycles, stable resource
creation counts, independent texture dimensions, anchor source/generation,
position/edge counters, HRESULTs, HRGN ownership, and orderly cleanup.

Final manual verification passed in three separate launches:

- `hold`: full-body framing, Japanese readability, centered waist/thigh
  placement, mascot drag following, transparent-area click-through, and one
  card-body click close;
- `timeout`: one fixed 15-second diagnostic timeout close, no duplicate close,
  and normal mascot input after hiding;
- `shutdown`: Tray and mascot Settings routes, interactive Settings,
  `IFileOpenDialog` open/cancel, and Tray orderly exit while Speech remained
  visible. No Player, mascot, Speech, or Tray surface remained.

The shutdown log records picker button/command routing `True/True`, an STA
thread, `CoInitializeEx` result `S_FALSE`, `IFileOpenDialog::Show` cancellation
`0x800704C7`, native Speech cleanup/live/window/region `True/0/1/0`, active and
retired runtime handles `0/0`, pending owned region `0`, Present and device
removed `S_OK/S_OK`, readback errors `0`, continuous/region failures `0/0`,
aggregate cleanup `True`, and fatal failure stage `0`.

For manual validation only, `DESKTOP_MASCOT_SPEECH_MANUAL_CHECK=hold` keeps
the card available for click, drag-follow, and transparent-area checks;
`timeout` uses a fixed 15-second close; and `shutdown` holds the card while
the user selects the established Tray exit. These controls do not change the
product 12-second `SpeechClosePolicy` contract and introduce no product
keyboard input.

M-048 intentionally has one fixed visual theme, one fixed timeout, no queue,
no animation transition, no decoration, no persisted Speech position, and no
conversation provider. Per-monitor DPI scaling of the fixed 170×64 pixel
surface and richer card geometry are future presentation milestones, not
implicit M-048 behavior.
