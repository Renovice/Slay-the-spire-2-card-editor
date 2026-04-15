using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCreatedCardEffectSourceSupport
{
	private sealed class DynamicVarSyncState
	{
		public string? EffectSourceIdsKey { get; set; }
	}

	private static readonly FieldInfo? _dynamicVarsField = typeof(CardModel).GetField("_dynamicVars", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly ConditionalWeakTable<CardModel, DynamicVarSyncState> _dynamicVarSyncStates = new();
	private static readonly ThreadLocal<HashSet<ModelId>> _dynamicVarSyncGuard = new(() => new HashSet<ModelId>());
	private static readonly ThreadLocal<HashSet<ModelId>> _keywordGuard = new(() => new HashSet<ModelId>());
	private static readonly ConcurrentDictionary<Type, MethodInfo?> _onPlayMethodCache = new();

	private static MethodInfo? ResolveOnPlayMethod(Type type)
	{
		for (Type? cursor = type; cursor != null; cursor = cursor.BaseType)
		{
			try
			{
				MethodInfo? method = cursor.GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					return method;
				}
			}
			catch
			{
				// ignored
			}
		}

		return null;
	}

	public static void EnsureEffectSourceDynamicVars(CardModel card)
	{
		EnsureEffectSourceDynamicVars(card, isUpgradePreview: false);
	}

	public static void EnsureEffectSourceDynamicVars(CardModel card, bool isUpgradePreview)
	{
		if (card == null)
		{
			return;
		}
		if (card is not CardEditorCreatedCardBase)
		{
			return;
		}

		HashSet<ModelId> guard = _dynamicVarSyncGuard.Value!;
		if (!guard.Add(card.Id))
		{
			return;
		}

		try
		{
			IReadOnlyList<ModelId> effectSourceIds = GetUnifiedEffectSourceIds(card, isUpgradePreview);
			if (effectSourceIds.Count == 0)
			{
				// Only clean up if we previously synced (avoids allocating a CWT entry)
				if (_dynamicVarSyncStates.TryGetValue(card, out DynamicVarSyncState? existingState)
					&& !string.IsNullOrWhiteSpace(existingState.EffectSourceIdsKey)
					&& _dynamicVarsField != null)
				{
					_dynamicVarsField.SetValue(card, null);
					existingState.EffectSourceIdsKey = null;
				}
				return;
			}

			int desiredUpgradeLevel = card.CurrentUpgradeLevel;
			if (isUpgradePreview)
			{
				desiredUpgradeLevel = Math.Max(desiredUpgradeLevel, 1);
			}

			string idsKey = $"{(isUpgradePreview ? 1 : 0)}|{desiredUpgradeLevel}|{string.Join(";", effectSourceIds)}";

			if (string.IsNullOrEmpty(idsKey))
			{
				// Only clean up if we previously synced (avoids allocating a CWT entry)
				if (_dynamicVarSyncStates.TryGetValue(card, out DynamicVarSyncState? existingState)
					&& !string.IsNullOrWhiteSpace(existingState.EffectSourceIdsKey)
					&& _dynamicVarsField != null)
				{
					_dynamicVarsField.SetValue(card, null);
					existingState.EffectSourceIdsKey = null;
				}
				return;
			}

			if (_dynamicVarsField == null)
			{
				return;
			}

			DynamicVarSyncState state = _dynamicVarSyncStates.GetOrCreateValue(card);
			if (string.Equals(state.EffectSourceIdsKey, idsKey, StringComparison.Ordinal))
			{
				return;
			}

			List<CardModel> effectSourceCards = BuildEffectSourceCards(card, isUpgradePreview, effectSourceIds);
			if (effectSourceCards.Count == 0)
			{
				return;
			}

			try
			{
				// Collect all unique DynamicVars from all sources (first source wins for conflicts)
				Dictionary<string, DynamicVar> seen = new();
				foreach (CardModel sourceCard in effectSourceCards)
				{
					DynamicVarSet cloned = sourceCard.DynamicVars.Clone(card);
					foreach (var kvp in cloned)
					{
						if (!seen.ContainsKey(kvp.Key))
						{
							seen[kvp.Key] = kvp.Value;
						}
					}
				}
				DynamicVarSet mergedVars = new DynamicVarSet(seen.Values);
				mergedVars.InitializeWithOwner(card);
				_dynamicVarsField.SetValue(card, mergedVars);
				state.EffectSourceIdsKey = idsKey;
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed syncing created-card effect source vars for {card.Id}: {ex}");
			}
		}
		finally
		{
			guard.Remove(card.Id);
		}
	}

	private static IReadOnlyList<ModelId> GetUnifiedEffectSourceIds(CardModel createdCard, bool isUpgradePreview)
	{
		try
		{
			// Prefer inline effect sources when present (unified timeline); otherwise fall back to legacy store list.
			List<ModelId> inline = new();
			foreach (CardExtraEffect effect in CardEditorExtraEffects.GetEffectsForDescription(createdCard, isUpgradePreview))
			{
				if (effect == null || effect.Kind != CardExtraEffectKind.RunEffectSourceCard)
				{
					continue;
				}

				if (!TryParseModelId(effect.SpecificCardId, out ModelId id))
				{
					continue;
				}
				if (id == ModelId.none || id == createdCard.Id)
				{
					continue;
				}

				if (!inline.Contains(id))
				{
					inline.Add(id);
				}
			}

			if (inline.Count > 0)
			{
				return inline;
			}
		}
		catch { }

		return CardEditorCreatedCardsStore.GetEffectSourceCardIds(createdCard.Id);
	}

	internal static IReadOnlyList<ModelId> GetRuntimeEffectSourceIds(CardModel createdCard, bool isUpgradePreview = false)
	{
		if (createdCard == null)
		{
			return Array.Empty<ModelId>();
		}

		return GetUnifiedEffectSourceIds(createdCard, isUpgradePreview);
	}

	internal static CardModel? BuildRuntimeEffectSourceCard(CardModel createdCard, ModelId effectSourceId, bool isUpgradePreview = false)
	{
		if (createdCard == null)
		{
			return null;
		}

		return BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview);
	}

	internal static IReadOnlySet<CardKeyword> GetEffectSourceKeywords(CardModel createdCard, bool isUpgradePreview = false)
	{
		HashSet<CardKeyword> keywords = new HashSet<CardKeyword>();
		if (createdCard == null)
		{
			return keywords;
		}

		HashSet<ModelId> guard = _keywordGuard.Value!;
		if (createdCard.Id != null && createdCard.Id != ModelId.none && !guard.Add(createdCard.Id))
		{
			return keywords;
		}

		try
		{
			foreach (CardModel sourceCard in BuildEffectSourceCards(createdCard, isUpgradePreview, GetUnifiedEffectSourceIds(createdCard, isUpgradePreview)))
			{
				if (sourceCard == null)
				{
					continue;
				}

				foreach (CardKeyword keyword in sourceCard.Keywords)
				{
					if (keyword != CardKeyword.None)
					{
						keywords.Add(keyword);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed collecting borrowed effect source keywords for {createdCard.Id}: {ex}");
		}
		finally
		{
			if (createdCard.Id != null && createdCard.Id != ModelId.none)
			{
				guard.Remove(createdCard.Id);
			}
		}

		return keywords;
	}

	private static bool TryParseModelId(string? text, out ModelId id)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				id = ModelId.none;
				return false;
			}

			id = ModelId.Deserialize(text.Trim());
			return true;
		}
		catch
		{
			id = ModelId.none;
			return false;
		}
	}

	public static List<string> GetEffectSourceDescriptions(CardModel createdCard, Creature? target, bool isUpgradePreview)
	{
		List<string> descriptions = new();
		List<CardModel> effectSourceCards = BuildEffectSourceCards(createdCard, isUpgradePreview);

		foreach (CardModel effectSourceCard in effectSourceCards)
		{
			try
			{
				string desc = isUpgradePreview
					? effectSourceCard.GetDescriptionForUpgradePreview()
					: effectSourceCard.GetDescriptionForPile(PileType.None, target);
				if (!string.IsNullOrWhiteSpace(desc))
				{
					descriptions.Add(desc);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed building effect source description for {createdCard.Id}: {ex}");
			}
		}

		return descriptions;
	}

	public static async Task RunEffectSourceOnPlay(CardEditorCreatedCardBase createdCard, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> effectSourceCards = BuildEffectSourceCards(createdCard, isUpgradePreview: false);

		foreach (CardModel effectSourceCard in effectSourceCards)
		{
			MethodInfo? onPlay = _onPlayMethodCache.GetOrAdd(
				effectSourceCard.GetType(),
				ResolveOnPlayMethod);
			if (onPlay == null)
			{
				continue;
			}

			CardPlay proxyPlay = new CardPlay
			{
				Card = effectSourceCard,
				Target = cardPlay.Target,
				ResultPile = cardPlay.ResultPile,
				Resources = cardPlay.Resources,
				IsAutoPlay = cardPlay.IsAutoPlay,
				PlayIndex = cardPlay.PlayIndex,
				PlayCount = cardPlay.PlayCount
			};

			try
			{
				if (onPlay.Invoke(effectSourceCard, new object[] { choiceContext, proxyPlay }) is Task task)
				{
					await task;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Borrowed effect source OnPlay failed for {createdCard.Id} (source {effectSourceCard.Id}): {ex}");
			}
		}
	}

	public static async Task RunSingleEffectSourceOnPlay(CardModel createdCard, PlayerChoiceContext choiceContext, CardPlay cardPlay, ModelId effectSourceId)
	{
		if (createdCard == null || choiceContext == null || cardPlay == null)
		{
			return;
		}
		if (effectSourceId == null || effectSourceId == ModelId.none)
		{
			return;
		}

		CardModel? effectSourceCard = BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview: false);
		if (effectSourceCard == null)
		{
			return;
		}

		MethodInfo? onPlay = _onPlayMethodCache.GetOrAdd(
			effectSourceCard.GetType(),
			ResolveOnPlayMethod);
		if (onPlay == null)
		{
			return;
		}

		CardPlay proxyPlay = new CardPlay
		{
			Card = effectSourceCard,
			Target = cardPlay.Target,
			ResultPile = cardPlay.ResultPile,
			Resources = cardPlay.Resources,
			IsAutoPlay = cardPlay.IsAutoPlay,
			PlayIndex = cardPlay.PlayIndex,
			PlayCount = cardPlay.PlayCount
		};

		try
		{
			if (onPlay.Invoke(effectSourceCard, new object[] { choiceContext, proxyPlay }) is Task task)
			{
				await task;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Borrowed effect source OnPlay failed for {createdCard.Id} (source {effectSourceCard.Id}): {ex}");
		}
	}

	public static string? GetSingleEffectSourceDescription(CardModel createdCard, Creature? target, bool isUpgradePreview, ModelId effectSourceId)
	{
		if (createdCard == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return null;
		}

		CardModel? effectSourceCard = BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview);
		if (effectSourceCard == null)
		{
			return null;
		}

		try
		{
			return isUpgradePreview
				? effectSourceCard.GetDescriptionForUpgradePreview()
				: effectSourceCard.GetDescriptionForPile(PileType.None, target);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed building effect source description for {createdCard.Id} (source {effectSourceCard.Id}): {ex}");
			return null;
		}
	}

	private static List<CardModel> BuildEffectSourceCards(CardModel createdCard, bool isUpgradePreview)
	{
		return BuildEffectSourceCards(createdCard, isUpgradePreview, CardEditorCreatedCardsStore.GetEffectSourceCardIds(createdCard.Id));
	}

	private static List<CardModel> BuildEffectSourceCards(CardModel createdCard, bool isUpgradePreview, IReadOnlyList<ModelId> effectSourceIds)
	{
		List<CardModel> result = new();

		foreach (ModelId effectSourceId in effectSourceIds)
		{
			CardModel? effectSourceCard = BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview);
			if (effectSourceCard != null)
			{
				result.Add(effectSourceCard);
			}
		}

		return result;
	}

	private static CardModel? BuildEffectSourceCard(CardModel createdCard, ModelId effectSourceId, bool isUpgradePreview)
	{
		if (effectSourceId == null || effectSourceId == ModelId.none)
		{
			return null;
		}

		if (createdCard == null || createdCard.Id == ModelId.none)
		{
			return null;
		}

		if (effectSourceId == createdCard.Id)
		{
			return null;
		}

		CardModel? sourceCanonical = ModelDb.GetByIdOrNull<CardModel>(effectSourceId);
		if (sourceCanonical == null)
		{
			return null;
		}

		try
		{
			CardModel effectSourceCard = sourceCanonical.ToMutable();
			CardEditorOverrides.ApplyTo(effectSourceCard);
			SyncEffectSourceCard(effectSourceCard, createdCard, isUpgradePreview);
			return effectSourceCard;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed building borrowed effect source card {effectSourceId} for {createdCard.Id}: {ex}");
			return null;
		}
	}

	private static void SyncEffectSourceCard(CardModel effectSourceCard, CardModel createdCard, bool isUpgradePreview)
	{
		try
		{
			if (createdCard.Owner != null && effectSourceCard.Owner == null)
			{
				effectSourceCard.Owner = createdCard.Owner;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed syncing Owner to effect source card: {ex}");
		}

		int desiredUpgradeLevel = createdCard.CurrentUpgradeLevel;
		if (isUpgradePreview)
		{
			desiredUpgradeLevel = Math.Max(desiredUpgradeLevel, 1);
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(createdCard.Id, out CardOverride draftOverride))
		{
			overrideData = draftOverride;
		}
		else if (CardEditorOverrides.TryGet(createdCard.Id, out CardOverride storedOverride))
		{
			overrideData = storedOverride;
		}

		// Apply base (un-upgraded) numeric overrides before upgrading so we can
		// correctly re-target upgrade deltas for borrowed effect sources.
		if (overrideData != null)
		{
			ApplyBaseNumericOverrides(effectSourceCard, overrideData);
		}

		NumericSnapshot snapshot = CaptureNumericSnapshot(effectSourceCard);

		if (desiredUpgradeLevel > 0)
		{
			bool prevSuppress = CardEditorOverrides.SuppressUpgradeOverrides;
			CardEditorOverrides.SuppressUpgradeOverrides = true;
			try
			{
				for (int i = 0; i < desiredUpgradeLevel; i++)
				{
					effectSourceCard.UpgradeInternal();
				}
				effectSourceCard.FinalizeUpgradeInternal();
			}
			finally
			{
				CardEditorOverrides.SuppressUpgradeOverrides = prevSuppress;
			}
		}

		if (desiredUpgradeLevel > 0 && overrideData?.Upgrade != null && !overrideData.Upgrade.IsEmpty())
		{
			ApplyUpgradeNumericOverrides(effectSourceCard, snapshot, overrideData.Upgrade);
		}

		CardEditorOverrides.SetInstanceOverride(effectSourceCard, BuildEffectSourceRuntimeOverride(effectSourceCard, overrideData));

		try
		{
			effectSourceCard.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed recalculating effect source dynamic vars: {ex}");
		}
	}

	private static CardOverride? BuildEffectSourceRuntimeOverride(CardModel effectSourceCard, CardOverride? createdCardOverride)
	{
		CardOverride? merged = null;
		if (CardEditorOverrides.TryGet(effectSourceCard, out CardOverride sourceOverride))
		{
			merged = CardEditorOverrides.Clone(sourceOverride);
		}

		if (createdCardOverride == null)
		{
			return merged;
		}

		merged ??= new CardOverride();

		if (createdCardOverride.DrawCostReduction.HasValue)
		{
			merged.DrawCostReduction = createdCardOverride.DrawCostReduction;
		}

		if (createdCardOverride.HandDiscardCount.HasValue)
		{
			merged.HandDiscardCount = createdCardOverride.HandDiscardCount;
		}

		if (createdCardOverride.DynamicVarBaseValues != null && createdCardOverride.DynamicVarBaseValues.Count > 0)
		{
			merged.DynamicVarBaseValues ??= new Dictionary<string, decimal>(StringComparer.Ordinal);
			foreach ((string key, decimal value) in createdCardOverride.DynamicVarBaseValues)
			{
				merged.DynamicVarBaseValues[key] = value;
			}
		}

		if (createdCardOverride.PowerAmounts != null && createdCardOverride.PowerAmounts.Count > 0)
		{
			merged.PowerAmounts ??= new Dictionary<ModelId, decimal>();
			foreach ((ModelId powerId, decimal value) in createdCardOverride.PowerAmounts)
			{
				merged.PowerAmounts[powerId] = value;
			}
		}

		if (createdCardOverride.SlyGrantDuration != null)
		{
			merged.SlyGrantDuration = createdCardOverride.SlyGrantDuration;
			merged.SlyGrantTurns = createdCardOverride.SlyGrantTurns;
		}

		if (createdCardOverride.TemporaryStrengthDuration != null)
		{
			merged.TemporaryStrengthDuration = createdCardOverride.TemporaryStrengthDuration;
			merged.TemporaryStrengthTurns = createdCardOverride.TemporaryStrengthTurns;
		}

		if (createdCardOverride.TemporaryDexterityDuration != null)
		{
			merged.TemporaryDexterityDuration = createdCardOverride.TemporaryDexterityDuration;
			merged.TemporaryDexterityTurns = createdCardOverride.TemporaryDexterityTurns;
		}

		if (createdCardOverride.TemporaryFocusDuration != null)
		{
			merged.TemporaryFocusDuration = createdCardOverride.TemporaryFocusDuration;
			merged.TemporaryFocusTurns = createdCardOverride.TemporaryFocusTurns;
		}

		return merged.IsEmpty() ? null : merged;
	}

	private sealed class NumericSnapshot
	{
		public int EnergyCostBase { get; init; }
		public int StarCostBase { get; init; }
		public int ReplayCountBase { get; init; }
		public Dictionary<string, decimal>? DynamicVarBaseValues { get; init; }
	}

	private static NumericSnapshot CaptureNumericSnapshot(CardModel card)
	{
		int energyCostBase = 0;
		try
		{
			energyCostBase = card.EnergyCost.GetWithModifiers(CostModifiers.None);
		}
		catch
		{
		}

		int starCostBase = 0;
		try
		{
			starCostBase = card.BaseStarCost;
		}
		catch
		{
		}

		int replayCountBase = 0;
		try
		{
			replayCountBase = card.BaseReplayCount;
		}
		catch
		{
		}

		Dictionary<string, decimal>? dynamicVarBaseValues = null;
		try
		{
			dynamicVarBaseValues = card.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);
		}
		catch
		{
		}

		return new NumericSnapshot
		{
			EnergyCostBase = energyCostBase,
			StarCostBase = starCostBase,
			ReplayCountBase = replayCountBase,
			DynamicVarBaseValues = dynamicVarBaseValues
		};
	}

	private static void ApplyBaseNumericOverrides(CardModel effectSourceCard, CardOverride overrideData)
	{
		if (overrideData == null)
		{
			return;
		}

		if (overrideData.EnergyCost.HasValue && !effectSourceCard.EnergyCost.CostsX)
		{
			effectSourceCard.EnergyCost.SetCustomBaseCost(overrideData.EnergyCost.Value);
		}

		if (overrideData.ReplayCount.HasValue)
		{
			effectSourceCard.BaseReplayCount = overrideData.ReplayCount.Value;
		}

		if (overrideData.DynamicVarBaseValues != null)
		{
			foreach ((string key, decimal value) in overrideData.DynamicVarBaseValues)
			{
				if (effectSourceCard.DynamicVars.TryGetValue(key, out DynamicVar dynamicVar))
				{
					dynamicVar.BaseValue = value;
				}
			}
		}

	}

	private static void ApplyUpgradeNumericOverrides(CardModel effectSourceCard, NumericSnapshot snapshot, CardUpgradeOverride upgrade)
	{
		if (upgrade.EnergyCostDelta.HasValue && !effectSourceCard.EnergyCost.CostsX)
		{
			int baseCost = snapshot.EnergyCostBase;
			int vanillaUpgraded = effectSourceCard.EnergyCost.GetWithModifiers(CostModifiers.None);
			int vanillaDelta = vanillaUpgraded - baseCost;
			int desiredDelta = upgrade.EnergyCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				try
				{
					CardEditorOverrides.MarkEnergyCostJustUpgraded(effectSourceCard);
					effectSourceCard.EnergyCost.SetCustomBaseCost(Math.Max(-1, vanillaUpgraded + adjust));
				}
				catch
				{
				}
			}
		}

		if (upgrade.StarCostDelta.HasValue && !effectSourceCard.HasStarCostX)
		{
			int vanillaUpgraded = effectSourceCard.BaseStarCost;
			int vanillaDelta = vanillaUpgraded - snapshot.StarCostBase;
			int desiredDelta = upgrade.StarCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				CardEditorOverrides.SetBaseStarCostUnsafe(effectSourceCard, Math.Max(-1, vanillaUpgraded + adjust));
			}
		}

		if (upgrade.ReplayCountDelta.HasValue)
		{
			int vanillaUpgraded = effectSourceCard.BaseReplayCount;
			int vanillaDelta = vanillaUpgraded - snapshot.ReplayCountBase;
			int desiredDelta = upgrade.ReplayCountDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				effectSourceCard.BaseReplayCount = Math.Max(0, vanillaUpgraded + adjust);
			}
		}

		if (upgrade.DynamicVarDeltas != null && upgrade.DynamicVarDeltas.Count > 0 && snapshot.DynamicVarBaseValues != null)
		{
			foreach ((string key, decimal desiredDelta) in upgrade.DynamicVarDeltas)
			{
				if (!snapshot.DynamicVarBaseValues.TryGetValue(key, out decimal baseValue))
				{
					continue;
				}
				if (!effectSourceCard.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					continue;
				}
				decimal vanillaUpgraded = dynamicVar.BaseValue;
				decimal vanillaDelta = vanillaUpgraded - baseValue;
				decimal adjust = desiredDelta - vanillaDelta;
				if (adjust != 0m)
				{
					dynamicVar.UpgradeValueBy(adjust);
				}
			}
		}

		if (upgrade.EnchantmentId != null || upgrade.AfflictionId != null)
		{
			CardEditorOverrides.ApplyOverrideToCard(effectSourceCard, new CardOverride
			{
				EnchantmentId = upgrade.EnchantmentId,
				EnchantmentAmount = upgrade.EnchantmentAmount,
				AfflictionId = upgrade.AfflictionId,
				AfflictionAmount = upgrade.AfflictionAmount
			});
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_DynamicVars")]
internal static class CardModel_get_DynamicVars_CreatedEffectSource_Patch
{
	public static void Prefix(CardModel __instance)
	{
		CardEditorCreatedCardEffectSourceSupport.EnsureEffectSourceDynamicVars(__instance);
	}
}
