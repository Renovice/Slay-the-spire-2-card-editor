using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Formatters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.addons.mega_text;

namespace SlayTheSpire2Mod.CardEditor;

[ModInitializer("Init")]
public static class CardEditorMod
{
	private const string HarmonyId = "slaythespire2.card_editor";

	internal static bool IsVerboseEditorLogging => CardEditorPerformanceSettings.VerboseLogging;

	internal static void VerboseLog(string message)
	{
		if (!IsVerboseEditorLogging)
		{
			return;
		}

		Log.Info(message);
	}

	public static void Init()
	{
		CardEditorExternalLocalization.Init();
		CardEditorCreatedCardsStore.EnsureLoaded();
		CardEditorDefinitionStore.EnsureLoaded();
		RegisterCreatedCardsInPools();

		Harmony harmony = new Harmony(HarmonyId);
		PatchPrivateGetDescriptionForPile(harmony);
		harmony.PatchAll(Assembly.GetExecutingAssembly());
		EnsureIgnoreDamagePatches(harmony);

	}

	private static void PatchPrivateGetDescriptionForPile(Harmony harmony)
	{
		// Patch the PRIVATE 3-param GetDescriptionForPile before PatchAll so created-card
		// descriptions still resolve even if a later attribute patch fails during init.
		try
		{
			// DescriptionPreviewType is a private enum nested inside CardModel
			var descPreviewType = typeof(CardModel).GetNestedType("DescriptionPreviewType", BindingFlags.NonPublic);
			if (descPreviewType == null)
			{
				Log.Warn("[CardEditor] Could not find DescriptionPreviewType enum");
			}
			else
			{
				var privateMethod = typeof(CardModel).GetMethod(
					"GetDescriptionForPile",
					BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					new Type[] { typeof(PileType), descPreviewType, typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature) },
					null
				);
				if (privateMethod != null)
				{
					var prefix = typeof(GetDescriptionForPile_ManualPatch).GetMethod(nameof(GetDescriptionForPile_ManualPatch.Prefix), BindingFlags.Static | BindingFlags.Public);
					var postfix = typeof(GetDescriptionForPile_ManualPatch).GetMethod(nameof(GetDescriptionForPile_ManualPatch.Postfix), BindingFlags.Static | BindingFlags.Public);
					harmony.Patch(privateMethod, prefix: new HarmonyMethod(prefix) { priority = Priority.First }, postfix: new HarmonyMethod(postfix));
					VerboseLog("[CardEditor] Patched PRIVATE GetDescriptionForPile(3-param) successfully");
				}
				else
				{
					Log.Warn("[CardEditor] Could not find PRIVATE GetDescriptionForPile method");
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[CardEditor] Failed to patch private GetDescriptionForPile: {ex}");
		}
	}

	private static void EnsureIgnoreDamagePatches(Harmony harmony)
	{
		try
		{
			EnsurePatched(
				harmony,
				typeof(AttackCommand).GetMethod(nameof(AttackCommand.Execute), BindingFlags.Instance | BindingFlags.Public)!,
				typeof(AttackCommand_Execute_ApplyIgnoreDamageProps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "AttackCommand.Execute");

			EnsurePatched(
				harmony,
				AccessTools.Method(typeof(Hook), "ModifyDamageInternal"),
				typeof(Hook_ModifyDamageInternal_IgnoreCaps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "Hook.ModifyDamageInternal");

			EnsurePatched(
				harmony,
				typeof(Hook).GetMethod(nameof(Hook.ModifyHpLostAfterOsty), BindingFlags.Static | BindingFlags.Public)!,
				typeof(Hook_ModifyHpLostAfterOsty_IgnoreNegation_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "Hook.ModifyHpLostAfterOsty");

			EnsurePatched(
				harmony,
				typeof(IntangiblePower).GetMethod(nameof(IntangiblePower.ModifyHpLostAfterOsty), BindingFlags.Instance | BindingFlags.Public)!,
				typeof(IntangiblePower_ModifyHpLostAfterOsty_IgnoreCaps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "IntangiblePower.ModifyHpLostAfterOsty");

			EnsurePatched(
				harmony,
				typeof(SlipperyPower).GetMethod(nameof(SlipperyPower.ModifyDamageCap), BindingFlags.Instance | BindingFlags.Public)!,
				typeof(SlipperyPower_ModifyDamageCap_IgnoreCaps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "SlipperyPower.ModifyDamageCap");

			EnsurePatched(
				harmony,
				typeof(HardToKillPower).GetMethod(nameof(HardToKillPower.ModifyDamageCap), BindingFlags.Instance | BindingFlags.Public)!,
				typeof(HardToKillPower_ModifyDamageCap_IgnoreCaps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "HardToKillPower.ModifyDamageCap");

			EnsurePatched(
				harmony,
				typeof(HardenedShellPower).GetMethod(nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate), BindingFlags.Instance | BindingFlags.Public)!,
				typeof(HardenedShellPower_ModifyHpLostBeforeOstyLate_IgnoreCaps_Patch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
				label: "HardenedShellPower.ModifyHpLostBeforeOstyLate");
		}
		catch (Exception ex)
		{
			Log.Error($"[CardEditor][IgnoreDamageDebug] Failed ensuring ignore-damage patches: {ex}");
		}
	}

	private static void EnsurePatched(Harmony harmony, MethodBase? target, MethodInfo? prefix, string label)
	{
		if (target == null || prefix == null)
		{
			Log.Warn($"[CardEditor][IgnoreDamageDebug] Missing patch target or prefix for {label}.");
			return;
		}

		Patches? infoBefore = Harmony.GetPatchInfo(target);
		bool alreadyPatched = infoBefore?.Owners.Contains(HarmonyId) == true;
		if (!alreadyPatched)
		{
			harmony.Patch(target, prefix: new HarmonyMethod(prefix));
		}

		Patches? infoAfter = Harmony.GetPatchInfo(target);
		string owners = infoAfter == null || infoAfter.Owners.Count == 0
			? "<none>"
			: string.Join(", ", infoAfter.Owners.Distinct());
		if (CardEditorIgnoreEffectHelpers.VerboseDebugEnabled)
		{
			Log.Info($"[CardEditor][IgnoreDamageDebug] EnsurePatched {label} alreadyPatched={alreadyPatched} owners={owners}");
		}
	}

	private static void RegisterCreatedCardsInPools()
	{
		try
		{
			Type[] pools =
			{
				typeof(IroncladCardPool),
				typeof(SilentCardPool),
				typeof(DefectCardPool),
				typeof(RegentCardPool),
				typeof(NecrobinderCardPool),
				typeof(ColorlessCardPool),
				typeof(CurseCardPool),
				typeof(StatusCardPool),
				typeof(TokenCardPool),
				typeof(EventCardPool),
				typeof(QuestCardPool)
			};

			int desired = Math.Clamp(CardEditorCreatedCardsStore.SlotCount, 1, CardEditorCreatedCardsStore.MaxSlotCount);
			List<Type> cards = typeof(CardEditorCreatedCardBase).Assembly
				.GetTypes()
				.Select(t => new { Type = t, Slot = TryParseCreatedCardSlot(t) })
				.Where(x => x.Slot.HasValue && x.Slot.Value >= 1 && x.Slot.Value <= desired)
				.OrderBy(x => x.Slot!.Value)
				.Select(x => x.Type)
				.ToList();

			if (cards.Count < desired)
			{
				Log.Warn($"[CardEditor] Created card slots requested={desired} but only found={cards.Count} types.");
			}

			foreach (Type poolType in pools)
			{
				foreach (Type cardType in cards)
				{
					ModHelper.AddModelToPool(poolType, cardType);
				}
			}

			ModHelper.AddModelToPool(typeof(EventCardPool), typeof(CardEditorBuiltTinkerCard));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed registering created cards in pools: {ex}");
		}
	}

	private static int? TryParseCreatedCardSlot(Type type)
	{
		if (type == null || type.IsAbstract || !typeof(CardEditorCreatedCardBase).IsAssignableFrom(type))
		{
			return null;
		}

		const string prefix = "CardEditorCreatedCard";
		if (!type.Name.StartsWith(prefix, StringComparison.Ordinal))
		{
			return null;
		}

		string suffix = type.Name.Substring(prefix.Length);
		if (!int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int slot))
		{
			return null;
		}

		return slot;
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.ToMutable))]
public static class CardModel_ToMutable_Patch
{
	public static void Postfix(CardModel __instance, CardModel __result)
	{
		if (__result == null)
		{
			return;
		}

		if (CardEditorOverrides.TryGetRuntimeCloneSourceOverride(__instance, out CardOverride overrideData))
		{
			CardEditorOverrides.ApplyOverrideToCard(__result, CardEditorOverrides.Clone(overrideData));
			return;
		}

		CardEditorOverrides.ApplyTo(__result);
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel), MethodType.Getter)]
public static class CardModel_MaxUpgradeLevel_Patch
{
	public static void Postfix(CardModel __instance, ref int __result)
	{
		if (__instance == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}

		if (CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData)
			&& overrideData.EndlessUpgrades == true)
		{
			__result = Math.Max(__result, int.MaxValue);
		}
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
public static class CardModel_UpgradeInternal_Patch
{
	public struct UpgradeSnapshot
	{
		public int PreUpgradeLevel;
		public int EnergyCostBase;
		public int StarCostBase;
		public int ReplayCountBase;
		public Dictionary<string, decimal>? DynamicVarBaseValues;
	}

	public static void Prefix(CardModel __instance, ref UpgradeSnapshot __state)
	{
		__state = default;
		__state.PreUpgradeLevel = -1;
		if (__instance == null || !__instance.IsMutable)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides || CardEditorOverrides.SuppressUpgradeOverrides)
		{
			return;
		}
		if (!CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData)
			|| overrideData.Upgrade == null
			|| overrideData.Upgrade.IsEmpty())
		{
			return;
		}
		CardEditorUpgradeDeltaDebugLog.LogCardState("UpgradeInternal.Prefix.overrideFound", __instance, overrideData);

		int preUpgradeLevel = __instance.CurrentUpgradeLevel;
		bool shouldCapture = preUpgradeLevel == 0 || overrideData.EndlessUpgrades == true;
		if (!shouldCapture)
		{
			return;
		}

		__state.PreUpgradeLevel = preUpgradeLevel;

		try
		{
			__state.EnergyCostBase = __instance.EnergyCost.GetWithModifiers(CostModifiers.None);
		}
		catch
		{
			__state.EnergyCostBase = 0;
		}

		try
		{
			__state.StarCostBase = __instance.BaseStarCost;
		}
		catch
		{
			__state.StarCostBase = -1;
		}

		try
		{
			__state.ReplayCountBase = __instance.BaseReplayCount;
		}
		catch
		{
			__state.ReplayCountBase = 0;
		}

		try
		{
			__state.DynamicVarBaseValues = __instance.DynamicVars.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BaseValue, StringComparer.Ordinal);
		}
		catch
		{
			__state.DynamicVarBaseValues = null;
		}
	}

	public static void Postfix(CardModel __instance, UpgradeSnapshot __state)
	{
		if (__instance == null || !__instance.IsMutable)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides || CardEditorOverrides.SuppressUpgradeOverrides)
		{
			return;
		}
		if (__state.PreUpgradeLevel < 0)
		{
			return;
		}
		if (!CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData)
			|| overrideData.Upgrade == null
			|| overrideData.Upgrade.IsEmpty())
		{
			return;
		}
		CardEditorUpgradeDeltaDebugLog.LogCardState("UpgradeInternal.Postfix.overrideFound", __instance, overrideData);

		CardUpgradeOverride upgrade = overrideData.Upgrade;
		bool isFirstUpgrade = __state.PreUpgradeLevel == 0 && __instance.CurrentUpgradeLevel == 1;
		bool isRepeatedEndlessUpgrade = overrideData.EndlessUpgrades == true
			&& __state.PreUpgradeLevel > 0
			&& __instance.CurrentUpgradeLevel == __state.PreUpgradeLevel + 1;
		if (!isFirstUpgrade && !isRepeatedEndlessUpgrade)
		{
			return;
		}

		ApplyNumericUpgradeAdjustments(__instance, upgrade, __state);

		if (!isFirstUpgrade)
		{
			try
			{
				__instance.DynamicVars.RecalculateForUpgradeOrEnchant();
			}
			catch
			{
			}
			return;
		}

		ApplyFirstUpgradeOnlyAdjustments(__instance, upgrade);
		CardEditorUpgradeDeltaDebugLog.LogCardState("UpgradeInternal.Postfix.afterFirstUpgradeAdjustments", __instance, overrideData);

		try
		{
			__instance.DynamicVars.RecalculateForUpgradeOrEnchant();
		}
		catch
		{
		}
	}

