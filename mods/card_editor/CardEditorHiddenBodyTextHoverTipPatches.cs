using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(NCardHolder), "CreateHoverTips")]
internal static class NCardHolder_CreateHoverTips_HiddenBodyText_Patch
{
	public static bool Prefix(NCardHolder __instance)
	{
		if (__instance?.CardNode?.Model == null
			|| CardEditorOverrides.SuppressAllOverrides
			|| !CardEditorOverrides.TryGetEffectiveOverride(__instance.CardNode.Model, out CardOverride overrideData)
			|| overrideData.HideCosmeticBodyText != true)
		{
			return true;
		}

		try
		{
			CardModel card = __instance.CardNode.Model;
			List<IHoverTip> hoverTips = new();
			hoverTips.AddRange(card.HoverTips ?? Array.Empty<IHoverTip>());

			string description = card.GetDescriptionForPile(__instance.CardNode.DisplayingPile, card.GetSafeCurrentTarget());
			if (!string.IsNullOrWhiteSpace(description))
			{
				hoverTips.Add(CardEditorVanillaKeywordSupport.CreateDynamicHoverTip(card.Title, description));
			}

			NHoverTipSet.CreateAndShow(__instance, hoverTips)?.SetAlignmentForCardHolder(__instance);
			return false;
		}
		catch
		{
			return true;
		}
	}
}
