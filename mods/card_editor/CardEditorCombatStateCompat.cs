using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCombatStateCompat
{
	private static readonly ConcurrentDictionary<Type, PropertyInfo?> CombatStatePropertyCache = new();

	internal static CombatState? AsCombatState(this object? state)
	{
		return state as CombatState;
	}

	internal static CombatState? GetConcreteCombatState(this object? source)
	{
		if (source == null)
		{
			return null;
		}

		if (source is CombatState combatState)
		{
			return combatState;
		}

		CombatState? reflected = TryGetReflectedCombatState(source);
		if (reflected != null)
		{
			return reflected;
		}

		return source switch
		{
			CardModel card => card.Owner?.Creature.GetConcreteCombatState(),
			Creature => null,
			PowerModel power => power.Owner.GetConcreteCombatState(),
			_ => null
		};
	}

	private static CombatState? TryGetReflectedCombatState(object source)
	{
		try
		{
			PropertyInfo? property = CombatStatePropertyCache.GetOrAdd(
				source.GetType(),
				static type => type.GetProperty("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			return property?.GetValue(source) as CombatState;
		}
		catch
		{
			return null;
		}
	}
}
