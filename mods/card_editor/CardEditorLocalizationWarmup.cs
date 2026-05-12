using System;
using System.Diagnostics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorLocalizationWarmup
{
	private static readonly string[] _editorKeys = """
additionalMoveTo.label
autoAction.label
autoAction.variant.draw
autoAction.variant.play
baseDeck.bookmarkTooltip
branch.alternate
branch.blockLoss
branch.count
branch.countSource
branch.filter
branch.if
branch.intent
branch.lastTurns
branch.mode
branch.orb
branch.status
branch.threshold
branch.turnWindow
branch.type
button.add
button.addEffect
button.addEffectSourceInline
button.addScalingEffect
button.apply
button.baseDeck
button.cancel
button.delete
button.editBase
button.editUpgrade
button.load
button.reset
button.revertToVanilla
button.save
cardAction.label
cardAction.variant.copyPileToDeck
cardAction.variant.discard
cardAction.variant.exactCopyPileToDeck
cardAction.variant.exhaust
cardAction.variant.extraEffect
cardAction.variant.keyword
cardAction.variant.move
cardAction.variant.play
cardAction.variant.removeDeck
cardAction.variant.transform
cardAction.variant.upgradeDeck
cardAction.variant.upgradePile
cardCostsLess.kind.createdCards
cardCostsLess.kind.drawnCards
cardCostsLess.kind.matchingCardsEnergy
cardCostsLess.kind.matchingCardsStars
cardCostsLess.kind.thisCardEnergy
cardCostsLess.kind.thisCardStars
cardGeneration.label
cardGeneration.variant.AddCopyOfThisCard
cardGeneration.variant.AddExactCopyToDeck
cardGeneration.variant.AddRandomCard
cardGeneration.variant.AddSpecificCard
cardGeneration.variant.ChooseOneOfThreeCards
cardGeneration.variant.CreatedCardsAreUpgraded
cardGeneration.variant.CreatedCardsCostLess
cardGeneration.variant.FetchSpecificCard
cardGeneration.variant.PlayRandomCard
cardMatch.any
cardMatch.cardId
cardMatch.label
cardMatch.tag
cardMatch.tag.any
cardMatch.tagKind.custom
cardMatch.tagKind.vanilla
chooseOne.option1
chooseOne.option2
chooseOne.option3
costFilter.label
countFilter.blockOnly
countWindow.lastTurns.label
drawCost.label
drawCost.less
drawnFromPile.anyPile
drawnFromPile.discardPile
drawnFromPile.drawPile
drawnFromPile.exhaustPile
duration.thisCombat
duration.thisTurn
duration.xTurns
effect.disableOnUpgrade
effectKind.group.buffs
effectKind.group.debuffs
effectKind.group.health
effectKind.group.stats
effectList.disabledPrefix
effectList.empty
effectList.noTrigger
effectList.unknownEffect
effectMode.apply
effectMode.cleanse
effectMode.gain
effectMode.label
effectMode.lose
effectSourceNumbers.label
effectSubtype.label
field.affliction
field.art
field.artSearch
field.cardType
field.class
field.cosmeticAnimationPreset
field.cosmeticStylePreset
field.cosmeticVfxAttach
field.cosmeticVfxPreset
field.costReductionOnDraw
field.customText
field.customTextPlaceholder
field.customTextUpgraded
field.discardCount
field.enabled
field.enabledDanger
field.enchantment
field.enemyStrengthLoss
field.energyCost
field.energyCostChange
field.finish
field.finishCosmetic
field.fullArt
field.hideBodyText
field.hideCostNumber
field.hideCostOrb
field.hideNameBanner
field.hideNameText
field.hideAncientInnerBorder
field.hideTextBackground
field.hideTypeBadge
field.rarity
field.replayCount
field.replayCountChange
field.starCost
field.starCostChange
field.starsGained
field.target
field.targeting
field.title
field.titleCosmetic
filter.label
finish.none
finish.prismaticBandGlare
finish.purpleWavesOcean
finish.rainbowGlitterArt
finish.rainbowRareFoil
finishEditor.editButton
finishEditor.noParams
ignoreEffects.label
ignoreEffects.variant.IgnoreBlock
ignoreEffects.variant.IgnoreDamageCaps
ignoreEffects.variant.IgnoreDamageModifiers
ignoreEffects.variant.IgnoreDamageNegation
ignoreEffects.variant.IgnoreEnemyDamageReductions
intent.label
keywordGroup.label
keywordGroup.placeholder
label.costModifier
label.duration
label.every
label.powerTurns
label.resource
label.uses
multiplier.label
cardText.customKeyword.amount
cardText.doesNotConsumeVigor
cardText.doesNotConsumeSelfHp
cardText.doesNotConsumeStatStatus
cardText.doesNotConsumePowerStatus
cardText.selectionDescriptor.includeSource
cardText.selectionDescriptor.includeSource.note
cardText.gainStatusEqual.allEnemies
cardText.gainStatusEqual.factor.multiple
cardText.gainStatusEqual.randomEnemy
cardText.gainStatusEqual.self
cardText.gainStatusEqual.source.its
cardText.gainStatusEqual.source.self
cardText.gainStatusEqual.source.their
cardText.gainStatusEqual.target
cardText.loseStatusEqual.allEnemies
cardText.loseStatusEqual.factor.multiple
cardText.loseStatusEqual.randomEnemy
cardText.loseStatusEqual.self
cardText.loseStatusEqual.source.its
cardText.loseStatusEqual.source.self
cardText.loseStatusEqual.source.their
cardText.loseStatusEqual.target
popup.editorTitle
popup.upgradeEditorTitle
powerHost.cardOwner
powerHost.cardOwnerWatchOpponents
powerHost.effectTargets
powerHost.triggerTarget
powerTriggerFrom.self
powerTriggerFrom.anyEnemy
powerTriggerFrom.anyAlly
powerTriggerFrom.anyone
powerTargeting.allEnemies
powerTargeting.randomEnemy
powerTargeting.rememberEnemyRandomFallback
powerTargeting.rememberFirstEnemy
powerTargeting.rememberLastEnemy
powerTargeting.triggerTarget
presets.maxCustomCards
presets.namePlaceholder
presets.runAtStartup
presets.select
presets.sortByCharacter
row.grantCount
row.grantDuration
row.grantFilter
row.powerFilter
row.powerHost
row.powerTriggerFrom
row.powerTargeting
row.powerTiming
resourceConsumptionMode.SelfHpAndSelfDamage
resourceConsumptionMode.SpecificPowerStatus
resourceConsumptionMode.SpecificStatStatus
resourceConsumptionMode.Vigor
resourceConsumption.label
resourceConsumptionPower.label
resourceConsumptionStat.label
row.statusToStatusMode
row.selfScalingField
row.selfScalingOperation
row.selfScalingTarget
row.selfScalingTargetEffect
row.scalingStageCondition
row.scalingStageEffect
row.statusBigIcon
row.statusIcon
row.triggering
scaling.includeBase
section.advancedProperties
section.cardPreview
section.coreEffect
section.cosmetics
section.creator
section.customTags
section.editor
section.effectConfiguration
section.effectList
section.extraEffects
section.keywords
section.numberChanges
section.numbers
section.scalingEffects
selectionSource.label
section.tags
section.vanillaEffects
selfScaling.field.amount
selfScaling.field.duration
selfScaling.field.repeat
selfScaling.field.secondaryAmount
selfScaling.field.threshold
selfScaling.operation.decrease
selfScaling.operation.increase
selfScaling.target.baseBlock
selfScaling.target.baseDamage
selfScaling.target.effectRowAmount
selfScaling.target.none
statusToStatus.mode.gain
statusToStatus.mode.lose
tooltip.resourceConsumptionStat
tooltip.resourceConsumptionPower
tooltip.statusToStatusMode
tooltip.scalingStageCondition
tooltip.scalingStageEffect
cardText.scalingStage
effectKind.ScalingStage
effectKind.PersistentSelfScaling
effectKind.PersistentTargetCardMutation
effectKind.SelfScaling
effectKind.TargetCardMutation
status.label
statusIcon.mode.auto
statusIcon.mode.baseGame
statusIcon.mode.custom
submenu.title
targetType.Default
threshold.label
timing.label
timing.mode.turnBoundary
timing.offset.nextTurn
timing.offset.thisTurn
tooltip.additionalMoveToDiscard
tooltip.additionalMoveToDraw
tooltip.additionalMoveToExhaust
tooltip.additionalMoveToHand
tooltip.affectedCardsFilter
tooltip.affectedCardsPool
tooltip.affectedCardsType
tooltip.applyPower
tooltip.autoAction
tooltip.blockLostCountingMode
tooltip.branch
tooltip.branchBlockLostCountingMode
tooltip.branchCondition
tooltip.branchConditionType
tooltip.branchCountConditionAmount
tooltip.branchCountEvent
tooltip.branchCountExcludeSource
tooltip.branchCountWindow
tooltip.branchCountWindowInclusion
tooltip.branchEffectSourceId
tooltip.branchEnemyIntent
tooltip.branchEnemyStatus
tooltip.branchMode
tooltip.cardAction
tooltip.cardCostsLessKind
tooltip.cardCostsLessMode
tooltip.cardCostsLessModifier
tooltip.cardGeneration
tooltip.cardMatchMode
tooltip.chooseOne.option
tooltip.conditionalBonusAmount
tooltip.conditionalBonusCondition
tooltip.conditionalBonusEnemyIntent
tooltip.conditionalBonusEnemyStatus
tooltip.costFilter
tooltip.costFilterMax
tooltip.costFilterMode
tooltip.countComparison
tooltip.countConditionAmount
tooltip.countEnemyIntent
tooltip.countEnemyStatus
tooltip.countEvent
tooltip.countEvent.inPile
tooltip.countExcludeSource
tooltip.countFilter
tooltip.countMode
tooltip.countOrbSelection
tooltip.countOrbType
tooltip.countPile
tooltip.countWindowInclusion
tooltip.createdCardsCostResource
tooltip.customTag
tooltip.disableOnUpgrade
tooltip.discardCountOverride
tooltip.drawCostLess
tooltip.drawCostLessAmount
tooltip.drawCostLessAmountUpgrade
tooltip.includeSourceCard
tooltip.drawTargetFilter
tooltip.drawTargetPool
tooltip.drawTargetType
tooltip.drawnFromPile
tooltip.duration
tooltip.liveNumberTokens
tooltip.effect.moveDown
tooltip.effect.moveUp
tooltip.effect.remove
tooltip.effectMode
tooltip.effectSource.moveDown
tooltip.effectSource.moveUp
tooltip.effectSubtype
tooltip.enchantment
tooltip.enchantmentDuration
tooltip.enchantmentTurns
tooltip.generatedCardType
tooltip.generatedCustomTag
tooltip.grant
tooltip.grantCount
tooltip.grantCountX
tooltip.grantFilter
tooltip.grantPile
tooltip.grantPool
tooltip.grantType
tooltip.grantedKeyword
tooltip.hidePresets
tooltip.ignoreEffects
tooltip.keywordGroup
tooltip.matchCardId
tooltip.matchCustomTag
tooltip.matchTagKind
tooltip.matchVanillaTag
tooltip.moveFromPile
tooltip.moveToPile
tooltip.moveToPosition
tooltip.multiplierStat
tooltip.orbAction
tooltip.orbFollowUp
tooltip.orbScope
tooltip.orbSelection
tooltip.orbType
tooltip.ostyAction
tooltip.power
tooltip.powerCountEnemyStatus
tooltip.powerCountEvent
tooltip.powerHost
tooltip.powerHost.cardOwner
tooltip.powerHost.cardOwnerWatchOpponents
tooltip.powerHost.effectTargets
tooltip.powerHost.triggerTarget
tooltip.powerTriggerFrom
tooltip.powerTargeting
tooltip.resourceConsumption
tooltip.removeCustomTag
tooltip.repeatCount
tooltip.repeatX
tooltip.resetDefault
tooltip.scalingIncludeBase
tooltip.selectPresetFirst
tooltip.selectedCardsFilter
tooltip.selectedCardsPool
tooltip.selectedCardsType
tooltip.selectionMode
tooltip.showPresets
tooltip.specificCardId
tooltip.statusCustomBigIcon
tooltip.statusCustomPackedIcon
tooltip.statusIconMode
tooltip.statusIconPower
tooltip.timingEdge
tooltip.timingMode
tooltip.timingOffset
tooltip.timingSide
tooltip.transformMode
tooltip.triggerCardFilter
tooltip.triggerCardPool
tooltip.triggerCardType
tooltip.triggerEveryN
tooltip.triggerMaxFires
tooltip.triggerMaxTurns
tooltip.turnBoundary.edge
tooltip.turnBoundary.location
tooltip.turnBoundary.side
tooltip.turnsThisCombat
tooltip.upgradeAuraDuration
tooltip.upgradeAuraDurationTurns
tooltip.upgradeDeltaAmount
tooltip.upgradeDuration
tooltip.upgradeDurationTurns
tooltip.upgradePile
tooltip.upgradeVariant
transform.label
turnBoundary.edge.end
turnBoundary.edge.endAfterDiscard
turnBoundary.edge.endBeforeDiscard
turnBoundary.edge.start
turnBoundary.edge.startAfterDraw
turnBoundary.edge.startBeforeDraw
cardText.powerTrigger.whenActorAction
cardText.powerTrigger.everyNActorAction
cardText.powerTrigger.actor.self
cardText.powerTrigger.actor.anyEnemy
cardText.powerTrigger.actor.anyAlly
cardText.powerTrigger.actor.anyone
turnBoundary.label
turnBoundary.location.any
turnBoundary.location.discard
turnBoundary.location.draw
turnBoundary.location.exhaust
turnBoundary.location.hand
turnBoundary.side.both
turnBoundary.side.enemy
turnBoundary.side.your
ui.blockLostCountingMode
ui.blockLostCountingMode.damageAndEffects
ui.blockLostCountingMode.includeBetweenTurns
ui.branch
ui.cardPicker.button
ui.cardPicker.title
ui.cardPicker.tooltip
ui.close
ui.conditionalBonus
ui.count
ui.count.excludeSource
ui.drawTarget
ui.enchantment
ui.grant
ui.noEffectSources
ui.orb
ui.power
ui.powerCountEnemyStatus
ui.powerCountEvent
ui.remove
ui.repeat
ui.scaling
ui.search
ui.search.placeholder
ui.sort
ui.turnWindow.includingThisTurn
ui.turnWindow.previousOnly
ui.turnWindowMode
upgradeVariant.CardsInPilesAura
upgradeVariant.CreatedByThisCard
upgradeVariant.CreatedCardsAura
value.customPrefix
value.default
value.noOverride
value.none
""".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	public static void WarmCurrentLanguage()
	{
		if (LocManager.Instance == null)
		{
			return;
		}

		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			CardEditorLoc.Prewarm(_editorKeys);
			WarmKeywordTitles();
			WarmEnumLabels();
			CardEditorMod.VerboseLog($"[CardEditor] Localization warmup complete: lang={LocManager.Instance.Language} keys={_editorKeys.Length} time={stopwatch.ElapsedMilliseconds}ms");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Localization warmup failed: {ex}");
		}
	}

	private static void WarmKeywordTitles()
	{
		foreach (CardKeyword keyword in Enum.GetValues<CardKeyword>())
		{
			if (keyword == CardKeyword.None)
			{
				continue;
			}

			try
			{
				string upper = keyword.ToString().ToUpperInvariant();
				_ = LocString.GetIfExists("card_keywords", upper + ".title")?.GetFormattedText();
			}
			catch
			{
			}
		}
	}

	private static void WarmEnumLabels()
	{
		WarmEnum<CardType>(type => _ = CardEditorLoc.Enum("cardType", type, type.ToString()), type => type != CardType.None);
		WarmEnum<TargetType>(target => _ = CardEditorLoc.Enum("targetType", target, target.ToString()), target => target != TargetType.None);
		WarmEnum<CardRarity>(rarity => _ = CardEditorLoc.Enum("rarity", rarity, rarity.ToString()), rarity => rarity != CardRarity.None);
		WarmEnum<CardEditorCreatedCardPool>(pool => _ = CardEditorLoc.Enum("creatorPool", pool, pool.ToString()));

		WarmEnum<CardEditorCosmeticVfxPreset>(preset => _ = CardEditorCosmetics.VfxPresetLabel(preset));
		WarmEnum<CardEditorCosmeticAnimationPreset>(preset => _ = CardEditorCosmetics.AnimationPresetLabel(preset));
		WarmEnum<CardEditorCosmeticStylePreset>(preset => _ = CardEditorCosmetics.StylePresetLabel(preset));
		WarmEnum<CardEditorCosmeticAttach>(attach => _ = CardEditorCosmetics.AttachLabel(attach));

		foreach (CardExtraEffectDefinition definition in CardEditorExtraEffects.Definitions)
		{
			_ = CardEditorLoc.Enum("effectKind", definition.Kind, definition.Label);
		}

		WarmEnum<CardExtraEffectTarget>(target => _ = CardEditorExtraEffects.TargetLabel(target));
		WarmEnum<CardExtraEffectTrigger>(trigger =>
		{
			_ = CardEditorExtraEffects.TriggerLabel(trigger);
			_ = CardEditorExtraEffects.TriggerLabel(trigger, asPower: false);
			_ = CardEditorExtraEffects.TriggerLabel(trigger, asPower: true);
			_ = CardEditorLoc.Enum("triggerPrefix", trigger, trigger.ToString());
		});
		WarmEnum<CardExtraEffectCountEvent>(ev =>
		{
			_ = CardEditorExtraEffects.CountEventLabel(ev);
			_ = CardEditorLoc.Enum("historyVerbPresent", ev, ev.ToString());
			_ = CardEditorLoc.Enum("historyVerbPast", ev, ev.ToString());
			_ = CardEditorLoc.Enum("historyVerbPerfect", ev, ev.ToString());
		});
		WarmEnum<CardExtraEffectScaleMode>(mode => _ = CardEditorExtraEffects.ScaleModeLabel(mode));
		WarmEnum<CardExtraEffectCountComparison>(comparison => _ = CardEditorExtraEffects.CountComparisonLabel(comparison));
		WarmEnum<CardExtraEffectCountWindow>(window => _ = CardEditorExtraEffects.CountWindowLabel(window));
		WarmEnum<CardExtraEffectCountCardFilter>(filter => _ = CardEditorExtraEffects.CountCardFilterLabel(filter));
		WarmEnum<CardKeyword>(keyword => _ = CardEditorExtraEffects.GrantedKeywordLabel(keyword), keyword => keyword != CardKeyword.None);
		WarmEnum<CardExtraEffectTiming>(timing => _ = CardEditorExtraEffects.TimingLabel(timing));
		WarmEnum<CardExtraEffectEnemyStatus>(status => _ = CardEditorExtraEffects.EnemyStatusLabel(status));
		WarmEnum<CardExtraEffectEnemyStatus>(status => _ = CardEditorExtraEffects.EnemyStatusDescription(status));
		WarmEnum<CardExtraEffectMultiplierStat>(stat => _ = CardEditorExtraEffects.MultiplierStatLabel(stat));
		WarmEnum<CardExtraEffectEnemyIntent>(intent => _ = CardEditorExtraEffects.EnemyIntentLabel(intent));
		WarmEnum<CardExtraEffectTransformMode>(mode => _ = CardEditorExtraEffects.TransformModeLabel(mode));
		WarmEnum<CardExtraEffectConditionalBonusCondition>(condition => _ = CardEditorExtraEffects.ConditionalBonusConditionLabel(condition));
		WarmEnum<CardExtraEffectBranchMode>(mode => _ = CardEditorExtraEffects.BranchModeLabel(mode));
		WarmEnum<CardExtraEffectBranchConditionType>(type => _ = CardEditorExtraEffects.BranchConditionTypeLabel(type));
		WarmEnum<CardExtraEffectDuration>(duration => _ = CardEditorExtraEffects.DurationLabel(duration));
		WarmEnum<CardCreatedCardsCostDuration>(duration => _ = CardEditorExtraEffects.CreatedCardsCostDurationLabel(duration));
		WarmEnum<CardCreatedCardsCostResource>(resource => _ = CardEditorExtraEffects.CreatedCardsCostResourceLabel(resource));
		WarmEnum<CardGeneratedCardPool>(pool => _ = CardEditorExtraEffects.GeneratedCardPoolLabel(pool));
		WarmEnum<CardGeneratedCardType>(type => _ = CardEditorExtraEffects.GeneratedCardTypeLabel(type));
		WarmEnum<CardExtraEffectCardPile>(pile => _ = CardEditorExtraEffects.CardPileLabel(pile));
		WarmEnum<CardExtraEffectCardPilePosition>(position => _ = CardEditorExtraEffects.CardPilePositionLabel(position));
		WarmEnum<CardExtraEffectCardSelectionMode>(mode => _ = CardEditorExtraEffects.CardSelectionModeLabel(mode));
		WarmEnum<CardExtraEffectCardGrantDuration>(duration => _ = CardEditorExtraEffects.CardGrantDurationLabel(duration));
		WarmEnum<CardExtraEffectEnchantmentDuration>(duration => _ = CardEditorExtraEffects.EnchantmentDurationLabel(duration));
		WarmEnum<CardExtraEffectCardCostsLessMode>(mode => _ = CardEditorExtraEffects.CardCostsLessModeLabel(mode));
		WarmEnum<CardExtraEffectCardCostsLessDuration>(duration => _ = CardEditorExtraEffects.CardCostsLessDurationLabel(duration));
		WarmEnum<CardExtraEffectCostModifier>(modifier => _ = CardEditorExtraEffects.CardCostsLessModifierLabel(modifier));
		WarmEnum<CardExtraEffectCostFilterMode>(mode => _ = CardEditorExtraEffects.CostFilterModeLabel(mode));
		WarmEnum<CardExtraEffectOrbAction>(action => _ = CardEditorExtraEffects.OrbActionLabel(action));
		WarmEnum<CardExtraEffectOrbScope>(scope => _ = CardEditorExtraEffects.OrbScopeLabel(scope));
		WarmEnum<CardExtraEffectOstyAction>(action => _ = CardEditorExtraEffects.OstyActionLabel(action));
		WarmEnum<CardExtraEffectOrbType>(type => _ = CardEditorExtraEffects.OrbTypeLabel(type));
		WarmEnum<CardExtraEffectOrbSelection>(selection => _ = CardEditorExtraEffects.OrbSelectionLabel(selection));
		WarmEnum<CardExtraEffectOrbFollowUp>(followUp => _ = CardEditorExtraEffects.OrbFollowUpLabel(followUp));
	}

	private static void WarmEnum<TEnum>(Action<TEnum> warm, Predicate<TEnum>? include = null) where TEnum : struct, Enum
	{
		foreach (TEnum value in Enum.GetValues<TEnum>())
		{
			if (include != null && !include(value))
			{
				continue;
			}

			warm(value);
		}
	}
}
