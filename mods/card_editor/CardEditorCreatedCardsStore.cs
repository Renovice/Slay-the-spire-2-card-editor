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

internal enum CardEditorRewardPoolBucket
{
	SameAsCard = 0,
	Common = 1,
	Uncommon = 2,
	Rare = 3,
	Event = 4
}

internal enum CardEditorRewardPoolInjectionMode
{
	AddToPool = 0,
	ForceInclude = 1,
	ReplacePool = 2
}

internal sealed class CardEditorCreatedCardDefinition
{
	public bool Enabled { get; set; }
	public string Title { get; set; } = string.Empty;
	public CardEditorCreatedCardPool Pool { get; set; } = CardEditorCreatedCardPool.Ironclad;
	public string? PoolTitle { get; set; }
	public CardRarity Rarity { get; set; } = CardRarity.Common;
	public CardType Type { get; set; } = CardType.Attack;
	public TargetType TargetType { get; set; } = TargetType.AnyEnemy;
	public bool FullArt { get; set; }
	public CardEditorVisualFinish Finish { get; set; }
	public Dictionary<string, float>? FinishParams { get; set; }
	public string? CustomFinishId { get; set; }
	public Dictionary<string, string>? CustomFinishParams { get; set; }
	public CardEditorVisualFinish BorderFinish { get; set; }
	public Dictionary<string, float>? BorderFinishParams { get; set; }
	public List<ModelId> EffectSourceCardIds { get; set; } = new();
	public CardEditorEffectSourcePlacement EffectSourcePlacement { get; set; } = CardEditorEffectSourcePlacement.BeforeCustomEffects;
	public ModelId? PortraitSourceCardId { get; set; }
	public string? CustomPortraitFile { get; set; }
	public bool CustomTextEnabled { get; set; }
	public string? CustomText { get; set; }
	public bool CustomTextUpgradedEnabled { get; set; }
	public string? CustomTextUpgraded { get; set; }
	public bool CustomUpgradePreviewTextEnabled { get; set; }
	public string? CustomUpgradePreviewText { get; set; }
	public bool CustomRewardPoolsEnabled { get; set; }
	public List<string> CustomRewardPoolIds { get; set; } = new();
	public CardEditorRewardPoolBucket RewardPoolBucket { get; set; } = CardEditorRewardPoolBucket.SameAsCard;
	public CardEditorRewardPoolInjectionMode RewardPoolInjectionMode { get; set; } = CardEditorRewardPoolInjectionMode.AddToPool;
	public CardOverride Override { get; set; } = new CardOverride();
}

internal static class CardEditorCreatedCardsStore
{
	private const int CurrentVersion = 3;
	public const int DefaultSlotCount = 50;
	public const int MaxSlotCount = 500;
	public static int SlotCount { get; private set; } = DefaultSlotCount;
	public static int ConfiguredSlotCount { get; private set; } = DefaultSlotCount;
	private const string CardArtFolderName = "card art";
	private static readonly ModelId DefaultPortraitSourceCardId = new("CARD", "STRIKE_IRONCLAD");

	private static readonly Dictionary<ModelId, CardEditorCreatedCardDefinition> _definitions = new();
	private static readonly Dictionary<ModelId, CardEditorCreatedCardDefinition> _draftDefinitions = new();
	private static bool _loaded;
	private static readonly object _lock = new();

	public static int Revision { get; private set; }
	internal static bool PersistenceSuspended { get; set; }

