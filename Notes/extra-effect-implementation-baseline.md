# Extra Effect Implementation Baseline

This note is the baseline checklist for adding any new Card Editor extra effect. The goal is not only "make the effect run". A new effect should behave like the rest of the editor: it should plug into timing, power mode, grant-to-card, scaling, conditions, branches, limits, text generation, upgrade deltas, and the shared UI grid wherever the effect's semantics allow it.

If an option is excluded, document the reason in code or in this file. The default assumption is that a new effect supports the universal systems.

## Core Rule

Every new extra effect is two things:

1. A core action, such as deal damage, draw cards, transform a card, apply a power, move cards, or add a reward.
2. A modifier-compatible effect row, which can be delayed, repeated, scaled, gated, branched, granted to other cards, converted into a power, limited by use count, upgraded, and described in card text.

Do not implement a narrow one-off row unless the mechanic truly cannot use the shared systems. For example, "if you played a colorless card this combat, do X" should use the shared history/count condition system, not a hardcoded colorless-card check inside the effect.

## Feature Scope Rule

When the request is "add a feature", treat that as the complete Card Editor version of the feature unless the request explicitly scopes it down. A complete feature includes the core runtime behavior and every compatible baseline editor system: timing, power mode, grant-to-card, repeat/X handling, scaling, conditions, branches, effect limits, self-scaling targets, upgrade deltas, serialization, clone/equality/merge behavior, text preview, live combat text, and UI layout consistency.

For database-backed mechanics, "complete" also means:

- exact reference picker backed by the game's loaded model database
- search/filter UI for that picker
- raw id fallback when an id cannot be resolved
- random mode with relevant filters such as pool, class/color, rarity, type, tag, or keyword
- runtime fallback when filters produce no legal candidates
- text that clearly distinguishes exact references from random filtered pools
- serialization for every selector so presets and saved cards round-trip cleanly
- upgrade delta support for all selectors

Do not ship a hardcoded single target, single rarity, or single pool version when the vanilla domain exposes a broader database.

## Main Source Files

- `mods/card_editor/CardEditorExtraEffects.cs`
  - `CardExtraEffectKind`
  - `CardExtraEffect`
  - `CardExtraEffectDefinition`
  - text formatting
  - effect execution
  - scaling/count/branch logic
  - clone/equality/upgrade merge helpers
- `mods/card_editor/NCardEditorPopup.cs`
  - extra effect row UI
  - advanced property grid
  - row visibility/order
  - selector defaults
  - save/apply from UI state
- `mods/card_editor/CardEditorExtraEffectPower.cs`
  - persistent power entries
  - trigger dispatch
  - power stack/merge behavior
- `mods/card_editor/CardEditorExtraEffectScheduler.cs`
  - one-shot delayed timing execution
- `mods/card_editor/CardEditorExtraEffectTriggerPatches.cs`
  - lifecycle trigger hooks
- `mods/card_editor/CardEditorAutoPlayLoopGuard.cs`
  - use limits and autoplay safety
- `mods/card_editor/CardEditorCreatedCardEffectSourceSupport.cs`
  - effect-source execution, dynamic variable sync, runtime granted effect packages
- Localization and packed copies:
  - `mods/card_editor_pack/.../localization/{eng,zhs}/extensions.json`
  - `built cfiles/localization/{eng,zhs}/extensions.json`
  - `built c files chinese/localization/{eng,zhs}/extensions.json`

## Add-A-New-Effect Checklist

### 1. Data Model

Add the new kind without breaking existing saves.

- Append a `CardExtraEffectKind` enum value. Do not renumber existing values.
- Add a `CardExtraEffectDefinition` with:
  - stable `Kind`
  - user-facing `Label`
  - allowed targets
  - default amount
  - default target
- Add any new fields to `CardExtraEffect` only when existing generic fields cannot express the behavior.
- If adding fields, update:
  - clone/copy helpers
  - equality/equivalence helpers
  - upgrade fusion/delta logic
  - reset/default handling
  - legacy migration if replacing an older effect
- Prefer reusing these existing fields before adding new ones:
  - `Amount`, `AmountIsX`, `AmountXPlus`
  - `RepeatCount`, `RepeatIsX`
  - `Target`
  - `Timing`, `Turns`
  - `Trigger`, `AsPower`, power trigger fields
  - count/scaling fields
  - branch fields
  - card match/filter fields
  - grant-to-card fields
  - specific card/power id fields
  - `EffectId`

