using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCardPlayContext
{
	private sealed class Scope : IDisposable
	{
		private readonly CardPlay? _play;
		private bool _disposed;

		public Scope(CardPlay? play)
		{
			_play = play;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			if (_play != null)
			{
				Pop(_play);
			}
		}
	}

	private static readonly AsyncLocal<Stack<CardPlay>?> _stack = new AsyncLocal<Stack<CardPlay>?>();

	public static CardPlay? Current
	{
		get
		{
			Stack<CardPlay>? stack = _stack.Value;
			return stack != null && stack.Count > 0 ? stack.Peek() : null;
		}
	}

	public static IDisposable PushScoped(CardPlay play)
	{
		Push(play);
		return new Scope(play);
	}

	public static void Push(CardPlay play)
	{
		if (play == null)
		{
			return;
		}
		Stack<CardPlay> stack = _stack.Value ??= new Stack<CardPlay>();
		stack.Push(play);
	}

	public static void Pop(CardPlay play)
	{
		Stack<CardPlay>? stack = _stack.Value;
		if (stack == null || stack.Count == 0 || play == null)
		{
			return;
		}
		if (ReferenceEquals(stack.Peek(), play))
		{
			stack.Pop();
			return;
		}

		CardPlay[] snapshot = stack.ToArray();
		stack.Clear();
		bool removed = false;
		for (int i = snapshot.Length - 1; i >= 0; i--)
		{
			CardPlay item = snapshot[i];
			if (!removed && ReferenceEquals(item, play))
			{
				removed = true;
				continue;
			}
			stack.Push(item);
		}
	}
}

internal static class CardEditorCreatedCardsCostController
{
	private sealed class PendingTurnDiscount
	{
		public required int Amount { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, List<PendingTurnDiscount>> DiscountsByCard { get; } = new Dictionary<CardModel, List<PendingTurnDiscount>>();
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

	public static void ApplyForTurns(CombatState combatState, CardModel card, int amount, int turns)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if ((amount <= 0 && amount != -1) || turns <= 0)
		{
			return;
		}

		ApplyThisTurn(card, amount);

		int remaining = turns - 1;
		if (remaining <= 0)
		{
			return;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.DiscountsByCard.TryGetValue(card, out List<PendingTurnDiscount>? list))
		{
			list = new List<PendingTurnDiscount>();
			schedule.DiscountsByCard[card] = list;
		}
		list.Add(new PendingTurnDiscount { Amount = amount, RemainingTurns = remaining });
	}

	public static void ApplyThisTurn(CardModel card, int amount)
	{
		if (card == null)
		{
			return;
		}
		if (amount == -1)
		{
			card.EnergyCost.SetThisTurn(0, reduceOnly: true);
			card.InvokeEnergyCostChanged();
			return;
		}
		if (amount <= 0)
		{
			return;
		}
		card.EnergyCost.AddThisTurn(-amount, reduceOnly: true);
		card.InvokeEnergyCostChanged();
	}

	public static void ApplyThisCombat(CardModel card, int amount)
	{
		if (card == null)
		{
			return;
		}
		if (amount == -1)
		{
			card.EnergyCost.SetThisCombat(0, reduceOnly: true);
			card.InvokeEnergyCostChanged();
			return;
		}
		if (amount <= 0)
		{
			return;
		}
		card.EnergyCost.AddThisCombat(-amount, reduceOnly: true);
		card.InvokeEnergyCostChanged();
	}

	public static void ApplyUntilPlayed(CardModel card, int amount)
	{
		if (card == null)
		{
			return;
		}
		if (amount == -1)
		{
			card.EnergyCost.SetUntilPlayed(0, reduceOnly: true);
			card.InvokeEnergyCostChanged();
			return;
		}
		if (amount <= 0)
		{
			return;
		}
		card.EnergyCost.AddUntilPlayed(-amount, reduceOnly: true);
		card.InvokeEnergyCostChanged();
	}

