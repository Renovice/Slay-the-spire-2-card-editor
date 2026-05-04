# Semantic Context Nodes

This file records source-derived semantics that should be checked before changing card text, triggers, branches, or hover tips.

## Fatal

Source references:

- `Slay the spire 2 beta branch SOURCE/src/Core/Models/Cards/Feed.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/Models/Cards/HandOfGreed.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/Models/Cards/TheHunt.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/HoverTips/StaticHoverTip.cs`
- `Slay the spire 2 beta branch SOURCE/localization/eng/cards.json`
- `Slay the spire 2 beta branch SOURCE/localization/eng/static_hover_tips.json`

Vanilla semantics:

- `Fatal` is a mechanic keyword / rules keyword in card text, but not a canonical `CardKeyword` enum value that can be added with `CardCmd.ApplyKeyword`.
- In code it is represented as a static hover tip term: `StaticHoverTip.Fatal`.
- Vanilla card text writes the condition as `If [gold]Fatal[/gold], ...`.
- The English hover tip says: title `Fatal`, description `Triggers whenever this card kills a non-minion enemy.`
- Vanilla Fatal cards add the hover tip explicitly with `HoverTipFactory.Static(StaticHoverTip.Fatal)`.
- Fatal cards check target eligibility before damage:
  - `bool shouldTriggerFatal = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());`
  - Then they run the attack and check `attackCommand.Results.Any(r => r.WasTargetKilled)`.
- `PowerModel.ShouldOwnerDeathTriggerFatal()` defaults to `true`.
- `MinionPower.ShouldOwnerDeathTriggerFatal()` returns `false`, so minion kills do not count for Fatal.
- `ReattachPower.ShouldOwnerDeathTriggerFatal()` only returns true when all linked segments are dead, so partial segment deaths do not count as Fatal.

Do not confuse these:

- `Fatal` mechanic keyword / static hover tip: a card-text condition for a card killing a fatal-eligible enemy.
- `DoomPower` / localized "Fatality": a power/status amount on creatures.
- Generic kill rewards such as `Sunder` or `KnockoutBlow`: they check `WasTargetKilled`, but they are not `Fatal` text because they do not use `ShouldOwnerDeathTriggerFatal()` and do not add `StaticHoverTip.Fatal`.

Card Editor rules:

- Card-facing Fatal text should be `If [gold]Fatal[/gold], ...`, not "if this card dealt a killing blow" and not a normal keyword row that simply grants a keyword to the card.
- Do not "slap Fatal on a card" as a granted keyword. The card needs text that uses the Fatal mechanic and execution logic that checks the Fatal condition.
- If a generated line includes `Fatal`, `CardEditorVanillaKeywordSupport` can infer the static Fatal hover tip because it registers `StaticHoverTip.Fatal` with the `Fatal` term.
- `CardExtraEffectTrigger.Fatal` and `CardExtraEffectBranchConditionType.Fatal` should mean "this card play killed a fatal-eligible enemy", not just "anything died".
- A branch condition evaluated before the current row resolves cannot represent "this row's damage was Fatal." That needs a post-row branch evaluation path, not just a text change.

## General Source Check Rule

Before adding a mechanic name to card text:

- Check whether vanilla represents it as a `CardKeyword`, `StaticHoverTip`, `PowerModel`, `DynamicVar`, card-specific text, or a command result.
- Check whether vanilla uses eligibility gates beyond the obvious result check.
- Copy vanilla card text style first, including color tags and hover-tip source.
- If our implementation is only an approximation, document that in card text or keep the feature out of the exact-vanilla path.

## Rules Words / Mechanic Keywords

Player-facing "keywords" are broader than the `CardKeyword` enum.

Treat any repeated rules word in vanilla card text as a mechanic keyword until proven otherwise. It may be backed by one of several code shapes:

- A canonical `CardKeyword` on the card, such as `Exhaust`, `Retain`, `Innate`, `Ethereal`, `Sly`, or `Unplayable`.
- A `StaticHoverTip`, such as `Fatal`, `Block`, `Evoke`, `Channeling`, `Forge`, `Summon`, or `Replay`.
- A `PowerModel` or status term, such as `Weak`, `Vulnerable`, `DoomPower`, or `Vigor`.
- A dynamic card-text mechanic with bespoke code, such as card reward, kill reward, generated-card selection, or actual-damage-result logic.
- A text-only label that still needs hover-tip support and exact vanilla phrasing.

