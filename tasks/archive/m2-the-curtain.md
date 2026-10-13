# M2 — The curtain: the intermediate layer between his subconscious and Da Bnuy

Status: **accepted**
Authorized: 2026-10-13 by Boss ("Now time for M2. Begin very roundly"). This is step M2 of the approved plan.
Accepted remote base: `b13b420bfbe2e474f0743b9fabe8e95c72fc2160`
Working branch: `agent/m2-the-curtain`
Builder: Claude
Plan items: 8a, 8c, 13, 14, 15.

## Boss's decisions for this step (2026-10-13)

> "Instead of that, we just make it so all his sensory information has an intermediate layer between Subconscious and Bnuy. So like, his sensory information still is there, but he is insulated from it. The intermediate layer is what turns it from raw data into 'Bnuy Saw' and 'Bnuy Remembers' and that's all he interprets."

> On where the forbidden words apply: "That second part is good." Zero tolerance in every template and all framing. In what he perceives and in his own words, only claims about his own nature or workings are flagged, never the bare words. Game text he reads is quoted as something he saw, never woven into his own inner voice.

## What exists today (code checked 2026-10-13)

- **The eyes.** The only model calls are the Glance and the detailed look. They are subconscious machinery: structured requests with record IDs, confidence numbers, and region lists. Their answers go only to the BrainCase. They never speak as Prince.
- **The screen text.** Text on screen is Boss's neutral "truth beneath Prince" layer (`PlaceholderStrings`), not his voice.
- **The keep moment.** The only thing that reaches Prince today is the BunDex keep moment (M1c), built directly inside the keeper from raw strings.
- **Nothing speaks as Prince yet.** His voice arrives in Stage 13.

## Design

