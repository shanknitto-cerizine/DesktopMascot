# Development Handoff

## Purpose and Authority

This document gives a new ChatGPT or Codex thread the current development
intent and the shortest path to the relevant authoritative records. It is not
a milestone history, architecture-contract copy, or execution-log archive.

When sources disagree, use this precedence order:

1. Current Git state obtained read-only.
2. `AGENTS.md`.
3. `Milestones.md`.
4. Relevant subsystem design documents.
5. `ProjectPrinciples.md`.
6. This document.

This document never overrides `AGENTS.md`, replaces a design contract, or
rewrites milestone evidence.

## Current Development Line

- Expected branch: `m045-complete-safety` (advisory; verify the actual branch
  through Git).
- Architecture baseline: M-046.
- Active design baseline: M-047.5 where Speech-specific; M-049 Conversation
  Domain is additive and does not advance the architecture baseline.
- Latest completed milestone: M-050 - Conversation Evaluation Result
  Contracts Foundation.
- Next product milestone: M-051 - Design Ready / Not Started.

Do not store an exact HEAD hash, upstream relation, or working-tree/index
state here. Obtain them from Git at the start of each task.

## Current Objective

M-050 is complete. Use this handoff workflow to plan, instruct, investigate,
and review M-051 as the next candidate; do not begin M-051 implementation
until its scope is explicitly approved. Workflow infrastructure is separate
from product-milestone design.

## Known Blockers

None.

The local Unity headless-license limitation recorded for M-049 is preserved in
`Milestones.md` as validation history. It is not an active product-development
blocker.

## Deferred and Explicitly Out of Scope

The following M-049 deferred work remains outside the current scope:

- Conversation Pack and VRM binding;
- Rule Engine and Script Engine;
- runtime concurrency policy and Speech Busy handling;
- cadence, pack schema, and pack trust;
- conversation history and AI fallback;
- animation, expression, and sound intents;
- Text SE and typing.

See `ConversationDomainDesign.md` for the detailed boundary. Do not expand
one item into a feature without an explicitly scoped milestone.

## Validation Snapshot

M-050:

- Automated Validation: Passed.
- Visual Verification: Not Applicable.
- Manual Interaction Verification: Not Required.
- Commit and remote tracking: Completed.

Detailed validation evidence and the retained headless-license note belong in
`Milestones.md`.

## Authoritative Document Map

- `AGENTS.md`: non-negotiable development and architecture rules.
- `Milestones.md`: milestone history and validation evidence.
- `ProjectPrinciples.md`: product and design principles.
- `RepositoryStructureDesign.md`: repository ownership and layout.
- `ConversationDomainDesign.md`: M-049 Conversation Domain contract.
- `ConversationEvaluationResultDesign.md`: M-050 Conversation Evaluation
  Result contract.
- `SpeechPresentationDesign.md`: M-047.5/M-048 Speech contract.
- `DevelopmentHandoff.md`: current intent only.
- `README.md`: documentation index.

## Responsibility Split

ChatGPT owns product and architecture discussion, milestone planning, Codex
instruction generation, result review, and major design decisions.

Codex owns repository inspection, bounded implementation, diagnostics, builds,
validation, Git inspection, and structured technical reports.

The user owns final product and design decisions, manual or visual
verification, permission for risky Git operations, and bridging ChatGPT and
Codex when necessary.

## Model Selection and Escalation

Use GPT-5.6 Sol for architecture design, milestone planning, ambiguous or
root-cause investigation, ownership/lifetime/shutdown reasoning, cross-system
reasoning, final architecture audit, and ambiguous Git-history meaning.

Use Terra for approved bounded implementation, mechanical corrections,
build/run work, known validation, routine Git cleanup or normal push, and
straightforward documentation edits.

Escalate Terra to Sol when architecture contracts conflict, a production
regression is unexpected, ownership or lifetime is unclear, Git history and
documentation conflict, validation repeatedly fails without explanation, scope
must expand, evidence contradicts the expected contract, or more than one safe
architectural option remains.

De-escalate Sol to Terra once the root cause, change boundary, affected
invariants, exact file or subsystem scope, and validation procedure are known
and no architectural decision remains.

This is workflow guidance, not a security or architecture contract.

## Thread Lifecycle

- Start a new thread for a new milestone.
- Use a focused Sol thread for architecture or design work.
- Use a Terra thread for bounded implementation.
- Use a dedicated Sol investigation for an unexpected complex failure.
- Use a short Sol thread for a final audit only when justified.

Keep one thread when its objective, scope, and validation loop are unchanged.
Start another thread when the objective or phase changes, the current decision
is buried in long history, the same context must be repeated, or the objective
cannot be stated concisely.

## Final Audit Gate

Use a complete staged audit for milestone completion, a commit gate,
new/untracked files, rename/move/delete work, generated-artifact risk,
multi-thread or multi-phase changes, and release/tag/push preparation.

1. Verify Git safety.
2. Confirm the intended file set.
3. Confirm the index is clean.
4. Temporarily stage only the exact intended files.
5. Run `git diff --cached --check`.
6. Inspect `git diff --cached --name-status`.
7. Review the staged diff.
8. Reject generated artifacts and secrets.
9. For an audit-only task, unstage exactly those files.
10. Commit only after the gate passes and authorization is explicit.

Use ordinary `git diff --check` as an early check for tracked worktree
changes. Use the staged gate to include untracked files. If the index is not
clean before the audit, do not change it; inspect and stop for direction.

## ChatGPT to Codex Instruction Template

```text
Recommended model:
Task type:
Mode:

Objective:

Repository Context:
Read AGENTS.md and DevelopmentHandoff.md first.
Verify Git baseline read-only.

Scope:

Allowed Changes:

Prohibited Changes:

Preserved Invariants:

Required Investigation:

Validation:

Git Authorization:

Stop Conditions:

Required Report:
```

Reference the relevant architecture contract rather than copying it. State
only task-specific invariants in the instruction.

## Codex to ChatGPT Routine Report Template

```text
Status:
Scope:
Changed Files:
Architecture Impact:
Validation:
Manual Verification Needed:
Known Issues:
Git State:
- Branch:
- HEAD:
- Working tree:
- Index:
- Commit/push performed:
Decision Needed:
Recommended Next Step:
```

## Failure and Escalation Report Template

```text
Status:
Failed Objective:
Observed Evidence:
Expected Behavior:
Scope Already Investigated:
Ruled Out:
Contract at Risk:
Changes Made:
Git State:
Decision Needed:
Safe Next Options:
```

Include reproducible evidence and decisive failure details. Do not omit them
for brevity when an architectural decision depends on the report.

## Update Rules

Update this document only at milestone completion, next-milestone selection,
a material active-blocker change, a deliberate thread handoff, or an
architecture/design-baseline change.

Do not update it for an ordinary turn, build, or transient validation result.
Do not add exact HEAD hashes, dirty state, ahead/behind state, detailed logs,
screenshots, generated artifacts, credentials/tokens, private file paths, or
chat transcripts. Git remains authoritative for Git state.
