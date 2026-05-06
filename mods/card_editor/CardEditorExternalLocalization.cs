using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

public static class CardEditorExternalLocalization
{
	private static bool _subscribed;
	private static readonly IReadOnlyDictionary<string, string[]> _languageAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
	{
		["zhs"] = ["zh", "zh_cn", "zh-cn", "chinese", "zht", "zh_tw", "zh-tw", "traditional_chinese"],
		["zh"] = ["zhs", "zh_cn", "zh-cn", "chinese", "zht", "zh_tw", "zh-tw", "traditional_chinese"],
		["zh_cn"] = ["zhs", "zh", "zh-cn", "chinese"],
		["zh-cn"] = ["zhs", "zh", "zh_cn", "chinese"],
		["zht"] = ["zhs", "zh", "zh_tw", "zh-tw", "traditional_chinese", "chinese"],
		["zh_tw"] = ["zhs", "zh", "zht", "zh-tw", "traditional_chinese", "chinese"],
		["zh-tw"] = ["zhs", "zh", "zht", "zh_tw", "traditional_chinese", "chinese"],
		["traditional_chinese"] = ["zhs", "zh", "zht", "zh_tw", "zh-tw", "chinese"],
		["kor"] = ["ko", "ko_kr", "ko-kr", "korean"],
		["ko"] = ["kor", "ko_kr", "ko-kr", "korean"],
		["ko_kr"] = ["kor", "ko", "ko-kr", "korean"],
		["ko-kr"] = ["kor", "ko", "ko_kr", "korean"],
		["korean"] = ["kor", "ko", "ko_kr", "ko-kr"]
	};

	public static void Init()
	{
		TryApply();
		TrySubscribe();
	}

	private static void TrySubscribe()
	{
		if (_subscribed)
		{
			return;
		}

		try
		{
			if (LocManager.Instance == null)
			{
				return;
			}

			LocManager.Instance.SubscribeToLocaleChange(() =>
			{
				TryApply();
				CardEditorUiState.RefreshLastLibraryIfActive();
			});
			_subscribed = true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed subscribing to locale changes: {ex}");
		}
	}

	private static void TryApply()
	{
		try
		{
			Apply();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed applying external localization: {ex}");
		}
	}

	private static void Apply()
	{
		if (LocManager.Instance == null)
		{
			return;
		}

		string language = LocManager.Instance.Language ?? "eng";
		string modDir = GetModDirectory();
		if (string.IsNullOrWhiteSpace(modDir))
		{
			return;
		}

		// Merge editable files after the game's normal loc load. These files are intentionally
		// outside the PCK so users can translate them without rebuilding the mod package.
		List<string> searchDirs = BuildSearchDirectories(modDir, language);

		MergeTableFromExisting(searchDirs, "cards.loc", "cards", "CARD_EDITOR_");
		CardEditorBuiltTinkerCard.EnsureLocalization();
		MergeTableFromExisting(searchDirs, "extensions.loc", "extensions", "CARD_EDITOR.");
		MergeTableFromExisting(searchDirs, "settings_ui.loc", "settings_ui", requiredKeyPrefix: null);

		// Runtime tweaks for vanilla strings we want to parameterize (no external files needed).
		CardEditorTargetedDiscardLocalizationOverrides.TryApply();
		CardEditorLoc.InvalidateCache();
		CardEditorLocalizationWarmup.WarmCurrentLanguage();
		NCardEditorPopup.InvalidateLocalizationSensitiveCaches();
	}

	private static List<string> BuildSearchDirectories(string modDir, string language)
	{
		List<string> searchDirs = [];
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		void AddDir(string dir)
		{
			if (string.IsNullOrWhiteSpace(dir))
			{
				return;
			}

			string normalized;
			try
			{
				normalized = Path.GetFullPath(dir);
			}
			catch
			{
				normalized = dir;
			}

			if (seen.Add(normalized))
			{
				searchDirs.Add(normalized);
			}
		}

		void AddLanguageDirs(string locRoot, string lang)
		{
			AddDir(Path.Combine(locRoot, lang));
			if (_languageAliases.TryGetValue(lang, out string[]? aliases))
			{
				foreach (string alias in aliases)
				{
					AddDir(Path.Combine(locRoot, alias));
				}
			}
		}

		string externalLocRoot = Path.Combine(modDir, "localization");
		AddLanguageDirs(externalLocRoot, language);

		// Legacy Chinese installs used localization/extensions.loc directly. Keep it as a final
		// override so existing user-edited Simplified/Traditional Chinese files keep working.
		if (IsChineseLanguage(language))
		{
			AddDir(externalLocRoot);
			AddDir(Path.Combine(modDir, "chinese"));
		}

		// Support mistaken nested extractions like mods/card_editor/card_editor/localization/zhs.
		AddLanguageDirs(Path.Combine(modDir, "card_editor", "localization"), language);

		return searchDirs;
	}

	private static bool IsChineseLanguage(string language)
	{
		return string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zh_cn", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zh-cn", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zht", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zh_tw", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "zh-tw", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "chinese", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(language, "traditional_chinese", StringComparison.OrdinalIgnoreCase);
	}

	private static void MergeTableFromExisting(IReadOnlyList<string> searchDirs, string fileName, string tableName, string? requiredKeyPrefix)
	{
		foreach (string dir in searchDirs)
		{
			string path = Path.Combine(dir, fileName);
			if (!File.Exists(path))
			{
				continue;
			}

			Dictionary<string, string> dict = LoadTranslationFile(path);
			if (dict.Count == 0)
			{
				continue;
			}

			Dictionary<string, string> filtered = requiredKeyPrefix == null
				? dict
				: dict
					.Where(kvp => kvp.Key.StartsWith(requiredKeyPrefix, StringComparison.Ordinal))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			if (filtered.Count == 0)
			{
				Log.Warn($"[CardEditor] External localization file had no usable keys for table='{tableName}' path='{path}'");
				continue;
			}

			try
			{
				LocTable table = LocManager.Instance!.GetTable(tableName);
				table.MergeWith(filtered);
				CardEditorMod.VerboseLog($"[CardEditor] Loaded external localization override: table={tableName} entries={filtered.Count} path='{path}'");
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed merging external localization table '{tableName}' from '{path}': {ex}");
			}
		}
	}

	private static Dictionary<string, string> LoadTranslationFile(string path)
	{
		try
		{
			string json = File.ReadAllText(path, Encoding.UTF8);
			Dictionary<string, string>? dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
			return dict ?? new Dictionary<string, string>();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed reading translation file '{path}': {ex}");
			return new Dictionary<string, string>();
		}
	}

	private static string GetModDirectory()
	{
		try
		{
			string? location = Assembly.GetExecutingAssembly().Location;
			if (string.IsNullOrWhiteSpace(location))
			{
				return "";
			}

			return Path.GetDirectoryName(location) ?? "";
		}
		catch
		{
			return "";
		}
	}
}
