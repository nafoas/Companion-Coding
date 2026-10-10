# Task 7 — Stateless Braincase Bridge

Status: **active — implementation**
Authorized: 2026-10-10 by Boss's standing direction to continue autonomously from Task 7 / Stage 5 under the Paw Gate model, stopping only before real credentials, paid/live API use, or a serious issue
Accepted remote base: `3e99685091b4b398719a2f8fae478a836c900221`
Accepted base tree: `e117c193a81f702a6d1a7125d34c862267f3c4e1`
Working branch: `agent/task-07-stateless-braincase-bridge`
Builder: Claude

## Objective

Complete the semantic-provider contracts for Roadmap Stage 5. The faraway Braincase stays stateless, and every identity, continuity, and memory decision stays local. One authorized attention sheet goes through a mock or replay provider and returns one structured interpretation. Retries and timeouts cannot duplicate output or memory. Remote proposals reach durability only through the accepted append-only `LocalWriteGate`. Outages and limits produce one packaged naptime notice and a local processing pause. A restarted bridge with a fresh remote conversation recovers continuity solely from local state.

No live or paid call is possible in this task. The real provider is a contract-complete disabled shell. Real credentials, live calls, and provider-specific validation remain Task 12 stop conditions.

## Entry evidence and authority resolution

- Stage 4 is accepted and recertified. `main` `3e99685` passed post-merge run `38028945996` at 325/325, and no task is active.
- Neutral-core packet Task 7 and Roadmap Stage 5 define the deliverables and Paw Gate scenarios below.
- Accepted architecture §7 names `ISemanticProvider`, `MockSemanticProvider`, `ReplaySemanticProvider`, and a disabled real-provider shell. It assigns them to a `CompanionCore.Api` project with `CompanionCore.Api.Tests`.
- Architecture trust boundaries 3 and 4 hold that `ApiBridge` never references `MemoryStore` or maintenance surfaces. It produces proposals only, and `LocalWriteGate` decides.
- `LocalWriteGate.SubmitAsync(proposal, expectedPrivacyGeneration, …)` and `RuntimePrivacyState.TryAcquireAdmissionLease(expectedGeneration, …)` were built in Tasks 2 and 4 specifically for this task. A pre-stop remote result therefore cannot become admissible after explicit resume.
- A capture grant's generation is the runtime privacy generation at authorization (`TargetAuthorizationService`). An attention sheet released by `TargetSessionController` has already passed target, generation, and privacy admission.

## Required implementation

### Project boundary

- Add `src/CompanionCore.Api` (namespace `CompanionCore.Api`) and `tests/CompanionCore.Api.Tests` to the solution, each with a committed lock file. No new NuGet package is introduced; JSON uses in-box `System.Text.Json`.
- `CompanionCore.Api` may reference only Memory (proposal types, `LocalWriteGate`, read-only retrieval), Privacy, Capture.Contracts (attention sheets and grants), and Runtime (the `IDiagnosticsSink` seam).
- It references no networking assembly. A structural test proves the assembly references neither `System.Net.Http` nor `System.Net.Sockets`.
- It has no reference to `MemoryStore`, the journal, backup, repair, or any maintenance capability. It never constructs a `MemoryRepository`.
- Test-only `InternalsVisibleTo` entries for `CompanionCore.Api.Tests` may be added to:
  - Capture.Contracts, to issue synthetic grants and sheets exactly as other test projects do;
  - Memory, to create a backup for the credential-leak scan;
  - Privacy, to exercise privacy stop and resume in fencing tests.

  No product assembly gains friend access.

### Protected credential interface, unconfigured by default

- `ICredentialStore` exposes state (`Unconfigured` or `Configured`), set (taking ownership), remove, a scoped borrow for the future real adapter, and a leak check: does any configured secret appear in a given text?
- `ProtectedCredential` holds the secret masked in a pinned buffer. It zeroes the buffer on dispose, has no value-bearing public property, redacts `ToString`, and serializes to nothing secret.
- `InMemoryCredentialStore` is RAM-only and process-lifetime, and it is never persisted. A new instance is the unconfigured development state. A persistent OS-protected store is a Task 12 concern.

