using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

// v0.109.0 folded the (PileType, CardPilePosition) result into the CardLocation record struct.
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardPlayResultLocation))]
internal static class Hook_ModifyCardPlayResultLocation_CardEditorExtraEffects_Patch
{
	public static void Postfix(
		CardModel card,
		ref CardLocation __result)
	{
		if (card == null)
		{
			return;
		}

		if (CardEditorExtraEffects.TryGetCardPlayResultPileOverride(card, out PileType overridePile, out CardPilePosition overridePosition))
		{
			__result = new CardLocation(__result.player, overridePile, overridePosition);
		}
	}
}

internal static class CardEditorExtraEffectTriggerPatchHelpers
{
	public static List<CardModel> GetCombatPileSnapshot(Player? player)
	{
		List<CardModel> cards = new List<CardModel>();
		if (player?.PlayerCombatState?.AllPiles == null)
		{
			return cards;
		}

		HashSet<CardModel> seen = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
		foreach (CardPile pile in player.PlayerCombatState.AllPiles)
		{
			if (pile?.Cards == null || pile.Cards.Count == 0)
			{
				continue;
			}

			foreach (CardModel card in pile.Cards)
			{
				if (card != null && seen.Add(card))
				{
					cards.Add(card);
				}
			}
		}

		return cards;
	}

	public static List<CardModel> GetDeckAndCombatPileSnapshot(Player? player)
	{
		List<CardModel> cards = new List<CardModel>();
		if (player == null)
		{
			return cards;
		}

		HashSet<CardModel> seen = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
		if (player.Deck?.Cards != null)
		{
			foreach (CardModel card in player.Deck.Cards)
			{
				if (card != null && seen.Add(card))
				{
					cards.Add(card);
				}
			}
		}

		foreach (CardModel card in GetCombatPileSnapshot(player))
		{
			if (card != null && seen.Add(card))
			{
				cards.Add(card);
			}
		}

		return cards;
	}

	public static async Task RunForEachCombatPileCard(
		CombatState combatState,
		Player player,
		ulong netId,
		Func<PlayerChoiceContext, CardModel, Task> runEffect)
	{
		if (combatState == null || player == null || runEffect == null)
		{
			return;
		}

		foreach (CardModel card in GetCombatPileSnapshot(player))
		{
			await RunForCard(player, netId, card, runEffect);
		}
	}

	public static async Task RunForEachDeckAndCombatPileCard(
		Player player,
		ulong netId,
		Func<PlayerChoiceContext, CardModel, Task> runEffect)
	{
		if (player == null || runEffect == null)
		{
			return;
		}

		foreach (CardModel card in GetDeckAndCombatPileSnapshot(player))
		{
			await RunForCard(player, netId, card, runEffect);
		}
	}

	public static async Task RunForCard(
		Player player,
		ulong netId,
		CardModel card,
		Func<PlayerChoiceContext, CardModel, Task> runEffect)
	{
		if (player == null || card == null || runEffect == null)
		{
			return;
		}

		HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId, GameActionType.Combat);
		Task task = runEffect(choiceContext, card);
		bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
		if (!completed && choiceContext.GameAction != null)
		{
			await choiceContext.GameAction.CompletionTask;
		}
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class Hook_BeforeCombatStart_CardEditorDeckPassive_Patch
{
	private static readonly Dictionary<CombatState, HashSet<CardModel>> DeferredChoiceCardsByCombat =
		new Dictionary<CombatState, HashSet<CardModel>>(ReferenceEqualityComparer<CombatState>.Instance);

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
		CardEditorRunSelfScalingState.RestoreForCombat(combatState);
		await CardEditorRunPowerState.ApplyForCombat(combatState);

		if (!CardEditorOverrides.HasAnyOverrides)
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
				if (player == null || player.Deck?.Cards == null || player.Deck.Cards.Count == 0)
				{
					continue;
				}

				List<CardModel> deckSnapshot = player.Deck.Cards.Where(card => card != null).ToList();
				foreach (CardModel card in deckSnapshot)
				{
					if (CardEditorExtraEffects.TriggerMayOpenCardSelectionUi(combatState, card, CardExtraEffectTrigger.DeckPassiveCombatStart))
					{
						DeferChoiceCard(combatState, card);
						continue;
					}

					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunDeckPassiveCombatStart(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}

			await CardEditorQuestEffects.RunInstalledPassivesForCombatStart(combatState, null);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Deck Passive combat-start effects failed: {ex}");
		}
	}

	private static void DeferChoiceCard(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		if (!DeferredChoiceCardsByCombat.TryGetValue(combatState, out HashSet<CardModel>? cards))
		{
			cards = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			DeferredChoiceCardsByCombat[combatState] = cards;
		}

		cards.Add(card);
	}

