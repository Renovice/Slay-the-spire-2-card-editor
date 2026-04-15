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
	private static readonly ConcurrentDictionary<Type, MethodInfo?> _onPlayMethodCache = new();

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
			IReadOnlyList<ModelId> effectSourceIds = CardEditorCreatedCardsStore.GetEffectSourceCardIds(card.Id);
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

			List<CardModel> effectSourceCards = BuildEffectSourceCards(card, isUpgradePreview);
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
				type => type.GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic));
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

	private static List<CardModel> BuildEffectSourceCards(CardModel createdCard, bool isUpgradePreview)
	{
		List<CardModel> result = new();
		IReadOnlyList<ModelId> effectSourceIds = CardEditorCreatedCardsStore.GetEffectSourceCardIds(createdCard.Id);

		foreach (ModelId effectSourceId in effectSourceIds)
		{
			if (effectSourceId == ModelId.none)
			{
				continue;
			}

			if (effectSourceId == createdCard.Id)
			{
				continue;
			}

			CardModel? sourceCanonical = ModelDb.GetByIdOrNull<CardModel>(effectSourceId);
			if (sourceCanonical == null)
			{
				continue;
			}

			try
			{
				CardModel effectSourceCard = sourceCanonical.ToMutable();
				SyncEffectSourceCard(effectSourceCard, createdCard, isUpgradePreview);
				result.Add(effectSourceCard);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed building borrowed effect source card {effectSourceId} for {createdCard.Id}: {ex}");
			}
		}

		return result;
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

		try
		{
			effectSourceCard.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed recalculating effect source dynamic vars: {ex}");
		}
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
					effectSourceCard.EnergyCost.SetCustomBaseCost(Math.Max(-1, vanillaUpgraded + adjust));
					effectSourceCard.InvokeEnergyCostChanged();
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
