# Task Handoff

## Task

LIVE-01 — Claude Braincase Adapter (`tasks/active/live-01-claude-adapter.md`), Stage 12 part 1.

- Branch: `agent/live-01-claude-adapter`.
- Base: `main` `ef0d959`.
- Builder: Claude.

## Changed

- **New `src/CompanionCore.Braincase.Anthropic`.** It holds `AnthropicSemanticProvider` and is the only networking assembly in the Braincase path, using the official `Anthropic` SDK 12.55.0.
- **`ProviderUnavailableReason`** gains `CredentialsRejected` and `RequestRejected`.
- **Tests:**
  - `AnthropicProviderTests` (18, fake transport only);
  - the bridge no-networking test also forbids the SDK and the adapter.
- Solution and lock files.

## Verification

- **Local:** full gate green: 1003/1003 on Linux, 0 warnings, no vulnerable packages (the new SDK included).
- **Mutation:** 17 mutants: 15 killed, 2 equivalent (defense in depth).
- **Windows CI:** pending.
- **No live call has been made, and no real credential exists anywhere in this repository or environment.**

## Remaining

LIVE-02:

- key storage;
- budget setup;
- composition;
- the live validation script.

Then CORE-12.

## Risks and assumptions

- The SDK requires the API key as a string for each call (J4).
- The default model is `claude-opus-5-5` at low effort, and it is configurable.

## Personal Round Judgments

LIVE-01 J1–J4, recorded in the packet.

## Review focus

- The bridge stays network-free.
- Live calls are off by default.
- Authority fields are local.
- Append-only proposals.
- No secret in replies or diagnostics.

## Next safe task

LIVE-02.