	private static void ApplyNumericUpgradeAdjustments(CardModel card, CardUpgradeOverride upgrade, UpgradeSnapshot snapshot)
	{
		if (upgrade.EnergyCostDelta.HasValue && !card.EnergyCost.CostsX)
		{
			int baseCost = snapshot.EnergyCostBase;
			int vanillaUpgraded = card.EnergyCost.GetWithModifiers(CostModifiers.None);
			int vanillaDelta = vanillaUpgraded - baseCost;
			int desiredDelta = upgrade.EnergyCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				try
				{
					CardEditorOverrides.MarkEnergyCostJustUpgraded(card);
					card.EnergyCost.SetCustomBaseCost(Math.Max(-1, vanillaUpgraded + adjust));
				}
				catch
				{
				}
			}
		}

		if (upgrade.StarCostDelta.HasValue && !card.HasStarCostX)
		{
			int vanillaUpgraded = card.BaseStarCost;
			int vanillaDelta = vanillaUpgraded - snapshot.StarCostBase;
			int desiredDelta = upgrade.StarCostDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				CardEditorOverrides.SetBaseStarCostUnsafe(card, Math.Max(-1, vanillaUpgraded + adjust));
				CardEditorOverrides.MarkStarCostJustUpgraded(card);
			}
		}

		if (upgrade.ReplayCountDelta.HasValue)
		{
			int vanillaUpgraded = card.BaseReplayCount;
			int vanillaDelta = vanillaUpgraded - snapshot.ReplayCountBase;
			int desiredDelta = upgrade.ReplayCountDelta.Value;
			int adjust = desiredDelta - vanillaDelta;
			if (adjust != 0)
			{
				card.BaseReplayCount = Math.Max(0, vanillaUpgraded + adjust);
			}
		}

		if (upgrade.DynamicVarDeltas != null && upgrade.DynamicVarDeltas.Count > 0 && snapshot.DynamicVarBaseValues != null)
		{
			foreach ((string key, decimal desiredDelta) in upgrade.DynamicVarDeltas)
			{
				if (!snapshot.DynamicVarBaseValues.TryGetValue(key, out decimal baseValue))
				{
					continue;
				}
				if (!card.DynamicVars.TryGetValue(key, out var dynamicVar))
				{
					continue;
				}
				decimal vanillaUpgraded = dynamicVar.BaseValue;
				decimal vanillaDelta = vanillaUpgraded - baseValue;
				decimal adjust = desiredDelta - vanillaDelta;
				if (adjust != 0m)
				{
					dynamicVar.UpgradeValueBy(adjust);
				}
			}
		}
	}

	private static void ApplyFirstUpgradeOnlyAdjustments(CardModel card, CardUpgradeOverride upgrade)
	{
		if (upgrade.KeywordsToRemove != null)
		{
			foreach (CardKeyword keyword in upgrade.KeywordsToRemove)
			{
				if (keyword != CardKeyword.None && card.Keywords.Contains(keyword))
				{
					card.RemoveKeyword(keyword);
				}
			}
		}

		if (upgrade.KeywordsToAdd != null)
		{
			foreach (CardKeyword keyword in upgrade.KeywordsToAdd)
			{
				if (keyword != CardKeyword.None && !card.Keywords.Contains(keyword))
				{
					card.AddKeyword(keyword);
				}
			}
		}

		if (upgrade.EnchantmentId != null || upgrade.AfflictionId != null)
		{
			CardEditorOverrides.ApplyOverrideToCard(card, new CardOverride
			{
				EnchantmentId = upgrade.EnchantmentId,
				EnchantmentAmount = upgrade.EnchantmentAmount,
				AfflictionId = upgrade.AfflictionId,
				AfflictionAmount = upgrade.AfflictionAmount
			});
		}
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.FinalizeUpgradeInternal))]
public static class CardModel_FinalizeUpgradeInternal_Patch
{
	public static void Postfix(CardModel __instance)
	{
		// Intentionally empty: applying base overrides here would wipe vanilla upgrade deltas.
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal))]
public static class CardModel_DowngradeInternal_Patch
{
	public static void Postfix(CardModel __instance)
	{
		if (__instance == null || !__instance.IsMutable)
		{
			return;
		}
		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return;
		}

		CardEditorOverrides.RefreshCardAfterUpgradeStateChanged(__instance);
	}
}

// Manual patch class for GetDescriptionForPile - applied explicitly in Init()
public static class GetDescriptionForPile_ManualPatch
{
	public static bool Prefix(CardModel __instance, object? __1, MegaCrit.Sts2.Core.Entities.Creatures.Creature? __2, ref string __result)
	{
		CardEditorRunSelfScalingState.TryRestoreCard(__instance);
		if (__instance is CardEditorBuiltTinkerCard builtTinkerCard)
		{
			__result = builtTinkerCard.ResolveDisplayDescription(__2, IsUpgradePreview(__1));
			return false;
		}

		if (__instance is not CardEditorCreatedCardBase)
		{
			return true; // Continue to original for non-created cards
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GetDescriptionForPile(PileType.None, __2);
			return false;
		}

		__result = CreatedCardTextBuilder.Build(__instance, __2, isUpgradePreview: IsUpgradePreview(__1));
		CardEditorUpgradeDeltaDebugLog.LogDescription("Description.PrivateGetDescriptionForPile.created", __instance, PileType.None, IsUpgradePreview(__1), __result);
		return false; // Skip original for created cards
	}

	public static void Postfix(CardModel __instance, object? __1, MegaCrit.Sts2.Core.Entities.Creatures.Creature? __2, ref string __result)
	{
		if (__instance is CardEditorCreatedCardBase)
		{
			return;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GetDescriptionForPile(PileType.None, __2);
			return;
		}

		CardEditorVanillaDescriptionOverrideSupport.ApplyVanillaDescriptionPostfix(__instance, ref __result, __2, isUpgradePreview: IsUpgradePreview(__1));
		CardEditorUpgradeDeltaDebugLog.LogDescription("Description.PrivateGetDescriptionForPile.postfix", __instance, PileType.None, IsUpgradePreview(__1), __result);
	}

	private static bool IsUpgradePreview(object? previewType)
	{
		try
		{
			return previewType != null && Convert.ToInt32(previewType, System.Globalization.CultureInfo.InvariantCulture) == 1;
		}
		catch
		{
			return false;
		}
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), typeof(PileType), typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature))]
public static class CardModel_GetDescriptionForPile_Patch
{
	[HarmonyPriority(Priority.First)]
	public static bool Prefix(CardModel __instance, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string __result)
	{
		CardEditorRunSelfScalingState.TryRestoreCard(__instance);
		if (__instance is CardEditorBuiltTinkerCard builtTinkerCard)
		{
			__result = builtTinkerCard.ResolveDisplayDescription(target, isUpgradePreview: false);
			return false;
		}

		if (__instance is not CardEditorCreatedCardBase)
		{
			return true; // Continue to original for non-created cards
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GetDescriptionForPile(PileType.None, target);
			return false;
		}

		__result = CreatedCardTextBuilder.Build(__instance, target, isUpgradePreview: false);
		CardEditorUpgradeDeltaDebugLog.LogDescription("Description.PublicGetDescriptionForPile.created", __instance, PileType.None, isUpgradePreview: false, __result);
		return false; // Skip original for created cards
	}

	public static void Postfix(CardModel __instance, PileType pileType, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target, ref string __result)
	{
		if (__instance is CardEditorCreatedCardBase)
		{
			return;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GetDescriptionForPile(pileType, target);
			return;
		}

		CardEditorVanillaDescriptionOverrideSupport.ApplyVanillaDescriptionPostfix(__instance, ref __result, target, isUpgradePreview: false);
		CardEditorUpgradeDeltaDebugLog.LogDescription("Description.PublicGetDescriptionForPile.postfix", __instance, pileType, isUpgradePreview: false, __result);
	}
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview))]
public static class CardModel_GetDescriptionForUpgradePreview_Patch
{
	public static void Prefix(CardModel __instance)
	{
		CardEditorRunSelfScalingState.TryRestoreCard(__instance);
	}

	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (__instance is CardEditorCreatedCardBase)
		{
			return;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(__instance, out CardModel identitySource))
		{
			__result = identitySource.GetDescriptionForUpgradePreview();
			return;
		}

		CardEditorVanillaDescriptionOverrideSupport.ApplyVanillaDescriptionPostfix(__instance, ref __result, __instance.GetSafeCurrentTarget(), isUpgradePreview: true);
		CardEditorUpgradeDeltaDebugLog.LogDescription("Description.GetDescriptionForUpgradePreview.postfix", __instance, PileType.None, isUpgradePreview: true, __result);
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_RunSelfScalingRestore_Patch
{
	public static void Prefix(NCard __instance, PileType pileType, CardPreviewMode previewMode, out IDisposable? __state)
	{
		__state = null;
		try
		{
			CardEditorRunSelfScalingState.TryRestoreCard(__instance?.Model);
			__state = CardEditorCardVisualPreviewContext.Push(__instance?.Model, pileType, previewMode);
		}
		catch
		{
		}
	}

	public static void Finalizer(IDisposable? __state)
	{
		__state?.Dispose();
	}
}

internal static class TargetTypeOverrideDescriptionFix
{
	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null)
		{
			return;
		}

		CardOverride? overrideData = null;
		if (CardEditorUiState.TryGetDraftOverride(card.Id, out CardOverride draft) && draft.TargetType != null)
		{
			overrideData = draft;
		}
		else if (CardEditorOverrides.TryGet(card.Id, out CardOverride stored) && stored.TargetType != null)
		{
			overrideData = stored;
		}

		if (overrideData?.TargetType == null)
		{
			return;
		}

