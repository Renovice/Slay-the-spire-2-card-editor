using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckUiState
{
	private static readonly List<ModelId> _pendingAddCards = new();

	public static ModelId EditingCharacterId { get; private set; } = ModelId.none;

	public static IReadOnlyList<ModelId> PendingAddCards => _pendingAddCards;

	public static void EnsureValidCharacter()
	{
		if (!CardEditorBaseDeckStore.IsSupportedCharacterId(EditingCharacterId))
		{
			EditingCharacterId = CardEditorBaseDeckStore.GetDefaultCharacterId();
		}
	}

	public static void SetEditingCharacter(ModelId characterId)
	{
		if (!CardEditorBaseDeckStore.IsSupportedCharacterId(characterId))
		{
			return;
		}

		EditingCharacterId = characterId;
		RefreshAll();
	}

	public static void EnterBaseDeckMode()
	{
		EnsureValidCharacter();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeck;
		RefreshAll();
	}

	public static void EnterAddMode()
	{
		EnsureValidCharacter();
		_pendingAddCards.Clear();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeckAdd;
		RefreshAll();
	}

	public static void ExitToEditor()
	{
		_pendingAddCards.Clear();
		CardEditorUiState.Mode = CardEditorLibraryMode.Editor;
		RefreshAll();
	}

	public static void ExitAddModeToDeck()
	{
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeck;
		RefreshAll();
	}

	public static void ClearPendingAddCards()
	{
		if (_pendingAddCards.Count == 0)
		{
			return;
		}
		_pendingAddCards.Clear();
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	public static void QueueCard(ModelId cardId)
	{
		if (cardId == null || cardId == ModelId.none)
		{
			return;
		}

		_pendingAddCards.Add(cardId);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	public static void DequeueCard(ModelId cardId)
	{
		int index = _pendingAddCards.FindLastIndex(id => id == cardId);
		if (index < 0)
		{
			return;
		}

		_pendingAddCards.RemoveAt(index);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	public static int GetPendingCount(ModelId cardId)
	{
		return _pendingAddCards.Count(id => id == cardId);
	}

	public static void CommitPendingCards()
	{
		if (_pendingAddCards.Count == 0)
		{
			ExitAddModeToDeck();
			return;
		}

		CardEditorBaseDeckStore.AddCards(EditingCharacterId, _pendingAddCards);
		_pendingAddCards.Clear();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeck;
		RefreshAll();
	}

	public static void ClearTransientState()
	{
		_pendingAddCards.Clear();
	}

	public static void RefreshAll()
	{
		CardEditorUiState.RefreshLastLibraryIfActive();
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}
}
