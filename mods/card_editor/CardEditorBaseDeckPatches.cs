using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBaseDeckLibraryHelper
{
	private enum PendingCardDetailAction
	{
		None,
		LeftClick,
		RightClick
	}

	private static ulong _suppressShowCardDetailUntilMs;
	private static ulong _pendingCardDetailActionUntilMs;
	private static NCardHolder? _pendingCardDetailHolder;
	private static PendingCardDetailAction _pendingCardDetailAction;
	private static bool _pendingCardDetailShiftPressed;

	private static readonly MethodInfo? _showCardDetailMethod =
		typeof(NCardLibrary).GetMethod("ShowCardDetail", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly Dictionary<string, ModelId> _characterFilterPaths = new(StringComparer.Ordinal)
	{
		["%IroncladPool"] = ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>().Id,
		["%SilentPool"] = ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Silent>().Id,
		["%DefectPool"] = ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Defect>().Id,
		["%RegentPool"] = ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Regent>().Id,
		["%NecrobinderPool"] = ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Necrobinder>().Id
	};

	public static Callable CreateAltPressedCallable(NCardLibrary library)
	{
		return Callable.From<NCardHolder>(holder => HandleAltPressed(library, holder));
	}

	public static Callable CreatePressedCallable(NCardLibrary library)
	{
		return Callable.From<NCardHolder>(holder => HandlePressed(library, holder));
	}

	public static void HandlePressed(NCardLibrary library, NCardHolder holder)
	{
		if (holder?.CardModel == null)
		{
			return;
		}

		RecordPendingCardDetailAction(holder, isRightClick: false, isShiftPressed: Input.IsKeyPressed(Key.Shift));
		_showCardDetailMethod?.Invoke(library, new object[] { holder });
	}

	public static void HandleAltPressed(NCardLibrary library, NCardHolder holder)
	{
		if (holder?.CardModel == null)
		{
			return;
		}

		RecordPendingCardDetailAction(holder, isRightClick: true, isShiftPressed: Input.IsKeyPressed(Key.Shift));
		_showCardDetailMethod?.Invoke(library, new object[] { holder });
	}

	public static void RecordPendingCardDetailAction(NCardHolder holder, bool isRightClick, bool isShiftPressed = false)
	{
		if (holder?.CardModel == null)
		{
			return;
		}

		_pendingCardDetailHolder = holder;
		_pendingCardDetailAction = isRightClick ? PendingCardDetailAction.RightClick : PendingCardDetailAction.LeftClick;
		_pendingCardDetailShiftPressed = isShiftPressed;
		_pendingCardDetailActionUntilMs = Time.GetTicksMsec() + 250;
	}

	public static bool TryConsumePendingCardDetailAction(NCardHolder holder, out bool isRightClick, out bool isShiftPressed)
	{
		isRightClick = false;
		isShiftPressed = false;
		if (_pendingCardDetailAction == PendingCardDetailAction.None || _pendingCardDetailHolder != holder)
		{
			return false;
		}

		ulong now = Time.GetTicksMsec();
		if (now > _pendingCardDetailActionUntilMs)
		{
			ClearPendingCardDetailAction();
			return false;
		}

		isRightClick = _pendingCardDetailAction == PendingCardDetailAction.RightClick;
		isShiftPressed = _pendingCardDetailShiftPressed;
		ClearPendingCardDetailAction();
		return true;
	}

	private static void ClearPendingCardDetailAction()
	{
		_pendingCardDetailActionUntilMs = 0;
		_pendingCardDetailHolder = null;
		_pendingCardDetailAction = PendingCardDetailAction.None;
		_pendingCardDetailShiftPressed = false;
	}

	public static void ArmShowCardDetailSuppression(ulong durationMs = 250)
	{
		_suppressShowCardDetailUntilMs = Time.GetTicksMsec() + durationMs;
	}

	public static bool ShouldSuppressShowCardDetailPopup()
	{
		if (_suppressShowCardDetailUntilMs == 0)
		{
			return false;
		}

		ulong now = Time.GetTicksMsec();
		bool shouldSuppress = now <= _suppressShowCardDetailUntilMs;
		_suppressShowCardDetailUntilMs = 0;
		return shouldSuppress;
	}

	public static void SyncSelectedCharacterFromLibrary(NCardLibrary library)
	{
		if (!CardEditorUiState.IsBaseDeckActive || library == null)
		{
			return;
		}

		foreach ((string path, ModelId characterId) in _characterFilterPaths)
		{
			NCardPoolFilter? filter = library.GetNodeOrNull<NCardPoolFilter>(path);
			if (filter?.IsSelected == true)
			{
				if (CardEditorBaseDeckUiState.EditingCharacterId != characterId)
				{
					CardEditorBaseDeckUiState.SetEditingCharacter(characterId);
				}
				return;
			}
		}
	}

	public static void ApplyEditingCharacterSelection(NCardLibrary library)
	{
		if (library == null || !CardEditorUiState.IsBaseDeckActive)
		{
			return;
		}

		ModelId selected = CardEditorBaseDeckUiState.EditingCharacterId;
		foreach ((string path, ModelId characterId) in _characterFilterPaths)
		{
			NCardPoolFilter? filter = library.GetNodeOrNull<NCardPoolFilter>(path);
			if (filter != null && CardEditorUiState.IsBaseDeckActive)
			{
				filter.IsSelected = characterId == selected;
			}
		}
	}

	public static bool TryGetSelectedCharacterFromLibrary(NCardLibrary library, out ModelId characterId)
	{
		foreach ((string path, ModelId candidate) in _characterFilterPaths)
		{
			NCardPoolFilter? filter = library.GetNodeOrNull<NCardPoolFilter>(path);
			if (filter?.IsSelected == true)
			{
				characterId = candidate;
				return true;
			}
		}

		characterId = ModelId.none;
		return false;
	}
}

[HarmonyPatch(typeof(NCardGrid), "OnHolderAltPressed")]
internal static class CardGrid_BaseDeck_ArmShowCardDetailSuppression_Patch
{
	public static void Prefix(NCardHolder holder)
	{
		if ((CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive || CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive)
			&& holder?.CardModel != null)
		{
			CardEditorBaseDeckLibraryHelper.RecordPendingCardDetailAction(holder, isRightClick: true, isShiftPressed: Input.IsKeyPressed(Key.Shift));
		}
	}
}

[HarmonyPatch(typeof(NCardGrid), "OnHolderPressed")]
internal static class CardGrid_BaseDeck_ArmShowCardDetailSuppression_OnPressed_Patch
{
	public static void Prefix(NCardHolder holder)
	{
		if ((CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive || CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive)
			&& holder?.CardModel != null)
		{
			CardEditorBaseDeckLibraryHelper.RecordPendingCardDetailAction(holder, isRightClick: false, isShiftPressed: Input.IsKeyPressed(Key.Shift));
		}
	}
}

[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class CardLibrary_AltPressedOverride_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		try
		{
			NCardGrid? grid = __instance.GetNodeOrNull<NCardGrid>("%CardGrid");
			if (grid == null)
			{
				return;
			}

			Callable vanillaPressedCallable = new Callable(__instance, "ShowCardDetail");
			Callable vanillaCallable = new Callable(__instance, "ShowCardDetail");
			Callable replacementPressedCallable = CardEditorBaseDeckLibraryHelper.CreatePressedCallable(__instance);
			Callable replacementCallable = CardEditorBaseDeckLibraryHelper.CreateAltPressedCallable(__instance);

			if (grid.IsConnected(NCardGrid.SignalName.HolderPressed, vanillaPressedCallable))
			{
				grid.Disconnect(NCardGrid.SignalName.HolderPressed, vanillaPressedCallable);
			}

			if (grid.IsConnected(NCardGrid.SignalName.HolderAltPressed, vanillaCallable))
			{
				grid.Disconnect(NCardGrid.SignalName.HolderAltPressed, vanillaCallable);
			}

			if (!grid.IsConnected(NCardGrid.SignalName.HolderPressed, replacementPressedCallable))
			{
				grid.Connect(NCardGrid.SignalName.HolderPressed, replacementPressedCallable);
			}

			if (!grid.IsConnected(NCardGrid.SignalName.HolderAltPressed, replacementCallable))
			{
				grid.Connect(NCardGrid.SignalName.HolderAltPressed, replacementCallable);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed overriding card library alt-press handling: {ex}");
		}
	}
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
internal static class CardLibrary_BaseDeck_OnOpened_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuOpened:postfix:start", __instance);
		if (CardEditorUiState.IsBaseDeckActive)
		{
			CardEditorBaseDeckLibraryHelper.ApplyEditingCharacterSelection(__instance);
			CardEditorUiState.RefreshLibrary(__instance);
		}

		CardEditorBaseDeckPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.SyncDeferred(__instance);
		CardEditorBaseDeckUiState.RefreshVisibleHighlights(__instance);
		CardEditorLibrarySelectionState.RefreshVisibleHighlights(__instance);
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuOpened:postfix:end", __instance);
	}
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
internal static class CardLibrary_BaseDeck_OnClosed_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuClosed:baseDeckPatch:start", __instance);
		CardEditorBaseDeckUiState.ClearTransientState();
		CardEditorLibrarySelectionState.ClearTransientState();
		CardEditorBaseDeckPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuClosed:baseDeckPatch:end", __instance);
	}
}