		TargetType overrideTargetType = overrideData.TargetType.Value;
		if (overrideTargetType == TargetType.None)
		{
			return;
		}

		TargetType vanillaTargetType;
		try
		{
			vanillaTargetType = ModelDb.GetById<CardModel>(card.Id).TargetType;
		}
		catch
		{
			return;
		}

		if (overrideTargetType == vanillaTargetType)
		{
			return;
		}

		string labelFallback = overrideTargetType switch
		{
			TargetType.AnyEnemy => "Any Enemy",
			TargetType.RandomEnemy => "Random Enemy",
			TargetType.AllEnemies => "All Enemies",
			TargetType.Self => "Self",
			TargetType.AnyAlly => "Any Ally",
			TargetType.AnyPlayer => "Any Player",
			TargetType.AllAllies => "All Allies",
			TargetType.TargetedNoCreature => "No Creature",
			_ => overrideTargetType.ToString()
		};

		string label = CardEditorLoc.Enum("targetType", overrideTargetType, labelFallback);
		string template = CardEditorLoc.T("cardText.targetingOverride", "Targeting: {Target}.");
		string line = template.Contains("{Target}", StringComparison.Ordinal)
			? template.Replace("{Target}", label)
			: template.Contains("{0}", StringComparison.Ordinal)
				? string.Format(CultureInfo.InvariantCulture, template, label)
				: $"Targeting: {label}.";
		if (ContainsDescriptionLine(description, line))
		{
			return;
		}
		description = string.IsNullOrEmpty(description) ? line : description + "\n" + line;
	}

	private static bool ContainsDescriptionLine(string description, string line)
	{
		if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(line))
		{
			return false;
		}

		foreach (string existingLine in description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			if (string.Equals(existingLine.Trim(), line.Trim(), StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}
}

internal static class TemporaryStatDurationDescriptionFix
{
	private const string StrengthMarker = "[gold]Strength[/gold] this turn";
	private const string DexterityMarker = "[gold]Dexterity[/gold] this turn";
	private const string FocusMarker = "[gold]Focus[/gold] this turn";

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}

		if (!TryGetOverrideData(card, out CardOverride? overrideData))
		{
			return;
		}

		if (overrideData.TemporaryStrengthDuration != null && IsCardPrimaryPowerType<TemporaryStrengthPower>(card))
		{
			ReplaceMarker(ref description, StrengthMarker, overrideData.TemporaryStrengthDuration.Value, overrideData.TemporaryStrengthTurns);
		}

		if (overrideData.TemporaryDexterityDuration != null && IsCardPrimaryPowerType<TemporaryDexterityPower>(card))
		{
			ReplaceMarker(ref description, DexterityMarker, overrideData.TemporaryDexterityDuration.Value, overrideData.TemporaryDexterityTurns);
		}

		if (overrideData.TemporaryFocusDuration != null && IsCardPrimaryPowerType<TemporaryFocusPower>(card))
		{
			ReplaceMarker(ref description, FocusMarker, overrideData.TemporaryFocusDuration.Value, overrideData.TemporaryFocusTurns);
		}
	}

	private static bool TryGetOverrideData(CardModel card, out CardOverride? overrideData)
	{
		overrideData = null;
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride effective) && HasAnyDurationOverrides(effective))
		{
			overrideData = effective;
			return true;
		}

		return false;
	}

	private static bool HasAnyDurationOverrides(CardOverride overrideData)
	{
		return overrideData.TemporaryStrengthDuration != null
			|| overrideData.TemporaryDexterityDuration != null
			|| overrideData.TemporaryFocusDuration != null;
	}

	private static bool IsCardPrimaryPowerType<T>(CardModel card) where T : PowerModel
	{
		if (card == null)
		{
			return false;
		}

		try
		{
			ModelId powerId = new ModelId("POWER", card.Id.Entry + "_POWER");
			return ModelDb.GetByIdOrNull<PowerModel>(powerId) is T;
		}
		catch
		{
			return false;
		}
	}

	private static void ReplaceMarker(ref string description, string marker, CardKeywordGrantDuration duration, int? turns)
	{
		if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(marker))
		{
			return;
		}
		if (!description.Contains(marker, StringComparison.Ordinal))
		{
			return;
		}

		switch (duration)
		{
			case CardKeywordGrantDuration.ThisCombat:
				description = description.Replace(marker, marker.Replace("this turn", "this combat", StringComparison.Ordinal), StringComparison.Ordinal);
				break;
			case CardKeywordGrantDuration.Turns:
			{
				int count = Math.Clamp(turns.GetValueOrDefault(2), 1, 99);
				if (count <= 1)
				{
					return;
				}
				description = description.Replace(marker, marker.Replace("this turn", $"for the next {count} turns", StringComparison.Ordinal), StringComparison.Ordinal);
				break;
			}
		}
	}
}

internal static class RetainHandDurationDescriptionFix
{
	private const string Marker = "[gold]Hand[/gold] this turn.";

	private static readonly ModelId _convergenceId = ModelDb.GetId<Convergence>();
	private static readonly ModelId _equilibriumId = ModelDb.GetId<Equilibrium>();
	private static readonly ModelId _salvoId = ModelDb.GetId<Salvo>();
	private static readonly ModelId _retainHandPowerId = ModelDb.GetId<RetainHandPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _convergenceId && card.Id != _equilibriumId && card.Id != _salvoId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int? overrideTurns = TryGetTurnsFromOverride(card);
		if (overrideTurns != null && overrideTurns.Value == 0)
		{
			RemoveLinesContainingMarker(ref description);
			return;
		}

		int turns = overrideTurns ?? TryGetEquilibriumTurns(card) ?? 1;
		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "[gold]Hand[/gold] this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"[gold]Hand[/gold] for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static void RemoveLinesContainingMarker(ref string description)
	{
		string[] lines = description.Split('\n');
		description = string.Join("\n", lines.Where(line => !line.Contains(Marker, StringComparison.Ordinal)));
		description = description.Trim();
	}

	private static int? TryGetTurnsFromOverride(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.TryGetValue(_retainHandPowerId, out decimal storedAmount))
		{
			return (int)storedAmount;
		}

		return null;
	}

	private static int? TryGetEquilibriumTurns(CardModel card)
	{
		if (card == null || card.Id != _equilibriumId)
		{
			return null;
		}
		try
		{
			if (card.DynamicVars.TryGetValue("Equilibrium", out var dv))
			{
				return (int)dv.BaseValue;
			}
		}
		catch
		{
		}
		return null;
	}
}

internal static class NoDrawDurationDescriptionFix
{
	private const string Marker = "You cannot draw additional cards this turn.";

	private static readonly ModelId _battleTranceId = ModelDb.GetId<BattleTrance>();
	private static readonly ModelId _bulletTimeId = ModelDb.GetId<BulletTime>();
	private static readonly ModelId _noDrawPowerId = ModelDb.GetId<NoDrawPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _battleTranceId && card.Id != _bulletTimeId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int? overrideTurns = TryGetTurnsFromOverride(card);
		if (overrideTurns == null)
		{
			return;
		}

		int turns = overrideTurns.Value;
		if (turns == 0)
		{
			RemoveNoDrawSentence(ref description);
			return;
		}

		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "You cannot draw additional cards this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"You cannot draw additional cards for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static void RemoveNoDrawSentence(ref string description)
	{
		description = description.Replace("\n" + Marker, string.Empty, StringComparison.Ordinal);
		description = description.Replace(Marker + "\n", string.Empty, StringComparison.Ordinal);
		description = description.Replace(Marker + " ", string.Empty, StringComparison.Ordinal);
		description = description.Replace(Marker, string.Empty, StringComparison.Ordinal);
		description = description.Trim();
	}

	private static int? TryGetTurnsFromOverride(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.TryGetValue(_noDrawPowerId, out decimal storedAmount))
		{
			return (int)storedAmount;
		}

		return null;
	}
}

internal static class ConquerorDurationDescriptionFix
{
	private const string Marker = "the enemy this turn.";

	private static readonly ModelId _conquerorId = ModelDb.GetId<Conqueror>();
	private static readonly ModelId _conquerorPowerId = ModelDb.GetId<ConquerorPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _conquerorId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int? overrideTurns = TryGetTurnsFromOverride(card);
		if (overrideTurns == null)
		{
			return;
		}

		int turns = overrideTurns.Value;
		if (turns == 0)
		{
			RemoveLinesContainingMarker(ref description);
			return;
		}

		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "the enemy this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"the enemy for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static void RemoveLinesContainingMarker(ref string description)
	{
		string[] lines = description.Split('\n');
		description = string.Join("\n", lines.Where(line => !line.Contains(Marker, StringComparison.Ordinal)));
		description = description.Trim();
	}

	private static int? TryGetTurnsFromOverride(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.TryGetValue(_conquerorPowerId, out decimal storedAmount))
		{
			return (int)storedAmount;
		}

		return null;
	}
}

internal static class ReflectDurationDescriptionFix
{
	private const string Marker = "your attacker this turn.";

	private static readonly ModelId _reflectId = ModelDb.GetId<Reflect>();
	private static readonly ModelId _reflectPowerId = ModelDb.GetId<ReflectPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _reflectId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int? overrideTurns = TryGetTurnsFromOverride(card);
		if (overrideTurns == null)
		{
			return;
		}

		int turns = overrideTurns.Value;
		if (turns == 0)
		{
			RemoveLinesContainingMarker(ref description);
			return;
		}

		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "your attacker this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"your attacker for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static void RemoveLinesContainingMarker(ref string description)
	{
		string[] lines = description.Split('\n');
		description = string.Join("\n", lines.Where(line => !line.Contains(Marker, StringComparison.Ordinal)));
		description = description.Trim();
	}

	private static int? TryGetTurnsFromOverride(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.TryGetValue(_reflectPowerId, out decimal storedAmount))
		{
			return (int)storedAmount;
		}

		return null;
	}
}

internal static class ColossusDurationDescriptionFix
{
	private const string Marker = "enemies this turn.";

	private static readonly ModelId _colossusId = ModelDb.GetId<Colossus>();
	private static readonly ModelId _colossusPowerId = ModelDb.GetId<ColossusPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _colossusId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int turns = TryGetTurns(card) ?? 1;
		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "enemies this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"enemies for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static int? TryGetTurns(CardModel card)
	{
		if (card == null)
		{
			return null;
		}

		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored))
		{
			if (stored.PowerAmounts != null && stored.PowerAmounts.TryGetValue(_colossusPowerId, out decimal storedAmount))
			{
				return (int)storedAmount;
			}
			if (stored.DynamicVarBaseValues != null && stored.DynamicVarBaseValues.TryGetValue("Colossus", out decimal storedTurns))
			{
				return (int)storedTurns;
			}
		}

		try
		{
			if (card.DynamicVars.TryGetValue("Colossus", out var dv))
			{
				return (int)dv.BaseValue;
			}
		}
		catch
		{
		}

		return null;
	}
}

internal static class BlurDurationDescriptionFix
{
	private const string Marker = "at the start of your next turn.";

	private static readonly ModelId _blurId = ModelDb.GetId<Blur>();
	private static readonly ModelId _blurPowerId = ModelDb.GetId<BlurPower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (card.Id != _blurId)
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		int turns = TryGetTurns(card) ?? 1;
		turns = Math.Max(1, turns);
		if (turns <= 1)
		{
			return;
		}

