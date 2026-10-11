# Task Handoff

## Task

CORE-12 — Core Prince Milestone: Placeholder Controls and Guided Run (`tasks/active/core-12-milestone.md`), Stage 12 part 3.

- Branch: `agent/core-12-milestone`.
- Base: `main` `3a7b67e`.
- Builder: Claude.

## Changed

- **App:**
  - the placeholder commands panel in `MainWindow.xaml` and `.cs`;
  - `AttachCompanion`;
  - the `wired` marker reports the Braincase and commands.
- **New doc:** `docs/Core-Prince-Milestone.md`.
- **Tests:**
  - `CoreMilestoneWalkthroughTests` (1);
  - `WiredCompositionTests` extended, plus 1 new Windows test.

## Verification

- **Local:** full gate green: 1026/1026 on Linux, 0 warnings, no vulnerable packages.
- **Windows CI:** pending.

## Remaining

- CI, merge, and closure.
- Then Boss's live first-run check (LIVE-02 D1) and the milestone hand-run (CORE-12 D1).

## Next safe task

None without Boss: the Core Bun Approval Hop gates Stage 13.
