# Solved Bugs

---

## [2026-05-02] Card Editor autoplay chains can recursively softlock combat

**Symptom:** A created card/power such as "when you play a Necrobinder card, play 3 random Necrobinder cards" could recursively trigger itself through the cards it autoplayed. Against damage-cap bosses this could create an effectively infinite autoplay chain and softlock combat.

**Root cause:** Card Editor-owned autoplay effects intentionally used the normal `CardCmd.AutoPlay` flow, so cards played by an effect could satisfy the same OnPlay/power condition again. There was no Card Editor-specific turn cap or opt-in guard for self-triggering autoplay chains.

**Fix:** Added a Card Editor autoplay loop guard with two settings: a per-player/per-turn autoplay cap (`EnableCardEditorAutoPlayLoopCap`, `CardEditorAutoPlayLoopCapPerTurn`) and an opt-in self-loop suppression mode (`PreventCardEditorAutoPlaySelfLoops`) that prevents cards played by a Card Editor effect from re-triggering that same effect chain. The per-effect loop limit now counts source effect activations, including immediate power-mode triggers, and can be scoped per card copy, all copies, or equivalent autoplay effects. The guard is applied before moving/generated cards enter autoplay, so capped plays do not leave cards stranded in the play pile. Generated descriptions now show live remaining uses for the turn and color that number with the normal card-text change highlighting. Files: `CardEditorAutoPlayLoopGuard.cs`, `CardEditorExtraEffects.cs`, `CardEditorExtraEffectPower.cs`, `CardEditorPerformanceSettings.cs`.

---

## [2026-05-02] Chemical X flashes but does not boost deferred Card Editor X effects

**Symptom:** X-cost created cards could trigger Chemical X's animation, but X-based extra effects stored as powers or delayed effects later resolved without the Chemical X bonus.

**Root cause:** Vanilla Chemical X applies through `ModifyXValue`, which only affects `ResolveEnergyXValue()` / `ResolveStarXValue()`. Immediate Card Editor effects used that path, but deferred/power effects stored `AmountIsX`, `RepeatIsX`, or `CardSelectionCountIsX` and resolved X later against the trigger play instead of the original X-cost card play.

**Fix:** Added a deferred-execution clone path that freezes X-derived amount, repeat count, and selection count from the original `CardPlay` before scheduling or storing power entries. The captured value includes vanilla `ModifyXValue`, so Chemical X now contributes to those deferred Card Editor X effects. Files: `CardEditorExtraEffects.cs`, `CardEditorExtraEffectPower.cs`, `CardEditorExtraEffectScheduler.cs`.

---

## [2026-05-02] Generated Pick One selector can leave a created card stuck

**Symptom:** A created card using "Generate Card / Pick One of Three" with a tight text/tag match could remain centered after play, with no pick UI completing and no discard/consume cleanup. Logs showed `TaskCanceledException` escaping from `CardSelectCmd.FromChooseACardScreen`.

**Root cause:** If the vanilla generated-card chooser overlay was canceled or removed before selection, the cancellation propagated through created-card OnPlay effect execution.

**Fix:** Added a shared generated-choice destination helper and treat chooser cancellation as a skipped generated choice instead of letting it escape. The cancellation recovery also signals the player-choice context to end, because the vanilla command does not reach that cleanup path after `CardsSelected()` is canceled. The chooser still opens for the generated options, including narrow filtered sets. File: `CardEditorExtraEffects.cs`.

---

## [2026-04-02] Orb actions not saving in presets

**Symptom:** Loading a preset in the Preset Creator reset all Orb Action effects to "Evoke your leftmost orb" regardless of what was saved.

**Root cause:** `CardExtraEffectDto` in `CardEditorPresetStore.cs` was missing six fields: `OrbAction`, `OrbType`, `OrbSelection`, `OrbFollowUp`, `CountOrbType`, `CountOrbSelection`. `FromEffect` never serialized them and `TryToEffect` never deserialized them — so every load used the C# default values (Evoke, Any, Leftmost, None).

**Fix:** Added the six missing string fields to `CardExtraEffectDto`, serialized them in `FromEffect`, and deserialized them (with fallback defaults) in `TryToEffect`. File: `CardEditorPresetStore.cs`.

---

## [2026-04-02] Cannot reduce self-Vulnerable stacks on upgraded card

**Symptom:** When editing an upgraded card's effect to apply fewer stacks of Vulnerable to yourself (e.g. 3 → 2), saving and reloading always reverted back to the base amount — the reduction was silently dropped.

**Root cause:** `CardUpgradeOverrideDto.ToUpgradeSafe()` in `CardEditorPresetStore.cs` validated upgrade delta amounts using `IsValidEffectAmount(kind, amount)` which requires `amount > 0` for most effect types. A negative delta (e.g. `-1` to reduce stacks by 1) failed this check and was replaced with `null`, discarding the change.

**Fix:** Changed the validation call to `IsValidUpgradeDeltaAmount(kind, amount)` which correctly allows any non-zero value (including negatives). File: `CardEditorPresetStore.cs`, `ToUpgradeSafe()`.

---

## [2026-04-02] No way to make Osty attack/heal/kill from a custom card

**Symptom:** The card editor only had `Summon Osty` as an Osty-related action. Users could not make Osty attack, attack all enemies, heal, or die as a card effect.

**Root cause:** Missing `CardExtraEffectKind.OstyAction = 74` effect kind — the Osty combat actions (`DamageCmd.Attack().FromOsty()`, `CreatureCmd.Heal(owner.Osty, ...)`, `CreatureCmd.Kill(owner.Osty)`) existed in the game but were never wired into the card editor.

**Fix:** Added `OstyAction = 74` to `CardExtraEffectKind`, new `CardExtraEffectOstyAction` enum (Attack, AttackAll, Heal, Kill), `OstyAction` property on `CardExtraEffect`, definition entry, `OstyActionLabel`/`FormatOstyAction` helpers, execution case in `ExecuteEffect`, `CloneEffect`/`EffectsMatchExceptAmount` entries, UI row (`ostyActionRow` + `ostyActionSelect` dropdown), and preset serialization. Files: `CardEditorExtraEffects.cs`, `NCardEditorPopup.cs`, `CardEditorPresetStore.cs`.
