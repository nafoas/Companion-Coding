# ERPP-01 — Session Transcript Continuity

Status: **accepted — Paw Gate passed 2026-10-10**. With Task 9, Stage 7 is final.
Authorized: proposed and authorized by Boss on 2026-08-11 as Task 9's companion gate. It is opened now under Boss's standing direction to continue autonomously through the pre-API tasks.
Accepted remote base: `cb169a672820233e4357e0b08ff2b0cd9efa3fc5`
Working branch: `agent/erpp-01-session-transcript`
Builder: Claude
Accepted gate head: `5a44dc84b03d870a9277572af2e8f2c28aa43b87`, tree `159c18b12cd930a0195f015423cba125a088f659`
Pull request: #29, squash-merged as `7623b0e0f1f33734fed4cdc29ddbcb3b99c5737d`
Final CI: push run `38054660628`, PR run `38054663515`, each 563/563

## Objective

Formalize the already-required incremental textual session journal as a complete, session-scoped conversation and event transcript, distinct from selective committed autobiographical memory.

Prince must be able to reconstruct the exact active conversational context immediately before an interruption, whether High Attention ("Bnuy Mode") or urgent. He must be able to answer "what were we just talking about?" without that exchange having become a durable memory. One-thread ordering is preserved across interruption, checkpoint and restart, and explicit resumption.

When this gate passes, combined Stage 7 behavior (Task 9 + ERPP-01) is final.

## Authority (from the archived Task 4 record of Boss's authorization)

- Typed structural events must record at least `ConversationActive`, `BnuyModeInterrupted`, `UrgentObservation`, `ReturnOffered`, and `ConversationResumed`, with stable thread and event identifiers and the pre-interruption resume point. Reconstruction must not depend on interpreting an undifferentiated text log.
- The transcript must exclude raw frames, rejected privacy material, credentials, and unrelated application content.
- It must be bounded by an explicit session lifecycle.
- It must never weaken append-only committed-memory authority.
- From the Design BunDex:
  - textual events are journaled incrementally;
  - sessions and Conversation Locks are checkpointed;
  - raw images are not crash-persisted.

## Required implementation

- New `src/CompanionCore.Transcript` and `tests/CompanionCore.Transcript.Tests`, each with a committed lock file.
  - References: Memory (only for the validated location capability), Privacy, and Conversation.
  - No reference to `MemoryStore`, `LocalWriteGate`, the memory journal, or any maintenance surface, so it can never write committed memory.

### Location and format

- `TranscriptLocation` derives a sibling `Transcripts` directory only from a validated development or test `MemoryStoreLocation`. It can never resolve the production root.
- One append-only file per session (`session-<id>.jsonl`). Each line is a checksummed JSON record with a monotonic sequence, and each append is flushed to disk before it is acknowledged. One exclusive writer handle fences a second writer on the same session.

### Typed events

| Event | Meaning |
|---|---|
| `SessionStarted` | The session begins (or resumes after recovery). |
| `ConversationActive` | A thread becomes active. |
| `Utterance` | One line of conversation, from Boss or Prince. |
| `BnuyModeInterrupted` | The thread is interrupted; carries its resume point. |
| `UrgentObservation` | A neutral urgent observation. |
| `ReturnOffered` | Resumption of the interrupted thread is offered. |
| `ConversationResumed` | The interrupted thread resumes. |
| `ConversationSettled` | The thread settles. |
| `CoordinatorCheckpoint` | The coordinator's validated `ConversationCheckpoint`. |
| `SessionEnded` | The session ends; the transcript becomes read-only. |

- Every event carries a stable event ID, the session ID, a UTC timestamp, and its kind. Thread events also carry the thread ID and topic.
- `BnuyModeInterrupted` records the resume point: the event ID of the thread's last recorded event before interruption. `ConversationResumed` references the interruption it ends.

### Recorder

`TranscriptRecorder` maps coordinator updates to structural events in order, records utterances and neutral urgent observations, and records coordinator checkpoints so a locked or suspended thread survives restart.

### Reconstruction

`TranscriptReader.Reconstruct` rebuilds:

- the current or most recent thread;
- its ordered utterances across interruptions and resumptions, bounded to the most recent N;
- whether that thread is interrupted, its resume point, and the exact context up to that point;
- the latest coordinator checkpoint, which restores the coordinator exactly.

The reconstruction works after a restart and never consults committed memory.

### Exclusions

- The transcript accepts text only. There is no image or byte field.
- Utterance and observation text is bounded and must not contain control characters.
- Any text containing a configured credential is refused.
- Content events are refused while privacy is paused, or when their expected privacy generation is stale.
- The transcript records only the session's own conversation and neutral urgent observations. Nothing arbitrary from other applications has a field to enter through.

### Lifecycle and recovery

- A session is explicitly started and ended. After `SessionEnded`, appends are refused.
- Each session is bounded by an event count and a byte budget; past the bound, appends are refused and the refusal is reported.
- Recovery:
  - a torn trailing line is truncated;
  - interior corruption is preserved aside, and a fresh continuation begins with a recovered `SessionStarted`;
  - a session left unended by a crash reopens for continued appends.

## Explicitly forbidden

- Committed-memory writes or update/delete authority.
- Consolidation into memories (Task 10).
- Raw frames, credentials, or rejected privacy material.
- Wiring into the app.
- Personality wording.
- Automatic deletion of transcripts. Retention policy is deferred for a Boss decision with Task 10 consolidation.

## Acceptance scenarios

