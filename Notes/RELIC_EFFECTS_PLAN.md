# Relic Effects — Design & Implementation Plan

Status: **Plan / approved scope, not yet implemented.**
Scope (per decision 2026-06-16): (1) add effects to **existing** relics via the override system **and** create **brand-new custom relics** from scratch; (2) expose the **full ~60-hook** trigger surface; (3) this doc is the persistent reference.

Goal: let users build relics that **trigger the card editor's existing effects** (on pickup, at combat/turn start, whenever a card is played, on kill, etc.) — "port all our card effects onto relics."

---

## 1. How relics actually work (the constraint that shapes everything)

- `RelicModel` is `public abstract class RelicModel : AbstractModel` (`Core/Models/RelicModel.cs:22`). Concrete relics are **C# subclasses** that implement behavior by **overriding virtual hook methods**. Relics are **not** data-driven.
- The hook methods are **inherited from `AbstractModel`** (`Core/Models/AbstractModel.cs:126-621`, ~60 of them) — e.g. `AfterCardPlayed`, `BeforeSideTurnStart`, `AfterSideTurnStart`, `AfterCombatVictory`, `AfterDamageReceived`, `BeforePowerAmountChanged`, plus value-returning `Modify*` hooks (`ModifyDamageAdditive/Multiplicative`, `ModifyBlockAdditive`, `ModifyEnergyGain`, `ModifyCardPlayCount`, …).
- `RelicModel.ShouldReceiveCombatHooks => true` (`:388`) auto-registers every relic with the unified Hook bus. `Hook.cs` dispatches each event to **every** listener (`combatState.IterateHookListeners()`) with **no card/relic/power distinction** — relics are peers of cards in the same bus.
- Relic-only lifecycle (NOT on the combat hook bus): `AfterObtained()` (`:500`, pickup) and `AfterRemoved()` (`:505`, lost) — plain virtual `Task`s, called manually by the run/event code.
- State: `DynamicVarSet` (`:261-275`) for counters/values, persisted via `[SavedProperty]` + `SavedProperties.From(this)` (`:516`). Display via `ShowCounter`/`DisplayAmount` (`:320-322`). Examples: HappyFlower (`AfterSideTurnStart`, energy), Kunai (`AfterCardPlayed`, Dexterity every N attacks), BlackBlood (`AfterCombatVictory`, heal).

**Implication:** we cannot inject effects into a relic's compiled hook methods. We add a **parallel layer** that, when a hook fires, runs the user-configured effects for relics that have them. This is exactly what the card editor already does for **powers**.

---

## 2. Current relic editor (what we extend)

- `RelicOverride` (`CardEditorRelicOverrides.cs:26-42`) — overlay on a **vanilla relic by `ModelId`** with only: `DynamicVarBaseValues` (number changes), `CustomDescription(Enabled)`, `PoolKeys`, `FixedSourceKeys`. **No effects.**
- Applied via Harmony patches: `RelicModel.ToMutable` postfix → `ApplyTo` (`:1189`), `get_DynamicDescription` prefix (`:1198`), `RelicPoolModel.GetUnlockedRelics`/`get_Pool`/`AncientEventModel.AllPossibleOptions` (`:1214-1283`).
- Persistence: `CardEditorRelicOverrideStore` JSON at `user://card_editor/relic_overrides.json`, `CurrentVersion=1` (`:754-911`).
- UI: `NRelicEditorPopup.cs` — `AddNumberSection`(402), `AddTextSection`(450), `AddPoolSection`(487). Preset panel exists but `SetCreatorMode(false)` (presets not yet enabled for relics).

---

## 3. Core architecture — relic as a new effect host

The effect engine (`CardEditorExtraEffects.ExecuteEffect`, `:26514` → `ExecuteEffectCore`, `:27348` → switch `:27721-28499`, 143 kinds) reads **owner** and **source card** from three `AsyncLocal` contexts, not from a real card play:
- `CardEditorEffectSourceContext.Current ?? cardPlay?.Card` — the source card.
- `CardEditorPowerExecutionHostContext.Current ?? cardPlay?.Card?.Owner?.Creature` — the executing creature.
- `CardEditorPowerSourceMap` — power→source-card map.

Powers already exploit this: on a trigger they build a **synthetic `CardPlay`** (`Card = sourceCard`, `IsAutoPlay = true`), push the three contexts (`CardEditorExtraEffectPower.cs:1001-1004`), and call `ExecuteEffect`. **The engine needs ZERO changes.**

