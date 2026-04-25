using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
internal static class Hook_AfterBlockGained_CardEditorResourceCount_Patch
{
	public static void Postfix(CombatState combatState, Creature creature, decimal amount, ValueProp props, CardModel? cardSource, ref Task __result)
	{
		if (__result == null || combatState == null || creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, creature, amount, cardSource);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature creature, decimal amount, CardModel? cardSource)
	{
		await original;
		try
		{
			int delta = Math.Max(0, (int)Math.Min(amount, int.MaxValue));
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentBlockApplied(delta);
				CardEditorExtraEffects.RecordResourceCount(combatState, creature, CardExtraEffectCountEvent.BlockGained, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, creature, CardExtraEffectCountEvent.BlockGained, triggeringCard: cardSource, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Block-gain count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergySpent))]
internal static class Hook_AfterEnergySpent_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, int amount, ref Task __result)
	{
		if (__result == null || combatState == null || card?.Owner?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, card.Owner.Creature, card, amount);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature creature, CardModel? triggeringCard, int amount)
	{
		await original;
		try
		{
			int delta = Math.Max(0, amount);
			if (delta > 0)
			{
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, creature, CardExtraEffectCountEvent.EnergyUsed, triggeringCard: triggeringCard, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Energy-spent trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class Hook_AfterDamageGiven_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(PlayerChoiceContext choiceContext, CombatState combatState, Creature? dealer, DamageResult results, ref Task __result)
	{
		if (__result == null || choiceContext == null || combatState == null || dealer == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, dealer, results);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature dealer, DamageResult results)
	{
		await original;
		try
		{
			int delta = Math.Max(0, results?.UnblockedDamage ?? 0);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentDamageApplied(delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, dealer, CardExtraEffectCountEvent.DamageDealt, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Damage-dealt trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
internal static class Hook_AfterDamageReceived_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(PlayerChoiceContext choiceContext, IRunState runState, CombatState? combatState, Creature target, DamageResult result, ref Task __result)
	{
		if (__result == null || choiceContext == null || combatState == null || target == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, target, result);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature target, DamageResult result)
	{
		await original;
		try
		{
			int delta = Math.Max(0, result?.UnblockedDamage ?? 0);
			if (delta > 0)
			{
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, target, CardExtraEffectCountEvent.DamageTaken, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Damage-taken trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSummon))]
internal static class Hook_AfterSummon_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, Player summoner, decimal amount, ref Task __result)
	{
		if (__result == null || choiceContext == null || combatState == null || summoner?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, summoner.Creature, amount);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature creature, decimal amount)
	{
		await original;
		try
		{
			int delta = Math.Max(0, (int)Math.Min(amount, int.MaxValue));
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentSummonApplied(delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, creature, CardExtraEffectCountEvent.Summoned, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Summon trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
internal static class PlayerCmd_GainEnergy_CardEditorResourceCount_Patch
{
	public static void Prefix(Player player, out int __state)
	{
		__state = player?.PlayerCombatState?.Energy ?? 0;
	}

	public static void Postfix(Player player, ref Task __result, int __state)
	{
		if (__result == null || player?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, __state, CardExtraEffectCountEvent.EnergyGained, gain: true);
	}

	private static async Task TrackAfter(Task original, Player player, int oldEnergy, CardExtraEffectCountEvent countEvent, bool gain)
	{
		await original;
		try
		{
			PlayerCombatState? combatState = player.PlayerCombatState;
			Creature actor = player.Creature;
			CombatState? state = actor.CombatState.AsCombatState();
			int currentEnergy = combatState?.Energy ?? oldEnergy;
			int delta = gain
				? Math.Max(0, currentEnergy - oldEnergy)
				: Math.Max(0, oldEnergy - currentEnergy);
			if (delta > 0 && state != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentEnergyApplied(delta, gain);
				CardEditorExtraEffects.RecordResourceCount(state, actor, countEvent, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(state, actor, countEvent, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Energy count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseEnergy))]
internal static class PlayerCmd_LoseEnergy_CardEditorResourceCount_Patch
{
	public static void Prefix(Player player, out int __state)
	{
		__state = player?.PlayerCombatState?.Energy ?? 0;
	}

	public static void Postfix(Player player, ref Task __result, int __state)
	{
		if (__result == null || player?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, __state, CardExtraEffectCountEvent.EnergyLost, gain: false);
	}

	private static async Task TrackAfter(Task original, Player player, int oldEnergy, CardExtraEffectCountEvent countEvent, bool gain)
	{
		await original;
		try
		{
			PlayerCombatState? combatState = player.PlayerCombatState;
			Creature actor = player.Creature;
			CombatState? state = actor.CombatState.AsCombatState();
			int currentEnergy = combatState?.Energy ?? oldEnergy;
			int delta = gain
				? Math.Max(0, currentEnergy - oldEnergy)
				: Math.Max(0, oldEnergy - currentEnergy);
			if (delta > 0 && state != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentEnergyApplied(delta, gain);
				CardEditorExtraEffects.RecordResourceCount(state, actor, countEvent, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(state, actor, countEvent, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Energy count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseBlockInternal))]
internal static class Creature_LoseBlockInternal_CardEditorResourceCount_Patch
{
	public static void Prefix(Creature __instance, out int __state)
	{
		__state = __instance?.Block ?? 0;
	}

	public static void Postfix(Creature __instance, int __state)
	{
		if (__instance?.CombatState.AsCombatState() == null)
		{
			return;
		}

		try
		{
			int delta = Math.Max(0, __state - __instance.Block);
			if (delta > 0 && __instance.CombatState.AsCombatState() != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentBlockRemoved(delta);
				CardEditorExtraEffects.RecordResourceCount(__instance.CombatState.AsCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(__instance.CombatState.AsCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Block-loss count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class Creature_DamageBlockInternal_CardEditorResourceCount_Patch
{
	public static void Prefix(Creature __instance, out int __state)
	{
		__state = __instance?.Block ?? 0;
	}

	public static void Postfix(Creature __instance, int __state)
	{
		if (__instance?.CombatState.AsCombatState() == null)
		{
			return;
		}

		try
		{
			int delta = Math.Max(0, __state - __instance.Block);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentBlockRemoved(delta);
				CardEditorExtraEffects.RecordResourceCount(__instance.CombatState.AsCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(__instance.CombatState.AsCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Damage-block count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Creature), nameof(Creature.AfterTurnStart))]
internal static class Creature_AfterTurnStart_CardEditorResourceCount_Patch
{
	public static void Prefix(Creature __instance, out int __state)
	{
		__state = __instance?.Block ?? 0;
	}

	public static void Postfix(Creature __instance, ref Task __result, int __state)
	{
		if (__result == null || __instance?.CombatState.AsCombatState() == null)
		{
			return;
		}

		__result = TrackAfter(__result, __instance, __state);
	}

	private static async Task TrackAfter(Task original, Creature creature, int oldBlock)
	{
		await original;
		try
		{
			CombatState? combatState = creature.CombatState.AsCombatState();
			if (combatState == null)
			{
				return;
			}

			int delta = Math.Max(0, oldBlock - creature.Block);
			if (delta > 0 && creature.Block <= 0)
			{
				CardEditorExtraEffects.RecordBetweenTurnsBlockClear(combatState, creature, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, creature, CardExtraEffectCountEvent.BlockLost, CardEditorExtraEffects.ResourceCountSource.BetweenTurnsBlockClear, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Between-turn block-clear count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class CreatureCmd_Heal_CardEditorResourceCount_Patch
{
	public static void Prefix(Creature creature, out int __state)
	{
		__state = creature?.CurrentHp ?? 0;
	}

	public static void Postfix(Creature creature, ref Task __result, int __state)
	{
		if (__result == null || creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, creature, __state);
	}

	private static async Task TrackAfter(Task original, Creature creature, int oldHp)
	{
		await original;
		try
		{
			CombatState? combatState = creature.CombatState.AsCombatState();
			if (combatState == null)
			{
				return;
			}

			int delta = Math.Max(0, creature.CurrentHp - oldHp);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentHealingApplied(delta);
				CardEditorExtraEffects.RecordResourceCount(combatState, creature, CardExtraEffectCountEvent.HealingReceived, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, creature, CardExtraEffectCountEvent.HealingReceived, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Healing count tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
internal static class Hook_AfterPowerAmountChanged_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(CombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource, ref Task __result)
	{
		if (__result == null || combatState == null || power?.Owner == null || amount == 0m)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, power, amount, cardSource);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, PowerModel power, decimal amount, CardModel? cardSource)
	{
		await original;
		try
		{
			Creature owner = power.Owner;
			if (owner == null)
			{
				return;
			}

			int delta = Math.Max(0, (int)Math.Min(Math.Abs(amount), int.MaxValue));
			if (delta <= 0)
			{
				return;
			}

			if (amount > 0m)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentPowerAmountChanged(power, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, owner, CardExtraEffectCountEvent.StatusGained, triggeringPower: power, triggeringCard: cardSource, amount: delta);
			}
			else if (amount < 0m)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentPowerAmountChanged(power, delta);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, owner, CardExtraEffectCountEvent.StatusLost, triggeringPower: power, triggeringCard: cardSource, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Power/status trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainStars))]
internal static class PlayerCmd_GainStars_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(decimal amount, Player player, ref Task __result)
	{
		if (__result == null || player?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, amount, CardExtraEffectCountEvent.StarsGained);
	}

	private static async Task TrackAfter(Task original, Player player, decimal amount, CardExtraEffectCountEvent countEvent)
	{
		await original;
		try
		{
			Creature actor = player.Creature;
			CombatState? combatState = actor?.CombatState.AsCombatState();
			int delta = amount <= 0m
				? 0
				: (int)Math.Min(decimal.Truncate(amount), int.MaxValue);
			if (combatState != null && delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentStarsApplied(delta, gain: true);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, actor, countEvent, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Star-gain trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseStars))]
internal static class PlayerCmd_LoseStars_CardEditorPowerCountEvent_Patch
{
	public static void Postfix(decimal amount, Player player, ref Task __result)
	{
		if (__result == null || player?.Creature == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, amount, CardExtraEffectCountEvent.StarsLost);
	}

	private static async Task TrackAfter(Task original, Player player, decimal amount, CardExtraEffectCountEvent countEvent)
	{
		await original;
		try
		{
			Creature actor = player.Creature;
			CombatState? combatState = actor?.CombatState.AsCombatState();
			int delta = amount <= 0m
				? 0
				: (int)Math.Min(decimal.Truncate(amount), int.MaxValue);
			if (combatState != null && delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentStarsApplied(delta, gain: false);
				await CardEditorExtraEffects.TriggerPowerCountEventAsync(combatState, actor, countEvent, amount: delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Star-loss trigger failed: {ex}");
		}
	}
}
