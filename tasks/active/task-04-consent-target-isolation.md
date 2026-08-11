# Task 4 — Consent and Target Isolation

Status: **Active**
Authorized: 2026-08-10 by Boss after Task 3 acceptance, under the accepted direct-build workflow
Accepted base: `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`
Working branch: `agent/task-04-consent-target-isolation`

## Objective

Build the neutral consent shell that can discover eligible local application windows without capturing them, classify executables into the four accepted authorization categories, authorize exactly one target, and enforce a stop-only privacy boundary with explicit resume and generation fencing.

This task proves target authority and privacy control using metadata-only synthetic frames. It does not implement Windows Graphics Capture, pixels, crops, visual interpretation, final labels, personality, or decorative UI.

## Entry evidence

- Task 3 passed its distinct Paw Gate, PR #7 was squash-merged to accepted `main` as `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`, and a fresh post-merge audit passed locked restore, all 11 Release builds with zero warnings/errors, 121/121 locally runnable tests, and accepted Windows run `31426920584` with 125/125 tests.
- The master packet assigns executable/window discovery, four authorization categories, one target, a stop-only `Ctrl+Shift+F12` hotkey, exact target status, and a one-monitor guard to Task 4.
- Architecture trust boundary 1 requires enumeration and capture to remain separate capabilities. The worker must only accept an authorization-approved window and has no full-screen fallback.
- Architecture trust boundary 4 and the emergency boundary require privacy stop to revoke the current generation before late work can reach presentation, journaling, or committed memory.

## Required implementation

### Metadata-only discovery

- Add a platform-neutral target-authorization core plus a Windows-only discovery adapter. Windows discovery may enumerate only eligible top-level application windows and query the minimum metadata needed to identify them.
- A discovered target contains an opaque nonzero window identifier, process ID, executable filename, and a deterministic fingerprint of the normalized executable path. Do not expose, retain, log, or persist raw executable paths.
- Do not query or retain window titles, document/tab text, command lines, notification content, foreground-window identity, accessibility trees, pixels, icons, thumbnails, or unrelated process metadata.
- Exclude the companion process, invisible/tool/owned/cloaked windows, windows whose executable identity cannot be proven, and stale/reused handles. Revalidate handle + PID + executable fingerprint immediately before authorization and resume.
- Discovery itself must never start, invoke, or prepare a capture worker and must never emit a frame.

### Authorization policy

- Implement exactly four neutral internal categories: `FamiliarAsk`, `UnknownAsk`, `Denied`, and `StandingAuthorized`.
- `UnknownAsk` is the default. `FamiliarAsk` still requires explicit per-session consent. `Denied` produces no invitation and cannot authorize. `StandingAuthorized` is set only by an explicit user policy change and may authorize the selected executable without another per-session prompt.
- Maintain built-in denied classifications for recognized browsers, social/messaging/email clients, word processors, cloud-storage clients, password managers, and other accepted default-sensitive categories. An unrecognized executable remains `UnknownAsk`, so it still produces zero frames before a decision.
- Store only executable fingerprint, filename, category, and trusted-game content-policy choice. Use a versioned, bounded, atomically replaced development/test policy file. Corrupt or unsupported policy data fails closed: built-in denials remain and no stored standing authorization is honored.
- No production policy root, arbitrary normal-runtime path override, import/export, cloud sync, or Builder-to-production transfer is added.

### One target and authorization capability

- `TargetAuthorizationService` owns at most one active target session. Discovery refresh, foreground changes, or another window appearing must never retarget it.
- Authorizing a second target while one is active fails closed; the existing target must first be explicitly ended or privacy-stopped.
- Replace targetless `ICaptureWorker.StartAsync` with a start operation requiring a sealed authorization grant that ordinary callers cannot construct. The grant names the exact window, PID, target session, and privacy generation.
- Every metadata-only frame carries the exact target session and generation. A target/frame admission gate discards mismatched, revoked, stale, denied, or privacy-rejected frames before any downstream presentation, journal, or memory boundary.
- Keep the Task 4 worker synthetic and in-process. It may emit deterministic metadata only after receiving a valid grant. No pixels or native capture resources exist in this task.

### Privacy stop, explicit resume, and write pause

- Add one runtime privacy-generation authority shared by target authorization and live memory admission. It starts active, monotonically advances whenever a target generation is revoked, and cannot wrap or move backward.
- Privacy stop first atomically marks the runtime paused and revokes the current generation. It then cancels the active target work token, pauses new live-memory admissions, stops the worker, and clears all synthetic buffered frame metadata.
- An already-admitted durable memory append may finish safely; privacy stop waits for admitted writes to drain. No new append may be admitted after the pause is visible.
- Repeated hotkey presses are idempotent and stop-only. They never resume, create a target, or reuse a revoked generation.
- Resume requires an explicit UI action, a still-valid target, one-display topology, and a successful stop/clear fence. Resume creates a fresh generation and never releases pre-stop metadata or work.
- Implement and wire `Ctrl+Shift+F12` through the WPF window message boundary. Registration failure/collision is detectable and visibly reported; the privacy-stop button remains available.
- Display-topology change is handled as a privacy event. Any topology other than exactly one attached display blocks authorization/resume and privacy-stops an active session.

