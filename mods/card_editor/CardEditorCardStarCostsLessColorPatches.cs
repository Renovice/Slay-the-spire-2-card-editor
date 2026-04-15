using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers.Models;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class CardEditorCardStarCostsLessColorPatches
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(CardCostHelper), "TryModifyStarCostWithHooks")!;
	}

	public static void Postfix(CardModel card, CombatState state, ref decimal hookModifiedCost, ref bool __result)
	{
		if (card == null || state == null)
		{
			return;
		}
		if (card.HasStarCostX)
		{
			return;
		}

		if (hookModifiedCost < 0m)
		{
			return;
		}

		CardExtraEffect.CardCostAdjustment combined = CardExtraEffect.CardCostAdjustment.Combine(
			CardEditorCreatedCardsCostController.GetStarCostAdjustment(state, card),
			CardExtraEffect.CardCostAdjustment.Combine(
				CardEditorExtraEffects.GetCardStarCostsLessAdjustment(state, card),
				CardEditorCardTypeCostAuras.GetStarCostAdjustment(state, card)));
		CardExtraEffect.CardCostAdjustment energyAdjustment = CardExtraEffect.CardCostAdjustment.Combine(
			CardEditorExtraEffects.GetCardCostsLessAdjustment(state, card),
			CardEditorCardTypeCostAuras.GetEnergyCostAdjustment(state, card));
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

		decimal before = hookModifiedCost;
		if (combined.Delta != 0)
		{
			hookModifiedCost = Math.Max(0m, hookModifiedCost - combined.Delta);
		}
		if (combined.ForceFree)
		{
			hookModifiedCost = 0m;
		}
		else if (combined.HalfCost)
		{
			hookModifiedCost = Math.Max(0m, Math.Floor(hookModifiedCost / 2m));
		}
		if (hookModifiedCost != before)
		{
			__result = true;
		}
	}
}
