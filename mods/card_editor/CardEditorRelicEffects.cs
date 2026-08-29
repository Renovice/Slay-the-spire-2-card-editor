using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

// Phase 1 execution spine for relic-hosted effects. See Notes/RELIC_EFFECTS_PLAN.md.
//
// A relic is not a card play, but the effect engine (CardEditorExtraEffects.ExecuteEffect) needs a
// CardModel "source card" + an owner creature, both of which it reads from AsyncLocal contexts. So a
// relic becomes a host exactly like a Power: we build a synthetic CardPlay around a lightweight proxy
// card, push the same two contexts the power host pushes, and call ExecuteEffect unchanged.

// The synthetic source card for relic-hosted effects. Reuses the created-card base so it is a real,
// ModelDb-registered CardModel that the effect engine already knows how to run (store lookups for an
// undefined id fall back to safe defaults: Attack / AnyEnemy).
public sealed class CardEditorRelicProxyCard : CardEditorCreatedCardBase
{
}

internal static class CardEditorRelicEffects
{
	// Runs every owned relic's configured effects that match the given trigger, for one player.
	internal static async Task RunRelicTrigger(CombatState combatState, PlayerChoiceContext choiceContext, Player player, RelicTriggerKind trigger)
	{
		if (combatState == null || player == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		CardEditorRelicOverrides.EnsureLoaded();

		Creature? ownerCreature = player.Creature;
		if (ownerCreature == null)
		{
			return;
		}

		CardModel? proxy = null;
		foreach (RelicModel relic in player.Relics)
		{
			if (relic == null)
			{
				continue;
			}

			RelicOverride? overrideData = CardEditorRelicOverrides.Get(relic.CanonicalInstance?.Id ?? relic.Id);
			if (overrideData?.ExtraEffects == null || overrideData.ExtraEffects.Count == 0)
			{
				continue;
			}

			bool hasTriggerMatch = false;
			foreach (RelicEffectEntry candidate in overrideData.ExtraEffects)
			{
				if (candidate?.Effect != null && candidate.Trigger == trigger)
				{
					hasTriggerMatch = true;
					break;
				}
			}
			if (!hasTriggerMatch)
			{
				continue;
			}
			if (!ShouldFireRelicTriggerThisTime(combatState, relic.CanonicalInstance?.Id ?? relic.Id, trigger, overrideData))
			{
				continue;
			}

			foreach (RelicEffectEntry entry in overrideData.ExtraEffects)
			{
				if (entry?.Effect == null || entry.Trigger != trigger)
				{
					continue;
				}

				proxy ??= TryCreateProxyCard(combatState, player);
				if (proxy == null)
				{
					return;
				}

				await RunOneEffect(combatState, choiceContext, proxy, ownerCreature, entry.Effect);
			}
		}

		// The proxy is only needed as a transient host during ExecuteEffect; drop it so it does
		// not accumulate in CombatState._allCards across every trigger firing this combat.
		if (proxy != null)
		{
			try
			{
				combatState.RemoveCard(proxy);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor][RelicEffects] proxy cleanup failed: {ex.Message}");
			}
		}
	}

	// Per-combat occurrence counter for each (relic, trigger), used by the Kunai-style every-N gate.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CombatState, Dictionary<string, int>> _relicTriggerCounts = new();

	// Last creature to land a LETHAL hit on each target, captured in AfterDamageGiven (which fires for the
	// killing hit BEFORE Kill()/AfterDeath) so OnEnemyKilled can attribute the kill to the right player -
	// the AfterDeath hook itself carries no dealer/killer argument.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Creature, Creature> _lastLethalDealer = new();

	internal static void RecordLethalDealer(Creature target, Creature dealer)
	{
		if (target == null || dealer == null)
		{
			return;
		}
		_lastLethalDealer.Remove(target);
		_lastLethalDealer.Add(target, dealer);
	}

	internal static Creature? ConsumeLethalDealer(Creature target)
	{
		if (target == null || !_lastLethalDealer.TryGetValue(target, out Creature? dealer))
		{
			return null;
		}
		_lastLethalDealer.Remove(target);
		return dealer;
	}

