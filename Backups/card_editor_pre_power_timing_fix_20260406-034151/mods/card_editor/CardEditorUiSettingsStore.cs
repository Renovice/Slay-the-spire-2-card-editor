using System;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorUiSettingsStore
{
	private const string SettingsPath = "user://card_editor/ui_settings.json";

	private sealed class UiSettingsDto
	{
		public bool AdvancedMode { get; set; }
	}

	private static bool? _advancedModeCached;

	public static bool GetAdvancedMode()
	{
		_advancedModeCached ??= ReadSettingsInternal().AdvancedMode;
		return _advancedModeCached.Value;
	}

	public static void SetAdvancedMode(bool enabled)
	{
		_advancedModeCached = enabled;

		try
		{
			string path = ProjectSettings.GlobalizePath(SettingsPath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			UiSettingsDto data = new UiSettingsDto
			{
				AdvancedMode = enabled
			};

			string json = JsonSerializer.Serialize(data, CreateJsonOptions());
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed saving UI settings: {ex}");
		}
	}

	private static UiSettingsDto ReadSettingsInternal()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(SettingsPath);
			if (!File.Exists(path))
			{
				return new UiSettingsDto();
			}

			string json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<UiSettingsDto>(json, CreateJsonOptions()) ?? new UiSettingsDto();
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed reading UI settings: {ex}");
			return new UiSettingsDto();
		}
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}
}

