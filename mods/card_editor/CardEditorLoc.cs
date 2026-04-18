using System;
using System.Globalization;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorLoc
{
	// We piggy-back on the base game's always-loaded "extensions" loc table and add our own
	// keys there via modded localization files (see: card_editor/localization/*/extensions.loc).
	private const string Table = "extensions";
	private const string Prefix = "CARD_EDITOR.";
	private static readonly Regex _zhsSpaceBeforeNumber = new(@"([\u3400-\u4DBF\u4E00-\u9FFF\u3000-\u303F])\s+(\d)", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceAfterNumber = new(@"(\d)\s+([\u3400-\u4DBF\u4E00-\u9FFF\u3000-\u303F])", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceBetweenNumberAndTag = new(@"(\d)\s+(\[[^\]]+\])", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceBetweenTagAndNumber = new(@"(\[[^\]]+\])\s+(\d)", RegexOptions.Compiled);

	public static string T(string key, string fallback)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return fallback;
		}

		string fullKey = key.StartsWith(Prefix, StringComparison.Ordinal) ? key : Prefix + key;

		try
		{
			// Mods are loaded before LocManager initializes, so we must not hard-require it here.
			if (LocManager.Instance == null)
			{
				return fallback;
			}

			if (!LocString.Exists(Table, fullKey))
			{
				return fallback;
			}

			return NormalizeLocalizedSpacing(new LocString(Table, fullKey).GetFormattedText());
		}
		catch
		{
			return fallback;
		}
	}

	public static string F(string key, string fallback, params (string Name, object? Value)[] args)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return fallback;
		}

		string fullKey = key.StartsWith(Prefix, StringComparison.Ordinal) ? key : Prefix + key;

		try
		{
			if (LocManager.Instance == null)
			{
				return fallback;
			}

			if (!LocString.Exists(Table, fullKey))
			{
				return fallback;
			}

			LocString loc = new LocString(Table, fullKey);
			if (args != null)
			{
				for (int i = 0; i < args.Length; i++)
				{
					(string name, object? value) = args[i];
					if (string.IsNullOrWhiteSpace(name) || value == null)
					{
						continue;
					}

					if (value is LocString locString)
					{
						loc.Add(name, locString);
						continue;
					}

					string formattedValue = value is IFormattable f
						? f.ToString(format: null, CultureInfo.InvariantCulture) ?? string.Empty
						: value.ToString() ?? string.Empty;
					loc.Add(name, formattedValue);
				}
			}

			string rendered = NormalizeLocalizedSpacing(loc.GetFormattedText());
			return string.IsNullOrWhiteSpace(rendered) ? fallback : rendered;
		}
		catch
		{
			return fallback;
		}
	}

	private static string NormalizeLocalizedSpacing(string rendered)
	{
		if (string.IsNullOrWhiteSpace(rendered) || !IsChineseLocaleActive())
		{
			return rendered;
		}

		string normalized = _zhsSpaceBeforeNumber.Replace(rendered, "$1$2");
		normalized = _zhsSpaceAfterNumber.Replace(normalized, "$1$2");
		normalized = _zhsSpaceBetweenNumberAndTag.Replace(normalized, "$1$2");
		normalized = _zhsSpaceBetweenTagAndNumber.Replace(normalized, "$1$2");
		return normalized;
	}

	private static bool IsChineseLocaleActive()
	{
		try
		{
			string? language = LocManager.Instance?.Language;
			return string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public static string Enum<TEnum>(string category, TEnum value, string fallback) where TEnum : struct, Enum
	{
		return T($"{category}.{value}", fallback);
	}
}
