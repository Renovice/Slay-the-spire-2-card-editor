using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace SlayTheSpire2Mod.CardEditor;

// Persists the Chainboard's board-only sequence links (drag-attach grouping) per card, so
// chains held together purely visually survive closing and reopening the editor.
// UI sidecar only: never part of the card override DTO or the multiplayer snapshot.
internal static class CardEditorChainLinkStore
{
	private const string StorePath = "user://card_editor/chain_links.json";

	// cardKey -> (effectId -> sourceEffectId)
	private static Dictionary<string, Dictionary<string, string>>? _byCard;

	private static Dictionary<string, Dictionary<string, string>> Load()
	{
		if (_byCard != null)
		{
			return _byCard;
		}

		try
		{
			string path = ProjectSettings.GlobalizePath(StorePath);
			if (File.Exists(path))
			{
				_byCard = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Chain-link store load failed: {ex.Message}");
		}

		return _byCard ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
	}

	internal static Dictionary<string, string> Get(string cardKey)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		return Load().TryGetValue(cardKey, out Dictionary<string, string>? links)
			? new Dictionary<string, string>(links, StringComparer.Ordinal)
			: new Dictionary<string, string>(StringComparer.Ordinal);
	}

	internal static void Set(string cardKey, Dictionary<string, string> links)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return;
		}

		Dictionary<string, Dictionary<string, string>> byCard = Load();
		if (links == null || links.Count == 0)
		{
			byCard.Remove(cardKey);
		}
		else
		{
			byCard[cardKey] = new Dictionary<string, string>(links, StringComparer.Ordinal);
		}

		try
		{
			string path = ProjectSettings.GlobalizePath(StorePath);
			string? dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			File.WriteAllText(path, JsonSerializer.Serialize(byCard, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Chain-link store save failed: {ex.Message}");
		}
	}
}