		if (turns >= 99)
		{
			description = description.Replace(Marker, "at the start of your turn this combat.", StringComparison.Ordinal);
			return;
		}

		description = description.Replace(Marker, $"at the start of your turn for the next {turns} turns.", StringComparison.Ordinal);
	}

	private static int? TryGetTurns(CardModel card)
	{
		if (card == null)
		{
			return null;
		}

		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored))
		{
			if (stored.PowerAmounts != null && stored.PowerAmounts.TryGetValue(_blurPowerId, out decimal storedAmount))
			{
				return (int)storedAmount;
			}
			if (stored.DynamicVarBaseValues != null && stored.DynamicVarBaseValues.TryGetValue("Blur", out decimal storedTurns))
			{
				return (int)storedTurns;
			}
		}

		try
		{
			if (card.DynamicVars.TryGetValue("Blur", out var dv))
			{
				return (int)dv.BaseValue;
			}
		}
		catch
		{
		}

		return null;
	}
}

internal static class HandTrickSlyDurationDescriptionFix
{
	private const string Marker = "[gold]Hand[/gold] this turn.";

	private static readonly ModelId _handTrickId = ModelDb.GetId<HandTrick>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || card.Id != _handTrickId || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		CardOverride? overrideData = null;
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride effective) && effective.SlyGrantDuration != null)
		{
			overrideData = effective;
		}

		if (overrideData?.SlyGrantDuration == null)
		{
			return;
		}

		switch (overrideData.SlyGrantDuration.Value)
		{
			case CardKeywordGrantDuration.ThisCombat:
				description = description.Replace(Marker, "[gold]Hand[/gold] this combat.", StringComparison.Ordinal);
				break;
			case CardKeywordGrantDuration.Turns:
			{
				int turns = Math.Clamp(overrideData.SlyGrantTurns.GetValueOrDefault(2), 1, 99);
				description = description.Replace(Marker, $"[gold]Hand[/gold] for the next {turns} turns.", StringComparison.Ordinal);
				break;
			}
		}
	}
}

internal static class SealedThroneDescriptionFix
{
	private static readonly ModelId _sealedThroneId = ModelDb.GetId<TheSealedThrone>();
	private static readonly ModelId _sealedThronePowerId = ModelDb.GetId<TheSealedThronePower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || card.Id != _sealedThroneId)
		{
			return;
		}

		int starsGained = TryGetStarsGained(card) ?? 1;
		string replacement = BuildStarIconReplacement(starsGained);
		description = description.Replace(StarIconsFormatter.starIconSprite, replacement);
	}

	private static int? TryGetStarsGained(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.TryGetValue(_sealedThronePowerId, out decimal storedAmount))
		{
			return (int)storedAmount;
		}

		return null;
	}

	private static string BuildStarIconReplacement(int count)
	{
		if (count <= 0)
		{
			return "0 " + StarIconsFormatter.starIconSprite;
		}
		if (count == 1)
		{
			return StarIconsFormatter.starIconSprite;
		}

		StringBuilder sb = new StringBuilder(StarIconsFormatter.starIconSprite.Length * count);
		for (int i = 0; i < count; i++)
		{
			sb.Append(StarIconsFormatter.starIconSprite);
		}
		return sb.ToString();
	}
}

internal static class KinglyKickDescriptionFix
{
	private static readonly ModelId _kinglyKickId = ModelDb.GetId<KinglyKick>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || card.Id != _kinglyKickId)
		{
			return;
		}

		int? reduction = TryGetReduction(card);
		if (!reduction.HasValue)
		{
			return;
		}

		int value = Math.Max(0, reduction.Value);
		if (value == 1)
		{
			return;
		}

		int beforeLines = CountBeforeDescriptionKeywordLines(card);
		int targetLineIndex = beforeLines + 1;

		string[] lines = description.Split('\n');
		if (targetLineIndex < 0 || targetLineIndex >= lines.Length)
		{
			return;
		}

		string updatedLine = ReplaceFirstInteger(lines[targetLineIndex], value);
		if (string.Equals(updatedLine, lines[targetLineIndex], StringComparison.Ordinal))
		{
			return;
		}

		lines[targetLineIndex] = updatedLine;
		description = string.Join('\n', lines);
	}

	private static int? TryGetReduction(CardModel card)
	{
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored) && stored.DrawCostReduction.HasValue)
		{
			return stored.DrawCostReduction.Value;
		}

		return null;
	}

	private static int CountBeforeDescriptionKeywordLines(CardModel card)
	{
		int count = 0;
		foreach (CardKeyword keyword in CardKeywordOrder.beforeDescription)
		{
			bool include = keyword switch
			{
				CardKeyword.Sly => card.IsSlyThisTurn,
				CardKeyword.Retain => card.ShouldRetainThisTurn,
				_ => card.Keywords.Contains(keyword)
			};
			if (include)
			{
				count++;
			}
		}
		return count;
	}

	private static string ReplaceFirstInteger(string text, int value)
	{
		int start = -1;
		int end = -1;
		for (int i = 0; i < text.Length; i++)
		{
			if (char.IsDigit(text[i]))
			{
				start = i;
				end = i + 1;
				while (end < text.Length && char.IsDigit(text[end]))
				{
					end++;
				}
				break;
			}
		}

		if (start < 0 || end <= start)
		{
			return text;
		}

		return text.Substring(0, start) + value + text.Substring(end);
	}
}

internal static class ResonanceEnemyStrengthLossDescriptionFix
{
	private const string Marker = "ALL enemies lose 1 [gold]Strength[/gold].";

	private static readonly ModelId _resonanceId = ModelDb.GetId<Resonance>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || card.Id != _resonanceId || string.IsNullOrWhiteSpace(description))
		{
			return;
		}
		if (!description.Contains(Marker, StringComparison.Ordinal))
		{
			return;
		}

		if (!TryGetLoss(card, out int loss))
		{
			return;
		}

		loss = Math.Max(0, loss);
		if (loss == 1)
		{
			return;
		}

		if (loss == 0)
		{
			description = description.Replace(" " + Marker, string.Empty, StringComparison.Ordinal);
			description = description.Replace("\n" + Marker, string.Empty, StringComparison.Ordinal);
			description = description.Replace(Marker, string.Empty, StringComparison.Ordinal);
			description = description.Trim();
			return;
		}

		description = description.Replace(Marker, $"ALL enemies lose {loss} [gold]Strength[/gold].", StringComparison.Ordinal);
	}

	private static bool TryGetLoss(CardModel card, out int loss)
	{
		loss = 1;

		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.DynamicVarBaseValues != null
			&& stored.DynamicVarBaseValues.TryGetValue(CardEditorOverrideKeys.ResonanceEnemyStrengthLoss, out decimal storedLoss))
		{
			loss = (int)storedLoss;
			return true;
		}

		return false;
	}
}

internal static class HardcodedPowerAmountDescriptionFix
{
	private static readonly ModelId _predatorId = ModelDb.GetId<Predator>();
	private static readonly ModelId _drawCardsNextTurnPowerId = ModelDb.GetId<DrawCardsNextTurnPower>();

	private static readonly ModelId _nightmareId = ModelDb.GetId<Nightmare>();
	private static readonly ModelId _nightmarePowerId = ModelDb.GetId<NightmarePower>();

	public static void TryFix(CardModel card, ref string description)
	{
		if (card == null || string.IsNullOrWhiteSpace(description))
		{
			return;
		}

		if (!TryGetOverrideData(card, out CardOverride? overrideData) || overrideData.PowerAmounts == null || overrideData.PowerAmounts.Count == 0)
		{
			return;
		}

		if (card.Id == _predatorId)
		{
			const int defaultCards = 2;
			int desired = GetDesiredPowerAmount(overrideData, _drawCardsNextTurnPowerId, defaultCards);
			if (desired != defaultCards)
			{
				description = ReplaceNextTurnDraw(description, defaultCards, desired);
			}
			return;
		}

		if (card.Id == _nightmareId)
		{
			const int defaultCopies = 3;
			int desired = GetDesiredPowerAmount(overrideData, _nightmarePowerId, defaultCopies);
			if (desired != defaultCopies)
			{
				description = ReplaceNextTurnCopies(description, defaultCopies, desired);
			}
		}
	}

	private static int GetDesiredPowerAmount(CardOverride overrideData, ModelId powerId, int defaultValue)
	{
		if (overrideData.PowerAmounts != null && overrideData.PowerAmounts.TryGetValue(powerId, out decimal value))
		{
			return (int)value;
		}
		return defaultValue;
	}

	private static bool TryGetOverrideData(CardModel card, out CardOverride? overrideData)
	{
		overrideData = null;

		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride stored)
			&& stored.PowerAmounts != null
			&& stored.PowerAmounts.Count > 0)
		{
			overrideData = stored;
			return true;
		}

		return false;
	}

	private static string ReplaceNextTurnDraw(string description, int original, int desired)
	{
		string oldLine = $"Next turn, draw {original} cards.";
		if (description.Contains(oldLine, StringComparison.Ordinal))
		{
			string desiredLine = $"Next turn, draw {desired} {(desired == 1 ? "card" : "cards")}.";
			return description.Replace(oldLine, desiredLine, StringComparison.Ordinal);
		}
		return description;
	}

	private static string ReplaceNextTurnCopies(string description, int original, int desired)
	{
		string oldFragment = $"Next turn, add {original} copies";
		if (description.Contains(oldFragment, StringComparison.Ordinal))
		{
			string desiredFragment = $"Next turn, add {desired} {(desired == 1 ? "copy" : "copies")}";
			return description.Replace(oldFragment, desiredFragment, StringComparison.Ordinal);
		}
		return description;
	}
}

[HarmonyPatch(typeof(KinglyKick), nameof(KinglyKick.AfterCardDrawn))]
public static class KinglyKick_AfterCardDrawn_CostReductionOverride_Patch
{
	public static void Postfix(KinglyKick __instance, CardModel card)
	{
		if (__instance == null || card != __instance)
		{
			return;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(__instance, out CardOverride overrideData) || !overrideData.DrawCostReduction.HasValue)
		{
			return;
		}

		int desiredReduction = Math.Max(0, overrideData.DrawCostReduction.Value);
		int adjust = desiredReduction - 1; // vanilla reduces by 1 already
		if (adjust != 0)
		{
			__instance.EnergyCost.AddThisCombat(-adjust);
		}
	}
}

[HarmonyPatch(typeof(NMainMenu), "_Ready")]
public static class MainMenu_Ready_Patch
{
	private const int MainMenuButtonDuplicateFlags =
		(int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation);

	private static MethodInfo? _focusMethod;
	private static MethodInfo? _unfocusMethod;
	private static bool _startupPresetsApplied;

	public static void Postfix(NMainMenu __instance)
	{
		TryAddEditorButton(__instance);
		TryAddCreatorButton(__instance);
		RefreshCustomButtons(__instance);
		TryApplyStartupPresetsOnce();
		NCardEditorPopup.TryQueueUiWarmup(__instance);
	}

