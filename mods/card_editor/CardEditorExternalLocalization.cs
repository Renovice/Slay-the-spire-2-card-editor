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
		["zhs"] = ["chinese"],
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

		// Merge localization in order: base file first, then any user override. This avoids
		// confusion when multiple files exist and a user edits the "wrong" one.
		List<string> searchDirs = BuildSearchDirectories(modDir, language);

		MergeTableFromExisting(searchDirs, "extensions.loc", "extensions");
		MergeTableFromExisting(searchDirs, "settings_ui.loc", "settings_ui");

		// Runtime tweaks for vanilla strings we want to parameterize (no external files needed).
		CardEditorTargetedDiscardLocalizationOverrides.TryApply();
		CardEditorLoc.InvalidateCache();
		CardEditorLocalizationWarmup.WarmCurrentLanguage();
	}

	private static List<string> BuildSearchDirectories(string modDir, string language)
	{
		HashSet<string> searchDirs = new(StringComparer.OrdinalIgnoreCase)
		{
			Path.Combine(modDir, "localization", language)
		};

		if (_languageAliases.TryGetValue(language, out string[]? aliases))
		{
			foreach (string alias in aliases)
			{
				searchDirs.Add(Path.Combine(modDir, "localization", alias));
			}
		}

		if (string.Equals(language, "zhs", StringComparison.OrdinalIgnoreCase))
		{
			searchDirs.Add(Path.Combine(modDir, "chinese"));
		}

		return searchDirs.ToList();
	}

	private static void MergeTableFromExisting(IReadOnlyList<string> searchDirs, string fileName, string tableName)
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

			Dictionary<string, string> filtered = dict
				.Where(kvp => kvp.Key.StartsWith("CARD_EDITOR.", StringComparison.Ordinal))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			if (filtered.Count == 0)
			{
				continue;
			}

			try
			{
				LocTable table = LocManager.Instance!.GetTable(tableName);
				table.MergeWith(filtered);
				Log.Info($"[CardEditor] Loaded external localization override: {tableName} ({Path.GetFileName(path)})");
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
