# Task 1 — Reproducible Neutral Skeleton

Status: **paused; authorization withdrawn**  
Originally authorized from accepted `main`: `58bbdf9915659b3887e311f7b9cdf819cc39fc13`  
Paused by user: 2026-08-09  
Preserved checkpoint: closed, unmerged PR #3 at `da4797e1a3df2c6f0ddaaa0248098fd40f656121`

## Official Bnuy Backpedal™

The user ended the Claude–ChatGPT collaborative construction workflow because its coordination and resource overhead did not provide a noticeable improvement over a simpler approach.

This task is preserved only as a reference checkpoint. It is not active, accepted, merge-authorized, or permission to continue. Do not resume Task 1, begin Task 2, or restart autonomous monitoring without a new user-approved plan and a new active task packet.

## Objective

Build the smallest reproducible Windows skeleton that proves one local runtime, deterministic lifecycle behavior, a neutral presentation seam, and a synthetic capture-worker contract. This is infrastructure only; it must not observe, remember, contact, or imitate anyone.

## Required reading and start procedure

1. Read `AGENTS.md`, this task, the neutral-core packet, construction roadmap, Design BunDex, `BUILD_LEDGER.md`, and the accepted Task 0 architecture.
2. Fetch the newly accepted `main` and create `claude/task-01-skeleton` from it. Do not continue on the Task 0 branch.
3. Keep exactly this one task active.
4. Preserve unrelated changes and use synthetic fixtures only.

## Required implementation

### Toolchain and solution

- C# with WPF on .NET 10 LTS.
- Pin one explicit .NET 10 SDK patch in `global.json`; do not use a floating major alias.
- Commit a reproducible solution, project references, dependency lock strategy, local build/test scripts, and Windows GitHub Actions CI.
- A clean checkout on a supported Windows runner must build and test without credentials, network calls, capture permissions, or production data.

### Neutral application shell

- One WPF application process.
- A deliberately plain shell containing:
  - one blank text box;
  - one neutral placeholder icon;
  - one neutral status display;
  - only the minimal controls needed to exercise start, nap, wake, and stop.
- No final names, themed labels, character wording, decoration, or polished layout.

### One runtime and lifecycle

- Construct exactly one authoritative `CompanionRuntime` in the application composition root.
- All windows resolve the same runtime instance; no window, worker, or view model may construct another.
- Enforce second-process single-instance behavior before any runtime or subsystem initialization.
- Implement deterministic start, nap, wake, and stop transitions with invalid transitions handled explicitly and testably.
- Shutdown must be idempotent, cancel owned work, dispose resources, and leave no worker or application process behind.

### Presentation contracts

- Define `IPersonalityAdapter`, `NeutralPersonalityAdapter : IPersonalityAdapter`, and rendering-only `IPresentationSink`.
- Implement the normative Task 1 mapping in architecture §6.2.1:
  - cold `start` → `lifecycle.started`, no expression intent;
  - recovered `start` → `lifecycle.recovering`, `recovering`;
  - `nap` → `lifecycle.napping`, no intent;
  - valid `wake` from nap → `lifecycle.waking`, no intent;
  - `stop` → `lifecycle.stopped`, no intent;
  - unknown events or invalid preconditions → `lifecycle.unknown`, no intent, with the raw event name available only to opt-in diagnostics and never rendered.
- Keep placeholder strings neutral and swappable. The sink renders opaque content/intents only and never generates, interprets, or filters personality.

### Capture contract and fake

- Define the bounded, cancellable `ICaptureWorker` contract needed by later tasks.
- Provide only an in-process synthetic fake/test double.
- The fake may emit deterministic synthetic status/frame metadata needed for tests, but no real pixels or private content.
- The fake must not construct identity/runtime state, write memory, use a network, inspect windows, or start another process.

### Diagnostics

- Structured local diagnostics must be off by default and enabled only through an explicit development/test switch.
- Never log credentials, private content, screenshots, raw unknown-event names unless the explicit diagnostics switch is enabled, or future production-root paths.
- Task 1 diagnostics are local and non-networked.

## Explicitly forbidden

Do not implement or introduce:

- real screen/window capture, WGC, D3D, HWND discovery, authorization, or privacy-hotkey behavior;
- an out-of-process worker implementation or worker executable;
- SQLite, `MemoryStore`, `SessionJournal`, backup, restore, migration, or any Task 2+ persistence;
- API bridges, semantic providers, network clients/calls, credentials, protected credential storage, or live model access;
- personality, Prince-specific/themed text, final labels, character prompts, or behavioral generation;
- animation, sprite integration, audio, microphone access, game integration, polished UI, packaging/distribution, or updater work;
- Task 2 work or convenience scaffolding whose only consumer is a later task.

Interfaces required above must remain narrow; do not pre-build later implementations behind them.

## Required tests and evidence

The handoff must name exact commands and results. Tests must cover:

1. **Clean build:** a clean checkout restores from declared dependencies and builds the full solution on Windows.
2. **No-key/no-network launch:** the app starts and reaches its neutral ready state with no API key, no network call, no capture, and no production-data access.
3. **One runtime across windows:** opening multiple application windows yields the same single `CompanionRuntime` instance and construction count remains one.
4. **Second-process behavior:** attempting a second application process does not initialize another runtime; it exits or activates the first instance deterministically.
5. **Deterministic lifecycle:** the complete valid start/nap/wake/stop sequence and invalid transitions produce the exact §6.2.1 content keys/intents every run.
6. **Clean shutdown:** normal and cancellation-path shutdowns are idempotent and leave no synthetic worker task, child process, or application process behind.
7. **Worker boundary:** the synthetic fake is in-process, deterministic, bounded/cancellable, and the solution contains no real-capture or out-of-process worker implementation.
8. **Diagnostics default:** structured diagnostics are silent by default and available only under the explicit switch.

CI must run the build and automated test suite on Windows. No automated test may require paid access, secrets, capture hardware, or a live desktop.

## Allowed change scope

- `global.json`, solution/project/dependency files;
- `src/CompanionCore.App/**`;
- `src/CompanionCore.Runtime/**`;
- `src/CompanionCore.Presentation/**`;
- `src/CompanionCore.Capture.Contracts/**`;
- `src/CompanionCore.Capture.Fake/**`;
- Task 1-focused tests under `tests/**`;
- build/test scripts and Windows CI under `scripts/**` and `.github/workflows/**`;
- `tasks/review/HANDOFF.md`;
- this task file only for Deferred Findings.

Do not modify `AGENTS.md`, the authoritative design/roadmap/master packet, accepted architecture, archived tasks, or `BUILD_LEDGER.md`.

## Acceptance gate

Task 1 passes only after the foreman independently inspects the actual diff and handoff and reruns relevant tests. Claude may not self-approve, merge, or begin Task 2.

## Handoff and stop rule

Before review:

1. complete `tasks/review/HANDOFF.md` with files changed, design decisions, exact commands/results, remaining work, risks, commit SHA, branch, next safe action, and credit status;
2. push `claude/task-01-skeleton`;
3. open a draft PR against the newly accepted `main`;
4. stop for foreman review.

If credits become insufficient, reach a safe checkpoint and use the reciprocal credit-aware handoff rule in `AGENTS.md`; do not claim unrun tests or gate approval.

## Deferred Findings

Record later-stage discoveries here without implementing them.