[HarmonyPatch(typeof(NCardLibrary), "UpdateCardPoolFilter")]
internal static class CardLibrary_BaseDeck_UpdateCardPoolFilter_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		if (CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.ClearSelections(__instance);
		}

		CardEditorBaseDeckLibraryHelper.SyncSelectedCharacterFromLibrary(__instance);
		CardEditorBaseDeckPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
		CardEditorBaseDeckUiState.RefreshVisibleHighlights(__instance);
		CardEditorLibrarySelectionState.RefreshVisibleHighlights(__instance);
	}
}

[HarmonyPatch(typeof(NCardHolder), nameof(NCardHolder.ReassignToCard))]
internal static class CardHolder_BaseDeck_ReassignHighlight_Patch
{
	public static void Postfix(NCardHolder __instance)
	{
		if (CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.ApplySelectionHighlight(__instance);
		}

		if (CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive)
		{
			CardEditorLibrarySelectionState.ApplySelectionHighlight(__instance);
		}
	}
}

[HarmonyPatch(typeof(NBackButton), "OnEnable")]
internal static class CardLibrary_BackButton_OnEnable_Patch
{
	public static void Postfix(NBackButton __instance)
	{
		CardEditorBaseDeckBookmarkHooks.NotifyBackButtonEnabled(__instance);
	}
}

