# Task Handoff

## Task

Task 0 — Repository survey and architecture proposal, per `tasks/active/task-00-architecture.md`. This handoff covers revision 3, addressing the foreman's PR #1 re-review of revision 2 (four bounded corrections plus one minor cleanup item).

## Completed

Revised `docs/architecture/task-00-architecture-proposal.md` to address the foreman's PR #1 review of commit `7d8681f`:

1. **Backup-cut protocol** (§5.2) — the prior revision named the backup mechanism but never defined the cut between "what's in the backup" and "what's still only in the journal," so a write committed after the snapshot began but before rotation could theoretically be lost. Specified an explicit six-step protocol: establish a momentary cut sequence, drain all frames at-or-before it into SQLite, snapshot, record the cut in the manifest, validate and atomically promote, then rotate only frames at-or-before the *promoted* backup's cut. This makes "no post-cut event can be discarded by that backup's rotation" true by construction rather than by convention.
2. **Presentation diagram** (§8) — the diagram mislabeled the sink as "NeutralPresentationSource" and never actually showed `IPersonalityAdapter` in the flow, even though §6.2 (from revision 2) correctly described the two-contract split. Redrew the diagram so the path is explicit: typed events/context → `IPersonalityAdapter` (neutral passthrough during core stages) → opaque content/intents → `IPresentationSink` (renders only).
3. **Memory-authority wording** (§11, trust boundary 4) — corrected "`LocalWriteGate` is the sole writer to `MemoryStore`," which contradicted the separately-approved `MaintenanceStore` capability in §6.3. Now states `LocalWriteGate` is the sole *live-runtime/automated/API* writer, `MaintenanceStore` is the only other writer and is unreachable from semantic/API components, and explicitly distinguishes `BackupRecoveryService`'s read-only snapshot from restore/migration's actual write path.
4. **Task execution map** (§13) — replaced the branching dependency diagram, which contradicted the "tasks are strictly sequential" claim, with two things: §13.1, the binding linear order `0 → 1 → 2 → ... → 12` (the only thing that governs what may start next), and §13.2, the old branching graph kept only as an explicitly labeled non-authoritative reference for understanding code-level dependencies.
5. **Minor cleanup** — my own `tasks/review/HANDOFF.md` (revision 2) mischaracterized risk-register rows 11–13 as "pre-Task-5 feasibility spikes"; they're actually tied to Task 3 (backup race), Task 5 (worker IPC contract test), and Task 4/Task 7 (generation-fencing test) respectively. Corrected below.

## Changed

- `docs/architecture/task-00-architecture-proposal.md` — revised in place per the four numbered items above; the revision note at the top now points at this round.
- `tasks/review/HANDOFF.md` (this file) — rewritten for revision 3, including the corrected risk-register labeling.

No other files were touched. `AGENTS.md`, `BUILD_LEDGER.md`, the design/roadmap/packet docs, `tasks/active/task-00-architecture.md`, and `tasks/review/FOREMAN_REVIEW.md` are unmodified.

## Verification

No automated tests were run — this remains a documentation-only task; no source tree or test suite exists yet. Verification is a content review against the four PR review items, traced one-by-one above, plus a full re-read of the revised document for internal consistency (diagram, §13.1/§13.2 cross-references, §11 boundary wording against §6.3).

## Remaining

Nothing remaining within this revision's scope. Per the foreman's instruction ("Do not begin Task 1. Revise only the architecture proposal and handoff, push one bounded commit, and stop for re-review."), no Task 1 scaffolding was started.

## Risks and assumptions

- Corrected labeling: risk-register rows 11 (Task 3), 12 (Task 5), and 13 (Task 4/Task 7) are gate-specific acceptance/contract tests, not a fifth and sixth "pre-Task-5 spike" alongside rows 1–2 (which genuinely are pre-Task-5 spikes). Only rows 1 and 2 are pre-Task-5 feasibility spikes; rows 11–13 are ordinary per-task acceptance tests scheduled at their respective gates.
- The backup-cut protocol (§5.2) is specified but unimplemented and untested — Task 3 is where it gets built and where the "no post-cut event can be discarded" property gets an actual test, not just an argument.
- Carried forward from revision 2: assumed the foreman's R8/this round's item 4 means "no parallel task *branches* may start," not "no internal parallelism within one gated task" (e.g. subagents on bounded pieces of a single authorized task) — §13.1's binding order is about start sequencing between tasks, and I've kept reading it that way; flagging again in case it's wrong.

## Review focus

- Whether §5.2's six-step cut protocol actually closes the gap the foreman identified, or whether a specific interleaving (e.g. a crash between step 1's cut and step 6's rotation) is still underspecified.
- Whether §8's redrawn diagram now matches §6.2's contract description precisely enough to scaffold Task 1's `CompanionCore.Presentation` module directly from it.
- Whether §13.1/§13.2's split (binding linear order vs. non-authoritative logical reference) is the right way to resolve "sequential in principle, branching in the diagram," or whether the foreman would rather the logical graph be dropped entirely to avoid any future ambiguity.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`; tracked by GitHub PR #1 (draft, base `main`), which is where this round's review comments were left.
- This revision is committed on top of `7d8681fcae0520e23d5829d71418809871c852d8` (revision 2, "task-00: revise architecture proposal per foreman review (R1-R10)"), which sits on `d668d3d` (foreman's `FOREMAN_REVIEW.md`) and `7060465`/`4c9e0d5` (original Task 0 submission).
- Worktree is clean except for this handoff file and the revised proposal, both included in this commit.
- Subscribed to PR #1 activity — future foreman review comments should arrive as webhook events rather than requiring a manual repo poll.

## Next safe task

Smallest safe next action, not yet started: await the foreman's re-review of this revision on PR #1. If approved, begin Task 1 (reproducible skeleton) exactly as scoped in §9/§15 of the proposal — `CompanionCore.App`, `CompanionCore.Runtime`, `CompanionCore.Presentation` (now correctly `IPersonalityAdapter` + `IPresentationSink`, not a single "NeutralPresentationSource"), `CompanionCore.Capture.Contracts`, and `CompanionCore.Capture.Fake`, with the single-instance guard and blank WPF shell — and nothing from `CompanionCore.Capture.Worker` (that's Task 5+, per R4/§6.1).

## Credit status

Not credit-related. Normal task-boundary stop, per the foreman's explicit instruction to push one bounded commit and stop for re-review.