### Structured, versioned request and response schemas

- Each request carries:
  - schema version and local operation ID;
  - operation kind (`InterpretAttentionSheet`);
  - attempt number;
  - the local Resume Packet;
  - the sheet description: kind, region kinds, geometry, media type, byte length, digest;
  - RAM-only access to the encoded image.
- A request never carries a remote conversation, session, or capsule identifier.
- Responses are strict JSON (schema v1):
  - an echoed operation ID;
  - a bounded interpretation: a neutral summary plus at most 16 observations, each with region kind, label, and confidence;
  - at most 8 memory proposals;
  - optional usage units.
  - Unknown members, wrong types, and out-of-range values make the whole response invalid. Enums are names only.
- Every provider returns either success (response JSON) or a typed failure: transient, outage, rate limited (with an optional retry-after), or unavailable (disabled, credentials missing, or live calls disabled). The mock, replay, and real-shell providers all feed the same parser and validator.

### Local operation IDs, bounded retry, cancellation, idempotency

- Each bridge operation receives one unique local operation ID. Every attempt and the resulting memory append reuse it.
- Attempts are bounded (default 3). Each is bounded by a per-attempt timeout and abandoned without trusting provider cooperation. Exponential backoff between attempts is bounded.
- Caller cancellation, bridge disposal, and `CancelPendingWork` cancel the operation without retry, write, or output.
- A late reply from an abandoned attempt is never observed.
- The response for an operation is processed at most once. The interpretation is published at most once.
- Record IDs are derived deterministically from the operation ID and proposal index, and the append uses the same operation ID. Any replay of the append is therefore `AlreadyCommitted`, never a duplicate or a conflict.
- At most one request is in flight. A concurrent request returns `Busy` immediately and its sheet is disposed (bounded queue of zero).

### Local allowlisted append proposals

- The only remote operation allowed is `memory.append.v1`. Update, delete, overwrite, replace-checkpoint, metadata mutation, or any unknown operation rejects all memory proposals from that response; nothing from it reaches the write gate.
- Remote source kinds are limited to `observed`, `read`, `inferred`, and `guess`. `told`, `remembered`, `user_correction`, and `integration` carry local authority and are rejected.
- Remote links may target only records supplied in that request's Resume Packet. Correction, supersession, and recurrence links must keep the exact subject.
- Record ID, timestamp, operation ID, session reference, and provenance metadata are always assigned locally. The schema has no field through which remote output could set them.
- Accepted proposals are submitted as one `AppendMemoryProposal` through `LocalWriteGate` under the request's expected privacy generation. A bridge-level admission lease is held across commit and output publication, so a privacy stop either waits for already-admitted work or fences it entirely.
- If any configured credential appears in remote-origin text, the response is rejected as invalid before it reaches memory, output, logs, or the journal.

### Local Resume Packet, rebuilt for every request

- Built only from local state. Inputs:
  - caller focus subjects;
  - the bridge journal's most-recent committed subjects;
  - `LocalWriteGate`-committed memories, through a read-only retrieval interface.
- Bounded and deterministic: at most 8 subjects, 6 records per subject, 32 records, and 24 KiB of recollection text. Current understanding comes first. Each item carries record ID, subject, scope, source kind, confidence, currency, and timestamp, and the packet carries the highest included journal sequence as its local checkpoint basis.
- Nothing from a prior remote response enters a later request except through committed local memory. No persistent remote Resume Capsule exists.

### Outage, limit, and packaged naptime

- Entering a nap (outage, rate limit, retries exhausted, or exceeding the optional local usage budget) works in order:
  1. a nap checkpoint is flushed to the local bridge journal;
  2. the operation's buffers are released and the sheet is disposed;
  3. exactly one `BraincaseNotice` (`Naptime`) is raised per nap episode;
  4. processing pauses.
