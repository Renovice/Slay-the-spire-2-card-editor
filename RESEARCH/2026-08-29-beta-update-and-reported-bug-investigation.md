# Beta Update and Reported Bug Investigation

Date: 2026-08-29

Scope:

- Card Editor mod under `mods/card_editor`.
- Installed Slay the Spire 2 beta assembly at `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll`.
- Newest decompiled game source under `Slay the spire 2 Source/src`.
- Player reports supplied with the beta update request.

Verification meanings used below:

- **True:** source evidence confirms a concrete failure path.
- **False:** the suspected missing behavior exists in the current source and no contradictory static path was found.
- **Fixed:** a source change was made and the mod builds against the installed beta.
- **Needs in-game regression:** static and build checks pass, but the behavior requires a running combat/UI to prove end to end.

## 1. New Beta Loader Failure

**Hypothesis:** The `ReflectionTypeLoadException` is caused by Card Editor being compiled against an older game API.

**Evidence:**

- The installed beta changed networking and command APIs used by the mod.
- The old mod referenced the former lobby/version shapes.
- `INetGameService.LocalVersion` and the current `PeerVersionInfo`/join-flow constructor signatures are present in the installed beta.
- All beta API call-site changes compile against the installed `sts2.dll`.

**Conclusion:** **True, fixed.** The mod was retargeted to the installed beta. A clean mod build completes with zero warnings and zero errors, and the deployed build reaches the main menu without the original loader exception.

Follow-up loader testing found and fixed one additional beta API rename in the remappable hotkey integration: `DefaultKeyboardInputMap` became `DefaultHotkeyInputMap`, and the settings-title map is now the public `NInputSettingsEntry.commandToLocTitle`. The final Steam launch applied 287 Harmony patch classes with zero skipped and reached the main menu without a Card Editor exception.

The manifest now declares `min_game_version: v0.111.0`, matching the installed beta and removing the mod-loader compatibility warning.

## 2. Reward Pools Can Invalidate Editor Data

**Hypothesis:** Selecting a reward pool causes startup deserialization to touch `ModelDb` before model content is ready, which makes the selected-pool record fail while an empty record survives.

**Evidence:**

- `CardEditorRewardPoolRegistry` built static ID sets by calling `ModelDb.Card<T>().Id`.
- Loading `created_cards.json` and presets can initialize this registry before full card-model access is safe.
- The failure only appears when reward-pool state is present, matching the report.
- A failed static initializer remains poisoned for the process, explaining why preset loading also fails after the initial load failure.
- `ModelDb.GetId<T>()` obtains the stable model ID without materializing the model.

**Conclusion:** **True, fixed.** Reward-pool registry IDs now use `ModelDb.GetId<T>()`. Regression test: select `Trial - Card Reward`, save, restart the game, and then load a preset containing reward-pool state.

## 3. Calling Bell Relic Count

**Hypothesis:** Editing Calling Bell's `Relics` dynamic variable cannot work because vanilla hardcodes three reward objects.

**Evidence:**

- The beta `CallingBell.GenerateRewards()` constructs exactly three rewards: Common, Uncommon, and Rare.
- It never reads `DynamicVars["Relics"]`.
- The editor changes the displayed dynamic variable, but vanilla reward generation ignores it.

**Conclusion:** **True, fixed.** A postfix resizes the generated reward list to the edited count and cycles Common/Uncommon/Rare when more than three are requested. A zero-count pickup still adds Curse of the Bell but skips the invalid empty reward screen.

## 4. Tea of Discourtesy and Ember Tea Combat Counts

**Hypothesis:** Editing the visible `Combats` dynamic variable does not modify the private saved counter used by runtime logic.

**Evidence:**

- Both relics have a separate `CombatsLeft` property backed by `_combatsLeft`.
- Their combat hooks decrement and test `CombatsLeft`, not the edited dynamic variable.
- The property setter updates the dynamic variable in the other direction, but a generic dynamic-variable override does not invoke the property setter.
- Ember Tea's Strength value works because that hook reads `DynamicVars.Strength` directly, matching the report.

**Conclusion:** **True, fixed.** Applying an override now synchronizes edited `Combats` into each relic's runtime `CombatsLeft` property. Failures are logged instead of silently ignored.

## 5. Hefty Tablet Card Count and Reward Screen

**Hypothesis:** Hefty Tablet's vanilla choose-a-card screen is tied to its expected three-option layout and becomes unreliable for edited counts outside that shape.

**Evidence:**

