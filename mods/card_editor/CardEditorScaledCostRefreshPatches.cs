using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

// Bug list #6 (Eviscerate-style costs): "this card costs 1 less for each card discarded this turn"
// is a CardCostsLess row with history scaling - the cost pipeline already evaluates it live
// (GetCardCostsLessAdjustment applies PerHistoryCount/ConditionOnly), but nothing told the hand UI
// to redraw when the counted event happened, so the printed cost looked frozen and the feature
// looked impossible to build. Refresh scaled-cost hand cards whenever a countable card event lands
// in the combat history.
internal static class CardEditorScaledCostRefreshHelper
{
	internal static void RefreshScaledHandCosts(CardModel? eventCard)
	{
		try
		{
			Player? owner = eventCard?.Owner;
			IReadOnlyList<CardModel>? handCards = owner?.PlayerCombatState?.Hand?.Cards;
			if (handCards == null || handCards.Count == 0)
			{
				return;
			}

			foreach (CardModel handCard in handCards.ToList())
			{
				if (handCard == null || !HasScaledCostRow(handCard))
				{
					continue;
				}

				try
				{
					handCard.InvokeEnergyCostChanged();
					NCard? node = NCard.FindOnTable(handCard);
					if (node != null)
					{
						node.UpdateVisuals(node.DisplayingPile, CardPreviewMode.Normal);
					}
				}
				catch
				{
					// ignored - a failed visual refresh must never break the discard/draw flow
				}
			}
		}
		catch
		{
			// ignored
		}
	}

	private static bool HasScaledCostRow(CardModel card)
	{
		try
		{
			foreach (CardExtraEffect effect in CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (effect != null
					&& effect.ScaleMode != CardExtraEffectScaleMode.None
					&& effect.Kind is CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess)
				{
					return true;
				}
			}
		}
		catch
		{
			// ignored
		}

		return false;
	}
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardDiscarded))]
internal static class CombatHistory_CardDiscarded_CardEditorScaledCostRefresh_Patch
{
	public static void Postfix(CardModel card)
	{
		CardEditorScaledCostRefreshHelper.RefreshScaledHandCosts(card);
	}
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardDrawn))]
internal static class CombatHistory_CardDrawn_CardEditorScaledCostRefresh_Patch
{
	public static void Postfix(CardModel card)
	{
		CardEditorScaledCostRefreshHelper.RefreshScaledHandCosts(card);
	}
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardExhausted))]
internal static class CombatHistory_CardExhausted_CardEditorScaledCostRefresh_Patch
{
	public static void Postfix(CardModel card)
	{
		CardEditorScaledCostRefreshHelper.RefreshScaledHandCosts(card);
	}
}
