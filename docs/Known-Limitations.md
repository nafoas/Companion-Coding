# Known Limitations (Stage 11 report)

This is the Roadmap's Stage 11 "known-limitations report": everything that is deliberately unfinished, provisional, or unverified, consolidated from the packet records. Nothing here weakens an invariant.

## Calibration (accepted values; adjusted on evidence)

Boss decided on 2026-10-11 that the agreed values are the accepted configuration, adjusted up or down only if real use shows a need. `docs/Calibration-Protocol.md` is the optional diagnostic for that.

| Item | Status | If it needs to change |
|---|---|---|
| Resource bounds (64 MB ring, at most 3 frames, 1 vision request, worker growth watchdog) | Accepted as agreed | Run the protocol on the symptom; adjust that bound (CAL-02 D1) |
| Local cadence (1 s, inside Noticing 1–2 s and Investigating 0.5–1 s) | Accepted as agreed | A state-aware split if CPU or responsiveness calls for it (CAL-01 D1) |
| Semantic cadence (Noticing 12 s, Engaged 6 s, Bnuy Mode floor 3 s) and Watchbun hours | Accepted as agreed | Tune from play |
| Attention, conversation, transcript, recall, and Watchbun numeric tuning | Behavior settled; defaults in force | Tune from play |

### Unverified on Boss's PC (observed in normal use)

- Real lock and sleep notifications. Forwarding is proven on CI; a real lock and suspend are unverified (WIRE-02 D4).
- Minimized and exclusive-fullscreen capture. Unsupported without target-PC evidence.

## Needs Stage 12 (stop condition: credentials and live API)

- The real remote semantic provider, protected key storage, and first live calls.
- Live response-contract, latency, cancellation, outage, and usage validation.
- The Bun Budget amount (agreed as a locally configured limit shown as sleepiness; no number chosen yet).
- Remote Resume Capsules and their 10–15-minute stability window.

## Deferred by design to later phases

- Interactive controls for camera, quiet-check answers, exit decisions, watch tasks, and Bnuy Repairs. The orchestrator commands exist and are tested (WIRE-02 D1; presentation phase).
- Personality, final UI, animation, and audio (Deferred Paw Pile 3, 5, 6).
- Multiple monitors (Deferred Paw Pile 1): one display is assumed; capture pauses otherwise.
- Export, migration, and distribution of Prince to another PC (Deferred Paw Pile 7).
- Importing a Vault export older than the live journal. That would accept losing later memories and needs Boss's decision (DEF-01 D1). The single-file export itself exists.

## Smaller deferred items

All done:

- long-session consolidation (DEF-01);
- single-file Vault export (DEF-01);
- full-resolution photographs (DEF-02).

The saved keepsake edge (1280) remains provisional photograph-storage tuning.

## Minor

- Relaunch detection sees only windows that title-free discovery accepts (WIRE-02 D2).
- Temporary test-mode roots are left under the temp directory (WIRE-02 D3).
- The synthetic capture source cannot emit `SourceResized`.
