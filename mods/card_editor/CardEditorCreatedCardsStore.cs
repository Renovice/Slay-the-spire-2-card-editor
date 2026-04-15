using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal enum CardEditorCreatedCardPool
{
	Ironclad = 0,
	Silent = 1,
	Defect = 2,
	Regent = 3,
	Necrobinder = 4,
	Colorless = 5,
	Curse = 6,
	Status = 7,
	Token = 8,
	Event = 9,
	Quest = 10
}

internal enum CardEditorEffectSourcePlacement
{
	BeforeCustomEffects = 0,
	AfterCustomEffects = 1
}

internal sealed class CardEditorCreatedCardDefinition
{
	public bool Enabled { get; set; }
	public string Title { get; set; } = string.Empty;
	public CardEditorCreatedCardPool Pool { get; set; } = CardEditorCreatedCardPool.Ironclad;
	public CardRarity Rarity { get; set; } = CardRarity.Common;
	public CardType Type { get; set; } = CardType.Attack;
	public TargetType TargetType { get; set; } = TargetType.AnyEnemy;
	public bool FullArt { get; set; }
	public CardEditorVisualFinish Finish { get; set; }
	public Dictionary<string, float>? FinishParams { get; set; }
	public List<ModelId> EffectSourceCardIds { get; set; } = new();
	public CardEditorEffectSourcePlacement EffectSourcePlacement { get; set; } = CardEditorEffectSourcePlacement.BeforeCustomEffects;
	public ModelId? PortraitSourceCardId { get; set; }
	public string? CustomPortraitFile { get; set; }
	public string? CustomText { get; set; }
	public string? CustomTextUpgraded { get; set; }
	public CardOverride Override { get; set; } = new CardOverride();
}

internal static class CardEditorCreatedCardsStore
{
	private const int CurrentVersion = 3;
	public const int DefaultSlotCount = 50;
	public const int MaxSlotCount = 200;
	public static int SlotCount { get; private set; } = DefaultSlotCount;
	public static int ConfiguredSlotCount { get; private set; } = DefaultSlotCount;
	private const string CardArtFolderName = "card art";

	private static readonly Dictionary<ModelId, CardEditorCreatedCardDefinition> _definitions = new();
	private static readonly Dictionary<ModelId, CardEditorCreatedCardDefinition> _draftDefinitions = new();
	private static bool _loaded;
	private static readonly object _lock = new();

