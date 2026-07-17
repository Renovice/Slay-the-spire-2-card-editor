# Potential Bugs

Last updated: 2026-06-21

This file tracks suspected bugs and risky interactions found during repo-wide review. Items marked as "reported" came from player/user reports; items marked as static-confirmed still need in-game reproduction, but they have a concrete source-level failure path.

## Deep-Dive Review Findings

### 1. Relic effects may not execute because the proxy card is never registered

**Priority:** P1  
**Area:** Relic Editor / relic effect runtime  
**Status:** Suspected bug from static review

`CardEditorRelicProxyCard` is used by both the relic editor and runtime relic trigger executor, but the normal model registration path appears to only auto-register classes named `CardEditorCreatedCard<number>`. The relic proxy class does not match that naming pattern.

Potential impact:

- The Relic Editor may fail to add effect triggers because the proxy card model cannot be resolved.
- Existing relic effects may fail at runtime when the system tries to create the proxy card.
- If proxy creation fails, relic trigger execution can return early and skip the remaining effects for that trigger.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorMod.cs`

### 2. Relic editor exposes source-card actions that are unsafe in relic effects

**Priority:** P1/P2  
**Area:** Relic Editor embedded card-effect UI  
**Status:** Suspected bug from static review

The embedded relic effect editor hides some unsupported effect kinds, but the unified `Card Action` selector still appears to expose variants that depend on a real source card, such as granting extra effects, transforming cards, playing cards from piles, delayed pile actions, or consuming card value.

In relic mode, these effects run through a hidden proxy card. That means an effect that looks valid in the UI may operate on the proxy instead of the player-visible card, or may silently do nothing.

Potential impact:

- Relic effects can be configured in the UI but behave incorrectly at runtime.
- Effects that rely on "this card" or source-card identity may target the hidden relic proxy.
- Some relic effects may appear broken even though the underlying card effect works in normal card context.

Relevant files:

- `mods/card_editor/NCardEditorPopup.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`

### 3. Delayed or countdown relic effects can retain a stale proxy-card reference

**Priority:** P2  
**Area:** Relic effects / scheduler / countdown effects  
**Status:** Suspected bug from static review

Relic triggers create a temporary proxy card, execute effects through it, then remove that proxy from combat state. However, delayed effects and countdown payloads can store the source card instance for later execution.

If a relic schedules delayed work, that later work may still reference the removed proxy card as its source or use-limit key.

Potential impact:

- Delayed relic effects may execute with stale source-card context.
- Use-limit tracking may be tied to a temporary proxy instead of a stable relic/effect identity.
- Effects depending on source-card state may fail, no-op, or behave inconsistently.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/CardEditorExtraEffectScheduler.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorCountdownEffectPower.cs`

### 4. Full solution build is broken by temporary/decompiler files

**Priority:** P2  
**Area:** Build / repo hygiene  
**Status:** Confirmed by build command

The mod project builds, but the full solution build fails because root-level temporary or decompiler files are implicitly compiled by the SDK-style root project.

Observed failing files:

- `.tmp_cardcmd.cs`
- `_tmp_NCardHolderHitbox.cs`
- `_tmp_NGridCardHolder.cs`

Potential impact:

- CI or users building `Slay the spire 2.sln` will fail before validating the actual mod.
- Build errors from scratch files can hide real compile errors.
- New contributors may think the repo is broken even if the card editor project itself builds.

Relevant files:

- `Slay the spire 2.csproj`
- `.tmp_cardcmd.cs`
- `_tmp_NCardHolderHitbox.cs`
- `_tmp_NGridCardHolder.cs`

### 5. `StarsSpent` counting patch is brittle because it hooks a private method

**Priority:** P3  
**Area:** Resource count tracking  
**Status:** Risky implementation, not confirmed broken

The `StarsSpent` resource counter patches private `CardModel.SpendStars(int)`. The vanilla game also exposes a public `Hook.AfterStarsSpent` event, which is likely a more stable integration point.

Current behavior may work today, but private method names/signatures are more likely to change across game updates.

Potential impact:

- `StarsSpent` tracking may silently stop working after an upstream update.
- Harmony patch failure could remove support for stars-spent conditions without obvious UI failure.

Relevant files:

- `mods/card_editor/CardEditorResourceCountPatches.cs`
- `Slay the spire 2 Source/src/Core/Models/CardModel.cs`
- `Slay the spire 2 Source/src/Core/Hooks/Hook.cs`

## Reported Gameplay Issues

### 6. Damage Rule Modifier - Hits All Enemies causes card soft lock when granted

**Priority:** P1  
**Area:** Damage Rule Modifier / Grant effects / card resolution  
**Status:** Reported and static-confirmed, needs in-game reproduction

Reported behavior:

When applying `Damage Rule Modifier -> Hits All Enemies` to another card through a `Grant` effect, the granted card can get stuck in the center of the screen when played. The game itself does not fully freeze, and other cards can still be played, but the affected card does not resolve and does not deal damage.

Potential impact:

- The played card never completes its resolution.
- Damage is not dealt.
- The card remains visually stuck in the center of the screen.
- The interaction may create a soft lock for that card/action while the rest of combat continues.

Static review notes:

- `SupportsGrantToCard` does not exclude `CardExtraEffectKind.HitsAllEnemies`, so the editor can grant this rule modifier to arbitrary cards.
- `TryGetRuleAdjustedTargetType` changes `AnyEnemy` and `RandomEnemy` cards to `TargetType.AllEnemies` when the rule modifier is active.
- `CardModel.get_TargetType` applies that adjusted target type globally through the vanilla classification patch.
- Vanilla `NCardPlay.TryPlayCard` calls `card.TryManualPlay(null)` for `AllEnemies` cards.
- Many vanilla single-target cards still run their original `OnPlay` implementation and immediately require `cardPlay.Target` to be non-null.

Likely root cause:

The modifier changes the targeting UI/classification, but it does not rewrite the underlying single-target card payload. A vanilla card such as `StrikeIronclad` or `Bash` receives `CardPlay.Target == null`, throws or aborts inside its own `OnPlay`, and the visual card play can remain unresolved in the center.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs` (`SupportsGrantToCard`, `TryGetRuleAdjustedTargetType`)
- `mods/card_editor/CardEditorVanillaClassificationPatches.cs`
- `mods/card_editor/CardEditorExtraEffectTargetingPatches.cs`
- `Slay the spire 2 Source/src/Core/Nodes/Combat/NCardPlay.cs`
- `Slay the spire 2 Source/src/Core/Models/Cards/StrikeIronclad.cs`
- `Slay the spire 2 Source/src/Core/Models/Cards/Bash.cs`

Suggested reproduction:

1. Create a card that grants `Damage Rule Modifier -> Hits All Enemies` to another card.
2. Use the grant effect in combat.
3. Play the modified target card.
4. Confirm whether the card remains centered and whether damage resolution begins or never starts.

### 7. Copy Debuffs appears to have no effect

**Priority:** P2  
**Area:** Debuff copying / effect execution  
**Status:** Reported and partially static-confirmed, needs in-game reproduction

Reported behavior:

The `Copy Debuffs` option appears to do nothing. It has reportedly been tested in multiple scenarios but does not seem to trigger or apply copied debuffs.

Potential impact:

- Debuffs are not copied from the expected source to the expected target.
- The UI may allow configuring an effect that never has visible runtime behavior.
- Depending on how the effect is written, it may be failing source selection, target selection, debuff filtering, or application.

Static review notes:

- `CopyDebuffs` is defined with allowed targets `OtherEnemies`, `AllEnemies`, and `RandomEnemy`; it does not allow the literal `Target` option.
- The implementation always chooses the debuff source with `ResolveSingleTarget(...)`, independent of the configured destination target.
- Manual enemy targeting is only forced when an On Play effect has `Target == CardExtraEffectTarget.Target`.
- Because the default `CopyDebuffs` target is `OtherEnemies`, a no-target card will not ask the player to pick the debuff source. `ResolveSingleTarget` then silently falls back to a random living enemy.
- In a one-enemy combat, the default `OtherEnemies` destination has no valid destination after excluding the source, so the effect no-ops.
- With the `RandomEnemy` destination, the random destination can be the same creature as the source and is then filtered out, causing another silent no-op.

Likely root cause:

The effect text says "Copy the target's debuffs", but the runtime does not reliably collect a target for this effect configuration. Depending on card target type and enemy count, it can copy from a random enemy, copy from an enemy with no debuffs, or resolve no destination at all.

Suggested reproduction:

1. Enter combat with an enemy or player that has one or more visible debuffs.
2. Use a card/effect configured with `Copy Debuffs`.
3. Confirm whether any debuff instances or stacks are applied to the intended target.
4. Repeat with both player-to-enemy and enemy-to-enemy/player cases if the UI allows those configurations.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs` (`CopyDebuffsFromTarget`, `RequiresManualEnemyTarget`, `ResolveSingleTarget`, `ResolveOtherEnemyTargets`)
- `mods/card_editor/CardEditorExtraEffectTargetingPatches.cs`

### 8. `OtherEnemies` effects on non-target cards can silently use a random excluded enemy

**Priority:** P2  
**Area:** Extra effect targeting / card editor UI  
**Status:** Static-confirmed risk, needs in-game reproduction

`OtherEnemies` depends on an excluded creature. The implementation gets that excluded creature from `ResolveSingleTarget`, but the manual-target hook only forces targeting for effects whose saved target is exactly `Target`.

Potential impact:

- Effects configured as "other enemies" can behave nondeterministically on cards with `TargetType.None`, `Self`, or other non-enemy target types.
- A card can hit/apply/status-copy to "other enemies" relative to a random enemy the player never selected.
- In single-enemy combats, these effects often no-op because the randomly or implicitly chosen source is excluded and no other enemies remain.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorExtraEffectTargetingPatches.cs`
- `mods/card_editor/NCardEditorPopup.cs`

### 9. Power-hosted amountless target effects may never get a target

**Priority:** P2  
**Area:** Relic-style/power-hosted effects / target selection  
**Status:** Static-confirmed risk, needs in-game reproduction

`CardEditorExtraEffectPower.HasOnPlayTargetEffects()` decides whether the played card should ask for a manual enemy target when a power-hosted effect needs `Target`. That check requires `e.Effect.Amount > 0`.

Amountless effects such as `Cleanse Debuffs` and `Cleanse Buffs` are valid with amount `0`, can be power-hosted, and can use `Target`. For those effects, the card may not ask for a target. Later, `RunForTrigger` sees `resolvedEffect.Target == Target`, finds `triggerPlay.Target == null`, and returns without executing.

Potential impact:

- Relic-like effects or power-hosted card effects that target an enemy can silently do nothing.
- `Cleanse Buffs` is especially exposed because its default target is `Target` and default amount is `0`.
- The direct card-effect path uses `IsValidEffectAmount`, so this bug may only appear when the same effect is hosted as a power/relic-style trigger.

Relevant files:

- `mods/card_editor/CardEditorExtraEffectPower.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorExtraEffectTargetingPatches.cs`

### 10. Relic `OnDamageTaken` can fire on fully blocked hits and miss lethal damage

**Priority:** P2  
**Area:** Relic editor / reactive combat triggers  
**Status:** Partially fixed in current working tree as of 2026-06-22; lethal path still appears current

The custom relic `OnDamageTaken` trigger is wired through the vanilla `Hook.AfterDamageReceived` hook. The patch receives a `DamageResult`, but it does not inspect whether HP was actually lost.

Current note:

- The current relic patch now checks `result.UnblockedDamage > 0`, so the fully-blocked-hit part appears fixed. The source comment still says vanilla skips this hook for lethal damage, so `OnDamageTaken` still appears unable to react to killing blows.

Potential impact:

- Relics configured as "When you take damage" can trigger when an attack is fully blocked and no HP damage is taken.
- Effects that are expected to react to lethal damage may never run, because vanilla damage handling skips `AfterDamageReceived` when the target dies.
- Relic behavior can differ from the editor label and from player expectations around "taking damage".

Static review notes:

- `Hook_AfterDamageReceived_CardEditorRelicEffects_Patch` ignores the `DamageResult result` argument and dispatches `RelicTriggerKind.OnDamageTaken` whenever the affected creature is the player.
- Vanilla `CreatureCmd.Damage` calls `Hook.AfterDamageReceived` for non-lethal damage results even when the attack was fully blocked.
- The same vanilla damage path adds lethal targets to `killedCreatures` and skips `AfterDamageReceived` for those results.
- `OnHpLost` is separately wired through `AfterCurrentHpChanged`, but that is a different trigger from the editor-facing "When you take damage" option.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicOverrides.cs`
- `Slay the spire 2 Source/src/Core/Commands/CreatureCmd.cs`

Suggested reproduction:

1. Create a custom relic effect on `OnDamageTaken`.
2. Block all incoming attack damage and confirm whether the effect still fires.
3. Repeat with lethal incoming damage and confirm whether the effect fires before death handling.

### 11. Relic `OnBlockGained` and `OnHeal` can fire for zero effective gain

**Priority:** P3  
**Area:** Relic editor / reactive combat triggers  
**Status:** Partially fixed in current working tree as of 2026-06-22; heal path still needs in-game reproduction

Some relic trigger patches react to attempted events rather than the effective amount gained.

Current note:

- The current `OnBlockGained` patch now checks `amount > 0`, so the zero-block path appears fixed. The `OnHeal` path still relies on `Hook.AfterCurrentHpChanged`'s positive `delta`; verify in-game whether that hook receives effective healing or requested healing at full HP.

Potential impact:

- `OnBlockGained` custom relic effects can fire when a block gain event resolves to `0` block.
- `OnHeal` custom relic effects can fire when healing is attempted at full HP, depending on the vanilla heal caller.
- Trigger counters, delayed effects, resource generation, or card creation can happen even though the visible block/HP value did not change.

Static review notes:

- `Hook_AfterBlockGained_CardEditorRelicEffects_Patch` dispatches `RelicTriggerKind.OnBlockGained` without checking whether the amount is greater than `0`.
- Vanilla `CreatureCmd.GainBlock` calls `Hook.AfterBlockGained` even when `modifiedAmount` is `0`; it only updates block/history when `modifiedAmount > 0`.
- `Hook_AfterCurrentHpChanged_CardEditorRelicEffects_Patch` dispatches `RelicTriggerKind.OnHeal` when the hook delta is positive.
- Vanilla `CreatureCmd.Heal` computes actual healing separately, but calls `Hook.AfterCurrentHpChanged(..., amount)` for positive requested healing, which can be larger than the effective healing and can be positive even when already at full HP.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `Slay the spire 2 Source/src/Core/Commands/CreatureCmd.cs`

Suggested reproduction:

1. Create a custom relic effect on `OnBlockGained`.
2. Cause a block gain event that resolves to `0` and confirm whether the relic fires.
3. Create a custom relic effect on `OnHeal`.
4. Attempt to heal while already at full HP and confirm whether the relic fires.

### 12. Relic `OnEnemyKilled` appears to mean any enemy death, not "you kill"

**Priority:** P3  
**Area:** Relic editor / kill/death triggers  
**Status:** Historical finding - changed in current working tree as of 2026-06-22

The editor label describes this trigger as "When you kill an enemy", but the runtime patch only checks that a non-player creature died. It does not check the damage source, owner, or player who caused the death.

Current note:

- The current code now records a lethal dealer from `Hook.AfterDamageGiven` and dispatches `OnEnemyKilled` only for that dealer's player, so the "all players / any enemy death" issue appears fixed for direct damage kills. See the later `OnEnemyKilled` poison/status-kill entry for a likely regression from this narrower attribution.

Potential impact:

- A custom relic can fire when an enemy dies from poison, self-damage, scripted damage, another enemy, or another player.
- In multiplayer or multi-player-like combat state, every player with a matching custom relic can receive the trigger when any enemy dies.
- Triggered rewards or effects can duplicate across players or fire for deaths the owner did not cause.

Static review notes:

- `Hook_AfterDeath_CardEditorRelicEffects_Patch` dispatches `RelicTriggerKind.OnEnemyKilled` for all players whenever a dead creature is not a player.
- The dispatch path uses `DispatchAll`, so it is not scoped to a specific player or killer.
- The death hook does not provide enough filtering in the current patch to prove the relic owner caused the kill.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicOverrides.cs`

Suggested reproduction:

1. Create a custom relic effect on `OnEnemyKilled`.
2. Kill an enemy with poison or another non-direct player damage source.
3. Confirm whether the relic fires.
4. If possible, test with multiple player creatures and confirm whether all matching player relics fire.

### 13. Imported or legacy relic `OnPickup` trigger can never dispatch

**Priority:** P3  
**Area:** Relic editor / imported relic definitions  
**Status:** Historical finding - changed in current working tree as of 2026-06-22

`RelicTriggerKind.OnPickup` still exists in the trigger enum and description text, but it is not part of the active relic trigger list and no runtime patch appears to dispatch it.

Current note:

- The current override loader now normalizes imported `OnPickup` effect rows and trigger groups to `OnCombatStart`, so they no longer remain as never-dispatched `OnPickup` rows. That still changes legacy/imported semantics; see the later `TriggerEveryN` normalization entry for a related current bug.

Potential impact:

- Imported or legacy relic JSON using `OnPickup` can save/load but never execute the configured effect.
- The description can still say "When obtained", making the relic look configured even though no runtime event calls it.
- Opening the relic in the current editor may normalize the unsupported trigger to another active trigger, changing imported data semantics.

Static review notes:

- `RelicTriggerKind.OnPickup` is still present and has description text.
- `NRelicEditorPopup.ActiveRelicTriggers` does not include `OnPickup`.
- Repo-wide search did not find a dispatch path for `RelicTriggerKind.OnPickup`.
- `NRelicEditorPopup.AddEffectGroup` normalizes unsupported trigger values to the first active trigger when displayed.

Relevant files:

- `mods/card_editor/CardEditorRelicOverrides.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`

### 14. Imported or legacy relic `AsPower` rows can execute immediately instead of installing a power

**Priority:** P3  
**Area:** Relic editor / imported relic definitions / effect execution  
**Status:** Static-confirmed edge case

The current embedded relic editor UI forces `AsPower = false`, which prevents newly-authored relic effects from using card-style power hosting. However, the shared preset DTO can still deserialize `AsPower = true` from imported or legacy data.

Potential impact:

- Imported relic effects marked `AsPower` may run their payload immediately when the relic trigger fires instead of installing a persistent `CardEditorExtraEffectPower`.
- Effects authored or hand-edited with card-style semantics can behave differently once attached to a relic.
- Debugging can be confusing because the embedded UI hides the `AsPower` control and forces it off only when the effect is read through the popup.

Static review notes:

- `NCardEditorPopup.ReadEmbeddedEffects()` forces `AsPower = false` for embedded effect hosts.
- The shared DTO still includes `AsPower` and relic overrides load effects through the normal `TryToEffect` path.
- `CardEditorRelicEffects.RunOneEffect` passes the relic effect directly into `CardEditorExtraEffects.ExecuteEffect`.
- The normal card path has explicit handling that separates power effects and installs them with `CardEditorExtraEffectPower.AddPowerEffects`; the relic path does not mirror that handling.

Relevant files:

- `mods/card_editor/NCardEditorPopup.cs`
- `mods/card_editor/CardEditorPresetStore.cs`
- `mods/card_editor/CardEditorRelicOverrides.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorExtraEffectPower.cs`

### 15. Relic `OnDamageDealt` can fire for blocked or zero-damage hits

**Priority:** P2  
**Area:** Relic editor / reactive combat triggers  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The custom relic `OnDamageDealt` trigger is wired through `Hook.AfterDamageGiven`, but the patch does not check `DamageResult.UnblockedDamage`.

Current note:

- The current `AfterDamageGiven` relic patch requires `results.UnblockedDamage > 0` before dispatching `OnDamageDealt`, matching the count-event path.

Potential impact:

- Relics configured as "When you deal damage" can trigger when an attack is fully blocked.
- Effects can trigger from damage attempts that did not lower enemy HP.
- Trigger behavior differs from the card/power count-event path, which already filters damage dealt to `UnblockedDamage > 0`.

Static review notes:

- `Hook_AfterDamageGiven_CardEditorRelicEffects_Patch` dispatches `RelicTriggerKind.OnDamageDealt` for the dealer's player without inspecting the `DamageResult`.
- Vanilla `CreatureCmd.Damage` calls `Hook.AfterDamageGiven` for every damage result, including fully blocked results where `UnblockedDamage == 0`.
- `CardEditorResourceCountPatches` handles the same vanilla hook for count events and explicitly requires `results.UnblockedDamage > 0` before recording `DamageDealt`.
- Several vanilla damage-given effects also guard on `result.UnblockedDamage > 0`, which suggests the raw hook itself means "damage event resolved", not necessarily "HP damage was dealt".

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/CardEditorResourceCountPatches.cs`
- `Slay the spire 2 Source/src/Core/Commands/CreatureCmd.cs`
- `Slay the spire 2 Source/src/Core/Entities/Creatures/DamageResult.cs`

Suggested reproduction:

1. Create a custom relic effect on `OnDamageDealt`.
2. Attack an enemy with enough block to fully absorb the hit.
3. Confirm whether the relic still fires even though enemy HP did not change.