```
Relic w/ effects ──obtained──▶ hidden companion effect-host on owner (seeded w/ effects + relic proxy-card)
        │
   game Hook fires ──▶ existing power-trigger patches ──▶ synthetic CardPlay + 3 contexts ──▶ ExecuteEffect()  [reused as-is]
   relic lifecycle (AfterObtained/Rest/etc.) ──▶ small new direct-dispatch ──▶ same synthetic-play path
   Modify* hook fires ──▶ separate passive-modifier layer (returns a number; NOT effect-runner)
```

**The one genuinely new primitive: a relic "proxy card"** — a minimal synthetic `CardModel` whose `Owner = relic.Owner`, used as the effect's `required CardModel SourceCard` (`CardEditorExtraEffectPower.cs:29`). Precedent: the created-cards system already manufactures synthetic cards.

---

## 4. Two distinct trigger mechanisms (critical for the "all 60 hooks" scope)

The ~60 hooks split into two kinds that need **different** handling:

1. **Reactive hooks** (`After*` / `Before*`, return `Task`) → **run a list of effects.** This is the main feature and maps cleanly onto the power-trigger model. Examples: `AfterCardPlayed`, `AfterSideTurnStart`, `AfterCombatVictory`, `AfterDamageReceived`, `AfterOrbChanneled`, `AfterShuffle`, `AfterEnergyReset`, plus lifecycle `AfterObtained`.
2. **Modify hooks** (`Modify*`, return a **value**) → **passive numeric modifiers**, NOT effect-runners. You cannot "run Gain Block" inside `ModifyDamageAdditive` — you return a number. These need a separate small config: `{ which calculation, +flat / ×mult, optional condition }`. Examples: `ModifyDamageAdditive/Multiplicative`, `ModifyBlockAdditive`, `ModifyEnergyGain`, `ModifyCardPlayCount`, `ModifyHandDraw`, `ModifyRestSiteHealAmount`.

