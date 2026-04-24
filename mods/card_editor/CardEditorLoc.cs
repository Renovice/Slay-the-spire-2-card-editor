using System;
using System.Collections.Generic;
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
	private static readonly Dictionary<string, string> _textCache = new(StringComparer.Ordinal);
	private static readonly HashSet<string> _existingKeyCache = new(StringComparer.Ordinal);
	private static readonly HashSet<string> _missingKeyCache = new(StringComparer.Ordinal);
	private static readonly Regex _zhsSpaceBeforeNumber = new(@"([\u3400-\u4DBF\u4E00-\u9FFF\u3000-\u303F])\s+(\d)", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceAfterNumber = new(@"(\d)\s+([\u3400-\u4DBF\u4E00-\u9FFF\u3000-\u303F])", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceBetweenNumberAndTag = new(@"(\d)\s+(\[[^\]]+\])", RegexOptions.Compiled);
	private static readonly Regex _zhsSpaceBetweenTagAndNumber = new(@"(\[[^\]]+\])\s+(\d)", RegexOptions.Compiled);
	private static string _cachedLanguage = string.Empty;
	private static bool _isChineseLocaleCached;

	public static void InvalidateCache()
	{
		_textCache.Clear();
		_existingKeyCache.Clear();
		_missingKeyCache.Clear();
		_cachedLanguage = string.Empty;
		_isChineseLocaleCached = false;
	}

	public static void Prewarm(IEnumerable<string> keys)
	{
		if (keys == null || LocManager.Instance == null)
		{
			return;
		}

		EnsureLocaleCacheState();
		foreach (string key in keys)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			string fullKey = NormalizeKey(key);
			string cacheKey = BuildCacheKey(fullKey);
			if (_textCache.ContainsKey(cacheKey) || !HasTranslation(fullKey, cacheKey))
			{
				continue;
			}

			// Warm the cache without formatting template strings like "{Payload}" that need runtime args.
			LocString loc = new LocString(Table, fullKey);
			_textCache[cacheKey] = NormalizeLocalizedSpacing(loc.GetRawText());
		}
	}

	public static string T(string key, string fallback)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return fallback;
		}

		string fullKey = NormalizeKey(key);

		try
		{
			// Mods are loaded before LocManager initializes, so we must not hard-require it here.
			if (LocManager.Instance == null)
			{
				return fallback;
			}
			EnsureLocaleCacheState();
			return TryGetTranslatedText(fullKey, out string? value) ? value : fallback;
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

		string fullKey = NormalizeKey(key);

		try
		{
			if (LocManager.Instance == null)
			{
				return fallback;
			}
			EnsureLocaleCacheState();

			string cacheKey = BuildCacheKey(fullKey);
			if (!HasTranslation(fullKey, cacheKey))
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
		if (string.IsNullOrWhiteSpace(rendered))
		{
			return rendered;
		}

		EnsureLocaleCacheState();
		if (!_isChineseLocaleCached)
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
		EnsureLocaleCacheState();
		return _isChineseLocaleCached;
	}

	public static string Enum<TEnum>(string category, TEnum value, string fallback) where TEnum : struct, Enum
	{
		return T($"{category}.{value}", fallback);
	}

	private static string NormalizeKey(string key)
	{
		return key.StartsWith(Prefix, StringComparison.Ordinal) ? key : Prefix + key;
	}

	private static string BuildCacheKey(string fullKey)
	{
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{_cachedLanguage}\u001f{fullKey}");
	}

	private static bool HasTranslation(string fullKey, string cacheKey)
	{
		if (_existingKeyCache.Contains(cacheKey))
		{
			return true;
		}

		if (_missingKeyCache.Contains(cacheKey))
		{
			return false;
		}

		bool exists = LocString.Exists(Table, fullKey);
		if (exists)
		{
			_existingKeyCache.Add(cacheKey);
		}
		else
		{
			_missingKeyCache.Add(cacheKey);
		}

		return exists;
	}

	private static bool TryGetTranslatedText(string fullKey, out string value)
	{
		string cacheKey = BuildCacheKey(fullKey);
		if (_textCache.TryGetValue(cacheKey, out value!))
		{
			return true;
		}

		if (!HasTranslation(fullKey, cacheKey))
		{
			value = string.Empty;
			return false;
		}

		LocString loc = new LocString(Table, fullKey);
		try
		{
			value = NormalizeLocalizedSpacing(loc.GetFormattedText());
		}
		catch
		{
			value = NormalizeLocalizedSpacing(loc.GetRawText());
		}
		_textCache[cacheKey] = value;
		return true;
	}

	private static void EnsureLocaleCacheState()
	{
		string language = GetCurrentLanguage();
		if (string.Equals(language, _cachedLanguage, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		_cachedLanguage = language;
		_isChineseLocaleCached = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
		_textCache.Clear();
		_existingKeyCache.Clear();
		_missingKeyCache.Clear();
	}

	private static string GetCurrentLanguage()
	{
		try
		{
			return LocManager.Instance?.Language ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}
}
