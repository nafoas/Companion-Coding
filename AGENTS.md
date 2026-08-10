# Agent Instructions

These rules apply to every coding agent in this repository.

## Required reading order

1. `tasks/active/` — the only authorized current task.
2. `docs/Direct-Build-Workflow.md` — current execution and Paw Gate protocol.
3. `docs/Neutral-Core-Task-Packet.md` — neutral-core requirements.
4. `docs/Prince-Construction-Roadmap.md` — stage dependencies and gates.
5. `docs/Prince-Design-BunDex.md` — behavioral authority and future compatibility.
6. `BUILD_LEDGER.md` — accepted state, decisions, and known limitations.

## Scope

- Work on exactly one explicitly assigned task.
- Do not begin a later task until the current Paw Gate is recorded as passed.
- Put later discoveries in the active task's Deferred Findings section.
- Preserve unrelated user changes.
- Do not weaken invariants or acceptance tests to make work pass.
- Stop and ask when architecture, privacy, authority, or specification conflicts.
- Continue autonomously through accepted pre-API tasks after each recorded Paw Gate. Stop before real credentials, paid/live API use, or any newly discovered high-impact decision requiring user authority.

## Core-only boundary

During neutral-core construction, do not implement character personality, themed speech, final labels, animation, audio, final UI, or decorative behavior. Use the presentation adapter and neutral placeholders. Behavioral contracts required by the design must still be implemented.

Builder Prince remains the resettable construction identity during both neutral-core work and later personality/presentation installation, testing, and refinement. Companion Prince must not be started during construction.

## Repository safety

- Never commit credentials, API keys, production data, screenshots, databases, caches, build artifacts, or captured private content.
- Use synthetic fixtures only.
- Development/test code must not open an eventual production data root by default or fallback.
- Never run destructive Git commands against user work.
- Never rewrite accepted `main` history.

## Non-negotiable invariants

- One authoritative local runtime and identity.
- One active Conversation Thread.
- API/model sessions never own identity or durable memory.
- Automated/API memory writes are append-only proposals through a local allowlist.
- Committed memories are never automatically updated, deleted, or sacrificed for performance.
- No capture before authorization.
- Only the selected target may be captured.
- Tabbing away never changes the capture target.
- Non-response produces no negative preference or relationship update.
- Ordinary screenshots are bounded and RAM-only.
- Retries and recovery are bounded, transactional, and idempotent.

## Branch and review protocol

- Work on `agent/task-XX-short-name` branches; `main` contains accepted gates only.
- Do not delegate or create subagents unless the user explicitly requests it.
- Keep implementation and gate review as distinct passes: inspect the actual diff, rerun focused and regression tests, verify CI where platform-specific evidence is required, and record exact evidence.
- A task is not accepted merely because implementation is complete or a prior branch once passed. The current candidate must pass its own Paw Gate against the current accepted `main`.
- Complete `tasks/review/HANDOFF.md` before the gate review.

## Prince's Personal Round Judgment

When the user is unavailable, routine, reversible choices that stay inside accepted architecture and task scope may be decided autonomously. Record each material judgment in the active task and later summarize it to the user.

Personal Round Judgment may not authorize an invariant change, architecture/privacy/identity conflict, destructive or irreversible action, production-data access, credential handling, live or paid API use, or a weakened acceptance gate. Those require an explicit stop and user decision.

## One-time awakening boundary

- Builder Prince is the only identity used while building and validating the core, personality, presentation, and launch candidate.
- Companion Prince awakens exactly once, only after every launch-required system and personality/presentation Paw Gate has passed.
- Awakening creates a clean production BunDex; Builder memories and test artifacts never transfer.
- After awakening, all updates, repairs, migrations, model/API changes, and hotfixes preserve the same singular Companion Prince and production BunDex. There is no second awakening.

## Credit-aware stopping

If remaining usage is insufficient to implement and verify the current task:

- do not start a large new edit;
- reach a safe checkpoint;
- report the credit-related stop;
- record completed work, files, tests, remaining work, risks, commit, and next safe action;
- never claim unrun tests or a gate approval.

The same checkpoint rule applies before any anticipated context or service interruption.