### 16. Relic `OnTurnEnd` ignores the vanilla participant list

**Priority:** P3  
**Area:** Relic editor / turn boundary triggers / multiplayer or extra-turn edge cases  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The vanilla `Hook.AfterTurnEnd` call includes the creatures that actually ended the turn, but the custom relic patch only reads the side and then dispatches `OnTurnEnd` to all players.

Current note:

- The current `AfterTurnEnd` relic patch accepts the `participants` argument and wraps only participant player creatures for player-side `OnTurnEnd`.

Potential impact:

- During extra-turn flows where only a subset of players are ending the turn, all players' custom `OnTurnEnd` relic effects can fire.
- Multiplayer or co-op combat can run owner-specific end-of-turn relic effects for players that were not part of the current end-turn participant set.
- Triggered card generation, resource gain, delayed effects, or counters can duplicate across players.

Static review notes:

- Vanilla `CombatManager.EndPlayerTurnPhaseTwoInternal` passes `playersEndingTurn.Select(p => p.Creature)` into `Hook.AfterTurnEnd`.
- `playersEndingTurn` can be a subset when `_playersTakingExtraTurn.Count > 0`.
- `Hook_AfterTurnEnd_CardEditorRelicEffects_Patch` ignores the participants argument and calls `Wrap`, which dispatches to every player in combat for `RelicTriggerKind.OnTurnEnd`.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `Slay the spire 2 Source/src/Core/Combat/CombatManager.cs`
- `Slay the spire 2 Source/src/Core/Hooks/Hook.cs`

### 17. Relic `Fire: Every N` setting is dropped before save and runtime

**Priority:** P1  
**Area:** Relic editor / trigger frequency / persistence  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The relic editor now stores a per-trigger `TriggerEveryN` dictionary and the runtime checks it before firing relic effects, but `CardEditorRelicOverrides.Clone` does not copy that dictionary. The central set/save/export paths all clone overrides, so the UI value can be lost before it reaches disk or runtime.

Current note:

- The current `CardEditorRelicOverrides.Clone()` now copies `TriggerEveryN` into a new dictionary, and `dotnet build mods\card_editor\card_editor.csproj -v:minimal -nologo` passes. This entry is kept as a historical finding from the prior pass; still worth verifying in-game save/reopen behavior.

Potential impact:

- Setting a relic trigger group to `Fire: Every 2`, `Every 3`, etc. can silently revert to "every time".
- Imported JSON with `TriggerEveryN` can load, but then be dropped when the override is cloned or saved again.
- Runtime code in `CardEditorRelicEffects.ShouldFireRelicTriggerThisTime` may almost always see `overrideData.TriggerEveryN == null`, so the intended Kunai-style gate never applies.

Static review notes:

- `RelicOverride.TriggerEveryN` exists and `RelicOverride.IsEmpty()` treats it as meaningful state.
- `RelicOverrideDto.ToOverride()` parses `TriggerEveryN`; `RelicOverrideDto.FromOverride()` serializes it.
- `NRelicEditorPopup.ApplyChanges()` builds `overrideData.TriggerEveryN` from each group's `EveryNSelect`.
- `CardEditorRelicEffects.ShouldFireRelicTriggerThisTime()` reads `overrideData.TriggerEveryN` and gates the trigger.
- `CardEditorRelicOverrides.Set()` stores `_overrides[relicId] = Clone(overrideData)`, but `Clone()` copies `DynamicVarBaseValues`, custom text, pools, fixed sources, `ExtraEffects`, and `EffectTriggers` only. It does not copy `TriggerEveryN`.

Relevant files:

- `mods/card_editor/CardEditorRelicOverrides.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`

Suggested reproduction:

1. Open a relic in the relic editor.
2. Add an effect group and set `Fire:` to `Every 3`.
3. Apply/save, then reopen the relic or inspect the saved override JSON.
4. Confirm whether the group is back to "Every time" or the saved override lacks `TriggerEveryN`.

### 18. Relic generated descriptions cannot mention `Fire: Every N`

**Priority:** P3  
**Area:** Relic editor / generated text  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The generated relic description builder only receives effect rows grouped by trigger. It does not receive the trigger group's `TriggerEveryN` value, so it cannot describe "every 2nd", "every 3rd", etc. Even after the persistence bug above is fixed, generated text can still say "When you play a card: ..." for an effect that actually fires only every Nth time.

Current note:

- The current live preview passes `CollectTriggerEveryN()` into `BuildEffectsDescriptionText(...)`, and the in-game postfix passes `overrideData.TriggerEveryN`; generated text now adds an `(every Nth time)` qualifier.

Potential impact:

- Relics with gated trigger groups can display text that overpromises how often the effect fires.
- Live preview text in the relic editor can mismatch the `Fire:` dropdown.
- In-game relic descriptions can be misleading if custom text is not enabled.

Static review notes:

- `NRelicEditorPopup.RefreshPreviewFromUi()` calls `CardEditorRelicOverrides.BuildEffectsDescriptionText(preview, CollectEffectGroupEntries())`.
- `CollectEffectGroupEntries()` returns `RelicEffectEntry` rows with trigger and effect only.
- `CardEditorRelicOverrides.BuildEffectsDescriptionText(RelicModel?, IReadOnlyList<RelicEffectEntry>?)` has no `TriggerEveryN` input and builds each line from `GetTriggerDescriptionPhrase(trigger)`.
- `TryAppendEffectsDescription()` uses the same builder for in-game dynamic descriptions.

Relevant files:

- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicOverrides.cs`

Suggested reproduction:

1. Create a relic effect group with `Fire: Every 3`.
2. Leave custom relic text disabled so generated text is used.
3. Check the live preview or in-game relic description.
4. Confirm whether the text says the effect fires every time instead of every 3rd trigger.

### 19. `Copy Buffs` has the same source-target mismatch as `Copy Debuffs`

**Priority:** P2  
**Area:** Card extra effects / targeting  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

`Copy Buffs / Stats` uses the effect target field for destinations, but it chooses the buff source from `ResolveSingleTarget(combatState, ownerCreature, cardPlay)`. The effect definition does not allow `Target` as a selectable target, so the editor gives users destination choices without a clear way to choose the source.

Potential impact:

- On normal targeted cards, the source is the card's target while the effect target means destination.
- On no-target cards or granted effects, the source can fall through to a random enemy instead of a chosen source.
- With default `AllAllies`, the effect can copy buffs from the played card's target to allies, but if there is no real target it can no-op or copy from an unintended enemy.
- The generated text says "Copy the target's buffs to all allies/self/etc.", but the UI field being edited is not actually the source target.

Static review notes:

- `CopyBuffs` is defined with allowed targets `AllAllies`, `Self`, `OtherEnemies`, and `AllEnemies`; `Target` is not allowed.
- `CopyBuffsFromTarget()` sets `Creature? source = ResolveSingleTarget(combatState, ownerCreature, cardPlay)`.
- The same method then resolves destinations from `ResolveTargets(..., effect.Target)` and excludes the source.
- `RequiresManualEnemyTarget()` only forces manual targeting for non-power `OnPlay` effects whose target is exactly `CardExtraEffectTarget.Target`; `CopyBuffs` can never satisfy that condition from its normal UI definition.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/NCardEditorPopup.cs`