	internal static async Task RunDeferredChoiceCards(CombatState combatState, Player player, PlayerChoiceContext choiceContext)
	{
		if (combatState == null || player == null || choiceContext == null)
		{
			return;
		}

		if (!DeferredChoiceCardsByCombat.TryGetValue(combatState, out HashSet<CardModel>? cards) || cards.Count == 0)
		{
			return;
		}

		List<CardModel> playerCards = cards
			.Where(card => card != null && ReferenceEquals(card.Owner, player))
			.ToList();
		foreach (CardModel card in playerCards)
		{
			cards.Remove(card);
			await CardEditorExtraEffects.RunDeckPassiveCombatStart(combatState, choiceContext, card);
		}

		if (cards.Count == 0)
		{
			DeferredChoiceCardsByCombat.Remove(combatState);
		}

		await CardEditorQuestEffects.RunInstalledPassivesForCombatStart(combatState, choiceContext);
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterAttack))]
internal static class Hook_AfterAttack_CardEditorExtraEffects_OstyDealDamage_Patch
{
	public static void Postfix(CombatState combatState, AttackCommand command, ref Task __result)
	{
		if (__result == null || combatState == null || command == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, command);
	}

	private static async Task RunAfter(Task original, CombatState combatState, AttackCommand command)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			List<DamageResult> attackResults = CardEditorExtraEffects.FlattenDamageResults(command.GetResultsCompat());
			// An attack command that produced no hits (e.g. the bracketing context of a play whose
			// rows all fizzled, or an attack with no living targets) is not an attack the player
			// saw happen. Vanilla is mixed here (PainfulStabs/Suck gate on results; Osty's
			// BoneFlute fires on whiffs) — for damage-reactive triggers, gating is the sane read.
			if (attackResults.Count == 0)
			{
				return;
			}
			Creature? attackTarget = attackResults.FirstOrDefault()?.Receiver;
			using IDisposable triggerAttackResults = CardEditorEffectExecutionAmountContext.PushTriggerAttackDamageResultsScoped(attackResults);
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				await CardEditorExtraEffectTriggerPatchHelpers.RunForEachCombatPileCard(
					combatState,
					player,
					netId.Value,
					(choiceContext, card) => CardEditorExtraEffects.RunAfterAttack(combatState, choiceContext, card, attackTarget));
			}

			Player? ostyOwner = null;
			foreach (Player player in combatState.Players)
			{
				if (player?.Osty != null && ReferenceEquals(command.Attacker, player.Osty))
				{
					ostyOwner = player;
					break;
				}
			}

			if (ostyOwner == null)
			{
				return;
			}

			await CardEditorExtraEffectTriggerPatchHelpers.RunForEachCombatPileCard(
				combatState,
				ostyOwner,
				netId.Value,
				(choiceContext, card) => CardEditorExtraEffects.RunAfterOstyDealDamage(combatState, choiceContext, card));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterAttack extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardEnteredCombat))]
internal static class Hook_AfterCardEnteredCombat_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, ref Task __result)
	{
		if (__result == null || combatState == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, card);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CardModel card)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			Player? owner = card.Owner;
			if (owner == null)
			{
				return;
			}

			await CardEditorExtraEffectTriggerPatchHelpers.RunForCard(
				owner,
				netId.Value,
				card,
				(choiceContext, currentCard) => CardEditorExtraEffects.RunAfterCardEnteredCombat(combatState, choiceContext, currentCard));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterCardEnteredCombat extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnEnd))]
