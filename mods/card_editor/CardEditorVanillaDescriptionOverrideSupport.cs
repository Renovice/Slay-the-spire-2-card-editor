using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorVanillaDescriptionOverrideSupport
{
	internal static bool SuppressModifiedBaseTextOverride { get; set; }
	internal static bool SuppressExtraEffectDescriptionAppend { get; set; }

	public static void ApplyVanillaDescriptionPostfix(CardModel card, ref string description, Creature? target, bool isUpgradePreview)
	{
		if (card == null)
		{
			return;
		}

		SealedThroneDescriptionFix.TryFix(card, ref description);
		KinglyKickDescriptionFix.TryFix(card, ref description);
		ResonanceEnemyStrengthLossDescriptionFix.TryFix(card, ref description);
		HardcodedPowerAmountDescriptionFix.TryFix(card, ref description);
		RetainHandDurationDescriptionFix.TryFix(card, ref description);
		NoDrawDurationDescriptionFix.TryFix(card, ref description);
		ConquerorDurationDescriptionFix.TryFix(card, ref description);
		ReflectDurationDescriptionFix.TryFix(card, ref description);
		ColossusDurationDescriptionFix.TryFix(card, ref description);
		BlurDurationDescriptionFix.TryFix(card, ref description);
		HandTrickSlyDurationDescriptionFix.TryFix(card, ref description);
		TemporaryStatDurationDescriptionFix.TryFix(card, ref description);
		TargetTypeOverrideDescriptionFix.TryFix(card, ref description);

		if (!SuppressModifiedBaseTextOverride)
		{
			TryApplyModifiedBaseText(card, ref description, target, isUpgradePreview);
		}

		if (!SuppressExtraEffectDescriptionAppend)
		{
			CardEditorExtraEffects.TryAppendDescription(card, ref description, target, isUpgradePreview);
		}

		description = CardEditorVanillaKeywordSupport.FormatDescription(description);
	}

	public static string BuildEditableBaseDescription(CardModel card, Creature? target = null)
	{
		if (card == null)
		{
			return string.Empty;
		}

		bool previousSuppressModifiedBase = SuppressModifiedBaseTextOverride;
		bool previousSuppressExtraEffects = SuppressExtraEffectDescriptionAppend;
		SuppressModifiedBaseTextOverride = true;
		SuppressExtraEffectDescriptionAppend = true;
		try
		{
			return card.GetDescriptionForPile(PileType.None, target) ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
		finally
		{
			SuppressModifiedBaseTextOverride = previousSuppressModifiedBase;
			SuppressExtraEffectDescriptionAppend = previousSuppressExtraEffects;
		}
	}

	private static void TryApplyModifiedBaseText(CardModel card, ref string description, Creature? target, bool isUpgradePreview)
	{
		if (card == null)
		{
			return;
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draftOverride)
			&& (draftOverride.ModifiedBaseTextEnabled == true || draftOverride.Upgrade?.ModifiedBaseTextEnabled == true))
		{
			overrideData = draftOverride;
		}
		else if (CardEditorOverrides.TryGet(card.Id, out CardOverride storedOverride)
			&& (storedOverride.ModifiedBaseTextEnabled == true || storedOverride.Upgrade?.ModifiedBaseTextEnabled == true))
		{
			overrideData = storedOverride;
		}

		bool wantsUpgradeText = isUpgradePreview || card.IsUpgraded;
		if (wantsUpgradeText && overrideData?.Upgrade?.ModifiedBaseTextEnabled == true)
		{
			string upgradedDescription = CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(
				overrideData.Upgrade.ModifiedBaseText ?? string.Empty,
				description);
			description = isUpgradePreview
				? CardEditorDescriptionNumberHighlighter.HighlightChangedNumbers(
					BuildBaseModifiedTextForUpgradePreview(card, target, overrideData) ?? string.Empty,
					upgradedDescription)
				: upgradedDescription;
			return;
		}

		if (!wantsUpgradeText && overrideData?.ModifiedBaseTextEnabled == true)
		{
			description = CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(
				overrideData.ModifiedBaseText ?? string.Empty,
				description);
		}
	}

	private static string? BuildBaseModifiedTextForUpgradePreview(CardModel upgradedCard, Creature? target, CardOverride overrideData)
	{
		try
		{
			CardModel baseCard = ModelDb.GetById<CardModel>(upgradedCard.Id).ToMutable();
			if (upgradedCard.Owner != null && baseCard.Owner == null)
			{
				baseCard.Owner = upgradedCard.Owner;
			}

			CardEditorOverrides.ApplyOverrideToCard(baseCard, overrideData);
			string liveBaseDescription = BuildEditableBaseDescription(baseCard, target);
			string template = overrideData.ModifiedBaseTextEnabled == true
				? overrideData.ModifiedBaseText ?? string.Empty
				: liveBaseDescription;
			return CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(template, liveBaseDescription);
		}
		catch
		{
			return null;
		}
	}
}
