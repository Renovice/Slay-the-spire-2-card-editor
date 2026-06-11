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
		public required CardExtraEffectCostModifier Modifier { get; init; }
		public int RemainingTurns { get; set; }
		// Cards stamped during the turn-start sequence (opening hand draw runs BEFORE
		// AfterPlayerTurnStart) must not have their first scheduled turn consumed in the same
		// round — that double-applies the modifier on turn one and ends the span a turn early.
		public int CreatedRoundNumber { get; init; }
	}

	private sealed class PendingStarDiscount
	{
		public required CardExtraEffect.CardCostAdjustment Adjustment { get; init; }
		public int RemainingTurns { get; set; }
		public bool ExpireOnPlayed { get; init; }
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, List<PendingTurnDiscount>> DiscountsByCard { get; } = new Dictionary<CardModel, List<PendingTurnDiscount>>();
		public Dictionary<CardModel, List<PendingStarDiscount>> StarDiscountsByCard { get; } = new Dictionary<CardModel, List<PendingStarDiscount>>();
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

	private static CardExtraEffectCostModifier NormalizeModifier(int amount, CardExtraEffectCostModifier modifier)
		=> modifier == CardExtraEffectCostModifier.Reduce && amount == -1
			? CardExtraEffectCostModifier.Free
			: modifier;

	private static CardExtraEffect.CardCostAdjustment BuildStarAdjustment(int amount, CardExtraEffectCostModifier modifier)
	{
		switch (NormalizeModifier(amount, modifier))
		{
			case CardExtraEffectCostModifier.Free:
				return new CardExtraEffect.CardCostAdjustment(int.MaxValue, ForceFree: false, HalfCost: false);
			case CardExtraEffectCostModifier.FreeToPlay:
				return new CardExtraEffect.CardCostAdjustment(0, ForceFree: true, HalfCost: false);
			case CardExtraEffectCostModifier.HalfCost:
				return new CardExtraEffect.CardCostAdjustment(0, ForceFree: false, HalfCost: true);
			default:
				if (amount == -1)
				{
					return new CardExtraEffect.CardCostAdjustment(int.MaxValue, ForceFree: false, HalfCost: false);
				}
				return amount > 0
					? new CardExtraEffect.CardCostAdjustment(amount, ForceFree: false, HalfCost: false)
					: default;
		}
	}

	private static bool TryAddStarDiscount(CardModel card, CardExtraEffect.CardCostAdjustment adjustment, int remainingTurns, bool expireOnPlayed)
	{
		if (card == null || adjustment.IsNeutral)
		{
			return false;
		}

		CombatState? combatState = card.GetConcreteCombatState() ?? card.TryGetOwnerCreature().GetConcreteCombatState();
		if (combatState == null)
		{
			return false;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.StarDiscountsByCard.TryGetValue(card, out List<PendingStarDiscount>? list))
		{
			list = new List<PendingStarDiscount>();
			schedule.StarDiscountsByCard[card] = list;
		}
		list.Add(new PendingStarDiscount
		{
			Adjustment = adjustment,
			RemainingTurns = Math.Max(0, remainingTurns),
			ExpireOnPlayed = expireOnPlayed
		});
		return true;
	}

	private static void NotifyCostChanged(CardModel card, CardCreatedCardsCostResource resource)
	{
		if (card == null)
		{
			return;
		}

		if (resource == CardCreatedCardsCostResource.Stars)
		{
			CardEditorOverrides.NotifyStarCostChanged(card);
		}
		else
		{
			card.InvokeEnergyCostChanged();
		}
	}

	private static bool TryApplyHalfCost(CardModel card, Action<int> setter)
	{
		if (card == null)
		{
			return false;
		}

		// Snapshot the LOCAL cost only: vanilla applies local modifiers first and global hooks
		// after, so baking a hook discount (Curious-style aura) into an absolute local modifier
		// would deduct it twice once the hook re-applies on top.
		int currentCost = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
		if (currentCost < 0)
		{
			return false;
		}

		setter(Math.Max(0, currentCost / 2));
		return true;
	}

	private static bool ApplyTrueFreeThisTurn(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.SetToFreeThisTurn();
		CardEditorOverrides.NotifyStarCostChanged(card);
		return true;
	}

	private static bool ApplyTrueFreeThisCombat(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.SetToFreeThisCombat();
		CardEditorOverrides.NotifyStarCostChanged(card);
		return true;
	}

	private static bool ApplyTrueFreeUntilPlayed(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.EnergyCost.SetUntilPlayed(0, reduceOnly: true);
		card.SetStarCostUntilPlayed(0);
		CardEditorOverrides.NotifyStarCostChanged(card);
		return true;
	}

	private static bool ApplyZeroThisTurn(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.EnergyCost.SetThisTurn(0, reduceOnly: true);
		return true;
	}

	private static bool ApplyZeroThisCombat(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.EnergyCost.SetThisCombat(0, reduceOnly: true);
		return true;
	}

	private static bool ApplyZeroUntilPlayed(CardModel card)
	{
		if (card == null)
		{
			return false;
		}

		card.EnergyCost.SetUntilPlayed(0, reduceOnly: true);
		return true;
	}

	private static bool ApplyPermanentEnergyCost(CardModel card, int amount, CardExtraEffectCostModifier modifier)
	{
		if (card == null || card.EnergyCost.CostsX)
		{
			return false;
		}

		int baseCost = card.EnergyCost.GetWithModifiers(CostModifiers.None);
		if (baseCost < 0)
		{
			return false;
		}

		int targetCost = baseCost;
		switch (modifier)
		{
			case CardExtraEffectCostModifier.Free:
			case CardExtraEffectCostModifier.FreeToPlay:
				targetCost = 0;
				break;
			case CardExtraEffectCostModifier.HalfCost:
				targetCost = Math.Max(0, baseCost / 2);
				break;
			default:
				if (amount == -1)
				{
					targetCost = 0;
					break;
				}
				if (amount <= 0)
				{
					return false;
				}
				targetCost = Math.Max(-1, baseCost - amount);
				break;
		}

		if (targetCost == baseCost)
		{
			return false;
		}

		card.EnergyCost.SetCustomBaseCost(targetCost);
		return true;
	}

	private static bool ApplyPermanentStarCost(CardModel card, int amount, CardExtraEffectCostModifier modifier)
	{
		if (card == null || card.HasStarCostX)
		{
			return false;
		}

		int baseCost = card.BaseStarCost;
		if (baseCost < 0)
		{
			return false;
		}

		int targetCost = baseCost;
		switch (modifier)
		{
			case CardExtraEffectCostModifier.Free:
			case CardExtraEffectCostModifier.FreeToPlay:
				targetCost = 0;
				break;
			case CardExtraEffectCostModifier.HalfCost:
				targetCost = Math.Max(0, baseCost / 2);
				break;
			default:
				if (amount == -1)
				{
					targetCost = 0;
					break;
				}
				if (amount <= 0)
				{
					return false;
				}
				targetCost = Math.Max(-1, baseCost - amount);
				break;
		}

		if (targetCost == baseCost)
		{
			return false;
		}

		CardEditorOverrides.SetBaseStarCostUnsafe(card, targetCost);
		return true;
	}

	private static bool ApplyThisTurnCore(CardModel card, int amount, CardExtraEffectCostModifier modifier)
	{
		if (card == null)
		{
			return false;
		}

		switch (NormalizeModifier(amount, modifier))
		{
			case CardExtraEffectCostModifier.Free:
				return ApplyZeroThisTurn(card);
			case CardExtraEffectCostModifier.FreeToPlay:
				return ApplyTrueFreeThisTurn(card);
			case CardExtraEffectCostModifier.HalfCost:
				return TryApplyHalfCost(card, value => card.EnergyCost.SetThisTurn(value, reduceOnly: true));
			case CardExtraEffectCostModifier.Increase:
				if (amount <= 0)
				{
					return false;
				}
				card.EnergyCost.AddThisTurn(amount, reduceOnly: false);
				return true;
			default:
				if (amount == -1)
				{
					return ApplyZeroThisTurn(card);
				}
				if (amount <= 0)
				{
					return false;
				}
				card.EnergyCost.AddThisTurn(-amount, reduceOnly: true);
				return true;
		}
	}

	private static bool ApplyThisCombatCore(CardModel card, int amount, CardExtraEffectCostModifier modifier)
	{
		if (card == null)
		{
			return false;
		}

		switch (NormalizeModifier(amount, modifier))
		{
			case CardExtraEffectCostModifier.Free:
				return ApplyZeroThisCombat(card);
			case CardExtraEffectCostModifier.FreeToPlay:
				return ApplyTrueFreeThisCombat(card);
			case CardExtraEffectCostModifier.HalfCost:
				return TryApplyHalfCost(card, value => card.EnergyCost.SetThisCombat(value, reduceOnly: true));
			default:
				if (amount == -1)
				{
					return ApplyZeroThisCombat(card);
				}
				if (amount <= 0)
				{
					return false;
				}
				card.EnergyCost.AddThisCombat(-amount, reduceOnly: true);
				return true;
		}
	}

	private static bool ApplyUntilPlayedCore(CardModel card, int amount, CardExtraEffectCostModifier modifier)
	{
		if (card == null)
		{
			return false;
		}

		switch (NormalizeModifier(amount, modifier))
		{
			case CardExtraEffectCostModifier.Free:
				return ApplyZeroUntilPlayed(card);
			case CardExtraEffectCostModifier.FreeToPlay:
				return ApplyTrueFreeUntilPlayed(card);
			case CardExtraEffectCostModifier.HalfCost:
				return TryApplyHalfCost(card, value => card.EnergyCost.SetUntilPlayed(value, reduceOnly: true));
			default:
				if (amount == -1)
				{
					return ApplyZeroUntilPlayed(card);
				}
				if (amount <= 0)
				{
					return false;
				}
				card.EnergyCost.AddUntilPlayed(-amount, reduceOnly: true);
				return true;
		}
	}

	public static void ApplyForTurns(CombatState combatState, CardModel card, int amount, int turns, CardExtraEffectCostModifier modifier = CardExtraEffectCostModifier.Reduce, CardCreatedCardsCostResource resource = CardCreatedCardsCostResource.Energy)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		modifier = NormalizeModifier(amount, modifier);
		if ((modifier == CardExtraEffectCostModifier.Reduce && (amount <= 0 && amount != -1)) || turns <= 0)
		{
			return;
		}

		if (resource == CardCreatedCardsCostResource.Stars)
		{
			if (TryAddStarDiscount(card, BuildStarAdjustment(amount, modifier), turns, expireOnPlayed: false))
			{
				NotifyCostChanged(card, resource);
			}
			return;
		}

		ApplyThisTurn(card, amount, modifier);

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
		list.Add(new PendingTurnDiscount { Amount = amount, Modifier = modifier, RemainingTurns = remaining, CreatedRoundNumber = combatState.RoundNumber });
	}

	// Enqueue future-turn applications WITHOUT applying anything now — for callers that already
	// stamped the first turn themselves. ApplyForTurns always applies immediately, which made the
	// controller's stamped-then-scheduled discounts double-apply on the first turn.
	public static void ScheduleRemainingTurns(CombatState combatState, CardModel card, int amount, int remainingTurns, CardExtraEffectCostModifier modifier)
	{
		if (combatState == null || card == null || remainingTurns <= 0)
		{
			return;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.DiscountsByCard.TryGetValue(card, out List<PendingTurnDiscount>? list))
		{
			list = new List<PendingTurnDiscount>();
			schedule.DiscountsByCard[card] = list;
		}
		list.Add(new PendingTurnDiscount { Amount = amount, Modifier = modifier, RemainingTurns = remainingTurns, CreatedRoundNumber = combatState.RoundNumber });
	}

	public static void ApplyThisTurn(CardModel card, int amount, CardExtraEffectCostModifier modifier = CardExtraEffectCostModifier.Reduce, CardCreatedCardsCostResource resource = CardCreatedCardsCostResource.Energy)
	{
		if (resource == CardCreatedCardsCostResource.Stars)
		{
			if (TryAddStarDiscount(card, BuildStarAdjustment(amount, modifier), remainingTurns: 1, expireOnPlayed: false))
			{
				NotifyCostChanged(card, resource);
			}
			return;
		}

		if (ApplyThisTurnCore(card, amount, modifier))
		{
			NotifyCostChanged(card, resource);
		}
	}

	public static void ApplyThisCombat(CardModel card, int amount, CardExtraEffectCostModifier modifier = CardExtraEffectCostModifier.Reduce, CardCreatedCardsCostResource resource = CardCreatedCardsCostResource.Energy)
	{
		if (resource == CardCreatedCardsCostResource.Stars)
		{
			if (TryAddStarDiscount(card, BuildStarAdjustment(amount, modifier), remainingTurns: 0, expireOnPlayed: false))
			{
				NotifyCostChanged(card, resource);
			}
			return;
		}

		if (ApplyThisCombatCore(card, amount, modifier))
		{
			NotifyCostChanged(card, resource);
		}
	}

	public static void ApplyUntilPlayed(CardModel card, int amount, CardExtraEffectCostModifier modifier = CardExtraEffectCostModifier.Reduce, CardCreatedCardsCostResource resource = CardCreatedCardsCostResource.Energy)
	{
		if (resource == CardCreatedCardsCostResource.Stars)
		{
			if (TryAddStarDiscount(card, BuildStarAdjustment(amount, modifier), remainingTurns: 0, expireOnPlayed: true))
			{
				NotifyCostChanged(card, resource);
			}
			return;
		}

		if (ApplyUntilPlayedCore(card, amount, modifier))
		{
			NotifyCostChanged(card, resource);
		}
	}

	public static void ApplyPermanent(CardModel card, int amount, CardExtraEffectCostModifier modifier = CardExtraEffectCostModifier.Reduce, CardCreatedCardsCostResource resource = CardCreatedCardsCostResource.Energy)
	{
		modifier = NormalizeModifier(amount, modifier);
		if (resource == CardCreatedCardsCostResource.Stars)
		{
			if (ApplyPermanentStarCost(card, amount, modifier))
			{
				NotifyCostChanged(card, resource);
			}
			return;
		}

		if (ApplyPermanentEnergyCost(card, amount, modifier))
		{
			NotifyCostChanged(card, resource);
		}
	}

	public static CardExtraEffect.CardCostAdjustment GetStarCostAdjustment(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return default;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || !schedule.StarDiscountsByCard.TryGetValue(card, out List<PendingStarDiscount>? discounts) || discounts.Count == 0)
		{
			return default;
		}

		CardExtraEffect.CardCostAdjustment combined = default;
		for (int i = 0; i < discounts.Count; i++)
		{
			PendingStarDiscount? pending = discounts[i];
			if (pending == null)
			{
				continue;
			}
			combined = CardExtraEffect.CardCostAdjustment.Combine(combined, pending.Adjustment);
		}
		return combined;
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

				// Skip entries created earlier in this same round (opening-draw stamps run before
				// AfterPlayerTurnStart): consuming them now double-applies on turn one and ends
				// the span a turn early. Known edge: co-op extra turns reuse the round number, so
				// an entry created on a normal turn also skips a same-round EXTRA turn's start —
				// the discount then resumes next round. Accepted as the lesser evil vs the
				// turn-one double-apply this guard exists to prevent.
				if (pending.CreatedRoundNumber == combatState.RoundNumber)
				{
					continue;
				}

				if (ApplyThisTurnCore(card, pending.Amount, pending.Modifier))
				{
					changed = true;
				}
				pending.RemainingTurns--;

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
				NotifyCostChanged(card, CardCreatedCardsCostResource.Energy);
			}
		}

		foreach ((CardModel card, List<PendingStarDiscount> discounts) in schedule.StarDiscountsByCard.ToList())
		{
			if (card == null || discounts == null || discounts.Count == 0)
			{
				schedule.StarDiscountsByCard.Remove(card);
				continue;
			}
			if (card.Owner != player || card.HasBeenRemovedFromState)
			{
				schedule.StarDiscountsByCard.Remove(card);
				NotifyCostChanged(card, CardCreatedCardsCostResource.Stars);
				continue;
			}

			bool changed = false;
			for (int i = discounts.Count - 1; i >= 0; i--)
			{
				PendingStarDiscount pending = discounts[i];
				if (pending == null)
				{
					discounts.RemoveAt(i);
					continue;
				}
				if (pending.RemainingTurns <= 0)
				{
					continue;
				}

				pending.RemainingTurns--;
				if (pending.RemainingTurns <= 0)
				{
					discounts.RemoveAt(i);
					changed = true;
				}
			}

			if (discounts.Count == 0)
			{
				schedule.StarDiscountsByCard.Remove(card);
			}

			if (changed)
			{
				NotifyCostChanged(card, CardCreatedCardsCostResource.Stars);
			}
		}

		if (schedule.DiscountsByCard.Count == 0 && schedule.StarDiscountsByCard.Count == 0)
		{
			_schedules.Remove(combatState);
		}
	}

	public static void OnCardPlayStarted(CardModel card)
	{
		if (card == null)
		{
			return;
		}

		CombatState? combatState = card.GetConcreteCombatState() ?? card.TryGetOwnerCreature().GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || !schedule.StarDiscountsByCard.TryGetValue(card, out List<PendingStarDiscount>? discounts) || discounts.Count == 0)
		{
			return;
		}

		bool changed = false;
		for (int i = discounts.Count - 1; i >= 0; i--)
		{
			PendingStarDiscount pending = discounts[i];
			if (pending == null || pending.ExpireOnPlayed)
			{
				discounts.RemoveAt(i);
				changed = true;
			}
		}

		if (discounts.Count == 0)
		{
			schedule.StarDiscountsByCard.Remove(card);
		}
		if (changed)
		{
			NotifyCostChanged(card, CardCreatedCardsCostResource.Stars);
		}
		if (schedule.DiscountsByCard.Count == 0 && schedule.StarDiscountsByCard.Count == 0)
		{
			_schedules.Remove(combatState);
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

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class CombatHistory_CardPlayStarted_CreatedCardsStarCost_Patch
{
	public static void Prefix(CardPlay cardPlay)
	{
		CardEditorCreatedCardsCostController.OnCardPlayStarted(cardPlay?.Card);
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
	[HarmonyPriority(Priority.Last)]
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

		CardModel? sourceCard = CardEditorGeneratedCardSourceResolver.ResolveSourceCard();
		if (sourceCard == null)
		{
			return;
		}

		CombatState? combatState = sourceCard.GetConcreteCombatState() ?? list[0].TryGetOwnerCreature().GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}

		IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetRuntimeEffectsForExecution(combatState, sourceCard);
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
				CardExtraEffectCostModifier modifier = CardEditorExtraEffects.GetEffectiveCardCostsLessModifier(effect);
				CardCreatedCardsCostResource resource = effect.CreatedCardsCostResource;
				switch (effect.CreatedCardsCostDuration)
				{
					case CardCreatedCardsCostDuration.Permanent:
						CardEditorCreatedCardsCostController.ApplyPermanent(card, effect.Amount, modifier, resource);
						break;
					case CardCreatedCardsCostDuration.ThisCombat:
						CardEditorCreatedCardsCostController.ApplyThisCombat(card, effect.Amount, modifier, resource);
						break;
					case CardCreatedCardsCostDuration.UntilPlayed:
						CardEditorCreatedCardsCostController.ApplyUntilPlayed(card, effect.Amount, modifier, resource);
						break;
					case CardCreatedCardsCostDuration.Turns:
					{
						int turns = effect.CreatedCardsCostTurns <= 0 ? 1 : effect.CreatedCardsCostTurns;
						CardEditorCreatedCardsCostController.ApplyForTurns(combatState, card, effect.Amount, turns, modifier, resource);
						break;
					}
					default:
						CardEditorCreatedCardsCostController.ApplyThisTurn(card, effect.Amount, modifier, resource);
						break;
				}
			}
		}
	}
}