internal static class Hook_BeforeTurnEnd_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Player)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, participants);
	}

	// During co-op extra turns vanilla ends the turn only for a SUBSET of players (participants);
	// firing end-of-turn triggers for everyone would re-run other players' effects each extra turn.
	internal static HashSet<Player>? BuildParticipatingPlayers(IEnumerable<Creature>? participants)
	{
		if (participants == null)
		{
			return null;
		}

		HashSet<Player> players = participants
			.Where(creature => creature?.Player != null)
			.Select(creature => creature.Player!)
			.ToHashSet();
		return players.Count > 0 ? players : null;
	}

	private static async Task RunAfter(Task original, CombatState combatState, IEnumerable<Creature> participants)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		HashSet<Player>? participating = BuildParticipatingPlayers(participants);

		try
		{
			CardEditorEndOfTurnInHandTracker.Clear(combatState);

			foreach (Player player in combatState.Players)
			{
				if (participating != null && player != null && !participating.Contains(player))
				{
					continue;
				}

				CardPile? hand = player?.PlayerCombatState?.Hand;
				if (hand == null || hand.Cards.Count == 0)
				{
					continue;
				}

				// Snapshot the hand to avoid issues if effects move cards between piles.
				List<CardModel> cardsInHand = hand.Cards.ToList();
				foreach (CardModel card in cardsInHand)
				{
					if (card == null)
					{
						continue;
					}

					// VerboseLog checks the flag itself, but the interpolated string is built by the
					// caller first - so this allocated a string per card per turn end even with
					// verbose logging off. Guard before the interpolation, not inside it.
					if (CardEditorMod.IsVerboseEditorLogging)
					{
						CardEditorMod.VerboseLog($"[CardEditor][EndOfTurnInHandHook] card={card.Id} pile={card.Pile?.Type} mutable={card.IsMutable} clone={card.IsClone} cloneOf={card.CloneOf?.Id}");
					}
					CardEditorEndOfTurnInHandTracker.Mark(combatState, card);

					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunEndOfTurnInHand(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}

			// Turn-boundary triggers for cards in piles other than hand: end of (player's) turn
			foreach (Player player in combatState.Players)
			{
				if (player == null || (participating != null && !participating.Contains(player)))
				{
					continue;
				}

				CardPile? drawPile = player.PlayerCombatState?.DrawPile;
				CardPile? discardPile = player.PlayerCombatState?.DiscardPile;
				CardPile? exhaustPile = player.PlayerCombatState?.ExhaustPile;

				List<CardModel> cards = new List<CardModel>();
				if (drawPile?.Cards != null && drawPile.Cards.Count > 0)
				{
					cards.AddRange(drawPile.Cards);
				}
				if (discardPile?.Cards != null && discardPile.Cards.Count > 0)
				{
					cards.AddRange(discardPile.Cards);
				}
				if (exhaustPile?.Cards != null && exhaustPile.Cards.Count > 0)
				{
					cards.AddRange(exhaustPile.Cards);
				}

				if (cards.Count == 0)
				{
					continue;
				}

				List<CardModel> snapshot = cards.Where(c => c != null).ToList();
				foreach (CardModel card in snapshot)
				{
					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunEndOfTurn(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}

			// AutoPlaySelfFromPile / AutoDrawSelfFromPile: end of (player's) turn
			foreach (Player player in combatState.Players)
			{
				if (player == null || (participating != null && !participating.Contains(player)))
				{
					continue;
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, choiceContext, player, CardExtraEffectTrigger.EndOfTurn);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}

				HookPlayerChoiceContext drawChoiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawChoiceContext, player, CardExtraEffectTrigger.EndOfTurn);
				bool drawCompleted = await drawChoiceContext.AssignTaskAndWaitForPauseOrCompletion(drawTask);
				if (!drawCompleted && drawChoiceContext.GameAction != null)
				{
					await drawChoiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Before-turn-end extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnEnd))]
internal static class Hook_BeforeTurnEnd_CardEditorExtraEffects_EnemyTurnBoundary_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Enemy)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				CardPile? hand = player.PlayerCombatState?.Hand;
				CardPile? drawPile = player.PlayerCombatState?.DrawPile;
				CardPile? discardPile = player.PlayerCombatState?.DiscardPile;
				CardPile? exhaustPile = player.PlayerCombatState?.ExhaustPile;

				List<CardModel> cards = new List<CardModel>();
				if (hand?.Cards != null && hand.Cards.Count > 0)
				{
					cards.AddRange(hand.Cards);
				}
				if (drawPile?.Cards != null && drawPile.Cards.Count > 0)
				{
					cards.AddRange(drawPile.Cards);
				}
				if (discardPile?.Cards != null && discardPile.Cards.Count > 0)
				{
					cards.AddRange(discardPile.Cards);
				}
				if (exhaustPile?.Cards != null && exhaustPile.Cards.Count > 0)
				{
					cards.AddRange(exhaustPile.Cards);
				}

				if (cards.Count == 0)
				{
					continue;
				}

				List<CardModel> snapshot = cards.Where(c => c != null).ToList();
				foreach (CardModel card in snapshot)
				{
					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunEndOfEnemyTurn(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Before-turn-end (enemy) extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
internal static class Hook_AfterCardDrawn_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw, ref Task __result)
	{
		if (__result == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, choiceContext, card);
	}

	private static async Task RunAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		await original;
		try
		{
			await CardEditorExtraEffects.RunAfterCardDrawn(combatState, choiceContext, card);
			await CardEditorQuestEffects.RecordCardDrawn(combatState, card);
			CardEditorExtraEffects.RefreshDynamicCardCostAdjustmentsForCountEvent(combatState, card.Owner?.Creature, CardExtraEffectCountEvent.Drawn);
			ulong? netId = LocalContext.NetId;
			if (netId.HasValue && combatState != null)
			{
				foreach (Player player in combatState.Players)
				{
					if (player == null) continue;
					// No candidate cards -> the sweeps could not do anything; skip the
					// two HookPlayerChoiceContext allocations and pile sweeps entirely.
					if (!CardEditorExtraEffects.HasAnySelfPileAutoCandidates(combatState, player)) continue;
					HookPlayerChoiceContext playCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task playTask = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, playCtx, player, CardExtraEffectTrigger.OnDraw);
					bool playDone = await playCtx.AssignTaskAndWaitForPauseOrCompletion(playTask);
					if (!playDone && playCtx.GameAction != null) await playCtx.GameAction.CompletionTask;

					HookPlayerChoiceContext drawCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawCtx, player, CardExtraEffectTrigger.OnDraw);
					bool drawDone = await drawCtx.AssignTaskAndWaitForPauseOrCompletion(drawTask);
					if (!drawDone && drawCtx.GameAction != null) await drawCtx.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] On-draw extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDiscarded))]
