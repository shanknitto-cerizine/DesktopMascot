# Cross-Platform Settings Presentation Foundation

Current architecture baseline: M-045

## Scope

M-045 keeps the Settings data model, validation, binding, and Unity IMGUI
surface platform-neutral while separating presentation requests from the
Windows Player-window implementation. This stage does not resize the Player,
create a native Settings window, add Android UI, or change the native mascot
pipeline.

## Presentation boundary

`ISettingsPresentationHost` is the common presentation boundary:

- `ShowSettings`
- `CloseSettings`
- `BringSettingsToFront`
- `IsSettingsVisible`
- `ApplyPresentationState`

The Windows implementation is the existing
`UnityPlayerWindowVisibilityController`. It remains the only owner of exact
`UnityWndClass` lookup and `DWMWA_CLOAK` state. `SettingsWindowController`
continues to own only Unity UI state and commands; it does not access HWND,
DWM, or Windows APIs.

Tray, native mascot context-menu, and Single Instance requests target the
presentation interface. The Windows VRM file picker continues to obtain its
owner through the Windows visibility controller because file-dialog ownership
is itself a platform-specific responsibility.

## Settings-only Player rendering

The native Camera pipeline remains unchanged:

```text
production Camera
-> CameraSourceTexture
-> one Camera-source Y-normalization
-> 256 x 256 NormalizedTransferTexture
-> D3D12/readback/HRGN/DirectComposition
```

The Player-only preview uses the existing separate Camera that renders
directly to the Player backbuffer. While Settings is visible, only this
preview Camera is disabled. The production Camera, active character root,
Animator, D3D12 transfer, readback, alpha-mask generation, HRGN publication,
and DirectComposition mascot remain active. Closing Settings re-enables the
same preview Camera; no RenderTexture is allocated or recreated.

This is smaller and safer than changing character layers, the production
Camera, or character root activation. It also keeps M-044 ownership and
render-safe disposal independent from presentation.

## Opaque Unity surface

While Settings is open, `SettingsWindowController` draws:

1. one full-Player opaque dark surface;
2. one opaque panel foundation under the IMGUI window;
3. the existing controls and commands.

Both colors have alpha 1. The opaque surface prevents the Player clear color
or any stale Player frame from showing through. It is drawn only while
Settings is logically open and does not clear, resize, or replace the native
transfer textures.

## Diagnostics

State-transition logging records:

- presentation host initialization;
- presentation requests;
- Settings visible and closed transitions;
- opaque background state;
- Settings-only rendering state;
- Player-preview character exclusion;
- DirectComposition Present progress;
- Close/Cancel counts;
- stable presentation failure stage.

No per-frame production logging is added.

## Invariants

- Unity 6000.3.20f1, D3D12, DirectComposition, and the native plugin are
  unchanged.
- Camera-source Y normalization remains exactly once at the source boundary.
- The 256 x 256 normalized transfer texture is unchanged.
- Runtime-character ownership and M-044 disposal are unchanged.
- Player visibility remains owned only by
  `UnityPlayerWindowVisibilityController`.
- Settings opens only through tray, mascot context menu, or Single Instance.
- Settings closes through Cancel or Close; F10 and Settings Escape are not
  product inputs.

## Deferred work

- final Player size and resizing;
- title-bar or borderless presentation;
- Win32, WPF, or WinUI Settings;
- Android presentation and file picker;
- UI Toolkit migration;
- final visual design and additional settings.
