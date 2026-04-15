using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

public sealed class CardOverride
{
	public string? PoolTitle { get; set; }
	public CardRarity? Rarity { get; set; }
	public CardType? CardType { get; set; }
	public TargetType? TargetType { get; set; }
	public int? EnergyCost { get; set; }
	public bool? EnergyCostX { get; set; }
	public int? StarCost { get; set; }
	public bool? StarCostX { get; set; }
	public int? ReplayCount { get; set; }
	public int? DrawCostReduction { get; set; } // for cards like Kingly Kick
	public HashSet<CardKeyword>? Keywords { get; set; }
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
	public CardUpgradeOverride? Upgrade { get; set; }

	public bool IsEmpty()
	{
		return string.IsNullOrWhiteSpace(PoolTitle)
			&& Rarity == null
			&& CardType == null
			&& TargetType == null
			&& EnergyCost == null
			&& EnergyCostX == null
			&& StarCost == null
			&& StarCostX == null
			&& ReplayCount == null
			&& DrawCostReduction == null
			&& (Keywords == null || Keywords.Count == 0)
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
			&& (Upgrade == null || Upgrade.IsEmpty());
	}
}

public enum CardKeywordGrantDuration
{
	ThisTurn = 0,
	ThisCombat = 1,
	Turns = 2
}

public sealed class CardUpgradeOverride
{
	public int? EnergyCostDelta { get; set; }
	public int? StarCostDelta { get; set; }
	public int? ReplayCountDelta { get; set; }
	public HashSet<CardKeyword>? KeywordsToAdd { get; set; }
	public HashSet<CardKeyword>? KeywordsToRemove { get; set; }
	public Dictionary<string, decimal>? DynamicVarDeltas { get; set; }
	public List<CardExtraEffect>? ExtraEffects { get; set; }

	public bool IsEmpty()
	{
		return EnergyCostDelta == null
			&& StarCostDelta == null
			&& ReplayCountDelta == null
			&& (KeywordsToAdd == null || KeywordsToAdd.Count == 0)
			&& (KeywordsToRemove == null || KeywordsToRemove.Count == 0)
			&& (DynamicVarDeltas == null || DynamicVarDeltas.Count == 0)
			&& (ExtraEffects == null || ExtraEffects.Count == 0);
	}
}

public static class CardEditorOverrides
{
	private static readonly Dictionary<ModelId, CardOverride> _overrides = new();
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

	internal static bool SuppressAllOverrides { get; set; }
	internal static bool SuppressUpgradeOverrides { get; set; }

	public static bool HasAnyOverrides => _overrides.Count > 0;

	public static IReadOnlyDictionary<ModelId, CardOverride> AllOverrides => _overrides;

	public static bool TryGet(ModelId id, out CardOverride overrideData)
	{
		return _overrides.TryGetValue(id, out overrideData);
	}

	internal static bool TryGetEffectiveOverride(ModelId id, out CardOverride overrideData)
	{
		if (CardEditorUiState.TryGetDraftOverride(id, out overrideData))
		{
			return true;
		}
		return _overrides.TryGetValue(id, out overrideData);
	}

	public static CardOverride? Get(ModelId id)
	{
		_overrides.TryGetValue(id, out CardOverride overrideData);
		return overrideData;
	}

	public static void Set(ModelId id, CardOverride overrideData)
	{
		if (overrideData == null || overrideData.IsEmpty())
		{
			_overrides.Remove(id);
			return;
		}
		_overrides[id] = overrideData;
	}

	public static void Clear(ModelId id)
	{
		_overrides.Remove(id);
	}

	public static void ClearAll()
	{
		_overrides.Clear();
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
		if (overrides == null)
		{
			return;
		}
		foreach ((ModelId key, CardOverride value) in overrides)
		{
			if (value == null || value.IsEmpty())
			{
				continue;
			}
			_overrides[key] = CloneOverride(value);
		}
	}

