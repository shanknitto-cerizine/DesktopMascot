# Single-Entry Exact-Match Local Conversation Evaluator

Milestone: M-053

Status: Completed

Implementation: Complete

Automated Validation: Passed

Visual Verification: Not Applicable

Manual Interaction Verification: Not Required

Final Repository / Staged Audit: Passed

Commit: `171c3a28f773765742c51d8e800ef0b32ed6ab74`

Commit message: `M-053 establish single-entry exact-match local conversation evaluator`

Push / Remote Protection: Completed

Architecture baseline: M-046

## Objective and non-goals

M-053 adds one production-usable local evaluator which owns exactly one
validated `LocalConversationResponseEntry`. It compares only
`request.Event.TriggerId` and `entry.TriggerId` through the existing ordinal
`ConversationLogicalId` equality. A match produces one correlated response;
a non-match produces one correlated `NoMatch`.

Production-usable evaluator implementation is not production runtime
integration. This milestone adds no bootstrap registration, event source,
active-character binding, automatic conversation, Speech adapter, or Settings
enablement.

M-053 excludes collections, multi-entry selection, duplicate handling,
ordering, priority, weighting, randomness, fallback routing, CharacterId
matching, speaker/character equality, Conversation Pack, Rule, Script,
provider, async work, persistence, filesystem, networking, and presentation.

## Contract

```csharp
internal sealed class SingleEntryLocalConversationEvaluator
    : ILocalConversationEvaluator
{
    internal SingleEntryLocalConversationEvaluator(
        LocalConversationResponseEntry entry);

    public ConversationEvaluationResult Evaluate(
        ConversationRequest request);
}
```

Construction requires exactly one non-null entry. A null entry throws
`ArgumentNullException(nameof(entry))`. The evaluator borrows the immutable
entry reference; it neither copies nor owns it, performs no repair or
normalization, and creates no global state.

A valid M-051 request is the only evaluation input. On a non-match, the result
is `NoMatch`, carries the input `RequestId`, and has no response. On a match,
the evaluator constructs a `ConversationResponse` using the request ID plus
the entry's response ID, speaker ID, text, and presentation intent, then
returns a correlated `ResponseProduced` result.

The validated request and validated entry make response/result construction
failures unreachable in normal operation. Such a failure throws a
non-localized `InvalidOperationException` without request, character, ID, or
text content. It is an internal invariant violation, not a new operational
result, retry, fallback, or exception-transport contract.

`CharacterId` remains request context only. It is not a matching key, and
M-053 defines no `SpeakerId == CharacterId` rule.

## Dependency and ownership boundary

```text
SingleEntryLocalConversationEvaluator
├─ ILocalConversationEvaluator
├─ LocalConversationResponseEntry
├─ ConversationEvaluationResult
└─ Conversation.Domain
   └─ basic System types
```

The Domain has no Evaluation dependency. M-053 owns no Unity, Character,
Speech, native, filesystem, network, provider, task, cancellation, timer,
thread, queue, cache, Settings, or persistent resource.

## Diagnostics and validation

Focused deterministic diagnostics verify matching and non-matching results,
full response value correlation, CharacterId independence, deterministic
repetition, unchanged request/entry values, and null-entry rejection.

M-049 Domain, M-050 Evaluation Result, M-051 Local Evaluator, M-052 Response
Entry, and M-053 diagnostics passed through Unity Mono `mono.exe` with
`lib/mono/4.5/csc.exe`; no network or restore was used. The M-053 source audit
passed for prohibited dependencies, single-entry tokens, and the
Domain-to-Evaluation reverse-reference boundary. Unity Development Player
managed/project compilation passed with only the established
`TransparentWindowController.borderless` CS0414 warning.

The required `.meta` and duplicate-GUID audit passed. The complete final
staged audit used exactly 10 M-053 intended files; `git diff --check` and
`git diff --cached --check` passed, and no unexpected or generated files were
present.

The optional Unity Editor-hosted diagnostic remains blocked by the known Unity
Licensing Client environment limitation and is not recorded as passed.

Validation history: the Unity build temporarily rewrote trailing whitespace
only in the unrelated `NotoSansCJKjp-Regular SDF.asset` Speech font asset.
`git diff --ignore-space-at-eol`, `git diff -w`, and normalized YAML comparison
confirmed no semantic, YAML-value, structure, reference, glyph, atlas, or font
data change. The asset was restored to HEAD before the final audit; it matches
HEAD and was not staged.

## Deferred decisions

M-053 does not decide multi-entry evaluation, selection policy, Character
binding, Rule/Script/Pack behavior, runtime wiring, Speech admission or Busy
policy, AI/provider behavior, async/cancellation, persistence, history,
cadence, or scheduling.
