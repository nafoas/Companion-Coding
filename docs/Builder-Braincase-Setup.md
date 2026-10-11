# Builder Prince's Braincase — one-time setup (Boss's PC)

Builder Prince thinks with Claude through the Anthropic API. API use is billed by the Anthropic Console, separately from a Claude subscription. This setup is done once.

## 1. Console account

1. Sign in at console.anthropic.com.
2. Add billing.
3. Set a monthly spending limit. That limit is the hard ceiling. Prince's own daily Bun Budget sits under it.
4. Create an API key.

## 2. Let Prince find the sign-in

Choose one of these. Prince never stores the key itself.

### Option A — simplest on Windows

Open a terminal (PowerShell or Command Prompt) and run, with your key:

```
setx ANTHROPIC_API_KEY "sk-ant-...your key..."
```

Then close and reopen the terminal. The key lives in your Windows user environment. To remove it later, run:

```
reg delete HKCU\Environment /v ANTHROPIC_API_KEY /f
```

### Option B — browser sign-in, no key to copy

1. Install the Anthropic CLI `ant` (releases: github.com/anthropics/anthropic-cli/releases).
2. Run `ant auth login` and approve in the browser.

The sign-in is saved under `%APPDATA%\Anthropic`, and Prince's SDK reads it automatically. It expires now and then; run `ant auth login` again when it does.

## 3. First live test (about a cent)

This needs the .NET 10 SDK, the same one that builds Prince. Run these from the repository folder:

```
dotnet run --project tools/CompanionCore.BraincaseCheck
dotnet run --project tools/CompanionCore.BraincaseCheck -- --live
```

- **The first command** only reports the settings and whether a sign-in was found. It spends nothing.
- **The second command** makes one detailed Claude call with a synthetic picture (sky, grass, a red square), never your screen. It prints the answer, the time it took, the tokens used, and the cost. Prince's own strict parser checks the answer.
- **It also makes five quick Glances** with the Glance model (Haiku) at the same picture. It prints each Glance's round trip in milliseconds, the median, and their cost (a fraction of a cent). These are the real numbers for your PC and connection; they decide any tuning.

When it passes, switch Builder Prince's live calls on:

```
dotnet run --project tools/CompanionCore.BraincaseCheck -- --live --turn-on --dollars-per-day=5
```

## Settings

The settings live in `braincase-settings.json` in Builder Prince's data folder. The check prints the exact path.

```json
{
  "liveCalls": false,
  "model": "claude-opus-5-5",
  "effort": "low",
  "glanceModel": "claude-haiku-5-5",
  "dollarsPerDay": 5
}
```

- **`liveCalls`:** set it to `false` to turn live calls off at any time.
- **`dollarsPerDay`:** the Bun Budget. When the day's (UTC) spending would pass it, Builder Prince sleeps until the next day. Memory is never touched.
- **`model`:** one of `claude-opus-5-5` (default), `claude-sonnet-5-5`, or `claude-haiku-5-5` (cheapest).
- **`effort`:** one of `low`, `medium`, `high`, `xhigh`, or `max`.
- **`glanceModel`:** Prince's quick, peripheral looks (VIS-02). The default is `claude-haiku-5-5`, the fastest and cheapest. `""` turns Glances off, and every look is then a detailed one. Glances are paid from the same daily Bun Budget, each at its own model's price.

A damaged file, or a model Prince does not know the price of, always means live calls stay off. The file never holds a key.
