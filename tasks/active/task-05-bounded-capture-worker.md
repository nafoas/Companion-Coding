# Task 5 — Bounded Capture Worker

Status: **Active; authorized by Boss on 2026-08-11 after Task 4 acceptance. Task 6 is not active.**
Accepted base: `b0cbc37604519ef587b3dbce8f1c589ea561b268`
Working branch: `agent/task-05-bounded-capture-worker`
Roadmap slice: Stage 4, first packet — local peepers and bounded capture only

## Objective

Replace the Task 1–4 in-process synthetic worker in the application composition root with a dedicated, restartable out-of-process capture worker that can hold only one sealed Task 4 authorization target, use Windows Graphics Capture for that exact HWND, retain ordinary frames only in bounded RAM, dispose every native/managed resource deterministically, and report honest no-signal/resource state.

This packet proves capture isolation and resource lifetime. It does not implement Task 6 regions, crops, attention sheets, change scoring, duplicate rejection, semantic interpretation, conversation, ERPP-01, personality, photographs, or production data.

## Entry evidence

- Task 4's reviewed implementation passed Windows run `31449205060` with 240/240 tests, zero build warnings/errors, and a clean dependency audit.
- ERPP-01 was recorded as a future Task 9 companion gate without product-code changes; its exact descendant passed Windows run `31470938172` with the same 240/240 tests.
- PR #9 was squash-merged to accepted `main` as `b0cbc37604519ef587b3dbce8f1c589ea561b268`; the merged tree exactly matched the final gated branch tree.
- Task 4 supplies the sealed grant naming one HWND, PID, executable identity, target session, and privacy generation. It remains the only capture authority.
- Architecture §6.1 requires the real capture worker to be out of process before WGC is accepted, with bounded/versioned IPC and no runtime or memory authority.

## Required implementation

### Dedicated worker boundary

- Add a dedicated worker host/process and a main-process `ICaptureWorker` client. Constructing the client must not launch capture or a process; only a valid sealed authorization grant may start it.
- The worker process owns WGC session/frame-pool/D3D resources, raw frame leases, bounded queues/ring, capture timing, and capture-side metrics. It must not reference or construct `CompanionRuntime`, `MemoryStore`, `LocalWriteGate`, target discovery, presentation, API, conversation, or personality assemblies.
- IPC must be versioned, size-bounded, same-user local-only where supported, cancellable, and request-correlated. It may carry bounded control, status, metrics, and title-free frame metadata only. Raw pixels remain inside the worker process in Task 5.
- A start message names exactly the grant's HWND, PID, executable filename/fingerprint, target-session ID, and privacy generation. The worker revalidates HWND → PID → executable filename/fingerprint before opening WGC and fails closed on any mismatch or stale/reused handle.
- The worker contains no window enumeration, foreground lookup, display-wide capture, alternate HWND, automatic retarget, `PrintWindow`, `BitBlt`, Desktop Duplication, or full-screen fallback capability.
- Unexpected worker exit/fault becomes an observable fail-closed status. Restart creates a fresh worker process, discards all old disposable frames, and reuses only a still-current grant supplied through Task 4's controller; it never constructs another runtime identity.

### Bounded frame ownership

- Implement one fixed **64 MiB** byte budget across accepted raw source frames and one hard maximum of **three** accepted source frames. A frame larger than the whole budget is rejected and disposed immediately.
- Implement a bounded processing queue with an explicit small capacity and a deterministic newest/oldest drop policy recorded under Personal Round Judgment. Queue pressure must never block WGC callbacks indefinitely or allocate an unbounded backlog.
- Implement a byte-bounded ring that owns accepted ordinary frame resources in RAM only. Admission evicts/disposes enough oldest disposable work to preserve both byte and count bounds before retaining newer work.
- Every frame/native surface, frame-pool object, capture session, D3D device/context, cancellation source, timer, pipe, stream, process handle, and event subscription has one explicit owner and a deterministic disposal path for normal stop, privacy stop, restart, target exit, fault, cancellation, and process shutdown.
- Stop-and-clear must synchronously make the client reject old messages, then cause the worker to dispose the queue/ring/native capture state. The returned cleared count is evidence only; Task 4's generation revocation remains the actual privacy authority.
- No ordinary frame or pixel buffer may be written to disk, logs, diagnostics, test artifacts, crash journals, BunDex storage, or Vaults.

