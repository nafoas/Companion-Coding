# VIS-03 — The Text Sniff: noticing new words on screen (Vision upgrade, part 3)

Status: **in progress**
Authorized: 2026-10-11 by Boss ("Built in + local Vision AI that is just part of the app, not a separate download"; "see, notice, and respond, without an immersion killing delay").
Accepted remote base: `716930a1eef471cefce0144134636be0842dddef`
Working branch: `agent/vis-03-text-sniff`
Builder: Claude

## Objective

The Design BunDex's local trigger list includes "New text appeared or disappeared" and "Dialogue/subtitle areas changed". It also notes that "a dialogue box changing 3% of the screen can matter more than 80% of the pixels in an animated waterfall".

VIS-01's Sniff sees pixels change. The Text Sniff adds Windows' built-in text recognition (`Windows.Media.Ocr`, part of Windows, with no download) on the dialogue and HUD regions, so new or changed words perk Prince's ears and bring a Glance forward.

## Privacy rule (Personal Round Judgment J1)

Recognized text never leaves the capture worker. The Text Sniff emits only bounded numbers:

- the region;
- the line and character counts;
- whether the text changed since that region was last read;
- the compute time.

A per-region digest is kept in worker RAM only, to detect change. The Glance and Inspect read text from the image they are already authorized to see, so no new form of the user's screen content is created, stored, or sent.

## Scope

1. **`TextSniffSignal`** (Contracts) and protocol v6 `TextSniffProduced`. It is strictly validated, carries no text, and passes the same privacy admission as sniffs and sheets.
2. **Worker.**
   - `ITextRecognizer` has two implementations: the Windows OCR engine on Windows, and an unavailable recognizer elsewhere or when no OCR language is installed.
   - The `TextSniffer` reads only text-likely regions (dialogue band, upper corners), only after a sniff saw change there, and at most once per 500 ms per region.
   - It crops from the frame already in RAM, downscaled to the engine's maximum dimension.
   - It runs after the sniff is published, so the ≤100 ms Sniff path is untouched.
3. **Client, controller, orchestrator.**
   - A Text Sniff rides the sniff lane and is admitted like a sniff.
   - New or changed text is attention evidence (below Bnuy Mode), and it brings the next Glance forward at once.
   - Text disappearing is evidence too, but weaker.
4. **Diagnostics.** Text reads, changes, compute time (last and maximum), and recognizer availability, in worker metrics.

## Acceptance

- **Text-free signal.** Strict bounds; a malformed one tears down the worker; none after a privacy stop or for a stale grant.
- **Text Sniffer** (with a scripted recognizer):
  - it reads only text-likely regions, after change, under the per-region rate limit;
  - "changed" follows the text, not the pixels;
  - no text string is reachable from any emitted signal or metric.
- **Windows CI.** The real OCR engine is either available and reads a blank crop as no lines, within budget, or reports unavailable cleanly. The Sniff latency gate still passes.
- **Orchestrator.** New dialogue text perks his ears and glances at once, and never enters Bnuy Mode on its own.
- **The whole suite passes.**

## Personal Round Judgments

- **J1 — Text never leaves the worker** (above).

## Deferred findings

- **Real OCR accuracy on game fonts** is verified on Boss's PC. CI has no rendered game text.
- **VIS-04:** the bundled local scene model.

## Paw Gate

_(pending)_
