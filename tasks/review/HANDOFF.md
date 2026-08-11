# Task Handoff

## Task

Task 4 — Consent and Target Isolation. Implementation is locally complete on
`agent/task-04-consent-target-isolation`; publication, exact Windows CI, and the
distinct Paw Gate review remain pending.

## Completed

- Added a platform-neutral privacy-generation authority. Privacy stop revokes first,
  pauses new live-write/frame admission, waits already-admitted work to drain, and
  requires explicit fresh-generation resume.
- Added a platform-neutral target-authorization core with exactly four categories,
  conservative built-in sensitive classifications, hard browser denial, one exact
  active target, per-session consent, explicit standing policy, and final handle/PID/
  executable-fingerprint revalidation.
- Added a versioned, canonical, checksummed, size/entry-bounded development/test policy
  file with same-directory atomic promotion. It stores only fingerprint, filename,
  authorization category, and content policy; corruption/version/size/tamper failures
  discard all stored authority while retaining built-in denial.
- Added a Windows-only metadata adapter that enumerates visible, unowned, uncloaked,
  non-tool top-level windows; excludes this process and unprovable identities; and
  queries no titles, foreground identity, command lines, accessibility content,
  thumbnails, pixels, or capture APIs.
- Replaced targetless synthetic worker start/restart with an unforgeable sealed grant.
  Grant issue/resume/revoke operations and the grant stored in results/session state
  are non-public, so ordinary callers cannot bypass the controller clear fence.
- Added exact session/generation/target metadata admission, live current-policy denial,
  one-display checks, and the Privacy Guard seam. Standard sensitive/unavailable input
  fails closed; trusted-game policy bypasses only content filtering.
- Added revocation-first controller orchestration, bounded one-attempt cleanup retry,
  late-frame rejection, target-work cancellation, explicit target end/replacement,
  explicit no-target runtime resume with no capture authority, and serialized disposal.
  Worker-start caller cancellation now revokes synchronously before an ignoring worker
  can emit, and reports the resulting privacy-paused state visibly.
- Routed shipping policy mutation through the serialized controller. Denying the active
  executable now revokes/cancels/stops/clears before persistence, while a failed save
  retains the prior policy and leaves the runtime safely paused.
- Added `Ctrl+Shift+F12` registration and WPF message wiring. Registration collision,
  missing HWND/source, unexpected native failure, exact message ownership, and
  deterministic unregister behavior are typed/visible; the chord is stop-only.
- Added display-change handling that stops active target work without auto-resume and
  frame-time topology checks that fail closed before the message handler runs.
- Extended the neutral WPF shell with title-free target discovery/selection, policy,
  consent, stop, explicit resume, end-target, exact current-session status, separate
  selection status, and hotkey availability. No themed/final presentation was added.
- Added the shared privacy admission seam to `LocalWriteGate`; an already-admitted
  append may finish and is drained, while new/stale-generation writes are rejected.
- Added synthetic race/negative/public-surface tests for stop-versus-resume,
  end-versus-authorization, cancellation, stale/reused targets, policy tamper/failure,
  wrong target/session/generation, policy becoming denied, monitor changes, worker
  faults, live-write drainage, bounded metadata, and hotkey failure paths.

## Changed

- `src/CompanionCore.Privacy/**` and `tests/CompanionCore.Privacy.Tests/**` — shared
  generation/admission authority and fail-closed local content-policy seam.
- `src/CompanionCore.TargetAuth/**` and `tests/CompanionCore.TargetAuth.Tests/**` —
  policy storage, discovery/authorization contracts, one-target session authority,
  controller, admission gate, and synthetic acceptance/race tests.
- `src/CompanionCore.TargetAuth.Windows/**` — minimal Win32 discovery, display topology,
  and global-hotkey adapters.
- `src/CompanionCore.Capture.Contracts/**`, `src/CompanionCore.Capture.Fake/**`, and
  capture tests — sealed target grant, exact metadata, stop-and-clear, bounded buffer.
- `src/CompanionCore.Memory/**` and memory tests — only the shared live-write privacy
  admission lease and tests; schemas, journal, backup, and repair protocols are unchanged.
- `src/CompanionCore.Presentation/**`, `src/CompanionCore.App/**`, and corresponding
  tests — neutral typed status, shell controls, shared composition, WPF hotkey/display
  boundary. Existing single-instance/runtime behavior remains shared and unchanged.
- `CompanionCore.slnx`, affected project files/locks, `README.md`, `BUILD_LEDGER.md`,
  Task 4/archived Task 3 control records, and this handoff.

No CI workflow, architecture, roadmap, Design BunDex, accepted archive/repair protocol,
production root, credential, network/API, semantic, conversation, personality, real
capture, screenshot, pixel buffer, animation, audio, or private fixture was added.

## Verification

- Accepted base: `f685dd2023a5844309c5b5fb7d0abd1bf54406b9`; Task 4 control commit
  `de169b2` activates the sole current packet.
