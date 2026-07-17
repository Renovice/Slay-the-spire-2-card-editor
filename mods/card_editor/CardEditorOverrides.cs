using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

public sealed class CardOverride
{
	public string? PoolTitle { get; set; }
	public CardRarity? Rarity { get; set; }
	public bool? Enabled { get; set; } // Dangerous for vanilla: removes from pools when false.
	public bool? CanBeGeneratedInCombat { get; set; }
	public bool? CanBeGeneratedByModifiers { get; set; }
	public string? TitleOverride { get; set; } // Cosmetic only (not localized).
	public bool? ModifiedBaseTextEnabled { get; set; }
	public string? ModifiedBaseText { get; set; }
	public bool? EndlessUpgrades { get; set; }
	public bool? FullArt { get; set; } // Cosmetic only (render-time).
	public CardEditorVisualFinish? Finish { get; set; } // Cosmetic only (render-time).
	public Dictionary<string, float>? FinishParams { get; set; } // Cosmetic only (render-time).
	public string? CustomFinishId { get; set; } // Cosmetic only (runtime-loaded shader).
	public Dictionary<string, string>? CustomFinishParams { get; set; } // Cosmetic only (runtime-loaded shader).
	public CardEditorVisualFinish? BorderFinish { get; set; } // Cosmetic only (render-time border overlay).
	public Dictionary<string, float>? BorderFinishParams { get; set; } // Cosmetic only (render-time border overlay).
	public float? PortraitOffsetX { get; set; } // Cosmetic only (render-time).
	public float? PortraitOffsetY { get; set; } // Cosmetic only (render-time).
	public float? PortraitZoom { get; set; } // Cosmetic only (render-time). 1 = vanilla scale.
	public CardType? CardType { get; set; }
	public TargetType? TargetType { get; set; }
	public int? EnergyCost { get; set; }
	public bool? EnergyCostX { get; set; }
	public int? StarCost { get; set; }
	public bool? StarCostX { get; set; }
	public int? ReplayCount { get; set; }
	public int? DrawCostReduction { get; set; } // for cards like Kingly Kick
	// For vanilla cards that use FromHandForDiscard(..., 1) and then discard only one selected card.
	// We patch those cards to support discarding N selected cards, and this stores the configured N.
	public int? HandDiscardCount { get; set; }
	public HashSet<CardKeyword>? Keywords { get; set; }
	public HashSet<CardTag>? TagsToAdd { get; set; }
	public HashSet<CardTag>? TagsToRemove { get; set; }
	public HashSet<string>? CustomTags { get; set; }
	public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
	public Dictionary<ModelId, decimal>? PowerAmounts { get; set; }
	public CardKeywordGrantDuration? SlyGrantDuration { get; set; }
	public int? SlyGrantTurns { get; set; }
	public CardKeywordGrantDuration? TemporaryStrengthDuration { get; set; }
	public int? TemporaryStrengthTurns { get; set; }
	public CardKeywordGrantDuration? TemporaryDexterityDuration { get; set; }
	public int? TemporaryDexterityTurns { get; set; }
	public CardKeywordGrantDuration? TemporaryFocusDuration { get; set; }
	public int? TemporaryFocusTurns { get; set; }
	public ModelId? EnchantmentId { get; set; }
	public int? EnchantmentAmount { get; set; }
	public ModelId? AfflictionId { get; set; }
	public int? AfflictionAmount { get; set; }
	public List<CardExtraEffect>? ExtraEffects { get; set; }
	public ModelId? PortraitSourceCardId { get; set; }
	public string? CustomPortraitFile { get; set; }
	public CardEditorCosmeticStylePreset? CosmeticStylePreset { get; set; }
	public CardEditorCosmeticAnimationPreset? CosmeticAnimationPreset { get; set; }
	public CardEditorCosmeticVfxPreset? CosmeticVfxPreset { get; set; }
	public CardEditorCosmeticAttach? CosmeticVfxAttach { get; set; }
	public bool? CosmeticPlayAttackerAnim { get; set; }
	public bool? HideCosmeticCostOrb { get; set; }
	public bool? HideCosmeticCostNumber { get; set; }
	public bool? HideCosmeticNameBanner { get; set; }
	public bool? HideCosmeticNameText { get; set; }
	public bool? HideCosmeticTypeBadge { get; set; }
	public bool? HideCosmeticTextBackground { get; set; }
	public bool? HideCosmeticBodyText { get; set; }
	public bool? HideCosmeticAncientInnerBorder { get; set; }
	public CardUpgradeOverride? Upgrade { get; set; }

