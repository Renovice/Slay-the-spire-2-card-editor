using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorUpgradeDeltaDebugLog
{
	private const bool Enabled = false;
	private const string Tag = "[CardEditor][UpgradeDeltaDebug]";
	private const int MaxDescriptionLength = 900;
	private const int MaxEffectStringLength = 1200;
	private static int _sequence;

	internal static void LogDescription(string context, CardModel? card, PileType pileType, bool isUpgradePreview, string? description)
	{
		if (!ShouldLog(card, description: description))
		{
			return;
		}

		Write($"{context} description pile={pileType} isUpgradePreview={isUpgradePreview} card={CardSummary(card)} desc=\"{Short(description, MaxDescriptionLength)}\"");
		LogEffectiveOverride(context + ".effectiveOverride", card);
	}

	internal static void LogCardState(string context, CardModel? card, CardOverride? overrideData = null, string? description = null)
	{
		if (!ShouldLog(card, overrideData, description))
		{
			return;
		}

		Write($"{context} card={CardSummary(card)} desc=\"{Short(description, MaxDescriptionLength)}\"");
		LogOverride(context + ".override", CardIdText(card), overrideData);
	}

	internal static void LogOverride(string context, ModelId? cardId, CardOverride? overrideData)
	{
		LogOverride(context, cardId?.ToString() ?? "null", overrideData);
	}

	internal static void LogOverride(string context, string? cardId, CardOverride? overrideData)
	{
		if (!ShouldLog(cardId, overrideData))
		{
			return;
		}

		int baseCount = overrideData?.ExtraEffects?.Count ?? 0;
		int upgradeCount = overrideData?.Upgrade?.ExtraEffects?.Count ?? 0;
		bool deltaMode = overrideData?.Upgrade?.ExtraEffectNumericFieldsAreDeltas ?? false;
		Write($"{context} cardId={cardId ?? "null"} baseEffects={baseCount} upgradeEffects={upgradeCount} upgradeDeltaMode={deltaMode} upgradeEmpty={overrideData?.Upgrade?.IsEmpty().ToString() ?? "null"}");
		LogEffectList(context + ".baseEffects", overrideData?.ExtraEffects);
		LogEffectList(context + ".upgradeEffects", overrideData?.Upgrade?.ExtraEffects);
	}

	internal static void LogUpgradeEditorRow(
		string context,
		string? cardId,
		int rowIndex,
		CardExtraEffect? baseEffect,
		CardExtraEffect? storedUpgradeEffect,
		CardExtraEffect? displayEffect,
		bool numericFieldsAreDeltas)
	{
		if (!ShouldLog(cardId, baseEffect, storedUpgradeEffect, displayEffect))
		{
			return;
		}

		Write(
			$"{context} cardId={cardId ?? "null"} row={rowIndex} upgradeDeltaMode={numericFieldsAreDeltas} " +
			$"base={EffectSummary(baseEffect)} storedUpgrade={EffectSummary(storedUpgradeEffect)} displayRow={EffectSummary(displayEffect)}");
	}

	internal static void LogUpgradeEditorDraftRow(
		string context,
		string? cardId,
		int rowIndex,
		CardExtraEffect? baseEffect,
		CardExtraEffect? draftUpgradeEffect,
		bool isDeltaRow,
		bool numericFieldsAreDeltas,
		bool meaningful)
	{
		if (!ShouldLog(cardId, baseEffect, draftUpgradeEffect))
		{
			return;
		}

		Write(
			$"{context} cardId={cardId ?? "null"} row={rowIndex} isDeltaRow={isDeltaRow} upgradeDeltaMode={numericFieldsAreDeltas} meaningful={meaningful} " +
			$"base={EffectSummary(baseEffect)} draftUpgrade={EffectSummary(draftUpgradeEffect)}");
	}

	internal static void LogUpgradeBuilt(string context, string? cardId, CardUpgradeOverride? upgrade, IEnumerable<CardExtraEffect?>? baseEffects)
	{
		IEnumerable<CardExtraEffect?> combinedEffects = (baseEffects ?? Enumerable.Empty<CardExtraEffect?>())
			.Concat(upgrade?.ExtraEffects ?? Enumerable.Empty<CardExtraEffect?>());
		if (!ShouldLog(cardId, effects: combinedEffects))
		{
			return;
		}

		Write($"{context} cardId={cardId ?? "null"} upgradeDeltaMode={upgrade?.ExtraEffectNumericFieldsAreDeltas.ToString() ?? "null"} upgradeEffects={upgrade?.ExtraEffects?.Count ?? 0}");
		LogEffectList(context + ".baseEffects", baseEffects);
		LogEffectList(context + ".upgradeEffects", upgrade?.ExtraEffects);
	}

	internal static void LogEffectiveEffectsStart(
		string context,
		CardModel? card,
		CardOverride? overrideData,
		bool considerUpgrade,
		int upgradeApplications)
	{
		if (!ShouldLog(card, overrideData))
		{
			return;
		}

		Write(
			$"{context} considerUpgrade={considerUpgrade} upgradeApplications={upgradeApplications} " +
			$"upgradeDeltaMode={overrideData?.Upgrade?.ExtraEffectNumericFieldsAreDeltas.ToString() ?? "null"} card={CardSummary(card)}");
		LogOverride(context + ".override", CardIdText(card), overrideData);
	}

	internal static void LogEffectiveEffectsResult(string context, CardModel? card, IEnumerable<CardExtraEffect?>? effects)
	{
		if (!ShouldLog(card, effects: effects))
		{
			return;
		}

		Write($"{context} card={CardSummary(card)} resultEffects={effects?.Count() ?? 0}");
		LogEffectList(context + ".effects", effects);
	}

	internal static void LogMergeSlot(
		string context,
		CardModel? card,
		int baseSlotIndex,
		int applicationIndex,
		CardExtraEffect? before,
		CardExtraEffect? upgrade,
		CardExtraEffect? after,
		bool numericFieldsAreDeltas)
	{
		if (!ShouldLog(card, effects: new[] { before, upgrade, after }))
		{
			return;
		}

		Write(
			$"{context} slot={baseSlotIndex} application={applicationIndex} upgradeDeltaMode={numericFieldsAreDeltas} card={CardSummary(card)} " +
			$"before={EffectSummary(before)} upgrade={EffectSummary(upgrade)} after={EffectSummary(after)}");
	}

	internal static void LogMergeFunction(
		string context,
		CardExtraEffect? baseEffect,
		CardExtraEffect? upgradeEffect,
		CardExtraEffect? fusedEffect,
		bool numericFieldsAreDeltas)
	{
		if (!ShouldLog(null, baseEffect, upgradeEffect, fusedEffect))
		{
			return;
		}

		Write(
			$"{context} upgradeDeltaMode={numericFieldsAreDeltas} " +
			$"base={EffectSummary(baseEffect)} upgrade={EffectSummary(upgradeEffect)} fused={EffectSummary(fusedEffect)}");
	}

	internal static void LogHistoryScalingResolve(string context, CardExtraEffect? effect, int rawCount, int multiplier)
	{
		if (!ShouldLog(null, effect))
		{
			return;
		}

		Write($"{context} rawCount={rawCount} multiplier={multiplier} effect={EffectSummary(effect)}");
	}

	private static void LogEffectiveOverride(string context, CardModel? card)
	{
		if (card == null)
		{
			return;
		}

		try
		{
			if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draft))
			{
				LogOverride(context + ".draft", card.Id, draft);
			}
		}
		catch (Exception ex)
		{
			Write($"{context}.draft lookupError={ex.GetType().Name}");
		}

		try
		{
			if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored))
			{
				LogOverride(context + ".storedOrInstance", card.Id, stored);
			}
		}
		catch (Exception ex)
		{
			Write($"{context}.storedOrInstance lookupError={ex.GetType().Name}");
		}
	}

	private static void LogEffectList(string context, IEnumerable<CardExtraEffect?>? effects)
	{
		if (effects == null)
		{
			return;
		}

		int index = 0;
		foreach (CardExtraEffect? effect in effects)
		{
			if (index >= 16)
			{
				Write($"{context}[more] truncatedAt=16");
				break;
			}

			if (ShouldLog(null, effect))
			{
				Write($"{context}[{index}] {EffectSummary(effect)}");
			}
			index++;
		}
	}

	private static bool ShouldLog(CardModel? card, CardOverride? overrideData = null, string? description = null, IEnumerable<CardExtraEffect?>? effects = null)
	{
		if (!Enabled)
		{
			return false;
		}

		if (HasSuspectText(CardIdText(card)) || HasSuspectText(Safe(() => card?.Title)) || HasSuspectText(description))
		{
			return true;
		}

		return ShouldLog(null, overrideData, effects);
	}

	private static bool ShouldLog(string? cardId, CardOverride? overrideData = null, IEnumerable<CardExtraEffect?>? effects = null)
	{
		if (!Enabled)
		{
			return false;
		}

		if (HasSuspectText(cardId))
		{
			return true;
		}

		if (EffectsContainSuspect(effects))
		{
			return true;
		}

		if (overrideData == null)
		{
			return false;
		}

		return EffectsContainSuspect(overrideData.ExtraEffects)
			|| EffectsContainSuspect(overrideData.Upgrade?.ExtraEffects);
	}

	private static bool ShouldLog(string? cardId, params CardExtraEffect?[] effects)
	{
		return ShouldLog(cardId, overrideData: null, effects);
	}

	private static bool EffectsContainSuspect(IEnumerable<CardExtraEffect?>? effects)
	{
		if (effects == null)
		{
			return false;
		}

		foreach (CardExtraEffect? effect in effects)
		{
			if (IsSuspectEffect(effect))
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsSuspectEffect(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return false;
		}

		return effect.Kind == CardExtraEffectKind.Forge
			|| effect.CountCardFilter == CardExtraEffectCountCardFilter.Forge
			|| effect.TriggerCardFilter == CardExtraEffectCountCardFilter.Forge
			|| effect.CardSelectionFilter == CardExtraEffectCountCardFilter.Forge
			|| effect.BranchCountCardFilter == CardExtraEffectCountCardFilter.Forge
			|| effect.SelfScalingNumberFilter == CardExtraEffectCountCardFilter.Forge
			|| HasSuspectText(effect.SpecificCardId)
			|| HasSuspectText(effect.SpecificCardId2)
			|| HasSuspectText(effect.SpecificCardId3)
			|| HasSuspectText(effect.CustomKeywordName)
			|| HasSuspectText(effect.CustomPowerName)
			|| IsSuspectEffect(effect.BranchEffect);
	}

	private static bool HasSuspectText(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}

		return value.IndexOf("rainbow", StringComparison.OrdinalIgnoreCase) >= 0
			|| value.IndexOf("infusion", StringComparison.OrdinalIgnoreCase) >= 0
			|| value.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string EffectSummary(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return "<null>";
		}

		string text =
			$"effectId={Short(effect.EffectId, 80)} kind={effect.Kind} target={effect.Target} amount={effect.Amount} " +
			$"amountIsX={effect.AmountIsX} amountXPlus={effect.AmountXPlus} disableOnUpgrade={effect.DisableOnUpgrade} " +
			$"scale={effect.ScaleMode} countEvent={effect.CountEvent} countWindow={effect.CountWindow} countPile={effect.CountCardPile} " +
			$"countPool={effect.CountCardPool} countType={effect.CountCardType} countFilter={effect.CountCardFilter} countAgg={effect.CountAggregationMode} " +
			$"countUsesAmount={effect.CountUsesCardEffectAmount} countExcludeSource={effect.CountExcludeSourceCard} countComparison={effect.CountComparison} " +
			$"countCondition={effect.CountConditionAmount} historyIncludesBase={effect.HistoryScalingIncludesBase} " +
			$"historyBaseRaw={effect.HistoryScalingBaseAmount?.ToString() ?? "null"} historyBaseResolved={CardEditorExtraEffects.ResolveHistoryScalingBaseAmount(effect, effect.Amount)} " +
			$"historyStepRaw={effect.HistoryScalingCountStep} historyStepResolved={CardEditorExtraEffects.ResolveHistoryScalingCountStep(effect)} " +
			$"repeatIsX={effect.RepeatIsX} repeatCount={effect.RepeatCount} trigger={effect.Trigger} triggerEveryN={effect.TriggerEveryN} " +
			$"triggerFilter={effect.TriggerCardFilter} selectionCount={effect.CardSelectionCount} selectionFilter={effect.CardSelectionFilter} " +
			$"specific={Short(effect.SpecificCardId, 100)} branchMode={effect.BranchMode} branchCountEvent={effect.BranchCountEvent} " +
			$"branchCountFilter={effect.BranchCountCardFilter} branchCountComparison={effect.BranchCountComparison} branchCountCondition={effect.BranchCountConditionAmount}";

		return Short(text, MaxEffectStringLength);
	}

	private static string CardSummary(CardModel? card)
	{
		if (card == null)
		{
			return "<null>";
		}

		return
			$"id={CardIdText(card)} title=\"{Short(Safe(() => card.Title), 160)}\" " +
			$"type={Safe(() => card.Type)} upgraded={Safe(() => card.IsUpgraded)} upgradeLevel={Safe(() => card.CurrentUpgradeLevel)} " +
			$"mutable={Safe(() => card.IsMutable)} clone={Safe(() => card.IsClone)} cloneOf={Safe(() => card.CloneOf?.Id)} " +
			$"pile={Safe(() => card.Pile?.Type)} suppressAll={CardEditorOverrides.SuppressAllOverrides} suppressUpgrade={CardEditorOverrides.SuppressUpgradeOverrides}";
	}

	private static string CardIdText(CardModel? card)
	{
		return Safe(() => card?.Id) ?? "null";
	}

	private static string Safe(Func<object?> getter)
	{
		try
		{
			return getter()?.ToString() ?? "null";
		}
		catch (Exception ex)
		{
			return "error:" + ex.GetType().Name;
		}
	}

	private static string Short(string? value, int maxLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		string normalized = value
			.Replace("\r\n", " | ", StringComparison.Ordinal)
			.Replace('\n', '|')
			.Replace('\r', '|')
			.Replace('\t', ' ');
		if (normalized.Length <= maxLength)
		{
			return normalized;
		}

		return normalized.Substring(0, maxLength) + "...";
	}

	private static void Write(string message)
	{
		if (!Enabled)
		{
			return;
		}

		int sequence = Interlocked.Increment(ref _sequence);
		Log.Info($"{Tag} #{sequence} {message}");
	}
}