Implementation rule:

- Do not assume "keyword" means "add a `CardKeyword`."
- Do not assume "not a `CardKeyword`" means "not a keyword." It can still be a rules keyword in the card text.
- First identify the vanilla backing model, then mirror both the text and the behavior.
- If a Card Editor control exposes such a mechanic, name it like the player-facing rules word, but save/execute it using the correct backing type.
- When generated text uses a mechanic keyword, make sure hover tips are inferred or explicitly attached.

## Vanilla Piles And Shuffle Semantics

Source references:

- `Slay the spire 2 beta branch SOURCE/src/Core/Commands/CardPileCmd.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/Models/Cards/Reboot.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/Models/Cards/ParticleWall.cs`
- `Slay the spire 2 beta branch SOURCE/src/Core/Hooks/Hook.cs`

Vanilla semantics:

- Vanilla has a real pile shuffle action: `CardPileCmd.Shuffle(PlayerChoiceContext, Player)`.
- `CardPileCmd.Shuffle` gathers the current discard pile plus every card currently in the draw pile, randomizes that combined list, then rebuilds the draw pile from it.
- Normal drawing can shuffle discard into draw through `CardPileCmd.ShuffleIfNecessary`.
- `Reboot` explicitly moves hand cards to draw, calls `CardPileCmd.Shuffle`, then draws.
- Some cards add cards to a random pile position with `CardPilePosition.Random`. That is random insertion, not a full pile shuffle.
- Vanilla does not automatically reshuffle a pile just because a new card is added to it. The add position controls where the new card goes unless an effect explicitly calls shuffle.
- Result-pile overrides also exist. `ParticleWall.GetResultPileType()` sends itself to draw instead of discard when the default result pile would be discard.
- There is also a generic hook path, `Hook.ModifyCardPlayResultPileTypeAndPosition`, for modifying where a played card resolves.

Card Editor rule:

- Treat "shuffle pile", "insert into random pile position", and "override this played card's result pile" as separate features. They are not equivalent in vanilla.

## Vanilla Lifecycle Hooks

Vanilla card and power behavior is not only `OnPlay`.

Observed source-level hook surfaces include:

- `AfterCardEnteredCombat`
- `BeforeHandDraw`
- `BeforeCardPlayed`
- `AfterCardPlayed` / `AfterCardPlayedLate`
- `BeforeCardAutoPlayed`
- `AfterCardDrawn`
- `AfterCardDiscarded`
- `AfterCardExhausted`
- `BeforeAttack` / `AfterAttack`
- `BeforeDeath` / `AfterDeath` / `AfterPreventingDeath`
- combat start/end hooks, including power `AfterCombatEnd`

Examples:

- `Pinpoint` and `Stomp` react to cards entering combat.
- `HowlFromBeyond` and `ThrummingHatchet` use before-hand-draw timing.
- Attack, death, and combat-end behavior is spread across card, power, monster, and relic models.

Card Editor rule:

- Recreating vanilla one-to-one needs both effect rows and hook timing coverage. Some vanilla behaviors depend on exact hook ordering, not just an equivalent-looking trigger label.

## Visual Overlays And Dynamic Card Identity

"Visual-only overlays/custom dynamic card identity" means behavior that changes how a card is represented, not just what gameplay action it executes.

Examples of this category:

- Card glows, badges, visual layers, auras, special preview overlays, or custom hover presentation.
- Dynamic text, title, art, or tooltip changes driven by combat state, upgrade state, generated-card state, or internal model state.
- Cards whose behavior depends on custom class identity or model overrides rather than an effect row.

Effect sources are action-oriented: they can say "when this trigger happens, execute this effect." They do not automatically reproduce every card render hook, hover-tip hook, art/title mutation, or custom model override. Those need separate editor-wide visual/identity features or dedicated compatibility hooks.
