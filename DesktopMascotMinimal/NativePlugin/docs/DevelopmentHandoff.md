# Development Handoff

## Purpose and authority

This document gives a new ChatGPT or Codex thread current development intent
and the shortest path to authoritative records. It is not milestone history,
an architecture-contract copy, an execution-log archive, or authority for live
Git state.

Use this precedence:

1. live Git state obtained read-only for Git facts;
2. `AGENTS.md` for operative repository rules;
3. `Milestones.md` for milestone state and validation history;
4. relevant subsystem design documents for detailed contracts;
5. `ProjectPrinciples.md` for future design guidance;
6. this document for current intent and navigation.

This handoff never overrides live Git, `AGENTS.md`, `Milestones.md`, or a
relevant design contract. At each task start, read `AGENTS.md` and this file,
then verify branch, HEAD, upstream, ahead/behind, worktree, index, and any
merge/rebase/cherry-pick state read-only. Do not store volatile Git facts here.

## Current development line

- Expected branch: `m045-complete-safety` (advisory; verify live Git).
- Architecture baseline: **M-046**.
- Speech-specific design baseline: **M-047.5**.
- M-048 and M-049 through M-057 are additive and do not advance M-046.
- M-054 is a completed design gate for production Conversation request and
  default-persona identity policy; it added no implementation.
- Latest completed milestone: **M-058 — Portable Windows x64 Release Candidate
  Foundation**.
- Active product milestone: **None**.
- M-057 automated, manual, visual, and final repository/staged validation
  passed.
- M-058 automated, manual, visual, and final repository/staged audit passed.

M-057 scope is authorized. Its detailed contract is
`SingleMascotClickSpeechResponseProductionIntegrationDesign.md`.

## Current objective and blockers

M-057 is complete under architecture baseline M-046 and Speech-specific design
baseline M-047.5. Its implementation, automated validation, user-performed
manual and visual verification, and final repository/staged audit are recorded
in `Milestones.md`. Commit and push remain separate explicit authorizations.

There is no active product blocker. M-057 native/managed Speech export parity
was restored by the canonical native build and Player deploy without native
source changes. The Assets and current product-named Player DLLs have matching
SHA-256 and exports, and the previously blocked `message-window-diagnostic`
passed with orderly shutdown. The user subsequently passed the required normal-
runtime manual and visual verification, including bundled/imported Character
click-to-Speech, Speech/drag/Settings interaction, and tray orderly shutdown.
The final exact-file-set staged audit passed. M-058 now has passed automated,
manual, and visual Release Candidate evidence, including clean first launch,
existing v1 persistence, Single Instance, Explorer tray recovery, and orderly
shutdown. It changes no runtime product behavior. Its final exact-file-set
repository/staged audit also passed, so M-058 is complete. Commit and push
remain separately unauthorized.

The Unity Editor batch Licensing Client
limitation is preserved as validation history in M-049 through M-053; it did
not block the accepted pure-managed focused diagnostics. Resolved serializer
or other transient validation incidents belong only in `Milestones.md`.

Deferred Conversation Pack/VRM binding, Rule/Script Engine, production wiring,
runtime concurrency/Speech Busy policy, cadence/trust/schema, history/AI,
animation/expression/audio, typing, and Text SE remain outside scope. See
`ConversationDomainDesign.md` and `ProjectPrinciples.md`; no item authorizes a
feature without an explicitly scoped milestone.

## Authoritative document map

- `AGENTS.md`: operative repository-wide rules, ownership, safety, validation,
  approval, and Git policy.
- `Milestones.md`: completed/in-progress scope, validation, commits, and
  historical evidence.
- `README.md`: documentation index.
- `RepositoryStructureDesign.md`: current source/layout/tool/generated-output
  boundary.
- `ProjectPrinciples.md`: adopted future product/design guidance.
- `SettingsPresentationDesign.md`: M-045 Settings presentation.
- `SpeechPresentationDesign.md`: M-047.5/M-048 Speech contract.
- `ConversationDomainDesign.md`: M-049 Conversation Domain.
- `ConversationEvaluationResultDesign.md`: M-050 evaluation result.
- `LocalConversationEvaluatorDesign.md`: M-051 local evaluator interface.
- `LocalConversationResponseEntryDesign.md`: M-052 response entry.
- `SingleEntryLocalConversationEvaluatorDesign.md`: M-053 exact-match
  evaluator.
- `ConversationRuntimeIdentityDesign.md`: M-054 production request identity
  and default Conversation persona binding policy.
- Other subsystem designs named by `AGENTS.md`: their detailed current
  ownership, lifecycle, implementation, and validation contracts.
- This file: current intent, model guidance, thread lifecycle, and navigation.

## Responsibility split

ChatGPT owns product/architecture discussion, milestone planning, major design
decisions, Codex instruction generation, and result review. Codex owns bounded
repository inspection/implementation, diagnostics, builds, validation, Git
inspection, and technical reporting. The user owns final product/design
decisions, required manual/visual verification, authorization for consequential
Git operations, and bridging separate threads.

## Model selection and escalation

Use GPT-5.6 Sol for architecture or milestone design, ambiguous/root-cause
investigation, ownership/lifetime/shutdown or cross-system reasoning, final
architecture audit, and ambiguous Git-history meaning.

Use Terra for explicitly approved bounded implementation, mechanical changes,
known build/run validation, routine Git cleanup or normal push, and
straightforward documentation edits.

Escalate Terra to Sol when contracts conflict, an unexpected production
regression occurs, ownership/lifetime is unclear, Git history conflicts with
documentation, validation repeatedly fails without explanation, scope must
expand, evidence contradicts the expected contract, or multiple safe
architectural options remain. Return to Terra only after root cause, scope,
affected invariants, files/subsystems, and validation are known and no design
decision remains. This is workflow guidance, not an architecture/security
contract or operation authorization.

## Thread lifecycle

- Start a new thread for a new milestone.
- Use focused Sol for architecture/design or an unexpected complex failure.
- Use Terra for bounded implementation and known validation.
- Use a short Sol final-audit thread only when justified.

Keep one thread while objective, scope, and validation loop remain unchanged.
Start another when the phase/objective changes, the current decision is buried,
repeated context dominates, or the objective cannot be stated concisely.

## Final audit linkage

Follow `AGENTS.md` under “Git authorization, safety, and final staged audit”. A
complete temporary staged audit is mandatory at milestone/commit gates and the
other risk conditions listed there. If the index is not clean before an audit,
do not modify it: inspect and stop for direction. Audit-only staging must be
limited to the intended files and immediately undone afterward. Commit/push
remain separately authorized operations.

## Instruction and report essentials

New task instructions should state model, task type/mode, objective, required
entry-document/Git inspection, scope, allowed/prohibited changes, preserved
invariants, investigation/validation, Git authorization, stop conditions, and
required report. Reference architecture contracts instead of copying them.

Routine reports should identify status, scope, changed files, architecture
impact, validation/manual verification, known issues, live Git state, decisions
needed, and next step. Failure reports must additionally identify observed
versus expected behavior, reproduced evidence, investigated/ruled-out scope,
contract risk, changes made, and safe options. Do not omit decisive evidence
when an architecture decision depends on it.

## Update rules

Update this file only for milestone completion, next-milestone selection,
material active-blocker change, deliberate thread handoff, or architecture/
design-baseline change. Do not update it for an ordinary turn, build, or
transient result.

Do not add exact HEAD hashes, dirty/ahead/behind state, detailed logs,
screenshots, generated artifacts, credentials/tokens, private file paths, or
chat transcripts. Live Git and the referenced authoritative documents remain
the source of truth.
