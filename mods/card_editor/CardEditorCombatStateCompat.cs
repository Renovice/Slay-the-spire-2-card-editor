using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCombatStateCompat
{
	// Compiled getters/invokers replace per-call PropertyInfo.GetValue / MethodInfo.Invoke
	// reflection on these hot paths. Built once per type; a reflection closure is used as
	// fallback if expression compilation is unavailable. Same values, same exceptions
	// swallowed by the existing try/catch wrappers.
	private static readonly ConcurrentDictionary<Type, Func<object, CombatState?>?> CombatStateGetterCache = new();
	private static readonly ConcurrentDictionary<Type, Func<object, object?, object?>?> IterateHookListenersCache = new();
	private static readonly Lazy<ConstructorInfo?> HookChoiceSourceCtor = new(FindHookChoiceSourceCtor);
	private static readonly Lazy<ConstructorInfo?> HookChoicePlayerCtor = new(FindHookChoicePlayerCtor);

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

		try
		{
			return source switch
			{
				CardModel card => card.TryGetOwnerCreature().GetConcreteCombatState(),
				Creature => null,
				PowerModel power => power.TryGetOwner().GetConcreteCombatState(),
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	internal static HookPlayerChoiceContext CreateHookPlayerChoiceContext(AbstractModel source, ulong netId, CombatState combatState, GameActionType gameActionType)
	{
		if (source == null)
		{
			throw new ArgumentNullException(nameof(source));
		}
		if (combatState == null)
		{
			throw new ArgumentNullException(nameof(combatState));
		}

		ConstructorInfo? ctor = HookChoiceSourceCtor.Value;
		if (ctor != null)
		{
			return (HookPlayerChoiceContext)ctor.Invoke(new object[] { source, netId, combatState, gameActionType });
		}

		Player? owner = TryResolveChoiceOwner(source, combatState);
		if (owner != null)
		{
			return CreateHookPlayerChoiceContext(owner, netId, gameActionType);
		}

		throw new MissingMethodException(typeof(HookPlayerChoiceContext).FullName, ".ctor(AbstractModel, ulong, CombatState, GameActionType)");
	}

	internal static HookPlayerChoiceContext CreateHookPlayerChoiceContext(Player owner, ulong netId, GameActionType gameActionType)
	{
		if (owner == null)
		{
			throw new ArgumentNullException(nameof(owner));
		}

		ConstructorInfo? ctor = HookChoicePlayerCtor.Value;
		if (ctor == null)
		{
			throw new MissingMethodException(typeof(HookPlayerChoiceContext).FullName, ".ctor(Player, ulong, GameActionType)");
		}

		return (HookPlayerChoiceContext)ctor.Invoke(new object[] { owner, netId, gameActionType });
	}

	internal static IEnumerable<AbstractModel> IterateHookListenersCompat(this IRunState? runState, ICombatState? combatState)
	{
		if (runState == null)
		{
			return Array.Empty<AbstractModel>();
		}

		Func<object, object?, object?>? invoker = IterateHookListenersCache.GetOrAdd(runState.GetType(), BuildIterateHookListenersInvoker);
		if (invoker == null)
		{
			return Array.Empty<AbstractModel>();
		}

		object? result = invoker(runState, combatState);
		if (result is IEnumerable<AbstractModel> typed)
		{
			return typed.ToList();
		}
		if (result is IEnumerable enumerable)
		{
			return enumerable.OfType<AbstractModel>().ToList();
		}

		return Array.Empty<AbstractModel>();
	}

	private static Func<object, object?, object?>? BuildIterateHookListenersInvoker(Type runStateType)
	{
		MethodInfo? method = FindIterateHookListeners(runStateType);
		if (method == null)
		{
			return null;
		}

		try
		{
			ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
			ParameterExpression arg = Expression.Parameter(typeof(object), "combatState");
			Type declaringType = method.DeclaringType ?? runStateType;
			Type parameterType = method.GetParameters()[0].ParameterType;
			Expression call = Expression.Call(
				Expression.Convert(instance, declaringType),
				method,
				Expression.Convert(arg, parameterType));
			Expression body = Expression.Convert(call, typeof(object));
			return Expression.Lambda<Func<object, object?, object?>>(body, instance, arg).Compile();
		}
		catch
		{
			return (instance, arg) => method.Invoke(instance, new object?[] { arg });
		}
	}

	internal static bool IsMutableSafe(this AbstractModel? model)
	{
		try
		{
			return model?.IsMutable == true;
		}
		catch
		{
			return false;
		}
	}

	internal static Player? TryGetOwner(this CardModel? card)
	{
		if (card == null || !card.IsMutableSafe())
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
		if (power == null || !power.IsMutableSafe())
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
			Func<object, CombatState?>? getter = CombatStateGetterCache.GetOrAdd(
				source.GetType(),
				static type => BuildCombatStateGetter(type));
			return getter?.Invoke(source);
		}
		catch
		{
			return null;
		}
	}

	private static Func<object, CombatState?>? BuildCombatStateGetter(Type type)
	{
		PropertyInfo? property = type.GetProperty("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property == null || property.GetGetMethod(true) == null || property.GetIndexParameters().Length != 0)
		{
			return null;
		}

		try
		{
			ParameterExpression source = Expression.Parameter(typeof(object), "source");
			Expression body = Expression.TypeAs(
				Expression.Property(Expression.Convert(source, type), property),
				typeof(CombatState));
			return Expression.Lambda<Func<object, CombatState?>>(body, source).Compile();
		}
		catch
		{
			return source => property.GetValue(source) as CombatState;
		}
	}

	private static ConstructorInfo? FindHookChoiceSourceCtor()
	{
		return typeof(HookPlayerChoiceContext)
			.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.FirstOrDefault(ctor =>
			{
				ParameterInfo[] parameters = ctor.GetParameters();
				return parameters.Length == 4
					&& typeof(AbstractModel).IsAssignableFrom(parameters[0].ParameterType)
					&& parameters[1].ParameterType == typeof(ulong)
					&& parameters[2].ParameterType.IsAssignableFrom(typeof(CombatState))
					&& parameters[3].ParameterType == typeof(GameActionType);
			});
	}

	private static ConstructorInfo? FindHookChoicePlayerCtor()
	{
		return typeof(HookPlayerChoiceContext)
			.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.FirstOrDefault(ctor =>
			{
				ParameterInfo[] parameters = ctor.GetParameters();
				return parameters.Length == 3
					&& typeof(Player).IsAssignableFrom(parameters[0].ParameterType)
					&& parameters[1].ParameterType == typeof(ulong)
					&& parameters[2].ParameterType == typeof(GameActionType);
			});
	}

	private static MethodInfo? FindIterateHookListeners(Type runStateType)
	{
		static bool Matches(MethodInfo method)
		{
			if (method.Name != "IterateHookListeners")
			{
				return false;
			}

			ParameterInfo[] parameters = method.GetParameters();
			return parameters.Length == 1
				&& parameters[0].ParameterType.IsAssignableFrom(typeof(CombatState));
		}

		return runStateType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(Matches)
			?? typeof(IRunState).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(Matches)
			?? runStateType.GetInterfaces().SelectMany(type => type.GetMethods()).FirstOrDefault(Matches);
	}

	private static Player? TryResolveChoiceOwner(AbstractModel source, CombatState combatState)
	{
		try
		{
			return source switch
			{
				CardModel card => card.Owner,
				RelicModel relic => relic.Owner,
				PotionModel potion => potion.Owner,
				AfflictionModel affliction => affliction.Card?.Owner,
				EnchantmentModel enchantment => enchantment.Card?.Owner,
				PowerModel power when power.TryGetOwner()?.Player != null => power.TryGetOwner()?.Player,
				PowerModel => combatState.Players.FirstOrDefault(),
				_ => null
			};
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
