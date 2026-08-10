# Task 1 — Reproducible Neutral Skeleton Adoption

Status: **active**
Authorized: 2026-08-10 under accepted R0 direct-build controls
Accepted base: `74e68218ffa7ef7680701c03971916278535c16c`
Working branch: `agent/task-01-skeleton`
Preserved source checkpoint: `da4797e1a3df2c6f0ddaaa0248098fd40f656121`

## Objective

Adopt and independently revalidate the smallest reproducible Windows skeleton proving one local runtime, deterministic lifecycle behavior, a neutral presentation seam, and a synthetic capture-worker contract. This is infrastructure only; it must not observe, remember, contact, or imitate anyone.

The preserved implementation is a review candidate, not accepted work. Import only Task 1 product/build/test files, review the exact diff against the current accepted base, and require fresh Windows evidence from the new pull request.

## Entry evidence

- Task 0 architecture and R0 direct-build/continuity controls are accepted on `main`.
- Closed PR #3 preserved a Task 1 implementation. Its last product-code SHA was independently reviewed and its final head added only a handoff update.
- Historical Windows evidence at the final checkpoint was 58/58 tests: Runtime 24, Presentation 19, Capture 11, and WPF integration 4, plus locked restore and Release build with zero warnings/errors.
- Historical results do not pass this gate; the exact adopted candidate must pass again.

## Adoption procedure

1. Start from the accepted base above, never from the retired Claude task branch.
2. Import only `.github/workflows/ci.yml`, solution/build files, `scripts/**`, `src/**`, and `tests/**` from the preserved checkpoint.
3. Do not import its stale `BUILD_LEDGER.md`, active/archive task moves, governing documents, or handoff.
4. Compare imported file hashes with the preserved checkpoint and explain any deliberate difference.
5. Inspect the full current-base product diff, including every process-launch/test-mode path.
6. Make only corrections required for this task's acceptance gate.

## Required implementation

### Toolchain and solution

- C# with WPF on .NET 10 LTS.
- Pin one explicit .NET 10 SDK patch in `global.json`; no floating major alias.
- Commit a reproducible solution, project references, locked dependency strategy, local build/test scripts, and Windows GitHub Actions CI.
- A clean checkout on a supported Windows runner must build and test without credentials, network calls, capture permissions, or production data.

### Neutral application shell

- One WPF application process.
- A deliberately plain shell with one blank text box, one neutral placeholder icon, one neutral status display, and only minimal start/nap/wake/stop controls.
- No final names, themed labels, character wording, decoration, animation, or polished layout.

### One runtime and lifecycle

- Construct exactly one authoritative `CompanionRuntime` in the application composition root.
- All windows resolve the same runtime instance; no window, worker, or view model constructs another.
- Enforce second-process single-instance behavior before runtime or subsystem initialization.
- Implement deterministic start, nap, wake, and stop transitions with explicit, testable invalid-transition handling.
- Shutdown is idempotent, cancels owned work, disposes resources, and leaves no worker or application process behind.

### Presentation contracts

- Define `IPersonalityAdapter`, `NeutralPersonalityAdapter : IPersonalityAdapter`, and rendering-only `IPresentationSink`.
- Implement architecture §6.2.1 exactly:
  - cold `start` → `lifecycle.started`, no expression intent;
  - recovered `start` → `lifecycle.recovering`, `recovering`;
  - `nap` → `lifecycle.napping`, no intent;
  - valid `wake` from nap → `lifecycle.waking`, no intent;
  - `stop` → `lifecycle.stopped`, no intent;
  - unknown events or invalid preconditions → `lifecycle.unknown`, no intent, with raw event name available only to opt-in diagnostics and never rendered.
- Keep placeholder strings neutral and swappable. The sink renders opaque content/intents and never generates, interprets, or filters personality.

### Capture contract and fake

- Define the bounded, cancellable `ICaptureWorker` contract needed by later tasks.
- Provide only an in-process synthetic fake/test double.
- It may emit deterministic synthetic status/frame metadata needed for tests, but no real pixels or private content.
- It must not construct identity/runtime state, write memory, use a network, inspect windows, or start another process.

### Diagnostics and test-only process surface

