using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace SlayTheSpire2Mod.CardEditor;

// Ensure our external localization files are merged after LocManager is initialized.
// Mod initializers can run before LocManager.Initialize(), so CardEditorExternalLocalization.Init()
// may no-op unless we re-run it once localization is ready.
[HarmonyPatch(typeof(LocManager), nameof(LocManager.Initialize))]
public static class CardEditor_LocManager_Initialize_Patch
{
	public static void Postfix()
	{
		CardEditorExternalLocalization.Init();
	}
}

