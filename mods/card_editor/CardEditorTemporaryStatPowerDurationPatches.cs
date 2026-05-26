using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryStatPowerDurationController
{
	private sealed class DurationState
	{
		public CardKeywordGrantDuration Mode { get; set; }
		public int RemainingTurns { get; set; }
	}

	private static readonly ConditionalWeakTable<PowerModel, DurationState> _states = new ConditionalWeakTable<PowerModel, DurationState>();

	public static void RegisterIfNeeded(PowerModel power, CardOverride overrideData)
	{
		if (power == null || overrideData == null)
		{
			return;
		}

		if (power is TemporaryStrengthPower)
		{
			RegisterInternal(power, overrideData.TemporaryStrengthDuration, overrideData.TemporaryStrengthTurns);
		}
		else if (power is TemporaryDexterityPower)
		{
			RegisterInternal(power, overrideData.TemporaryDexterityDuration, overrideData.TemporaryDexterityTurns);
		}
		else if (power is TemporaryFocusPower)
		{
			RegisterInternal(power, overrideData.TemporaryFocusDuration, overrideData.TemporaryFocusTurns);
		}
	}

	private static void RegisterInternal(PowerModel power, CardKeywordGrantDuration? duration, int? turns)
	{
		if (power == null)
		{
			return;
		}

		if (duration == null || duration == CardKeywordGrantDuration.ThisTurn)
		{
			_states.Remove(power);
			return;
		}

		DurationState state = _states.GetOrCreateValue(power);
		switch (duration.Value)
		{
			case CardKeywordGrantDuration.ThisCombat:
				state.Mode = CardKeywordGrantDuration.ThisCombat;
				state.RemainingTurns = 0;
				break;
			case CardKeywordGrantDuration.Turns:
			{
				int desired = Math.Clamp(turns.GetValueOrDefault(2), 1, 99);
				if (state.Mode != CardKeywordGrantDuration.Turns)
				{
					state.RemainingTurns = desired;
				}
				else
				{
					state.RemainingTurns = Math.Max(state.RemainingTurns, desired);
				}
				state.Mode = CardKeywordGrantDuration.Turns;
				break;
			}
			default:
				_states.Remove(power);
				break;
		}
	}

	public static bool ShouldSkipTurnEndRemoval(PowerModel power, CombatSide side, IEnumerable<Creature>? participants = null)
	{
		if (power == null || power.Owner == null)
		{
			return false;
		}
		if (participants != null && !participants.Contains(power.Owner))
		{
			return false;
		}
		if (side != power.Owner.Side)
		{
			return false;
		}

		if (!_states.TryGetValue(power, out DurationState? state) || state == null)
		{
			return false;
		}

		switch (state.Mode)
		{
			case CardKeywordGrantDuration.ThisCombat:
				return true;
			case CardKeywordGrantDuration.Turns:
				if (state.RemainingTurns > 1)
				{
					state.RemainingTurns--;
					return true;
				}
				return false;
			default:
				return false;
		}
	}
}

[HarmonyPatch]
internal static class TemporaryStrengthPower_AfterSideTurnEnd_CardEditorDurationOverride_Patch
{
	private static System.Reflection.MethodBase? TargetMethod()
	{
		return AccessTools.Method(typeof(TemporaryStrengthPower), "AfterSideTurnEnd");
	}

	public static bool Prefix(TemporaryStrengthPower __instance, PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side, participants))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch]
internal static class TemporaryDexterityPower_AfterSideTurnEnd_CardEditorDurationOverride_Patch
{
	private static System.Reflection.MethodBase? TargetMethod()
	{
		return AccessTools.Method(typeof(TemporaryDexterityPower), "AfterSideTurnEnd");
	}

	public static bool Prefix(TemporaryDexterityPower __instance, PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side, participants))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch]
internal static class TemporaryFocusPower_AfterSideTurnEnd_CardEditorDurationOverride_Patch
{
	private static System.Reflection.MethodBase? TargetMethod()
	{
		return AccessTools.Method(typeof(TemporaryFocusPower), "AfterSideTurnEnd");
	}

	public static bool Prefix(TemporaryFocusPower __instance, PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side, participants))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryStrengthPower), "AfterTurnEnd")]
internal static class TemporaryStrengthPower_AfterTurnEnd_CardEditorDurationOverride_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryStrengthPower), "AfterTurnEnd") != null;
	}

	public static bool Prefix(TemporaryStrengthPower __instance, PlayerChoiceContext choiceContext, CombatSide side, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryDexterityPower), "AfterTurnEnd")]
internal static class TemporaryDexterityPower_AfterTurnEnd_CardEditorDurationOverride_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryDexterityPower), "AfterTurnEnd") != null;
	}

	public static bool Prefix(TemporaryDexterityPower __instance, PlayerChoiceContext choiceContext, CombatSide side, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryFocusPower), "AfterTurnEnd")]
internal static class TemporaryFocusPower_AfterTurnEnd_CardEditorDurationOverride_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryFocusPower), "AfterTurnEnd") != null;
	}

	public static bool Prefix(TemporaryFocusPower __instance, PlayerChoiceContext choiceContext, CombatSide side, ref Task __result)
	{
		if (!CardEditorTemporaryStatPowerDurationController.ShouldSkipTurnEndRemoval(__instance, side))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}
