using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorLibrarySelectionState
{
	private static readonly List<ModelId> _selectedCardIds = new();

	public static int SelectedCardCount => _selectedCardIds.Count;

	public static bool HasSelection => SelectedCardCount > 0;

	public static bool IsSelected(CardModel? cardModel)
	{
		return cardModel != null && FindSelectionIndex(cardModel.Id) >= 0;
	}

	public static bool ShouldOpenBatchEditor(CardModel? clickedCardModel)
	{
		return (CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive)
			&& clickedCardModel != null
			&& SelectedCardCount > 1
			&& FindSelectionIndex(clickedCardModel.Id) >= 0;
	}

	public static IReadOnlyList<ModelId> GetOrderedSelection(ModelId preferredId)
	{
		List<ModelId> ordered = new(_selectedCardIds.Count);
		if (preferredId != null && preferredId != ModelId.none && FindSelectionIndex(preferredId) >= 0)
		{
			ordered.Add(preferredId);
		}

		foreach (ModelId id in _selectedCardIds)
		{
			if (id == null || id == ModelId.none || ContainsId(ordered, id))
			{
				continue;
			}

			ordered.Add(id);
		}

		return ordered;
	}

	public static void ToggleSelection(NCardLibrary library, NCardHolder holder)
	{
		if (!(CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive) || library == null || holder?.CardModel == null)
		{
			return;
		}

		ModelId cardId = holder.CardModel.Id;
		int existingIndex = FindSelectionIndex(cardId);
		if (existingIndex >= 0)
		{
			_selectedCardIds.RemoveAt(existingIndex);
		}
		else if (cardId != null && cardId != ModelId.none)
		{
			_selectedCardIds.Add(cardId);
		}

		ApplySelectionHighlight(holder);
	}

	public static void AddSelection(NCardLibrary library, NCardHolder holder)
	{
		if (!(CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive) || library == null || holder?.CardModel == null)
		{
			return;
		}

		ModelId cardId = holder.CardModel.Id;
		if (cardId != null && cardId != ModelId.none && FindSelectionIndex(cardId) < 0)
		{
			_selectedCardIds.Add(cardId);
		}

		ApplySelectionHighlight(holder);
	}

	public static void RemoveSelection(NCardLibrary library, NCardHolder holder)
	{
		if (!(CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive) || library == null || holder?.CardModel == null)
		{
			return;
		}

		int existingIndex = FindSelectionIndex(holder.CardModel.Id);
		if (existingIndex >= 0)
		{
			_selectedCardIds.RemoveAt(existingIndex);
		}

		ApplySelectionHighlight(holder);
	}

	public static void ClearTransientState()
	{
		ClearSelections();
	}

	public static void ClearSelections(NCardLibrary? library = null)
	{
		if (_selectedCardIds.Count == 0)
		{
			return;
		}

		_selectedCardIds.Clear();
		NCardLibrary? targetLibrary = library;
		if (targetLibrary == null)
		{
			CardEditorUiState.TryGetLastLibrary(out targetLibrary);
		}

		if (targetLibrary != null)
		{
			RefreshVisibleHighlights(targetLibrary);
		}
	}

	public static void ApplySelectionHighlight(NCardHolder? holder)
	{
		if (holder?.CardNode?.CardHighlight == null)
		{
			return;
		}

		NCardHighlight highlight = holder.CardNode.CardHighlight;
		if (IsSelected(holder.CardModel))
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

	private static int FindSelectionIndex(ModelId id)
	{
		if (id == null || id == ModelId.none)
		{
			return -1;
		}

		string target = id.ToString();
		for (int i = 0; i < _selectedCardIds.Count; i++)
		{
			ModelId candidate = _selectedCardIds[i];
			if (candidate != null && string.Equals(candidate.ToString(), target, StringComparison.Ordinal))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool ContainsId(List<ModelId> ids, ModelId id)
	{
		if (id == null || id == ModelId.none)
		{
			return false;
		}

		string target = id.ToString();
		foreach (ModelId candidate in ids)
		{
			if (candidate != null && string.Equals(candidate.ToString(), target, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}
