using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

// Bug list #5 (Knowledge Demon / Disintegration): encounters create their offered cards through
// CombatState.CreateCard, which DOES apply the user's override (via the ToMutable postfix) - but
// some encounters then overwrite the card's DynamicVars afterwards (the demon hard-sets
// Disintegration's escalating damage), clobbering the edit. Re-assert the override's absolute var
// values right before any choose-a-card screen presents the cards, so the user's edit is what the
// player sees and what OnChosen executes with.
[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
internal static class CardSelectCmd_FromChooseACardScreen_CardEditorOverrideVars_Patch
{
	public static void Prefix(IReadOnlyList<CardModel> cards)
	{
		if (cards == null)
		{
			return;
		}

		foreach (CardModel card in cards)
		{
			CardEditorOverrides.ReassertDynamicVarBaseValues(card);
		}
	}
}
