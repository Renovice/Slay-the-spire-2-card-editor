using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorRunPowerState
{
	private const int CurrentVersion = 1;
	private const string StorePath = "user://card_editor/run_power_state.json";
	private static readonly TimeSpan MaxRunStateAge = TimeSpan.FromDays(14);
	private static readonly ConditionalWeakTable<CombatState, AppliedCombatMarker> _appliedCombatMarkers = new();

	private static FileDto? _cache;

	public static void Add(Creature? target, string? powerId, int amount, CardExtraEffectDuration duration)
	{
		if (target?.Player == null || string.IsNullOrWhiteSpace(powerId) || amount <= 0)
		{
			return;
		}

		Player player = target.Player;
		IRunState? runState = player.RunState;
		if (!CardEditorRunCardCounterState.TryBuildRunKeyForCardEditor(runState, out string runKey))
		{
			return;
		}

		string playerKey = BuildPlayerKey(player, runState);
		if (string.IsNullOrWhiteSpace(playerKey))
		{
			return;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run))
		{
			run = new RunDto();
			file.Runs[runKey] = run;
		}
		run.UpdatedAtUtc = DateTime.UtcNow;
		run.Players ??= new Dictionary<string, PlayerPowerDto>(StringComparer.Ordinal);
		if (!run.Players.TryGetValue(playerKey, out PlayerPowerDto? playerPowers))
		{
			playerPowers = new PlayerPowerDto();
			run.Players[playerKey] = playerPowers;
		}
		playerPowers.Powers ??= new List<RunPowerEntryDto>();

		string normalizedPowerId = powerId.Trim();
		RunPowerEntryDto? entry = playerPowers.Powers.FirstOrDefault(candidate =>
			candidate != null
			&& string.Equals(candidate.PowerId, normalizedPowerId, StringComparison.Ordinal)
			&& candidate.Duration == duration);
		if (entry == null)
		{
			entry = new RunPowerEntryDto
			{
				PowerId = normalizedPowerId,
				Duration = duration,
				Amount = 0
			};
			playerPowers.Powers.Add(entry);
		}
		entry.Amount = Math.Clamp(entry.Amount + amount, 0, 999999999);

		PruneOldRuns(file);
		Save(file);
	}

	public static async Task ApplyForCombat(CombatState? combatState)
	{
		if (combatState == null
			|| !CardEditorRunCardCounterState.TryBuildRunKeyForCardEditor(combatState.RunState, out string runKey))
		{
			return;
		}

		FileDto file = Load();
		if (!file.Runs.TryGetValue(runKey, out RunDto? run)
			|| run?.Players == null
			|| run.Players.Count == 0)
		{
			return;
		}

		AppliedCombatMarker marker = _appliedCombatMarkers.GetValue(combatState, _ => new AppliedCombatMarker());
		foreach (Player player in combatState.Players ?? Enumerable.Empty<Player>())
		{
			Creature? creature = player?.Creature;
			if (player == null || creature == null)
			{
				continue;
			}

			string playerKey = BuildPlayerKey(player, combatState.RunState);
			if (string.IsNullOrWhiteSpace(playerKey)
				|| !run.Players.TryGetValue(playerKey, out PlayerPowerDto? playerPowers)
				|| playerPowers?.Powers == null)
			{
				continue;
			}

			foreach (RunPowerEntryDto entry in playerPowers.Powers.ToList())
			{
				if (entry == null || string.IsNullOrWhiteSpace(entry.PowerId) || entry.Amount <= 0)
				{
					continue;
				}

				string appliedKey = $"{runKey}|{playerKey}|{entry.PowerId}|{entry.Duration}";
				if (!marker.AppliedKeys.Add(appliedKey))
				{
					continue;
				}

				await CardEditorExtraEffects.ApplyConfiguredPowerForCardEditor(creature, entry.PowerId, entry.Amount, creature, null);

				CardEditorPowerPersistenceTrackerPower? persistenceTracker = await PowerCmd.Apply<CardEditorPowerPersistenceTrackerPower>(creature, 1, creature, null, silent: true);
				persistenceTracker?.Track(entry.PowerId, entry.Amount, entry.Duration, combatState);
			}
		}
	}

	private static string BuildPlayerKey(Player? player, IRunState? runState)
	{
		if (player == null || runState == null)
		{
			return string.Empty;
		}

		int index = -1;
		try
		{
			IReadOnlyList<Player> players = runState.Players;
			for (int i = 0; i < players.Count; i++)
			{
				if (ReferenceEquals(players[i], player))
				{
					index = i;
					break;
				}
			}
		}
		catch
		{
		}

		string character = string.Empty;
		try
		{
			character = player.Character?.Id?.ToString() ?? string.Empty;
		}
		catch
		{
		}

		string netId = string.Empty;
		try
		{
			netId = player.NetId.ToString();
		}
		catch
		{
		}

		return $"player:{index}|net:{netId}|character:{character}";
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
			Log.Warn($"[CardEditor][RunPowerState] Failed to load store: {ex}");
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

			run.Players ??= new Dictionary<string, PlayerPowerDto>(StringComparer.Ordinal);
			foreach ((string playerKey, PlayerPowerDto? player) in run.Players.ToList())
			{
				if (player == null)
				{
					run.Players.Remove(playerKey);
					continue;
				}

				player.Powers ??= new List<RunPowerEntryDto>();
				player.Powers.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.PowerId) || entry.Amount <= 0);
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
			Log.Warn($"[CardEditor][RunPowerState] Failed to save store: {ex}");
		}
	}

	private static void PruneOldRuns(FileDto file)
	{
		DateTime cutoff = DateTime.UtcNow - MaxRunStateAge;
		foreach ((string runKey, RunDto run) in file.Runs.ToList())
		{
			bool hasPowers = run?.Players?.Values.Any(player => player?.Powers?.Any(entry => entry != null && entry.Amount > 0) == true) == true;
			if (run == null || run.UpdatedAtUtc < cutoff || !hasPowers)
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

	private sealed class AppliedCombatMarker
	{
		public HashSet<string> AppliedKeys { get; } = new(StringComparer.Ordinal);
	}

	private sealed class FileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public Dictionary<string, RunDto> Runs { get; set; } = new(StringComparer.Ordinal);
	}

	private sealed class RunDto
	{
		public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
		public Dictionary<string, PlayerPowerDto> Players { get; set; } = new(StringComparer.Ordinal);
	}

	private sealed class PlayerPowerDto
	{
		public List<RunPowerEntryDto> Powers { get; set; } = new();
	}

	private sealed class RunPowerEntryDto
	{
		public string PowerId { get; set; } = string.Empty;
		public int Amount { get; set; }
		public CardExtraEffectDuration Duration { get; set; } = CardExtraEffectDuration.Permanent;
	}
}
