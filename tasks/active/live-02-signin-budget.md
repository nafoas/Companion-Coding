# LIVE-02 — Sign-in, Dollar Bun Budget, Composition, and First-Run Check (Stage 12, part 2)

Status: **active — gate review**
Authorized: 2026-10-11 by Boss ("This is just for the Builder Bnuy, just put the key somewhere convenient. Dollars per day works … Tell me when you need me, until then you can work").
Accepted remote base: `e8d3d7ebd46f2d7ff80ce671dc6aa58d30aa2fbe`
Working branch: `agent/live-02-signin-budget`
Builder: Claude

## Boss's direction

- **Key convenience.** The key may live wherever is convenient, because this is Builder Prince only. Prince never stores it. It uses the official SDK's own sign-in: the `ANTHROPIC_API_KEY` environment variable (`setx`), or an `ant auth login` profile.
- **Budget.** Dollars per day.
- **No linking to the Claude subscription.** Linking to his Claude account is not possible: API use is billed by the Anthropic Console, separately. Boss was told so.

## Scope

- **Bridge (network-free) — the Bun Budget in dollars.**
  - New `BridgeOptions.DailyCostBudget(LimitPerDay, InputCostPerMillionUnits, OutputCostPerMillionUnits)`.
  - Before each look, the day's (UTC) cost, input plus output, plus the next look's estimated input must stay within the limit. Otherwise the existing `LocalBudget` nap runs until the next UTC day, without a call.
  - The journal now keeps the day's output units, with an older-snapshot default of zero, and they survive compaction and restart.
  - A provider-reported input count now replaces the local estimate for the answered attempt, so the budget follows real usage.
- **Adapter assembly:**
  - **`AnthropicSignIn`** detects a sign-in the SDK will resolve: `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `ANTHROPIC_PROFILE`, or a saved `ant auth login` profile under `%APPDATA%\Anthropic`, `~/.config/anthropic`, or `ANTHROPIC_CONFIG_DIR`.
  - **`AnthropicSemanticProvider.AllowAmbientCredentials`.** Without a stored key, it uses that sign-in only when allowed and present. Otherwise the result is `CredentialsMissing` and nothing is sent. A sign-in the SDK cannot resolve gets a 401, which is a typed `CredentialsRejected`.
  - **`AnthropicPricing`** holds USD per million tokens for `claude-opus-5-5`, `claude-sonnet-5-5`, `claude-haiku-5-5`, and `claude-opus-4-8`. The default model is budgeted at the higher of its own rates and its refusal fallback's rates. An unknown model has no budget, so live calls stay off.
  - **`BuilderBraincaseSettings`.**
    - `braincase-settings.json` lives in Builder Prince's data root. Its fields are `liveCalls` (default false), `model`, `effort`, and `dollarsPerDay` (default 5).
    - It is created off when missing, and it never holds a key: an unknown field such as `apiKey` is refused.
    - A damaged, invalid, or unknown-model file always resolves to off.
  - **`BuilderBraincase.Compose`.** It selects the offline shell unless live calls are on, and otherwise Claude under the dollar budget.
  - **`BraincaseLiveCheck`.** It makes exactly one call with a synthetic picture (no screen, memory, or grant), validated by the shared strict parser. It reports the verdict, summary, usage, cost, and latency.
- **App.** `CompanionComposition` uses `BuilderBraincase.Compose` on its data root, passes the bridge budget to the host, and disposes the provider.
- **`tools/CompanionCore.BraincaseCheck`** is Boss's first-run program:
  - status only by default;
  - `--live` makes one call;
  - `--turn-on` switches live calls on after a pass;
  - `--dollars-per-day=N`.
- **`docs/Builder-Braincase-Setup.md`** is the one-time setup guide.

## Evidence

**Tests (fake transport only; no network and no real key):**

- **`BridgeCostBudgetTests` (8):**
  - the cost arithmetic;
  - output spending reaches the limit, and the next look sleeps until tomorrow without a call;
  - two looks fit, a third naps, and a new day fits two again;
  - the day's spending survives a restart;
  - invalid budgets are refused.
- **`BuilderBraincaseTests` (14):**
  - sign-in detection (the key, token, and profile variables, a saved login, a blank key, none);
  - the saved-login folder follows the SDK;
  - settings are created off and round-trip, damaged or unknown settings always resolve to off, and an `apiKey` field is refused;
  - pricing uses the fallback's higher rates;
  - settings use the SDK sign-in;
  - selection is the shell until live calls are on, then Claude under the budget;
  - signed-in calls: not allowed, absent, a stale sign-in (401 → `CredentialsRejected`, with no key header), and the environment-variable key used;
  - the live check passes with the parsed answer and its cost, and reports a failed call and a refused answer.
- **`BraincaseJournalTests` compaction test:** extended to keep the day's output units.

**Offline smoke run of the check program** (Linux, no sign-in): it reports the settings path, the defaults, and "Sign-in: not found" with instructions, then exits 1. It writes the default settings file with live calls off.

## Mutation pass

19 mutants, all killed:

- the cost check;
- output counted, reported input, the output day reset, and the output snapshot;
- the output price, and the some-price rule;
- the ambient opt-in, the key variable, a blank key, and the saved login;
- the fallback rates;
- the dollars check, an unknown model, and no key field;
- the live switch, the budget applied, and ambient allowed;
- the parser check.

Four of these were first survivors, killed after the tests were strengthened.

## Personal Round Judgments

- **J1 — The SDK's own sign-in, with no key storage in Prince.** This is Boss's "somewhere convenient". On Windows, `setx ANTHROPIC_API_KEY` is the simplest; `ant auth login` is the no-copy alternative.
- **J2 — A $5/day default Bun Budget.** It is protective, changeable in one setting, and sits under Boss's Console limit.
- **J3 — UTC day.** This matches the accepted bridge budget day.
- **J4 — The live check validates the provider and the parser without memory or a grant.** Minting a capture grant outside target authorization would bypass a deliberate authority rule.

## Known limits

- **Credential-echo scan.** The bridge's echo check scans only locally stored secrets. A key held by the SDK's environment or profile is not scanned. It is sent only as a request header and never appears in the prompt.
- **Settings and resets.** The settings file sits in Builder Prince's data root, so a Builder reset also resets it to off.

## Paw Gate

Pending.

## Deferred findings

- **D1 — The first live run.** It needs Boss: Console account, sign-in, and `--live`.
- **D2 — CORE-12.** Placeholder controls, and the guided 10-point Core Prince milestone run.
