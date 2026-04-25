using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCombatStateCompat
{
	internal static CombatState? AsCombatState(this object? state)
	{
		return state as CombatState;
	}

	internal static CombatState? GetConcreteCombatState(this CardModel? card)
	{
		return card?.CombatState.AsCombatState() ?? card?.Owner?.Creature?.CombatState.AsCombatState();
	}
}