	private static void TryApplyStartupPresetsOnce()
	{
		if (_startupPresetsApplied)
		{
			return;
		}
		_startupPresetsApplied = true;

		try
		{
			if (CardEditorPresetStore.TryLoadStartupPreset(out string editorPresetName, out Dictionary<ModelId, CardOverride> overrides, out Dictionary<ModelId, List<ModelId>> baseDecks))
			{
				CardEditorOverrides.ReplaceAll(overrides);
				CardEditorBaseDeckStore.ImportSnapshot(baseDecks);
				CardEditorBaseDeckUiState.EnsureValidCharacter();
				CardEditorMod.VerboseLog($"[CardEditor] Auto-loaded preset at startup: '{editorPresetName}' ({overrides.Count} cards, {baseDecks.Count} base decks)");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading startup preset: {ex}");
		}

		try
		{
			if (CardEditorCreatorPresetStore.TryLoadStartupPreset(out string creatorPresetName, out Dictionary<ModelId, CardEditorCreatedCardDefinition> defs))
			{
				CardEditorCreatedCardsStore.ImportSnapshot(defs);
				CardEditorMod.VerboseLog($"[CardEditor] Auto-loaded creator preset at startup: '{creatorPresetName}' ({defs.Count} cards)");
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed loading startup creator preset: {ex}");
		}
	}

	private static void TryAddEditorButton(NMainMenu menu)
	{
		Control buttonContainer = menu.GetNodeOrNull<Control>("MainMenuTextButtons");
		if (buttonContainer == null || buttonContainer.HasNode("EditorButton"))
		{
			return;
		}
		NMainMenuTextButton compendium = menu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/CompendiumButton");
		if (compendium == null)
		{
			return;
		}
		NMainMenuTextButton editorButton = (NMainMenuTextButton)compendium.Duplicate(MainMenuButtonDuplicateFlags);
		editorButton.Name = "EditorButton";
		Label label = editorButton.GetNode<Label>("Label");
		label.Text = CardEditorLoc.T("menu.editor", "Editor");
		editorButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OpenEditor(menu)));
		ConnectFocusHooks(menu, editorButton);
		buttonContainer.AddChild(editorButton);
		int insertAt = compendium.GetIndex() + 1;
		buttonContainer.MoveChild(editorButton, insertAt);

		// Ensure initial visuals match other main menu buttons. Since we're adding after NMainMenu._Ready,
		// the base menu's initialization may not have applied focus/unfocus styling to our duplicated node yet.
		RefreshCustomButton(menu, editorButton, "menu.editor", "Editor");
	}

	private static void ConnectFocusHooks(NMainMenu menu, NMainMenuTextButton button)
	{
		_focusMethod ??= typeof(NMainMenu).GetMethod("MainMenuButtonFocused", BindingFlags.Instance | BindingFlags.NonPublic);
		_unfocusMethod ??= typeof(NMainMenu).GetMethod("MainMenuButtonUnfocused", BindingFlags.Instance | BindingFlags.NonPublic);
		if (_focusMethod != null)
		{
			button.Connect(NClickableControl.SignalName.Focused, Callable.From<NMainMenuTextButton>(b => _focusMethod.Invoke(menu, new object[] { b })));
		}
		if (_unfocusMethod != null)
		{
			button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NMainMenuTextButton>(b => _unfocusMethod.Invoke(menu, new object[] { b })));
		}
	}

	private static void TryApplyInitialMainMenuButtonStyle(NMainMenu menu, NMainMenuTextButton button)
	{
		if (menu == null || button == null || !GodotObject.IsInstanceValid(menu) || !GodotObject.IsInstanceValid(button))
		{
			return;
		}

		try
		{
			_focusMethod ??= typeof(NMainMenu).GetMethod("MainMenuButtonFocused", BindingFlags.Instance | BindingFlags.NonPublic);
			_unfocusMethod ??= typeof(NMainMenu).GetMethod("MainMenuButtonUnfocused", BindingFlags.Instance | BindingFlags.NonPublic);
			_unfocusMethod?.Invoke(menu, new object[] { button });
		}
		catch
		{
		}
	}

	internal static void RefreshCustomButtons(NMainMenu menu)
	{
		if (menu == null || !GodotObject.IsInstanceValid(menu))
		{
			return;
		}

		Control buttonContainer = menu.GetNodeOrNull<Control>("MainMenuTextButtons");
		if (buttonContainer == null)
		{
			return;
		}

		RefreshCustomButton(menu, buttonContainer.GetNodeOrNull<NMainMenuTextButton>("EditorButton"), "menu.editor", "Editor");
		RefreshCustomButton(menu, buttonContainer.GetNodeOrNull<NMainMenuTextButton>("CreatorButton"), "menu.creator", "Creator");
	}

	private static void RefreshCustomButton(NMainMenu menu, NMainMenuTextButton? button, string locKey, string fallback)
	{
		if (button == null || !GodotObject.IsInstanceValid(button))
		{
			return;
		}

		Label? label = button.GetNodeOrNull<Label>("Label");
		if (label != null)
		{
			label.Text = CardEditorLoc.T(locKey, fallback);
			label.Modulate = Colors.White;
		}

		button.Visible = true;
		button.MouseFilter = Control.MouseFilterEnum.Stop;
		button.Enable();
		Callable.From(() => TryApplyInitialMainMenuButtonStyle(menu, button)).CallDeferred();
	}

	private static void OpenEditor(NMainMenu menu)
	{
		CardEditorLibrarySelectionState.ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.Editor;
		NCardLibrary library = menu.SubmenuStack.GetSubmenuType<NCardLibrary>();
		IRunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState != null)
		{
			library.Initialize(runState);
		}
		Callable.From(() =>
		{
			menu.SubmenuStack.Push(library);
			CardEditorUiState.SetLastLibrary(library);
			Callable.From(() => CardEditorBaseDeckBookmarkHooks.Sync(library)).CallDeferred();
		}).CallDeferred();
	}

	private static void TryAddCreatorButton(NMainMenu menu)
	{
		Control buttonContainer = menu.GetNodeOrNull<Control>("MainMenuTextButtons");
		if (buttonContainer == null || buttonContainer.HasNode("CreatorButton"))
		{
			return;
		}
		NMainMenuTextButton compendium = menu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/CompendiumButton");
		if (compendium == null)
		{
			return;
		}

		NMainMenuTextButton creatorButton = (NMainMenuTextButton)compendium.Duplicate(MainMenuButtonDuplicateFlags);
		creatorButton.Name = "CreatorButton";
		Label label = creatorButton.GetNode<Label>("Label");
		label.Text = CardEditorLoc.T("menu.creator", "Creator");
		creatorButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OpenCreator(menu)));
		ConnectFocusHooks(menu, creatorButton);
		buttonContainer.AddChild(creatorButton);

		int insertAt = buttonContainer.GetNodeOrNull("EditorButton")?.GetIndex() + 1 ?? compendium.GetIndex() + 2;
		buttonContainer.MoveChild(creatorButton, insertAt);

		RefreshCustomButton(menu, creatorButton, "menu.creator", "Creator");
	}

	private static void OpenCreator(NMainMenu menu)
	{
		CardEditorMod.VerboseLog("[CardEditor] OpenCreator: Setting Mode = Creator");
		CardEditorLibrarySelectionState.ClearSelections();
		CardEditorUiState.Mode = CardEditorLibraryMode.Creator;
		NCardLibrary library = menu.SubmenuStack.GetSubmenuType<NCardLibrary>();
		IRunState? runState = RunManager.Instance.DebugOnlyGetState();
		if (runState != null)
		{
			library.Initialize(runState);
		}
		CardEditorMod.VerboseLog("[CardEditor] OpenCreator: Pushing library (deferred)");
		Callable.From(() => menu.SubmenuStack.Push(library)).CallDeferred();
	}
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.RefreshButtons))]
public static class MainMenu_RefreshButtons_CardEditorButtons_Patch
{
	public static void Postfix(NMainMenu __instance)
	{
		MainMenu_Ready_Patch.RefreshCustomButtons(__instance);
	}
}

[HarmonyPatch(typeof(NMainMenu), "OnSubmenuStackChanged")]
public static class MainMenu_SubmenuStackChanged_CardEditorButtons_Patch
{
	public static void Postfix(NMainMenu __instance)
	{
		MainMenu_Ready_Patch.RefreshCustomButtons(__instance);
	}
}

[HarmonyPatch(typeof(NCardLibrary), "ShowCardDetail")]
public static class CardLibrary_ShowCardDetail_Patch
{
	private const string PopupOpenBlockerName = "CardEditorPopupOpenBlocker";

	private static Control? AddPopupOpenBlocker()
	{
		Node? host = NModalContainer.Instance;
		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			host = NGame.Instance;
		}

		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			return null;
		}

		host.GetNodeOrNull<Control>(PopupOpenBlockerName)?.QueueFreeSafely();

		Control blocker = new()
		{
			Name = PopupOpenBlockerName,
			ZAsRelative = false,
			ZIndex = 500,
			Visible = true,
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.Arrow
		};
		blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		ColorRect backstop = new()
		{
			Color = new Color(0f, 0f, 0f, 0.01f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		blocker.AddChild(backstop);

		host.AddChild(blocker);
		host.MoveChild(blocker, host.GetChildCount() - 1);
		return blocker;
	}

	private static void OpenEditorPopupDeferred(ModelId cardId, NCardLibrary library)
	{
		Callable.From(() =>
		{
			Control? blocker = AddPopupOpenBlocker();
			Callable.From(() => CompleteEditorPopupOpen(cardId, library, blocker)).CallDeferred();
		}).CallDeferred();
	}

	private static void CompleteEditorPopupOpen(ModelId cardId, NCardLibrary library, Control? blocker)
	{
		ulong openStartMs = Time.GetTicksMsec();
		try
		{
			if (cardId == null || cardId == ModelId.none || library == null || !GodotObject.IsInstanceValid(library))
			{
				return;
			}

			CardEditorCustomPortraitLoader.TryGetCustomPortrait(cardId, out _);

			Action onApplied = () =>
			{
				if (GodotObject.IsInstanceValid(library))
				{
					CardEditorUiState.RefreshLibrary(library);
				}
			};

			CardModel canonical = ModelDb.GetById<CardModel>(cardId);
			CardModel preview = CardEditorOverrides.BuildPreview(canonical);
			Log.Info($"[CardEditor][PopupLag] OpenFromLibrary start card={cardId} t={Time.GetTicksMsec()}");
			NCardEditorPopup popup = NCardEditorPopup.GetOrCreatePersistentPopup(preview, onApplied);

			CardEditorMod.VerboseLog("[CardEditor] Opening persistent popup from cache host");
			if (popup.GetParent() == null)
			{
				NGame.Instance?.AddChildSafely(popup);
			}

			popup.ForceLayoutRefreshNow();
			Callable.From(popup.ForceLayoutRefreshNow).CallDeferred();
			CardEditorBaseDeckPanelHooks.RefreshLastLibrary();
			CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary();
			Callable.From(CardEditorBaseDeckPanelHooks.RefreshLastLibrary).CallDeferred();
			Callable.From(CardEditorBaseDeckBookmarkHooks.RefreshLastLibrary).CallDeferred();

			ulong openElapsedMs = Time.GetTicksMsec() - openStartMs;
			Log.Info($"[CardEditor][PopupLag] OpenFromLibrary end card={cardId} elapsedMs={openElapsedMs} popupParent={(popup.GetParent() != null)}");
			CardEditorMod.VerboseLog($"[CardEditor][Perf] OpenEditorPopup card={cardId} elapsedMs={openElapsedMs}");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed opening editor popup for {cardId}: {ex}");
		}
		finally
		{
			blocker?.QueueFreeSafely();
		}
	}

	public static bool Prefix(NCardHolder holder, NCardLibrary __instance)
	{
		CardEditorMod.VerboseLog("[CardEditor] ShowCardDetail prefix");
		if ((CardEditorUiState.IsBaseDeckActive || CardEditorUiState.IsBaseDeckAddActive) &&
			CardEditorBaseDeckLibraryHelper.ShouldSuppressShowCardDetailPopup())
		{
			return false;
		}

		if (holder?.CardModel != null &&
			CardEditorBaseDeckLibraryHelper.TryConsumePendingCardDetailAction(holder, out bool isRightClick, out _))
		{
			CardEditorBaseDeckLibraryHelper.ArmShowCardDetailSuppression(80);

			if (CardEditorUiState.IsBaseDeckActive && !isRightClick)
			{
				CardEditorBaseDeckUiState.ToggleSelection(__instance, holder);
				return false;
			}

			if (CardEditorUiState.IsBaseDeckAddActive && !isRightClick)
			{
				CardEditorBaseDeckUiState.ToggleSelection(__instance, holder);
				return false;
			}
		}

		if (!CardEditorUiState.IsActive)
		{
			return true;
		}
		if (holder?.CardModel == null)
		{
			return false;
		}
		OpenEditorPopupDeferred(holder.CardModel.Id, __instance);
		return false;
	}
}

