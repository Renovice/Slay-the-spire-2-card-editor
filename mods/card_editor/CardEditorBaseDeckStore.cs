using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckStore
{
	private const int CurrentVersion = 1;
	private const string StorePath = "user://card_editor/base_decks.json";

	private static readonly Dictionary<ModelId, List<ModelId>> _overrides = new();
	private static bool _loaded;

	// Lazy cache for supported character ids and the corresponding HashSet for O(1) Contains.
	// ModelDb.AllCharacters is fixed for the session (no runtime character-mutation API exists).
	// Order is preserved from GetSupportedCharacters() so existing callers that depend on it are unaffected.
	private static IReadOnlyList<ModelId>? _supportedCharacterIdsCache;
	private static HashSet<ModelId>? _supportedCharacterIdSet;

	public static IReadOnlyList<ModelId> SupportedCharacterIds => GetSupportedCharacterIds();
	public static int Revision { get; private set; }

	public static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
		_overrides.Clear();

		try
		{
			string path = ProjectSettings.GlobalizePath(StorePath);
			if (!File.Exists(path))
			{
				return;
			}

			string json = File.ReadAllText(path);
			BaseDeckStoreDto? dto = JsonSerializer.Deserialize<BaseDeckStoreDto>(json, CreateJsonOptions());
			if (dto?.Decks == null || dto.Version <= 0 || dto.Version > CurrentVersion)
			{
				return;
			}

			foreach ((string key, List<string> cardIds) in dto.Decks)
			{
				if (!TryParseModelId(key, out ModelId characterId) || !IsSupportedCharacterId(characterId))
				{
					continue;
				}

				List<ModelId> parsed = new();
				foreach (string rawCardId in cardIds ?? Enumerable.Empty<string>())
				{
					if (TryParseModelId(rawCardId, out ModelId cardId) && ModelDb.GetByIdOrNull<CardModel>(cardId) != null)
					{
						parsed.Add(cardId);
					}
				}

				_overrides[characterId] = parsed;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading base deck store: {ex}");
		}
	}

	public static CharacterModel GetCharacter(ModelId characterId)
	{
		return ModelDb.GetById<CharacterModel>(characterId);
	}

	public static string GetCharacterTitle(ModelId characterId)
	{
		try
		{
			return GetCharacter(characterId).Title.GetRawText();
		}
		catch
		{
			return characterId.Entry ?? characterId.ToString();
		}
	}

	public static bool IsSupportedCharacterId(ModelId characterId)
	{
		if (characterId == null || characterId == ModelId.none)
		{
			return false;
		}

		// Ensure the cache is populated (idempotent), then use O(1) HashSet lookup.
		GetSupportedCharacterIds();
		return _supportedCharacterIdSet?.Contains(characterId) == true;
	}

	public static ModelId GetDefaultCharacterId()
	{
		EnsureLoaded();
		IReadOnlyList<ModelId> supported = SupportedCharacterIds;
		return supported.Count > 0 ? supported[0] : ModelId.none;
	}

	public static List<ModelId> GetDeckIds(ModelId characterId)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId))
		{
			return new List<ModelId>();
		}

		if (_overrides.TryGetValue(characterId, out List<ModelId>? ids))
		{
			return new List<ModelId>(ids);
		}

		return GetCharacter(characterId).StartingDeck
			.Where(c => c != null && c.Id != null)
			.Select(c => c.Id)
			.ToList();
	}

	public static List<CardModel> GetDeckPreviewCards(ModelId characterId)
	{
		List<CardModel> cards = new();
		foreach (ModelId cardId in GetDeckIds(characterId))
		{
			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(cardId);
			if (canonical == null)
			{
				continue;
			}

			try
			{
				cards.Add(CardEditorOverrides.BuildPreview(canonical));
			}
			catch
			{
				cards.Add(canonical);
			}
		}
		return cards;
	}

	public static bool HasOverride(ModelId characterId)
	{
		EnsureLoaded();
		return _overrides.ContainsKey(characterId);
	}

	public static void SetDeck(ModelId characterId, IEnumerable<ModelId> cardIds)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId))
		{
			return;
		}

		List<ModelId> sanitized = cardIds
			.Where(id => id != null && id != ModelId.none && ModelDb.GetByIdOrNull<CardModel>(id) != null)
			.ToList();
		_overrides[characterId] = sanitized;
		Revision++;
	}

	public static void AddCard(ModelId characterId, ModelId cardId)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId) || cardId == null || cardId == ModelId.none || ModelDb.GetByIdOrNull<CardModel>(cardId) == null)
		{
			return;
		}

		List<ModelId> cards = GetDeckIds(characterId);
		cards.Add(cardId);
		_overrides[characterId] = cards;
		Revision++;
	}

	public static void AddCards(ModelId characterId, IEnumerable<ModelId> cardIds)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId))
		{
			return;
		}

		List<ModelId> cards = GetDeckIds(characterId);
		bool changed = false;
		foreach (ModelId cardId in cardIds)
		{
			if (cardId == null || cardId == ModelId.none || ModelDb.GetByIdOrNull<CardModel>(cardId) == null)
			{
				continue;
			}
			cards.Add(cardId);
			changed = true;
		}

		if (!changed)
		{
			return;
		}

		_overrides[characterId] = cards;
		Revision++;
	}

	public static void RemoveOne(ModelId characterId, ModelId cardId)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId))
		{
			return;
		}

		List<ModelId> cards = GetDeckIds(characterId);
		int index = cards.FindIndex(id => id == cardId);
		if (index < 0)
		{
			return;
		}

		cards.RemoveAt(index);
		_overrides[characterId] = cards;
		Revision++;
	}

	public static void ResetToVanilla(ModelId characterId)
	{
		EnsureLoaded();
		if (!IsSupportedCharacterId(characterId))
		{
			return;
		}

		_overrides.Remove(characterId);
		Revision++;
	}

	public static Dictionary<ModelId, List<ModelId>> ExportSnapshot()
	{
		EnsureLoaded();
		return _overrides.ToDictionary(
			kvp => kvp.Key,
			kvp => new List<ModelId>(kvp.Value));
	}

	public static void ImportSnapshot(IReadOnlyDictionary<ModelId, List<ModelId>>? snapshot)
	{
		EnsureLoaded();
		_overrides.Clear();

		if (snapshot == null)
		{
			return;
		}

		foreach ((ModelId characterId, List<ModelId> cardIds) in snapshot)
		{
			if (!IsSupportedCharacterId(characterId))
			{
				continue;
			}

			List<ModelId> sanitized = (cardIds ?? new List<ModelId>())
				.Where(cardId => cardId != null && cardId != ModelId.none && ModelDb.GetByIdOrNull<CardModel>(cardId) != null)
				.ToList();

			_overrides[characterId] = sanitized;
		}

		Revision++;
	}

	public static void ClearAllOverrides()
	{
		EnsureLoaded();
		if (_overrides.Count == 0)
		{
			return;
		}

		_overrides.Clear();
		Revision++;
	}

	public static bool TryGetCharacterIdForCard(CardModel? card, out ModelId characterId)
	{
		if (card == null)
		{
			characterId = ModelId.none;
			return false;
		}

		return TryGetCharacterIdForCardPool(card.VisualCardPool ?? card.Pool, out characterId);
	}

	public static bool TryGetCharacterIdForCardPool(CardPoolModel? pool, out ModelId characterId)
	{
		if (pool != null)
		{
			foreach (CharacterModel character in GetSupportedCharacters())
			{
				try
				{
					CardPoolModel? characterPool = character.CardPool;
					if (characterPool == null)
					{
						continue;
					}

					if (characterPool == pool
						|| characterPool.Id == pool.Id
						|| string.Equals(characterPool.Title, pool.Title, StringComparison.OrdinalIgnoreCase))
					{
						characterId = character.Id;
						return true;
					}
				}
				catch
				{
				}
			}
		}

		characterId = ModelId.none;
		return false;
	}

	private static IReadOnlyList<ModelId> GetSupportedCharacterIds()
	{
		// ModelDb.AllCharacters is fixed for a session; cache to avoid re-running the LINQ
		// chain on every SupportedCharacterIds access and every IsSupportedCharacterId call.
		if (_supportedCharacterIdsCache != null)
		{
			return _supportedCharacterIdsCache;
		}

		IReadOnlyList<ModelId> ids = GetSupportedCharacters()
			.Select(character => character.Id)
			.ToList();

		_supportedCharacterIdsCache = ids;
		_supportedCharacterIdSet = new HashSet<ModelId>(ids);
		return _supportedCharacterIdsCache;
	}

	private static List<CharacterModel> GetSupportedCharacters()
	{
		try
		{
			return ModelDb.AllCharacters
				.Where(IsSupportedCharacter)
				.GroupBy(character => character.Id)
				.Select(group => group.First())
				.ToList();
		}
		catch
		{
			return new List<CharacterModel>();
		}
	}

	private static bool IsSupportedCharacter(CharacterModel? character)
	{
		if (character?.Id == null || character.Id == ModelId.none)
		{
			return false;
		}

		try
		{
			return character.StartingDeck.Any(card => card?.Id != null && card.Id != ModelId.none);
		}
		catch
		{
			return false;
		}
	}

	private static bool TryParseModelId(string text, out ModelId id)
	{
		try
		{
			id = ModelId.Deserialize(text);
			return true;
		}
		catch
		{
			id = ModelId.none;
			return false;
		}
	}

	// Hoisted: a fresh JsonSerializerOptions per call defeats System.Text.Json's cached
	// serialization metadata.
	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return _jsonOptions;
	}

	private sealed class BaseDeckStoreDto
	{
		public int Version { get; set; }
		public DateTime SavedAtUtc { get; set; }
		public Dictionary<string, List<string>> Decks { get; set; } = new(StringComparer.Ordinal);
	}
}
