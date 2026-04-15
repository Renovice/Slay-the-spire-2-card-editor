using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorVanillaKeywordSupport
{
	private sealed record HoverTerm(string Term, Func<IHoverTip>? CreateHoverTip);

	private sealed class Cache
	{
		public required string Signature { get; init; }
		public required Regex TermRegex { get; init; }
		public required IReadOnlyDictionary<string, HoverTerm> TermsByText { get; init; }
	}

	private static readonly StringComparer _termComparer = StringComparer.OrdinalIgnoreCase;
	private static readonly string[] _colorTags =
	{
		"gold",
		"purple",
		"red",
		"green",
		"blue",
		"yellow",
		"orange",
		"white",
		"black",
		"gray",
		"grey",
		"cyan",
		"magenta"
	};

	private static readonly object _cacheLock = new object();
	private static Cache? _cache;

	public static bool ShouldAugmentCard(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		return card is CardEditorCreatedCardBase || CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false).Count > 0;
	}

	public static string FormatDescription(string description)
	{
		if (string.IsNullOrWhiteSpace(description))
		{
			return description;
		}

		return FormatDescription(description, GetCache());
	}

	public static string FormatDescription(CardModel? card, string description, Creature? target = null, bool isUpgradePreview = false)
	{
		if (string.IsNullOrWhiteSpace(description))
		{
			return description;
		}

		return FormatDescription(description, GetCache(card, target, isUpgradePreview));
	}

	private static string FormatDescription(string description, Cache cache)
	{
		StringBuilder builder = new StringBuilder(description.Length + 32);
		int index = 0;
		int colorDepth = 0;
		int imageDepth = 0;

		while (index < description.Length)
		{
			int tagStart = description.IndexOf('[', index);
			if (tagStart < 0)
			{
				AppendFormattedPlainText(builder, description.Substring(index), colorDepth == 0 && imageDepth == 0, cache);
				break;
			}

			if (tagStart > index)
			{
				AppendFormattedPlainText(builder, description.Substring(index, tagStart - index), colorDepth == 0 && imageDepth == 0, cache);
			}

			int tagEnd = description.IndexOf(']', tagStart);
			if (tagEnd < 0)
			{
				AppendFormattedPlainText(builder, description.Substring(tagStart), colorDepth == 0, cache);
				break;
			}

			string tag = description.Substring(tagStart, tagEnd - tagStart + 1);
			builder.Append(tag);
			UpdateImageDepth(tag, ref imageDepth);
			UpdateColorDepth(tag, ref colorDepth);
			index = tagEnd + 1;
		}

		return builder.ToString();
	}

	public static IReadOnlyList<IHoverTip> InferHoverTips(string description)
	{
		if (string.IsNullOrWhiteSpace(description))
		{
			return Array.Empty<IHoverTip>();
		}

		Cache cache = GetCache();
		List<IHoverTip> tips = new List<IHoverTip>();
		int index = 0;
		int imageDepth = 0;

		while (index < description.Length)
		{
			int tagStart = description.IndexOf('[', index);
			if (tagStart < 0)
			{
				if (imageDepth == 0)
				{
					CollectTipsFromPlainText(tips, description.Substring(index), cache);
				}
				break;
			}

			if (tagStart > index && imageDepth == 0)
			{
				CollectTipsFromPlainText(tips, description.Substring(index, tagStart - index), cache);
			}

			int tagEnd = description.IndexOf(']', tagStart);
			if (tagEnd < 0)
			{
				CollectTipsFromPlainText(tips, description.Substring(tagStart), cache);
				break;
			}

			string tag = description.Substring(tagStart, tagEnd - tagStart + 1);
			UpdateImageDepth(tag, ref imageDepth);
			index = tagEnd + 1;
		}

		return IHoverTip.RemoveDupes(tips).ToList();
	}

	private static void AppendFormattedPlainText(StringBuilder builder, string text, bool shouldFormat, Cache cache)
	{
		if (!shouldFormat || string.IsNullOrEmpty(text))
		{
			builder.Append(text);
			return;
		}

		string formatted = cache.TermRegex.Replace(text, match =>
		{
			if (!cache.TermsByText.TryGetValue(match.Value, out HoverTerm? term))
			{
				return match.Value;
			}

			return $"[gold]{term.Term}[/gold]";
		});

		builder.Append(formatted);
	}

	private static void CollectTipsFromPlainText(List<IHoverTip> tips, string text, Cache cache)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		foreach (Match match in cache.TermRegex.Matches(text))
		{
			if (!cache.TermsByText.TryGetValue(match.Value, out HoverTerm? term))
			{
				continue;
			}

			if (term.CreateHoverTip == null)
			{
				continue;
			}

			try
			{
				tips.Add(term.CreateHoverTip());
			}
			catch
			{
			}
		}
	}

	private static Cache GetCache(CardModel? card, Creature? target, bool isUpgradePreview)
	{
		Cache baseCache = GetCache();
		if (card == null)
		{
			return baseCache;
		}

		IReadOnlyList<CardExtraEffectKeywordSummary> customKeywordSummaries =
			CardEditorExtraEffects.GetCustomKeywordSummaries(card, target, isUpgradePreview);
		if (customKeywordSummaries.Count == 0)
		{
			return baseCache;
		}

		Dictionary<string, HoverTerm>? mergedTerms = null;
		foreach (CardExtraEffectKeywordSummary summary in customKeywordSummaries)
		{
			string keywordName = summary.Name?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(keywordName) || baseCache.TermsByText.ContainsKey(keywordName))
			{
				continue;
			}

			mergedTerms ??= new Dictionary<string, HoverTerm>(baseCache.TermsByText, _termComparer);
			if (mergedTerms.ContainsKey(keywordName))
			{
				continue;
			}

			string keywordDescription = summary.Description?.Trim() ?? string.Empty;
			mergedTerms[keywordName] = new HoverTerm(
				keywordName,
				string.IsNullOrWhiteSpace(keywordDescription)
					? null
					: () => CreateDynamicHoverTip(keywordName, keywordDescription));
		}

		if (mergedTerms == null)
		{
			return baseCache;
		}

		return new Cache
		{
			Signature = baseCache.Signature,
			TermsByText = mergedTerms,
			TermRegex = BuildRegex(mergedTerms.Keys)
		};
	}

	private static void UpdateColorDepth(string tag, ref int colorDepth)
	{
		if (TryGetOpeningColorTag(tag, out _))
		{
			colorDepth++;
			return;
		}

		if (TryGetClosingColorTag(tag, out _) && colorDepth > 0)
		{
			colorDepth--;
		}
	}

	private static void UpdateImageDepth(string tag, ref int imageDepth)
	{
		if (string.Equals(tag, "[img]", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth++;
			return;
		}

		if (string.Equals(tag, "[/img]", StringComparison.OrdinalIgnoreCase) && imageDepth > 0)
		{
			imageDepth--;
		}
	}

	private static bool TryGetOpeningColorTag(string tag, out string colorName)
	{
		colorName = string.Empty;
		if (tag.Length < 3 || tag[0] != '[' || tag[^1] != ']' || tag[1] == '/')
		{
			return false;
		}

		string content = tag.Substring(1, tag.Length - 2);
		int equalsIndex = content.IndexOf('=');
		if (equalsIndex >= 0)
		{
			content = content.Substring(0, equalsIndex);
		}

		if (_colorTags.Contains(content, StringComparer.OrdinalIgnoreCase))
		{
			colorName = content;
			return true;
		}

		return false;
	}

	private static bool TryGetClosingColorTag(string tag, out string colorName)
	{
		colorName = string.Empty;
		if (tag.Length < 4 || tag[0] != '[' || tag[1] != '/' || tag[^1] != ']')
		{
			return false;
		}

		string content = tag.Substring(2, tag.Length - 3);
		if (_colorTags.Contains(content, StringComparer.OrdinalIgnoreCase))
		{
			colorName = content;
			return true;
		}

		return false;
	}

	private static Cache GetCache()
	{
		string signature = BuildSignature();
		Cache? current = _cache;
		if (current != null && string.Equals(current.Signature, signature, StringComparison.Ordinal))
		{
			return current;
		}

		lock (_cacheLock)
		{
			current = _cache;
			if (current != null && string.Equals(current.Signature, signature, StringComparison.Ordinal))
			{
				return current;
			}

			Dictionary<string, HoverTerm> termsByText = BuildTerms();
			Regex regex = BuildRegex(termsByText.Keys);
			current = new Cache
			{
				Signature = signature,
				TermsByText = termsByText,
				TermRegex = regex
			};
			_cache = current;
			return current;
		}
	}

	private static string BuildSignature()
	{
		string blockTitle = GetHoverTipTitle(HoverTipFactory.Static(StaticHoverTip.Block)) ?? "Block";
		string strengthTitle = GetHoverTipTitle(HoverTipFactory.FromPower<StrengthPower>()) ?? "Strength";
		return blockTitle + "|" + strengthTitle;
	}

	private static Dictionary<string, HoverTerm> BuildTerms()
	{
		Dictionary<string, HoverTerm> terms = new Dictionary<string, HoverTerm>(_termComparer);

		foreach (CardKeyword keyword in Enum.GetValues<CardKeyword>())
		{
			if (keyword == CardKeyword.None)
			{
				continue;
			}

			CardKeyword capturedKeyword = keyword;
			AddTerm(terms, GetHoverTipTitle(HoverTipFactory.FromKeyword(capturedKeyword)), () => HoverTipFactory.FromKeyword(capturedKeyword));
		}

		AddTerm(terms, "Exhausted", () => HoverTipFactory.FromKeyword(CardKeyword.Exhaust));

		AddPowerTerm<StrengthPower>(terms);
		AddPowerTerm<DexterityPower>(terms);
		AddPowerTerm<FocusPower>(terms);
		AddPowerTerm<WeakPower>(terms);
		AddPowerTerm<FrailPower>(terms);
		AddPowerTerm<VulnerablePower>(terms);
		AddPowerTerm<PoisonPower>(terms);
		AddPowerTerm<DoomPower>(terms);
		AddPowerTerm<ConstrictPower>(terms);
		AddPowerTerm<ArtifactPower>(terms);
		AddPowerTerm<ThornsPower>(terms);
		AddPowerTerm<RegenPower>(terms);
		AddPowerTerm<PlatingPower>(terms);
		AddPowerTerm<IntangiblePower>(terms);
		AddPowerTerm<BufferPower>(terms);
		AddPowerTerm<VigorPower>(terms);
		AddPowerTerm<BlurPower>(terms);
		AddPowerTerm<RitualPower>(terms);

		AddOrbTerm<LightningOrb>(terms);
		AddOrbTerm<FrostOrb>(terms);
		AddOrbTerm<DarkOrb>(terms);
		AddOrbTerm<PlasmaOrb>(terms);
		AddOrbTerm<GlassOrb>(terms);

		AddStaticTerm(terms, StaticHoverTip.Block);
		AddStaticTerm(terms, StaticHoverTip.Channeling, "Channel");
		AddStaticTerm(terms, StaticHoverTip.Evoke, "Evoke", "Evoked");
		AddStaticTerm(terms, StaticHoverTip.Fatal, "Fatal");
		AddStaticTerm(terms, StaticHoverTip.Forge, "Upgrade", "Upgraded", "Forge", "Forged");
		AddStaticTerm(terms, StaticHoverTip.SummonStatic, "Summon", "Summoned");
		AddStaticTerm(terms, StaticHoverTip.Transform, "Transform");

		AddStaticLocalizationTerm(terms, "Deck", "DECK");
		AddStaticLocalizationTerm(terms, "Draw Pile", "DRAW_PILE");
		AddStaticLocalizationTerm(terms, "Discard Pile", "DISCARD_PILE");
		AddStaticLocalizationTerm(terms, "Exhaust Pile", "EXHAUST_PILE");
		AddTerm(terms, "Hand");

		return terms;
	}

	private static void AddPowerTerm<T>(Dictionary<string, HoverTerm> terms) where T : PowerModel
	{
		AddTerm(terms, GetHoverTipTitle(HoverTipFactory.FromPower<T>()), () => HoverTipFactory.FromPower<T>());
	}

	private static void AddOrbTerm<T>(Dictionary<string, HoverTerm> terms) where T : OrbModel
	{
		AddTerm(terms, GetHoverTipTitle(HoverTipFactory.FromOrb<T>()), () => HoverTipFactory.FromOrb<T>());
	}

	private static void AddStaticTerm(Dictionary<string, HoverTerm> terms, StaticHoverTip tip, params string[] aliases)
	{
		StaticHoverTip capturedTip = tip;
		AddTerm(terms, GetHoverTipTitle(HoverTipFactory.Static(capturedTip)), () => HoverTipFactory.Static(capturedTip));
		for (int i = 0; i < aliases.Length; i++)
		{
			string alias = aliases[i];
			AddTerm(terms, alias, () => HoverTipFactory.Static(capturedTip));
		}
	}

	private static void AddStaticLocalizationTerm(Dictionary<string, HoverTerm> terms, string term, string localizationKey)
	{
		AddTerm(terms, term, () => CreateStaticLocalizationHoverTip(localizationKey));
	}

	private static HoverTip CreateStaticLocalizationHoverTip(string localizationKey)
	{
		return new HoverTip(
			new LocString("static_hover_tips", localizationKey + ".title"),
			new LocString("static_hover_tips", localizationKey + ".description"));
	}

	private static void AddTerm(Dictionary<string, HoverTerm> terms, string? term, Func<IHoverTip>? createHoverTip = null)
	{
		if (string.IsNullOrWhiteSpace(term) || terms.ContainsKey(term))
		{
			return;
		}

		terms[term] = new HoverTerm(term, createHoverTip);
	}

	private static string? GetHoverTipTitle(IHoverTip tip)
	{
		if (tip is HoverTip hoverTip && !string.IsNullOrWhiteSpace(hoverTip.Title))
		{
			return hoverTip.Title;
		}

		return null;
	}

	internal static IHoverTip CreateDynamicHoverTip(string title, string description)
	{
		string safeTitle = string.IsNullOrWhiteSpace(title) ? "Custom Keyword" : title.Trim();
		string safeDescription = string.IsNullOrWhiteSpace(description) ? safeTitle : description.Trim();
		string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(safeTitle + "\n" + safeDescription)));
		string titleKey = "CARD_EDITOR.RUNTIME_KEYWORD." + hash + ".title";
		string descriptionKey = "CARD_EDITOR.RUNTIME_KEYWORD." + hash + ".description";

		if (LocManager.Instance != null)
		{
			try
			{
				LocTable table = LocManager.Instance.GetTable("extensions");
				table.MergeWith(new Dictionary<string, string>
				{
					[titleKey] = safeTitle,
					[descriptionKey] = safeDescription
				});
			}
			catch
			{
			}
		}

		return new HoverTip(
			new LocString("extensions", titleKey),
			new LocString("extensions", descriptionKey));
	}

	private static Regex BuildRegex(IEnumerable<string> terms)
	{
		List<string> escapedTerms = terms
			.Where(term => !string.IsNullOrWhiteSpace(term))
			.OrderByDescending(term => term.Length)
			.Select(Regex.Escape)
			.ToList();

		if (escapedTerms.Count == 0)
		{
			return new Regex("$a", RegexOptions.Compiled);
		}

		string pattern = $@"(?<![\p{{L}}\p{{N}}])(?:{string.Join("|", escapedTerms)})(?![\p{{L}}\p{{N}}])";
		return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}
}

