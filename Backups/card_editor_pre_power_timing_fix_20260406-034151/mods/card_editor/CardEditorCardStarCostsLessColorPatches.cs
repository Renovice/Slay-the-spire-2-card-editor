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

		int delta = CardEditorExtraEffects.GetCardStarCostsLessReduction(state, card);
		delta += CardEditorCardTypeCostAuras.GetStarCostDelta(state, card);
		if (delta == 0)
		{
			return;
		}

		decimal before = hookModifiedCost;
		hookModifiedCost = Math.Max(0m, hookModifiedCost - delta);
		if (hookModifiedCost != before)
		{
			__result = true;
		}
	}
}