- Vanilla generates `DynamicVars.Cards` options but sends them to `FromChooseACardScreen`, a specialized choose-one presentation.
- The reported broken reward presentation occurs when the option count is edited.
- The general reward-grid selector accepts variable-sized option collections and supports selecting zero or one card.
- Counts one through three retain the tested vanilla flow; zero and counts above three require the general grid.

**Conclusion:** **True for nonstandard option counts, fixed.** Zero and counts above three now use the variable-size reward grid. The chosen rare card and Injury are added normally, and unchosen cards are recorded in run history. Counts one through three retain vanilla behavior.

## 6. Nightmare Does Not Copy Card Mutations

**Hypothesis:** Nightmare copies the current card model values but loses Card Editor's externally stored persistent mutation metadata.

**Evidence:**

- Nightmare calls `CardModel.CreateClone()` when selecting the card and again when creating the hand copies.
- `CreateClone()` routes through `CombatState.CloneCard()`.
- Card Editor stores mutation-diff metadata outside normal card fields for non-created cards.
- `ClonePreservingMutability()` copies the visible mutated values but cannot copy Card Editor's external metadata automatically.
- Losing the metadata can make the clone revert or fail to preserve subsequent mutation behavior.

**Conclusion:** **True, fixed.** `RunState.CloneCard` and `CombatState.CloneCard` postfixes copy mutation metadata to the clone and mark the already-copied values as applied, preventing the diff from being applied twice.

## 7. Scaling by a Stackable Debuff Count

**Hypothesis:** The runtime can only count enemies that have a debuff, not the amount of that debuff on one enemy.

**Evidence:**

- The current amount-source path supports `Value Source -> Target -> Power / Status`.
- `GetSpecificPowerValueSourceAmount` returns the selected power/status instance's `Amount`.
- The source can therefore read the Mark stack count on the selected enemy rather than counting matching enemies.
- The configuration is persisted and is used by runtime amount resolution.

**Conclusion:** **False as a missing runtime feature.** Configure `Amount Source: Value Source`, `Actor: Target`, `Value: Power / Status`, then choose the Mark/debuff power. This is a discoverability problem unless an in-game regression shows that a specific debuff is missing from the selector.

## 8. Decimal Source Multiplier Rounding

**Hypothesis:** Source multipliers round midpoint values to nearest instead of following vanilla floor behavior.

**Evidence:**

- `ScaleAmountSourceAmount` used decimal rounding rather than truncation/flooring.
- For example, a source of 3 with a multiplier of 0.5 produced 2 instead of 1.

**Conclusion:** **True, fixed.** Positive scaled source amounts now use `decimal.Floor`, matching the requested vanilla behavior.

## 9. Source Effect Ignores Live Damage and Block Modifiers

**Hypothesis:** A vanilla dynamic-variable source reads only `BaseValue`, so it sees the original card number but not Strength, Vulnerable, Dexterity, or similar live hooks.

**Evidence:**

- The old source resolver returned the dynamic variable's base value.
- Vanilla resolves damage through `Hook.ModifyDamage` and block through `Hook.ModifyBlock` with card/card-play context.
- Reading the base avoids all runtime modifiers, matching the report.
- Applying modifiers to the source and then executing the dependent damage/block as powered would apply the same modifiers twice.

**Conclusion:** **True, fixed.** Damage and block dynamic sources now resolve through the beta's live hook APIs. Dependent damage/block rows are marked unpowered so the already-resolved Strength/Vulnerable/Dexterity contribution is not applied a second time. Resolver exceptions are logged and fall back to base value.

## 10. Delayed Power Effects Rescale After the Power Is Played

**Hypothesis:** Damage/block stored inside a generated power is executed as a new powered card effect, so later Strength, Weak, Vulnerable, Dexterity, and similar modifiers alter the previously established amount.

**Evidence:**

- Power payloads cloned the effect but did not distinguish their delayed execution from immediate card effects.
- Every later damage/block execution entered the ordinary powered command path.
- This explains a power saying "deal 3 at end of turn" changing after Strength is gained.

**Conclusion:** **True, fixed.** Damage and block payloads cloned for power execution carry an internal snapshot flag. All damage variants and block execution add `ValueProp.Unpowered` for these payloads. Immediate card effects retain normal live scaling.

## 11. Granting Hits All Enemies Soft-Locks a Card

**Hypothesis:** Granting `HitsAllEnemies` to a vanilla single-target card changes targeting after the game has already committed to the original targeting/resolution flow.

**Evidence:**

