using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorExtraEffectScheduler
{
	private sealed class ScheduledEffect
	{
		public required CardModel Card { get; init; }
		public required Player Owner { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public required ResourceInfo Resources { get; init; }
		public required PileType ResultPile { get; init; }

		public Creature? LockedTarget { get; init; }
		public Creature? ExecutionHost { get; init; }
		public CardModel? SourceCardInstance { get; init; }
		public object? UseLimitSourceInstance { get; init; }
		public Dictionary<string, List<CardModel>> SelectedCardsByEffectId { get; init; } = new(StringComparer.Ordinal);
		public int TriggerEventAmount { get; init; } = 1;
		public int RemainingTriggers { get; set; }
		public int SkipTriggers { get; set; }
		public int TriggerCounter { get; set; }
	}

	private sealed class CombatSchedule
	{
		public List<ScheduledEffect> Effects { get; } = new List<ScheduledEffect>();
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules = new ConditionalWeakTable<CombatState, CombatSchedule>();

	public static void Schedule(
		CombatState combatState,
		CardPlay sourcePlay,
		CardExtraEffect effect,
		Creature? lockedTarget,
		Creature? executionHost = null,
		int triggerEventAmount = 1,
		object? useLimitSourceInstance = null)
	{
		if (combatState == null || sourcePlay == null || effect == null)
		{
			return;
		}

		CardExtraEffect scheduledEffect = CardEditorExtraEffects.CloneForDeferredExecution(sourcePlay, effect);
		if (scheduledEffect.Timing == CardExtraEffectTiming.Immediate
			|| scheduledEffect.Turns < 0
			|| !CardEditorExtraEffects.IsValidEffectAmount(scheduledEffect.Kind, scheduledEffect.Amount)
			|| scheduledEffect.RepeatCount < 0)
		{
			return;
		}

		Player? owner = sourcePlay.Card.Owner;
		if (owner == null)
		{
			return;
		}

		int remaining = scheduledEffect.Turns == 0 ? -1 : Math.Max(1, scheduledEffect.Turns);
		int skip = 0;
		if (scheduledEffect.Timing == CardExtraEffectTiming.EndOfAnyTurn)
		{
			skip = 1;
		}
		else if (scheduledEffect.Timing == CardExtraEffectTiming.EndOfTurn && combatState.CurrentSide == CombatSide.Player)
		{
			skip = 1;
		}
		else if (scheduledEffect.Timing == CardExtraEffectTiming.EndOfEnemyTurn && combatState.CurrentSide == CombatSide.Enemy)
		{
			skip = 1;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		schedule.Effects.Add(new ScheduledEffect
		{
			Card = CreateScheduledCardSnapshot(owner, sourcePlay.Card)!, // sourcePlay.Card is non-null; snapshot falls back to sourceCard
			Owner = owner,
			Effect = scheduledEffect,
			LockedTarget = scheduledEffect.Target == CardExtraEffectTarget.Target ? lockedTarget : null,
			ExecutionHost = executionHost,
			SourceCardInstance = sourcePlay.Card,
			UseLimitSourceInstance = useLimitSourceInstance ?? sourcePlay.Card,
			SelectedCardsByEffectId = CardEditorEffectExecutionAmountContext.CaptureCurrentSelectedCards(cloneCards: true),
			TriggerEventAmount = Math.Max(0, triggerEventAmount),
			RemainingTriggers = remaining,
			SkipTriggers = skip,
			Resources = sourcePlay.Resources,
			ResultPile = sourcePlay.ResultPile
		});
	}

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		_schedules.Remove(combatState);
	}

	public static async Task RunAfterPlayerTurnStart(CombatState combatState, PlayerChoiceContext choiceContext, Player player)
	{
		if (combatState == null || choiceContext == null || player == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.Effects.Count == 0)
		{
			return;
		}

		for (int i = schedule.Effects.Count - 1; i >= 0; i--)
		{
			ScheduledEffect scheduled = schedule.Effects[i];
			if (scheduled == null)
			{
				schedule.Effects.RemoveAt(i);
				continue;
			}
			if (scheduled.Effect.Timing != CardExtraEffectTiming.StartOfTurn
				&& scheduled.Effect.Timing != CardExtraEffectTiming.StartOfAnyTurn)
			{
				continue;
			}
			if (scheduled.Owner != player)
			{
				continue;
			}
			if (scheduled.SkipTriggers > 0)
			{
				scheduled.SkipTriggers--;
				continue;
			}
			if (!ShouldFireOnThisTrigger(scheduled))
			{
				continue;
			}

			await ExecuteScheduledEffect(combatState, choiceContext, scheduled);
			if (scheduled.RemainingTriggers > 0)
			{
				scheduled.RemainingTriggers--;
				if (scheduled.RemainingTriggers <= 0)
				{
					schedule.Effects.RemoveAt(i);
				}
			}
		}
	}

	public static async Task RunAfterEnemyTurnStart(CombatState combatState, PlayerChoiceContext choiceContext, Player player)
	{
		if (combatState == null || choiceContext == null || player == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.Effects.Count == 0)
		{
			return;
		}

		for (int i = schedule.Effects.Count - 1; i >= 0; i--)
		{
			ScheduledEffect scheduled = schedule.Effects[i];
			if (scheduled == null)
			{
				schedule.Effects.RemoveAt(i);
				continue;
			}
			if (scheduled.Effect.Timing != CardExtraEffectTiming.StartOfEnemyTurn
				&& scheduled.Effect.Timing != CardExtraEffectTiming.StartOfAnyTurn)
			{
				continue;
			}
			if (scheduled.Owner != player)
			{
				continue;
			}
			if (scheduled.SkipTriggers > 0)
			{
				scheduled.SkipTriggers--;
				continue;
			}
			if (!ShouldFireOnThisTrigger(scheduled))
			{
				continue;
			}

			await ExecuteScheduledEffect(combatState, choiceContext, scheduled);
			if (scheduled.RemainingTriggers > 0)
			{
				scheduled.RemainingTriggers--;
				if (scheduled.RemainingTriggers <= 0)
				{
					schedule.Effects.RemoveAt(i);
				}
			}
		}
	}

	public static async Task RunAfterTurnEnd(CombatState combatState, CombatSide side)
	{
		if (combatState == null || side == CombatSide.None)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.Effects.Count == 0)
		{
			return;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		foreach (Player player in combatState.Players)
		{
			HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
			Task task = RunAfterTurnEndForPlayer(combatState, choiceContext, player, side);
			bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
			if (!completed && choiceContext.GameAction != null)
			{
				await choiceContext.GameAction.CompletionTask;
			}
		}
	}

	private static async Task RunAfterTurnEndForPlayer(CombatState combatState, PlayerChoiceContext choiceContext, Player player, CombatSide side)
	{
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.Effects.Count == 0)
		{
			return;
		}

		bool allowPlayerEnd =
			side == CombatSide.Player;
		bool allowEnemyEnd =
			side == CombatSide.Enemy;

		for (int i = schedule.Effects.Count - 1; i >= 0; i--)
		{
			ScheduledEffect scheduled = schedule.Effects[i];
			if (scheduled == null)
			{
				schedule.Effects.RemoveAt(i);
				continue;
			}
			bool shouldRun = scheduled.Effect.Timing switch
			{
				CardExtraEffectTiming.EndOfTurn => allowPlayerEnd,
				CardExtraEffectTiming.EndOfThisTurn => allowPlayerEnd,
				CardExtraEffectTiming.EndOfEnemyTurn => allowEnemyEnd,
				CardExtraEffectTiming.EndOfAnyTurn => allowPlayerEnd || allowEnemyEnd,
				CardExtraEffectTiming.EndOfThisAnyTurn => allowPlayerEnd || allowEnemyEnd,
				_ => false
			};
			if (!shouldRun)
			{
				continue;
			}
			if (scheduled.Owner != player)
			{
				continue;
			}
			if (scheduled.SkipTriggers > 0)
			{
				scheduled.SkipTriggers--;
				continue;
			}
			if (!ShouldFireOnThisTrigger(scheduled))
			{
				continue;
			}

			await ExecuteScheduledEffect(combatState, choiceContext, scheduled);
			if (scheduled.RemainingTriggers > 0)
			{
				scheduled.RemainingTriggers--;
				if (scheduled.RemainingTriggers <= 0)
				{
					schedule.Effects.RemoveAt(i);
				}
			}
		}
	}

	private static async Task ExecuteScheduledEffect(CombatState combatState, PlayerChoiceContext choiceContext, ScheduledEffect scheduled)
	{
		if (scheduled.Card == null)
		{
			return;
		}

		CardPlay play = new CardPlay
		{
			Card = scheduled.Card,
			Target = scheduled.LockedTarget,
			ResultPile = scheduled.ResultPile,
			Resources = scheduled.Resources,
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};

		try
		{
			using IDisposable _ = CardEditorCardPlayContext.PushScoped(play);
			using IDisposable __ = CardEditorEffectSourceContext.PushScoped(scheduled.SourceCardInstance ?? scheduled.Card);
			using IDisposable ___ = CardEditorPowerExecutionHostContext.PushScoped(scheduled.ExecutionHost);
			using IDisposable ____ = CardEditorAutoPlayLoopGuard.PushUseLimitSourceInstance(scheduled.UseLimitSourceInstance ?? scheduled.Card);
			using IDisposable _____ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
			using IDisposable ______ = CardEditorEffectExecutionAmountContext.PushSelectedCardsScoped(scheduled.SelectedCardsByEffectId);
			await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, play, scheduled.Effect, scheduled.TriggerEventAmount);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Scheduled extra effect failed: {ex}");
		}
	}

	private static CardModel? CreateScheduledCardSnapshot(Player owner, CardModel sourceCard)
	{
		if (owner == null || sourceCard == null)
		{
			return sourceCard;
		}

		try
		{
			CardModel snapshot = owner.RunState.CloneCard(sourceCard);
			if (snapshot == null)
			{
				return sourceCard;
			}

			try
			{
				snapshot.Owner = sourceCard.Owner;
			}
			catch
			{
			}

			if (CardEditorOverrides.TryGetEffectiveOverride(sourceCard, out CardOverride effectiveOverride))
			{
				CardEditorOverrides.SetInstanceOverride(snapshot, effectiveOverride);
			}

			return snapshot;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed cloning scheduled effect card snapshot for {sourceCard.Id}: {ex}");
			return sourceCard;
		}
	}

	private static bool ShouldFireOnThisTrigger(ScheduledEffect scheduled)
	{
		if (scheduled?.Effect == null)
		{
			return false;
		}

		if (scheduled.Effect.TriggerEveryN < 2)
		{
			return true;
		}

		scheduled.TriggerCounter++;
		return scheduled.TriggerCounter % scheduled.Effect.TriggerEveryN == 0;
	}
}