	public bool IsEmpty()
	{
		return string.IsNullOrWhiteSpace(PoolTitle)
			&& Rarity == null
			&& Enabled == null
			&& CanBeGeneratedInCombat == null
			&& CanBeGeneratedByModifiers == null
			&& string.IsNullOrWhiteSpace(TitleOverride)
			&& ModifiedBaseTextEnabled == null
			&& string.IsNullOrWhiteSpace(ModifiedBaseText)
			&& EndlessUpgrades == null
			&& FullArt == null
			&& Finish == null
			&& (FinishParams == null || FinishParams.Count == 0)
			&& string.IsNullOrWhiteSpace(CustomFinishId)
			&& (CustomFinishParams == null || CustomFinishParams.Count == 0)
			&& BorderFinish == null
			&& (BorderFinishParams == null || BorderFinishParams.Count == 0)
			&& PortraitOffsetX == null
			&& PortraitOffsetY == null
			&& PortraitZoom == null
			&& CardType == null
			&& TargetType == null
			&& EnergyCost == null
			&& EnergyCostX == null
			&& StarCost == null
			&& StarCostX == null
			&& ReplayCount == null
			&& DrawCostReduction == null
			&& HandDiscardCount == null
			&& (Keywords == null || Keywords.Count == 0)
			&& (TagsToAdd == null || TagsToAdd.Count == 0)
			&& (TagsToRemove == null || TagsToRemove.Count == 0)
			&& (CustomTags == null || CustomTags.Count == 0)
			&& (DynamicVarBaseValues == null || DynamicVarBaseValues.Count == 0)
			&& (PowerAmounts == null || PowerAmounts.Count == 0)
			&& SlyGrantDuration == null
			&& SlyGrantTurns == null
			&& TemporaryStrengthDuration == null
			&& TemporaryStrengthTurns == null
			&& TemporaryDexterityDuration == null
			&& TemporaryDexterityTurns == null
			&& TemporaryFocusDuration == null
			&& TemporaryFocusTurns == null
			&& EnchantmentId == null
			&& EnchantmentAmount == null
			&& AfflictionId == null
			&& AfflictionAmount == null
			&& (ExtraEffects == null || ExtraEffects.Count == 0)
			&& PortraitSourceCardId == null
			&& string.IsNullOrWhiteSpace(CustomPortraitFile)
			&& CosmeticStylePreset == null
			&& CosmeticAnimationPreset == null
			&& CosmeticVfxPreset == null
			&& CosmeticVfxAttach == null
			&& CosmeticPlayAttackerAnim == null
			&& HideCosmeticCostOrb == null
			&& HideCosmeticCostNumber == null
			&& HideCosmeticNameBanner == null
			&& HideCosmeticNameText == null
			&& HideCosmeticTypeBadge == null
			&& HideCosmeticTextBackground == null
			&& HideCosmeticBodyText == null
			&& HideCosmeticAncientInnerBorder == null
			&& (Upgrade == null || Upgrade.IsEmpty());
	}
}

public enum CardEditorVisualFinish
{
	None = 0,
	RainbowRareFoil = 1,
	RainbowGlitterArt = 2,
	PrismaticBandGlare = 5,
	PurpleWavesOcean = 6,
	Whirlpool = 7,
	Miasma = 8,
	Aurora = 9,
	Constellation = 10,
	DvdRipple = 11,
	Lightning = 12,
	Flame = 13,
	BaseGameInfection = 100,
	BaseGameGalvanized = 101,
	BaseGameBound = 102,
	BaseGameEntangled = 103,
	BaseGameHexed = 104,
	BaseGameRinging = 105,
	BaseGameSmog = 106
}

public enum CardKeywordGrantDuration
{
	ThisTurn = 0,
	ThisCombat = 1,
	Turns = 2
}

public sealed class CardUpgradeOverride
{
	public bool? ModifiedBaseTextEnabled { get; set; }
	public string? ModifiedBaseText { get; set; }
	public int? EnergyCostDelta { get; set; }
	public int? StarCostDelta { get; set; }
	public int? ReplayCountDelta { get; set; }
	public ModelId? EnchantmentId { get; set; }
	public int? EnchantmentAmount { get; set; }
	public ModelId? AfflictionId { get; set; }
	public int? AfflictionAmount { get; set; }
	public HashSet<CardKeyword>? KeywordsToAdd { get; set; }
	public HashSet<CardKeyword>? KeywordsToRemove { get; set; }
	public Dictionary<string, decimal>? DynamicVarDeltas { get; set; }
	public List<CardExtraEffect>? ExtraEffects { get; set; }
	public bool ExtraEffectNumericFieldsAreDeltas { get; set; }

	public bool IsEmpty()
	{
		return ModifiedBaseTextEnabled == null
			&& string.IsNullOrWhiteSpace(ModifiedBaseText)
			&& EnergyCostDelta == null
			&& StarCostDelta == null
			&& ReplayCountDelta == null
			&& EnchantmentId == null
			&& EnchantmentAmount == null
			&& AfflictionId == null
			&& AfflictionAmount == null
			&& (KeywordsToAdd == null || KeywordsToAdd.Count == 0)
			&& (KeywordsToRemove == null || KeywordsToRemove.Count == 0)
			&& (DynamicVarDeltas == null || DynamicVarDeltas.Count == 0)
			&& (ExtraEffects == null || ExtraEffects.Count == 0);
	}
}

public static class CardEditorOverrides
{
	private sealed class RuntimeEnchantmentSnapshot
	{
		public required ModelId Id { get; init; }
		public required int Amount { get; init; }
	}

