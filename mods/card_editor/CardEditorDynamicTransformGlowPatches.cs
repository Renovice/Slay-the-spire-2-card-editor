using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorDynamicTransformGlowController
{
	private static readonly Color TransformedGlowColor = new Color(0.72f, 0.42f, 1f, 0.98f);

	internal static void Sync(NCard? cardNode)
	{
		if (cardNode?.CardHighlight == null)
		{
			return;
		}

		CardModel? card = cardNode.Model;
		if (card == null || !CardEditorExtraEffects.IsDynamicTransformReplacement(card))
		{
			return;
		}

		if (card.ShouldGlowRed)
		{
			return;
		}

		cardNode.CardHighlight.Modulate = TransformedGlowColor;
		cardNode.CardHighlight.AnimShow();
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_DynamicTransformGlow_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorDynamicTransformGlowController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_DynamicTransformGlow_Patch
{
	[HarmonyPriority(Priority.Last)]
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorDynamicTransformGlowController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.UpdateCard))]
internal static class NHandCardHolder_UpdateCard_DynamicTransformGlow_Patch
{
	[HarmonyPriority(Priority.Last)]
	public static void Postfix(NHandCardHolder __instance)
	{
		try
		{
			CardEditorDynamicTransformGlowController.Sync(__instance?.CardNode);
		}
		catch
		{
		}
	}
}
