using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorRunSelfScalingState
{
	private const int CurrentVersion = 2;
	private const int OverrideDtoVersion = 13;
	private const string StorePath = "user://card_editor/run_self_scaling_state.json";
	private static readonly TimeSpan MaxRunStateAge = TimeSpan.FromDays(14);
	private static readonly ConditionalWeakTable<CardModel, AppliedMarker> _appliedMarkers = new ConditionalWeakTable<CardModel, AppliedMarker>();

	private static FileDto? _cache;
	internal static bool IsRefreshingForRestore { get; private set; }

	public static void RecordPersistentMutation(CardModel? card, CardEditorExtraEffects.SelfScalingMutationDiff diff)
	{
		if (card == null || diff == null || diff.IsEmpty || card.Id == null || card.Id == ModelId.none)
		{
			return;
		}

		IRunState? runState = TryGetRunState(card);
		if (!TryBuildRunKey(runState, out string runKey))
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Could not persist mutation for {card.Id}: no active run key.");
			return;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run))
		{
			run = new RunDto();
			file.Runs[runKey] = run;
		}
		run.Cards ??= new Dictionary<string, CardEditorPresetStore.CardOverrideDto>(StringComparer.Ordinal);
		run.Diffs ??= new Dictionary<string, SelfScalingDiffDto>(StringComparer.Ordinal);

		if (!TryBuildCardInstanceKey(card, runState, out string cardKey))
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Could not persist mutation for {card.Id}: no stable card instance key.");
			return;
		}

		run.UpdatedAtUtc = DateTime.UtcNow;
		CardEditorExtraEffects.SelfScalingMutationDiff combined = run.Diffs != null && run.Diffs.TryGetValue(cardKey, out SelfScalingDiffDto? existingDto) && existingDto != null
			? existingDto.ToDiff()
			: new CardEditorExtraEffects.SelfScalingMutationDiff();
		CardEditorExtraEffects.AccumulateSelfScalingMutationDiff(combined, diff);
		if (combined.IsEmpty)
		{
			run.Diffs.Remove(cardKey);
		}
		else
		{
			run.Diffs[cardKey] = SelfScalingDiffDto.FromDiff(combined);
		}
		run.Cards.Remove(cardKey);
		PruneOldRuns(file);
		Save(file);
		SetAppliedMarker(card, BuildAppliedMarker(runKey, cardKey, run));
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

		runState ??= TryGetRunState(card);
		if (!TryBuildRunKey(runState, out string runKey))
		{
			return false;
		}

		FileDto file = Load();
		if (!TryBuildCardInstanceKey(card, runState, out string cardKey))
		{
			return false;
		}

		if (!file.Runs.TryGetValue(runKey, out RunDto? run) || run == null)
		{
			return false;
		}
		run.Cards ??= new Dictionary<string, CardEditorPresetStore.CardOverrideDto>(StringComparer.Ordinal);
		run.Diffs ??= new Dictionary<string, SelfScalingDiffDto>(StringComparer.Ordinal);

		try
		{
			bool alreadyRebased = cardAlreadyRebased;
			if (!run.Diffs.TryGetValue(cardKey, out SelfScalingDiffDto? diffDto) || diffDto == null)
			{
				if (!TryMigrateLegacySnapshot(card, run, cardKey, cardAlreadyRebased, out diffDto) || diffDto == null)
				{
					return false;
				}
				alreadyRebased = true;
				run.UpdatedAtUtc = DateTime.UtcNow;
				PruneOldRuns(file);
				Save(file);
			}

			CardEditorExtraEffects.SelfScalingMutationDiff diff = diffDto.ToDiff();
			if (diff.IsEmpty)
			{
				return false;
			}

			string marker = BuildAppliedMarker(runKey, cardKey, run);
			if (IsAppliedMarkerCurrent(card, marker))
			{
				return true;
			}

			if (!alreadyRebased)
			{
				RebaseCardToCurrentDefinition(card);
			}

			bool applied = CardEditorExtraEffects.ApplyPersistentSelfScalingDiff(card, diff);
			SetAppliedMarker(card, marker);
			return applied;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RunSelfScaling] Failed to restore {card.Id}: {ex}");
			return false;
		}
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

		bool previous = IsRefreshingForRestore;
		IsRefreshingForRestore = true;
		try
		{
			CardEditorOverrides.SetInstanceOverride(card, null);
			CardEditorOverrides.RefreshCardAfterUpgradeStateChanged(card);
		}
		finally
		{
			IsRefreshingForRestore = previous;
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

	private sealed class AppliedMarker
	{
		public string Value { get; init; } = string.Empty;
	}
}
