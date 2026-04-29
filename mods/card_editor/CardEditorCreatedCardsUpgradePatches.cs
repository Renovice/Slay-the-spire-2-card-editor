using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardsToCombat))]
internal static class CardPileCmd_AddGeneratedCardsToCombat_CreatedCardsUpgraded_Patch
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

		CombatState? combatState = sourceCard.CombatState.AsCombatState() ?? list[0].Owner?.Creature?.CombatState.AsCombatState();
		IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetRuntimeEffectsForExecution(combatState, sourceCard);
		if (effects == null || effects.Count == 0)
		{
			return;
		}

		List<CardExtraEffect>? upgradeEffects = null;
		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null
				|| effect.Kind != CardExtraEffectKind.CreatedCardsUpgraded
				|| effect.Trigger != CardExtraEffectTrigger.OnPlay
				|| !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
			{
				continue;
			}
			(upgradeEffects ??= new List<CardExtraEffect>()).Add(effect);
		}
		if (upgradeEffects == null || upgradeEffects.Count == 0)
		{
			return;
		}

		foreach (CardModel card in list)
		{
			if (card == null)
			{
				continue;
			}

			foreach (CardExtraEffect effect in upgradeEffects)
			{
				Player? owner = card.Owner;
				if (owner != null && !CardEditorExtraEffects.MatchesAffectedCardFilters(owner, card, effect))
				{
					continue;
				}

				int times = Math.Clamp(effect.Amount, 1, 99);
				for (int i = 0; i < times && card.IsUpgradable; i++)
				{
					CardCmd.Upgrade(card, CardPreviewStyle.None);
				}
			}
		}
	}
}
