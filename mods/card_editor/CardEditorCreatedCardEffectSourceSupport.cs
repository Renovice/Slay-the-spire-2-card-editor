using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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

	private sealed class RuntimeEffectSourceInvocationState
	{
		public HashSet<string> InvocationKeys { get; } = new(StringComparer.Ordinal);
	}

	private static readonly FieldInfo? _dynamicVarsField = typeof(CardModel).GetField("_dynamicVars", BindingFlags.Instance | BindingFlags.NonPublic);
	private static readonly ConditionalWeakTable<CardModel, DynamicVarSyncState> _dynamicVarSyncStates = new();
	private static readonly ConditionalWeakTable<CardPlay, RuntimeEffectSourceInvocationState> _runtimeEffectSourceInvocations = new();
	private static readonly ThreadLocal<HashSet<ModelId>> _dynamicVarSyncGuard = new(() => new HashSet<ModelId>());
	private static readonly ThreadLocal<HashSet<ModelId>> _keywordGuard = new(() => new HashSet<ModelId>());
	private static readonly ConcurrentDictionary<Type, MethodInfo?> _onPlayMethodCache = new();
	private static readonly ConditionalWeakTable<CardModel, Dictionary<string, RuntimeVanillaEffectSourceState>> _runtimeVanillaEffectSourceStates = new();

	private sealed class RuntimeVanillaEffectSourceState
	{
		public required CardModel SourceCard { get; init; }
		public required CombatState? CombatState { get; init; }
	}

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

	internal static CardModel? BuildRuntimeEffectSourceCard(CardModel createdCard, ModelId effectSourceId, bool isUpgradePreview = false, string? runtimeSourceInstanceKey = null)
	{
		if (createdCard == null)
		{
			return null;
		}

		if (!isUpgradePreview && TryGetPersistentVanillaEffectSourceCard(createdCard, effectSourceId, runtimeSourceInstanceKey, out CardModel? persistentSourceCard))
		{
			return persistentSourceCard;
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
		Dictionary<ModelId, int> sourceCounts = new();
		foreach (ModelId effectSourceId in GetRuntimeEffectSourceIds(createdCard, isUpgradePreview))
		{
			try
			{
				int occurrence = sourceCounts.TryGetValue(effectSourceId, out int seen) ? seen : 0;
				sourceCounts[effectSourceId] = occurrence + 1;
				string? desc = GetSingleEffectSourceDescription(createdCard, target, isUpgradePreview, effectSourceId, CreateRuntimeSourceInstanceKey(effectSourceId, occurrence, "placement"));
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
		Dictionary<ModelId, int> sourceCounts = new();
		foreach (ModelId effectSourceId in GetRuntimeEffectSourceIds(createdCard, isUpgradePreview: false))
		{
			int occurrence = sourceCounts.TryGetValue(effectSourceId, out int seen) ? seen : 0;
			sourceCounts[effectSourceId] = occurrence + 1;
			await RunSingleEffectSourceOnPlay(createdCard, choiceContext, cardPlay, effectSourceId, CreateRuntimeSourceInstanceKey(effectSourceId, occurrence, "placement"));
		}
	}

	public static async Task RunSingleEffectSourceOnPlay(CardModel createdCard, PlayerChoiceContext choiceContext, CardPlay cardPlay, ModelId effectSourceId, string? runtimeSourceInstanceKey = null, string? customKeywordFilter = null)
	{
		if (createdCard == null || choiceContext == null || cardPlay == null)
		{
			return;
		}
		if (effectSourceId == null || effectSourceId == ModelId.none)
		{
			return;
		}

		string invocationKey = runtimeSourceInstanceKey ?? CreateRuntimeSourceInstanceKey(effectSourceId, 0, "default");
		if (!TryRegisterInvocation(cardPlay, invocationKey))
		{
			return;
		}

		CardModel? effectSourceCard = BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview: false);
		if (effectSourceCard == null)
		{
			return;
		}

		try
		{
			CombatState? combatState = createdCard.CombatState.AsCombatState() ?? cardPlay?.Card?.CombatState.AsCombatState();
			if (combatState != null)
			{
				IReadOnlyList<CardExtraEffect> borrowedEffects = CardEditorExtraEffects.GetRuntimeEffectsForBorrowedSource(combatState, createdCard, effectSourceId, customKeywordFilter);
				if (borrowedEffects.Count > 0)
				{
					using IDisposable _ = CardEditorCardPlayContext.PushScoped(cardPlay);
					using IDisposable __ = CardEditorEffectSourceContext.PushScoped(effectSourceCard);
					await CardEditorExtraEffects.RunResolvedOnPlayEffectsDuringCardPlay(combatState, choiceContext, cardPlay, borrowedEffects);
				}
			}

			if (!string.IsNullOrWhiteSpace(customKeywordFilter))
			{
				return;
			}

			if (effectSourceCard is CardEditorCreatedCardBase)
			{
				return;
			}

			effectSourceCard = GetOrCreatePersistentVanillaEffectSourceCard(createdCard, effectSourceId, runtimeSourceInstanceKey) ?? effectSourceCard;
			BindVanillaEffectSourceCardToRuntimePlay(effectSourceCard, cardPlay);

			MethodInfo? onPlay = _onPlayMethodCache.GetOrAdd(
				effectSourceCard.GetType(),
				ResolveOnPlayMethod);
			if (onPlay == null)
			{
				return;
			}

			using IDisposable ___ = CardEditorEffectSourceContext.PushScoped(effectSourceCard);
			if (onPlay.Invoke(effectSourceCard, new object[] { choiceContext, cardPlay }) is Task task)
			{
				await task;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Borrowed effect source OnPlay failed for {createdCard.Id} (source {effectSourceCard.Id}): {ex}");
		}
	}

	private static bool TryRegisterInvocation(CardPlay? cardPlay, string invocationKey)
	{
		if (cardPlay == null || string.IsNullOrWhiteSpace(invocationKey))
		{
			return true;
		}

		RuntimeEffectSourceInvocationState state = _runtimeEffectSourceInvocations.GetOrCreateValue(cardPlay);
		lock (state.InvocationKeys)
		{
			return state.InvocationKeys.Add(invocationKey);
		}
	}

	public static string? GetSingleEffectSourceDescription(CardModel createdCard, Creature? target, bool isUpgradePreview, ModelId effectSourceId, string? runtimeSourceInstanceKey = null, string? customKeywordFilter = null)
	{
		if (createdCard == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return null;
		}

		CardModel? effectSourceCard = BuildRuntimeEffectSourceCard(createdCard, effectSourceId, isUpgradePreview, runtimeSourceInstanceKey);
		if (effectSourceCard == null)
		{
			return null;
		}

		try
		{
			CardPlay previewPlay = new CardPlay
			{
				Card = createdCard,
				Target = target,
				ResultPile = createdCard.Pile?.Type ?? PileType.None,
				Resources = new ResourceInfo
				{
					EnergySpent = 0,
					EnergyValue = 0,
					StarsSpent = 0,
					StarValue = 0
				},
				IsAutoPlay = true,
				PlayIndex = 0,
				PlayCount = 1
			};
			using IDisposable playScope = CardEditorCardPlayContext.PushScoped(previewPlay);
			using IDisposable sourceScope = CardEditorEffectSourceContext.PushScoped(effectSourceCard);

			string keywordFilter = customKeywordFilter?.Trim() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(keywordFilter))
			{
				CardExtraEffectKeywordSummary? keywordSummary = CardEditorExtraEffects.GetCustomKeywordSummaries(effectSourceCard, target, isUpgradePreview)
					.FirstOrDefault(summary => string.Equals(summary.Name?.Trim() ?? string.Empty, keywordFilter, StringComparison.OrdinalIgnoreCase));
				if (keywordSummary != null && !string.IsNullOrWhiteSpace(keywordSummary.Description))
				{
					return keywordSummary.Description.Trim();
				}

				return null;
			}

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

	internal static async Task RunResolvedCreatedCardOnPlay(CardEditorCreatedCardBase createdCard, CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (createdCard == null || combatState == null || choiceContext == null || cardPlay?.Card == null)
		{
			return;
		}

		using IDisposable _ = CardEditorCardPlayContext.PushScoped(cardPlay);

		bool hasInlineEffectSources = CardEditorExtraEffects.GetEffectsForDescription(cardPlay.Card, isUpgradePreview: false)
			.Any(e => e != null && e.Kind == CardExtraEffectKind.RunEffectSourceCard);
		if (hasInlineEffectSources)
		{
			await CardEditorExtraEffects.RunCreatedCardOnPlayDuringCardPlay(combatState, choiceContext, cardPlay);
			return;
		}

		if (CardEditorCreatedCardsStore.GetEffectSourcePlacement(createdCard.Id) == CardEditorEffectSourcePlacement.AfterCustomEffects)
		{
			await CardEditorExtraEffects.RunCreatedCardOnPlayDuringCardPlay(combatState, choiceContext, cardPlay);
			await RunEffectSourceOnPlay(createdCard, choiceContext, cardPlay);
		}
		else
		{
			await RunEffectSourceOnPlay(createdCard, choiceContext, cardPlay);
			await CardEditorExtraEffects.RunCreatedCardOnPlayDuringCardPlay(combatState, choiceContext, cardPlay);
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

		if (desiredUpgradeLevel > 0)
		{
			for (int i = 0; i < desiredUpgradeLevel && effectSourceCard.IsUpgradable; i++)
			{
				NumericSnapshot snapshot = CaptureNumericSnapshot(effectSourceCard);
				effectSourceCard.UpgradeInternal();
				effectSourceCard.FinalizeUpgradeInternal();

				if (overrideData?.Upgrade != null && !overrideData.Upgrade.IsEmpty())
				{
					ApplyUpgradeNumericOverrides(effectSourceCard, snapshot, overrideData.Upgrade);
				}
			}
		}

		bool appliedManualUpgradeOverrides = desiredUpgradeLevel > 0
			&& overrideData?.Upgrade != null
			&& !overrideData.Upgrade.IsEmpty();

		CardEditorOverrides.SetInstanceOverride(effectSourceCard, BuildEffectSourceRuntimeOverride(effectSourceCard, overrideData));

		try
		{
			effectSourceCard.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed recalculating effect source dynamic vars: {ex}");
		}

		if (appliedManualUpgradeOverrides && !isUpgradePreview)
		{
			try
			{
				// Clear runtime "just upgraded" flags after retargeting upgrade deltas so later
				// combat debuffs can highlight reduced values red instead of staying green.
				effectSourceCard.FinalizeUpgradeInternal();
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed finalizing effect source upgrade overrides: {ex}");
			}
		}
	}

	internal static string CreateRuntimeSourceInstanceKey(ModelId effectSourceId, int occurrenceIndex, string scope)
	{
		string source = effectSourceId?.ToString() ?? ModelId.none.ToString();
		return $"{scope}:{source}:{Math.Max(0, occurrenceIndex)}";
	}

	private static CardModel? GetOrCreatePersistentVanillaEffectSourceCard(CardModel createdCard, ModelId effectSourceId, string? runtimeSourceInstanceKey)
	{
		if (createdCard == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return null;
		}

		CombatState? hostCombatState = createdCard.CombatState.AsCombatState() ?? createdCard.Owner?.Creature?.CombatState.AsCombatState();
		string stateKey = runtimeSourceInstanceKey ?? CreateRuntimeSourceInstanceKey(effectSourceId, 0, "default");
		Dictionary<string, RuntimeVanillaEffectSourceState> states = _runtimeVanillaEffectSourceStates.GetOrCreateValue(createdCard);
		if (states.TryGetValue(stateKey, out RuntimeVanillaEffectSourceState? state)
			&& state?.SourceCard != null
			&& state.CombatState.AsCombatState() == hostCombatState)
		{
			return state.SourceCard;
		}

		CardModel? sourceCard = BuildEffectSourceCard(createdCard, effectSourceId, isUpgradePreview: false);
		if (sourceCard == null || sourceCard is CardEditorCreatedCardBase)
		{
			return sourceCard;
		}

		states[stateKey] = new RuntimeVanillaEffectSourceState
		{
			SourceCard = sourceCard,
			CombatState = hostCombatState
		};
		return sourceCard;
	}

	private static bool TryGetPersistentVanillaEffectSourceCard(CardModel createdCard, ModelId effectSourceId, string? runtimeSourceInstanceKey, out CardModel? sourceCard)
	{
		sourceCard = null;
		if (createdCard == null || effectSourceId == null || effectSourceId == ModelId.none)
		{
			return false;
		}

		string stateKey = runtimeSourceInstanceKey ?? CreateRuntimeSourceInstanceKey(effectSourceId, 0, "default");
		CombatState? hostCombatState = createdCard.CombatState.AsCombatState() ?? createdCard.Owner?.Creature?.CombatState.AsCombatState();
		if (!_runtimeVanillaEffectSourceStates.TryGetValue(createdCard, out Dictionary<string, RuntimeVanillaEffectSourceState>? states))
		{
			return false;
		}

		if (!states.TryGetValue(stateKey, out RuntimeVanillaEffectSourceState? state) || state?.SourceCard == null)
		{
			return false;
		}

		if (state.CombatState.AsCombatState() != hostCombatState)
		{
			states.Remove(stateKey);
			return false;
		}

		sourceCard = state.SourceCard;
		return true;
	}

	private static void BindVanillaEffectSourceCardToRuntimePlay(CardModel effectSourceCard, CardPlay cardPlay)
	{
		if (effectSourceCard == null || cardPlay?.Card == null)
		{
			return;
		}

		CardModel hostCard = cardPlay.Card;
		try
		{
			if (hostCard.Owner != null && effectSourceCard.Owner == null)
			{
				effectSourceCard.Owner = hostCard.Owner;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed binding Owner to borrowed vanilla source card {effectSourceCard.Id}: {ex}");
		}

		try
		{
			if ((hostCard.CombatState.AsCombatState() != null || hostCard.Owner?.Creature?.CombatState.AsCombatState() != null)
				&& effectSourceCard.UpgradePreviewType != CardUpgradePreviewType.Combat)
			{
				effectSourceCard.UpgradePreviewType = CardUpgradePreviewType.Combat;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed binding combat preview state to borrowed vanilla source card {effectSourceCard.Id}: {ex}");
		}

		try
		{
			if (effectSourceCard.EnergyCost.CostsX)
			{
				int capturedX = Math.Max(0, cardPlay.Resources.EnergySpent);
				if (capturedX == 0 && hostCard.EnergyCost.CostsX)
				{
					capturedX = Math.Max(0, hostCard.EnergyCost.CapturedXValue);
				}
				effectSourceCard.EnergyCost.CapturedXValue = capturedX;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed binding X energy state to borrowed vanilla source card {effectSourceCard.Id}: {ex}");
		}

		try
		{
			if (effectSourceCard.HasStarCostX)
			{
				int capturedStars = Math.Max(0, cardPlay.Resources.StarsSpent);
				if (capturedStars == 0 && hostCard.HasStarCostX)
				{
					capturedStars = Math.Max(0, hostCard.LastStarsSpent);
				}
				effectSourceCard.LastStarsSpent = capturedStars;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed binding X star state to borrowed vanilla source card {effectSourceCard.Id}: {ex}");
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
