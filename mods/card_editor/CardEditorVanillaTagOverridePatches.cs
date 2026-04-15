using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardModel), "get_Tags")]
internal static class CardModel_get_Tags_VanillaTagOverride_Patch
{
	public static void Postfix(CardModel __instance, ref IEnumerable<CardTag> __result)
	{
		if (__instance == null || __instance.Id == null)
		{
			return;
		}
		if (__instance is CardEditorCreatedCardBase)
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
		bool hasAdd = overrideData.TagsToAdd != null && overrideData.TagsToAdd.Count > 0;
		bool hasRemove = overrideData.TagsToRemove != null && overrideData.TagsToRemove.Count > 0;
		if (!hasAdd && !hasRemove)
		{
			return;
		}

		HashSet<CardTag> tags = new HashSet<CardTag>();
		if (__result != null)
		{
			foreach (CardTag tag in __result)
			{
				if (tag != CardTag.None)
				{
					tags.Add(tag);
				}
			}
		}

		if (hasRemove)
		{
			tags.ExceptWith(overrideData.TagsToRemove!);
		}
		if (hasAdd)
		{
			tags.UnionWith(overrideData.TagsToAdd!);
		}

		tags.Remove(CardTag.None);
		__result = tags;
	}
}