### Neutral shell and privacy-guard seam

- Extend the plain WPF shell only enough to refresh eligible targets, select one, inspect/change its neutral policy category, explicitly consent, stop, explicitly resume, and see the exact authorized target/status.
- Route visible status through the neutral presentation seam with stable content keys and typed `PrivacyPaused` intent. Do not add Prince wording, themed labels, final art, animation, or polish.
- Add the local Privacy Guard policy boundary now: standard targets reject an explicit `ClearlySensitive` assessment before downstream admission; an explicitly trusted game may bypass content filtering but never authorization, exact-target checks, generation checks, or the one-monitor guard. Pixel classification remains later work.

## Failure and race bounds

- Authorization and resume fail closed on cancellation, stale discovery, target exit/reuse, policy corruption, monitor-count uncertainty, or validation error.
- Revocation is synchronous and precedes asynchronous worker/write cleanup, so a late frame cannot become current even when a worker ignores cancellation or faults during stop.
- A failed worker stop/clear leaves privacy paused. Resume retries stop/clear and may proceed only after success.
- Policy replacement uses a same-directory temporary file and atomic promotion. A failed or cancelled save cannot turn a proposed standing authorization into active authority.
- Event subscriptions, hotkey registrations, cancellation sources, process handles, and temporary policy files are bounded and disposed deterministically.

## Explicitly forbidden

Do not implement or introduce:

- real screen/window/display capture, WGC, D3D, `PrintWindow`, `BitBlt`, Desktop Duplication, screenshots, crops, image buffers, thumbnails, or a full-screen fallback;
- automatic foreground targeting, capture of notifications/other windows, window-title/document-text collection, browser accompaniment, or tab capture;
- more than one active target, implicit consent, familiarity-as-consent, hotkey toggle/resume, automatic resume, or standing authorization inferred from behavior;
- semantic interpretation, API/provider code, network calls, credentials, paid calls, attention scoring, conversation, durable photographs, or model-generated memory;
- personality voice, themed final labels, custom artwork, animation, audio, final UI, production data, or Companion Awakening;
- memory schema/journal/backup/repair changes beyond the narrow live-write pause admission seam required by privacy stop.

## Required tests and evidence

All fixtures use synthetic executable identities, synthetic window IDs, and unique development/test roots.

1. Clean locked restore, clean direct/transitive dependency audit, and Release build on Windows with zero warnings/errors.
2. Discovery produces eligible minimal descriptors but zero worker starts/frames; public models expose no title, command-line, raw-path, foreground, pixels, or notification fields.
3. Unknown and familiar targets require explicit consent; denied targets produce neither invitation nor capture; standing authorization works only after an explicit persisted policy change.
4. Corrupt/unsupported/oversized policy storage fails closed and cannot retain or create standing authority; failed save leaves the prior valid file and in-memory authority unchanged.
5. Only one validated target may be active. Discovery refresh and simulated foreground changes never change the active target; stale/reused handles fail before worker start.
6. The capture contract cannot start without a genuine authorization grant; a valid synthetic frame is tagged with exact session/generation/target identity.
7. Zero frames are admitted before authorization, from another target, after revocation, while denied, or after privacy rejection.
8. Privacy stop revokes first, cancels pending work, pauses live memory admission, stops/clears the worker, drops a deliberately late frame, and is idempotent.
9. Explicit resume alone creates a strictly newer generation after target/topology revalidation; a second hotkey press never resumes.
10. Worker stop/clear failure remains fail-closed and blocks resume until cleanup succeeds.
11. One-monitor guard blocks discovery authorization/resume under zero, unknown, or multiple displays and privacy-stops an active session after a topology change.
12. Standard-target clearly-sensitive assessments are rejected before a downstream spy; trusted-game bypass never bypasses target/session/generation checks.
13. `Ctrl+Shift+F12` registration/unregistration and collision failure are deterministic at the Win32 adapter seam; UI reports unavailable registration without crashing.
14. All 125 accepted Task 1–3 tests pass alongside the focused Task 4 suite. Windows-only WPF process tests remain part of the exact-candidate CI gate.

## Allowed change scope

- new `src/CompanionCore.Privacy/**`, `src/CompanionCore.TargetAuth/**`, and `src/CompanionCore.TargetAuth.Windows/**` projects;
- new corresponding test projects and synthetic fixtures;
- `src/CompanionCore.Capture.Contracts/**`, `src/CompanionCore.Capture.Fake/**`, and capture tests for authorization-required metadata-only worker contracts;
- `src/CompanionCore.Memory/**` and memory tests only for the shared privacy-generation/live-write admission fence; no schema, journal, backup, or repair protocol changes;
- `src/CompanionCore.Presentation/**`, `src/CompanionCore.App/**`, and their tests only for neutral Task 4 status/control wiring and the Windows hotkey/display-message adapter;
- `CompanionCore.slnx`, affected project files/locks, `README.md`, `BUILD_LEDGER.md`, this packet/archived Task 3 packet, and `tasks/review/HANDOFF.md`.

