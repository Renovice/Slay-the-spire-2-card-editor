using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckUiState
{
	private static readonly HashSet<CardModel> _selectedDeckCards = new(ReferenceEqualityComparer.Instance);
	private static readonly HashSet<CardModel> _selectedCardListCards = new(ReferenceEqualityComparer.Instance);

	public static ModelId EditingCharacterId { get; private set; } = ModelId.none;

	public static int SelectedCardCount => GetSelectionSetForCurrentMode().Count;

	public static bool HasSelection => SelectedCardCount > 0;

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

		ClearSelections();
		EditingCharacterId = characterId;
		RefreshAll();
	}

	public static void EnterBaseDeckMode()
	{
		EnsureValidCharacter();
		ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeck;
		RefreshAll();
	}

	public static void EnterAddMode()
	{
		EnsureValidCharacter();
		ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeckAdd;
		RefreshAll();
	}

	public static void ExitToEditor()
	{
		ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.Editor;
		RefreshAll();
	}

	public static void ExitAddModeToDeck()
	{
		ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.BaseDeck;
		if (CardEditorUiState.TryGetLastLibrary(out NCardLibrary? library) && library != null)
		{
			CardEditorBaseDeckLibraryHelper.ApplyEditingCharacterSelection(library);
		}
		RefreshAll();
	}

	public static void ResetEditedDeck()
	{
		EnsureValidCharacter();
		ClearSelections();
		CardEditorBaseDeckStore.ResetToVanilla(EditingCharacterId);
		RefreshAll();
	}

	public static void ToggleSelection(NCardLibrary library, NCardHolder holder)
	{
		if (library == null || holder?.CardModel == null)
		{
			return;
		}

		HashSet<CardModel> selection = GetSelectionSetForCurrentMode();
		if (selection.Contains(holder.CardModel))
		{
			selection.Remove(holder.CardModel);
		}
		else
		{
			selection.Add(holder.CardModel);
		}

		ApplySelectionHighlight(holder);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	public static void AddSelectedToDeck()
	{
		EnsureValidCharacter();
		List<ModelId> cardIds = GetSelectionSetForCurrentMode()
			.Where(card => card?.Id != null && card.Id != ModelId.none)
			.Select(card => card.Id)
			.ToList();
		if (cardIds.Count == 0)
		{
			return;
		}

		CardEditorBaseDeckStore.AddCards(EditingCharacterId, cardIds);
		ClearSelections();
		RefreshAll();
	}

	public static void DeleteSelectedFromDeck()
	{
		if (!CardEditorUiState.IsBaseDeckActive)
		{
			return;
		}

		List<ModelId> cardIds = _selectedDeckCards
			.Where(card => card?.Id != null && card.Id != ModelId.none)
			.Select(card => card.Id)
			.ToList();
		if (cardIds.Count == 0)
		{
			return;
		}

		foreach (ModelId cardId in cardIds)
		{
			CardEditorBaseDeckStore.RemoveOne(EditingCharacterId, cardId);
		}

		ClearSelections();
		RefreshAll();
	}

	public static void ClearTransientState()
	{
		ClearSelections();
	}

	public static void ClearSelections(NCardLibrary? library = null)
	{
		ClearSelectionSet(_selectedDeckCards, library);
		ClearSelectionSet(_selectedCardListCards, library);
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	public static bool IsSelectedInCurrentMode(CardModel? cardModel)
	{
		if (cardModel == null)
		{
			return false;
		}

		return GetSelectionSetForCurrentMode().Contains(cardModel);
	}

	public static void ApplySelectionHighlight(NCardHolder? holder)
	{
		if (holder?.CardNode?.CardHighlight == null)
		{
			return;
		}

		NCardHighlight highlight = holder.CardNode.CardHighlight;
		if (IsSelectedInCurrentMode(holder.CardModel))
		{
			highlight.Modulate = NCardHighlight.gold;
			highlight.AnimShow();
		}
		else
		{
			highlight.Modulate = NCardHighlight.playableColor;
			highlight.AnimHideInstantly();
		}
	}

	public static void RefreshVisibleHighlights(NCardLibrary library)
	{
		NCardGrid? grid = library?.GetNodeOrNull<NCardGrid>("%CardGrid");
		if (grid == null)
		{
			return;
		}

		foreach (NGridCardHolder holder in grid.CurrentlyDisplayedCardHolders)
		{
			ApplySelectionHighlight(holder);
		}
	}

	public static void RefreshAll()
	{
		CardEditorUiState.RefreshLastLibraryIfActive();
		CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
		CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
	}

	private static HashSet<CardModel> GetSelectionSetForCurrentMode()
	{
		return CardEditorUiState.IsBaseDeckAddActive ? _selectedCardListCards : _selectedDeckCards;
	}

	private static void ClearSelectionSet(HashSet<CardModel> selection, NCardLibrary? library)
	{
		if (selection.Count == 0)
		{
			return;
		}

		NCardLibrary? targetLibrary = library;
		if (targetLibrary == null)
		{
			CardEditorUiState.TryGetLastLibrary(out targetLibrary);
		}

		if (targetLibrary != null)
		{
			foreach (CardModel card in selection)
			{
				TryResetHighlight(targetLibrary, card);
			}
		}

		selection.Clear();
		if (targetLibrary != null)
		{
			RefreshVisibleHighlights(targetLibrary);
		}
	}

	private static void TryResetHighlight(NCardLibrary library, CardModel cardModel)
	{
		NCardGrid? grid = library.GetNodeOrNull<NCardGrid>("%CardGrid");
		NCard? cardNode = grid?.GetCardNode(cardModel);
		if (cardNode?.CardHighlight == null)
		{
			return;
		}

		cardNode.CardHighlight.Modulate = NCardHighlight.playableColor;
		cardNode.CardHighlight.AnimHideInstantly();
	}

	private sealed class ReferenceEqualityComparer : IEqualityComparer<CardModel>
	{
		public static readonly ReferenceEqualityComparer Instance = new();

		public bool Equals(CardModel? x, CardModel? y)
		{
			return ReferenceEquals(x, y);
		}

		public int GetHashCode(CardModel obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}
}
