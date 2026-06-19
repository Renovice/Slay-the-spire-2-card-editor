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

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd))]
internal static class Hook_AfterTurnEnd_CardEditorRelicEffects_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Player || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.Wrap(__result, combatState, RelicTriggerKind.OnTurnEnd);
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
	public static void Postfix(CombatState? combatState, Creature target, ref Task __result)
	{
		if (__result == null || combatState == null || target == null || !CardEditorRelicOverrides.HasAnyOverrides)
		{
			return;
		}
		__result = CardEditorRelicEffects.WrapForTarget(__result, combatState, target, RelicTriggerKind.OnDamageTaken);
	}
}