### 2. Runtime

Implement the action through vanilla systems where possible.

- Use vanilla commands/helpers for damage, block, draw, discard, exhaust, powers, orbs, pile movement, card creation, shuffle, and rewards when they exist.
- Do not reimplement vanilla math for damage, block, Strength, Weak, Vulnerable, Artifact, Vigor, or generated-card upgrades.
- Preserve the real source card, owner, target, `CardPlay`, and combat state.
- Make the effect work from:
  - normal card play
  - delayed timing
  - power entries
  - granted effects
  - branch effects
  - run-effect-source rows
  - auto-run rows
- Apply universal resolution in this order unless there is a documented reason not to:
  - resolve amount/value source
  - apply conditional bonus
  - evaluate count condition
  - apply history scaling
  - apply repeat count
  - apply use/effect limit
  - execute branch replacement/addition if relevant

### 3. UI

All new controls must live in the existing extra-effect row grid.

- Use `CreateEffectFormRow`, existing compact slots, existing dropdown styling, and existing spin-button patterns.
- Do not create one-off freeform layouts for a new effect.
- Preserve the standard row grammar:
  - row name on the left
  - parameter boxes after it
  - checkbox labels to the left or right using the same style as neighboring rows
  - no extra inline labels like `Color`, `Type`, `Effect` when the row standard already makes the dropdown role clear
- Add visibility logic in `UpdateExtraEffectCustomRows` and row ordering in the advanced grid.
- Add tooltips for any control whose meaning is not obvious.
- Ensure the row works in base and upgrade editing.
- Ensure row state is read back into `CardExtraEffect` in both normal apply and upgrade delta apply paths.

### 4. Text

A new effect needs accurate card text, not only runtime behavior.

- Add a text formatter for the base action.
- Include timing wording.
- Include power wording when `AsPower` is enabled.
- Include grant-to-card wording when `GrantToCard` is enabled.
- Include count/scaling text.
- Include condition-only text.
- Include branch text.
- Include use-limit text and live remaining uses where applicable.
- Include specific card/power names, not raw ids, unless resolution fails.
- Use normal vanilla/card-editor color semantics for dynamic numbers.
- If the effect can be inactive, text must not claim active behavior unless the condition/limit is active.
- Update localization keys where the rest of the system expects localization.

### 5. Upgrade Behavior

Every new effect must define base/upgraded behavior.

- Upgrade deltas should be stable and understandable.
- Numeric fields should merge with base values where the existing editor expects deltas.
- Non-numeric mode changes should be explicit replacement values.
- New fields must be included in:
  - `FuseUpgradeEffect`
  - `AreExtraEffectsEquivalent`
  - `CloneEffect`
  - any "has upgrade difference" helper
  - any row display code that builds upgrade delta rows
- Endless upgrade scaling must not corrupt row identity or target the wrong effect row.

## Universal Modifier Baseline

The systems below are the baseline. A new effect should opt in unless the semantics are incompatible.

## Timing Baseline

There are two timing layers: effect trigger and delayed execution timing.

### Effect Triggers

`CardExtraEffectTrigger` currently supports:

- `OnPlay`
- `OnDraw`
- `OnDiscard`
- `OnExhaust`
- `EndOfTurnInHand`
- `StartOfTurn`
- `EndOfTurn`
- `StartOfEnemyTurn`
- `EndOfEnemyTurn`
- `Fatal`
- `OstyDealDamage`
- `AfterCombat`
- `OnChannel`
- `OnEvoke`
- `TurnBoundary`
- `OnCountEvent`
- `DeckPassiveCombatStart`
- `DeckPassiveCombatEnd`
- `OnMovedToTopOfPile`
- `OnMovedToBottomOfPile`
- `AfterCardEnteredCombat`
- `BeforeHandDraw`
- `AfterAttack`
- `AfterDeath`
- `AfterCombatEnd`
- `OnChosen`

New effects should work under all trigger paths that can legally produce the effect's context. If a trigger lacks a target, resolve target fallback consistently with existing effects.

### Delayed Timing

`CardExtraEffectTiming` currently supports:

