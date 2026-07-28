# Runtime Character Persistence and Startup Restoration

## Scope

M-042 persists only the currently selected runtime-imported VRM 1.0 source
needed for a later startup restore. It does not add a model library, recent
list, instant return-to-bundled operation, file copy, migration, network
access, or a settings-schema change.

The architecture baseline remains M-041 until manual verification passes.

## Responsibility boundary

`CharacterSelectionPersistenceManager` is the only character-persistence
owner. Its collaborators have deliberately smaller responsibilities:

- `CharacterSelectionStore`: directory initialization, bounded reads, atomic
  temporary writes, replacement, deletion, and temporary-file cleanup;
- `CharacterSelectionSerializer`: strict JSON, version, absolute-path, and
  SHA-256 validation;
- `CharacterSelectionRecord`: immutable values only;
- `RuntimeCharacterSelectionController`: startup-restore and manual-selection
  state machine;
- `CharacterAssetManager`: active and retired runtime-root ownership only;
- `CharacterDescriptor`: runtime references only, with no path ownership.

Character persistence is separate from `SettingsManager`,
`WindowPositionPersistence`, native composition, and Single Instance.

## Production record

The production file is:

```text
%LOCALAPPDATA%\DesktopMascotMinimal\character-selection.json
```

The schema is intentionally minimal:

```json
{
  "schemaVersion": 1,
  "selectionVersion": 1,
  "absolutePath": "C:\\path\\model.vrm",
  "contentSha256": "64 lowercase hexadecimal characters"
}
```

The absolute path is necessary to reopen a user-selected external file. It is
private persisted input: it is not copied to `settings.json`,
`window-position.json`, `CharacterDescriptor`, UI, or normal logs. The UI
derives and shows only a file name. Normal logs do not include the SHA value
or exception messages.

The serializer rejects blank or malformed JSON, missing, duplicate, or
unknown members, wrong types, negative or unsupported schema values,
unsupported selection versions, relative/device paths, paths with control or
malformed surrogate characters, non-`.vrm` paths, excessive path length, and
non-hexadecimal or non-64-character hashes. A record larger than 256 KiB is
not read.

Local absolute and UNC paths are permitted when the platform file APIs accept
them. Reparse points and removable/network storage are treated as external,
temporarily available input; existence, size, readability, and content hash
are checked again on every restore.

## Initialization and persistence availability

Only the primary normal-runtime process creates the production persistence
manager. Secondary processes exit at the M-038 startup gate first and never
read or write the character record. Explicit diagnostics do not touch the
production record. The development import diagnostic uses an isolated
temporary M-042 record.

`CharacterSelectionStore.Initialize()` calls `Directory.CreateDirectory` for
the record directory. A failure is classified as
`PersistenceUnavailable`. The bundled character still starts and remains
fully usable; persistence failure is not a fatal startup condition.

Structural record failures (`InvalidRecord` and `UnsupportedVersion`) are
distinguished from external or I/O failures (`ReadFailure` and
`PersistenceUnavailable`). M-042 preserves every failed record rather than
deleting or rewriting it automatically.

## Atomic save

Persistence runs only after a successful character activation transaction:

```text
Picker
-> preflight and SHA-256
-> UniVRM import and preparation
-> synchronous CharacterAssetManager activation
-> atomic character-selection save
-> UI result
```

The store writes UTF-8 to a uniquely named temporary file in the destination
directory, flushes managed and filesystem buffers, and then commits:

- initial record: `File.Move`;
- existing record: `File.Replace`.

M-042 intentionally has no delete-and-move fallback for an existing record.
If `File.Replace` is unavailable or fails, the commit is a non-fatal
`WriteFailure`, the previous valid record remains authoritative, and the
temporary file is cleaned up when possible. The newly activated runtime model
remains active; its generation and ownership are not rolled back.

Selecting the exact persisted path and content again suppresses a redundant
write. Selecting the same content through a different explicit path retains
the M-041 `AlreadyActive` behavior without import or generation change, but
updates the persisted path atomically so that the user's latest chosen
location can be restored.

## Startup restoration

Normal startup always establishes the safe bundled character first:

```text
BeforeSceneLoad
-> primary Single Instance gate
-> settings and character-persistence initialization

AfterSceneLoad
-> CharacterAssetManager validates and publishes BundledScene generation 1
-> runtime pipeline, preview, Settings, visibility, commands, and tray start
-> RuntimeCharacterSelectionController requests one startup restore
-> preflight and current SHA-256
-> hash equality check
-> UniVRM import, preparation, and synchronous activation
-> RuntimeImported generation 2
```

Manual selection is rejected while restore is pending or executing. A restore
request is issued at most once. Settings may display progress, but closing
Settings does not cancel restoration.

The stored SHA is compared before `Vrm10.LoadPathAsync`. A mismatch does not
import. Missing, unreadable, empty, oversized, invalid, VRM 0.x, preparation,
or activation failure also leaves the bundled root and generation 1 active.
These are non-fatal fallback results, and the record is preserved for an
explicit later retry or replacement.

## Clear-on-next-start command

`次回起動時は同梱モデルを使用` atomically removes only the character
selection record. It does not switch the current character, change the active
generation, alter Settings dirty state, or dispose an active root. The next
launch therefore remains at bundled generation 1. A later successful manual
selection creates a new record.

## Shutdown and ownership

The persistence shutdown barrier closes before selection/import shutdown. It
rejects new save and clear requests. An in-progress picker or UniVRM import
still follows the M-041 cooperative shutdown contract; no thread, dialog,
file handle, or process is forcibly terminated.

Native publication and Camera presentation stop before
`CharacterAssetManager` disposes its active and retired runtime handles.
Persistence cleanup only clears its in-memory reference; it never owns or
disposes a character root. Cleanup is idempotent.

## Validation

Focused validation covers:

- missing, valid, Japanese-path, malformed, strict-schema, oversized, and
  unsupported records;
- atomic initial create and replacement;
- simulated commit failure, previous-record preservation, and temporary-file
  cleanup;
- duplicate-write suppression and alternate-path update;
- unavailable directory initialization;
- shutdown write/clear rejection and idempotent cleanup;
- clear semantics and production-path isolation.

Integration validation covers bundled generation 1 followed by a matching
saved-record restore to one `RuntimeImported` generation 2 character, same-SHA
import suppression, continuous presentation, and orderly release of all
active and retired handles.

Manual validation is still required for persistence across two real launches,
fallback after moved or changed external files, the clear-on-next-start
command, UI privacy, animation, click-through, drag, Settings command routes,
and orderly exit.

## Known limitations and future work

- The record intentionally contains a private absolute local or UNC path.
- UniVRM 0.131.0 import cannot abort immediately.
- A temporarily unavailable external drive or share is not searched or
  remapped automatically.
- M-042 does not copy, move, delete, repair, or modify the VRM itself.
- Recent models, a model library, instant bundled switching, relocation
  assistance, license display, portable profiles, per-model framing, and
  render-safe early disposal of retired roots remain future work.
