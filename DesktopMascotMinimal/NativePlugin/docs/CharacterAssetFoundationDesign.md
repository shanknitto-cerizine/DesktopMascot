# Character Asset Foundation Design

## Scope

M-039 adds the upstream ownership boundary that selects the character used by
normal runtime. It does not add external file selection, runtime VRM import,
character switching, persistence, or UI.

The validated downstream pipeline remains:

```text
Unity Camera
→ Camera source RenderTexture
→ one Camera-source Y-normalization Blit
→ normalized transfer RenderTexture
→ D3D12 copy/readback
→ alpha mask
→ HRGN
→ SetWindowRgn
→ DirectComposition
```

## Current bundled character

`MascotMain.unity` contains one generated VRM prefab instance sourced from:

```text
Assets/_Project/Models/VRM/TEST_MODEL.vrm
GUID: b2c4fdc826a0d7e4aa1efd906ba4780d
```

The Scene root is named `TEST_MODEL`. Its existing Transform, Animator,
Animator Controller, `Vrm10Instance`, `MascotCharacter`, expression,
interaction, drag, collider, and Camera references are retained without
modification.

The byte-identical file below also exists but is not referenced by the Scene:

```text
Assets/_Project/Scripts/TEST_MODEL.vrm
GUID: da9fbec19d6e818488c9e0d4083daefd
```

Both copies currently have SHA-256
`140DEC9A688716150C49E3C5FEFFAFC42682B64BC26DC54E49843DF9C411C4DF`.
The second file is recorded as an unused duplicate asset. M-039 does not
move, delete, replace, or consolidate it.

## Normal-runtime startup boundary

Character discovery occurs at the beginning of the normal-runtime
`AfterSceneLoad` callback:

```text
Scene load
→ CharacterAssetManager.GetOrCreateProduction
→ structural bundled-character initialization
→ only on success:
   runtime pipeline / Player preview / Settings / visibility /
   context menu / tray
```

`runtime-smoke`, `drag-diagnostic`, `real-static-diagnostic`, and
`real-animated-diagnostic` do not create the production manager. Existing
diagnostic Camera and Animator discovery remains unchanged.

If structural initialization fails, no runtime pipeline, Player-preview
Camera, mascot composition surface, Settings controller, context menu, or tray
controller is created. The generic primary-startup abort path closes the
single-instance acceptance barrier, releases the primary coordinator, posts
the established exact `UnityWndClass` orderly close when available, and calls
`Application.Quit`. It does not use a character-specific single-instance
contract or force process termination.

## CharacterAssetManager

`CharacterAssetManager` is the only owner of the active-character selection
in normal runtime. The production instance is process-local and guarded by a
static production slot and shutdown barrier:

- repeated bootstrap access returns the same manager;
- repeated initialization returns the same descriptor;
- successful bundled activation sets generation 1 once;
- the manager never instantiates a second character;
- cleanup is idempotent;
- the production slot cannot be recreated after its shutdown barrier closes.

The manager searches `MascotCharacter` components, including inactive
objects, and requires exactly one candidate. Selection does not depend on the
GameObject name or `GetInstanceID`.

Structural failure states are explicit:

- no candidate;
- multiple candidates;
- missing `Vrm10Instance`;
- missing `Animator`;
- missing Animator Controller;
- inactive root;
- initialization after shutdown.

`Vrm10Instance.Runtime == null` is not a structural failure. UniVRM can
populate it after Scene load. Runtime readiness remains a live descriptor
property for observation.

## CharacterDescriptor

The descriptor is read-only and contains:

- logical internal ID:
  `desktop-mascot.bundled-default`;
- display name;
- source type `BundledScene`;
- borrowed root GameObject;
- `MascotCharacter`;
- `Animator`;
- `Vrm10Instance`;
- root instance ID for same-instance diagnostics only;
- structural availability;
- live VRM runtime readiness.

No value is added to `settings.json`, `window-position.json`, or another
persistent schema.

## Ownership and cleanup

The bundled Scene GameObject is borrowed. The manager does not call
`Instantiate`, `Destroy`, `AddRef`, `Release`, or otherwise assume ownership
of the Scene model or UniVRM runtime.

At orderly shutdown:

1. the manager closes its activation barrier;
2. existing native, readback, region, composition, Camera, preview, tray, and
   menu cleanup continues in the validated order;
3. the manager clears its borrowed descriptor references idempotently;
4. the Scene remains Unity-owned.

M-039 owns only selection state, the descriptor, generation, and shutdown
state. It owns no native, GPU, COM, GDI, filesystem, VRM asset, or Scene
GameObject resource.

## Future runtime-import extension point

A future explicit milestone may add a source/provider above the manager:

```text
external VRM importer
→ imported root plus explicit ownership metadata
→ CharacterAssetManager activation boundary
→ CharacterDescriptor
→ existing Camera and native pipeline
```

The source type and ownership contract must distinguish borrowed
`BundledScene` roots from future manager-owned runtime-imported roots. An
owned imported root would be destroyed only after render/readback/native work
has stopped. M-039 does not implement that provider, import, replacement,
selection, persistence, or UI.

## Validation

Focused validation covers:

- diagnostic-mode separation;
- one bundled Scene candidate;
- exact existing root, Animator, and `Vrm10Instance`;
- stable logical ID;
- idempotent initialization and generation;
- missing, duplicate, and missing-reference failures;
- failure gating before runtime-surface creation;
- idempotent cleanup;
- activation rejection after shutdown;
- absence of character-foundation persistence.

The normal runtime additionally logs production manager count, initialization
status, source, generation, structural references, live VRM runtime readiness,
and character cleanup result.

Full M-031 through M-038 and production rendering regressions remain required.
Until user visual verification passes, M-039 remains:

```text
Status: Automated Validation Passed
Visual verification: Pending
Architecture baseline: M-038
```
