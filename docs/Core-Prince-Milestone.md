# Core Prince Milestone (Stage 12) — Boss's hand-run

The Roadmap's Stage 12 milestone: ten checks on the real App, with the plain placeholder interface and no personality yet. Passing them is the **Core Bun Approval Hop**. The same ten checks also run automatically on every build (`CoreMilestoneWalkthroughTests`), on synthetic fakes. This hand-run is the real-world version.

## Before you start

- Builder Prince's live calls can be on or off; the setup is in `docs/Builder-Braincase-Setup.md`. With live calls off, Prince still runs every local check. Checks 3–4 need live calls on to produce real interpretations and memories.
- **Pick a game or app window you're happy for Prince to look at.** Only that window is ever captured.
- **What you'll see:** the window shows neutral status lines. The "Companion commands (placeholder)" panel lists recent notices, newest first. Each check names the notice to look for.

## The ten checks

1. **Invitation and target consent.**
   1. Press **Refresh targets** and pick the window.
   2. Set **Authorization**, then press **Apply policy**.
   3. Press **Authorize selected target**.

   Expect a `SessionStarted` notice.
2. **Noticing through Bnuy Mode.** Play normally for a minute. Expect `Attention` notices as Prince notices change. A big moment, or a completed watch task (check 5), takes him to the highest attention state.
3. **One ambient comment and one real Conversation Thread.** With live calls on, expect `Conversation` notices. Answer with the **Reply** buttons. The command line shows whether the thread accepted the reply.
4. **A substantive conversation becomes a memory.** With live calls on, a notable moment produces an appended memory. Prince never edits or deletes one.
5. **A Watchbun background event.**
   1. In **Watch task**, enter a key and press **Add watch task**.
   2. Tab away from the game.

   When the event happens, expect a Watchbun notice, alerting the target only and never stealing focus.
6. **A session ending and Adventure Record consolidation.**
   1. Close the game, or press **End target**.
   2. If asked, press **Consolidate** under **After exit**.

   Expect `Consolidated`, `VaultBackedUp`, and `SessionEnded`.
7. **Restart as the same Prince.** Close the App and open it again. Expect `Recovering` if a session was open. His memories and conversation history are the same, and nothing is captured until you authorize again.
8. **Da Bun Vault restoration (opt-in only).**
   1. With no target active, press **Repair Vault...** and confirm.
   2. Optionally, press **Export Vault...** first to keep a verified copy outside the data folder.

   Repair only ever restores what's missing from the latest backup and never removes newer memories.
9. **Privacy Loaf and API-outage Naptime.**
   - Press **Privacy stop** (or Ctrl+Shift+F12): capture stops at once, and `PrivacyPaused` appears. **Explicit resume** continues the same session.
   - For Naptime, disconnect the network briefly while live calls are on. Expect a Braincase napping notice. Nothing is lost, and he wakes when the service answers.
   - Spending past the Bun Budget also puts him to sleep until the next day.
10. **Stable resource use.** Leave Prince running through a normal play session. Optionally, start with `--calibration-log` and send the report (`docs/Calibration-Protocol.md`); it's a diagnostic now, not a gate.

## When you're done

Tell me, for each check: passed, failed (with what you saw), or skipped. When you're happy, give the **Core Bun Approval Hop**. Then Stage 13, personality installation on Builder Prince, can begin.
