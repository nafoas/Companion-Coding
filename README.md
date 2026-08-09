# Companion Coding

A staged, local-first Windows companion-engine project.

The repository currently contains architecture and construction controls only. Product implementation begins with Task 1 only after Task 0's architecture proposal passes review.

## Builder workflow

1. Read `AGENTS.md`.
2. Read `docs/Claude-Companion-Core-Task-Packet.md`.
3. Work on exactly the task in `tasks/active/`.
4. Use a dedicated `claude/task-XX-name` branch.
5. Run and report tests honestly.
6. Complete `tasks/review/HANDOFF.md`.
7. Stop for foreman review; do not self-advance.

## Current task

Task 0: repository survey and architecture proposal. No product code yet.

## Important boundaries

- Neutral utilitarian core first; personality and final presentation later.
- Development data and eventual production data must remain physically separate.
- Durable identity and memory are local and append-only to automated/API systems.
- Capture requires authorization and remains restricted to one target.
- Live paid API use is reserved for the final core gate; earlier work uses mocks and replay fixtures.

See the documents in `docs/` for the complete specification and roadmap.