### Capture behavior and honest status

- Use `Windows.Graphics.Capture` targeting the exact authorized HWND. WGC is the only production capture mechanism in this packet.
- Handle content-size changes by clearing incompatible frames and safely recreating/reconfiguring the frame pool without changing the target or generation.
- Detect minimized targets explicitly where Windows exposes that state. Detect lack of frame arrival separately from unchanged visual content and report a neutral no-signal/stalled status rather than recycling stale frames.
- Closing/reusing the authorized HWND, identity mismatch, WGC unavailability, device loss, IPC corruption, or capture fault stops/clears the worker and reports failure; none may trigger a fallback or retarget.
- Minimized and exclusive-fullscreen capture remain **unsupported unless an actual target-PC feasibility spike proves otherwise**. The implementation and tests must behave honestly when they produce no signal. A cloud/CI result may not be represented as target-PC evidence.
- Provide a private-safe feasibility harness/script for a synthetic test window. It must support visible/occluded capture verification and optional minimized/exclusive experiments without capturing any unrelated application.

### Metrics and soak evidence

- Expose immutable metric snapshots covering worker PID/status, accepted/dropped/disposed frame totals, ring count/bytes, queue depth/capacity, current/max accepted source frames, current/max accounted bytes, resize/stall/fault/restart counts, oldest-frame lifetime, worker private/working-set memory where available, and native process handle count where available.
- Metrics must not contain window titles, raw executable paths, pixels, content hashes derived from private pixels, or unrelated process metadata.
- Add deterministic virtual-time/unit tests for frame lifetime, stale/no-signal transitions, bounds, cancellation, and disposal.
- Add an accelerated long-duration synthetic soak that represents multiple hours of capture events and asserts stable accounting, zero leaked owned frames after clear/dispose, bounded queue/ring maxima, and no growth proportional to total frames.
- Add a real worker-process restart soak on Windows that repeatedly starts/restarts/stops the synthetic private-safe source, proves child-process cleanup, and proves `CompanionRuntime.ConstructionCount` remains one in the application process.

### Neutral application integration

- Wire the out-of-process client into the normal development application composition root. The Task 1–4 fake remains available only for unit tests and explicitly marked test paths.
- Preserve Task 4 consent, exact-target admission, privacy-stop ordering, explicit resume, one-monitor guard, policy behavior, and neutral status presentation.
- Surface worker running/no-signal/fault status through typed neutral state only as needed to exercise Task 5. Do not add final labels, themed wording, artwork, animation, audio, or decorative UI.

## Failure and race bounds

- Start cancellation or privacy revocation before the worker confirms running must leave the worker stopped/cleared and must admit zero frames.
- A frame/status/response arriving after stop, restart, generation change, request cancellation, or client disposal is rejected without publication.
- Only one command writer and one response reader own an IPC connection. Concurrent operations are serialized or explicitly correlated; responses cannot be delivered to the wrong operation.
- Malformed, oversized, unsupported-version, duplicate, out-of-order, or target-mismatched IPC messages fail closed and tear down that worker instance.
- Failed stop/clear leaves Task 4 privacy paused. Resume must continue to require its existing successful cleanup fence.
- Process start/connect/command/stop timeouts are bounded. Forced termination is permitted only for the dedicated disposable worker after graceful bounded shutdown fails; it must never target the app/runtime process or an unresolved PID.

## Explicitly forbidden

Do not introduce:

- Task 6 orientation frames, focus regions, crops, attention sheets, local change/motion scoring, duplicate rejection, scaling remap, or manual look-here behavior;
- screen/display capture, fallback capture, browser/tab capture, foreground targeting, notification/title/document/accessibility collection, or more than one HWND;
- semantic/API/network code, credentials, model calls, attention scoring, conversation, ERPP-01 implementation, or memory proposals;
- durable screenshots/photographs, image files, captured fixtures, private content, production roots, or Vault changes;
- personality voice, Prince-specific labels, final UI, animation, audio, or Companion Awakening;
- any worker reference to runtime identity or committed-memory assemblies.

