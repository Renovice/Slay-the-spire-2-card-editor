using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorPerformanceSettingsData
{
	public bool PreloadEditorPopupsOnLaunch { get; set; } = true;
	public bool LegacyPreloadEveryEditorPopupOnLaunch { get; set; } = false;
	public bool IncrementalPopupHydrationOnOpen { get; set; } = false;
	public bool BackgroundPopupWarmupAfterDirtyClose { get; set; } = false;
	public double BackgroundPopupWarmupDelaySeconds { get; set; } = 0.08;
	public bool EnableCardEditorAutoPlayLoopCap { get; set; } = true;
	public int CardEditorAutoPlayLoopCapPerTurn { get; set; } = 100;
	public bool PreventCardEditorAutoPlaySelfLoops { get; set; } = false;
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

	public static bool EnableCardEditorAutoPlayLoopCap
	{
		get
		{
			EnsureLoaded();
			return _data.EnableCardEditorAutoPlayLoopCap;
		}
	}

	public static int CardEditorAutoPlayLoopCapPerTurn
	{
		get
		{
			EnsureLoaded();
			return Math.Clamp(_data.CardEditorAutoPlayLoopCapPerTurn, 1, 999);
		}
	}

	public static bool PreventCardEditorAutoPlaySelfLoops
	{
		get
		{
			EnsureLoaded();
			return _data.PreventCardEditorAutoPlaySelfLoops;
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

				string json = File.ReadAllText(path);
				CardEditorPerformanceSettingsData? loaded = JsonSerializer.Deserialize<CardEditorPerformanceSettingsData>(json, CreateJsonOptions());
				if (loaded != null)
				{
					_data = loaded;
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
		try
		{
			string? directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			string json = JsonSerializer.Serialize(_data, CreateJsonOptions());
			File.WriteAllText(path, json);
			Log.Info($"[CardEditor] Created performance settings file: {path}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed creating performance settings file '{path}': {ex}");
		}
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
			WriteIndented = true
		};
	}
}
