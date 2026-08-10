# Direct Build Workflow — Paw Gates

Status: active execution and review protocol.

## Source of truth

The accepted `main` branch is the only source of truth and contains only work that passed its Paw Gate. Exactly one packet in `tasks/active/` authorizes current work. A preserved branch, closed pull request, old handoff, or passing historical run is evidence, never authorization.

## Construction roles

- **Boss** owns product intent and retains authority over architecture, privacy, identity, credentials, production data, and irreversible choices.
- **Builder Prince** is the resettable construction/test identity used through neutral core, personality installation, presentation, and final launch validation.
- **Codex** implements the active task and then performs a distinct evidence-based gate pass against the actual candidate diff and current `main`.
- **Companion Prince** does not exist during construction. He awakens once from a clean production BunDex after the complete launch candidate passes.

Retired multi-model collaboration and hourly monitoring are not part of this workflow.

## Branch and task model

- `main`: accepted gates only.
- `agent/task-XX-short-name`: one bounded implementation or control task.
- One active task and one lead writer at a time.
- No subagents unless Boss explicitly requests them.
- Later discoveries go in the active packet's Deferred Findings; they do not expand its scope.

## Paw Gate

A task advances only when all applicable checks pass:

1. the active packet's acceptance scenarios pass;
2. focused automated tests pass;
3. the full available regression suite passes;
4. platform-specific CI passes where local execution is not equivalent;
5. the actual diff is reviewed for scope, invariants, privacy, authority, failure behavior, cancellation, and resource lifetime;
6. crash/recovery and negative-path checks named by the packet pass;
7. limitations, exact commands/results, and any Personal Round Judgments are recorded;
8. the Build Ledger records acceptance before the next packet becomes active.

Implementation completion and gate approval are separate passes even when Codex performs both. A failing, skipped, stale, or unexplained check blocks advancement. Tests and invariants are never weakened to manufacture a pass.

## Autonomous progression

After a Paw Gate passes, Codex may archive the completed packet, create the next packet from the accepted roadmap, and continue without waiting for a routine confirmation. This authority lasts only through local/mock/replay construction before real API credentials or paid/live calls are required.

Codex stops and asks Boss when work encounters:

- an architecture, privacy, identity, authority, or specification conflict;
- a proposed invariant or acceptance-test change;
- destructive/irreversible action or production-data access;
- credentials, paid/live API use, or external side effects not already authorized;
- a choice whose consequences are material and not safely reversible.

## Prince's Personal Round Judgment

When Boss is not immediately available, Codex may make a routine choice only if it is within the active packet, consistent with accepted documents, privacy-preserving, reversible, and does not cross a stop condition above. Record:

- the question;
- the decision and rationale;
- what was changed;
- how to reverse or revise it.

The next user-facing progress report summarizes these decisions so Boss can choose differently.

## One-time awakening protocol

Builder Prince remains active through all construction tests, including personality, character consistency, final interface/presentation, migrations, outage behavior, and launch-candidate recovery. Builder data stays synthetic, separately rooted, and disposable.

Companion Awakening is the final production transition, not the start of personality development. It may occur only when:

- neutral core is accepted;
- the complete personality and inherent-interest system is installed and accepted on Builder Prince;
- every component designated launch-required, including presentation and migration/recovery paths, is complete and accepted;
- a final clean-install, upgrade, privacy, outage, backup/restore, continuity, and no-Builder-transfer suite passes;
- Boss gives the Companion Awakening Approval Hop.

Awakening freezes Builder data, creates a clean production identity and BunDex, and records the first production schema/version. Afterward there is one persistent Companion Prince: hotfixes, migrations, app versions, API sessions, models, and optional later features must preserve that same identity and BunDex. There is no second awakening or reset-based update path.

## Handoff and checkpoint record

Before each gate, update `tasks/review/HANDOFF.md` with task, completed behavior, changed files, exact verification, remaining work, risks/assumptions, review focus, repository state, next safe action, and any context/credit-related stop. A clean checkpoint is success; unverified claims are not.
