using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
			if (customUpgraded != null)
			{
				string rendered = CardEditorVanillaKeywordSupport.FormatDescription(card, customUpgraded, target, isUpgradePreview);
				return isUpgradePreview ? ApplyUpgradePreviewNumericHighlight(card, target, rendered) : rendered;
			}
		}

		string? customText = CardEditorCreatedCardsStore.GetCustomText(card.Id);
		if (customText != null)
		{
			string rendered = CardEditorVanillaKeywordSupport.FormatDescription(card, customText, target, isUpgradePreview);
			return isUpgradePreview ? ApplyUpgradePreviewNumericHighlight(card, target, rendered) : rendered;
		}

		List<string> lines = new List<string>();

		bool hasInlineEffectSources = CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview)
			.Any(e => e != null && e.Kind == CardExtraEffectKind.RunEffectSourceCard);

		List<string> sourceLines = new List<string>();
		if (!hasInlineEffectSources)
		{
			List<string> sourceDescriptions = CardEditorCreatedCardEffectSourceSupport.GetEffectSourceDescriptions(card, target, isUpgradePreview);
			foreach (string sourceDescription in sourceDescriptions)
			{
				if (!string.IsNullOrWhiteSpace(sourceDescription))
				{
					string fixedDesc = sourceDescription;
					TargetTypeOverrideDescriptionFix.TryFix(card, ref fixedDesc);
					sourceLines.AddRange(fixedDesc.Split('\n').Where(l => !string.IsNullOrEmpty(l)));
				}
			}
		}

		List<string> customLines = new List<string>();
		string customDescription = string.Empty;
		CardEditorExtraEffects.TryAppendDescription(card, ref customDescription, target, isUpgradePreview);
		if (!string.IsNullOrWhiteSpace(customDescription))
		{
			customDescription = CardEditorVanillaKeywordSupport.FormatDescription(customDescription);
			customLines.AddRange(customDescription.Split('\n').Where(l => !string.IsNullOrEmpty(l)));
		}

		if (CardEditorCreatedCardsStore.GetEffectSourcePlacement(card.Id) == CardEditorEffectSourcePlacement.AfterCustomEffects)
		{
			lines.AddRange(customLines);
			lines.AddRange(sourceLines);
		}
		else
		{
			lines.AddRange(sourceLines);
			lines.AddRange(customLines);
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

		string description = string.Join('\n', lines.Where(l => !string.IsNullOrEmpty(l)));
		return isUpgradePreview ? ApplyUpgradePreviewNumericHighlight(card, target, description) : description;
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

	private static string ApplyUpgradePreviewNumericHighlight(CardModel card, Creature? target, string upgradedDescription)
	{
		if (card == null || string.IsNullOrWhiteSpace(upgradedDescription))
		{
			return upgradedDescription;
		}

		string? baseDescription = TryBuildBaseDescription(card, target);
		if (string.IsNullOrWhiteSpace(baseDescription) || string.Equals(baseDescription, upgradedDescription, System.StringComparison.Ordinal))
		{
			return upgradedDescription;
		}

		string[] baseLines = baseDescription.Split('\n');
		string[] upgradedLines = upgradedDescription.Split('\n');
		for (int i = 0; i < upgradedLines.Length; i++)
		{
			string baseLine = i < baseLines.Length ? baseLines[i] : string.Empty;
			upgradedLines[i] = HighlightChangedNumbersInLine(baseLine, upgradedLines[i]);
		}

		return string.Join('\n', upgradedLines);
	}

	private static string? TryBuildBaseDescription(CardModel upgradedCard, Creature? target)
	{
		try
		{
			CardModel baseCard = ModelDb.GetById<CardModel>(upgradedCard.Id).ToMutable();
			if (upgradedCard.Owner != null && baseCard.Owner == null)
			{
				baseCard.Owner = upgradedCard.Owner;
			}

			return Build(baseCard, target, isUpgradePreview: false);
		}
		catch
		{
			return null;
		}
	}

	private static string HighlightChangedNumbersInLine(string baseLine, string upgradedLine)
	{
		List<string> baseTokens = ExtractVisibleNumberTokens(baseLine);
		StringBuilder builder = new StringBuilder(upgradedLine.Length + 24);
		int tokenIndex = 0;
		int greenDepth = 0;
		int imageDepth = 0;

		for (int i = 0; i < upgradedLine.Length;)
		{
			if (upgradedLine[i] == '[')
			{
				int tagEnd = upgradedLine.IndexOf(']', i);
				if (tagEnd < 0)
				{
					builder.Append(upgradedLine.AsSpan(i));
					break;
				}

				string tag = upgradedLine.Substring(i, tagEnd - i + 1);
				builder.Append(tag);
				UpdateInlineTagState(tag, ref greenDepth, ref imageDepth);
				i = tagEnd + 1;
				continue;
			}

			if (greenDepth == 0 && imageDepth == 0 && char.IsDigit(upgradedLine[i]))
			{
				int start = i;
				while (i < upgradedLine.Length && char.IsDigit(upgradedLine[i]))
				{
					i++;
				}

				string token = upgradedLine.Substring(start, i - start);
				string? baseToken = tokenIndex < baseTokens.Count ? baseTokens[tokenIndex] : null;
				if (!string.Equals(baseToken, token, System.StringComparison.Ordinal))
				{
					builder.Append("[green]").Append(token).Append("[/green]");
				}
				else
				{
					builder.Append(token);
				}

				tokenIndex++;
				continue;
			}

			builder.Append(upgradedLine[i]);
			i++;
		}

		return builder.ToString();
	}

	private static List<string> ExtractVisibleNumberTokens(string line)
	{
		List<string> tokens = new List<string>();
		int greenDepth = 0;
		int imageDepth = 0;
		for (int i = 0; i < line.Length;)
		{
			if (line[i] == '[')
			{
				int tagEnd = line.IndexOf(']', i);
				if (tagEnd < 0)
				{
					break;
				}

				string tag = line.Substring(i, tagEnd - i + 1);
				UpdateInlineTagState(tag, ref greenDepth, ref imageDepth);
				i = tagEnd + 1;
				continue;
			}

			if (imageDepth == 0 && char.IsDigit(line[i]))
			{
				int start = i;
				while (i < line.Length && char.IsDigit(line[i]))
				{
					i++;
				}

				tokens.Add(line.Substring(start, i - start));
				continue;
			}

			i++;
		}

		return tokens;
	}

	private static void UpdateInlineTagState(string tag, ref int greenDepth, ref int imageDepth)
	{
		if (tag.Equals("[green]", System.StringComparison.OrdinalIgnoreCase))
		{
			greenDepth++;
		}
		else if (tag.Equals("[/green]", System.StringComparison.OrdinalIgnoreCase))
		{
			greenDepth = greenDepth > 0 ? greenDepth - 1 : 0;
		}
		else if (tag.StartsWith("[img", System.StringComparison.OrdinalIgnoreCase))
		{
			imageDepth++;
		}
		else if (tag.Equals("[/img]", System.StringComparison.OrdinalIgnoreCase))
		{
			imageDepth = imageDepth > 0 ? imageDepth - 1 : 0;
		}
	}
}
