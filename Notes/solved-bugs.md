# Solved Bugs

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