- `Immediate`
- `StartOfTurn`
- `EndOfTurn`
- `EndOfThisTurn`
- `StartOfEnemyTurn`
- `EndOfEnemyTurn`
- `StartOfAnyTurn`
- `EndOfAnyTurn`
- `EndOfThisAnyTurn`

If an effect can be delayed, it should be scheduleable with these timing options through `CardEditorExtraEffectScheduler`. Repeating effects should use power mode rather than ad hoc scheduling loops.

### Turn Boundary Options

For `TurnBoundary` style triggers:

- edge: `Start`, `End`, `StartAfterDraw`, `EndAfterDiscard`
- side: `YourTurn`, `EnemyTurn`, `Both`
- card location: `Any`, `Hand`, `DrawPile`, `DiscardPile`, `ExhaustPile`

If a new effect depends on "where the card is" or "when in the turn", use this existing boundary vocabulary.

## Power Baseline

If the effect can logically become "whenever X, do Y", it should support `AsPower`.

Power mode must include:

- `Trigger`
- `PowerTriggerCountEvent`
- `TriggerEveryN`
- `TriggerMaxFires`
- `TriggerMaxTurns`
- `Duration`
- trigger card pool/type/effect filters
- power/status filters for status-based triggers
- `PowerTriggerUsesEventAmount` where the event has a meaningful amount
- `PowerHost`
- `PowerTriggerFrom`
- `PowerTargeting`
- `PowerStackMode`

Power host options:

- `CardOwner`
- `TriggerTarget`
- `CardOwnerWatchOpponents`

Power trigger source options:

- `Self`
- `AnyEnemy`
- `AnyAlly`
- `Anyone`

Power targeting options:

- `TriggerTarget`
- `RememberFirstEnemy`
- `RememberLastEnemy`
- `RememberEnemyRandomFallback`
- `RandomEnemy`
- `AllEnemies`

Power stack behavior:

- `Merge` means equivalent power effects should combine into one logical power entry, following vanilla-style stacking.
- `Separate` means each application stays independent.

Power identity must be based on the effect entry, not accidentally on the base card row that created it. If the user chooses `This Copy`, `All Copies`, `Equivalent`, or `Global`, that choice should decide where counts and uses live.

The base card should not lose turn/combat uses just because it created a power. The power entry should own power firing limits unless the chosen scope explicitly says the card/equivalent/global group is shared.

## Grant-To-Card Baseline

If an effect can be useful as "grant this behavior to cards", it should support `GrantToCard`.

Grant-to-card must include:

- selection pile
- selection mode
- selection amount, including X amount where supported
- include source card toggle
- future matching cards toggle where semantic
- grant filter by pool/type/effect
- card match filter
- duration
- turns field when duration is `Turns`
- enchantment id/duration when using enchantment-like grants

Selection modes:

- `Choose`
- `Random`
- `All`
- `UpTo`
- `Top`
- `Bottom`
- `EachPile`
- `ThisCard`

Grant durations:

- `ThisTurn`
- `ThisCombat`
- `UntilPlayed`
- `Turns`

Enchantment durations:

- `Permanent`
- `ThisTurn`
- `ThisCombat`
- `UntilPlayed`
- `Turns`

Granted effects must preserve row identity, text, limits, power behavior, and scaling behavior. Granting an effect should not silently strip modifiers unless that modifier cannot make sense on a granted card.

## Amount And Value Source Baseline

Any effect with a numeric amount should support these amount modes where possible:

- fixed amount
- X amount
- X plus N
- amount from applied effect row
- amount from value source

Amount source modes:

- `Fixed`
- `AppliedEffectRow`
- `ValueSource`

Value source modes:

- `Common`
- `PowerStatus`

Value source actors:

- `Self`
- `Target`
- `AllEnemies`
- `AllAllies`

Value source aggregation:

- `Value`
- `Sum`
- `Highest`
- `Lowest`
- `Average`

Value source kinds:

- `CurrentHp`
- `MaxHp`
- `MissingHp`
- `Block`
- `Strength`
- `Dexterity`
- `Focus`
- `Weak`
- `Frail`
- `Vulnerable`
- `Poison`
- `Doom`
- `Constrict`
- `Artifact`
- `Thorns`
- `Regen`
- `Plating`
- `Intangible`
- `Buffer`
- `Vigor`
- `Blur`
- `Ritual`