internal static class Hook_AfterCardDiscarded_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, ref Task __result)
	{
		if (__result == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, choiceContext, card);
	}

	private static async Task RunAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		await original;
		try
		{
			await CardEditorExtraEffects.RunAfterCardDiscarded(combatState, choiceContext, card);
			await CardEditorQuestEffects.RecordCardDiscarded(combatState, card);
			CardEditorExtraEffects.RefreshDynamicCardCostAdjustmentsForCountEvent(combatState, card.Owner?.Creature, CardExtraEffectCountEvent.Discarded);
			ulong? netId = LocalContext.NetId;
			if (netId.HasValue && combatState != null)
			{
				foreach (Player player in combatState.Players)
				{
					if (player == null) continue;
					// No candidate cards -> the sweeps could not do anything; skip the
					// two HookPlayerChoiceContext allocations and pile sweeps entirely.
					if (!CardEditorExtraEffects.HasAnySelfPileAutoCandidates(combatState, player)) continue;
					HookPlayerChoiceContext playCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task playTask = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, playCtx, player, CardExtraEffectTrigger.OnDiscard);
					bool playDone = await playCtx.AssignTaskAndWaitForPauseOrCompletion(playTask);
					if (!playDone && playCtx.GameAction != null) await playCtx.GameAction.CompletionTask;

					HookPlayerChoiceContext drawCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawCtx, player, CardExtraEffectTrigger.OnDiscard);
					bool drawDone = await drawCtx.AssignTaskAndWaitForPauseOrCompletion(drawTask);
					if (!drawDone && drawCtx.GameAction != null) await drawCtx.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] On-discard extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted))]
internal static class Hook_AfterCardExhausted_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal, ref Task __result)
	{
		if (__result == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, choiceContext, card);
	}

	private static async Task RunAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardModel card)
	{
		await original;
		try
		{
			await CardEditorExtraEffects.RunAfterCardExhausted(combatState, choiceContext, card);
			await CardEditorQuestEffects.RecordCardExhausted(combatState, card);
			CardEditorExtraEffects.RefreshDynamicCardCostAdjustmentsForCountEvent(combatState, card.Owner?.Creature, CardExtraEffectCountEvent.Exhausted);
			ulong? netId = LocalContext.NetId;
			if (netId.HasValue && combatState != null)
			{
				foreach (Player player in combatState.Players)
				{
					if (player == null) continue;
					// No candidate cards -> the sweeps could not do anything; skip the
					// two HookPlayerChoiceContext allocations and pile sweeps entirely.
					if (!CardEditorExtraEffects.HasAnySelfPileAutoCandidates(combatState, player)) continue;
					HookPlayerChoiceContext playCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task playTask = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, playCtx, player, CardExtraEffectTrigger.OnExhaust);
					bool playDone = await playCtx.AssignTaskAndWaitForPauseOrCompletion(playTask);
					if (!playDone && playCtx.GameAction != null) await playCtx.GameAction.CompletionTask;

					HookPlayerChoiceContext drawCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawCtx, player, CardExtraEffectTrigger.OnExhaust);
					bool drawDone = await drawCtx.AssignTaskAndWaitForPauseOrCompletion(drawTask);
					if (!drawDone && drawCtx.GameAction != null) await drawCtx.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] On-exhaust extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class Hook_AfterCardGeneratedForCombat_CardEditorQuestEffects_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, ref Task __result)
	{
		if (__result == null || combatState == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunAfter(__result, combatState, card);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CardModel card)
	{
		await original;
		try
		{
			await CardEditorQuestEffects.RecordCardGenerated(combatState, card);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Generated-card quest progress failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterOrbEvoked))]
