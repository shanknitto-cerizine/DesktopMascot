# Conversation Evaluation Result Contracts Foundation

Milestone: M-050

Status: Automated Validation Passed — commit pending

Architecture baseline: M-046

## Objective and non-goals

M-050 adds the smallest pure-managed outcome contract for a future
conversation evaluation. It records whether one caller-supplied request
produced a `ConversationResponse` or had `NoMatch`; it does not evaluate a
request or invoke any service.

This milestone excludes evaluator/provider interfaces and implementations,
Rule Engine, Script Engine, Conversation Pack, Character or VRM binding,
Speech integration, production event wiring, Busy handling, queuing, retry,
priority, scheduling, AI, networking, filesystem work, persistence, Unity
runtime behavior, native code, D3D12, and DirectComposition changes.

## Contracts and validation

`ConversationEvaluationOutcome` has exactly two stable values:
`ResponseProduced = 1` and `NoMatch = 2`.

`ConversationEvaluationResult` is immutable and contains its outcome, the
caller-supplied `ConversationRequestId`, and an optional
`ConversationResponse`. Its public construction boundary is `TryCreate`.
Expected invalid input returns `false`, produces no result, and reports one
non-localized `ConversationEvaluationContractFailure` value.

For `ResponseProduced`, a non-null response is required and its `RequestId`
must equal the result `RequestId` using the existing Domain ordinal semantics.
For `NoMatch`, the response must be null. Unsupported enum values and default
or otherwise invalid request IDs are rejected.

The result makes no presentation admission claim. In particular, it neither
introduces nor interprets M-048 `SpeechShowResult.Busy`.

## Dependency and ownership boundary

The dependency direction is:

```text
Conversation.Evaluation
→ Conversation.Domain
→ basic System types
```

Evaluation source owns no Unity, Character, Speech, native, filesystem,
network, provider, queue, timer, worker, cache, or persistent resource. It
has no Domain-to-Evaluation reverse reference. The M-049 Domain source is
unchanged by M-050.

## Focused diagnostics

`DesktopMascotConversationEvaluationDiagnostics` is a deterministic pure
managed body. It verifies both valid outcomes, missing and unexpected
responses, request-response mismatch, invalid request ID, unsupported outcome,
and repeated construction.

`ConversationEvaluationDiagnosticsBuild` is Editor-only audit support. It
runs that body, scans Evaluation source for prohibited dependencies, and
verifies that Domain source has no Evaluation reference. Its batch entry point
passed in the Unity Editor batch host. The same deterministic body also passed
through Roslyn alongside Unity Development Player compilation and runtime-smoke
evidence.