	private static readonly Dictionary<ModelId, CardOverride> _overrides = new();
	private static readonly ConditionalWeakTable<CardModel, CardOverride> _instanceOverrides = new();
	private static readonly FieldInfo? _afflictionAmountField = typeof(AfflictionModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? _cardAfflictionSetter = typeof(CardModel).GetProperty(nameof(CardModel.Affliction))?.GetSetMethod(true);
	private static readonly FieldInfo? _cardTypeBackingField = typeof(CardModel).GetField("<Type>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _rarityBackingField = typeof(CardModel).GetField("<Rarity>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _targetTypeBackingField = typeof(CardModel).GetField("<TargetType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _poolField = typeof(CardModel).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly MethodInfo? _baseStarCostSetter = typeof(CardModel).GetProperty(nameof(CardModel.BaseStarCost))?.GetSetMethod(true);
	private static readonly FieldInfo? _baseStarCostField = typeof(CardModel).GetField("_baseStarCost", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _starCostSetField = typeof(CardModel).GetField("_starCostSet", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _energyCostField = typeof(CardModel).GetField("_energyCost", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _starCostChangedField = typeof(CardModel).GetField("StarCostChanged", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _energyWasJustUpgradedBackingField = typeof(CardEnergyCost).GetField("<WasJustUpgraded>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly FieldInfo? _wasStarCostJustUpgradedField = typeof(CardModel).GetField("_wasStarCostJustUpgraded", BindingFlags.Instance | BindingFlags.NonPublic);

	internal static bool SuppressAllOverrides { get; set; }
	internal static bool SuppressUpgradeOverrides { get; set; }

	internal static int Revision { get; private set; }

	public static bool HasAnyOverrides => _overrides.Count > 0;

	public static IReadOnlyDictionary<ModelId, CardOverride> AllOverrides => _overrides;

	public static bool TryGet(ModelId id, out CardOverride overrideData)
	{
		return _overrides.TryGetValue(id, out overrideData);
	}

	internal static bool TryGet(CardModel? card, out CardOverride overrideData)
	{
		if (card != null && _instanceOverrides.TryGetValue(card, out overrideData))
		{
			return true;
		}

		if (card != null)
		{
			return _overrides.TryGetValue(card.Id, out overrideData);
		}

		overrideData = null!;
		return false;
	}

	internal static bool TryGetRuntimeCloneSourceOverride(CardModel? card, out CardOverride overrideData)
	{
		if (TryGetInstanceOverride(card, out overrideData))
		{
			return true;
		}

		if (card != null)
		{
			return _overrides.TryGetValue(card.Id, out overrideData);
		}

		overrideData = null!;
		return false;
	}

	internal static void MarkEnergyCostJustUpgraded(CardModel card)
	{
		if (card == null)
		{
			return;
		}
		try
		{
			CardEnergyCost cost = card.EnergyCost;
			_energyWasJustUpgradedBackingField?.SetValue(cost, true);
		}
		catch
		{
		}
	}

	internal static void MarkStarCostJustUpgraded(CardModel card)
	{
		if (card == null)
		{
			return;
		}
		try
		{
			_wasStarCostJustUpgradedField?.SetValue(card, true);
		}
		catch
		{
		}
	}

	internal static bool TryGetEffectiveOverride(ModelId id, out CardOverride overrideData)
	{
		if (CardEditorUiState.TryGetDraftOverride(id, out overrideData))
		{
			return true;
		}
		return _overrides.TryGetValue(id, out overrideData);
	}

	internal static bool TryGetEffectiveOverride(CardModel? card, out CardOverride overrideData)
	{
		if (TryGetInstanceOverride(card, out overrideData))
		{
			return true;
		}

		if (card != null)
		{
			return _overrides.TryGetValue(card.Id, out overrideData);
		}

		overrideData = null!;
		return false;
	}

	private static bool TryGetInstanceOverride(CardModel? card, out CardOverride overrideData)
	{
		CardModel? cursor = card;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			if (_instanceOverrides.TryGetValue(cursor, out overrideData))
			{
				return true;
			}

			try
			{
				if (!cursor.IsClone || cursor.CloneOf == null)
				{
					break;
				}
				cursor = cursor.CloneOf;
			}
			catch
			{
				break;
			}
		}

		overrideData = null!;
		return false;
	}

	public static CardOverride? Get(ModelId id)
	{
		_overrides.TryGetValue(id, out CardOverride overrideData);
		return overrideData;
	}

	internal static void SetInstanceOverride(CardModel? card, CardOverride? overrideData)
	{
		if (card == null)
		{
			return;
		}

		_instanceOverrides.Remove(card);
		if (overrideData != null && !overrideData.IsEmpty())
		{
			_instanceOverrides.Add(card, CloneOverride(overrideData));
			CardEditorOverrideOnPlayPatcher.EnsureForCardType(card.GetType());
		}
	}

	internal static CardOverride Clone(CardOverride source)
	{
		return CloneOverride(source);
	}

	public static void Set(ModelId id, CardOverride overrideData)
	{
		if (overrideData == null || overrideData.IsEmpty())
		{
			if (_overrides.Remove(id))
			{
				Revision++;
			}
			return;
		}
		_overrides[id] = overrideData;
		Revision++;
		// Overridden vanilla cards get an OnPlay postfix so their rows run with the play.
		CardEditorOverrideOnPlayPatcher.EnsureForCardId(id);
	}

	public static void Clear(ModelId id)
	{
		if (_overrides.Remove(id))
		{
			Revision++;
		}
	}

	public static void ClearAll()
	{
		if (_overrides.Count > 0)
		{
			_overrides.Clear();
			Revision++;
		}
	}

	public static Dictionary<ModelId, CardOverride> ExportSnapshot()
	{
		Dictionary<ModelId, CardOverride> snapshot = new Dictionary<ModelId, CardOverride>(_overrides.Count);
		foreach ((ModelId key, CardOverride value) in _overrides)
		{
			snapshot[key] = CloneOverride(value);
		}
		return snapshot;
	}

	public static void ReplaceAll(IReadOnlyDictionary<ModelId, CardOverride> overrides)
	{
		_overrides.Clear();
		if (overrides != null)
		{
			foreach ((ModelId key, CardOverride value) in overrides)
			{
				if (value == null || value.IsEmpty())
				{
					continue;
				}
				_overrides[key] = CloneOverride(value);
			}
		}

		// Created cards persist their data in CardEditorCreatedCardsStore (created_cards.json).
		// Editor presets are meant for vanilla/compendium edits; if we allow ReplaceAll to wipe
		// created-card overrides, created cards appear to "go blank" after relaunch when a startup
		// preset is enabled. Always restore created-card overrides after loading an editor preset.
		TryMergeCreatedCardOverrides();

		Revision++;

		// Preset loads / multiplayer sync can introduce overrides for card types not patched yet.
		CardEditorOverrideOnPlayPatcher.EnsureForAllStoredOverrides();
	}

	private static void TryMergeCreatedCardOverrides()
	{
		try
		{
			Dictionary<ModelId, CardEditorCreatedCardDefinition> defs = CardEditorCreatedCardsStore.ExportSnapshot();
			foreach ((ModelId id, CardEditorCreatedCardDefinition def) in defs)
			{
				CardOverride? ov = def?.Override;
				if (ov == null || ov.IsEmpty())
				{
					continue;
				}

				// Creator store is the source of truth for created cards. Prefer it over editor presets.
				_overrides[id] = CloneOverride(ov);
			}
		}
		catch
		{
		}
	}

	private static CardOverride CloneOverride(CardOverride source)
	{
		return new CardOverride
		{
			PoolTitle = source.PoolTitle,
			Rarity = source.Rarity,
			Enabled = source.Enabled,
			CanBeGeneratedInCombat = source.CanBeGeneratedInCombat,
			CanBeGeneratedByModifiers = source.CanBeGeneratedByModifiers,
			TitleOverride = source.TitleOverride,
			ModifiedBaseTextEnabled = source.ModifiedBaseTextEnabled,
			ModifiedBaseText = source.ModifiedBaseText,
			EndlessUpgrades = source.EndlessUpgrades,
			FullArt = source.FullArt,
			Finish = source.Finish,
			FinishParams = source.FinishParams != null ? new Dictionary<string, float>(source.FinishParams) : null,
			CustomFinishId = source.CustomFinishId,
			CustomFinishParams = source.CustomFinishParams != null ? new Dictionary<string, string>(source.CustomFinishParams, StringComparer.Ordinal) : null,
			BorderFinish = source.BorderFinish,
			BorderFinishParams = source.BorderFinishParams != null ? new Dictionary<string, float>(source.BorderFinishParams) : null,
			PortraitOffsetX = source.PortraitOffsetX,
			PortraitOffsetY = source.PortraitOffsetY,
			PortraitZoom = source.PortraitZoom,
			CardType = source.CardType,
			TargetType = source.TargetType,
			EnergyCost = source.EnergyCost,
			EnergyCostX = source.EnergyCostX,
			StarCost = source.StarCost,
			StarCostX = source.StarCostX,
			ReplayCount = source.ReplayCount,
			DrawCostReduction = source.DrawCostReduction,
			HandDiscardCount = source.HandDiscardCount,
			Keywords = source.Keywords != null ? new HashSet<CardKeyword>(source.Keywords) : null,
			TagsToAdd = source.TagsToAdd != null ? new HashSet<CardTag>(source.TagsToAdd) : null,
			TagsToRemove = source.TagsToRemove != null ? new HashSet<CardTag>(source.TagsToRemove) : null,
			CustomTags = source.CustomTags != null ? new HashSet<string>(source.CustomTags, StringComparer.OrdinalIgnoreCase) : null,
			DynamicVarBaseValues = source.DynamicVarBaseValues != null
				? new Dictionary<string, decimal>(source.DynamicVarBaseValues, StringComparer.Ordinal)
				: null,
			PowerAmounts = source.PowerAmounts != null
				? new Dictionary<ModelId, decimal>(source.PowerAmounts)
				: null,
			SlyGrantDuration = source.SlyGrantDuration,
			SlyGrantTurns = source.SlyGrantTurns,
			TemporaryStrengthDuration = source.TemporaryStrengthDuration,
			TemporaryStrengthTurns = source.TemporaryStrengthTurns,
			TemporaryDexterityDuration = source.TemporaryDexterityDuration,
			TemporaryDexterityTurns = source.TemporaryDexterityTurns,
			TemporaryFocusDuration = source.TemporaryFocusDuration,
			TemporaryFocusTurns = source.TemporaryFocusTurns,
			EnchantmentId = source.EnchantmentId,
			EnchantmentAmount = source.EnchantmentAmount,
			AfflictionId = source.AfflictionId,
			AfflictionAmount = source.AfflictionAmount,
			PortraitSourceCardId = source.PortraitSourceCardId,
			CustomPortraitFile = source.CustomPortraitFile,
			CosmeticStylePreset = source.CosmeticStylePreset,
			CosmeticAnimationPreset = source.CosmeticAnimationPreset,
			CosmeticVfxPreset = source.CosmeticVfxPreset,
			CosmeticVfxAttach = source.CosmeticVfxAttach,
			CosmeticPlayAttackerAnim = source.CosmeticPlayAttackerAnim,
			HideCosmeticCostOrb = source.HideCosmeticCostOrb,
			HideCosmeticCostNumber = source.HideCosmeticCostNumber,
			HideCosmeticNameBanner = source.HideCosmeticNameBanner,
			HideCosmeticNameText = source.HideCosmeticNameText,
			HideCosmeticTypeBadge = source.HideCosmeticTypeBadge,
			HideCosmeticTextBackground = source.HideCosmeticTextBackground,
			HideCosmeticBodyText = source.HideCosmeticBodyText,
			HideCosmeticAncientInnerBorder = source.HideCosmeticAncientInnerBorder,
			ExtraEffects = source.ExtraEffects != null
				? source.ExtraEffects.Select(e => e != null
					? CardEditorExtraEffects.CloneEffect(e)
					: null!).ToList()
				: null,
			Upgrade = source.Upgrade != null && !source.Upgrade.IsEmpty()
				? new CardUpgradeOverride
				{
					ModifiedBaseTextEnabled = source.Upgrade.ModifiedBaseTextEnabled,
					ModifiedBaseText = source.Upgrade.ModifiedBaseText,
					EnergyCostDelta = source.Upgrade.EnergyCostDelta,
					StarCostDelta = source.Upgrade.StarCostDelta,
					ReplayCountDelta = source.Upgrade.ReplayCountDelta,
					EnchantmentId = source.Upgrade.EnchantmentId,
					EnchantmentAmount = source.Upgrade.EnchantmentAmount,
					AfflictionId = source.Upgrade.AfflictionId,
					AfflictionAmount = source.Upgrade.AfflictionAmount,
					ExtraEffectNumericFieldsAreDeltas = source.Upgrade.ExtraEffectNumericFieldsAreDeltas,
					KeywordsToAdd = source.Upgrade.KeywordsToAdd != null ? new HashSet<CardKeyword>(source.Upgrade.KeywordsToAdd) : null,
					KeywordsToRemove = source.Upgrade.KeywordsToRemove != null ? new HashSet<CardKeyword>(source.Upgrade.KeywordsToRemove) : null,
					DynamicVarDeltas = source.Upgrade.DynamicVarDeltas != null
						? new Dictionary<string, decimal>(source.Upgrade.DynamicVarDeltas, StringComparer.Ordinal)
						: null,
					ExtraEffects = source.Upgrade.ExtraEffects != null
						? source.Upgrade.ExtraEffects.Select(e => e != null
							? CardEditorExtraEffects.CloneEffect(e)
							: null!).ToList()
						: null
				}
				: null
		};
	}

	public static CardModel BuildPreview(CardModel canonicalCard)
	{
		CardModel preview = canonicalCard.ToMutable();
		ApplyTo(preview);
		CardEditorUpgradeDeltaDebugLog.LogCardState("Overrides.BuildPreview.afterApplyTo", preview);
		return preview;
	}

	public static void ApplyTo(CardModel card)
	{
		if (!card.IsMutable)
		{
			return;
		}
		if (SuppressAllOverrides)
		{
			return;
		}
		if (!_overrides.TryGetValue(card.Id, out CardOverride overrideData))
		{
			return;
		}
		ApplyOverride(card, overrideData);
	}

	public static void ApplyOverrideToCard(CardModel card, CardOverride overrideData)
	{
		if (!card.IsMutable)
		{
			return;
		}
		ApplyOverride(card, overrideData);
	}

	// Encounter code can overwrite a card's DynamicVars AFTER creation already applied the user's
	// override (Knowledge Demon sets Disintegration's escalating damage post-CreateCard), so the
	// edit never shows. Re-asserts only the override's absolute var values on a live instance.
	public static void ReassertDynamicVarBaseValues(CardModel? card)
	{
		if (card == null || !card.IsMutable || SuppressAllOverrides)
		{
			return;
		}
		if (!_overrides.TryGetValue(card.Id, out CardOverride overrideData)
			|| overrideData.DynamicVarBaseValues == null
			|| overrideData.DynamicVarBaseValues.Count == 0)
		{
			return;
		}

		try
		{
			foreach ((string key, decimal value) in overrideData.DynamicVarBaseValues)
			{
				if (card.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					dynamicVar.BaseValue = value;
				}
			}
		}
		catch
		{
		}
	}

	private static void ApplyOverride(CardModel card, CardOverride overrideData)
	{
		CardEditorUpgradeDeltaDebugLog.LogCardState("Overrides.ApplyOverride.before", card, overrideData);
		TryGet(card, out CardOverride previousOverride);
		RuntimeEnchantmentSnapshot? runtimeEnchantmentToRestore = CaptureRuntimeEnchantmentBeforeExplicitNone(card, overrideData, previousOverride);
		SetInstanceOverride(card, overrideData);

		if (!string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			TrySetPool(card, overrideData.PoolTitle);
		}
		if (overrideData.Rarity is CardRarity overrideRarity && overrideRarity != CardRarity.None)
		{
			TrySetRarity(card, overrideRarity);
		}
		if (overrideData.CardType is CardType overrideCardType && overrideCardType != CardType.None)
		{
			TrySetCardType(card, overrideCardType);
		}
		if (overrideData.TargetType is TargetType overrideTargetType && overrideTargetType != TargetType.None)
		{
			TrySetTargetType(card, overrideTargetType);
		}
		if (overrideData.EnergyCostX.HasValue)
		{
			InvalidateEnergyCostCache(card);
			try
			{
				_ = card.EnergyCost;
				card.InvokeEnergyCostChanged();
			}
			catch
			{
			}
		}
		if (overrideData.EnergyCost.HasValue && !card.EnergyCost.CostsX)
		{
			card.EnergyCost.SetCustomBaseCost(overrideData.EnergyCost.Value);
		}
		if (overrideData.StarCost.HasValue && !card.HasStarCostX)
		{
			TrySetBaseStarCost(card, overrideData.StarCost.Value);
		}
		if (overrideData.ReplayCount.HasValue)
		{
			card.BaseReplayCount = overrideData.ReplayCount.Value;
		}
		ApplyActiveUpgradeCostOverrides(card, overrideData);
		if (overrideData.Keywords != null)
		{
			HashSet<CardKeyword> desired = overrideData.Keywords;
			foreach (CardKeyword keyword in card.Keywords.ToArray())
			{
				if (!desired.Contains(keyword))
				{
					card.RemoveKeyword(keyword);
				}
			}
			foreach (CardKeyword keyword in desired)
			{
				if (keyword != CardKeyword.None && !card.Keywords.Contains(keyword))
				{
					card.AddKeyword(keyword);
				}
			}
		}
		if (overrideData.DynamicVarBaseValues != null)
		{
			foreach ((string key, decimal value) in overrideData.DynamicVarBaseValues)
			{
				if (card.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					dynamicVar.BaseValue = value;
				}
			}
		}
		ApplyEnchantmentOverride(card, overrideData);
		RestoreRuntimeEnchantmentAfterExplicitNone(card, runtimeEnchantmentToRestore);
		ApplyAfflictionOverride(card, overrideData);
		ReapplyRuntimeModifiers(card);
	}

	private static void ApplyActiveUpgradeCostOverrides(CardModel card, CardOverride overrideData)
	{
		if (card == null || overrideData?.Upgrade == null || card.GetSafeCurrentUpgradeLevel() <= 0)
		{
			return;
		}

		CardUpgradeOverride upgrade = overrideData.Upgrade;
		if (!upgrade.EnergyCostDelta.HasValue && !upgrade.StarCostDelta.HasValue && !upgrade.ReplayCountDelta.HasValue)
		{
			return;
		}

		if (upgrade.EnergyCostDelta.HasValue
			&& !card.EnergyCost.CostsX
			&& TryResolveBaseEnergyCost(card, overrideData, out int baseEnergyCost))
		{
			try
			{
				MarkEnergyCostJustUpgraded(card);
				card.EnergyCost.SetCustomBaseCost(Math.Max(-1, baseEnergyCost + upgrade.EnergyCostDelta.Value));
			}
			catch
			{
			}
		}

		if (upgrade.StarCostDelta.HasValue
			&& !card.HasStarCostX
			&& TryResolveBaseStarCost(card, overrideData, out int baseStarCost))
		{
			try
			{
				SetBaseStarCostUnsafe(card, Math.Max(-1, baseStarCost + upgrade.StarCostDelta.Value));
				MarkStarCostJustUpgraded(card);
			}
			catch
			{
			}
		}

		if (upgrade.ReplayCountDelta.HasValue
			&& TryResolveBaseReplayCount(card, overrideData, out int baseReplayCount))
		{
			try
			{
				card.BaseReplayCount = Math.Max(0, baseReplayCount + upgrade.ReplayCountDelta.Value);
			}
			catch
			{
			}
		}
	}

	private static bool TryResolveBaseEnergyCost(CardModel card, CardOverride overrideData, out int cost)
	{
		if (overrideData.EnergyCost.HasValue)
		{
			cost = overrideData.EnergyCost.Value;
			return true;
		}

		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
			cost = canonical.EnergyCost.Canonical;
			return cost >= -1;
		}
		catch
		{
			cost = 0;
			return false;
		}
	}

	private static bool TryResolveBaseStarCost(CardModel card, CardOverride overrideData, out int cost)
	{
		if (overrideData.StarCost.HasValue)
		{
			cost = overrideData.StarCost.Value;
			return true;
		}

		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
			cost = canonical.CanonicalStarCost;
			return cost >= -1;
		}
		catch
		{
			cost = 0;
			return false;
		}
	}

	private static bool TryResolveBaseReplayCount(CardModel card, CardOverride overrideData, out int replayCount)
	{
		if (overrideData.ReplayCount.HasValue)
		{
			replayCount = overrideData.ReplayCount.Value;
			return true;
		}

		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
			replayCount = canonical.BaseReplayCount;
			return replayCount >= 0;
		}
		catch
		{
			replayCount = 0;
			return false;
		}
	}

	private static void TrySetCardType(CardModel card, CardType type)
	{
		try
		{
			_cardTypeBackingField?.SetValue(card, type);
		}
		catch
		{
		}
	}

	private static void TrySetRarity(CardModel card, CardRarity rarity)
	{
		try
		{
			_rarityBackingField?.SetValue(card, rarity);
		}
		catch
		{
		}
	}

	private static void TrySetTargetType(CardModel card, TargetType targetType)
	{
		try
		{
			_targetTypeBackingField?.SetValue(card, targetType);
		}
		catch
		{
		}
	}

	private static void TrySetPool(CardModel card, string poolTitle)
	{
		try
		{
			string trimmed = poolTitle?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return;
			}

			CardPoolModel? pool = ModelDb.AllCardPools.FirstOrDefault(p =>
				string.Equals(p.Title, trimmed, StringComparison.OrdinalIgnoreCase));
			if (pool == null)
			{
				return;
			}

			_poolField?.SetValue(card, pool);
		}
		catch
		{
		}
	}

	private static void TrySetBaseStarCost(CardModel card, int cost)
	{
		try
		{
			if (_baseStarCostSetter != null)
			{
				_baseStarCostSetter.Invoke(card, new object[] { cost });
				return;
			}
		}
		catch
		{
		}

		try
		{
			if (card.HasStarCostX)
			{
				return;
			}
			_baseStarCostField?.SetValue(card, cost);
			_starCostSetField?.SetValue(card, true);
		}
		catch
		{
		}

		try
		{
			NotifyStarCostChanged(card);
		}
		catch
		{
		}
	}

	internal static void NotifyStarCostChanged(CardModel card)
	{
		if (card == null)
		{
			return;
		}

		try
		{
			(_starCostChangedField?.GetValue(card) as Action)?.Invoke();
		}
		catch
		{
		}
	}

	internal static void InvalidateEnergyCostCache(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}
		try
		{
			_energyCostField?.SetValue(card, null);
		}
		catch
		{
		}
	}

	internal static void SetBaseStarCostUnsafe(CardModel card, int cost)
	{
		TrySetBaseStarCost(card, cost);
	}

	private static RuntimeEnchantmentSnapshot? CaptureRuntimeEnchantmentBeforeExplicitNone(CardModel card, CardOverride overrideData, CardOverride? previousOverride)
	{
		if (card?.Enchantment == null || overrideData.EnchantmentId != ModelId.none)
		{
			return null;
		}
		if (!IsRuntimeCardInstance(card))
		{
			return null;
		}
		if (previousOverride?.EnchantmentId is ModelId previousId
			&& previousId != ModelId.none
			&& previousId == card.Enchantment.Id)
		{
			return null;
		}

		return new RuntimeEnchantmentSnapshot
		{
			Id = card.Enchantment.Id,
			Amount = Math.Max(1, card.Enchantment.Amount)
		};
	}

	private static bool IsRuntimeCardInstance(CardModel card)
	{
		try
		{
			if (card.GetConcreteCombatState() != null)
			{
				return true;
			}
		}
		catch
		{
		}

		try
		{
			if ((card.Pile?.Type ?? PileType.None) != PileType.None)
			{
				return true;
			}
		}
		catch
		{
		}

		try
		{
			if (card.DeckVersion != null)
			{
				return true;
			}
		}
		catch
		{
		}

		try
		{
			return card.Owner?.PlayerCombatState != null;
		}
		catch
		{
			return false;
		}
	}

	private static void RestoreRuntimeEnchantmentAfterExplicitNone(CardModel card, RuntimeEnchantmentSnapshot? snapshot)
	{
		if (card == null || snapshot == null || snapshot.Id == ModelId.none || snapshot.Amount <= 0)
		{
			return;
		}
		if (card.Enchantment != null)
		{
			return;
		}

		EnchantmentModel? enchantment = ModelDb.GetByIdOrNull<EnchantmentModel>(snapshot.Id)?.ToMutable();
		if (enchantment == null)
		{
			return;
		}

		try
		{
			card.EnchantInternal(enchantment, Math.Max(1, snapshot.Amount));
			card.Enchantment?.ModifyCard();
			card.FinalizeUpgradeInternal();
			card.InvokeEnergyCostChanged();
		}
		catch
		{
		}
	}

	private static void ApplyEnchantmentOverride(CardModel card, CardOverride overrideData)
	{
		if (overrideData.EnchantmentId == null)
		{
			return;
		}
		ModelId enchantmentId = overrideData.EnchantmentId;
		if (enchantmentId == ModelId.none)
		{
			card.ClearEnchantmentInternal();
			return;
		}
		int amount = Math.Max(1, overrideData.EnchantmentAmount ?? 1);
		EnchantmentModel enchantment = ModelDb.GetById<EnchantmentModel>(enchantmentId).ToMutable();
		card.ClearEnchantmentInternal();
		card.EnchantInternal(enchantment, amount);
		card.Enchantment?.ModifyCard();
		card.FinalizeUpgradeInternal();
	}

	private static void ApplyAfflictionOverride(CardModel card, CardOverride overrideData)
	{
		if (overrideData.AfflictionId == null)
		{
			return;
		}
		ModelId afflictionId = overrideData.AfflictionId;
		if (afflictionId == ModelId.none)
		{
			TryClearAffliction(card);
			return;
		}
		int amount = Math.Max(1, overrideData.AfflictionAmount ?? 1);
		AfflictionModel affliction = ModelDb.GetById<AfflictionModel>(afflictionId).ToMutable();

		if (card.Owner?.PlayerCombatState != null)
		{
			try
			{
				card.ClearAfflictionInternal();
				card.AfflictInternal(affliction, amount);
				affliction.AfterApplied();
				return;
			}
			catch
			{
			}
		}

		SetAfflictionPreviewSafe(card, affliction, amount);
	}

	internal static void ReapplyRuntimeModifiers(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}

		bool changed = false;
		try
		{
			if (card.Enchantment != null)
			{
				card.Enchantment.ModifyCard();
				card.FinalizeUpgradeInternal();
				changed = true;
			}
		}
		catch
		{
		}

		try
		{
			if (card.Affliction != null)
			{
				card.Affliction.AfterApplied();
				changed = true;
			}
		}
		catch
		{
		}

		if (changed)
		{
			try
			{
				card.InvokeEnergyCostChanged();
			}
			catch
			{
			}
		}
	}

	private static void TryClearAffliction(CardModel card)
	{
		if (card.Affliction == null)
		{
			return;
		}
		if (card.Owner?.PlayerCombatState != null)
		{
			try
			{
				card.ClearAfflictionInternal();
				return;
			}
			catch
			{
			}
		}
		try
		{
			card.Affliction?.ClearInternal();
		}
		catch
		{
		}
		try
		{
			_cardAfflictionSetter?.Invoke(card, new object?[] { null });
		}
		catch
		{
		}
	}

	private static void SetAfflictionPreviewSafe(CardModel card, AfflictionModel affliction, int amount)
	{
		TryClearAffliction(card);
		try
		{
			affliction.Card = card;
		}
		catch
		{
		}
		try
		{
			_afflictionAmountField?.SetValue(affliction, amount);
		}
		catch
		{
		}
		try
		{
			_cardAfflictionSetter?.Invoke(card, new object?[] { affliction });
		}
		catch
		{
		}
	}

	public static void ApplyCanonicalValues(CardModel card)
	{
		if (!card.IsMutable)
		{
			return;
		}
		SetInstanceOverride(card, null);
		CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
		InvalidateEnergyCostCache(card);
		try
		{
			_poolField?.SetValue(card, canonical.Pool);
		}
		catch
		{
		}
		TrySetRarity(card, canonical.Rarity);
		TrySetCardType(card, canonical.Type);
		TrySetTargetType(card, canonical.TargetType);
		if (!card.EnergyCost.CostsX)
		{
			card.EnergyCost.SetCustomBaseCost(canonical.EnergyCost.Canonical);
		}
		if (!card.HasStarCostX)
		{
			TrySetBaseStarCost(card, canonical.CanonicalStarCost);
		}
		card.BaseReplayCount = canonical.BaseReplayCount;
		HashSet<CardKeyword> canonicalKeywords = new HashSet<CardKeyword>(canonical.CanonicalKeywords);
		foreach (CardKeyword keyword in card.Keywords.ToArray())
		{
			if (!canonicalKeywords.Contains(keyword))
			{
				card.RemoveKeyword(keyword);
			}
		}
		foreach (CardKeyword keyword in canonicalKeywords)
		{
			if (!card.Keywords.Contains(keyword))
			{
				card.AddKeyword(keyword);
			}
		}
		foreach (var pair in canonical.DynamicVars)
		{
			if (card.DynamicVars.TryGetValue(pair.Key, out var dynamicVar))
			{
				dynamicVar.BaseValue = pair.Value.BaseValue;
			}
		}

		// Preserve run-time state (enchantments/afflictions) when syncing existing cards.
		// Do not call CanEnchant/CanAfflict on an already attached modifier: vanilla rejects
		// non-stackable modifiers when the card already has that same modifier attached.
		ReapplyRuntimeModifiers(card);
	}

	public static void ApplyToExistingCards(ModelId id)
	{
		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}
		foreach (Player player in runState.Players)
		{
			foreach (var pile in player.Piles)
			{
				foreach (CardModel card in pile.Cards)
				{
					if (card.Id == id)
					{
						ApplyToExistingCardInstance(card);
					}
				}
			}
		}
	}

