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
	// Cost INCREASES enter the pipeline BEFORE the vanilla hooks run, like vanilla TangledPower's
	// early hook, so late "free" powers (Free Skill, Corruption, Void Form) still win over them.
	public static void Prefix(CombatState combatState, CardModel card, ref decimal originalCost)
	{
		try
		{
			if (CardEditorEnergyCostVisibilityHelper.SuppressCardEditorCostHooks
				|| combatState == null
				|| card == null
				|| originalCost < 0m)
			{
				return;
			}

			CardExtraEffect.CardCostAdjustment combined = CardExtraEffect.CardCostAdjustment.Combine(
				CardEditorExtraEffects.GetCardCostsLessAdjustment(combatState, card),
				CardEditorCardTypeCostAuras.GetEnergyCostAdjustment(combatState, card));
			if (combined.Delta < 0)
			{
				originalCost = Math.Max(0m, originalCost - combined.Delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] CardCostsLess prefix failed: {ex}");
		}
	}

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

			// Increases were applied in the Prefix; only reductions act after the vanilla hooks.
			if (combined.Delta > 0)
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
