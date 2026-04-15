using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class Hook_AfterCardChangedPiles_CardCostsLessDuration_Patch
{
	public static void Postfix(CombatState? combatState, CardModel card, PileType oldPile)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides)
		{
			return;
		}

		try
		{
			PileType newPile = card.Pile?.Type ?? PileType.None;
			if (newPile == PileType.Hand && oldPile != PileType.Hand)
			{
				CardEditorExtraEffects.ApplyIntrinsicTimedCardCostsLessOnEnterHand(combatState, card);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed applying timed CardCostsLess on pile change: {ex}");
		}
	}
}

