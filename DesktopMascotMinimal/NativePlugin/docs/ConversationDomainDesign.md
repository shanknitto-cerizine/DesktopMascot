# Conversation Domain Contracts Foundation

Milestone: M-049

Status: Automated Validation Passed

Architecture baseline: M-046

## Objective and non-goals

M-049 creates the smallest shared conversation vocabulary independent of AI,
Rule, Script, Speech, Unity, Windows presentation, and native code. It does
not create production dialogue or connect any event source to that vocabulary.

M-049 excludes Rule Engine, Script Engine, Conversation Pack, Speech adapter,
production event wiring, mascot click or drag integration, startup/idle/time
scheduling, AI Provider, Text SE, typing, Settings, persistence, schema
changes, native code, D3D12, DirectComposition, Scene, Prefab, and VRM work.

## Ownership and lifetime

`ConversationEvent`, `ConversationRequest`, and `ConversationResponse` are
immutable managed data. They own no Unity, native, filesystem, GPU, network,
Character, or Speech resource and require no cleanup. Callers hold them only
for their evaluation or integration lifetime. The domain creates no queue,
registry, cache, worker, timer, clock, GUID, random value, or global counter.

The domain is safe for concurrent reads because its data is immutable. It does
not define a threading, ordering, priority, or scheduling policy.

## Identifier contract

`ConversationLogicalId` is the stable semantic identity used for trigger,
character, response, and speaker IDs. `ConversationRequestId` is a distinct
caller-supplied correlation ID for one evaluation request. The domain never
generates a request ID.

Both types accept only strings that are 1 through 128 characters and contain
lowercase ASCII letters, digits, `-`, and `.`. A leading dot, trailing dot,
empty segment, or consecutive dots is invalid. Equality and hash use ordinal
semantics. Inputs are never trimmed, lowercased, or Unicode-normalized.

`desktop-mascot.` is product-reserved. M-049 defines no registry, namespace
ownership policy, alias, wildcard, hierarchy, or pack loader.

## Domain contracts

### ConversationEvent

`ConversationEvent` represents the semantic kind of one conversation trigger.
It has exactly one required field: `TriggerId`. It deliberately has no
timestamp, mouse position, button, idle duration, payload dictionary, priority,
sequence, or platform object.

### ConversationRequest

`ConversationRequest` asks a future evaluator to handle one event for one
logical character. Its required fields are `RequestId`, `Event`, and
`CharacterId`. It cannot contain `CharacterDescriptor`, GameObject, character
generation, VRM path/hash, HWND, or another Unity/native reference.

### ConversationResponse

`ConversationResponse` is a domain statement that a character wants to give
an utterance. Its required fields are `RequestId`, `ResponseId`, `SpeakerId`,
`Text`, and `PresentationIntent`. M-049 supports exactly one intent:
`CharacterUtterance`.

The response does not mean that a presentation accepted it. It contains no
Speech HWND, SpeechMessage, Speech theme, timeout, transition, animation,
expression, audio, Text SE, priority, provider metadata, or UI implementation.

Text is required, must not be whitespace-only, and is limited to 1,024 UTF-16
code units. CR, LF, and TAB are allowed; other control characters and unpaired
surrogates are rejected. Text is not trimmed or newline-normalized. This bound
does not promise that text fits the M-048 Speech card.

## Validation and failures

All public construction paths are `TryCreate` factories. Expected invalid
input does not throw and never produces a valid object. Failures use the
non-localized `ConversationContractFailure` enum:

```text
None
MissingRequiredValue
InvalidRequestId
InvalidTriggerId
InvalidCharacterId
InvalidResponseId
InvalidSpeakerId
EmptyText
TextTooLong
InvalidText
UnsupportedPresentationIntent
```

Failure values are diagnostics only; they must not embed user text or become
localized UI messages. A missing response is not represented by an empty or
invalid `ConversationResponse`; a future evaluator result contract owns
`NoMatch` and similar semantics.

## Built-in trigger IDs

The primary taxonomy is stable logical IDs, not an enum:

```text
desktop-mascot.trigger.mascot-click
desktop-mascot.trigger.drag-start
desktop-mascot.trigger.drag-end
desktop-mascot.trigger.startup
desktop-mascot.trigger.idle
desktop-mascot.trigger.time-based
desktop-mascot.trigger.explicit-request
```

`ConversationTriggerIds` provides only these constants. Defining these IDs does
not wire production sources or authorize their automatic use.

## Concurrency and Busy boundary

M-049 owns no priority, queue, scheduler, drop, coalesce, defer, deadline,
timestamp, or sequence policy. Those are future runtime policy decisions.

M-048 `SpeechShowResult.Busy` is a presentation admission result. It is not a
ConversationResponse state. A response can be generated without being shown;
drop, pending, retry, or alternate-presentation behavior is deferred to a
Speech integration milestone.

## Dependency and performance boundary

Domain source may use only basic `System` types. It must not reference
UnityEngine, MonoBehaviour, GameObject, Camera, RenderTexture, HWND, HRGN,
D3D12, DirectComposition, DXGI, Win32, Android lifecycle, AI SDKs, vendor/model
names, filesystem, VRM paths, `CharacterDescriptor`, or `SpeechMessage`.

It has no Update/LateUpdate, polling, background thread, timer, network,
filesystem, history, queue, retry, cache, or native/GPU resource. Validation
is linear in the supplied ID or text length.

## Focused diagnostics

`DesktopMascotConversationDomainDiagnostics` is a pure deterministic body that
validates all built-in IDs, valid and invalid contracts, text boundaries,
ordinal ID semantics, request-response correlation, and repeated validation.
It uses no filesystem, Unity API, or external state.

`ConversationDomainDiagnosticsBuild` is an Editor-only validation layer. It
runs the pure body and separately scans Domain source for forbidden Unity,
Windows/native, Speech, Character, AI, and filesystem dependencies. No Player,
asmdef, or Unity Test Framework dependency is required for the focused contract
test.

The Editor batch entry point remains available as an optional execution host.
Direct Unity `-executeMethod` execution was unavailable in this environment
because the `com.unity.editor.headless` entitlement could not be resolved; the
failure occurred before entry-point execution. The same deterministic
diagnostic body passed through the installed Roslyn compiler, while Unity
project compilation and runtime-smoke independently passed. These equivalent
validation results are accepted for M-049 completion. Editor batch host
success is not a unique correctness requirement for this pure-managed
milestone. The headless-license failure remains recorded as an environment
limitation, not a domain validation failure.

## Deferred decisions

Conversation Pack-to-VRM binding, Script DSL, runtime concurrency policy,
Speech Busy handling, cadence, pack storage/schema/trust, history, AI fallback,
and animation/expression/sound intents remain outside M-049. Future additions
must preserve this domain's ownership and dependency boundaries.
