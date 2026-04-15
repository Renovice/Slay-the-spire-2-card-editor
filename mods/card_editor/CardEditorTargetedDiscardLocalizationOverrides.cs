using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTargetedDiscardLocalizationOverrides
{
	private static readonly string[] _keys =
	{
		"ACROBATICS.description",
		"SURVIVOR.description",
		"DAGGER_THROW.description"
	};

	internal static void TryApply()
	{
		try
		{
			Apply();
		}
		catch
		{
		}
	}

	private static void Apply()
	{
		if (LocManager.Instance == null)
		{
			return;
		}

		string language = LocManager.Instance.Language ?? "eng";
		LocTable table;
		try
		{
			table = LocManager.Instance.GetTable("cards");
		}
		catch
		{
			return;
		}

		Dictionary<string, string> overrides = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string key in _keys)
		{
			string raw;
			try
			{
				raw = table.GetRawText(key);
			}
			catch
			{
				continue;
			}

			if (raw.Contains("{" + CardEditorTargetedDiscardSupport.DiscardCountVarName, StringComparison.Ordinal))
			{
				continue;
			}

			string updated = BuildUpdatedRawText(raw, language);
			if (!string.Equals(updated, raw, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(updated))
			{
				overrides[key] = updated;
			}
		}

		if (overrides.Count > 0)
		{
			table.MergeWith(overrides);
		}
	}

	private static string BuildUpdatedRawText(string raw, string language)
	{
		string token = "{" + CardEditorTargetedDiscardSupport.DiscardCountVarName + ":diff()}";

		if (string.Equals(language, "eng", StringComparison.OrdinalIgnoreCase))
		{
			// English needs pluralization ("card" vs "cards").
			string replacement = "Discard " + token + " {" + CardEditorTargetedDiscardSupport.DiscardCountVarName + ":plural:card|cards}.";
			if (raw.Contains("Discard 1 card.", StringComparison.Ordinal))
			{
				return raw.Replace("Discard 1 card.", replacement, StringComparison.Ordinal);
			}
		}

		// Locale-agnostic fallback: replace the last standalone "1" (usually the discard count).
		return ReplaceLastStandaloneDigit(raw, '1', token);
	}

	private static string ReplaceLastStandaloneDigit(string raw, char digit, string replacement)
	{
		if (string.IsNullOrEmpty(raw))
		{
			return raw;
		}

		for (int i = raw.LastIndexOf(digit); i >= 0; i = raw.LastIndexOf(digit, i - 1))
		{
			char before = i > 0 ? raw[i - 1] : '\0';
			char after = i + 1 < raw.Length ? raw[i + 1] : '\0';
			if ((before == '\0' || !char.IsDigit(before)) && (after == '\0' || !char.IsDigit(after)))
			{
				return raw.Substring(0, i) + replacement + raw.Substring(i + 1);
			}
		}

		return raw;
	}
}

