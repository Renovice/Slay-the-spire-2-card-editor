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

	internal static bool SuppressCardEditorCostHooks => _suppressCardEditorCostHooks;

	internal static IDisposable SuppressCardEditorCostHooksScoped()
	{
		bool previous = _suppressCardEditorCostHooks;
		_suppressCardEditorCostHooks = true;
		return new SuppressionScope(previous);
	}

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

	private sealed class SuppressionScope : IDisposable
	{
		private readonly bool _previous;
		private bool _disposed;

		public SuppressionScope(bool previous)
		{
			_previous = previous;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_suppressCardEditorCostHooks = _previous;
			_disposed = true;
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

		CardExtraEffect.CardCostAdjustment combined = CardExtraEffect.CardCostAdjustment.Combine(
			CardEditorExtraEffects.GetCardCostsLessAdjustment(state, card),
			CardEditorCardTypeCostAuras.GetEnergyCostAdjustment(state, card));
		if (combined.IsNeutral)
		{
			return;
		}
		if (__result && hookModifiedCost <= 0m && combined.Delta > 0 && !combined.ForceFree && !combined.HalfCost)
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

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class NCard_UpdateEnergyCostVisuals_CardEditorHiddenCost_Patch
{
	public static void Postfix(NCard __instance)
	{
		CardModel? card = __instance?.Model;
		if (__instance == null || card == null)
		{
			return;
		}
		if (card.EnergyCost == null)
		{
			return;
		}

		// Always reset the reused NCard's cost visuals unless we intentionally want the no-cost sentinel hidden.
		// Without this, a node that previously displayed a hidden cost can keep the label invisible for later cards.
		if (card.EnergyCost.CostsX)
		{
			CardEditorEnergyCostVisibilityHelper.SetEnergyCostHidden(__instance, hidden: false);
			return;
		}

		// Outside combat (e.g. compendium), allow negative base costs to hide the energy icon just like curses.
		// Use pile state instead of CardModel.CombatState; the getter changed ABI in v0.104 and crashed old builds.
		bool isCombatCard = card.Pile?.IsCombatPile ?? false;
		if (!isCombatCard)
		{
			decimal baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
			CardEditorEnergyCostVisibilityHelper.SetEnergyCostHidden(__instance, baseCost < 0m);
			return;
		}

		decimal localCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
		// In combat, respect the card's own local negative "no-cost" sentinel exactly like vanilla unplayables/event cards.
		// This also avoids re-showing a numeric energy cost on curses such as Ascender's Bane. We intentionally look at the
		// stable local value rather than transient hook-modified values so normal 0-cost cards keep their digit.
		bool shouldHide = localCost < 0m;
		CardEditorEnergyCostVisibilityHelper.SetEnergyCostHidden(__instance, shouldHide);
	}
}
