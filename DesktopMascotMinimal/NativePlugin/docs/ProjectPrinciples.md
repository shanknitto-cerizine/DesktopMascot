# DesktopMascot Project Principles

Status: Adopted for M-049 and later design work

Architecture baseline: M-046

## Purpose and precedence

These principles guide future product and architecture decisions. They do not
replace validated architecture contracts, ownership rules, or completed
milestone results. A change to an established contract still requires an
explicitly scoped and validated milestone.

## A. Mascot First

- Principle: Prioritize the character's natural desktop presence over a list
  of application features.
- Why: A persistent dashboard or chat application is not the product goal.
- Architectural consequence: Conversation and presentation serve the
  character experience; they do not become the primary surface.
- Preferred design: A character response appears as a small, non-activating
  presentation near the mascot.
- Design to avoid: A permanently foregrounded chat window or dashboard.

## B. Quiet Presence

- Principle: The mascot must not continuously demand user attention.
- Why: Interruptions accumulate quickly in an always-running application.
- Architectural consequence: Automatic output requires bounded cadence,
  suppression, and user control.
- Preferred design: A short, low-frequency response that closes predictably.
- Design to avoid: Speaking for every startup, idle interval, or clock event.

## C. Interaction Without Focus

- Principle: Ambient mascot operation does not require keyboard focus.
- Why: The mascot must coexist with the user's primary application.
- Architectural consequence: Mascot, tray, context interaction, and ambient
  presentation remain non-activating by default.
- Preferred design: A mascot interaction can receive a non-activating answer.
- Design to avoid: Showing the Player or Settings for routine responses.

This principle is limited to ambient operation. An explicitly opened Settings
surface or file picker may acquire focus when that is required for a deliberate
configuration task.

## D. Local First

- Principle: Essential character behavior works without network or AI.
- Why: Availability, privacy, latency, and cost must not depend on a service.
- Architectural consequence: Local events, rules, scripts, and packs remain
  capable of providing the baseline experience.
- Preferred design: A local rule handles a mascot interaction immediately.
- Design to avoid: Sending every event to an external API before responding.

## E. AI Optional

- Principle: AI is an opt-in extension provider, never a required runtime
  dependency.
- Why: The mascot must remain complete and useful without AI.
- Architectural consequence: Conversation domain contracts remain separate
  from provider SDKs and network policy.
- Preferred design: Use an AI fallback only after local handling and explicit
  user consent.
- Design to avoid: Making an LLM request the only path for a basic reaction.

## F. Character Customization

- Principle: Character customization may include dialogue, reactions, and
  sound as well as a VRM's visual model.
- Why: These are part of character identity.
- Architectural consequence: A future Conversation Pack is a separate,
  versioned domain asset; it is not owned by `CharacterAssetManager`.
- Preferred design: Select metadata, phrases, rules, and optional sound
  configuration through a dedicated pack boundary.
- Design to avoid: Embedding dialogue in a Scene, native source, or VRM path.

## G. Provider Independence

- Principle: Conversation behavior does not depend on a particular AI vendor.
- Why: Offline use, service replacement, and privacy policy must remain
  possible.
- Architectural consequence: Rule, Script, and optional AI providers consume
  and produce provider-independent conversation contracts.
- Preferred design: Replace a provider without changing event sources or
  presentation ownership.
- Design to avoid: Putting model names, endpoints, or SDK types in the domain.

## H. Character Presentation Consistency

- Principle: Speech, animation, expression, and sound are coordinated as one
  character response.
- Why: Independent reactions can make a character feel inconsistent.
- Architectural consequence: Future response coordination is separate from
  the resource ownership of each presentation subsystem.
- Preferred design: A response coordinator requests presentation intents from
  independently owned subsystems.
- Design to avoid: A single manager owning Speech HWNDs, character roots,
  audio, and native resources together.

## I. Sound as Presence

- Principle: Sound is a brief, unobtrusive presence cue rather than a generic
  notification.
- Why: Audio is particularly intrusive in a desktop environment.
- Architectural consequence: Future Text SE must be optional, bounded,
  configurable, and character-specific.
- Preferred design: A short, low-volume cue associated with an active text
  presentation.
- Design to avoid: A loud notification sound for every message.

## J. User Control

- Principle: Users control automatic conversation, sound, frequency, and
  network use.
- Why: Comfort and privacy preferences vary by user.
- Architectural consequence: Production enablement requires versioned
  settings, safe defaults, and explicit consent where network use is involved.
- Preferred design: AI and network use are disabled until deliberately enabled.
- Design to avoid: Enabling cloud behavior or sound silently on install.

## K. Cross-platform Boundary

- Principle: Domain, Conversation, and Character behavior remain separate
  from platform presentation.
- Why: Shared behavior should be reusable by a future platform.
- Architectural consequence: Shared contracts contain no HWND, HRGN, D3D12,
  DirectComposition, Android lifecycle, or platform renderer types.
- Preferred design: One response contract can feed Windows and future Android
  presenters.
- Design to avoid: Calling Win32 APIs from a rule or Conversation Pack.

This preserves the current Windows implementation. It does not authorize
Android dependencies or speculative abstractions before a dedicated milestone.

## L. Performance Discipline

- Principle: Always-running status does not permit unbounded resource use.
- Why: A mascot must cost less than the value it provides.
- Architectural consequence: Use event-driven work, bounded state, and
  observable ownership; leave disabled providers uninitialized.
- Preferred design: Evaluate a rule only when an event is supplied.
- Design to avoid: Per-frame rule scans, unbounded history, or unlimited retry.

## Interpretation constraints

- `CharacterAssetManager` remains the only normal-runtime character-selection
  and runtime-root ownership boundary.
- M-048 Speech retains its independent HWND, RenderTexture, D3D12,
  DirectComposition, and HRGN ownership.
- Principles coordinate behavior; they do not merge subsystem ownership.
- Existing Settings, Tray, persistence, diagnostic, and production separation
  contracts remain authoritative until explicitly changed.