## Required tests and evidence

All automated image fixtures are generated synthetic buffers or a purpose-built private-safe test window. No captured screenshots enter the repository or CI artifacts.

1. Locked restore, clean direct/transitive dependency audit, and all Release projects build on Windows with zero warnings/errors.
2. Public/assembly surface proves the worker cannot construct runtime/memory authority and exposes no enumeration, full-screen, alternate-target, title, raw-path, or durable-image operation.
3. No worker process/start/frame exists before a genuine sealed grant; denied/stale/revoked/mismatched targets yield zero output.
4. The real client/worker IPC round trip produces metadata only for the exact authorized target/session/generation and rejects forged/mismatched/late messages.
5. Ring/accounting never exceeds 64 MiB or three accepted source frames; queue never exceeds its declared capacity under adversarial producer pressure.
6. Oversized, evicted, dropped, queued, processed, stopped, restarted, faulted, and cancelled frames/resources are each disposed exactly once.
7. Resize clears incompatible retained work and continues on the same target; minimized/no-arrival produces honest paused/no-signal status and no stale-frame replay.
8. Privacy stop clears all buffered raw work in the child and late output remains blocked by Task 4's generation gate; explicit resume starts a clean generation/process state.
9. Repeated unexpected worker crashes/restarts never construct a second runtime, never leave a child alive, and never reuse a revoked grant.
10. Accelerated multi-hour synthetic capture passes with bounded maxima and zero outstanding owned frames/bytes after stop/dispose; a shorter real-process soak shows no monotonic process/handle growth across restarts.
11. The private-safe WGC feasibility harness is buildable and reports visible/occluded/minimized/exclusive results honestly. Only actual target-PC output may establish minimized/exclusive support; absent that evidence, both remain recorded unsupported.
12. Every accepted Task 1–4 regression passes unchanged alongside Task 5 tests, including Windows/WPF process cases.

## Allowed change scope

- `src/CompanionCore.Capture.Contracts/**` for bounded worker status/metrics/protocol-facing contracts;
- `src/CompanionCore.Capture.Fake/**` only to preserve contract compatibility and test-only behavior;
- new `src/CompanionCore.Capture.Client/**` and `src/CompanionCore.Capture.Worker/**` projects;
- new private-safe capture fixture/spike projects under `tests/**` or `tools/**` with synthetic visuals only;
- `tests/CompanionCore.Capture.Tests/**`, `tests/CompanionCore.App.IntegrationTests/**`, and narrowly affected Task 4 tests;
- `src/CompanionCore.TargetAuth/**` only for worker-status/restart integration that preserves its existing authority; no policy/discovery redesign;
- `src/CompanionCore.App/**` only for worker composition and neutral Task 5 status/test wiring;
- `CompanionCore.slnx`, affected project/lock files, `.github/workflows/ci.yml`, and `scripts/**` for reproducible build/test/soak/spike execution;
- `README.md`, `BUILD_LEDGER.md`, this packet/archived Task 4 packet, and `tasks/review/HANDOFF.md`.

Do not modify the Design BunDex, roadmap, master packet, architecture proposal, accepted memory protocols/schemas, privacy-generation semantics, authorization policy semantics, prior archived/paused packets, or unrelated files.

## Paw Gate

Task 5 passes only when:

- the normal application uses a dedicated out-of-process WGC worker and cannot start it without Task 4's exact sealed authorization;
- worker IPC and assembly dependencies are bounded, versioned, target-only, and structurally incapable of runtime identity or memory mutation;
- every raw frame remains RAM-only and the queue/ring never exceed 64 MiB, three accepted source frames, or their declared capacities under stress;
- stop, privacy stop, target loss, cancellation, fault, resize, and restart dispose all disposable/native resources and reject late work;
- no-signal/minimized/exclusive limitations are reported honestly without fallback or stale replay;
- current-candidate Windows CI passes restore, audit, zero-warning Release build, all regressions, focused worker/process tests, and the accelerated soak;
- an actual-diff review finds no Task 6+, API, conversation, ERPP implementation, personality, durable-image, production-data, or capture-isolation leak;
- exact evidence, limitations, and Personal Round Judgments are recorded before Task 6 becomes active.

