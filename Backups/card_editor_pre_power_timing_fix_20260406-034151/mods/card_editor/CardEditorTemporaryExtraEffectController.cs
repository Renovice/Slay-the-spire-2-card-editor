using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryExtraEffectController
{
	private sealed class ExtraEffectGrant
	{
		public required CardExtraEffect Effect { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class CardState
	{
		public List<ExtraEffectGrant> Grants { get; } = new List<ExtraEffectGrant>();
		public List<CardExtraEffect> Effects { get; } = new List<CardExtraEffect>();
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
				cursor = cursor.CloneOf;
			}
		}
		catch
		{
			// ignored
		}

		return card;
	}

	public static bool HasAny(CombatState combatState)
	{
		if (combatState == null)
		{
			return false;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return false;
		}
		return schedule.States.Count > 0;
	}

	public static IReadOnlyList<CardExtraEffect> GetEffects(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return Array.Empty<CardExtraEffect>();
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return Array.Empty<CardExtraEffect>();
		}

		if (!schedule.States.TryGetValue(card, out CardState? state) || state.Effects.Count == 0)
		{
			CardModel key = ResolveScheduleKey(card);
			if (ReferenceEquals(key, card) || !schedule.States.TryGetValue(key, out state) || state.Effects.Count == 0)
			{
				return Array.Empty<CardExtraEffect>();
			}
		}
		return state.Effects;
	}

	internal static bool TryStackTimedCardCostsLess(CombatState combatState, CardModel card, CardExtraEffect effect, CardExtraEffectCardGrantDuration duration, int turns)
	{
		if (combatState == null || card == null || effect == null)
		{
			return false;
		}
		if (effect.Kind is not (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess))
		{
			return false;
		}
		if (!CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return false;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return false;
		}
		CardModel key = ResolveScheduleKey(card);
		if (!schedule.States.TryGetValue(key, out CardState? state) || state.Grants.Count == 0)
		{
			return false;
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

		foreach (ExtraEffectGrant grant in state.Grants)
		{
			if (grant?.Effect == null)
			{
				continue;
			}
			if (!IsEquivalentTimedCardCostsLessIgnoringAmount(grant.Effect, effect))
			{
				continue;
			}
			if (grant.Duration != duration)
			{
				continue;
			}

			grant.Effect.Amount += effect.Amount;
			if (remainingTurns > 0)
			{
				grant.RemainingTurns = Math.Max(grant.RemainingTurns, remainingTurns);
			}
			return true;
		}

		return false;
	}

	private static bool IsEquivalentTimedCardCostsLessIgnoringAmount(CardExtraEffect existing, CardExtraEffect candidate)
	{
		if (existing == null || candidate == null)
		{
			return false;
		}
		if (existing.Kind != candidate.Kind)
		{
			return false;
		}
		if (existing.Kind is not (CardExtraEffectKind.CardCostsLess or CardExtraEffectKind.CardStarCostsLess))
		{
			return false;
		}

		return existing.ScaleMode == candidate.ScaleMode
			&& existing.CountEvent == candidate.CountEvent
			&& existing.CountWindow == candidate.CountWindow
			&& existing.CountTurns == candidate.CountTurns
			&& existing.CountCardPool == candidate.CountCardPool
			&& existing.CountCardType == candidate.CountCardType
			&& existing.CountCardFilter == candidate.CountCardFilter
			&& existing.CountOnlyBlockCards == candidate.CountOnlyBlockCards
			&& existing.CardCostsLessDuration == candidate.CardCostsLessDuration
			&& existing.CardCostsLessTurns == candidate.CardCostsLessTurns
			&& existing.CardCostsLessMode == candidate.CardCostsLessMode;
	}

	public static void Grant(CombatState combatState, CardModel card, CardExtraEffect effect, CardExtraEffectCardGrantDuration duration, int turns)
	{
		if (combatState == null || card == null || effect == null)
		{
			return;
		}
		if (!CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
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
			state = new CardState();
			schedule.States[key] = state;
		}

		CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(effect);
		stored.GrantToCard = false;

		state.Grants.Add(new ExtraEffectGrant
		{
			Effect = stored,
			Duration = duration,
			RemainingTurns = remainingTurns
		});
		state.Effects.Add(stored);
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

		for (int i = state.Grants.Count - 1; i >= 0; i--)
		{
			ExtraEffectGrant grant = state.Grants[i];
			if (grant == null)
			{
				state.Grants.RemoveAt(i);
				state.Effects.RemoveAt(i);
				continue;
			}
			if (grant.Duration == CardExtraEffectCardGrantDuration.UntilPlayed)
			{
				state.Grants.RemoveAt(i);
				state.Effects.RemoveAt(i);
			}
		}

		if (state.Grants.Count == 0)
		{
			schedule.States.Remove(key);
			if (schedule.States.Count == 0)
			{
				_schedules.Remove(combatState);
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
				schedule.States.Remove(card);
				continue;
			}
			if (card.HasBeenRemovedFromState)
			{
				schedule.States.Remove(card);
				continue;
			}

			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				ExtraEffectGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					state.Effects.RemoveAt(i);
					continue;
				}

				if (grant.Duration is CardExtraEffectCardGrantDuration.ThisTurn or CardExtraEffectCardGrantDuration.Turns)
				{
					grant.RemainingTurns--;
					if (grant.RemainingTurns <= 0)
					{
						state.Grants.RemoveAt(i);
						state.Effects.RemoveAt(i);
					}
				}
			}

			if (state.Grants.Count == 0)
			{
				schedule.States.Remove(card);
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
		_schedules.Remove(combatState);
	}
}
