using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorPowerCmdCompat
{
	private static readonly Type PlayerChoiceContextType = typeof(PlayerChoiceContext);

	private static PlayerChoiceContext FallbackContext()
	{
		return new ThrowingPlayerChoiceContext();
	}

	internal static MethodInfo? FindGenericApplyTargetsMethod(bool enumerableTargets)
	{
		return typeof(PowerCmd)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethodDefinition)
			.FirstOrDefault(method =>
			{
				ParameterInfo[] parameters = method.GetParameters();
				int offset = HasChoiceContextPrefix(parameters) ? 1 : 0;
				if (!HasArityWithOptionalSilent(parameters, offset, requiredAfterOffset: 4))
				{
					return false;
				}

				Type targetType = parameters[offset].ParameterType;
				bool isEnumerable = targetType != typeof(Creature)
					&& typeof(IEnumerable<Creature>).IsAssignableFrom(targetType);
				return isEnumerable == enumerableTargets
					&& parameters[offset + 1].ParameterType == typeof(decimal)
					&& parameters[offset + 2].ParameterType == typeof(Creature)
					&& parameters[offset + 3].ParameterType == typeof(CardModel);
			});
	}

	internal static MethodInfo? FindApplyPowerMethod()
	{
		return typeof(PowerCmd)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == nameof(PowerCmd.Apply) && !method.IsGenericMethodDefinition)
			.FirstOrDefault(method =>
			{
				ParameterInfo[] parameters = method.GetParameters();
				int offset = HasChoiceContextPrefix(parameters) ? 1 : 0;
				return HasArityWithOptionalSilent(parameters, offset, requiredAfterOffset: 5)
					&& parameters[offset].ParameterType == typeof(PowerModel)
					&& parameters[offset + 1].ParameterType == typeof(Creature)
					&& parameters[offset + 2].ParameterType == typeof(decimal)
					&& parameters[offset + 3].ParameterType == typeof(Creature)
					&& parameters[offset + 4].ParameterType == typeof(CardModel);
			});
	}

	internal static MethodInfo? FindModifyAmountMethod()
	{
		return typeof(PowerCmd)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == nameof(PowerCmd.ModifyAmount) && !method.IsGenericMethodDefinition)
			.FirstOrDefault(method =>
			{
				ParameterInfo[] parameters = method.GetParameters();
				int offset = HasChoiceContextPrefix(parameters) ? 1 : 0;
				return HasArityWithOptionalSilent(parameters, offset, requiredAfterOffset: 4)
					&& parameters[offset].ParameterType == typeof(PowerModel)
					&& parameters[offset + 1].ParameterType == typeof(decimal)
					&& parameters[offset + 2].ParameterType == typeof(Creature)
					&& parameters[offset + 3].ParameterType == typeof(CardModel);
			});
	}

	private static bool HasArityWithOptionalSilent(ParameterInfo[] parameters, int offset, int requiredAfterOffset)
	{
		int requiredLength = offset + requiredAfterOffset;
		if (parameters.Length == requiredLength)
		{
			return true;
		}

		return parameters.Length == requiredLength + 1
			&& parameters[requiredLength].ParameterType == typeof(bool);
	}

	private static bool HasChoiceContextPrefix(ParameterInfo[] parameters)
	{
		return parameters.Length > 0 && PlayerChoiceContextType.IsAssignableFrom(parameters[0].ParameterType);
	}

	private static object?[] BuildArguments(MethodInfo method, params object?[] args)
	{
		ParameterInfo[] parameters = method.GetParameters();
		bool hasChoiceContextPrefix = HasChoiceContextPrefix(parameters);
		int acceptedArgumentCount = Math.Min(args.Length, parameters.Length - (hasChoiceContextPrefix ? 1 : 0));
		IEnumerable<object?> acceptedArguments = args.Take(acceptedArgumentCount);

		return hasChoiceContextPrefix
			? new object?[] { FallbackContext() }.Concat(acceptedArguments).ToArray()
			: acceptedArguments.ToArray();
	}

	private static T InvokeTask<T>(MethodInfo? method, params object?[] args)
		where T : class
	{
		if (method == null)
		{
			throw new MissingMethodException(typeof(PowerCmd).FullName, nameof(PowerCmd.Apply));
		}
		return (T)method.Invoke(null, BuildArguments(method, args))!;
	}

	internal static Task<IReadOnlyList<T>> Apply<T>(IEnumerable<Creature> targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		MethodInfo? method = FindGenericApplyTargetsMethod(enumerableTargets: true)?.MakeGenericMethod(typeof(T));
		return InvokeTask<Task<IReadOnlyList<T>>>(method, targets, amount, applier, cardSource, silent);
	}

	internal static Task<T?> Apply<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
		where T : PowerModel
	{
		MethodInfo? method = FindGenericApplyTargetsMethod(enumerableTargets: false)?.MakeGenericMethod(typeof(T));
		return InvokeTask<Task<T?>>(method, target, amount, applier, cardSource, silent);
	}

	internal static Task Apply(PowerModel power, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false)
	{
		return InvokeTask<Task>(FindApplyPowerMethod(), power, target, amount, applier, cardSource, silent);
	}

	internal static Task<int> ModifyAmount(PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
	{
		return InvokeTask<Task<int>>(FindModifyAmountMethod(), power, offset, applier, cardSource, silent);
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
