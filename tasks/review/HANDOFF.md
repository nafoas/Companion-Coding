# Task Handoff

## Task

Task 0 — Repository survey and architecture proposal, per `tasks/active/task-00-architecture.md`. This handoff covers revision 5, addressing the foreman's PR #1 re-review of revision 4 (one final contract-definition gap; everything else confirmed passing).

## Completed

Added §6.2.1 to `docs/architecture/task-00-architecture-proposal.md`: a normative, implementable mapping table for `NeutralPersonalityAdapter`'s Task-1 scope. The foreman's point was that "deterministically maps typed input to placeholder content" described *that* a mapping exists but not *what* it is — not enough for Task 1 to implement or test against. The new table covers every event Task 1 actually produces:

| Event | Content key | Expression intent | Context fields | 
|---|---|---|---|
| `start`, cold (no checkpoint) | `lifecycle.started` | none | `hadRecoveredCheckpoint = false` |
| `start`, checkpoint recovered | `lifecycle.recovering` | `recovering` | `hadRecoveredCheckpoint = true` |
| `nap` | `lifecycle.napping` | none | none |
| `wake` (valid, from `nap`) | `lifecycle.waking` | none | `priorState` |
| `stop` | `lifecycle.stopped` | none | `isCleanShutdown` (diagnostics only) |
| anything else / invalid transition | `lifecycle.unknown` | none | raw unrecognized event name (logged, never rendered) |

The mapping is stated as a pure, total function — deterministic, no clock/randomness dependence, and defined for every input including ones the table doesn't explicitly name (the last row is the required fallback). §8's presentation-flow diagram now points to §6.2.1 by reference instead of repeating "deterministically maps" without specifics, and clarifies that Task 1's actual event source is `CompanionRuntime`'s lifecycle states, not `AttentionEngine`/`ConversationCoordinator` (those don't exist until Tasks 8–9 — the general diagram shows the eventual full picture, but §6.2.1 scopes the table to what Task 1 alone needs).

## Changed

- `docs/architecture/task-00-architecture-proposal.md` — added §6.2.1 (normative mapping table); updated §8's presentation-flow block to reference it; updated the top-of-document revision note for revision 5.
- `tasks/review/HANDOFF.md` (this file) — rewritten for revision 5.

No other files were touched.

## Verification

No automated tests were run — still documentation-only. Verification: re-checked the table against §6.2's contract description (input/output types, "may pass expression intents through unchanged" — only the `recovering` row uses that, correctly), and against the packet's Task 1 acceptance criteria (lifecycle start/nap/wake/stop states, structured diagnostics behind a switch — the unknown-event fallback logs behind that same switch, doesn't add a new one).

## Remaining

Nothing remaining within this revision's scope. Per the foreman's instruction ("Update the handoff to identify that definition, then stop for re-review... Revise only the architecture proposal and `tasks/review/HANDOFF.md`, push one bounded commit, and stop. Do not begin Task 1."), no Task 1 scaffolding was started.

## Risks and assumptions

- Assumed the foreman's ask was scoped to Task 1's four lifecycle events plus a fallback, not the full eventual intent vocabulary (`observing`, `investigating`, `urgent`, `taking_note`, `privacy_paused`) — those belong to `AttentionEngine`/`ConversationCoordinator`, which are Task 8/9 deliverables and don't exist yet. Said this explicitly in §6.2.1's intro so it reads as a deliberate scope boundary, not an oversight, and flagging here in case the foreman actually wanted the full vocabulary sketched now.
- The `wake`-from-invalid-`priorState` case is specified as routing to the fallback row rather than, say, throwing — chose "never throw, always render something" over "surface the invalid transition loudly," consistent with the fallback's stated purpose (`IPresentationSink` must never be left with nothing to render). If the foreman would rather an invalid lifecycle transition be a louder failure (e.g. logged as an error, not just diagnostics), that's a small change to make in the next round.
- The actual placeholder strings behind each content key (e.g. what "Ready." or "Resuming from last checkpoint." might be) are explicitly left as Task 1 implementation detail, not fixed here — only the key → intent → context mapping is normative. Flagging in case the foreman wants literal strings pinned at the architecture stage rather than left to Task 1.

## Review focus

- Whether §6.2.1's table is actually sufficient for Task 1 to implement `NeutralPersonalityAdapter` directly against, or whether specific method signatures / a formal grammar are still needed before that's true.
- Whether scoping the table to only Task 1's four lifecycle events (deferring the full intent vocabulary to Tasks 8–9) is the right cut, per the first risk item above.

## Repository state

- Branch: `claude/multi-ai-code-collab-o5qhj1`; tracked by GitHub PR #1 (draft, base `main`).
- This revision is committed on top of `0d7fca6275e3354cc7826694fa988d7c0e1033f7` (revision 4), which sits on `5d82ee4` (revision 3), `7d8681f` (revision 2), `d668d3d` (foreman's `FOREMAN_REVIEW.md`), and `7060465`/`4c9e0d5` (original Task 0 submission).
- Worktree is clean except for this handoff file and the revised proposal, both included in this commit.

## Next safe task

Smallest safe next action, not yet started: await the foreman's re-review of this revision on PR #1. If approved, begin Task 1 (reproducible skeleton) exactly as scoped in §9/§15 of the proposal, implementing `NeutralPersonalityAdapter` directly against §6.2.1's table — `CompanionCore.App`, `CompanionCore.Runtime`, `CompanionCore.Presentation` (`IPersonalityAdapter`/`NeutralPersonalityAdapter` + `IPresentationSink`), `CompanionCore.Capture.Contracts`, and `CompanionCore.Capture.Fake`, with the single-instance guard and blank WPF shell — and nothing from `CompanionCore.Capture.Worker` (Task 5+, per §6.1).

## Credit status

Not credit-related. Normal task-boundary stop, per the foreman's explicit instruction to push one bounded commit and stop.
