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

	public static string ApplyLiveNumbersFromReference(
		string template,
		string? referenceDescription,
		IReadOnlyList<string>? fallbackNumberTokens = null,
		IReadOnlyDictionary<string, string>? stableTokenValues = null)
	{
		if (string.IsNullOrWhiteSpace(template))
		{
			return template;
		}

		List<RenderedNumberToken> fallbackTokens = BuildFallbackRenderedNumberTokens(fallbackNumberTokens);
		if (TryApplySemanticLiveNumberTokens(template, referenceDescription, fallbackTokens, stableTokenValues, out string semanticText))
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

		// Multi-line custom text is authored line-by-line. Prefer semantic tokens and
		// matched lines above; when those cannot identify a line, use a controlled
		// 6.8-style fallback: custom text number N mirrors generated text number N.
		// Extra custom numbers stay literal if the generated text has fewer slots.
		if (SplitDescriptionLines(template).Length > 1 || SplitDescriptionLines(referenceDescription).Length > 1)
		{
			List<string> templateTokens = ExtractVisibleNumberTokens(template, includeHighlightedNumbers: false);
			List<RenderedNumberToken> fallbackReferenceTokens = ExtractRenderedNumberTokens(referenceDescription);
			if (fallbackReferenceTokens.Count == 0 && fallbackTokens.Count > 0)
			{
				fallbackReferenceTokens = fallbackTokens;
			}
			if (templateTokens.Count == 0 || fallbackReferenceTokens.Count == 0)
			{
				return template;
			}

			return ApplyRenderedNumberTokens(template, fallbackReferenceTokens);
		}

		List<RenderedNumberToken> referenceTokens = ExtractRenderedNumberTokens(referenceDescription);
		if (referenceTokens.Count == 0 && fallbackTokens.Count > 0)
		{
			referenceTokens = fallbackTokens;
		}
		if (referenceTokens.Count == 0)
		{
			return template;
		}

		return ApplyRenderedNumberTokens(template, referenceTokens);
	}

	public static string BuildStableEffectAmountToken(string effectId)
		=> "e:" + (effectId ?? string.Empty).Trim();

	public static string BuildStableEffectAmountToken(string effectId, int numberIndex)
	{
		string baseToken = BuildStableEffectAmountToken(effectId);
		return numberIndex <= 1
			? baseToken
			: baseToken + ":" + numberIndex.ToString(CultureInfo.InvariantCulture);
	}

	public static string NormalizeLiveNumberLineKey(string text)
		=> NormalizeVisibleLineWithoutNumbers(text);

	public static string BuildLiveNumberTokenTemplate(string referenceDescription, IReadOnlyDictionary<string, string>? liveTokenByLineKey = null)
		=> BuildLiveNumberTokenTemplateCore(referenceDescription, NormalizeLiveTokenLineMap(liveTokenByLineKey));

	public static string BuildLiveNumberTokenTemplateWithLineTokens(
		string referenceDescription,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? liveTokensByLineKey = null)
		=> BuildLiveNumberTokenTemplateCore(referenceDescription, liveTokensByLineKey);

	private static string BuildLiveNumberTokenTemplateCore(
		string referenceDescription,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? liveTokensByLineKey = null)
	{
		if (string.IsNullOrWhiteSpace(referenceDescription))
		{
			return referenceDescription;
		}

		StringBuilder builder = new StringBuilder(referenceDescription.Length + 16);
		int tokenIndex = 0;
		int imageDepth = 0;
		string[] lines = SplitDescriptionLines(referenceDescription);
		if (liveTokensByLineKey != null && liveTokensByLineKey.Count > 0 && lines.Length > 0)
		{
			bool replacedLine = false;
			List<string> renderedLines = new List<string>(lines.Length);
			foreach (string line in lines)
			{
				string key = NormalizeVisibleLineWithoutNumbers(line);
				IReadOnlyList<string>? stableTokens = null;
				if (!string.IsNullOrWhiteSpace(key))
				{
					liveTokensByLineKey.TryGetValue(key, out stableTokens);
				}

				int lineTokenIndex = 0;
				string converted = ReplaceVisibleNumbersAndLiveTokensWithTokens(
					line,
					_ =>
					{
						lineTokenIndex++;
						tokenIndex++;
						return TryGetStableLineToken(stableTokens, lineTokenIndex, out string stableToken)
							? "{{" + stableToken + "}}"
							: $"{{{{n{tokenIndex.ToString(CultureInfo.InvariantCulture)}}}}}";
					},
					out bool lineReplaced);
				renderedLines.Add(converted);
				replacedLine |= lineReplaced;
			}

			if (replacedLine)
			{
				return string.Join('\n', renderedLines);
			}
		}

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

	public static string BuildLiveNumberTokenTemplateFromCustomText(
		string customText,
		string? referenceDescription,
		IReadOnlyDictionary<string, string>? liveTokenByLineKey = null)
		=> BuildLiveNumberTokenTemplateFromCustomTextCore(customText, referenceDescription, NormalizeLiveTokenLineMap(liveTokenByLineKey));

	public static string BuildLiveNumberTokenTemplateFromCustomTextWithLineTokens(
		string customText,
		string? referenceDescription,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? liveTokensByLineKey = null)
		=> BuildLiveNumberTokenTemplateFromCustomTextCore(customText, referenceDescription, liveTokensByLineKey);

	private static string BuildLiveNumberTokenTemplateFromCustomTextCore(
		string customText,
		string? referenceDescription,
		IReadOnlyDictionary<string, IReadOnlyList<string>>? liveTokensByLineKey = null)
	{
		if (string.IsNullOrWhiteSpace(customText))
		{
			return string.IsNullOrWhiteSpace(referenceDescription)
				? customText
				: BuildLiveNumberTokenTemplateCore(referenceDescription, liveTokensByLineKey);
		}

		if (string.IsNullOrWhiteSpace(referenceDescription))
		{
			return BuildLiveNumberTokenTemplateCore(customText);
		}

		string[] customLines = SplitDescriptionLines(customText);
		string[] referenceLines = SplitDescriptionLines(referenceDescription);
		Dictionary<string, int> referenceLineIndexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		List<List<RenderedNumberToken>> referenceTokensByLine = new List<List<RenderedNumberToken>>(referenceLines.Length);

		for (int i = 0; i < referenceLines.Length; i++)
		{
			List<RenderedNumberToken> tokens = ExtractRenderedNumberTokens(referenceLines[i]);
			referenceTokensByLine.Add(tokens);
			if (tokens.Count == 0)
			{
				continue;
			}

			string key = NormalizeVisibleLineWithoutNumbers(referenceLines[i]);
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			if (referenceLineIndexByKey.ContainsKey(key))
			{
				duplicateKeys.Add(key);
				continue;
			}

			referenceLineIndexByKey[key] = i;
		}

		int globalTokenIndex = 0;
		bool replacedAny = false;
		List<string> renderedLines = new List<string>(customLines.Length);
		foreach (string customLine in customLines)
		{
			string key = NormalizeVisibleLineWithoutNumbers(customLine);
			if (!string.IsNullOrWhiteSpace(key)
				&& liveTokensByLineKey != null
				&& liveTokensByLineKey.TryGetValue(key, out IReadOnlyList<string>? stableTokens))
			{
				int stableLineTokenIndex = 0;
				string converted = ReplaceVisibleNumbersAndLiveTokensWithTokens(
					customLine,
					_ =>
					{
						stableLineTokenIndex++;
						globalTokenIndex++;
						return TryGetStableLineToken(stableTokens, stableLineTokenIndex, out string stableToken)
							? "{{" + stableToken + "}}"
							: $"{{{{n{globalTokenIndex.ToString(CultureInfo.InvariantCulture)}}}}}";
					},
					out bool stableReplaced);
				replacedAny |= stableReplaced;
				renderedLines.Add(converted);
				continue;
			}

			if (!string.IsNullOrWhiteSpace(key)
				&& !duplicateKeys.Contains(key)
				&& referenceLineIndexByKey.TryGetValue(key, out int referenceLineIndex))
			{
				List<RenderedNumberToken> lineTokens = referenceTokensByLine[referenceLineIndex];
				int lineTokenIndex = 0;
				string converted = ReplaceVisibleNumbersWithTokens(customLine, _ =>
				{
					lineTokenIndex++;
					globalTokenIndex++;
					return lineTokenIndex <= lineTokens.Count
						? $"{{{{l{(referenceLineIndex + 1).ToString(CultureInfo.InvariantCulture)}n{lineTokenIndex.ToString(CultureInfo.InvariantCulture)}}}}}"
						: $"{{{{n{globalTokenIndex.ToString(CultureInfo.InvariantCulture)}}}}}";
				}, out bool lineReplaced, rawToken =>
				{
					if (IsSemanticLiveNumberToken(rawToken))
					{
						lineTokenIndex++;
						globalTokenIndex++;
					}
				});
				replacedAny |= lineReplaced;
				renderedLines.Add(converted);
				continue;
			}

			string fallbackConverted = ReplaceVisibleNumbersWithTokens(customLine, _ =>
			{
				globalTokenIndex++;
				return $"{{{{n{globalTokenIndex.ToString(CultureInfo.InvariantCulture)}}}}}";
			}, out bool fallbackReplaced, rawToken =>
			{
				if (IsSemanticLiveNumberToken(rawToken))
				{
					globalTokenIndex++;
				}
			});
			replacedAny |= fallbackReplaced;
			renderedLines.Add(fallbackConverted);
		}

		return replacedAny ? string.Join('\n', renderedLines) : customText;
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>>? NormalizeLiveTokenLineMap(IReadOnlyDictionary<string, string>? liveTokenByLineKey)
	{
		if (liveTokenByLineKey == null || liveTokenByLineKey.Count == 0)
		{
			return null;
		}

		Dictionary<string, IReadOnlyList<string>> normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> pair in liveTokenByLineKey)
		{
			string key = pair.Key;
			string token = pair.Value;
			if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(token))
			{
				continue;
			}

			normalized[key] = new[] { token.Trim() };
		}

		return normalized.Count == 0 ? null : normalized;
	}

	private static bool TryGetStableLineToken(IReadOnlyList<string>? stableTokens, int oneBasedIndex, out string stableToken)
	{
		stableToken = string.Empty;
		if (stableTokens == null || oneBasedIndex <= 0 || oneBasedIndex > stableTokens.Count)
		{
			return false;
		}

		stableToken = stableTokens[oneBasedIndex - 1]?.Trim() ?? string.Empty;
		return !string.IsNullOrWhiteSpace(stableToken);
	}

	private static List<RenderedNumberToken> BuildFallbackRenderedNumberTokens(IReadOnlyList<string>? fallbackNumberTokens)
	{
		if (fallbackNumberTokens == null || fallbackNumberTokens.Count == 0)
		{
			return new List<RenderedNumberToken>();
		}

		List<RenderedNumberToken> tokens = new List<RenderedNumberToken>(fallbackNumberTokens.Count);
		foreach (string? rawToken in fallbackNumberTokens)
		{
			string token = rawToken?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(token))
			{
				continue;
			}

			tokens.Add(new RenderedNumberToken(token, token));
		}

		return tokens;
	}

	private static bool TryApplySemanticLiveNumberTokens(
		string template,
		string? referenceDescription,
		IReadOnlyList<RenderedNumberToken> fallbackGlobalTokens,
		IReadOnlyDictionary<string, string>? stableTokenValues,
		out string rendered)
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
					if (TryResolveSemanticLiveNumberToken(token, globalTokens, lineTokens, fallbackGlobalTokens, stableTokenValues, out string replacement))
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

	private static string ReplaceVisibleNumbersWithTokens(
		string text,
		Func<int, string> tokenFactory,
		out bool replacedAny,
		Action<string>? onExistingLiveToken = null)
	{
		replacedAny = false;
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		StringBuilder builder = new StringBuilder(text.Length + 16);
		int tokenIndex = 0;
		int imageDepth = 0;

		for (int i = 0; i < text.Length;)
		{
			if (i + 3 < text.Length && text[i] == '{' && text[i + 1] == '{')
			{
				int tokenEnd = text.IndexOf("}}", i + 2, StringComparison.Ordinal);
				if (tokenEnd >= 0)
				{
					onExistingLiveToken?.Invoke(text.Substring(i + 2, tokenEnd - i - 2));
					builder.Append(text.AsSpan(i, tokenEnd + 2 - i));
					i = tokenEnd + 2;
					continue;
				}
			}

			if (TryReadTag(text, i, out string tag, out int tagEndExclusive))
			{
				builder.Append(tag);
				UpdateTemplateTokenTagState(tag, ref imageDepth);
				i = tagEndExclusive;
				continue;
			}

			if (imageDepth == 0 && TryReadNumericToken(text, i, out int tokenEndExclusive))
			{
				tokenIndex++;
				builder.Append(tokenFactory(tokenIndex));
				replacedAny = true;
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(text[i]);
			i++;
		}

		return replacedAny ? builder.ToString() : text;
	}

	private static string ReplaceVisibleNumbersAndLiveTokensWithTokens(
		string text,
		Func<int, string> tokenFactory,
		out bool replacedAny)
	{
		replacedAny = false;
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		StringBuilder builder = new StringBuilder(text.Length + 16);
		int tokenIndex = 0;
		int imageDepth = 0;

		for (int i = 0; i < text.Length;)
		{
			if (i + 3 < text.Length && text[i] == '{' && text[i + 1] == '{')
			{
				int tokenEnd = text.IndexOf("}}", i + 2, StringComparison.Ordinal);
				if (tokenEnd >= 0)
				{
					string rawToken = text.Substring(i + 2, tokenEnd - i - 2);
					string original = text.Substring(i, tokenEnd + 2 - i);
					if (imageDepth == 0 && IsSemanticLiveNumberToken(rawToken))
					{
						tokenIndex++;
						string replacement = tokenFactory(tokenIndex);
						builder.Append(replacement);
						replacedAny |= !string.Equals(original, replacement, StringComparison.Ordinal);
						i = tokenEnd + 2;
						continue;
					}

					builder.Append(original);
					i = tokenEnd + 2;
					continue;
				}
			}

			if (TryReadTag(text, i, out string tag, out int tagEndExclusive))
			{
				builder.Append(tag);
				UpdateTemplateTokenTagState(tag, ref imageDepth);
				i = tagEndExclusive;
				continue;
			}

			if (imageDepth == 0 && TryReadNumericToken(text, i, out int tokenEndExclusive))
			{
				tokenIndex++;
				builder.Append(tokenFactory(tokenIndex));
				replacedAny = true;
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(text[i]);
			i++;
		}

		return replacedAny ? builder.ToString() : text;
	}

	private static bool IsSemanticLiveNumberToken(string rawToken)
	{
		string token = rawToken.Trim();
		if (token.Length < 2)
		{
			return false;
		}

		if (token.StartsWith("e:", StringComparison.OrdinalIgnoreCase))
		{
			return token.Length > 2;
		}

		if ((token[0] == 'n' || token[0] == 'N')
			&& TryParsePositiveInt(token, 1, token.Length - 1, out _))
		{
			return true;
		}

		if (token[0] != 'l' && token[0] != 'L')
		{
			return false;
		}

		int numberMarker = token.IndexOf('n', 1);
		if (numberMarker < 0)
		{
			numberMarker = token.IndexOf('N', 1);
		}

		return numberMarker > 1
			&& numberMarker + 1 < token.Length
			&& TryParsePositiveInt(token, 1, numberMarker - 1, out _)
			&& TryParsePositiveInt(token, numberMarker + 1, token.Length - numberMarker - 1, out _);
	}

	private static bool TryResolveSemanticLiveNumberToken(
		string rawToken,
		IReadOnlyList<RenderedNumberToken> globalTokens,
		IReadOnlyList<List<RenderedNumberToken>> lineTokens,
		IReadOnlyList<RenderedNumberToken> fallbackGlobalTokens,
		IReadOnlyDictionary<string, string>? stableTokenValues,
		out string replacement)
	{
		replacement = string.Empty;
		string token = rawToken.Trim();
		if (token.Length < 2)
		{
			return false;
		}

		if (stableTokenValues != null && stableTokenValues.TryGetValue(token, out string? stableReplacement))
		{
			replacement = stableReplacement;
			return true;
		}

		if ((token[0] == 'n' || token[0] == 'N')
			&& TryParsePositiveInt(token, 1, token.Length - 1, out int globalNumberIndex))
		{
			return TryGetRenderedToken(globalTokens, globalNumberIndex - 1, out replacement)
				|| TryGetRenderedToken(fallbackGlobalTokens, globalNumberIndex - 1, out replacement);
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
				return TryGetRenderedToken(tokens, lineNumberIndex - 1, out replacement);
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

	private static bool TryGetRenderedToken(IReadOnlyList<RenderedNumberToken> tokens, int zeroBasedIndex, out string renderedText)
	{
		renderedText = string.Empty;
		if (zeroBasedIndex < 0 || zeroBasedIndex >= tokens.Count)
		{
			return false;
		}

		renderedText = tokens[zeroBasedIndex].RenderedText;
		return true;
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

	public static string ApplyLiveNumbersAndManagedLinesFromReference(
		string template,
		string? referenceDescription,
		IReadOnlyList<string>? fallbackNumberTokens = null,
		IReadOnlyDictionary<string, string>? stableTokenValues = null)
	{
		string synced = ApplyLiveNumbersFromReference(template, referenceDescription, fallbackNumberTokens, stableTokenValues);
		return ApplyManagedLinesFromReference(synced, referenceDescription);
	}

	public static string HighlightChangedNumbers(string baseDescription, string upgradedDescription)
	{
		if (string.IsNullOrWhiteSpace(baseDescription) || string.IsNullOrWhiteSpace(upgradedDescription))
		{
			return upgradedDescription;
		}

		// Upgrade-preview green takes precedence over live combat coloring (vanilla forces upgraded
		// vars green in previews). Strip live [green]/[red] tags before diffing — otherwise numbers
		// the live sync already tagged are invisible to the comparison and the preview is inconsistent.
		baseDescription = StripLiveNumberHighlightTags(baseDescription);
		upgradedDescription = StripLiveNumberHighlightTags(upgradedDescription);

		if (string.Equals(baseDescription, upgradedDescription, StringComparison.Ordinal))
		{
			return upgradedDescription;
		}

		string[] baseLines = SplitDescriptionLines(baseDescription);
		string[] upgradedLines = SplitDescriptionLines(upgradedDescription);
		if (baseLines.Length > 1 || upgradedLines.Length > 1)
		{
			return HighlightChangedNumbersByLine(baseLines, upgradedLines);
		}

		// Single line: word-level diff so non-numeric keyword changes ("Anger" -> "Anger+") are greened
		// too, not just numbers (the old single-line path only compared numeric tokens, so a one-line
		// description with a keyword change showed no green at all).
		return HighlightChangedWordsInLine(baseDescription, upgradedDescription);
	}

	// "[[green]]...[[/green]]" in custom text renders green ONLY in upgrade previews and disappears
	// everywhere else — the custom-text equivalent of vanilla's {IfUpgraded} green. Resolved at the
	// END of the text pipelines so the preview diff pass cannot strip the converted tags.
	internal static string ResolvePreviewOnlyHighlightMarkers(string text, bool isUpgradePreview)
	{
		if (string.IsNullOrEmpty(text) || !text.Contains("[[", StringComparison.Ordinal))
		{
			return text;
		}

		return isUpgradePreview
			? text
				.Replace("[[green]]", "[green]", StringComparison.OrdinalIgnoreCase)
				.Replace("[[/green]]", "[/green]", StringComparison.OrdinalIgnoreCase)
			: text
				.Replace("[[green]]", string.Empty, StringComparison.OrdinalIgnoreCase)
				.Replace("[[/green]]", string.Empty, StringComparison.OrdinalIgnoreCase);
	}

	private static string StripLiveNumberHighlightTags(string text)
	{
		if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0)
		{
			return text;
		}

		StringBuilder builder = new StringBuilder(text.Length);
		for (int i = 0; i < text.Length;)
		{
			if (text[i] == '[')
			{
				int tagEnd = text.IndexOf(']', i);
				if (tagEnd >= 0)
				{
					string tag = text.Substring(i, tagEnd - i + 1);
					if (IsLiveNumberHighlightTag(tag))
					{
						i = tagEnd + 1;
						continue;
					}
				}
			}

			builder.Append(text[i]);
			i++;
		}

		return builder.ToString();
	}

	private static string HighlightChangedNumbersByLine(string[] baseLines, string[] upgradedLines)
	{
		if (upgradedLines.Length == 0)
		{
			return string.Empty;
		}

		// Pair each upgraded line with the most-similar UNUSED base line (exact visible-key first, then
		// best word overlap), then word-diff that pair. This means a line whose keyword changed
		// ("Anger" -> "Anger+") still pairs with its base and only the changed WORD is greened, while a
		// genuinely new line (e.g. a prepended "Innate"/"固有") has no similar base line and is greened
		// whole, matching vanilla {IfUpgraded}. Decoupling from line index also fixes the old bug where a
		// prepended line shifted every following line and turned them all green.
		bool[] baseUsed = new bool[baseLines.Length];
		List<string> renderedLines = new List<string>(upgradedLines.Length);
		foreach (string upgradedLine in upgradedLines)
		{
			int matchIndex = FindBestBaseLineMatch(upgradedLine, baseLines, baseUsed);
			if (matchIndex >= 0)
			{
				baseUsed[matchIndex] = true;
				renderedLines.Add(HighlightChangedWordsInLine(baseLines[matchIndex], upgradedLine));
			}
			else
			{
				renderedLines.Add(WrapLineInUpgradeGreen(upgradedLine));
			}
		}

		return string.Join('\n', renderedLines);
	}

	// Picks the unused base line that best corresponds to upgradedLine: an exact visible-key match wins;
	// otherwise the unused base line with the highest word overlap above a majority threshold. Returns -1
	// when the upgraded line is blank or has no comparable base line (caller greens it whole as new text).
	private static int FindBestBaseLineMatch(string upgradedLine, string[] baseLines, bool[] baseUsed)
	{
		string upgradedKey = NormalizeVisibleLineWithoutNumbers(upgradedLine);
		if (string.IsNullOrWhiteSpace(upgradedKey))
		{
			return -1;
		}

		for (int i = 0; i < baseLines.Length; i++)
		{
			if (baseUsed[i])
			{
				continue;
			}

			if (string.Equals(NormalizeVisibleLineWithoutNumbers(baseLines[i]), upgradedKey, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		int bestIndex = -1;
		double bestSimilarity = 0.5; // require a majority of words in common to treat as the same line
		for (int i = 0; i < baseLines.Length; i++)
		{
			if (baseUsed[i])
			{
				continue;
			}

			double similarity = LineWordSimilarity(baseLines[i], upgradedLine);
			if (similarity > bestSimilarity)
			{
				bestSimilarity = similarity;
				bestIndex = i;
			}
		}

		return bestIndex;
	}

	// Word/keyword-level diff: greens only the upgraded tokens that are NOT on the longest common word
	// subsequence with the base line. A bracketed color span like "[gold]Minion Strike[/gold]" is ONE
	// token, so the whole keyword greens as a unit and an existing tag is never split. Changed tokens are
	// greened on their VISIBLE text (the renderer shows the innermost color, so wrapping the bare visible
	// text forces green); unchanged tokens are emitted verbatim, preserving their [gold]/[blue] markup.
	private static string HighlightChangedWordsInLine(string baseLine, string upgradedLine)
	{
		if (string.IsNullOrWhiteSpace(upgradedLine))
		{
			return upgradedLine;
		}

		List<DiffToken> upgradedTokens = TokenizeLineForDiff(upgradedLine);
		List<DiffToken> baseTokens = TokenizeLineForDiff(baseLine);

		List<string> baseWords = new List<string>();
		foreach (DiffToken token in baseTokens)
		{
			if (token.IsWord)
			{
				baseWords.Add(token.Key);
			}
		}

		List<string> upgradedWords = new List<string>();
		foreach (DiffToken token in upgradedTokens)
		{
			if (token.IsWord)
			{
				upgradedWords.Add(token.Key);
			}
		}

		bool[] upgradedWordMatched = ComputeLcsMatched(baseWords, upgradedWords);

		StringBuilder builder = new StringBuilder(upgradedLine.Length + 24);
		int wordIndex = 0;
		foreach (DiffToken token in upgradedTokens)
		{
			if (!token.IsWord)
			{
				builder.Append(token.Raw);
				continue;
			}

			bool unchanged = wordIndex < upgradedWordMatched.Length && upgradedWordMatched[wordIndex];
			wordIndex++;
			builder.Append(unchanged ? token.Raw : StsTextUtilities.HighlightChangeText(token.Key, 1));
		}

		return builder.ToString();
	}

	// Fraction (0..1) of words common to the two lines via LCS; used only to decide line pairing.
	private static double LineWordSimilarity(string baseLine, string upgradedLine)
	{
		List<string> baseWords = new List<string>();
		foreach (DiffToken token in TokenizeLineForDiff(baseLine))
		{
			if (token.IsWord)
			{
				baseWords.Add(token.Key);
			}
		}

		List<string> upgradedWords = new List<string>();
		foreach (DiffToken token in TokenizeLineForDiff(upgradedLine))
		{
			if (token.IsWord)
			{
				upgradedWords.Add(token.Key);
			}
		}

		if (baseWords.Count == 0 || upgradedWords.Count == 0)
		{
			return 0.0;
		}

		bool[] matched = ComputeLcsMatched(baseWords, upgradedWords);
		int common = 0;
		foreach (bool isMatched in matched)
		{
			if (isMatched)
			{
				common++;
			}
		}

		return (double)common / Math.Max(baseWords.Count, upgradedWords.Count);
	}

	// Standard LCS over word keys. Returns, per upgraded word, whether it lies on the common subsequence
	// (= UNCHANGED). Words not on it are insertions/substitutions, i.e. the upgrade's changes.
	private static bool[] ComputeLcsMatched(List<string> baseWords, List<string> upgradedWords)
	{
		int baseCount = baseWords.Count;
		int upgradedCount = upgradedWords.Count;
		bool[] matched = new bool[upgradedCount];
		if (baseCount == 0 || upgradedCount == 0)
		{
			return matched;
		}

		int[,] lengths = new int[baseCount + 1, upgradedCount + 1];
		for (int a = baseCount - 1; a >= 0; a--)
		{
			for (int b = upgradedCount - 1; b >= 0; b--)
			{
				lengths[a, b] = string.Equals(baseWords[a], upgradedWords[b], StringComparison.Ordinal)
					? lengths[a + 1, b + 1] + 1
					: Math.Max(lengths[a + 1, b], lengths[a, b + 1]);
			}
		}

		int baseIndex = 0;
		int upgradedIndex = 0;
		while (baseIndex < baseCount && upgradedIndex < upgradedCount)
		{
			if (string.Equals(baseWords[baseIndex], upgradedWords[upgradedIndex], StringComparison.Ordinal))
			{
				matched[upgradedIndex] = true;
				baseIndex++;
				upgradedIndex++;
			}
			else if (lengths[baseIndex + 1, upgradedIndex] >= lengths[baseIndex, upgradedIndex + 1])
			{
				baseIndex++;
			}
			else
			{
				upgradedIndex++;
			}
		}

		return matched;
	}

	// Splits a line into word/keyword tokens (Key = visible text, used for comparison) and whitespace
	// tokens (Key empty). A bracketed color/keyword tag group stays ONE token: only whitespace at group
	// depth 0 separates tokens, so the space inside "[gold]Minion Strike[/gold]" does not split it. Image
	// tags are neutral inline units. Tags go into Raw but never into Key, so output preserves markup while
	// comparison is on visible text only.
	private static List<DiffToken> TokenizeLineForDiff(string line)
	{
		List<DiffToken> tokens = new List<DiffToken>();
		if (string.IsNullOrEmpty(line))
		{
			return tokens;
		}

		StringBuilder raw = new StringBuilder();
		StringBuilder key = new StringBuilder();
		int groupDepth = 0;

		void FlushWord()
		{
			if (raw.Length > 0)
			{
				tokens.Add(new DiffToken(raw.ToString(), key.ToString()));
				raw.Clear();
				key.Clear();
			}
		}

		int i = 0;
		while (i < line.Length)
		{
			if (line[i] == '[' && TryReadTag(line, i, out string tag, out int tagEnd))
			{
				bool isImage = tag.StartsWith("[img", StringComparison.OrdinalIgnoreCase)
					|| tag.StartsWith("[/img", StringComparison.OrdinalIgnoreCase);
				bool isClose = tag.StartsWith("[/", StringComparison.Ordinal);

				// A color/keyword tag group is its OWN token: flush any preceding plain word when a group
				// opens, and flush the group itself when it closes, so adjacent text/punctuation (e.g. the
				// "." in "[gold]Minion Strike[/gold].") is not pulled inside the keyword's green wrap.
				if (!isImage && !isClose && groupDepth == 0)
				{
					FlushWord();
				}

				raw.Append(tag);
				if (!isImage)
				{
					if (isClose)
					{
						if (groupDepth > 0)
						{
							groupDepth--;
						}
					}
					else
					{
						groupDepth++;
					}
				}

				if (!isImage && isClose && groupDepth == 0)
				{
					FlushWord();
				}

				i = tagEnd;
				continue;
			}

			char c = line[i];
			if (char.IsWhiteSpace(c) && groupDepth == 0)
			{
				FlushWord();
				tokens.Add(new DiffToken(c.ToString(), string.Empty));
				i++;
				continue;
			}

			raw.Append(c);
			key.Append(c);
			i++;
		}

		FlushWord();
		return tokens;
	}

	private readonly struct DiffToken
	{
		public DiffToken(string raw, string key)
		{
			Raw = raw;
			Key = key;
		}

		public string Raw { get; }

		public string Key { get; }

		public bool IsWord => Key.Length > 0;
	}

	private static string WrapLineInUpgradeGreen(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return line;
		}

		return "[green]" + line + "[/green]";
	}

	private static Dictionary<string, List<string>> BuildUniqueBaseNumberTokensByLineKey(string[] baseLines)
	{
		Dictionary<string, List<string>> tokensByKey = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> duplicateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string baseLine in baseLines)
		{
			string key = NormalizeVisibleLineWithoutNumbers(baseLine);
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			// Include number-FREE lines too (empty token list): they still need to be MATCHABLE by key so
			// an identical, number-free line is recognized as unchanged instead of being greened whole when
			// a sibling line is inserted/removed above it (e.g. "Innate"/"固有" prepended on upgrade, which
			// shifts indices and previously left the unchanged line unmatched -> wrongly all green).
			List<string> tokens = ExtractVisibleNumberTokens(baseLine, includeHighlightedNumbers: false);

			if (tokensByKey.ContainsKey(key))
			{
				duplicateKeys.Add(key);
				continue;
			}

			tokensByKey[key] = tokens;
		}

		foreach (string duplicateKey in duplicateKeys)
		{
			tokensByKey.Remove(duplicateKey);
		}

		return tokensByKey;
	}

	private static string HighlightChangedNumbersInLine(string upgradedLine, IReadOnlyList<string> baseTokens)
	{
		// An empty base token list means every number in the line is new: each token compares
		// against a missing base token and renders green, matching vanilla upgrade-added text.
		if (string.IsNullOrWhiteSpace(upgradedLine))
		{
			return upgradedLine;
		}

		StringBuilder builder = new StringBuilder(upgradedLine.Length + 24);
		int tokenIndex = 0;
		int highlightDepth = 0;
		int imageDepth = 0;

		for (int i = 0; i < upgradedLine.Length;)
		{
			if (TryAppendTag(upgradedLine, ref i, builder, ref highlightDepth, ref imageDepth))
			{
				continue;
			}

			if (highlightDepth == 0 && imageDepth == 0 && TryReadNumericToken(upgradedLine, i, out int tokenEndExclusive))
			{
				string token = upgradedLine.Substring(i, tokenEndExclusive - i);
				string? baseToken = tokenIndex < baseTokens.Count ? baseTokens[tokenIndex] : null;
				int comparison = GetUpgradeHighlightComparison(baseToken, token);
				builder.Append(comparison != 0
					? StsTextUtilities.HighlightChangeText(token, comparison)
					: token);
				tokenIndex++;
				i = tokenEndExclusive;
				continue;
			}

			builder.Append(upgradedLine[i]);
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

			if (removeNumbers
				&& i + 3 < line.Length
				&& line[i] == '{'
				&& line[i + 1] == '{')
			{
				int tokenEnd = line.IndexOf("}}", i + 2, StringComparison.Ordinal);
				if (tokenEnd >= 0 && IsSemanticLiveNumberToken(line.Substring(i + 2, tokenEnd - i - 2)))
				{
					i = tokenEnd + 2;
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

		if (start > 0 && IsAsciiIdentifierBoundary(text[start - 1]))
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
		if (cursor < text.Length && IsAsciiIdentifierBoundary(text[cursor]))
		{
			return false;
		}

		return true;
	}

	private static bool IsAsciiIdentifierBoundary(char value)
	{
		return value == '_'
			|| (value >= 'A' && value <= 'Z')
			|| (value >= 'a' && value <= 'z');
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
			// Changed => green, matching vanilla upgrade-preview semantics (no red for buffed downsides).
			return upgradedValue == baseValue ? 0 : 1;
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
