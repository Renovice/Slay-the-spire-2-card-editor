using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class Hook_BeforeCardPlayed_CosmeticsPatch
{
	public static void Postfix(CombatState combatState, CardPlay cardPlay, ref Task __result)
	{
		Task original = __result ?? Task.CompletedTask;
		__result = Run(original, combatState, cardPlay);
	}

	private static async Task Run(Task original, CombatState combatState, CardPlay cardPlay)
	{
		await original;
		await CardEditorCosmetics.RunBeforeCardPlayed(combatState, cardPlay);
	}
}