	private static bool ShouldFireRelicTriggerThisTime(CombatState combatState, ModelId relicId, RelicTriggerKind trigger, RelicOverride overrideData)
	{
		int everyN = 1;
		if (overrideData.TriggerEveryN != null && overrideData.TriggerEveryN.TryGetValue(trigger, out int configured) && configured > 1)
		{
			everyN = configured;
		}
		if (everyN <= 1)
		{
			return true;
		}

		Dictionary<string, int> counts = _relicTriggerCounts.GetOrCreateValue(combatState);
		string key = relicId.ToString() + ":" + (int)trigger;
		counts.TryGetValue(key, out int count);
		count++;
		counts[key] = count;
		return count % everyN == 0;
	}

	private static CardModel? TryCreateProxyCard(CombatState combatState, Player player)
	{
		try
		{
			CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId<CardEditorRelicProxyCard>());
			return combatState.CreateCard(canonical, player);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] Failed to create relic proxy card: {ex.Message}");
			return null;
		}
	}

	private static async Task RunOneEffect(CombatState combatState, PlayerChoiceContext choiceContext, CardModel proxy, Creature ownerCreature, CardExtraEffect effect)
	{
		try
		{
			CardPlay play = new CardPlay
			{
				Card = proxy,
				Player = proxy.Owner,
				Target = null,
				ResultPile = PileType.None,
				Resources = new ResourceInfo
				{
					EnergySpent = 0,
					EnergyValue = 0,
					StarsSpent = 0,
					StarValue = 0
				},
				IsAutoPlay = true,
				PlayIndex = 0,
				PlayCount = 1
			};

			using IDisposable _ = CardEditorEffectSourceContext.PushScoped(proxy);
			using IDisposable __ = CardEditorPowerExecutionHostContext.PushScoped(ownerCreature);
			await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, play, effect, 1);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] Relic effect execution failed: {ex.Message}");
		}
	}

	// Combat-wide trigger: await the original hook, then fire `trigger` for every player in combat.
	internal static async Task Wrap(Task original, CombatState combatState, RelicTriggerKind trigger)
	{
		await original;
		try
		{
			await DispatchAll(combatState, trigger);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] {trigger} dispatch failed: {ex.Message}");
		}
	}

	// Target-scoped trigger (e.g. damage received): fire only for the player whose creature is `target`.
	internal static async Task WrapForTarget(Task original, CombatState combatState, Creature target, RelicTriggerKind trigger)
	{
		await original;
		try
		{
			Player? player = combatState.Players.FirstOrDefault(p => p != null && p.Creature == target);
			if (player != null)
			{
				await DispatchForPlayer(combatState, player, trigger);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] {trigger} (target) dispatch failed: {ex.Message}");
		}
	}

	// Player-scoped trigger (card played by / turn started for a specific player): fire only for them.
	internal static async Task WrapForOnePlayer(Task original, CombatState combatState, Player player, RelicTriggerKind trigger)
	{
		await original;
		try
		{
			if (player != null)
			{
				await DispatchForPlayer(combatState, player, trigger);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] {trigger} (player) dispatch failed: {ex.Message}");
		}
	}

	private static async Task DispatchAll(CombatState combatState, RelicTriggerKind trigger)
	{
		if (combatState?.Players == null)
		{
			return;
		}
		foreach (Player player in combatState.Players)
		{
			if (player != null)
			{
				await DispatchForPlayer(combatState, player, trigger);
			}
		}
	}

	private static async Task DispatchForPlayer(CombatState combatState, Player player, RelicTriggerKind trigger)
	{
		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}
		HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
		Task task = RunRelicTrigger(combatState, choiceContext, player, trigger);
		bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
		if (!completed && choiceContext.GameAction != null)
		{
			// Effect opened a player-choice UI: wait for that action to fully resolve before
			// returning, so the rest of the turn does not run ahead of the unresolved choice.
			await choiceContext.GameAction.CompletionTask;
		}
	}
}

