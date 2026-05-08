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

internal static class CardEditorRunCardCounterState
{
	private const int CurrentVersion = 1;
	private const string StorePath = "user://card_editor/run_card_counters.json";
	private static readonly TimeSpan MaxRunStateAge = TimeSpan.FromDays(14);
	private static readonly ConditionalWeakTable<CombatState, CombatCounterStore> _combatStores = new ConditionalWeakTable<CombatState, CombatCounterStore>();

	private static FileDto? _cache;

	public static int Increment(CombatState? combatState, CardModel? card, string counterKey, bool persistent)
	{
		return Add(combatState, card, counterKey, 1, persistent);
	}

	public static int Add(CombatState? combatState, CardModel? card, string counterKey, int amount, bool persistent)
	{
		if (card == null || string.IsNullOrWhiteSpace(counterKey))
		{
			return 0;
		}
		if (amount <= 0)
		{
			return Get(combatState, card, counterKey, persistent);
		}

		if (!persistent)
		{
			if (combatState == null)
			{
				return 0;
			}
			return AddCombatCounter(combatState, card, counterKey, amount);
		}

		return AddPersistentCounter(card, counterKey, amount);
	}

	public static int Get(CombatState? combatState, CardModel? card, string counterKey, bool persistent)
	{
		if (card == null || string.IsNullOrWhiteSpace(counterKey))
		{
			return 0;
		}

		if (!persistent)
		{
			return combatState == null ? 0 : GetCombatCounter(combatState, card, counterKey);
		}

		return GetPersistentCounter(card, counterKey);
	}

	public static void Clear(CombatState? combatState, CardModel? card, string counterKey, bool persistent)
	{
		if (card == null || string.IsNullOrWhiteSpace(counterKey))
		{
			return;
		}

		if (!persistent)
		{
			if (combatState != null && _combatStores.TryGetValue(combatState, out CombatCounterStore? store))
			{
				store.Clear(card, counterKey);
			}
			return;
		}

		ClearPersistentCounter(card, counterKey);
	}

	private static int AddCombatCounter(CombatState combatState, CardModel card, string counterKey, int amount)
	{
		CombatCounterStore store = _combatStores.GetValue(combatState, _ => new CombatCounterStore());
		return store.Add(card, counterKey, amount);
	}

	private static int GetCombatCounter(CombatState combatState, CardModel card, string counterKey)
	{
		return _combatStores.TryGetValue(combatState, out CombatCounterStore? store)
			? store.Get(card, counterKey)
			: 0;
	}

	private static int AddPersistentCounter(CardModel card, string counterKey, int amount)
	{
		IRunState? runState = TryGetRunState(card);
		if (!TryBuildRunKey(runState, out string runKey)
			|| !TryBuildCardInstanceKey(card, runState, out string cardKey))
		{
			return 0;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run))
		{
			run = new RunDto();
			file.Runs[runKey] = run;
		}
		run.UpdatedAtUtc = DateTime.UtcNow;
		run.Cards ??= new Dictionary<string, CardCounterDto>(StringComparer.Ordinal);
		if (!run.Cards.TryGetValue(cardKey, out CardCounterDto? cardCounters))
		{
			cardCounters = new CardCounterDto();
			run.Cards[cardKey] = cardCounters;
		}
		cardCounters.Counters ??= new Dictionary<string, int>(StringComparer.Ordinal);
		cardCounters.Counters.TryGetValue(counterKey, out int value);
		value = Math.Max(0, value) + amount;
		cardCounters.Counters[counterKey] = value;
		PruneOldRuns(file);
		Save(file);
		return value;
	}

	private static int GetPersistentCounter(CardModel card, string counterKey)
	{
		IRunState? runState = TryGetRunState(card);
		if (!TryBuildRunKey(runState, out string runKey)
			|| !TryBuildCardInstanceKey(card, runState, out string cardKey))
		{
			return 0;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run)
			|| run?.Cards == null
			|| !run.Cards.TryGetValue(cardKey, out CardCounterDto? cardCounters)
			|| cardCounters?.Counters == null
			|| !cardCounters.Counters.TryGetValue(counterKey, out int value))
		{
			return 0;
		}

		return Math.Max(0, value);
	}

	private static void ClearPersistentCounter(CardModel card, string counterKey)
	{
		IRunState? runState = TryGetRunState(card);
		if (!TryBuildRunKey(runState, out string runKey)
			|| !TryBuildCardInstanceKey(card, runState, out string cardKey))
		{
			return;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run)
			|| run?.Cards == null
			|| !run.Cards.TryGetValue(cardKey, out CardCounterDto? cardCounters)
			|| cardCounters?.Counters == null)
		{
			return;
		}

		if (cardCounters.Counters.Remove(counterKey))
		{
			if (cardCounters.Counters.Count == 0)
			{
				run.Cards.Remove(cardKey);
			}
			run.UpdatedAtUtc = DateTime.UtcNow;
			PruneOldRuns(file);
			Save(file);
		}
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

	internal static bool TryBuildRunKeyForCardEditor(IRunState? runState, out string key)
		=> TryBuildRunKey(runState, out key);

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
			Log.Warn($"[CardEditor][RunCardCounters] Failed to load store: {ex}");
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
			run.Cards ??= new Dictionary<string, CardCounterDto>(StringComparer.Ordinal);
			foreach ((string cardKey, CardCounterDto? card) in run.Cards.ToList())
			{
				if (card == null)
				{
					run.Cards.Remove(cardKey);
					continue;
				}
				card.Counters ??= new Dictionary<string, int>(StringComparer.Ordinal);
			}
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
			Log.Warn($"[CardEditor][RunCardCounters] Failed to save store: {ex}");
		}
	}

	private static void PruneOldRuns(FileDto file)
	{
		DateTime cutoff = DateTime.UtcNow - MaxRunStateAge;
		foreach ((string runKey, RunDto run) in file.Runs.ToList())
		{
			bool hasCounters = run?.Cards != null && run.Cards.Count > 0;
			if (run == null || run.UpdatedAtUtc < cutoff || !hasCounters)
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

	private sealed class CombatCounterStore
	{
		private readonly ConditionalWeakTable<CardModel, CardCounters> _cards = new ConditionalWeakTable<CardModel, CardCounters>();

		public int Add(CardModel card, string counterKey, int amount)
		{
			CardCounters counters = _cards.GetValue(card, _ => new CardCounters());
			counters.Values.TryGetValue(counterKey, out int value);
			value = Math.Max(0, value) + amount;
			counters.Values[counterKey] = value;
			return value;
		}

		public int Get(CardModel card, string counterKey)
		{
			return _cards.TryGetValue(card, out CardCounters? counters)
				&& counters.Values.TryGetValue(counterKey, out int value)
				? Math.Max(0, value)
				: 0;
		}

		public void Clear(CardModel card, string counterKey)
		{
			if (_cards.TryGetValue(card, out CardCounters? counters))
			{
				counters.Values.Remove(counterKey);
			}
		}
	}

	private sealed class CardCounters
	{
		public Dictionary<string, int> Values { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
	}

	private sealed class FileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public Dictionary<string, RunDto> Runs { get; set; } = new Dictionary<string, RunDto>(StringComparer.Ordinal);
	}

	private sealed class RunDto
	{
		public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
		public Dictionary<string, CardCounterDto> Cards { get; set; } = new Dictionary<string, CardCounterDto>(StringComparer.Ordinal);
	}

	private sealed class CardCounterDto
	{
		public Dictionary<string, int> Counters { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
	}
}
