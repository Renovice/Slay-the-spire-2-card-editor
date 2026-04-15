using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyStarCost))]
internal static class Hook_ModifyStarCost_CardEditorCardStarCostsLess_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, decimal originalCost, ref decimal __result)
	{
		try
		{
			// Keep vanilla "no star cost / X-cost" visuals intact (negative sentinel values),
			// but never allow cost reductions to push a normally-costed card below 0 in combat.
			if (__result < 0m)
			{
				if (originalCost < 0m)
				{
					return;
				}
				__result = 0m;
			}
			if (combatState == null || card == null)
			{
				return;
			}

			int delta = CardEditorExtraEffects.GetCardStarCostsLessReduction(combatState, card);
			delta += CardEditorCardTypeCostAuras.GetStarCostDelta(combatState, card);
			if (delta == 0)
			{
				return;
			}

			__result = Math.Max(0m, __result - delta);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] CardStarCostsLess patch failed: {ex}");
		}
	}
}
