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

	public static void HandleAltPressed(NCardLibrary library, NCardHolder holder)
	{
		if (holder?.CardModel == null)
		{
			return;
		}

		if (CardEditorUiState.IsBaseDeckActive)
		{
			CardEditorBaseDeckStore.RemoveOne(CardEditorBaseDeckUiState.EditingCharacterId, holder.CardModel.Id);
			CardEditorBaseDeckUiState.RefreshAll();
			return;
		}

		if (CardEditorUiState.IsBaseDeckAddActive)
		{
			CardEditorBaseDeckUiState.DequeueCard(holder.CardModel.Id);
			return;
		}

		_showCardDetailMethod?.Invoke(library, new object[] { holder });
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

			Callable vanillaCallable = new Callable(__instance, "ShowCardDetail");
			Callable replacementCallable = CardEditorBaseDeckLibraryHelper.CreateAltPressedCallable(__instance);

			if (grid.IsConnected(NCardGrid.SignalName.HolderAltPressed, vanillaCallable))
			{
				grid.Disconnect(NCardGrid.SignalName.HolderAltPressed, vanillaCallable);
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
		CardEditorBaseDeckLibraryHelper.SyncSelectedCharacterFromLibrary(__instance);
		CardEditorBaseDeckPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
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