	public static void SetDraftMeta(ModelId cardId, bool enabled, string? title, CardEditorCreatedCardPool pool, string? poolTitle, CardRarity rarity, CardType type, TargetType targetType, List<ModelId>? effectSourceCardIds, CardEditorEffectSourcePlacement effectSourcePlacement, ModelId? portraitSourceCardId, string? customPortraitFile, bool fullArt, CardEditorVisualFinish finish, string? customText, bool customTextEnabled, Dictionary<string, float>? finishParams = null, bool customRewardPoolsEnabled = false, List<string>? customRewardPoolIds = null, CardEditorRewardPoolBucket rewardPoolBucket = CardEditorRewardPoolBucket.SameAsCard, CardEditorRewardPoolInjectionMode rewardPoolInjectionMode = CardEditorRewardPoolInjectionMode.AddToPool, string? customFinishId = null, Dictionary<string, string>? customFinishParams = null, CardEditorVisualFinish borderFinish = CardEditorVisualFinish.None, Dictionary<string, float>? borderFinishParams = null)
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
				PoolTitle = persistent.PoolTitle,
				Rarity = persistent.Rarity,
				Type = persistent.Type,
				TargetType = persistent.TargetType,
				FullArt = persistent.FullArt,
				Finish = persistent.Finish,
				FinishParams = persistent.FinishParams != null ? new Dictionary<string, float>(persistent.FinishParams) : null,
				CustomFinishId = persistent.CustomFinishId,
				CustomFinishParams = persistent.CustomFinishParams != null ? new Dictionary<string, string>(persistent.CustomFinishParams, StringComparer.Ordinal) : null,
				BorderFinish = persistent.BorderFinish,
				BorderFinishParams = persistent.BorderFinishParams != null ? new Dictionary<string, float>(persistent.BorderFinishParams) : null,
				EffectSourceCardIds = new List<ModelId>(persistent.EffectSourceCardIds),
				EffectSourcePlacement = persistent.EffectSourcePlacement,
				PortraitSourceCardId = persistent.PortraitSourceCardId,
				CustomPortraitFile = persistent.CustomPortraitFile,
				CustomTextEnabled = persistent.CustomTextEnabled,
				CustomText = persistent.CustomText,
				CustomTextUpgradedEnabled = persistent.CustomTextUpgradedEnabled,
				CustomTextUpgraded = persistent.CustomTextUpgraded,
				CustomUpgradePreviewTextEnabled = persistent.CustomUpgradePreviewTextEnabled,
				CustomUpgradePreviewText = persistent.CustomUpgradePreviewText,
				CustomRewardPoolsEnabled = persistent.CustomRewardPoolsEnabled,
				CustomRewardPoolIds = new List<string>(persistent.CustomRewardPoolIds),
				RewardPoolBucket = persistent.RewardPoolBucket,
				RewardPoolInjectionMode = persistent.RewardPoolInjectionMode,
				Override = persistent.Override
			};
			_draftDefinitions[cardId] = draft;
		}

		draft.Enabled = enabled;
		draft.Title = title?.Trim() ?? string.Empty;
		draft.Pool = pool;
		draft.PoolTitle = ResolvePoolTitleForStorage(pool, poolTitle);
		if (TryParsePoolTitleAsLegacyEnum(draft.PoolTitle, out CardEditorCreatedCardPool parsedPool))
		{
			draft.Pool = parsedPool;
		}
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
		draft.CustomFinishId = string.IsNullOrWhiteSpace(customFinishId) ? null : customFinishId.Trim();
		draft.CustomFinishParams = customFinishParams != null && customFinishParams.Count > 0
			? new Dictionary<string, string>(customFinishParams, StringComparer.Ordinal)
			: null;
		draft.BorderFinish = borderFinish;
		draft.BorderFinishParams = borderFinishParams != null ? new Dictionary<string, float>(borderFinishParams) : null;
		draft.CustomTextEnabled = customTextEnabled;
		draft.CustomText = customText == null ? null : (string.IsNullOrWhiteSpace(customText) ? string.Empty : customText);
		draft.CustomRewardPoolsEnabled = customRewardPoolsEnabled;
		draft.CustomRewardPoolIds = NormalizeRewardPoolIds(customRewardPoolIds);
		draft.RewardPoolBucket = rewardPoolBucket;
		draft.RewardPoolInjectionMode = rewardPoolInjectionMode;
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
			return ResolvePoolModel(def);
		}
		return GetDefaultPoolModel();
	}

	internal static string GetResolvedPoolTitle(CardEditorCreatedCardDefinition? definition)
	{
		return ResolvePoolModel(definition).Title;
	}

	public static IReadOnlyList<string> GetAvailablePoolTitles()
	{
		try
		{
			return ModelDb.AllCardPools
				.Where(pool => pool != null && !string.IsNullOrWhiteSpace(pool.Title))
				.Select(pool => pool.Title.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
		catch
		{
			return new List<string> { GetPoolTitleFallback(CardEditorCreatedCardPool.Ironclad) };
		}
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

		string resolvedTitle = GetResolvedPoolTitle(def);
		IReadOnlyList<string> availableTitles = GetAvailablePoolTitles();
		for (int i = 0; i < availableTitles.Count; i++)
		{
			if (string.Equals(availableTitles[i], resolvedTitle, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return int.MaxValue;
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

	public static string? GetCustomFinishId(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.CustomFinishId : null;
	}

	public static Dictionary<string, string>? GetCustomFinishParams(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.CustomFinishParams : null;
	}

	public static CardEditorVisualFinish GetBorderFinish(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.BorderFinish : CardEditorVisualFinish.None;
	}

	public static Dictionary<string, float>? GetBorderFinishParams(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.BorderFinishParams : null;
	}

	public static string? GetCustomPortraitFile(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.CustomPortraitFile : null;
	}

	public static ModelId? GetPortraitSourceCardId(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.PortraitSourceCardId : null;
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
				".webp",
				".gif"
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
		Revision++;
		Save();
	}

	public static void SetMeta(ModelId cardId, string? title, CardEditorCreatedCardPool pool, string? poolTitle, CardRarity rarity, CardType type, TargetType targetType, List<ModelId>? effectSourceCardIds, ModelId? portraitSourceCardId, string? customPortraitFile, bool fullArt, CardEditorVisualFinish finish, string? customText, bool customTextEnabled, Dictionary<string, float>? finishParams = null, bool customRewardPoolsEnabled = false, List<string>? customRewardPoolIds = null, CardEditorRewardPoolBucket rewardPoolBucket = CardEditorRewardPoolBucket.SameAsCard, CardEditorRewardPoolInjectionMode rewardPoolInjectionMode = CardEditorRewardPoolInjectionMode.AddToPool, string? customFinishId = null, Dictionary<string, string>? customFinishParams = null, CardEditorVisualFinish borderFinish = CardEditorVisualFinish.None, Dictionary<string, float>? borderFinishParams = null)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}
		def.Title = title?.Trim() ?? string.Empty;
		def.Pool = pool;
		def.PoolTitle = ResolvePoolTitleForStorage(pool, poolTitle);
		if (TryParsePoolTitleAsLegacyEnum(def.PoolTitle, out CardEditorCreatedCardPool parsedPool))
		{
			def.Pool = parsedPool;
		}
		def.Rarity = rarity;
		def.Type = type;
		def.TargetType = targetType;
		def.EffectSourceCardIds = NormalizeEffectSourceCardIds(cardId, effectSourceCardIds);
		def.PortraitSourceCardId = portraitSourceCardId;
		def.CustomPortraitFile = string.IsNullOrWhiteSpace(customPortraitFile) ? null : customPortraitFile.Trim();
		def.FullArt = fullArt;
		def.Finish = finish;
		def.FinishParams = finishParams != null ? new Dictionary<string, float>(finishParams) : null;
		def.CustomFinishId = string.IsNullOrWhiteSpace(customFinishId) ? null : customFinishId.Trim();
		def.CustomFinishParams = customFinishParams != null && customFinishParams.Count > 0
			? new Dictionary<string, string>(customFinishParams, StringComparer.Ordinal)
			: null;
		def.BorderFinish = borderFinish;
		def.BorderFinishParams = borderFinishParams != null ? new Dictionary<string, float>(borderFinishParams) : null;
		def.CustomTextEnabled = customTextEnabled;
		def.CustomText = customText == null ? null : (string.IsNullOrWhiteSpace(customText) ? string.Empty : customText);
		def.CustomRewardPoolsEnabled = customRewardPoolsEnabled;
		def.CustomRewardPoolIds = NormalizeRewardPoolIds(customRewardPoolIds);
		def.RewardPoolBucket = rewardPoolBucket;
		def.RewardPoolInjectionMode = rewardPoolInjectionMode;
		Revision++;
		Save();
	}

	public static bool AreCustomRewardPoolsEnabled(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.CustomRewardPoolsEnabled;
	}

	public static IReadOnlyList<string> GetCustomRewardPoolIds(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.CustomRewardPoolIds : Array.Empty<string>();
	}

	public static CardEditorRewardPoolBucket GetRewardPoolBucket(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.RewardPoolBucket : CardEditorRewardPoolBucket.SameAsCard;
	}

	public static CardEditorRewardPoolInjectionMode GetRewardPoolInjectionMode(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) ? def.RewardPoolInjectionMode : CardEditorRewardPoolInjectionMode.AddToPool;
	}

	public static string? GetCustomText(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomTextEnabled ? def.CustomText : null;
		}
		return null;
	}

	public static string? GetStoredCustomText(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomText;
		}
		return null;
	}

	public static bool IsCustomTextEnabled(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.CustomTextEnabled;
	}

	public static string? GetCustomTextUpgraded(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomTextUpgradedEnabled ? def.CustomTextUpgraded : null;
		}
		return null;
	}

	public static string? GetStoredCustomTextUpgraded(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomTextUpgraded;
		}
		return null;
	}

	public static bool IsCustomTextUpgradedEnabled(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.CustomTextUpgradedEnabled;
	}

	// Preview-only upgrade text: shown ONLY on the upgrade screen (bypasses the auto green-diff so the
	// user controls the markup); never appears on the played upgraded card.
	public static string? GetCustomUpgradePreviewText(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomUpgradePreviewTextEnabled ? def.CustomUpgradePreviewText : null;
		}
		return null;
	}

	public static string? GetStoredCustomUpgradePreviewText(ModelId cardId)
	{
		EnsureLoaded();
		if (TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def))
		{
			return def.CustomUpgradePreviewText;
		}
		return null;
	}

	public static bool IsCustomUpgradePreviewTextEnabled(ModelId cardId)
	{
		EnsureLoaded();
		return TryGetEffectiveDefinition(cardId, out CardEditorCreatedCardDefinition? def) && def.CustomUpgradePreviewTextEnabled;
	}

	public static void SetDraftCustomTextUpgraded(ModelId cardId, string? customTextUpgraded, bool enabled)
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
				PoolTitle = persistent.PoolTitle,
				Rarity = persistent.Rarity,
				Type = persistent.Type,
				TargetType = persistent.TargetType,
				FullArt = persistent.FullArt,
			Finish = persistent.Finish,
			FinishParams = persistent.FinishParams != null ? new Dictionary<string, float>(persistent.FinishParams) : null,
			CustomFinishId = persistent.CustomFinishId,
			CustomFinishParams = persistent.CustomFinishParams != null ? new Dictionary<string, string>(persistent.CustomFinishParams, StringComparer.Ordinal) : null,
			BorderFinish = persistent.BorderFinish,
			BorderFinishParams = persistent.BorderFinishParams != null ? new Dictionary<string, float>(persistent.BorderFinishParams) : null,
			EffectSourceCardIds = new List<ModelId>(persistent.EffectSourceCardIds),
				EffectSourcePlacement = persistent.EffectSourcePlacement,
				PortraitSourceCardId = persistent.PortraitSourceCardId,
				CustomPortraitFile = persistent.CustomPortraitFile,
				CustomTextEnabled = persistent.CustomTextEnabled,
				CustomText = persistent.CustomText,
				CustomTextUpgradedEnabled = persistent.CustomTextUpgradedEnabled,
				CustomTextUpgraded = persistent.CustomTextUpgraded,
				CustomUpgradePreviewTextEnabled = persistent.CustomUpgradePreviewTextEnabled,
				CustomUpgradePreviewText = persistent.CustomUpgradePreviewText,
				CustomRewardPoolsEnabled = persistent.CustomRewardPoolsEnabled,
				CustomRewardPoolIds = new List<string>(persistent.CustomRewardPoolIds),
				RewardPoolBucket = persistent.RewardPoolBucket,
				RewardPoolInjectionMode = persistent.RewardPoolInjectionMode,
				Override = persistent.Override
			};
			_draftDefinitions[cardId] = draft;
		}

		draft.CustomTextUpgradedEnabled = enabled;
		draft.CustomTextUpgraded = customTextUpgraded == null ? null : (string.IsNullOrWhiteSpace(customTextUpgraded) ? string.Empty : customTextUpgraded);
	}

	public static void SetCustomTextUpgraded(ModelId cardId, string? customTextUpgraded, bool enabled)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}

		def.CustomTextUpgradedEnabled = enabled;
		def.CustomTextUpgraded = customTextUpgraded == null ? null : (string.IsNullOrWhiteSpace(customTextUpgraded) ? string.Empty : customTextUpgraded);
		Revision++;
		Save();
	}

	public static void SetDraftCustomUpgradePreviewText(ModelId cardId, string? text, bool enabled)
	{
		EnsureLoaded();
		if (!_draftDefinitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? draft))
		{
			_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? persistent);
			draft = CloneDefinition(persistent);
			_draftDefinitions[cardId] = draft;
		}

		draft.CustomUpgradePreviewTextEnabled = enabled;
		draft.CustomUpgradePreviewText = text == null ? null : (string.IsNullOrWhiteSpace(text) ? string.Empty : text);
	}

	public static void SetCustomUpgradePreviewText(ModelId cardId, string? text, bool enabled)
	{
		EnsureLoaded();
		if (!_definitions.TryGetValue(cardId, out CardEditorCreatedCardDefinition? def))
		{
			def = new CardEditorCreatedCardDefinition();
			_definitions[cardId] = def;
		}

		def.CustomUpgradePreviewTextEnabled = enabled;
		def.CustomUpgradePreviewText = text == null ? null : (string.IsNullOrWhiteSpace(text) ? string.Empty : text);
		Revision++;
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
		Revision++;
		Save();

		// The canonical instance's vanilla-parity DynamicVars (Damage/Block mirrors for Thrash-style
		// readers) were derived from the previous definition; drop the cached set so the next access
		// re-derives, and so new run instances clone fresh values.
		try
		{
			CardEditorExtraEffects.InvalidateVanillaParityVars(ModelDb.GetByIdOrNull<CardModel>(cardId));
		}
		catch
		{
		}
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
		bool preserveUnreadFile = false;
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
				else
				{
					preserveUnreadFile = true;
					string reason = file == null
						? "file could not be parsed"
						: file.Cards == null
							? "file has no card data"
							: $"file version {file.Version} is outside the supported range (1..{CurrentVersion})";
					Log.Warn($"[CardEditor] created_cards.json not loaded ({reason}); leaving the file untouched.");
				}
			}

			SlotCount = Math.Clamp(desiredSlotCount, 1, MaxSlotCount);
			ConfiguredSlotCount = SlotCount;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading created cards store: {ex}");
			preserveUnreadFile = true;
			_definitions.Clear();
			SlotCount = DefaultSlotCount;
			ConfiguredSlotCount = DefaultSlotCount;
		}

		EnsureDefaultDefinitions();
		if (preserveUnreadFile)
		{
			// Auto-saving here would overwrite the user's cards with defaults. Keep the file as-is and
			// stash a one-time backup so a later save cannot silently destroy the original data.
			TryBackupUnreadStoreFile();
			return;
		}
		Save();
	}

	private static void TryBackupUnreadStoreFile()
	{
		try
		{
			string path = GetStorePath();
			string backupPath = path + ".load-failed.bak";
			if (File.Exists(path) && !File.Exists(backupPath))
			{
				File.Copy(path, backupPath, overwrite: false);
				Log.Warn($"[CardEditor] Backed up unreadable created cards store to {backupPath}");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Could not back up created cards store: {ex}");
		}
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
		Revision++;
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
		Revision++;
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
					PoolTitle = GetPoolTitleFallback(CardEditorCreatedCardPool.Ironclad),
					Rarity = CardRarity.Common,
					Type = CardType.Attack,
					TargetType = TargetType.AnyEnemy,
					EffectSourceCardIds = new List<ModelId>(),
					PortraitSourceCardId = DefaultPortraitSourceCardId,
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
		if (PersistenceSuspended)
		{
			return;
		}

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
		Revision++;
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

	private static CardPoolModel GetDefaultPoolModel()
	{
		if (TryGetPoolModel(CardEditorCreatedCardPool.Ironclad, out CardPoolModel pool))
		{
			return pool;
		}

		try
		{
			CardPoolModel? first = ModelDb.AllCardPools.FirstOrDefault(poolModel => poolModel != null);
			if (first != null)
			{
				return first;
			}
		}
		catch
		{
		}

		return GetPoolModel(CardEditorCreatedCardPool.Ironclad);
	}

	private static CardPoolModel ResolvePoolModel(CardEditorCreatedCardDefinition? definition)
	{
		if (definition != null && TryGetPoolModelByTitle(definition.PoolTitle, out CardPoolModel resolvedByTitle))
		{
			return resolvedByTitle;
		}

		if (definition != null && TryGetPoolModel(definition.Pool, out CardPoolModel resolvedByEnum))
		{
			return resolvedByEnum;
		}

		return GetDefaultPoolModel();
	}

	private static string ResolvePoolTitleForStorage(CardEditorCreatedCardPool pool, string? poolTitle)
	{
		string? normalized = NormalizePoolTitle(poolTitle);
		if (!string.IsNullOrWhiteSpace(normalized))
		{
			return normalized;
		}

		if (TryGetPoolModel(pool, out CardPoolModel resolved) && !string.IsNullOrWhiteSpace(resolved.Title))
		{
			return resolved.Title.Trim();
		}

		return GetPoolTitleFallback(pool);
	}

	private static string GetPoolTitleFallback(CardEditorCreatedCardPool pool)
	{
		return pool switch
		{
			CardEditorCreatedCardPool.Silent => "Silent",
			CardEditorCreatedCardPool.Defect => "Defect",
			CardEditorCreatedCardPool.Regent => "Regent",
			CardEditorCreatedCardPool.Necrobinder => "Necrobinder",
			CardEditorCreatedCardPool.Colorless => "Colorless",
			CardEditorCreatedCardPool.Curse => "Curse",
			CardEditorCreatedCardPool.Status => "Status",
			CardEditorCreatedCardPool.Token => "Token",
			CardEditorCreatedCardPool.Event => "Event",
			CardEditorCreatedCardPool.Quest => "Quest",
			_ => "Ironclad"
		};
	}

	private static string? NormalizePoolTitle(string? poolTitle)
	{
		return string.IsNullOrWhiteSpace(poolTitle) ? null : poolTitle.Trim();
	}

	private static bool TryGetPoolModelByTitle(string? poolTitle, out CardPoolModel pool)
	{
		string? normalized = NormalizePoolTitle(poolTitle);
		if (!string.IsNullOrWhiteSpace(normalized))
		{
			try
			{
				CardPoolModel? resolved = ModelDb.AllCardPools.FirstOrDefault(candidate =>
					candidate != null && string.Equals(candidate.Title, normalized, StringComparison.OrdinalIgnoreCase));
				if (resolved != null)
				{
					pool = resolved;
					return true;
				}
			}
			catch
			{
			}
		}

		pool = null!;
		return false;
	}

	private static bool TryParsePoolTitleAsLegacyEnum(string? poolTitle, out CardEditorCreatedCardPool pool)
	{
		string? normalized = NormalizePoolTitle(poolTitle);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			pool = CardEditorCreatedCardPool.Ironclad;
			return false;
		}

		foreach (CardEditorCreatedCardPool candidate in Enum.GetValues<CardEditorCreatedCardPool>())
		{
			if (string.Equals(GetPoolTitleFallback(candidate), normalized, StringComparison.OrdinalIgnoreCase))
			{
				pool = candidate;
				return true;
			}

			if (TryGetPoolModel(candidate, out CardPoolModel model)
				&& string.Equals(model.Title, normalized, StringComparison.OrdinalIgnoreCase))
			{
				pool = candidate;
				return true;
			}
		}

		if (Enum.TryParse(normalized, ignoreCase: true, out pool))
		{
			return true;
		}

		pool = CardEditorCreatedCardPool.Ironclad;
		return false;
	}

	private static bool TryGetPoolModel(CardEditorCreatedCardPool pool, out CardPoolModel model)
	{
		try
		{
			model = GetPoolModel(pool);
			return model != null;
		}
		catch
		{
			model = null!;
			return false;
		}
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

	private static List<string> NormalizeRewardPoolIds(IEnumerable<string>? rewardPoolIds)
	{
		List<string> result = new();
		if (rewardPoolIds == null)
		{
			return result;
		}

		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		foreach (string? rawId in rewardPoolIds)
		{
			string? id = rawId?.Trim();
			if (string.IsNullOrWhiteSpace(id) || !CardEditorRewardPoolRegistry.IsKnownPoolId(id))
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
			PoolTitle = source.PoolTitle,
			Rarity = source.Rarity,
			Type = source.Type,
			TargetType = source.TargetType,
			FullArt = source.FullArt,
			Finish = source.Finish,
			FinishParams = source.FinishParams != null ? new Dictionary<string, float>(source.FinishParams) : null,
			CustomFinishId = source.CustomFinishId,
			CustomFinishParams = source.CustomFinishParams != null ? new Dictionary<string, string>(source.CustomFinishParams, StringComparer.Ordinal) : null,
			BorderFinish = source.BorderFinish,
			BorderFinishParams = source.BorderFinishParams != null ? new Dictionary<string, float>(source.BorderFinishParams) : null,
			EffectSourceCardIds = new List<ModelId>(source.EffectSourceCardIds),
			EffectSourcePlacement = source.EffectSourcePlacement,
			PortraitSourceCardId = source.PortraitSourceCardId,
			CustomPortraitFile = source.CustomPortraitFile,
			CustomTextEnabled = source.CustomTextEnabled,
			CustomText = source.CustomText,
			CustomTextUpgradedEnabled = source.CustomTextUpgradedEnabled,
			CustomTextUpgraded = source.CustomTextUpgraded,
			CustomUpgradePreviewTextEnabled = source.CustomUpgradePreviewTextEnabled,
			CustomUpgradePreviewText = source.CustomUpgradePreviewText,
			CustomRewardPoolsEnabled = source.CustomRewardPoolsEnabled,
			CustomRewardPoolIds = new List<string>(source.CustomRewardPoolIds),
			RewardPoolBucket = source.RewardPoolBucket,
			RewardPoolInjectionMode = source.RewardPoolInjectionMode,
			Override = source.Override ?? new CardOverride()
		};
	}

	internal sealed class CreatedCardDto
	{
		public bool Enabled { get; set; }
		public string? Title { get; set; }
		public string? Pool { get; set; }
		public string? PoolTitle { get; set; }
		public string? Rarity { get; set; }
		public string? Type { get; set; }
		public string? TargetType { get; set; }
		public bool FullArt { get; set; }
		public string? Finish { get; set; }
		public Dictionary<string, decimal>? FinishParams { get; set; }
		public string? CustomFinishId { get; set; }
		public Dictionary<string, string>? CustomFinishParams { get; set; }
		public string? BorderFinish { get; set; }
		public Dictionary<string, decimal>? BorderFinishParams { get; set; }
		public string? EffectSourceCardId { get; set; }
		public List<string>? EffectSourceCardIds { get; set; }
		public string? EffectSourcePlacement { get; set; }
		public string? PortraitSourceCardId { get; set; }
		public string? CustomPortraitFile { get; set; }
		public bool? CustomTextEnabled { get; set; }
		public string? CustomText { get; set; }
		public bool? CustomTextUpgradedEnabled { get; set; }
		public string? CustomTextUpgraded { get; set; }
		public bool? CustomUpgradePreviewTextEnabled { get; set; }
		public string? CustomUpgradePreviewText { get; set; }
		public bool? CustomRewardPoolsEnabled { get; set; }
		public List<string>? CustomRewardPoolIds { get; set; }
		public string? RewardPoolBucket { get; set; }
		public string? RewardPoolInjectionMode { get; set; }
		public CardEditorPresetStore.CardOverrideDto? Override { get; set; }

		public static CreatedCardDto FromDefinition(CardEditorCreatedCardDefinition def)
		{
			return new CreatedCardDto
			{
				Enabled = def.Enabled,
				Title = def.Title,
				Pool = def.Pool.ToString(),
				PoolTitle = def.PoolTitle,
				Rarity = def.Rarity.ToString(),
				Type = def.Type.ToString(),
				TargetType = def.TargetType.ToString(),
				FullArt = def.FullArt,
				Finish = def.Finish.ToString(),
				FinishParams = def.FinishParams != null && def.FinishParams.Count > 0
					? def.FinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
				CustomFinishId = def.CustomFinishId,
				CustomFinishParams = def.CustomFinishParams != null && def.CustomFinishParams.Count > 0
					? new Dictionary<string, string>(def.CustomFinishParams, StringComparer.Ordinal)
					: null,
				BorderFinish = def.BorderFinish != CardEditorVisualFinish.None ? def.BorderFinish.ToString() : null,
				BorderFinishParams = def.BorderFinishParams != null && def.BorderFinishParams.Count > 0
					? def.BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (decimal)kvp.Value)
					: null,
				EffectSourceCardIds = def.EffectSourceCardIds?.Select(id => id.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
				EffectSourcePlacement = def.EffectSourcePlacement.ToString(),
				PortraitSourceCardId = def.PortraitSourceCardId?.ToString(),
				CustomPortraitFile = def.CustomPortraitFile,
				CustomTextEnabled = def.CustomTextEnabled,
				CustomText = def.CustomText,
				CustomTextUpgradedEnabled = def.CustomTextUpgradedEnabled,
				CustomTextUpgraded = def.CustomTextUpgraded,
				CustomUpgradePreviewTextEnabled = def.CustomUpgradePreviewTextEnabled,
				CustomUpgradePreviewText = def.CustomUpgradePreviewText,
				CustomRewardPoolsEnabled = def.CustomRewardPoolsEnabled,
				CustomRewardPoolIds = def.CustomRewardPoolIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
				RewardPoolBucket = def.RewardPoolBucket.ToString(),
				RewardPoolInjectionMode = def.RewardPoolInjectionMode.ToString(),
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

			def.PoolTitle = NormalizePoolTitle(PoolTitle);

			if (!string.IsNullOrWhiteSpace(Pool) && Enum.TryParse(Pool, out CardEditorCreatedCardPool parsedPool))
			{
				def.Pool = parsedPool;
			}
			else if (TryParsePoolTitleAsLegacyEnum(def.PoolTitle, out CardEditorCreatedCardPool parsedPoolFromTitle))
			{
				def.Pool = parsedPoolFromTitle;
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

			if (!string.IsNullOrWhiteSpace(CustomFinishId))
			{
				def.CustomFinishId = CustomFinishId.Trim();
			}

			if (CustomFinishParams != null && CustomFinishParams.Count > 0)
			{
				def.CustomFinishParams = new Dictionary<string, string>(CustomFinishParams, StringComparer.Ordinal);
			}

			if (!string.IsNullOrWhiteSpace(BorderFinish)
				&& Enum.TryParse(BorderFinish, ignoreCase: true, out CardEditorVisualFinish parsedBorderFinish))
			{
				def.BorderFinish = parsedBorderFinish;
			}

			if (BorderFinishParams != null && BorderFinishParams.Count > 0)
			{
				def.BorderFinishParams = BorderFinishParams.ToDictionary(kvp => kvp.Key, kvp => (float)kvp.Value);
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

			def.CustomTextEnabled = CustomTextEnabled ?? (CustomText != null);
			def.CustomTextUpgradedEnabled = CustomTextUpgradedEnabled ?? (CustomTextUpgraded != null);

			if (CustomText != null)
			{
				def.CustomText = string.IsNullOrWhiteSpace(CustomText) ? string.Empty : CustomText;
			}

			if (CustomTextUpgraded != null)
			{
				def.CustomTextUpgraded = string.IsNullOrWhiteSpace(CustomTextUpgraded) ? string.Empty : CustomTextUpgraded;
			}

			def.CustomUpgradePreviewTextEnabled = CustomUpgradePreviewTextEnabled ?? (CustomUpgradePreviewText != null);
			if (CustomUpgradePreviewText != null)
			{
				def.CustomUpgradePreviewText = string.IsNullOrWhiteSpace(CustomUpgradePreviewText) ? string.Empty : CustomUpgradePreviewText;
			}

			def.CustomRewardPoolsEnabled = CustomRewardPoolsEnabled ?? false;
			def.CustomRewardPoolIds = NormalizeRewardPoolIds(CustomRewardPoolIds);

			if (!string.IsNullOrWhiteSpace(RewardPoolBucket)
				&& Enum.TryParse(RewardPoolBucket, ignoreCase: true, out CardEditorRewardPoolBucket parsedBucket))
			{
				def.RewardPoolBucket = parsedBucket;
			}

			if (!string.IsNullOrWhiteSpace(RewardPoolInjectionMode)
				&& Enum.TryParse(RewardPoolInjectionMode, ignoreCase: true, out CardEditorRewardPoolInjectionMode parsedMode))
			{
				def.RewardPoolInjectionMode = parsedMode;
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
