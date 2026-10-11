# VIS-04 — The Scene Sniff: a local vision model built into the app (Vision upgrade, part 4)

Status: **in progress**
Authorized: 2026-10-11 by Boss ("Built in+ local Vision AI that is just part of the app, not a separate download"; "see, notice, and respond, without an immersion killing delay"; "I have a strong PC, so allocate as much RAM is needed").
Accepted remote base: `4959bf023e1673b99c36fed8a5d4b2fbab21e356`
Working branch: `agent/vis-04-scene-sniff`
Builder: Claude

## Objective

The Sniff (VIS-01) sees pixels move. The Text Sniff (VIS-03) sees words change. The Scene Sniff adds a local image model that understands what kind of scene is on screen and notices when it becomes a different one: a menu opens, a fight starts, he walks from a forest into a town. It runs in the capture worker, on the GPU through DirectML where it is faster, otherwise on the CPU. Nothing is sent anywhere, and the model ships with the app.

## Model (Personal Round Judgment J1)

The agreed plan said "CLIP". OpenAI's CLIP model card says "any deployed use case of the model — whether commercial or not — is currently out of scope", so it should not ship inside Prince. The Scene Sniff uses **SigLIP base, patch 16, 224 px**: Google's CLIP-family model under **Apache-2.0**, which is stronger at zero-shot recognition.

- **Weights:** the ONNX export `Xenova/siglip-base-patch16-224` at revision `4649052661e53c7000355844105f8a1792088239`.
  - Vision model `onnx/vision_model.onnx`, fp32, 371,819,850 bytes, SHA-256 `f89d41bac7f4d4b87e010a467d93f98689d708916ed22f5a07f96fdfa26f475f`.
  - The logit scale and bias come from `google/siglip-base-patch16-224` at `7fd15f0689c79d79e38b1c2e2e2370a7bf2761ed`.
- **Not committed.** The build fetches the vision model once into a git-ignored cache, verifies the digest, and copies it next to the worker. `-p:CompanionSkipSceneModel=true` builds without it; the Scene Sniff then reports unavailable.
- **Label embeddings are precomputed.** `tools/scene-labels/make_scene_labels.py` produces a 32-entry neutral vocabulary from the pinned text model. The app ships only the vision model.
- **Third-party notices** record SigLIP (Apache-2.0) and ONNX Runtime (MIT).

## Privacy (J2)

- The image embedding never leaves the worker. It is kept only as a RAM reference for change detection and zeroed on reset.
- A Scene Sniff carries only bounded numbers: novelty, whether it is a full shift, the index and confidence of the closest label in the fixed neutral vocabulary, the compute time, and whether the GPU ran it.
- It passes the same privacy admission as sniffs and sheets.

## Scope

1. **`SceneSniffSignal`** (Contracts), the fixed `SceneVocabulary` keys, protocol v7 `SceneSniffProduced`, and worker metrics: model available, accelerated, reads, shifts, and compute time (last and maximum).
2. **Worker.**
   - `ISceneEmbedder`, with ONNX Runtime SigLIP on DirectML or CPU, and an unavailable variant.
   - At startup the model loads in the background, and the faster engine wins a short warm-up race.
   - **Preprocessing:** an area-sampled 224×224 RGB tensor of the whole frame (SigLIP's own squash resize), built from the frame already in RAM.
   - **`SceneSniffer`:** one read at a time, off the frame path. A read is due on a Sniff trigger (at most every 250 ms), or after 1 s without one.
   - **Novelty** is 1 − cosine against the scene last reported.
     - At novelty ≥ 0.10 the Sniffer reports a change, and at ≥ 0.20 a full shift.
     - From measurement: the same scene stays at ≥ 0.97 cosine; a dialogue box appearing scores about 0.88; a different scene scores 0.47–0.62.
3. **Client, controller, orchestrator.**
   - Scene Sniffs ride the sniff lane, are strictly validated and sequenced, and are admitted like sniffs.
   - **`SceneReflex`:**
     - a change is attention evidence that scales with novelty, carries a `scene:<key>` topic (so a familiar scene habituates), and stays below Bnuy Mode;
     - a full shift brings the next Glance forward;
     - it never writes memory.
4. **Build and CI.** ONNX Runtime with DirectML (pinned, locked). A model fetch with digest verification. The CI model cache. The App copies the worker's runtime and model.

## Acceptance

- **Signal and protocol.** Strict bounds; malformed signals tear the worker down; nothing is emitted after a privacy stop or for a stale grant; no embedding is reachable from any signal or metric.
- **Scene Sniffer** (with a scripted embedder): due rules, one read in flight, novelty against the last reported scene, the change and shift thresholds, reset, failure tolerance, and label scoring.
- **Preprocessing** matches the reference implementation in the generator tool (one synthetic pattern, within tolerance).
- **Windows CI (real model).** The real SigLIP model loads and runs, on DirectML or CPU. A synthetic pattern's embedding matches the Python reference (cosine ≥ 0.99), within a stated compute budget. The Sniff latency gate still passes.
- **Orchestrator.** A scene shift perks his ears and glances at once. Scene evidence alone never enters Bnuy Mode. A familiar scene habituates.
- **The whole suite passes.**

## Personal Round Judgments

- **J1 — SigLIP instead of CLIP**, for a clear shipping license (above).
- **J2 — Embeddings never leave the worker** (above).

## Deferred findings

_(none yet)_

## Paw Gate

_(pending)_
