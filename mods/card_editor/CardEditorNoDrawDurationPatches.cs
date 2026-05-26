using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class NoDrawPower_AfterSideTurnEnd_Duration_Patch
{
	private static System.Reflection.MethodBase? TargetMethod()
	{
		return AccessTools.Method(typeof(NoDrawPower), "AfterSideTurnEnd");
	}

	public static bool Prefix(NoDrawPower __instance, PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature>? participants, ref Task __result)
	{
		if (__instance == null)
		{
			return true;
		}
		if (participants != null && !participants.Contains(__instance.Owner))
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

[HarmonyPatch(typeof(NoDrawPower), "AfterTurnEnd")]
internal static class NoDrawPower_AfterTurnEnd_Duration_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(NoDrawPower), "AfterTurnEnd") != null;
	}

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
