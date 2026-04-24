using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
internal static class Hook_AfterCardChangedPiles_CardEditorMovedPileTriggers_Patch
{
	public static void Postfix(CombatState? combatState, CardModel card, PileType oldPile)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides && !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}
		if (!CardEditorExtraEffects.IsCardAtTopOfPile(card) && !CardEditorExtraEffects.IsCardAtBottomOfPile(card))
		{
			return;
		}

		TaskHelper.RunSafely(RunAfter(combatState, card));
	}

	private static async Task RunAfter(CombatState combatState, CardModel card)
	{
		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		Player? owner = card.Owner;
		if (owner == null)
		{
			return;
		}

		try
		{
			bool isTop = CardEditorExtraEffects.IsCardAtTopOfPile(card);
			bool isBottom = CardEditorExtraEffects.IsCardAtBottomOfPile(card);

			if (isTop)
			{
				HookPlayerChoiceContext topChoiceContext = new HookPlayerChoiceContext(owner, netId.Value, GameActionType.Combat);
				Task topTask = CardEditorExtraEffects.RunAfterCardMovedToTopOfPile(combatState, topChoiceContext, card);
				bool topCompleted = await topChoiceContext.AssignTaskAndWaitForPauseOrCompletion(topTask);
				if (!topCompleted && topChoiceContext.GameAction != null)
				{
					await topChoiceContext.GameAction.CompletionTask;
				}
			}

			if (isBottom)
			{
				HookPlayerChoiceContext bottomChoiceContext = new HookPlayerChoiceContext(owner, netId.Value, GameActionType.Combat);
				Task bottomTask = CardEditorExtraEffects.RunAfterCardMovedToBottomOfPile(combatState, bottomChoiceContext, card);
				bool bottomCompleted = await bottomChoiceContext.AssignTaskAndWaitForPauseOrCompletion(bottomTask);
				if (!bottomCompleted && bottomChoiceContext.GameAction != null)
				{
					await bottomChoiceContext.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterCardChangedPiles moved-trigger extra effects failed: {ex}");
		}
	}
}