## Personal Round Judgment log

- **J1 — Authority is narrower than assembly friendship.** The IPC records and bounded serializer are inert public transport contracts, while `CaptureAuthorizationGrant.Issue` remains internal and neither the client nor worker assembly is a friend of that issuer. The worker therefore consumes serialized target facts but cannot mint the sealed Task 4 capability it requires the client to hold.
- **J2 — One disposable child and one strict pipe epoch.** The client constructor launches nothing. A start backed by a sealed grant creates one randomly named same-user Windows pipe, one 256-bit handshake nonce, one dedicated child, protocol version 1, a 64 KiB message ceiling, correlated commands, and strictly increasing control sequences. Malformed, duplicate, out-of-order, or current-target-mismatched traffic tears down that child.
- **J3 — Newest useful work wins inside hard bounds.** The nonblocking processing queue has capacity two and drops its oldest pending frame when pressured. Admission first evicts the oldest retained/pending disposable work, then accepts newer work only if total accounted source ownership remains at or below 64 MiB and the configured count ceiling. Tests exercise a ceiling of three; production WGC retains at most two accepted leases so one of its three frame-pool buffers remains available for continued capture.
- **J4 — Accounting follows raw-source equivalence without exporting pixels.** Each accepted BGRA source lease accounts `width × height × 4` bytes; invalid or over-budget dimensions fail closed. Only target/session/generation, sequence, time, dimensions, and accounted bytes cross IPC. Raw WGC surfaces remain child-owned RAM resources and are never logged, serialized, written to disk, or placed in test artifacts.
- **J5 — Revocation precedes cleanup evidence.** Stop/restart first closes the client's grant and event-admission gates synchronously. The child then stops WGC, clears queue/ring/native leases, and acknowledges counts/bytes; a timeout or corrupt response leads only to bounded termination of the exact owned child. Epoch, grant-reference, target, generation, and sequence checks reject queued or late metadata again at dispatch time.
- **J6 — Silence is a state, never recycled vision.** A two-second virtual-time no-arrival threshold yields `NoSignal`; `IsIconic` yields distinct `PausedMinimized`. Both transitions synchronously clear retained leases, and a genuinely new frame is required to return to `Running`. Resize clears incompatible work before frame-pool recreation. Minimized and exclusive-fullscreen support remain unsupported until the optional harness modes are run on the actual target PC.
- **J7 — Observers cannot obstruct capture control.** Worker notifications use a bounded newest-preserving channel separate from direct correlated command responses. The client has one sole IPC reader plus a separate bounded event dispatcher; slow or failing application observers cannot block metrics, stop, cleanup, or protocol fault handling.
- **J8 — Evidence is synthetic and private-safe by construction.** Unit tests use generated buffers and virtual time; the accelerated soak offers 216,000 frames (six simulated hours at 10 fps) and requires bounded maxima plus zero outstanding leases. Windows process tests use only the explicit synthetic worker flag and fresh child processes. The manual WGC harness spawns and authorizes its own pulsing fixture, verifies visible/occluded metadata, optionally performs real minimized/DXGI-exclusive experiments, and never saves or exposes captured pixels.

## Deferred Findings

- Task 6 owns orientation frames, regions, synchronized source extraction, crops, attention sheets, scaling remap, local change scoring, and duplicate rejection.
- Final resource thresholds and the 8–12-hour physical soak remain Task 12; Task 5 supplies provisional hard bounds and accelerated/worker-process soak evidence.
- Multi-monitor accompaniment, dedicated game integrations, browser tabs, audio, and durable photographs remain deferred under the Roadmap.
- ERPP-01 Session Transcript Continuity remains affiliated with Task 9 and has no implementation surface in Task 5.
