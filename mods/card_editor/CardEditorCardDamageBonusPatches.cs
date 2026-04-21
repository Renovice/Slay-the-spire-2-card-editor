using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class Hook_ModifyDamageInternal_CardDamageBonus_Patch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(Hook), "ModifyDamageInternal")!;
	}

	[HarmonyPriority(Priority.First)]
	public static void Prefix(
		CombatState? combatState,
		Creature? target,
		Creature? dealer,
		ref decimal damage,
		ValueProp props,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		CombatState? effectiveCombatState = combatState
			?? effectiveSource?.CombatState
			?? dealer?.CombatState
			?? target?.CombatState;
		if (effectiveCombatState == null || effectiveSource == null)
		{
			return;
		}

		int bonus = CardEditorExtraEffects.GetCardDamageBonusAmount(effectiveCombatState, effectiveSource, target, dealer);
		if (bonus <= 0)
		{
			return;
		}

		damage += bonus;
	}
}
