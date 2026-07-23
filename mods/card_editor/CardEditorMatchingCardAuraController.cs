using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorMatchingCardAuraController
{
	private enum AuraKind
	{
		ExtraEffect = 0,
		Keyword = 1,
		Replay = 2
	}

	private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
	{
		public static readonly ReferenceComparer<T> Instance = new();

		public bool Equals(T? x, T? y)
		{
			return ReferenceEquals(x, y);
		}

		public int GetHashCode(T obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}

	private sealed class AuraGrant
	{
		public required AuraKind Kind { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public CardModel? SourceCard { get; init; }
		public int ReplayAmount { get; init; }
		public CardExtraEffectCardGrantDuration AuraDuration { get; init; }
		public int RemainingTurns { get; set; }
		public HashSet<CardModel> AppliedCards { get; } = new HashSet<CardModel>(ReferenceComparer<CardModel>.Instance);
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
		CardEditorRuntimeCacheVersion.Bump();
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

				if (grant.AuraDuration is CardExtraEffectCardGrantDuration.ThisTurn or CardExtraEffectCardGrantDuration.Turns)
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

		CardEditorRuntimeCacheVersion.Bump();
	}

	public static void OnAfterCardPlayed(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return;
		}

		foreach (PlayerState state in schedule.States.Values)
		{
			if (state?.Grants == null || state.Grants.Count == 0)
			{
				continue;
			}

			for (int i = 0; i < state.Grants.Count; i++)
			{
				AuraGrant? grant = state.Grants[i];
				if (grant == null || grant.Effect.CardGrantDuration != CardExtraEffectCardGrantDuration.UntilPlayed)
				{
					continue;
				}

				grant.AppliedCards.Remove(card);
			}
		}

		CardEditorRuntimeCacheVersion.Bump();
	}

	public static void ApplyExtraEffectAura(CombatState combatState, Player owner, CardExtraEffect effect, CardModel? sourceCard, IReadOnlyList<CardModel>? currentCards)
	{
		if (combatState == null || owner == null || effect == null)
		{
			return;
		}

		AuraGrant grant = CreateGrant(AuraKind.ExtraEffect, effect, sourceCard, replayAmount: 0);
		StoreGrant(combatState, owner, grant);
		ApplyToCurrentCards(combatState, owner, grant, currentCards);
	}

	public static void ApplyKeywordAura(CombatState combatState, Player owner, CardExtraEffect effect, CardModel? sourceCard, IReadOnlyList<CardModel>? currentCards)
	{
		if (combatState == null || owner == null || effect == null)
		{
			return;
		}

		AuraGrant grant = CreateGrant(AuraKind.Keyword, effect, sourceCard, replayAmount: 0);
		StoreGrant(combatState, owner, grant);
		ApplyToCurrentCards(combatState, owner, grant, currentCards);
	}

	public static void ApplyReplayAura(CombatState combatState, Player owner, CardExtraEffect effect, CardModel? sourceCard, IReadOnlyList<CardModel>? currentCards, int replayAmount)
	{
		if (combatState == null || owner == null || effect == null || replayAmount == 0)
		{
			return;
		}

		AuraGrant grant = CreateGrant(AuraKind.Replay, effect, sourceCard, replayAmount);
		StoreGrant(combatState, owner, grant);
		ApplyToCurrentCards(combatState, owner, grant, currentCards);
	}

	public static IReadOnlyList<CardExtraEffect> GetVirtualExtraEffects(CombatState combatState, CardModel card)
	{
		if (combatState == null || card?.Owner == null)
		{
			return Array.Empty<CardExtraEffect>();
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return Array.Empty<CardExtraEffect>();
		}
		if (!schedule.States.TryGetValue(card.Owner, out PlayerState? state) || state.Grants.Count == 0)
		{
			return Array.Empty<CardExtraEffect>();
		}

		List<CardExtraEffect>? effects = null;
		foreach (AuraGrant? grant in state.Grants)
		{
			if (grant == null || grant.Kind != AuraKind.ExtraEffect)
			{
				continue;
			}
			if (!CardMatchesGrant(card.Owner, grant, card, requireMutable: false))
			{
				continue;
			}
			if (WasGrantAlreadyApplied(grant, card))
			{
				continue;
			}

			CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(grant.Effect);
			stored.GrantToCard = false;
			effects ??= new List<CardExtraEffect>();
			if (effects.Any(existing => CardEditorExtraEffects.IsDuplicateGrantedEffect(existing, stored)))
			{
				continue;
			}
			effects.Add(stored);
		}

		return effects != null ? effects : Array.Empty<CardExtraEffect>();
	}

	internal static void OnCardGenerated(CombatState combatState, CardModel card)
	{
		if (combatState == null || card?.Owner == null)
		{
			return;
		}

		TryApplyAurasToCard(combatState, card.Owner, card);
	}

	internal static void OnCardChangedPiles(CombatState combatState, CardModel card)
	{
		if (combatState == null || card?.Owner == null)
		{
			return;
		}

		PileType pileType = card.Pile?.Type ?? PileType.None;
		if (pileType is PileType.None or PileType.Play)
		{
			return;
		}

		TryApplyAurasToCard(combatState, card.Owner, card);
	}

	private static AuraGrant CreateGrant(AuraKind kind, CardExtraEffect effect, CardModel? sourceCard, int replayAmount)
	{
		CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(effect);
		CardExtraEffectCardGrantDuration auraDuration = stored.CardGrantDuration == CardExtraEffectCardGrantDuration.Turns
			? CardExtraEffectCardGrantDuration.Turns
			: stored.CardGrantDuration == CardExtraEffectCardGrantDuration.ThisTurn
				? CardExtraEffectCardGrantDuration.ThisTurn
				: CardExtraEffectCardGrantDuration.ThisCombat;

		int remainingTurns = auraDuration switch
		{
			CardExtraEffectCardGrantDuration.ThisTurn => 1,
			CardExtraEffectCardGrantDuration.Turns => Math.Clamp(stored.CardGrantTurns, 1, 99),
			_ => 0
		};

		return new AuraGrant
		{
			Kind = kind,
			Effect = stored,
			SourceCard = sourceCard,
			ReplayAmount = replayAmount,
			AuraDuration = auraDuration,
			RemainingTurns = remainingTurns
		};
	}

	private static void StoreGrant(CombatState combatState, Player owner, AuraGrant grant)
	{
		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.States.TryGetValue(owner, out PlayerState? state))
		{
			state = new PlayerState();
			schedule.States[owner] = state;
		}

		foreach (AuraGrant existing in state.Grants)
		{
			if (existing == null
				|| existing.Kind != grant.Kind
				|| !CardEditorExtraEffects.IsDuplicateGrantedEffect(existing.Effect, grant.Effect))
			{
				continue;
			}

			if (grant.RemainingTurns > 0)
			{
				existing.RemainingTurns = Math.Max(existing.RemainingTurns, grant.RemainingTurns);
			}
			CardEditorRuntimeCacheVersion.Bump();
			return;
		}

		state.Grants.Add(grant);
		CardEditorRuntimeCacheVersion.Bump();
	}

	private static void ApplyToCurrentCards(CombatState combatState, Player owner, AuraGrant grant, IReadOnlyList<CardModel>? currentCards)
	{
		if (currentCards == null || currentCards.Count == 0)
		{
			return;
		}

		for (int i = 0; i < currentCards.Count; i++)
		{
			CardModel? card = currentCards[i];
			if (card == null)
			{
				continue;
			}

			TryApplyGrantToCard(combatState, owner, grant, card);
		}
	}

	private static void TryApplyAurasToCard(CombatState combatState, Player owner, CardModel card)
	{
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return;
		}
		if (!schedule.States.TryGetValue(owner, out PlayerState? state) || state.Grants.Count == 0)
		{
			return;
		}

		for (int i = 0; i < state.Grants.Count; i++)
		{
			TryApplyGrantToCard(combatState, owner, state.Grants[i], card);
		}
	}

	private static void TryApplyGrantToCard(CombatState combatState, Player owner, AuraGrant grant, CardModel card)
	{
		if (combatState == null || owner == null || grant == null || card == null)
		{
			return;
		}
		if (!CardMatchesGrant(owner, grant, card, requireMutable: true))
		{
			return;
		}
		if (WasGrantAlreadyApplied(grant, card))
		{
			return;
		}

		switch (grant.Kind)
		{
			case AuraKind.Keyword:
				ApplyKeywordGrant(combatState, card, grant);
				break;
			case AuraKind.Replay:
				ApplyReplayGrant(combatState, card, grant);
				break;
			default:
				ApplyExtraEffectGrant(combatState, card, grant);
				break;
		}

		grant.AppliedCards.Add(card);
		CardEditorRuntimeCacheVersion.Bump();
		RefreshCardVisuals(card);
	}

	private static bool CardMatchesGrant(Player owner, AuraGrant grant, CardModel card, bool requireMutable)
	{
		if (owner == null || grant == null || card == null)
		{
			return false;
		}
		if (requireMutable && !card.IsMutable)
		{
			return false;
		}
		if (!PileMatches(grant.Effect.CardSelectionPile, card.Pile?.Type ?? PileType.None))
		{
			return false;
		}
		if (!grant.Effect.IncludeSourceCardInSelection && grant.SourceCard != null && ReferenceEquals(card, grant.SourceCard))
		{
			return false;
		}
		if (!CardEditorExtraEffects.MatchesCardSelectionFilters(owner, card, grant.Effect, includeCostFilter: false))
		{
			return false;
		}

		return true;
	}

	private static bool WasGrantAlreadyApplied(AuraGrant grant, CardModel card)
	{
		if (grant == null || card == null)
		{
			return false;
		}
		if (grant.AppliedCards.Contains(card))
		{
			return true;
		}

		CardModel? cursor = card.CloneOf;
		for (int i = 0; i < 8 && cursor != null; i++)
		{
			if (grant.AppliedCards.Contains(cursor))
			{
				return true;
			}
			cursor = cursor.CloneOf;
		}

		return false;
	}

	private static void ApplyKeywordGrant(CombatState combatState, CardModel card, AuraGrant grant)
	{
		switch (GetEffectiveGrantDuration(grant))
		{
			case CardExtraEffectCardGrantDuration.ThisTurn:
			case CardExtraEffectCardGrantDuration.Turns:
				CardEditorTemporaryKeywordController.ApplyForTurns(combatState, card, grant.Effect.GrantedKeyword, GetEffectiveGrantTurns(grant));
				break;
			case CardExtraEffectCardGrantDuration.UntilPlayed:
				CardEditorTemporaryKeywordController.ApplyUntilPlayed(combatState, card, grant.Effect.GrantedKeyword);
				break;
			default:
				CardEditorTemporaryKeywordController.ApplyThisCombat(combatState, card, grant.Effect.GrantedKeyword);
				break;
		}
	}

	private static void ApplyReplayGrant(CombatState combatState, CardModel card, AuraGrant grant)
	{
		CardEditorTemporaryReplayController.Apply(
			combatState,
			card,
			grant.ReplayAmount,
			GetEffectiveGrantDuration(grant),
			GetEffectiveGrantTurns(grant));
	}

	private static void ApplyExtraEffectGrant(CombatState combatState, CardModel card, AuraGrant grant)
	{
		CardEditorTemporaryExtraEffectController.Grant(
			combatState,
			card,
			grant.Effect,
			GetEffectiveGrantDuration(grant),
			GetEffectiveGrantTurns(grant));
	}

	private static CardExtraEffectCardGrantDuration GetEffectiveGrantDuration(AuraGrant grant)
	{
		if (grant == null)
		{
			return CardExtraEffectCardGrantDuration.ThisTurn;
		}

		if (grant.Effect.CardGrantDuration == CardExtraEffectCardGrantDuration.Turns && grant.RemainingTurns <= 1)
		{
			return CardExtraEffectCardGrantDuration.ThisTurn;
		}

		if (grant.Effect.CardGrantDuration == CardExtraEffectCardGrantDuration.ThisTurn && grant.RemainingTurns <= 1)
		{
			return CardExtraEffectCardGrantDuration.ThisTurn;
		}

		return grant.Effect.CardGrantDuration;
	}

	private static int GetEffectiveGrantTurns(AuraGrant grant)
	{
		if (grant == null)
		{
			return 1;
		}

		return grant.Effect.CardGrantDuration == CardExtraEffectCardGrantDuration.Turns
			? Math.Clamp(grant.RemainingTurns, 1, 99)
			: Math.Clamp(grant.Effect.CardGrantTurns, 1, 99);
	}

	private static bool PileMatches(CardExtraEffectCardPile selection, PileType pileType)
	{
		if (selection == CardExtraEffectCardPile.AllPiles)
		{
			return pileType is PileType.Hand or PileType.Draw or PileType.Discard or PileType.Exhaust;
		}

		return selection switch
		{
			CardExtraEffectCardPile.Hand => pileType == PileType.Hand,
			CardExtraEffectCardPile.DrawPile => pileType == PileType.Draw,
			CardExtraEffectCardPile.DiscardPile => pileType == PileType.Discard,
			CardExtraEffectCardPile.ExhaustPile => pileType == PileType.Exhaust,
			CardExtraEffectCardPile.Deck => pileType == PileType.Deck,
			_ => false
		};
	}

	private static void RefreshCardVisuals(CardModel card)
	{
		try
		{
			card.InvokeEnergyCostChanged();

			NCard? node = NCard.FindOnTable(card);
			if (node != null)
			{
				node.UpdateVisuals(node.DisplayingPile, CardPreviewMode.Normal);
			}
		}
		catch
		{
			// ignored
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class Hook_AfterCardGeneratedForCombat_MatchingCardAura_Patch
{
	public static void Postfix(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		CardEditorMatchingCardAuraController.OnCardGenerated(combatState, card);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class Hook_AfterCardChangedPiles_MatchingCardAura_Patch
{
	public static void Postfix(CombatState? combatState, CardModel card, PileType oldPile)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		CardEditorMatchingCardAuraController.OnCardChangedPiles(combatState, card);
	}
}
