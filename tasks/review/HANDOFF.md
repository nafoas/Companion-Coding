# Task Handoff

## Task

WIRE-02 — Windows Platform Signals and App Composition (`tasks/active/wire-02-app-composition.md`).

- Branch: `agent/wire-02-app-composition`.
- Base: `main` `1250602`.
- Builder: Claude.

## Changed

- **New `CompanionCore.Platform.Windows`:**
  - `WindowsPlatformSignals`, behind `IWindowsPlatformNative`;
  - `WindowsPlatformNative` (Win32: foreground identity, last-input tick, window existence and hung state, a synchronize/limited-query process handle).
- **New `CompanionCore.Platform.Windows.Tests`:** 22 tests, fake native plus real Win32 on Windows.
- **App:**
  - `CompanionComposition`: the host, the offline Braincase, platform signals, the tick, and `SystemEvents`;
  - `App.xaml.cs` wiring, including `--test-mode=wired`;
  - a `MainWindow` companion status line.
- **Presentation:**
  - `IPersonalityAdapter.Map(CompanionNotice)`;
  - the neutral mapping;
  - family placeholders.
  - New `CompanionNoticeMappingTests`.
- **Orchestration:** additive `OrchestratorSnapshot.ConversationLineage`; Scenario 7 asserts it.
- **App integration tests:**
  - `WiredCompositionTests`;
  - a shared real-App collection;
  - `AppProcess.Run` extra arguments.
- Solution, lock files, and the packet.

## Verification

- **Local:**
  - 0 warnings, 0 errors, 0 vulnerable packages;
  - 893/893 on Linux.
- **Mutation pass:** 34 mutants, 34 killed after three test sharpenings.
- **Windows CI** on `e63242c`: push run `38078377838` and PR run `38078393543` each passed **908/908**, with verified artifacts and the expected attention-sheet PNG digest.
- **Real App:** two launches restored the identical conversation lineage with zero faults.

## Remaining

- CI on the evidence head, the merge-ref check, merge, closure, and post-merge `main` CI.
- Then the regression audit of every prior task and the roundtable with Boss.

## Risks and assumptions

- WIRE-02 D1–D4.
- Polling intervals and every numeric bound are provisional until Stage 11.

## Personal Round Judgments

WIRE-02 J1–J10, recorded in the packet.

## Review focus

- Foreground minimization and the absence of title, text, or hook APIs.
- Strict exit ordering.
- Test-root isolation.
- The non-blocking notice marshalling.
- The shutdown order.

## Repository state

- Branch `agent/wire-02-app-composition`, based on `main` `1250602`.

## Next safe task

The regression audit of all prior tasks, then the roundtable.
