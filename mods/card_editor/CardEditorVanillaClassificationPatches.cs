using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardModel), "get_Pool")]
internal static class CardModel_get_Pool_VanillaOverride_Patch
{
	public static void Postfix(CardModel __instance, ref CardPoolModel __result)
	{
		if (__instance?.Id == null || __result == null)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			return;
		}

		string desired = overrideData.PoolTitle.Trim();
		CardPoolModel? pool = ModelDb.AllCardPools.FirstOrDefault(p => string.Equals(p.Title, desired, StringComparison.OrdinalIgnoreCase));
		if (pool != null)
		{
			__result = pool;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_VisualCardPool")]
internal static class CardModel_get_VisualCardPool_VanillaOverride_Patch
{
	public static void Postfix(CardModel __instance, ref CardPoolModel __result)
	{
		if (__instance?.Id == null || __result == null)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			return;
		}

		string desired = overrideData.PoolTitle.Trim();
		CardPoolModel? pool = ModelDb.AllCardPools.FirstOrDefault(p => string.Equals(p.Title, desired, StringComparison.OrdinalIgnoreCase));
		if (pool != null)
		{
			__result = pool;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Rarity")]
internal static class CardModel_get_Rarity_VanillaOverride_Patch
{
	public static void Postfix(CardModel __instance, ref CardRarity __result)
	{
		if (__instance?.Id == null)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGetEffectiveOverride(__instance.Id, out CardOverride overrideData))
		{
			return;
		}
		if (CardEditorFullArtRenderContext.IsActive && overrideData.FullArt == true)
		{
			__result = CardRarity.Ancient;
			return;
		}
		if (overrideData.Rarity is CardRarity rarity && rarity != CardRarity.None)
		{
			__result = rarity;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Type")]
internal static class CardModel_get_Type_VanillaOverride_Patch
{
	public static void Postfix(CardModel __instance, ref CardType __result)
	{
		if (__instance?.Id == null)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData))
		{
			return;
		}
		if (overrideData.CardType is CardType type && type != CardType.None)
		{
			__result = type;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_TargetType")]
internal static class CardModel_get_TargetType_VanillaOverride_Patch
{
	public static void Postfix(CardModel __instance, ref TargetType __result)
	{
		if (__instance?.Id == null)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData))
		{
			overrideData = null!;
		}
		if (overrideData != null && overrideData.TargetType is TargetType target && target != TargetType.None)
		{
			__result = target;
		}
		if (CardEditorExtraEffects.TryGetRuleAdjustedTargetType(__instance, __result, out TargetType adjustedTargetType))
		{
			__result = adjustedTargetType;
		}
	}
}