1. **Interruption context.** A conversation is interrupted by High Attention and then urgency. Reconstruction returns the exact pre-interruption utterances, the resume point, and the interrupted thread, and "what were we just talking about?" is answerable from it alone.
2. **Ordering across restart and resumption.** One-thread ordering holds across interruption, close/reopen (restart), and resumption: same thread ID, monotonic sequence, the resume point referenced.
3. **Locked thread across restart.** A locked thread survives restart through the recorded coordinator checkpoint. Restoring the coordinator from the transcript yields the same locked thread.
4. **Every typed event with stable IDs.** All five required structural events appear with stable, unique IDs.
5. **Exclusions.** Credentials, privacy-paused or stale-generation content, oversized or control-character text, and appends after the session ends are all refused, and none reaches disk.
6. **Recovery and fencing.** A torn tail is truncated, interior corruption is preserved with a recovered continuation, a crashed session reopens, and a second writer is fenced.
7. **Bounds.** The session's event and byte bounds hold.
8. **No committed memory.** The transcript assemblies have no committed-memory writer reachable, and a committed memory store is never created by transcript work.
9. **Determinism.** Identical inputs produce identical transcripts, apart from wall-clock-free stable IDs.
10. **Regression.** All 546 accepted tests still pass on Windows CI.

## Allowed change scope

- `CompanionCore.slnx`
- `src/CompanionCore.Transcript/**`
- `tests/CompanionCore.Transcript.Tests/**`
- `src/CompanionCore.Memory/AssemblyInfo.cs` and `src/CompanionCore.Privacy/AssemblyInfo.cs`: one test-only friend line each, so the tests can build synthetic memory locations and exercise privacy pause, as the Api tests do
- Control records: this packet, `tasks/review/HANDOFF.md`, `BUILD_LEDGER.md`, `README.md`

## Paw Gate

Gate result: **PASS** on 2026-10-10. The separate review confirmed:

**Acceptance.** Scenarios 1–9 pass through 17 tests that drive the real coordinator through the recorder.

**Actual-diff review.**
- The 17-path change is allowlisted.
- No committed-memory writer is reachable, which a reflection test proves.
- The privacy and credential gates cover every content event.
- The writer fence coexists with live shared reads.
- Recovery and recorder ordering were verified.

**Bug hunt.** The live-read defect was fixed (J2). 18 of 18 transcript guards are covered after two test additions and one removal of redundant code (J7).

**Local gate.**
- Locked restore and a clean audit on 28 projects.
- Strict build with 0 warnings and 0 errors.
- 550/550 executable tests.

**Windows CI.**
- Implementation head `7362924`: push run `38054434361` and PR run `38054444729`.
- Gate head `5a44dc8`: push run `38054660628` (job `114220547875`) and PR run `38054663515` (job `114220556433`).
- Every run passed 563/563 on the first attempt, with verified artifacts and the accepted attention-sheet digest.

**Merge ref.** Exact parents (`cb169a6`, `5a44dc8`) and a tree equal to the head tree.

## Personal Round Judgments

All are inside the packet and reversible by a later bounded packet.

### J1 — Its own project

The transcript lives in a new `CompanionCore.Transcript` project, rather than inside Memory's accepted `SessionJournal`, which is the committed-memory recovery tail. Keeping it separate leaves committed-memory authority structurally untouched: the transcript assembly has no member typed as `LocalWriteGate` or `MemoryRepository`, and a test proves it.

### J2 — The writer fence is a separate lock file; the transcript itself is shared for reading

The first version opened the transcript `FileShare.None`, which made live reconstruction impossible. The test suite caught it. `FileShare.Read` on the data file alone would not fence a second writer on Unix, where it takes only a shared lock. The exclusive `.lock` file fences writers, and readers may read a live session.

### J3 — Deterministic, stable event IDs

IDs derive from the session ID and sequence. A preserved corrupt transcript is diagnostic only; IDs are unique within the live transcript.

### J4 — Privacy and credential gates on every content event

Every event except session start and end requires the expected privacy generation to be current, including structural events and coordinator checkpoints, because those carry topic keys. Text, topic, and serialized checkpoint content are all checked for configured credentials.

### J5 — Bounds

Per session: 20,000 events and 8 MiB, with room always reserved for `SessionEnded`. Text is limited to 4,000 characters and topics to 256. All are provisional until Stage 11.

### J6 — Crash recovery is visible

A session left unended by a crash reopens with a recovered `SessionStarted` marker, so restarts appear in the ordered record. An ended session reopens read-only.

### J7 — The pre-interruption context is a snapshot taken at the interruption

The resume point is, by construction, the thread's last event before the interruption. A redundant sequence filter flagged by the mutation pass was removed, and the snapshot's copy semantics are guarded by a test.

## Bug hunt and mutation evidence

- J2's live-read defect was found and fixed during implementation.
- A mutation pass disabled 18 transcript guards:
  - the privacy generation check;
  - credential echo;
  - text and control-character validation;
  - the session-ended refusal;
  - the event bound;
  - the writer fence;
  - torn-tail tolerance;
  - checksum verification;
  - sequence continuity;
  - the reopen marker;
  - the resume point;
  - the interruption reference;
  - urgent observation first;
  - the pre-interruption snapshot;
  - resumed meaning active;
  - location validation;
  - deterministic IDs.

  15 were killed at once. Two exposed test gaps (sequence gap, interruption reference), which gained tests. One exposed redundant code (J7), which was removed. All are now killed.

## Deferred findings

1. **Retention (Boss decision with Task 10):** transcripts are never deleted automatically. A retention or consolidation policy needs Boss's choice alongside Task 10 memory consolidation.
2. **Wiring:** the app composes the coordinator with a recorder, journals every update and utterance, checkpoints on lock and suspend changes, and restores on start.
3. **Stage 11 calibration** of the J5 bounds.
