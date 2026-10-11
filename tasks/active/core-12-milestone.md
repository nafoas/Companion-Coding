# CORE-12 — Core Prince Milestone: Placeholder Controls and Guided Run (Stage 12, part 3)

Status: **active — gate review**
Authorized: 2026-10-11 by Boss ("Let's try Stage 12 … Tell me when you need me, until then you can work").
Accepted remote base: `3a7b67e170ebbd628161a66eaa110a655d9d705c`
Working branch: `agent/core-12-milestone`
Builder: Claude

## Objective

The Roadmap's Stage 12 milestone is a stable placeholder-interface build that demonstrates ten checks end to end. Passing them is the Core Bun Approval Hop. This packet builds what the hand-run needs, and an automated chained version of the same ten checks. The Approval Hop itself is Boss's (D1).

## Scope

- **App window: a "Companion commands (placeholder)" panel.** It uses neutral wording only, and is enabled only once the companion host exists (`MainWindow.AttachCompanion`).
  - **Reply:** Click, Yes/no, Acknowledge, Substantive (the one Conversation Thread), plus a conversation lock.
  - **Quiet check:** Keep watching, Naptime, Back eventually.
  - **After exit:** Consolidate, Wait for relaunch, Preserve paused.
  - **Watch task:** add by event key.
  - **Take photograph.**
  - **Vault:**
    - **Export** uses a save dialog. The export must be a new file outside the data root.
    - **Repair** is opt-in with a confirmation. It runs only when no target is active, and only continues the current line of memories.
  - **Braincase status:** offline, or Claude live with the daily Bun Budget, plus any settings issue.
  - **A recent-notice log,** newest first and bounded at 200.
- **The App wires the panel to the composition.** The `wired` test-mode marker now also reports `BRAINCASE:` and `COMMANDS:`.
- **`docs/Core-Prince-Milestone.md`:** Boss's guided hand-run of the ten checks.

## Evidence

- **`CoreMilestoneWalkthroughTests.TheTenMilestoneChecks_RunInOnePrincesLife`** (Linux), in one Prince's life:
  - consent;
  - Bnuy Mode through a completed watch task, which is also the Watchbun background event;
  - an interpreted look, conversation notices, an appended memory, and a reply to the thread;
  - an API-outage nap;
  - Privacy Loaf, and an explicit resume of the same session;
  - session end with consolidation and Vault backup;
  - a restart with the same lineage and memories;
  - a Boss-initiated repair that keeps everything;
  - no faults, and no sheet bytes on disk.
- **Windows App integration (CI):**
  - `wired` reports `BRAINCASE:offline COMMANDS:True` and creates the settings file with live calls off;
  - `Wired_WithLiveCallsTurnedOn_SelectsClaude_WithoutMakingAnyCall` reports `BRAINCASE:live COMMANDS:True` with 0 faults, and captures nothing.

## Personal Round Judgments

- **J1 — Thin placeholder glue.** Each button calls one existing orchestrator or host command and shows its typed result. No authority or behavior lives in the window.
- **J2 — Repair stays opt-in, with confirmation.** This matches Boss's continuity rule: nothing restores or rolls back on its own.

## Known observations

- **One unidentified local failure.** During repeated local runs, one run out of about 87 had one failing Orchestration test. That came from a 5-run loop, followed by 52 sequential and 30 concurrent runs that were all clean. The failing test's name was not captured, and the failure did not recur under logging, including 3-way concurrent load. Notice ordering in the new walkthrough was reviewed and is deterministic. CI will be watched for any recurrence; if it recurs, it is root-caused, not re-run.

## Paw Gate

Pending (CI). The milestone's acceptance is Boss's hand-run (D1).

## Deferred findings

- **D1 — The Core Bun Approval Hop:** Boss's hand-run of `docs/Core-Prince-Milestone.md`, with live calls on after the LIVE-02 first-run check.
