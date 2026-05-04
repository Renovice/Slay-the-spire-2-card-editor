using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardModel), "get_Title")]
internal static class CardModel_get_Title_VanillaTitleOverride_Patch
{
	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (__instance == null || __instance.Id == null)
		{
			return;
		}
		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.Title;
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
		if (!CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(overrideData.TitleOverride))
		{
			return;
		}

		string title = overrideData.TitleOverride.Trim();
		if (!__instance.IsUpgraded)
		{
			__result = title;
			return;
		}
		if (__instance.MaxUpgradeLevel > 1)
		{
			__result = $"{title}+{__instance.CurrentUpgradeLevel}";
			return;
		}
		__result = title + "+";
	}
}
