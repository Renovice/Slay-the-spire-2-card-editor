using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryReplayController
{
	private sealed class ReplayGrant
	{
		public required int Delta { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class CardState
	{
		public int? Baseline { get; set; }
		public List<ReplayGrant> Grants { get; } = new List<ReplayGrant>();
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, CardState> States { get; } = new Dictionary<CardModel, CardState>();
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules = new ConditionalWeakTable<CombatState, CombatSchedule>();

	private static CardModel ResolveScheduleKey(CardModel card)
	{
		if (card == null)
		{
			return null!;
		}

		try
		{
			CardModel? cursor = card;
			for (int i = 0; i < 8 && cursor != null; i++)
			{
				if (!cursor.IsClone || cursor.CloneOf == null)
				{
					return cursor;
				}

				if (!cursor.CloneOf.IsMutable)
				{
					return cursor;
				}

				cursor = cursor.CloneOf;
			}
		}
		catch
		{
			// ignored
		}

		return card;
	}

	public static void Apply(CombatState combatState, CardModel card, int delta, CardExtraEffectCardGrantDuration duration, int turns)
	{
		if (combatState == null || card == null || !card.IsMutable)
		{
			return;
		}
		if (delta == 0)
		{
			return;
		}

		int remainingTurns = 0;
		if (duration == CardExtraEffectCardGrantDuration.ThisTurn)
		{
			remainingTurns = 1;
		}
		else if (duration == CardExtraEffectCardGrantDuration.Turns)
		{
			remainingTurns = Math.Clamp(turns, 1, 99);
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		CardModel key = ResolveScheduleKey(card);
		if (!schedule.States.TryGetValue(key, out CardState? state))
		{
			state = new CardState
			{
				Baseline = key.BaseReplayCount
			};
			schedule.States[key] = state;
		}

		state.Grants.Add(new ReplayGrant
		{
			Delta = delta,
			Duration = duration,
			RemainingTurns = remainingTurns
		});

		Reapply(key, state);
	}

	public static void OnAfterCardPlayed(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return;
		}

		CardModel key = ResolveScheduleKey(card);
		if (!schedule.States.TryGetValue(key, out CardState? state) || state.Grants.Count == 0)
		{
			return;
		}

		bool changed = false;
		for (int i = state.Grants.Count - 1; i >= 0; i--)
		{
			ReplayGrant grant = state.Grants[i];
			if (grant == null)
			{
				state.Grants.RemoveAt(i);
				changed = true;
				continue;
			}
			if (grant.Duration == CardExtraEffectCardGrantDuration.UntilPlayed)
			{
				state.Grants.RemoveAt(i);
				changed = true;
			}
		}

		if (changed)
		{
			FinalizeStateChange(schedule, key, state);
		}
	}

	public static void OnAfterPlayerTurnEnd(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return;
		}

		foreach ((CardModel card, CardState state) in schedule.States.ToList())
		{
			if (card == null || state == null || state.Grants.Count == 0)
			{
				schedule.States.Remove(card!);
				continue;
			}
			if (!card.IsMutable || card.HasBeenRemovedFromState)
			{
				schedule.States.Remove(card);
				continue;
			}

			bool changed = false;
			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				ReplayGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					changed = true;
					continue;
				}

				if (grant.Duration is CardExtraEffectCardGrantDuration.ThisTurn or CardExtraEffectCardGrantDuration.Turns)
				{
					grant.RemainingTurns--;
					if (grant.RemainingTurns <= 0)
					{
						state.Grants.RemoveAt(i);
						changed = true;
					}
				}
			}

			if (changed)
			{
				FinalizeStateChange(schedule, card, state);
			}
		}

		if (schedule.States.Count == 0)
		{
			_schedules.Remove(combatState);
		}
	}

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return;
		}

		foreach ((CardModel card, CardState state) in schedule.States)
		{
			if (card == null || state == null || !card.IsMutable)
			{
				continue;
			}

			Reapply(card, state, forceClearGrants: true);
		}

		_schedules.Remove(combatState);
	}

	private static void FinalizeStateChange(CombatSchedule schedule, CardModel card, CardState state)
	{
		if (state.Grants.Count == 0)
		{
			Reapply(card, state, forceClearGrants: true);
			schedule.States.Remove(card);
			return;
		}

		Reapply(card, state);
	}

	private static void Reapply(CardModel card, CardState state, bool forceClearGrants = false)
	{
		if (card == null || state == null || !card.IsMutable)
		{
			return;
		}

		int baseline = state.Baseline ?? card.BaseReplayCount;
		if (state.Baseline == null)
		{
			state.Baseline = baseline;
		}

		if (forceClearGrants)
		{
			state.Grants.Clear();
			card.BaseReplayCount = Math.Max(0, baseline);
			return;
		}

		long total = baseline;
		foreach (ReplayGrant grant in state.Grants)
		{
			if (grant == null || grant.Delta == 0)
			{
				continue;
			}
			total += grant.Delta;
		}

		card.BaseReplayCount = total <= 0 ? 0 : total >= int.MaxValue ? int.MaxValue : (int)total;
	}
}
