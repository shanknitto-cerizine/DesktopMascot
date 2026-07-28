# Runtime VRM Import Foundation Design

## Scope

M-040 adds one opt-in Development-launch path for importing one external
VRM 1.0 character and activating it through the M-039
`CharacterAssetManager`. A launch without the explicit import argument keeps
the bundled `TEST_MODEL`, logical ID, and generation 1 unchanged.

M-040 does not add a file picker, settings field, persistent VRM path,
character-switching UI, provider registry, Addressables, AssetBundles, native
code, or another Camera/GPU path.

## Input boundary

The canonical Development launch is:

```powershell
.\Tools\Development\Run-DevelopmentPlayer.ps1 `
    -RuntimeVrmImportPath "<absolute-path-to-model.vrm>"
```

The launcher supplies both:

```text
--desktop-mascot-development-launch
--runtime-vrm-import=<absolute path>
```

The import argument is ignored without the Development-launch marker.
Neither value is stored in `settings.json`, `window-position.json`, or another
persistent file. Secondary instances retain the M-038 activation protocol and
do not forward a VRM path.

Input validation rejects empty and relative paths, missing files,
directories, non-`.vrm` extensions, unreadable or empty files, and files
larger than the constant 256 MiB limit. A content SHA-256 is computed before
UniVRM import and becomes the non-path logical source ID.

## Installed UniVRM contract

The repository resolves UniVRM 0.131.0 from:

```text
com.vrmc.vrm  v0.131.0
com.vrmc.gltf v0.131.0
lock hash: 3b99078d26b362733ad9bf463f98c83b8a1b4c9f
```

M-040 calls the installed API from the Unity main thread:

```csharp
await Vrm10.LoadPathAsync(
    path,
    canLoadVrm0X: false,
    controlRigGenerationOption:
        ControlRigGenerationOption.Generate,
    showMeshes: false,
    ct: cancellationToken);
```

`canLoadVrm0X: false` explicitly rejects VRM 0.x migration.
`showMeshes: false` keeps imported renderers hidden until validation and
activation commit complete.

The result is a `Vrm10Instance`. Its root also contains the imported
`Animator`, Humanoid Avatar, and `RuntimeGltfInstance`. The imported Animator
Controller is replaced with the existing bundled
`MascotAnimator.controller`; the replaced reference is retained until
activation succeeds so preparation and activation failure can roll back.

`Vrm10Instance.Runtime` is lazy and is not used as a structural failure
condition. Existing expression logic continues to observe runtime readiness.

## Source and ownership

`RuntimeVrmCharacterSource` owns:

- validated path state and the pre-import SHA-256;
- the cancellation source and one in-flight import task;
- the hidden, prepared `RuntimeGltfInstance` before activation;
- rejection of another load after start or shutdown;
- idempotent cleanup of an unactivated import.

Ownership after successful activation transfers exactly once to
`CharacterAssetManager` through `RuntimeImportedCharacterHandle`.
`CharacterDescriptor` is reference-only.

| Character/resource | Owner |
|---|---|
| Bundled Scene `TEST_MODEL` root | Unity Scene, borrowed by Manager |
| In-flight or prepared imported root | `RuntimeVrmCharacterSource` |
| Activated imported root | `CharacterAssetManager` |
| Descriptor | no GameObject/resource ownership |

The official release operation is:

```csharp
RuntimeGltfInstance.Dispose()
```

UniVRM then destroys the root and its transferred Texture, Material, Mesh,
Avatar, expression, and related imported resources through its
`OnDestroy` ownership path. M-040 does not destroy those objects separately.

## Preparation and activation

The imported root stays inactive while M-040:

1. validates `Vrm10Instance`, `RuntimeGltfInstance`, Animator, Humanoid Avatar,
   and the bundled Animator Controller;
2. applies the bundled root position, rotation, and scale;
3. verifies finite, non-empty renderer bounds and Camera-frustum intersection;
4. adds runtime-only `MascotCharacter`, expression, interaction, drag, and
   `BoxCollider` components;
5. derives the collider deterministically from imported renderer bounds in
   root-local space.

Camera framing is not changed. Automatic model scaling is deferred because
arbitrary clothing, hair, pose, and body proportions make a bounds-based
scale policy a separate visual milestone.

`TryActivateImportedCharacter` performs one synchronous, non-yielding commit:

```text
activate imported root while meshes remain hidden
-> verify Standing Idle on the live imported Animator
-> deactivate bundled root
-> ShowMeshes()
-> play Standing Idle
-> transfer ownership to CharacterAssetManager
-> replace descriptor
-> increment generation once
```

No frame can render both silhouettes. Any failure before ownership commit
disables the imported renderers, deactivates the imported root, restores the
bundled root and prior Animator Controller, leaves descriptor/generation
unchanged, and disposes the unactivated import.

M-040 adds no public command for switching back after successful activation.
The bundled descriptor is retained only as the transaction rollback target.

## GPU and presentation boundary

The imported model is another source rendered by the existing Camera. No
native resource refers directly to its Mesh, Material, or Texture.

The validated pipeline remains:

```text
Camera
-> Camera source RenderTexture
-> one Camera-source Y-normalization Blit
-> 256 x 256 normalized transfer RenderTexture
-> D3D12 copy/readback
-> alpha mask
-> HRGN / SetWindowRgn
-> DirectComposition
```

Activation does not restart or replace the Camera, D3D12 device, command
queue, readback resources, native window, region system, or composition swap
chain.

## Shutdown and cancellation

The installed UniVRM 0.131.0 source explicitly states:

```text
Current Vrm10Importer.LoadAsync implementation CAN'T ABORT.
```

Cancellation can reject work before import and is checked again after core
import, but it cannot forcibly interrupt the middle of Unity-object creation.
M-040 therefore uses cooperative shutdown:

```text
RuntimeVrmCharacterSource.BeginShutdown
-> reject new import/activation and request cancellation
-> wait for the import task to settle
-> stop readback and region publication
-> stop native composition
-> dispose Camera pipeline and restore targetTexture
-> RuntimeGltfInstance.Dispose for the active owned character
-> CharacterAssetManager descriptor cleanup
-> source cleanup
-> existing tray/visibility/Single Instance orderly completion
```

A 30-second wait is a diagnostic failure threshold, not a force-abort
timeout. If crossed, the failure is logged and cooperative waiting continues
so partially created Unity resources are not abandoned. No thread abort,
process termination, or early owned-root destruction is used.

The readback callback no longer initiates managed cleanup merely because
shutdown started. Owned character release occurs only after native shutdown
and Camera cleanup.

## Validation

Focused validation covers path categories, the exact 256 MiB boundary,
pre-import SHA-256, one-load behavior, source shutdown, import/preparation
results, source type, generation, component availability, rollback, ownership
transfer, and idempotent cleanup.

The actual integration launch imports the repository's valid VRM 1.0 through
`LoadPathAsync`; invalid launches must retain bundled generation 1. Existing
M-031 through M-039 tests, runtime-smoke, drag, real-animated, normal runtime,
and orderly-shutdown invariants remain required.

Until user visual verification passes:

```text
Status: Automated Validation Passed
Visual verification: Pending
Architecture baseline: M-039
```
