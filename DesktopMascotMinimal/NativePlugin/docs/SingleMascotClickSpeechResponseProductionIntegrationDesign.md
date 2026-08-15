# Single Mascot-Click Speech Response Production Integration

Milestone: M-057

Status: Completed

Architecture baseline: M-046

Speech-specific design baseline: M-047.5

## Scope

M-057 connects one normal-runtime mascot click evaluation to the existing
independent Speech presentation. It uses the M-056 compiled-in local response,
the default M-054 Conversation identity, and no network, AI, animation,
expression, audio, Text SE, queue, retry, persistence, or Conversation Pack.

```text
completed mascot click
→ MascotClickConversationController
→ local synchronous evaluation
→ ConversationEvaluationResult
→ ConversationSpeechPresentationAdapter
→ SpeechPresentationController.TryShow
```

`NoMatch` is a successful no-op. A `ResponseProduced` result is the only
presentation candidate.

## Ownership

`ConversationSpeechPresentationAdapter` is a plain managed integration owner.
It borrows a synchronous `SpeechMessage` admission delegate, converts one
validated result, owns its acceptance barrier and bounded counters, and owns no
Character, Speech resource, native resource, Settings, Persistence, queue,
history, provider, or pack.

`SpeechPresentationController` remains the sole managed presentation lifecycle
owner. The existing native host retains its HWND, D3D12, DirectComposition, and
HRGN ownership. `CharacterAssetManager` remains uninvolved in Conversation.
`DesktopMascotRuntimePipeline` coordinates the three shutdown barriers and
managed cleanup without absorbing those subsystem resources.

## Conversion and admission

For `ResponseProduced`, the adapter preserves `RequestId.Value` as
`SpeechMessage.MessageId`, preserves `response.SpeakerId.Value`, sets display
name to `Mascot`, uses `response.Text` unchanged, and uses the M-048 fixed
Normal / ManualOrTimeout / None / None presentation values.

`Accepted` and `Coalesced` succeed. `Busy` drops the response. `Shutdown`
rejects it. `Failed` is a Speech-only drop. There is no queue, retry, or
pending presentation backlog. Null/corrupt results, correlation mismatch,
unsupported intent, invalid conversion, and `TryShow(Invalid)` are runtime
invariant failures and must not log private text or Character data.

## Production Camera isolation

`SpeechCameraIsolationController` is the sole owner of the temporary
production Camera culling-mask mutation. It records the exact original mask,
excludes only Speech Layer 30 before Player preview creation, and restores the
exact original value idempotently during cleanup and `OnDestroy` fallback. It
does not change Camera FOV, framing, transform, target texture, mascot scale,
or Y-normalization.

The Player preview copies the already-isolated mask. The Speech Camera alone
renders Layer 30. Diagnostic mascot FOV/framing stays separately owned by
`DesktopMascotSpeechMascotScaleDiagnostics`.

## Lifecycle

Normal runtime creates and attaches the Speech controller before initialization
so partial initialization remains cleanup-safe. A successful initialization
creates the adapter and passes its borrowed reference to the click controller.
Initialization failure leaves click evaluation usable and disables only
Conversation presentation. Normal runtime never shows the diagnostic greeting.

Shutdown first closes click acceptance, then adapter acceptance, then Speech
Show acceptance. Existing cooperative import draining and native aggregate
shutdown remain unchanged. The native aggregate path releases Speech before
mascot composition teardown; managed Speech cleanup then precedes Camera-mask
restoration and Character release.

## Validation

Focused managed diagnostics cover conversion, admission, Busy drop, NoMatch,
shutdown, Speech failure, and invariant classification. M-056 click semantics,
M-048 Speech diagnostics, production Camera/preview mask isolation, normal
runtime click-to-Speech, runtime-smoke, drag, Settings, Character import,
Single Instance, and orderly shutdown remain targeted regressions. Manual
visual/interaction verification passed for bundled and imported Characters,
Speech/drag/Settings interaction, and orderly tray exit. Transparent-area
click-through retains the existing M-048 focused diagnostic evidence because
the exact Speech window bounds were not reliably identifiable in normal
runtime. The final repository/staged audit passed with exactly the intended
M-057 file set; commit and push remain separate authorization gates.
