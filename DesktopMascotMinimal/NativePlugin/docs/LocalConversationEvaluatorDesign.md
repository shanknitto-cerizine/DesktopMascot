# Synchronous Local Conversation Evaluator Contract Foundation

Milestone: M-051

Status: Implementation Complete — Automated Validation Passed

Final repository audit: Pending

Commit: Pending

Visual Verification: Not Applicable

Manual Interaction Verification: Not Required

Architecture baseline: M-046

## Objective and non-goals

M-051 adds the smallest local, synchronous, bounded invocation boundary for a
future conversation evaluator. It does not add a production evaluator,
dialogue content, matching policy, runtime event wiring, or a presentation
adapter.

The contract is deliberately local only. It does not require future
asynchronous or network-backed providers to implement the same interface.

## Contract

```csharp
internal interface ILocalConversationEvaluator
{
    ConversationEvaluationResult Evaluate(ConversationRequest request);
}
```

A valid invocation receives a non-null, successfully constructed
`ConversationRequest` and returns one non-null
`ConversationEvaluationResult`. The result `RequestId` equals the input
request `RequestId`.

`NoMatch` means the local evaluation completed successfully without a
response. `ResponseProduced` means a valid correlated response was produced.
Neither result admits, invokes, or displays Speech or any presentation.

Evaluation is synchronous and bounded. This contract owns no I/O, scheduler,
timer, worker, queue, retry, cache, filesystem, network, or provider
lifecycle semantics.

## Dependency boundary

```text
Conversation.Evaluation
→ Conversation.Domain
→ basic System types
```

The M-049 Domain has no reverse dependency on Evaluation. The evaluator
contract has no Unity, Character, Speech, native, Windows, filesystem,
network, AI, Pack, Rule, Script, MonoBehaviour, or frame-loop dependency.

## Diagnostics

The deterministic diagnostic-only implementations produce the two valid
outcomes. Focused diagnostics verify correlation, response presence rules,
deterministic repeated calls, and unchanged M-049/M-050 object state. They
are not production evaluators and are not connected to runtime or Speech.

Focused M-051 diagnostics passed, including correlated `NoMatch`, correlated
`ResponseProduced`, response-presence invariants, request-ID correlation, and
deterministic repeated evaluation. Focused M-049 Domain and M-050 Evaluation
Result regression diagnostics also passed.

The successful pure-managed execution host was Unity Mono `mono.exe` with
`lib/mono/4.5/csc.exe`; it used no network and no restore. The optional Unity
Editor batch diagnostic remains blocked by the known Unity Licensing Client
environment limitation and is not recorded as passed.
