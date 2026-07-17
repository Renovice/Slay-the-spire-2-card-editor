using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

// v0.108.0 removed ITemporaryPower.IgnoreNextInstance (the flag that let a caller change a temporary
// wrapper's Amount without the wrapper re-applying its internal power). The wrappers now apply their
// internal power from BeforeApplied / AfterPowerAmountChanged instead, so this recreates the old
// contract mod-side: flag the instance, and prefixes on those two methods consume the flag to skip
// exactly one internal application.
internal static class CardEditorTemporaryPowerCompat
{
	private static readonly ConditionalWeakTable<PowerModel, object> PendingSuppressions = new();

	internal static void IgnoreNextInternalApplication(PowerModel? power)
	{
		if (power is not ITemporaryPower)
		{
			return;
		}

		PendingSuppressions.Remove(power);
		PendingSuppressions.Add(power, string.Empty);
	}

	internal static bool TryConsume(PowerModel? power)
	{
		return power != null && PendingSuppressions.Remove(power);
	}
}

[HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.BeforeApplied))]
internal static class TemporaryStrengthPower_BeforeApplied_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryStrengthPower), "BeforeApplied")?.DeclaringType == typeof(TemporaryStrengthPower);
	}

	public static bool Prefix(TemporaryStrengthPower __instance, ref Task __result)
	{
		if (!CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryStrengthPower), nameof(TemporaryStrengthPower.AfterPowerAmountChanged))]
internal static class TemporaryStrengthPower_AfterPowerAmountChanged_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryStrengthPower), "AfterPowerAmountChanged")?.DeclaringType == typeof(TemporaryStrengthPower);
	}

	public static bool Prefix(TemporaryStrengthPower __instance, PowerModel power, ref Task __result)
	{
		if (!ReferenceEquals(power, __instance) || !CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryDexterityPower), nameof(TemporaryDexterityPower.BeforeApplied))]
internal static class TemporaryDexterityPower_BeforeApplied_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryDexterityPower), "BeforeApplied")?.DeclaringType == typeof(TemporaryDexterityPower);
	}

	public static bool Prefix(TemporaryDexterityPower __instance, ref Task __result)
	{
		if (!CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryDexterityPower), nameof(TemporaryDexterityPower.AfterPowerAmountChanged))]
internal static class TemporaryDexterityPower_AfterPowerAmountChanged_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryDexterityPower), "AfterPowerAmountChanged")?.DeclaringType == typeof(TemporaryDexterityPower);
	}

	public static bool Prefix(TemporaryDexterityPower __instance, PowerModel power, ref Task __result)
	{
		if (!ReferenceEquals(power, __instance) || !CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryFocusPower), nameof(TemporaryFocusPower.BeforeApplied))]
internal static class TemporaryFocusPower_BeforeApplied_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryFocusPower), "BeforeApplied")?.DeclaringType == typeof(TemporaryFocusPower);
	}

	public static bool Prefix(TemporaryFocusPower __instance, ref Task __result)
	{
		if (!CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}

[HarmonyPatch(typeof(TemporaryFocusPower), nameof(TemporaryFocusPower.AfterPowerAmountChanged))]
internal static class TemporaryFocusPower_AfterPowerAmountChanged_CardEditorSuppression_Patch
{
	public static bool Prepare()
	{
		return AccessTools.Method(typeof(TemporaryFocusPower), "AfterPowerAmountChanged")?.DeclaringType == typeof(TemporaryFocusPower);
	}

	public static bool Prefix(TemporaryFocusPower __instance, PowerModel power, ref Task __result)
	{
		if (!ReferenceEquals(power, __instance) || !CardEditorTemporaryPowerCompat.TryConsume(__instance))
		{
			return true;
		}

		__result = Task.CompletedTask;
		return false;
	}
}