// Phase 1 dispatch: fire OnCombatStart relic effects. Mirrors the deck-passive combat-start patch.
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class Hook_BeforeCombatStart_CardEditorRelicEffects_Patch
{
	public static void Postfix(IRunState runState, CombatState? combatState, ref Task __result)
	{
		if (__result == null || runState == null || combatState == null)
		{
			return;
		}
		__result = RunAfter(__result, runState, combatState);
	}

	private static async Task RunAfter(Task original, IRunState runState, CombatState combatState)
	{
		await original;
		if (!CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in runState.Players)
			{
				if (player == null)
				{
					continue;
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorRelicEffects.RunRelicTrigger(combatState, choiceContext, player, RelicTriggerKind.OnCombatStart);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEffects] BeforeCombatStart relic dispatch failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
internal static class Hook_AfterPlayerTurnStart_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState combatState, Player player, ref Task __result)
	{
		// AfterPlayerTurnStart identifies whose turn started; scope "start of your turn" to them.
		if (__result == null || combatState == null || player == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, combatState, player, RelicTriggerKind.OnTurnStart);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnEnd))]
internal static class Hook_AfterTurnEnd_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
	{
		if (__result == null || combatState == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		if (side == CombatSide.Player)
		{
			// Scope "at the end of your turn" to the players who actually ended the turn (extra-turn flows
			// can end only a subset), instead of firing for every player in combat.
			if (participants != null)
			{
				foreach (Creature participant in participants)
				{
					if (participant?.Player != null)
					{
						__result = CardEditorRelicEffects.WrapForTarget(__result, combatState, participant, RelicTriggerKind.OnTurnEnd);
					}
				}
			}
		}
		else if (side == CombatSide.Enemy)
		{
			__result = CardEditorRelicEffects.Wrap(__result, combatState, RelicTriggerKind.OnEnemyTurnEnd);
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class Hook_AfterCardPlayed_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState combatState, CardPlay cardPlay, ref Task __result)
	{
		// Scope "when you play a card" to the player who actually played it (not all players in MP).
		Player? owner = cardPlay?.Card?.Owner;
		if (__result == null || combatState == null || owner == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, combatState, owner, RelicTriggerKind.OnCardPlayed);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class Hook_AfterCombatEnd_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState? combatState, ref Task __result)
	{
		if (__result == null || combatState == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.Wrap(__result, combatState, RelicTriggerKind.OnCombatEnd);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
internal static class Hook_AfterDamageReceived_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState? combatState, Creature target, DamageResult result, ref Task __result)
	{
		// Only fire when HP was actually lost (skip fully-blocked hits). Note: vanilla skips this hook for
		// lethal damage, so an effect on "When you take damage" still cannot react to a killing blow.
		if (__result == null || combatState == null || target == null || result == null || result.UnblockedDamage <= 0 || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.WrapForTarget(__result, combatState, target, RelicTriggerKind.OnDamageTaken);
	}
}

// ===== Phase 2 reactive in-combat triggers (2026-06-19) =====
// All follow the existing pattern: postfix a game Hook, then wrap the returned Task so relic effects fire
// after the original. Scope: Wrap = every player; WrapForOnePlayer = a specific player; WrapForTarget =
// the player whose creature is the event subject (enemy subjects resolve to no player = no dispatch).
// These hooks pass the combat state as ICombatState; the concrete CombatState pattern-cast guards null too.

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class Hook_AfterCombatVictory_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState? combatState, ref Task __result)
	{
		if (__result == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.Wrap(__result, cs, RelicTriggerKind.OnCombatVictory);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class Hook_AfterSideTurnStart_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, CombatSide side, ref Task __result)
	{
		// Player-side turn start is already handled (scoped) by the AfterPlayerTurnStart patch; here we
		// only add the ENEMY side so "at the start of the enemy turn" works.
		if (__result == null || side != CombatSide.Enemy || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.Wrap(__result, cs, RelicTriggerKind.OnEnemyTurnStart);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
internal static class Hook_AfterCardDrawn_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, CardModel card, ref Task __result)
	{
		Player? owner = card?.Owner;
		if (__result == null || owner == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, owner, RelicTriggerKind.OnCardDrawn);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDiscarded))]
internal static class Hook_AfterCardDiscarded_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, CardModel card, ref Task __result)
	{
		Player? owner = card?.Owner;
		if (__result == null || owner == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, owner, RelicTriggerKind.OnCardDiscarded);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
internal static class Hook_AfterCardExhausted_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, CardModel card, ref Task __result)
	{
		Player? owner = card?.Owner;
		if (__result == null || owner == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, owner, RelicTriggerKind.OnCardExhausted);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterShuffle))]
