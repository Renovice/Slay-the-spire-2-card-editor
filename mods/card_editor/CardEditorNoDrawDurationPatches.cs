using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(NoDrawPower), nameof(NoDrawPower.AfterTurnEnd))]
internal static class NoDrawPower_AfterTurnEnd_Duration_Patch
{
	public static bool Prefix(NoDrawPower __instance, PlayerChoiceContext choiceContext, CombatSide side, ref Task __result)
	{
		if (__instance == null)
		{
			return true;
		}
		if (__instance.Amount <= 1m)
		{
			return true;
		}

		__result = PowerCmd.Decrement(__instance);
		return false;
	}
}