	private static CardOverride CloneOverride(CardOverride source)
	{
		return new CardOverride
		{
			PoolTitle = source.PoolTitle,
			Rarity = source.Rarity,
			CardType = source.CardType,
			TargetType = source.TargetType,
			EnergyCost = source.EnergyCost,
			EnergyCostX = source.EnergyCostX,
			StarCost = source.StarCost,
			StarCostX = source.StarCostX,
			ReplayCount = source.ReplayCount,
			DrawCostReduction = source.DrawCostReduction,
			Keywords = source.Keywords != null ? new HashSet<CardKeyword>(source.Keywords) : null,
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
			ExtraEffects = source.ExtraEffects != null
				? source.ExtraEffects.Select(e => e != null
					? new CardExtraEffect
					{
						Kind = e.Kind,
						Target = e.Target,
						Amount = e.Amount,
						AmountIsX = e.AmountIsX,
						AmountXPlus = e.AmountXPlus,
						Trigger = e.Trigger,
						Timing = e.Timing,
						Turns = e.Turns,
						Duration = e.Duration,
						AsPower = e.AsPower,
						RepeatIsX = e.RepeatIsX,
						RepeatCount = e.RepeatCount,
						TriggerCardPool = e.TriggerCardPool,
						TriggerCardType = e.TriggerCardType,
						TriggerCardFilter = e.TriggerCardFilter,
						CreatedCardsCostDuration = e.CreatedCardsCostDuration,
						CreatedCardsCostTurns = e.CreatedCardsCostTurns,
						CardCostsLessDuration = e.CardCostsLessDuration,
						CardCostsLessTurns = e.CardCostsLessTurns,
						CardCostsLessMode = e.CardCostsLessMode,
						GeneratedCardPool = e.GeneratedCardPool,
						GeneratedCardType = e.GeneratedCardType,
						ScaleMode = e.ScaleMode,
						CountEvent = e.CountEvent,
						CountWindow = e.CountWindow,
						CountTurns = e.CountTurns,
						CountCardPile = e.CountCardPile,
						CountCardPool = e.CountCardPool,
						CountCardType = e.CountCardType,
						CountCardFilter = e.CountCardFilter,
						CountOnlyBlockCards = e.CountOnlyBlockCards,
						HistoryScalingIncludesBase = e.HistoryScalingIncludesBase,
						GrantToCard = e.GrantToCard,
						CardSelectionMode = e.CardSelectionMode,
						CardSelectionPile = e.CardSelectionPile,
						CardGrantDuration = e.CardGrantDuration,
						CardGrantTurns = e.CardGrantTurns,
						CardSelectionCountIsX = e.CardSelectionCountIsX,
						CardSelectionCount = e.CardSelectionCount,
						CardSelectionPool = e.CardSelectionPool,
						CardSelectionType = e.CardSelectionType,
						CardSelectionFilter = e.CardSelectionFilter,
						MoveToPile = e.MoveToPile,
						MoveToPosition = e.MoveToPosition,
						SpecificCardId = e.SpecificCardId
					}
					: null!).ToList()
				: null,
			Upgrade = source.Upgrade != null && !source.Upgrade.IsEmpty()
				? new CardUpgradeOverride
				{
					EnergyCostDelta = source.Upgrade.EnergyCostDelta,
					StarCostDelta = source.Upgrade.StarCostDelta,
					ReplayCountDelta = source.Upgrade.ReplayCountDelta,
					KeywordsToAdd = source.Upgrade.KeywordsToAdd != null ? new HashSet<CardKeyword>(source.Upgrade.KeywordsToAdd) : null,
					KeywordsToRemove = source.Upgrade.KeywordsToRemove != null ? new HashSet<CardKeyword>(source.Upgrade.KeywordsToRemove) : null,
					DynamicVarDeltas = source.Upgrade.DynamicVarDeltas != null
						? new Dictionary<string, decimal>(source.Upgrade.DynamicVarDeltas, StringComparer.Ordinal)
						: null,
					ExtraEffects = source.Upgrade.ExtraEffects != null
						? source.Upgrade.ExtraEffects.Select(e => e != null
							? new CardExtraEffect
							{
								Kind = e.Kind,
								Target = e.Target,
								Amount = e.Amount,
								AmountIsX = e.AmountIsX,
								AmountXPlus = e.AmountXPlus,
								Trigger = e.Trigger,
								Timing = e.Timing,
								Turns = e.Turns,
								Duration = e.Duration,
								AsPower = e.AsPower,
								RepeatIsX = e.RepeatIsX,
								RepeatCount = e.RepeatCount,
								TriggerCardPool = e.TriggerCardPool,
								TriggerCardType = e.TriggerCardType,
								TriggerCardFilter = e.TriggerCardFilter,
								CreatedCardsCostDuration = e.CreatedCardsCostDuration,
								CreatedCardsCostTurns = e.CreatedCardsCostTurns,
								CardCostsLessDuration = e.CardCostsLessDuration,
								CardCostsLessTurns = e.CardCostsLessTurns,
								CardCostsLessMode = e.CardCostsLessMode,
								GeneratedCardPool = e.GeneratedCardPool,
								GeneratedCardType = e.GeneratedCardType,
								ScaleMode = e.ScaleMode,
								CountEvent = e.CountEvent,
								CountWindow = e.CountWindow,
								CountTurns = e.CountTurns,
								CountCardPile = e.CountCardPile,
								CountCardPool = e.CountCardPool,
								CountCardType = e.CountCardType,
								CountCardFilter = e.CountCardFilter,
								CountOnlyBlockCards = e.CountOnlyBlockCards,
								HistoryScalingIncludesBase = e.HistoryScalingIncludesBase,
								GrantToCard = e.GrantToCard,
								CardSelectionMode = e.CardSelectionMode,
								CardSelectionPile = e.CardSelectionPile,
								CardGrantDuration = e.CardGrantDuration,
								CardGrantTurns = e.CardGrantTurns,
								CardSelectionCountIsX = e.CardSelectionCountIsX,
								CardSelectionCount = e.CardSelectionCount,
								CardSelectionPool = e.CardSelectionPool,
								CardSelectionType = e.CardSelectionType,
								CardSelectionFilter = e.CardSelectionFilter,
								MoveToPile = e.MoveToPile,
								MoveToPosition = e.MoveToPosition,
								SpecificCardId = e.SpecificCardId
							}
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

	private static void ApplyOverride(CardModel card, CardOverride overrideData)
	{
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
		ApplyAfflictionOverride(card, overrideData);

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
		card.ClearEnchantmentInternal();
		TryClearAffliction(card);
		try
		{
			card.InvokeEnergyCostChanged();
		}
		catch
		{
		}
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

	private static void ApplyToExistingCardInstance(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}

		int desiredUpgradeLevel = card.CurrentUpgradeLevel;
		if (desiredUpgradeLevel <= 0)
		{
			ApplyTo(card);
			return;
		}

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

		try
		{
			if (card.CombatState != null && (card.Pile?.Type ?? PileType.None) == PileType.Hand)
			{
				CardEditorExtraEffects.ApplyIntrinsicTimedCardCostsLessOnEnterHand(card.CombatState, card);
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
