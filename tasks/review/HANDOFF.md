# Task Handoff

## Task

LIVE-02 — Sign-in, Dollar Bun Budget, Composition, and First-Run Check (`tasks/active/live-02-signin-budget.md`), Stage 12 part 2.

- Branch: `agent/live-02-signin-budget`.
- Base: `main` `e8d3d7e`.
- Builder: Claude.

## Changed

- **Bridge:** `DailyCostBudget`; the journal keeps the day's output units; a reported input count replaces the estimate.
- **Adapter assembly:**
  - `AnthropicSignIn`;
  - `AllowAmbientCredentials`;
  - `AnthropicPricing`;
  - `BuilderBraincaseSettings`;
  - `BuilderBraincase.Compose`;
  - `BraincaseLiveCheck`.
- **Api:** IVT to the adapter assembly (strict parser and request construction only).
- **App:** composition selects Builder Prince's Braincase from his settings and passes the bridge budget.
- **New tool:** `tools/CompanionCore.BraincaseCheck`.
- **New doc:** `docs/Builder-Braincase-Setup.md`.
- **Tests:**
  - `BridgeCostBudgetTests` (8);
  - `BuilderBraincaseTests` (14);
  - the journal compaction test extended.

## Verification

- **Local:** full gate green: 1025/1025 on Linux, 0 warnings, no vulnerable packages.
- **Mutation:** 19 mutants, 19 killed.
- **Offline check-program smoke run:** passed.
- **No live call has been made, and no real key exists in this environment.**

## Remaining

- CI, merge, and closure.
- Then Boss's first live run (D1).
- CORE-12.

## Next safe task

CORE-12 (the placeholder controls and milestone run can be built before Boss's live run).
