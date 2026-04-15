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
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorUpgradeAuraController
{
	private sealed class UpgradeGrant
	{
		public required CardExtraEffect Effect { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class PlayerState
	{
		public List<UpgradeGrant> Grants { get; } = new List<UpgradeGrant>();
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
				UpgradeGrant grant = state.Grants[i];
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
		if (effect.Kind is not (CardExtraEffectKind.GeneratedCardsUpgraded or CardExtraEffectKind.CardsInPileUpgradedAura))
		{
			return;
		}
		if (!CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
		{
			return;
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
			remainingTurns = 1;
		}
		else if (duration == CardExtraEffectCardGrantDuration.Turns)
		{
			remainingTurns = Math.Clamp(effect.CardCostsLessTurns, 1, 99);
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.States.TryGetValue(owner, out PlayerState? state))
		{
			state = new PlayerState();
			schedule.States[owner] = state;
		}

		CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(effect);
		state.Grants.Add(new UpgradeGrant
		{
			Effect = stored,
			Duration = duration,
			RemainingTurns = remainingTurns
		});

		if (stored.Kind == CardExtraEffectKind.CardsInPileUpgradedAura)
		{
			try
			{
				UpgradeExistingCardsInPiles(owner, stored);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Upgrade aura (pile) failed applying to existing cards: {ex}");
			}
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

		for (int i = 0; i < state.Grants.Count; i++)
		{
			CardExtraEffect? effect = state.Grants[i]?.Effect;
			if (effect == null || effect.Kind != CardExtraEffectKind.GeneratedCardsUpgraded)
			{
				continue;
			}

			if (!CardEditorExtraEffects.MatchesAffectedCardFilters(owner, card, effect))
			{
				continue;
			}

			UpgradeCard(card, effect.Amount);
		}
	}

	internal static void OnCardChangedPiles(CombatState combatState, CardModel card)
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

		PileType pileType = card.Pile?.Type ?? PileType.None;
		if (pileType is PileType.None or PileType.Play)
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

		for (int i = 0; i < state.Grants.Count; i++)
		{
			CardExtraEffect? effect = state.Grants[i]?.Effect;
			if (effect == null || effect.Kind != CardExtraEffectKind.CardsInPileUpgradedAura)
			{
				continue;
			}

			if (!PileMatches(effect.CardSelectionPile, pileType))
			{
				continue;
			}

			if (!CardEditorExtraEffects.MatchesAffectedCardFilters(owner, card, effect))
			{
				continue;
			}

			UpgradeCard(card, effect.Amount);
		}
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
			_ => false
		};
	}

	private static void UpgradeExistingCardsInPiles(Player owner, CardExtraEffect effect)
	{
		if (owner == null || effect == null)
		{
			return;
		}

		foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
		{
			if (!PileMatches(effect.CardSelectionPile, pileType))
			{
				continue;
			}

			CardPile? pile = pileType.GetPile(owner);
			if (pile == null)
			{
				continue;
			}

			foreach (CardModel card in pile.Cards)
			{
				if (card == null)
				{
					continue;
				}
				if (!CardEditorExtraEffects.MatchesAffectedCardFilters(owner, card, effect))
				{
					continue;
				}
				UpgradeCard(card, effect.Amount);
			}
		}
	}

	private static void UpgradeCard(CardModel card, int rawTimes)
	{
		if (card == null)
		{
			return;
		}

		int times = Math.Clamp(rawTimes, 1, 99);
		for (int i = 0; i < times && card.IsUpgradable; i++)
		{
			CardCmd.Upgrade(card, CardPreviewStyle.None);
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class Hook_AfterCardGeneratedForCombat_GeneratedCardsUpgraded_Patch
{
	public static void Postfix(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		CardEditorUpgradeAuraController.OnCardGenerated(combatState, card);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class Hook_AfterCardChangedPiles_CardsInPileUpgradedAura_Patch
{
	public static void Postfix(CombatState? combatState, CardModel card, PileType oldPile)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		CardEditorUpgradeAuraController.OnCardChangedPiles(combatState, card);
	}
}

