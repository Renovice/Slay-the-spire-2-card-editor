using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardModel), "get_ShouldGlowGold")]
internal static class CardModel_get_ShouldGlowGold_ConditionalEffectGlow_Patch
{
	private static void Postfix(CardModel __instance, ref bool __result)
	{
		if (__result || __instance == null)
		{
			return;
		}

		if (CardEditorExtraEffects.ShouldGlowGoldForConditionalEffects(__instance))
		{
			__result = true;
		}
	}
}