- Locked restore passed for all 16 projects with exact SDK 10.0.302:
  `dotnet restore CompanionCore.slnx --locked-mode -m:1 -p:RestoreDisableParallel=true -p:EnableWindowsTargeting=true`.
- Release cross-build passed for all 16 projects with 0 warnings and 0 errors:
  `dotnet build CompanionCore.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false -p:EnableWindowsTargeting=true -p:UseSharedCompilation=false`.
- The pre-review candidate passed all 226/226 locally runnable tests: Runtime 26,
  Capture 14, Memory 68, Presentation 50, Privacy 13, and TargetAuth 55.
- Actual-diff review then added two TargetAuth race cases (worker-start cancellation and
  active-policy denial) and hardened the corresponding source. The refreshed execution
  environment no longer contains the pinned `dotnet` SDK, so no post-hardening local
  result is claimed; exact-candidate Windows CI must run all expected 240 cases.
- The Windows-only app/integration assembly previously cross-built successfully and
  declares 12 cases (four accepted real-WPF process regressions plus eight Task 4 Win32
  adapter cases). Execution remains part of the pending Windows gate.
- `git diff --check` passes. Static review finds only the allowlisted target metadata,
  display, and hotkey native imports; there is no capture/title/foreground native API.
- Local `dotnet package list --vulnerable --include-transitive` could not complete because
  this environment cancelled its network approval before a decision. No clean local
  vulnerability result is claimed; the unchanged permanent Windows CI audit is required.
- `dotnet format --verify-no-changes` could not connect to its sandbox-denied Roslyn
  build-host pipe (`SocketException: Permission denied`). No formatter result is claimed;
  all compilers report zero warnings and `git diff --check` is clean.

## Remaining

- Commit the reviewed implementation, publish a draft PR, and run the exact Windows
  workflow at that immutable candidate head.
- Require locked restore, the permanent direct/transitive vulnerability audit, Release
  build with zero warnings/errors, and all expected 238 Windows tests including the
  four real WPF process cases.
- Perform the distinct actual-diff Paw Gate review against current accepted `main`, fix
  any finding without weakening tests/invariants, and rerun affected/full gates.
- Record exact head/tree/run/job/artifact evidence and the final 240-test breakdown in
  this handoff and `BUILD_LEDGER.md`.
  Task 4 must not be marked accepted or merged until those checks pass.

## Risks and assumptions

- Real capture and pixels intentionally do not exist. WGC/minimized/fullscreen behavior,
  out-of-process IPC, image buffers, and resource soaks remain Task 5.
- Win32 enumeration, hotkey registration, WPF process launch, and Windows file semantics
  are compiler-checked locally but require the named Windows execution oracle.
- Executable-path fingerprint plus filename is the Task 4 development identity. Publisher/
  code-signing hardening and production policy migration remain later security work.
- Filename classification is conservative, not exhaustive. Unrecognized executables
  remain `UnknownAsk`, so misses require consent and never become implicit authority.
- The no-target Resume path reopens only runtime write admission after clear/topology
  checks and creates no target, grant, worker start, or frame (Personal Round Judgment J5).
- Repeated stop advances the paused generation to defeat an in-flight older resume while
  remaining externally idempotent and strictly stop-only (J6).

## Review focus

- Prove every worker start is downstream of explicit/standing authorization and a
  hidden genuine grant, and every admitted frame matches the current target/session/
  generation/policy/topology before downstream delivery.
- Prove a stop, end, policy denial, stale handle, cancellation, validation error, monitor
  change, or worker fault cannot leave or recreate current capture authority.
- Recheck the review corrections: cancellation during an ignoring worker start must
  revoke before its late frame, and an active executable's denial must revoke/clear in
  the same serialized controller operation before the policy becomes live.
- Prove stop is synchronous-revocation-first, cancels target work, clears bounded
  metadata, drains admitted writes, rejects late work, and cannot toggle into resume.
- Prove policy parsing/promotion is canonical, bounded, fail-closed, and path-free, with
  browser/default-sensitive denial surviving invalid persisted state.
- Review the native import list and public reflection tests for title, raw-path,
  foreground, full-screen/capture, grant-issuer, and low-level resume leakage.
- Recheck the actual changed-path set contains no Task 5+, API, personality, production,
  credential, private-content, or unrelated accepted-protocol change.

## Repository state

- Branch: `agent/task-04-consent-target-isolation`.
- Accepted base: `f685dd2023a5844309c5b5fb7d0abd1bf54406b9` (`origin/main`).
- Control commit: `de169b2`; the reviewed implementation is not yet committed or
  published at the time of this checkpoint.
- Exactly one packet is active: `tasks/active/task-04-consent-target-isolation.md`.
- No unrelated user changes were found in the fresh Task 4 worktree.

## Next safe action

Commit the exact local candidate, publish a draft PR, and require the full Windows gate
before beginning the distinct Paw Gate review. Do not begin Task 5.

## Credit status

No credit-related stop. Local construction and verification are complete; platform CI
and review remain available.
