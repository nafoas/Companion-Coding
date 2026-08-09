# Task 0 — Repository Survey and Architecture Proposal

## Objective

Produce an evidence-based Windows architecture proposal for the neutral Companion Core. Do not implement product behavior.

## Entry criteria

- Read `AGENTS.md`.
- Read all three documents in `docs/`.
- Confirm that the repository contains planning/control files only.

## Required output

Commit an architecture proposal under `docs/architecture/` containing:

1. repository inventory;
2. recommended Windows language/runtime/UI stack;
3. credible alternatives and tradeoffs;
4. capture technology analysis for visible, occluded, minimized, borderless, and exclusive-fullscreen targets;
5. local database and crash-journal choice;
6. process and worker isolation design;
7. semantic-provider interface with mock/replay-first operation;
8. module dependency diagram;
9. proposed source/test directory structure;
10. build, test, packaging, and Windows-version strategy;
11. privacy and threat-boundary summary;
12. risk register with feasibility spikes;
13. staged dependency map;
14. explicit invariant and non-goal acknowledgement;
15. open decisions requiring foreman approval.

## Allowed changes

- `docs/architecture/**`
- `tasks/review/HANDOFF.md`
- this task file only to add Deferred Findings

Do not modify the authoritative design, roadmap, master task packet, `AGENTS.md`, or Build Ledger.

## Non-goals

- No application source code.
- No project scaffolding beyond architecture-only examples clearly marked non-executable.
- No API calls or credentials.
- No personality, animation, audio, UI design, or game integration.
- No selection-by-implementation: propose the stack and wait for approval.

## Acceptance criteria

- Recommendation addresses every required output.
- Alternatives are evaluated rather than dismissed.
- Unsupported/minimized capture limitations are stated honestly.
- Memory authority, consent, worker isolation, API statelessness, and dev/production separation are reflected in module boundaries.
- Risks have proposed proof-of-concept spikes and stop conditions.
- No product code is committed.
- `tasks/review/HANDOFF.md` is complete and names the reviewed commit.

## Deferred Findings

Add later-stage discoveries here without implementing them.