[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
public static class CardLibrary_Ready_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		CardEditorPresetPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
	}
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuOpened))]
public static class CardLibrary_OnSubmenuOpened_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		CardEditorPresetPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
	}
}

internal static class CardEditorPresetPanelHooks
{
	private const string OpenMetaKey = "card_editor_preset_panel_open";

	public static bool IsOpen(NCardLibrary library)
	{
		return library != null && library.HasMeta(OpenMetaKey) && library.GetMeta(OpenMetaKey).AsBool();
	}

	public static void SetOpen(NCardLibrary library, bool open)
	{
		if (library == null)
		{
			return;
		}

		library.SetMeta(OpenMetaKey, open);
	}

	public static void ToggleOpen(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		SetOpen(library, !IsOpen(library));
		Sync(library);
	}

	public static void Sync(NCardLibrary library)
	{
		if (library == null)
		{
			return;
		}

		NCardEditorPresetPanel? panel = library.GetNodeOrNull<NCardEditorPresetPanel>("CardEditorPresetPanel");
		NCardEditorPresetToggleButton? toggleButton = library.GetNodeOrNull<NCardEditorPresetToggleButton>("CardEditorPresetToggleButton");
		NCardEditorBaseDeckPanel? baseDeckPanel = library.GetNodeOrNull<NCardEditorBaseDeckPanel>("CardEditorBaseDeckPanel");
		bool shouldShowPanel =
			CardEditorUiState.IsEditorActive ||
			CardEditorUiState.IsCreatorActive ||
			CardEditorUiState.IsBaseDeckActive ||
			CardEditorUiState.IsBaseDeckAddActive;
		bool shouldShowStandaloneButton = CardEditorUiState.IsEditorActive || CardEditorUiState.IsCreatorActive;
		if (CardEditorMod.IsVerboseEditorLogging)
		{
			CardEditorMod.VerboseLog($"[CardEditor][PresetButton] Sync mode={CardEditorUiState.Mode} shouldShowPanel={shouldShowPanel} shouldShowStandalone={shouldShowStandaloneButton} panelExists={panel != null} buttonExists={toggleButton != null} baseDeckPanelExists={baseDeckPanel != null} open={IsOpen(library)}");
		}
		if (shouldShowPanel)
		{
			CardEditorUiState.SetLastLibrary(library);
			if (baseDeckPanel == null)
			{
				CardEditorBaseDeckPanelHooks.Sync(library);
				baseDeckPanel = library.GetNodeOrNull<NCardEditorBaseDeckPanel>("CardEditorBaseDeckPanel");
			}

			if (panel == null)
			{
				panel = NCardEditorPresetPanel.Create(library, creatorMode: CardEditorUiState.IsCreatorActive);
				library.AddChild(panel);
			}

			if (toggleButton != null)
			{
				toggleButton.QueueFree();
				toggleButton = null;
			}

			panel.SetCreatorMode(CardEditorUiState.IsCreatorActive);
			panel.RefreshPresetList();
			panel.Visible = IsOpen(library);
			if (panel.Visible)
			{
				Callable.From(() =>
				{
					if (GodotObject.IsInstanceValid(panel))
					{
						panel.ApplyLayoutTuning();
						CardEditorPresetPanelTunerHooks.RefreshFor(library);
					}
				}).CallDeferred();
			}

			baseDeckPanel?.RefreshState();
		}
		else
		{
			SetOpen(library, open: false);
			if (panel != null)
			{
				panel.Visible = false;
			}

			if (toggleButton != null)
			{
				toggleButton.QueueFree();
			}

			baseDeckPanel?.RefreshState();
		}

		CardEditorPresetButtonTunerHooks.Sync(library, shouldShowPanel);
		CardEditorPresetPanelTunerHooks.Sync(library, shouldShowPanel);
	}
}

[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary.OnSubmenuClosed))]
public static class CardLibrary_OnSubmenuClosed_Patch
{
	public static void Postfix(NCardLibrary __instance)
	{
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuClosed:modeResetPatch:start", __instance);
		CardEditorMod.VerboseLog($"[CardEditor] OnSubmenuClosed: Resetting Mode from {CardEditorUiState.Mode} to None");
		CardEditorBaseDeckBookmarkHooks.ForceReset(__instance);
		CardEditorUiState.Mode = CardEditorLibraryMode.None;
		CardEditorUiState.SetLastLibrary(null);
		CardEditorPresetPanelHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.Sync(__instance);
		CardEditorBaseDeckBookmarkHooks.LogLibraryLifecycle("Library.OnSubmenuClosed:modeResetPatch:end", __instance);
	}
}

[HarmonyPatch(typeof(NCardLibraryGrid), "FilterCards", typeof(Func<CardModel, bool>), typeof(List<SortingOrders>))]
public static class CardLibraryGrid_FilterCards_Patch
{
	private static readonly FieldInfo? _allCardsField =
		typeof(NCardLibraryGrid).GetField("_allCards", BindingFlags.Instance | BindingFlags.NonPublic);
	private static int _cachedCreatorCardsOverrideRevision = -1;
	private static int _cachedCreatorCardsCreatedRevision = -1;
	private static int _cachedCreatorCardsDraftRevision = -1;
	private static List<CardModel>? _cachedCreatorCards;
	private static int _cachedEditableLibraryOverrideRevision = -1;
	private static int _cachedEditableLibraryCreatedRevision = -1;
	private static int _cachedEditableLibraryDraftRevision = -1;
	private static int _cachedEditableLibrarySourceCount = -1;
	private static List<CardModel>? _cachedEditableLibraryCards;

	private static NCardLibrary? FindOwningLibrary(Node? node)
	{
		Node? current = node;
		while (current != null)
		{
			if (current is NCardLibrary library)
			{
				return library;
			}

			current = current.GetParent();
		}

		return null;
	}

