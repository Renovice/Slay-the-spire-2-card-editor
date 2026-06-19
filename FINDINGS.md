## 2026-06-16 - Relic effects feasibility (deep dive) + plan

Hypothesis: Porting all card effects onto relics is feasible by reusing the existing effect/trigger infra.
Finding: True, high reuse (~80-90%). The effect engine needs ZERO changes; relics become a new host.
Evidence (6-agent deep dive): StS2 relics are AbstractModel subclasses whose behavior is overridden virtual Hook methods (Core/Models/RelicModel.cs:22/:388 ShouldReceiveCombatHooks; ~60 hooks in AbstractModel.cs:126-621) — the SAME hook bus cards/powers use. The card editor already runs effects from a non-card host: power triggers build a synthetic CardPlay + push 3 AsyncLocal contexts (CardEditorExtraEffectPower.cs:1001-1004) and call ExecuteEffect (CardEditorExtraEffects.cs:26514), which reads owner/source from those contexts. So a relic = same pattern; the one new primitive is a relic "proxy card" for the effect's required CardModel SourceCard. Current relic editor only overrides numbers/desc/pool (CardEditorRelicOverrides.cs:26-42). Two trigger kinds: reactive After*/Before* (run effects, main feature) vs Modify* (return a value -> separate passive-modifier layer). Effects bucket into host-agnostic (work as-is) / adaptable / card-only (gate with a new SupportsOnRelic()).
Decision (user, 2026-06-16): support BOTH overriding existing relics AND brand-new custom relics; expose ALL ~60 hooks.
Full plan: Notes/RELIC_EFFECTS_PLAN.md (architecture, trigger map, effect buckets, 6 components, 6 phases, design decisions, risks, evidence index).
Next Step: await go-ahead to build Phase 1 spine (proxy card + relic effect host + one reactive trigger end-to-end).

## 2026-06-14 - Completed Simplified-Chinese (zhs) localization (native-speaker bug report)

Hypothesis: The zhs loc gap is just my recent patches' labels.
Finding: Partially true — bigger. The new features' DROPDOWN labels were translated, but their CARD-TEXT strings (11 keys: cardText.currentStars/Energy/OrbSlot ScalingSuffix, condition.haveStars/starsCount/haveEnergy/energyCount/haveOrbSlots/orbSlotsCount, value.hasStarCost/hasEnergyCost) were never in ANY loc file — only code fallbacks — so a Chinese user saw English rules text. Plus a pre-existing 16-key zhs backlog (generatedPool.Status/Curse/Quest/Token/Event/AllPools, valueSource.eventActor, poolSuffix/poolDescriptor.allCardPools, powerTrigger.actor + powerTriggerFrom.markedTarget, 5 tooltip.powerTriggerFrom.*).
Evidence: python eng-vs-zhs key diff (was 16 missing + the 11 not-in-any-file). Translated all 27 via a 3-agent workflow (2 independent translators + adjudicator with back-translation + placeholder verification + glossary enforcement: 星星/能量/充能球槽位/攻击-技能-能力, 生物 for creature, 厄运 for Doom, 友方 for ally). Applied via format-preserving insertion script: zhs +27 (->2520), eng +11 (->2503, English source for the new keys so files stay consistent). Re-diff: 0 keys missing from zhs; all {Effect}/{Comparison}/{Value} placeholders verified present, no {StarDescriptor}/{Plural} leak. LOC-ONLY (no code change/rebuild).
Note: kor has the same 16+11 gap (NOT done — user asked Chinese only; offer separately). Scaling-suffix zhs bakes the noun (drops the English-carrying {Descriptor} token) so step>1 multiplier isn't shown — minor, common case perfect.
Next Step: commit+push+Steam-deploy the loc; optionally do kor next.

## 2026-06-14 - Implemented Current-resource count events + Non-Attack/Skill/Power type filters

