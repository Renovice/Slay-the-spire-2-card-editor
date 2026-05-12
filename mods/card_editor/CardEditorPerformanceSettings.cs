using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorPerformanceSettingsData
{
	public bool PreloadEditorPopupsOnLaunch { get; set; } = false;
	public bool LegacyPreloadEveryEditorPopupOnLaunch { get; set; } = false;
	public bool IncrementalPopupHydrationOnOpen { get; set; } = true;
	public bool BackgroundPopupWarmupAfterDirtyClose { get; set; } = true;
	public double BackgroundPopupWarmupDelaySeconds { get; set; } = 0.08;
	public bool VerboseLogging { get; set; } = false;
	public bool VerboseDamageDebugLogging { get; set; } = false;
}

internal static class CardEditorPerformanceSettings
{
	// The game treats any .json under mods/ as a mod manifest, so this file uses JSON content
	// with a .txt extension to avoid manifest loading errors.
	private const string SettingsFileName = "card_editor_settings.txt";

	private static readonly object _lock = new();
	private static CardEditorPerformanceSettingsData _data = new();
	private static bool _loaded;

	public static bool PreloadEditorPopupsOnLaunch
	{
		get
		{
			EnsureLoaded();
			return _data.PreloadEditorPopupsOnLaunch;
		}
	}

	public static bool LegacyPreloadEveryEditorPopupOnLaunch
	{
		get
		{
			EnsureLoaded();
			return _data.LegacyPreloadEveryEditorPopupOnLaunch;
		}
	}

	public static bool VerboseLogging
	{
		get
		{
			EnsureLoaded();
			return _data.VerboseLogging;
		}
	}

	public static bool IncrementalPopupHydrationOnOpen
	{
		get
		{
			EnsureLoaded();
			return _data.IncrementalPopupHydrationOnOpen;
		}
	}

	public static bool BackgroundPopupWarmupAfterDirtyClose
	{
		get
		{
			EnsureLoaded();
			return _data.BackgroundPopupWarmupAfterDirtyClose;
		}
	}

	public static double BackgroundPopupWarmupDelaySeconds
	{
		get
		{
			EnsureLoaded();
			return Math.Max(0.0, _data.BackgroundPopupWarmupDelaySeconds);
		}
	}

	public static bool VerboseDamageDebugLogging
	{
		get
		{
			EnsureLoaded();
			return _data.VerboseDamageDebugLogging;
		}
	}

	public static void SetPreloadEditorPopupsOnLaunch(bool value)
	{
		EnsureLoaded();
		lock (_lock)
		{
			if (_data.PreloadEditorPopupsOnLaunch == value)
			{
				return;
			}

			_data.PreloadEditorPopupsOnLaunch = value;
			WriteSettings(GetSettingsPath(), created: false);
		}
	}

	public static void SetLegacyPreloadEveryEditorPopupOnLaunch(bool value)
	{
		EnsureLoaded();
		lock (_lock)
		{
			if (_data.LegacyPreloadEveryEditorPopupOnLaunch == value)
			{
				return;
			}

			_data.LegacyPreloadEveryEditorPopupOnLaunch = value;
			WriteSettings(GetSettingsPath(), created: false);
		}
	}

	public static void EnsureLoaded()
	{
		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}