Suggested reproduction:

1. Put `Copy Buffs / Stats` on a card that does not normally target a creature.
2. Leave the destination as `All Allies` or `Self`.
3. Play it in combat where enemies have visible buffs.
4. Confirm whether the source is random, missing, or otherwise not controllable from the UI.

### 20. `Copy Buffs` can clone hidden editor infrastructure powers

**Priority:** P2  
**Area:** Card extra effects / power copying / hidden behavior powers  
**Status:** Static-confirmed risk

`CopyBuffsFromTarget()` clones every power whose `TypeForCurrentAmount` is `PowerType.Buff`. That includes hidden card-editor infrastructure powers because several of those are implemented as invisible buffs. Other cleanup code explicitly avoids sweeping these internal powers, but `Copy Buffs` does not have the same exclusion.

Current note:

- The current `CopyBuffsFromTarget()` filters out powers from the card-editor assembly except `CardEditorCustomStatusPower`, mirroring the cleanse exclusion. This entry is kept as a historical finding; re-test with visible custom statuses and vanilla buffs to make sure the new exclusion is not too broad.

Potential impact:

- Copying buffs from a creature with editor-managed behavior powers can duplicate hidden effect runners onto another creature.
- Persistent extra-effect powers, duration trackers, temp-stat trackers, countdown powers, or persistence trackers can be copied as if they were normal buffs.
- This can create duplicated triggers, stale source-card behavior, unexpected delayed effects, or hidden powers on enemies/allies.

Static review notes:

- `CopyBuffsFromTarget()` filters only `power.TypeForCurrentAmount == PowerType.Buff`, clones each matching power, and applies it to destinations.
- `CardEditorExtraEffectPower`, `CardEditorPowerPersistenceTrackerPower`, `CardEditorTempStatTrackerPower<T>`, and `CardEditorPowerDurationTrackerPower` are `PowerType.Buff` and hidden/internal.
- `CleanseRemainingPowersByType()` already documents that the mod's invisible infrastructure powers should not be swept, and excludes mod-assembly powers except `CardEditorCustomStatusPower`.
- `CopyBuffsFromTarget()` does not reuse that exclusion or otherwise restrict to visible/vanilla/custom-status buffs.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorExtraEffectPower.cs`
- `mods/card_editor/CardEditorPowerPersistenceTrackerPower.cs`
- `mods/card_editor/CardEditorTempStatTrackerPowers.cs`
- `mods/card_editor/CardEditorCountdownEffectPower.cs`

Suggested reproduction:

1. Give the player or an enemy a persistent/as-power editor effect that installs `CardEditorExtraEffectPower`.
2. Use `Copy Buffs / Stats` with that creature as the source.
3. Inspect the destination's powers or behavior after copying.
4. Confirm whether hidden editor powers are copied along with normal buffs.

### 21. `Each Target` value source double-consumes use limits

**Priority:** P2  
**Area:** Card extra effects / value source actor / use limits  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The `Each Target (its own value)` path fans an effect out by recursively calling `ExecuteEffect()` once per resolved target. However, `ExecuteEffect()` consumes the effect use limit before the fan-out check, and each recursive call consumes the same limit again.

Current note:

- The current `ExecuteEffect()` skips use-limit consumption while `_eachTargetCurrent != null`, so the specific double-consumption issue appears fixed. This entry is kept as a historical finding; see the later `[ThreadStatic]` async-state entry for a remaining risk in the same area.

Potential impact:

- A row with `Each Target` and `Use Limit = 1` can consume its only use in the outer call, then execute zero targets.
- Higher limits are consumed once for the fan-out wrapper plus once per target, so fewer targets run than the limit suggests.
- Per-combat/per-turn counters become off by one or worse for every `Each Target` row.

Static review notes:

- `ExecuteEffect()` calls `CardEditorAutoPlayLoopGuard.TryConsumeEffectUseLimit(...)` before the `ValueSourceActor == EachTarget` fan-out branch.
- The fan-out branch loops resolved targets and calls `await ExecuteEffect(... effect ...)` recursively.
- The recursive call skips the fan-out because `_eachTargetCurrent` is set, but it still reaches the same use-limit consumption code.
- The guard itself increments the source-effect counter before execution once the limit is available.
- Description text has a smaller related mismatch: `ResolveValueSourceReferenceText()` does not handle `EachTarget`, so card text can fall through to "your Strength/Block/etc." even though runtime reads each target's value.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorAutoPlayLoopGuard.cs`

Suggested reproduction:

1. Create an effect that targets all enemies and uses `Each Target (its own value)` as the amount source.
2. Add `Use Limit = 1`.
3. Play it against multiple enemies.
4. Confirm whether no enemy, or only fewer enemies than expected, receive the effect.

### 22. Power `Marked Target` watcher ignores manual targets

**Priority:** P2  
**Area:** Card extra effects / persistent powers / target memory  
**Status:** Historical finding - appears fixed in current working tree as of 2026-06-22

The new persistent power actor filter `Marked Target` stores the watched creature with `CaptureWatchedTarget()`, but that helper only reads `sourcePlay.Target`. The editor's manual target patch deliberately stores targets separately for cards that normally have no creature target, and then clears the actual play target to let the base game play the no-target card.

Current note:

- The current `CaptureWatchedTarget()` falls back to `CardEditorExtraEffects.TryGetManualTarget(...)`, and `RequiresManualEnemyTarget()` now forces a pick for `MarkedTarget` power effects on no-target cards.

Potential impact:

- A no-target card that installs a persistent power watching `Marked Target` can create an entry with `WatchedTarget == null`.
- Later event checks require a non-null watched target, so the persistent effect never fires.
- Users can be forced to choose a manual target and still get no runtime behavior from "Marked Target".

Static review notes:

- `CardPlay_TryPlayCard_ExtraEffectsTarget_Patch` calls `CardEditorExtraEffects.SetManualTarget(card, target)` and then sets `target = null` before vanilla play continues.
- `ResolveSingleTarget()` consults `TryGetManualTarget(...)`, so normal immediate effects can use that manual target.
- `CardEditorExtraEffectPower.CaptureWatchedTarget()` returns only `sourcePlay.Target` when `PowerTriggerFrom` is `MarkedTarget`.
- `WatchesEventActor()` requires `entry.WatchedTarget != null` and reference equality with the later event actor.

Relevant files:

- `mods/card_editor/CardEditorExtraEffectTargetingPatches.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorExtraEffectPower.cs`

Suggested reproduction:

1. Create a no-target card that installs an `As Power` effect with trigger actor `Marked Target`.
2. Play it and select an enemy through the manual target prompt.
3. Trigger the watched event from that enemy.
4. Confirm whether the persistent power never fires because the watched target was not stored.

### 23. `DrawAndCheck` branch currently breaks the card editor build

**Priority:** P0  
**Area:** Card extra effects / build health  
**Status:** Historical finding - fixed in current working tree as of 2026-06-22

The current `mods/card_editor/card_editor.csproj` build fails because the new `DrawAndCheck` execution branch references `branchDepth` from inside `ExecuteEffectCore()`. That variable only exists in the outer `ExecuteEffect()` method and is not in scope inside `ExecuteEffectCore()`.

