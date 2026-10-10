# Task Handoff

## Task

Task 9 — Conversation Coordinator and Seed Banks (`tasks/active/task-09-conversation-coordinator.md`), Roadmap Stage 7. Branch `agent/task-09-conversation-coordinator`, based on accepted `main` `cf034f60ef15e175d071bf624d960ed8188beb06`.

ERPP-01 follows as its own companion packet.

Builder: Claude.

## Completed

- New `CompanionCore.Conversation`: a deterministic, I/O-free `ConversationCoordinator`. It provides:
  - exactly one thread, with game ranked above initiated;
  - the Player Conversation Lock;
  - independent, bounded, deduplicating game and initiated Seed Banks with eviction;
  - a semantic-scan game clock and a 30 s (offset 15 s) initiated clock with bounded catch-up;
  - one expression gate, with game context favored;
  - state-dependent expression chance;
  - an Afterglow opening guarantee;
  - ambient commentary, pep talks, and urgent alerts that become a thread only through explicit promotion;
  - substantive-only seeding;
  - idle settling;
  - urgent hold, then resume offer, then resume or settle;
  - ask-first for sensitive seeds and threads;
  - neutral presentation counting, retiring a seed after three;
  - a positive-only engagement profile;
  - follow-up saturation;
  - Brain Fart conditions;
  - a full, validated, serializable checkpoint and restore.
- New `CompanionCore.Conversation.Tests`: 44 deterministic tests covering acceptance scenarios 1–12 and all seven Stage 7 Paw Gate scenarios.

## Changed

- `CompanionCore.slnx`
- `src/CompanionCore.Conversation/**` (7 files, including the lock file)
- `tests/CompanionCore.Conversation.Tests/**` (4 files, including the lock file)
- this handoff and the active packet

## Verification

Local, on the pinned SDK 10.0.302 (Linux cross-build):

- Locked restore passed and the audit of all 26 projects is clean.
- The strict Release build had 0 warnings and 0 errors.
- 533/533 executable tests passed:

  | Suite | Tests |
  |---|---|
  | Api | 128 |
  | Attention | 49 |
  | Capture | 14 |
  | Capture Worker | 68 |
  | Conversation | 44 |
  | Memory | 68 |
  | Presentation | 50 |
  | Privacy | 13 |
  | Runtime | 26 |
  | TargetAuth | 73 |

- Mutation pass: 32 of 32 coordinator rules are killed, after four test strengthenings and one rule correction (J4).
- Windows CI on implementation head `a0e5efe`: push run `38053076912` (job `114215981421`) and PR run `38053089128` (job `114216016451`) each passed 546/546 on the first attempt (Conversation 44/44).
  - Both artifact digests verified on each path.
  - The attention-sheet PNG keeps the accepted digest `5eb11c96…b046dd`.

## Remaining

- CI on this evidence descendant, the merge-ref check, merge, closure records, and post-merge `main` CI. Then ERPP-01.

## Risks and assumptions

- All numbers are provisional calibration (J2).
- The coordinator is not yet wired, and transcript durability belongs to ERPP-01.

## Personal Round Judgments

J1–J8 are recorded in the packet.

## Review focus

- Priority and lock checks.
- `Advance` expiry, idle, and resume ordering.
- Seeding qualification.
- Neutral non-response.
- Checkpoint validation.

## Repository state

- Branch `agent/task-09-conversation-coordinator`, draft PR #27. Implementation `a0e5efe`, then this evidence descendant.

## Next safe task

Complete this Paw Gate. Then open ERPP-01 (Session Transcript Continuity) through its own packet.
