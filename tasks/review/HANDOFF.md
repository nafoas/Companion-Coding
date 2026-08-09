# Task Handoff

## Task

Task 0 — Repository survey and architecture proposal, per `tasks/active/task-00-architecture.md`. Produce an evidence-based Windows architecture proposal for the neutral Companion Core without implementing product behavior.

## Completed

Wrote `docs/architecture/task-00-architecture-proposal.md`, covering all 15 required outputs from the task file: repository inventory; recommended stack (C#/.NET 8/WPF) with alternatives and tradeoffs; capture technology analysis across visible/occluded/minimized/borderless/exclusive-fullscreen target states; local database and journal choice (SQLite + NDJSON journal); process/worker isolation design; a mock/replay-first `ISemanticProvider` interface; module dependency diagram; proposed source/test directory structure; build/test/packaging/Windows-version strategy; a four-boundary privacy/threat summary; a risk register with feasibility spikes and stop conditions; the staged dependency map; an explicit invariant/non-goal acknowledgement; and five open decisions flagged for foreman approval before Task 1.

## Changed

- `docs/architecture/task-00-architecture-proposal.md` (new) — the Task 0 deliverable described above.

No other files were touched. `AGENTS.md`, `BUILD_LEDGER.md`, the design/roadmap/packet docs, and `tasks/active/task-00-architecture.md` are unmodified, per the task's allowed-changes list.

## Verification

No automated tests were run — this task is documentation-only by design ("Do not implement product behavior yet"; "No product code is committed"). No test suite exists yet to run. Verification for this task is a review of the document's content and completeness, not a command.

## Remaining

- Nothing remaining within Task 0's scope.
- Five open decisions are flagged in §15 of the proposal and need foreman sign-off before Task 1 scaffolding begins (WPF vs. WinUI 3; in-process vs. cross-process `CaptureWorker` isolation; SQLite vs. an alternative embedded store; the proposed minimized/exclusive-fullscreen handling ceiling; whether Task 2 and Task 4 may be parallelized).

## Risks and assumptions

- Assumed Windows 10 1903+ as the minimum supported OS version, since that's the floor for `Windows.Graphics.Capture`; not yet confirmed by the foreman.
- Assumed no existing preference for the embedded database (proposed SQLite); flagged as open decision #3 in case there's prior context this proposal doesn't have.
- The exclusive-fullscreen and minimized-window capture limitations (§4, §12) are real ceilings of the Windows capture APIs, not gaps in this proposal's research — flagged explicitly rather than glossed over, per the task's requirement to state unsupported/minimized capture limitations honestly.
- No feasibility spikes have actually been run yet (e.g. capturing a minimized test window on the real target OS build) — the risk register proposes them for early Task 5, not before Task 0's approval, since the task explicitly excludes project scaffolding beyond non-executable architecture examples.

## Review focus

- Whether the module boundaries in §6 and §8 actually make "second identity" and "write-gate bypass" structurally impossible rather than just conventionally discouraged.
- Whether the capture-limitation ceilings in §4 (minimized windows, exclusive fullscreen) are an acceptable answer or whether the foreman wants a spike run before Task 0 is accepted rather than deferred into Task 5.
- The five open decisions in §15 — these are the items most likely to change downstream module contracts if answered differently than proposed.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`.
- Reviewed commit: `4c9e0d555ffc4d67207ef306b2d1817b7f328789` ("task-00: add architecture proposal for neutral companion core"), on top of `a80eb5c` (the foreman's baseline: `docs: add companion architecture and gated build workflow`).
- Worktree is clean except for this handoff file itself, which this commit will also include.

## Next safe task

Smallest safe next action, not yet started: once the foreman approves the stack and the five open decisions in §15, begin Task 1 (reproducible skeleton) on a task-scoped branch, scaffolding only `CompanionCore.App`, `CompanionCore.Runtime`, and `CompanionCore.Presentation` with the single-instance guard and blank WPF shell described in §9 of the proposal.

## Credit status

Not credit-related. This is a normal task-boundary stop: Task 0 is complete and the packet requires stopping for reviewer approval before any product code begins.
