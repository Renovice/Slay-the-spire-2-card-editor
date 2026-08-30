# Card Editor changelog

## 10.1.4 - Run Effect Source lifecycle parity (2026-08-30)

- Fixed borrowed result destinations for `Shining Strike` and multiplayer teammate transfer for `The Ball`.
- Fixed borrowed draw behavior for `Kingly Kick`, `Kingly Punch`, and `Void`.
- Fixed borrowed Exhaust behavior for `Drum of Battle` and `Midnight`.
- Fixed borrowed card-play behavior for `Banshee's Cry`, `Pinpoint`, and `Make It So`.
- Fixed `Howl From Beyond` hosts not auto-playing from the Exhaust Pile.
- Preserved persistent borrowed source state so effects such as `Kingly Punch` retain their combat-long damage growth.
- Expanded the beta regression suite to 18 scenarios and pinned it to the repository's tracked beta assemblies.

## 10.1.3 - Run Effect Source lifecycle fixes (2026-08-30)

- Fixed `Run Effect Source -> Particle Wall` not returning the host card to Hand after a normal play.
- Fixed `Run Effect Source -> Right Hand Hand` not returning the host card from Discard after a card costing at least 2 Energy is played.
- Fixed `Run Effect Source -> I Am Invincible` not auto-playing the host card from the top of the Draw Pile at end of turn.
- Added beta regression coverage for the three cards' immediate effects and borrowed lifecycle behavior.

## 10.1.2 - Whenever, After Death, and Match Energy fixes (2026-08-30)

- Fixed `Reduce Cost -> This Card -> Whenever (Event)` power triggers creating an ignored power-tagged grant instead of an active passive cost reduction.
- Fixed card-owner `Resources -> After Death` powers defaulting to the owner's death while their generated text promised any creature death.
- Fixed card-selection energy-cost qualifiers being rendered twice with differently-colored energy icons.
- Expanded the headless beta suite to 14 scenarios, including every supported Whenever event and generated rules text.

## 10.1.1 - Result Pile destination fix (2026-08-30)

- Fixed `Card Action -> Result Pile` saving every selected destination as Hand for both base and upgraded card edits.
- Added regression coverage for the base-card and upgraded-card editor serialization paths.

## 10.1.0 - Public Beta compatibility and runtime fixes (2026-08-29)

- Retargeted the mod to Slay the Spire 2 public beta `v0.111.0`, including the current networking, hotkey, card-command, and combat APIs.
- Fixed reward-pool state loading, edited Calling Bell/Tea/Hefty Tablet relic values, Nightmare mutation copies, decimal source rounding, live damage/block sources, and delayed power-value snapshots.
- Restored move, manual Exhaust, filtered Transform, triggered This Card cost reductions, After Death power dispatch, and Copy Debuffs against the current beta runtime.
- Added a headless regression harness covering ten runtime and source-contract scenarios without launching the game.
- Kept the unsafe `Grant -> Hits All Enemies` combination blocked so affected cards cannot become stranded during resolution.

## 10.0 — The Card Engine & Chainboard update (2026-07-19)

The biggest rework since release: effects are now a composable engine, and you can build them visually.

### The Chainboard
- New **Effect Chains** board in the main editor column: every effect is a card in a chain, `(box) ──▶ (box) ──▶ (box)`.
- Click a card to edit it **in place**: Amount, Target, piles, "amount = step #n", and more as chips right on the card.
- **+ IF condition** on any step — "this only happens if…" or "if condition: run a different effect instead", authored entirely in the board.
- **Drag & drop**: grab a card, drop it on another to chain them. Groupings are saved per card and survive reopening.
- "+" between or after steps always adds INTO that chain, auto-wired to act on the previous step's result when the effect supports it.
- Chains wrap instead of scrolling; a live sentence under each chain shows its actual rules text; chained rows carry a ⛓ badge in the classic list.

### The Card Engine
- **Selection bus**: card-producing effects (fetch, generate, transform, select…) publish their cards; any later step can act on exactly those cards — N steps deep.
- **Value bus**: amounts can come from earlier results (damage dealt, cards drawn, kills…) for far more effect kinds, including orbs.
- **Inline branch payloads**: conditions can run a directly-configured effect — no helper card needed.
- **Grants unlocked**: pile operations are grantable to cards; granted payloads keep their card filters (fixes "remove all Curses" turning into "remove your whole deck", and "give a specific card a keyword" no longer opens an unfiltered picker on upgraded cards).
- **Truthful targeting**: target dropdowns only offer targets the effect actually resolves; multiplayer wording follows vanilla ("another player", "ALL players", …).
- Vanilla-verbiage text pass across the entire effect corpus, per-target.

### Editor & quality of life
- Categorized effect browser with plain-language descriptions and a Recents row.
- Card data refresh, upgrade-diff highlighting fixes, auto number-link opt-out, and the full 15-item bug-list sweep (stacking parity, enemy copies honoring overrides, composite actions, turn-start hand picks, and more).

### Compatibility & safety
- Compatible with the **2026-07-18 game update** (older builds crash the card library on it).
- Multiplayer sync guard: both players must run the same build — mismatches are refused loudly instead of desyncing quietly.
- Boot-time text-snapshot and capability audits guard against silent regressions.

> Multiplayer note: all players need this exact version installed.
