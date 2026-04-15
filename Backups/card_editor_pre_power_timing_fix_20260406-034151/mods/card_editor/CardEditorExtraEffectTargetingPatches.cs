using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(NMouseCardPlay), "TargetSelection")]
internal static class MouseCardPlay_TargetSelection_ExtraEffectsTarget_Patch
{
	private static readonly MethodInfo? _singleCreatureTargeting =
		typeof(NMouseCardPlay).GetMethod("SingleCreatureTargeting", BindingFlags.Instance | BindingFlags.NonPublic);

	public static bool Prefix(NMouseCardPlay __instance, TargetMode targetMode, ref Task __result)
	{
		CardModel? card = __instance.Holder?.CardModel;
		if (card == null)
		{
			return true;
		}

		TargetType targetType = card.TargetType;
		if (targetType == TargetType.AnyEnemy || targetType == TargetType.AnyAlly)
		{
			return true;
		}

		if (!CardEditorExtraEffects.RequiresManualEnemyTarget(card))
		{
			return true;
		}

		if (_singleCreatureTargeting == null)
		{
			return true;
		}

		if (_singleCreatureTargeting.Invoke(__instance, new object?[] { targetMode, TargetType.AnyEnemy }) is Task task)
		{
			__result = task;
			return false;
		}

		return true;
	}
}

[HarmonyPatch(typeof(NControllerCardPlay), "MultiCreatureTargeting")]
internal static class ControllerCardPlay_MultiCreatureTargeting_ExtraEffectsTarget_Patch
{
	private static readonly MethodInfo? _singleCreatureTargeting =
		typeof(NControllerCardPlay).GetMethod("SingleCreatureTargeting", BindingFlags.Instance | BindingFlags.NonPublic);

	public static bool Prefix(NControllerCardPlay __instance)
	{
		CardModel? card = __instance.Holder?.CardModel;
		if (card == null)
		{
			return true;
		}

		TargetType targetType = card.TargetType;
		if (targetType == TargetType.AnyEnemy || targetType == TargetType.AnyAlly)
		{
			return true;
		}

		if (!CardEditorExtraEffects.RequiresManualEnemyTarget(card))
		{
			return true;
		}

		if (_singleCreatureTargeting == null)
		{
			return true;
		}

		if (_singleCreatureTargeting.Invoke(__instance, new object?[] { TargetType.AnyEnemy }) is Task task)
		{
			TaskHelper.RunSafely(task);
			return false;
		}

		return true;
	}
}

[HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
internal static class CardPlay_TryPlayCard_ExtraEffectsTarget_Patch
{
	public static bool Prefix(NCardPlay __instance, ref Creature? target)
	{
		CardModel? card = __instance.Holder?.CardModel;
		if (card == null)
		{
			return true;
		}

		TargetType targetType = card.TargetType;
		if (targetType == TargetType.AnyEnemy || targetType == TargetType.AnyAlly)
		{
			return true;
		}

		if (!CardEditorExtraEffects.RequiresManualEnemyTarget(card))
		{
			return true;
		}

		if (target == null)
		{
			__instance.CancelPlayCard();
			return false;
		}

		CardEditorExtraEffects.SetManualTarget(card, target);
		target = null;
		return true;
	}
}