Actual result sources are part of the shared amount/count baseline, not one-off behavior inside individual effects. Effects can read prior row results for HP damage, blocked damage, total damage, overkill damage, total plus overkill damage, killed-count, and application/instance count. Pile-operation and generated-card rows should also report their live applied count by `EffectId`, so later compatible rows can use the actual number of cards moved, exhausted, drawn, generated, played, transformed, or otherwise touched. The paired configured amount/count modes expose what the source row tried to do before runtime caps or eligibility reduce it. Count Logic can also use `Effect Result` for thresholds and repeat scaling, including self-repeating damage loops like Echoing Slash.

Omnislice-style follow-ups use the shared target/amount-source system: first row damages the original target, then later rows can target `Other Enemies` and source their amount from the first row's `Total + Overkill` result. The same setup should work for damage, Doom, block, power application, or any numeric effect that supports amount sources.

Misery-style debuff propagation should use vanilla power semantics, not a hardcoded editor list. Use `PowerModel.TypeForCurrentAmount == PowerType.Debuff` to decide what counts as a debuff, clone the original `PowerModel` with mutability preserved, merge into existing non-instanced powers by id, and call `ITemporaryPower.IgnoreNextInstance()` before applying or modifying copied temporary powers so the temporary wrapper does not double-apply its internally paired power.

If a future vanilla recreation needs target-specific callback identity, such as "the exact target damaged by this same hit chain", add that as a result metric/filter extension instead of creating a bespoke card-only branch.

## Repeat Baseline

Any simple action should support repeat.

Repeat fields:

- `RepeatCount`
- `RepeatIsX`

Do not enable repeat for multi-step UX flows, card pickers, pile operations, complex card generation, play-permission rows, hover rows, transform state rows, self-scaling rows, or effects that would duplicate UI prompts in confusing ways.

## Scaling And Count Baseline

Any amount-like effect should support the shared scaling system.

Scale modes:

- `None`
- `PerHistoryCount` / "Scale Amount by Count"
- `ConditionOnly` / "Only If Count"
- `RepeatByCount` / "Repeat by Count"

Count windows:

- `ThisTurn`
- `ThisCombat`
- `LastTurns`

Window inclusion:

- `IncludeThisTurn`
- `ExcludeThisTurn`

Count comparisons:

- `None`
- `AtLeast`
- `AtMost`
- `Exactly`

Count aggregation modes:

- `CardCount`
- `MatchingEffectAmount`
- `CurrentEnergyCost`
- `BaseEnergyCost`
- `CurrentStarCost`
- `BaseStarCost`

Other count/scaling controls:

- count turns amount for `LastTurns`
- block-loss counting mode
- count step, such as "per 2 matching cards"
- base amount inclusion
- base amount override
- repeat-scaling extra times
- exclude source card

Block-loss counting modes:

- `DamageAndEffects`
- `IncludeBetweenTurns`

### Count Events

Current count events:

- `Played`
- `Drawn`
- `Discarded`
- `Exhausted`
- `Generated`
- `InPile`
- `OrbChanneled`
- `OrbEvoked`
- `CurrentOrbs`
- `EmptyOrbSlots`
- `OrbInPosition`
- `EnemyHasStatus`
- `EnemyIntent`
- `PlayedCardEnergyCost`
- `StarsGained`
- `StarsLost`
- `EnergyGained`
- `EnergyLost`
- `EnergyUsed`
- `BlockGained`
- `BlockLost`
- `StatusGained`
- `StatusLost`
- `DamageDealt`
- `DamageTaken`
- `HealingReceived`
- `Summoned`
- `TimesLostHp`
- `TimesGainedHp`
- `OstyAttacked`
- `OstyAlive`
- `TimesDealtDamage`
- `ThisCardPlayed`
- `ThisCardDrawn`
- `ThisCardDiscarded`
- `ThisCardExhausted`
- `ThisCardDamageDealt`

### Example: "If You Played A Colorless Card"

Use the generic count system:

- `ScaleMode = ConditionOnly`
- `CountEvent = Played`
- `CountWindow = ThisTurn` or `ThisCombat`
- `CountCardPool = Colorless`
- `CountCardType = Any`
- `CountCardFilter = Any`
- `CountComparison = AtLeast`
- `CountConditionAmount = 1`

The same shape should cover "if you have 3 colorless cards in hand":

