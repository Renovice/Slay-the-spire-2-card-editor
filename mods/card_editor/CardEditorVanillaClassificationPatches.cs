using System;
using System.Collections.Generic;
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
		if (CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData)
			&& !string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			string desired = overrideData.PoolTitle.Trim();
			CardPoolModel? pool = ModelDb.AllCardPools.FirstOrDefault(p => string.Equals(p.Title, desired, StringComparison.OrdinalIgnoreCase));
			if (pool != null)
			{
				__result = pool;
			}
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.Pool;
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
		if (CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData)
			&& !string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			string desired = overrideData.PoolTitle.Trim();
			CardPoolModel? pool = ModelDb.AllCardPools.FirstOrDefault(p => string.Equals(p.Title, desired, StringComparison.OrdinalIgnoreCase));
			if (pool != null)
			{
				__result = pool;
			}
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.VisualCardPool;
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
		if (CardEditorOverrides.TryGetEffectiveOverride(__instance.Id, out CardOverride overrideData))
		{
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
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.Rarity;
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
		if (CardEditorOverrides.TryGet(__instance.Id, out CardOverride overrideData)
			&& overrideData.CardType is CardType type
			&& type != CardType.None)
		{
			__result = type;
		}
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.Type;
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
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.TargetType;
		}
		if (CardEditorExtraEffects.TryGetRuleAdjustedTargetType(__instance, __result, out TargetType adjustedTargetType))
		{
			__result = adjustedTargetType;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_CanonicalKeywords")]
internal static class CardModel_get_CanonicalKeywords_DynamicIdentity_Patch
{
	public static void Postfix(CardModel __instance, ref IEnumerable<CardKeyword> __result)
	{
		if (__instance?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.CanonicalKeywords;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Tags")]
internal static class CardModel_get_Tags_DynamicIdentity_Patch
{
	public static void Postfix(CardModel __instance, ref IEnumerable<CardTag> __result)
	{
		if (__instance?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.Tags;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_GainsBlock")]
internal static class CardModel_get_GainsBlock_DynamicIdentity_Patch
{
	public static void Postfix(CardModel __instance, ref bool __result)
	{
		if (__instance?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GainsBlock;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_CanBeGeneratedInCombat")]
internal static class CardModel_get_CanBeGeneratedInCombat_CardEditorOverride_Patch
{
	public static void Postfix(CardModel __instance, ref bool __result)
	{
		if (__instance?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}

		if (CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData)
			&& overrideData.CanBeGeneratedInCombat.HasValue)
		{
			__result = overrideData.CanBeGeneratedInCombat.Value;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_CanBeGeneratedByModifiers")]
internal static class CardModel_get_CanBeGeneratedByModifiers_CardEditorOverride_Patch
{
	public static void Postfix(CardModel __instance, ref bool __result)
	{
		if (__instance?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}

		if (CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData)
			&& overrideData.CanBeGeneratedByModifiers.HasValue)
		{
			__result = overrideData.CanBeGeneratedByModifiers.Value;
		}
	}
}