internal static class Hook_AfterOrbEvoked_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, OrbModel orb, ref Task __result)
	{
		if (__result == null || combatState == null || orb == null)
		{
			return;
		}

		__result = RunAfter(__result, combatState, orb);
	}

	private static async Task RunAfter(Task original, CombatState combatState, OrbModel orb)
	{
		await original;
		try
		{
			CardEditorExtraEffects.RecordOrbEvoked(combatState, orb);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Orb-evoke count tracking failed: {ex}");
		}

		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
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
			foreach (Player player in combatState.Players)
			{
				if (player == null) continue;
				foreach (PileType pileType in new[] { PileType.Draw, PileType.Discard, PileType.Hand, PileType.Exhaust })
				{
					CardPile? pile = pileType.GetPile(player);
					if (pile == null) continue;
					foreach (CardModel card in pile.Cards.ToList())
					{
						if (card == null) continue;
						HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
						Task task = CardEditorExtraEffects.RunAfterOrbEvoked(combatState, choiceContext, card);
						bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
						if (!completed && choiceContext.GameAction != null)
						{
							await choiceContext.GameAction.CompletionTask;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] On-evoke extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterOrbChanneled))]
internal static class Hook_AfterOrbChanneled_CardEditorExtraEffects_Patch
{
	public static void Postfix(CombatState combatState, Player player, OrbModel orb, ref Task __result)
	{
		if (__result == null || combatState == null || player == null || orb == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, player);
	}

	private static async Task RunAfter(Task original, CombatState combatState, Player player)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (PileType pileType in new[] { PileType.Draw, PileType.Discard, PileType.Hand, PileType.Exhaust })
			{
				CardPile? pile = pileType.GetPile(player);
				if (pile == null) continue;
				foreach (CardModel card in pile.Cards.ToList())
				{
					if (card == null) continue;
					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunAfterOrbChanneled(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] On-channel extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterFlush))]
internal static class Hook_AfterFlush_RetainedCards_CardEditorExtraEffects_Patch
{
	public static void Postfix(object? combatState, Player player, IReadOnlyCollection<CardModel> retainedCards, ref Task __result)
	{
		CombatState? concreteCombatState = combatState.GetConcreteCombatState();
		if (__result == null || concreteCombatState == null || player == null || retainedCards == null || retainedCards.Count == 0)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(concreteCombatState))
		{
			return;
		}
		__result = RunAfter(__result, concreteCombatState, player, retainedCards);
	}

	private static async Task RunAfter(Task original, CombatState combatState, Player player, IReadOnlyCollection<CardModel> retainedCards)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		foreach (CardModel card in retainedCards.Where(card => card != null).ToList())
		{
			if (CardEditorEndOfTurnInHandTracker.WasProcessed(combatState, card))
			{
				continue;
			}

			Player? owner = card.Owner ?? player;
			if (owner == null)
			{
				continue;
			}

			HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(owner, netId.Value, GameActionType.Combat);
			try
			{
				Task task = CardEditorExtraEffects.RunEndOfTurnInHand(combatState, choiceContext, card);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Retained-card end-of-turn extra effects failed: {ex}");
			}
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeHandDraw))]
internal static class Hook_BeforeHandDraw_AutoPlaySelfFromPile_Patch
{
	public static void Postfix(CombatState combatState, Player player, PlayerChoiceContext playerChoiceContext, ref Task __result)
	{
		if (__result == null || combatState == null || player == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, player, playerChoiceContext);
	}

