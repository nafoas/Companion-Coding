# Stage 11 Calibration Protocol (Boss's PC)

Question 9 in the design conversation set the rule: **provisional leak protection from day one, final limits from real measurements.** This protocol produces those measurements. Everything recorded is numbers and state names only: memory, handles, CPU, frame counts, attention and Watchbun state, and Braincase usage counters. No screenshots, titles, paths, game text, or memories are ever written.

## Boss's decision (2026-10-11): the agreed values are the configuration

Boss decided that the values agreed in the design conversation are the accepted running configuration, not a placeholder awaiting measurement. "If it needs to go up or down, we change it."

So this protocol is **optional**. It is a diagnostic tool for when real use suggests a value is too tight or too loose, such as a stutter, a worker restart, or Prince feeling too slow or too eager. It is no longer a gate. Any adjustment is a small, reviewed change to the value concerned. No invariant changes, and no committed memory is ever affected.

## Agreed values in force (CAL-01)

| Item | Value |
|---|---|
| Screenshot ring | 64 MB fixed |
| Full-resolution source frames | at most 3 |
| Active vision requests | 1 |
| Local capture spacing | 1 s (Noticing 1–2 s, Investigating 0.5–1 s) |
| Semantic look | Noticing 12 s or triggered; Engaged 6 s; Bnuy Mode floor 3 s |
| BIC clock | 30 s, offset from game observations |
| Watchbun bedtime | check after 1 quiet hour; sleepy consolidation after another hour |

## 1. Start Prince with calibration recording

From the build output folder:

```
CompanionCore.App.exe --calibration-log
```

Samples are written every 10 seconds to `%LOCALAPPDATA%\CompanionCore.Dev\Calibration\`. The log is bounded at 8 files × 8 MiB, so it can never fill the disk.

## 2. Measure each state separately

This is the design's "Baseline calibration" list. Spend roughly 15–30 minutes in each state unless noted.

1. **Prince resting**: the App open, no target authorized.
2. **Noticing**: a calm game scene (menus, walking, an idle area).
3. **Engaged**: ordinary play with things happening.
4. **Sustained Bnuy Mode**: an intense stretch (a boss fight or a hard section).
5. **Watchbun Mode**: tab away from the game and leave it running; let the quiet check appear.
6. **Repeated attention-sheet creation**: resize the game window a few times, and toggle fullscreen and windowed.
7. **API failures**: unplug or disable networking for a few minutes. Until Stage 12 the Braincase is offline anyway; this rehearses the path.
8. **Sleep and wake**: lock the PC, then put it to sleep and wake it.
9. **Several game sessions**: close and reopen the game a few times; try Bnuy Repairs once.
10. **The 8–12-hour soak**: a long ordinary session (it can include stepping away).

Run at 1080p and at any other resolution you use.

## 3. Produce the report

```
CompanionCore.CalibrationReport.exe "%LOCALAPPDATA%\CompanionCore.Dev\Calibration" --physical-memory-gb 32
```

This writes `calibration-report.md` beside the samples, containing:

- **per-state baselines and peaks:** median, p95, and maximum of app and worker private memory, CPU, and handles;
- **growth after warm-up:** a leak is use that keeps rising after work settles, not merely high use. The verdict is *Stable*, *SustainedGrowth*, or *InsufficientData*;
- **recommended thresholds:**
  - soft concern: 1.5× the sustained peak;
  - worker restart: 2×;
  - emergency Naptime: 3×;
  - each capped at a quarter of physical memory, comfortably above legitimate peaks and far below anything that threatens the 32 GB system.

## 4. When an adjustment is wanted, send the report back

Paste `calibration-report.md` into the chat. The Builder then:

- adjusts only the values the report (or the observed problem) shows need to change;
- investigates any *SustainedGrowth* verdict first, because growth is a leak to fix, not a limit to raise.

No resource response may ever delete, rewrite, or summarize away committed BunDex memories.