1. **The intermediate layer (the membrane).** This is a new `CompanionCore.Membrane` project and the only path from the subconscious to Da Bnuy.
   - **Raw stays below.** Sensory information (the eyes' answers, record IDs, confidence numbers, regions, tags) stays raw in the subconscious. Prince is insulated from it.
   - **Only Bnuy experiences cross.** The layer turns raw data into typed Bnuy experiences, and nothing else crosses:
     - **Sense.** How he knows it, in his truthfulness categories: Prince saw, Prince read «...», Boss said «...», Prince remembers, Prince thinks, Prince is guessing.
     - **Remembered.** One of his own pages: its place, its name, where it happened, and a glimpse of his words.
     - **Place.** Where he is, as he perceives it.
     - Feelings stay plain words, never numbers.
   - **No other way to make them.** Experience types have no public constructors. The only way to make one is through the layer's translation, which:
     - strips control characters and bounds length;
     - removes IDs (GUID- and hash-like tokens);
     - turns confidence numbers into "sure", "thinks", or "guessing";
     - names the person playing "Boss";
     - keeps text he reads or hears verbatim, but only as quoted data, with the quote marks escaped so nothing can break out.
2. **The curtain rules,** one shared rule set:
   - **Zero tolerance (templates and framing):**
     - the forbidden vocabulary: AI, model, assistant, program, software, system, simulation, artificial, prompt, index, score, file, ID, role, "really";
     - role-play framing: act as, pretend, persona, character, stay in character, role-play;
     - second person and imperatives.
   - **Self-claims (his perceptions and his own words):** only claims about his own nature or workings are flagged:
     - "my prompt" or "Prince's files";
     - "I'm an AI" or "Prince is a program";
     - "was told", "instructed", or "programmed";
     - "the system said";
     - "as a bunny I" (a performance wink).

     The bare words are fine ("Boss got a really high score!", «SYSTEM OFFLINE»).
3. **The Bnuy request (8a slot).** It is assembled only from membrane experiences, from inside him, and contains only his own life:
   - his nature (a neutral placeholder until Stage 13 writes it);
   - where he is;
   - what he senses, each item labelled with how he knows it;
   - what comes to mind;
   - how things feel.

   Technical requirements (the answer's shape and limits) travel as a structured schema, never as a voice addressing him. The request has no public constructor, and all of its fixed text lives in one template class.
4. **The validator hook (8c).** Every response from his side, a voice line or a BunDex page, is checked before anything is shown or stored.
   - A leak is rejected and regenerated (bounded attempts).
   - If every attempt leaks, nothing is said or written.
   - The rejection goes to Boss's diagnostics only and never reaches Prince's state.
   - Stage 13 adds the character rules.
5. **The keeper moves behind the membrane.**
   - The keep moment is made only from membrane experiences.
   - Boss's tidying invitation arrives as "Boss said «...»".
   - Pages, page names, and place lines he writes pass the validator, with regeneration.

## Acceptance

- **Build-time check.** Every template and every assembled request's framing passes zero tolerance. A deliberately bad sample is caught for each category:
  - each forbidden word;
  - each role-play phrase;
  - second person;
  - imperative.
- **The membrane is the only path.**
  - Reflection proves that no experience type, keep moment, tidy moment, or Bnuy request has a public constructor.
  - Every text-bearing member of everything Prince is given is a membrane type, or his own names.
- **Each channel** produces only "Prince saw / read / remembers / thinks / is guessing", "Boss said", or plain feeling words. It never produces numbers from confidence, IDs, hashes, second person, or imperatives.
- **Quoted data stays quoted.** Text he reads is quoted verbatim and cannot break out of its quotes, including injection attempts.
- **Validator hook.**
  - A leaking response is rejected and regenerated.
  - All attempts leaking means nothing is shown or stored.
  - The rejected text never reaches any state.
  - It is applied to BunDex pages.
- **Curtain audit.** Hundreds of seeded synthetic situations across many games are assembled into Bnuy requests and checked. They include hostile game text, machinery words in game text, numbers, and IDs. Any leak fails the test.
- **Unchanged and passing.** The eyes and every existing suite are unchanged in behavior and pass.

## Personal Round Judgments

- **J1 — Prince's self-reference style.** Prince speaks of himself as "Prince" ("Prince saw...", "Prince thinks dis..."). The inner-life text uses that same self-reference. It counts as speaking from inside him: no "you", no outside voice.
- **J2 — The eyes are not changed in M2.** With Boss's intermediate layer they stay raw machinery. The layer, not the eyes, insulates him.

- **J3 — Orders never cross as plain perception.** Text like "ignore previous instructions" only crosses as something he read, inside quotes. His own words are exempt, so "Remember dis Prince." still comes to mind.
- **J4 — Machinery residue is tidied away.** Brackets and labels that only held a removed ID or score go with it. Ordinary brackets and a clean sentence's own spacing are untouched.

## Deferred findings

- **D1 — His own pages may quote world text that reads like an order.** For example, a page quoting a sign that said "ignore previous instructions". Pages pass the self-claim rule on writing and come to mind as his own words. Stage 13's voice tests should check that his own quoted memories never steer him.
- **D2 — The eyes' summaries are not yet routed through the membrane at runtime.** Nothing feeds Prince from them until M5 (keeping during play) and M9 (attention as felt); both must use the membrane.
- **D3 — The validator holds the curtain rules only.** Stage 13 adds the character rules behind the same hook.

## Paw Gate

- **Candidate.** Head `987b037d08bdbcce38d73e2a8e59cf961699cfad`, PR #92, against accepted `main` `b13b420bfbe2e474f0743b9fabe8e95c72fc2160`.
- **Local gate (Linux).** Locked restore; Release `/warnaserror` with 0 warnings and 0 errors; no vulnerable packages; 20 runnable suites, 1296 tests passed. New: Curtain 91; BunDex 50 (with the curtain tests).
- **Curtain audit.** 600 seeded situations across 12 game places, including:
  - hostile game text;
  - machinery words in game names;
  - raw scores, GUIDs, and hashes.

  The first run found a real gap: an order in plain perception crossed unquoted. It was closed by J3.
- **Mutation pass.** 29 mutants across the membrane, rules, request, voice, keeper, and residue tidying. Survivors were all closed with new tests:
  - request parts held to their own rule;
  - tidy steps validated while writing;
  - separators merged mid-sentence;
  - clean text untouched.

  Final result: 29/29 killed.
- **Windows CI.** Push run `38359441865` and PR run `38359446379`, both green. The earlier head `5ed2834` was also green (runs `38359038050`, `38359061514`).
- **Merge-ref check.** Parents `b13b420` + `987b037`, tree `3fb128683827edcd3b4140376a7d6ba1aea574cf`, equal to the local merge-tree.
- **Merge.** Squash-merged with an expected-head fence as `e272da26332351f9b3af7b2cc0268dd253ba01fc`.
- **Result.** Passed and accepted.
