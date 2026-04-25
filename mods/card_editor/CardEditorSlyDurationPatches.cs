using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ApplySingleTurnSly))]
internal static class CardCmd_ApplySingleTurnSly_DurationOverride_Patch
{
	public static bool Prefix(CardModel card)
	{
		if (card == null)
		{
			return true;
		}

		CardModel? sourceCard = CardEditorCardPlayContext.Current?.Card;
		if (sourceCard == null && CardEditorHookModelContext.Current is CardModel hookCard)
		{
			sourceCard = hookCard;
		}
		if (sourceCard == null)
		{
			return true;
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(sourceCard.Id, out CardOverride draft))
		{
			overrideData = draft;
		}
		else if (CardEditorOverrides.TryGet(sourceCard.Id, out CardOverride stored))
		{
			overrideData = stored;
		}

		if (overrideData?.SlyGrantDuration == null)
		{
			return true;
		}

		CardKeywordGrantDuration duration = overrideData.SlyGrantDuration.Value;
		if (duration == CardKeywordGrantDuration.ThisTurn)
		{
			return true;
		}

		CombatState? combatState = sourceCard.CombatState.AsCombatState() ?? card.CombatState.AsCombatState() ?? card.Owner?.Creature?.CombatState.AsCombatState();
		if (combatState == null)
		{
			return true;
		}

		switch (duration)
		{
			case CardKeywordGrantDuration.ThisCombat:
				CardEditorTemporaryKeywordController.ApplyThisCombat(combatState, card, CardKeyword.Sly);
				return false;
			case CardKeywordGrantDuration.Turns:
			{
				int turns = overrideData.SlyGrantTurns.GetValueOrDefault(2);
				if (turns < 1)
				{
					turns = 1;
				}
				CardEditorTemporaryKeywordController.ApplyForTurns(combatState, card, CardKeyword.Sly, turns);
				return false;
			}
			default:
				return true;
		}
	}
}

