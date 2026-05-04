using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.TextEffects;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorDescriptionNumberHighlighter
{
	private readonly struct RenderedNumberToken
	{
		public RenderedNumberToken(string plainText, string renderedText)
		{
			PlainText = plainText;
			RenderedText = renderedText;
		}

		public string PlainText { get; }
		public string RenderedText { get; }
	}

	private static readonly Lazy<HashSet<string>> _runtimeKeywordLineKeys = new Lazy<HashSet<string>>(BuildRuntimeKeywordLineKeys);
	private static readonly Lazy<HashSet<string>> _runtimeReplayLineKeys = new Lazy<HashSet<string>>(BuildRuntimeReplayLineKeys);

	public static string ApplyLiveNumbersFromReference(string template, string? referenceDescription)
	{
		if (string.IsNullOrWhiteSpace(template))
		{
			return template;
		}

		if (TryApplySemanticLiveNumberTokens(template, referenceDescription, out string semanticText))
		{
			return semanticText;
		}

		if (string.IsNullOrWhiteSpace(referenceDescription))
		{
			return template;
		}

		string? lineMatched = TryApplyLiveNumbersByMatchingLines(template, referenceDescription);
		if (lineMatched != null)
		{
			return lineMatched;
		}

		// Multi-line custom text is authored line-by-line. If no line can be matched
		// safely, keep the user's text literal instead of applying unrelated numbers
		// from earlier/later generated lines. When the numeric slot count is exactly
		// the same, keep the 6.8 behavior: custom text number N mirrors generated
		// text number N, including Strength/Vulnerable target previews.
		if (SplitDescriptionLines(template).Length > 1 || SplitDescriptionLines(referenceDescription).Length > 1)
		{
			List<string> templateTokens = ExtractVisibleNumberTokens(template, includeHighlightedNumbers: false);
			List<RenderedNumberToken> fallbackReferenceTokens = ExtractRenderedNumberTokens(referenceDescription);
			if (templateTokens.Count == 0 || templateTokens.Count != fallbackReferenceTokens.Count)
			{
				return template;
			}

			return ApplyRenderedNumberTokens(template, fallbackReferenceTokens);
		}

		List<RenderedNumberToken> referenceTokens = ExtractRenderedNumberTokens(referenceDescription);
		if (referenceTokens.Count == 0)
		{
			return template;
		}

		return ApplyRenderedNumberTokens(template, referenceTokens);
	}

	public static string BuildLiveNumberTokenTemplate(string referenceDescription)
	{
		if (string.IsNullOrWhiteSpace(referenceDescription))
		{
			return referenceDescription;
		}

		StringBuilder builder = new StringBuilder(referenceDescription.Length + 16);
		int tokenIndex = 0;
		int imageDepth = 0;

		for (int i = 0; i < referenceDescription.Length;)
		{
			if (TryReadTag(referenceDescription, i, out string tag, out int tagEndExclusive))
			{
				if (!IsLiveNumberHighlightTag(tag))
				{
					builder.Append(tag);
				}
				UpdateTemplateTokenTagState(tag, ref imageDepth);
				i = tagEndExclusive;
				continue;
			}

			if (imageDepth == 0 && TryReadNumericToken(referenceDescription, i, out int tokenEndExclusive))
			{
				tokenIndex++;
				builder.Append("{{n");
				builder.Append(tokenIndex.ToString(CultureInfo.InvariantCulture));
				builder.Append("}}");
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(referenceDescription[i]);
			i++;
		}

		return tokenIndex > 0 ? builder.ToString() : referenceDescription;
	}

	private static bool TryApplySemanticLiveNumberTokens(string template, string? referenceDescription, out string rendered)
	{
		rendered = template;
		if (!template.Contains("{{", StringComparison.Ordinal))
		{
			return false;
		}

		List<RenderedNumberToken> globalTokens = string.IsNullOrWhiteSpace(referenceDescription)
			? new List<RenderedNumberToken>()
			: ExtractRenderedNumberTokens(referenceDescription);
		List<List<RenderedNumberToken>> lineTokens = new List<List<RenderedNumberToken>>();
		if (!string.IsNullOrWhiteSpace(referenceDescription))
		{
			foreach (string line in SplitDescriptionLines(referenceDescription))
			{
				lineTokens.Add(ExtractRenderedNumberTokens(line));
			}
		}

		StringBuilder builder = new StringBuilder(template.Length + 16);
		bool replacedAny = false;
		for (int i = 0; i < template.Length;)
		{
			if (i + 3 < template.Length && template[i] == '{' && template[i + 1] == '{')
			{
				int tokenEnd = template.IndexOf("}}", i + 2, StringComparison.Ordinal);
				if (tokenEnd >= 0)
				{
					string token = template.Substring(i + 2, tokenEnd - i - 2);
					if (TryResolveSemanticLiveNumberToken(token, globalTokens, lineTokens, out string replacement))
					{
						builder.Append(replacement);
						replacedAny = true;
						i = tokenEnd + 2;
						continue;
					}
				}
			}

			builder.Append(template[i]);
			i++;
		}

		if (!replacedAny)
		{
			return false;
		}

		rendered = builder.ToString();
		return true;
	}

	private static bool TryResolveSemanticLiveNumberToken(
		string rawToken,
		IReadOnlyList<RenderedNumberToken> globalTokens,
		IReadOnlyList<List<RenderedNumberToken>> lineTokens,
		out string replacement)
	{
		replacement = string.Empty;
		string token = rawToken.Trim();
		if (token.Length < 2)
		{
			return false;
		}

		if ((token[0] == 'n' || token[0] == 'N')
			&& TryParsePositiveInt(token, 1, token.Length - 1, out int globalNumberIndex))
		{
			replacement = GetRenderedTokenOrFallback(globalTokens, globalNumberIndex - 1);
			return true;
		}

		if (token[0] == 'l' || token[0] == 'L')
		{
			int numberMarker = token.IndexOf('n', 1);
			if (numberMarker < 0)
			{
				numberMarker = token.IndexOf('N', 1);
			}

			if (numberMarker > 1
				&& numberMarker + 1 < token.Length
				&& TryParsePositiveInt(token, 1, numberMarker - 1, out int lineIndex)
				&& TryParsePositiveInt(token, numberMarker + 1, token.Length - numberMarker - 1, out int lineNumberIndex))
			{
				IReadOnlyList<RenderedNumberToken> tokens = Array.Empty<RenderedNumberToken>();
				int zeroBasedLineIndex = lineIndex - 1;
				if (zeroBasedLineIndex >= 0 && zeroBasedLineIndex < lineTokens.Count)
				{
					tokens = lineTokens[zeroBasedLineIndex];
				}
				replacement = GetRenderedTokenOrFallback(tokens, lineNumberIndex - 1);
				return true;
			}
		}

		return false;
	}

	private static bool TryParsePositiveInt(string text, int start, int length, out int value)
	{
		value = 0;
		if (start < 0 || length <= 0 || start + length > text.Length)
		{
			return false;
		}

		for (int i = start; i < start + length; i++)
		{
			if (!char.IsDigit(text[i]))
			{
				return false;
			}

			value = (value * 10) + (text[i] - '0');
		}

		return value > 0;
	}

	private static string GetRenderedTokenOrFallback(IReadOnlyList<RenderedNumberToken> tokens, int zeroBasedIndex)
	{
		return zeroBasedIndex >= 0 && zeroBasedIndex < tokens.Count
			? tokens[zeroBasedIndex].RenderedText
			: "0";
	}

	private static string? TryApplyLiveNumbersByMatchingLines(string template, string referenceDescription)
	{
		string[] templateLines = SplitDescriptionLines(template);
		string[] referenceLines = SplitDescriptionLines(referenceDescription);
		if (templateLines.Length <= 1 || referenceLines.Length <= 1)
		{
			return null;
		}

		Dictionary<string, List<RenderedNumberToken>> referenceTokensByLineKey = new Dictionary<string, List<RenderedNumberToken>>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string referenceLine in referenceLines)
		{
			string key = NormalizeVisibleLineWithoutNumbers(referenceLine);
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			List<RenderedNumberToken> tokens = ExtractRenderedNumberTokens(referenceLine);
			if (tokens.Count == 0)
			{
				continue;
			}

			if (referenceTokensByLineKey.ContainsKey(key))
			{
				duplicateKeys.Add(key);
				continue;
			}

			referenceTokensByLineKey[key] = tokens;
		}

		if (referenceTokensByLineKey.Count == 0)
		{
			return null;
		}

		bool matchedAny = false;
		List<string> renderedLines = new List<string>(templateLines.Length);
		foreach (string templateLine in templateLines)
		{
			string key = NormalizeVisibleLineWithoutNumbers(templateLine);
			if (!string.IsNullOrWhiteSpace(key)
				&& !duplicateKeys.Contains(key)
				&& referenceTokensByLineKey.TryGetValue(key, out List<RenderedNumberToken>? lineTokens))
			{
				renderedLines.Add(ApplyRenderedNumberTokens(templateLine, lineTokens));
				matchedAny = true;
				continue;
			}

			renderedLines.Add(templateLine);
		}

		return matchedAny ? string.Join('\n', renderedLines) : null;
	}

	private static string ApplyRenderedNumberTokens(string template, IReadOnlyList<RenderedNumberToken> referenceTokens)
	{
		StringBuilder builder = new StringBuilder(template.Length + 16);
		int tokenIndex = 0;
		int highlightDepth = 0;
		int imageDepth = 0;

		for (int i = 0; i < template.Length;)
		{
			if (TryAppendTag(template, ref i, builder, ref highlightDepth, ref imageDepth))
			{
				continue;
			}

			if (highlightDepth == 0 && imageDepth == 0 && TryReadNumericToken(template, i, out int tokenEndExclusive))
			{
				builder.Append(tokenIndex < referenceTokens.Count
					? referenceTokens[tokenIndex].RenderedText
					: template.AsSpan(i, tokenEndExclusive - i));
				tokenIndex++;
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(template[i]);
			i++;
		}

		return builder.ToString();
	}

	public static string ApplyLiveNumbersAndManagedLinesFromReference(string template, string? referenceDescription)
	{
		string synced = ApplyLiveNumbersFromReference(template, referenceDescription);
		return ApplyManagedLinesFromReference(synced, referenceDescription);
	}

	public static string HighlightChangedNumbers(string baseDescription, string upgradedDescription)
	{
		if (string.IsNullOrWhiteSpace(baseDescription) || string.IsNullOrWhiteSpace(upgradedDescription))
		{
			return upgradedDescription;
		}

		if (string.Equals(baseDescription, upgradedDescription, StringComparison.Ordinal))
		{
			return upgradedDescription;
		}

		List<string> baseTokens = ExtractVisibleNumberTokens(baseDescription, includeHighlightedNumbers: false);
		if (baseTokens.Count == 0)
		{
			return upgradedDescription;
		}

		StringBuilder builder = new StringBuilder(upgradedDescription.Length + 24);
		int tokenIndex = 0;
		int highlightDepth = 0;
		int imageDepth = 0;

		for (int i = 0; i < upgradedDescription.Length;)
		{
			if (TryAppendTag(upgradedDescription, ref i, builder, ref highlightDepth, ref imageDepth))
			{
				continue;
			}

			if (highlightDepth == 0 && imageDepth == 0 && TryReadNumericToken(upgradedDescription, i, out int tokenEndExclusive))
			{
				string token = upgradedDescription.Substring(i, tokenEndExclusive - i);
				string? baseToken = tokenIndex < baseTokens.Count ? baseTokens[tokenIndex] : null;
				int comparison = GetUpgradeHighlightComparison(baseToken, token);
				builder.Append(comparison != 0
					? StsTextUtilities.HighlightChangeText(token, comparison)
					: token);
				tokenIndex++;
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(upgradedDescription[i]);
			i++;
		}

		return builder.ToString();
	}

	private static string ApplyManagedLinesFromReference(string customDescription, string? referenceDescription)
	{
		if (string.IsNullOrWhiteSpace(customDescription) || string.IsNullOrWhiteSpace(referenceDescription))
		{
			return customDescription;
		}

		string[] referenceLines = SplitDescriptionLines(referenceDescription);
		List<string> prefixLines = new List<string>();
		List<string> suffixLines = new List<string>();
		int firstBodyLine = FindFirstUnmanagedLine(referenceLines);

		for (int i = 0; i < referenceLines.Length; i++)
		{
			string trimmed = referenceLines[i].Trim();
			if (!IsManagedRuntimeLine(trimmed))
			{
				continue;
			}

			if (firstBodyLine >= 0 && i < firstBodyLine)
			{
				prefixLines.Add(trimmed);
			}
			else
			{
				suffixLines.Add(trimmed);
			}
		}

		if (prefixLines.Count == 0 && suffixLines.Count == 0)
		{
			return customDescription;
		}

		HashSet<string> emittedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<string> resultLines = new List<string>();
		AppendUniqueManagedLines(prefixLines, resultLines, emittedKeys);

		foreach (string line in SplitDescriptionLines(customDescription))
		{
			string trimmed = line.Trim();
			if (trimmed.Length == 0 || IsManagedRuntimeLine(trimmed))
			{
				continue;
			}

			resultLines.Add(trimmed);
			string key = NormalizeVisibleLine(trimmed);
			if (!string.IsNullOrWhiteSpace(key))
			{
				emittedKeys.Add(key);
			}
		}

		AppendUniqueManagedLines(suffixLines, resultLines, emittedKeys);
		return string.Join('\n', resultLines);
	}

	private static void AppendUniqueManagedLines(IEnumerable<string> sourceLines, List<string> resultLines, HashSet<string> emittedKeys)
	{
		foreach (string line in sourceLines)
		{
			string trimmed = line.Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			string key = NormalizeVisibleLine(trimmed);
			if (string.IsNullOrWhiteSpace(key) || !emittedKeys.Add(key))
			{
				continue;
			}

			resultLines.Add(trimmed);
		}
	}

	private static int FindFirstUnmanagedLine(string[] lines)
	{
		for (int i = 0; i < lines.Length; i++)
		{
			string trimmed = lines[i].Trim();
			if (trimmed.Length > 0 && !IsManagedRuntimeLine(trimmed))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsManagedRuntimeLine(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}

		if (line.Contains("[purple]", StringComparison.OrdinalIgnoreCase)
			|| line.Contains("[/purple]", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		string visibleKey = NormalizeVisibleLine(line);
		if (visibleKey.Length == 0)
		{
			return false;
		}

		if (_runtimeKeywordLineKeys.Value.Contains(visibleKey))
		{
			return true;
		}

		return _runtimeReplayLineKeys.Value.Contains(NormalizeVisibleLineWithoutNumbers(line));
	}

	private static string[] SplitDescriptionLines(string text)
	{
		return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
	}

	private static HashSet<string> BuildRuntimeKeywordLineKeys()
	{
		HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string period = ".";
		try
		{
			period = new LocString("card_keywords", "PERIOD").GetRawText();
		}
		catch
		{
		}

		foreach (CardKeyword keyword in Enum.GetValues<CardKeyword>())
		{
			try
			{
				string prefix = StringHelper.Slugify(keyword.ToString());
				LocString title = new LocString("card_keywords", prefix + ".title");
				AddRuntimeLineKey(keys, title.GetFormattedText() + period);
				AddRuntimeLineKey(keys, title.GetRawText() + period);
			}
			catch
			{
			}

			AddRuntimeLineKey(keys, keyword + period);
		}

		return keys;
	}

	private static HashSet<string> BuildRuntimeReplayLineKeys()
	{
		HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			LocString replay = new LocString("static_hover_tips", "REPLAY.extraText");
			replay.Add("Times", 1);
			AddRuntimeReplayLineKey(keys, replay.GetFormattedText() ?? string.Empty);
			AddRuntimeReplayLineKey(keys, replay.GetRawText());
		}
		catch
		{
		}

		return keys;
	}

	private static void AddRuntimeLineKey(HashSet<string> keys, string text)
	{
		string key = NormalizeVisibleLine(text);
		if (!string.IsNullOrWhiteSpace(key))
		{
			keys.Add(key);
		}
	}

	private static void AddRuntimeReplayLineKey(HashSet<string> keys, string text)
	{
		string key = NormalizeVisibleLineWithoutNumbers(text);
		if (!string.IsNullOrWhiteSpace(key))
		{
			keys.Add(key);
		}
	}

	private static string NormalizeVisibleLine(string line)
	{
		return NormalizeVisibleLineCore(line, removeNumbers: false);
	}

	private static string NormalizeVisibleLineWithoutNumbers(string line)
	{
		return NormalizeVisibleLineCore(line, removeNumbers: true);
	}

	private static string NormalizeVisibleLineCore(string line, bool removeNumbers)
	{
		StringBuilder builder = new StringBuilder(line.Length);
		bool previousWhitespace = false;
		for (int i = 0; i < line.Length;)
		{
			if (line[i] == '[')
			{
				int tagEnd = line.IndexOf(']', i);
				if (tagEnd >= 0)
				{
					i = tagEnd + 1;
					continue;
				}
			}

			if (removeNumbers && TryReadNumericToken(line, i, out int tokenEndExclusive))
			{
				i = tokenEndExclusive;
				continue;
			}

			char c = line[i++];
			if (char.IsWhiteSpace(c))
			{
				if (!previousWhitespace && builder.Length > 0)
				{
					builder.Append(' ');
					previousWhitespace = true;
				}

				continue;
			}

			builder.Append(c);
			previousWhitespace = false;
		}

		return builder.ToString().Trim();
	}

	private static List<string> ExtractVisibleNumberTokens(string text, bool includeHighlightedNumbers)
	{
		List<string> tokens = new List<string>();
		int highlightDepth = 0;
		int imageDepth = 0;

		for (int i = 0; i < text.Length;)
		{
			StringBuilder discard = new StringBuilder();
			if (TryAppendTag(text, ref i, discard, ref highlightDepth, ref imageDepth))
			{
				continue;
			}

			if ((includeHighlightedNumbers || highlightDepth == 0) && imageDepth == 0 && TryReadNumericToken(text, i, out int tokenEndExclusive))
			{
				tokens.Add(text.Substring(i, tokenEndExclusive - i));
				i = tokenEndExclusive;
				continue;
			}

			i++;
		}

		return tokens;
	}

	private static List<RenderedNumberToken> ExtractRenderedNumberTokens(string text)
	{
		List<RenderedNumberToken> tokens = new List<RenderedNumberToken>();
		string? activeHighlightTag = null;
		int imageDepth = 0;

		for (int i = 0; i < text.Length;)
		{
			if (TryReadTag(text, i, out string tag, out int tagEndExclusive))
			{
				UpdateRenderedTokenTagState(tag, ref activeHighlightTag, ref imageDepth);
				i = tagEndExclusive;
				continue;
			}

			if (imageDepth == 0 && TryReadNumericToken(text, i, out int tokenEndExclusive))
			{
				string plainText = text.Substring(i, tokenEndExclusive - i);
				string renderedText = activeHighlightTag == null
					? plainText
					: $"[{activeHighlightTag}]{plainText}[/{activeHighlightTag}]";
				tokens.Add(new RenderedNumberToken(plainText, renderedText));
				i = tokenEndExclusive;
				continue;
			}

			i++;
		}

		return tokens;
	}

	private static bool TryReadTag(string text, int index, out string tag, out int endExclusive)
	{
		tag = string.Empty;
		endExclusive = index;
		if (index < 0 || index >= text.Length || text[index] != '[')
		{
			return false;
		}

		int tagEnd = text.IndexOf(']', index);
		if (tagEnd < 0)
		{
			return false;
		}

		tag = text.Substring(index, tagEnd - index + 1);
		endExclusive = tagEnd + 1;
		return true;
	}

	private static bool TryAppendTag(string text, ref int index, StringBuilder builder, ref int highlightDepth, ref int imageDepth)
	{
		if (index < 0 || index >= text.Length || text[index] != '[')
		{
			return false;
		}

		int tagEnd = text.IndexOf(']', index);
		if (tagEnd < 0)
		{
			return false;
		}

		string tag = text.Substring(index, tagEnd - index + 1);
		builder.Append(tag);
		UpdateInlineTagState(tag, ref highlightDepth, ref imageDepth);
		index = tagEnd + 1;
		return true;
	}

	private static bool TryReadNumericToken(string text, int start, out int endExclusive)
	{
		endExclusive = start;
		if (start < 0 || start >= text.Length)
		{
			return false;
		}

		if (start > 0 && char.IsLetter(text[start - 1]))
		{
			return false;
		}

		int cursor = start;
		if ((text[cursor] == '+' || text[cursor] == '-') && cursor + 1 < text.Length && char.IsDigit(text[cursor + 1]))
		{
			cursor++;
		}

		if (cursor >= text.Length || !char.IsDigit(text[cursor]))
		{
			return false;
		}

		while (cursor < text.Length && char.IsDigit(text[cursor]))
		{
			cursor++;
		}

		if (cursor < text.Length
			&& text[cursor] == '.'
			&& cursor + 1 < text.Length
			&& char.IsDigit(text[cursor + 1]))
		{
			cursor++;
			while (cursor < text.Length && char.IsDigit(text[cursor]))
			{
				cursor++;
			}
		}

		endExclusive = cursor;
		if (cursor < text.Length && char.IsLetter(text[cursor]))
		{
			return false;
		}

		return true;
	}

	private static int GetUpgradeHighlightComparison(string? baseToken, string upgradedToken)
	{
		if (string.IsNullOrWhiteSpace(upgradedToken))
		{
			return 0;
		}

		if (string.IsNullOrWhiteSpace(baseToken))
		{
			return 1;
		}

		if (string.Equals(baseToken, upgradedToken, StringComparison.Ordinal))
		{
			return 0;
		}

		if (decimal.TryParse(baseToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal baseValue)
			&& decimal.TryParse(upgradedToken, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal upgradedValue))
		{
			return upgradedValue.CompareTo(baseValue);
		}

		return 1;
	}

	private static bool IsLiveNumberHighlightTag(string tag)
	{
		return tag.Equals("[green]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[red]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[/green]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[/red]", StringComparison.OrdinalIgnoreCase);
	}

	private static void UpdateTemplateTokenTagState(string tag, ref int imageDepth)
	{
		if (tag.StartsWith("[img", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth++;
		}
		else if (tag.Equals("[/img]", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth = imageDepth > 0 ? imageDepth - 1 : 0;
		}
	}

	private static void UpdateInlineTagState(string tag, ref int highlightDepth, ref int imageDepth)
	{
		if (tag.Equals("[green]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[red]", StringComparison.OrdinalIgnoreCase))
		{
			highlightDepth++;
		}
		else if (tag.Equals("[/green]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[/red]", StringComparison.OrdinalIgnoreCase))
		{
			highlightDepth = highlightDepth > 0 ? highlightDepth - 1 : 0;
		}
		else if (tag.StartsWith("[img", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth++;
		}
		else if (tag.Equals("[/img]", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth = imageDepth > 0 ? imageDepth - 1 : 0;
		}
	}

	private static void UpdateRenderedTokenTagState(string tag, ref string? activeHighlightTag, ref int imageDepth)
	{
		if (tag.Equals("[green]", StringComparison.OrdinalIgnoreCase))
		{
			activeHighlightTag = "green";
		}
		else if (tag.Equals("[red]", StringComparison.OrdinalIgnoreCase))
		{
			activeHighlightTag = "red";
		}
		else if (tag.Equals("[/green]", StringComparison.OrdinalIgnoreCase)
			|| tag.Equals("[/red]", StringComparison.OrdinalIgnoreCase))
		{
			activeHighlightTag = null;
		}
		else if (tag.StartsWith("[img", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth++;
		}
		else if (tag.Equals("[/img]", StringComparison.OrdinalIgnoreCase))
		{
			imageDepth = imageDepth > 0 ? imageDepth - 1 : 0;
		}
	}
}