Do not modify Task 1 lifecycle/single-instance behavior, accepted memory schemas/protocols, CI semantics, the Design BunDex, roadmap, master packet, architecture, prior archived/paused packets, or unrelated files.

## Paw Gate

Task 4 passes only when:

- consent is structurally required before any worker start/frame, and each admitted frame proves exact target + current session/generation;
- denied/default-sensitive targets, stale targets, foreground changes, and additional displays cannot leak authority or retarget capture;
- stop-only privacy behavior is revocation-first, clears/cancels disposable work, pauses live writes, rejects late work, and requires explicit fresh-generation resume;
- the Windows shell visibly reports exact target and hotkey availability while remaining neutral and title-free;
- actual diff review finds no real capture, full-screen fallback, private metadata, Task 5+ behavior, API/personality, or production surface;
- fresh exact-candidate Windows CI passes locked restore, dependency audit, Release build, every accepted regression, and the complete Task 4 suite;
- limitations and Personal Round Judgments are recorded before Task 5 becomes active.

## Personal Round Judgment log

### J1 — Split platform-neutral authority from the Windows metadata adapter

The authorization state machine, generation fencing, policy store, and synthetic tests remain `net10.0`; only minimal Win32 discovery/topology/hotkey adapters target Windows. This preserves deterministic non-Windows verification and keeps native enumeration separate from authority. It can be recombined later without changing contracts.

### J2 — Title-free exact target descriptors

Task selection uses executable filename, PID, opaque HWND, and a SHA-256 fingerprint of the normalized executable path. Window titles and raw paths are unnecessary for the acceptance scenarios and can contain unrelated private text, so they are not collected. If later usability evidence proves a title is necessary, that requires a separately reviewed privacy-aware display-only design.

### J3 — One runtime privacy generation shared with live-write admission

Capture grants/metadata and live-memory admission use the same monotonic runtime generation authority. Privacy stop revokes synchronously before asynchronous cleanup, preventing late work from becoming current. Task 7 must carry this generation into semantic/write proposals; Task 4 only adds the current pause fence and metadata-frame enforcement.

### J4 — Persist policy identity without raw executable paths

The development/test policy file stores a normalized-path fingerprint and filename rather than a raw path. This supports exact local matching without retaining usernames or directory text in authorization settings. Code-signing/publisher identity hardening remains a later security-gate concern; unknown/replaced targets still fail to per-session consent rather than capturing automatically when identity does not match.

### J5 — Explicit no-target privacy resume creates no capture authority

The global stop chord can be pressed before any target exists or after a paused target is explicitly ended. The same explicit Resume control may therefore clear the runtime write pause with no target, after the stop/clear fence and one-display check succeed. That path creates no target session, authorization grant, worker start, or frame. When a paused target does exist, resume still requires exact target revalidation and creates a fresh target generation as specified.

### J6 — Repeated stop advances the revocation fence but remains stop-only

Every privacy-stop press leaves the runtime paused and performs no resume, target creation, worker start, or frame release, so its externally authorized effect is idempotent. Each press still advances the monotonic paused generation. That narrow internal advance invalidates an explicit-resume attempt that began before a later stop press and prevents the older attempt from winning the race.

### J7 — Worker-start cancellation is a synchronous authority event

The controller registers a narrow cancellation fence only while an authorized worker start is in progress. Cancellation synchronously privacy-pauses/revokes the generation and cancels target work before awaiting ordinary cleanup, so a worker that ignores cancellation cannot emit one final current frame. Once start has crossed its checked completion boundary, the caller token is detached and cannot later revoke an established session accidentally.

### J8 — Explicit policy denial is serialized with target control

The shipping policy mutator is exposed through `TargetSessionController`, not directly on the policy catalog. If the changed executable is the currently authorized one and the new category is `Denied`, the controller revokes first, cancels work, stops/clears metadata, and drains admitted work before persisting the denial. A save failure therefore leaves the prior policy unchanged but the runtime safely paused; a denied transition cannot race one more admitted frame or leave the worker running.

## Deferred Findings

- Real WGC target capture, minimized/exclusive-fullscreen feasibility spikes, out-of-process worker IPC, image rings, and resource soak testing remain Task 5.
- Pixel/OCR sensitivity classification remains with the visual pipeline; this task establishes only the fail-closed Privacy Guard policy/admission seam.
- API/semantic-result generation propagation and stale-result memory rejection remain Task 7; Task 4 provides the shared generation authority they must consume.
- Persistent production policy roots, installer behavior, code-signing/publisher identity, and Builder-to-Companion migration remain later guarded stages.
- Multi-monitor accompaniment remains deferred; this task only blocks/pause-stops when exactly one display cannot be proven.
