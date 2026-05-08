using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorConditionalCustomGlowController
{
	internal static void Sync(NCard? cardNode)
	{
		if (cardNode?.CardHighlight == null)
		{
			return;
		}

		CardModel? card = cardNode.Model;
		if (card == null || !CardEditorExtraEffects.TryGetConditionalGlowColor(card, out Color color))
		{
			return;
		}

		cardNode.CardHighlight.Modulate = color;
		cardNode.CardHighlight.AnimShow();
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_ConditionalCustomGlow_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorConditionalCustomGlowController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_ConditionalCustomGlow_Patch
{
	[HarmonyPriority(Priority.Last)]
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorConditionalCustomGlowController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.UpdateCard))]
internal static class NHandCardHolder_UpdateCard_ConditionalCustomGlow_Patch
{
	[HarmonyPriority(Priority.Last)]
	public static void Postfix(NHandCardHolder __instance)
	{
		try
		{
			CardEditorConditionalCustomGlowController.Sync(__instance?.CardNode);
		}
		catch
		{
		}
	}
}
