using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorLibraryCursorHelper
{
	public static void ApplyEditorCardCursors(IEnumerable<NGridCardHolder>? holders)
	{
		if (!CardEditorUiState.IsActive || holders == null)
		{
			return;
		}

		foreach (NGridCardHolder holder in holders)
		{
			if (holder?.Hitbox != null && GodotObject.IsInstanceValid(holder.Hitbox))
			{
				holder.Hitbox.MouseDefaultCursorShape = Control.CursorShape.Help;
			}
		}
	}
}

[HarmonyPatch(typeof(NCardLibraryGrid), "InitGrid")]
internal static class CardLibraryGrid_InitGrid_CardEditorCursorPatch
{
	public static void Postfix(NCardLibraryGrid __instance)
	{
		CardEditorLibraryCursorHelper.ApplyEditorCardCursors(__instance?.CurrentlyDisplayedCardHolders);
	}
}

[HarmonyPatch(typeof(NCardLibraryGrid), "AssignCardsToRow")]
internal static class CardLibraryGrid_AssignCardsToRow_CardEditorCursorPatch
{
	public static void Postfix(List<NGridCardHolder> row)
	{
		CardEditorLibraryCursorHelper.ApplyEditorCardCursors(row);
	}
}
