# Immediate Bundled Character Restoration

## Scope

M-043 adds an immediate Settings command that returns the active
runtime-imported character to the existing bundled Scene `TEST_MODEL`.
It does not instantiate or reload a bundled model, dispose a runtime root
early, change character framing, or modify the native presentation pipeline.

The architecture baseline remains M-042 until manual verification passes.

## Responsibility boundary

The request follows this path:

```text
SettingsWindowController
-> RuntimeCharacterSelectionController
-> CharacterAssetManager.TryActivateBundledCharacter
-> synchronous activation transaction
-> CharacterSelectionPersistenceManager.TryClear
-> stable UI result
```

`SettingsWindowController` only renders state and forwards input.
`RuntimeCharacterSelectionController` owns operation exclusion, state,
shutdown rejection, persistence sequencing, and user-facing status mapping.
`CharacterAssetManager` remains the only component that changes the active
character Descriptor or runtime-root ownership.

## Bundled identity and ownership

The bundled root is the existing `TEST_MODEL` Prefab instance in
`MascotMain.unity`. The Scene owns it. `CharacterAssetManager` borrows the
same immutable Descriptor created at generation 1 and retains that Descriptor
while runtime-imported characters are active.

Runtime activation makes the bundled root inactive but does not disable its
individual Renderer, Animator, or `Vrm10Instance` components and does not
destroy or dispose it. Re-activation therefore uses the retained root and
Descriptor; no new Descriptor, Prefab instance, Scene reload, or VRM import is
created.

## Bundled validation

Validation runs while the bundled root remains inactive and checks:

- retained bundled Descriptor, root, and `MascotCharacter`;
- `BundledScene` source identity;
- valid loaded Scene through `Scene.IsValid()` and `Scene.isLoaded`;
- retained Animator and `Vrm10Instance` references;
- enabled Animator and `Vrm10Instance`;
- Animator Controller availability;
- `MascotExpressionController` and `MascotInteractionController`;
- at least one enabled Renderer.

`Vrm10Instance.Runtime == null` alone is not a structural failure.

An inactive Animator can report `isInitialized == false` and cannot reliably
answer `HasState`. Therefore Animator initialization and the
`Base Layer.Standing Idle` state are checked immediately after the root
becomes active and before `Animator.Play`.

## Activation transaction

Runtime Imported to Bundled uses one synchronous main-thread transaction:

```text
validate retained Bundled references while inactive
-> reserve retired-handle capacity
-> recheck shutdown barrier
-> deactivate current Runtime root
-> activate Bundled root
-> verify Animator active, initialized, and Standing Idle available
-> play Standing Idle from normalized time 0
-> move prior active Runtime handle to retired ownership
-> publish retained Bundled Descriptor
-> increment generation exactly once
```

There is no `await`, yield, or frame boundary. Deactivating the old root
before activating the bundled root prevents two logical active characters.
The short in-memory interval with neither root active cannot produce a
rendered empty frame because the whole commit occurs in one continuation.

Before commit, any validation or shutdown failure leaves the current runtime
character unchanged. A commit exception restores both roots, active
Descriptor, active handle, retired list, and generation.

When Bundled is already active, the Manager returns
`BundledCharacterAlreadyActive` without changing a root, Descriptor,
generation, or handle.

## Runtime-handle retirement

After a successful Runtime-to-Bundled transaction:

```text
Active source: BundledScene
Active Runtime handle count: 0
Retired Runtime handle count: prior count + 1
Active character root count: 1
```

The retired root remains inactive. Its Animator, SpringBone, and MonoBehaviour
updates stop, but its owned UniVRM resources remain allocated. It is not
reused by a later explicit selection and is disposed only after native
composition and the Camera pipeline stop through the existing
`ReleaseActiveOwnedCharacterAfterPipelineStop` path.

## Animation and expression lifecycle

The bundled Animator retains the existing `MascotAnimator` Controller.
Restoration explicitly plays `Base Layer.Standing Idle` from the start.
`Animator.Rebind`, CrossFade, controller replacement, and UniVRM Runtime
recreation are not used.

`MascotExpressionController` now has one shared initialization/resume path.
`OnEnable` and `Start` converge on it, and separate initialization and blink
Coroutine guards prevent duplicate execution. `OnDisable` stops both tracked
Coroutines. A bundled root that was inactive resumes automatic blinking when
it becomes active again.

## Persistence ordering

Persistence is not part of the character activation transaction:

```text
Bundled activation success or AlreadyBundled
-> CharacterSelectionPersistenceManager.TryClear
-> UI result
```

Activation failure never clears the record. Clear failure is non-fatal:
Bundled remains active, generation and retired ownership remain committed,
and the old record remains authoritative for a possible restore on the next
launch. No rollback to the prior runtime character occurs.

If Bundled is already active, a stored record is cleared without character
mutation. With no stored record the command is a complete no-op.

The existing next-start-only command remains available as a secondary action:
it clears the record while preserving the current character.

## Operation exclusion and shutdown

The existing `RuntimeCharacterSelectionController` owns the Bundled request.
One shared gate covers picker, preflight, import, activation, persistence
save/clear, and synchronous Bundled activation. Restore, import, a concurrent
request, and shutdown reject the command; no latest-wins behavior or request
queue is added.

The persistence barrier still closes before selection and Manager shutdown.
If activation committed before persistence closes but clear is rejected,
Bundled remains active and shutdown continues with the previous record.
Retired handles still reach zero only after the Camera and native pipeline
have stopped.

## Privacy and isolation

The operation does not display or log an absolute path or SHA, does not place
a path in a Descriptor, and does not copy, modify, or delete the external VRM
file. It only removes `character-selection.json` after successful character
activation. `settings.json` and `window-position.json` are unchanged.

Runtime import preparation logs only exception type and a stable status; it
does not log `exception.Message`.

## Validation

The M-043 focused diagnostic validates:

- retained bundled root and Descriptor;
- Scene loaded, Animator initialized, Standing Idle, and blink restart;
- validation-failure rollback;
- Runtime-to-Bundled source, generation, root count, and ownership changes;
- non-fatal persistence-clear failure;
- AlreadyBundled record clear and repeated no-op;
- busy and shutdown rejection;
- settings, position, and external VRM content invariance;
- final active and retired handle release to zero.

Native C++, CMake, exports, D3D12, readback, HRGN, and DirectComposition are
unchanged, so native build and dumpbin validation are not required for M-043.

## Future work

- render-safe early disposal of retired runtime roots;
- recent-character and model-library UI;
- bundled-character variants;
- model thumbnails and VRM license UI;
- per-model framing;
- transition animation or crossfade;
- removal of the brief Bundled-first startup presentation.
