# LIVE-01 — Claude Braincase Adapter (Stage 12, part 1)

Status: **active — gate review**
Authorized: 2026-10-11 by Boss ("Let's try Stage 12"). Builder Bnuy uses Claude as his Braincase. The Finalized Bnuy will use ChatGPT. Builder stays neutral until Stage 13 ("Let's get it working before we Bnuify it").
Accepted remote base: `ef0d959dabe6fd74e72eac297f00552d0a2911f8`
Working branch: `agent/live-01-claude-adapter`
Builder: Claude

## Boss's decisions this packet implements or respects (2026-10-11)

- **Two brains, two BunDexes.**
  - Builder Bnuy thinks with Claude; the Finalized Bnuy will think with ChatGPT.
  - Each Bnuy's memories, personality, and peets live only in his own BunDex.
  - The model is a stateless borrowed brain.
  - The two BunDexes are already isolated (separate data roots; development builds cannot open production).
- **Seed and growth.** A written personality spec seeds each new BunDex: every Builder reset for Builder Bnuy, exactly once at Awakening for the Bnuy. After that, everything is appended, never overwritten. The Bnuy gets **no artificial changes**: his memory, personality, and continuity are singular and unbroken.
- **Builder stays utilitarian until Stage 13.** This adapter's instructions are neutral, with no persona.
- **Budget.** It is entered locally on Boss's PC, next to the key, matching the spending limit he sets in the Anthropic Console (LIVE-02). Live calls never run without a budget.

## Scope

- **New assembly `CompanionCore.Braincase.Anthropic`.** It is the only networking assembly in the Braincase path. It references the official `Anthropic` SDK 12.55.0. The bridge (`CompanionCore.Api`) stays network-free. Its accepted no-networking test now also forbids the SDK and the adapter assembly.
- **`AnthropicSemanticProvider` (`ISemanticProvider`).** One stateless Messages API call per operation.
  - **Request:** the RAM-only attention-sheet PNG plus the canonical request JSON, with neutral instructions that treat image and request text as data, never instructions.
  - **Structured outputs** constrain the answer to the semantic schema:
    - every object is strict;
    - proposals are append-only (`memory.append.v1` constant);
    - source kinds are limited to the remote allowlist (observed, read, inferred, guess);
    - there is no target-record field.
  - **Locally assigned fields:** the schema version, the operation ID, and usage (from the API's own token counts, cached input included). The model never sets them.
  - **Shared checks:** every reply still passes the bridge's strict parser, credential-echo check, and local allowlist.
- **Live calls are off by default.** They require `LiveCallsEnabled` plus a configured key. A photograph sheet is never sent.
- **The SDK never retries** (`MaxRetries = 0`). The bridge owns bounded retries, backoff, and Naptime.
- **Failure mapping:**

  | Failure | Result |
  |---|---|
  | 401 / 403 | Unavailable (`CredentialsRejected`, new) |
  | 400 / 404 / 422 | Unavailable (`RequestRejected`, new) |
  | 429 | RateLimited |
  | 503 / 529 | Outage |
  | Other 5xx / API errors | Transient |
  | SDK I/O and network failures | Outage |
  | A refusal or a max-tokens stop | Transient, never an interpretation |

  Remote error text is never recorded.
- **Model and effort.**
  - The model defaults to `claude-opus-5-5`, the current recommended model. It is configurable: a cheaper model is one setting.
  - Effort defaults to `low`, because perception is latency-sensitive.
- **Refusal fallback.** With the default model the request opts into the server-side refusal fallback (array form, to `claude-opus-4-8`). Other models are sent without it.

## Not in this packet (LIVE-02 and CORE-12)

- Protected key storage (Windows Credential Manager).
- The local setup for key and budget.
- App composition that selects this provider.
- The live validation script Boss runs on his PC.
- The Core Prince milestone controls and demo.

## Evidence

**`AnthropicProviderTests`, 18 tests, fake transport only; no network and no real credential:**

- live calls are off by default and need a key before anything is sent;
- the request carries the key header, the model, the neutral instructions, the effort, the json_schema format, the fallback, the image bytes, and the exact request JSON;
- another model is sent without the fallback;
- authority fields are local: a model's claimed operation ID, schema version, and usage are ignored, and cached input counts;
- 7 HTTP mappings, each sent exactly once (no SDK retries);
- a network failure is an outage;
- a refusal and a max-tokens stop are never an interpretation;
- a photograph is never sent;
- the schema is strict everywhere and offers only append with remote source kinds;
- invalid options are refused;
- **through the real bridge:**
  - an answer becomes one interpretation and one appended memory;
  - a delete proposal is rejected by the allowlist;
  - an answer echoing the key is rejected as a credential echo and never reaches diagnostics.

**Bridge boundary test extended:** the bridge assemblies reference neither `Anthropic` nor this adapter.

## Mutation pass

17 mutants:

- **15 killed:**
  - the live gate and the photograph gate;
  - SDK retries;
  - the local operation ID and API usage;
  - the stop reason;
  - the 401 and 529 mappings;
  - the fallback and the effort option;
  - strict objects;
  - the token bound;
  - the injection rule;
  - the image sent;
  - the SDK I/O outage.
- **2 equivalent (defense in depth):**
  - **Empty-key check.** `ProtectedCredential` already refuses blank values.
  - **Raw `HttpRequestException` arm.** The SDK wraps network failures in `AnthropicIOException`, whose mapping is killed.

## Personal Round Judgments

- **J1 — A separate networking assembly.** This keeps the accepted Task 7 boundary (a network-free bridge) instead of weakening its test.
- **J2 — Default model and effort.** `claude-opus-5-5` at `low` effort. The model is not downgraded for cost; that stays Boss's choice and is one setting.
- **J3 — Refusals and truncation are transient.** This keeps the provider contract unchanged; the bridge's bounded retries apply.
- **J4 — The key as a string.** The SDK takes the API key as a string, so the clear key exists as an unzeroable string for the duration of one call (known limit). It is never logged or stored.

## Paw Gate

Pending.

## Deferred findings

- **D1 — LIVE-02:** Windows Credential Manager store, local key and budget setup, App composition selecting this provider, and the live validation script.
- **D2 — Stage 14:** an OpenAI adapter for the Finalized Bnuy behind the same seam, and the launch suite run on both brains.