	public static void ApplyAllToExistingCards()
	{
		if (_overrides.Count == 0)
		{
			return;
		}
		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}
		foreach (Player player in runState.Players)
		{
			foreach (var pile in player.Piles)
			{
				foreach (CardModel card in pile.Cards)
				{
					ApplyToExistingCardInstance(card);
				}
			}
		}
	}

	// Bug list #14: manual "Refresh Live Cards". Rebuilds EVERY live card instance in the run
	// (Player.Piles spans the run deck AND the in-combat hand/draw/discard/exhaust piles) from its
	// canonical definition + effective override: full downgrade -> canonical -> override -> re-upgrade,
	// runtime modifiers preserved, vanilla-parity vars re-derived, table visuals redrawn. Recovery
	// tool for base/upgrade desyncs that previously needed a full game restart. Unlike
	// ApplyAllToExistingCards this does not require any override to exist - created-card instances
	// resync from their definitions too.
	public static int RefreshAllLiveCardData()
	{
		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null)
		{
			return 0;
		}

		int refreshed = 0;
		foreach (Player player in runState.Players)
		{
			foreach (var pile in player.Piles)
			{
				foreach (CardModel card in pile.Cards.ToList())
				{
					if (card == null)
					{
						continue;
					}

					try
					{
						ApplyToExistingCardInstance(card);
						refreshed++;
					}
					catch
					{
						// one broken instance must not abort the sweep
					}

					try
					{
						CardEditorExtraEffects.RefreshCardVisuals(card);
					}
					catch
					{
						// a failed visual redraw must not abort the data resync
					}
				}
			}
		}

		return refreshed;
	}

	private static void ApplyToExistingCardInstance(CardModel card)
	{
		RefreshCardAfterUpgradeStateChanged(card);
		CardEditorRunSelfScalingState.TryRestoreCard(card, cardAlreadyRebased: true);
		// Live created-card instances re-derive their vanilla-parity DynamicVars (Damage/Block
		// mirrors for Thrash-style readers) from the changed definition on next access.
		CardEditorExtraEffects.InvalidateVanillaParityVars(card);
	}

	internal static void RefreshCardAfterUpgradeStateChanged(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}

		int desiredUpgradeLevel = Math.Max(0, card.CurrentUpgradeLevel);
		bool prevSuppressAll = SuppressAllOverrides;
		SuppressAllOverrides = true;

		try
		{
			while (card.CurrentUpgradeLevel > 0)
			{
				card.DowngradeInternal();
			}
		}
		catch
		{
			try
			{
				ApplyCanonicalValues(card);
			}
			catch
			{
			}
			desiredUpgradeLevel = 0;
		}
		finally
		{
			SuppressAllOverrides = prevSuppressAll;
		}

		try
		{
			ApplyCanonicalValues(card);
		}
		catch
		{
		}

		ApplyTo(card);

		for (int i = 0; i < desiredUpgradeLevel; i++)
		{
			try
			{
				card.UpgradeInternal();
				card.FinalizeUpgradeInternal();
			}
			catch
			{
				break;
			}
		}

		ReapplyRuntimeModifiers(card);

		try
		{
			if (card.GetConcreteCombatState() != null && (card.Pile?.Type ?? PileType.None) == PileType.Hand)
			{
				CardEditorExtraEffects.ApplyIntrinsicTimedCardCostsLessOnEnterHand(card.GetConcreteCombatState(), card);
			}
		}
		catch
		{
		}
	}

	public static void ResetExistingCards(ModelId id)
	{
		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}
		foreach (Player player in runState.Players)
		{
			foreach (var pile in player.Piles)
			{
				foreach (CardModel card in pile.Cards)
				{
					if (card.Id == id)
					{
						ApplyCanonicalValues(card);
					}
				}
			}
		}
	}

	public static void ResetExistingCardsForIds(IEnumerable<ModelId> ids)
	{
		if (ids == null)
		{
			return;
		}
		HashSet<ModelId> idSet = ids as HashSet<ModelId> ?? ids.ToHashSet();
		if (idSet.Count == 0)
		{
			return;
		}

		RunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState == null)
		{
			return;
		}

		foreach (Player player in runState.Players)
		{
			foreach (var pile in player.Piles)
			{
				foreach (CardModel card in pile.Cards)
				{
					if (idSet.Contains(card.Id))
					{
						ResetCardToVanillaPreservingUpgrade(card);
					}
				}
			}
		}
	}

	private static void ResetCardToVanillaPreservingUpgrade(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}

		int upgradeLevel = card.CurrentUpgradeLevel;
		CardModel canonical = ModelDb.GetById<CardModel>(card.Id);

		try
		{
			card.DowngradeInternal();
		}
		catch
		{
			ApplyCanonicalValues(card);
			upgradeLevel = 0;
		}

		try
		{
			_poolField?.SetValue(card, canonical.Pool);
		}
		catch
		{
		}
		TrySetRarity(card, canonical.Rarity);
		TrySetCardType(card, canonical.Type);
		TrySetTargetType(card, canonical.TargetType);
		card.BaseReplayCount = canonical.BaseReplayCount;

		for (int i = 0; i < upgradeLevel; i++)
		{
			try
			{
				card.UpgradeInternal();
			}
			catch
			{
				break;
			}
		}

		try
		{
			card.FinalizeUpgradeInternal();
		}
		catch
		{
		}

		try
		{
			card.Enchantment?.ModifyCard();
		}
		catch
		{
		}
		try
		{
			card.Affliction?.AfterApplied();
		}
		catch
		{
		}
	}
}
