# Task Handoff

## Task

No task is active. The last accepted packet is CORE-12 (`tasks/archive/core-12-milestone.md`), merged through PR #61 as `18b5215e3bc5810d76937465314a55ed57ba323a`.

Builder: Claude.

**Stage 12 is built and waits for Boss.**

## FIX-02 (accepted)

Main CI on `de2646a` failed one crash-loop test whose settle wait gave up by poll count on a slow runner. The test-only fix (PR #63, `288c81f`) waits by wall clock and reports bridge diagnostics on timeout. It passed 1044/1044 on both CI runs. Ledger entry: FIX-02.

## Changed (CORE-12 closure)

This docs-only reconciliation:

- archives CORE-12;
- updates `BUILD_LEDGER.md` and `README.md`;
- resets this handoff.

## Verification

- CORE-12 head `0d8b65b`, tree `c989179`: push run `38142489617` and PR run `38142501429` passed 1044/1044 with verified artifacts.
- The merge ref had exact parents and an equal tree.

## Remaining (Boss)

1. **The first live check.** Follow `docs/Builder-Braincase-Setup.md`: a Console account, then `setx ANTHROPIC_API_KEY` or `ant auth login`, then run `tools/CompanionCore.BraincaseCheck` with `--live` (and `--turn-on`).
2. **The milestone hand-run** (`docs/Core-Prince-Milestone.md`), and the **Core Bun Approval Hop**.

## Next safe task

None until the Core Bun Approval Hop. Stage 13, personality installation on Builder Prince, follows it.