- `ScaleMode = ConditionOnly`
- `CountEvent = InPile`
- `CountCardPile = Hand`
- `CountCardPool = Colorless`
- `CountComparison = AtLeast`
- `CountConditionAmount = 3`

Do not create separate effect kinds for these cases.

## Card Filters Baseline

Any effect that chooses, counts, generates, grants to, moves, transforms, plays, copies, upgrades, or removes cards should use the shared filters.

Card piles:

- `Hand`
- `DrawPile`
- `DiscardPile`
- `ExhaustPile`
- `AllPiles`
- `Deck`

Pile positions:

- `Top`
- `Bottom`
- `Random`

Generated card pools:

- `Default`
- `Colorless`
- `Ironclad`
- `Silent`
- `Defect`
- `Regent`
- `Necrobinder`
- `OtherColors`
- `Any`
- `Ancient`
- `All`

Generated card types:

- `Any`
- `Attack`
- `Skill`
- `Power`
- `Playable`
- `Status`
- `Curse`
- `Quest`

Count/card effect filters:

- `Any`
- `DealDamage`
- `GainBlock`
- `DrawCards`
- `GainEnergy`
- `GainStars`
- `Heal`
- `Strength`
- `Dexterity`
- `Focus`
- `Weak`
- `Frail`
- `Vulnerable`
- `Poison`
- `Doom`
- `Constrict`
- `Artifact`
- `Thorns`
- `Regen`
- `Plating`
- `Intangible`
- `Buffer`
- `Vigor`
- `Blur`
- `Ritual`
- `Summon`
- `Forge`
- `LoseHp`
- `Exhaust`
- `Ethereal`
- `Innate`
- `Retain`
- `Sly`
- `Eternal`
- `CreatesCards`
- `EnergyCostModifier`
- `StarCostModifier`

Card match modes:

- `Any`
- `CardId`
- `Tag`
- `CustomKeyword`
- `NameContains`

Tag kinds:

- `Vanilla`
- `Custom`

Cost filters:

- disabled
- at most N
- at least N
- exactly N

If a new effect has multiple card filters, name the rows clearly by role but keep the same dropdown order and styling. For example, `Filter`, `Grant Filter`, `Power Filter`, and `Draw Filter` should all use the same compact pool/type/effect dropdown pattern.

## Target And Enemy-State Baseline

Target options:

- `Self`
- `Target`
- `RandomEnemy`
- `AllEnemies`
- `AnyPlayer`
- `AnyAlly`
- `AllAllies`

Enemy/status filters:

- `Weak`
- `Frail`
- `Vulnerable`
- `Poison`
- `Doom`
- `Constrict`
- `Artifact`
- `Thorns`
- `Regen`
- `Plating`
- `Intangible`
- `Buffer`
- `Vigor`
- `Blur`
- `Ritual`
- `Strength`
- `Dexterity`
- `Focus`
- `AnyPowerStatus`
- `Buff`
- `Debuff`

Enemy intent filters:

- `Attack`
- `Defense`
- `Buff`
- `Debuff`
- `Heal`
- `Escape`
- `Summon`
- `Sleep`
- `Stun`

If a new effect needs "target has X", "self has X", or "enemy intends Y", use these selectors rather than making a bespoke condition.

## Conditional Bonus Baseline

Conditional bonus is for changing an effect amount when a condition passes.

Fields:

- `ConditionalBonusAmount`
- `ConditionalBonusConditionType`
- `ConditionalBonusCondition`
- `ConditionalBonusEnemyStatus`
- `ConditionalBonusPowerId`
- `ConditionalBonusEnemyIntent`

Condition types:

- `TargetCheck`
- `HistoryCount`

Target-check conditions:

- target has block
- target has status or power
- target has intent
- self has block
- self has status or power
- target has no block
- self has no block
- target lacks status or power
- self lacks status or power
- target intent is not
- target is damaged
- self is damaged
- target is bloodied
- self is bloodied
- target is full HP
- self is full HP
- target is not bloodied
- self is not bloodied
- target has less HP than you
- target has more HP than you
- target has less Block than you
- target has more Block than you

History-count conditional bonus must use the same count/scaling fields as the main row.

## Branch Baseline

Branching is for running a different effect or an additional effect when a condition passes.

Branch modes:

- `InsteadIf`
- `AlsoIf`

Branch condition types:

- `TargetCheck`
- `HistoryCount`
- `Fatal`