Current note:

- The current `DrawAndCheck` branch uses `_drawAndCheckBranchDepth` instead of the out-of-scope `branchDepth`, and `dotnet build mods\card_editor\card_editor.csproj -v:minimal -nologo` now succeeds with 0 warnings and 0 errors.

Potential impact:

- The card editor mod cannot compile in the current working tree.
- Any packaging/build step that depends on `mods/card_editor/card_editor.csproj` is blocked.
- The new `DrawAndCheck` effect cannot be tested until the compile error is resolved.

Static review notes:

- `ExecuteEffect()` accepts `int branchDepth` and uses it for recursive branch execution.
- `ExecuteEffectCore()` accepts only `(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, CardExtraEffect effect, int triggerEventAmount)`.
- The `DrawAndCheck` case inside `ExecuteEffectCore()` calls `ExecuteEffect(... drawBranch, branchDepth + 1, triggerEventAmount)`, but `branchDepth` is not defined in that scope.
- `dotnet build mods\card_editor\card_editor.csproj -v:minimal -nologo` fails with `CS0103: The name 'branchDepth' does not exist in the current context`.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`

Suggested reproduction:

1. Run `dotnet build mods\card_editor\card_editor.csproj -v:minimal -nologo`.
2. Confirm the build fails at `CardEditorExtraEffects.cs` on the `DrawAndCheck` branch call to `branchDepth + 1`.

### 24. `DrawAndCheck` branch settings are saved through the wrong branch condition UI

**Priority:** P2  
**Area:** Card extra effects / branch UI / draw effects  
**Status:** Static-confirmed bug

`Draw, Then Branch If Drawn Type` now compiles, but the runtime and editor do not agree on how its branch should be configured. Runtime deliberately skips the normal branch-condition check for `DrawAndCheck` and branches only when a drawn card matches `effect.BranchCountCardType`. The save UI, however, only preserves `BranchCountCardType` when the user selects the generic `History Count` branch condition, and it only saves any branch effect at all when a generic branch condition is considered usable.

Potential impact:

- A user can enable a `DrawAndCheck` branch, choose a source-card branch effect, leave the default target-check condition empty, and have the branch silently discarded on save.
- If the user chooses `Target Check` or `Attack Result` to make the branch look valid, runtime ignores that condition for `DrawAndCheck`.
- To choose the drawn card type, the user must select the unrelated `History Count` branch condition because that is the only path that saves `BranchCountCardType`.
- The UI exposes extra history/card filters like pile, rarity, card id, tags, and damage history, but `DrawAndCheck` runtime only checks `BranchCountCardType`, so those settings can be silently ignored.

Static review notes:

- `ExecuteEffect()` excludes `DrawAndCheck` from the generic `DoesBranchConditionPass(...)` branch condition path.
- The `DrawAndCheck` case in `ExecuteEffectCore()` checks only `MatchesGeneratedCardType(drawnCard, effect.BranchCountCardType)` before running `GetUsableBranchEffect(effect)`.
- `NCardEditorPopup` saves `BranchCountCardType` only when `branchConditionType == HistoryCount`.
- `NCardEditorPopup` discards `branchEffect` unless `hasUsableBranchCondition` is true, even though `DrawAndCheck` runtime does not use that condition.
- `UpdateExtraEffectBranchRows()` only reveals branch count/card-type controls for `HistoryCount`, not specifically for `DrawAndCheck`.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/NCardEditorPopup.cs`

Suggested reproduction:

1. Add a `Draw, Then Branch If Drawn Type` effect.
2. Enable `Branch`, select a source card for the branch, and leave the branch condition as the default target-check/no-condition setup.
3. Apply/reopen the card and confirm whether the branch was cleared or does not run.
4. Repeat with branch type `History Count`, configure rarity/tag/card-id filters, and confirm runtime still only checks the generated card type.

### 25. `Current Turn Number` count source uses combat round instead of player turn number

**Priority:** P3  
**Area:** Card extra effects / count source / co-op and extra turns  
**Status:** Static-confirmed risk

The new `Current Turn Number` count event returns `combatState.RoundNumber`. Elsewhere in the same mod, turn-based scheduling explicitly avoids `RoundNumber` and uses `player.PlayerCombatState.TurnNumber` because vanilla increments the player turn number for co-op extra turns. That means a card effect labelled as current turn number may actually scale from the combat round counter and can be wrong during extra-turn or per-player turn flows.

Potential impact:

- `Current Turn Number` scaling or branch conditions may be off by one or use the wrong counter semantics.
- Co-op extra turns can fail to advance this count even though the player is taking another turn.
- Cards that should get stronger or branch on each player turn can behave as if only full combat rounds matter.

Static review notes:

- `ResolveCountMultiplier()` returns `combatState?.RoundNumber ?? 0` for `CardExtraEffectCountEvent.CurrentTurnNumber`.
- The same method already has access to `owner` for `CurrentStars`, `CurrentEnergy`, `CurrentOrbSlots`, and Osty values.
- `CardEditorCreatedCardsCostPatches.PendingTurnDiscount` comments state that per-player `TurnNumber` is used, not `RoundNumber`, because vanilla increments it for co-op extra turns.
- The UI label and generated text say "Current Turn Number" and "for each turn this combat", which reads like player-turn semantics rather than combat-round semantics.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorCreatedCardsCostPatches.cs`

Suggested reproduction:

1. Create an effect whose amount or branch condition uses `Current Turn Number`.
2. Test it in a combat flow with an extra player turn or co-op turn sequencing.
3. Compare the value against `player.PlayerCombatState.TurnNumber` and `combatState.RoundNumber`.
4. Confirm whether the effect tracks combat rounds instead of the acting player's turns.

### 26. `EachTarget` and `DrawAndCheck` hold execution context in `[ThreadStatic]` across awaits

**Priority:** P3  
**Area:** Card extra effects / async execution context  
**Status:** Static-confirmed async-state risk

The new `Each Target (its own value)` fan-out and the fixed `DrawAndCheck` branch-depth cap both store execution context in `[ThreadStatic]` fields while awaiting nested effect execution. That is risky in async code: if an awaited operation resumes on a different thread, the continuation can lose the context or leave stale context behind on the original thread. Most surrounding ambient execution context in this mod uses `AsyncLocal`, and one existing `ThreadStatic` guard explicitly warns to dispose before awaiting.

Potential impact:

- `EachTarget` inner execution can lose `_eachTargetCurrent` after an await, causing amount source and target resolution to fall back to normal behavior.
- A stale `_eachTargetCurrent` left on the original thread can affect later unrelated target resolution if control returns there unexpectedly.
- `DrawAndCheck` branch-depth limiting can undercount, overcount, or leave a stale nonzero depth if an awaited branch resumes on another thread.
- These bugs may be intermittent and depend on the awaited command path, making them hard to reproduce consistently.

Static review notes:

- `ExecuteEffect()` sets `_eachTargetCurrent = eachTarget`, then awaits `ExecuteEffect(...)`, then clears the field in `finally`.
- `DrawAndCheck` increments `_drawAndCheckBranchDepth`, then awaits nested `ExecuteEffect(...)`, then decrements the field in `finally`.
- Both fields are declared `[System.ThreadStatic]`.
- Existing ambient contexts such as `CardEditorEffectExecutionAmountContext`, `CardEditorEffectSourceContext`, `CardEditorHookModelContext`, and `CardEditorPowerExecutionHostContext` use `AsyncLocal`.
- `CardEditorReflectiveOnPlayGuard` documents that its `ThreadStatic` flag is disposed before awaiting because holding `ThreadStatic` state across an await can linger on whichever thread resumes first.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorOverrideOnPlayPatches.cs`
- `mods/card_editor/CardEditorEffectExecutionAmountContext.cs`
- `mods/card_editor/CardEditorPowerExecutionHostContext.cs`

