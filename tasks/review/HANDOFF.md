# Task Handoff

## Task

Task 7 — Stateless Braincase Bridge (`tasks/active/task-07-stateless-braincase-bridge.md`), Roadmap Stage 5. Branch `agent/task-07-stateless-braincase-bridge`, based on accepted `main` `3e99685091b4b398719a2f8fae478a836c900221`.

Builder: Claude.

## Completed

- New `CompanionCore.Api` project (architecture §7) with:
  - the `ISemanticProvider` seam, strict schema-v1 request and response contracts, and one shared parser;
  - deterministic `MockSemanticProvider` and fixture-driven `ReplaySemanticProvider`;
  - `RealSemanticProviderShell`, which has no transport: neutrally unavailable without a key, and live calls disabled even with one;
  - `DisabledSemanticProvider`, plus graceful `BraincaseConfiguration` and `SemanticProviderFactory`.
- `ApiBridge`:
  - local operation IDs reused by every attempt and by the append;
  - bounded attempts, attempt timeouts that abandon non-cooperative providers, bounded backoff, and caller, pending-work, and disposal cancellation;
  - one request in flight;
  - responses processed and published at most once, with deterministic record IDs so a replayed append is `AlreadyCommitted`;
  - grant and privacy-generation fencing before sending, before each attempt, at admission, and before publication.
- Local allowlist: only `memory.append.v1`, remote-permitted source kinds only, links only to records in the request's packet with exact-subject correction, and all authority fields assigned locally. Any forbidden proposal rejects the whole batch.
- Local Resume Packet rebuilt for every request from committed memory and recent journal subjects, bounded and deterministic. Requests carry no remote conversation or capsule state.
- Outage and limit naptime, in order:
  1. durable nap checkpoint;
  2. buffer release;
  3. exactly one typed `Naptime` notice;
  4. local refusal without provider contact.

  After that come single-attempt exponential probes capped at 30 minutes, clamped rate-limit retry-after, and an optional local daily budget. One `Awake` notice follows a successful probe, and an `Unavailable` notice is raised once per reason. Nap state survives restart.
- Checksummed, flushed, append-only bridge journal in a sibling `Braincase` directory of a validated development or test root:
  - torn-tail truncation;
  - corrupt-journal preservation;
  - interrupted-operation marking (never re-sent);
  - atomic bounded compaction;
  - exclusive writer fence;
  - fail-closed on write failure.
- `InMemoryCredentialStore` / `ProtectedCredential` (RAM-only, masked, pinned, zeroed, redacted). Remote text echoing a configured secret is rejected, including JSON-escaped forms.
- Hidden diagnostics snapshot and provisional usage estimates. Logs carry categories, enums, and counts only.

## Changed

50 paths, all inside the packet allowlist:

- `CompanionCore.slnx`;
- `src/CompanionCore.Api/**` (21 files including the lock file);
- `tests/CompanionCore.Api.Tests/**` (15 files, including two sanitized replay fixtures, the lock file, and the sequential-execution `AssemblyInfo.cs`);
- one test-only friend line each in the Capture.Contracts, Memory, and Privacy `AssemblyInfo.cs`;
- this handoff and the active packet.

No App, CI, presentation, capture, target-authorization, memory-behavior, privacy-behavior, runtime, or documentation source changed.

## Verification

Local, on the pinned SDK 10.0.302 (Linux cross-build):

- `dotnet restore CompanionCore.slnx --locked-mode -p:EnableWindowsTargeting=true` passed for all 22 projects.
- `dotnet build CompanionCore.slnx -c Release --no-restore /m:1 /p:EnableWindowsTargeting=true /warnaserror --no-incremental`: 0 warnings, 0 errors.
- The vulnerable-package audit covered all 22 projects and found nothing vulnerable.
- Executable tests passed 440/440 with no skips:

  | Suite | Tests |
  |---|---|
  | Api | 128 |
  | Capture | 14 |
  | Capture Worker | 68 |
  | Memory | 68 |
  | Presentation | 50 |
  | Privacy | 13 |
  | Runtime | 26 |
  | TargetAuth | 73 |

- The Api suite ran 40 consecutive times with no failure.
- Mutation pass: disabling each key guard turned the suite red. The exceptions are two documented defense-in-depth layers (J14) and the attempt timeout, which shows up as a blame-hang abort. A gap the pass exposed was closed with an assertion.
- `git diff --check` is clean, exactly one packet is active, and the allowlist check passed.

Windows CI (`windows-latest`, locked restore, 22-project audit, Release build, all tests, both artifact uploads):

- **Earlier heads `cecc766`, `fc05f61`, `9cc20be`:** 3 of 6 runs failed only the App shutdown test, at its 30-second bound:
  - PR run `38031390951`, attempt 1;
  - push run `38031750912`;
  - PR run `38031754225`.

  The other runs passed 453/453. TRX timing traced the failures to this PR's suite load (J15), not to a legacy flake.
- **Corrected head `f1f3b8c`:**

  | Event | Run | Job | Result | App shutdown test | Api suite wall time |
  |---|---|---|---|---|---|
  | push | `38032069504` | `114154913047` | 453/453, first attempt | 3.5 s | ≈42 s, sequential |
  | PR | `38032071891` | `114154920016` | 453/453, first attempt | 10.2 s | ≈40 s, sequential |

  Per suite:

  | Suite | Tests |
  |---|---|
  | Api | 128 |
  | App Integration | 13 |
  | Capture | 14 |
  | Capture Worker | 68 |
  | Memory | 68 |
  | Presentation | 50 |
  | Privacy | 13 |
  | Runtime | 26 |
  | TargetAuth | 73 |

  - Both artifact digests verified on each path.
  - The synthetic attention sheet is 792×621 RGBA with the accepted digest `sha256:5eb11c967890ac8b3fb4cfcc1b5892ed8462c77598a3e0e78abc939f73b046dd`, and it was inspected visually.

## Remaining

- CI on this evidence descendant, the merge-ref check, merge, closure records, and post-merge `main` CI.

## Risks and assumptions

- All bounds and the usage estimate are provisional until Stage 11 and the final API gate (J9).
- The daily budget pre-check counts one attempt's estimate. Retries within the same operation can add at most `MaximumAttempts - 1` more.
- `CancelPendingWork` reaches operations that have already linked their token. One that has not yet linked it is still fenced by privacy generation.
- The bridge is not composed into the WPF app yet (J2, deferred finding 1). The privacy-stop route to `CancelPendingWork` arrives with that composition; until then, generation fencing alone guards late results.

## Personal Round Judgments

J1–J15 are recorded in the packet.

## Review focus

- Privacy fencing order in `ApiBridge.ProcessSuccessAsync`: lease, then gate append under the expected generation, then currency check, then publish.
- Nap ordering in `EnterNap`: checkpoint, then buffer release, then completion, then notice.
- Journal recovery and compaction atomicity.
- Credential hygiene: no exception text or remote text in logs, and both raw and decoded echo checks.

## Repository state

- Branch `agent/task-07-stateless-braincase-bridge`, draft PR #21. Commits:
  - implementation `cecc766`;
  - records `fc05f61` and `9cc20be`;
  - load correction `f1f3b8c`;
  - this evidence descendant.

## Next safe task

Complete this Paw Gate. Then open Task 8 (attention engine, Stage 6) through its own packet.
