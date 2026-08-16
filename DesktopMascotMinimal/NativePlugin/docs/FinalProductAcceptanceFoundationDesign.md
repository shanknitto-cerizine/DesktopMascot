# M-059 — Final Product Acceptance Foundation

Status: Completed

Acceptance Foundation: Passed

Final Product Acceptance Execution: Pending

Product Ready: Not Yet Declared

Architecture baseline: M-046

Speech-specific design baseline: M-047.5

## Purpose

M-059 establishes the acceptance contract used to decide whether the frozen
M-058 portable Release Candidate is Product Ready. It adds no runtime
behavior, source code, native change, Scene change, persistence change, or
Release-artifact rebuild.

This record defines the supported environment, acceptance cases, evidence,
and disposition rules. It does not execute final product acceptance and does
not declare Product Ready.

## Product decision and frozen artifact

The initial Product Ready product is:

- product version: `0.1.0`;
- publisher/company: `Ceritizine_poc`;
- distribution: portable unsigned ZIP;
- installer, updater, and code signing: not required;
- supported operating system: Windows 11 x64;
- graphics API: Direct3D 12;
- initial Conversation: a mascot click displays the fixed `こんにちは。`
  response.

Multiple responses, selection policy, Conversation content expansion, Windows
10 support, installer/updater, signing, publisher-identity changes,
automatic Conversation, provider/AI/network behavior, Conversation Pack,
Rule Engine, animation, expression, audio, and Text SE are outside the
initial Product Ready contract.

The only acceptance artifact is:

```text
あなたといつも-0.1.0-windows-x64.zip
SHA-256: D57E706F2B309970ACE9037348545FB6AA35D3A15CD0D41843F13775F9F8C9A1
```

Every final-acceptance case records this filename and SHA-256. A rebuilt ZIP
with a different hash is a new Release Candidate identity and cannot inherit
this artifact's acceptance evidence.

## Product Ready definition

Initial Product Ready means that the frozen artifact passes every required
final-product-acceptance case on its declared Windows 11 x64 environment,
with traceable evidence, no unresolved MUST failure, and user-reported manual
and visual verification. Product Ready is not established by M-059 foundation
documentation alone.

## Three evidence layers

The following layers remain separate and none substitutes for another:

1. **Milestone diagnostics** prove focused subsystem contracts, ownership,
   lifecycle, failure-stage, and internal invariant behavior. They retain the
   existing M-029 through M-058 evidence. A diagnostic-only scenario such as
   an internally evaluated NoMatch is not a final product-acceptance case.
2. **Artifact validation** proves the package identity and contents: ZIP
   SHA-256, `manifest.sha256`, release metadata, artifact allow/deny rules,
   x64/native-export/CRT properties, and notices. M-058 remains the source of
   this artifact contract.
3. **Final product acceptance** proves the user-visible frozen-artifact
   experience on the declared support environment, including clean launch,
   normal operation, lifecycle, recovery, and orderly exit.

## Acceptance environments

### Environment A — clean portability environment

Environment A is a clean Windows 11 x64 environment with a Direct3D 12
capable GPU and driver. It uses a standard user account, a clean
`%LOCALAPPDATA%\DesktopMascotMinimal` state, and has no Unity Editor,
repository, or development-tool dependency. It proves portable deployment and
clean first launch. Its evidence records Windows edition, version, build,
x64 status, GPU, driver, D3D12 availability, account type, and clean-state
precondition without recording a username.

### Environment B — lifecycle environment

Environment B is a Windows 11 x64 environment that can perform an actual
Windows Sleep and Resume on D3D12-capable hardware. It proves long-running,
power, Explorer recovery, abnormal-termination relaunch, and final orderly
shutdown behavior. Its evidence additionally records the power model and
sleep/resume timestamps. VM suspend is not Windows Sleep and cannot satisfy a
power case.

One clean physical Windows 11 x64 environment may serve as both A and B only
when it meets both contracts.

## Acceptance case record schema

Every case record contains all of the following:

- Case ID and revision;
- requirement;
- artifact filename and SHA-256;
- Environment ID;
- Windows edition, version, build, and x64 status;
- GPU, driver, and Direct3D 12 evidence;
- clean-state and persistence preconditions;
- steps;
- expected result and actual result;
- start and end timestamps;
- status: `Pass`, `Fail`, `Blocked`, or `Not Run`;
- evidence references;
- warning classification;
- defect ID, if any;
- retest relation; and
- user manual/visual verification statement.

Evidence must not contain a username, absolute external VRM path, external
VRM content hash, private user content, credentials, tokens, or unredacted
private filesystem information. Screenshots, logs, and summaries use stable
case/environment/artifact IDs instead.

## Acceptance catalog