**Plan:** ship reactive-hook effects first (the bulk of "port our effects"); add the passive-modifier layer as its own sub-feature (it's smaller but distinct UI + dispatch).

---

## 5. Full trigger map (reactive hooks → relic-trigger enum)

Add `CardExtraEffectRelicTrigger` (new enum, serialized by name) grouped as below; each maps to an `AbstractModel` hook (or `RelicModel` lifecycle). Surfacing **all** is approved; UI groups them so the long list stays usable.

- **Lifecycle:** OnPickup (`RelicModel.AfterObtained`), OnRemoved (`AfterRemoved`), OnRest, OnShopEntered, OnRoomEntered, OnMapGenerated, OnItemPurchased.
- **Combat lifecycle:** OnCombatStart (`BeforeCombatStart`/after), OnCombatEnd (`AfterCombatEnd`), OnCombatVictory (`AfterCombatVictory`).
- **Turn:** OnPlayerTurnStart (`AfterPlayerTurnStart`/`AfterSideTurnStart`), OnTurnStartPre (`BeforeSideTurnStart`), OnTurnEnd, OnEnemyTurnStart/End.
- **Cards:** OnCardPlayed (`AfterCardPlayed`), OnBeforeCardPlayed, OnCardDrawn, OnCardDiscarded, OnCardExhausted, OnShuffle (`AfterShuffle`), OnFlush (`Before/AfterFlush`).
- **Combat math reactions:** OnDamageDealt, OnDamageReceived (`AfterDamageReceived`), OnBlockGained, OnHpLost, OnHeal, OnEnemyKilled (Fatal).
- **Powers/statuses:** OnPowerGained (`After/BeforePowerAmountChanged`), OnPowerLost, OnStatusApplied.
- **Resources/orbs:** OnEnergyGained, OnEnergyReset (`AfterEnergyReset`), OnStarsGained, OnGoldGained, OnOrbChanneled (`AfterOrbChanneled`), OnOrbEvoked.

(Final exact list pinned during Phase 2 by reading `AbstractModel.cs:126-621`.)

**Modify-hook config** (separate): ModifyDamage, ModifyBlock, ModifyEnergyGain, ModifyCardPlayCount, ModifyHandDraw, ModifyRestSiteHeal, etc.

---

## 6. Which effects port (143 kinds → 3 buckets)

- ✅ **Host-agnostic, work as-is:** GainBlock, DealDamage, Heal, LoseHp, apply/remove all Powers & statuses, Draw/Discard/Exhaust(pile), GainEnergy/Stars/Gold, Channel/Evoke Orbs, Summon, conditions, value-sources, the new Current-Stars/Energy/OrbSlots scaling, Non-Attack/Skill/Power filters.
- ⚠️ **Adaptable (small overload):** CreatedCardsCostLess / GeneratedCardsCostLess / cost auras — already take an `owner` param (`CardEditorCardTypeCostAuras`, `CardEditorDrawnGeneratedCostController`); add a relic overload.
- ❌ **Excluded on relics (hidden in UI):** CardCostsLess-on-self, ExhaustThisCard, AddCopyOfThisCard / AddExactCopyOfThisCardToDeck, AutoPlaySelfFromPile/AutoDrawSelfFromPile, SelfScaling/TargetCardMutation/TransformThisCard, X-cost, GrantToCard, Enchant. (`SupportsAsPower()` `:5287` already blacklists 23; add a `SupportsOnRelic(kind)` companion gate.)

---

## 7. Components to build

| # | Component | Key reuse / new | Files |
|---|---|---|---|
| A | **Relic proxy card** — synthetic `CardModel`, `Owner = relic.Owner`, no pile | new; mirror created-cards synth | new `CardEditorRelicProxyCard.cs` |
| B | **Relic effect host** — store effect entries; reuse `PowerEffectEntry`; attach hidden companion power at combat start / direct-dispatch for lifecycle | reuse `CardEditorExtraEffectPower` (`:24-200`, AddPowerEffects `:133`, dispatch `:1060/1130/1343/1365`) | new `CardEditorRelicEffectHost.cs` |
| C | **Trigger enum + dispatch** — reactive hooks (extend existing `CardEditorExtraEffectTriggerPatches`), lifecycle patches (`RelicModel.AfterObtained` etc.), Modify-hook patches | reuse in-combat patches; small new lifecycle/Modify patches | `CardEditorExtraEffectTriggerPatches.cs` + new `CardEditorRelicTriggerPatches.cs` |
| D | **Custom-relic definition system** — ID prefix `CARD_EDITOR_CREATED_RELIC`, a generic data-driven `RelicModel` subclass that reads its triggers/effects/display from the definition; rarity; pool registration (reuse pool-override patches); icon via `CardEditorCustomIconLoader`; runtime loc for Title/Desc/Flavor (mirror `TryBuildCustomDynamicDescription` `:172`) | mirror created-cards (`CardEditorCreatedCards.cs`, `CardEditorDefinitionStore`, `CardEditorCustomIconLoader.cs`) | new `CardEditorCreatedRelics.cs`, `CardEditorRelicDefinitionStore.cs` |
| E | **Serialization** — `RelicOverride.ExtraEffects: List<RelicEffect>` (RelicEffect = `CardExtraEffect` + relic trigger) + `ModifyHooks` list; bump relic-override `CurrentVersion` 1→2 (old presets load, missing field = empty); reuse `CardExtraEffectDto` (`CardEditorPresetStore.cs:1556-2700`) | reuse effect DTO; registry pattern `CardEditorCustomStatusRegistry.cs` | `CardEditorRelicOverrides.cs`, `CardEditorPresetStore.cs` |
| F | **UI** — effect rows in `NRelicEditorPopup` reusing `AddExtraEffectRow` (`NCardEditorPopup.cs:14132`, ~75-80% reusable); swap trigger dropdown for relic triggers; hide OnPlay/timing/"This Card"/"Triggering Card"; plus a "New Custom Relic" creation flow (name/desc/icon/rarity/pool) | reuse `ExtraEffectRow` (`:4492`), `ConfigureExtraEffectTargets` host param | `NRelicEditorPopup.cs` |

---

## 8. Phased implementation

1. **Spine** — A (proxy card) + B (host) + minimal E, with ONE reactive trigger end-to-end (e.g. "At combat start: gain 5 Block" on an overridden relic). Proves contexts/dispatch.
2. **Reactive triggers + gating** — full reactive relic-trigger enum (all hooks) + dispatch + `SupportsOnRelic()` filtering + serialization (override existing relics).
3. **Relic editor UI** — effect rows + relic-trigger dropdown + hide card-only controls + card-text-style auto description.
4. **Custom relics from scratch** — D (definition store, generic data-driven relic subclass, ID/icon/loc/rarity/pool), creation UI.
5. **Modify hooks** — passive numeric-modifier layer (config + Modify* patches + UI).
6. **Polish** — counters/`DisplayAmount`, lifecycle triggers (rest/shop), targeting model, presets-for-relics, localization of generated text.

---

## 9. Key design decisions (recommendations)

- **Host = hidden companion power for in-combat triggers** (max reuse), + direct dispatch for out-of-combat lifecycle. Companion power applied on combat start (seeded from owned relics' effects), removed on combat end.
- **Proxy card**: a lightweight wrapper `CardModel` per relic (Option A from the dive) — minimal surface, `Owner = relic.Owner`, `Pile = null`.
- **Targeting**: owner-centric (Self/AllEnemies/RandomEnemy/AllAllies) + "event actor/target" where the triggering event provides one (e.g. OnEnemyKilled → the killed creature; OnCardPlayed → the played card's target). No "marked target" picking for relics initially.
- **Counters**: generic `DynamicVar['Counter1..N']` auto-created when a user adds a counter; `DisplayAmount` bound to a chosen counter.
- **Custom relic localization**: editor-defined English text injected at runtime via the existing runtime-LocString mechanism (`CreateRuntimeRelicLocString`, `:198`); per-language translation optional/later.
- **Modify hooks**: separate "passive modifier" entries, not effects.

---

## 10. Open questions to resolve during build

- Exact final reactive-trigger list & their `AbstractModel` method names/signatures (pin from `AbstractModel.cs:126-621`).
- Relic-effect **scheduling/timing** (relics may want "next turn"/"start of next combat") — does the existing `CardEditorExtraEffectScheduler` cover it, or add relic timing?
- **Stacking/merge** semantics if a player has duplicates of a custom relic.
- **Selection-mode effects** on relics (effects that ask the player to pick cards) — support with a choice UI, or block.
- Out-of-combat effect execution context (no combat state) — which host-agnostic effects are even valid at pickup/rest.
- `AfterRemoved` is a manual callback (not on the hook bus) — confirm where it's actually invoked before relying on OnRemoved.

---

## 11. Risks & mitigations

- **R: Modify-hook return semantics** — biggest unknown; mitigate by scoping it to a later phase with a narrow config, not the general effect engine.
- **R: Proxy-card completeness** — some effects read more `CardModel` members than expected; mitigate by building incrementally (Phase 1 spine) and expanding the proxy as effects demand.
- **R: Out-of-combat triggers** lack combat state — gate which effects are allowed per trigger.
- **R: Custom-relic icon/loc/pool registration** — largest new surface; isolated to Phase 4, mirrors the proven created-cards system.
- **R: Save back-compat** — additive fields + version bump; old presets must load (validated).

---

## 12. Effort (rough)

Large feature. Phases 1-3 (override existing relics with full reactive triggers + UI) = the core "port our effects" win and is the bulk of the value. Phase 4 (custom relics from scratch) and Phase 5 (modify hooks) are each substantial add-ons. Recommend shipping per-phase.

---

## Evidence index (from the 6-agent deep dive, 2026-06-16)
RelicModel `Core/Models/RelicModel.cs` (:22 abstract, :388 hooks, :500/:505 lifecycle, :261-275 vars, :320-322 display). AbstractModel hooks `Core/Models/AbstractModel.cs:126-621`. Hook dispatch `Core/Hooks/Hook.cs`. Current editor `CardEditorRelicOverrides.cs` (DTO :26-42, patches :1189-1283, store :754-911), `NRelicEditorPopup.cs`. Engine `CardEditorExtraEffects.cs` (ExecuteEffect :26514, Core :27348, switch :27721-28499, kinds :57-202, SupportsAsPower :5287, triggers :738-766, targets :726-736). Bridge `CardEditorExtraEffectPower.cs` (entry :26-43, AddPowerEffects :133, dispatch :1060/:1130/:1343/:1365, contexts :1001-1004), `CardEditorEffectSourceContext.cs`, `CardEditorPowerExecutionHostContext.cs`, `CardEditorPowerSourceMap.cs`. UI `NCardEditorPopup.cs` (AddExtraEffectRow :14132, ExtraEffectRow :4492). Serialization `CardEditorPresetStore.cs` (DTO :1556-2700, v17), registry pattern `CardEditorCustomStatusRegistry.cs`. Custom-content precedent `CardEditorCreatedCards.cs`, `CardEditorDefinitionStore`, `CardEditorCustomIconLoader.cs`.