- While napping, requests are refused locally without contacting the provider and without further notices.
- Outage naps allow one bounded probe after an exponentially growing interval (1 minute doubling to 30 minutes). Rate-limit naps last until the clamped retry-after.
- A successful probe raises one `Awake` notice.
- Nap state survives bridge restart, so a restart cannot resume hammering a limited service.
- `Unavailable` providers produce a neutral `Unavailable` outcome without retry or nap. The notice is raised once per unavailability reason, not per request.
- Notices are typed (`BraincaseNoticeKind`, nap reason, unavailable reason) and carry no wording. Their presentation mapping arrives with app composition (J2).

### Local bridge journal, diagnostics, and usage estimation

- `BraincaseStateLocation` derives a sibling `Braincase` directory only from a validated development or test `MemoryStoreLocation`. It can never resolve the production root.
- The journal is append-only JSON lines, each with a checksum, and each append is flushed to disk. Records:
  - started, completed, interrupted, nap, awake, and snapshot entries;
  - operation ID, kind, attempts, outcome, proposal and commit counts;
  - committed subjects and usage estimates.
  - It never stores credentials, images, interpretation text, recollection text, or provider error text.
- Recovery tolerates and truncates one torn trailing line. A corrupt interior is preserved aside, and the bridge starts with a fresh journal. Operations that were in flight at a crash are marked `interrupted` and are never re-sent: their images were RAM-only, and their results were never admitted.
- Compaction is bounded: past 512 KiB, a snapshot is written atomically (temp file, flush, replace).
- Hidden diagnostics are a snapshot returned only on explicit request: counters, nap state, and provisional usage estimates. Log lines through `IDiagnosticsSink` contain fixed categories, enums, and counts only, never exception messages or remote text.

### Graceful configuration and the disabled real provider

- `BraincaseConfiguration` parses the provider kind (`Real`, `Mock`, `Replay`, `Disabled`) and an optional replay fixture directory, and never throws. The default is `Real`; unknown values fall back to `Disabled` with a diagnostic.
- `SemanticProviderFactory` builds the selected provider. A missing or invalid replay directory degrades to a disabled provider, not a crash.
- `RealSemanticProviderShell` implements the full contract but has no transport. Without a credential it reports `Unavailable(CredentialsMissing)`; with one it reports `Unavailable(LiveCallsDisabled)`.
- `ReplaySemanticProvider` loads sanitized JSON fixtures, bounded in file size and count, keyed by request shape (operation kind, sheet kind, region kinds, packet subjects), and replays scripted reply sequences through the same contract.
- `MockSemanticProvider` runs deterministic scripted steps and records the request text for assertions.

## Explicitly forbidden

- Any network client, HTTP call, socket, real model prompt, real credential, or paid/live API usage.
- Any memory update, delete, overwrite, compaction, or `MaintenanceStore` reachability from the API side.
- Persistent remote conversation or Resume Capsule state, and model auto-switching.
- App/WPF composition changes. The app opens no memory repository yet, so wiring the bridge into it waits until a consumer exists.
- Attention scoring (Task 8), conversation threads (Task 10), ERPP, personality voice, final UI, animation, or audio.
- Durable images, logging of image bytes, or production data-root access.

## Acceptance scenarios