Suggested reproduction:

1. Create an `Each Target (its own value)` effect whose per-target execution performs an awaited command such as draw, delayed branch, or source-card execution.
2. Run it in a scenario that can resume async work after the awaited command.
3. Inspect whether all per-target executions still resolve the intended target and amount.
4. For `DrawAndCheck`, chain nested draw-check branches and verify that the recursion cap behaves consistently across repeated plays.

### 27. Relic `Fire: Every N` counters are shared by relic id and trigger, not by owner

**Priority:** P2  
**Area:** Relic editor / trigger frequency / multiplayer or duplicate relics  
**Status:** Static-confirmed bug

The `Fire: Every N` gate stores one counter per `(combatState, relicId, trigger)`. It does not include the owning player, the actual relic instance, or the relic list slot. In co-op or any state where two players can own the same relic id, both relics advance the same counter.

Potential impact:

- If two players own the same custom relic with `Fire: Every 2`, player A's first trigger can increment the counter to 1 and do nothing, then player B's first trigger increments it to 2 and fires.
- One player's actions can make another player's relic fire or skip.
- If duplicate copies of the same relic id are ever possible, multiple copies can share one cooldown/counter instead of each copy tracking its own Nth trigger.

Static review notes:

- `RunRelicTrigger()` calls `ShouldFireRelicTriggerThisTime(combatState, relic.CanonicalInstance?.Id ?? relic.Id, trigger, overrideData)` once per owned relic before running matching effects.
- `ShouldFireRelicTriggerThisTime()` stores counters in `_relicTriggerCounts` under `string key = relicId.ToString() + ":" + (int)trigger`.
- The key does not include `player`, `ownerCreature`, or the actual `RelicModel` instance being iterated.
- The dispatch paths are now player-scoped for many triggers, so this shared counter is one of the remaining multiplayer/copy isolation risks.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`

Suggested reproduction:

1. Give two players the same relic id with a custom `OnCardPlayed` effect group set to `Fire: Every 2`.
2. Have player A play one card.
3. Have player B play one card.
4. Confirm whether player B's relic fires on their first card because player A advanced the shared counter.

### 28. Relic `Fire: Every N` values above 10 are silently truncated by the editor

**Priority:** P3  
**Area:** Relic editor / trigger frequency / imported data  
**Status:** Static-confirmed bug

The runtime and JSON parser accept any `TriggerEveryN` value greater than 1, but the relic editor dropdown only offers values from `Every time` through `Every 10`. When an override with a larger value is opened, the UI clamps it to the `Every 10` option and saving writes `10` back.

Potential impact:

- Hand-edited or imported relic JSON with `TriggerEveryN = 20`, `50`, etc. can be silently reduced to `10` after opening and applying the relic editor.
- Runtime behavior and generated descriptions can change just by viewing/editing unrelated relic fields.
- Users have no visible warning that a larger imported gate was clipped.

Static review notes:

- `ParseTriggerEveryN()` accepts every parsed enum key with `kv.Value > 1`; it does not cap values at 10.
- `ShouldFireRelicTriggerThisTime()` uses the configured value directly when it is greater than 1.
- `AddEffectGroup()` only adds dropdown items `Every time` and `Every 2` through `Every 10`.
- `AddEffectGroup()` selects `Math.Clamp(everyN - 1, 0, 9)`.
- `CollectTriggerEveryN()` persists `everyNGroup.EveryNSelect.Selected + 1`, so any loaded value above 10 becomes 10 on save.

Relevant files:

- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorRelicOverrides.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`

Suggested reproduction:

1. Add or import a relic override with `TriggerEveryN` for a trigger set to `20`.
2. Open the relic in the relic editor.
3. Apply/save without intending to change the trigger frequency.
4. Inspect the saved override and confirm whether the value is now `10`.

### 29. Relic `OnEnemyKilled` can miss poison, status, or scripted player kills

**Priority:** P2  
**Area:** Relic editor / kill attribution / reactive combat triggers  
**Status:** Static-confirmed regression risk

The current `OnEnemyKilled` fix scopes the trigger to the player whose lethal hit was captured in `Hook.AfterDamageGiven`. That fixes the earlier "any enemy death" behavior for direct damage, but it also means deaths that do not pass through `AfterDamageGiven` with a lethal dealer are treated as unattributed and do not fire the relic trigger.

Potential impact:

- Enemies killed by poison, doom, constrict, scripted HP loss, or other non-attack/status damage can skip custom relic effects labelled "When you kill an enemy".
- Player-authored damage-over-time cards can feel inconsistent: the card applied the status, but the relic does not see the later kill.
- This can break relics that grant rewards, energy, cards, or counters on enemy death.

Static review notes:

- `RecordLethalDealer()` is called only from the `Hook.AfterDamageGiven` relic patch when `results.WasTargetKilled`.
- `Hook_AfterDeath_CardEditorRelicEffects_Patch` calls `ConsumeLethalDealer(creature)` and returns without dispatch if no stored killer is found.
- The source comment explicitly says unattributed deaths, including poison/scripted deaths, no longer fire.
- The editor label remains `When you kill an enemy`, which users may reasonably expect to include kills caused by player-applied statuses.

Relevant files:

- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`

Suggested reproduction:

1. Create a custom relic effect on `OnEnemyKilled`.
2. Apply poison, doom, constrict, or another delayed/status kill source to an enemy.
3. Let the status kill the enemy without a direct lethal hit from `AfterDamageGiven`.
4. Confirm whether the relic trigger fails to fire.

### 30. Legacy trigger normalization can orphan `TriggerEveryN` settings

**Priority:** P3  
**Area:** Relic editor / imported data / trigger normalization  
**Status:** Static-confirmed edge-case bug

Imported `OnPickup` relic effects are now normalized to `OnCombatStart`, and imported trigger lists also exclude `OnPickup`. However, `ParseTriggerEveryN()` still accepts any defined `RelicTriggerKind`, including `OnPickup`, and keeps the key unchanged. That can leave the effect under one trigger and its frequency gate under another.

Potential impact:

- A legacy/imported `OnPickup` effect with `TriggerEveryN = 3` can become an `OnCombatStart` effect that fires every combat instead of every 3rd combat start.
- The generated description and runtime gate look for the normalized trigger and miss the orphaned `OnPickup` frequency value.
- Opening/saving imported data can silently change both the trigger and its gating semantics.

Static review notes:

- `ParseEffectEntries()` normalizes parsed `OnPickup` effect rows to `RelicTriggerKind.OnCombatStart`.
- `ParseTriggers()` excludes `OnPickup`.
- `ParseTriggerEveryN()` accepts any defined `RelicTriggerKind` with value greater than 1 and does not exclude or normalize `OnPickup`.
- `BuildEffectsDescriptionText()` and `ShouldFireRelicTriggerThisTime()` look up the every-N value by the effect/runtime trigger, so `OnCombatStart` will not see a stored `OnPickup` key.

Relevant files:

- `mods/card_editor/CardEditorRelicOverrides.cs`
- `mods/card_editor/CardEditorRelicEffects.cs`
- `mods/card_editor/NRelicEditorPopup.cs`

Suggested reproduction:

1. Import or hand-edit a relic override with an `ExtraEffects` row using trigger `OnPickup` and a `TriggerEveryN` entry for `OnPickup`.
2. Load the override.
3. Confirm the effect row becomes `OnCombatStart`.
4. Confirm the every-N gate is not applied to `OnCombatStart`.

### 31. `Choose One` option upgrade mode is lost on save or preset round-trip

**Priority:** P2  
**Area:** Card editor / Choose One effect source / preset and created-card serialization  
**Status:** Static-confirmed data-loss bug

Each `Choose One` option has its own upgrade mode (`Match Upgrade`, `Base`, `Upgraded`) and the runtime uses that mode when building exact-card and matching-card options. However, the DTO used for presets/created cards does not serialize that per-option setting, so it silently resets to the default `MatchSource` after save/load/export/import.

Potential impact:

- A card can preview and run correctly during the current editor session, then behave differently after reload.
- Choosing `Base` or `Upgraded` for one specific option is lost even though the top-level effect upgrade mode is preserved.
- Created cards, normal card overrides, presets, and keyword/status behavior effects that route through `CardExtraEffectDto` are affected.

Static review notes:

- `CardExtraEffectChooseOneOption` defines `UpgradeMode` and defaults it to `MatchSource`.
- The UI creates a per-option upgrade dropdown and writes back into `option.UpgradeMode`.
- `CloneChooseOneOption()` preserves `UpgradeMode`, so in-memory preview/apply flows keep the value.
- Runtime execution uses `option.UpgradeMode` in both query and exact-card Choose One paths.
- `ChooseOneOptionDto` serializes mode, full text, card id, and query settings, but has no `UpgradeMode` field.
- `ChooseOneOptionDto.FromOption()` does not write `option.UpgradeMode`, and `ToOption()` never restores it.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorPresetStore.cs`
- `mods/card_editor/NCardEditorPopup.cs`

Suggested reproduction:

1. Create a `Choose One Effect Source` effect with one option set to `Base` or `Upgraded`.
2. Save/export the preset or created card.
3. Reload/import it.
4. Confirm the option has reset to `Match Upgrade` and generated/borrowed option cards no longer use the selected upgrade state.

### 32. Persistent card counters collide across multiplayer players

**Priority:** P2  
**Area:** Run-progress quests / delayed pile counters / multiplayer persistence  
**Status:** Static-confirmed multiplayer bug

Persistent card counters are stored by run key and a card key built from card id, deck index, and same-id ordinal. `TryFindDeckOrdinal()` searches every player's deck, but the final card key does not include the player identity. In multiplayer, two players with the same card id in the same deck slot/copy ordinal can therefore share the same persistent counter entry.

Potential impact:

- Player A can advance, complete, or clear Player B's run-progress quest if both quest cards resolve to the same `card.Id|deck:N|copy:M` key.
- Starter cards and duplicated custom quest cards are especially likely to collide because multiple players can have identical ids at identical deck positions.
- Delayed pile actions using `DelayedPileCounterScope.Run` can also share counters across players.
- Quest completion cleanup can clear the shared counter, causing another player's visible progress to jump backwards or complete unexpectedly.

Static review notes:

- Persistent counters are saved under `run.Cards[cardKey].Counters[counterKey]`.
- `TryBuildCardInstanceKey()` builds keys as `${card.Id}|deck:${deckIndex}|copy:${sameIdOrdinal}`.
- `TryFindDeckOrdinal()` loops `runState.Players` and identifies the matching deck card by reference, but returns only deck index and same-id ordinal.
- No player net id, player index, character id, or owner id is included in the card key.
- Run-progress quests use persistent counters through `QuestCountersArePersistent = true`.
- Run-scoped delayed pile counters use the same persistent counter store.

Relevant files:

- `mods/card_editor/CardEditorRunCardCounterState.cs`
- `mods/card_editor/CardEditorQuestEffects.cs`
- `mods/card_editor/CardEditorExtraEffects.cs`

Suggested reproduction:

1. Start a multiplayer run where two players have the same custom quest card id in the same deck slot/copy ordinal.
2. Give the quest a run-progress condition, such as played cards, rooms entered, or enemy kills.
3. Advance only one player's quest.
4. Confirm the other player's quest progress reads from the same persistent counter or is cleared when the first quest completes.

### 33. `Gain Status Equal To Status` exposes `Set` and `Remove` modes but runtime treats them as `Gain`

**Priority:** P2  
**Area:** Card editor / status-to-status effects / runtime execution  
**Status:** Static-confirmed UI/runtime mismatch

The shared `StatusToStatusMode` enum supports `Gain`, `Lose`, `Set`, and `Remove`, and the editor shows all four modes for `Gain Status Equal To Status`. Runtime for this effect only checks for `Lose`. Every other mode falls through to the apply path, so `Set` and `Remove` behave like `Gain`.

Potential impact:

- A user selecting `Set` expects the destination power/status to become the computed amount, but it instead adds the computed amount to the existing stacks.
- A user selecting `Remove` expects the destination power/status to be removed, but it instead applies/gains stacks.
- The generated description is also misleading for `Set`/`Remove`, because `FormatStatusToStatus()` only distinguishes `Lose` from `Gain`.
- Saved presets preserve the selected mode, so the data looks valid even though runtime ignores two of the four choices.

Static review notes:

- `CardExtraEffectStatusToStatusMode` defines `Gain`, `Lose`, `Set`, and `Remove`.
- `NCardEditorPopup` adds all four mode options to the dropdown and makes that row visible for `GainStatusEqualToStatus`.
- `CardEditorPresetStore` serializes and deserializes `StatusToStatusMode`.
- `ModifyActivePower` implements all four modes in its delta switch, showing the enum itself is intended to support those semantics somewhere.
- `GainStatusEqualToStatus()` only branches when `StatusToStatusMode == Lose`; otherwise it applies the computed amount with `ApplyCustomStatusPower()` or `PowerCmd.Apply()`.
- `FormatStatusToStatus()` also only checks `Lose`, so `Set` and `Remove` are described as gain text.

Relevant files:

- `mods/card_editor/CardEditorExtraEffects.cs`
- `mods/card_editor/CardEditorPresetStore.cs`
- `mods/card_editor/NCardEditorPopup.cs`

Suggested reproduction:

1. Create a `Gain Status Equal To Status` effect.
2. Set the mode to `Set` or `Remove`.
3. Give the target an existing stack of the destination power/status and a nonzero source status.
4. Play the card and confirm the destination stacks increase instead of being set or removed.
