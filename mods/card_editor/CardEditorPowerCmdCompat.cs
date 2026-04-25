using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPowerCmdCompat
{
	private static PlayerChoiceContext FallbackContext()
	{
		return new ThrowingPlayerChoiceContext();
	}

	internal static Task<IReadOnlyList<T>> Apply<T>(IEnumerable<Creature> targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		return PowerCmd.Apply<T>(FallbackContext(), targets, amount, applier, cardSource, silent);
	}

	internal static Task<T?> Apply<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		return PowerCmd.Apply<T>(FallbackContext(), target, amount, applier, cardSource, silent);
	}

	internal static Task Apply(PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
	{
		return PowerCmd.Apply(FallbackContext(), power, target, amount, applier, cardSource, silent);
	}

	internal static Task<int> ModifyAmount(PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
	{
		return PowerCmd.ModifyAmount(FallbackContext(), power, offset, applier, cardSource, silent);
	}

	internal static Task Remove(PowerModel? power)
	{
		return PowerCmd.Remove(power);
	}

	internal static Task Remove<T>(Creature creature)
		where T : PowerModel
	{
		return PowerCmd.Remove<T>(creature);
	}
}
