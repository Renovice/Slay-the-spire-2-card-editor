using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
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
			CardModel card => card.TryGetOwnerCreature().GetConcreteCombatState(),
			Creature => null,
			PowerModel power => power.TryGetOwner().GetConcreteCombatState(),
			_ => null
		};
	}

	internal static Player? TryGetOwner(this CardModel? card)
	{
		if (card == null || !card.IsMutable)
		{
			return null;
		}

		try
		{
			return card.Owner;
		}
		catch
		{
			return null;
		}
	}

	internal static Creature? TryGetOwnerCreature(this CardModel? card)
	{
		return card.TryGetOwner()?.Creature;
	}

	internal static CardPile? TryGetPile(this CardModel? card)
	{
		if (card == null)
		{
			return null;
		}

		try
		{
			return card.Pile;
		}
		catch
		{
			return null;
		}
	}

	internal static Creature? TryGetOwner(this PowerModel? power)
	{
		if (power == null || !power.IsMutable)
		{
			return null;
		}

		try
		{
			return power.Owner;
		}
		catch
		{
			return null;
		}
	}

	internal static Creature? GetSafeCurrentTarget(this CardModel? card)
	{
		if (card == null)
		{
			return null;
		}

		try
		{
			return card.CurrentTarget;
		}
		catch
		{
			return null;
		}
	}

	internal static int GetSafeCurrentUpgradeLevel(this CardModel? card)
	{
		if (card == null)
		{
			return 0;
		}

		try
		{
			return Math.Max(0, card.CurrentUpgradeLevel);
		}
		catch
		{
			return 0;
		}
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

internal static class CardEditorCardVisualPreviewContext
{
	private readonly struct Entry
	{
		internal Entry(CardModel? card, PileType pileType, CardPreviewMode previewMode)
		{
			Card = card;
			PileType = pileType;
			PreviewMode = previewMode;
		}

		internal CardModel? Card { get; }
		internal PileType PileType { get; }
		internal CardPreviewMode PreviewMode { get; }
	}

	private sealed class Scope : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			Stack<Entry>? stack = _stack;
			if (stack == null || stack.Count == 0)
			{
				return;
			}

			stack.Pop();
		}
	}

	[ThreadStatic]
	private static Stack<Entry>? _stack;

	internal static IDisposable Push(CardModel? card, PileType pileType, CardPreviewMode previewMode)
	{
		_stack ??= new Stack<Entry>();
		_stack.Push(new Entry(card, pileType, previewMode));
		return new Scope();
	}

	internal static CardPreviewMode GetPreviewMode(CardModel? card)
	{
		Stack<Entry>? stack = _stack;
		if (stack == null || stack.Count == 0)
		{
			return CardPreviewMode.Normal;
		}

		if (card != null)
		{
			foreach (Entry entry in stack)
			{
				if (ReferenceEquals(entry.Card, card))
				{
					return entry.PreviewMode;
				}
			}
		}

		return stack.Peek().PreviewMode;
	}
}
