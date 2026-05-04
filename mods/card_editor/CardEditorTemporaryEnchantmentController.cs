using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryEnchantmentController
{
	private sealed class EnchantmentSnapshot
	{
		public required ModelId Id { get; init; }
		public required int Amount { get; init; }
	}

	private sealed class EnchantmentGrant
	{
		public required ModelId Id { get; init; }
		public required int Amount { get; init; }
		public required CardExtraEffectEnchantmentDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class CardState
	{
		public EnchantmentSnapshot? Baseline { get; set; }
		public List<EnchantmentGrant> Grants { get; } = new List<EnchantmentGrant>();
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

				// Some generated-in-combat cards have CloneOf chains that point at immutable canonical models.
				// If the next link isn't mutable, we must key off the current (mutable) instance so the enchant
				// actually applies to the card the player selected.
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

	public static void Apply(CombatState combatState, CardModel card, ModelId enchantmentId, int amount, CardExtraEffectEnchantmentDuration duration, int turns)
	{
		if (combatState == null || card == null || !card.IsMutable)
		{
			return;
		}
		if (enchantmentId == ModelId.none || amount <= 0)
		{
			return;
		}

		// Permanent enchantments should persist on the player's deck card (not just the combat clone).
		// The combat clone points at the deck instance via DeckVersion when it originated from the deck.
		if (duration == CardExtraEffectEnchantmentDuration.Permanent)
		{
			try
			{
				CardModel? deckCard = card.DeckVersion;
				if (deckCard != null && deckCard.IsMutable)
				{
					ApplySnapshot(deckCard, new EnchantmentSnapshot { Id = enchantmentId, Amount = amount });
				}
			}
			catch
			{
				// ignored
			}
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		CardModel key = ResolveScheduleKey(card);
		if (!schedule.States.TryGetValue(key, out CardState? state))
		{
			state = new CardState
			{
				Baseline = CaptureSnapshot(key)
			};
			schedule.States[key] = state;
		}

		state.Grants.Add(new EnchantmentGrant
		{
			Id = enchantmentId,
			Amount = amount,
			Duration = duration,
			RemainingTurns = duration switch
			{
				CardExtraEffectEnchantmentDuration.ThisTurn => 1,
				CardExtraEffectEnchantmentDuration.Turns => Math.Clamp(turns, 1, 99),
				_ => 0
			}
		});

		ReapplyCardState(key, state);
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
			EnchantmentGrant grant = state.Grants[i];
			if (grant == null)
			{
				state.Grants.RemoveAt(i);
				changed = true;
				continue;
			}
			if (grant.Duration == CardExtraEffectEnchantmentDuration.UntilPlayed)
			{
				state.Grants.RemoveAt(i);
				changed = true;
			}
		}

		FinalizeStateChange(schedule, key, state, changed);
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

		foreach ((CardModel card, CardState state) in schedule.States)
		{
			if (card == null || state == null || state.Grants.Count == 0)
			{
				continue;
			}
			if (!card.IsMutable || card.HasBeenRemovedFromState)
			{
				continue;
			}

			bool changed = false;
			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				EnchantmentGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					changed = true;
					continue;
				}

				if (grant.Duration is CardExtraEffectEnchantmentDuration.ThisTurn or CardExtraEffectEnchantmentDuration.Turns)
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
				ReapplyCardState(card, state);
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
				EnchantmentGrant grant = state.Grants[i];
				if (grant == null || grant.Duration != CardExtraEffectEnchantmentDuration.Permanent)
				{
					state.Grants.RemoveAt(i);
				}
			}

			ReapplyCardState(card, state);
		}

		_schedules.Remove(combatState);
	}

	public static void OnCardTransformed(CombatState combatState, CardModel original, CardModel replacement)
	{
		if (combatState == null || original == null || replacement == null || !replacement.IsMutable)
		{
			return;
		}

		if (TryMoveScheduledState(combatState, original, replacement))
		{
			return;
		}

		CopyVisibleEnchantment(original, replacement);
	}

	private static bool TryMoveScheduledState(CombatState combatState, CardModel original, CardModel replacement)
	{
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return false;
		}

		CardModel fromKey = ResolveScheduleKey(original);
		CardModel toKey = ResolveScheduleKey(replacement);
		if (fromKey == null || toKey == null || !schedule.States.TryGetValue(fromKey, out CardState? state))
		{
			return false;
		}

		schedule.States.Remove(fromKey);
		if (schedule.States.TryGetValue(toKey, out CardState? existing))
		{
			if (existing.Baseline == null)
			{
				existing.Baseline = state.Baseline;
			}
			existing.Grants.AddRange(state.Grants);
			state = existing;
		}
		else
		{
			schedule.States[toKey] = state;
		}

		ReapplyCardState(toKey, state);
		return true;
	}

	private static void CopyVisibleEnchantment(CardModel source, CardModel destination)
	{
		EnchantmentSnapshot? snapshot = CaptureSnapshot(source);
		if (snapshot == null)
		{
			return;
		}

		ClearCardEnchantment(destination);
		ApplySnapshot(destination, snapshot);
	}

	private static void FinalizeStateChange(CombatSchedule schedule, CardModel card, CardState state, bool changed)
	{
		if (!changed)
		{
			return;
		}

		if (state.Grants.Count == 0)
		{
			ReapplyCardState(card, state);
			schedule.States.Remove(card);
			return;
		}

		ReapplyCardState(card, state);
	}

	private static void CleanupEmptySchedule(CombatState combatState, CombatSchedule schedule)
	{
		List<CardModel> toRemove = new List<CardModel>();
		foreach ((CardModel card, CardState state) in schedule.States)
		{
			if (card == null || state == null || state.Grants.Count > 0)
			{
				continue;
			}
			toRemove.Add(card);
		}

		foreach (CardModel card in toRemove)
		{
			schedule.States.Remove(card);
		}

		if (schedule.States.Count == 0)
		{
			_schedules.Remove(combatState);
		}
	}

	private static EnchantmentSnapshot? CaptureSnapshot(CardModel card)
	{
		if (card?.Enchantment == null)
		{
			return null;
		}

		return new EnchantmentSnapshot
		{
			Id = card.Enchantment.Id,
			Amount = Math.Max(1, card.Enchantment.Amount)
		};
	}

	private static void ReapplyCardState(CardModel card, CardState state)
	{
		if (card == null || state == null || !card.IsMutable)
		{
			return;
		}

		ClearCardEnchantment(card);
		if (state.Baseline != null)
		{
			ApplySnapshot(card, state.Baseline);
		}

		foreach (EnchantmentGrant grant in state.Grants)
		{
			if (grant == null || grant.Id == ModelId.none || grant.Amount <= 0)
			{
				continue;
			}

			ApplySnapshot(card, new EnchantmentSnapshot
			{
				Id = grant.Id,
				Amount = grant.Amount
			});
		}
	}

	private static void ClearCardEnchantment(CardModel card)
	{
		CardCmd.ClearEnchantment(card);
		card.FinalizeUpgradeInternal();
		card.InvokeEnergyCostChanged();
	}

	private static void ApplySnapshot(CardModel card, EnchantmentSnapshot snapshot)
	{
		if (card == null || snapshot == null || snapshot.Id == ModelId.none || snapshot.Amount <= 0)
		{
			return;
		}

		EnchantmentModel? enchantment = ModelDb.GetByIdOrNull<EnchantmentModel>(snapshot.Id)?.ToMutable();
		if (enchantment == null)
		{
			return;
		}

		try
		{
			if (card.Enchantment != null && card.Enchantment.GetType() != enchantment.GetType())
			{
				ClearCardEnchantment(card);
			}

			if (card.Enchantment != null && card.Enchantment.GetType() == enchantment.GetType())
			{
				CardCmd.Enchant(enchantment, card, snapshot.Amount);
			}
			else if (enchantment.CanEnchant(card))
			{
				CardCmd.Enchant(enchantment, card, snapshot.Amount);
			}
		}
		catch (InvalidOperationException)
		{
			return;
		}

		card.InvokeEnergyCostChanged();
	}
}
