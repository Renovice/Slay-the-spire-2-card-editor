using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using System;
using System.Linq;
using System.Reflection;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorAttackCommandCompat
{
	private static readonly Lazy<MethodInfo?> TargetingAllOpponentsMethod =
		new(() => FindTargetingMethod(nameof(AttackCommand.TargetingAllOpponents), parameterCount: 1));

	private static readonly Lazy<MethodInfo?> TargetingRandomOpponentsMethod =
		new(() => FindTargetingMethod(nameof(AttackCommand.TargetingRandomOpponents), parameterCount: 2));

	private static readonly Lazy<PropertyInfo?> ResultsProperty =
		new(() => typeof(AttackCommand).GetProperty("Results", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

	private static readonly Lazy<FieldInfo?> ResultsField =
		new(() => typeof(AttackCommand).GetField("_results", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

	internal static AttackCommand TargetingAllOpponentsCompat(this AttackCommand attack, CombatState combatState)
	{
		return InvokeTargeting(TargetingAllOpponentsMethod.Value, attack, combatState);
	}

	internal static AttackCommand TargetingRandomOpponentsCompat(this AttackCommand attack, CombatState combatState, bool allowDuplicates = true)
	{
		return InvokeTargeting(TargetingRandomOpponentsMethod.Value, attack, combatState, allowDuplicates);
	}

	internal static object? GetResultsCompat(this AttackCommand attack)
	{
		if (attack == null)
		{
			return null;
		}

		PropertyInfo? property = ResultsProperty.Value;
		if (property != null)
		{
			return property.GetValue(attack);
		}

		return ResultsField.Value?.GetValue(attack);
	}

	private static AttackCommand InvokeTargeting(MethodInfo? method, AttackCommand attack, CombatState combatState, params object[] extraArgs)
	{
		if (attack == null)
		{
			throw new ArgumentNullException(nameof(attack));
		}
		if (combatState == null)
		{
			throw new ArgumentNullException(nameof(combatState));
		}
		if (method == null)
		{
			throw new MissingMethodException(typeof(AttackCommand).FullName, "Targeting*Opponents");
		}

		object[] args = new object[1 + extraArgs.Length];
		args[0] = combatState;
		Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
		return (AttackCommand)method.Invoke(attack, args)!;
	}

	private static MethodInfo? FindTargetingMethod(string name, int parameterCount)
	{
		return typeof(AttackCommand)
			.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.FirstOrDefault(method =>
			{
				if (method.Name != name)
				{
					return false;
				}

				ParameterInfo[] parameters = method.GetParameters();
				return parameters.Length == parameterCount
					&& parameters[0].ParameterType.IsAssignableFrom(typeof(CombatState))
					&& (parameterCount == 1 || parameters[1].ParameterType == typeof(bool));
			});
	}
}
