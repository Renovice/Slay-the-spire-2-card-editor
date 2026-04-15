using System;
using System.Globalization;
using MegaCrit.Sts2.Core.Localization;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorLoc
{
	// We piggy-back on the base game's always-loaded "extensions" loc table and add our own
	// keys there via modded localization files (see: card_editor/localization/*/extensions.loc).
	private const string Table = "extensions";
	private const string Prefix = "CARD_EDITOR.";

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

			return new LocString(Table, fullKey).GetFormattedText();
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

			string rendered = loc.GetFormattedText();
			return string.IsNullOrWhiteSpace(rendered) ? fallback : rendered;
		}
		catch
		{
			return fallback;
		}
	}

	public static string Enum<TEnum>(string category, TEnum value, string fallback) where TEnum : struct, Enum
	{
		return T($"{category}.{value}", fallback);
	}
}
