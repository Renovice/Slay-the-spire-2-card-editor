using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorRunSelfScalingState
{
	private const int CurrentVersion = 2;
	private const int OverrideDtoVersion = 13;
	private const string StorePath = "user://card_editor/run_self_scaling_state.json";
	private const string SerializedMutationPropertyName = nameof(CardEditorCreatedCardBase.CardEditorSelfScalingDiff);
	private static readonly TimeSpan MaxRunStateAge = TimeSpan.FromDays(14);
	private static readonly ConditionalWeakTable<CardModel, AppliedMarker> _appliedMarkers = new ConditionalWeakTable<CardModel, AppliedMarker>();
	private static readonly ConditionalWeakTable<CardModel, SerializedMutationPayload> _serializedMutationPayloads = new ConditionalWeakTable<CardModel, SerializedMutationPayload>();
	private static int _restoreSuppressionDepth;

	private static FileDto? _cache;
	internal static bool IsRefreshingForRestore => _restoreSuppressionDepth > 0;

	internal static IDisposable PushRestoreSuppression()
	{
		_restoreSuppressionDepth++;
		return new RestoreSuppressionScope();
	}

	public static void RecordPersistentMutation(CardModel? card, CardEditorExtraEffects.SelfScalingMutationDiff diff)
	{
		if (card == null || diff == null || diff.IsEmpty || card.Id == null || card.Id == ModelId.none)
		{
			return;
		}
		if (card is CardEditorCreatedCardBase createdCard)
		{
			RecordCreatedCardMutation(createdCard, diff);
			return;
		}

		RecordSerializedCardMutation(card, diff);
	}

	public static void RestoreForCombat(CombatState? combatState)
	{
		if (combatState == null)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players ?? Enumerable.Empty<Player>())
			{
				RestoreCards(player?.Deck?.Cards, combatState.RunState);
				RestoreCards(player?.PlayerCombatState?.Hand?.Cards, combatState.RunState);
				RestoreCards(player?.PlayerCombatState?.DrawPile?.Cards, combatState.RunState);
				RestoreCards(player?.PlayerCombatState?.DiscardPile?.Cards, combatState.RunState);
				RestoreCards(player?.PlayerCombatState?.ExhaustPile?.Cards, combatState.RunState);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] RestoreForCombat failed: {ex}");
		}
	}

	public static bool TryRestoreCard(CardModel? card, IRunState? runState = null, bool cardAlreadyRebased = false)
	{
		if (card == null || !card.IsMutable || card.Id == null || card.Id == ModelId.none)
		{
			return false;
		}
		if (IsRefreshingForRestore)
		{
			return false;
		}
		if (card is CardEditorCreatedCardBase createdCard)
		{
			return TryRestoreCreatedCard(card, createdCard, cardAlreadyRebased);
		}

		return TryRestoreSerializedCard(card, cardAlreadyRebased);
	}

	public static void WriteSavedMutationToSerializable(CardModel? card, SerializableCard? save)
	{
		if (card == null || save == null)
		{
			return;
		}

		string payload = card is CardEditorCreatedCardBase createdCard
			? createdCard.CardEditorSelfScalingDiff ?? string.Empty
			: GetSerializedMutationPayload(card);
		WriteSerializedMutationPayload(save, payload);
	}

	public static void ReadSavedMutationFromSerializable(SerializableCard? save, CardModel? card)
	{
		if (save == null || card == null)
		{
			return;
		}
		if (!TryReadSerializedMutationPayload(save, out string payload))
		{
			return;
		}

		if (card is CardEditorCreatedCardBase createdCard)
		{
			createdCard.CardEditorSelfScalingDiff = payload;
		}
		else
		{
			SetSerializedMutationPayload(card, payload);
		}

		TryRestoreCard(card, cardAlreadyRebased: true);
	}

	private static void RecordSerializedCardMutation(CardModel card, CardEditorExtraEffects.SelfScalingMutationDiff diff)
	{
		if (card == null || diff == null || diff.IsEmpty)
		{
			return;
		}

		CardEditorExtraEffects.SelfScalingMutationDiff combined = TryReadStoredCardDiff(card, out CardEditorExtraEffects.SelfScalingMutationDiff existing, out _)
			? existing
			: new CardEditorExtraEffects.SelfScalingMutationDiff();
		CardEditorExtraEffects.AccumulateSelfScalingMutationDiff(combined, diff);
		string payload = combined.IsEmpty
			? string.Empty
			: JsonSerializer.Serialize(SelfScalingDiffDto.FromDiff(combined));
		SetSerializedMutationPayload(card, payload);
		SetAppliedMarker(card, BuildSavedCardMarker(payload));
	}

	private static void RecordCreatedCardMutation(CardEditorCreatedCardBase card, CardEditorExtraEffects.SelfScalingMutationDiff diff)
	{
		if (card == null || diff == null || diff.IsEmpty)
		{
			return;
		}

		CardEditorExtraEffects.SelfScalingMutationDiff combined = TryReadCreatedCardDiff(card, out CardEditorExtraEffects.SelfScalingMutationDiff existing, out _)
			? existing
			: new CardEditorExtraEffects.SelfScalingMutationDiff();
		CardEditorExtraEffects.AccumulateSelfScalingMutationDiff(combined, diff);
		card.CardEditorSelfScalingDiff = combined.IsEmpty
			? string.Empty
			: JsonSerializer.Serialize(SelfScalingDiffDto.FromDiff(combined));
		SetAppliedMarker(card, BuildSavedCardMarker(card.CardEditorSelfScalingDiff));
	}

	private static bool TryRestoreCreatedCard(CardModel card, CardEditorCreatedCardBase createdCard, bool cardAlreadyRebased)
	{
		if (!TryReadCreatedCardDiff(createdCard, out CardEditorExtraEffects.SelfScalingMutationDiff diff, out string payload) || diff.IsEmpty)
		{
			return false;
		}

		string marker = BuildSavedCardMarker(payload);
		if (IsAppliedMarkerCurrent(card, marker))
		{
			return true;
		}

		try
		{
			using (PushRestoreSuppression())
			{
				if (!cardAlreadyRebased)
				{
					RebaseCardToCurrentDefinition(card);
				}

				bool applied = CardEditorExtraEffects.ApplyPersistentSelfScalingDiff(card, diff);
				SetAppliedMarker(card, marker);
				return applied;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Failed to restore saved card mutation for {card.Id}: {ex}");
			return false;
		}
	}

	private static bool TryRestoreSerializedCard(CardModel card, bool cardAlreadyRebased)
	{
		if (!TryReadStoredCardDiff(card, out CardEditorExtraEffects.SelfScalingMutationDiff diff, out string payload) || diff.IsEmpty)
		{
			return false;
		}

		string marker = BuildSavedCardMarker(payload);
		if (IsAppliedMarkerCurrent(card, marker))
		{
			return true;
		}

		try
		{
			using (PushRestoreSuppression())
			{
				if (!cardAlreadyRebased)
				{
					RebaseCardToCurrentDefinition(card);
				}

				bool applied = CardEditorExtraEffects.ApplyPersistentSelfScalingDiff(card, diff);
				SetAppliedMarker(card, marker);
				return applied;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Failed to restore serialized card mutation for {card.Id}: {ex}");
			return false;
		}
	}

	private static bool TryReadCreatedCardDiff(
		CardEditorCreatedCardBase card,
		out CardEditorExtraEffects.SelfScalingMutationDiff diff,
		out string payload)
	{
		diff = new CardEditorExtraEffects.SelfScalingMutationDiff();
		payload = card?.CardEditorSelfScalingDiff ?? string.Empty;
		if (string.IsNullOrWhiteSpace(payload))
		{
			return false;
		}

		try
		{
			SelfScalingDiffDto? dto = JsonSerializer.Deserialize<SelfScalingDiffDto>(payload);
			if (dto == null)
			{
				return false;
			}

			diff = dto.ToDiff();
			return !diff.IsEmpty;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Ignoring invalid saved card mutation payload for {card?.Id?.ToString() ?? "unknown"}: {ex.Message}");
			return false;
		}
	}

	private static bool TryReadStoredCardDiff(
		CardModel card,
		out CardEditorExtraEffects.SelfScalingMutationDiff diff,
		out string payload)
	{
		if (card is CardEditorCreatedCardBase createdCard)
		{
			return TryReadCreatedCardDiff(createdCard, out diff, out payload);
		}

		diff = new CardEditorExtraEffects.SelfScalingMutationDiff();
		payload = GetSerializedMutationPayload(card);
		if (string.IsNullOrWhiteSpace(payload))
		{
			return false;
		}

		try
		{
			SelfScalingDiffDto? dto = JsonSerializer.Deserialize<SelfScalingDiffDto>(payload);
			if (dto == null)
			{
				return false;
			}

			diff = dto.ToDiff();
			return !diff.IsEmpty;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Ignoring invalid serialized card mutation payload for {card?.Id?.ToString() ?? "unknown"}: {ex.Message}");
			return false;
		}
	}

	private static string BuildSavedCardMarker(string payload)
		=> "card-prop:" + (payload ?? string.Empty);

	private static string GetSerializedMutationPayload(CardModel card)
	{
		return card != null && _serializedMutationPayloads.TryGetValue(card, out SerializedMutationPayload? payload)
			? payload.Value ?? string.Empty
			: string.Empty;
	}

	private static void SetSerializedMutationPayload(CardModel card, string? payload)
	{
		if (card == null)
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(payload))
		{
			_serializedMutationPayloads.Remove(card);
			return;
		}

		_serializedMutationPayloads.GetOrCreateValue(card).Value = payload;
	}

	private static bool TryReadSerializedMutationPayload(SerializableCard save, out string payload)
	{
		payload = string.Empty;
		if (save?.Props?.strings == null)
		{
			return false;
		}

		foreach (SavedProperties.SavedProperty<string> item in save.Props.strings)
		{
			if (string.Equals(item.name, SerializedMutationPropertyName, StringComparison.Ordinal))
			{
				payload = item.value ?? string.Empty;
				return !string.IsNullOrWhiteSpace(payload);
			}
		}

		return false;
	}

	private static void WriteSerializedMutationPayload(SerializableCard save, string? payload)
	{
		if (save == null)
		{
			return;
		}

		List<SavedProperties.SavedProperty<string>>? strings = save.Props?.strings;
		if (strings != null)
		{
			for (int i = strings.Count - 1; i >= 0; i--)
			{
				if (string.Equals(strings[i].name, SerializedMutationPropertyName, StringComparison.Ordinal))
				{
					strings.RemoveAt(i);
				}
			}
		}

		if (string.IsNullOrWhiteSpace(payload))
		{
			return;
		}

		save.Props ??= new SavedProperties();
		save.Props.strings ??= new List<SavedProperties.SavedProperty<string>>();
		save.Props.strings.Add(new SavedProperties.SavedProperty<string>(SerializedMutationPropertyName, payload));
	}

	private static bool TryMigrateLegacySnapshot(CardModel card, RunDto run, string cardKey, bool cardAlreadyRebased, out SelfScalingDiffDto? diffDto)
	{
		diffDto = null;
		if (card == null
			|| run?.Cards == null
			|| !run.Cards.TryGetValue(cardKey, out CardEditorPresetStore.CardOverrideDto? dto)
			|| dto == null)
		{
			return false;
		}

		CardOverride overrideData = dto.ToOverrideSafe(card.Id, OverrideDtoVersion);
		if (overrideData.IsEmpty())
		{
			run.Cards.Remove(cardKey);
			return false;
		}

		if (!cardAlreadyRebased)
		{
			RebaseCardToCurrentDefinition(card);
		}

		CardEditorExtraEffects.SelfScalingMutationDiff migrated = CardEditorExtraEffects.BuildSelfScalingMutationDiffFromCurrentCard(card, overrideData);
		if (migrated.IsEmpty)
		{
			run.Cards.Remove(cardKey);
			return false;
		}

		diffDto = SelfScalingDiffDto.FromDiff(migrated);
		run.Diffs[cardKey] = diffDto;
		run.Cards.Remove(cardKey);
		return true;
	}

	private static void RebaseCardToCurrentDefinition(CardModel card)
	{
		if (card == null || !card.IsMutable)
		{
			return;
		}

		using (PushRestoreSuppression())
		{
			CardEditorOverrides.SetInstanceOverride(card, null);
			CardEditorOverrides.RefreshCardAfterUpgradeStateChanged(card);
		}
	}

	private static void RestoreCards(IEnumerable<CardModel>? cards, IRunState? runState)
	{
		if (cards == null)
		{
			return;
		}

		foreach (CardModel card in cards.Where(card => card != null))
		{
			TryRestoreCard(card, runState);
		}
	}

	private static string BuildAppliedMarker(string runKey, string cardKey, RunDto run)
		=> $"{runKey}|{cardKey}|rev:{CardEditorOverrides.Revision}|updated:{run.UpdatedAtUtc.Ticks}";

	private static bool IsAppliedMarkerCurrent(CardModel card, string marker)
	{
		if (card == null || string.IsNullOrWhiteSpace(marker))
		{
			return false;
		}

		return _appliedMarkers.TryGetValue(card, out AppliedMarker? applied)
			&& string.Equals(applied.Value, marker, StringComparison.Ordinal);
	}

	private static void SetAppliedMarker(CardModel card, string marker)
	{
		if (card == null || string.IsNullOrWhiteSpace(marker))
		{
			return;
		}

		_appliedMarkers.Remove(card);
		_appliedMarkers.Add(card, new AppliedMarker { Value = marker });
	}

	private static IRunState? TryGetRunState(CardModel card)
	{
		try
		{
			return card.Owner?.RunState;
		}
		catch
		{
			return null;
		}
	}

	private static bool TryBuildRunKey(IRunState? runState, out string key)
	{
		key = string.Empty;
		if (runState == null)
		{
			return false;
		}

		string seed = TryReadRunIdentity(runState);
		string character = string.Empty;
		try
		{
			character = LocalContext.GetMe(runState)?.Character?.Id?.ToString() ?? string.Empty;
		}
		catch
		{
		}

		if (string.IsNullOrWhiteSpace(seed))
		{
			seed = BuildDeckSignature(runState);
		}
		if (string.IsNullOrWhiteSpace(seed))
		{
			return false;
		}

		key = $"{runState.GetType().FullName}|{character}|{seed}";
		return true;
	}

	private static string TryReadRunIdentity(object runState)
	{
		string[] directNames =
		{
			"RunId", "Id", "SaveId", "RunGuid", "Guid", "Seed", "RunSeed", "RngSeed", "SeedString"
		};

		foreach (string name in directNames)
		{
			if (TryReadMember(runState, name, out object? value) && IsUsefulIdentity(value))
			{
				return $"{name}:{value}";
			}
		}

		if (TryReadMember(runState, "Rng", out object? rng) && rng != null)
		{
			foreach (string name in new[] { "Seed", "RunSeed", "InitialSeed", "State" })
			{
				if (TryReadMember(rng, name, out object? value) && IsUsefulIdentity(value))
				{
					return $"Rng.{name}:{value}";
				}
			}
		}

		return string.Empty;
	}

	private static bool TryReadMember(object instance, string name, out object? value)
	{
		value = null;
		if (instance == null || string.IsNullOrWhiteSpace(name))
		{
			return false;
		}

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		try
		{
			PropertyInfo? property = instance.GetType().GetProperty(name, flags);
			if (property != null && property.GetIndexParameters().Length == 0)
			{
				value = property.GetValue(instance);
				return true;
			}

			FieldInfo? field = instance.GetType().GetField(name, flags);
			if (field != null)
			{
				value = field.GetValue(instance);
				return true;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool TryBuildCardInstanceKey(CardModel card, IRunState? runState, out string key)
	{
		key = string.Empty;
		if (card == null || card.Id == null || card.Id == ModelId.none || runState == null)
		{
			return false;
		}

		CardModel persistentCard = TryGetPersistentCard(card);
		if (TryFindDeckOrdinal(runState, persistentCard, out int deckIndex, out int sameIdOrdinal))
		{
			key = $"{card.Id}|deck:{deckIndex}|copy:{sameIdOrdinal}";
			return true;
		}

		return false;
	}

	private static CardModel TryGetPersistentCard(CardModel card)
	{
		try
		{
			if (card.DeckVersion is CardModel deckCard && deckCard.IsMutable)
			{
				return deckCard;
			}
		}
		catch
		{
		}

		return card;
	}

	private static bool TryFindDeckOrdinal(IRunState runState, CardModel targetCard, out int deckIndex, out int sameIdOrdinal)
	{
		deckIndex = -1;
		sameIdOrdinal = -1;
		if (runState == null || targetCard == null || targetCard.Id == null || targetCard.Id == ModelId.none)
		{
			return false;
		}

		try
		{
			foreach (Player player in runState.Players ?? Enumerable.Empty<Player>())
			{
				IReadOnlyList<CardModel>? deckCards = player?.Deck?.Cards;
				if (deckCards == null)
				{
					continue;
				}

				int matchingIdOrdinal = 0;
				for (int i = 0; i < deckCards.Count; i++)
				{
					CardModel? deckCard = deckCards[i];
					if (deckCard == null)
					{
						continue;
					}

					bool sameId = deckCard.Id != null && deckCard.Id.Equals(targetCard.Id);
					if (!sameId)
					{
						continue;
					}

					if (ReferenceEquals(deckCard, targetCard))
					{
						deckIndex = i;
						sameIdOrdinal = matchingIdOrdinal;
						return true;
					}

					matchingIdOrdinal++;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsUsefulIdentity(object? value)
	{
		if (value == null)
		{
			return false;
		}

		string text = value.ToString() ?? string.Empty;
		return !string.IsNullOrWhiteSpace(text)
			&& !string.Equals(text, "0", StringComparison.Ordinal)
			&& !string.Equals(text, "00000000-0000-0000-0000-000000000000", StringComparison.OrdinalIgnoreCase);
	}

	private static string BuildDeckSignature(IRunState runState)
	{
		try
		{
			List<string> ids = new List<string>();
			foreach (Player player in runState.Players ?? Enumerable.Empty<Player>())
			{
				if (player?.Deck?.Cards == null)
				{
					continue;
				}

				ids.AddRange(player.Deck.Cards
					.Where(card => card?.Id != null && card.Id != ModelId.none)
					.Select(card => card.Id.ToString()));
			}

			return ids.Count == 0
				? string.Empty
				: "Deck:" + string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));
		}
		catch
		{
			return string.Empty;
		}
	}

	private static FileDto Load()
	{
		if (_cache != null)
		{
			return _cache;
		}

		try
		{
			string path = GetStorePath();
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				_cache = JsonSerializer.Deserialize<FileDto>(json, CreateJsonOptions()) ?? new FileDto();
				NormalizeFile(_cache);
				return _cache;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Failed to load store: {ex}");
		}

		_cache = new FileDto();
		return _cache;
	}

	private static void NormalizeFile(FileDto file)
	{
		file.Runs ??= new Dictionary<string, RunDto>(StringComparer.Ordinal);
		foreach ((string runKey, RunDto? run) in file.Runs.ToList())
		{
			if (run == null)
			{
				file.Runs.Remove(runKey);
				continue;
			}

			run.Cards ??= new Dictionary<string, CardEditorPresetStore.CardOverrideDto>(StringComparer.Ordinal);
			run.Diffs ??= new Dictionary<string, SelfScalingDiffDto>(StringComparer.Ordinal);
		}
	}

	private static void Save(FileDto file)
	{
		try
		{
			string path = GetStorePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			string json = JsonSerializer.Serialize(file, CreateJsonOptions());
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Failed to save store: {ex}");
		}
	}

	private static void PruneOldRuns(FileDto file)
	{
		DateTime cutoff = DateTime.UtcNow - MaxRunStateAge;
		foreach ((string runKey, RunDto run) in file.Runs.ToList())
		{
			bool hasLegacyCards = run?.Cards != null && run.Cards.Count > 0;
			bool hasDiffs = run?.Diffs != null && run.Diffs.Count > 0;
			if (run == null || run.UpdatedAtUtc < cutoff || (!hasLegacyCards && !hasDiffs))
			{
				file.Runs.Remove(runKey);
			}
		}
	}

	private static string GetStorePath()
	{
		return ProjectSettings.GlobalizePath(StorePath);
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private sealed class FileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public Dictionary<string, RunDto> Runs { get; set; } = new Dictionary<string, RunDto>(StringComparer.Ordinal);
	}

	private sealed class RunDto
	{
		public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
		public Dictionary<string, CardEditorPresetStore.CardOverrideDto> Cards { get; set; } = new Dictionary<string, CardEditorPresetStore.CardOverrideDto>(StringComparer.Ordinal);
		public Dictionary<string, SelfScalingDiffDto> Diffs { get; set; } = new Dictionary<string, SelfScalingDiffDto>(StringComparer.Ordinal);
	}

	private sealed class SelfScalingDiffDto
	{
		public Dictionary<string, decimal> DynamicVarDeltas { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);
		public Dictionary<string, decimal> DynamicVarBaseValues { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);
		public List<EffectFieldDeltaDto> EffectFieldDeltas { get; set; } = new List<EffectFieldDeltaDto>();
		public int EnergyCostDelta { get; set; }
		public int? EnergyCostBaseValue { get; set; }
		public int StarCostDelta { get; set; }
		public int? StarCostBaseValue { get; set; }

		public static SelfScalingDiffDto FromDiff(CardEditorExtraEffects.SelfScalingMutationDiff diff)
		{
			SelfScalingDiffDto dto = new SelfScalingDiffDto();
			if (diff == null)
			{
				return dto;
			}

			dto.DynamicVarDeltas = new Dictionary<string, decimal>(diff.DynamicVarDeltas, StringComparer.Ordinal);
			dto.DynamicVarBaseValues = new Dictionary<string, decimal>(diff.DynamicVarBaseValues, StringComparer.Ordinal);
			dto.EffectFieldDeltas = diff.EffectFieldDeltas
				.Where(delta => delta != null && delta.Delta != 0)
				.Select(delta => new EffectFieldDeltaDto
				{
					EffectId = delta.EffectId,
					Index = delta.Index,
					Field = delta.Field,
					Delta = delta.Delta,
					BaseValue = delta.BaseValue
				})
				.ToList();
			dto.EnergyCostDelta = diff.EnergyCostDelta;
			dto.EnergyCostBaseValue = diff.EnergyCostBaseValue;
			dto.StarCostDelta = diff.StarCostDelta;
			dto.StarCostBaseValue = diff.StarCostBaseValue;
			return dto;
		}

		public CardEditorExtraEffects.SelfScalingMutationDiff ToDiff()
		{
			CardEditorExtraEffects.SelfScalingMutationDiff diff = new CardEditorExtraEffects.SelfScalingMutationDiff();
			if (DynamicVarDeltas != null)
			{
				foreach ((string key, decimal delta) in DynamicVarDeltas)
				{
					if (string.IsNullOrWhiteSpace(key) || delta == 0m)
					{
						continue;
					}
					diff.DynamicVarDeltas[key] = delta;
				}
			}
			if (DynamicVarBaseValues != null)
			{
				foreach ((string key, decimal baseValue) in DynamicVarBaseValues)
				{
					if (!string.IsNullOrWhiteSpace(key))
					{
						diff.DynamicVarBaseValues[key] = baseValue;
					}
				}
			}
			if (EffectFieldDeltas != null)
			{
				foreach (EffectFieldDeltaDto delta in EffectFieldDeltas)
				{
					if (delta == null || delta.Delta == 0)
					{
						continue;
					}
					diff.EffectFieldDeltas.Add(new CardEditorExtraEffects.SelfScalingMutationDiff.EffectFieldDelta
					{
						EffectId = delta.EffectId,
						Index = delta.Index,
						Field = delta.Field,
						Delta = delta.Delta,
						BaseValue = delta.BaseValue
					});
				}
			}
			diff.EnergyCostDelta = EnergyCostDelta;
			diff.EnergyCostBaseValue = EnergyCostBaseValue;
			diff.StarCostDelta = StarCostDelta;
			diff.StarCostBaseValue = StarCostBaseValue;
			return diff;
		}
	}

	private sealed class EffectFieldDeltaDto
	{
		public string? EffectId { get; set; }
		public int Index { get; set; }
		public CardExtraEffectSelfScalingField Field { get; set; }
		public int Delta { get; set; }
		public int? BaseValue { get; set; }
	}

	private sealed class SerializedMutationPayload
	{
		public string Value { get; set; } = string.Empty;
	}

	private sealed class AppliedMarker
	{
		public string Value { get; init; } = string.Empty;
	}

	private sealed class RestoreSuppressionScope : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_restoreSuppressionDepth = Math.Max(0, _restoreSuppressionDepth - 1);
		}
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.ToSerializable))]
internal static class CardModel_ToSerializable_CardEditorRunSelfScalingState_Patch
{
	private static void Postfix(CardModel __instance, SerializableCard __result)
	{
		CardEditorRunSelfScalingState.WriteSavedMutationToSerializable(__instance, __result);
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable))]
internal static class CardModel_FromSerializable_CardEditorRunSelfScalingState_Patch
{
	private static void Postfix(SerializableCard save, CardModel __result)
	{
		CardEditorRunSelfScalingState.ReadSavedMutationFromSerializable(save, __result);
	}
}