Branch effect selection should use the same row/effect-source selector pattern as self-scaling and effect-limit row selection. If multiple branch rows are needed, use multiple effects or a future branch-tree editor rather than ad hoc add/clear controls.

For condition-only effects like play permission or stateful transform, branch data may be used as a condition source without creating a separate branch effect. Text must make that clear.

## Use Limits And Effect Limits

Use limits are user-facing limits. Autoplay loop safety is separate and should remain a hard safety cap.

Current use-limit windows:

- `Turn`
- `Combat`
- `Run`

Current scopes:

- `ThisCard`
- `AllCopies`
- `EquivalentEffect`
- `Global`

Rules:

- Prefer the standalone `EffectLimit` effect when limiting another row.
- Direct per-effect use-limit controls should only appear on effects where that is still useful and not redundant.
- Limit row selection should behave like self-scaling row selection: pick a row, keep stable row identity, and use multiple limit effects for multiple target rows.
- Power-created effects should track uses on the power/effect entry unless the selected scope says otherwise.
- Text should show remaining uses when runtime state exists.
- Disabled/inactive limits must not generate active "up to X uses" text.

## Self-Scaling Baseline

Self-scaling is the universal "mutate this card or selected effect row" system. New amount-like effects should be targetable by it.

Operations:

- `Increase`
- `Decrease`

Target types:

- `BaseDamage`
- `BaseBlock`
- `EffectRowAmount`
- `DynamicVar`
- `AllNumbers`
- `AllNumbersIncludingRepeats`
- `EnergyCost`
- `StarCost`
- `AllGameplayNumbers`
- `AllGameplayNumbersIncludingRepeats`
- `RepeatCounts`

Recipient modes:

- `ThisCard`
- `SelectedCards`
- `MatchingCards`
- `ThisAndSelectedCards`
- `ThisAndMatchingCards`

Fields:

- `Amount`
- `Repeat`
- `SecondaryAmount`
- `Threshold`
- `Duration`

Number selection:

- `All`
- `First`
- `Random`

New effects should expose self-scaling target fields if they have an amount, repeat count, conditional bonus amount, threshold, duration, or dynamic number that users would expect to scale.

## Specific Card/Power Baseline

Specific card references should use:

- `SpecificCardId`
- `SpecificCardUpgradeMode`
- `CardReferenceDisplayMode`

Upgrade modes:

- `MatchSource`
- `Base`
- `Upgraded`

Display modes:

- `NameOnly`
- `FullText`

Specific power/status references should use:

- `PowerId`
- `StatusIconMode`
- `StatusIconPowerId`
- `StatusCustomPackedIconPath`
- `StatusCustomBigIconPath`
- `CustomPowerName`
- `CustomPowerDescription`

Do not show raw ids in card text if a card or power title can be resolved.

## Database Reference Baseline

Any effect that references a game database entry should support both exact and filtered-random modes when both make gameplay sense.

Exact mode should include:

- a searchable picker backed by the relevant `ModelDb` collection or vanilla database source
- a visible id field for manual paste/debug workflows
- a safe unresolved-id path that preserves the saved id instead of deleting it
- text that resolves to the model title/name when possible

Filtered-random mode should include all relevant domain filters, not only amount:

- cards: pool/color, type, effect/classification, cost, tag, custom keyword, exact id/name matching, upgrade mode
- powers/statuses: specific id, buff/debuff/status category, icon source, custom name/description
- potions: exact potion id, potion pool, rarity, combat-only eligibility, duplicate behavior
- relics/rewards/events, when added: pool/source, rarity/tier, ownership and duplicate rules

Runtime candidate selection should prefer vanilla factories and model definitions. If the requested filters produce no legal candidates, fall back to the closest legal broader set and log or fail softly instead of throwing during combat.

## Potion Maker Baseline

The Alchemize-style potion effect is the pattern for future database-backed generators.

Potion maker controls:

- amount, X amount, repeat, timing, power, grant, scaling, branches, effect limits, upgrade delta, and serialization like other amount effects
- mode: random potion or specific potion
- exact potion picker backed by `ModelDb.AllPotions`
- manual potion id field for unresolved or pasted ids
- random pool filter: vanilla/class pools, character pools, shared, event, token, or all
- random rarity filter: any, common, uncommon, rare, event, token, or none
- combat-only toggle to avoid potions that are not legal in combat when the effect runs in combat
- duplicate toggle to decide whether candidate selection can repeat the same potion id before exhausting other candidates

