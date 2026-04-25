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
	private static readonly Lazy<HashSet<string>> _runtimeKeywordLineKeys = new Lazy<HashSet<string>>(BuildRuntimeKeywordLineKeys);
	private static readonly Lazy<HashSet<string>> _runtimeReplayLineKeys = new Lazy<HashSet<string>>(BuildRuntimeReplayLineKeys);

	public static string ApplyLiveNumbersFromReference(string template, string? referenceDescription)
	{
		if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(referenceDescription))
		{
			return template;
		}

		List<string> referenceTokens = ExtractVisibleNumberTokens(referenceDescription, includeHighlightedNumbers: true);
		if (referenceTokens.Count == 0)
		{
			return template;
		}

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
					? referenceTokens[tokenIndex]
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
}