	public static void OnAfterPlayerTurnStart(CombatState combatState, Player player)
	{
		if (combatState == null || player == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.DiscountsByCard.Count == 0)
		{
			return;
		}

		foreach ((CardModel card, List<PendingTurnDiscount> discounts) in schedule.DiscountsByCard.ToList())
		{
			if (card == null || discounts == null || discounts.Count == 0)
			{
				schedule.DiscountsByCard.Remove(card);
				continue;
			}
			if (card.Owner != player || card.HasBeenRemovedFromState)
			{
				schedule.DiscountsByCard.Remove(card);
				continue;
			}

			bool changed = false;
			for (int i = discounts.Count - 1; i >= 0; i--)
			{
				PendingTurnDiscount pending = discounts[i];
				if (pending == null)
				{
					discounts.RemoveAt(i);
					continue;
				}
				if (pending.RemainingTurns <= 0)
				{
					discounts.RemoveAt(i);
					continue;
				}

				if (pending.Amount == -1)
				{
					card.EnergyCost.SetThisTurn(0, reduceOnly: true);
				}
				else
				{
					card.EnergyCost.AddThisTurn(-pending.Amount, reduceOnly: true);
				}
				pending.RemainingTurns--;
				changed = true;

				if (pending.RemainingTurns <= 0)
				{
					discounts.RemoveAt(i);
				}
			}

			if (discounts.Count == 0)
			{
				schedule.DiscountsByCard.Remove(card);
			}

			if (changed)
			{
				card.InvokeEnergyCostChanged();
			}
		}
	}
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class CombatHistory_CardPlayStarted_CardEditorContext_Patch
{
	public static void Prefix(CardPlay cardPlay)
	{
		CardEditorCardPlayContext.Push(cardPlay);
	}
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
internal static class CombatHistory_CardPlayFinished_CardEditorContext_Patch
{
	public static void Prefix(CardPlay cardPlay)
	{
		CardEditorCardPlayContext.Pop(cardPlay);
	}
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardsToCombat))]
internal static class CardPileCmd_AddGeneratedCardsToCombat_CreatedCardsCostLess_Patch
{
	public static void Prefix(ref IEnumerable<CardModel> cards)
	{
		if (cards == null)
		{
			return;
		}

		List<CardModel> list;
		if (cards is List<CardModel> alreadyList)
		{
			list = alreadyList;
		}
		else
		{
			list = cards.ToList();
			cards = list;
		}

		if (list.Count == 0)
		{
			return;
		}

		CardModel? sourceCard = CardEditorCardPlayContext.Current?.Card;
		if (sourceCard == null && CardEditorHookModelContext.Current is PowerModel power)
		{
			sourceCard = CardEditorPowerSourceMap.TryGetSourceCard(power);
		}
		if (sourceCard == null)
		{
			return;
		}

		CombatState? combatState = sourceCard.CombatState ?? list[0].Owner?.Creature?.CombatState;
		if (combatState == null)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetEffectsForDescription(sourceCard, isUpgradePreview: false);
		if (effects == null || effects.Count == 0)
		{
			return;
		}

		List<CardExtraEffect>? costLessEffects = null;
		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null
				|| effect.Kind != CardExtraEffectKind.CreatedCardsCostLess
				|| effect.Trigger != CardExtraEffectTrigger.OnPlay
				|| !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
			{
				continue;
			}
			(costLessEffects ??= new List<CardExtraEffect>()).Add(effect);
		}
		if (costLessEffects == null || costLessEffects.Count == 0)
		{
			return;
		}

		foreach (CardModel card in list)
		{
			if (card == null)
			{
				continue;
			}

			foreach (CardExtraEffect effect in costLessEffects)
			{
				switch (effect.CreatedCardsCostDuration)
				{
					case CardCreatedCardsCostDuration.ThisCombat:
						CardEditorCreatedCardsCostController.ApplyThisCombat(card, effect.Amount);
						break;
					case CardCreatedCardsCostDuration.UntilPlayed:
						CardEditorCreatedCardsCostController.ApplyUntilPlayed(card, effect.Amount);
						break;
					case CardCreatedCardsCostDuration.Turns:
					{
						int turns = effect.CreatedCardsCostTurns <= 0 ? 1 : effect.CreatedCardsCostTurns;
						CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, effect.Amount, turns);
						break;
					}
					default:
						CardEditorCreatedCardsCostController.ApplyThisTurn(card, effect.Amount);
						break;
				}
			}
		}
	}
}
