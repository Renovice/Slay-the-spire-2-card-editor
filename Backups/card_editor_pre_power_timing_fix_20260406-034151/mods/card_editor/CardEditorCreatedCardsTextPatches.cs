using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardModel), "get_Title")]
public static class CardModel_get_Title_CreatedCards_Patch
{
	public static bool Prefix(CardModel __instance, ref string __result)
	{
		if (__instance is not CardEditorCreatedCardBase)
		{
			return true;
		}

		string title = CardEditorCreatedCardsStore.GetTitleForCard(__instance.Id);
		if (!__instance.IsUpgraded)
		{
			__result = title;
			return false;
		}

		if (__instance.MaxUpgradeLevel > 1)
		{
			__result = $"{title}+{__instance.CurrentUpgradeLevel}";
			return false;
		}

		__result = title + "+";
		return false;
	}
}

// NOTE: GetDescriptionForPile patch moved to CardEditorMod.cs to consolidate with the Postfix
// [HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), typeof(PileType), typeof(Creature))]
// public static class CardModel_GetDescriptionForPile_CreatedCards_Patch - DISABLED

[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview))]
public static class CardModel_GetDescriptionForUpgradePreview_CreatedCards_Patch
{
	[HarmonyPriority(Priority.First)]
	public static bool Prefix(CardModel __instance, ref string __result)
	{
		if (__instance is not CardEditorCreatedCardBase)
		{
			return true;
		}

		__result = CreatedCardTextBuilder.Build(__instance, __instance.CurrentTarget, isUpgradePreview: true);
		return false;
	}
}

internal static class CreatedCardTextBuilder
{
	private static readonly LocString _keywordPeriod = new LocString("card_keywords", "PERIOD");

	public static string Build(CardModel card, Creature? target, bool isUpgradePreview)
	{
		if (card == null)
		{
			return string.Empty;
		}

		// Custom text override — user-written text replaces auto-generated description
		bool wantsUpgradedText = isUpgradePreview || card.IsUpgraded;
		if (wantsUpgradedText)
		{
			string? customUpgraded = CardEditorCreatedCardsStore.GetCustomTextUpgraded(card.Id);
			if (!string.IsNullOrWhiteSpace(customUpgraded))
			{
				return customUpgraded;
			}
		}

		string? customText = CardEditorCreatedCardsStore.GetCustomText(card.Id);
		if (!string.IsNullOrWhiteSpace(customText))
		{
			return customText;
		}

		List<string> lines = new List<string>();

		string description = string.Empty;
		List<string> sourceDescriptions = CardEditorCreatedCardEffectSourceSupport.GetEffectSourceDescriptions(card, target, isUpgradePreview);
		foreach (string sourceDescription in sourceDescriptions)
		{
			if (!string.IsNullOrWhiteSpace(sourceDescription))
			{
				string fixedDesc = sourceDescription;
				TargetTypeOverrideDescriptionFix.TryFix(card, ref fixedDesc);
				lines.AddRange(fixedDesc.Split('\n').Where(l => !string.IsNullOrEmpty(l)));
			}
		}

		CardEditorExtraEffects.TryAppendDescription(card, ref description, target, isUpgradePreview);
		if (!string.IsNullOrWhiteSpace(description))
		{
			description = CardEditorVanillaKeywordSupport.FormatDescription(description);
			lines.AddRange(description.Split('\n').Where(l => !string.IsNullOrEmpty(l)));
		}

		try
		{
			LocString? enchantText = card.Enchantment?.DynamicExtraCardText;
			if (enchantText != null)
			{
				lines.Add("[purple]" + enchantText.GetFormattedText() + "[/purple]");
			}
		}
		catch
		{
		}

		try
		{
			LocString? afflictText = card.Affliction?.DynamicExtraCardText;
			if (afflictText != null)
			{
				lines.Add("[purple]" + afflictText.GetFormattedText() + "[/purple]");
			}
		}
		catch
		{
		}

		HashSet<CardKeyword> keywords = new HashSet<CardKeyword>(card.Keywords);
		foreach (CardKeyword cardKeyword in CardKeywordOrder.beforeDescription)
		{
			bool include = cardKeyword switch
			{
				CardKeyword.Sly => card.IsSlyThisTurn,
				CardKeyword.Retain => card.ShouldRetainThisTurn,
				_ => keywords.Contains(cardKeyword),
			};
			if (include)
			{
				lines.Insert(0, GetKeywordCardText(cardKeyword));
			}
		}

		int enchantedReplayCount = card.GetEnchantedReplayCount();
		if (enchantedReplayCount > 0)
		{
			try
			{
				LocString replay = new LocString("static_hover_tips", "REPLAY.extraText");
				replay.Add("Times", enchantedReplayCount);
				lines.Add(replay.GetFormattedText() ?? string.Empty);
			}
			catch
			{
			}
		}

		foreach (CardKeyword cardKeyword in CardKeywordOrder.afterDescription)
		{
			if (keywords.Contains(cardKeyword))
			{
				lines.Add(GetKeywordCardText(cardKeyword));
			}
		}

		return string.Join('\n', lines.Where(l => !string.IsNullOrEmpty(l)));
	}

	private static string GetKeywordCardText(CardKeyword keyword)
	{
		try
		{
			string prefix = StringHelper.Slugify(keyword.ToString());
			LocString title = new LocString("card_keywords", prefix + ".title");
			return "[gold]" + title.GetFormattedText() + "[/gold]" + _keywordPeriod.GetRawText();
		}
		catch
		{
			return "[gold]" + keyword + "[/gold].";
		}
	}
}