			_loaded = true;
			string path = GetSettingsPath();
			try
			{
				if (!File.Exists(path))
				{
					WriteDefaultSettings(path);
					return;
				}

				string json = NormalizeLegacyBooleanLiterals(ReadAllTextShared(path));
				CardEditorPerformanceSettingsData? loaded = JsonSerializer.Deserialize<CardEditorPerformanceSettingsData>(json, CreateJsonOptions());
				if (loaded != null)
				{
					_data = loaded;
					WriteSettings(path, created: false);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed loading performance settings from '{path}': {ex}");
			}
		}
	}

	private static void WriteDefaultSettings(string path)
	{
		WriteSettings(path, created: true);
	}

	private static void WriteSettings(string path, bool created)
	{
		try
		{
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			string json = SerializeSettings(_data);
			WriteAllTextShared(path, json);
			if (created)
			{
				Log.Info($"[CardEditor] Created performance settings file: {path}");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed writing performance settings file '{path}': {ex}");
		}
	}

	private static string SerializeSettings(CardEditorPerformanceSettingsData data)
	{
		StringBuilder builder = new();
		builder.AppendLine("{");
		builder.AppendLine("  // Popup performance");
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.PreloadEditorPopupsOnLaunch), data.PreloadEditorPopupsOnLaunch, comma: true);
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.LegacyPreloadEveryEditorPopupOnLaunch), data.LegacyPreloadEveryEditorPopupOnLaunch, comma: true);
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.IncrementalPopupHydrationOnOpen), data.IncrementalPopupHydrationOnOpen, comma: true);
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.BackgroundPopupWarmupAfterDirtyClose), data.BackgroundPopupWarmupAfterDirtyClose, comma: true);
		AppendNumber(builder, nameof(CardEditorPerformanceSettingsData.BackgroundPopupWarmupDelaySeconds), Math.Max(0.0, data.BackgroundPopupWarmupDelaySeconds), comma: true);
		builder.AppendLine();
		builder.AppendLine("  // Diagnostics");
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.VerboseLogging), data.VerboseLogging, comma: true);
		AppendBool(builder, nameof(CardEditorPerformanceSettingsData.VerboseDamageDebugLogging), data.VerboseDamageDebugLogging, comma: false);
		builder.AppendLine("}");
		return builder.ToString();
	}

	private static void AppendBool(StringBuilder builder, string name, bool value, bool comma)
	{
		builder.Append("  \"")
			.Append(name)
			.Append("\": ")
			.Append(value ? "true" : "false")
			.AppendLine(comma ? "," : "");
	}

	private static void AppendNumber(StringBuilder builder, string name, double value, bool comma)
	{
		builder.Append("  \"")
			.Append(name)
			.Append("\": ")
			.Append(value.ToString("0.###", CultureInfo.InvariantCulture))
			.AppendLine(comma ? "," : "");
	}

	private static string ReadAllTextShared(string path)
	{
		using FileStream stream = new(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
		return reader.ReadToEnd();
	}

	private static void WriteAllTextShared(string path, string content)
	{
		using FileStream stream = new(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read);
		using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		writer.Write(content);
	}

	private static string NormalizeLegacyBooleanLiterals(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}

		StringBuilder builder = new(text.Length);
		bool inString = false;
		bool escaped = false;
		for (int i = 0; i < text.Length;)
		{
			char c = text[i];
			if (inString)
			{
				builder.Append(c);
				if (escaped)
				{
					escaped = false;
				}
				else if (c == '\\')
				{
					escaped = true;
				}
				else if (c == '"')
				{
					inString = false;
				}
				i++;
				continue;
			}

			if (c == '"')
			{
				inString = true;
				builder.Append(c);
				i++;
				continue;
			}

			if (MatchesJsonWord(text, i, "True"))
			{
				builder.Append("true");
				i += 4;
				continue;
			}

			if (MatchesJsonWord(text, i, "False"))
			{
				builder.Append("false");
				i += 5;
				continue;
			}

			builder.Append(c);
			i++;
		}

		return builder.ToString();
	}

	private static bool MatchesJsonWord(string text, int index, string word)
	{
		if (index < 0 || index + word.Length > text.Length)
		{
			return false;
		}

		if (!text.AsSpan(index, word.Length).SequenceEqual(word.AsSpan()))
		{
			return false;
		}

		return !IsJsonWordChar(index > 0 ? text[index - 1] : '\0')
			&& !IsJsonWordChar(index + word.Length < text.Length ? text[index + word.Length] : '\0');
	}

	private static bool IsJsonWordChar(char c)
	{
		return char.IsLetterOrDigit(c) || c == '_';
	}

	private static string GetSettingsPath()
	{
		string modDir = GetModDirectory();
		if (!string.IsNullOrWhiteSpace(modDir))
		{
			return Path.Combine(modDir, SettingsFileName);
		}

		return ProjectSettings.GlobalizePath($"user://card_editor/{SettingsFileName}");
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

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			WriteIndented = true
		};
	}
}
