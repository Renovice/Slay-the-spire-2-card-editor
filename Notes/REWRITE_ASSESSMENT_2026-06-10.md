# Card Editor — Rewrite/Rework Assessment (2026-06-10)

Deep multi-agent analysis of the mod (27 agents over `mods/card_editor` + decompiled vanilla source).
Every bug root cause below was independently adversarially verified against the code.

**Verdict: do NOT do a ground-up rewrite. Do a staged rework: fix the 8 verified bugs (all small + surgical),
then consolidate per-kind logic so text/behavior can't drift, then adopt vanilla's DynamicVar pipeline for
numbers/green highlighting. Keep the save format exactly as is (it's name-keyed JSON and survives).**

---

## 1. Architecture facts (mod)

- `CardExtraEffect` = one flat class, **252 auto-properties** (CardEditorExtraEffects.cs:1280–1624); ~104 support enums; `CardExtraEffectKind` has **143 kinds** (:57–202) with 143 definition entries (:1766–2912).
- Execution = ONE monolithic `ExecuteEffectCore` (:26486–27623): ~26-kind early-return if-chain, special-cased DealDamage (`DamageCmd.Attack(amount).WithHitCount(repeats)` :26760), then a **111-case switch** inside a repeat loop.
- Card text = a **parallel, independent** switch (`TryFormatLine` :15293, ~133 arms) + ~30 `bool X(Kind)` capability gates (SupportsRepeat :23452, SupportsDuration :5175, SupportsAsPower :5219 …). Text and behavior are hand-synced — this is the structural cause of "dead options / text doesn't match".
- Structural duplication: `BranchCount*` fields (:1331–1364) are a verbatim ~33-field copy of the `Count*` group; the repeat formula is duplicated between `ResolveRepeatCount` (:23612) and `ApplyRepeatSuffix` (:23663); per-field plumbing requires 4 manual mappings (class + `CloneEffect` :37569 + DTO `FromEffect`/`TryToEffect` in CardEditorPresetStore.cs:1898/2164).

## 2. Vanilla reference (decompiled src) — what "1:1" means

- Cards: `CardModel.OnPlay(choiceContext, cardPlay)` + fluent `*Cmd` helpers (`src\Core\Commands\`). No action queue below player-input granularity — everything is awaited inline.
- Numbers: `DynamicVar` (BaseValue / EnchantedValue / PreviewValue + `WasJustUpgraded`). **Green rule** (`DynamicVar.ToHighlightedString`, StsTextUtilities.HighlightChangeText):
  - Green ONLY in (a) **upgrade preview** — `NUpgradePreview` calls `UpgradeInternal()` without `FinalizeUpgradeInternal()`, so `WasJustUpgraded` vars go green unconditionally (even if the value decreased). A *permanent* upgrade finalizes immediately → an upgraded card in deck/hand shows **no** green, just "+" title.
  - (b) **live combat modifiers**, hand/play piles only, baseline = **EnchantedValue** (not raw base).
- Multi-hit: `AttackCommand.WithHitCount(n)`, per-hit target re-roll, `Hook.ModifyAttackHitCount`. X-cost: `HasEnergyCostX`, captured at spend time, `Hook.ModifyXValue`.
- Death: `CreatureCmd.KillWithoutCheckingWinCondition` fires `Hook.AfterDeath` (:384) **before** `RemoveAllPowersAfterDeath()` (:398) — dying creature's powers DO receive their own death event. Vanilla on-death pattern = power overriding `AfterDeath` + self-filter (InfestedPower, MagicBombPower).
- Official mod surface: `ModHelper.SubscribeForCombatStateHooks`, `ModHelper.AddModelToPool`.

## 3. Verified bug root causes (fix locations)

| # | Bug | Root cause | Fix |
|---|-----|-----------|-----|
| 1 | "This Turn"/"Last X Turns" behave as "This Combat" (discards, energy, most counters) | `GetCombatHistoryEntryRoundNumber` (CardEditorExtraEffects.cs:33391) reflects `RoundNumber` on `entry.GetType()`, but vanilla declares it **private on the abstract base** `CombatHistoryEntry` → reflection returns null → falls back to **current round** for every entry → ThisTurn matches everything. (`LastTurns+ExcludeThisTurn` counts NOTHING.) Affects every vanilla-history count event; mod-recorded ones (EnergyGained/Lost, BlockLost, HealingReceived, OrbEvoked) window fine. | Reflect on `typeof(CombatHistoryEntry)` or use vanilla's public `CombatHistoryEntry.HappenedThisTurn()`. One method fixes ~25 count events at once. |
| 2 | Multi-hit: "Repeat by Count" = 1 base + N; count 0 still hits once | `ResolveBaseRepeatCount` floors at 1 (:23634); `ResolveRepeatCount` adds scaled repeats on top (:23614). The 0-repeat short-circuit already exists downstream (every consumer guards ≤0). | Add opt-in `RepeatScalingReplacesBase` ("Total hits = count"): change :23607–23615, mirror in `ApplyRepeatSuffix`, + field plumbing (class/Clone/DTO×2/UI toggle). LOW–MODERATE. |
| 3 | Corpse Explosion: host = "Attach to Trigger Target" does nothing | NOT vanilla ordering (hook fires before power removal). It's the mod's own guard: `RunLifecycleTrigger` (CardEditorExtraEffectPower.cs:1350–1355) silently returns for **non-player-owned powers** for every trigger except AfterAttack. | Widen: `allowEnemyOwned = trigger is AfterAttack or AfterDeath` (and consider the other lifecycle triggers + `RunLifecycleTriggerWithHookContext` :1319). |
| 3b | "Fatal" blacks out | asPower gate, not DealDamage: NCardEditorPopup.cs:26122 disables Fatal when Power is ticked; :24404 hides Power when Fatal selected. Runtime half-supports Fatal-as-power (`RunFatalPowerEffects`) but it's unreachable from authoring. | Decide on semantics, then unblock the UI gate or remove the dead runtime path. |
| 3c | No "Self" Effect Host | Dropdown has exactly CardOwner / TriggerTarget / EffectTargets (NCardEditorPopup.cs:15511–15517). "Self" only exists in Trigger-From / Target enums. | Docs/UI label fix; optionally add a true Self host. |
| 4 | Art tuning leaks to next edited card | All vanilla cards share ONE cached popup (and created cards another). `RetargetLocalizedSharedPopup` rebinds everything EXCEPT `_portraitOffsetXField/_portraitOffsetYField/_portraitZoomField` (created only in one-time BuildUi, NCardEditorPopup.cs:6000–6061) → stale text written into next card's override on Apply (:31387–31396). Flip side: existing tuning is never *loaded* on retarget either. | Rebind the 3 fields from `GetEffectivePopupOverride()` in `BindVanillaArtControls` / `BindCreatedArtControls`. |
| 5 | Card names randomly vanish / wander between cards (~85% confidence) | `CardEditorCardVisualElementPatches.cs` visibility-snapshot latch on POOLED NCard nodes: `NCard.Create` sets Model before `_Ready` → snapshot captures `TitleVisible=null` with `IsCaptured=true` → restore is a permanent no-op → label latched hidden; re-capture of hidden state poisons the node for the session. Pooled node rebinds to arbitrary cards → "a different card's name disappears". Vanilla never resets `_titleLabel.Visible`. Precondition: any cosmetic hide flag in overrides. | Skip capture while `!IsNodeReady()` / fields null; treat scene defaults as baseline; never capture while a hide is applied; restore on pool return. |
| 6 | Any Finish + full-art → art bleeds past frame bottom | Finish overlay ColorRects are children of `_portraitCanvasGroup`, sized to the **Frame** rect (−150,−211→150,211) instead of AncientPortrait's visual rect (−153,−215→146,206) (CardEditorCardFinishPatches.cs:569–576, 3356–3362). Union grows 5px down/4px right → vanilla ancient-portrait CanvasGroup mask (calibrated `const vec2 size = vec2(598,842)`) stretches → bottom cutoff drops ~5px. | Size full-art overlays to the AncientPortrait rect. |
| 7 | Rainbow Glitter (Art) does nothing | Shader reads stage built-in `FRAGCOORD` inside custom function `art_rect_uv()` (CardEditorCardFinishPatches.cs:1310–1314) — illegal in Godot 4 → compile failure → effect no-op. Regression from the "art space" refactor (absent in Mar-30 .bak). | Pass `FRAGCOORD.xy` from `fragment()` as a parameter. Then also fix the stale screen-rect uniforms (:213–231) that will become visible. |
| 8 | Status editor "can't create new status" | Create-new exists but is labeled **"Create Effect"** (:36789); first saved status auto-loads on open (:36832); `SaveDefinition`→`UpsertStatus` derives id from NAME and **deletes the previously loaded status on rename** (CardEditorDefinitionStore.cs:267–271). No Save-As-New. | Rename button, don't auto-load, treat name-change as create (or prompt). |

### Latent / adjacent (found during verification, worth fixing)
- **DESTRUCTIVE downgrade**: `CardEditorCreatedCardsStore.LoadInternal` skips cards when `Version > CurrentVersion` then unconditionally `Save()`s → older mod **overwrites** newer `created_cards.json` with defaults (:762, :791–792). Same on any parse exception. Fix before any format work.
- Created cards embedded in presets always parse with hardcoded `fileVersion: 3` (CardEditorCreatedCardsStore.cs:1450) → legacy normalizers run on modern data.
- Unknown enum `Kind`/`Target` on load → effect **silently dropped, no log** (CardEditorPresetStore.cs:1341, :2168). Quarantine + warn instead.
- `CardEditorFullArtRenderContext.Enter/Exit` is Prefix/Postfix — should be Finalizer (leaks ThreadStatic if Reload throws).
- Mod live-number green baseline compares vs RAW base, vanilla vs EnchantedValue (CardEditorExtraEffects.cs:22231) → enchanted cards permanently green.

## 4. Feature asks

- **"Whenever X → Event Target" for more events**: runtime ALREADY delivers the event actor as the synthetic CardPlay's Target for most count events (CardEditorExtraEffectPower.cs:1176–1199). The ONLY blocker is the UI gate `SupportsSelectedEventTarget` (NCardEditorPopup.cs:29401–29408) restricting to DamageDealt/DamageTaken. Widening it = unlocking "whenever any enemy gains block, THAT enemy loses HP" nearly for free (audit per-event actor semantics first; timed triggers have no event target).
- **Auto Action + Whenever**: double-blocked — `SupportsAsPower` excludes the Auto kinds (CardEditorExtraEffects.cs:5219) and the UI snaps the trigger back to OnPlay (NCardEditorPopup.cs:24409–24426). Needs a real (small) design decision, not just ungating.

## 5. Warcraft-3 Event→Condition→Action proposal

- The data model is ALREADY per-row (one Kind + one Trigger) — and the description preview already merges consecutive same-trigger lines (`MergeDescriptionEffectLines`, break at :11340).
- **Event-first grouping is a view-layer change, LOW invasiveness**: group rows under trigger headers, stamp the group trigger into each member on save. No save-format change, round-trips with old saves. Trigger-dependent sub-row visibility + AsPower-sensitive labels must move with it.
- A TRUE shared Event/Condition node owning multiple Actions = data model + save format change. Not needed: do it as UI sugar over the flat model ("copy condition to group members").
- Recommendation: adopt the ordering as presentation (and group the effect summary sidebar by trigger — it's currently a flat numbered list), keep the flat model.

## 6. Persistence / backward compat (constraint check for any rework)

- Format: System.Text.Json, PascalCase, **enums stored as NAME strings** (not ints) → reordering/renumbering enums is safe; RENAMING members breaks saves. ModelIds are stable strings.
- Preset `Version` 17 with three explicit migrations (v2 absolute-upgrade, v<12 repeat scaling, v<13 delta defaults); everything else silent-defaults.
- A new internal engine can keep the wire format and map old names → new concepts; the importer must handle: name tables (143 kinds + ~80 enums), absent-vs-explicit-default ambiguity, `ExtraEffectNumericFieldsAreDeltas` flag, index-aligned null slots in upgrade lists, effect-id cross references (`AmountSourceEffectId` etc.), embedded keyword/status library.

## 7. Recommended plan (staged, each stage shippable)

1. **Safety**: GitHub backup (done, commit 5829e4c); fix the destructive created_cards.json overwrite; stop silent effect drops on load.
2. **Bug wave**: fixes #1–#8 above. All surgical; #1 and #3 are the big user-facing wins and are one-method/one-line class fixes.
3. **Consistency spine** (the real "rework"): per-kind descriptor consolidating Execute + FormatLine + capability flags into ONE place per kind (kill the parallel switches and the ~30 gates); dedupe Branch*/Count* and the repeat formula; auto-derived Clone/DTO plumbing. This is what eliminates the "dead options" class permanently. UI untouched.
4. **Vanilla-faithful numbers**: route created-card amounts through real `DynamicVar`s so vanilla's `diff()` formatter, EnchantedValue baseline, and upgrade-preview `WasJustUpgraded` produce the green numbers — delete the three text-diff heuristics (identity-based, not string-diff-based). This is the single highest-leverage "1:1 with vanilla" change.
5. **Editor flow**: Event-first grouping (view layer), status-editor UX fixes, Event Target widening.
