using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
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
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, creature, CardExtraEffectCountEvent.BlockGained, triggeringCard: cardSource, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(creature, CardExtraEffectCountEvent.BlockGained, delta, combatState);
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
		Creature? ownerCreature = card.TryGetOwnerCreature();
		if (__result == null || combatState == null || ownerCreature == null)
		{
			return;
		}

		__result = TrackAfter(__result, combatState, ownerCreature, card, amount);
	}

	private static async Task TrackAfter(Task original, CombatState combatState, Creature creature, CardModel? triggeringCard, int amount)
	{
		await original;
		try
		{
			int delta = Math.Max(0, amount);
			if (delta > 0)
			{
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, creature, CardExtraEffectCountEvent.EnergyUsed, triggeringCard: triggeringCard, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(creature, CardExtraEffectCountEvent.EnergyUsed, delta, combatState);
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

		__result = TrackAfter(__result, choiceContext, combatState, dealer, results);
	}

	private static async Task TrackAfter(Task original, PlayerChoiceContext choiceContext, CombatState combatState, Creature dealer, DamageResult results)
	{
		await original;
		try
		{
			int delta = Math.Max(0, results?.UnblockedDamage ?? 0);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentDamageApplied(delta);
				await CardEditorExtraEffects.TriggerPowerCountEventWithContextAsync(combatState, choiceContext, dealer, CardExtraEffectCountEvent.DamageDealt, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(dealer, CardExtraEffectCountEvent.DamageDealt, delta, combatState);
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

		__result = TrackAfter(__result, choiceContext, combatState, target, result);
	}

	private static async Task TrackAfter(Task original, PlayerChoiceContext choiceContext, CombatState combatState, Creature target, DamageResult result)
	{
		await original;
		try
		{
			int delta = Math.Max(0, result?.UnblockedDamage ?? 0);
			if (delta > 0)
			{
				await CardEditorExtraEffects.TriggerPowerCountEventWithContextAsync(combatState, choiceContext, target, CardExtraEffectCountEvent.DamageTaken, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(target, CardExtraEffectCountEvent.DamageTaken, delta, combatState);
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

		__result = TrackAfter(__result, choiceContext, combatState, summoner.Creature, amount);
	}

	private static async Task TrackAfter(Task original, PlayerChoiceContext choiceContext, CombatState combatState, Creature creature, decimal amount)
	{
		await original;
		try
		{
			int delta = Math.Max(0, (int)Math.Min(amount, int.MaxValue));
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentSummonApplied(delta);
				await CardEditorExtraEffects.TriggerPowerCountEventWithContextAsync(combatState, choiceContext, creature, CardExtraEffectCountEvent.Summoned, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(creature, CardExtraEffectCountEvent.Summoned, delta, combatState);
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
			CombatState? state = actor.GetConcreteCombatState();
			int currentEnergy = combatState?.Energy ?? oldEnergy;
			int delta = gain
				? Math.Max(0, currentEnergy - oldEnergy)
				: Math.Max(0, oldEnergy - currentEnergy);
			if (delta > 0 && state != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentEnergyApplied(delta, gain);
				CardEditorExtraEffects.RecordResourceCount(state, actor, countEvent, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(state, actor, countEvent, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(actor, countEvent, delta, state);
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
			CombatState? state = actor.GetConcreteCombatState();
			int currentEnergy = combatState?.Energy ?? oldEnergy;
			int delta = gain
				? Math.Max(0, currentEnergy - oldEnergy)
				: Math.Max(0, oldEnergy - currentEnergy);
			if (delta > 0 && state != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentEnergyApplied(delta, gain);
				CardEditorExtraEffects.RecordResourceCount(state, actor, countEvent, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(state, actor, countEvent, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(actor, countEvent, delta, state);
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
		if (__instance.GetConcreteCombatState() == null)
		{
			return;
		}

		try
		{
			int delta = Math.Max(0, __state - __instance.Block);
			if (delta > 0 && __instance.GetConcreteCombatState() != null)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentBlockRemoved(delta);
				CardEditorExtraEffects.RecordResourceCount(__instance.GetConcreteCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(__instance.GetConcreteCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, amount: delta);
				TaskHelper.RunSafely(CardEditorQuestEffects.RecordRunProgress(__instance, CardExtraEffectCountEvent.BlockLost, delta, __instance.GetConcreteCombatState()));
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
		if (__instance.GetConcreteCombatState() == null)
		{
			return;
		}

		try
		{
			int delta = Math.Max(0, __state - __instance.Block);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentBlockRemoved(delta);
				CardEditorExtraEffects.RecordResourceCount(__instance.GetConcreteCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(__instance.GetConcreteCombatState(), __instance, CardExtraEffectCountEvent.BlockLost, amount: delta);
				TaskHelper.RunSafely(CardEditorQuestEffects.RecordRunProgress(__instance, CardExtraEffectCountEvent.BlockLost, delta, __instance.GetConcreteCombatState()));
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
		if (__result == null || __instance.GetConcreteCombatState() == null)
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
			CombatState? combatState = creature.GetConcreteCombatState();
			if (combatState == null)
			{
				return;
			}

			int delta = Math.Max(0, oldBlock - creature.Block);
			if (delta > 0 && creature.Block <= 0)
			{
				CardEditorExtraEffects.RecordBetweenTurnsBlockClear(combatState, creature, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, creature, CardExtraEffectCountEvent.BlockLost, CardEditorExtraEffects.ResourceCountSource.BetweenTurnsBlockClear, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(creature, CardExtraEffectCountEvent.BlockLost, delta, combatState);
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
			CombatState? combatState = creature.GetConcreteCombatState();
			if (combatState == null)
			{
				return;
			}

			int delta = Math.Max(0, creature.CurrentHp - oldHp);
			if (delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentHealingApplied(delta);
				CardEditorExtraEffects.RecordResourceCount(combatState, creature, CardExtraEffectCountEvent.HealingReceived, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, creature, CardExtraEffectCountEvent.HealingReceived, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(creature, CardExtraEffectCountEvent.HealingReceived, delta, combatState);
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
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, owner, CardExtraEffectCountEvent.StatusGained, triggeringPower: power, triggeringCard: cardSource, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(owner, CardExtraEffectCountEvent.StatusGained, delta, combatState, power);
			}
			else if (amount < 0m)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentPowerAmountChanged(power, delta);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, owner, CardExtraEffectCountEvent.StatusLost, triggeringPower: power, triggeringCard: cardSource, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(owner, CardExtraEffectCountEvent.StatusLost, delta, combatState, power);
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
			CombatState? combatState = actor.GetConcreteCombatState();
			int delta = amount <= 0m
				? 0
				: (int)Math.Min(decimal.Truncate(amount), int.MaxValue);
			if (combatState != null && delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentStarsApplied(delta, gain: true);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, actor, countEvent, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(actor, countEvent, delta, combatState);
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
			CombatState? combatState = actor.GetConcreteCombatState();
			int delta = amount <= 0m
				? 0
				: (int)Math.Min(decimal.Truncate(amount), int.MaxValue);
			if (combatState != null && delta > 0)
			{
				CardEditorEffectExecutionAmountContext.ReportCurrentStarsApplied(delta, gain: false);
				CardEditorExtraEffects.TriggerPowerCountEvent(combatState, actor, countEvent, amount: delta);
				await CardEditorQuestEffects.RecordRunProgress(actor, countEvent, delta, combatState);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Star-loss trigger failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainGold))]
internal static class PlayerCmd_GainGold_CardEditorQuestCount_Patch
{
	public static void Prefix(Player player, out int __state)
	{
		__state = player?.Gold ?? 0;
	}

	public static void Postfix(Player player, ref Task __result, int __state)
	{
		if (__result == null || player == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, __state);
	}

	private static async Task TrackAfter(Task original, Player player, int oldGold)
	{
		await original;
		try
		{
			int delta = Math.Max(0, player.Gold - oldGold);
			if (delta > 0)
			{
				await CardEditorQuestEffects.RecordRunProgress(player, CardExtraEffectCountEvent.GoldGained, delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Gold-gain quest tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseGold))]
internal static class PlayerCmd_LoseGold_CardEditorQuestCount_Patch
{
	public static void Prefix(Player player, out int __state)
	{
		__state = player?.Gold ?? 0;
	}

	public static void Postfix(Player player, ref Task __result, int __state)
	{
		if (__result == null || player == null)
		{
			return;
		}

		__result = TrackAfter(__result, player, __state);
	}

	private static async Task TrackAfter(Task original, Player player, int oldGold)
	{
		await original;
		try
		{
			int delta = Math.Max(0, oldGold - player.Gold);
			if (delta > 0)
			{
				await CardEditorQuestEffects.RecordRunProgress(player, CardExtraEffectCountEvent.GoldLost, delta);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Gold-loss quest tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionUsed))]
internal static class Hook_AfterPotionUsed_CardEditorQuestCount_Patch
{
	public static void Postfix(IRunState runState, object? combatState, PotionModel potion, Creature? target, ref Task __result)
	{
		if (__result == null || potion?.Owner == null)
		{
			return;
		}

		__result = TrackAfter(__result, potion.Owner, combatState.GetConcreteCombatState());
	}

	private static async Task TrackAfter(Task original, Player owner, CombatState? combatState)
	{
		await original;
		try
		{
			await CardEditorQuestEffects.RecordRunProgress(owner, CardExtraEffectCountEvent.PotionUsed, 1, combatState);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Potion-use quest tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterRoomEntered))]
internal static class Hook_AfterRoomEntered_CardEditorQuestCount_Patch
{
	public static void Postfix(IRunState runState, AbstractRoom room, ref Task __result)
	{
		if (__result == null || runState == null || room == null)
		{
			return;
		}

		__result = TrackAfter(__result, runState, room);
	}

	private static async Task TrackAfter(Task original, IRunState runState, AbstractRoom room)
	{
		await original;
		try
		{
			await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.RoomEntered);
			if (room is CombatRoom || room.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss)
			{
				await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.CombatEntered);
			}
			if (room.RoomType == RoomType.Shop)
			{
				await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.ShopEntered);
			}
			if (room.RoomType == RoomType.RestSite)
			{
				await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.RestSiteEntered);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Room-enter quest tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class Hook_AfterCombatVictory_CardEditorQuestCount_Patch
{
	public static void Postfix(IRunState runState, object? combatState, CombatRoom room, ref Task __result)
	{
		if (__result == null || runState == null)
		{
			return;
		}

		__result = TrackAfter(__result, runState, combatState.GetConcreteCombatState());
	}

	private static async Task TrackAfter(Task original, IRunState runState, CombatState? combatState)
	{
		await original;
		try
		{
			await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.CombatWon, 1, combatState);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Combat-win quest tracking failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class Hook_AfterDeath_CardEditorQuestCount_Patch
{
	public static void Postfix(IRunState runState, object? combatState, Creature creature, bool wasRemovalPrevented, float deathAnimLength, ref Task __result)
	{
		if (__result == null || runState == null || creature == null || wasRemovalPrevented || !creature.IsEnemy)
		{
			return;
		}

		__result = TrackAfter(__result, runState, combatState.GetConcreteCombatState());
	}

	private static async Task TrackAfter(Task original, IRunState runState, CombatState? combatState)
	{
		await original;
		try
		{
			await CardEditorQuestEffects.RecordRunProgress(runState, CardExtraEffectCountEvent.EnemyKilled, 1, combatState);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Enemy-kill quest tracking failed: {ex}");
		}
	}
}
