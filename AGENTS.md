# Agent Instructions

These rules apply to every coding agent and subagent in this repository.

## Required reading order

1. `tasks/active/` — the only authorized current task.
2. `docs/Claude-Companion-Core-Task-Packet.md` — neutral-core requirements.
3. `docs/Prince-Construction-Roadmap.md` — stage dependencies and gates.
4. `docs/Prince-Design-BunDex.md` — behavioral authority and future compatibility.
5. `BUILD_LEDGER.md` — accepted state and known limitations.

## Scope

- Work on exactly one explicitly assigned task.
- Do not begin a later task without foreman approval.
- Put later discoveries in the active task's Deferred Findings section.
- Preserve unrelated user changes.
- Do not weaken invariants or acceptance tests to make work pass.
- Stop and ask when architecture, privacy, authority, or specification conflicts.

## Core-only boundary

During neutral-core construction, do not implement character personality, themed speech, final labels, animation, audio, final UI, or decorative behavior. Use the presentation adapter and neutral placeholders. Behavioral contracts required by the design must still be implemented.

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

- Claude works on `claude/task-XX-short-name`.
- Subagents may work only on bounded subtasks; the lead Claude instance integrates and verifies their work.
- Subagents do not merge directly into `main`.
- A task is not accepted until the foreman independently reviews the diff and reruns relevant tests.
- Complete `tasks/review/HANDOFF.md` before requesting review.

## Credit-aware stopping

If remaining usage is insufficient to implement and verify the current task:

- do not start a large new edit;
- reach a safe checkpoint;
- report the credit-related stop;
- record completed work, files, tests, remaining work, risks, commit, and next safe action;
- never claim unrun tests or a gate approval.

The foreman follows the same reciprocal handoff rule.