[HarmonyPatch(typeof(NBackButton), "OnPress")]
internal static class CardLibrary_BackButton_OnPress_Patch
{
	public static void Prefix(NBackButton __instance)
	{
		CardEditorBaseDeckBookmarkHooks.LogBackButtonPressed(__instance);
	}
}

[HarmonyPatch(typeof(NBackButton), "OnDisable")]
internal static class CardLibrary_BackButton_OnDisable_Patch
{
	public static void Postfix(NBackButton __instance)
	{
		CardEditorBaseDeckBookmarkHooks.NotifyBackButtonDisabled(__instance);
	}
}

[HarmonyPatch(typeof(Player), "PopulateStartingDeck")]
internal static class Player_PopulateStartingDeck_BaseDeck_Patch
{
	private static readonly MethodInfo? _populateDeckMethod =
		typeof(Player).GetMethod("PopulateDeck", BindingFlags.Instance | BindingFlags.NonPublic);

	public static bool Prefix(Player __instance)
	{
		try
		{
			CardEditorBaseDeckStore.EnsureLoaded();
			ModelId characterId = __instance.Character?.Id ?? ModelId.none;
			if (!CardEditorBaseDeckStore.IsSupportedCharacterId(characterId) || !CardEditorBaseDeckStore.HasOverride(characterId))
			{
				return true;
			}

			List<CardModel> cards = new();
			foreach (ModelId cardId in CardEditorBaseDeckStore.GetDeckIds(characterId))
			{
				CardModel? card = ModelDb.GetByIdOrNull<CardModel>(cardId);
				if (card == null)
				{
					continue;
				}

				CardModel mutable = card.ToMutable();
				mutable.FloorAddedToDeck = 1;
				cards.Add(mutable);
			}

			_populateDeckMethod?.Invoke(__instance, new object[] { cards, false });
			return false;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed applying base deck override: {ex}");
			return true;
		}
	}
}