Potion maker text should name an exact potion when selected. For random mode, text should mention relevant rarity/pool filters when they narrow the result, such as "Create a random rare vanilla potion." If the effect is inactive because a condition or effect limit fails, text must not claim that a potion will be created.

## Transform And Identity Baseline

Transform effects must be gameplay transforms, not just visual identity swaps.

Current transform modes:

- random transform
- specific card transform
- stateful transform
- toggle transform

Stateful transform durations:

- `ThisTurn`
- `ThisCombat`
- `Turns`
- `Combats`
- `Run`
- `UntilPlayed`
- `UntilConditionStops`

Transform effects should:

- preserve enchantments and active granted effect packages unless the user explicitly chooses otherwise
- keep runtime limits attached to the correct transformed card/effect identity
- allow normal history/count conditions
- allow event-style triggers, such as "whenever you play a matching card"
- allow return conditions for toggle-style transforms
- update text on the current transformed card, not only on the original base card
- use a distinct transformed-card visual marker without conflicting with vanilla selection outlines

## Card Generation And Pile Baseline

Card creation/play/move effects should distinguish these actions:

- create a new card into hand
- create a new card into a selected pile
- create and immediately play a random card
- play an existing card from a pile
- move cards between piles
- shuffle draw/discard
- add to top/bottom/random position
- override this card's result pile after play

Do not model "play an existing card" by generating a new copy unless the effect explicitly says it creates a card.

## Branchable Effect Source Baseline

`RunEffectSourceCard`, `ChooseOneEffectSource`, `ConditionalAutoRunEffects`, `EffectLimit`, `SelfScaling`, and future branch/stage systems should all use stable effect-row ids.

Rules:

- Never target rows by visible index only.
- Reordering rows must not change what gets targeted.
- Upgrade rows must retain identity.
- Deleted rows should fail gracefully and update text/UI.
- If a picker targets one row, add another effect for another row instead of bolting on one-off add/clear UI.

## Selected Card Source Baseline

Rows that select, draw, move, mutate, grant to, or otherwise resolve concrete `CardModel` instances should publish those selected cards by stable `EffectId`.

Later rows can use selection mode `Selected Row` to consume those exact card references instead of running a second selector. This is the universal path for cards like Transfigure: first row chooses and mutates the hand card, later rows grant replay, enchant, transform, move, or mutate that same selected card.

Rules:

- `Selected Row` must only list earlier rows, never later rows.
- It should preserve exact runtime card references when possible.
- Deck-only operations may map a runtime card to its `DeckVersion` when required.
- If the source row selects nothing, the consuming row should safely do nothing.
- The feature must stay independent from visible row numbers; store and resolve stable ids.

## Required Final Checks

Before considering a new effect finished:

- Base card can use it.
- Upgraded card can use it.
- Upgrade delta preview is correct.
- Text preview updates when controls change.
- Runtime card text updates in combat for dynamic values.
- It works with `Timing`.
- It works with `AsPower` if semantic.
- It works with `GrantToCard` if semantic.
- It works with `ScaleMode`.
- It works with condition-only counts.
- It works with conditional bonus if amount-like.
- It works with branches if semantic.
- It works with `EffectLimit` if it can be limited.
- It works with self-scaling if it exposes mutable numbers.
- It respects card filters, cost filters, tags, custom keywords, and exact card id matching where relevant.
- It does not affect untouched vanilla cards.
- It uses vanilla commands/helpers when possible.
- It has localization and packed localization copies where needed.
- It has no UI rows that are visibly out of line with the grid.
- It has a live-game verification path written down if the effect needs runtime testing.

## Anti-Patterns

Avoid these when adding effects:

- hardcoding "colorless", "attack", "played this turn", or similar when the count/filter system can express it
- adding a new effect kind for a single condition that should be a modifier
- creating a row that cannot be delayed, powered, granted, scaled, or branched for no technical reason
- implementing damage/block/status math manually instead of using vanilla commands
- using source card null/fake card hacks to pass filters
- letting power-created effects consume the base card's use count accidentally
- using visible row indexes as stable ids
- showing active text for disabled controls
- adding labels/spacing that break the established row grammar
- changing shared vanilla rendering/combat behavior without a strict Card Editor eligibility guard
