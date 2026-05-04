using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorDrawnGeneratedCostController
{
	private sealed class CostGrant
	{
		public ModelId? SourceCardId { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class PlayerState
	{
		public List<CostGrant> Grants { get; } = new List<CostGrant>();
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
				CostGrant grant = state.Grants[i];
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
		if (effect.Kind is not (CardExtraEffectKind.DrawnCardsCostLess or CardExtraEffectKind.GeneratedCardsCostLess))
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
				&& a.TriggerCardPool == b.TriggerCardPool
				&& a.TriggerCardType == b.TriggerCardType
				&& a.DrawnFromPile == b.DrawnFromPile;
		}

		CardExtraEffectCardGrantDuration duration = effect.CardCostsLessDuration switch
		{
			CardExtraEffectCardCostsLessDuration.ThisTurn => CardExtraEffectCardGrantDuration.ThisTurn,
			CardExtraEffectCardCostsLessDuration.Turns => CardExtraEffectCardGrantDuration.Turns,
			CardExtraEffectCardCostsLessDuration.ThisCombat => CardExtraEffectCardGrantDuration.ThisCombat,
			CardExtraEffectCardCostsLessDuration.UntilPlayed => CardExtraEffectCardGrantDuration.ThisCombat,
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

		ModelId? sourceCardId = null;
		try
		{
			sourceCardId = CardEditorCardPlayContext.Current?.Card?.Id;
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

		// For repeating timed triggers with "this combat" duration, re-applying should stack (e.g. "at end of your turn, drawn cards cost 1 less this combat").
		// Keep the grant list compact by accumulating into the existing grant for the same source + configuration.
		if (sourceCardId != null
			&& duration == CardExtraEffectCardGrantDuration.ThisCombat
			&& IsTimedTrigger(effect.Trigger))
		{
			for (int i = 0; i < state.Grants.Count; i++)
			{
				CostGrant? existing = state.Grants[i];
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

		state.Grants.Add(new CostGrant
		{
			SourceCardId = sourceCardId,
			Effect = CardEditorExtraEffects.CloneEffect(effect),
			Duration = duration,
			RemainingTurns = remainingTurns
		});
	}

	internal static void OnCardArrivedInHand(CombatState combatState, CardModel card, PileType oldPile)
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
		for (int i = 0; i < state.Grants.Count; i++)
		{
			CardExtraEffect? effect = state.Grants[i]?.Effect;
			if (effect == null || effect.Kind != CardExtraEffectKind.DrawnCardsCostLess)
			{
				continue;
			}

			// Check source pile filter.
			if (effect.DrawnFromPile != CardExtraEffectCardPile.AllPiles && effect.DrawnFromPile != CardExtraEffectCardPile.Hand)
			{
				PileType requiredOld = effect.DrawnFromPile switch
				{
					CardExtraEffectCardPile.DrawPile => PileType.Draw,
					CardExtraEffectCardPile.DiscardPile => PileType.Discard,
					CardExtraEffectCardPile.ExhaustPile => PileType.Exhaust,
					_ => PileType.None
				};
				if (requiredOld != PileType.None && oldPile != requiredOld)
				{
					continue;
				}
			}

			// Check card pool / type filters.
			if (!CardEditorExtraEffects.MatchesCountPool(owner, card, effect.TriggerCardPool))
			{
				continue;
			}
			if (!MatchesType(card, effect.TriggerCardType))
			{
				continue;
			}

			StampCostReduction(combatState, card, effect);
			changed = true;
		}

		if (changed)
		{
			card.InvokeEnergyCostChanged();
		}
	}

	internal static void OnCardGenerated(CombatState combatState, CardModel card)
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
		for (int i = 0; i < state.Grants.Count; i++)
		{
			CardExtraEffect? effect = state.Grants[i]?.Effect;
			if (effect == null || effect.Kind != CardExtraEffectKind.GeneratedCardsCostLess)
			{
				continue;
			}

			if (!CardEditorExtraEffects.MatchesCountPool(owner, card, effect.TriggerCardPool))
			{
				continue;
			}
			if (!MatchesType(card, effect.TriggerCardType))
			{
				continue;
			}

			StampCostReduction(combatState, card, effect);
			changed = true;
		}

		if (changed)
		{
			card.InvokeEnergyCostChanged();
		}
	}

	internal static void StampCostReductionForCard(CombatState combatState, CardModel card, CardExtraEffect effect, int rawAmount)
	{
		CardExtraEffectCostModifier modifier = CardEditorExtraEffects.GetEffectiveCardCostsLessModifier(effect);
		int amount = Math.Abs(rawAmount);
		bool isCostMore = rawAmount < 0;

		if (modifier == CardExtraEffectCostModifier.Free)
		{
			switch (effect.CardCostsLessDuration)
			{
				case CardExtraEffectCardCostsLessDuration.ThisTurn:
					card.EnergyCost.SetThisTurn(0, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.ThisCombat:
					card.EnergyCost.SetThisCombat(0, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.UntilPlayed:
					card.EnergyCost.SetUntilPlayed(0, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.Turns:
				{
					int turns = Math.Max(1, effect.CardCostsLessTurns);
					card.EnergyCost.SetThisTurn(0, reduceOnly: true);
					if (turns > 1)
					{
						CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, 0, turns, CardExtraEffectCostModifier.Free);
					}
					break;
				}
				default:
					card.EnergyCost.SetThisCombat(0, reduceOnly: true);
					break;
			}
			return;
		}
		if (modifier == CardExtraEffectCostModifier.FreeToPlay)
		{
			switch (effect.CardCostsLessDuration)
			{
				case CardExtraEffectCardCostsLessDuration.ThisTurn:
					card.SetToFreeThisTurn();
					CardEditorOverrides.NotifyStarCostChanged(card);
					break;
				case CardExtraEffectCardCostsLessDuration.ThisCombat:
					card.SetToFreeThisCombat();
					CardEditorOverrides.NotifyStarCostChanged(card);
					break;
				case CardExtraEffectCardCostsLessDuration.UntilPlayed:
					card.EnergyCost.SetUntilPlayed(0, reduceOnly: true);
					card.SetStarCostUntilPlayed(0);
					CardEditorOverrides.NotifyStarCostChanged(card);
					break;
				case CardExtraEffectCardCostsLessDuration.Turns:
				{
					int turns = Math.Max(1, effect.CardCostsLessTurns);
					card.SetToFreeThisTurn();
					CardEditorOverrides.NotifyStarCostChanged(card);
					if (turns > 1)
					{
						CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, 0, turns, CardExtraEffectCostModifier.FreeToPlay);
					}
					break;
				}
				default:
					card.SetToFreeThisCombat();
					CardEditorOverrides.NotifyStarCostChanged(card);
					break;
			}
			return;
		}
		if (modifier == CardExtraEffectCostModifier.HalfCost)
		{
			int currentCost;
			using (CardEditorEnergyCostVisibilityHelper.SuppressCardEditorCostHooksScoped())
			{
				currentCost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
			}
			if (currentCost < 0)
			{
				return;
			}

			int halfCost = Math.Max(0, currentCost / 2);
			switch (effect.CardCostsLessDuration)
			{
				case CardExtraEffectCardCostsLessDuration.ThisTurn:
					card.EnergyCost.SetThisTurn(halfCost, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.ThisCombat:
					card.EnergyCost.SetThisCombat(halfCost, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.UntilPlayed:
					card.EnergyCost.SetUntilPlayed(halfCost, reduceOnly: true);
					break;
				case CardExtraEffectCardCostsLessDuration.Turns:
				{
					int turns = Math.Max(1, effect.CardCostsLessTurns);
					card.EnergyCost.SetThisTurn(halfCost, reduceOnly: true);
					if (turns > 1)
					{
						CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, 0, turns, CardExtraEffectCostModifier.HalfCost);
					}
					break;
				}
				default:
					card.EnergyCost.SetThisCombat(halfCost, reduceOnly: true);
					break;
			}
			return;
		}

		switch (effect.CardCostsLessDuration)
		{
			case CardExtraEffectCardCostsLessDuration.ThisTurn:
				if (isCostMore)
					card.EnergyCost.AddThisTurn(amount, reduceOnly: false);
				else
					card.EnergyCost.AddThisTurn(-amount, reduceOnly: true);
				break;
			case CardExtraEffectCardCostsLessDuration.ThisCombat:
				if (isCostMore)
					card.EnergyCost.AddThisCombat(amount, reduceOnly: false);
				else
					card.EnergyCost.AddThisCombat(-amount, reduceOnly: true);
				break;
			case CardExtraEffectCardCostsLessDuration.UntilPlayed:
				if (isCostMore)
					card.EnergyCost.AddUntilPlayed(amount, reduceOnly: false);
				else
					card.EnergyCost.AddUntilPlayed(-amount, reduceOnly: true);
				break;
			case CardExtraEffectCardCostsLessDuration.Turns:
			{
				int turns = Math.Max(1, effect.CardCostsLessTurns);
				if (isCostMore)
					card.EnergyCost.AddThisTurn(amount, reduceOnly: false);
				else
					card.EnergyCost.AddThisTurn(-amount, reduceOnly: true);
				int remaining = turns - 1;
				if (remaining > 0)
				{
					CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, isCostMore ? -amount : amount, remaining + 1, CardExtraEffectCostModifier.Reduce);
				}
				break;
			}
			default:
				if (isCostMore)
					card.EnergyCost.AddThisCombat(amount, reduceOnly: false);
				else
					card.EnergyCost.AddThisCombat(-amount, reduceOnly: true);
				break;
		}
	}

	private static void StampCostReduction(CombatState combatState, CardModel card, CardExtraEffect effect)
		=> StampCostReductionForCard(combatState, card, effect, effect.Amount);

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

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class Hook_AfterCardChangedPiles_DrawnCardsCostLess_Patch
{
	public static void Postfix(CombatState? combatState, CardModel card, PileType oldPile)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		// Only apply when the card arrives in hand.
		if (card.Pile == null || card.Pile.Type != PileType.Hand)
		{
			return;
		}
		CardEditorDrawnGeneratedCostController.OnCardArrivedInHand(combatState, card, oldPile);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class Hook_AfterCardGeneratedForCombat_GeneratedCardsCostLess_Patch
{
	public static void Postfix(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		CardEditorDrawnGeneratedCostController.OnCardGenerated(combatState, card);
	}
}
