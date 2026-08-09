# Shared Codebase Workflow — Foreman and Claude

## Source of truth

Use one Git repository. The accepted `main` branch is the only source of truth and contains only work that passed review and its current gate.

Recommended layout:

```text
AGENTS.md
BUILD_LEDGER.md
docs/
  Prince-Design-BunDex.md
  Prince-Construction-Roadmap.md
  Claude-Companion-Core-Task-Packet.md
  architecture/
tasks/
  backlog/
  active/
  review/
tests/
src/
```

Personality-bearing implementation is absent from neutral-core stages. Design documents remain as authority, while Claude works from the personality-neutral task packet.

## Branch model

- `main`: accepted stages only; never direct experiments.
- `claude/task-XX-short-name`: Claude's bounded implementation branch.
- `foreman/review-XX-short-name`: optional review/fix branch.
- One active stage at a time.
- One lead writer per task/module at a time.

Claude may use internal subagents or worktrees for bounded subtasks, but Claude consolidates them into one coherent task branch, runs the complete tests, and presents one handoff. Subagents never merge directly to `main`.

## First exchange

1. Initialize the repository with the design, roadmap, task packet, and workflow files.
2. Create `tasks/active/task-00-architecture.md` containing Task 0.
3. Claude performs Task 0 only: survey and architecture proposal, no product implementation.
4. Claude commits the proposal and writes `tasks/review/HANDOFF.md`.
5. Boss makes the commit, diff, or repository available to the foreman.
6. The foreman reviews architecture, risks, contracts, and assumptions.
7. Findings return as a numbered review in the task file.
8. Claude responds on the same branch.
9. After approval, merge or squash to `main`, update the Build Ledger, and create Task 1.

## Task packet contract

Every active task declares:

- objective and entry criteria;
- allowed modules/files;
- required interfaces and invariants;
- explicit non-goals;
- acceptance scenarios and test commands;
- expected handoff artifacts;
- credit-risk estimate when known.

Claude does not broaden the task. Later discoveries go into `Deferred findings`.

## Commit discipline

- Preserve unrelated user changes.
- Keep generated output, secrets, production data, screenshots, databases, and credentials outside Git.
- Commit lockfiles and schema migrations deliberately.
- Prefer small coherent commits within a task branch.
- Use messages such as `task-03: add append-only journal recovery`.
- Do not rewrite accepted `main` history.
- Do not merge with failing tests or an unexplained dirty worktree.

## Claude completion handoff

Claude updates `tasks/review/HANDOFF.md` with:

1. task ID and objective;
2. completed behavior;
3. files/modules changed;
4. commands run and exact results;
5. incomplete or deferred work;
6. risks, blockers, and assumptions;
7. branch and commit SHA;
8. review focus;
9. next smallest safe task;
10. whether the stop was caused by approaching credits.

## Foreman review

The foreman reads the task, diff, and handoff; verifies scope and invariants; reruns focused and regression tests; inspects privacy, memory authority, resource lifetime, cancellation, and failure behavior; and records evidence-backed findings. Claude never self-approves a gate. The foreman never approves on description alone.

## How Boss transfers work between models

Preferred order:

1. a shared private Git remote accessible to both environments;
2. a repository archive containing `.git` and the working tree;
3. a commit patch plus the exact base commit and handoff.

Chat messages alone are not a codebase transfer. The receiving model needs the actual repository state or a verifiable diff against a known base.

If no shared remote exists, Claude can produce:

```text
git status --short
git rev-parse HEAD
git diff --stat <accepted-base>..HEAD
git bundle create task-XX.bundle <accepted-base>..HEAD
```

Do not include credentials, production data, caches, or captured images.

## Review loop

```text
Approved main
  -> Claude task branch
  -> tests and handoff
  -> foreman review
  -> Claude corrections when needed
  -> independent verification
  -> gate approval
  -> merge to main
  -> next task
```

## API timing

All stages before the final hardening gate use deterministic mock and replay semantic providers. No dedicated key or paid call is required. The real remote provider and protected key are enabled only at the final core gate, after local authority, privacy, memory, retry, cancellation, and failure contracts pass with mocks.

## Production continuity boundary

Construction uses a development data root and synthetic memory. After the neutral core passes, Builder data is frozen and excluded. Production begins with a clean data root and thereafter survives every hotfix through migrations and backups. Development tools must not open production data by default or through accidental path fallback.
