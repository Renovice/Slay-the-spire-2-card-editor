using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Linq;

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

		// Front gate: every skipped pass below requires a draft/stored/instance override,
		// a created-card/transform surface, or granted effects to change anything, so for
		// untouched vanilla cards they are provably no-ops. Only the three fixes that can
		// also key off the card's own DynamicVars run outside the gate (cheap early-outs:
		// a ModelId compare + string.Contains). FormatDescription and the preview-marker
		// resolve still run for every card because custom keywords can highlight vanilla
		// text.
		if (CardEditorRuntimeCaches.HasAnyModSurface(card, card.GetConcreteCombatState()))
		{
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

			bool modifiedBaseTextApplied = false;
			if (!SuppressModifiedBaseTextOverride)
			{
				modifiedBaseTextApplied = TryApplyModifiedBaseText(card, ref description, target, isUpgradePreview);
			}

			if (!SuppressExtraEffectDescriptionAppend
				&& !ShouldTreatModifiedBaseTextAsFullDescription(card, description, target, isUpgradePreview, modifiedBaseTextApplied))
			{
				CardEditorExtraEffects.TryAppendDescription(card, ref description, target, isUpgradePreview);
			}
		}
		else
		{
			// These can rewrite text from the card's own DynamicVars even without any
			// override (vanilla-upgraded Equilibrium/Colossus/Blur turn counts).
			RetainHandDurationDescriptionFix.TryFix(card, ref description);
			ColossusDurationDescriptionFix.TryFix(card, ref description);
			BlurDurationDescriptionFix.TryFix(card, ref description);
		}

		description = CardEditorVanillaKeywordSupport.FormatDescription(description);
		description = CardEditorDescriptionNumberHighlighter.ResolvePreviewOnlyHighlightMarkers(description, isUpgradePreview);
	}

	public static string BuildEditableBaseDescription(CardModel card, Creature? target = null)
	{
		return BuildEditableDescription(card, target, includeExtraEffects: false);
	}

	public static string BuildEditableFullDescription(CardModel card, Creature? target = null)
	{
		return BuildEditableDescription(card, target, includeExtraEffects: true);
	}

	private static string BuildEditableDescription(CardModel card, Creature? target, bool includeExtraEffects)
	{
		if (card == null)
		{
			return string.Empty;
		}

		bool previousSuppressModifiedBase = SuppressModifiedBaseTextOverride;
		bool previousSuppressExtraEffects = SuppressExtraEffectDescriptionAppend;
		SuppressModifiedBaseTextOverride = true;
		SuppressExtraEffectDescriptionAppend = !includeExtraEffects;
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

	private static bool TryApplyModifiedBaseText(CardModel card, ref string description, Creature? target, bool isUpgradePreview)
	{
		if (card == null)
		{
			return false;
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draftOverride)
			&& (draftOverride.ModifiedBaseTextEnabled == true || draftOverride.Upgrade?.ModifiedBaseTextEnabled == true))
		{
			overrideData = draftOverride;
		}
		else if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride storedOverride)
			&& (storedOverride.ModifiedBaseTextEnabled == true || storedOverride.Upgrade?.ModifiedBaseTextEnabled == true))
		{
			overrideData = storedOverride;
		}

		bool wantsUpgradeText = isUpgradePreview || card.IsUpgraded;
		if (wantsUpgradeText && overrideData?.Upgrade?.ModifiedBaseTextEnabled == true)
		{
			string referenceDescription = GetModifiedTextReferenceDescription(card, description, target, isUpgradePreview);
			IReadOnlyList<string> fallbackLiveNumbers = CardEditorExtraEffects.GetLiveNumberFallbackTokensForDescription(card, isUpgradePreview);
			IReadOnlyDictionary<string, string> stableLiveNumbers = CardEditorExtraEffects.GetStableLiveNumberTokensForDescription(card, isUpgradePreview);
			string upgradedDescription = CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(
				overrideData.Upgrade.ModifiedBaseText ?? string.Empty,
				referenceDescription,
				fallbackLiveNumbers,
				stableLiveNumbers);
			description = isUpgradePreview
				? CardEditorDescriptionNumberHighlighter.HighlightChangedNumbers(
					BuildBaseModifiedTextForUpgradePreview(card, target, overrideData) ?? string.Empty,
					upgradedDescription)
				: upgradedDescription;
			return true;
		}

		if (!wantsUpgradeText && overrideData?.ModifiedBaseTextEnabled == true)
		{
			string referenceDescription = GetModifiedTextReferenceDescription(card, description, target, isUpgradePreview);
			IReadOnlyList<string> fallbackLiveNumbers = CardEditorExtraEffects.GetLiveNumberFallbackTokensForDescription(card, isUpgradePreview);
			IReadOnlyDictionary<string, string> stableLiveNumbers = CardEditorExtraEffects.GetStableLiveNumberTokensForDescription(card, isUpgradePreview);
			description = CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(
				overrideData.ModifiedBaseText ?? string.Empty,
				referenceDescription,
				fallbackLiveNumbers,
				stableLiveNumbers);
			return true;
		}

		return false;
	}

	private static bool ShouldTreatModifiedBaseTextAsFullDescription(
		CardModel card,
		string modifiedDescription,
		Creature? target,
		bool isUpgradePreview,
		bool modifiedBaseTextApplied)
	{
		if (!modifiedBaseTextApplied || card == null || string.IsNullOrWhiteSpace(modifiedDescription))
		{
			return false;
		}

		try
		{
			if (HasEditableFullDescriptionAppend(card, target, isUpgradePreview))
			{
				return true;
			}

			string baseOnlyDescription = isUpgradePreview
				? BuildEditableBaseDescriptionForUpgradePreview(card, target)
				: BuildEditableBaseDescription(card, target);
			return CountNonEmptyLines(modifiedDescription) > CountNonEmptyLines(baseOnlyDescription);
		}
		catch
		{
			return false;
		}
	}

	private static string GetModifiedTextReferenceDescription(CardModel card, string currentDescription, Creature? target, bool isUpgradePreview)
	{
		if (!HasEditableFullDescriptionAppend(card, target, isUpgradePreview))
		{
			return currentDescription;
		}

		return isUpgradePreview
			? BuildEditableFullDescriptionForUpgradePreview(card, target)
			: BuildEditableFullDescription(card, target);
	}

	private static bool HasEditableFullDescriptionAppend(CardModel card, Creature? target, bool isUpgradePreview)
	{
		try
		{
			return CardEditorExtraEffects.HasAppendableDescriptionLines(card, target, isUpgradePreview);
		}
		catch
		{
			return false;
		}
	}

	private static string BuildEditableBaseDescriptionForUpgradePreview(CardModel upgradedCard, Creature? target)
	{
		bool previousSuppressModifiedBase = SuppressModifiedBaseTextOverride;
		bool previousSuppressExtraEffects = SuppressExtraEffectDescriptionAppend;
		SuppressModifiedBaseTextOverride = true;
		SuppressExtraEffectDescriptionAppend = true;
		try
		{
			return upgradedCard.GetDescriptionForUpgradePreview() ?? string.Empty;
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

	private static string BuildEditableFullDescriptionForUpgradePreview(CardModel upgradedCard, Creature? target)
	{
		bool previousSuppressModifiedBase = SuppressModifiedBaseTextOverride;
		bool previousSuppressExtraEffects = SuppressExtraEffectDescriptionAppend;
		SuppressModifiedBaseTextOverride = true;
		SuppressExtraEffectDescriptionAppend = false;
		try
		{
			return upgradedCard.GetDescriptionForUpgradePreview() ?? string.Empty;
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

	private static int CountNonEmptyLines(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}

		return text
			.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
			.Count(line => !string.IsNullOrWhiteSpace(line));
	}

	private static string? BuildBaseModifiedTextForUpgradePreview(CardModel upgradedCard, Creature? target, CardOverride overrideData)
	{
		try
		{
			CardModel baseCard = ModelDb.GetById<CardModel>(upgradedCard.Id).ToMutable();
			MegaCrit.Sts2.Core.Entities.Players.Player? owner = upgradedCard.TryGetOwner();
			if (owner != null && baseCard.TryGetOwner() == null)
			{
				baseCard.Owner = owner;
			}

			CardEditorOverrides.ApplyOverrideToCard(baseCard, overrideData);
			string liveBaseDescription = BuildEditableBaseDescription(baseCard, target);
			string template = overrideData.ModifiedBaseTextEnabled == true
				? overrideData.ModifiedBaseText ?? string.Empty
				: liveBaseDescription;
			IReadOnlyList<string> fallbackLiveNumbers = CardEditorExtraEffects.GetLiveNumberFallbackTokensForDescription(baseCard, isUpgradePreview: false);
			IReadOnlyDictionary<string, string> stableLiveNumbers = CardEditorExtraEffects.GetStableLiveNumberTokensForDescription(baseCard, isUpgradePreview: false);
			return CardEditorDescriptionNumberHighlighter.ApplyLiveNumbersAndManagedLinesFromReference(
				template,
				liveBaseDescription,
				fallbackLiveNumbers,
				stableLiveNumbers);
		}
		catch
		{
			return null;
		}
	}
}
