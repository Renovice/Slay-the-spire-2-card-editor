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

			CardExtraEffect.CardCostAdjustment combined = CardExtraEffect.CardCostAdjustment.Combine(
				CardEditorCreatedCardsCostController.GetStarCostAdjustment(combatState, card),
				CardExtraEffect.CardCostAdjustment.Combine(
					CardEditorExtraEffects.GetCardStarCostsLessAdjustment(combatState, card),
					CardEditorCardTypeCostAuras.GetStarCostAdjustment(combatState, card)));
			CardExtraEffect.CardCostAdjustment energyAdjustment = CardExtraEffect.CardCostAdjustment.Combine(
				CardEditorExtraEffects.GetCardCostsLessAdjustment(combatState, card),
				CardEditorCardTypeCostAuras.GetEnergyCostAdjustment(combatState, card));
			if (energyAdjustment.ForceFree)
			{
				combined = CardExtraEffect.CardCostAdjustment.Combine(
					combined,
					new CardExtraEffect.CardCostAdjustment(0, ForceFree: true, HalfCost: false));
			}
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
			Log.Warn($"[CardEditor] CardStarCostsLess patch failed: {ex}");
		}
	}
}
