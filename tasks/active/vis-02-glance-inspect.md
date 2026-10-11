# VIS-02 — Glance (peripheral vision) and Inspect (focus) (Vision upgrade, part 2)

Status: **in progress**
Authorized: 2026-10-11 by Boss:

- "an efficient Haiku handler could work … I want this done now, with greatly reduced latency".
- "a Glance is sorta like his peripheral vision, as long as it doesn't overwrite his main focus (only add to it), then it works. Don't make him scatterbrained, but equally don't give him tunnel vision".

Accepted remote base: `df470e4e0cf4e0b6ee4af3638095d4a03436be44`
Working branch: `agent/vis-02-glance`
Builder: Claude

## Objective

The Design BunDex's "Prince Glance" was agreed as a cheap, frequent look that asks whether something is happening and where, and when to look again. It escalates to a detailed look only when something is interesting. VIS-01's Sniff notices locally. The Glance now understands quickly, with Haiku. The Inspect, the detailed model, stays his main focus and the only source of memory proposals.

## Scope

1. **One Braincase, two lanes.** `ApiBridge.GlanceAsync` adds a Glance lane next to the Inspect lane: at most one Glance and one Inspect at a time.
   - **A Glance is one attempt with a short timeout** (`GlanceTimeout`, 5 s): a stale glance is useless.
   - **The strict Glance schema carries no memory proposals at all:** interest, confidence, the changed region (one of the sheet's regions), a short neutral label, escalate, and next-look seconds.
   - **Credential echoes are rejected,** as in the Inspect lane.
   - **Glances share the nap:** a napping Braincase sends nothing. Only the Inspect lane probes; an outage or rate limit on a Glance enters the same nap.
2. **One Bun Budget in dollars.** `DailyCostBudget` gains Glance prices. The journal records Glance usage in its own day counters through a new `Glanced` entry. Old journals keep verifying, because null fields are not written.
3. **Provider.** `AnthropicSemanticProvider` answers Glance requests with the Glance model (default `claude-haiku-5-5`): thinking off, low effort, a small output cap, neutral Glance instructions, and the strict Glance schema.
4. **Orchestrator** (enabled by `OrchestratorOptions.Glance`; the App turns it on when a Glance model is configured).
   - **The routine look is a Glance.** It is due at once on a sniff trigger, otherwise at the time the last Glance recommended, clamped to 2–30 s (the agreed adaptive-cadence table). Before any recommendation exists, it falls back to the state's pace.
   - **Peripheral, not overwriting.** A Glance result is attention evidence: a changed-region interest event with a score ceiling below Bnuy Mode. It never cancels or replaces a running Inspect, and Glances keep running during an Inspect, so there is no tunnel vision.
   - **Escalation, not scatterbrain.** An escalating or interesting Glance (≥ 0.7) asks for one Inspect. At most one Inspect waits; further escalations fold into it.
   - **Bnuy Mode keeps its focused Inspects** at the event floor.
5. **Builder settings.**
   - `braincase-settings.json` gains `glanceModel` (default `claude-haiku-5-5`; `""` turns the Glance off).
   - The budget prices both models.
   - `BraincaseCheck --live` makes five Glance calls as well and prints each one's latency and the median. This is Boss's real measurement.

## Acceptance

- **Lanes.** A Glance runs while an Inspect is in flight; a second Glance is Busy. Nothing is sent while napping, and the budget counts both lanes in dollars. A Glance can never propose a memory, an unknown field rejects the response, and a privacy stop fences it.
- **Journal.** `Glanced` usage survives a reopen and a compaction, and an old journal written before VIS-02 still verifies.
- **Provider.** A Glance request uses the Glance model, disabled thinking, and the Glance schema, and is wrapped in the local envelope.
- **Orchestrator.**
  - A sniff trigger glances at once, and a Glance's next-look recommendation is clamped and honored.
  - An escalation produces exactly one Inspect, even after several escalations.
  - A Glance during an Inspect leaves the Inspect's result intact, and a Glance alone never enters Bnuy Mode.
  - With the Glance off, everything behaves as before.
- **The whole suite passes.**

## Personal Round Judgments

_(recorded during implementation)_

## Deferred findings

- **Glance latency on Boss's PC** is measured by `BraincaseCheck --live`; the numbers decide any tuning.
- **VIS-03:** local text reading. **VIS-04:** the bundled local scene model.

## Paw Gate

_(pending)_
