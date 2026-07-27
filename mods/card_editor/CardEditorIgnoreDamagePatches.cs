using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), new[]
{
	typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
	typeof(IEnumerable<Creature>),
	typeof(decimal),
	typeof(ValueProp),
	typeof(Creature),
	typeof(CardModel)
})]
internal static class CreatureCmd_Damage_IgnoreProps_Patch
{
	public static void Prefix(ref ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		CombatState? combatState = CardEditorCardPlayContext.Current?.Card.GetConcreteCombatState() ?? cardSource.GetConcreteCombatState() ?? dealer.GetConcreteCombatState();
		(bool ignoreBlock, bool ignoreDamageModifiers) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
			CardExtraEffectKind.IgnoreBlock,
			CardExtraEffectKind.IgnoreDamageModifiers,
			cardSource,
			dealer,
			combatState,
			target: null);
		if (ignoreBlock)
		{
			props |= ValueProp.Unblockable;
		}
		if (ignoreDamageModifiers)
		{
			props |= ValueProp.Unpowered;
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class Hook_ModifyDamage_IgnoreProps_Patch
{
	public static void Prefix(ICombatState? combatState, Creature? target, Creature? dealer, ref ValueProp props, CardModel? cardSource)
	{
		using CardEditorDebugPerfTimer.PerfScope _perf = CardEditorDebugPerfTimer.Measure("ModifyDamage(mod)");
		// Public-build signature gives ICombatState; the pair-fetch (one effects build for both
		// kinds) is the perf-pass optimisation and is kept.
		CombatState? concreteCombatState = combatState.GetConcreteCombatState();
		(bool ignoreBlock, bool ignoreDamageModifiers) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
			CardExtraEffectKind.IgnoreBlock,
			CardExtraEffectKind.IgnoreDamageModifiers,
			cardSource,
			dealer,
			concreteCombatState,
			target);
		if (ignoreBlock)
		{
			props |= ValueProp.Unblockable;
		}
		if (ignoreDamageModifiers)
		{
			props |= ValueProp.Unpowered;
		}
	}
}