- Structured local diagnostics are off by default and enabled only through an explicit development/test switch.
- Never log credentials, private content, screenshots, raw unknown-event names without that switch, or future production-root paths.
- Diagnostics are local and non-networked.
- Any shipping-binary `--test-mode` or equivalent path must be inert with respect to identity, capture, persistence, privacy, network, and content. It may expose only bounded lifecycle/window mechanics required by integration tests, must be explicit, and must not weaken ordinary startup guards.

## Explicitly forbidden

Do not implement or introduce:

- real screen/window capture, WGC, D3D, HWND discovery, authorization, or privacy-hotkey behavior;
- an out-of-process worker implementation or worker executable;
- SQLite, `MemoryStore`, `SessionJournal`, backup, restore, migration, or any Task 2+ persistence;
- API bridges, semantic providers, network clients/calls, credentials, protected credential storage, or live model access;
- personality, Prince-specific/themed text, final labels, character prompts, or behavioral generation;
- production data-root discovery or fallback;
- animation, sprite integration, audio, microphone access, game integration, polished UI, packaging/distribution, or updater work;
- Task 2 work or convenience scaffolding whose only consumer is a later task.

## Required tests and evidence

The handoff names exact commands and results. Tests cover:

1. clean locked restore and full Release build on Windows;
2. no-key/no-network launch to neutral ready state with no capture or production-data access;
3. one runtime across multiple windows and construction count of one;
4. deterministic second-process behavior before another runtime initializes;
5. exact §6.2.1 lifecycle content keys/intents, valid and invalid transitions;
6. idempotent normal/cancellation shutdown with no leftover worker or app process;
7. deterministic bounded/cancellable in-process fake and absence of real/out-of-process capture;
8. diagnostics silent by default and available only under the explicit switch;
9. `--test-mode` or equivalent cannot cross its allowed lifecycle/window-only surface;
10. full Windows CI suite on the exact PR candidate.

No automated test may require paid access, secrets, capture hardware, a live desktop beyond the bounded WPF integration harness, or a production data root.

## Allowed change scope

- `.github/workflows/ci.yml`;
- `global.json`, `CompanionCore.slnx`, `Directory.Build.props`;
- `scripts/build.ps1`, `scripts/test.ps1`;
- `src/CompanionCore.App/**`;
- `src/CompanionCore.Runtime/**`;
- `src/CompanionCore.Presentation/**`;
- `src/CompanionCore.Capture.Contracts/**`;
- `src/CompanionCore.Capture.Fake/**`;
- Task 1-focused tests under `tests/**`;
- `BUILD_LEDGER.md`, `README.md`, this task/archived packet, and `tasks/review/HANDOFF.md` for control and gate records only.

Do not modify the Design BunDex, roadmap, direct workflow, neutral-core packet, accepted architecture, archived Task 0/R0 packets, paused historical Task 1 packet, or unrelated files.

## Paw Gate

Task 1 passes only when:

- the source import contains only the allowed preserved product/build/test paths;
- imported file contents match the preserved final checkpoint except for reviewed, explained corrections;
- the actual current-base diff satisfies the task and contains no Task 2+ behavior;
- process/test-mode, single-runtime, diagnostics, network, capture, and production-root boundaries pass code review;
- fresh exact-candidate Windows CI passes locked restore, Release build, all 58 or more tests, and WPF integration;
- no invariant or test is weakened;
- handoff, limitations, and any Personal Round Judgments are recorded;
- the Build Ledger records acceptance before Task 2 becomes active.

## Personal Round Judgment log

No material judgment yet. Routine mechanical adoption uses the already accepted Task 1 design; any divergence is logged here before the gate.

## Deferred Findings

### D1 — Roadmap Stage 1 is broader than the accepted Task 1 contract

The roadmap's Stage 1 still asks for an event bus/state store, a Conversation Thread placeholder, packaged Prince messages, and restart recovery of a tiny checkpoint. The more specific accepted architecture/master Task 1 and both the historical and current active packets instead define a neutral no-persistence skeleton; they place `SessionJournal`/transactional checkpoint work in Task 2 and the Conversation Coordinator in Task 9, while core-only rules prohibit Prince-authored messages now.

This is a specification conflict, not permission to pull Task 2/9 work forward. Task 1 implementation and CI may proceed, but its Paw Gate cannot be declared accepted until Boss chooses whether to amend the stale Stage 1 roadmap wording (recommended) or deliberately expand/re-sequence the active task.
