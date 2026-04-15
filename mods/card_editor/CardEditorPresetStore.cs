using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPresetStore
{
private const int CurrentVersion = 7;
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
		overrides = new Dictionary<ModelId, CardOverride>();
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
					StringComparer.Ordinal)
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
		overrides = new Dictionary<ModelId, CardOverride>();
		presetName = GetStartupPresetName() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(presetName))
		{
			return false;
		}
		return TryLoadPreset(presetName, out overrides);
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
		public string? TitleOverride { get; set; }
		public bool? FullArt { get; set; }
		public string? Finish { get; set; }
		public Dictionary<string, decimal>? FinishParams { get; set; }
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
				TitleOverride = source.TitleOverride,
				FullArt = source.FullArt,
				Finish = source.Finish?.ToString(),
				FinishParams = source.FinishParams != null && source.FinishParams.Count > 0
					? source.FinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
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
					CardUpgradeOverride upgrade = Upgrade.ToUpgradeSafe();
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
				TitleOverride = string.IsNullOrWhiteSpace(TitleOverride) ? null : TitleOverride.Trim(),
				FullArt = FullArt,
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
				HideCosmeticBodyText = HideCosmeticBodyText
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
					? source.ExtraEffects.Select(e => e != null ? CardExtraEffectDto.FromEffect(e) : null).ToList()
					: null
			};
		}

		public CardUpgradeOverride ToUpgradeSafe()
		{
			CardUpgradeOverride result = new CardUpgradeOverride
			{
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
					parsed.Add(effect);
				}
				if (parsed.Any(e => e != null))
				{
					result.ExtraEffects = parsed;
				}
			}

			return result;
		}
	}

	internal sealed class CardExtraEffectDto
	{
		public string? Kind { get; set; }
		public string? Target { get; set; }
		public int Amount { get; set; }
		public bool AmountIsX { get; set; }
		public int AmountXPlus { get; set; }
		public string? Trigger { get; set; }
		public string? PowerTriggerCountEvent { get; set; }
		public string? PowerTriggerEnemyStatus { get; set; }
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
		public string? CreatedCardsCostDuration { get; set; }
		public int CreatedCardsCostTurns { get; set; }
		public string? CreatedCardsCostResource { get; set; }
		public string? CardCostsLessDuration { get; set; }
		public int CardCostsLessTurns { get; set; }
		public string? CardCostsLessMode { get; set; }
		public string? CardCostsLessModifier { get; set; }
		public string? GeneratedCardPool { get; set; }
		public string? GeneratedCardType { get; set; }
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
		public bool HistoryScalingIncludesBase { get; set; }
		public bool DisableOnUpgrade { get; set; }

		public bool GrantToCard { get; set; }
		public string? CardSelectionMode { get; set; }
		public string? CardSelectionPile { get; set; }
		public string? CardGrantDuration { get; set; }
		public int CardGrantTurns { get; set; }
		public string? EnchantmentId { get; set; }
		public string? EnchantmentDuration { get; set; }
		public int EnchantmentTurns { get; set; } = 1;
		public bool RepeatIsX { get; set; }
		public int RepeatCount { get; set; } = 1;
		public bool CardSelectionCountIsX { get; set; }
		public int CardSelectionCount { get; set; } = 1;
		public string? CardSelectionPool { get; set; }
		public string? CardSelectionType { get; set; }
		public string? CardSelectionFilter { get; set; }
		public string? MoveToPile { get; set; }
		public string? MoveToPosition { get; set; }
		public bool UseMoveDestinationForGeneratedCards { get; set; }
		public string? AdditionalMoveToPiles { get; set; }
		public string? DrawnFromPile { get; set; }
		public string? SpecificCardId { get; set; }
		public string? TransformMode { get; set; }
		public int ConditionalBonusAmount { get; set; }
		public string? ConditionalBonusCondition { get; set; }
		public string? ConditionalBonusEnemyStatus { get; set; }
		public string? ConditionalBonusEnemyIntent { get; set; }
		public string? BranchMode { get; set; }
		public string? BranchConditionType { get; set; }
		public string? BranchCondition { get; set; }
		public string? BranchEnemyStatus { get; set; }
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
		public bool BranchCountExcludeSourceCard { get; set; }
		public string? BranchCountOrbType { get; set; }
		public string? BranchCountOrbSelection { get; set; }
		public string? BranchCountEnemyStatus { get; set; }
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
		public string? CountEnemyIntent { get; set; }
		public string? CountComparison { get; set; }
		public int CountConditionAmount { get; set; } = 1;
		public bool CountExcludeSourceCard { get; set; }
		public string? OstyAction { get; set; }
		public string? MultiplierStat { get; set; }
		public string? GrantedKeyword { get; set; }
		public string? CardMatchMode { get; set; }
		public string? MatchCardId { get; set; }
		public string? MatchTagKind { get; set; }
		public string? MatchVanillaTag { get; set; }
		public string? MatchCustomTag { get; set; }
		public string? CustomKeywordName { get; set; }
		public bool CostFilterEnabled { get; set; }
		public int CostFilterMax { get; set; }

		public static CardExtraEffectDto FromEffect(CardExtraEffect effect)
		{
			return new CardExtraEffectDto
			{
				Kind = effect.Kind.ToString(),
				Target = effect.Target.ToString(),
				Amount = effect.Amount,
				AmountIsX = effect.AmountIsX,
				AmountXPlus = effect.AmountXPlus,
				Trigger = effect.Trigger.ToString(),
				PowerTriggerCountEvent = effect.PowerTriggerCountEvent.ToString(),
				PowerTriggerEnemyStatus = effect.PowerTriggerEnemyStatus.ToString(),
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
				CreatedCardsCostDuration = effect.CreatedCardsCostDuration.ToString(),
				CreatedCardsCostTurns = effect.CreatedCardsCostTurns,
				CreatedCardsCostResource = effect.CreatedCardsCostResource.ToString(),
				CardCostsLessDuration = effect.CardCostsLessDuration.ToString(),
				CardCostsLessTurns = effect.CardCostsLessTurns,
				CardCostsLessMode = effect.CardCostsLessMode.ToString(),
				CardCostsLessModifier = effect.CardCostsLessModifier.ToString(),
				GeneratedCardPool = effect.GeneratedCardPool.ToString(),
				GeneratedCardType = effect.GeneratedCardType.ToString(),
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
				HistoryScalingIncludesBase = effect.HistoryScalingIncludesBase,
				DisableOnUpgrade = effect.DisableOnUpgrade,
				GrantToCard = effect.GrantToCard,
				CardSelectionMode = effect.CardSelectionMode.ToString(),
				CardSelectionPile = effect.CardSelectionPile.ToString(),
				CardGrantDuration = effect.CardGrantDuration.ToString(),
				CardGrantTurns = effect.CardGrantTurns,
				EnchantmentId = effect.EnchantmentId,
				EnchantmentDuration = effect.EnchantmentDuration.ToString(),
				EnchantmentTurns = effect.EnchantmentTurns,
				RepeatIsX = effect.RepeatIsX,
				RepeatCount = effect.RepeatCount,
				CardSelectionCountIsX = effect.CardSelectionCountIsX,
				CardSelectionCount = effect.CardSelectionCount,
				CardSelectionPool = effect.CardSelectionPool.ToString(),
				CardSelectionType = effect.CardSelectionType.ToString(),
				CardSelectionFilter = effect.CardSelectionFilter.ToString(),
				MoveToPile = effect.MoveToPile.ToString(),
				MoveToPosition = effect.MoveToPosition.ToString(),
				UseMoveDestinationForGeneratedCards = effect.UseMoveDestinationForGeneratedCards,
				AdditionalMoveToPiles = effect.AdditionalMoveToPiles.ToString(),
				DrawnFromPile = effect.DrawnFromPile.ToString(),
				SpecificCardId = effect.SpecificCardId,
				TransformMode = effect.TransformMode.ToString(),
				ConditionalBonusAmount = effect.ConditionalBonusAmount,
				ConditionalBonusCondition = effect.ConditionalBonusCondition.ToString(),
				ConditionalBonusEnemyStatus = effect.ConditionalBonusEnemyStatus.ToString(),
				ConditionalBonusEnemyIntent = effect.ConditionalBonusEnemyIntent.ToString(),
				BranchMode = effect.BranchMode.ToString(),
				BranchConditionType = effect.BranchConditionType.ToString(),
				BranchCondition = effect.BranchCondition.ToString(),
				BranchEnemyStatus = effect.BranchEnemyStatus.ToString(),
				BranchEnemyIntent = effect.BranchEnemyIntent.ToString(),
				BranchEffect = effect.BranchEffect != null ? FromEffect(effect.BranchEffect) : null,
				BranchCountEvent = effect.BranchCountEvent.ToString(),
				BranchCountWindow = effect.BranchCountWindow.ToString(),
				BranchCountWindowInclusion = effect.BranchCountWindowInclusion.ToString(),
				BranchBlockLostCountingMode = effect.BranchBlockLostCountingMode.ToString(),
				BranchCountTurns = effect.BranchCountTurns,
				BranchCountCardPile = effect.BranchCountCardPile.ToString(),
				BranchCountCardPool = effect.BranchCountCardPool.ToString(),
				BranchCountCardType = effect.BranchCountCardType.ToString(),
				BranchCountCardFilter = effect.BranchCountCardFilter.ToString(),
				BranchCountExcludeSourceCard = effect.BranchCountExcludeSourceCard,
				BranchCountOrbType = effect.BranchCountOrbType.ToString(),
				BranchCountOrbSelection = effect.BranchCountOrbSelection.ToString(),
				BranchCountEnemyStatus = effect.BranchCountEnemyStatus.ToString(),
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
				CountEnemyIntent = effect.CountEnemyIntent.ToString(),
				CountComparison = effect.CountComparison.ToString(),
				CountConditionAmount = effect.CountConditionAmount,
				CountExcludeSourceCard = effect.CountExcludeSourceCard,
				OstyAction = effect.OstyAction.ToString(),
				MultiplierStat = effect.MultiplierStat.ToString(),
				GrantedKeyword = effect.GrantedKeyword.ToString(),
				CardMatchMode = effect.CardMatchMode.ToString(),
				MatchCardId = effect.MatchCardId,
				MatchTagKind = effect.MatchTagKind.ToString(),
				MatchVanillaTag = effect.MatchVanillaTag.ToString(),
				MatchCustomTag = effect.MatchCustomTag,
				CustomKeywordName = effect.CustomKeywordName,
				CostFilterEnabled = effect.CostFilterEnabled,
				CostFilterMax = effect.CostFilterMax
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
			effect.CustomKeywordName = string.IsNullOrWhiteSpace(CustomKeywordName) ? null : CustomKeywordName.Trim();
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
			effect.HistoryScalingIncludesBase = HistoryScalingIncludesBase;

			effect.GrantToCard = GrantToCard;
			effect.RepeatIsX = RepeatIsX;
			effect.RepeatCount = numericFieldsAreDeltas ? RepeatCount : (RepeatCount <= 0 ? 1 : RepeatCount);

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

			effect.TransformMode = CardExtraEffectTransformMode.Random;
			if (!string.IsNullOrWhiteSpace(TransformMode) && Enum.TryParse(TransformMode, out CardExtraEffectTransformMode parsedTransformMode))
			{
				effect.TransformMode = parsedTransformMode;
			}

			effect.ConditionalBonusAmount = ConditionalBonusAmount;
			effect.ConditionalBonusCondition = CardExtraEffectConditionalBonusCondition.None;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusCondition) && Enum.TryParse(ConditionalBonusCondition, out CardExtraEffectConditionalBonusCondition parsedConditionalBonusCondition))
			{
				effect.ConditionalBonusCondition = parsedConditionalBonusCondition;
			}
			effect.ConditionalBonusEnemyStatus = CardExtraEffectEnemyStatus.Weak;
			if (!string.IsNullOrWhiteSpace(ConditionalBonusEnemyStatus) && Enum.TryParse(ConditionalBonusEnemyStatus, out CardExtraEffectEnemyStatus parsedConditionalBonusEnemyStatus))
			{
				effect.ConditionalBonusEnemyStatus = parsedConditionalBonusEnemyStatus;
			}
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

			CardKeyword grantedKeyword = CardKeyword.Exhaust;
			if (!string.IsNullOrWhiteSpace(GrantedKeyword) && Enum.TryParse(GrantedKeyword, out CardKeyword parsedGrantedKeyword))
			{
				grantedKeyword = parsedGrantedKeyword;
			}
			effect.GrantedKeyword = grantedKeyword;

			effect.CostFilterEnabled = CostFilterEnabled;
			effect.CostFilterMax = CostFilterMax;

			return true;
		}
	}
}