1. One authorized attention sheet through the mock provider yields exactly one structured interpretation, one committed append, and one published output. The sheet is disposed afterwards.
2. The same through the replay provider from a committed sanitized fixture.
3. An attempt that times out and is then retried yields one output and one commit. A late reply from the timed-out attempt is never observed.
4. Transient failures retry within the bound, with backoff, then succeed exactly once. Replaying the append for the same operation is `AlreadyCommitted`.
5. A sheet whose grant does not match, or whose generation is stale or privacy-paused, is refused before any provider call. A privacy stop during an in-flight request fences the late result from memory and output.
6. Remote update, delete, unknown-operation, forbidden-source-kind, foreign-link, and cross-subject-correction proposals are rejected locally with nothing committed. Malformed, unknown-member, wrong-echo, and oversized responses are invalid.
7. An outage writes a nap checkpoint, disposes the buffers, raises one naptime notice, and stops repeated requests. A further request makes no provider call and raises no notice. The probe schedule is bounded. A rate limit honors the clamped retry-after. Retries exhausted becomes a nap. Committed memory is unchanged.
8. Restarting the bridge with a fresh provider instance recovers continuity solely from local state:
   - the committed recollection appears in the new packet;
   - the earlier remote-only summary never does;
   - nap state persists;
   - an interrupted operation is marked and not re-sent.