	private static async Task RunAfter(Task original, CombatState combatState, Player player, PlayerChoiceContext choiceContext)
	{
		await original;
		if (player == null || choiceContext == null)
		{
			return;
		}

		try
		{
			CardEditorConditionalFromPileFiredTracker.Clear(combatState);
			await Hook_BeforeCombatStart_CardEditorDeckPassive_Patch.RunDeferredChoiceCards(combatState, player, choiceContext);

			CardPile? hand = player?.PlayerCombatState?.Hand;
			CardPile? drawPile = player?.PlayerCombatState?.DrawPile;
			CardPile? discardPile = player?.PlayerCombatState?.DiscardPile;
			CardPile? exhaustPile = player?.PlayerCombatState?.ExhaustPile;

			List<CardModel> cards = new List<CardModel>();
			if (hand?.Cards != null && hand.Cards.Count > 0)
			{
				cards.AddRange(hand.Cards);
			}
			if (drawPile?.Cards != null && drawPile.Cards.Count > 0)
			{
				cards.AddRange(drawPile.Cards);
			}
			if (discardPile?.Cards != null && discardPile.Cards.Count > 0)
			{
				cards.AddRange(discardPile.Cards);
			}
			if (exhaustPile?.Cards != null && exhaustPile.Cards.Count > 0)
			{
				cards.AddRange(exhaustPile.Cards);
			}

			if (cards.Count > 0)
			{
				List<CardModel> snapshot = cards.Where(c => c != null).ToList();
				foreach (CardModel card in snapshot)
				{
					await CardEditorExtraEffects.RunStartOfTurn(combatState, choiceContext, card);
					await CardEditorExtraEffects.RunBeforeHandDraw(combatState, choiceContext, card);
				}
			}

			await CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, choiceContext, player!, CardExtraEffectTrigger.StartOfTurn);
			await CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, choiceContext, player!, CardExtraEffectTrigger.StartOfTurn);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] BeforeHandDraw auto-play/draw-self failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath))]
internal static class Hook_AfterDeath_CardEditorExtraEffects_Patch
{
	public static void Postfix(IRunState runState, CombatState combatState, Creature creature, bool wasRemovalPrevented, float deathAnimLength, ref Task __result)
	{
		if (__result == null || combatState == null || creature == null)
		{
			return;
		}
		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		if (!CardEditorOverrides.HasAnyOverrides
			&& !CardEditorTemporaryExtraEffectController.HasAny(combatState)
			&& currentPlay?.Card is not CardEditorCreatedCardBase)
		{
			return;
		}
		__result = RunAfter(__result, combatState, creature);
	}

