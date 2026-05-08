using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCardTypeCostAuras
{
	private sealed class AuraGrant
	{
		public ModelId? SourceCardId { get; init; }
		public CardModel? SourceCard { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
		public int RemainingUses { get; set; }
		public bool SkipNextPlayForSourceCard { get; set; }
	}

	private sealed class PlayerState
	{
		public List<AuraGrant> Grants { get; } = new List<AuraGrant>();
	}

	private sealed class CombatSchedule
	{
		public Dictionary<Player, PlayerState> States { get; } = new Dictionary<Player, PlayerState>();
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules = new ConditionalWeakTable<CombatState, CombatSchedule>();

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		_schedules.Remove(combatState);
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

		foreach ((Player player, PlayerState state) in schedule.States.ToList())
		{
			if (player == null || state == null || state.Grants.Count == 0)
			{
				schedule.States.Remove(player);
				continue;
			}

			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				AuraGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					continue;
				}

				if (grant.Duration is CardExtraEffectCardGrantDuration.ThisTurn or CardExtraEffectCardGrantDuration.Turns)
				{
					grant.RemainingTurns--;
					if (grant.RemainingTurns <= 0)
					{
						state.Grants.RemoveAt(i);
					}
				}
			}

			if (state.Grants.Count == 0)
			{
				schedule.States.Remove(player);
			}
		}

		if (schedule.States.Count == 0)
		{
			_schedules.Remove(combatState);
		}
	}

	public static void Apply(CombatState combatState, Player owner, CardExtraEffect effect)
	{
		if (combatState == null || owner == null || effect == null)
		{
			return;
		}
		if (effect.Kind is not (CardExtraEffectKind.CardTypeCostsLess or CardExtraEffectKind.CardTypeStarCostsLess))
		{
			return;
		}
		if (!CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return;
		}

		static bool IsTimedTrigger(CardExtraEffectTrigger trigger)
			=> trigger is CardExtraEffectTrigger.TurnBoundary
				or CardExtraEffectTrigger.StartOfTurn
				or CardExtraEffectTrigger.EndOfTurn
				or CardExtraEffectTrigger.EndOfTurnInHand
				or CardExtraEffectTrigger.StartOfEnemyTurn
				or CardExtraEffectTrigger.EndOfEnemyTurn;

		static bool IsPlayerEndBoundary(CombatState combatState, CardExtraEffect effect)
		{
			if (combatState.CurrentSide != CombatSide.Player)
			{
				return false;
			}

			if (effect.Trigger is CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand)
			{
				return true;
			}

			return effect.Trigger == CardExtraEffectTrigger.TurnBoundary
				&& effect.TurnBoundary == CardExtraEffectTurnBoundary.End
				&& effect.TurnBoundarySide is CardExtraEffectTurnBoundarySide.YourTurn or CardExtraEffectTurnBoundarySide.Both;
		}

		static bool AreEquivalentTimedAuraEffectIgnoringAmount(CardExtraEffect a, CardExtraEffect b)
		{
			if (a.Kind != b.Kind)
			{
				return false;
			}

			return a.Trigger == b.Trigger
				&& a.TurnBoundary == b.TurnBoundary
				&& a.TurnBoundarySide == b.TurnBoundarySide
				&& a.TurnBoundaryCardLocation == b.TurnBoundaryCardLocation
				&& a.CardCostsLessDuration == b.CardCostsLessDuration
				&& a.CardCostsLessTurns == b.CardCostsLessTurns
				&& a.CardCostsLessModifier == b.CardCostsLessModifier
				&& a.MatchingCostUseLimitMode == b.MatchingCostUseLimitMode
				&& a.MatchingCostUseLimit == b.MatchingCostUseLimit
				&& a.TriggerCardPool == b.TriggerCardPool
				&& a.TriggerCardType == b.TriggerCardType;
		}

		CardExtraEffectCardGrantDuration duration = effect.CardCostsLessDuration switch
		{
			CardExtraEffectCardCostsLessDuration.ThisTurn => CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardCostsLessDuration.Turns => CardExtraEffectCardGrantDuration.Turns,
			CardExtraEffectCardCostsLessDuration.ThisCombat => CardExtraEffectCardGrantDuration.ThisCombat,
			// UntilPlayed doesn't make sense as an aura; treat it like "this combat".
			CardExtraEffectCardCostsLessDuration.UntilPlayed => CardExtraEffectCardGrantDuration.ThisCombat,
			// "Permanent" means "for the rest of combat" once it has triggered.
			_ => CardExtraEffectCardGrantDuration.ThisCombat
		};

		int remainingTurns = 0;
		if (duration == CardExtraEffectCardGrantDuration.ThisTurn)
		{
			remainingTurns = IsPlayerEndBoundary(combatState, effect) ? 2 : 1;
		}
		else if (duration == CardExtraEffectCardGrantDuration.Turns)
		{
			int baseTurns = Math.Clamp(effect.CardCostsLessTurns, 1, 99);
			remainingTurns = baseTurns + (IsPlayerEndBoundary(combatState, effect) ? 1 : 0);
		}

		CardModel? sourceCard = null;
		ModelId? sourceCardId = null;
		try
		{
			sourceCard = CardEditorCardPlayContext.Current?.Card;
			sourceCardId = sourceCard?.Id;
		}
		catch
		{
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.States.TryGetValue(owner, out PlayerState? state))
		{
			state = new PlayerState();
			schedule.States[owner] = state;
		}

		// For repeating timed triggers with "this combat" duration, re-applying should stack (e.g. "at end of your turn, cards cost 1 less this combat").
		// Keep the grant list compact by accumulating into the existing grant for the same source + configuration.
		if (sourceCardId != null
			&& duration == CardExtraEffectCardGrantDuration.ThisCombat
			&& IsTimedTrigger(effect.Trigger)
			&& !CardEditorExtraEffects.IsMatchingCostUseLimited(effect))
		{
			for (int i = 0; i < state.Grants.Count; i++)
			{
				AuraGrant? existing = state.Grants[i];
				if (existing?.SourceCardId == null
					|| !existing.SourceCardId.Equals(sourceCardId)
					|| existing.Duration != duration
					|| !AreEquivalentTimedAuraEffectIgnoringAmount(existing.Effect, effect))
				{
					continue;
				}

				if (CardEditorExtraEffects.GetEffectiveCardCostsLessModifier(existing.Effect) == CardExtraEffectCostModifier.Reduce)
				{
					existing.Effect.Amount += effect.Amount;
				}
				return;
			}
		}

		state.Grants.Add(new AuraGrant
		{
			SourceCardId = sourceCardId,
			SourceCard = sourceCard,
			Effect = CardEditorExtraEffects.CloneEffect(effect),
			Duration = duration,
			RemainingTurns = remainingTurns,
			RemainingUses = CardEditorExtraEffects.GetMatchingCostUseLimit(effect),
			SkipNextPlayForSourceCard = CardEditorExtraEffects.IsMatchingCostUseLimited(effect) && sourceCard != null
		});
		NotifyOwnerCardCostsChanged(owner);
	}

	public static void OnAfterCardPlayed(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		Player? owner = card.Owner;
		if (owner == null)
		{
			return;
		}

		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return;
		}
		if (!schedule.States.TryGetValue(owner, out PlayerState? state) || state.Grants.Count == 0)
		{
			return;
		}

		bool changed = false;
		for (int i = state.Grants.Count - 1; i >= 0; i--)
		{
			AuraGrant? grant = state.Grants[i];
			if (grant?.Effect == null)
			{
				state.Grants.RemoveAt(i);
				changed = true;
				continue;
			}

			if (!CardEditorExtraEffects.IsMatchingCostUseLimited(grant.Effect))
			{
				continue;
			}

			if (grant.SkipNextPlayForSourceCard && ReferenceEquals(grant.SourceCard, card))
			{
				grant.SkipNextPlayForSourceCard = false;
				continue;
			}

			if (!MatchesAura(owner, card, grant.Effect))
			{
				continue;
			}

			grant.RemainingUses--;
			changed = true;
			if (grant.RemainingUses <= 0)
			{
				state.Grants.RemoveAt(i);
			}
		}

		if (state.Grants.Count == 0)
		{
			schedule.States.Remove(owner);
			if (schedule.States.Count == 0)
			{
				_schedules.Remove(combatState);
			}
		}

		if (changed)
		{
			NotifyOwnerCardCostsChanged(owner);
		}
	}

	public static CardExtraEffect.CardCostAdjustment GetEnergyCostAdjustment(CombatState combatState, CardModel card)
	{
		return GetCostAdjustment(combatState, card, CardExtraEffectKind.CardTypeCostsLess);
	}

	public static CardExtraEffect.CardCostAdjustment GetStarCostAdjustment(CombatState combatState, CardModel card)
	{
		return GetCostAdjustment(combatState, card, CardExtraEffectKind.CardTypeStarCostsLess);
	}

	public static int GetEnergyCostDelta(CombatState combatState, CardModel card)
	{
		return GetEnergyCostAdjustment(combatState, card).Delta;
	}

	public static int GetStarCostDelta(CombatState combatState, CardModel card)
	{
		return GetStarCostAdjustment(combatState, card).Delta;
	}

	private static CardExtraEffect.CardCostAdjustment GetCostAdjustment(CombatState combatState, CardModel card, CardExtraEffectKind kind)
	{
		if (combatState == null || card == null || kind is not (CardExtraEffectKind.CardTypeCostsLess or CardExtraEffectKind.CardTypeStarCostsLess))
		{
			return new CardExtraEffect.CardCostAdjustment(0, false, false);
		}

		Player? owner = card.Owner;
		if (owner == null)
		{
			return new CardExtraEffect.CardCostAdjustment(0, false, false);
		}

		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return new CardExtraEffect.CardCostAdjustment(0, false, false);
		}
		if (!schedule.States.TryGetValue(owner, out PlayerState? state) || state.Grants.Count == 0)
		{
			return new CardExtraEffect.CardCostAdjustment(0, false, false);
		}

		CardExtraEffect.CardCostAdjustment adjustment = new(0, false, false);
		for (int i = 0; i < state.Grants.Count; i++)
		{
			AuraGrant? grant = state.Grants[i];
			CardExtraEffect? effect = grant?.Effect;
			if (effect == null || effect.Kind != kind)
			{
				continue;
			}
			if (!HasRemainingUses(grant) || !MatchesAura(owner, card, effect))
			{
				continue;
			}
			switch (CardEditorExtraEffects.GetEffectiveCardCostsLessModifier(effect))
			{
				case CardExtraEffectCostModifier.Free:
					adjustment = CardExtraEffect.CardCostAdjustment.Combine(adjustment, new CardExtraEffect.CardCostAdjustment(int.MaxValue, false, false));
					break;
				case CardExtraEffectCostModifier.FreeToPlay:
					adjustment = CardExtraEffect.CardCostAdjustment.Combine(adjustment, new CardExtraEffect.CardCostAdjustment(0, true, false));
					break;
				case CardExtraEffectCostModifier.HalfCost:
					adjustment = CardExtraEffect.CardCostAdjustment.Combine(adjustment, new CardExtraEffect.CardCostAdjustment(0, false, true));
					break;
				default:
					adjustment = CardExtraEffect.CardCostAdjustment.Combine(adjustment, new CardExtraEffect.CardCostAdjustment(effect.Amount, false, false));
					break;
			}
		}

		return adjustment;
	}

	private static bool HasRemainingUses(AuraGrant? grant)
	{
		if (grant?.Effect == null)
		{
			return false;
		}

		return !CardEditorExtraEffects.IsMatchingCostUseLimited(grant.Effect)
			|| grant.RemainingUses > 0;
	}

	private static bool MatchesAura(Player owner, CardModel card, CardExtraEffect effect)
	{
		return CardEditorExtraEffects.MatchesCountPool(owner, card, effect.TriggerCardPool)
			&& MatchesType(card, effect.TriggerCardType);
	}

	private static void NotifyOwnerCardCostsChanged(Player owner)
	{
		try
		{
			if (owner?.PlayerCombatState?.AllCards == null)
			{
				return;
			}

			foreach (CardModel? card in owner.PlayerCombatState.AllCards)
			{
				card?.InvokeEnergyCostChanged();
			}
		}
		catch
		{
		}
	}

	private static bool MatchesType(CardModel card, CardGeneratedCardType type)
	{
		if (type == CardGeneratedCardType.Any)
		{
			return true;
		}

		if (type == CardGeneratedCardType.Playable)
		{
			return card.Type is CardType.Attack or CardType.Skill or CardType.Power;
		}

		CardType desired = type switch
		{
			CardGeneratedCardType.Attack => CardType.Attack,
			CardGeneratedCardType.Skill => CardType.Skill,
			CardGeneratedCardType.Power => CardType.Power,
			CardGeneratedCardType.Status => CardType.Status,
			CardGeneratedCardType.Curse => CardType.Curse,
			CardGeneratedCardType.Quest => CardType.Quest,
			_ => CardType.Skill
		};
		return card.Type == desired;
	}
}
