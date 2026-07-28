# M-038 Single Instance Design

Status: Completed

Visual verification: Passed

## Scope

M-038 applies only to normal runtime. Explicit `runtime-smoke`,
`drag-diagnostic`, `real-static-diagnostic`, and
`real-animated-diagnostic` launches retain their isolated diagnostic
lifecycles.

The implementation prevents a second normal-runtime process from creating a
mascot, tray icon, production Settings state, or window-position state. The
only secondary action is a bounded `OpenSettings` notification followed by
normal process exit.

## Startup order

`SingleInstanceStartupGate` is the one
`RuntimeInitializeLoadType.BeforeSplashScreen` normal-runtime startup gate:

1. preserve M-037 by applying the initial Player cloak through
   `UnityPlayerWindowVisibilityController`;
2. call the native single-instance initializer;
3. continue only when primary ownership was acquired;
4. otherwise request an orderly secondary `Application.Quit(0)`.

The later `BeforeSceneLoad` bootstrap checks the gate before product Settings,
window-position, native mascot, tray, or runtime initialization. The
`AfterSceneLoad` bootstrap has the same defensive guard.

## Ownership

The native `SingleInstanceCoordinator` owns:

- session-local named mutex
  `Local\DesktopMascotMinimal.SingleInstance.v1`;
- session-local auto-reset activation event;
- session-local manual-reset readiness and shutdown events;
- primary/acceptance/pending state and bounded diagnostic counters.

The stable names use the internal engineering identity and are not derived
from the localized product name. `Global\` is intentionally not used:
single-instance enforcement is scoped to the current interactive Windows
session rather than all user sessions.

The mutex is created without initial ownership and acquired with a zero-time
wait. `WAIT_OBJECT_0` and `WAIT_ABANDONED` establish the sole primary.
`WAIT_TIMEOUT` identifies a secondary. Final orderly cleanup closes the
notification handles, releases the owned mutex, and closes its handle.
Process termination releases kernel handles, so an abandoned or ended owner
does not prevent a future primary.

## Notification protocol

The primary creates the named events immediately after mutex acquisition. A
secondary retries for at most 80 attempts at 25 ms intervals. Each attempt:

1. opens readiness, shutdown, and activation events;
2. requires readiness to be signaled and shutdown not to be signaled;
3. signals the activation event;
4. verifies that shutdown did not begin concurrently.

The activation event is auto-reset and therefore acts as a kernel pending
latch. Repeated `SetEvent` calls while it is already signaled do not queue
additional work. A burst of equivalent requests converges to one logical
pending `OpenSettings` activation. After the primary consumes it, a later
launch can produce the next logical generation.

An earlier prototype used a dedicated `HWND_MESSAGE`. Automated
multi-process execution demonstrated that Player processes launched into
different Window Stations/desktops shared the session-local mutex but could
not discover one another's windows. Named events remove this UI-desktop
dependency and keep notification independent from the Player, mascot, and
tray windows.

## Primary routing

`SingleInstanceController.Update` polls the native activation event on the
Unity managed main thread. Each consumed logical generation calls
`UnityPlayerWindowVisibilityController.TryActivateSettingsFromSecondary`.
That method:

- rejects work after the visibility shutdown barrier;
- uncloaks/restores/foregrounds the exact current-process
  `UnityWndClass` through the sole visibility owner;
- opens the existing `SettingsWindowController` only if it is closed;
- does not recreate the component or reinitialize an already-open ViewModel.

Consequently, unapplied Settings edits survive repeated secondary activation.

## Shutdown race policy

`DesktopMascotRuntimePipeline` closes the single-instance acceptance barrier
at the beginning of every orderly and emergency shutdown path. The native
coordinator signals shutdown and clears readiness before the established
composition, tray, region, Camera, and managed cleanup continues.

A secondary that arrives after the barrier performs only the bounded retry.
It does not become primary during that launch and exits normally, leaving a
later explicit user launch to start after shutdown finishes. Final cleanup
destroys the notification channel and releases the mutex immediately before
the existing exact-`UnityWndClass` `WM_CLOSE` and `Application.Quit` sequence.

## Invariants

- No process-name or localized-title detection.
- No `Process.MainWindowHandle`, `Process.CloseMainWindow`, kill,
  `TerminateProcess`, `ExitProcess`, `Environment.Exit`, `SendInput`, or
  global hooks.
- No production `settings.json` or `window-position.json` access by a
  secondary.
- No duplicate mascot, tray, Player presentation pipeline, Settings
  component, or visibility owner.
- M-037 remains the only Player show/cloak owner.
- M-036 orderly shutdown and M-028 through M-035 rendering, region, drag,
  persistence, and command contracts remain unchanged.

## Validation plan

- native coalescing focused diagnostic;
- existing Settings and Player-visibility focused diagnostics, including
  preservation of open dirty Settings;
- primary plus one secondary process;
- primary with Settings already open plus another secondary;
- rapid secondary burst and convergence to one remaining primary;
- acquisition after prior primary orderly exit;
- native and Unity Development Player builds;
- M-031 through M-037 focused regressions;
- runtime-smoke, drag-diagnostic, real-animated, normal runtime, and orderly
  shutdown invariants;
- final user visual and interaction verification.
