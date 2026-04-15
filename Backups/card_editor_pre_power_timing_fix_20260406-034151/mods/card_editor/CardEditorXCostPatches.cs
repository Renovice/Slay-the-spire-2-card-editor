using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardEnergyCost))]
internal static class CardEnergyCost_XCostOverride_Patches
{
	[HarmonyPatch(MethodType.Constructor, typeof(CardModel), typeof(int), typeof(bool))]
	[HarmonyPrefix]
	private static void Ctor_Prefix(CardModel card, int canonicalCost, ref bool costsX)
	{
		try
		{
			if (card == null || CardEditorOverrides.SuppressAllOverrides)
			{
				return;
			}

			if (CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
				&& overrideData != null
				&& overrideData.EnergyCostX.HasValue)
			{
				costsX = overrideData.EnergyCostX.Value;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Energy X-cost patch failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_HasStarCostX")]
internal static class CardModel_StarXCostOverride_Patch
{
	private static void Postfix(CardModel __instance, ref bool __result)
	{
		try
		{
			if (__instance == null || CardEditorOverrides.SuppressAllOverrides)
			{
				return;
			}

			if (CardEditorOverrides.TryGetEffectiveOverride(__instance.Id, out CardOverride overrideData)
				&& overrideData != null
				&& overrideData.StarCostX.HasValue)
			{
				__result = overrideData.StarCostX.Value;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Star X-cost patch failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_CanonicalStarCost")]
internal static class CardModel_CanonicalStarCostOverride_Patch
{
	private static void Postfix(CardModel __instance, ref int __result)
	{
		try
		{
			if (__instance == null || CardEditorOverrides.SuppressAllOverrides)
			{
				return;
			}

			// Treat editor-assigned star costs as "canonical" so vanilla systems that check CanonicalStarCost
			// (e.g. Crescent Spear scaling) count custom/overridden star-cost cards.
			if (__result < 0
				&& CardEditorOverrides.TryGetEffectiveOverride(__instance.Id, out CardOverride overrideData)
				&& overrideData?.StarCost is int overriddenStarCost
				&& overriddenStarCost >= 0)
			{
				__result = overriddenStarCost;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Canonical star cost patch failed: {ex}");
		}
	}
}
