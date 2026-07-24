# Card Editor changelog

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
