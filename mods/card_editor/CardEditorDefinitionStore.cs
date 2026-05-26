using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorCustomKeywordDefinition
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public IReadOnlyList<CardExtraEffect> Effects { get; init; } = Array.Empty<CardExtraEffect>();
}

internal static class CardEditorDefinitionStore
{
	private sealed class DefinitionsFileDto
	{
		public int Version { get; set; }
		public DateTime SavedAtUtc { get; set; }
		public List<KeywordDefinitionDto>? Keywords { get; set; }
		public List<StatusDefinitionDto>? Statuses { get; set; }
	}

	private sealed class KeywordDefinitionDto
	{
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public List<CardEditorPresetStore.CardExtraEffectDto?>? Effects { get; set; }
	}

	private sealed class StatusDefinitionDto
	{
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Type { get; set; }
		public string? IconMode { get; set; }
		public string? IconPowerId { get; set; }
		public string? CustomPackedIconPath { get; set; }
		public string? CustomBigIconPath { get; set; }
		public List<CardEditorPresetStore.CardExtraEffectDto?>? BehaviorEffects { get; set; }
	}

	private const int CurrentVersion = 1;
	private const string KeywordIdPrefix = "card_editor_custom_keyword:";
	private static readonly object _lock = new();
	private static readonly Dictionary<string, CardEditorCustomKeywordDefinition> _keywords = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, CardEditorCustomStatusDefinition> _statuses = new(StringComparer.OrdinalIgnoreCase);
	private static bool _loaded;

	public static int Revision { get; private set; }
	internal static bool PersistenceSuspended { get; set; }

