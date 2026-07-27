using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporarySelfScalingController
{
	private sealed class SelfScalingGrant
	{
		public required CardEditorExtraEffects.SelfScalingMutationDiff Diff { get; init; }
		public required CardExtraEffectCardCostsLessDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class CardState
	{
		public List<SelfScalingGrant> Grants { get; } = new List<SelfScalingGrant>();
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, CardState> States { get; } = new Dictionary<CardModel, CardState>();
		public Dictionary<CardModel, List<CardEditorExtraEffects.SelfScalingMutationDiff>> DeferredPermanentRuntimeMutations { get; } =
			new Dictionary<CardModel, List<CardEditorExtraEffects.SelfScalingMutationDiff>>(ReferenceEqualityComparer<CardModel>.Instance);
	}

	private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

		public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

		public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules = new ConditionalWeakTable<CombatState, CombatSchedule>();

	private static CardModel ResolveScheduleKey(CardModel card)
	{
		return CardEditorExtraEffects.ResolveSelfScalingMutationTargetCard(card) ?? card;
	}

	public static void Grant(
		CombatState combatState,
		CardModel card,
		CardEditorExtraEffects.SelfScalingMutationDiff diff,
		CardExtraEffectCardCostsLessDuration duration,
		int turns)
	{
		if (combatState == null || card == null || !card.IsMutable || diff == null || diff.IsEmpty)
		{
			return;
		}
		if (duration is CardExtraEffectCardCostsLessDuration.Permanent or CardExtraEffectCardCostsLessDuration.ThisCombat)
		{
			return;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		CardModel key = ResolveScheduleKey(card);
		if (!schedule.States.TryGetValue(key, out CardState? state))
		{
			state = new CardState();
			schedule.States[key] = state;
		}

		state.Grants.Add(new SelfScalingGrant
		{
			Diff = diff,
			Duration = duration,
			RemainingTurns = duration == CardExtraEffectCardCostsLessDuration.Turns
				? Math.Clamp(turns, 1, 99)
				: duration == CardExtraEffectCardCostsLessDuration.ThisTurn ? 1 : 0
		});
	}

	public static void DeferPermanentRuntimeMutation(
		CombatState combatState,
		CardModel card,
		CardEditorExtraEffects.SelfScalingMutationDiff diff)
	{
		if (combatState == null || card == null || !card.IsMutable || diff == null || diff.IsEmpty)
		{
			return;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.DeferredPermanentRuntimeMutations.TryGetValue(card, out List<CardEditorExtraEffects.SelfScalingMutationDiff>? diffs))
		{
			diffs = new List<CardEditorExtraEffects.SelfScalingMutationDiff>();
			schedule.DeferredPermanentRuntimeMutations[card] = diffs;
		}

		diffs.Add(diff);
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
		ApplyDeferredPermanentRuntimeMutations(schedule, card);
		if (!ReferenceEquals(key, card))
		{
			ApplyDeferredPermanentRuntimeMutations(schedule, key);
		}

		if (!schedule.States.TryGetValue(key, out CardState? state) || state.Grants.Count == 0)
		{
			CleanupEmptySchedule(combatState, schedule);
			return;
		}

		bool changed = false;
		for (int i = state.Grants.Count - 1; i >= 0; i--)
		{
			SelfScalingGrant grant = state.Grants[i];
			if (grant == null)
			{
				state.Grants.RemoveAt(i);
				changed = true;
				continue;
			}
			if (grant.Duration == CardExtraEffectCardCostsLessDuration.UntilPlayed)
			{
				CardEditorExtraEffects.ApplyTemporarySelfScalingDiff(key, grant.Diff, direction: -1);
				state.Grants.RemoveAt(i);
				changed = true;
			}
		}

		FinalizeStateChange(combatState, schedule, key, state, changed);
	}

	private static void ApplyDeferredPermanentRuntimeMutations(CombatSchedule schedule, CardModel card)
	{
		if (schedule == null
			|| card == null
			|| !schedule.DeferredPermanentRuntimeMutations.TryGetValue(card, out List<CardEditorExtraEffects.SelfScalingMutationDiff>? diffs)
			|| diffs.Count == 0)
		{
			return;
		}

		schedule.DeferredPermanentRuntimeMutations.Remove(card);
		foreach (CardEditorExtraEffects.SelfScalingMutationDiff diff in diffs)
		{
			if (diff == null || diff.IsEmpty)
			{
				continue;
			}

			try
			{
				CardEditorExtraEffects.ApplyPersistentSelfScalingRuntimeDiff(card, diff);
			}
			catch
			{
				// ignored
			}
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
				SelfScalingGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					changed = true;
					continue;
				}

				if (grant.Duration is CardExtraEffectCardCostsLessDuration.ThisTurn or CardExtraEffectCardCostsLessDuration.Turns)
				{
					grant.RemainingTurns--;
					if (grant.RemainingTurns <= 0)
					{
						CardEditorExtraEffects.ApplyTemporarySelfScalingDiff(card, grant.Diff, direction: -1);
						state.Grants.RemoveAt(i);
						changed = true;
					}
				}
			}

			if (changed && state.Grants.Count == 0)
			{
				schedule.States.Remove(card);
			}
		}

		CleanupEmptySchedule(combatState, schedule);
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

			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				SelfScalingGrant grant = state.Grants[i];
				if (grant == null)
				{
					continue;
				}
				CardEditorExtraEffects.ApplyTemporarySelfScalingDiff(card, grant.Diff, direction: -1);
			}
		}

		_schedules.Remove(combatState);
	}

	private static void FinalizeStateChange(CombatState combatState, CombatSchedule schedule, CardModel card, CardState state, bool changed)
	{
		if (!changed)
		{
			return;
		}
		if (state.Grants.Count == 0)
		{
			schedule.States.Remove(card);
		}
		CleanupEmptySchedule(combatState, schedule);
	}

	private static void CleanupEmptySchedule(CombatState combatState, CombatSchedule schedule)
	{
		foreach ((CardModel card, CardState state) in schedule.States.ToList())
		{
			if (card == null || state == null || state.Grants.Count == 0)
			{
				schedule.States.Remove(card!);
			}
		}
		if (schedule.States.Count == 0 && schedule.DeferredPermanentRuntimeMutations.Count == 0)
		{
			_schedules.Remove(combatState);
		}
	}
}