9. A synthetic credential is set and removed. The marker never appears in logs, diagnostics, the bridge journal, the memory store, journal, or backup archive, or in exception-derived text. A response echoing it is rejected.
10. Selecting the real provider without credentials gives a neutral unavailable state and no crash. With a synthetic credential, live calls are still impossible. The API assembly references no networking assembly.
11. Unknown or invalid configuration and replay-fixture failures degrade gracefully.
12. Journal recovery: a torn trailing line is truncated, interior corruption is preserved aside, compaction is atomic and bounded, and recovered nap and subject state are exact.
13. All 325 accepted tests still pass on Windows CI; the new suite adds to that total.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Api/**`
- `tests/CompanionCore.Api.Tests/**`
- `src/CompanionCore.Capture.Contracts/AssemblyInfo.cs`, `src/CompanionCore.Memory/AssemblyInfo.cs`, and `src/CompanionCore.Privacy/AssemblyInfo.cs` (one test-only friend entry each)
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

Do not modify accepted capture, target-authorization, memory behavior, privacy behavior, runtime, presentation, App, CI, or documentation sources.

## Paw Gate

Pending. The gate requires:

- every acceptance scenario passing;
- a strict Release build with 0 warnings;
- the full local suite and both Windows CI event paths green, with verified artifacts;
- actual-diff review for scope, invariants, privacy fencing, authority, cancellation, resource lifetime, and credential hygiene;
- an exact merge-ref check;
- Personal Round Judgments recorded.

## Personal Round Judgments

Each judgment stays inside the packet, accepted architecture, and privacy invariants, and is reversible by a later bounded packet.

### J1 — Project name follows the accepted architecture

Architecture §7 and its directory plan name `CompanionCore.Api` and `CompanionCore.Api.Tests`. Bridge types use the neutral "Braincase" name already present in the roadmap and design. Reverse by renaming the projects; no contract depends on the name.

### J2 — Notice presentation mapping waits for app composition

Mapping notices in `NeutralPersonalityAdapter` would make Presentation reference Api. That would pull Memory and the native SQLite bundle into the WPF app's dependency and lock graph, even though the app opens no repository and has no bridge consumer yet. Notices stay typed and wording-free, which is the core boundary. The mapping lands with the first app composition of the bridge. Reverse by adding the reference and mapping in that packet.

### J3 — Zero-length pending queue

At most one request is in flight. A concurrent request returns `Busy` at once and its sheet is disposed, so there is no waiting slot to bound. Request cadence belongs to the Task 8 attention engine, which can decide what to resend. Reverse by adding a bounded latest-wins slot.

### J4 — A forbidden proposal rejects the whole batch

If any proposal in a response is not an allowlisted append, none of that response's proposals reach the write gate. This matches the atomic one-operation append, and the safer choice wins when remote output is suspect. The neutral interpretation is still delivered once. Reverse by filtering per proposal.

### J5 — Remote source-kind allowlist

Remote proposals may claim only `observed`, `read`, `inferred`, or `guess`. `told`, `remembered`, `user_correction`, and `integration` assert local or user authority and stay local-only. Reverse by editing one set.

### J6 — Invalid responses are not retried

A malformed, out-of-bounds, wrong-echo, or credential-echo response fails closed without another attempt. A deterministic contract violation is not transient, and repeating it would spend paid calls after Task 12. Reverse by classifying a subset as transient.

### J7 — Interrupted operations are never re-sent or admitted after restart

Their images were RAM-only, and a crash erases any proof that the pre-crash privacy generation was still current. They are marked `interrupted`. Only memory already committed before the crash survives, and it survives through the accepted store.

### J8 — RAM-only credential store

`InMemoryCredentialStore` is the unconfigured development state and the synthetic test store. Persistent OS-protected storage needs real credential handling, a Task 12 stop condition.

### J9 — Provisional bounds and usage estimate

These are configuration values, provisional until Stage 11 profiling and the final API gate:

- 3 attempts, 30-second attempt timeout, backoff from 0.5 s capped at 8 s;
- single-attempt outage probes, doubling from 1 to 30 minutes;
- rate-limit clamp of 30 s to 6 h, default 5 minutes;
- usage units estimated as request JSON bytes ÷ 4 plus image KiB.

### J10 — Bridge journal placement and failure mode

The journal lives in a sibling `Braincase` directory derived only from a validated development or test `MemoryStoreLocation`, so it cannot reach production. One exclusive writer handle fences a second bridge. `FileShare.None` is used because Unix shared locks would not fence. If the journal cannot be written, the bridge fails closed and sends nothing further.

### J11 — A corrupt journal starts fresh

Interior corruption is preserved aside for diagnosis, and the bridge starts awake with empty journal state. Bounded attempts and nap rules still apply from the first request. Committed memory is untouched, because it lives in the accepted store.

### J12 — No notice replay after restart

A nap recovered from the journal is enforced silently: the sleepy notice was already given once. `NapStatus` exposes it for presentation.

### J13 — Default provider is the real shell

The one primary provider is selected by default. Without a key it is neutrally unavailable, so the eventual production configuration is the one exercised in development. Unknown values select `Disabled`.

### J14 — Review findings fixed before the gate

The Builder's own adversarial review found two defects; each was fixed with a regression test:

- A JSON-escaped secret could slip past the raw-text echo check. Every decoded field is now checked as well.
- A probe that found the provider unavailable left the nap expired, so every later request would have probed. A failed probe of any kind now extends the nap silently.

A mutation pass then disabled each key guard in turn and confirmed the suite fails. The two survivors are deliberate defense-in-depth layers:

- the raw-text echo check, backed by the decoded-text check;
- the bridge-level privacy lease, backed by the write gate's generation lease and the pre-publication currency check.

One gap the pass exposed (budget-nap buffer release before the notice) gained an assertion.

## Deferred findings

1. App composition when the first consumer exists (Tasks 8–10):
   - open the development repository;
   - wire the bridge;
   - route privacy stop to `ApiBridge.CancelPendingWork`;
   - map notices in the presentation adapter.
2. Bridge diagnostics and usage estimates into Stage 11 "Show Da Technical Thinks" diagnostics, alongside the Stage 4 orientation-failsafe counters.
3. Persistent OS-protected credential storage and the live provider adapter (Task 12, stop condition).
4. Conversation and text-formulation request kinds when the conversation thread exists (Task 10).
5. **Legacy App-integration shutdown timeout:** `AppProcessTests.Shutdown_StopThenClose_ExitsCleanlyWithStoppedStateAndNoLeftoverProcess` exceeded its 30-second exit bound on Task 7's PR run `38031390951`, attempt 1. The push run `38031379455` on the identical commit passed 453/453. Neither the App nor anything it references changed. The Build Ledger records earlier App-integration timeouts under runner saturation (Task 5). This flake recurs, so it gets its own root-cause packet (R5) before Task 8, rather than repeated reruns.