- The current code explicitly excludes `HitsAllEnemies` from grantable effects.
- The surrounding source comment records the center-screen unresolved-card failure caused by this interaction.

**Conclusion:** **Historically true, already guarded.** Keep as an in-game regression case. The current editor/runtime must reject or skip this grant rather than creating an unresolvable card play.

## 12. Copy Debuffs Appears to Do Nothing

**Hypothesis:** Copy Debuffs previously ran without a source enemy because otherwise self-targeting cards did not request a manual enemy target.

**Evidence:**

- The current target requirement explicitly includes `CopyDebuffs`.
- Targeting patches force selection of the source enemy.
- The default destination is other enemies, excluding the source enemy.
- In a one-enemy combat, there are intentionally no other enemies that can receive the copied debuffs.

**Conclusion:** **Historical targeting failure appears fixed; needs in-game regression.** Test with at least two living enemies and a copyable debuff on the manually selected source.

## 13. Card Action: Move, Exhaust, and Transform

**Hypothesis:** The beta changed card-play and pile-command contracts, breaking older synthetic plays and direct pile manipulation.

**Evidence:**

- Current synthetic `CardPlay` construction supplies the beta-required `Player` field.
- Move and Exhaust use the current `CardSelectCmd.FromHand`, `CardCmd.Discard`/`CardCmd.Exhaust`, and `CardPileCmd.Add` APIs.
- Transform uses effective card type/tag filters and the beta transform pipeline.
- These paths compile against the installed beta.

**Conclusion:** **The old API incompatibility is fixed statically; needs in-game regression.** Test hand/discard/exhaust/draw top and bottom, hand exhaust, and filtered transform by both type and tag.

## 14. Reduce Cost: This Card: Whenever Event

**Hypothesis:** Triggered cost reduction was treated as a passive modifier or was not dispatched after the selected event.

**Evidence:**

- Current code separates passive and triggered cost definitions.
- Lifecycle, play, reaction, and boundary trigger paths call `ApplyTriggeredCardCostsLess`.
- Permanent and timed variants have explicit mutation/grant handling.

**Conclusion:** **No remaining static missing dispatch found; needs in-game regression.** Test at least On Play, On Draw, turn boundary, and After Death with This Turn, This Combat, Until Played, Turns, and Permanent durations.

## 15. Match Energy Listed Twice and Card Text Overflow

**Hypothesis:** The current definitions contain duplicate Match Energy entries.

**Evidence:**

- Static search finds only one current `Matching Cards (Energy)` label.
- No second current `Match Energy` definition was found.
- Text fitting depends on rendered card UI and cannot be proven by source search or compilation.

**Conclusion:** **False for a current static duplicate; UI regression still needed.** Verify the selector and render an intentionally long generated description in game.

## 16. Resources: After Death: Check on Power Does Nothing

**Hypothesis:** After-death effects were skipped when the generated power was hosted on an enemy, or the dying creature's power was removed before Card Editor dispatched it.

**Evidence:**

- The current power path explicitly permits enemy-hosted After Death powers.
- `CardEditorExtraEffectPower.AfterDeath` suppresses the preliminary prevented-death callback and runs on final death.
- A hook patch also routes card-based After Death effects through the current lifecycle dispatcher.

**Conclusion:** **The reported source-level dispatch gaps are addressed in current code; needs in-game regression.** Test both player-hosted "whenever a creature dies" and enemy-hosted "when this creature dies" power effects.

## Verification Checklist

- [x] Compile Card Editor against the installed beta `sts2.dll`.
- [x] Build result: zero warnings and zero errors.
- [x] Confirm current beta signatures from the newest decompile.
- [x] Run `git diff --check` on handwritten C# changes.
- [x] Deploy final DLL and PDB to the Steam mod folder.
- [x] Confirm deployed SHA-256 hashes match build outputs.
- [x] Launch through Steam and confirm Card Editor loads without `ReflectionTypeLoadException`.
- [x] Confirm all 287 Harmony patch classes apply with zero skipped.
- [x] Confirm the consistency and text-snapshot audits pass.
- [ ] Run the in-game regression cases listed above.

## Static Verification Boundary

Compilation proves that the mod references the installed beta API correctly. Source tracing proves the stated failure paths and that the corrected paths are connected. It does not prove Godot UI behavior, Harmony patch application during game startup, multiplayer synchronization, save/reload behavior, or combat outcomes. Those require the final deployed build and in-game regression testing.

The final loader test did prove Harmony patch application and startup through the main menu. Combat outcomes, reward UI interactions, and save/restart reproduction cases remain separate in-game tests.