	public static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}

			LoadInternal();
			_loaded = true;
		}
	}

	public static string? BuildKeywordId(string? name)
	{
		string normalized = NormalizeName(name);
		return string.IsNullOrWhiteSpace(normalized)
			? null
			: KeywordIdPrefix + Uri.EscapeDataString(normalized);
	}

	public static IReadOnlyList<CardEditorCustomKeywordDefinition> GetKeywordDefinitions()
	{
		EnsureLoaded();
		return _keywords.Values
			.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
			.Select(CloneKeywordDefinition)
			.ToList();
	}

	public static IReadOnlyList<CardEditorCustomStatusDefinition> GetStatusDefinitions()
	{
		EnsureLoaded();
		return _statuses.Values
			.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
			.Select(CloneStatusDefinition)
			.ToList();
	}

	public static void ImportDefinitions(
		IEnumerable<CardEditorCustomKeywordDefinition>? keywords,
		IEnumerable<CardEditorCustomStatusDefinition>? statuses)
	{
		EnsureLoaded();
		bool changed = false;

		foreach (CardEditorCustomKeywordDefinition def in keywords ?? Array.Empty<CardEditorCustomKeywordDefinition>())
		{
			string name = NormalizeName(def?.Name);
			string? id = string.IsNullOrWhiteSpace(def?.Id) ? BuildKeywordId(name) : def.Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			_keywords[id] = new CardEditorCustomKeywordDefinition
			{
				Id = id,
				Name = name,
				Description = NormalizeDescription(def?.Description),
				Effects = CloneEffects(def?.Effects)
			};
			changed = true;
		}

		foreach (CardEditorCustomStatusDefinition def in statuses ?? Array.Empty<CardEditorCustomStatusDefinition>())
		{
			string name = NormalizeName(def?.Name);
			string? id = string.IsNullOrWhiteSpace(def?.Id) ? CardEditorCustomStatusRegistry.BuildId(name) : def.Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			_statuses[id] = new CardEditorCustomStatusDefinition
			{
				Id = id,
				Name = name,
				Description = NormalizeDescription(def?.Description),
				SourceCardId = null,
				BehaviorEffects = CloneEffects(def?.BehaviorEffects),
				Type = def?.Type ?? PowerType.Buff,
				IconMode = def?.IconMode ?? CardExtraEffectStatusIconMode.Auto,
				IconPowerId = NormalizeOptional(def?.IconPowerId),
				CustomPackedIconPath = NormalizeOptional(def?.CustomPackedIconPath),
				CustomBigIconPath = NormalizeOptional(def?.CustomBigIconPath)
			};
			changed = true;
		}

		if (!changed)
		{
			return;
		}

		Revision++;
		Save();
	}

	internal static void ReplaceDefinitions(
		IEnumerable<CardEditorCustomKeywordDefinition>? keywords,
		IEnumerable<CardEditorCustomStatusDefinition>? statuses)
	{
		EnsureLoaded();
		_keywords.Clear();
		_statuses.Clear();

		foreach (CardEditorCustomKeywordDefinition def in keywords ?? Array.Empty<CardEditorCustomKeywordDefinition>())
		{
			string name = NormalizeName(def?.Name);
			string? id = string.IsNullOrWhiteSpace(def?.Id) ? BuildKeywordId(name) : def.Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			_keywords[id] = new CardEditorCustomKeywordDefinition
			{
				Id = id,
				Name = name,
				Description = NormalizeDescription(def?.Description),
				Effects = CloneEffects(def?.Effects)
			};
		}

		foreach (CardEditorCustomStatusDefinition def in statuses ?? Array.Empty<CardEditorCustomStatusDefinition>())
		{
			string name = NormalizeName(def?.Name);
			string? id = string.IsNullOrWhiteSpace(def?.Id) ? CardEditorCustomStatusRegistry.BuildId(name) : def.Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			_statuses[id] = new CardEditorCustomStatusDefinition
			{
				Id = id,
				Name = name,
				Description = NormalizeDescription(def?.Description),
				SourceCardId = null,
				BehaviorEffects = CloneEffects(def?.BehaviorEffects),
				Type = def?.Type ?? PowerType.Buff,
				IconMode = def?.IconMode ?? CardExtraEffectStatusIconMode.Auto,
				IconPowerId = NormalizeOptional(def?.IconPowerId),
				CustomPackedIconPath = NormalizeOptional(def?.CustomPackedIconPath),
				CustomBigIconPath = NormalizeOptional(def?.CustomBigIconPath)
			};
		}

		Revision++;
		Save();
	}

	public static string? UpsertKeyword(string? previousId, string? name, string? description, IEnumerable<CardExtraEffect>? effects)
	{
		EnsureLoaded();
		string normalizedName = NormalizeName(name);
		string? id = BuildKeywordId(normalizedName);
		if (string.IsNullOrWhiteSpace(id))
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(previousId)
			&& !string.Equals(previousId.Trim(), id, StringComparison.OrdinalIgnoreCase))
		{
			_keywords.Remove(previousId.Trim());
		}

		_keywords[id] = new CardEditorCustomKeywordDefinition
		{
			Id = id,
			Name = normalizedName,
			Description = NormalizeDescription(description),
			Effects = CloneEffects(effects)
		};
		Revision++;
		Save();
		return id;
	}

	public static string? UpsertStatus(
		string? previousId,
		string? name,
		string? description,
		PowerType type,
		CardExtraEffectStatusIconMode iconMode,
		string? iconPowerId,
		string? customPackedIconPath,
		string? customBigIconPath,
		IEnumerable<CardExtraEffect>? behaviorEffects)
	{
		EnsureLoaded();
		string normalizedName = NormalizeName(name);
		string? id = CardEditorCustomStatusRegistry.BuildId(normalizedName);
		if (string.IsNullOrWhiteSpace(id))
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(previousId)
			&& !string.Equals(previousId.Trim(), id, StringComparison.OrdinalIgnoreCase))
		{
			_statuses.Remove(previousId.Trim());
		}

		_statuses[id] = new CardEditorCustomStatusDefinition
		{
			Id = id,
			Name = normalizedName,
			Description = NormalizeDescription(description),
			SourceCardId = null,
			BehaviorEffects = CloneEffects(behaviorEffects),
			Type = type,
			IconMode = iconMode,
			IconPowerId = NormalizeOptional(iconPowerId),
			CustomPackedIconPath = NormalizeOptional(customPackedIconPath),
			CustomBigIconPath = NormalizeOptional(customBigIconPath)
		};
		Revision++;
		Save();
		return id;
	}

	public static bool DeleteKeyword(string? id)
	{
		EnsureLoaded();
		if (string.IsNullOrWhiteSpace(id) || !_keywords.Remove(id.Trim()))
		{
			return false;
		}

		Revision++;
		Save();
		return true;
	}

	public static bool DeleteStatus(string? id)
	{
		EnsureLoaded();
		if (string.IsNullOrWhiteSpace(id) || !_statuses.Remove(id.Trim()))
		{
			return false;
		}

		Revision++;
		Save();
		return true;
	}

	private static void LoadInternal()
	{
		try
		{
			_keywords.Clear();
			_statuses.Clear();

			string path = GetStorePath();
			if (!File.Exists(path))
			{
				return;
			}

			string json = File.ReadAllText(path);
			DefinitionsFileDto? file = JsonSerializer.Deserialize<DefinitionsFileDto>(json, CreateJsonOptions());
			if (file == null || file.Version <= 0 || file.Version > CurrentVersion)
			{
				return;
			}

			foreach (KeywordDefinitionDto dto in file.Keywords ?? new List<KeywordDefinitionDto>())
			{
				CardEditorCustomKeywordDefinition? def = dto.ToKeywordDefinitionSafe();
				if (def != null)
				{
					_keywords[def.Id] = def;
				}
			}

			foreach (StatusDefinitionDto dto in file.Statuses ?? new List<StatusDefinitionDto>())
			{
				CardEditorCustomStatusDefinition? def = dto.ToStatusDefinitionSafe();
				if (def != null)
				{
					_statuses[def.Id] = def;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading definition store: {ex}");
			_keywords.Clear();
			_statuses.Clear();
		}
	}

	private static void Save()
	{
		if (PersistenceSuspended)
		{
			return;
		}

		try
		{
			string path = GetStorePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			DefinitionsFileDto file = new DefinitionsFileDto
			{
				Version = CurrentVersion,
				SavedAtUtc = DateTime.UtcNow,
				Keywords = _keywords.Values
					.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
					.Select(FromDefinition)
					.ToList(),
				Statuses = _statuses.Values
					.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
					.Select(FromDefinition)
					.ToList()
			};

			File.WriteAllText(path, JsonSerializer.Serialize(file, CreateJsonOptions()));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving definition store: {ex}");
		}
	}

	private static string GetStorePath()
	{
		return ProjectSettings.GlobalizePath("user://card_editor/definitions.json");
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private static CardEditorCustomKeywordDefinition? ToKeywordDefinitionSafe(this KeywordDefinitionDto dto)
	{
		string name = NormalizeName(dto.Name);
		string? id = string.IsNullOrWhiteSpace(dto.Id) ? BuildKeywordId(name) : dto.Id.Trim();
		if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		return new CardEditorCustomKeywordDefinition
		{
			Id = id,
			Name = name,
			Description = NormalizeDescription(dto.Description),
			Effects = ParseEffects(dto.Effects)
		};
	}

	private static KeywordDefinitionDto FromDefinition(CardEditorCustomKeywordDefinition def)
	{
		return new KeywordDefinitionDto
		{
			Id = def.Id,
			Name = def.Name,
			Description = def.Description,
			Effects = SerializeEffects(def.Effects)
		};
	}

	private static CardEditorCustomStatusDefinition? ToStatusDefinitionSafe(this StatusDefinitionDto dto)
	{
		string name = NormalizeName(dto.Name);
		string? id = string.IsNullOrWhiteSpace(dto.Id) ? CardEditorCustomStatusRegistry.BuildId(name) : dto.Id.Trim();
		if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		PowerType type = PowerType.Buff;
		if (!string.IsNullOrWhiteSpace(dto.Type) && Enum.TryParse(dto.Type, out PowerType parsedType))
		{
			type = parsedType;
		}

		CardExtraEffectStatusIconMode iconMode = CardExtraEffectStatusIconMode.Auto;
		if (!string.IsNullOrWhiteSpace(dto.IconMode) && Enum.TryParse(dto.IconMode, out CardExtraEffectStatusIconMode parsedIconMode))
		{
			iconMode = parsedIconMode;
		}

		return new CardEditorCustomStatusDefinition
		{
			Id = id,
			Name = name,
			Description = NormalizeDescription(dto.Description),
			SourceCardId = null,
			BehaviorEffects = ParseEffects(dto.BehaviorEffects),
			Type = type,
			IconMode = iconMode,
			IconPowerId = NormalizeOptional(dto.IconPowerId),
			CustomPackedIconPath = NormalizeOptional(dto.CustomPackedIconPath),
			CustomBigIconPath = NormalizeOptional(dto.CustomBigIconPath)
		};
	}

	private static StatusDefinitionDto FromDefinition(CardEditorCustomStatusDefinition def)
	{
		return new StatusDefinitionDto
		{
			Id = def.Id,
			Name = def.Name,
			Description = def.Description,
			Type = def.Type.ToString(),
			IconMode = def.IconMode.ToString(),
			IconPowerId = def.IconPowerId,
			CustomPackedIconPath = def.CustomPackedIconPath,
			CustomBigIconPath = def.CustomBigIconPath,
			BehaviorEffects = SerializeEffects(def.BehaviorEffects)
		};
	}

	private static IReadOnlyList<CardExtraEffect> ParseEffects(IEnumerable<CardEditorPresetStore.CardExtraEffectDto?>? dtos)
	{
		if (dtos == null)
		{
			return Array.Empty<CardExtraEffect>();
		}

		List<CardExtraEffect> effects = new();
		foreach (CardEditorPresetStore.CardExtraEffectDto? dto in dtos)
		{
			if (dto == null || !dto.TryToEffect(out CardExtraEffect effect))
			{
				continue;
			}
			effects.Add(effect);
		}
		return effects;
	}

	private static List<CardEditorPresetStore.CardExtraEffectDto?> SerializeEffects(IEnumerable<CardExtraEffect>? effects)
	{
		return (effects ?? Array.Empty<CardExtraEffect>())
			.Where(effect => effect != null)
			.Select(effect => (CardEditorPresetStore.CardExtraEffectDto?)CardEditorPresetStore.CardExtraEffectDto.FromEffect(effect))
			.ToList();
	}

	private static IReadOnlyList<CardExtraEffect> CloneEffects(IEnumerable<CardExtraEffect>? effects)
	{
		return (effects ?? Array.Empty<CardExtraEffect>())
			.Where(effect => effect != null)
			.Select(CardEditorExtraEffects.CloneEffect)
			.ToList();
	}

	private static CardEditorCustomKeywordDefinition CloneKeywordDefinition(CardEditorCustomKeywordDefinition def)
	{
		return new CardEditorCustomKeywordDefinition
		{
			Id = def.Id,
			Name = def.Name,
			Description = def.Description,
			Effects = CloneEffects(def.Effects)
		};
	}

	private static CardEditorCustomStatusDefinition CloneStatusDefinition(CardEditorCustomStatusDefinition def)
	{
		return new CardEditorCustomStatusDefinition
		{
			Id = def.Id,
			Name = def.Name,
			Description = def.Description,
			SourceCardId = def.SourceCardId,
			BehaviorEffects = CloneEffects(def.BehaviorEffects),
			Type = def.Type,
			IconMode = def.IconMode,
			IconPowerId = def.IconPowerId,
			CustomPackedIconPath = def.CustomPackedIconPath,
			CustomBigIconPath = def.CustomBigIconPath
		};
	}

	private static string NormalizeName(string? name)
	{
		return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
	}

	private static string NormalizeDescription(string? description)
	{
		return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
	}

	private static string? NormalizeOptional(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