	private static async Task RunAfter(Task original, CombatState combatState, Creature creature)
	{
		try
		{
			await RunCurrentCardFinalKillFatalBeforeDeathResolution(combatState, creature);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Final-kill Fatal pre-death effects failed: {ex}");
		}

		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				await CardEditorExtraEffectTriggerPatchHelpers.RunForEachCombatPileCard(
					combatState,
					player,
					netId.Value,
					(choiceContext, card) => CardEditorExtraEffects.RunAfterDeath(combatState, choiceContext, card, creature));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterDeath extra effects failed: {ex}");
		}
	}

	private static async Task RunCurrentCardFinalKillFatalBeforeDeathResolution(CombatState combatState, Creature creature)
	{
		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		CardModel? currentCard = currentPlay?.Card;
		Player? player = currentCard?.Owner;
		Creature? ownerCreature = player?.Creature;
		if (combatState == null
			|| creature == null
			|| currentPlay == null
			|| currentCard == null
			|| player == null
			|| ownerCreature == null
			|| creature.Side == ownerCreature.Side
			|| !creature.Powers.All(power => power.ShouldOwnerDeathTriggerFatal())
			|| !IsFinalOpponentDeath(combatState, ownerCreature, creature))
		{
			return;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		await CardEditorExtraEffectTriggerPatchHelpers.RunForCard(
			player,
			netId.Value,
			currentCard,
			(choiceContext, _) => CardEditorExtraEffects.RunFatalForCardPlayNow(combatState, choiceContext, currentPlay));
	}

	private static bool IsFinalOpponentDeath(CombatState combatState, Creature ownerCreature, Creature deadCreature)
	{
		if (combatState == null || ownerCreature == null || deadCreature == null)
		{
			return false;
		}

		foreach (Creature opponent in combatState.GetOpponentsOf(ownerCreature))
		{
			if (opponent == null || ReferenceEquals(opponent, deadCreature))
			{
				continue;
			}

			if (opponent.IsAlive)
			{
				return false;
			}
		}

		return true;
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class Hook_AfterCombatEnd_CardEditorExtraEffects_Patch
{
	[HarmonyPriority(Priority.First)]
	public static void Postfix(IRunState runState, CombatState combatState, CombatRoom room, ref Task __result)
	{
		if (__result == null || combatState == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				await CardEditorExtraEffectTriggerPatchHelpers.RunForEachDeckAndCombatPileCard(
					player,
					netId.Value,
					(choiceContext, card) => CardEditorExtraEffects.RunAfterCombatEnd(combatState, choiceContext, card));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterCombatEnd extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class Hook_AfterSideTurnStart_AutoPlaySelfFromPile_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Enemy)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				CardPile? hand = player.PlayerCombatState?.Hand;
				CardPile? drawPile = player.PlayerCombatState?.DrawPile;
				CardPile? discardPile = player.PlayerCombatState?.DiscardPile;
				CardPile? exhaustPile = player.PlayerCombatState?.ExhaustPile;

				List<CardModel> cards = new List<CardModel>();
				if (hand?.Cards != null && hand.Cards.Count > 0)
				{
					cards.AddRange(hand.Cards);
				}
				if (drawPile?.Cards != null && drawPile.Cards.Count > 0)
				{
					cards.AddRange(drawPile.Cards);
				}
				if (discardPile?.Cards != null && discardPile.Cards.Count > 0)
				{
					cards.AddRange(discardPile.Cards);
				}
				if (exhaustPile?.Cards != null && exhaustPile.Cards.Count > 0)
				{
					cards.AddRange(exhaustPile.Cards);
				}

				if (cards.Count > 0)
				{
					List<CardModel> snapshot = cards.Where(c => c != null).ToList();
					foreach (CardModel card in snapshot)
					{
						HookPlayerChoiceContext cardChoiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
						Task triggerTask = CardEditorExtraEffects.RunStartOfEnemyTurn(combatState, cardChoiceContext, card);
						bool triggerCompleted = await cardChoiceContext.AssignTaskAndWaitForPauseOrCompletion(triggerTask);
						if (!triggerCompleted && cardChoiceContext.GameAction != null)
						{
							await cardChoiceContext.GameAction.CompletionTask;
						}
					}
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, choiceContext, player, CardExtraEffectTrigger.StartOfEnemyTurn);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}

				HookPlayerChoiceContext drawChoiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawChoiceContext, player, CardExtraEffectTrigger.StartOfEnemyTurn);
				bool drawCompleted = await drawChoiceContext.AssignTaskAndWaitForPauseOrCompletion(drawTask);
				if (!drawCompleted && drawChoiceContext.GameAction != null)
				{
					await drawChoiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterSideTurnStart (enemy) auto-play/draw-self failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class Hook_AfterCardPlayed_AutoPlayDrawSelfFromPile_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (__result == null || combatState == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState, cardPlay);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CardPlay cardPlay)
	{
		await original;
		await CardEditorQuestEffects.RecordCardPlayed(combatState, cardPlay);

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				HookPlayerChoiceContext playChoiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task playTask = CardEditorExtraEffects.RunAutoPlaySelfFromPile(combatState, playChoiceContext, player, CardExtraEffectTrigger.OnPlay);
				bool playCompleted = await playChoiceContext.AssignTaskAndWaitForPauseOrCompletion(playTask);
				if (!playCompleted && playChoiceContext.GameAction != null)
				{
					await playChoiceContext.GameAction.CompletionTask;
				}

				HookPlayerChoiceContext drawChoiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task drawTask = CardEditorExtraEffects.RunAutoDrawSelfFromPile(combatState, drawChoiceContext, player, CardExtraEffectTrigger.OnPlay);
				bool drawCompleted = await drawChoiceContext.AssignTaskAndWaitForPauseOrCompletion(drawTask);
				if (!drawCompleted && drawChoiceContext.GameAction != null)
				{
					await drawChoiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterCardPlayed auto-play/draw-self failed: {ex}");
		}
	}
}

internal static class CardEditorTurnBoundaryTriggerRunner
{
	public static IEnumerable<CardModel> SnapshotAllCards(Player player)
	{
		if (player == null)
		{
			yield break;
		}

		CardPile? hand = player.PlayerCombatState?.Hand;
		CardPile? drawPile = player.PlayerCombatState?.DrawPile;
		CardPile? discardPile = player.PlayerCombatState?.DiscardPile;
		CardPile? exhaustPile = player.PlayerCombatState?.ExhaustPile;

		foreach (CardPile? pile in new[] { hand, drawPile, discardPile, exhaustPile })
		{
			if (pile?.Cards == null || pile.Cards.Count == 0)
			{
				continue;
			}

			foreach (CardModel card in pile.Cards.Where(c => c != null).ToList())
			{
				yield return card;
			}
		}
	}

	public static async Task RunForPlayer(CombatState combatState, Player player, PlayerChoiceContext choiceContext, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
	{
		foreach (CardModel card in SnapshotAllCards(player))
		{
			await CardEditorExtraEffects.RunTurnBoundary(combatState, choiceContext, card, boundary, side);
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
internal static class Hook_AfterPlayerTurnStart_CardEditorTurnBoundaryAfterDraw_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__result == null || combatState == null || player == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunAfter(__result, combatState, choiceContext, player);
	}

	private static async Task RunAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, Player player)
	{
		await original;
		try
		{
			await CardEditorTurnBoundaryTriggerRunner.RunForPlayer(combatState, player, choiceContext, CardExtraEffectTurnBoundary.StartAfterDraw, CardExtraEffectTurnBoundarySide.YourTurn);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary after-draw extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
internal static class Hook_BeforeSideTurnStart_CardEditorTurnBoundaryEnemyBeforeStart_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Enemy)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorTurnBoundaryTriggerRunner.RunForPlayer(combatState, player, choiceContext, CardExtraEffectTurnBoundary.Start, CardExtraEffectTurnBoundarySide.EnemyTurn);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary enemy-before-start extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
internal static class Hook_AfterSideTurnStart_CardEditorTurnBoundaryEnemyAfterStart_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side != CombatSide.Enemy)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorTurnBoundaryTriggerRunner.RunForPlayer(combatState, player, choiceContext, CardExtraEffectTurnBoundary.StartAfterDraw, CardExtraEffectTurnBoundarySide.EnemyTurn);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary enemy-after-start extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnEnd))]
internal static class Hook_AfterTurnEnd_CardEditorTurnBoundaryAfterDiscard_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || combatState == null || side == CombatSide.None)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunAfter(__result, combatState, side);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CombatSide side)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		CardExtraEffectTurnBoundarySide boundarySide = side == CombatSide.Enemy
			? CardExtraEffectTurnBoundarySide.EnemyTurn
			: CardExtraEffectTurnBoundarySide.YourTurn;

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null)
				{
					continue;
				}

				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorTurnBoundaryTriggerRunner.RunForPlayer(combatState, player, choiceContext, CardExtraEffectTurnBoundary.EndAfterDiscard, boundarySide);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary after-discard extra effects failed: {ex}");
		}
	}
}