	private static void TryInjectCreatedCardSlot(int slot)
	{
		try
		{
			if (slot < 1 || slot > CardEditorCreatedCardsStore.SlotCount)
			{
				return;
			}

			string typeName = $"{typeof(CardEditorCreatedCardBase).Namespace}.CardEditorCreatedCard{slot.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}";
			Type? type = typeof(CardEditorCreatedCardBase).Assembly.GetType(typeName);
			if (type != null)
			{
				ModelDb.Inject(type);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed injecting created card slot {slot}: {ex}");
		}
	}

	public static bool Prefix(NCardLibraryGrid __instance, Func<CardModel, bool> filter, List<SortingOrders> sortingPriority)
	{
		CardEditorLibraryMode mode = CardEditorUiState.Mode;
		if (CardEditorMod.IsVerboseEditorLogging)
		{
			Log.Info($"[CardEditor] FilterCards Prefix: mode={mode}");
		}

		if (mode == CardEditorLibraryMode.Editor || mode == CardEditorLibraryMode.BaseDeck || mode == CardEditorLibraryMode.BaseDeckAdd)
		{
			NCardLibrary? library = FindOwningLibrary(__instance);
			if (library != null)
			{
				CardEditorUiState.SetLastLibrary(library);
				CardEditorBaseDeckBookmarkHooks.Sync(library);
			}
		}

		if (mode == CardEditorLibraryMode.Creator)
		{
			List<CardModel> creatorCards = BuildCreatorCards();
			List<CardModel> displayCards;
			if (CardEditorCreatorPresetStore.GetSortByCharacter())
			{
				// Filter by active pool/class tabs and sort by character class.
				displayCards = creatorCards.Where(filter).OrderBy(c => CardEditorCreatedCardsStore.GetPoolSortIndex(c.Id)).ToList();
			}
			else
			{
				// Original behaviour: show every card in slot order, ignore filters.
				displayCards = new List<CardModel>(creatorCards);
			}
			if (CardEditorMod.IsVerboseEditorLogging)
			{
				Log.Info($"[CardEditor] Creator mode: {creatorCards.Count} cards, {displayCards.Count} displayed");
			}
			List<SortingOrders> stableSort = new List<SortingOrders>(1) { SortingOrders.Ascending };
			__instance.SetCards(displayCards, PileType.None, stableSort, Task.CompletedTask);
			return false;
		}

		if (_allCardsField?.GetValue(__instance) is not List<CardModel> allCards)
		{
			return true;
		}

		if (mode == CardEditorLibraryMode.BaseDeck)
		{
			List<CardModel> deckCards = CardEditorBaseDeckStore.GetDeckPreviewCards(CardEditorBaseDeckUiState.EditingCharacterId)
				.ToList();
			__instance.SetCards(deckCards, PileType.None, sortingPriority, Task.CompletedTask);
			return false;
		}

		if (mode == CardEditorLibraryMode.Editor || mode == CardEditorLibraryMode.BaseDeckAdd)
		{
			List<CardModel> editCards = BuildEditableLibraryCards(allCards, filter);
			__instance.SetCards(editCards, PileType.None, sortingPriority, Task.CompletedTask);
			return false;
		}

		// Normal mode: let the original run if no overrides; otherwise rebuild overridden cards as previews.
		if (!CardEditorOverrides.HasAnyOverrides)
		{
			return true;
		}

		List<CardModel> cards = allCards
			.Where(filter)
			.Select(c => CardEditorOverrides.TryGet(c.Id, out _) ? CardEditorOverrides.BuildPreview(c) : c)
			.ToList();
		__instance.SetCards(cards, PileType.None, sortingPriority, Task.CompletedTask);
		return false;
	}

	private static List<CardModel> BuildCreatorCards()
	{
		CardEditorCreatedCardsStore.EnsureLoaded();
		int overrideRevision = CardEditorOverrides.Revision;
		int createdRevision = CardEditorCreatedCardsStore.Revision;
		int draftRevision = CardEditorUiState.DraftRevision;
		if (_cachedCreatorCards != null
			&& _cachedCreatorCardsOverrideRevision == overrideRevision
			&& _cachedCreatorCardsCreatedRevision == createdRevision
			&& _cachedCreatorCardsDraftRevision == draftRevision)
		{
			return new List<CardModel>(_cachedCreatorCards);
		}

		IReadOnlyList<ModelId> ids = CardEditorCreatedCardsStore.GetAllCreatedCardIds();
		List<CardModel> cards = new List<CardModel>(ids.Count);
		for (int i = 0; i < ids.Count; i++)
		{
			ModelId id = ids[i];
			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
			if (canonical == null)
			{
				TryInjectCreatedCardSlot(i + 1);
				canonical = ModelDb.GetByIdOrNull<CardModel>(id);
			}
			if (canonical == null)
			{
				Log.Warn($"[CardEditor] BuildCreatorCards: slot {i+1} ({id}) failed to resolve");
				continue;
			}
			cards.Add(BuildPreviewSafely(canonical, $"BuildCreatorCards: preview failed for slot {i + 1}"));
		}

		_cachedCreatorCardsOverrideRevision = overrideRevision;
		_cachedCreatorCardsCreatedRevision = createdRevision;
		_cachedCreatorCardsDraftRevision = draftRevision;
		_cachedCreatorCards = cards;
		return cards;
	}

	private static List<CardModel> BuildEditableLibraryCards(List<CardModel> allCards, Func<CardModel, bool> filter)
	{
		CardEditorCreatedCardsStore.EnsureLoaded();
		int overrideRevision = CardEditorOverrides.Revision;
		int createdRevision = CardEditorCreatedCardsStore.Revision;
		int draftRevision = CardEditorUiState.DraftRevision;
		if (_cachedEditableLibraryCards == null
			|| _cachedEditableLibraryOverrideRevision != overrideRevision
			|| _cachedEditableLibraryCreatedRevision != createdRevision
			|| _cachedEditableLibraryDraftRevision != draftRevision
			|| _cachedEditableLibrarySourceCount != allCards.Count)
		{
			List<CardModel> rebuiltCards = allCards
				.Where(c => !CardEditorCreatedCardsStore.IsCreatedCardId(c.Id))
				.Select(c => BuildPreviewSafely(c))
				.ToList();

			IReadOnlyList<ModelId> ids = CardEditorCreatedCardsStore.GetAllCreatedCardIds();
			for (int i = 0; i < ids.Count; i++)
			{
				ModelId id = ids[i];
				if (!CardEditorCreatedCardsStore.IsEnabled(id))
				{
					continue;
				}
				CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(id);
				if (canonical == null)
				{
					TryInjectCreatedCardSlot(i + 1);
					canonical = ModelDb.GetByIdOrNull<CardModel>(id);
				}
				if (canonical == null)
				{
					continue;
				}

				rebuiltCards.Add(BuildPreviewSafely(canonical, $"Editor mode: preview failed for created card {id}"));
			}

			_cachedEditableLibraryOverrideRevision = overrideRevision;
			_cachedEditableLibraryCreatedRevision = createdRevision;
			_cachedEditableLibraryDraftRevision = draftRevision;
			_cachedEditableLibrarySourceCount = allCards.Count;
			_cachedEditableLibraryCards = rebuiltCards;
		}

		return _cachedEditableLibraryCards.Where(filter).ToList();
	}

	private static CardModel BuildPreviewSafely(CardModel canonical, string? warnContext = null)
	{
		try
		{
			return CardEditorOverrides.BuildPreview(canonical);
		}
		catch (Exception ex)
		{
			if (!string.IsNullOrWhiteSpace(warnContext))
			{
				Log.Warn($"[CardEditor] {warnContext}: {ex.Message}");
			}

			return canonical;
		}
	}
}

[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
public static class CardLibraryGrid_GetCardVisibility_Patch
{
	public static bool Prefix(NCardLibraryGrid __instance, CardModel card, ref ModelVisibility __result)
	{
		if (CardEditorUiState.IsActive)
		{
			// In editor/creator mode every card should appear as fully visible.
			__result = ModelVisibility.Visible;
			return false;
		}
		if (!card.IsMutable)
		{
			// Let the original handle immutable (canonical) cards in the regular compendium.
			return true;
		}
		// Mutable (preview) cards are shown in normal mode when overrides are active.
		// Check unlock/discovery status against the canonical model.
		FieldInfo? unlockedField = typeof(NCardLibraryGrid).GetField("_unlockedCards", BindingFlags.Instance | BindingFlags.NonPublic);
		if (unlockedField?.GetValue(__instance) is HashSet<CardModel> unlockedCards)
		{
			CardModel canonical = ModelDb.GetById<CardModel>(card.Id);
			if (!unlockedCards.Contains(canonical))
			{
				__result = ModelVisibility.Locked;
				return false;
			}
		}
		if (!SaveManager.Instance.Progress.DiscoveredCards.Contains(card.Id))
		{
			__result = ModelVisibility.NotSeen;
			return false;
		}
		__result = ModelVisibility.Visible;
		return false;
	}
}

[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen.Open))]
public static class InspectCardScreen_Open_Patch
{
	public static bool Prefix()
	{
		if (CardEditorUiState.IsActive)
		{
			CardEditorMod.VerboseLog("[CardEditor] Blocking InspectCardScreen.Open");
			return false;
		}
		return true;
	}
}

[HarmonyPatch]
public static class PowerCmd_Apply_Patch
{
	private static readonly ModelId _resonanceId = ModelDb.GetId<Resonance>();
	private static readonly ModelId _strengthPowerId = ModelDb.GetId<StrengthPower>();

	private static MethodBase TargetMethod()
	{
		MethodInfo? method = CardEditorPowerCmdCompat.FindApplyPowerMethod();
		if (method == null)
		{
			throw new MissingMethodException(typeof(PowerCmd).FullName, nameof(PowerCmd.Apply));
		}
		return method;
	}

	public static void Prefix(PowerModel power, ref decimal amount, CardModel? cardSource)
	{
		if (power != null && cardSource != null)
		{
			CardEditorPowerSourceMap.Register(power, cardSource);
		}

		CardModel? durationSourceCard = cardSource;
		if (durationSourceCard == null && CardEditorHookModelContext.Current is PowerModel ctxPower)
		{
			durationSourceCard = CardEditorPowerSourceMap.TryGetSourceCard(ctxPower);
		}
		if (durationSourceCard == null)
		{
			return;
		}
		if (!CardEditorOverrides.TryGetEffectiveOverride(durationSourceCard, out CardOverride overrideData))
		{
			return;
		}

		if (cardSource != null
			&& cardSource.Id == _resonanceId
			&& power != null
			&& power.Id == _strengthPowerId
			&& amount == -1m
			&& overrideData.DynamicVarBaseValues != null
			&& overrideData.DynamicVarBaseValues.TryGetValue(CardEditorOverrideKeys.ResonanceEnemyStrengthLoss, out decimal enemyLoss))
		{
			int loss = Math.Clamp((int)enemyLoss, 0, 999);
			amount = loss == 0 ? 0m : -loss;
		}

		CardEditorTemporaryStatPowerDurationController.RegisterIfNeeded(power, overrideData);

		if (cardSource != null
			&& overrideData.PowerAmounts != null
			&& overrideData.PowerAmounts.Count > 0
			&& overrideData.PowerAmounts.TryGetValue(power.Id, out decimal overriddenAmount))
		{
			amount = overriddenAmount;
		}
	}
}

[HarmonyPatch]
public static class PowerCmd_ModifyAmount_Patch
{
	private static MethodBase TargetMethod()
	{
		MethodInfo? method = CardEditorPowerCmdCompat.FindModifyAmountMethod();
		if (method == null)
		{
			throw new MissingMethodException(typeof(PowerCmd).FullName, nameof(PowerCmd.ModifyAmount));
		}
		return method;
	}

	public static void Prefix(PowerModel power, ref decimal offset, CardModel? cardSource)
	{
		CardModel? durationSourceCard = cardSource;
		if (durationSourceCard == null && CardEditorHookModelContext.Current is PowerModel ctxPower)
		{
			durationSourceCard = CardEditorPowerSourceMap.TryGetSourceCard(ctxPower);
		}
		if (durationSourceCard == null)
		{
			return;
		}
		if (!CardEditorOverrides.TryGetEffectiveOverride(durationSourceCard, out CardOverride overrideData))
		{
			return;
		}

		CardEditorTemporaryStatPowerDurationController.RegisterIfNeeded(power, overrideData);

		if (cardSource != null
			&& overrideData.PowerAmounts != null
			&& overrideData.PowerAmounts.Count > 0
			&& overrideData.PowerAmounts.TryGetValue(power.Id, out decimal overriddenAmount))
		{
			offset = overriddenAmount;
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
public static class Hook_AfterCardPlayed_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		if (!CardEditorOverrides.HasAnyOverrides
			&& !CardEditorTemporaryExtraEffectController.HasAny(combatState)
			&& !CardEditorTemporaryEnchantmentController.HasAny(combatState))
		{
			return;
		}
		__result = RunExtraEffectsAfter(__result, combatState, choiceContext, cardPlay);
	}

	private static async Task RunExtraEffectsAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await original;
		try
		{
			await CardEditorExtraEffects.RunAfterCardPlayed(combatState, choiceContext, cardPlay);
			await CardEditorAfflictionEffects.RunAfterCardPlayed(combatState, choiceContext, cardPlay);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Extra effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(CardEditorCreatedCardBase), "OnPlay")]
internal static class CardEditorCreatedCards_OnPlay_RunExtraEffects_Patch
{
	public static void Postfix(CardEditorCreatedCardBase __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		CombatState? combatState = cardPlay?.Card.GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}
		if (!CardEditorCreatedCardsStore.HasEffectSourceCard(__instance.Id)
			&& !CardEditorOverrides.HasAnyOverrides
			&& !CardEditorTemporaryExtraEffectController.HasAny(combatState))
		{
			return;
		}

		__result = RunOnPlayExtraEffectsDuringCardPlay(__instance, __result, combatState, choiceContext, cardPlay);
	}

	private static async Task RunOnPlayExtraEffectsDuringCardPlay(CardEditorCreatedCardBase createdCard, Task original, CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await original;
		try
		{
			await CardEditorCreatedCardEffectSourceSupport.RunResolvedCreatedCardOnPlay(createdCard, combatState, choiceContext, cardPlay);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Created card OnPlay extra effects failed: {ex}");
		}
	}
}

internal static class CardEditorPowerTurnBoundaryRunner
{
	public static async Task RunForObservedSide(
		CombatState combatState,
		PlayerChoiceContext choiceContext,
		CardExtraEffectTurnBoundary boundary,
		CombatSide observedSide)
	{
		if (combatState == null || choiceContext == null || observedSide == CombatSide.None)
		{
			return;
		}

		foreach (Creature creature in SnapshotCreatures(combatState))
		{
			await RunForCreature(creature, choiceContext, boundary, observedSide);
		}
	}

	public static async Task RunForObservedSideWithHookContexts(
		CombatState combatState,
		CardExtraEffectTurnBoundary boundary,
		CombatSide observedSide)
	{
		if (combatState == null || observedSide == CombatSide.None)
		{
			return;
		}

		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			return;
		}

