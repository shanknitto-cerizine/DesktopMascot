# Runtime VRM Selection UI Design

## Scope

M-041 adds a non-persistent Character section to the existing normal-runtime
Settings overlay. The user can select one VRM 1.0 file, import it through the
M-040 path, and replace the active character without restarting Camera,
D3D12, readback, HRGN, the native mascot window, or DirectComposition.

Character selection is an immediate runtime operation. It is not part of the
persistent Settings ViewModel. Apply, Cancel, Close, Escape, dirty state, and
Restore Defaults retain their M-034 meanings. Neither `settings.json` nor
`window-position.json` receives a character path, ID, source type, or recent
file.

## File picker

`WindowsVrmFilePicker` is a managed Windows-only Common Item Dialog boundary.
It creates `IFileOpenDialog` on one dedicated background STA thread, balances
`CoInitializeEx(COINIT_APARTMENTTHREADED)` with `CoUninitialize`, and returns
only a result DTO to the Unity main thread.

The dialog:

- uses the current process's exact `UnityWndClass` as owner;
- never uses the native mascot or tray owner window;
- filters `*.vrm`;
- requires an existing file-system item and path;
- uses UTF-16 COM strings;
- does not add the selected item to Recent Documents;
- does not call Unity APIs from its STA thread.

The picker never controls Player visibility. The existing
`UnityPlayerWindowVisibilityController` makes the Player interactive and
returns the revalidated exact owner handle.

Shutdown does not force-destroy an open dialog. New requests are rejected,
the result is discarded if it returns after the shutdown barrier, elapsed
dialog wait time is logged, and orderly shutdown continues only after the
user closes or cancels the dialog.

## Runtime selection controller

`RuntimeCharacterSelectionController` lives on the normal runtime owner and
outlives any individual Settings opening. It owns:

- the explicit state machine;
- one picker operation;
- one preflight/import operation;
- duplicate request rejection;
- user-safe status text;
- the selection shutdown barrier;
- operation cleanup.

States are:

```text
Idle
SelectingFile
Importing
Activating
Succeeded
Failed
Shutdown
```

Only one picker or import can be active. A second request is rejected; M-041
does not implement latest-wins. Closing Settings does not cancel import.
Reopening the same Settings controller renders the current operation state.
Single Instance activation can only reopen that same Settings surface and
does not transfer a VRM path or create another picker.

## Preflight and privacy

M-040 path validation and SHA-256 calculation are exposed as a reusable
preflight result. The absolute path exists only in that transient operation
result. It is not stored in a descriptor, persistent file, normal log, or UI.
The UI displays only `Path.GetFileName` output.

The controller compares the preflight SHA-based logical source ID with the
active descriptor before calling UniVRM. Identical content, even at another
path, returns AlreadyActive:

```text
LoadPathAsync calls: unchanged
active root: unchanged
generation: unchanged
owned handles: unchanged
```

Exception messages are not written by the selection/import boundary because
they can contain a selected path. Logs contain only stable status and
exception type.

UniVRM 0.131.0 with `canLoadVrm0X: false` reports both VRM 0.x and other
non-VRM-1.0 inputs through a generic load exception. M-041 intentionally maps
both to the same user-facing unsupported-model message rather than adding a
second low-level parser.

## Activation and ownership

Preparation and activation reuse M-040 unchanged:

```text
showMeshes:false import
-> structural preparation while hidden
-> final shutdown check
-> synchronous non-yielding activation
-> previous root inactive
-> ShowMeshes on the new root
-> descriptor/ownership/generation commit
```

Failure keeps the previously active bundled or runtime-imported character,
generation, and ownership unchanged.

Successful runtime-to-runtime replacement transfers the new handle to
`CharacterAssetManager`. The prior runtime root becomes inactive and remains
Manager-owned in `retiredOwnedRuntimeCharacters`. Exactly one character root
is active. The Manager publishes active, retired, and total handle counts for
diagnostics.

The active handle and every retired handle are disposed only after native
composition and the Camera pipeline stop. Cleanup must report active/retired
counts `0/0`.

## Known notes

- Retired runtime roots retain their imported meshes, materials, textures,
  Avatar, and related memory until orderly shutdown. This deliberately
  preserves the M-040 release-after-pipeline-stop contract.
- UniVRM 0.131.0 core import cannot abort immediately. Import shutdown remains
  cooperative with the existing 30-second diagnostic threshold.
- An open Common Item Dialog is also cooperative during shutdown. Orderly exit
  can wait for the user to close or cancel it.
- The available integration fixture has the same visible character content as
  the bundled model. Source type, generation, Animator, Vrm10Instance, active
  root, and ownership counters provide the authoritative activation evidence.

## Known future work

- render-safe early retirement of inactive imported roots;
- persisted character selection and startup restoration;
- a command to return to the bundled character;
- recent-model and model-management UI;
- VRM license information;
- automatic model framing;
- non-Windows file pickers.

## Validation

Focused and integration validation must cover:

- picker filter, owner, cancellation, duplicate request rejection, and
  shutdown-result discard;
- Settings dirty-state and schema independence;
- identical SHA skip before `LoadPathAsync`;
- import/validation/activation failure preserving the current root;
- at least three consecutive successful activations;
- exactly one active root after every commit;
- active/retired ownership counts before and after shutdown;
- continuous Present, readback, mask, HRGN, click-through, drag, Settings,
  tray, visibility, Single Instance, and orderly-shutdown regressions.

Until manual verification:

```text
Status: Automated Validation Passed
Visual verification: Pending
Architecture baseline: M-040
```
