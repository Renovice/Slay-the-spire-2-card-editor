using System;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

internal enum CardEditorMultiplayerAuthorityMode
{
	HostOnly = 0,
	AnyPlayer = 1
}

internal sealed class CardEditorMultiplayerSettingsData
{
	public bool MultiplayerSyncEnabled { get; set; } = true;
	public CardEditorMultiplayerAuthorityMode AuthorityMode { get; set; } = CardEditorMultiplayerAuthorityMode.HostOnly;
}

internal static class CardEditorMultiplayerSettings
{
	private const string SettingsPath = "user://card_editor/multiplayer_settings.json";

	private static readonly object _lock = new();
	private static CardEditorMultiplayerSettingsData _data = new();
	private static bool _loaded;

	public static int Revision { get; private set; }

	public static bool MultiplayerSyncEnabled
	{
		get
		{
			EnsureLoaded();
			return _data.MultiplayerSyncEnabled;
		}
		set
		{
			EnsureLoaded();
			if (_data.MultiplayerSyncEnabled == value)
			{
				return;
			}

			_data.MultiplayerSyncEnabled = value;
			Revision++;
			Save();
		}
	}

	public static CardEditorMultiplayerAuthorityMode AuthorityMode
	{
		get
		{
			EnsureLoaded();
			return _data.AuthorityMode;
		}
		set
		{
			EnsureLoaded();
			if (_data.AuthorityMode == value)
			{
				return;
			}

			_data.AuthorityMode = value;
			Revision++;
			Save();
		}
	}

	public static bool AllowAnyPlayerControl => AuthorityMode == CardEditorMultiplayerAuthorityMode.AnyPlayer;

	public static void EnsureLoaded()
	{
		lock (_lock)
		{
			if (_loaded)
			{
				return;
			}

			_loaded = true;
			try
			{
				string path = ProjectSettings.GlobalizePath(SettingsPath);
				if (!File.Exists(path))
				{
					return;
				}

				string json = File.ReadAllText(path);
				CardEditorMultiplayerSettingsData? loaded = JsonSerializer.Deserialize<CardEditorMultiplayerSettingsData>(json, CreateJsonOptions());
				if (loaded != null)
				{
					_data = loaded;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed loading multiplayer settings: {ex}");
			}
		}
	}

	private static void Save()
	{
		lock (_lock)
		{
			try
			{
				string path = ProjectSettings.GlobalizePath(SettingsPath);
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				string json = JsonSerializer.Serialize(_data, CreateJsonOptions());
				File.WriteAllText(path, json);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][MultiplayerSync] Failed saving multiplayer settings: {ex}");
			}
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