internal static class CardEditorEndOfTurnInHandTracker
{
	private static readonly Dictionary<CombatState, HashSet<CardModel>> _processedByCombat = new Dictionary<CombatState, HashSet<CardModel>>();

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		_processedByCombat[combatState] = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
	}

	public static void Mark(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!_processedByCombat.TryGetValue(combatState, out HashSet<CardModel>? set))
		{
			set = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			_processedByCombat[combatState] = set;
		}
		set.Add(card);
	}

	public static bool WasProcessed(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return false;
		}
		return _processedByCombat.TryGetValue(combatState, out HashSet<CardModel>? set) && set.Contains(card);
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}

internal static class CardEditorConditionalFromPileFiredTracker
{
	private static readonly Dictionary<CombatState, HashSet<CardModel>> _firedByCombat = new Dictionary<CombatState, HashSet<CardModel>>();

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		_firedByCombat[combatState] = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
	}

	public static bool HasFired(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return false;
		}
		return _firedByCombat.TryGetValue(combatState, out HashSet<CardModel>? set) && set.Contains(card);
	}

	public static void MarkFired(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!_firedByCombat.TryGetValue(combatState, out HashSet<CardModel>? set))
		{
			set = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
			_firedByCombat[combatState] = set;
		}
		set.Add(card);
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class Hook_AfterCombatVictory_CardEditorExtraEffects_Patch
{
	public static void Postfix(IRunState runState, CombatState? combatState, CombatRoom room, ref Task __result)
	{
		if (__result == null || combatState == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		try
		{
			foreach (Player player in combatState.Players)
			{
				if (player == null) continue;

				if (player.Deck?.Cards != null && player.Deck.Cards.Count > 0)
				{
					List<CardModel> deckSnapshot = player.Deck.Cards.Where(card => card != null).ToList();
					foreach (CardModel card in deckSnapshot)
					{
						HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
						Task task = CardEditorExtraEffects.RunDeckPassiveCombatEnd(combatState, choiceContext, card);
						bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
						if (!completed && choiceContext.GameAction != null)
						{
							await choiceContext.GameAction.CompletionTask;
						}
					}
				}

				HashSet<CardModel> seen = new HashSet<CardModel>(ReferenceEqualityComparer<CardModel>.Instance);
				List<CardModel> cards = new List<CardModel>();
				foreach (PileType pileType in new[] { PileType.Draw, PileType.Discard, PileType.Hand, PileType.Exhaust })
				{
					CardPile? pile = pileType.GetPile(player);
					if (pile == null) continue;
					foreach (CardModel card in pile.Cards)
					{
						if (card != null && seen.Add(card)) cards.Add(card);
					}
				}

				foreach (CardModel card in cards)
				{
					HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
					Task task = CardEditorExtraEffects.RunAfterCombat(combatState, choiceContext, card);
					bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
					if (!completed && choiceContext.GameAction != null)
					{
						await choiceContext.GameAction.CompletionTask;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] After-combat-victory effects failed: {ex}");
		}
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();
		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
		public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
	}
}
