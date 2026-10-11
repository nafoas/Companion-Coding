# VIS-02 — Glance (peripheral vision) and Inspect (focus) (Vision upgrade, part 2)

Status: **accepted** — Paw Gate PASS; PR #67 squash-merged as `da7ea8293c0fce7f46fe3aa86b45af4ea2429c9c`.
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

- **J1 — One Braincase, two lanes, one journal.** A second bridge would have split the dollars-per-day budget and the nap into two ledgers. The Glance lane shares both, through a dedicated slot.
- **J2 — A Glance has no Started entry.** It has no side effects to recover; a single `Glanced` entry records its usage. A crash mid-Glance can lose one Glance's accounting (about $0.0001).
- **J3 — Only the Inspect lane probes a nap.** Napping is one state owned by one lane. A Glance outage or rate limit enters the same nap; a timeout or transient failure is just a missed Glance.
- **J4 — A Glance carries no memories.** It sends an empty resume packet and uses a schema with no proposals. Any extra field, including an attempted memory, rejects the whole Glance.
- **J5 — A Glance label is never shown.** It only becomes a bounded, neutral attention topic (`glance:…`). The decoded label is checked for credential echoes.
- **J6 — A Glance's attention ceiling sits below Bnuy Mode.** Only an Inspect's understanding, or decisive evidence, enters it, matching the Sniff.
- **J7 — Escalation queues one flag, not a list.** "Don't make him scatterbrained": however many Glances escalate during an Inspect, one more Inspect follows.
- **J8 — The Glance is off unless configured.** It is on in the App when live calls are on and a Glance model is set. With it off, behavior is exactly as before, which every earlier test still proves.
- **J9 — Haiku with thinking off at low effort.** That is the fastest allowed setting. No refusal fallback exists for Haiku, so a declined Glance is missed.

## Mutation pass

- **31 mutants.** 30 were killed; 1 survives as deliberate defense in depth: the raw-text credential check, since the decoded-label check catches every echo too.
- **The first round found four test gaps:** timeout duration, the old journal shape after compaction, the 30 s ceiling clamp, and the ceiling masked by habituation. All four were fixed, and their mutants are now killed.

## Deferred findings

- **Glance latency on Boss's PC** is measured by `BraincaseCheck --live`; the numbers decide any tuning.
- **VIS-03:** local text reading. **VIS-04:** the bundled local scene model.

## Paw Gate

**PASS.**

- **CI.** Head `d6a20c51c418d8be0869bb044b1cbfd513b66195` passed push run `38167891536` and PR run `38167905535` at 1102/1102, with verified artifacts.
- **Local.** The full gate passed: 1084/1084, 0 warnings, no vulnerable packages.
- **Merge ref.** Parents were exactly `df470e4` and `d6a20c5`, and the tree was equal.
- **Merge.** Squash-merged through the expected-head fence.