| Case | Requirement and required result |
| --- | --- |
| `FPA-ART-001` | Recompute and match the frozen ZIP SHA-256; validate manifest and release metadata before use. |
| `FPA-ENV-001` | Environment A meets the Windows 11 x64, D3D12, standard-user, clean-state, and no-development-dependency contract. |
| `FPA-START-001` | A clean extraction and first launch show the bundled model, one mascot and tray icon, no unexpected Settings or Player surface, and the local essential flow works without network. |
| `FPA-CORE-001` | Mascot click shows `こんにちは。`; Busy drops without backlog; card close/re-show, drag classification, and existing transparent-region interaction remain normal. |
| `FPA-CHAR-001` | Bundled Character, permitted runtime import/restore, and return-to-bundled preserve the established M-039 through M-044 contracts. |
| `FPA-SET-001` | Settings open/apply/cancel/close works while the runtime continues normally. |
| `FPA-PERSIST-001` | Committed position, settings, and selected-character records preserve existing v1 compatibility across orderly restart. |
| `FPA-SI-001` | A secondary launch routes to the existing primary and creates no duplicate mascot, tray icon, or process. |
| `FPA-TRAY-001` | One Explorer restart converges to one functional tray icon while mascot, Speech, drag, and Settings continue to work. |
| `FPA-SOAK-001` | Environment B runs for four continuous awake hours; sleep intervals are excluded. At start, about two hours, and near the end, the primary interaction, Settings, tray, and presentation checks pass without progressive functional degradation or fatal failure. |
| `FPA-PWR-001` | Environment B completes two actual Sleep/Resume cycles: first sleep at least two minutes, second sleep at least thirty minutes. Each resume restores the primary surfaces and checks Character, click/Speech, drag, Settings, tray, and Single Instance. |
| `FPA-ABN-001` | From an idle state with committed persistence and no picker/import/write/teardown in flight, force-terminate once, relaunch the frozen artifact, recover primary ownership, one mascot, one functional tray icon, committed persistence, click response, then orderly exit. |
| `FPA-SHUT-001` | After lifecycle work, orderly exit releases mascot, tray, Speech, Player, and process according to the existing shutdown contract. |
| `FPA-RES-001` | After each applicable exit, no residual Player process, window, or functional tray icon remains. A transient shell-cached icon is not accepted as a second functional icon and must clear through normal shell refresh. |

## Bounded lifecycle and abnormal termination

`FPA-SOAK-001` requires four continuous **awake** runtime hours. This is the
initial bounded Product Ready value; no 8- or 24-hour run is required. The
functional observations at start, approximately two hours, and end do not use
process-wide memory samples as ownership proof. Existing direct ownership and
final cleanup evidence remain authoritative.

`FPA-PWR-001` requires exactly two actual Windows Sleep/Resume cycles: one
short cycle of at least two minutes and one sustained cycle of at least thirty
minutes. A resume observation should occur within sixty seconds and its exact
time is recorded; a slower or failed restore is a case failure unless a
separate approved acceptance revision changes that criterion.

`FPA-ABN-001` is a minimal relaunch-resilience requirement, not a crash
handling feature. It requires one forced termination only after persistence
has committed and while no operation is in flight. It does not require crash
reporting, auto-restart, fault injection, preservation of uncommitted data, or
orderly-cleanup counters/logs from the terminated process.

## Results, pass criteria, and stop rules

A case passes only when the frozen artifact and qualified environment match
the record, every required result is met without an undocumented workaround,
and required evidence is complete and privacy-safe.

A case fails for a user-visible deviation, hang, crash, device/fatal failure,
persistence corruption, inability to relaunch, permanent resource residue,
missing prerequisite, artifact mismatch, or other unmet expected result.

A case is blocked only when its environment, artifact, or safe precondition is
unavailable. A blocked or not-run MUST case prevents a Product Ready decision.

On any MUST failure, stop the affected acceptance track. Do not expand M-059
into an unbounded runtime repair. A repair requiring runtime/native ownership,
artifact rebuild, persistence migration, support-matrix change, or Product
Ready definition change returns to a separately scoped minimum defect or
capability milestone. After a changed artifact, repeat all affected cases
against its new frozen identity.

Overall Product Ready requires every MUST case to pass with the same artifact
SHA-256, no unresolved MUST failure, a final evidence audit, and the user's
manual/visual acceptance report.

## Evidence and execution boundary

Required final evidence consists of artifact identity, environment identity,
case records, bounded relevant logs, timestamps, screenshots for visual
states, residual process/window/tray checks, user manual/visual statements,
and a final summary. A screenshot does not prove an internal invariant; a log
does not replace visual acceptance.

M-059 establishes this foundation only. It does not execute the four-hour
soak, Sleep/Resume, forced termination, clean-environment acceptance, or final
Product Ready decision. Those activities start only at the later explicit
final-acceptance execution gate.

## Architecture and scope boundary

M-059 changes no runtime graph, ownership, resource lifetime, native API,
Scene, ProjectSettings, or M-058 artifact. `CharacterAssetManager`, the
separate Persistence owners, Speech ownership, D3D12/DirectComposition path,
Single Instance, tray, and orderly shutdown contracts remain unchanged.

The Architecture Viewer records M-059 only in its Product Vision / Roadmap
view: acceptance foundation established, final acceptance pending, Product
Integration still PARTIAL, and Product Ready not yet declared.
