# Conversation Runtime Request Identity and Character Binding Design Gate

Milestone: M-054

Status: Completed

Architecture baseline: M-046

Speech-specific design baseline: M-047.5

## Purpose and scope

M-054 records the production identity policy for a future Conversation runtime
integration. It is a design gate only. It adds no implementation, source type,
persistence, production wiring, or presentation behavior and does not change
the M-049 through M-053 contracts.

## Character conversation identity

The initial and default production Conversation identity is:

```text
desktop-mascot.character.default
```

This is an explicit Conversation identity binding and a permanent default
conversation persona identity. It is independent of visual Character identity.
Bundled and runtime-imported visuals use the same fallback, which remains
stable across Character switches, VRM reimports, and process restarts.

The binding must not expose a path, filename, display name, SHA-256 or other
content hash, `CharacterDescriptor.InternalId`, or Character generation to
Conversation. `CharacterAssetManager` gains no Conversation responsibility.
The binding has no persistence. A future explicit Conversation Pack binding is
a separate milestone; the default identity is not a temporary migration
identity.

## Conversation request identity

Production `ConversationRequestId` values use this form:

```text
request-<unsigned-decimal>
```

The production Conversation integration owner privately owns one `ulong`
counter. It starts at `request-1` and provides uniqueness within one normal-
runtime process lifetime. A later process may reuse `request-1`.

The owner allocates an ID only at the main-thread request-acceptance boundary.
Event sources and native code do not generate request IDs. Allocation is
independent of Character identity and never includes a path, hash, or private
content. An allocated value is never reused after a downstream failure.

`ulong.MaxValue` may be issued once. After that allocation, the owner must
reject further requests rather than wrap. The counter is not persisted. M-054
adds no dedicated generator interface and no global or static allocator.

## Architecture consequences

M-054 requires no implementation or source changes, new types, persistence,
`CharacterAssetManager` changes, Conversation Domain changes, or Speech
changes. Architecture baseline M-046 and Speech-specific design baseline
M-047.5 remain unchanged.
