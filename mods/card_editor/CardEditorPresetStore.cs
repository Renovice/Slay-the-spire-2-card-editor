using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPresetStore
{
	private const int CurrentVersion = 15;
	private const string PresetExtension = ".json";
	private const string SettingsPath = "user://card_editor/presets_settings.json";

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
			Log.Warn($"[CardEditor] Failed listing presets: {ex}");
			return new List<string>();
		}
	}

	public static bool TryLoadPreset(string presetName, out Dictionary<ModelId, CardOverride> overrides)
	{
		return TryLoadPreset(presetName, out overrides, out _);
	}

	public static bool TryLoadPreset(string presetName, out Dictionary<ModelId, CardOverride> overrides, out Dictionary<ModelId, List<ModelId>> baseDecks)
	{
		overrides = new Dictionary<ModelId, CardOverride>();
		baseDecks = new Dictionary<ModelId, List<ModelId>>();
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

			string json = File.ReadAllText(path);
			PresetFileDto? data = JsonSerializer.Deserialize<PresetFileDto>(json, CreateJsonOptions());
			if (data == null || data.Overrides == null)
			{
				return false;
			}

			if (data.Version <= 0 || data.Version > CurrentVersion)
			{
				Log.Warn($"[CardEditor] Unsupported preset version={data.Version} (current={CurrentVersion})");
				return false;
			}

			foreach ((string idString, CardOverrideDto dto) in data.Overrides)
			{
				if (!TryParseModelId(idString, out ModelId cardId))
				{
					continue;
				}
				if (ModelDb.GetByIdOrNull<CardModel>(cardId) == null)
				{
					continue;
				}

				CardOverride overrideData = dto.ToOverrideSafe(cardId, data.Version);
				if (!overrideData.IsEmpty())
				{
					overrides[cardId] = overrideData;
				}
			}

			if (data.BaseDecks != null)
			{
				baseDecks = DeserializeBaseDecks(data.BaseDecks);
			}
			ImportDefinitionsFromPreset(data);

			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading preset '{presetName}': {ex}");
			return false;
		}
	}

	public static bool TrySavePreset(string presetName, IReadOnlyDictionary<ModelId, CardOverride> overrides)
	{
		return TrySavePreset(presetName, overrides, null);
	}

	public static bool TrySavePreset(string presetName, IReadOnlyDictionary<ModelId, CardOverride> overrides, IReadOnlyDictionary<ModelId, List<ModelId>>? baseDecks)
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

			PresetFileDto data = new PresetFileDto
			{
				Version = CurrentVersion,
				SavedAtUtc = DateTime.UtcNow,
				Overrides = overrides.ToDictionary(
					kvp => kvp.Key.ToString(),
					kvp => CardOverrideDto.FromOverride(kvp.Value),
					StringComparer.Ordinal),
				BaseDecks = SerializeBaseDecks(baseDecks),
				KeywordDefinitions = CardEditorDefinitionStore.GetKeywordDefinitions()
					.Select(CustomKeywordDefinitionDto.FromDefinition)
					.ToList(),
				StatusDefinitions = CardEditorDefinitionStore.GetStatusDefinitions()
					.Select(CustomStatusDefinitionDto.FromDefinition)
					.ToList()
			};

			string json = JsonSerializer.Serialize(data, CreateJsonOptions());
			File.WriteAllText(path, json);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving preset '{presetName}': {ex}");
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
			Log.Warn($"[CardEditor] Failed deleting preset '{presetName}': {ex}");
			return false;
		}
	}

	public static string? GetStartupPresetName()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(SettingsPath);
			if (!File.Exists(path))
			{
				return null;
			}

			string json = File.ReadAllText(path);
			PresetSettingsDto? data = JsonSerializer.Deserialize<PresetSettingsDto>(json, CreateJsonOptions());
			string safe = SanitizePresetName(data?.StartupPresetName ?? string.Empty);
			return string.IsNullOrWhiteSpace(safe) ? null : safe;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed reading preset settings: {ex}");
			return null;
		}
	}

	public static void SetStartupPresetName(string? presetName)
	{
		try
		{
			string safe = SanitizePresetName(presetName ?? string.Empty);

			string path = ProjectSettings.GlobalizePath(SettingsPath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			PresetSettingsDto data = new PresetSettingsDto
			{
				StartupPresetName = string.IsNullOrWhiteSpace(safe) ? null : safe
			};

			string json = JsonSerializer.Serialize(data, CreateJsonOptions());
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving preset settings: {ex}");
		}
	}

	public static bool TryLoadStartupPreset(out string presetName, out Dictionary<ModelId, CardOverride> overrides)
	{
		return TryLoadStartupPreset(out presetName, out overrides, out _);
	}

	public static bool TryLoadStartupPreset(out string presetName, out Dictionary<ModelId, CardOverride> overrides, out Dictionary<ModelId, List<ModelId>> baseDecks)
	{
		overrides = new Dictionary<ModelId, CardOverride>();
		baseDecks = new Dictionary<ModelId, List<ModelId>>();
		presetName = GetStartupPresetName() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(presetName))
		{
			return false;
		}
		return TryLoadPreset(presetName, out overrides, out baseDecks);
	}

	private static string EnsurePresetDirectory()
	{
		string dir = ProjectSettings.GlobalizePath("user://card_editor/presets");
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

	private static Dictionary<string, List<string>> SerializeBaseDecks(IReadOnlyDictionary<ModelId, List<ModelId>>? baseDecks)
	{
		if (baseDecks == null || baseDecks.Count == 0)
		{
			return new Dictionary<string, List<string>>(StringComparer.Ordinal);
		}

		Dictionary<string, List<string>> serialized = new(StringComparer.Ordinal);
		foreach ((ModelId characterId, List<ModelId> cardIds) in baseDecks)
		{
			if (!CardEditorBaseDeckStore.IsSupportedCharacterId(characterId))
			{
				continue;
			}

			serialized[characterId.ToString()] = (cardIds ?? new List<ModelId>())
				.Where(cardId => cardId != null && cardId != ModelId.none && ModelDb.GetByIdOrNull<CardModel>(cardId) != null)
				.Select(cardId => cardId.ToString())
				.ToList();
		}

		return serialized;
	}

	private static Dictionary<ModelId, List<ModelId>> DeserializeBaseDecks(Dictionary<string, List<string>>? serialized)
	{
		Dictionary<ModelId, List<ModelId>> baseDecks = new();
		if (serialized == null)
		{
			return baseDecks;
		}

		foreach ((string rawCharacterId, List<string> rawCards) in serialized)
		{
			if (!TryParseModelId(rawCharacterId, out ModelId characterId) || !CardEditorBaseDeckStore.IsSupportedCharacterId(characterId))
			{
				continue;
			}

			List<ModelId> cards = new();
			foreach (string rawCardId in rawCards ?? new List<string>())
			{
				if (TryParseModelId(rawCardId, out ModelId cardId) && cardId != ModelId.none && ModelDb.GetByIdOrNull<CardModel>(cardId) != null)
				{
					cards.Add(cardId);
				}
			}

			baseDecks[characterId] = cards;
		}

		return baseDecks;
	}

	private static void ImportDefinitionsFromPreset(PresetFileDto data)
	{
		try
		{
			List<CardEditorCustomKeywordDefinition> keywords = new();
			foreach (CustomKeywordDefinitionDto dto in data.KeywordDefinitions ?? new List<CustomKeywordDefinitionDto>())
			{
				CardEditorCustomKeywordDefinition? def = dto.ToDefinitionSafe();
				if (def != null)
				{
					keywords.Add(def);
				}
			}

			List<CardEditorCustomStatusDefinition> statuses = new();
			foreach (CustomStatusDefinitionDto dto in data.StatusDefinitions ?? new List<CustomStatusDefinitionDto>())
			{
				CardEditorCustomStatusDefinition? def = dto.ToDefinitionSafe();
				if (def != null)
				{
					statuses.Add(def);
				}
			}

			if (keywords.Count > 0 || statuses.Count > 0)
			{
				CardEditorDefinitionStore.ImportDefinitions(keywords, statuses);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed importing preset definition library: {ex}");
		}
	}

	private static List<CardExtraEffect> DeserializeDefinitionEffects(IEnumerable<CardExtraEffectDto?>? dtos)
	{
		if (dtos == null)
		{
			return new List<CardExtraEffect>();
		}

		List<CardExtraEffect> effects = new();
		foreach (CardExtraEffectDto? dto in dtos)
		{
			if (dto == null || !dto.TryToEffect(out CardExtraEffect effect))
			{
				continue;
			}

			effects.Add(effect);
		}

		return effects;
	}

	private static List<CardExtraEffectDto?> SerializeDefinitionEffects(IEnumerable<CardExtraEffect>? effects)
	{
		return (effects ?? Array.Empty<CardExtraEffect>())
			.Where(effect => effect != null)
			.Select(effect => (CardExtraEffectDto?)CardExtraEffectDto.FromEffect(effect))
			.ToList();
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private static CardUpgradeOverride BuildUpgradeOverrideFromLegacyAbsolute(ModelId cardId, CardOverride baseOverride, CardOverride desiredUpgradedAbsolute)
	{
		bool prevSuppressAll = CardEditorOverrides.SuppressAllOverrides;
		bool prevSuppressUpgrade = CardEditorOverrides.SuppressUpgradeOverrides;

		try
		{
			CardEditorOverrides.SuppressAllOverrides = true;

			CardModel canonical = ModelDb.GetById<CardModel>(cardId);

			CardModel baseCard = canonical.ToMutable();
			CardEditorOverrides.ApplyOverrideToCard(baseCard, baseOverride);

			int baseEnergyCost = baseCard.EnergyCost.GetWithModifiers(CostModifiers.None);
			int baseStarCost = baseCard.BaseStarCost;
			int baseReplayCount = baseCard.BaseReplayCount;
			Dictionary<string, decimal> baseVars = baseCard.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);

			CardModel vanillaUpgraded = canonical.ToMutable();
			CardEditorOverrides.ApplyOverrideToCard(vanillaUpgraded, baseOverride);

			try
			{
				CardEditorOverrides.SuppressUpgradeOverrides = true;
				if (vanillaUpgraded.IsUpgradable)
				{
					vanillaUpgraded.UpgradeInternal();
				}
				vanillaUpgraded.FinalizeUpgradeInternal();
			}
			catch
			{
			}
			finally
			{
				CardEditorOverrides.SuppressUpgradeOverrides = prevSuppressUpgrade;
			}

			int vanillaEnergyCost = vanillaUpgraded.EnergyCost.GetWithModifiers(CostModifiers.None);
			int vanillaStarCost = vanillaUpgraded.BaseStarCost;
			int vanillaReplayCount = vanillaUpgraded.BaseReplayCount;
			ModelId vanillaEnchantmentId = vanillaUpgraded.Enchantment?.Id ?? ModelId.none;
			int vanillaEnchantmentAmount = Math.Max(1, vanillaUpgraded.Enchantment?.Amount ?? 1);
			ModelId vanillaAfflictionId = vanillaUpgraded.Affliction?.Id ?? ModelId.none;
			int vanillaAfflictionAmount = Math.Max(1, vanillaUpgraded.Affliction?.Amount ?? 1);
			Dictionary<string, decimal> vanillaVars = vanillaUpgraded.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);
			HashSet<CardKeyword> vanillaKeywords = new HashSet<CardKeyword>(vanillaUpgraded.Keywords);

			CardUpgradeOverride upgrade = new CardUpgradeOverride();
			if (desiredUpgradedAbsolute.ModifiedBaseTextEnabled == true)
			{
				upgrade.ModifiedBaseTextEnabled = true;
				upgrade.ModifiedBaseText = desiredUpgradedAbsolute.ModifiedBaseText ?? string.Empty;
			}

			if (!baseCard.EnergyCost.CostsX)
			{
				int desiredEnergy = desiredUpgradedAbsolute.EnergyCost ?? vanillaEnergyCost;
				int desiredDelta = desiredEnergy - baseEnergyCost;
				int vanillaDelta = vanillaEnergyCost - baseEnergyCost;
				if (desiredDelta != vanillaDelta)
				{
					upgrade.EnergyCostDelta = desiredDelta;
				}
			}

			if (!baseCard.HasStarCostX)
			{
				int desiredStar = desiredUpgradedAbsolute.StarCost ?? vanillaStarCost;
				if (desiredStar >= -1)
				{
					int desiredDelta = desiredStar - baseStarCost;
					int vanillaDelta = vanillaStarCost - baseStarCost;
					if (desiredDelta != vanillaDelta)
					{
						upgrade.StarCostDelta = desiredDelta;
					}
				}
			}

			{
				int desiredReplay = desiredUpgradedAbsolute.ReplayCount ?? vanillaReplayCount;
				int desiredDelta = desiredReplay - baseReplayCount;
				int vanillaDelta = vanillaReplayCount - baseReplayCount;
				if (desiredDelta != vanillaDelta)
				{
					upgrade.ReplayCountDelta = desiredDelta;
				}
			}

			if (desiredUpgradedAbsolute.EnchantmentId != null)
			{
				ModelId desiredEnchantmentId = desiredUpgradedAbsolute.EnchantmentId;
				int desiredEnchantmentAmount = Math.Max(1, desiredUpgradedAbsolute.EnchantmentAmount ?? vanillaEnchantmentAmount);
				if (desiredEnchantmentId != vanillaEnchantmentId
					|| (desiredEnchantmentId != ModelId.none && desiredEnchantmentAmount != vanillaEnchantmentAmount))
				{
					upgrade.EnchantmentId = desiredEnchantmentId;
					if (desiredEnchantmentId != ModelId.none)
					{
						upgrade.EnchantmentAmount = desiredEnchantmentAmount;
					}
				}
			}

			if (desiredUpgradedAbsolute.AfflictionId != null)
			{
				ModelId desiredAfflictionId = desiredUpgradedAbsolute.AfflictionId;
				int desiredAfflictionAmount = Math.Max(1, desiredUpgradedAbsolute.AfflictionAmount ?? vanillaAfflictionAmount);
				if (desiredAfflictionId != vanillaAfflictionId
					|| (desiredAfflictionId != ModelId.none && desiredAfflictionAmount != vanillaAfflictionAmount))
				{
					upgrade.AfflictionId = desiredAfflictionId;
					if (desiredAfflictionId != ModelId.none)
					{
						upgrade.AfflictionAmount = desiredAfflictionAmount;
					}
				}
			}

			if (baseVars.Count > 0)
			{
				Dictionary<string, decimal> deltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
				foreach ((string key, decimal baseValue) in baseVars)
				{
					if (!vanillaVars.TryGetValue(key, out decimal vanillaValue))
					{
						continue;
					}

					decimal desiredValue = vanillaValue;
					if (desiredUpgradedAbsolute.DynamicVarBaseValues != null && desiredUpgradedAbsolute.DynamicVarBaseValues.TryGetValue(key, out decimal overridden))
					{
						desiredValue = overridden;
					}

					decimal desiredDelta = desiredValue - baseValue;
					decimal vanillaDelta = vanillaValue - baseValue;
					if (desiredDelta != vanillaDelta)
					{
						deltas[key] = desiredDelta;
					}
				}
				if (deltas.Count > 0)
				{
					upgrade.DynamicVarDeltas = deltas;
				}
			}

			HashSet<CardKeyword> desiredKeywords = desiredUpgradedAbsolute.Keywords ?? vanillaKeywords;
			HashSet<CardKeyword> toRemove = new HashSet<CardKeyword>(vanillaKeywords);
			toRemove.ExceptWith(desiredKeywords);
			HashSet<CardKeyword> toAdd = new HashSet<CardKeyword>(desiredKeywords);
			toAdd.ExceptWith(vanillaKeywords);

			if (toRemove.Count > 0)
			{
				upgrade.KeywordsToRemove = toRemove;
			}
			if (toAdd.Count > 0)
			{
				upgrade.KeywordsToAdd = toAdd;
			}

			if (desiredUpgradedAbsolute.ExtraEffects != null && desiredUpgradedAbsolute.ExtraEffects.Count > 0)
			{
				List<CardExtraEffect> desiredEffects = desiredUpgradedAbsolute.ExtraEffects
					.Where(e => e != null && CardEditorExtraEffects.IsValidEffectAmount(e.Kind, e.Amount))
					.Select(e => CardEditorExtraEffects.CloneEffect(e!))
					.ToList();

				if (desiredEffects.Count > 0)
				{
					List<CardExtraEffect>? baseEffects = baseOverride.ExtraEffects;
					if (baseEffects == null || baseEffects.Count == 0)
					{
						upgrade.ExtraEffects = desiredEffects;
					}
					else
					{
						int baseCount = baseEffects.Count;
						List<CardExtraEffect> converted = new List<CardExtraEffect>(baseCount);
						for (int i = 0; i < baseCount; i++)
						{
							converted.Add(null!);
						}

						List<CardExtraEffect> append = new List<CardExtraEffect>();

						for (int i = 0; i < desiredUpgradedAbsolute.ExtraEffects.Count; i++)
						{
							CardExtraEffect? desiredEffect = desiredUpgradedAbsolute.ExtraEffects[i];
							if (desiredEffect == null || !CardEditorExtraEffects.IsValidEffectAmount(desiredEffect.Kind, desiredEffect.Amount))
							{
								continue;
							}

							CardExtraEffect clone = CardEditorExtraEffects.CloneEffect(desiredEffect);

							if (i < baseCount && baseEffects[i] != null && CardEditorExtraEffects.EffectsMatchExceptAmount(baseEffects[i], clone))
							{
								converted[i] = clone;
							}
							else
							{
								append.Add(clone);
							}
						}

						if (append.Count > 0)
						{
							converted.AddRange(append);
						}

						if (converted.Any(e => e != null))
						{
							upgrade.ExtraEffects = converted;
						}
					}
				}
			}

			return upgrade;
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = prevSuppressAll;
			CardEditorOverrides.SuppressUpgradeOverrides = prevSuppressUpgrade;
		}
	}

	private sealed class PresetFileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public DateTime SavedAtUtc { get; set; }
		public Dictionary<string, CardOverrideDto> Overrides { get; set; } = new Dictionary<string, CardOverrideDto>(StringComparer.Ordinal);
		public Dictionary<string, List<string>> BaseDecks { get; set; } = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		public List<CustomKeywordDefinitionDto> KeywordDefinitions { get; set; } = new();
		public List<CustomStatusDefinitionDto> StatusDefinitions { get; set; } = new();
	}

	private sealed class CustomKeywordDefinitionDto
	{
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public List<CardExtraEffectDto?>? Effects { get; set; }

		public static CustomKeywordDefinitionDto FromDefinition(CardEditorCustomKeywordDefinition def)
		{
			return new CustomKeywordDefinitionDto
			{
				Id = def.Id,
				Name = def.Name,
				Description = def.Description,
				Effects = SerializeDefinitionEffects(def.Effects)
			};
		}

		public CardEditorCustomKeywordDefinition? ToDefinitionSafe()
		{
			string name = string.IsNullOrWhiteSpace(Name) ? string.Empty : Name.Trim();
			string? id = string.IsNullOrWhiteSpace(Id) ? CardEditorDefinitionStore.BuildKeywordId(name) : Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			return new CardEditorCustomKeywordDefinition
			{
				Id = id,
				Name = name,
				Description = string.IsNullOrWhiteSpace(Description) ? string.Empty : Description.Trim(),
				Effects = DeserializeDefinitionEffects(Effects)
			};
		}
	}

	private sealed class CustomStatusDefinitionDto
	{
		public string? Id { get; set; }
		public string? Name { get; set; }
		public string? Description { get; set; }
		public string? Type { get; set; }
		public string? IconMode { get; set; }
		public string? IconPowerId { get; set; }
		public string? CustomPackedIconPath { get; set; }
		public string? CustomBigIconPath { get; set; }
		public List<CardExtraEffectDto?>? BehaviorEffects { get; set; }

		public static CustomStatusDefinitionDto FromDefinition(CardEditorCustomStatusDefinition def)
		{
			return new CustomStatusDefinitionDto
			{
				Id = def.Id,
				Name = def.Name,
				Description = def.Description,
				Type = def.Type.ToString(),
				IconMode = def.IconMode.ToString(),
				IconPowerId = def.IconPowerId,
				CustomPackedIconPath = def.CustomPackedIconPath,
				CustomBigIconPath = def.CustomBigIconPath,
				BehaviorEffects = SerializeDefinitionEffects(def.BehaviorEffects)
			};
		}

		public CardEditorCustomStatusDefinition? ToDefinitionSafe()
		{
			string name = string.IsNullOrWhiteSpace(Name) ? string.Empty : Name.Trim();
			string? id = string.IsNullOrWhiteSpace(Id) ? CardEditorCustomStatusRegistry.BuildId(name) : Id.Trim();
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			PowerType type = PowerType.Buff;
			if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse(Type, out PowerType parsedType))
			{
				type = parsedType;
			}

			CardExtraEffectStatusIconMode iconMode = CardExtraEffectStatusIconMode.Auto;
			if (!string.IsNullOrWhiteSpace(IconMode) && Enum.TryParse(IconMode, out CardExtraEffectStatusIconMode parsedIconMode))
			{
				iconMode = parsedIconMode;
			}

			return new CardEditorCustomStatusDefinition
			{
				Id = id,
				Name = name,
				Description = string.IsNullOrWhiteSpace(Description) ? string.Empty : Description.Trim(),
				SourceCardId = null,
				BehaviorEffects = DeserializeDefinitionEffects(BehaviorEffects),
				Type = type,
				IconMode = iconMode,
				IconPowerId = string.IsNullOrWhiteSpace(IconPowerId) ? null : IconPowerId.Trim(),
				CustomPackedIconPath = string.IsNullOrWhiteSpace(CustomPackedIconPath) ? null : CustomPackedIconPath.Trim(),
				CustomBigIconPath = string.IsNullOrWhiteSpace(CustomBigIconPath) ? null : CustomBigIconPath.Trim()
			};
		}
	}

	private sealed class PresetSettingsDto
	{
		public string? StartupPresetName { get; set; }
	}

	internal sealed class CardOverrideDto
	{
		public string? PoolTitle { get; set; }
		public string? Rarity { get; set; }
		public bool? Enabled { get; set; }
		public bool? CanBeGeneratedInCombat { get; set; }
		public bool? CanBeGeneratedByModifiers { get; set; }
		public string? TitleOverride { get; set; }
		public bool? ModifiedBaseTextEnabled { get; set; }
		public string? ModifiedBaseText { get; set; }
		public bool? EndlessUpgrades { get; set; }
		public bool? FullArt { get; set; }
		public string? Finish { get; set; }
		public Dictionary<string, decimal>? FinishParams { get; set; }
		public string? CustomFinishId { get; set; }
		public Dictionary<string, string>? CustomFinishParams { get; set; }
		public string? BorderFinish { get; set; }
		public Dictionary<string, decimal>? BorderFinishParams { get; set; }
		public decimal? PortraitOffsetX { get; set; }
		public decimal? PortraitOffsetY { get; set; }
		public decimal? PortraitZoom { get; set; }
		public string? CardType { get; set; }
		public string? TargetType { get; set; }
		public int? EnergyCost { get; set; }
		public bool? EnergyCostX { get; set; }
		public int? StarCost { get; set; }
		public bool? StarCostX { get; set; }
		public int? ReplayCount { get; set; }
		public int? DrawCostReduction { get; set; }
		public int? HandDiscardCount { get; set; }
		public List<string>? Keywords { get; set; }
		public List<string>? TagsToAdd { get; set; }
		public List<string>? TagsToRemove { get; set; }
		public List<string>? CustomTags { get; set; }
		public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
		public Dictionary<string, decimal>? PowerAmounts { get; set; }
		public string? SlyGrantDuration { get; set; }
		public int? SlyGrantTurns { get; set; }
		public string? TemporaryStrengthDuration { get; set; }
		public int? TemporaryStrengthTurns { get; set; }
		public string? TemporaryDexterityDuration { get; set; }
		public int? TemporaryDexterityTurns { get; set; }
		public string? TemporaryFocusDuration { get; set; }
		public int? TemporaryFocusTurns { get; set; }
		public string? EnchantmentId { get; set; }
		public int? EnchantmentAmount { get; set; }
		public string? AfflictionId { get; set; }
		public int? AfflictionAmount { get; set; }
		public string? PortraitSourceCardId { get; set; }
		public string? CustomPortraitFile { get; set; }
		public string? CosmeticStylePreset { get; set; }
		public string? CosmeticAnimationPreset { get; set; }
		public string? CosmeticVfxPreset { get; set; }
		public string? CosmeticVfxAttach { get; set; }
		public bool? CosmeticPlayAttackerAnim { get; set; }
		public bool? HideCosmeticCostOrb { get; set; }
		public bool? HideCosmeticCostNumber { get; set; }
		public bool? HideCosmeticNameBanner { get; set; }
		public bool? HideCosmeticNameText { get; set; }
		public bool? HideCosmeticTypeBadge { get; set; }
		public bool? HideCosmeticTextBackground { get; set; }
		public bool? HideCosmeticBodyText { get; set; }
		public bool? HideCosmeticAncientInnerBorder { get; set; }
		public List<CardExtraEffectDto?>? ExtraEffects { get; set; }
		public CardUpgradeOverrideDto? Upgrade { get; set; }
		public CardOverrideDto? Upgraded { get; set; } // legacy (v2)

		public static CardOverrideDto FromOverride(CardOverride source)
		{
			return new CardOverrideDto
			{
				PoolTitle = source.PoolTitle,
				Rarity = source.Rarity?.ToString(),
				Enabled = source.Enabled,
				CanBeGeneratedInCombat = source.CanBeGeneratedInCombat,
				CanBeGeneratedByModifiers = source.CanBeGeneratedByModifiers,
				TitleOverride = source.TitleOverride,
				ModifiedBaseTextEnabled = source.ModifiedBaseTextEnabled,
				ModifiedBaseText = source.ModifiedBaseText,
				EndlessUpgrades = source.EndlessUpgrades,
				FullArt = source.FullArt,
				Finish = source.Finish?.ToString(),
				FinishParams = source.FinishParams != null && source.FinishParams.Count > 0
					? source.FinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
				CustomFinishId = source.CustomFinishId,
				CustomFinishParams = source.CustomFinishParams != null && source.CustomFinishParams.Count > 0
					? new Dictionary<string, string>(source.CustomFinishParams, StringComparer.Ordinal)
					: null,
				BorderFinish = source.BorderFinish?.ToString(),
				BorderFinishParams = source.BorderFinishParams != null && source.BorderFinishParams.Count > 0
					? source.BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
				PortraitOffsetX = source.PortraitOffsetX.HasValue ? (decimal)source.PortraitOffsetX.Value : null,
				PortraitOffsetY = source.PortraitOffsetY.HasValue ? (decimal)source.PortraitOffsetY.Value : null,
				PortraitZoom = source.PortraitZoom.HasValue ? (decimal)source.PortraitZoom.Value : null,
				CardType = source.CardType?.ToString(),
				TargetType = source.TargetType?.ToString(),
				EnergyCost = source.EnergyCost,
				EnergyCostX = source.EnergyCostX,
				StarCost = source.StarCost,
				StarCostX = source.StarCostX,
				ReplayCount = source.ReplayCount,
				DrawCostReduction = source.DrawCostReduction,
				HandDiscardCount = source.HandDiscardCount,
				Keywords = source.Keywords?.Select(k => k.ToString()).ToList(),
				TagsToAdd = source.TagsToAdd?.Where(t => t != CardTag.None).Select(t => t.ToString()).ToList(),
				TagsToRemove = source.TagsToRemove?.Where(t => t != CardTag.None).Select(t => t.ToString()).ToList(),
				CustomTags = source.CustomTags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
				DynamicVarBaseValues = source.DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(source.DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				PowerAmounts = source.PowerAmounts != null
					? source.PowerAmounts.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value, StringComparer.Ordinal)
					: null,
				SlyGrantDuration = source.SlyGrantDuration?.ToString(),
				SlyGrantTurns = source.SlyGrantTurns,
				TemporaryStrengthDuration = source.TemporaryStrengthDuration?.ToString(),
				TemporaryStrengthTurns = source.TemporaryStrengthTurns,
				TemporaryDexterityDuration = source.TemporaryDexterityDuration?.ToString(),
				TemporaryDexterityTurns = source.TemporaryDexterityTurns,
				TemporaryFocusDuration = source.TemporaryFocusDuration?.ToString(),
				TemporaryFocusTurns = source.TemporaryFocusTurns,
				EnchantmentId = source.EnchantmentId?.ToString(),
				EnchantmentAmount = source.EnchantmentAmount,
				AfflictionId = source.AfflictionId?.ToString(),
				AfflictionAmount = source.AfflictionAmount,
				PortraitSourceCardId = source.PortraitSourceCardId?.ToString(),
				CustomPortraitFile = source.CustomPortraitFile,
				CosmeticStylePreset = source.CosmeticStylePreset?.ToString(),
				CosmeticAnimationPreset = source.CosmeticAnimationPreset?.ToString(),
				CosmeticVfxPreset = source.CosmeticVfxPreset?.ToString(),
				CosmeticVfxAttach = source.CosmeticVfxAttach?.ToString(),
				CosmeticPlayAttackerAnim = source.CosmeticPlayAttackerAnim,
				HideCosmeticCostOrb = source.HideCosmeticCostOrb,
				HideCosmeticCostNumber = source.HideCosmeticCostNumber,
				HideCosmeticNameBanner = source.HideCosmeticNameBanner,
				HideCosmeticNameText = source.HideCosmeticNameText,
				HideCosmeticTypeBadge = source.HideCosmeticTypeBadge,
				HideCosmeticTextBackground = source.HideCosmeticTextBackground,
				HideCosmeticBodyText = source.HideCosmeticBodyText,
				HideCosmeticAncientInnerBorder = source.HideCosmeticAncientInnerBorder,
				ExtraEffects = source.ExtraEffects != null
					? source.ExtraEffects.Select(e => e != null ? CardExtraEffectDto.FromEffect(e) : null).ToList()
					: null,
				Upgrade = source.Upgrade != null && !source.Upgrade.IsEmpty()
					? CardUpgradeOverrideDto.FromUpgrade(source.Upgrade)
					: null,
				Upgraded = null
			};
		}

		public CardOverride ToOverrideSafe(ModelId cardId, int fileVersion)
		{
			CardOverride result = ToBaseOverrideSafe();

			if (fileVersion >= 3)
			{
				if (Upgrade != null)
				{
					CardUpgradeOverride upgrade = Upgrade.ToUpgradeSafe(fileVersion);
					if (!upgrade.IsEmpty())
					{
						result.Upgrade = upgrade;
					}
				}
				return result;
			}

			// Legacy v2: convert absolute upgraded overrides into additive upgrade deltas.
			if (fileVersion == 2 && Upgraded != null)
			{
				CardOverride desiredUpgradedAbsolute = Upgraded.ToBaseOverrideSafe();
				CardUpgradeOverride upgrade = BuildUpgradeOverrideFromLegacyAbsolute(cardId, result, desiredUpgradedAbsolute);
				if (!upgrade.IsEmpty())
				{
					result.Upgrade = upgrade;
				}
			}

			return result;
		}

		private CardOverride ToBaseOverrideSafe()
		{
			CardOverride result = new CardOverride
			{
				Enabled = Enabled,
				CanBeGeneratedInCombat = CanBeGeneratedInCombat,
				CanBeGeneratedByModifiers = CanBeGeneratedByModifiers,
				TitleOverride = string.IsNullOrWhiteSpace(TitleOverride) ? null : TitleOverride.Trim(),
				ModifiedBaseTextEnabled = ModifiedBaseTextEnabled,
				ModifiedBaseText = ModifiedBaseText != null ? ModifiedBaseText : null,
				EndlessUpgrades = EndlessUpgrades,
				FullArt = FullArt,
				CustomFinishId = string.IsNullOrWhiteSpace(CustomFinishId) ? null : CustomFinishId.Trim(),
				CustomFinishParams = CustomFinishParams != null && CustomFinishParams.Count > 0
					? new Dictionary<string, string>(CustomFinishParams, StringComparer.Ordinal)
					: null,
				PortraitOffsetX = PortraitOffsetX.HasValue ? (float)PortraitOffsetX.Value : null,
				PortraitOffsetY = PortraitOffsetY.HasValue ? (float)PortraitOffsetY.Value : null,
				PortraitZoom = PortraitZoom.HasValue ? (float)PortraitZoom.Value : null,
				EnergyCost = EnergyCost,
				EnergyCostX = EnergyCostX,
				StarCost = StarCost,
				StarCostX = StarCostX,
				ReplayCount = ReplayCount,
				DrawCostReduction = DrawCostReduction,
				HandDiscardCount = HandDiscardCount,
				EnchantmentAmount = EnchantmentAmount,
				AfflictionAmount = AfflictionAmount,
				SlyGrantTurns = SlyGrantTurns,
				TemporaryStrengthTurns = TemporaryStrengthTurns,
				TemporaryDexterityTurns = TemporaryDexterityTurns,
				TemporaryFocusTurns = TemporaryFocusTurns,
				HideCosmeticCostOrb = HideCosmeticCostOrb,
				HideCosmeticCostNumber = HideCosmeticCostNumber,
				HideCosmeticNameBanner = HideCosmeticNameBanner,
				HideCosmeticNameText = HideCosmeticNameText,
				HideCosmeticTypeBadge = HideCosmeticTypeBadge,
				HideCosmeticTextBackground = HideCosmeticTextBackground,
				HideCosmeticBodyText = HideCosmeticBodyText,
				HideCosmeticAncientInnerBorder = HideCosmeticAncientInnerBorder
			};

			if (!string.IsNullOrWhiteSpace(Finish)
				&& Enum.TryParse(Finish, ignoreCase: true, out CardEditorVisualFinish parsedFinish)
				&& parsedFinish != CardEditorVisualFinish.None)
			{
				result.Finish = parsedFinish;
			}

			if (FinishParams != null && FinishParams.Count > 0)
			{
				result.FinishParams = FinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
			}

			if (!string.IsNullOrWhiteSpace(BorderFinish)
				&& Enum.TryParse(BorderFinish, ignoreCase: true, out CardEditorVisualFinish parsedBorderFinish)
				&& parsedBorderFinish != CardEditorVisualFinish.None)
			{
				result.BorderFinish = parsedBorderFinish;
			}

			if (BorderFinishParams != null && BorderFinishParams.Count > 0)
			{
				result.BorderFinishParams = BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
			}

			if (!string.IsNullOrWhiteSpace(PoolTitle))
			{
				result.PoolTitle = PoolTitle.Trim();
			}

			if (!string.IsNullOrWhiteSpace(Rarity)
				&& Enum.TryParse(Rarity, ignoreCase: true, out CardRarity parsedRarity)
				&& parsedRarity != CardRarity.None)
			{
				result.Rarity = parsedRarity;
			}

			if (!string.IsNullOrWhiteSpace(CardType)
				&& Enum.TryParse(CardType, out CardType parsedType)
				&& parsedType != MegaCrit.Sts2.Core.Entities.Cards.CardType.None)
			{
				result.CardType = parsedType;
			}

			if (!string.IsNullOrWhiteSpace(TargetType)
				&& Enum.TryParse(TargetType, out TargetType parsedTargetType)
				&& parsedTargetType != MegaCrit.Sts2.Core.Entities.Cards.TargetType.None)
			{
				result.TargetType = parsedTargetType;
			}

			if (!string.IsNullOrWhiteSpace(CosmeticAnimationPreset)
				&& Enum.TryParse(CosmeticAnimationPreset, ignoreCase: true, out CardEditorCosmeticAnimationPreset parsedAnimationPreset)
				&& parsedAnimationPreset != CardEditorCosmeticAnimationPreset.None)
			{
				result.CosmeticAnimationPreset = parsedAnimationPreset;
			}

			if (!string.IsNullOrWhiteSpace(CosmeticStylePreset)
				&& Enum.TryParse(CosmeticStylePreset, ignoreCase: true, out CardEditorCosmeticStylePreset parsedStylePreset)
				&& parsedStylePreset != CardEditorCosmeticStylePreset.None)
			{
				result.CosmeticStylePreset = parsedStylePreset;
			}

			if (!string.IsNullOrWhiteSpace(CosmeticVfxPreset)
				&& Enum.TryParse(CosmeticVfxPreset, ignoreCase: true, out CardEditorCosmeticVfxPreset parsedPreset)
				&& parsedPreset != CardEditorCosmeticVfxPreset.None)
			{
				result.CosmeticVfxPreset = parsedPreset;
			}

			if (result.CosmeticVfxPreset != null
				&& !string.IsNullOrWhiteSpace(CosmeticVfxAttach)
				&& Enum.TryParse(CosmeticVfxAttach, ignoreCase: true, out CardEditorCosmeticAttach parsedAttach))
			{
				result.CosmeticVfxAttach = parsedAttach;
			}

			if (CosmeticPlayAttackerAnim == true)
			{
				result.CosmeticPlayAttackerAnim = true;
			}

			if (Keywords != null)
			{
				HashSet<CardKeyword> parsed = new HashSet<CardKeyword>();
				foreach (string keyword in Keywords)
				{
					if (!string.IsNullOrWhiteSpace(keyword)
						&& Enum.TryParse(keyword, out CardKeyword parsedKeyword)
						&& parsedKeyword != CardKeyword.None)
					{
						parsed.Add(parsedKeyword);
					}
				}

				if (Keywords.Count == 0 || parsed.Count > 0)
				{
					result.Keywords = parsed;
				}
			}

			if (TagsToAdd != null)
			{
				HashSet<CardTag> parsed = new HashSet<CardTag>();
				foreach (string tag in TagsToAdd)
				{
					if (!string.IsNullOrWhiteSpace(tag)
						&& Enum.TryParse(tag, out CardTag parsedTag)
						&& parsedTag != CardTag.None)
					{
						parsed.Add(parsedTag);
					}
				}
				if (TagsToAdd.Count == 0 || parsed.Count > 0)
				{
					result.TagsToAdd = parsed;
				}
			}

			if (TagsToRemove != null)
			{
				HashSet<CardTag> parsed = new HashSet<CardTag>();
				foreach (string tag in TagsToRemove)
				{
					if (!string.IsNullOrWhiteSpace(tag)
						&& Enum.TryParse(tag, out CardTag parsedTag)
						&& parsedTag != CardTag.None)
					{
						parsed.Add(parsedTag);
					}
				}
				if (TagsToRemove.Count == 0 || parsed.Count > 0)
				{
					result.TagsToRemove = parsed;
				}
			}

			if (CustomTags != null)
			{
				HashSet<string> parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string tag in CustomTags)
				{
					string trimmed = tag?.Trim() ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(trimmed))
					{
						parsed.Add(trimmed);
					}
				}
				if (CustomTags.Count == 0 || parsed.Count > 0)
				{
					result.CustomTags = parsed;
				}
			}

			if (DynamicVarBaseValues != null && DynamicVarBaseValues.Count > 0)
			{
				result.DynamicVarBaseValues = new Dictionary<string, decimal>(DynamicVarBaseValues, StringComparer.Ordinal);
			}

			if (PowerAmounts != null && PowerAmounts.Count > 0)
			{
				Dictionary<ModelId, decimal> parsed = new Dictionary<ModelId, decimal>();
				foreach ((string idString, decimal amount) in PowerAmounts)
				{
					if (!TryParseModelId(idString, out ModelId powerId))
					{
						continue;
					}
					if (ModelDb.GetByIdOrNull<PowerModel>(powerId) == null)
					{
						continue;
					}
					parsed[powerId] = amount;
				}
				if (parsed.Count > 0)
				{
					result.PowerAmounts = parsed;
				}
			}

			if (!string.IsNullOrWhiteSpace(SlyGrantDuration) && Enum.TryParse(SlyGrantDuration, out CardKeywordGrantDuration parsedSlyDuration))
			{
				result.SlyGrantDuration = parsedSlyDuration;
			}

			if (!string.IsNullOrWhiteSpace(TemporaryStrengthDuration) && Enum.TryParse(TemporaryStrengthDuration, out CardKeywordGrantDuration parsedTempStrDuration))
			{
				result.TemporaryStrengthDuration = parsedTempStrDuration;
			}

			if (!string.IsNullOrWhiteSpace(TemporaryDexterityDuration) && Enum.TryParse(TemporaryDexterityDuration, out CardKeywordGrantDuration parsedTempDexDuration))
			{
				result.TemporaryDexterityDuration = parsedTempDexDuration;
			}

			if (!string.IsNullOrWhiteSpace(TemporaryFocusDuration) && Enum.TryParse(TemporaryFocusDuration, out CardKeywordGrantDuration parsedTempFocusDuration))
			{
				result.TemporaryFocusDuration = parsedTempFocusDuration;
			}

			if (!string.IsNullOrWhiteSpace(EnchantmentId) && TryParseModelId(EnchantmentId, out ModelId enchantmentId))
			{
				if (enchantmentId == ModelId.none || ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId) != null)
				{
					result.EnchantmentId = enchantmentId;
				}
			}

			if (!string.IsNullOrWhiteSpace(AfflictionId) && TryParseModelId(AfflictionId, out ModelId afflictionId))
			{
				if (afflictionId == ModelId.none || ModelDb.GetByIdOrNull<AfflictionModel>(afflictionId) != null)
				{
					result.AfflictionId = afflictionId;
				}
			}

			if (!string.IsNullOrWhiteSpace(PortraitSourceCardId) && TryParseModelId(PortraitSourceCardId, out ModelId portraitSourceId))
			{
				if (portraitSourceId == ModelId.none || ModelDb.GetByIdOrNull<CardModel>(portraitSourceId) != null)
				{
					result.PortraitSourceCardId = portraitSourceId;
				}
			}

			if (!string.IsNullOrWhiteSpace(CustomPortraitFile))
			{
				result.CustomPortraitFile = CustomPortraitFile.Trim();
			}

			if (ExtraEffects != null && ExtraEffects.Count > 0)
			{
				List<CardExtraEffect> parsed = new List<CardExtraEffect>();
				foreach (CardExtraEffectDto? dto in ExtraEffects)
				{
					if (dto == null || !dto.TryToEffect(out CardExtraEffect effect))
					{
						continue;
					}
					if (!effect.AmountIsX && !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
					{
						continue;
					}
					parsed.Add(effect);
				}
				if (parsed.Count > 0)
				{
					result.ExtraEffects = parsed;
				}
			}

			return result;
		}
	}

	internal sealed class CardUpgradeOverrideDto
	{
		public bool? ModifiedBaseTextEnabled { get; set; }
		public string? ModifiedBaseText { get; set; }
		public int? EnergyCostDelta { get; set; }
		public int? StarCostDelta { get; set; }
		public int? ReplayCountDelta { get; set; }
		public string? EnchantmentId { get; set; }
		public int? EnchantmentAmount { get; set; }
		public string? AfflictionId { get; set; }
		public int? AfflictionAmount { get; set; }
		public bool ExtraEffectNumericFieldsAreDeltas { get; set; }
		public List<string>? KeywordsToAdd { get; set; }
		public List<string>? KeywordsToRemove { get; set; }
		public Dictionary<string, decimal>? DynamicVarDeltas { get; set; }
		public List<CardExtraEffectDto?>? ExtraEffects { get; set; }

		public static CardUpgradeOverrideDto FromUpgrade(CardUpgradeOverride source)
		{
			return new CardUpgradeOverrideDto
			{
				ModifiedBaseTextEnabled = source.ModifiedBaseTextEnabled,
				ModifiedBaseText = source.ModifiedBaseText,
				EnergyCostDelta = source.EnergyCostDelta,
				StarCostDelta = source.StarCostDelta,
				ReplayCountDelta = source.ReplayCountDelta,
				EnchantmentId = source.EnchantmentId?.ToString(),
				EnchantmentAmount = source.EnchantmentAmount,
				AfflictionId = source.AfflictionId?.ToString(),
				AfflictionAmount = source.AfflictionAmount,
				ExtraEffectNumericFieldsAreDeltas = source.ExtraEffectNumericFieldsAreDeltas,
				KeywordsToAdd = source.KeywordsToAdd?.Select(k => k.ToString()).ToList(),
				KeywordsToRemove = source.KeywordsToRemove?.Select(k => k.ToString()).ToList(),
				DynamicVarDeltas = source.DynamicVarDeltas != null
					? new Dictionary<string, decimal>(source.DynamicVarDeltas, StringComparer.Ordinal)
					: null,
				ExtraEffects = source.ExtraEffects != null
					? source.ExtraEffects.Select(e => e != null ? CardExtraEffectDto.FromEffect(e, numericFieldsAreDeltas: source.ExtraEffectNumericFieldsAreDeltas) : null).ToList()
					: null
			};
		}

		public CardUpgradeOverride ToUpgradeSafe(int fileVersion = CurrentVersion)
		{
			CardUpgradeOverride result = new CardUpgradeOverride
			{
				ModifiedBaseTextEnabled = ModifiedBaseTextEnabled,
				ModifiedBaseText = ModifiedBaseText != null ? ModifiedBaseText : null,
				EnergyCostDelta = EnergyCostDelta,
				StarCostDelta = StarCostDelta,
				ReplayCountDelta = ReplayCountDelta,
				ExtraEffectNumericFieldsAreDeltas = ExtraEffectNumericFieldsAreDeltas
			};

			if (!string.IsNullOrWhiteSpace(EnchantmentId) && TryParseModelId(EnchantmentId, out ModelId enchantmentId))
			{
				if (enchantmentId == ModelId.none || ModelDb.GetByIdOrNull<EnchantmentModel>(enchantmentId) != null)
				{
					result.EnchantmentId = enchantmentId;
					result.EnchantmentAmount = EnchantmentAmount;
				}
			}

			if (!string.IsNullOrWhiteSpace(AfflictionId) && TryParseModelId(AfflictionId, out ModelId afflictionId))
			{
				if (afflictionId == ModelId.none || ModelDb.GetByIdOrNull<AfflictionModel>(afflictionId) != null)
				{
					result.AfflictionId = afflictionId;
					result.AfflictionAmount = AfflictionAmount;
				}
			}

			if (KeywordsToAdd != null && KeywordsToAdd.Count > 0)
			{
				HashSet<CardKeyword> parsed = new HashSet<CardKeyword>();
				foreach (string keyword in KeywordsToAdd)
				{
					if (!string.IsNullOrWhiteSpace(keyword) && Enum.TryParse(keyword, out CardKeyword parsedKeyword) && parsedKeyword != CardKeyword.None)
					{
						parsed.Add(parsedKeyword);
					}
				}
				if (parsed.Count > 0)
				{
					result.KeywordsToAdd = parsed;
				}
			}

			if (KeywordsToRemove != null && KeywordsToRemove.Count > 0)
			{
				HashSet<CardKeyword> parsed = new HashSet<CardKeyword>();
				foreach (string keyword in KeywordsToRemove)
				{
					if (!string.IsNullOrWhiteSpace(keyword) && Enum.TryParse(keyword, out CardKeyword parsedKeyword) && parsedKeyword != CardKeyword.None)
					{
						parsed.Add(parsedKeyword);
					}
				}
				if (parsed.Count > 0)
				{
					result.KeywordsToRemove = parsed;
				}
			}

			if (DynamicVarDeltas != null && DynamicVarDeltas.Count > 0)
			{
				result.DynamicVarDeltas = new Dictionary<string, decimal>(DynamicVarDeltas, StringComparer.Ordinal);
			}

			if (ExtraEffects != null && ExtraEffects.Count > 0)
			{
				List<CardExtraEffect> parsed = new List<CardExtraEffect>();
				foreach (CardExtraEffectDto? dto in ExtraEffects)
				{
					if (dto == null)
					{
						parsed.Add(null!);
						continue;
					}
					if (!dto.TryToEffect(out CardExtraEffect effect, numericFieldsAreDeltas: ExtraEffectNumericFieldsAreDeltas))
					{
						parsed.Add(null!);
						continue;
					}
					if (fileVersion < 12)
					{
						NormalizeLegacyRepeatScaling(effect, dto, ExtraEffectNumericFieldsAreDeltas);
					}
					if (fileVersion < 13 && ExtraEffectNumericFieldsAreDeltas)
					{
						NormalizeLegacyDeltaDefaults(effect, dto);
					}
					parsed.Add(effect);
				}
				if (parsed.Any(e => e != null))
				{
					result.ExtraEffects = parsed;
				}
			}

			return result;
		}

		private static void NormalizeLegacyRepeatScaling(CardExtraEffect effect, CardExtraEffectDto dto, bool numericFieldsAreDeltas)
		{
			if (effect == null
				|| effect.ScaleMode != CardExtraEffectScaleMode.RepeatByCount
				|| dto.RepeatScalingExtraTimes != 0
				|| dto.HistoryScalingCountStep == 0
				|| dto.HistoryScalingCountStep == 1)
			{
				return;
			}

			effect.RepeatScalingExtraTimes = Math.Clamp(dto.HistoryScalingCountStep, numericFieldsAreDeltas ? -99 : 1, 99);
			effect.HistoryScalingCountStep = numericFieldsAreDeltas ? 0 : 1;
		}

		private static void NormalizeLegacyDeltaDefaults(CardExtraEffect effect, CardExtraEffectDto dto)
		{
			// Older versions could persist one-based UI defaults in upgrade delta rows. In delta mode those fields are additive,
			// so a stored default of 1 turns an unchanged "per 5" count into "per 6" after the upgrade is merged.
			if (dto.HistoryScalingCountStep == 1)
			{
				effect.HistoryScalingCountStep = 0;
			}

			if (dto.RepeatScalingExtraTimes == 1)
			{
				effect.RepeatScalingExtraTimes = 0;
			}

			if (dto.CountConditionAmount == 1)
			{
				effect.CountConditionAmount = 0;
			}

			if (dto.BranchCountConditionAmount == 1)
			{
				effect.BranchCountConditionAmount = 0;
			}
		}
	}

	internal sealed class CardExtraEffectDto
	{
		public string? Kind { get; set; }
		public string? Target { get; set; }
		public int Amount { get; set; }
		public bool AmountIsX { get; set; }
		public int AmountXPlus { get; set; }
		public string? AmountSourceMode { get; set; }
		public string? AmountSourceEffectId { get; set; }
		public decimal AmountSourceMultiplier { get; set; } = 1m;
		public string? ValueSourceMode { get; set; }
		public string? ValueSourceActor { get; set; }
		public string? ValueSourceAggregation { get; set; }
		public string? ValueSourceKind { get; set; }
		public string? ValueSourcePowerId { get; set; }
		public string? Trigger { get; set; }
		public string? PowerTriggerCountEvent { get; set; }
		public string? PowerTriggerEnemyStatus { get; set; }
		public string? PowerTriggerPowerId { get; set; }
		public string? TurnBoundary { get; set; }
		public string? TurnBoundarySide { get; set; }
		public string? TurnBoundaryCardLocation { get; set; }
		public string? Timing { get; set; }
		public int Turns { get; set; }
		public string? Duration { get; set; }
		public bool AsPower { get; set; }
		public string? TriggerCardPool { get; set; }
		public string? TriggerCardType { get; set; }
		public string? TriggerCardFilter { get; set; }
		public int TriggerEveryN { get; set; }
		public int TriggerMaxFires { get; set; }
		public int TriggerMaxTurns { get; set; }
		public bool AutoPlayAllowSelfTrigger { get; set; } = true;
		public int AutoPlayLoopLimit { get; set; }
		public string? AutoPlayLoopScope { get; set; }
		public string? UseLimitWindow { get; set; }
		public string? PowerStackMode { get; set; }
		public string? PowerPersistenceMode { get; set; }
		public string? DurationTickPolicy { get; set; }
		public bool AutoPlayForceExhaust { get; set; }
		public string? PlayPreventionBlockMode { get; set; }
		public string? PlayPreventionExemption { get; set; }
		public string? GlowColorMode { get; set; }
		public string? GlowCustomColor { get; set; }
		public string? QuestMode { get; set; }
		public int QuestActIndex { get; set; } = -1;
		public string? QuestEventId { get; set; }
		public string? ConsumedCardAction { get; set; }
		public string? ConsumedCardValueSource { get; set; }
		public string? PotionMode { get; set; }
		public string? PotionPoolFilter { get; set; }
		public string? PotionRarityFilter { get; set; }
		public bool? PotionInCombatOnly { get; set; }
		public bool PotionAllowDuplicates { get; set; }
		public string? SpecificPotionId { get; set; }
		public string? CardRewardRarityFilter { get; set; }
		public string? CardRewardSource { get; set; }
		public string? CardRewardRarityOdds { get; set; }
		public string? CreatedCardsCostDuration { get; set; }
		public int CreatedCardsCostTurns { get; set; }
		public string? CreatedCardsCostResource { get; set; }
		public string? CardCostsLessDuration { get; set; }
		public int CardCostsLessTurns { get; set; }
		public string? CardCostsLessMode { get; set; }
		public string? CardCostsLessModifier { get; set; }
		public string? GeneratedCardPool { get; set; }
		public string? GeneratedCardType { get; set; }
		public string? GeneratedCardCustomTag { get; set; }
		public string? GeneratedCardOwnerMode { get; set; }
		public string? ScaleMode { get; set; }
		public string? CountEvent { get; set; }
		public string? CountWindow { get; set; }
		public string? CountWindowInclusion { get; set; }
		public string? BlockLostCountingMode { get; set; }
		public int CountTurns { get; set; }
		public string? CountCardPile { get; set; }
		public string? CountCardPool { get; set; }
		public string? CountCardType { get; set; }
		public string? CountCardFilter { get; set; }
		public bool CountOnlyBlockCards { get; set; }
		public string? CountAggregationMode { get; set; }
		public bool CountUsesCardEffectAmount { get; set; }
		public bool HistoryScalingIncludesBase { get; set; }
		public int? HistoryScalingBaseAmount { get; set; }
		public int HistoryScalingCountStep { get; set; }
		public int RepeatScalingExtraTimes { get; set; }
		public bool DisableOnUpgrade { get; set; }

		public bool GrantToCard { get; set; }
		public string? CardSelectionMode { get; set; }
		public string? CardSelectionPile { get; set; }
		public string? CardSelectionSourceEffectId { get; set; }
		public bool IncludeSourceCardInSelection { get; set; }
		public bool FutureMatchingCards { get; set; }
		public string? CardGrantDuration { get; set; }
		public int CardGrantTurns { get; set; }
		public string? EnchantmentId { get; set; }
		public string? EnchantmentDuration { get; set; }
		public int EnchantmentTurns { get; set; } = 1;
		public bool RepeatIsX { get; set; }
		public int RepeatCount { get; set; } = 1;
		public bool CardSelectionCountIsX { get; set; }
		public int CardSelectionCount { get; set; } = 1;
		public bool CardSelectionOfferCountIsX { get; set; }
		public int CardSelectionOfferCount { get; set; } = 3;
		public string? CardSelectionPool { get; set; }
		public string? CardSelectionType { get; set; }
		public string? CardSelectionFilter { get; set; }
		public string? MoveToPile { get; set; }
		public string? MoveToPosition { get; set; }
		public bool UseMoveDestinationForGeneratedCards { get; set; }
		public string? AdditionalMoveToPiles { get; set; }
		public string? CopyMode { get; set; }
		public string? DelayedPileAction { get; set; }
		public string? DelayedPileCounterUnit { get; set; }
		public string? DelayedPileCounterScope { get; set; }
		public string? DrawnFromPile { get; set; }
		public string? SpecificCardId { get; set; }
		public bool ShowReferencedCardText { get; set; }
		public string? SpecificCardId2 { get; set; }
		public string? SpecificCardId3 { get; set; }
		public string? ChooseOneExecutionMode { get; set; }
		public string? ChooseOneResolveMode { get; set; }
		public string? ChooseOneChoiceRule { get; set; }
		public int ChooseOneResolveCount { get; set; } = 1;
		public ChooseOneOptionDto? ChooseOneOption1 { get; set; }
		public ChooseOneOptionDto? ChooseOneOption2 { get; set; }
		public ChooseOneOptionDto? ChooseOneOption3 { get; set; }
		public string? TransformMode { get; set; }
		public string? StatefulTransformMode { get; set; }
		public string? StatefulTransformDuration { get; set; }
		public int StatefulTransformDurationAmount { get; set; } = 1;
		public int ConditionalBonusAmount { get; set; }
		public string? ConditionalBonusConditionType { get; set; }
		public string? ConditionalBonusCondition { get; set; }
		public string? ConditionalBonusEnemyStatus { get; set; }
		public string? ConditionalBonusPowerId { get; set; }
		public string? ConditionalBonusEnemyIntent { get; set; }
		public string? BranchMode { get; set; }
		public string? BranchConditionType { get; set; }
		public string? BranchCondition { get; set; }
		public string? BranchEnemyStatus { get; set; }
		public string? BranchPowerId { get; set; }
		public string? BranchEnemyIntent { get; set; }
		public CardExtraEffectDto? BranchEffect { get; set; }
		public string? BranchCountEvent { get; set; }
		public string? BranchCountWindow { get; set; }
		public string? BranchCountWindowInclusion { get; set; }
		public string? BranchBlockLostCountingMode { get; set; }
		public int BranchCountTurns { get; set; }
		public string? BranchCountCardPile { get; set; }
		public string? BranchCountCardPool { get; set; }
		public string? BranchCountCardType { get; set; }
		public string? BranchCountCardFilter { get; set; }
		public string? BranchCountAggregationMode { get; set; }
		public bool BranchCountUsesCardEffectAmount { get; set; }
		public bool BranchCountExcludeSourceCard { get; set; }
		public string? BranchCountOrbType { get; set; }
		public string? BranchCountOrbSelection { get; set; }
		public string? BranchCountEnemyStatus { get; set; }
		public string? BranchCountPowerId { get; set; }
		public string? BranchCountEnemyIntent { get; set; }
		public string? BranchCountComparison { get; set; }
		public int BranchCountConditionAmount { get; set; } = 1;
		public string? PowerId { get; set; }
		public string? OrbAction { get; set; }
		public string? OrbType { get; set; }
		public string? OrbSelection { get; set; }
		public string? OrbFollowUp { get; set; }
		public string? OrbScope { get; set; }
		public string? CountOrbType { get; set; }
		public string? CountOrbSelection { get; set; }
		public string? CountEnemyStatus { get; set; }
		public string? CountPowerId { get; set; }
		public string? CountEnemyIntent { get; set; }
		public string? CountComparison { get; set; }
		public int CountConditionAmount { get; set; } = 1;
		public string? ConditionProgressDisplay { get; set; }
		public bool CountExcludeSourceCard { get; set; }
		public string? OstyAction { get; set; }
		public string? MultiplierStat { get; set; }
		public string? MultiplierSourceMode { get; set; }
		public string? MultiplierPowerId { get; set; }
		public string? GrantedKeyword { get; set; }
		public string? StatusIconMode { get; set; }
		public string? StatusIconPowerId { get; set; }
		public string? StatusCustomPackedIconPath { get; set; }
		public string? StatusCustomBigIconPath { get; set; }
		public string? CustomPowerName { get; set; }
		public string? CustomPowerDescription { get; set; }
		public string? PowerHost { get; set; }
		public string? PowerTriggerFrom { get; set; }
		public string? PowerTargeting { get; set; }
		public string? CardMatchMode { get; set; }
		public string? MatchCardId { get; set; }
		public string? MatchTagKind { get; set; }
		public string? MatchVanillaTag { get; set; }
		public string? MatchCustomTag { get; set; }
		public string? MatchCustomKeyword { get; set; }
		public string? CustomKeywordName { get; set; }
		public bool NameFilterEnabled { get; set; }
		public string? NameFilterText { get; set; }
		public bool CostFilterEnabled { get; set; }
		public string? CostFilterMode { get; set; }
		public int CostFilterMax { get; set; }
		public string? EffectId { get; set; }
		public string? ResourceConsumptionMode { get; set; }
		public string? ResourceConsumptionStat { get; set; }
		public string? StatusToStatusMode { get; set; }
		public string? SelfScalingOperation { get; set; }
		public string? SelfScalingTargetType { get; set; }
		public string? SelfScalingField { get; set; }
		public string? SelfScalingRecipientMode { get; set; }
		public string? SelfScalingNumberSelectionMode { get; set; }
		public string? SelfScalingNumberFilter { get; set; }
		public string? SelfScalingTargetEffectId { get; set; }
		public string? SelfScalingDynamicVarKey { get; set; }

		public sealed class ChooseOneOptionDto
		{
			public string? Mode { get; set; }
			public bool ShowFullText { get; set; }
			public string? CardId { get; set; }
			public string? QuerySource { get; set; }
			public string? QueryPile { get; set; }
			public string? QueryPool { get; set; }
			public string? QueryType { get; set; }
			public string? QuerySelectionMode { get; set; }
			public int QueryCount { get; set; } = 1;
			public string? QueryMatchMode { get; set; }
			public string? QueryMatchCardId { get; set; }
			public string? QueryMatchTagKind { get; set; }
			public string? QueryMatchVanillaTag { get; set; }
			public string? QueryMatchCustomTag { get; set; }
			public string? QueryMatchCustomKeyword { get; set; }

			public static ChooseOneOptionDto? FromOption(CardExtraEffectChooseOneOption? option)
			{
				if (option == null)
				{
					return null;
				}

				return new ChooseOneOptionDto
				{
					Mode = option.Mode.ToString(),
					ShowFullText = option.ShowFullText,
					CardId = option.CardId,
					QuerySource = option.QuerySource.ToString(),
					QueryPile = option.QueryPile.ToString(),
					QueryPool = option.QueryPool.ToString(),
					QueryType = option.QueryType.ToString(),
					QuerySelectionMode = option.QuerySelectionMode.ToString(),
					QueryCount = option.QueryCount,
					QueryMatchMode = option.QueryMatchMode.ToString(),
					QueryMatchCardId = option.QueryMatchCardId,
					QueryMatchTagKind = option.QueryMatchTagKind.ToString(),
					QueryMatchVanillaTag = option.QueryMatchVanillaTag.ToString(),
					QueryMatchCustomTag = option.QueryMatchCustomTag,
					QueryMatchCustomKeyword = option.QueryMatchCustomKeyword
				};
			}

			public CardExtraEffectChooseOneOption ToOption()
			{
				CardExtraEffectChooseOneOption option = new CardExtraEffectChooseOneOption
				{
					ShowFullText = ShowFullText,
					CardId = string.IsNullOrWhiteSpace(CardId) ? null : CardId.Trim(),
					QueryCount = Math.Clamp(QueryCount <= 0 ? 1 : QueryCount, 1, 99),
					QueryMatchCardId = string.IsNullOrWhiteSpace(QueryMatchCardId) ? null : QueryMatchCardId.Trim(),
					QueryMatchCustomTag = string.IsNullOrWhiteSpace(QueryMatchCustomTag) ? null : QueryMatchCustomTag.Trim(),
					QueryMatchCustomKeyword = string.IsNullOrWhiteSpace(QueryMatchCustomKeyword) ? null : QueryMatchCustomKeyword.Trim()
				};

				if (!string.IsNullOrWhiteSpace(Mode) && Enum.TryParse(Mode, out CardExtraEffectChooseOneOptionMode parsedMode))
				{
					option.Mode = parsedMode;
				}
				if (!string.IsNullOrWhiteSpace(QuerySource) && Enum.TryParse(QuerySource, out CardExtraEffectChooseOneQuerySource parsedSource))
				{
					option.QuerySource = parsedSource;
				}
				if (!string.IsNullOrWhiteSpace(QueryPile) && Enum.TryParse(QueryPile, out CardExtraEffectCardPile parsedPile))
				{
					option.QueryPile = parsedPile;
				}
				if (!string.IsNullOrWhiteSpace(QueryPool) && Enum.TryParse(QueryPool, out CardGeneratedCardPool parsedPool))
				{
					option.QueryPool = parsedPool;
				}
				if (!string.IsNullOrWhiteSpace(QueryType) && Enum.TryParse(QueryType, out CardGeneratedCardType parsedType))
				{
					option.QueryType = parsedType;
				}
				if (!string.IsNullOrWhiteSpace(QuerySelectionMode) && Enum.TryParse(QuerySelectionMode, out CardExtraEffectCardSelectionMode parsedSelectionMode))
				{
					option.QuerySelectionMode = parsedSelectionMode;
				}
				if (!string.IsNullOrWhiteSpace(QueryMatchMode) && Enum.TryParse(QueryMatchMode, out CardExtraEffectCardMatchMode parsedMatchMode))
				{
					option.QueryMatchMode = parsedMatchMode;
				}
				if (!string.IsNullOrWhiteSpace(QueryMatchTagKind) && Enum.TryParse(QueryMatchTagKind, out CardExtraEffectCardMatchTagKind parsedTagKind))
				{
					option.QueryMatchTagKind = parsedTagKind;
				}
				if (!string.IsNullOrWhiteSpace(QueryMatchVanillaTag) && Enum.TryParse(QueryMatchVanillaTag, out CardTag parsedTag))
				{
					option.QueryMatchVanillaTag = parsedTag;
				}

				return option;
			}
		}

		public static CardExtraEffectDto FromEffect(CardExtraEffect effect, bool numericFieldsAreDeltas = false)
		{
			return new CardExtraEffectDto
			{
				Kind = effect.Kind.ToString(),
				Target = effect.Target.ToString(),
				Amount = effect.Amount,
				AmountIsX = effect.AmountIsX,
				AmountXPlus = effect.AmountXPlus,
				AmountSourceMode = effect.AmountSourceMode.ToString(),
				AmountSourceEffectId = effect.AmountSourceEffectId,
				AmountSourceMultiplier = Math.Clamp(effect.AmountSourceMultiplier <= 0m ? 1m : effect.AmountSourceMultiplier, 0.01m, 999m),
				ValueSourceMode = effect.ValueSourceMode.ToString(),
				ValueSourceActor = effect.ValueSourceActor.ToString(),
				ValueSourceAggregation = effect.ValueSourceAggregation.ToString(),
				ValueSourceKind = effect.ValueSourceKind.ToString(),
				ValueSourcePowerId = effect.ValueSourcePowerId,
				Trigger = effect.Trigger.ToString(),
				PowerTriggerCountEvent = effect.PowerTriggerCountEvent.ToString(),
				PowerTriggerEnemyStatus = effect.PowerTriggerEnemyStatus.ToString(),
				PowerTriggerPowerId = effect.PowerTriggerPowerId,
				TurnBoundary = effect.TurnBoundary.ToString(),
				TurnBoundarySide = effect.TurnBoundarySide.ToString(),
				TurnBoundaryCardLocation = effect.TurnBoundaryCardLocation.ToString(),
				Timing = effect.Timing.ToString(),
				Turns = effect.Turns,
				Duration = effect.Duration.ToString(),
				AsPower = effect.AsPower,
				TriggerCardPool = effect.TriggerCardPool.ToString(),
				TriggerCardType = effect.TriggerCardType.ToString(),
				TriggerCardFilter = effect.TriggerCardFilter.ToString(),
				TriggerEveryN = effect.TriggerEveryN,
				TriggerMaxFires = effect.TriggerMaxFires,
				TriggerMaxTurns = effect.TriggerMaxTurns,
				AutoPlayAllowSelfTrigger = effect.AutoPlayAllowSelfTrigger,
				AutoPlayLoopLimit = effect.AutoPlayLoopLimit,
				AutoPlayLoopScope = effect.AutoPlayLoopScope.ToString(),
				UseLimitWindow = effect.UseLimitWindow.ToString(),
				PowerStackMode = effect.PowerStackMode.ToString(),
				PowerPersistenceMode = effect.PowerPersistenceMode.ToString(),
				DurationTickPolicy = effect.DurationTickPolicy.ToString(),
				AutoPlayForceExhaust = effect.AutoPlayForceExhaust,
				PlayPreventionBlockMode = effect.PlayPreventionBlockMode.ToString(),
				PlayPreventionExemption = effect.PlayPreventionExemption.ToString(),
				GlowColorMode = effect.GlowColorMode.ToString(),
				GlowCustomColor = effect.GlowCustomColor,
				QuestMode = effect.QuestMode.ToString(),
				QuestActIndex = effect.QuestActIndex,
				QuestEventId = effect.QuestEventId,
				ConsumedCardAction = effect.ConsumedCardAction.ToString(),
				ConsumedCardValueSource = effect.ConsumedCardValueSource.ToString(),
				PotionMode = effect.PotionMode.ToString(),
				PotionPoolFilter = effect.PotionPoolFilter.ToString(),
				PotionRarityFilter = effect.PotionRarityFilter.ToString(),
				PotionInCombatOnly = effect.PotionInCombatOnly,
				PotionAllowDuplicates = effect.PotionAllowDuplicates,
				SpecificPotionId = effect.SpecificPotionId,
				CardRewardRarityFilter = effect.CardRewardRarityFilter.ToString(),
				CardRewardSource = effect.CardRewardSource.ToString(),
				CardRewardRarityOdds = effect.CardRewardRarityOdds.ToString(),
				CreatedCardsCostDuration = effect.CreatedCardsCostDuration.ToString(),
				CreatedCardsCostTurns = effect.CreatedCardsCostTurns,
				CreatedCardsCostResource = effect.CreatedCardsCostResource.ToString(),
				CardCostsLessDuration = effect.CardCostsLessDuration.ToString(),
				CardCostsLessTurns = effect.CardCostsLessTurns,
				CardCostsLessMode = effect.CardCostsLessMode.ToString(),
				CardCostsLessModifier = effect.CardCostsLessModifier.ToString(),
				GeneratedCardPool = effect.GeneratedCardPool.ToString(),
				GeneratedCardType = effect.GeneratedCardType.ToString(),
				GeneratedCardCustomTag = effect.GeneratedCardCustomTag,
				GeneratedCardOwnerMode = effect.GeneratedCardOwnerMode.ToString(),
				ScaleMode = effect.ScaleMode.ToString(),
				CountEvent = effect.CountEvent.ToString(),
				CountWindow = effect.CountWindow.ToString(),
				CountWindowInclusion = effect.CountWindowInclusion.ToString(),
				BlockLostCountingMode = effect.BlockLostCountingMode.ToString(),
				CountTurns = effect.CountTurns,
				CountCardPile = effect.CountCardPile.ToString(),
				CountCardPool = effect.CountCardPool.ToString(),
				CountCardType = effect.CountCardType.ToString(),
				CountCardFilter = effect.CountCardFilter.ToString(),
				CountOnlyBlockCards = effect.CountOnlyBlockCards,
				CountAggregationMode = CardEditorExtraEffects.GetEffectiveCountAggregationMode(effect).ToString(),
				CountUsesCardEffectAmount = effect.CountUsesCardEffectAmount,
				HistoryScalingIncludesBase = effect.HistoryScalingIncludesBase,
				HistoryScalingBaseAmount = effect.HistoryScalingBaseAmount,
				HistoryScalingCountStep = numericFieldsAreDeltas
					? effect.HistoryScalingCountStep
					: CardEditorExtraEffects.ResolveHistoryScalingCountStep(effect),
				RepeatScalingExtraTimes = numericFieldsAreDeltas
					? effect.RepeatScalingExtraTimes
					: CardEditorExtraEffects.ResolveRepeatScalingExtraTimes(effect),
				DisableOnUpgrade = effect.DisableOnUpgrade,
				GrantToCard = effect.GrantToCard,
				CardSelectionMode = effect.CardSelectionMode.ToString(),
				CardSelectionPile = effect.CardSelectionPile.ToString(),
				CardSelectionSourceEffectId = effect.CardSelectionSourceEffectId,
				IncludeSourceCardInSelection = effect.IncludeSourceCardInSelection,
				FutureMatchingCards = effect.FutureMatchingCards,
				CardGrantDuration = effect.CardGrantDuration.ToString(),
				CardGrantTurns = effect.CardGrantTurns,
				EnchantmentId = effect.EnchantmentId,
				EnchantmentDuration = effect.EnchantmentDuration.ToString(),
				EnchantmentTurns = effect.EnchantmentTurns,
				RepeatIsX = effect.RepeatIsX,
				RepeatCount = effect.RepeatCount,
				CardSelectionCountIsX = effect.CardSelectionCountIsX,
				CardSelectionCount = effect.CardSelectionCount,
				CardSelectionOfferCountIsX = effect.CardSelectionOfferCountIsX,
				CardSelectionOfferCount = effect.CardSelectionOfferCount,
				CardSelectionPool = effect.CardSelectionPool.ToString(),
				CardSelectionType = effect.CardSelectionType.ToString(),
				CardSelectionFilter = effect.CardSelectionFilter.ToString(),
				MoveToPile = effect.MoveToPile.ToString(),
				MoveToPosition = effect.MoveToPosition.ToString(),
				UseMoveDestinationForGeneratedCards = effect.UseMoveDestinationForGeneratedCards,
				AdditionalMoveToPiles = effect.AdditionalMoveToPiles.ToString(),
				CopyMode = effect.CopyMode.ToString(),
				DelayedPileAction = effect.DelayedPileAction.ToString(),
				DelayedPileCounterUnit = effect.DelayedPileCounterUnit.ToString(),
				DelayedPileCounterScope = effect.DelayedPileCounterScope.ToString(),
				DrawnFromPile = effect.DrawnFromPile.ToString(),
				SpecificCardId = effect.SpecificCardId,
				ShowReferencedCardText = effect.CardReferenceDisplayMode == CardExtraEffectCardReferenceDisplayMode.FullText,
				SpecificCardId2 = effect.SpecificCardId2,
				SpecificCardId3 = effect.SpecificCardId3,
				ChooseOneExecutionMode = effect.ChooseOneExecutionMode.ToString(),
				ChooseOneResolveMode = effect.ChooseOneResolveMode.ToString(),
				ChooseOneChoiceRule = effect.ChooseOneChoiceRule.ToString(),
				ChooseOneResolveCount = Math.Clamp(effect.ChooseOneResolveCount <= 0 ? 1 : effect.ChooseOneResolveCount, 1, 3),
				ChooseOneOption1 = ChooseOneOptionDto.FromOption(effect.ChooseOneOption1),
				ChooseOneOption2 = ChooseOneOptionDto.FromOption(effect.ChooseOneOption2),
				ChooseOneOption3 = ChooseOneOptionDto.FromOption(effect.ChooseOneOption3),
				TransformMode = effect.TransformMode.ToString(),
				StatefulTransformMode = effect.StatefulTransformMode.ToString(),
				StatefulTransformDuration = effect.StatefulTransformDuration.ToString(),
				StatefulTransformDurationAmount = numericFieldsAreDeltas
					? effect.StatefulTransformDurationAmount
					: Math.Clamp(effect.StatefulTransformDurationAmount <= 0 ? 1 : effect.StatefulTransformDurationAmount, 1, 99),
				ConditionalBonusAmount = effect.ConditionalBonusAmount,
				ConditionalBonusConditionType = effect.ConditionalBonusConditionType.ToString(),
				ConditionalBonusCondition = effect.ConditionalBonusCondition.ToString(),
				ConditionalBonusEnemyStatus = effect.ConditionalBonusEnemyStatus.ToString(),
				ConditionalBonusPowerId = effect.ConditionalBonusPowerId,
				ConditionalBonusEnemyIntent = effect.ConditionalBonusEnemyIntent.ToString(),
				BranchMode = effect.BranchMode.ToString(),
				BranchConditionType = effect.BranchConditionType.ToString(),
				BranchCondition = effect.BranchCondition.ToString(),
				BranchEnemyStatus = effect.BranchEnemyStatus.ToString(),
				BranchPowerId = effect.BranchPowerId,
				BranchEnemyIntent = effect.BranchEnemyIntent.ToString(),
				BranchEffect = effect.BranchEffect != null ? FromEffect(effect.BranchEffect, numericFieldsAreDeltas) : null,
				BranchCountEvent = effect.BranchCountEvent.ToString(),
				BranchCountWindow = effect.BranchCountWindow.ToString(),
				BranchCountWindowInclusion = effect.BranchCountWindowInclusion.ToString(),
				BranchBlockLostCountingMode = effect.BranchBlockLostCountingMode.ToString(),
				BranchCountTurns = effect.BranchCountTurns,
				BranchCountCardPile = effect.BranchCountCardPile.ToString(),
				BranchCountCardPool = effect.BranchCountCardPool.ToString(),
				BranchCountCardType = effect.BranchCountCardType.ToString(),
				BranchCountCardFilter = effect.BranchCountCardFilter.ToString(),
				BranchCountAggregationMode = CardEditorExtraEffects.GetEffectiveBranchCountAggregationMode(effect).ToString(),
				BranchCountUsesCardEffectAmount = effect.BranchCountUsesCardEffectAmount,
				BranchCountExcludeSourceCard = effect.BranchCountExcludeSourceCard,
				BranchCountOrbType = effect.BranchCountOrbType.ToString(),
				BranchCountOrbSelection = effect.BranchCountOrbSelection.ToString(),
				BranchCountEnemyStatus = effect.BranchCountEnemyStatus.ToString(),
				BranchCountPowerId = effect.BranchCountPowerId,
				BranchCountEnemyIntent = effect.BranchCountEnemyIntent.ToString(),
				BranchCountComparison = effect.BranchCountComparison.ToString(),
				BranchCountConditionAmount = effect.BranchCountConditionAmount,
				PowerId = effect.PowerId,
				OrbAction = effect.OrbAction.ToString(),
				OrbType = effect.OrbType.ToString(),
				OrbSelection = effect.OrbSelection.ToString(),
				OrbFollowUp = effect.OrbFollowUp.ToString(),
				OrbScope = effect.OrbScope.ToString(),
				CountOrbType = effect.CountOrbType.ToString(),
				CountOrbSelection = effect.CountOrbSelection.ToString(),
				CountEnemyStatus = effect.CountEnemyStatus.ToString(),
				CountPowerId = effect.CountPowerId,
				CountEnemyIntent = effect.CountEnemyIntent.ToString(),
				CountComparison = effect.CountComparison.ToString(),
				CountConditionAmount = effect.CountConditionAmount,
				ConditionProgressDisplay = effect.ConditionProgressDisplay.ToString(),
				CountExcludeSourceCard = effect.CountExcludeSourceCard,
				OstyAction = effect.OstyAction.ToString(),
				MultiplierStat = effect.MultiplierStat.ToString(),
				MultiplierSourceMode = effect.MultiplierSourceMode.ToString(),
				MultiplierPowerId = effect.MultiplierPowerId,
				GrantedKeyword = effect.GrantedKeyword.ToString(),
				StatusIconMode = effect.StatusIconMode.ToString(),
				StatusIconPowerId = effect.StatusIconPowerId,
				StatusCustomPackedIconPath = effect.StatusCustomPackedIconPath,
				StatusCustomBigIconPath = effect.StatusCustomBigIconPath,
				CustomPowerName = effect.CustomPowerName,
				CustomPowerDescription = effect.CustomPowerDescription,
				PowerHost = CardEditorExtraEffects.GetEffectivePowerHost(effect).ToString(),
				PowerTriggerFrom = CardEditorExtraEffects.GetEffectivePowerTriggerFrom(effect).ToString(),
				PowerTargeting = effect.PowerTargeting.ToString(),
				CardMatchMode = effect.CardMatchMode.ToString(),
				MatchCardId = effect.MatchCardId,
				MatchTagKind = effect.MatchTagKind.ToString(),
				MatchVanillaTag = effect.MatchVanillaTag.ToString(),
				MatchCustomTag = effect.MatchCustomTag,
				MatchCustomKeyword = effect.MatchCustomKeyword,
				CustomKeywordName = effect.CustomKeywordName,
				NameFilterEnabled = effect.NameFilterEnabled,
				NameFilterText = effect.NameFilterText,
				CostFilterEnabled = effect.CostFilterEnabled,
				CostFilterMode = effect.CostFilterMode.ToString(),
				CostFilterMax = effect.CostFilterMax,
				EffectId = effect.EffectId,
				ResourceConsumptionMode = effect.ResourceConsumptionMode.ToString(),
				ResourceConsumptionStat = effect.ResourceConsumptionStat.ToString(),
				StatusToStatusMode = effect.StatusToStatusMode.ToString(),
				SelfScalingOperation = effect.SelfScalingOperation.ToString(),
				SelfScalingTargetType = effect.SelfScalingTargetType.ToString(),
				SelfScalingField = effect.SelfScalingField.ToString(),
				SelfScalingRecipientMode = effect.SelfScalingRecipientMode.ToString(),
				SelfScalingNumberSelectionMode = effect.SelfScalingNumberSelectionMode.ToString(),
				SelfScalingNumberFilter = effect.SelfScalingNumberFilter.ToString(),
				SelfScalingTargetEffectId = effect.SelfScalingTargetEffectId,
				SelfScalingDynamicVarKey = effect.SelfScalingDynamicVarKey
			};
		}

		public bool TryToEffect(out CardExtraEffect effect, bool numericFieldsAreDeltas = false)
		{
			effect = new CardExtraEffect();

			if (string.IsNullOrWhiteSpace(Kind) || !Enum.TryParse(Kind, out CardExtraEffectKind kind))
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(Target) || !Enum.TryParse(Target, out CardExtraEffectTarget target))
			{
				return false;
			}

			CardExtraEffectTrigger trigger = CardExtraEffectTrigger.OnPlay;
			if (!string.IsNullOrWhiteSpace(Trigger) && Enum.TryParse(Trigger, out CardExtraEffectTrigger parsedTrigger))
			{
				trigger = parsedTrigger;
			}

			CardExtraEffectTiming timing = CardExtraEffectTiming.Immediate;
			if (!string.IsNullOrWhiteSpace(Timing) && Enum.TryParse(Timing, out CardExtraEffectTiming parsedTiming))
			{
				timing = parsedTiming;
			}
			int turns = Turns;
			if (!numericFieldsAreDeltas && timing != CardExtraEffectTiming.Immediate && turns < 0)
			{
				turns = 1;
			}
			if (timing == CardExtraEffectTiming.Immediate)
			{
				turns = 0;
			}

			effect.Kind = kind;
			effect.Target = target;
			effect.Amount = Amount;
			effect.AmountIsX = AmountIsX;
			effect.AmountXPlus = AmountIsX ? Math.Max(0, AmountXPlus) : 0;
			effect.AmountSourceMode = CardExtraEffectAmountSourceMode.Fixed;
			if (!string.IsNullOrWhiteSpace(AmountSourceMode)
				&& Enum.TryParse(AmountSourceMode, out CardExtraEffectAmountSourceMode parsedAmountSourceMode))
			{
				effect.AmountSourceMode = parsedAmountSourceMode;
			}
			effect.AmountSourceEffectId = string.IsNullOrWhiteSpace(AmountSourceEffectId)
				? null
				: AmountSourceEffectId.Trim();
			effect.AmountSourceMultiplier = Math.Clamp(AmountSourceMultiplier <= 0m ? 1m : AmountSourceMultiplier, 0.01m, 999m);
			effect.ValueSourceMode = CardExtraEffectValueSourceMode.Common;
			if (!string.IsNullOrWhiteSpace(ValueSourceMode)
				&& Enum.TryParse(ValueSourceMode, out CardExtraEffectValueSourceMode parsedValueSourceMode))
			{
				effect.ValueSourceMode = parsedValueSourceMode;
			}
			effect.ValueSourceActor = CardExtraEffectValueSourceActor.Self;
			if (!string.IsNullOrWhiteSpace(ValueSourceActor)
				&& Enum.TryParse(ValueSourceActor, out CardExtraEffectValueSourceActor parsedValueSourceActor))
			{
				effect.ValueSourceActor = parsedValueSourceActor;
			}
			effect.ValueSourceAggregation = CardExtraEffectValueSourceAggregation.Value;
			if (!string.IsNullOrWhiteSpace(ValueSourceAggregation)
				&& Enum.TryParse(ValueSourceAggregation, out CardExtraEffectValueSourceAggregation parsedValueSourceAggregation))
			{
				effect.ValueSourceAggregation = parsedValueSourceAggregation;
			}
			effect.ValueSourceKind = CardExtraEffectValueSourceKind.MaxHp;
			if (!string.IsNullOrWhiteSpace(ValueSourceKind)
				&& Enum.TryParse(ValueSourceKind, out CardExtraEffectValueSourceKind parsedValueSourceKind))
			{
				effect.ValueSourceKind = parsedValueSourceKind;
			}
			effect.ValueSourcePowerId = string.IsNullOrWhiteSpace(ValueSourcePowerId)
				? null
				: ValueSourcePowerId.Trim();
			effect.DisableOnUpgrade = DisableOnUpgrade;
			effect.Trigger = trigger;
			effect.PowerTriggerCountEvent = CardExtraEffectCountEvent.BlockLost;
			if (!string.IsNullOrWhiteSpace(PowerTriggerCountEvent) && Enum.TryParse(PowerTriggerCountEvent, out CardExtraEffectCountEvent parsedPowerTriggerCountEvent))
			{
				effect.PowerTriggerCountEvent = parsedPowerTriggerCountEvent;
			}
			effect.PowerTriggerEnemyStatus = CardExtraEffectEnemyStatus.AnyPowerStatus;
			if (!string.IsNullOrWhiteSpace(PowerTriggerEnemyStatus) && Enum.TryParse(PowerTriggerEnemyStatus, out CardExtraEffectEnemyStatus parsedPowerTriggerEnemyStatus))
			{
				effect.PowerTriggerEnemyStatus = parsedPowerTriggerEnemyStatus;
			}
			effect.PowerTriggerPowerId = string.IsNullOrWhiteSpace(PowerTriggerPowerId)
				? null
				: PowerTriggerPowerId.Trim();
			effect.TurnBoundary = CardExtraEffectTurnBoundary.End;
			if (!string.IsNullOrWhiteSpace(TurnBoundary) && Enum.TryParse(TurnBoundary, out CardExtraEffectTurnBoundary parsedTurnBoundary))
			{
				effect.TurnBoundary = parsedTurnBoundary;
			}
			effect.TurnBoundarySide = CardExtraEffectTurnBoundarySide.YourTurn;
			if (!string.IsNullOrWhiteSpace(TurnBoundarySide) && Enum.TryParse(TurnBoundarySide, out CardExtraEffectTurnBoundarySide parsedTurnBoundarySide))
			{
				effect.TurnBoundarySide = parsedTurnBoundarySide;
			}
			effect.TurnBoundaryCardLocation = CardExtraEffectTurnBoundaryCardLocation.Any;
			if (!string.IsNullOrWhiteSpace(TurnBoundaryCardLocation) && Enum.TryParse(TurnBoundaryCardLocation, out CardExtraEffectTurnBoundaryCardLocation parsedTurnBoundaryCardLocation))
			{
				effect.TurnBoundaryCardLocation = parsedTurnBoundaryCardLocation;
			}
			effect.Timing = timing;
			effect.Turns = turns;
			effect.AsPower = AsPower;
			effect.CountCardPile = CardExtraEffectCardPile.Hand;
			if (!string.IsNullOrWhiteSpace(CountCardPile) && Enum.TryParse(CountCardPile, out CardExtraEffectCardPile parsedCountPile))
			{
				effect.CountCardPile = parsedCountPile;
			}

			CardExtraEffectDuration duration = CardExtraEffectDuration.Permanent;
			if (!string.IsNullOrWhiteSpace(Duration) && Enum.TryParse(Duration, out CardExtraEffectDuration parsedDuration))
			{
				duration = parsedDuration;
			}
			effect.Duration = duration;

			effect.CardMatchMode = CardExtraEffectCardMatchMode.Any;
			if (!string.IsNullOrWhiteSpace(CardMatchMode) && Enum.TryParse(CardMatchMode, out CardExtraEffectCardMatchMode parsedMatchMode))
			{
				effect.CardMatchMode = parsedMatchMode;
			}
			effect.MatchCardId = string.IsNullOrWhiteSpace(MatchCardId) ? null : MatchCardId.Trim();
			effect.MatchTagKind = CardExtraEffectCardMatchTagKind.Vanilla;
			if (!string.IsNullOrWhiteSpace(MatchTagKind) && Enum.TryParse(MatchTagKind, out CardExtraEffectCardMatchTagKind parsedMatchTagKind))
			{
				effect.MatchTagKind = parsedMatchTagKind;
			}
			effect.MatchVanillaTag = CardTag.None;
			if (!string.IsNullOrWhiteSpace(MatchVanillaTag) && Enum.TryParse(MatchVanillaTag, out CardTag parsedVanillaTag))
			{
				effect.MatchVanillaTag = parsedVanillaTag;
			}
			effect.MatchCustomTag = string.IsNullOrWhiteSpace(MatchCustomTag) ? null : MatchCustomTag.Trim();
			effect.MatchCustomKeyword = string.IsNullOrWhiteSpace(MatchCustomKeyword) ? null : MatchCustomKeyword.Trim();
			effect.CustomKeywordName = string.IsNullOrWhiteSpace(CustomKeywordName) ? null : CustomKeywordName.Trim();
			effect.NameFilterEnabled = NameFilterEnabled;
			effect.NameFilterText = string.IsNullOrWhiteSpace(NameFilterText) ? null : NameFilterText.Trim();
			effect.CountExcludeSourceCard = CountExcludeSourceCard;
			effect.PowerId = string.IsNullOrWhiteSpace(PowerId) ? null : PowerId.Trim();

			CardGeneratedCardPool triggerPool = CardGeneratedCardPool.All;
			if (!string.IsNullOrWhiteSpace(TriggerCardPool) && Enum.TryParse(TriggerCardPool, out CardGeneratedCardPool parsedTriggerPool))
			{
				triggerPool = parsedTriggerPool;
			}
			effect.TriggerCardPool = triggerPool;

			CardGeneratedCardType triggerType = CardGeneratedCardType.Any;
			if (!string.IsNullOrWhiteSpace(TriggerCardType) && Enum.TryParse(TriggerCardType, out CardGeneratedCardType parsedTriggerType))
			{
				triggerType = parsedTriggerType;
			}
			effect.TriggerCardType = triggerType;

			CardExtraEffectCountCardFilter triggerFilter = CardExtraEffectCountCardFilter.Any;
			if (!string.IsNullOrWhiteSpace(TriggerCardFilter) && Enum.TryParse(TriggerCardFilter, out CardExtraEffectCountCardFilter parsedTriggerFilter))
			{
				triggerFilter = parsedTriggerFilter;
			}
			effect.TriggerCardFilter = triggerFilter;

			effect.TriggerEveryN = numericFieldsAreDeltas ? TriggerEveryN : Math.Max(0, TriggerEveryN);
			effect.TriggerMaxFires = numericFieldsAreDeltas ? TriggerMaxFires : Math.Max(0, TriggerMaxFires);
			effect.TriggerMaxTurns = numericFieldsAreDeltas ? TriggerMaxTurns : Math.Max(0, TriggerMaxTurns);
			effect.AutoPlayAllowSelfTrigger = AutoPlayAllowSelfTrigger;
			effect.AutoPlayLoopLimit = numericFieldsAreDeltas
				? Math.Clamp(AutoPlayLoopLimit, -999, 999)
				: Math.Clamp(AutoPlayLoopLimit, 0, 999);
			effect.AutoPlayLoopScope = CardExtraEffectAutoPlayLoopScope.ThisCard;
			if (!string.IsNullOrWhiteSpace(AutoPlayLoopScope)
				&& Enum.TryParse(AutoPlayLoopScope, out CardExtraEffectAutoPlayLoopScope parsedLoopScope))
			{
				effect.AutoPlayLoopScope = parsedLoopScope;
			}
			effect.UseLimitWindow = CardExtraEffectUseLimitWindow.Turn;
			if (!string.IsNullOrWhiteSpace(UseLimitWindow)
				&& Enum.TryParse(UseLimitWindow, out CardExtraEffectUseLimitWindow parsedUseLimitWindow))
			{
				effect.UseLimitWindow = parsedUseLimitWindow;
			}
			effect.PowerStackMode = CardExtraEffectPowerStackMode.Merge;
			if (!string.IsNullOrWhiteSpace(PowerStackMode)
				&& Enum.TryParse(PowerStackMode, out CardExtraEffectPowerStackMode parsedPowerStackMode))
			{
				effect.PowerStackMode = parsedPowerStackMode;
			}
			effect.PowerPersistenceMode = CardExtraEffectPowerPersistenceMode.Normal;
			if (!string.IsNullOrWhiteSpace(PowerPersistenceMode)
				&& Enum.TryParse(PowerPersistenceMode, out CardExtraEffectPowerPersistenceMode parsedPowerPersistenceMode))
			{
				effect.PowerPersistenceMode = parsedPowerPersistenceMode;
			}
			effect.DurationTickPolicy = CardExtraEffectDurationTickPolicy.Default;
			if (!string.IsNullOrWhiteSpace(DurationTickPolicy)
				&& Enum.TryParse(DurationTickPolicy, out CardExtraEffectDurationTickPolicy parsedDurationTickPolicy))
			{
				effect.DurationTickPolicy = parsedDurationTickPolicy;
			}
			effect.AutoPlayForceExhaust = AutoPlayForceExhaust;
			effect.PlayPreventionBlockMode = CardExtraEffectPlayPreventionBlockMode.ManualAndAutoPlay;
			if (!string.IsNullOrWhiteSpace(PlayPreventionBlockMode)
				&& Enum.TryParse(PlayPreventionBlockMode, out CardExtraEffectPlayPreventionBlockMode parsedPlayPreventionBlockMode))
			{
				effect.PlayPreventionBlockMode = parsedPlayPreventionBlockMode;
			}
			effect.PlayPreventionExemption = CardExtraEffectPlayPreventionExemption.None;
			if (!string.IsNullOrWhiteSpace(PlayPreventionExemption)
				&& Enum.TryParse(PlayPreventionExemption, out CardExtraEffectPlayPreventionExemption parsedPlayPreventionExemption))
			{
				effect.PlayPreventionExemption = parsedPlayPreventionExemption;
			}
			effect.GlowColorMode = CardExtraEffectGlowColorMode.Red;
			if (!string.IsNullOrWhiteSpace(GlowColorMode)
				&& Enum.TryParse(GlowColorMode, out CardExtraEffectGlowColorMode parsedGlowColorMode))
			{
				effect.GlowColorMode = parsedGlowColorMode;
			}
			effect.GlowCustomColor = string.IsNullOrWhiteSpace(GlowCustomColor) ? null : GlowCustomColor.Trim();
			effect.QuestMode = CardExtraEffectQuestMode.RunProgress;
			if (!string.IsNullOrWhiteSpace(QuestMode)
				&& Enum.TryParse(QuestMode, out CardExtraEffectQuestMode parsedQuestMode))
			{
				effect.QuestMode = parsedQuestMode;
			}
			effect.QuestActIndex = QuestActIndex <= 0 ? -1 : QuestActIndex;
			effect.QuestEventId = string.IsNullOrWhiteSpace(QuestEventId) ? null : QuestEventId.Trim();
			effect.ConsumedCardAction = CardExtraEffectConsumedCardAction.Exhaust;
			if (!string.IsNullOrWhiteSpace(ConsumedCardAction)
				&& Enum.TryParse(ConsumedCardAction, out CardExtraEffectConsumedCardAction parsedConsumedCardAction))
			{
				effect.ConsumedCardAction = parsedConsumedCardAction;
			}
			effect.ConsumedCardValueSource = CardExtraEffectConsumedCardValueSource.Damage;
			if (!string.IsNullOrWhiteSpace(ConsumedCardValueSource)
				&& Enum.TryParse(ConsumedCardValueSource, out CardExtraEffectConsumedCardValueSource parsedConsumedCardValueSource))
			{
				effect.ConsumedCardValueSource = parsedConsumedCardValueSource;
			}
			effect.PotionMode = CardExtraEffectPotionMode.Random;
			if (!string.IsNullOrWhiteSpace(PotionMode)
				&& Enum.TryParse(PotionMode, out CardExtraEffectPotionMode parsedPotionMode))
			{
				effect.PotionMode = parsedPotionMode;
			}
			effect.PotionPoolFilter = CardExtraEffectPotionPoolFilter.Vanilla;
			if (!string.IsNullOrWhiteSpace(PotionPoolFilter)
				&& Enum.TryParse(PotionPoolFilter, out CardExtraEffectPotionPoolFilter parsedPotionPoolFilter))
			{
				effect.PotionPoolFilter = parsedPotionPoolFilter;
			}
			effect.PotionRarityFilter = CardExtraEffectPotionRarityFilter.Any;
			if (!string.IsNullOrWhiteSpace(PotionRarityFilter)
				&& Enum.TryParse(PotionRarityFilter, out CardExtraEffectPotionRarityFilter parsedPotionRarityFilter))
			{
				effect.PotionRarityFilter = parsedPotionRarityFilter;
			}
			effect.PotionInCombatOnly = PotionInCombatOnly ?? true;
			effect.PotionAllowDuplicates = PotionAllowDuplicates;
			if (!string.IsNullOrWhiteSpace(SpecificPotionId))
			{
				effect.SpecificPotionId = SpecificPotionId.Trim();
			}
			effect.CardRewardRarityFilter = CardExtraEffectCardRewardRarityFilter.Any;
			if (!string.IsNullOrWhiteSpace(CardRewardRarityFilter)
				&& Enum.TryParse(CardRewardRarityFilter, out CardExtraEffectCardRewardRarityFilter parsedCardRewardRarityFilter))
			{
				effect.CardRewardRarityFilter = parsedCardRewardRarityFilter;
			}
			effect.CardRewardSource = CardExtraEffectCardRewardSource.RoomDefault;
			if (!string.IsNullOrWhiteSpace(CardRewardSource)
				&& Enum.TryParse(CardRewardSource, out CardExtraEffectCardRewardSource parsedCardRewardSource))
			{
				effect.CardRewardSource = parsedCardRewardSource;
			}
			effect.CardRewardRarityOdds = CardExtraEffectCardRewardRarityOdds.RoomDefault;
			if (!string.IsNullOrWhiteSpace(CardRewardRarityOdds)
				&& Enum.TryParse(CardRewardRarityOdds, out CardExtraEffectCardRewardRarityOdds parsedCardRewardRarityOdds))
			{
				effect.CardRewardRarityOdds = parsedCardRewardRarityOdds;
			}

			CardCreatedCardsCostDuration createdCostDuration = CardCreatedCardsCostDuration.ThisTurn;
			if (!string.IsNullOrWhiteSpace(CreatedCardsCostDuration) && Enum.TryParse(CreatedCardsCostDuration, out CardCreatedCardsCostDuration parsedCreatedCostDuration))
			{
				createdCostDuration = parsedCreatedCostDuration;
			}
			effect.CreatedCardsCostDuration = createdCostDuration;
			effect.CreatedCardsCostTurns = CreatedCardsCostTurns;
			CardCreatedCardsCostResource createdCostResource = CardCreatedCardsCostResource.Energy;
			if (!string.IsNullOrWhiteSpace(CreatedCardsCostResource) && Enum.TryParse(CreatedCardsCostResource, out CardCreatedCardsCostResource parsedCreatedCostResource))
			{
				createdCostResource = parsedCreatedCostResource;
			}
			effect.CreatedCardsCostResource = createdCostResource;

			CardExtraEffectCardCostsLessDuration costsLessDuration = CardExtraEffectCardCostsLessDuration.Permanent;
			if (!string.IsNullOrWhiteSpace(CardCostsLessDuration) && Enum.TryParse(CardCostsLessDuration, out CardExtraEffectCardCostsLessDuration parsedCostsLessDuration))
			{
				costsLessDuration = parsedCostsLessDuration;
			}
			effect.CardCostsLessDuration = costsLessDuration;

			int costsLessTurns = CardCostsLessTurns;
			if (!numericFieldsAreDeltas && costsLessTurns <= 0)
			{
				costsLessTurns = 1;
			}
			effect.CardCostsLessTurns = costsLessTurns;

			CardExtraEffectCardCostsLessMode costsLessMode = CardExtraEffectCardCostsLessMode.Legacy;
			if (!string.IsNullOrWhiteSpace(CardCostsLessMode) && Enum.TryParse(CardCostsLessMode, out CardExtraEffectCardCostsLessMode parsedCostsLessMode))
			{
				costsLessMode = parsedCostsLessMode;
			}
			effect.CardCostsLessMode = costsLessMode;

			CardExtraEffectCostModifier costsLessModifier = CardExtraEffectCostModifier.Reduce;
			if (!string.IsNullOrWhiteSpace(CardCostsLessModifier) && Enum.TryParse(CardCostsLessModifier, out CardExtraEffectCostModifier parsedCostsLessModifier))
			{
				costsLessModifier = parsedCostsLessModifier;
			}
			effect.CardCostsLessModifier = costsLessModifier;

			CardGeneratedCardPool generatedPool = CardGeneratedCardPool.Default;
			if (!string.IsNullOrWhiteSpace(GeneratedCardPool) && Enum.TryParse(GeneratedCardPool, out CardGeneratedCardPool parsedPool))
			{
				generatedPool = parsedPool;
			}
			effect.GeneratedCardPool = generatedPool;

			CardGeneratedCardType generatedType = CardGeneratedCardType.Any;
			if (!string.IsNullOrWhiteSpace(GeneratedCardType) && Enum.TryParse(GeneratedCardType, out CardGeneratedCardType parsedType))
			{
				generatedType = parsedType;
			}
			effect.GeneratedCardType = generatedType;
			effect.GeneratedCardCustomTag = string.IsNullOrWhiteSpace(GeneratedCardCustomTag)
				? null
				: GeneratedCardCustomTag.Trim();

			CardExtraEffectGeneratedCardOwnerMode generatedOwnerMode = CardExtraEffectGeneratedCardOwnerMode.SourceOwner;
			if (!string.IsNullOrWhiteSpace(GeneratedCardOwnerMode)
				&& Enum.TryParse(GeneratedCardOwnerMode, out CardExtraEffectGeneratedCardOwnerMode parsedGeneratedOwnerMode))
			{
				generatedOwnerMode = parsedGeneratedOwnerMode;
			}
			effect.GeneratedCardOwnerMode = generatedOwnerMode;

			CardExtraEffectScaleMode scaleMode = CardExtraEffectScaleMode.None;
			if (!string.IsNullOrWhiteSpace(ScaleMode) && Enum.TryParse(ScaleMode, out CardExtraEffectScaleMode parsedScaleMode))
			{
				scaleMode = parsedScaleMode;
			}
			effect.ScaleMode = scaleMode;

			CardExtraEffectCountEvent countEvent = CardExtraEffectCountEvent.Played;
			if (!string.IsNullOrWhiteSpace(CountEvent) && Enum.TryParse(CountEvent, out CardExtraEffectCountEvent parsedCountEvent))
			{
				countEvent = parsedCountEvent;
			}
			effect.CountEvent = countEvent;

			CardExtraEffectCountWindow countWindow = CardExtraEffectCountWindow.ThisCombat;
			if (!string.IsNullOrWhiteSpace(CountWindow) && Enum.TryParse(CountWindow, out CardExtraEffectCountWindow parsedCountWindow))
			{
				countWindow = parsedCountWindow;
			}
			effect.CountWindow = countWindow;

			CardExtraEffectCountWindowInclusion countWindowInclusion = CardExtraEffectCountWindowInclusion.IncludeThisTurn;
			if (!string.IsNullOrWhiteSpace(CountWindowInclusion) && Enum.TryParse(CountWindowInclusion, out CardExtraEffectCountWindowInclusion parsedCountWindowInclusion))
			{
				countWindowInclusion = parsedCountWindowInclusion;
			}
			effect.CountWindowInclusion = countWindowInclusion;

			CardExtraEffectBlockLostCountingMode blockLostCountingMode = CardExtraEffectBlockLostCountingMode.DamageAndEffects;
			if (!string.IsNullOrWhiteSpace(BlockLostCountingMode) && Enum.TryParse(BlockLostCountingMode, out CardExtraEffectBlockLostCountingMode parsedBlockLostCountingMode))
			{
				blockLostCountingMode = parsedBlockLostCountingMode;
			}
			effect.BlockLostCountingMode = blockLostCountingMode;

			int countTurns = CountTurns;
			if (!numericFieldsAreDeltas && countTurns <= 0)
			{
				countTurns = 1;
			}
			effect.CountTurns = countTurns;

			CardGeneratedCardPool countPool = CardGeneratedCardPool.Default;
			if (!string.IsNullOrWhiteSpace(CountCardPool) && Enum.TryParse(CountCardPool, out CardGeneratedCardPool parsedCountPool))
			{
				countPool = parsedCountPool;
			}
			effect.CountCardPool = countPool;

			CardGeneratedCardType countType = CardGeneratedCardType.Any;
			if (!string.IsNullOrWhiteSpace(CountCardType) && Enum.TryParse(CountCardType, out CardGeneratedCardType parsedCountType))
			{
				countType = parsedCountType;
			}
			effect.CountCardType = countType;

			CardExtraEffectCountCardFilter countFilter = CardExtraEffectCountCardFilter.Any;
			if (!string.IsNullOrWhiteSpace(CountCardFilter) && Enum.TryParse(CountCardFilter, out CardExtraEffectCountCardFilter parsedCountFilter))
			{
				countFilter = parsedCountFilter;
			}
			effect.CountCardFilter = countFilter;

			effect.CountOnlyBlockCards = CountOnlyBlockCards;
			effect.CountAggregationMode = CardExtraEffectCountAggregationMode.CardCount;
			if (!string.IsNullOrWhiteSpace(CountAggregationMode) && Enum.TryParse(CountAggregationMode, out CardExtraEffectCountAggregationMode parsedCountAggregationMode))
			{
				effect.CountAggregationMode = parsedCountAggregationMode;
			}
			effect.CountUsesCardEffectAmount = CountUsesCardEffectAmount;
			effect.HistoryScalingIncludesBase = HistoryScalingIncludesBase;
			effect.HistoryScalingBaseAmount = HistoryScalingBaseAmount;
			effect.HistoryScalingCountStep = numericFieldsAreDeltas
				? Math.Clamp(HistoryScalingCountStep, -999, 999)
				: HistoryScalingCountStep <= 0 ? 1 : Math.Clamp(HistoryScalingCountStep, 1, 999);
			effect.RepeatScalingExtraTimes = numericFieldsAreDeltas
				? Math.Clamp(RepeatScalingExtraTimes, -99, 99)
				: RepeatScalingExtraTimes <= 0 ? 1 : Math.Clamp(RepeatScalingExtraTimes, 1, 99);

			effect.GrantToCard = GrantToCard;
			effect.RepeatIsX = RepeatIsX;
			if (numericFieldsAreDeltas)
			{
				effect.RepeatCount = RepeatCount;
			}
			else
			{
				effect.RepeatCount = RepeatIsX
					? Math.Clamp(RepeatCount, 0, 99)
					: (RepeatCount <= 0 ? 1 : RepeatCount);
			}

			CardExtraEffectCardSelectionMode selectionMode = CardExtraEffectCardSelectionMode.Choose;
			if (!string.IsNullOrWhiteSpace(CardSelectionMode) && Enum.TryParse(CardSelectionMode, out CardExtraEffectCardSelectionMode parsedSelectionMode))
			{
				selectionMode = parsedSelectionMode;
			}
			effect.CardSelectionMode = selectionMode;

			CardExtraEffectCardPile selectionPile = CardExtraEffectCardPile.Hand;
			if (!string.IsNullOrWhiteSpace(CardSelectionPile) && Enum.TryParse(CardSelectionPile, out CardExtraEffectCardPile parsedSelectionPile))
			{
				selectionPile = parsedSelectionPile;
			}
			effect.CardSelectionPile = selectionPile;
			effect.CardSelectionSourceEffectId = string.IsNullOrWhiteSpace(CardSelectionSourceEffectId)
				? null
				: CardSelectionSourceEffectId.Trim();
			effect.IncludeSourceCardInSelection = IncludeSourceCardInSelection;
			effect.FutureMatchingCards = FutureMatchingCards;

			CardExtraEffectCardGrantDuration grantDuration = CardExtraEffectCardGrantDuration.ThisTurn;
			if (!string.IsNullOrWhiteSpace(CardGrantDuration) && Enum.TryParse(CardGrantDuration, out CardExtraEffectCardGrantDuration parsedGrantDuration))
			{
				grantDuration = parsedGrantDuration;
			}
			effect.CardGrantDuration = grantDuration;

			int grantTurns = CardGrantTurns;
			if (!numericFieldsAreDeltas && grantTurns <= 0)
			{
				grantTurns = 1;
			}
			effect.CardGrantTurns = grantTurns;

			if (!string.IsNullOrWhiteSpace(EnchantmentId))
			{
				effect.EnchantmentId = EnchantmentId.Trim();
			}

			CardExtraEffectEnchantmentDuration enchantmentDuration = CardExtraEffectEnchantmentDuration.ThisCombat;
			if (!string.IsNullOrWhiteSpace(EnchantmentDuration) && Enum.TryParse(EnchantmentDuration, out CardExtraEffectEnchantmentDuration parsedEnchantmentDuration))
			{
				enchantmentDuration = parsedEnchantmentDuration;
			}
			effect.EnchantmentDuration = enchantmentDuration;

			int enchantmentTurns = EnchantmentTurns;
			if (!numericFieldsAreDeltas && enchantmentTurns <= 0)
			{
				enchantmentTurns = 1;
			}
			effect.EnchantmentTurns = enchantmentTurns;

			effect.CardSelectionCountIsX = CardSelectionCountIsX;
			int selectionCount = CardSelectionCount;
			if (!numericFieldsAreDeltas && selectionCount < 0)
			{
				selectionCount = 0;
			}
			effect.CardSelectionCount = selectionCount;

			effect.CardSelectionOfferCountIsX = CardSelectionOfferCountIsX;
			int selectionOfferCount = CardSelectionOfferCount;
			if (!numericFieldsAreDeltas)
			{
				selectionOfferCount = Math.Clamp(selectionOfferCount, 0, 99);
			}
			effect.CardSelectionOfferCount = selectionOfferCount;

			CardGeneratedCardPool selectionPool = CardGeneratedCardPool.All;
			if (!string.IsNullOrWhiteSpace(CardSelectionPool) && Enum.TryParse(CardSelectionPool, out CardGeneratedCardPool parsedSelectionPool))
			{
				selectionPool = parsedSelectionPool;
			}
			effect.CardSelectionPool = selectionPool;

			CardGeneratedCardType selectionType = CardGeneratedCardType.Any;
			if (!string.IsNullOrWhiteSpace(CardSelectionType) && Enum.TryParse(CardSelectionType, out CardGeneratedCardType parsedSelectionType))
			{
				selectionType = parsedSelectionType;
			}
			effect.CardSelectionType = selectionType;

			CardExtraEffectCountCardFilter selectionFilter = CardExtraEffectCountCardFilter.Any;
			if (!string.IsNullOrWhiteSpace(CardSelectionFilter) && Enum.TryParse(CardSelectionFilter, out CardExtraEffectCountCardFilter parsedSelectionFilter))
			{
				selectionFilter = parsedSelectionFilter;
			}
			effect.CardSelectionFilter = selectionFilter;

			CardExtraEffectCardPile moveToPile = CardExtraEffectCardPile.DrawPile;
			if (!string.IsNullOrWhiteSpace(MoveToPile) && Enum.TryParse(MoveToPile, out CardExtraEffectCardPile parsedMoveToPile))
			{
				moveToPile = parsedMoveToPile;
			}
			effect.MoveToPile = moveToPile;

			CardExtraEffectCardPilePosition moveToPosition = CardExtraEffectCardPilePosition.Top;
			if (!string.IsNullOrWhiteSpace(MoveToPosition) && Enum.TryParse(MoveToPosition, out CardExtraEffectCardPilePosition parsedMoveToPosition))
			{
				moveToPosition = parsedMoveToPosition;
			}
			effect.MoveToPosition = moveToPosition;
			effect.UseMoveDestinationForGeneratedCards = UseMoveDestinationForGeneratedCards;
			effect.AdditionalMoveToPiles = CardExtraEffectAdditionalMoveToPiles.None;
			if (!string.IsNullOrWhiteSpace(AdditionalMoveToPiles) && Enum.TryParse(AdditionalMoveToPiles, out CardExtraEffectAdditionalMoveToPiles parsedAdditionalMoveToPiles))
			{
				effect.AdditionalMoveToPiles = parsedAdditionalMoveToPiles;
			}

			effect.CopyMode = CardExtraEffectCopyMode.OneCopyPerSelectedCard;
			if (!string.IsNullOrWhiteSpace(CopyMode) && Enum.TryParse(CopyMode, out CardExtraEffectCopyMode parsedCopyMode))
			{
				effect.CopyMode = parsedCopyMode;
			}

			effect.DelayedPileAction = CardExtraEffectDelayedPileAction.RemoveFromDeck;
			if (!string.IsNullOrWhiteSpace(DelayedPileAction) && Enum.TryParse(DelayedPileAction, out CardExtraEffectDelayedPileAction parsedDelayedPileAction))
			{
				effect.DelayedPileAction = parsedDelayedPileAction;
			}

			effect.DelayedPileCounterUnit = CardExtraEffectDelayedPileCounterUnit.Triggers;
			if (!string.IsNullOrWhiteSpace(DelayedPileCounterUnit) && Enum.TryParse(DelayedPileCounterUnit, out CardExtraEffectDelayedPileCounterUnit parsedDelayedPileCounterUnit))
			{
				effect.DelayedPileCounterUnit = parsedDelayedPileCounterUnit;
			}

			effect.DelayedPileCounterScope = CardExtraEffectDelayedPileCounterScope.ThisCombat;
			if (!string.IsNullOrWhiteSpace(DelayedPileCounterScope) && Enum.TryParse(DelayedPileCounterScope, out CardExtraEffectDelayedPileCounterScope parsedDelayedPileCounterScope))
			{
				effect.DelayedPileCounterScope = parsedDelayedPileCounterScope;
			}

			CardExtraEffectCardPile drawnFromPile = CardExtraEffectCardPile.AllPiles;
			if (!string.IsNullOrWhiteSpace(DrawnFromPile) && Enum.TryParse(DrawnFromPile, out CardExtraEffectCardPile parsedDrawnFromPile))
			{
				drawnFromPile = parsedDrawnFromPile;
			}
			effect.DrawnFromPile = drawnFromPile;

			if (!string.IsNullOrWhiteSpace(SpecificCardId))
			{
				effect.SpecificCardId = SpecificCardId.Trim();
			}
			effect.CardReferenceDisplayMode = ShowReferencedCardText
				? CardExtraEffectCardReferenceDisplayMode.FullText
				: CardExtraEffectCardReferenceDisplayMode.NameOnly;
			effect.ResourceConsumptionMode = CardExtraEffectResourceConsumptionMode.Vigor;
			if (!string.IsNullOrWhiteSpace(ResourceConsumptionMode)
				&& Enum.TryParse(ResourceConsumptionMode, out CardExtraEffectResourceConsumptionMode parsedResourceConsumptionMode))
			{
				effect.ResourceConsumptionMode = parsedResourceConsumptionMode;
			}
			effect.ResourceConsumptionStat = CardExtraEffectMultiplierStat.Vigor;
			if (!string.IsNullOrWhiteSpace(ResourceConsumptionStat)
				&& Enum.TryParse(ResourceConsumptionStat, out CardExtraEffectMultiplierStat parsedResourceConsumptionStat))
			{
				effect.ResourceConsumptionStat = parsedResourceConsumptionStat;
			}
			effect.StatusToStatusMode = CardExtraEffectStatusToStatusMode.Gain;
			if (!string.IsNullOrWhiteSpace(StatusToStatusMode)
				&& Enum.TryParse(StatusToStatusMode, out CardExtraEffectStatusToStatusMode parsedStatusToStatusMode))
			{
				effect.StatusToStatusMode = parsedStatusToStatusMode;
			}
			if (!string.IsNullOrWhiteSpace(SpecificCardId2))
			{
				effect.SpecificCardId2 = SpecificCardId2.Trim();
			}
			if (!string.IsNullOrWhiteSpace(SpecificCardId3))
			{
				effect.SpecificCardId3 = SpecificCardId3.Trim();
			}
			effect.ChooseOneExecutionMode = CardExtraEffectChooseOneExecutionMode.BorrowEffectsOnly;
			if (!string.IsNullOrWhiteSpace(ChooseOneExecutionMode)
				&& Enum.TryParse(ChooseOneExecutionMode, out CardExtraEffectChooseOneExecutionMode parsedChooseOneExecutionMode))
			{
				effect.ChooseOneExecutionMode = parsedChooseOneExecutionMode;
			}
			effect.ChooseOneResolveMode = CardExtraEffectChooseOneResolveMode.PlayerChoice;
			if (!string.IsNullOrWhiteSpace(ChooseOneResolveMode)
				&& Enum.TryParse(ChooseOneResolveMode, out CardExtraEffectChooseOneResolveMode parsedChooseOneResolveMode))
			{
				effect.ChooseOneResolveMode = parsedChooseOneResolveMode;
			}
			effect.ChooseOneChoiceRule = CardExtraEffectChooseOneChoiceRule.Exactly;
			if (!string.IsNullOrWhiteSpace(ChooseOneChoiceRule)
				&& Enum.TryParse(ChooseOneChoiceRule, out CardExtraEffectChooseOneChoiceRule parsedChooseOneChoiceRule))
			{
				effect.ChooseOneChoiceRule = parsedChooseOneChoiceRule;
			}
			effect.ChooseOneResolveCount = Math.Clamp(ChooseOneResolveCount <= 0 ? 1 : ChooseOneResolveCount, 1, 3);
			if (ChooseOneOption1 != null)
			{
				effect.ChooseOneOption1 = ChooseOneOption1.ToOption();
			}
			if (ChooseOneOption2 != null)
			{
				effect.ChooseOneOption2 = ChooseOneOption2.ToOption();
			}
			if (ChooseOneOption3 != null)
			{
				effect.ChooseOneOption3 = ChooseOneOption3.ToOption();
			}

			effect.TransformMode = CardExtraEffectTransformMode.Random;
			if (!string.IsNullOrWhiteSpace(TransformMode) && Enum.TryParse(TransformMode, out CardExtraEffectTransformMode parsedTransformMode))
			{
				effect.TransformMode = parsedTransformMode;
			}

			effect.StatefulTransformMode = CardExtraEffectStatefulTransformMode.Transform;
			if (!string.IsNullOrWhiteSpace(StatefulTransformMode) && Enum.TryParse(StatefulTransformMode, out CardExtraEffectStatefulTransformMode parsedStatefulTransformMode))
			{
				effect.StatefulTransformMode = parsedStatefulTransformMode;
			}
			effect.StatefulTransformDuration = CardExtraEffectStatefulTransformDuration.ThisCombat;
			if (!string.IsNullOrWhiteSpace(StatefulTransformDuration) && Enum.TryParse(StatefulTransformDuration, out CardExtraEffectStatefulTransformDuration parsedStatefulTransformDuration))
			{
				effect.StatefulTransformDuration = parsedStatefulTransformDuration;
			}
			effect.StatefulTransformDurationAmount = numericFieldsAreDeltas
				? StatefulTransformDurationAmount
				: Math.Clamp(StatefulTransformDurationAmount <= 0 ? 1 : StatefulTransformDurationAmount, 1, 99);

			effect.ConditionalBonusAmount = ConditionalBonusAmount;
			effect.ConditionalBonusConditionType = CardExtraEffectBranchConditionType.None;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusConditionType) && Enum.TryParse(ConditionalBonusConditionType, out CardExtraEffectBranchConditionType parsedConditionalBonusConditionType))
			{
				effect.ConditionalBonusConditionType = parsedConditionalBonusConditionType;
			}
			effect.ConditionalBonusCondition = CardExtraEffectConditionalBonusCondition.None;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusCondition) && Enum.TryParse(ConditionalBonusCondition, out CardExtraEffectConditionalBonusCondition parsedConditionalBonusCondition))
			{
				effect.ConditionalBonusCondition = parsedConditionalBonusCondition;
			}
			if (effect.ConditionalBonusConditionType == CardExtraEffectBranchConditionType.None
				&& effect.ConditionalBonusCondition != CardExtraEffectConditionalBonusCondition.None)
			{
				effect.ConditionalBonusConditionType = CardExtraEffectBranchConditionType.TargetCheck;
			}
			effect.ConditionalBonusEnemyStatus = CardExtraEffectEnemyStatus.Weak;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusEnemyStatus) && Enum.TryParse(ConditionalBonusEnemyStatus, out CardExtraEffectEnemyStatus parsedConditionalBonusEnemyStatus))
			{
				effect.ConditionalBonusEnemyStatus = parsedConditionalBonusEnemyStatus;
			}
			effect.ConditionalBonusPowerId = string.IsNullOrWhiteSpace(ConditionalBonusPowerId)
				? null
				: ConditionalBonusPowerId.Trim();
			effect.ConditionalBonusEnemyIntent = CardExtraEffectEnemyIntent.Attack;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusEnemyIntent) && Enum.TryParse(ConditionalBonusEnemyIntent, out CardExtraEffectEnemyIntent parsedConditionalBonusEnemyIntent))
			{
				effect.ConditionalBonusEnemyIntent = parsedConditionalBonusEnemyIntent;
			}
			effect.BranchMode = CardExtraEffectBranchMode.None;
			if (!string.IsNullOrWhiteSpace(BranchMode) && Enum.TryParse(BranchMode, out CardExtraEffectBranchMode parsedBranchMode))
			{
				effect.BranchMode = parsedBranchMode;
			}
			effect.BranchConditionType = CardExtraEffectBranchConditionType.None;
			if (!string.IsNullOrWhiteSpace(BranchConditionType) && Enum.TryParse(BranchConditionType, out CardExtraEffectBranchConditionType parsedBranchConditionType))
			{
				effect.BranchConditionType = parsedBranchConditionType;
			}
			effect.BranchCondition = CardExtraEffectConditionalBonusCondition.None;
			if (!string.IsNullOrWhiteSpace(BranchCondition) && Enum.TryParse(BranchCondition, out CardExtraEffectConditionalBonusCondition parsedBranchCondition))
			{
				effect.BranchCondition = parsedBranchCondition;
			}
			if (effect.BranchConditionType == CardExtraEffectBranchConditionType.None
				&& effect.BranchMode != CardExtraEffectBranchMode.None
				&& effect.BranchCondition != CardExtraEffectConditionalBonusCondition.None)
			{
				effect.BranchConditionType = CardExtraEffectBranchConditionType.TargetCheck;
			}
			effect.BranchEnemyStatus = CardExtraEffectEnemyStatus.Weak;
			if (!string.IsNullOrWhiteSpace(BranchEnemyStatus) && Enum.TryParse(BranchEnemyStatus, out CardExtraEffectEnemyStatus parsedBranchEnemyStatus))
			{
				effect.BranchEnemyStatus = parsedBranchEnemyStatus;
			}
			effect.BranchPowerId = string.IsNullOrWhiteSpace(BranchPowerId)
				? null
				: BranchPowerId.Trim();
			effect.BranchEnemyIntent = CardExtraEffectEnemyIntent.Attack;
			if (!string.IsNullOrWhiteSpace(BranchEnemyIntent) && Enum.TryParse(BranchEnemyIntent, out CardExtraEffectEnemyIntent parsedBranchEnemyIntent))
			{
				effect.BranchEnemyIntent = parsedBranchEnemyIntent;
			}
			effect.BranchCountEvent = CardExtraEffectCountEvent.Played;
			if (!string.IsNullOrWhiteSpace(BranchCountEvent) && Enum.TryParse(BranchCountEvent, out CardExtraEffectCountEvent parsedBranchCountEvent))
			{
				effect.BranchCountEvent = parsedBranchCountEvent;
			}
			effect.BranchCountWindow = CardExtraEffectCountWindow.ThisCombat;
			if (!string.IsNullOrWhiteSpace(BranchCountWindow) && Enum.TryParse(BranchCountWindow, out CardExtraEffectCountWindow parsedBranchCountWindow))
			{
				effect.BranchCountWindow = parsedBranchCountWindow;
			}
			effect.BranchCountWindowInclusion = CardExtraEffectCountWindowInclusion.IncludeThisTurn;
			if (!string.IsNullOrWhiteSpace(BranchCountWindowInclusion) && Enum.TryParse(BranchCountWindowInclusion, out CardExtraEffectCountWindowInclusion parsedBranchCountWindowInclusion))
			{
				effect.BranchCountWindowInclusion = parsedBranchCountWindowInclusion;
			}
			effect.BranchBlockLostCountingMode = CardExtraEffectBlockLostCountingMode.DamageAndEffects;
			if (!string.IsNullOrWhiteSpace(BranchBlockLostCountingMode) && Enum.TryParse(BranchBlockLostCountingMode, out CardExtraEffectBlockLostCountingMode parsedBranchBlockLostCountingMode))
			{
				effect.BranchBlockLostCountingMode = parsedBranchBlockLostCountingMode;
			}
			effect.BranchCountTurns = numericFieldsAreDeltas ? BranchCountTurns : Math.Max(1, BranchCountTurns);
			effect.BranchCountCardPile = CardExtraEffectCardPile.Hand;
			if (!string.IsNullOrWhiteSpace(BranchCountCardPile) && Enum.TryParse(BranchCountCardPile, out CardExtraEffectCardPile parsedBranchCountCardPile))
			{
				effect.BranchCountCardPile = parsedBranchCountCardPile;
			}
			effect.BranchCountCardPool = CardGeneratedCardPool.All;
			if (!string.IsNullOrWhiteSpace(BranchCountCardPool) && Enum.TryParse(BranchCountCardPool, out CardGeneratedCardPool parsedBranchCountCardPool))
			{
				effect.BranchCountCardPool = parsedBranchCountCardPool;
			}
			effect.BranchCountCardType = CardGeneratedCardType.Any;
			if (!string.IsNullOrWhiteSpace(BranchCountCardType) && Enum.TryParse(BranchCountCardType, out CardGeneratedCardType parsedBranchCountCardType))
			{
				effect.BranchCountCardType = parsedBranchCountCardType;
			}
			effect.BranchCountCardFilter = CardExtraEffectCountCardFilter.Any;
			if (!string.IsNullOrWhiteSpace(BranchCountCardFilter) && Enum.TryParse(BranchCountCardFilter, out CardExtraEffectCountCardFilter parsedBranchCountCardFilter))
			{
				effect.BranchCountCardFilter = parsedBranchCountCardFilter;
			}
			effect.BranchCountAggregationMode = CardExtraEffectCountAggregationMode.CardCount;
			if (!string.IsNullOrWhiteSpace(BranchCountAggregationMode) && Enum.TryParse(BranchCountAggregationMode, out CardExtraEffectCountAggregationMode parsedBranchCountAggregationMode))
			{
				effect.BranchCountAggregationMode = parsedBranchCountAggregationMode;
			}
			effect.BranchCountUsesCardEffectAmount = BranchCountUsesCardEffectAmount;
			effect.BranchCountExcludeSourceCard = BranchCountExcludeSourceCard;
			effect.BranchCountOrbType = CardExtraEffectOrbType.Any;
			if (!string.IsNullOrWhiteSpace(BranchCountOrbType) && Enum.TryParse(BranchCountOrbType, out CardExtraEffectOrbType parsedBranchCountOrbType))
			{
				effect.BranchCountOrbType = parsedBranchCountOrbType;
			}
			effect.BranchCountOrbSelection = CardExtraEffectOrbSelection.Leftmost;
			if (!string.IsNullOrWhiteSpace(BranchCountOrbSelection) && Enum.TryParse(BranchCountOrbSelection, out CardExtraEffectOrbSelection parsedBranchCountOrbSelection))
			{
				effect.BranchCountOrbSelection = parsedBranchCountOrbSelection;
			}
			effect.BranchCountEnemyStatus = CardExtraEffectEnemyStatus.Weak;
			if (!string.IsNullOrWhiteSpace(BranchCountEnemyStatus) && Enum.TryParse(BranchCountEnemyStatus, out CardExtraEffectEnemyStatus parsedBranchCountEnemyStatus))
			{
				effect.BranchCountEnemyStatus = parsedBranchCountEnemyStatus;
			}
			effect.BranchCountPowerId = string.IsNullOrWhiteSpace(BranchCountPowerId)
				? null
				: BranchCountPowerId.Trim();
			effect.BranchCountEnemyIntent = CardExtraEffectEnemyIntent.Attack;
			if (!string.IsNullOrWhiteSpace(BranchCountEnemyIntent) && Enum.TryParse(BranchCountEnemyIntent, out CardExtraEffectEnemyIntent parsedBranchCountEnemyIntent))
			{
				effect.BranchCountEnemyIntent = parsedBranchCountEnemyIntent;
			}
			effect.BranchCountComparison = CardExtraEffectCountComparison.None;
			if (!string.IsNullOrWhiteSpace(BranchCountComparison) && Enum.TryParse(BranchCountComparison, out CardExtraEffectCountComparison parsedBranchCountComparison))
			{
				effect.BranchCountComparison = parsedBranchCountComparison;
			}
			effect.BranchCountConditionAmount = numericFieldsAreDeltas ? BranchCountConditionAmount : Math.Max(0, BranchCountConditionAmount);
			if (BranchEffect != null && BranchEffect.TryToEffect(out CardExtraEffect parsedBranchEffect, numericFieldsAreDeltas))
			{
				parsedBranchEffect.BranchMode = CardExtraEffectBranchMode.None;
				parsedBranchEffect.BranchConditionType = CardExtraEffectBranchConditionType.None;
				parsedBranchEffect.BranchCondition = CardExtraEffectConditionalBonusCondition.None;
				parsedBranchEffect.BranchEnemyStatus = default;
				parsedBranchEffect.BranchEnemyIntent = default;
				parsedBranchEffect.BranchCountEvent = default;
				parsedBranchEffect.BranchCountWindow = default;
				parsedBranchEffect.BranchCountWindowInclusion = default;
				parsedBranchEffect.BranchBlockLostCountingMode = default;
				parsedBranchEffect.BranchCountTurns = 0;
				parsedBranchEffect.BranchCountCardPile = default;
				parsedBranchEffect.BranchCountCardPool = default;
				parsedBranchEffect.BranchCountCardType = default;
				parsedBranchEffect.BranchCountCardFilter = default;
				parsedBranchEffect.BranchCountAggregationMode = default;
				parsedBranchEffect.BranchCountUsesCardEffectAmount = false;
				parsedBranchEffect.BranchCountExcludeSourceCard = false;
				parsedBranchEffect.BranchCountOrbType = default;
				parsedBranchEffect.BranchCountOrbSelection = default;
				parsedBranchEffect.BranchCountEnemyStatus = default;
				parsedBranchEffect.BranchCountEnemyIntent = default;
				parsedBranchEffect.BranchCountComparison = default;
				parsedBranchEffect.BranchCountConditionAmount = 0;
				parsedBranchEffect.BranchEffect = null;
				effect.BranchEffect = parsedBranchEffect;
			}
			else
			{
				effect.BranchEffect = null;
			}

			CardExtraEffectOrbAction orbAction = CardExtraEffectOrbAction.Evoke;
			if (!string.IsNullOrWhiteSpace(OrbAction) && Enum.TryParse(OrbAction, out CardExtraEffectOrbAction parsedOrbAction))
			{
				orbAction = parsedOrbAction;
			}
			effect.OrbAction = orbAction;

			CardExtraEffectOrbType orbType = CardExtraEffectOrbType.Any;
			if (!string.IsNullOrWhiteSpace(OrbType) && Enum.TryParse(OrbType, out CardExtraEffectOrbType parsedOrbType))
			{
				orbType = parsedOrbType;
			}
			effect.OrbType = orbType;

			CardExtraEffectOrbSelection orbSelection = CardExtraEffectOrbSelection.Leftmost;
			if (!string.IsNullOrWhiteSpace(OrbSelection) && Enum.TryParse(OrbSelection, out CardExtraEffectOrbSelection parsedOrbSelection))
			{
				orbSelection = parsedOrbSelection;
			}
			effect.OrbSelection = orbSelection;

			CardExtraEffectOrbFollowUp orbFollowUp = CardExtraEffectOrbFollowUp.None;
			if (!string.IsNullOrWhiteSpace(OrbFollowUp) && Enum.TryParse(OrbFollowUp, out CardExtraEffectOrbFollowUp parsedOrbFollowUp))
			{
				orbFollowUp = parsedOrbFollowUp;
			}
			effect.OrbFollowUp = orbFollowUp;

			CardExtraEffectOrbScope orbScope = CardExtraEffectOrbScope.Fixed;
			if (!string.IsNullOrWhiteSpace(OrbScope) && Enum.TryParse(OrbScope, out CardExtraEffectOrbScope parsedOrbScope))
			{
				orbScope = parsedOrbScope;
			}
			effect.OrbScope = orbScope;

			CardExtraEffectOrbType countOrbType = CardExtraEffectOrbType.Any;
			if (!string.IsNullOrWhiteSpace(CountOrbType) && Enum.TryParse(CountOrbType, out CardExtraEffectOrbType parsedCountOrbType))
			{
				countOrbType = parsedCountOrbType;
			}
			effect.CountOrbType = countOrbType;

			CardExtraEffectOrbSelection countOrbSelection = CardExtraEffectOrbSelection.Leftmost;
			if (!string.IsNullOrWhiteSpace(CountOrbSelection) && Enum.TryParse(CountOrbSelection, out CardExtraEffectOrbSelection parsedCountOrbSelection))
			{
				countOrbSelection = parsedCountOrbSelection;
			}
			effect.CountOrbSelection = countOrbSelection;

			CardExtraEffectEnemyStatus countEnemyStatus = CardExtraEffectEnemyStatus.AnyPowerStatus;
			if (!string.IsNullOrWhiteSpace(CountEnemyStatus) && Enum.TryParse(CountEnemyStatus, out CardExtraEffectEnemyStatus parsedCountEnemyStatus))
			{
				countEnemyStatus = parsedCountEnemyStatus;
			}
			effect.CountEnemyStatus = countEnemyStatus;
			effect.CountPowerId = string.IsNullOrWhiteSpace(CountPowerId)
				? null
				: CountPowerId.Trim();

			CardExtraEffectEnemyIntent countEnemyIntent = CardExtraEffectEnemyIntent.Attack;
			if (!string.IsNullOrWhiteSpace(CountEnemyIntent) && Enum.TryParse(CountEnemyIntent, out CardExtraEffectEnemyIntent parsedCountEnemyIntent))
			{
				countEnemyIntent = parsedCountEnemyIntent;
			}
			effect.CountEnemyIntent = countEnemyIntent;

			CardExtraEffectCountComparison countComparison = CardExtraEffectCountComparison.None;
			if (!string.IsNullOrWhiteSpace(CountComparison) && Enum.TryParse(CountComparison, out CardExtraEffectCountComparison parsedCountComparison))
			{
				countComparison = parsedCountComparison;
			}
			effect.CountComparison = countComparison;
			effect.CountConditionAmount = CountConditionAmount;
			CardExtraEffectConditionProgressDisplay conditionProgressDisplay = CardExtraEffectConditionProgressDisplay.Hidden;
			if (!string.IsNullOrWhiteSpace(ConditionProgressDisplay)
				&& Enum.TryParse(ConditionProgressDisplay, out CardExtraEffectConditionProgressDisplay parsedConditionProgressDisplay))
			{
				conditionProgressDisplay = parsedConditionProgressDisplay;
			}
			effect.ConditionProgressDisplay = conditionProgressDisplay;

			CardExtraEffectOstyAction ostyAction = CardExtraEffectOstyAction.Attack;
			if (!string.IsNullOrWhiteSpace(OstyAction) && Enum.TryParse(OstyAction, out CardExtraEffectOstyAction parsedOstyAction))
			{
				ostyAction = parsedOstyAction;
			}
			effect.OstyAction = ostyAction;

			CardExtraEffectMultiplierStat multiplierStat = CardExtraEffectMultiplierStat.Strength;
			if (!string.IsNullOrWhiteSpace(MultiplierStat) && Enum.TryParse(MultiplierStat, out CardExtraEffectMultiplierStat parsedMultiplierStat))
			{
				multiplierStat = parsedMultiplierStat;
			}
			effect.MultiplierStat = multiplierStat;
			effect.MultiplierSourceMode = CardExtraEffectValueSourceMode.Common;
			if (!string.IsNullOrWhiteSpace(MultiplierSourceMode) && Enum.TryParse(MultiplierSourceMode, out CardExtraEffectValueSourceMode parsedMultiplierSourceMode))
			{
				effect.MultiplierSourceMode = parsedMultiplierSourceMode;
			}
			effect.MultiplierPowerId = string.IsNullOrWhiteSpace(MultiplierPowerId)
				? null
				: MultiplierPowerId.Trim();

			CardKeyword grantedKeyword = CardKeyword.Exhaust;
			if (!string.IsNullOrWhiteSpace(GrantedKeyword) && Enum.TryParse(GrantedKeyword, out CardKeyword parsedGrantedKeyword))
			{
				grantedKeyword = parsedGrantedKeyword;
			}
			effect.GrantedKeyword = grantedKeyword;
			if (!string.IsNullOrWhiteSpace(StatusIconMode) && Enum.TryParse(StatusIconMode, out CardExtraEffectStatusIconMode parsedStatusIconMode))
			{
				effect.StatusIconMode = parsedStatusIconMode;
			}
			if (!string.IsNullOrWhiteSpace(StatusIconPowerId))
			{
				effect.StatusIconPowerId = StatusIconPowerId.Trim();
			}
			if (!string.IsNullOrWhiteSpace(StatusCustomPackedIconPath))
			{
				effect.StatusCustomPackedIconPath = StatusCustomPackedIconPath.Trim();
			}
			if (!string.IsNullOrWhiteSpace(StatusCustomBigIconPath))
			{
				effect.StatusCustomBigIconPath = StatusCustomBigIconPath.Trim();
			}
			effect.CustomPowerName = string.IsNullOrWhiteSpace(CustomPowerName) ? null : CustomPowerName.Trim();
			effect.CustomPowerDescription = string.IsNullOrWhiteSpace(CustomPowerDescription) ? null : CustomPowerDescription.Trim();
			if (!string.IsNullOrWhiteSpace(PowerHost) && Enum.TryParse(PowerHost, out CardExtraEffectPowerHost parsedPowerHost))
			{
				effect.PowerHost = parsedPowerHost;
			}
			if (!string.IsNullOrWhiteSpace(PowerTriggerFrom) && Enum.TryParse(PowerTriggerFrom, out CardExtraEffectPowerTriggerFrom parsedPowerTriggerFrom))
			{
				effect.PowerTriggerFrom = parsedPowerTriggerFrom;
			}
			if (!string.IsNullOrWhiteSpace(PowerTargeting) && Enum.TryParse(PowerTargeting, out CardExtraEffectPowerTargeting parsedPowerTargeting))
			{
				effect.PowerTargeting = parsedPowerTargeting;
			}

			effect.EffectId = string.IsNullOrWhiteSpace(EffectId) ? null : EffectId.Trim();
			if (!string.IsNullOrWhiteSpace(SelfScalingOperation)
				&& Enum.TryParse(SelfScalingOperation, out CardExtraEffectSelfScalingOperation parsedSelfScalingOperation))
			{
				effect.SelfScalingOperation = parsedSelfScalingOperation;
			}
			if (!string.IsNullOrWhiteSpace(SelfScalingTargetType)
				&& Enum.TryParse(SelfScalingTargetType, out CardExtraEffectSelfScalingTargetType parsedSelfScalingTargetType))
			{
				effect.SelfScalingTargetType = parsedSelfScalingTargetType;
			}
			if (!string.IsNullOrWhiteSpace(SelfScalingField)
				&& Enum.TryParse(SelfScalingField, out CardExtraEffectSelfScalingField parsedSelfScalingField))
			{
				effect.SelfScalingField = parsedSelfScalingField;
			}
			if (!string.IsNullOrWhiteSpace(SelfScalingRecipientMode)
				&& Enum.TryParse(SelfScalingRecipientMode, out CardExtraEffectSelfScalingRecipientMode parsedSelfScalingRecipientMode))
			{
				effect.SelfScalingRecipientMode = parsedSelfScalingRecipientMode;
			}
			if (!string.IsNullOrWhiteSpace(SelfScalingNumberSelectionMode)
				&& Enum.TryParse(SelfScalingNumberSelectionMode, out CardExtraEffectSelfScalingNumberSelectionMode parsedSelfScalingNumberSelectionMode))
			{
				effect.SelfScalingNumberSelectionMode = parsedSelfScalingNumberSelectionMode;
			}
			if (!string.IsNullOrWhiteSpace(SelfScalingNumberFilter)
				&& Enum.TryParse(SelfScalingNumberFilter, out CardExtraEffectCountCardFilter parsedSelfScalingNumberFilter))
			{
				effect.SelfScalingNumberFilter = parsedSelfScalingNumberFilter;
			}
			effect.SelfScalingTargetEffectId = string.IsNullOrWhiteSpace(SelfScalingTargetEffectId)
				? null
				: SelfScalingTargetEffectId.Trim();
			effect.SelfScalingDynamicVarKey = string.IsNullOrWhiteSpace(SelfScalingDynamicVarKey)
				? null
				: SelfScalingDynamicVarKey.Trim();

			effect.CostFilterEnabled = CostFilterEnabled;
			if (!string.IsNullOrWhiteSpace(CostFilterMode)
				&& Enum.TryParse(CostFilterMode, out CardExtraEffectCostFilterMode parsedCostFilterMode))
			{
				effect.CostFilterMode = parsedCostFilterMode;
			}
			effect.CostFilterMax = CostFilterMax;

			return true;
		}
	}
}
