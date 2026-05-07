# Card Editor Base-Game Card Coverage Audit

Scope: current STS2 v0.104.0 decompiled card classes. Static mechanical audit of whether a card can be recreated from Card Editor metadata plus Extra Effects. Exact art, VFX, SFX, and wording are ignored unless they affect behavior. Using `RunEffectSourceCard` to execute the original card is not counted as recreating the card. Base-game powers applied via the editor's full Power/Status picker are counted as supported because the power model carries its own behavior.

Correction note: this pass treats the editor's existing card-generation, generated-card modifier, pile selection, exact-copy, Osty, and persistent self-scaling primitives as first-class. The earlier report was too pessimistic for generated-card and selector cards.

Full table: `card_editor_game_card_coverage.csv` (577 cards).

## Result Counts

| Scope | Exact | Almost | Partial | No | Unknown | Total |
|---|---:|---:|---:|---:|---:|---:|
| All card classes | 384 | 186 | 5 | 2 | 0 | 577 |
| Non-deprecated/game pools | 383 | 186 | 5 | 2 | 0 | 576 |

## By Primary Pool

| Pool | Exact | Almost | Partial | No | Unknown | Total |
|---|---:|---:|---:|---:|---:|---:|
| Colorless | 43 | 20 | 1 | 0 | 0 | 64 |
| Curse | 12 | 6 | 0 | 0 | 0 | 18 |
| Defect | 49 | 39 | 0 | 0 | 0 | 88 |
| Deprecated | 1 | 0 | 0 | 0 | 0 | 1 |
| Event | 19 | 7 | 1 | 0 | 0 | 27 |
| Ironclad | 69 | 18 | 0 | 0 | 0 | 87 |
| Necrobinder | 55 | 33 | 0 | 0 | 0 | 88 |
| Quest | 0 | 1 | 0 | 2 | 0 | 3 |
| Regent | 63 | 23 | 2 | 0 | 0 | 88 |
| Silent | 55 | 32 | 1 | 0 | 0 | 88 |
| Status | 7 | 4 | 0 | 0 | 0 | 11 |
| Token | 11 | 3 | 0 | 0 | 0 | 14 |

## Meaning Of Buckets

- Exact: direct metadata plus supported effect rows; generic Apply Power counts when the base-game power itself carries the behavior.
- Almost: behavior is buildable, but a small selector/randomness/capacity edge remains.
- Partial: the main payoff can be approximated, but a missing editor contract prevents 1:1 behavior.
- No: current editor rows cannot realistically express the behavior without C# or invoking the original card.
- Unknown: parser did not confidently recognize the behavior.

## Remaining Main Gaps

- repeat same selected card: 2
- generated card target player: 1
- map/event generation: 1
- map/reward generation: 1
- power payload selected card: 1
- random subset then choose one: 1

## Remaining Partial / No Cards

| Card | Pool | Coverage | Review point | Needed effects |
|---|---|---|---|---|
| LanternKey | Quest | No | map/event generation |  |
| SpoilsMap | Quest | No | map/reward generation |  |
| SeekerStrike | Colorless | Partial | random subset then choose one | DealDamage; ShuffleDrawPile; MoveCardsBetweenPiles |
| DualWield | Event | Partial | repeat same selected card | CopyExactCardsFromPileToDeck |
| DecisionsDecisions | Regent | Partial | repeat same selected card | DrawCards; PlayCardFromPile |
| Largesse | Regent | Partial | generated card target player | AddRandomCardToHand; CreatedCardsUpgraded |
| Nightmare | Silent | Partial | power payload selected card | ApplyPower; card selector payload |

## Stoke Note

Stoke's previous blocker was the lack of a row-result amount source between pile operations and generated-card rows. That universal path now exists: make the exhaust row report its live applied count by `EffectId`, then set the generated-card row's amount source to that row's Applied Effect / Instances value. Use Configured Count instead when the follow-up should use the row's requested count rather than the actual cards affected.

## Transfigure Note

Transfigure's previous blocker was the lack of a shared selected-card source across rows. That universal path now exists: make the first target-card mutation row choose the hand card, then set the later replay/mutation row selection mode to `Selected Row` and target that first row.
