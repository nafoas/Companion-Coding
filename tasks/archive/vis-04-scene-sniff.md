# VIS-04 — The Scene Sniff: a local vision model built into the app (Vision upgrade, part 4)

Status: **accepted** — Paw Gate PASS; PR #72 squash-merged as `3567519c4d4cc827a7e7f25f0d0fa29c6e2e12f2`.
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
- **J3 — Library telemetry.**
  - ONNX Runtime's Windows telemetry is on by default. The worker turns it off before loading the model.
  - DirectML has no switch of its own. Its diagnostics follow the Windows diagnostic-data setting, like Direct3D and the GPU driver.
  - Neither ever receives anything but the local model's GPU work. This is flagged to Boss.
- **J4 — The CPU never takes every core.** The CPU fallback uses at most half the cores, and at most 4 threads, so a running game keeps its own. The DirectML/CPU choice is a measured warm-up race at load.
- **J5 — A whole-frame squash, as SigLIP was trained.** The frame is area-sampled to 224×224 with 4×4 taps per pixel, so the HUD and the screen's edges are seen too.

## Deferred findings

- **GPU speed is measured on Boss's PC.** CI runners have no GPU, so the CPU won the warm-up race there.
  - CI's CPU numbers (2 threads on a shared VM): 3.3 s to load, 388 ms median read.
  - The read runs off the frame path, one at a time, so CPU speed limits only how often scenes are compared, never the Sniff.
  - `BraincaseCheck` does not time the scene model. The worker metrics report `SceneModelAccelerated` and the read times.
- **The vocabulary is generic.** Per-game labels, and learning a scene's look over time, wait for later work.
- **The thresholds are measured on photographs.** Change 0.10 and shift 0.20 are accepted defaults, to be tuned from play.

## Paw Gate

### Mutation pass

44 hand-written mutants, run against the focused test projects. 43 are caught.

- **First run:** 38 failed a test, and 1 hung its test until the 300 s timeout ("deferred until loaded" made the load block), which also counts as caught.
- **Three survivors were real gaps,** now closed:
  - a failing model read is returned as nothing (a direct `Read` assertion);
  - only Sniff triggers bring a read forward: a changed, non-trigger frame waits for the refresh;
  - a visual reset forgets the reference scene.
- **One survivor is equivalent ("missing model").** Without the existence check, the load still fails and reports unavailable; the check is only a fast path.

### Gate

- **Local gate (Linux):** locked restore; `/warnaserror` build, 0 warnings; no vulnerable packages; every suite passed (Capture.Worker 131, Orchestration 115, TargetAuth 99, Api 187, …).
- **Windows CI.** Head `353441a4396a51159cd4dd9cf3e9014168b077ea` passed push run `38174260185` and PR run `38174274555` at 1146/1146, with verified artifact digests and the expected attention-sheet PNG (`5eb11c96…`).
  - The real SigLIP model was fetched (digest-verified) and loaded on both runs, and ran on the CPU (the runners have no GPU).
  - It matched the Python reference embedding at cosine 1.00000.
  - Load took 3.1 s and 3.7 s; median reads took 305 ms and 349 ms.
  - The Sniff latency gate and the OCR test still pass.
- **Merge ref.** `refs/pull/72/merge` had exact parents `4959bf0` (main) and `353441a` (head), and tree `96ff5256170e089fe07bf95ffdbc9a9e3447b491`, equal to a local merge.
- **Merge.** Marked ready and squash-merged with the expected-head fence as `3567519c4d4cc827a7e7f25f0d0fa29c6e2e12f2`, which carries the same tree.
- **Result: PASS.** The Vision upgrade (VIS-01 to VIS-04) is complete.
