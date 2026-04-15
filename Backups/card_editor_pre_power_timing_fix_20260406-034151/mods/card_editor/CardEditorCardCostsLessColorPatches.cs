using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers.Models;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorEnergyCostVisibilityHelper
{
	[ThreadStatic]
	private static bool _suppressCardEditorCostHooks;

	private static readonly FieldInfo _energyLabelField = AccessTools.Field(typeof(NCard), "_energyLabel")!;
	private static readonly FieldInfo _energyIconField = AccessTools.Field(typeof(NCard), "_energyIcon")!;
	private static readonly FieldInfo _unplayableEnergyIconField = AccessTools.Field(typeof(NCard), "_unplayableEnergyIcon")!;

	internal static bool SuppressCardEditorCostHooks => _suppressCardEditorCostHooks;

	internal static decimal GetPreEditorModifiedEnergyCost(CombatState state, CardModel card, decimal originalCost)
	{
		bool previous = _suppressCardEditorCostHooks;
		_suppressCardEditorCostHooks = true;
		try
		{
			return Hook.ModifyEnergyCostInCombat(state, card, originalCost);
		}
		finally
		{
			_suppressCardEditorCostHooks = previous;
		}
	}

	internal static void SetEnergyCostHidden(NCard cardNode, bool hidden)
	{
		if (cardNode == null)
		{
			return;
		}

		if (_energyLabelField.GetValue(cardNode) is CanvasItem energyLabel)
		{
			energyLabel.Visible = !hidden;
		}

		if (_energyIconField.GetValue(cardNode) is CanvasItem energyIcon)
		{
			energyIcon.Visible = !hidden;
		}

		if (hidden && _unplayableEnergyIconField.GetValue(cardNode) is CanvasItem unplayableEnergyIcon)
		{
			unplayableEnergyIcon.Visible = false;
		}
	}
}

[HarmonyPatch]
internal static class CardEditorCardCostsLessColorPatches
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(CardCostHelper), "TryModifyEnergyCostWithHooks")!;
	}

	public static void Postfix(CardModel card, CombatState state, ref decimal hookModifiedCost, ref bool __result)
	{
		if (card == null || state == null)
		{
			return;
		}
		if (CardEditorEnergyCostVisibilityHelper.SuppressCardEditorCostHooks)
		{
			return;
		}
		if (card.EnergyCost.CostsX)
		{
			return;
		}
		if (hookModifiedCost < 0m)
		{
			return;
		}

		int delta = CardEditorExtraEffects.GetCardCostsLessReduction(state, card);
		delta += CardEditorCardTypeCostAuras.GetEnergyCostDelta(state, card);
		if (delta == 0)
		{
			return;
		}
		if (__result && hookModifiedCost <= 0m && delta > 0)
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

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class NCard_UpdateEnergyCostVisuals_CardEditorHiddenCost_Patch
{
	public static void Postfix(NCard __instance)
	{
		CardModel? card = __instance?.Model;
		CombatState? state = card?.CombatState;
		if (__instance == null || card == null)
		{
			return;
		}
		if (card.EnergyCost == null || card.EnergyCost.CostsX)
		{
			return;
		}

		// Outside combat (e.g. compendium), allow negative base costs to hide the energy icon just like curses.
		if (state == null)
		{
			decimal baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
			CardEditorEnergyCostVisibilityHelper.SetEnergyCostHidden(__instance, baseCost < 0m);
			return;
		}

		int delta = CardEditorExtraEffects.GetCardCostsLessReduction(state, card);
		delta += CardEditorCardTypeCostAuras.GetEnergyCostDelta(state, card);
		if (delta <= 0)
		{
			return;
		}

		decimal localCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
		decimal costBeforeEditor = CardEditorEnergyCostVisibilityHelper.GetPreEditorModifiedEnergyCost(state, card, localCost);
		// In combat, cost reductions clamp at 0 (vanilla behavior). Only hide the energy icon when the card already has
		// a negative "no-cost" sentinel value (e.g. X-cost / no-energy-cost cards).
		bool shouldHide = costBeforeEditor < 0m;
		CardEditorEnergyCostVisibilityHelper.SetEnergyCostHidden(__instance, shouldHide);
	}
}