Hypothesis: The requested scaling params + inverse type filters can be added with contained, low-risk changes.
Finding: True (builds clean, 0 errors). Count events fully done; inverse types functional + labeled + primary text done, with secondary auto-text noted as follow-up.
Evidence (count events, CardEditorExtraEffects.cs): appended CardExtraEffectCountEvent CurrentStars=49/CurrentEnergy=50/CurrentOrbSlots=51 (serialize by NAME via PresetStore string DTO, so append is safe + UI index==value preserved). Resolver GetHistoryCountMultiplier reads owner.PlayerCombatState.Stars / .Energy / .OrbQueue.Capacity (all verified real in decompiled Source/.../PlayerCombatState.cs:71,90 + OrbQueue.Capacity). Mirrored EmptyOrbSlots at: CountEventLabel (3216), CountEventUsesWindow exclusion (live reads, no turn/combat window), scaling-suffix text (17053-area), condition text (17707-area). Loc countEvent.* added to all 6 files (eng/kor/zhs × card_editor_pack + built cfiles).
Evidence (inverse types): appended CardGeneratedCardType NonAttack=8/NonSkill=9/NonPower=10. Handled in ALL 5 type-matchers (else default mis-matches — 31479's default returns card==card=always true): CardEditorExtraEffects MatchesGeneratedCardType (31479) + candidate-filter else-if (37184-area), CardEditorCardTypeCostAuras.MatchesType (418), CardEditorDrawnGeneratedCostController.MatchesType (500), CardEditorRewardPools.MatchesType (808). GeneratedCardTypeLabel + count-filter typeAdj (BuildCountCardFilter ~18686) + loc generatedType.* (6 files). Chose explicit enum members over an invert-toggle: auto-handles UI (Enum.GetValues) + serialization (by-name), works universally incl. generation, ~1/2 the sites of the toggle (no 3-bool UI/serialization/equality plumbing).
Open follow-up: secondary auto-text typeAdj sites (trigger-condition + generation descriptors, ~5 sites) still render the type qualifier as "card" not "non-Attack card" (filter WORKS, only the rules-text adjective is missing). Not yet committed/deployed.
Next Step: optionally finish secondary typeAdj text; commit+push+Steam-deploy when user asks.

## 2026-06-13 (CORRECTED) - Power-specific (non-global) created-card discount: use "Created Cards Cost Less" (created-by-this), On Play

Hypothesis (mine, first pass): "Created Cards Cost Less" (CreatedCardsCostLess) can't reach cards made by a power on the same card; must use the Global variant.
Finding: FALSE — I was wrong (Bartek correct). CreatedCardsCostLess IS source-card-scoped and DOES cover a power's generations, and it is the correct tool when you want ONLY that card/power's generated cards discounted (NOT global).
Evidence: CardEditorCreatedCardsCostPatches.cs:855 CardPileCmd_AddGeneratedCardsToCombat_CreatedCardsCostLess_Patch — prefix on the single generation chokepoint CardPileCmd.AddGeneratedCardsToCombat: (1) ResolveSourceCard() (882), (2) reads ONLY that source card's effects for CreatedCardsCostLess (894-911), (3) stamps ONLY this batch (917-950). CardEditorGeneratedCardSourceResolver: when CardEditorHookModelContext.Current is a PowerModel, resolves source via CardEditorPowerSourceMap.TryGetSourceCard(power) → the power's owning card. So a power's generated cards resolve to its card and get that card's CreatedCardsCostLess — scoped, never global. GATE: only honored when effect.Trigger == OnPlay (905); duration via CreatedCardsCostDuration (UntilPlayed → ApplyUntilPlayed). Distinct from GeneratedCardsCostLess (=58, "Generated Cards Cost Less"/"...(Global)") which is the player-wide grant.
UI location: "Created Cards Cost Less" is NOT a top-level effect — it's variant #10 in the "Card Generation" effect's variant dropdown (UnifiedCardGenerationVariant.CreatedCardsCostLess, NCardEditorPopup.cs:17014). Likely failure cause if it looked dead: Trigger not set to On Play.
Next Step: Config = Card Generation → variant "Created Cards Cost Less", Trigger On Play, Reduce 1, Until Played, Energy, on the same card as the generating power.

## 2026-06-13 (SUPERSEDED — see correction above) - Power-generated cards via "Created Cards Cost Less (Global)"

Hypothesis (Bartek's): The user is wrong that a power-generated card can't get the card's "cards created cost less" discount; it's achievable (e.g. by latching the effect onto the power).
Finding: True (achievable; user's claim of impossible is false) — but the real fix is the effect KIND, not where it's attached.
Evidence: CardEditorExtraEffects.cs — TWO kinds: CreatedCardsCostLess=27 "Created Cards Cost Less" (2253) vs GeneratedCardsCostLess=58 "Created Cards Cost Less (Global)" (2677). SupportsAsPower (5274) EXCLUDES CreatedCardsCostLess (on-play, this-card-scoped only; can't be a power) but ALLOWS GeneratedCardsCostLess. Execution: GeneratedCardsCostLess → CardEditorDrawnGeneratedCostController.Apply (28344/27330) registers a PLAYER-WIDE standing grant (no source-card filter). OnCardGenerated (controller 292) stamps the discount on ANY generated card matching pool/type — fired by Hook.AfterCardGeneratedForCombat (controller patch 545). Generation path: ChooseOneOfThreeCardsToHand (36177) → AddGeneratedChoiceToConfiguredDestinations (36052) → TryAddGeneratedCardToCombat (35967) → vanilla CardPileCmd.AddGeneratedCardsToCombat (35980) which fires that hook. Duration: UntilPlayed → grant=ThisCombat (controller 160); ThisCombat grants are NOT decremented at turn end (controller 74), so the grant persists all combat and covers the power's start-of-turn generations every turn.
Reason: The user used "Created Cards Cost Less" (this-card-scoped, immediate, non-power) so the power's separate generation isn't covered — correct symptom, wrong conclusion. The "(Global)" variant is a standing aura that ignores which card/power generated the card.
Next Step: Tell user to swap effect 1 to "Created Cards Cost Less (Global)", On play, Reduce 1, duration Until Played, pool/type matching the power's cards. (Attaching to the power also works but is unnecessary and order-sensitive.)

## 2026-06-13 - Added HasStarCost / HasEnergyCost count filters (the missing feature)

Hypothesis: A new "card has a star/energy cost" count filter can be added with a small, contained change without breaking the build or existing presets.
Finding: True (build clean, 0 errors; all sites verified by 6-agent discovery + completeness audit, no site missed).
Evidence: mods/card_editor/CardEditorExtraEffects.cs — enum CardExtraEffectCountCardFilter +HasStarCost=38/HasEnergyCost=39 (1141); CountCardFilterLabel +2 arms (3914); CountCardFilterPrefixLabel +2 arms "Star-cost"/"Energy-cost" (3949, card-text adjective); MatchesCountCardEffectFilter +2 cases returning GetCardStarCostAmount/GetCardEnergyCostAmount(card, useBaseCost:true) > 0 (35309). Loc keys added to card_editor_pack + built cfiles eng/kor/zhs (all 6 JSON re-validated). `dotnet build -c Release` = Build succeeded, 0 errors.
Reason: MatchesCountCardEffectFilter (35186) is the SINGLE predicate switch — grant (34879), branch, self-scaling, trigger, and InPile/history counters all delegate to it, so one edit covers every consumer. Default arm is `return true`, so the explicit cases were mandatory (else match-all). Used base cost (card identity, matches Crescent Spear "cards that have a star cost") via the existing defensive helpers (handle null + X-cost). Predicate-only/effect-amount switches (DoesEffectContributeToCountCardFilter→false, GetCountCardFilterDynamicAmount→0, CountCardFilterSupportsAmount excluded) all correct by default, matching the CanBePlayed precedent. UI dropdowns (7), localization warmup, and preset Enum.TryParse auto-handle new members; appended at end so index==value keeps old presets valid.
Known edge: X-cost (HasStarCostX / EnergyCost.CostsX) cards report 0 → NOT counted as having a cost (one-line change if desired). Git hunk header mislabels the matcher edit as MatchesGrantCardFilters — git xfuncname artifact from the nested local funcs; edit is verifiably inside MatchesCountCardEffectFilter.
Next Step: User to launch game and confirm in-combat the +2 applies per star-cost card in hand; tune base-vs-current or X-cost handling if desired.

## 2026-06-13 - "Star Cost Effects" count filter does NOT mean "card costs stars" (user bug report)

Hypothesis: A card "Gain 3 Block, +2 per card in hand with a Star Cost" using Count-by-Cards + filter "Star Cost Effects" stays at base 3 because of a misconfiguration the user can fix.
Finding: Mixed / conflicting evidence — the no-bonus behavior is CORRECT (not a code bug), the user's mental model is FALSE, and their goal is currently UNACHIEVABLE (missing feature, not a setting).
Evidence: CardEditorExtraEffects.cs enum CardExtraEffectCountCardFilter (1102-1142, last = CanBePlayed=37; no "HasStarCost"); label map StarCostModifier => "Star Cost Effects" (3911); predicate PassesCardMatchFilter case StarCostModifier => HasAnyExtraEffectKind(CardStarCostsLess, CardTypeStarCostsLess) (35285-35286) and effect-level twin (34524); aggregation GetCountAggregationAmount CardCount => 1 per match (34306-34314); BaseStarCost/CurrentStarCost modes sum star pips not card count (34311-34312); card.CurrentStarCost / card.BaseStarCost exist (31207, 7599).
Reason: "Star Cost Effects" matches cards whose OWN effect modifies other cards' star costs (the "Card Star Cost Changes" effect kind), NOT cards that cost stars to play. A normal star-costing card has no such effect, so the hand count is 0 and the +2 never applies. The user's "Lose 1 Star" test card uses kind LoseStars (108), also not in the filter set, so it correctly fails too. No existing filter tests star cost > 0; the only star-cost-aware path (BaseStarCost aggregation) sums pips (a 3-star card = +6, not +2), so it cannot express "flat +2 per star-cost card."
Next Step: Add filter CardExtraEffectCountCardFilter.HasStarCost (= card.CurrentStarCost > 0; optionally HasEnergyCost) — one enum value + one PassesCardMatchFilter case + label + loc. Trivial. Until then, tell the user it is a missing feature, not their settings.

## 2026-06-11 - Adversarial re-review of shared-attack-context fix wave

Hypothesis: The post-rejection fixes (inline AsyncLocal scope, powered-row count, phase revert, empty-results guards, co-op participants guards) are now fully correct.
Finding: Partially true (2 refutations, rest confirmed).
Evidence: mods/card_editor/CardEditorExtraEffects.cs (12389-12972, 26602-26811, 27087-27094, 37128-37145), CardEditorMod.cs:2768-2786, CardEditorExtraEffectPower.cs:1518-1524/1618-1635, CardEditorExtraEffectTriggerPatches.cs:318-325, CardEditorCountdownEffectPower.cs:124-140, CardEditorTempStatTrackerPowers.cs:27-43; vanilla AttackContext.cs, AttackCommand.cs, VigorPower.cs, GigantificationPower.cs, CreatureCmd.cs:174, CombatManager.cs:945/1033-1038/1065-1084, DoubleDamagePower/BurstPower.
Reason: (R1) Powered-row count uses raw e.Target while execution maps Target->Self via GetEffectiveResolvedTarget on TargetType.Self cards: 2 "Target" damage rows on a Self-target card open a powered bracketing context whose rows all run Unpowered -> Vigor latched+burned for zero benefit (regression vs old per-row Unpowered attacks). (R2) In RunAfterCardPlayed the SharedAttackContextScope spans the power-add block; an AfterAttack-trigger power granted by the same play fires once off the play's own bracketing context at scope dispose (old code fired AfterAttacks before power adds; created-card path closes the scope before powers are added).
Next Step: (1) Count powered rows with GetEffectiveResolvedTarget(cardPlay, e.Target) != Self instead of raw e.Target; (2) dispose the shared scope right after the immediate-row loop in RunAfterCardPlayed, before Fatal/power-add reactions.

## 2026-06-11 - Delta verification of R1/R2/echo/PetOwner fixes (2nd adversarial pass)

Hypothesis: The just-applied fixes (effective-target powered count, two-step inline scope close, echo damageProps gating, PetOwner rng fallback) survive adversarial re-review.
Finding: Partially true (all five items hold as implemented; two residual edge gaps, neither a regression vs the refuted code).
Evidence: mods/card_editor/CardEditorExtraEffects.cs (12389-12612 RunAfterCardPlayed, 12640-12765 RunResolvedOnPlayEffectsDuringCardPlay, 12809-12869 count+scope, 12907-13010 ExecuteDamageRowInSharedContext, 26649-26725 ExecuteDynamicResultRepeatDamage, 26741/26811/27152 gating, 26882 GrantToCard divert, 37050-37090 ResolveTargets, 37183 GetEffectiveResolvedTarget); vanilla AttackContext.cs (_disposed guard, swallowed AfterAttack exceptions), AttackCommand.cs:418, Creature.cs:159, VigorPower.cs; CardEditorCreatedCardEffectSourceSupport.cs:370.
Reason: Count and every execute path now gate powered on identical predicates (GetEffectiveResolvedTarget==Self || UsesDamageResultAmountSource); scope close is structurally leak-free and double-dispose-safe (AttackContext.DisposeAsync idempotent, sets _disposed before awaiting). Residual: (a) count is static while TryResolveExecutableEffectAmount/ResolveRepeatCount are dynamic -> a play counted at 2 powered rows whose amounts resolve to 0 at runtime still opens+disposes a powered bracketing context and burns latched Vigor for zero hits; (b) nested borrowed-source invocation for the SAME CardPlay restores the outer still-open context on inner close, so the inner RunFatalForCreatedCardOnPlayIfNeeded damage rows can join the outer (undisposed) context, contrary to the "Fatal never joins the play's context" intent.
Next Step: Optionally guard RunFatalForCardPlayNow rows from joining any ambient context (ignore ambient when executing Fatal-trigger rows), and accept the static-count Vigor edge or re-check resolved amounts before opening the context.

## 2026-06-11 - Fixability study: override-row timing + 3 accepted edge cases (8-agent design/verify pass)

Hypothesis: The deferred override-row timing item and the three accepted edge cases (zero-hit Vigor burn, Fatal joining the open shared context, discount skipping co-op extra turns) are all genuinely fixable, not permanent limitations.
Finding: True (all four designs verified feasible; 4/4 survived adversarial attack).
Evidence: Vanilla CardModel.cs:1334/1499-1636 (OnPlayWrapper awaits OnPlay at 1563 before Enchantment/Affliction/Hook.AfterCardPlayed/routing), 579 flat CardModel subclasses with ZERO base.OnPlay() call sites; created-card precedent CardEditorMod.cs:2789-2825 already composes rows onto OnPlay's hot task; CardPlayHookPhase enum already plumbed (ImmediateRowsOnly opens/closes the shared context). VigorPower.cs:57-77/GigantificationPower.cs:55-75 apply bonuses even unlatched -> lazy JIT context creation is equal-or-closer to vanilla; only 3 vanilla BeforeAttack listeners exist. Feed/TheHunt/HandOfGreed/KnockoutBlow/Sunder run kill bonuses strictly after Execute() returns -> Fatal damage = separate attack (one ~10-line down-flowing mask in RunForCardPlayTrigger, no restore needed). CombatManager.cs:531/1185/1189: AfterPlayerTurnStart DOES fire on co-op extra turns, RoundNumber does NOT increment but per-player TurnNumber DOES -> CreatedTurnNumber stamp replaces CreatedRoundNumber.
Reason: (1) "Unachievable" applied only to the hook-postfix seam - per-card OnPlay postfixes (lazy-patched per override) are the proven created-card mechanism; 3 traps found+solved (reflective vanilla-OnPlay payload re-entry needs a suppression gate; borrowed-source marker pollution; idempotency check must not be owner-based - Acrobatics/Survivor/DaggerThrow OnPlay already owned by our HarmonyId). (2-4) small/medium concrete edit lists, all verified against source.
Bonus bugs found: (a) DESTRUCTIVE co-op bug - OnAfterPlayerTurnStart owner-mismatch branch DELETES other players' pending discounts every round (CardEditorCreatedCardsCostPatches.cs:650-654/712-717, Remove instead of skip); (b) PendingStarDiscount loop (:719-739) has no same-turn guard at all.
Next Step: Implement in order: discount-extra-turn + co-op deletion fix (small), Fatal mask (small), lazy Vigor context (medium), override OnPlay postfixes (medium, with the 3 verifier adjustments).

## 2026-06-11 - "Fix the unfixable" wave implemented (override OnPlay timing + 3 edges)

Hypothesis: The four verified designs (per-card OnPlay postfixes, lazy JIT shared context, Fatal ambient mask, CreatedTurnNumber discounts) can be implemented without regressions.
Finding: Partially true on first pass (1 must-fix found), True after fix (5-reviewer pass + focused re-verify all PASS).
Evidence: 5-agent adversarial workflow over the working tree + 1 focused re-verify agent; mods/card_editor/{CardEditorOverrideOnPlayPatches.cs (new), CardEditorExtraEffects.cs, CardEditorEffectExecutionAmountContext.cs, CardEditorCreatedCardsCostPatches.cs, CardEditorMod.cs, CardEditorOverrides.cs, CardEditorUiState.cs, CardEditorCreatedCardEffectSourceSupport.cs}; vanilla CardModel.cs:1499-1637, CombatManager.cs, AttackContext/AttackCommand, VigorPower, EffectScope flush semantics.
Reason: Must-fix was cross-phase session loss - the OnPlay/AfterCardPlayed split gave each phase a fresh execution-amount session, so reaction-time amount sources (self-scaling from damage dealt, end-of-play Fatal, triggered cost-less, selected-card chaining) resolved 0. Fixed by stashing the immediate phase's ROOT session per CardPlay (ConditionalWeakTable) and re-adopting it in the reactions phase; verified effective because EffectScope.Dispose flushes frame data into session-LEVEL dictionaries which survive in the stashed Session object. Key insight for the future: RootSessionScope.Dispose only nulls the AsyncLocal - the Session object and its dictionaries survive.
Bonus fixes shipped: destructive co-op discount deletion (owner-mismatch Remove -> skip), star-only schedules never ticking (early-return condition), star same-turn guard, dead-dealer phantom-hit guards in repeat-damage helpers, safe TryGetOwner at stamp sites, Init-time patch sweep documented as no-op (ModelDb not ready) with the real sweep at NMainMenu._Ready.
Next Step: In-game smoke test (multi-row Gigantification card, Fatal-row card, co-op extra turn discounts, override with self-scaling row sourced from damage).

## 2026-06-11 - Rainbow Glitter tiny top-left window root cause

Hypothesis: The glitter pattern renders in a small fixed top-left region because the shader maps coordinates from screen pixels (FRAGCOORD) instead of the card-tracking UV space all other finishes use.
Finding: True.
Evidence: mods/card_editor/CardEditorCardFinishPatches.cs - Rainbow Glitter was the ONLY shader using FRAGCOORD (old art_rect_uv helper dividing FRAGCOORD by card_effect_screen_origin/size); all 10 other finish shaders use effect_uv = card_effect_uv_origin + UV * card_effect_uv_scale. ApplyLocalArtSpace snapshots the global rect ONCE at sync time - pre-layout it degenerates to origin (0,0) with the 300x422 fallback = the exact tiny top-left window; even a correct snapshot would go stale on every card move/hover/scale.
Reason: FRAGCOORD is window-pixel space; a one-shot screen-rect uniform cannot track an animating Control. UV on the portrait TextureRect (with uv_origin=0/uv_scale=1 from ApplyLocalArtSpace) IS the art-rect coordinate, updated for free by the renderer.
Next Step: In-game check: glitter covers the full art, follows the card in hand/hover/reward screens, matches other finishes. Lesson recorded: finish shaders must stay in UV space; never coordinate-map via FRAGCOORD + snapshotted rect uniforms.

## 2026-06-11 - Auto Action x Whenever ungated (universal, loop-safe)

Hypothesis: Auto Action rows can be ungated for Whenever triggers by routing them through the existing power pipeline, with a chain-scoped guard bounding cross-effect recursion.
Finding: True (design verified by 2-agent pass, implementation by 3 adversarial reviewers; one consensus must-fix found and fixed, plus an exponential stack-merge growth path closed).
Evidence: The gate was 3 pieces: SupportsAsPower exclusions (the root - blocked install/routing/UI), the UI snap-back (already self-resolving once SupportsAsPower passes: it silently ticks Power), and a missing ExecuteEffect dispatch (auto rows reaching it were silent no-ops that still CONSUMED Use Limit). Re-entrancy was already proven: PlayCardFromPile was a legal Whenever power calling CardCmd.AutoPlay from hook continuations - and ran UNGUARDED (real runaway shipping before this work, now retroactively capped).
Implementation: storage-time NormalizeSelfPileAutoEffect in AddPowerEffects (unified-kind keys); ExecuteEffectCore dispatch -> TryRunSelfPileAutoEffect (host via UsesSourceCardForImmediatePowerExecution + EffectSourceContext for scheduler clones); IsPowerEffect skips in all card-hosted sweeps (no double-fire); chain guard in CardEditorAutoPlayLoopGuard (AsyncLocal parent-linked path nodes + shared totals: same key max 3/path, depth 12, 64 activations/chain; consumeActivation made real for precounted inner entries); SINGLE Use Limit consumption point inside TryRunSelfPileAutoEffect AFTER all pile/position/condition gates (power rows skip the generic ExecuteEffect consume - was burning uses on non-matching trigger fires; card-hosted rows now consume once AND actually gate, previously display-only/double-fast); auto-action power entries do NOT merge-stack (replay refreshes - stacking doubled per event with Allow Self Trigger on); card text emits action-clause-only under ApplyPowerTriggerPrefix.
Reason: All 3 unified variants + 2 legacy kinds funnel through one runner; all 20 count events + every other power trigger funnel through ExecuteOrSchedulePowerEffect -> universal by construction.
Next Step: In-game: "Whenever a card is drawn, if this is in your discard pile, play it" (fires once per draw, chain-capped if self-amplifying); Use Limit 2/turn gates only on matching activations; legacy OnDraw/turn-boundary auto rows unchanged.

## 2026-06-12 - Adversarial review: "Triggering Card" unlock (uncommitted vs 97a9a13)

Hypothesis: The ThisCard/"Triggering Card" widening lets a power row "Whenever you discard a card, IT gains Sly" work with zero manual picking, without breaking other kinds, save/load, or card text.
Finding: Partially true (headline build works end-to-end; no crashes; but the new power-ThisCard text branch is trigger-blind, Generated rows silently change executionPlay.Card for ALL kinds, FetchSpecificCardToHand gets a meaningless ThisCard option, and several formatters lack ThisCard/Top/Bottom branches).
Evidence: CardEditorExtraEffectPower.cs:1074-1079,1206-1235,980-1006,1021-1042; CardEditorExtraEffects.cs:5399-5424,29043-29073,29151-29156,30302-30308,30012-30115,20029-20046,16340-16371,34840-34843; NCardEditorPopup.cs:22321-22356,25566,25596-25609,25973; CardEditorExtraEffectScheduler.cs:93,313-332.
Reason: Runtime resolution verified by tracing AfterCardDiscarded -> TriggerCountEvent(triggeringCard) -> ExecuteOrSchedulePowerEffect -> executionPlay.Card -> GrantKeywordToCards source-card append (no tickbox needed; pile is NOT a still-there guard). Text branch keys only on IsPowerEffect, so non-card triggers (StartOfTurn etc.) render "it gains" while acting on the HOST card.
Next Step: Make FormatGrantKeywordToPile's power ThisCard branch trigger-aware (reuse PowerTriggerProvidesTriggeringCard + ThisCardSelectionUsesExecutionCard); gate Fetch out of the ThisCard insert; fix Select((int)All) index-vs-id at NCardEditorPopup.cs:25973/25983.

## 2026-06-12 - "Whenever you discard a card, it gains Sly" unlocked (Triggering Card)

Hypothesis: The runtime already delivered the event card to Whenever power rows; only UI selection exposure was missing.
Finding: True (2-agent research + adversarial implementation review).
Evidence: Vanilla discards append to the BOTTOM of the discard pile (CardPileCmd.Add default position) - the community's Top Offer attempt grabbed the oldest card. The Whenever pipeline sets executionPlay.Card = triggeringCard for kinds not forcing the host; the ThisCard selection mode (existing, save-safe, zero selector UI) auto-includes the source card as candidate WITHOUT the "Include this card" tickbox; GrantKeywordToCards receives cardPlay.Card. The ONLY blocker: RefreshMoveSelectionModeOptions never offered ThisCard for GrantKeywordToPile (only DelayedPileAction/RemoveCardsFromDeck).
Shipped: ThisCard offered for all SupportsIncludingSourceCardInSelection kinds (except Fetch - id-based, mode-blind), labeled "Triggering Card" when power + card-carrying trigger + a kind that passes cardPlay.Card (NOT the EffectSourceContext-first kinds DelayedPileAction/TransformCards/PlayCardFromPile/ConsumeCardValue - those resolve to the HOST on power rows); Top/Bottom exposed for the card-action family (honest positional picks - "bottom of discard pile" = newest discard); trigger-aware grant text ("Whenever you discard a card, it gains Sly." / "This card gains..."); Generated count event now passes triggeringCard (fixes Generated card filters that never fired; aligns with Played/Drawn/Discarded/Exhausted - NOTE: old presets with non-host kinds on Generated rows now act on the generated card instead of the host, accepted as the correct universal semantics); adjacent pre-existing Select(id-as-index) bug fixed at the future-matching-cards coercion (would have shifted targets with the new insert).
Documented edges: scheduled (non-Immediate) ThisCard acts on the host snapshot (graceful no-op for granting); the Card Source pile dropdown is NOT a "still there" guard for ThisCard (the event card is granted even if it moved); six formatters still lack ThisCard/Top/Bottom text branches (cosmetic, listed in review).
Next Step: In-game: power "Whenever you discard a card" + Grant Keyword Sly + Mode "Triggering Card" - each discarded card gains Sly with no picker.

## 2026-06-12 - Adversarial review: ThisCard/Top/Bottom text branches (8 formatters)

Hypothesis: The text-only diff covers every selection mode the widened dropdowns can produce, with correct branch order, grammar, power-prefix lowercasing, and the exact ThisCardSelectionUsesExecutionCard kind list.
Finding: Partially true (one coverage gap).
Evidence: git diff vs d030bda; CardEditorExtraEffects.cs FormatMoveCardsBetweenPiles 19671-19689, FormatUpgradeCardsInPile 19995-20007, UsesEventCardThisCardWording 20054-20058, FormatGrantKeywordToPile 20078-20090, FormatDiscardCards 20177-20189, FormatExhaustCards 20232-20244, FormatTransformCards 20300-20312, FormatCopyCardsFromPileToDeck 20622-20634, FormatSelectCardsFromPile 20731-20743, FormatRemoveCardsFromDeck 20783-20788, FormatPlayCardFromPile 21039-21116, BuildDelayedPileSelectionText 19847-19859, FormatConsumeCardValue 21128-21131, ApplyPowerTriggerPrefix 16340-16342, kind lists 3475-3508, 29130-29152; NCardEditorPopup.cs 22272-22335, 25568.
Reason: All 8 edited formatters verified correct (order, grammar, lowercase-after-prefix, wrapping AppendCardSelectionNote->BuildCostFilteredText). But PlayCardFromPile is in SupportsIncludingSourceCardInSelection AND its MoveSelectionModeSelect is visible, so ThisCard IS offered for it - and FormatPlayCardFromPile has no ThisCard branch: it falls to "Play a card from your hand." ConsumeCardValue/DelayedPileAction Top/Bottom claim verified TRUE via BuildDelayedPileSelectionText.
Next Step: Add a ThisCard branch to FormatPlayCardFromPile ("Play this card." host-card wording; kind is EffectSourceContext-first so no "it" variant), or gate PlayCardFromPile out of the ThisCard insert like Fetch.

## 2026-06-16 - Relic Editor: Custom Effects UI (reuse whole card popup)

Hypothesis: the card editor's effect UI can be reused for relics without re-implementing it or doing surgery on NCardEditorPopup.
Finding: True.
Evidence:
- AddExtraEffectRow is a private instance method woven through NCardEditorPopup (_extraEffectRows referenced in 20+ places) - not extractable cheaply.
- NCardEditorPopup.Create(CardModel, Action onApplied, useModalContainer) opens the whole popup on any card with an apply callback.
- NModalContainer is single-slot (Add rejects a 2nd modal). BUT Close clears the modal ONLY if _useModalContainer==true; with useModalContainer:false the popup just QueueFrees itself and is parented via NGame.Instance.AddChildSafely (proven by ShowPopup else-branch, line 35088). So the card popup can OVERLAY the relic editor without evicting it.
- Card effect round-trip goes through CardEditorOverrides: popup reads via TryGetEffectiveOverride(Get), apply writes via Set (both _isCreatedCard branches mirror to it). IsCreatedCardId requires entry prefix "CARD_EDITOR_CREATED_CARD" - the proxy (CardEditorRelicProxyCard) does NOT match, so it is treated as vanilla, writes only to CardEditorOverrides, and never appears in the created-cards list.
Reason: classified True because the architecture compiles (0 errors) and every coupling/edge (single-slot modal, created-card pollution, seed/readback path) was resolved by reading the source, not guessed.
What Changed:
- Phase 2: wired 6 relic triggers to game hooks (OnCombatStart/TurnStart/TurnEnd/CardPlayed/CombatEnd/DamageTaken). OnEnemyKilled/OnPickup deferred.
- Phase 3: NRelicEditorPopup gained a "Custom Effects" section - one row per trigger with an "Edit Effects (N)/Add Effects" button that opens the full card-effect editor on the proxy card, seeded/read-back via CardEditorOverrides; committed to RelicOverride.ExtraEffects in ApplyCurrentRelicEditsToStore.
Next Step: in-game test (user) - verify the overlay displays/blocks input, effects round-trip per trigger, and a relic with e.g. "OnCombatStart: gain Block" actually fires.

## 2026-06-16 - Relic effects: "share one component" via embedded reuse (architecture locked)

Hypothesis: relics can host the FULL card-effect UI with one shared source of truth, without a ~20k-line duplication and without changing the card editor's behavior.
Finding: True (architecture validated end-to-end; foundation built + compiling).
Evidence / why duplication was rejected: AddExtraEffectRow is 7,471 lines (14132-21603); ExtraEffectRow is 511 lines (~200 controls, ~40 KeywordTickbox); dependency closure is 400+ helpers. Full 1:1 copy ~= 20k lines + drift + dead card-only options. User chose "share one component".
Key viability facts:
- Initialize() (1457) is lightweight STATE-SETTING only (no UI build); layout consts (_fieldMinSize/_coreEffectDropdownWidth/_labelWidth) are static/const -> an embedded host needs almost no special init.
- BuildExtraEffectsUi(VBoxContainer) (11868) is the self-contained effects-section builder (_extraEffectsContainer field at 216).
- QueuePreviewUpdate is called 409x but funnels through QueuePreviewRefresh -> gating ONE method no-ops all preview work.
- NModalContainer is single-slot (Add rejects 2nd modal); useModalContainer:false popups free only themselves (Close at 35244) and are parented via NGame.Instance.AddChildSafely (ShowPopup else-branch 35088).
- BuildOverrideFromUi (31294-32908) effects loop (31740-32808) is CLEAN of overrideData; non-effect blocks are 31304-31736 and 32815-32905.
What Changed (DONE, additive, card path provably unchanged - git diff NCardEditorPopup = +19/-1, all behind _isEmbeddedEffectHost flag default false; builds 0 errors):
1. Added field `_isEmbeddedEffectHost`.
2. QueuePreviewRefresh early-returns when embedded.
3. BuildOverrideFromUi: moved Keywords into a gate; gated both non-effect regions with `if (!_isEmbeddedEffectHost)`. Embedded mode -> only ExtraEffects built.
Architecture (group-based, NOT per-row trigger): relic editor hosts one hidden NCardEditorPopup instance PER relic-trigger group; sets _isEmbeddedEffectHost=true, Initialize on the proxy card, calls BuildExtraEffectsUi into the relic menu container; relic trigger chosen at group level (per-row card trigger dropdown hidden in embedded mode); readback = host.BuildOverrideFromUi().ExtraEffects paired with the group trigger -> RelicEffectEntry.
Next Step (NOT yet done): (a) add internal embedded API on NCardEditorPopup: InitializeAsEmbeddedHost(proxyCard)+BuildEmbeddedEffectsUi(container)+ReadEmbeddedEffects(); (b) hide per-row TriggerSelect column when _isEmbeddedEffectHost; (c) optional kind-filter for relic-unsupported kinds; (d) rewrite NRelicEditorPopup Custom Effects section as trigger groups each embedding a host. Then user in-game test.

## 2026-06-16 - Relic full-parity effects: IMPLEMENTATION COMPLETE (builds, awaiting in-game test)

Finding: True - the embedded "share one component" implementation is complete and compiles (0 errors).
NCardEditorPopup (git diff +80/-1, additive, all behind _isEmbeddedEffectHost; card path output identical):
- _isEmbeddedEffectHost field; QueuePreviewRefresh + _Ready early-return when embedded.
- BuildOverrideFromUi non-effect regions gated -> embedded build = ExtraEffects only.
- Embedded API (internal): InitializeAsEmbeddedEffectHost(CardModel), BuildEmbeddedEffectsUi(VBoxContainer), LoadEmbeddedEffect(CardExtraEffect), ReadEmbeddedEffects()->List<CardExtraEffect>.
- Per-row trigger dropdown hidden when embedded.
NRelicEditorPopup (+240): Custom Effects section = trigger groups. Each group = relic-trigger dropdown + hidden NCardEditorPopup host (InitializeAsEmbeddedEffectHost on the proxy card, AddChild, BuildEmbeddedEffectsUi into the group container, LoadEmbeddedEffect per saved effect). "Add Effect Trigger" button; per-group Remove. ApplyCurrentRelicEditsToStore -> CollectEffectGroupEntries() reads each host.ReadEmbeddedEffects() paired with the group trigger -> RelicOverride.ExtraEffects.
UNTESTED RUNTIME RISKS (user tests in-game): (1) embedded host runs AddExtraEffectRow/BuildExtraEffectsUi outside the full popup - may NPE on popup state set only in EnsureUiBuilt; (2) BuildOverrideFromUi top calls CompletePendingExistingExtraEffectRowsNow/CompleteDeferredCreatedEffectValueRowsNow before the gates - may touch popup state; (3) layout/sizing of the embedded editor inside the relic scroll. Fix path: run game, open a relic, Add Effect Trigger, build an effect, Apply, check combat.
Next Step: user in-game test; iterate on any NPE/layout from the 3 risks above.

## 2026-06-16 - Relic effects deep-dive bug hunt: 10 confirmed, 10 FIXED (builds 0 errors)

Adversarial workflow (6 dimensions, per-finding verify): 17 candidates -> 10 confirmed (7 correctly refuted). All fixed + compile-verified. User tests in-game.
HIGH:
1. EnchantCard dropdown empty + dropped on save (embedded skips EnsureUiBuilt -> _enchantmentIds empty). FIX: new EnsureEnchantmentIdsLoaded() (NCardEditorPopup) called from PopulateExtraEffectEnchantmentSelect + GetSelectedExtraEffectEnchantmentId (lazy-load, mirrors powers).
2. Player-choice relic effects abandoned the paused action. FIX: CardEditorRelicEffects DispatchForPlayer + BeforeCombatStart loop now `bool completed = await Assign...; if(!completed && ctx.GameAction!=null) await ctx.GameAction.CompletionTask;` (matches canonical sites).
3. AddCopyOfThisCard/AddExactCopyOfThisCardToDeck on a relic cloned the blank proxy into the run deck (save corruption). FIX: guard `if (cardPlay?.Card is CardEditorRelicProxyCard) return;` at top of AddExactCopiesOfThisCardToDeck + AddCopiesOfThisCard (CardEditorExtraEffects).
4. Card-picker overlay parented to the invisible embedded host -> never rendered + soft-lock. FIX: new AddOverlayChild() routes overlays to the visible host (GetParent) when embedded; all 7 AddChild(overlay) sites now use it (card path identical).
MEDIUM:
5. Saved trigger outside ActiveRelicTriggers mislabeled/never fired. FIX: AddEffectGroup normalizes trigger to ActiveRelicTriggers[0] when selIndex<0.
6. OnCardPlayed fired for all players in MP (and OnTurnStart). FIX: new WrapForOnePlayer; AfterCardPlayed scopes to cardPlay.Card.Owner, AfterPlayerTurnStart scopes to player.
7. Proxy card leaked into CombatState._allCards each trigger. FIX: combatState.RemoveCard(proxy) after dispatch in RunRelicTrigger.
LOW:
9. Trigger parse case-sensitive + accepted out-of-range numerics. FIX: Enum.TryParse(ignoreCase:true) && Enum.IsDefined in ParseEffectEntries.
10. Hidden "As Power" toggle made relic effects one-shot. FIX: ReadEmbeddedEffects forces e.AsPower=false.
Diff (card path provably unchanged, all behind _isEmbeddedEffectHost): NCardEditorPopup +127/-8 (8 = Keywords move + 7 overlay-call reroutes), CardEditorExtraEffects +10, CardEditorRelicOverrides +68/-3, NRelicEditorPopup +247. CardEditorRelicEffects (new file) updated.
Refuted (not bugs, sound reasoning): proxy-override double-load, host name dups (cosmetic), cross-group contamination, GetById-throws, OnCombatEnd timing, OnPickup resave, unfiltered kind dropdown (cosmetic dead options).
Next Step: user in-game test of the full feature + these fixes.

## 2026-06-16 - Relic effects UX/usability pass: 18 confirmed, fixed the clear wins (builds 0 errors)

Adversarial UX workflow (6 dimensions, per-finding verify): 29 candidates -> 18 confirmed (11 refuted; one refutation corrected my own assumption - the row-width overflow is real but the "dead column gap" was correctly refuted).
FIXED (9 fixes covering 12 confirmed; card path provably unchanged - all NCardEditorPopup changes gated on _isEmbeddedEffectHost):
- Layout overflow (#1): widened relic PanelSize 980x660 -> 1180x720 so full effect rows (~670px) fit the settings column without a horizontal scrollbar.
- One-group-per-trigger (#2 silent merge on save/reopen, #3 Add-button duplicate, #4 dropdown duplicate): FirstUnusedTrigger now nullable; added RefreshGroupTriggerOptions() that SetItemDisabled on already-used triggers in every group dropdown and disables "Add Effect Trigger" when all 6 used; called after add/remove and on trigger change.
- Help autowrap (#13): CreateMutedLabel sets AutowrapMode.WordSmart (all muted help wraps).
- Group frame (#7): groupPanel gets CreateInnerStyle() stylebox -> each trigger renders as a distinct inset card.
- Redundant heading (#6): BuildExtraEffectsUi skips the "Extra Effects" section label when embedded.
- Dead control "Add Effect Source" (#9,#10): hidden when embedded (card-only RunEffectSourceCard picker).
- Dead control "As Power" (#11): tickbox hidden when embedded (value was force-discarded -> misleading).
- Card-only kind filter (#12): new RelicUnsupportedEffectKinds denylist (AddCopyOfThisCard, AddExactCopyOfThisCardToDeck, CardCostsLess, CardStarCostsLess, SelfScaling, PersistentSelfScaling, AutoPlaySelfFromPile, RunEffectSourceCard) skipped from the relic kind dropdown; conservative (ambiguous-but-harmless kinds kept).
DEFERRED (documented, lower value / needs in-game verify): empty group vanishes on reopen (#5/#14 - would need a persisted-triggers data-model change; minor); hidden-trigger column micro-alignment (#8 - contested confirmed-vs-refuted, cosmetic, verify in-game); card-play-only target options offered (#16 - low, benign fallback); per-group title/index (#17 - "When:" dropdown serves as title); multiple pickers can stack (#18 - contrived).
Diff: NCardEditorPopup +190/-42 (all embedded-gated), NRelicEditorPopup +292/-1.
Next Step: user in-game test of the relic menu UX.

## 2026-06-16 - Relic effects: fixed the 4 previously-deferred UX items (builds 0 errors)

- Empty group vanishes on reopen (#5/#14): added RelicOverride.EffectTriggers (List<RelicTriggerKind>) + DTO string round-trip (ParseTriggers with ignoreCase+IsDefined), Clone, and IsEmpty now counts it. ApplyCurrentRelicEditsToStore persists every group's trigger (even empty). LoadExistingEffectGroups now builds groups from EffectTriggers (authoritative) unioned with effect-derived triggers, falling back to effect-derived for legacy overrides. Empty trigger groups now survive a reopen.
- Card-play-only target options (#16): ConfigureExtraEffectTargets strips Target + EventTarget when _isEmbeddedEffectHost (they resolve to a fallback with no card-play/hovered/event context); default-target fixup also runs in embedded mode so a damage effect defaults to RandomEnemy/Self instead of an inapplicable Target. Conservative denylist (kept ally/player targets that may be valid in MP).
- Group titles (#17): each group now has a 24px trigger-name heading (CreateHeading) above the "When:" row, updated live on trigger change - distinct scannable anchor per group.
- Picker stacking (#18): AddOverlayChild now guards at the shared parent (relic editor) via an EmbeddedOverlayMetaKey marker - opening a picker frees any already-open one, so embedded group pickers can't stack.
All gated on _isEmbeddedEffectHost (card editor behavior unchanged) except the additive EffectTriggers field. Deferred remaining: only #8 (hidden-trigger column micro-alignment - contested confirmed-vs-refuted, cosmetic, verify in-game).
Next Step: user in-game test.

## 2026-06-16 - Multiplayer sync deep dive: WHY it fails + robust fix design (diagnosis only, not yet implemented)

Hypothesis: the sync feature fails because "players' cards don't match" and the snapshot is off-by-default.
Finding: Partially true / reframed. Default is ON (MultiplayerSyncEnabled=true). The real causes are TIMING + DETERMINISM, not coverage.
Evidence (game source + mod, via 4-probe workflow w3ps0pjo3):
- Desync = host-authoritative XxHash32 over NetFullCombatState (HP/block/powers/energy/stars/gold, 5 card piles BY id+upgrade+SavedProperties+enchantment, relics, orbs, RNG seeds+counters, last action id + hook id). Cards hashed by IDENTITY not stats (SerializableCard.cs). Per-action, FIRST mismatch = immediate hard kick + abandon run, no resync (ChecksumTracker.cs:118-199). Also checked at event-room + rest-site exits.
- MP is deterministic LOCKSTEP: only actions+card-index cross the wire; each peer re-simulates from a shared seed. Starting deck built synchronously at Player.CreateForNewRun BEFORE Launch; LobbyBeginRunMessage carries no deck. RNG = shared seeded RunRngSet/PlayerRngSet; effects must draw in identical order/count. The all-peers-ready barrier is IStartRunLobbyListener.BeginRun (StartRunLobby.cs).
- Mod RNG SOURCE is correct (all combat picks use owner.RunState.Rng.Combat* seeded channels; no System.Random/Guid in combat). BUT effects run as LOCAL per-peer Harmony postfixes (re-simulated, not replicated); risks: rng==null silent fallbacks offset the stream, per-player trigger loops gated on LocalContext.NetId, PlayerChoice ChooseOne mode = unreplicated human choice.
ROOT CAUSES (layered): (1) NO BARRIER - snapshot applied async on client's first _Process after lobby bind, with nothing blocking deck-build/combat-start; first run desyncs. (2) Mid-run ApplyAllToExistingCards mutates live cards on only the client = itself a desync. (3) Custom effects re-simulate locally -> need identical defs + identical RNG/trigger order across peers. (4) Config: client applies snapshot regardless of own setting; host tickbox is the only real switch; persisted so a prior OFF sticks.
ROBUST FIX (design, not yet built): L1 apply snapshot at the BeginRun barrier + gate run-start/ready until client _lastAppliedSequence>0 (handshake + timeout). L2 freeze the definition set for the whole MP run; never apply mid-combat (defer to safe boundary). L3 determinism hardening: kill rng==null fallbacks, pin ?? fallback-owner RNG, ensure triggers fire identical count/order per peer, force ChooseOne Random (not PlayerChoice) in MP. L4 host-authoritative sync UX + push-on-ready/retry + detect peers missing the mod. L5 (last-resort opt-in) disable ChecksumTracker.IsEnabled consistently on host to suppress the kick - sacrifices correctness, players silently diverge; prevention >> suppression.
Next Step: confirm with user which layers to implement; L1+L2 directly solve "cards dont match"; L3 needed for custom effects to be MP-safe.

## 2026-06-16 - MP sync robust fix: implemented L1/L2/L4/L5 (+L3 verified), bug-hunted, 15 bugs found, fixed the real ones

IMPLEMENTED (all build 0 errors; changes confined to CardEditorMultiplayerSync.cs + CardEditorMultiplayerSettings.cs - card editor/effects untouched):
- L1 ready handshake: Harmony prefix on StartRunLobby.SetReady (+LoadRunLobby.SetReady) -> AllowClientReady(fireReadyTrue, ready). A client holds "ready" until its host snapshot is applied (or 8s timeout), so the run never starts on mismatched definitions. Re-fires via Update->FirePendingReadyIfNeeded.
- L2 freeze-for-run: IsRunActive()=RunManager.Instance.IsInProgress. CanEditSharedState() returns false during a run; OnSnapshotReceived + host Update broadcast skip during a run. Definitions frozen at run start. (Verified no deadlock: RunManager.State is null during the lobby so the snapshot applies pre-run; freeze self-releases at CleanUp.)
- L4: client re-requests snapshot every 2s until applied; timeout warning.
- L5 escape hatch: DisableDesyncProtection setting + Harmony prefix on ChecksumTracker.CompareChecksums (host-only) suppressing the kick; new "Disable Desync Protection" settings line. (Verified CompareChecksums is the single complete chokepoint for the kick chain.)
- L3 (effect determinism): NO code change needed - VERIFIED the mod's RNG already uses the game's seeded channels AND player-choices go through CardSelectCmd.FromChooseACardScreen which is fully MP-synced via RunManager.PlayerChoiceSynchronizer (same path vanilla Discover uses). So given matched definitions (L1/L2), effects are deterministic. Forcing ChooseOne->Random would have been a regression; correctly avoided. rng==null fallback confirmed unreachable+deterministic.
BUG HUNT (workflow wmyyfbbmc): 20 candidates -> 15 confirmed. FIXED: (1/5) stale ready-gate state leaking across sessions -> ClearPendingReady() on bind+detach + session check in FirePendingReadyIfNeeded; (2/3/9) client un-ready silently re-readied -> AllowClientReady clears pending on !ready; (4/11/15) LoadRunLobby ungated -> added LoadRunLobby.SetReady patch + delegate generalization; (6/10) client DisableDesyncProtection no-op -> greyed the tickbox on clients (host-only); (8) 8s stall vs sync-off -> short-circuit AllowClientReady when !MultiplayerSyncEnabled; (12) redundant snapshot broadcast on desync toggle -> dropped Revision++; (14) hover fallback text -> aligned with loc.
DEFERRED (low/cosmetic): (7) editor Apply/Reset silently no-op during a run with a misleading "host-controlled" log (popup stays open, no toast) - needs button-greying across ~10 sites; (13) desync settings line double-fires its handler (benign, idempotent, pre-existing pattern shared by all 5 lines).
REFUTED (not bugs): freeze-deadlock, checksum-patch-incomplete, L2-blocks-saved-run-apply, FirePendingReady-throw, rng-null determinism.
Next Step: user 2-peer in-game test (checksums are masked in singleplayer/testmode, so only a real Host+Client session validates this).

## 2026-06-16 - MP sync second bug hunt + cosmetic fix (editor-lock feedback)

COSMETIC FIX (the deferred bug 7): GetSharedStateLockReason() helper; card editor + relic editor now grey Apply/Reset + set a why-tooltip ("Card Editor is locked during a multiplayer run...") when editing is locked, instead of a silent no-op; all 5 editor block-site logs (card/relic/base-deck/preset) now report the accurate reason instead of the misleading "host-controlled".
SECOND HUNT (workflow w5egeyu83): 18 candidates -> 9 confirmed (3 refuted: controller-select unreachable, PersistenceSuspended-symmetry hypothetical, transient-disconnect safe). FIXED:
- MEDIUM (regression in the cosmetic fix): card-editor Apply/Reset greying was computed once in BuildUi, but the popup is a persistent/cached instance so reopen showed a stale state. FIX: store _applyButton/_resetButton fields + RefreshSharedStateLockUi() called from PreparePersistentPopupForOpen (every open). Relic editor unaffected (rebuilds per open).
- MEDIUM (pre-existing coverage gap): client SetSlotCountForNextRun only sets ConfiguredSlotCount (next launch), not active SlotCount; the slot card types are registered at launch clamped to the local count, so a higher host slot count can't be realized this session -> divergent created-card/reward pools -> desync. FIX: loud Log.Warn on the client when host CreatedCardSlotCount > client active SlotCount, advising raise Max Custom Cards + restart (full runtime re-registration is out of scope; next-launch alignment already queued).
- LOW: held-ready could double-fire SetReady(true) on a manual re-click racing the applied snapshot. FIX: ClearPendingReady() on the IsClientSnapshotApplied() short-circuit in AllowClientReady.
DEFERRED/NOTED (low, safe): (a) greying goes stale if a run STARTS/ENDS while the editor stays open (rare; block-site CanEditSharedState re-check makes it safe; would need a live run-state notification to open popups); (b) dead class CardEditorMultiplayerCreatedCardDto (~183 lines, unreferenced, superseded by CardEditorCreatedCardsStore.CreatedCardDto) - safe to delete, flagged; (c) settings tickbox double-applies via both the Toggled lambda and the OnTick/OnUntick patch - benign (idempotent setters), pre-existing pattern on all 5 lines.
All builds 0 errors. Verified relic ExtraEffects+EffectTriggers DO round-trip through the synced RelicOverrideDto (no coverage gap).
Next Step: user 2-peer in-game test.

## 2026-06-19 - Relic effects "missing" = stale deploy; deferred fixes (dead DTO, double-apply)

Hypothesis: The relic-effects UI not showing in the relic editor is a code bug (AddEffectsSection not running / early-return / not wired).
Finding: FALSE - stale-deployment problem, not a code bug.
Evidence:
- NRelicEditorPopup.Build():330 calls AddEffectsSection(settings) after number/text/pool; AddEffectsSection():702 unconditionally adds the "Custom Effects" heading + "Add Effect Trigger" button (no early-return / no throw for a relic with no effects).
- csproj only outputs to build\ (no post-build deploy). Live game loads ...\Steam\steamapps\common\Slay the Spire 2\mods\Card_editor\card_editor.dll dated 2026-06-14 (3,500,032 B). Repo staging older still: card_editor_pack 2026-06-06, built cfiles 2026-06-12. Fresh build = 2026-06-19 (3,841,536 B). The relic-effects feature was built AFTER 06-14, so the running DLL predates it.
Reason: Code-only feature (pure C#, hardcoded UI strings -> no .pck change needed) was never copied to the game's mod folder; the game kept loading a pre-feature DLL.
What Changed: Deployed fresh DLL to live game (kept backup card_editor.dll.bak-2026-06-14), card_editor_pack, and built cfiles. Relic Custom Effects section appears on next launch (bottom of relic editor).

Deferred fixes done this session (user: "Fix these btw"):
- Dead DTO removed: CardEditorMultiplayerCreatedCardDto (184 lines, unreferenced) deleted from CardEditorMultiplayerSync.cs via verified-boundary splice. Build 0 errors.
- Settings double-apply fixed: OnTick/OnUntick NBackgroundModeTickbox patches now suppress-only (return !TryGetMultiplayerSettingsLineKind); the Toggled lambda in EnsureSettingsTickboxLine is the single apply path. Also removed the now-dead AND divergent ApplyMultiplayerSettingsTick (it called MarkSettingsDirtyForBroadcast() whereas the lambda sets _forceImmediateBroadcast directly).
- Deferred item (a) run-start/end stale greying: judged effectively handled - both editors recompute the lock on open (card editor RefreshSharedStateLockUi in PreparePersistentPopupForOpen; relic editor rebuilds each open), and the editor is never open during the lobby->run / run->menu transition, so the while-open case is unreachable. No live-notification plumbing added.

Still open:
- Rarity dropdown greyed on first open of card editor until a "class" (card-type) swap. No literal .Disabled on _createdRaritySelect/_vanillaRaritySelect; retarget path DOES re-select rarity (BindCreatedBaseControlsForCurrentCard:1671). Suspected first-open refresh gap via the same-card fast path RetargetLocalizedSharedPopup:1522-1533 (returns without RefreshLocalizedSharedPopupControls). Needs in-game confirmation (created vs vanilla; screenshot) before touching the 38k-line editor.

Next Step: user restarts game, confirms relic Custom Effects section shows + clarifies rarity bug (created/vanilla + meaning of "swapping class").

## 2026-06-19 - Verified ChatGPT review (Notes/POTENTIAL_BUGS_2026-06-19.md, 5 items)

Hypothesis: ChatGPT's 5 flagged issues are all real bugs needing fixes.
Finding: Mixed - 1 real+fixed, 1 real-but-out-of-mod-scope, 1 unverifiable-statically (testable in-game), 2 not-bugs.
Evidence + verdicts:
- P1 client edit mid-run (CardEditorMultiplayerSync.OnEditRequestReceived): TRUE -> FIXED. Handler checked Host+SyncEnabled+AnyPlayer authority but NOT IsRunActive(), unlike OnSnapshotReceived / host-broadcast skip / CanEditSharedState. A mid-run client edit (AuthorityMode=AnyPlayer) would ApplyState on host + BroadcastSnapshotToReadyPeers -> desync. Added the same IsRunActive() freeze guard (verbose-logged). Verified compiled into DLL via UTF-16 (strings -e l) search; rebuilt + redeployed to live game + staging.
- P1 root solution build broken by scratch files: TRUE but OUT OF MOD SCOPE. .tmp_cardcmd.cs (48KB, gitignored), _tmp_NCardHolderHitbox.cs, _tmp_NGridCardHolder.cs at root; root csproj uses SDK default compile (no <Compile Remove>) so solution build fails. Mod builds fine via card_editor.csproj (the deploy path). NOT fixed - offered cleanup (move scratch out of compile glob, or <Compile Remove>).
- P2 relic proxy card registration: UNCONFIRMED statically. CardEditorRelicProxyCard : CardEditorCreatedCardBase (same base as working CardEditorCreatedCard01-30). RegisterCreatedCardsInPools is POOL registration (filters CardEditorCreatedCardNN by NAME) and correctly excludes the proxy (must never be pooled) - NOT the ModelDb type-registration path. The mod-loader/ModHelper assigning ModelDb ids is in a compiled assembly (not in decompiled source), so proxy auto-registration cannot be proven by reading. Reuses the working base by deliberate design (comment CardEditorRelicEffects.cs:26-28). DIRECTLY TESTABLE: open relic -> click Add Effect Trigger; works => proxy resolves; if it logs "Relic proxy card is not registered" (NRelicEditorPopup:833) => needs explicit registration. = #1 in-game check after deploy.
- Risk end-of-turn relic semantics: NOT a bug. Hook.AfterTurnEnd is SIDE-level (side param, no player) so Wrap (all players on CombatSide.Player) is the only option + correct for STS2 co-op shared-side turns; consistent with existing scheduled-effect iteration. OnTurnStart uses WrapForOnePlayer only because Hook.AfterTurnStart carries a player. Playtest-confirm, no change.
- Risk desync escape hatch broad: BY DESIGN (L5 Disable Desync Protection, user-enabled, gated). No change.
Next Step: user restarts, tests Add Effect Trigger (P2 gating check), reports; decide P1b scratch-file cleanup.

## 2026-06-19 - Fixed 4 reported bugs (Neow pool, grant-to-ally, self-damage display, osty hover)

Hypothesis: 4 user/forum-reported bugs are real and fixable.
Finding: True for 3 fully + B partially (B's AnyPlayer CARD-target crash is a game-level gap, deferred).
- A (relic removed from all pools still spawns via Neow): TRUE -> FIXED. Neow.GenerateInitialOptions uses a HARDCODED RelicOption<T> list, not the pools; all Neow offerings + NeowsBones gate on RelicModel.IsAllowedAtNeow. Added CardEditorRelicOverrides.IsRemovedFromAllPools (override exists + PoolKeys empty; editor only writes PoolKeys when != vanilla, so empty == deliberate) + a TargetMethods Postfix on every IsAllowedAtNeow impl forcing false for removed relics. Normal rewards/library/ancient already respected the override via GetUnlockedRelics.
- B (grant keyword to ally hand): grant always hit self, AnyPlayer crashed, text said "your hand". FIXED (functional + text): GrantKeywordToPile case now loops ResolveTargetPlayers (like GainGold) honoring Self/AnyAlly/AnyPlayer/AllAllies; description uses new target-aware GetCardPileLocationForTarget. MP-safe (shared RunState RNG / synced choice; enemy/non-player targets resolve to no grant). PARTIAL: the "any player" CARD target-type crash is a GAME targeting gap (CardModel.cs:1404 marks AnyAlly unplayable when <=1 player but has no AnyPlayer equivalent), exposed by the editor offering AnyPlayer. Needs the crash log to patch safely. Workaround: use AnyAlly card target (works) + AnyAlly/AnyPlayer effect target.
- C (self-damage "Take 5" shows attack-scaled number): TRUE -> FIXED. TryGetHookedMoveAmountPreview now returns false for non-osty DealDamage with Self/AnyPlayer/AnyAlly/AllAllies target -> preview shows enchanted base (no Strength/Vulnerable/Weak), matching flat runtime damage.
- D (osty attack damage not updating on enemy hover): TRUE -> FIXED. FormatLineForAmount condition (~15943) now includes OstyAction -> routes through TryGetScaledAmountText -> TryGetHookedMoveAmountPreview (already handles Osty: effectiveDealer = owner.Osty), recomputing Vulnerable/Lethality on hover.
Files: CardEditorExtraEffects.cs (B,C,D), CardEditorRelicOverrides.cs (A). Build 0 errors. Deployed live + staging; committed to main.
Next: user retest; for B's AnyPlayer crash, provide the exception/log so the game-level targeting gap can be patched.

## 2026-06-19 - Fixed Any-Player card-target crash (B#2)

Hypothesis: the 'any player' card-target crash is fixable safely.
Finding: True -> FIXED (via mapping, not a risky core patch).
Root cause: TargetType.AnyPlayer is only half-wired for COMBAT card targeting in the base game - NTargetManager.AllowedToTargetNode handles it (case at ~282) but CardModel.IsValidTarget falls through to 'return false' for any non-null AnyPlayer target (no AnyPlayer branch), and NCardPlay/NControllerCardPlay/NMouseCardPlay + the mod's CardEditorExtraEffectTargetingPatches only special-case AnyEnemy/AnyAlly. So a PLAYED AnyPlayer card has zero valid targets -> crash. AnyPlayer is only used by rest-site/special contexts via direct StartTargeting, never as a played combat card.
Fix: map AnyPlayer -> AnyAlly at the single source of a created card's target (CardEditorCreatedCards.TargetType getter), covering both the dynamic-identity and store paths, new and existing cards. AnyAlly is fully supported end to end; the effect-level target (CardExtraEffectTarget, fixed earlier via ResolveTargetPlayers) still restricts grants to player allies, so grant-to-ally keeps working. Chose this over patching ~5 core game UI methods (high risk to all cards).
Tradeoff: an AnyPlayer card target now behaves like AnyAlly (Osty hoverable in the targeting cursor), but clicking Osty grants nothing (effect filters to players). Build 0 errors; deployed live + staging; committed to main.
Next: user retest the grant-to-ally card with the AnyAlly (or formerly-AnyPlayer) target.