internal static class Hook_AfterShuffle_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Player shuffler, ref Task __result)
	{
		if (__result == null || shuffler == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, shuffler, RelicTriggerKind.OnShuffle);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class Hook_AfterDamageGiven_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Creature? dealer, DamageResult results, Creature target, ref Task __result)
	{
		if (dealer == null || results == null)
		{
			return;
		}
		// AfterDamageGiven fires for the lethal hit BEFORE Kill()/AfterDeath, so capture the killer here for
		// OnEnemyKilled to attribute the kill (the AfterDeath hook carries no dealer argument).
		if (results.WasTargetKilled && target != null)
		{
			CardEditorRelicEffects.RecordLethalDealer(target, dealer);
		}
		// OnDamageDealt: only on damage that actually landed (UnblockedDamage > 0), matching the count-event
		// path; "When you deal damage" no longer triggers on fully-blocked hits.
		if (__result != null && results.UnblockedDamage > 0 && combatState is CombatState cs && CardEditorRelicOverrides.HasAnyOverrides)
		{
			__result = CardEditorRelicEffects.WrapForTarget(__result, cs, dealer, RelicTriggerKind.OnDamageDealt);
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
internal static class Hook_AfterBlockGained_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Creature creature, decimal amount, ref Task __result)
	{
		// Vanilla calls AfterBlockGained even when the resolved block is 0; only fire for real block gains.
		if (__result == null || creature == null || amount <= 0m || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForTarget(__result, cs, creature, RelicTriggerKind.OnBlockGained);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCurrentHpChanged))]
internal static class Hook_AfterCurrentHpChanged_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState? combatState, Creature creature, decimal delta, ref Task __result)
	{
		if (__result == null || creature == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		if (delta < 0m)
		{
			__result = CardEditorRelicEffects.WrapForTarget(__result, cs, creature, RelicTriggerKind.OnHpLost);
		}
		else if (delta > 0m)
		{
			__result = CardEditorRelicEffects.WrapForTarget(__result, cs, creature, RelicTriggerKind.OnHeal);
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class Hook_AfterDeath_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState? combatState, Creature creature, ref Task __result)
	{
		// "When you kill an enemy": fire for all players when a non-player creature dies.
		if (__result == null || creature == null || creature.IsPlayer || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		// Attribute the kill to the player whose lethal hit was captured in AfterDamageGiven; unattributed
		// deaths (poison/scripted/another enemy) no longer fire, and only the killer's player fires (not all).
		Creature? killer = CardEditorRelicEffects.ConsumeLethalDealer(creature);
		if (killer?.Player == null) return;
		__result = CardEditorRelicEffects.WrapForTarget(__result, cs, killer, RelicTriggerKind.OnEnemyKilled);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset))]
internal static class Hook_AfterEnergyReset_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Player player, ref Task __result)
	{
		if (__result == null || player == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, player, RelicTriggerKind.OnEnergyReset);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterOrbChanneled))]
internal static class Hook_AfterOrbChanneled_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Player player, ref Task __result)
	{
		if (__result == null || player == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, player, RelicTriggerKind.OnOrbChanneled);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterStarsGained))]
internal static class Hook_AfterStarsGained_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Player gainer, ref Task __result)
	{
		if (__result == null || gainer == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, gainer, RelicTriggerKind.OnStarsGained);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeHandDraw))]
internal static class Hook_BeforeHandDraw_CardEditorRelicEffects_Patch
{
	public static void Postfix(ICombatState combatState, Player player, ref Task __result)
	{
		if (__result == null || player == null || combatState is not CombatState cs || !CardEditorRelicOverrides.HasAnyOverrides) return;
		__result = CardEditorRelicEffects.WrapForOnePlayer(__result, cs, player, RelicTriggerKind.OnHandDraw);
	}
}
