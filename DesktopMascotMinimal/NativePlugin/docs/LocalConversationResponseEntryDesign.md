# Local Conversation Response Entry Contract Foundation

Milestone: M-052

Status: Completed

Automated Validation: Passed

Final repository audit: Passed

Visual Verification: Not Applicable

Manual Interaction Verification: Not Required

Commit: `836dcaeaab7e34572143ee2994d2afdf68fb5c39`

Commit message: `M-052 establish local conversation response entry contract`

Remote protection: Completed

Architecture baseline: M-046

## Objective and non-goals

M-052 adds immutable, pure-managed local response material for a future local
conversation evaluator. A `LocalConversationResponseEntry` contains exactly a
trigger, response identity, speaker identity, text, and presentation intent.
It is not a production evaluator, an entry collection, or matching policy.

The entry deliberately has no request identity or character identity. A future
evaluator, outside M-052, supplies the caller's request correlation when it
constructs a `ConversationResponse`.

## Contract

```csharp
internal sealed class LocalConversationResponseEntry
{
    internal ConversationLogicalId TriggerId { get; }
    internal ConversationLogicalId ResponseId { get; }
    internal ConversationLogicalId SpeakerId { get; }
    internal string Text { get; }
    internal ConversationPresentationIntent PresentationIntent { get; }

    internal static bool TryCreate(
        ConversationLogicalId triggerId,
        ConversationLogicalId responseId,
        ConversationLogicalId speakerId,
        string text,
        ConversationPresentationIntent presentationIntent,
        out LocalConversationResponseEntry entry,
        out ConversationContractFailure failure);
}
```

Expected invalid input returns `false`, produces no entry, and reports the
existing stable non-localized `ConversationContractFailure`. The entry never
trims, normalizes, repairs, or generates values.

`TriggerId`, `ResponseId`, and `SpeakerId` must be valid
`ConversationLogicalId` values. Text and presentation-intent validation reuse
the existing `ConversationResponse` validation helpers without changing the
Domain contract's accepted inputs, rejected inputs, or failure semantics. The
only accepted presentation intent remains `CharacterUtterance`.

## Dependency and ownership boundary

```text
Conversation.Evaluation
→ Conversation.Domain
→ basic System types
```

The entry owns no Unity, Character, Speech, native, filesystem, network,
provider, task, thread, timer, queue, cache, persistence, or runtime resource.
The Domain has no Evaluation reference. The entry has no request ID, character
ID, Character descriptor, VRM identity, path, priority, weight, order,
condition, or presentation runtime state.

## Diagnostics

Focused deterministic diagnostics cover valid value preservation, invalid IDs,
the complete reused text and intent boundary, no repair, ordinal ID semantics,
and repeated deterministic construction. M-049, M-050, and M-051 focused
diagnostics remain regression coverage.

The Editor-only audit scans Evaluation dependencies, confirms the entry has no
request or character contract token, and confirms Domain has no Evaluation
reference. The Unity Editor batch host remains optional because of the known
Unity Licensing Client environment limitation.

Focused M-052 diagnostics and M-049, M-050, and M-051 regression diagnostics
passed through Unity Mono `mono.exe` with `lib/mono/4.5/csc.exe`; no network or
restore was used. The Unity Development Player managed build, dependency and
request-independence audit, required `.meta` audit, and duplicate-GUID audit
also passed. The optional Unity Editor batch host remains blocked by the known
Licensing Client environment limitation and is not recorded as passed.

## Deferred decisions

M-052 adds no production evaluator, exact-match logic, hardcoded dialogue,
collection, registry, duplicate or ordering policy, priority, weighting,
randomness, Character binding, Rule, Script, Conversation Pack, Speech
adapter, Busy policy, async execution, persistence, Settings, AI/provider,
native implementation, or production event wiring.
