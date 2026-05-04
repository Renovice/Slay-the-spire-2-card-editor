using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPlay))]
internal static class Hook_ShouldPlay_CardEditorPlayRules_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, ref AbstractModel? preventer, AutoPlayType autoPlayType, ref bool __result)
	{
		if (!__result)
		{
			return;
		}

		try
		{
			if (CardEditorExtraEffects.ShouldPreventPlayFromEditorRules(combatState, card, autoPlayType, out AbstractModel? p))
			{
				preventer = p;
				__result = false;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Play rule ShouldPlay failed: {ex}");
		}
	}
}
