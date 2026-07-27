using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
	private sealed record HoverTerm(string Term, Func<IHoverTip>? CreateHoverTip)
	{
		private IHoverTip? _resolvedTip;
		private bool _resolved;

		// The factory used to be invoked once per matched term per HOVER, and several are expensive:
		// the power/orb ones go through reflection, and the static-localisation ones run the game's
		// SmartFormat over vanilla entries such as DISCARD_PILE.title, whose "{Hotkey:...}" selector
		// cannot resolve from here - the formatter then logs a localisation error WITH A FULL STACK
		// TRACE on every single hover (512 of them in one recorded session, triggered by any card
		// whose text merely mentions a pile, which custom cards do constantly). A tip's content
		// cannot change while the owning Cache is alive, so resolve once and reuse; rebuilding the
		// Cache on a signature change drops these along with it.
		internal IHoverTip? ResolveHoverTip()
		{
			if (_resolved)
			{
				return _resolvedTip;
			}

			_resolved = true;
			_resolvedTip = CreateHoverTip?.Invoke();
			return _resolvedTip;
		}
	}

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
	private static MethodInfo? _fromPowerGenericMethod;

	public static bool ShouldAugmentCard(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		if (card is CardEditorCreatedCardBase)
		{
			return true;
		}

		if (!CardEditorExtraEffects.HasCardEditorDescriptionSurface(card))
		{
			return false;
		}

		try
		{
			return CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false).Count > 0;
		}
		catch
		{
			return false;
		}
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
		Stack<string> colorStack = new Stack<string>();
		int imageDepth = 0;

		while (index < description.Length)
		{
			int tagStart = description.IndexOf('[', index);
			if (tagStart < 0)
			{
				AppendFormattedPlainText(builder, description.Substring(index), colorStack.Count == 0 && imageDepth == 0, cache);
				break;
			}

			if (tagStart > index)
			{
				AppendFormattedPlainText(builder, description.Substring(index, tagStart - index), colorStack.Count == 0 && imageDepth == 0, cache);
			}

			int tagEnd = description.IndexOf(']', tagStart);
			if (tagEnd < 0)
			{
				AppendFormattedPlainText(builder, description.Substring(tagStart), colorStack.Count == 0 && imageDepth == 0, cache);
				break;
			}

			string tag = description.Substring(tagStart, tagEnd - tagStart + 1);
			if (TryGetOpeningColorTag(tag, out string openingColor))
			{
				builder.Append(tag);
				colorStack.Push(openingColor);
				UpdateImageDepth(tag, ref imageDepth);
				index = tagEnd + 1;
				continue;
			}
			if (TryGetClosingColorTag(tag, out string closingColor))
			{
				if (colorStack.Count > 0 && string.Equals(colorStack.Peek(), closingColor, StringComparison.OrdinalIgnoreCase))
				{
					builder.Append(tag);
					colorStack.Pop();
				}
				index = tagEnd + 1;
				continue;
			}

			builder.Append(tag);
			UpdateImageDepth(tag, ref imageDepth);
			index = tagEnd + 1;
		}

		while (colorStack.Count > 0)
		{
			builder.Append("[/");
			builder.Append(colorStack.Pop());
			builder.Append(']');
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

		return RemoveDuplicateHoverTips(tips);
	}

	internal static List<IHoverTip> RemoveDuplicateHoverTips(IEnumerable<IHoverTip>? tips)
	{
		if (tips == null)
		{
			return new List<IHoverTip>();
		}

		List<IHoverTip> vanillaDeduped;
		try
		{
			vanillaDeduped = IHoverTip.RemoveDupes(tips.Where(tip => tip != null)).ToList();
		}
		catch
		{
			vanillaDeduped = tips.Where(tip => tip != null).ToList();
		}

		List<IHoverTip> result = new List<IHoverTip>(vanillaDeduped.Count);
		HashSet<string> seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (IHoverTip tip in vanillaDeduped)
		{
			string? key = GetHoverTipDedupeKey(tip);
			if (!string.IsNullOrWhiteSpace(key) && !seenTitles.Add(key))
			{
				continue;
			}

			result.Add(tip);
		}

		return result;
	}

	private static string? GetHoverTipDedupeKey(IHoverTip? tip)
	{
		if (tip is HoverTip hoverTip && !string.IsNullOrWhiteSpace(hoverTip.Title))
		{
			return Regex.Replace(hoverTip.Title.Trim(), @"\s+", " ");
		}

		return null;
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
				// Resolved once per term (see HoverTerm.ResolveHoverTip) rather than per hover.
				IHoverTip? tip = term.ResolveHoverTip();
				if (tip != null)
				{
					tips.Add(tip);
				}
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

		IReadOnlyList<CardExtraEffectKeywordSummary> customKeywordSummaries;
		try
		{
			customKeywordSummaries = CardEditorExtraEffects.GetCustomKeywordSummaries(card, target, isUpgradePreview);
		}
		catch
		{
			return baseCache;
		}
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
		string strengthTitle = GetHoverTipTitle(TryCreatePowerHoverTip(typeof(StrengthPower))) ?? "Strength";
		string powerFactorySignature = GetFromPowerGenericMethod()?.ToString() ?? "no-power-factory";
		return blockTitle + "|" + strengthTitle + "|" + powerFactorySignature;
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
		Type powerType = typeof(T);
		IHoverTip? previewTip = TryCreatePowerHoverTip(powerType);
		string? term = GetHoverTipTitle(previewTip) ?? GetFallbackPowerTerm(powerType);
		AddTerm(terms, term, () => TryCreatePowerHoverTip(powerType) ?? previewTip ?? CreateDynamicHoverTip(term ?? GetFallbackPowerTerm(powerType), term ?? GetFallbackPowerTerm(powerType)));
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

	private static string? GetHoverTipTitle(IHoverTip? tip)
	{
		if (tip is HoverTip hoverTip && !string.IsNullOrWhiteSpace(hoverTip.Title))
		{
			return hoverTip.Title;
		}

		return null;
	}

	private static MethodInfo? GetFromPowerGenericMethod()
	{
		if (_fromPowerGenericMethod != null)
		{
			return _fromPowerGenericMethod;
		}

		_fromPowerGenericMethod = typeof(HoverTipFactory)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == "FromPower" && method.IsGenericMethodDefinition)
			.OrderBy(method => method.GetParameters().Length)
			.FirstOrDefault(method =>
			{
				ParameterInfo[] parameters = method.GetParameters();
				return parameters.Length == 0
					|| (parameters.Length == 1 && Nullable.GetUnderlyingType(parameters[0].ParameterType) == typeof(int));
			});
		return _fromPowerGenericMethod;
	}

	private static IHoverTip? TryCreatePowerHoverTip(Type powerType)
	{
		try
		{
			MethodInfo? genericMethod = GetFromPowerGenericMethod();
			if (genericMethod == null)
			{
				return null;
			}

			MethodInfo constructedMethod = genericMethod.MakeGenericMethod(powerType);
			object?[]? args = constructedMethod.GetParameters().Length == 0
				? null
				: new object?[] { null };
			return constructedMethod.Invoke(null, args) as IHoverTip;
		}
		catch
		{
			return null;
		}
	}

	private static string GetFallbackPowerTerm(Type powerType)
	{
		string name = powerType.Name;
		if (name.EndsWith("Power", StringComparison.Ordinal))
		{
			name = name.Substring(0, name.Length - "Power".Length);
		}

		return Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
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
		if (_isAugmentingHoverTips || __instance == null)
		{
			return;
		}

		try
		{
			_isAugmentingHoverTips = true;
			if (!CardEditorVanillaKeywordSupport.ShouldAugmentCard(__instance))
			{
				return;
			}

			Creature? currentTarget = __instance.GetSafeCurrentTarget();
			string description = __instance.GetDescriptionForPile(PileType.Hand, currentTarget);
			IReadOnlyList<IHoverTip> inferredTips = CardEditorVanillaKeywordSupport.InferHoverTips(description);
			IReadOnlyList<CardExtraEffectKeywordSummary> customKeywordSummaries =
				CardEditorExtraEffects.GetCustomKeywordSummaries(__instance, currentTarget, isUpgradePreview: false);

			IEnumerable<IHoverTip> existingTips = __result ?? Array.Empty<IHoverTip>();
			IEnumerable<IHoverTip> combinedTips = existingTips.Concat(inferredTips);
			if (customKeywordSummaries.Count > 0)
			{
				combinedTips = combinedTips.Concat(customKeywordSummaries.Select(summary => CardEditorVanillaKeywordSupport.CreateDynamicHoverTip(summary.Name, summary.Description)));
			}
			IReadOnlyList<IHoverTip> hoverPreviewTips = CardEditorExtraEffects.GetAdditionalHoverPreviewTips(__instance);
			if (hoverPreviewTips.Count > 0)
			{
				combinedTips = combinedTips.Concat(hoverPreviewTips);
			}
			IReadOnlyList<IHoverTip> appliedPowerTips = CardEditorExtraEffects.GetAppliedPowerHoverTips(__instance, isUpgradePreview: false);
			if (appliedPowerTips.Count > 0)
			{
				combinedTips = combinedTips.Concat(appliedPowerTips);
			}

			List<IHoverTip> finalTips = CardEditorVanillaKeywordSupport.RemoveDuplicateHoverTips(combinedTips);
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
