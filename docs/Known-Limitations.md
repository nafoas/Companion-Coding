# Known Limitations (Stage 11 report)

This is the Roadmap's Stage 11 "known-limitations report": everything that is deliberately unfinished, provisional, or unverified, consolidated from the packet records. Nothing here weakens an invariant.

## Calibration (accepted values; adjusted on evidence)

Boss decided on 2026-10-11 that the agreed values are the accepted configuration, adjusted up or down only if real use shows a need. `docs/Calibration-Protocol.md` is the optional diagnostic for that.

| Item | Status | If it needs to change |
|---|---|---|
| Resource bounds (512 MB ring and 256 MB visual working memory, raised by Boss for VIS-01; at most 3 frames; 1 vision request; worker growth watchdog) | Accepted | Run the protocol on the symptom; adjust that bound (CAL-02 D1) |
| Local cadence (VIS-01: Noticing 250 ms, Engaged 150 ms, Bnuy Mode 100 ms; the Sniff ≤100 ms on CI) | Accepted (Boss, 2026-10-11) | Tune from play |
| Scene Sniff (VIS-04: SigLIP base/16, read on Sniff triggers at most every 250 ms or every 1 s; change at novelty 0.10, shift at 0.20; 32-label neutral vocabulary; GPU via DirectML, otherwise the CPU at no more than half the cores) | Accepted (Prince's Personal Round Judgment, VIS-04 J1–J5) | GPU speed and real-game thresholds are measured on Boss's PC; CI's CPU took about 300–350 ms per read |
| Text Sniff (VIS-03: dialogue band and upper corners; read on a 0.02 cell change or after 2 s unread; at most once per 500 ms per region, one read at a time; per-region habituation) | Accepted (Prince's Personal Round Judgment, VIS-03 J2–J5) | Real game-font accuracy and the live end-to-end path are checked on Boss's PC; regions follow the default layout |
| Glance cadence (VIS-02: the Glance's own recommendation, clamped to 2–30 s; 5 s Glance timeout) | Accepted (Boss, 2026-10-11) | Glance latency is unmeasured until Boss runs `BraincaseCheck --live`; tune from those numbers |
| Semantic cadence (Noticing 12 s, Engaged 6 s, Bnuy Mode floor 3 s) and Watchbun hours | Accepted as agreed | Tune from play |
| Attention, conversation, transcript, recall, and Watchbun numeric tuning | Behavior settled; defaults in force. Afterglow (30 s to 3 min) restarts on each interesting event (ATT-01, Boss 2026-10-12) | Tune from play |

### Unverified on Boss's PC (observed in normal use)

- Real lock and sleep notifications. Forwarding is proven on CI; a real lock and suspend are unverified (WIRE-02 D4).
- Minimized and exclusive-fullscreen capture. Unsupported without target-PC evidence.

## Continuity rule (Boss, 2026-10-11)

Prince is one unbroken line of continuous Bun:

- Nothing restores, rolls back, or deletes his memories on its own.
- Repair is opt-in and only continues his current line.
- An older Vault export is never imported (closes DEF-01 D1).

## Stage 12 (built; Boss's first live run pending)

- **Built:** the Claude Braincase, protected sign-in, the dollars-per-day Bun Budget (LIVE-01, LIVE-02), and the Glance and Inspect lanes (VIS-02).
- **First live run:** `BraincaseCheck --live` checks the response contract, latency (including Glance timing), cancellation, outage handling, and usage on Boss's PC. No live call is made from the build environment.
- **No remote Resume Capsules,** by design (Design BunDex). Prince rebuilds his context from local Resume Packets.

## Deferred by design to later phases

- Final interactive controls. CORE-12 added plain placeholder buttons for the camera, quiet check, and Vault repair; exit decisions and watch tasks are reachable through the tested orchestrator commands (WIRE-02 D1; presentation phase).
- Personality, final UI, animation, and audio (Deferred Paw Pile 3, 5, 6).
- Multiple monitors (Deferred Paw Pile 1): one display is assumed; capture pauses otherwise.
- Export, migration, and distribution of Prince to another PC (Deferred Paw Pile 7).

## Smaller deferred items

All done:

- long-session consolidation (DEF-01);
- single-file Vault export (DEF-01);
- full-resolution photographs (DEF-02).

The saved keepsake edge (1280) remains provisional photograph-storage tuning.

## Pre-Stage 13 memory work (M1–M10)

- The BunDex writer is a stand-in until Stage 13 gives Prince his voice; the orchestrator does not call the keeper yet (M5).
- Boss's case-by-case page removal has no command yet; it will need its own ledger entry (M1 D1).
- Homes are remembered per kind of thing only, so they can flip between games (M1b D1); the keep pull never relaxes after two "not now"s (M1b D2); Prince cannot yet ask to reorganize (M1b D3).

## Minor

- Relaunch detection sees only windows that title-free discovery accepts (WIRE-02 D2).
- Temporary test-mode roots are left under the temp directory (WIRE-02 D3).
- The synthetic capture source cannot emit `SourceResized`.