		foreach (Creature creature in SnapshotCreatures(combatState))
		{
			if (creature.GetPower<CardEditorExtraEffectPower>() == null)
			{
				continue;
			}

			Player? contextPlayer = FindContextPlayer(combatState, creature);
			if (contextPlayer == null)
			{
				continue;
			}

			HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(contextPlayer, netId.Value, GameActionType.Combat);
			Task powerTask = RunForCreature(creature, choiceContext, boundary, observedSide);
			bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(powerTask);
			if (!completed && choiceContext.GameAction != null)
			{
				await choiceContext.GameAction.CompletionTask;
			}
		}
	}

	private static async Task RunForCreature(
		Creature creature,
		PlayerChoiceContext choiceContext,
		CardExtraEffectTurnBoundary boundary,
		CombatSide observedSide)
	{
		if (creature == null || choiceContext == null)
		{
			return;
		}

		CardEditorExtraEffectPower? power = creature.GetPower<CardEditorExtraEffectPower>();
		if (power == null)
		{
			return;
		}

		await power.RunTurnBoundary(choiceContext, boundary, GetOwnerRelativeSide(creature, observedSide));
	}

	private static CardExtraEffectTurnBoundarySide GetOwnerRelativeSide(Creature owner, CombatSide observedSide)
	{
		if (owner == null || observedSide == CombatSide.None)
		{
			return CardExtraEffectTurnBoundarySide.Both;
		}

		return owner.Side == observedSide
			? CardExtraEffectTurnBoundarySide.YourTurn
			: CardExtraEffectTurnBoundarySide.EnemyTurn;
	}

	private static IEnumerable<Creature> SnapshotCreatures(CombatState combatState)
	{
		HashSet<Creature> seen = new HashSet<Creature>();

		foreach (Creature creature in combatState.PlayerCreatures ?? Enumerable.Empty<Creature>())
		{
			if (creature != null && seen.Add(creature))
			{
				yield return creature;
			}
		}

		foreach (Creature creature in combatState.Enemies ?? Enumerable.Empty<Creature>())
		{
			if (creature != null && seen.Add(creature))
			{
				yield return creature;
			}
		}
	}

	private static Player? FindContextPlayer(CombatState combatState, Creature creature)
	{
		Player? ownerPlayer = combatState.Players?.FirstOrDefault(player => ReferenceEquals(player?.Creature, creature));
		return ownerPlayer ?? combatState.Players?.FirstOrDefault(player => player?.Creature != null);
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
public static class Hook_AfterPlayerTurnStart_Patch
{
	public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		__result = RunScheduledAfter(__result, combatState, choiceContext, player);
	}

	private static async Task RunScheduledAfter(Task original, CombatState combatState, PlayerChoiceContext choiceContext, Player player)
	{
		await original;
		try
		{
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.StartAfterDraw, CardExtraEffectTurnBoundarySide.YourTurn);
			await CardEditorExtraEffectScheduler.RunAfterPlayerTurnStart(combatState, choiceContext, player);
			CardEditorCreatedCardsCostController.OnAfterPlayerTurnStart(combatState, player);
			await CardEditorPowerTurnBoundaryRunner.RunForObservedSide(combatState, choiceContext, CardExtraEffectTurnBoundary.StartAfterDraw, CombatSide.Player);
			Creature? creature = player?.Creature;
			if (creature != null)
			{
				CardEditorExtraEffectPower? power = creature.GetPower<CardEditorExtraEffectPower>();
				if (power != null)
				{
					await power.RunStartOfTurn(choiceContext);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Scheduled start-of-turn effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeHandDraw))]
public static class Hook_BeforeHandDraw_CardEditorTurnBoundaryPower_Patch
{
	public static void Postfix(CombatState combatState, Player player, PlayerChoiceContext playerChoiceContext, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}

		__result = RunAfter(__result, combatState, player, playerChoiceContext);
	}

	private static async Task RunAfter(Task original, CombatState combatState, Player player, PlayerChoiceContext choiceContext)
	{
		await original;
		try
		{
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.Start, CardExtraEffectTurnBoundarySide.YourTurn);
			await CardEditorPowerTurnBoundaryRunner.RunForObservedSide(combatState, choiceContext, CardExtraEffectTurnBoundary.Start, CombatSide.Player);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary before-draw power effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
public static class Hook_AfterSideTurnStart_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		if (side != CombatSide.Enemy)
		{
			return;
		}
		__result = RunScheduledAfter(__result, combatState);
	}

	private static async Task RunScheduledAfter(Task original, CombatState combatState)
	{
		await original;

		try
		{
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.StartAfterDraw, CardExtraEffectTurnBoundarySide.EnemyTurn);
			ulong? netId = LocalContext.NetId;
			if (!netId.HasValue)
			{
				return;
			}

			foreach (Player player in combatState.Players)
			{
				HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task task = CardEditorExtraEffectScheduler.RunAfterEnemyTurnStart(combatState, choiceContext, player);
				bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
				if (!completed && choiceContext.GameAction != null)
				{
					await choiceContext.GameAction.CompletionTask;
				}
			}

			await CardEditorPowerTurnBoundaryRunner.RunForObservedSideWithHookContexts(combatState, CardExtraEffectTurnBoundary.StartAfterDraw, CombatSide.Enemy);

			foreach (Player player in combatState.Players)
			{
				Creature? creature = player?.Creature;
				CardEditorExtraEffectPower? power = creature?.GetPower<CardEditorExtraEffectPower>();
				if (power == null)
				{
					continue;
				}

				HookPlayerChoiceContext powerCtx = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
				Task powerTask = power.RunStartOfEnemyTurn(powerCtx);
				bool powerCompleted = await powerCtx.AssignTaskAndWaitForPauseOrCompletion(powerTask);
				if (!powerCompleted && powerCtx.GameAction != null)
				{
					await powerCtx.GameAction.CompletionTask;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Scheduled enemy-start-of-turn effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
public static class Hook_BeforeSideTurnStart_CardEditorTurnBoundaryPower_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null || side != CombatSide.Enemy)
		{
			return;
		}

		__result = RunAfter(__result, combatState);
	}

	private static async Task RunAfter(Task original, CombatState combatState)
	{
		await original;
		try
		{
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.Start, CardExtraEffectTurnBoundarySide.EnemyTurn);
			ulong? netId = LocalContext.NetId;
			if (!netId.HasValue)
			{
				return;
			}

			await CardEditorPowerTurnBoundaryRunner.RunForObservedSideWithHookContexts(combatState, CardExtraEffectTurnBoundary.Start, CombatSide.Enemy);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary enemy-before-start power effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeTurnEnd))]
public static class Hook_BeforeTurnEnd_CardEditorTurnBoundaryPower_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}

		__result = RunAfter(__result, combatState, side);
	}

	private static async Task RunAfter(Task original, CombatState combatState, CombatSide side)
	{
		await original;
		try
		{
			CardExtraEffectTurnBoundarySide boundarySide = side == CombatSide.Enemy
				? CardExtraEffectTurnBoundarySide.EnemyTurn
				: CardExtraEffectTurnBoundarySide.YourTurn;
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.End, boundarySide);

			ulong? netId = LocalContext.NetId;
			if (!netId.HasValue)
			{
				return;
			}

			await CardEditorPowerTurnBoundaryRunner.RunForObservedSideWithHookContexts(combatState, CardExtraEffectTurnBoundary.End, side);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Turn-boundary before-end power effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd))]
public static class Hook_AfterTurnEnd_Patch
{
	public static void Postfix(CombatState combatState, CombatSide side, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		__result = RunScheduledAfter(__result, combatState, side);
	}

	private static async Task RunScheduledAfter(Task original, CombatState combatState, CombatSide side)
	{
		await original;
		try
		{
			await CardEditorExtraEffectScheduler.RunAfterTurnEnd(combatState, side);
			CardExtraEffectTurnBoundarySide boundarySide = side == CombatSide.Enemy
				? CardExtraEffectTurnBoundarySide.EnemyTurn
				: CardExtraEffectTurnBoundarySide.YourTurn;
			await CardEditorPowerDurationTrackerPower.RunDurationBoundary(combatState, CardExtraEffectTurnBoundary.EndAfterDiscard, boundarySide);

			ulong? netId = LocalContext.NetId;
			if (netId.HasValue)
			{
				await CardEditorPowerTurnBoundaryRunner.RunForObservedSideWithHookContexts(combatState, CardExtraEffectTurnBoundary.EndAfterDiscard, side);
			}
			if (side == CombatSide.Player)
			{
				CardEditorTemporaryKeywordController.OnAfterPlayerTurnEnd(combatState);
				CardEditorTemporaryExtraEffectController.OnAfterPlayerTurnEnd(combatState);
				CardEditorTemporaryEnchantmentController.OnAfterPlayerTurnEnd(combatState);
				CardEditorTemporaryReplayController.OnAfterPlayerTurnEnd(combatState);
				CardEditorTemporarySelfScalingController.OnAfterPlayerTurnEnd(combatState);
				CardEditorMatchingCardAuraController.OnAfterPlayerTurnEnd(combatState);
				CardEditorCardTypeCostAuras.OnAfterPlayerTurnEnd(combatState);
				CardEditorDrawnGeneratedCostController.OnAfterPlayerTurnEnd(combatState);
				CardEditorUpgradeAuraController.OnAfterPlayerTurnEnd(combatState);
				CardEditorTemporaryUpgradeController.OnAfterPlayerTurnEnd(combatState);
				await CardEditorExtraEffects.RunStatefulTransformsAfterPlayerTurnEnd(combatState);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Scheduled end-of-turn effects failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
public static class Hook_AfterCombatEnd_ClearScheduledEffects_Patch
{
	public static void Postfix(IRunState runState, CombatState? combatState, CombatRoom room, ref Task __result)
	{
		if (__result == null)
		{
			return;
		}
		__result = ClearScheduledAfter(__result, combatState);
	}

	private static async Task ClearScheduledAfter(Task original, CombatState? combatState)
	{
		try
		{
			await original;
		}
		finally
		{
			if (combatState != null)
			{
				CardEditorExtraEffectScheduler.Clear(combatState);
				CardEditorCreatedCardsCostController.Clear(combatState);
				await CardEditorExtraEffects.RunStatefulTransformsAfterCombatEnd(combatState);
				CardEditorTemporaryKeywordController.Clear(combatState);
				CardEditorTemporaryExtraEffectController.Clear(combatState);
				CardEditorTemporaryEnchantmentController.Clear(combatState);
				CardEditorTemporaryReplayController.Clear(combatState);
				CardEditorTemporarySelfScalingController.Clear(combatState);
				CardEditorMatchingCardAuraController.Clear(combatState);
				CardEditorCardTypeCostAuras.Clear(combatState);
				CardEditorDrawnGeneratedCostController.Clear(combatState);
				CardEditorUpgradeAuraController.Clear(combatState);
				CardEditorTemporaryUpgradeController.Clear(combatState);
				CardEditorExtraEffects.ClearOrbCountHistory(combatState);
				CardEditorExtraEffects.ClearResourceCountHistory(combatState);
			}
		}
	}
}