	public static void SetDraftMeta(ModelId cardId, bool enabled, string? title, CardEditorCreatedCardPool pool, CardRarity rarity, CardType type, TargetType targetType, List<ModelId>? effectSourceCardIds, CardEditorEffectSourcePlacement effectSourcePlacement, ModelId? portraitSourceCardId, string? customPortraitFile, bool fullArt, CardEditorVisualFinish finish, string? customText, Dictionary<string, float>? finishParams = null)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? persistent))
		{
			persistent = new CardEditorCreatedCardDefinition();
		}

		if (!_draftDefinitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? draft))
		{
			draft = new CardEditorCreatedCardDefinition
			{
				Enabled = persistent.Enabled,
				Title = persistent.Title,
				Pool = persistent.Pool,
				Rarity = persistent.Rarity,
				Type = persistent.Type,
				TargetType = persistent.TargetType,
				FullArt = persistent.FullArt,
				Finish = persistent.Finish,
				FinishParams = persistent.FinishParams != null ? new Dictionary<string, float>(persistent.FinishParams) : null,
				EffectSourceCardIds = new List<ModelId>(persistent.EffectSourceCardIds),
				EffectSourcePlacement = persistent.EffectSourcePlacement,
				PortraitSourceCardId = persistent.PortraitSourceCardId,
				CustomPortraitFile = persistent.CustomPortraitFile,
				CustomText = persistent.CustomText,
				CustomTextUpgraded = persistent.CustomTextUpgraded,
				Override = persistent.Override
			};
			_draftDefinitions[cardId] = draft;
		}

		draft.Enabled = enabled;
		draft.Title = title?.Trim() ?? string.Empty;
		draft.Pool = pool;
		draft.Rarity = rarity;
		draft.Type = type;
		draft.TargetType = targetType;
		draft.EffectSourceCardIds = NormalizeEffectSourceCardIds(cardId, effectSourceCardIds);
		draft.EffectSourcePlacement = effectSourcePlacement;
		draft.PortraitSourceCardId = portraitSourceCardId;
		draft.CustomPortraitFile = string.IsNullOrWhiteSpace(customPortraitFile) ? null : customPortraitFile.Trim();
		draft.FullArt = fullArt;
		draft.Finish = finish;
		draft.FinishParams = finishParams != null ? new Dictionary<string, float>(finishParams) : null;
		draft.CustomText = customText == null ? null : (string.IsNullOrWhiteSpace(customText) ? string.Empty : customText);
	}

	public static void ClearDraftMeta(ModelId cardId)
	{
		_draftDefinitions.Remove(cardId);
	}

	public static bool IsCreatedCardId(ModelId id)
	{
		return id != null
			&& string.Equals(id.Category, "CARD", StringComparison.Ordinal)
			&& id.Entry != null
			&& id.Entry.StartsWith("CARD_EDITOR_CREATED_CARD", StringComparison.Ordinal);
	}

	public static IReadOnlyList<ModelId> GetAllCreatedCardIds()
	{
		int count = Math.Clamp(SlotCount, 1, MaxSlotCount);
		List<ModelId> ids = new List<ModelId>(count);
		for (int i = 1; i <= count; i++)
		{
			ids.Add(GetCardIdForSlot(i));
		}
		return ids;
	}

	public static bool TryGetCreatedCardIdForSlot(int slot, out ModelId id)
	{
		EnsureLoaded();
		int count = Math.Clamp(SlotCount, 1, MaxSlotCount);
		if (slot < 1 || slot > count)
		{
			id = ModelId.none;
			return false;
		}

		id = GetCardIdForSlot(slot);
		return true;
	}

	public static CardPoolModel GetPoolForCard(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return GetPoolModel(def.Pool);
		}
		return GetPoolModel(CardEditorCreatedCardPool.Ironclad);
	}

	public static string? GetPortraitPathForCard(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? effective)
			&& !string.IsNullOrWhiteSpace(effective.CustomPortraitFile))
		{
			return null;
		}
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.PortraitSourceCardId != null && def.PortraitSourceCardId != ModelId.none)
		{
			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(def.PortraitSourceCardId);
			if (source != null)
			{
				try
				{
					return source.PortraitPath;
				}
				catch
				{
				}
			}
		}
		return null;
	}

	public static CardRarity GetRarityForCard(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			// Full Art is cosmetic: render as Ancient (full-art layout) only during NCard.Reload,
			// but keep the actual rarity for reward/drop logic.
			if (CardEditorFullArtRenderContext.IsActive && def.FullArt)
			{
				return CardRarity.Ancient;
			}
			return def.Rarity;
		}
		return CardRarity.Common;
	}

	public static CardType GetCardTypeForCard(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.Type : CardType.Attack;
	}

	public static TargetType GetTargetTypeForCard(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.TargetType : TargetType.AnyEnemy;
	}

	public static IReadOnlyList<ModelId> GetEffectSourceCardIds(ModelId cardId)
	{
		EnsureLoaded();
		if (!TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return Array.Empty<ModelId>();
		}

		if (def.EffectSourceCardIds == null || def.EffectSourceCardIds.Count == 0)
		{
			return Array.Empty<ModelId>();
		}

		return def.EffectSourceCardIds;
	}

	public static CardEditorEffectSourcePlacement GetEffectSourcePlacement(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def)
			? def.EffectSourcePlacement
			: CardEditorEffectSourcePlacement.BeforeCustomEffects;
	}

	public static bool HasEffectSourceCard(ModelId cardId)
	{
		return GetEffectSourceCardIds(cardId).Count > 0;
	}

	public static string GetTitleForCard(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && !string.IsNullOrWhiteSpace(def.Title))
		{
			return def.Title;
		}
		if (TryParseSlotIndex(cardId, out int slot))
		{
			return $"Custom Card {slot.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}";
		}
		return "Custom Card";
	}

	public static bool IsEnabled(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.Enabled;
	}

	public static int GetPoolSortIndex(ModelId cardId)
	{
		EnsureLoaded();
		if (!TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return int.MaxValue;
		}
		return def.Pool switch
		{
			CardEditorCreatedCardPool.Ironclad => 0,
			CardEditorCreatedCardPool.Silent => 1,
			CardEditorCreatedCardPool.Defect => 2,
			CardEditorCreatedCardPool.Regent => 3,
			CardEditorCreatedCardPool.Necrobinder => 4,
			CardEditorCreatedCardPool.Colorless => 5,
			CardEditorCreatedCardPool.Curse => 6,
			CardEditorCreatedCardPool.Status => 7,
			CardEditorCreatedCardPool.Token => 8,
			CardEditorCreatedCardPool.Event => 9,
			CardEditorCreatedCardPool.Quest => 10,
			_ => 11
		};
	}

	public static bool IsFullArt(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.FullArt;
	}

	public static CardEditorVisualFinish GetFinish(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.Finish : CardEditorVisualFinish.None;
	}

	public static Dictionary<string, float>? GetFinishParams(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.FinishParams : null;
	}

	public static string? GetCustomPortraitFile(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.CustomPortraitFile : null;
	}

	public static IReadOnlyList<string> ListCustomPortraitFiles()
	{
		try
		{
			string dir = GetCustomArtDirectory();
			if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
			{
				return Array.Empty<string>();
			}

			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				".png",
				".jpg",
				".jpeg",
				".webp"
			};

			return Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
				.Where(p => allowed.Contains(Path.GetExtension(p)))
				.Select(Path.GetFileName)
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToList()!;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed listing custom art files: {ex}");
			return Array.Empty<string>();
		}
	}

	public static bool TryGetCustomPortraitAbsolutePath(ModelId cardId, out string absolutePath)
	{
		absolutePath = string.Empty;
		string? file = GetCustomPortraitFile(cardId);
		if (string.IsNullOrWhiteSpace(file))
		{
			return false;
		}

		try
		{
			string dir = GetCustomArtDirectory();
			if (string.IsNullOrWhiteSpace(dir))
			{
				return false;
			}

			string candidate = Path.Combine(dir, file);
			if (!File.Exists(candidate))
			{
				return false;
			}

			absolutePath = candidate;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string GetCustomArtDirectory()
	{
		try
		{
			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (string.IsNullOrWhiteSpace(modDir))
			{
				return string.Empty;
			}

			string dir = Path.Combine(modDir, CardArtFolderName);
			if (Directory.Exists(dir))
			{
				return dir;
			}

			string alt = Path.Combine(modDir, "card_art");
			return Directory.Exists(alt) ? alt : dir;
		}
		catch
		{
			return string.Empty;
		}
	}

	public static bool TryGetDefinition(ModelId cardId, out CardEditorCreatedCardDefinition def)
	{
		EnsureLoaded();
		return _definitions.TryGetValue(cardId, out def!);
	}

	private static bool TryGetEffectiveDefinition(ModelId cardId, out CardEditorCreatedCardDefinition def)
	{
		if (_draftDefinitions.TryGetValue(cardId, out def!))
		{
			return true;
		}
		return _definitions.TryGetValue(cardId, out def!);
	}

	public static void SetEnabled(ModelId cardId, bool enabled)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}
		def.Enabled = enabled;
		Save();
	}

	public static void SetMeta(ModelId cardId, string? title, CardEditorCreatedCardPool pool, CardRarity rarity, CardType type, TargetType targetType, List<ModelId>? effectSourceCardIds, ModelId? portraitSourceCardId, string? customPortraitFile, bool fullArt, CardEditorVisualFinish finish, string? customText, Dictionary<string, float>? finishParams = null)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}
		def.Title = title?.Trim() ?? string.Empty;
		def.Pool = pool;
		def.Rarity = rarity;
		def.Type = type;
		def.TargetType = targetType;
		def.EffectSourceCardIds = NormalizeEffectSourceCardIds(cardId, effectSourceCardIds);
		def.PortraitSourceCardId = portraitSourceCardId;
		def.CustomPortraitFile = string.IsNullOrWhiteSpace(customPortraitFile) ? null : customPortraitFile.Trim();
		def.FullArt = fullArt;
		def.Finish = finish;
		def.FinishParams = finishParams != null ? new Dictionary<string, float>(finishParams) : null;
		def.CustomText = customText == null ? null : (string.IsNullOrWhiteSpace(customText) ? string.Empty : customText);
		Save();
	}

	public static string? GetCustomText(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomText;
		}
		return null;
	}

	public static string? GetCustomTextUpgraded(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomTextUpgraded;
		}
		return null;
	}

	public static void SetDraftCustomTextUpgraded(ModelId cardId, string? customTextUpgraded)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? persistent))
		{
			persistent = new CardEditorCreatedCardDefinition();
		}

		if (!_draftDefinitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? draft))
		{
			draft = new CardEditorCreatedCardDefinition
			{
				Enabled = persistent.Enabled,
				Title = persistent.Title,
				Pool = persistent.Pool,
				Rarity = persistent.Rarity,
				Type = persistent.Type,
				TargetType = persistent.TargetType,
				FullArt = persistent.FullArt,
				Finish = persistent.Finish,
				FinishParams = persistent.FinishParams != null ? new Dictionary<string, float>(persistent.FinishParams) : null,
				EffectSourceCardIds = new List<ModelId>(persistent.EffectSourceCardIds),
				EffectSourcePlacement = persistent.EffectSourcePlacement,
				PortraitSourceCardId = persistent.PortraitSourceCardId,
				CustomPortraitFile = persistent.CustomPortraitFile,
				CustomText = persistent.CustomText,
				CustomTextUpgraded = persistent.CustomTextUpgraded,
				Override = persistent.Override
			};
			_draftDefinitions[cardId] = draft;
		}

		draft.CustomTextUpgraded = customTextUpgraded == null ? null : (string.IsNullOrWhiteSpace(customTextUpgraded) ? string.Empty : customTextUpgraded);
	}

	public static void SetCustomTextUpgraded(ModelId cardId, string? customTextUpgraded)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}

		def.CustomTextUpgraded = customTextUpgraded == null ? null : (string.IsNullOrWhiteSpace(customTextUpgraded) ? string.Empty : customTextUpgraded);
		Save();
	}

	public static void SetOverride(ModelId cardId, CardOverride overrideData)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}
		def.Override = overrideData ?? new CardOverride();
		CardEditorOverrides.Set(cardId, def.Override);
		Save();
	}

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

	private static void LoadInternal()
	{
		try
		{
			_definitions.Clear();
			int desiredSlotCount = DefaultSlotCount;
			string path = GetStorePath();
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				CreatedCardsFileDto? file = JsonSerializer.Deserialize<CreatedCardsFileDto>(json, CreateJsonOptions());
				if (file != null && file.Cards != null && file.Version > 0 && file.Version <= CurrentVersion)
				{
					if (file.SlotCount > 0)
					{
						desiredSlotCount = file.SlotCount;
					}
					foreach ((string idString, CreatedCardDto dto) in file.Cards)
					{
						if (!TryParseModelId(idString, out ModelId cardId) || !IsCreatedCardId(cardId))
						{
							continue;
						}
						CardEditorCreatedCardDefinition def = dto.ToDefinitionSafe(cardId);
						_definitions[cardId] = def;
					}
				}
			}

			SlotCount = Math.Clamp(desiredSlotCount, 1, MaxSlotCount);
			ConfiguredSlotCount = SlotCount;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading created cards store: {ex}");
			_definitions.Clear();
			SlotCount = DefaultSlotCount;
			ConfiguredSlotCount = DefaultSlotCount;
		}

		EnsureDefaultDefinitions();
		Save();
	}

	public static Dictionary<ModelId, CardEditorCreatedCardDefinition> ExportSnapshot()
	{
		EnsureLoaded();
		return _definitions.ToDictionary(kvp => kvp.Key, kvp => CloneDefinition(kvp.Value));
	}

	public static void ImportSnapshot(IReadOnlyDictionary<ModelId, CardEditorCreatedCardDefinition> definitions)
	{
		EnsureLoaded();

		HashSet<ModelId> previousIds = _definitions.Keys.ToHashSet();
		foreach (ModelId id in previousIds)
		{
			CardEditorOverrides.Clear(id);
		}

		_definitions.Clear();
		_draftDefinitions.Clear();

		if (definitions != null)
		{
			foreach ((ModelId id, CardEditorCreatedCardDefinition def) in definitions)
			{
				if (id == null || !IsCreatedCardId(id))
				{
					continue;
				}
				_definitions[id] = CloneDefinition(def);
			}
		}

		EnsureDefaultDefinitions();
		Save();
	}

	public static void ResetAllToDefaults()
	{
		EnsureLoaded();

		foreach (ModelId id in _definitions.Keys.ToList())
		{
			CardEditorOverrides.Clear(id);
		}

		_definitions.Clear();
		_draftDefinitions.Clear();
		EnsureDefaultDefinitions();
		Save();
	}

	public static void ReapplyOverridesToGlobal()
	{
		EnsureLoaded();
		foreach ((ModelId id, CardEditorCreatedCardDefinition def) in _definitions)
		{
			if (def.Override != null && !def.Override.IsEmpty())
			{
				CardEditorOverrides.Set(id, def.Override);
			}
			else
			{
				CardEditorOverrides.Clear(id);
			}
		}
	}

	private static void EnsureDefaultDefinitions()
	{
		int count = Math.Clamp(SlotCount, 1, MaxSlotCount);
		for (int i = 1; i <= count; i++)
		{
			ModelId cardId = GetCardIdForSlot(i);
			if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
			{
				def = new CardEditorCreatedCardDefinition
				{
					Enabled = false,
					Title = $"Custom Card {i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}",
					Pool = CardEditorCreatedCardPool.Ironclad,
					Rarity = CardRarity.Common,
					Type = CardType.Attack,
					TargetType = TargetType.AnyEnemy,
					EffectSourceCardIds = new List<ModelId>(),
					PortraitSourceCardId = ModelDb.GetId<StrikeIronclad>(),
					FullArt = false,
					Finish = CardEditorVisualFinish.None,
					Override = new CardOverride()
				};
				_definitions[cardId] = def;
			}

			if (def.Override != null && !def.Override.IsEmpty())
			{
				CardEditorOverrides.Set(cardId, def.Override);
			}
		}
	}

	private static void Save()
	{
		try
		{
			string path = GetStorePath();
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			int configuredSlotCount = Math.Clamp(ConfiguredSlotCount, 1, MaxSlotCount);
			CreatedCardsFileDto file = new CreatedCardsFileDto
			{
				Version = CurrentVersion,
				SavedAtUtc = DateTime.UtcNow,
				SlotCount = configuredSlotCount,
				Cards = _definitions.ToDictionary(kvp => kvp.Key.ToString(), kvp => CreatedCardDto.FromDefinition(kvp.Value), StringComparer.Ordinal)
			};

			string json = JsonSerializer.Serialize(file, CreateJsonOptions());
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving created cards store: {ex}");
		}
	}

	public static void SetSlotCountForNextRun(int desiredSlotCount)
	{
		EnsureLoaded();
		ConfiguredSlotCount = Math.Clamp(desiredSlotCount, 1, MaxSlotCount);
		Save();
	}

	private static string GetStorePath()
	{
		return ProjectSettings.GlobalizePath("user://card_editor/created_cards.json");
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private static ModelId GetCardIdForSlot(int slot)
	{
		slot = Math.Clamp(slot, 1, Math.Clamp(SlotCount, 1, MaxSlotCount));
		string entry = $"CARD_EDITOR_CREATED_CARD{slot.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}";
		return new ModelId("CARD", entry);
	}

	private static bool TryParseSlotIndex(ModelId id, out int slot)
	{
		slot = 0;
		if (!IsCreatedCardId(id))
		{
			return false;
		}
		string suffix = id.Entry.Substring("CARD_EDITOR_CREATED_CARD".Length);
		return int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out slot)
			&& slot >= 1
			&& slot <= SlotCount;
	}

	private static CardPoolModel GetPoolModel(CardEditorCreatedCardPool pool)
	{
		return pool switch
		{
			CardEditorCreatedCardPool.Silent => ModelDb.CardPool<SilentCardPool>(),
			CardEditorCreatedCardPool.Defect => ModelDb.CardPool<DefectCardPool>(),
			CardEditorCreatedCardPool.Regent => ModelDb.CardPool<RegentCardPool>(),
			CardEditorCreatedCardPool.Necrobinder => ModelDb.CardPool<NecrobinderCardPool>(),
			CardEditorCreatedCardPool.Colorless => ModelDb.CardPool<ColorlessCardPool>(),
			CardEditorCreatedCardPool.Curse => ModelDb.CardPool<CurseCardPool>(),
			CardEditorCreatedCardPool.Status => ModelDb.CardPool<StatusCardPool>(),
			CardEditorCreatedCardPool.Token => ModelDb.CardPool<TokenCardPool>(),
			CardEditorCreatedCardPool.Event => ModelDb.CardPool<EventCardPool>(),
			CardEditorCreatedCardPool.Quest => ModelDb.CardPool<QuestCardPool>(),
			_ => ModelDb.CardPool<IroncladCardPool>()
		};
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

	private static List<ModelId> NormalizeEffectSourceCardIds(ModelId createdCardId, List<ModelId>? effectSourceCardIds)
	{
		if (effectSourceCardIds == null || effectSourceCardIds.Count == 0)
		{
			return new List<ModelId>();
		}

		HashSet<ModelId> seen = new();
		List<ModelId> result = new(effectSourceCardIds.Count);

		foreach (ModelId id in effectSourceCardIds)
		{
			if (id == ModelId.none || id == createdCardId)
			{
				continue;
			}

			if (seen.Add(id))
			{
				result.Add(id);
			}
		}

		return result;
	}

	private sealed class CreatedCardsFileDto
	{
		public int Version { get; set; } = CurrentVersion;
		public DateTime SavedAtUtc { get; set; }
		public int SlotCount { get; set; } = DefaultSlotCount;
		public Dictionary<string, CreatedCardDto> Cards { get; set; } = new Dictionary<string, CreatedCardDto>(StringComparer.Ordinal);
	}

	private static CardEditorCreatedCardDefinition CloneDefinition(CardEditorCreatedCardDefinition? source)
	{
		if (source == null)
		{
			return new CardEditorCreatedCardDefinition();
		}

		return new CardEditorCreatedCardDefinition
		{
			Enabled = source.Enabled,
			Title = source.Title,
			Pool = source.Pool,
			Rarity = source.Rarity,
			Type = source.Type,
			TargetType = source.TargetType,
			FullArt = source.FullArt,
			Finish = source.Finish,
			FinishParams = source.FinishParams != null ? new Dictionary<string, float>(source.FinishParams) : null,
			EffectSourceCardIds = new List<ModelId>(source.EffectSourceCardIds),
			EffectSourcePlacement = source.EffectSourcePlacement,
			PortraitSourceCardId = source.PortraitSourceCardId,
			CustomPortraitFile = source.CustomPortraitFile,
			CustomText = source.CustomText,
			CustomTextUpgraded = source.CustomTextUpgraded,
			Override = source.Override ?? new CardOverride()
		};
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
		public string? EffectSourcePlacement { get; set; }
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
				EffectSourceCardIds = def.EffectSourceCardIds?.Select(id => id.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
				EffectSourcePlacement = def.EffectSourcePlacement.ToString(),
				PortraitSourceCardId = def.PortraitSourceCardId?.ToString(),
				CustomPortraitFile = def.CustomPortraitFile,
				CustomText = def.CustomText,
				CustomTextUpgraded = def.CustomTextUpgraded,
				Override = CardEditorPresetStore.CardOverrideDto.FromOverride(def.Override ?? new CardOverride())
			};
		}

		public CardEditorCreatedCardDefinition ToDefinitionSafe(ModelId cardId)
		{
			CardEditorCreatedCardDefinition def = new CardEditorCreatedCardDefinition
			{
				Enabled = Enabled,
				Title = Title?.Trim() ?? string.Empty,
				FullArt = FullArt,
				Finish = CardEditorVisualFinish.None
			};

			if (!string.IsNullOrWhiteSpace(Pool) && Enum.TryParse(Pool, out CardEditorCreatedCardPool parsedPool))
			{
				def.Pool = parsedPool;
			}

			if (!string.IsNullOrWhiteSpace(Rarity) && Enum.TryParse(Rarity, out CardRarity parsedRarity))
			{
				def.Rarity = parsedRarity;
			}

			if (!string.IsNullOrWhiteSpace(Finish)
				&& Enum.TryParse(Finish, ignoreCase: true, out CardEditorVisualFinish parsedFinish))
			{
				def.Finish = parsedFinish;
			}

			if (FinishParams != null && FinishParams.Count > 0)
			{
				def.FinishParams = FinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
			}

			if (!string.IsNullOrWhiteSpace(Type)
				&& Enum.TryParse(Type, out CardType parsedType)
				&& parsedType != CardType.None)
			{
				def.Type = parsedType;
			}

			if (!string.IsNullOrWhiteSpace(TargetType) && Enum.TryParse(TargetType, out TargetType parsedTarget))
			{
				def.TargetType = parsedTarget;
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

			def.EffectSourceCardIds = NormalizeEffectSourceCardIds(cardId, def.EffectSourceCardIds);

			if (!string.IsNullOrWhiteSpace(EffectSourcePlacement)
				&& Enum.TryParse(EffectSourcePlacement, ignoreCase: true, out CardEditorEffectSourcePlacement parsedPlacement))
			{
				def.EffectSourcePlacement = parsedPlacement;
			}

			if (!string.IsNullOrWhiteSpace(PortraitSourceCardId) && TryParseModelId(PortraitSourceCardId, out ModelId parsedPortraitId))
			{
				def.PortraitSourceCardId = parsedPortraitId;
			}

			if (!string.IsNullOrWhiteSpace(CustomPortraitFile))
			{
				def.CustomPortraitFile = CustomPortraitFile.Trim();
			}

			if (CustomText != null)
			{
				def.CustomText = string.IsNullOrWhiteSpace(CustomText) ? string.Empty : CustomText;
			}

			if (CustomTextUpgraded != null)
			{
				def.CustomTextUpgraded = string.IsNullOrWhiteSpace(CustomTextUpgraded) ? string.Empty : CustomTextUpgraded;
			}

			if (Override != null)
			{
				try
				{
					CardOverride overrideData = Override.ToOverrideSafe(cardId, fileVersion: 3);
					def.Override = overrideData ?? new CardOverride();
				}
				catch
				{
					def.Override = new CardOverride();
				}
			}

			return def;
		}
	}
}
