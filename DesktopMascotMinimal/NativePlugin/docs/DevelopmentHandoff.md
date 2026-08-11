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
- M-048 and M-049 through M-053 are additive and do not advance M-046.
- Latest completed milestone: **M-053 — Single-Entry Exact-Match Local
  Conversation Evaluator**.
- Active product milestone: **None**.
- Next candidate: **M-054 — Design Investigation: Not Started**.

M-054 requires explicit scope authorization. Do not begin its design,
planning, or implementation as workflow/documentation maintenance.

## Current objective and blockers

M-049 through M-053 are complete under architecture baseline M-046. Detailed
scope, validation, commits, and environment limitations are recorded in
`Milestones.md` and their design documents.

There is no active product blocker. The Unity Editor batch Licensing Client
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
