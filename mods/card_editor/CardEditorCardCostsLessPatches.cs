using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class Hook_ModifyEnergyCostInCombat_CardEditorCardCostsLess_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, decimal originalCost, ref decimal __result)
	{
		try
		{
			if (CardEditorEnergyCostVisibilityHelper.SuppressCardEditorCostHooks)
			{
				return;
			}
			// Keep vanilla "no energy cost / X-cost" visuals intact (negative sentinel values),
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

			CardExtraEffect.CardCostAdjustment combined = CardExtraEffect.CardCostAdjustment.Combine(
				CardEditorExtraEffects.GetCardCostsLessAdjustment(combatState, card),
				CardEditorCardTypeCostAuras.GetEnergyCostAdjustment(combatState, card));
			if (combined.IsNeutral)
			{
				return;
			}

			if (combined.Delta != 0)
			{
				__result = Math.Max(0m, __result - combined.Delta);
			}
			if (combined.ForceFree)
			{
				__result = 0m;
			}
			else if (combined.HalfCost)
			{
				__result = Math.Max(0m, Math.Floor(__result / 2m));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] CardCostsLess patch failed: {ex}");
		}
	}
}
