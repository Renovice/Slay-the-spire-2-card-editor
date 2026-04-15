using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCreatorPresetStore
{
	private const int CurrentVersion = 1;
	private const int OverrideDtoVersion = 3;
	private const string PresetExtension = ".json";
	private const string SettingsPath = "user://card_editor/creator_presets_settings.json";

	private static bool? _sortByCharacterCached;

	public static List<string> ListPresetNames()
	{
		try
		{
			string dir = EnsurePresetDirectory();
			return Directory.EnumerateFiles(dir, "*" + PresetExtension, SearchOption.TopDirectoryOnly)
				.Select(Path.GetFileNameWithoutExtension)
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToList()!;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed listing creator presets: {ex}");
			return new List<string>();
		}
	}

	public static bool TryLoadPreset(string presetName, out Dictionary<ModelId, CardEditorCreatedCardDefinition> definitions)
	{
		definitions = new Dictionary<ModelId, CardEditorCreatedCardDefinition>();
		string safeName = SanitizePresetName(presetName);
		if (string.IsNullOrWhiteSpace(safeName))
		{
			return false;
		}

		try
		{
			CardEditorCreatedCardsStore.EnsureLoaded();

			string path = GetPresetPath(safeName);
			if (!File.Exists(path))
			{
				return false;
			}

			string json = File.ReadAllText(path);
			CreatorPresetFileDto? data = JsonSerializer.Deserialize<CreatorPresetFileDto>(json, CreateJsonOptions());
			if (data == null || data.Cards == null)
			{
				return false;
			}

			if (data.Version <= 0 || data.Version > CurrentVersion)
			{
				Log.Warn($"[CardEditor] Unsupported creator preset version={data.Version} (current={CurrentVersion})");
				return false;
			}

			foreach ((string idString, CreatedCardDto dto) in data.Cards)
			{
				if (!TryParseModelId(idString, out ModelId cardId) || !IsCreatedCardIdInRange(cardId))
				{
					continue;
				}

				CardEditorCreatedCardDefinition def = dto.ToDefinitionSafe(cardId);
				definitions[cardId] = def;
			}

			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading creator preset '{presetName}': {ex}");
			return false;
		}
	}

	public static bool TrySavePreset(string presetName, IReadOnlyDictionary<ModelId, CardEditorCreatedCardDefinition> definitions)
	{
		string safeName = SanitizePresetName(presetName);
		if (string.IsNullOrWhiteSpace(safeName))
		{
			return false;
		}

		try
		{
			EnsurePresetDirectory();
			string path = GetPresetPath(safeName);

			CardEditorCreatedCardsStore.EnsureLoaded();

			CreatorPresetFileDto data = new CreatorPresetFileDto
			{
				Version = CurrentVersion,
				SavedAtUtc = DateTime.UtcNow,
				SlotCount = Math.Clamp(CardEditorCreatedCardsStore.SlotCount, 1, CardEditorCreatedCardsStore.MaxSlotCount),
				Cards = definitions
					.Where(kvp => kvp.Key != null && IsCreatedCardIdInRange(kvp.Key))
					.ToDictionary(
						kvp => kvp.Key.ToString(),
						kvp => CreatedCardDto.FromDefinition(kvp.Value),
						StringComparer.Ordinal)
			};

			string json = JsonSerializer.Serialize(data, CreateJsonOptions());
			File.WriteAllText(path, json);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving creator preset '{presetName}': {ex}");
			return false;
		}
	}

	public static bool TryDeletePreset(string presetName)
	{
		string safeName = SanitizePresetName(presetName);
		if (string.IsNullOrWhiteSpace(safeName))
		{
			return false;
		}

		try
		{
			string path = GetPresetPath(safeName);
			if (!File.Exists(path))
			{
				return false;
			}
			File.Delete(path);

			string? startup = GetStartupPresetName();
			if (!string.IsNullOrWhiteSpace(startup) && string.Equals(startup, safeName, StringComparison.OrdinalIgnoreCase))
			{
				SetStartupPresetName(null);
			}

			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed deleting creator preset '{presetName}': {ex}");
			return false;
		}
	}

	public static string? GetStartupPresetName()
	{
		CreatorPresetSettingsDto data = ReadSettingsInternal();
		string safe = SanitizePresetName(data.StartupPresetName ?? string.Empty);
		return string.IsNullOrWhiteSpace(safe) ? null : safe;
	}

	public static void SetStartupPresetName(string? presetName)
	{
		string safe = SanitizePresetName(presetName ?? string.Empty);
		try
		{
			CreatorPresetSettingsDto data = ReadSettingsInternal();
			data.StartupPresetName = string.IsNullOrWhiteSpace(safe) ? null : safe;
			WriteSettingsInternal(data);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving creator preset settings: {ex}");
		}
	}

	public static bool GetSortByCharacter()
	{
		_sortByCharacterCached ??= ReadSettingsInternal().SortByCharacter;
		return _sortByCharacterCached.Value;
	}

	public static void SetSortByCharacter(bool value)
	{
		_sortByCharacterCached = value;
		try
		{
			CreatorPresetSettingsDto data = ReadSettingsInternal();
			data.SortByCharacter = value;
			WriteSettingsInternal(data);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving creator preset settings: {ex}");
		}
	}

	private static CreatorPresetSettingsDto ReadSettingsInternal()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(SettingsPath);
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				return JsonSerializer.Deserialize<CreatorPresetSettingsDto>(json, CreateJsonOptions()) ?? new CreatorPresetSettingsDto();
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed reading creator preset settings: {ex}");
		}
		return new CreatorPresetSettingsDto();
	}

	private static void WriteSettingsInternal(CreatorPresetSettingsDto data)
	{
		string path = ProjectSettings.GlobalizePath(SettingsPath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string json = JsonSerializer.Serialize(data, CreateJsonOptions());
		File.WriteAllText(path, json);
	}

	public static bool TryLoadStartupPreset(out string presetName, out Dictionary<ModelId, CardEditorCreatedCardDefinition> definitions)
	{
		definitions = new Dictionary<ModelId, CardEditorCreatedCardDefinition>();
		presetName = GetStartupPresetName() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(presetName))
		{
			return false;
		}
		return TryLoadPreset(presetName, out definitions);
	}

	private static string EnsurePresetDirectory()
	{
		string dir = ProjectSettings.GlobalizePath("user://card_editor/creator_presets");
		Directory.CreateDirectory(dir);
		return dir;
	}

	private static string GetPresetPath(string safeName)
	{
		string dir = EnsurePresetDirectory();
		return Path.Combine(dir, safeName + PresetExtension);
	}

	private static string SanitizePresetName(string name)
	{
		if (name == null)
		{
			return string.Empty;
		}
		string trimmed = name.Trim();
		if (trimmed.Length == 0)
		{
			return string.Empty;
		}
		foreach (char c in Path.GetInvalidFileNameChars())
		{
			trimmed = trimmed.Replace(c, '_');
		}
		return trimmed;
	}

	private static bool TryParseModelId(string text, out ModelId id)
	{
		try
		{
			id = ModelId.Deserialize(text);
			return true;
		}
		catch
		{
			id = ModelId.none;
			return false;
		}
	}

	private static bool IsCreatedCardIdInRange(ModelId id)
	{
		if (!CardEditorCreatedCardsStore.IsCreatedCardId(id))
		{
			return false;
		}
		string suffix = id.Entry.Substring("CARD_EDITOR_CREATED_CARD".Length);
		return int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int slot)
			&& slot >= 1
			&& slot <= CardEditorCreatedCardsStore.SlotCount;
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private sealed class CreatorPresetFileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public DateTime SavedAtUtc { get; set; }
		public int SlotCount { get; set; }
		public Dictionary<string, CreatedCardDto> Cards { get; set; } = new Dictionary<string, CreatedCardDto>(StringComparer.Ordinal);
	}

	private sealed class CreatorPresetSettingsDto
	{
		public string? StartupPresetName { get; set; }
		public bool SortByCharacter { get; set; }
	}

	private sealed class CreatedCardDto
	{
		public bool Enabled { get; set; }
		public string? Title { get; set; }
		public string? Pool { get; set; }
		public string? Rarity { get; set; }
		public string? Type { get; set; }
		public string? TargetType { get; set; }
		public bool FullArt { get; set; }
		public string? Finish { get; set; }
		public Dictionary<string, decimal>? FinishParams { get; set; }
		public string? EffectSourceCardId { get; set; }
		public List<string>? EffectSourceCardIds { get; set; }
		public string? PortraitSourceCardId { get; set; }
		public string? CustomPortraitFile { get; set; }
		public string? CustomText { get; set; }
		public string? CustomTextUpgraded { get; set; }
		public CardEditorPresetStore.CardOverrideDto? Override { get; set; }

		public static CreatedCardDto FromDefinition(CardEditorCreatedCardDefinition def)
		{
			return new CreatedCardDto
			{
				Enabled = def.Enabled,
				Title = def.Title,
				Pool = def.Pool.ToString(),
				Rarity = def.Rarity.ToString(),
				Type = def.Type.ToString(),
				TargetType = def.TargetType.ToString(),
				FullArt = def.FullArt,
				Finish = def.Finish.ToString(),
				FinishParams = def.FinishParams != null && def.FinishParams.Count > 0
					? def.FinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
				EffectSourceCardIds = def.EffectSourceCardIds != null && def.EffectSourceCardIds.Count > 0
					? def.EffectSourceCardIds.Select(id => id.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
					: null,
				PortraitSourceCardId = def.PortraitSourceCardId?.ToString(),
				CustomPortraitFile = def.CustomPortraitFile,
				CustomText = def.CustomText,
				CustomTextUpgraded = def.CustomTextUpgraded,
				Override = CardEditorPresetStore.CardOverrideDto.FromOverride(def.Override ?? new CardOverride())
			};
		}

		public CardEditorCreatedCardDefinition ToDefinitionSafe(ModelId cardId)
		{
			CardEditorCreatedCardDefinition def = new CardEditorCreatedCardDefinition();

			def.Enabled = Enabled;
			def.Title = Title?.Trim() ?? string.Empty;

			if (Enum.TryParse(Pool ?? string.Empty, ignoreCase: true, out CardEditorCreatedCardPool pool))
			{
				def.Pool = pool;
			}

			if (Enum.TryParse(Rarity ?? string.Empty, ignoreCase: true, out MegaCrit.Sts2.Core.Entities.Cards.CardRarity rarity))
			{
				def.Rarity = rarity;
			}

			if (Enum.TryParse(Type ?? string.Empty, ignoreCase: true, out MegaCrit.Sts2.Core.Entities.Cards.CardType type))
			{
				def.Type = type;
			}

			if (Enum.TryParse(TargetType ?? string.Empty, ignoreCase: true, out MegaCrit.Sts2.Core.Entities.Cards.TargetType targetType))
			{
				def.TargetType = targetType;
			}

			def.FullArt = FullArt;

			if (Enum.TryParse(Finish ?? string.Empty, ignoreCase: true, out CardEditorVisualFinish finish))
			{
				def.Finish = finish;
			}

			if (FinishParams != null && FinishParams.Count > 0)
			{
				def.FinishParams = FinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
			}

			if (EffectSourceCardIds != null && EffectSourceCardIds.Count > 0)
			{
				foreach (string idStr in EffectSourceCardIds)
				{
					if (!string.IsNullOrWhiteSpace(idStr) && TryParseModelId(idStr, out ModelId parsedId))
					{
						def.EffectSourceCardIds.Add(parsedId);
					}
				}
			}
			else if (!string.IsNullOrWhiteSpace(EffectSourceCardId) && TryParseModelId(EffectSourceCardId, out ModelId parsedEffectSourceId))
			{
				def.EffectSourceCardIds.Add(parsedEffectSourceId);
			}

			if (!string.IsNullOrWhiteSpace(PortraitSourceCardId) && TryParseModelId(PortraitSourceCardId, out ModelId portraitId))
			{
				def.PortraitSourceCardId = portraitId;
			}

			def.CustomPortraitFile = string.IsNullOrWhiteSpace(CustomPortraitFile) ? null : CustomPortraitFile.Trim();

			if (!string.IsNullOrWhiteSpace(CustomText))
			{
				def.CustomText = CustomText;
			}

			if (!string.IsNullOrWhiteSpace(CustomTextUpgraded))
			{
				def.CustomTextUpgraded = CustomTextUpgraded;
			}

			def.Override = Override?.ToOverrideSafe(cardId, OverrideDtoVersion) ?? new CardOverride();

			return def;
		}
	}
}
