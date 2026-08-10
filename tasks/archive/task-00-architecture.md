# Task 0 — Repository Survey and Architecture Proposal

Status: **accepted and archived**  
Reviewed builder SHA: `9a51231aa8d76a399ef43ae4a8bfc5cdbd1195b3`  
Independent approval review: `4892489052`
Merged to `main`: `58bbdf9915659b3887e311f7b9cdf819cc39fc13`

## Objective

Produce an evidence-based Windows architecture proposal for the neutral Companion Core. Do not implement product behavior.

## Entry criteria

- Read `AGENTS.md`.
- Read all three authoritative documents in `docs/`.
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
15. decisions requiring independent gate approval.

## Allowed changes

- `docs/architecture/**`
- `tasks/review/HANDOFF.md`
- the active task file only for Deferred Findings

## Acceptance result

Passed on the exact reviewed SHA above. The accepted architecture is `docs/architecture/task-00-architecture-proposal.md`. No product code was included.