[HarmonyPatch(typeof(CardModel), "get_HoverTips")]
internal static class CardModel_HoverTips_CardEditorKeywordSupport_Patch
{
	[ThreadStatic]
	private static bool _isAugmentingHoverTips;

	public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (_isAugmentingHoverTips || __instance == null || !CardEditorVanillaKeywordSupport.ShouldAugmentCard(__instance))
		{
			return;
		}

		try
		{
			_isAugmentingHoverTips = true;
			string description = __instance.GetDescriptionForPile(PileType.Hand, __instance.CurrentTarget);
			IReadOnlyList<IHoverTip> inferredTips = CardEditorVanillaKeywordSupport.InferHoverTips(description);
			IReadOnlyList<CardExtraEffectKeywordSummary> customKeywordSummaries =
				CardEditorExtraEffects.GetCustomKeywordSummaries(__instance, __instance.CurrentTarget, isUpgradePreview: false);

			IEnumerable<IHoverTip> existingTips = __result ?? Array.Empty<IHoverTip>();
			IEnumerable<IHoverTip> combinedTips = existingTips.Concat(inferredTips);
			if (customKeywordSummaries.Count > 0)
			{
				combinedTips = combinedTips.Concat(customKeywordSummaries.Select(summary => CardEditorVanillaKeywordSupport.CreateDynamicHoverTip(summary.Name, summary.Description)));
			}

			List<IHoverTip> finalTips = IHoverTip.RemoveDupes(combinedTips).ToList();
			if (finalTips.Count == 0)
			{
				return;
			}

			__result = finalTips;
		}
		catch
		{
		}
		finally
		{
			_isAugmentingHoverTips = false;
		}
	}
}
