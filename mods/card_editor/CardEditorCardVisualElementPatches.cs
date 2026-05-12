using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCardVisualElementController
{
	private sealed class VisibilitySnapshot
	{
		public object? TrackedModel { get; set; }
		public bool? TitleVisible { get; set; }
		public bool? DescriptionVisible { get; set; }
		public bool? BannerVisible { get; set; }
		public bool? AncientBannerVisible { get; set; }
		public bool? TypePlaqueVisible { get; set; }
		public bool? TypeLabelVisible { get; set; }
		public bool? EnergyIconVisible { get; set; }
		public bool? EnergyLabelVisible { get; set; }
		public bool? UnplayableEnergyIconVisible { get; set; }
		public bool? AncientTextBgVisible { get; set; }
		public bool? AncientBorderGlassOverlayVisible { get; set; }
		public bool IsCaptured { get; set; }
	}

	private static readonly ConditionalWeakTable<NCard, VisibilitySnapshot> _snapshots = new();
	private static readonly FieldInfo _titleLabelField = AccessTools.Field(typeof(NCard), "_titleLabel")!;
	private static readonly FieldInfo _descriptionLabelField = AccessTools.Field(typeof(NCard), "_descriptionLabel")!;
	private static readonly FieldInfo _bannerField = AccessTools.Field(typeof(NCard), "_banner")!;
	private static readonly FieldInfo _ancientBannerField = AccessTools.Field(typeof(NCard), "_ancientBanner")!;
	private static readonly FieldInfo _typePlaqueField = AccessTools.Field(typeof(NCard), "_typePlaque")!;
	private static readonly FieldInfo _typeLabelField = AccessTools.Field(typeof(NCard), "_typeLabel")!;
	private static readonly FieldInfo _energyIconField = AccessTools.Field(typeof(NCard), "_energyIcon")!;
	private static readonly FieldInfo _energyLabelField = AccessTools.Field(typeof(NCard), "_energyLabel")!;
	private static readonly FieldInfo _unplayableEnergyIconField = AccessTools.Field(typeof(NCard), "_unplayableEnergyIcon")!;
	private static readonly FieldInfo _ancientTextBgField = AccessTools.Field(typeof(NCard), "_ancientTextBg")!;
	private static readonly FieldInfo _ancientBorderGlassOverlayField = AccessTools.Field(typeof(NCard), "_ancientBorderGlassOverlay")!;

	internal static void Sync(NCard? cardNode)
	{
		if (cardNode == null)
		{
			return;
		}

		var card = cardNode.Model;
		if (card == null)
		{
			return;
		}

		VisibilitySnapshot snapshot = _snapshots.GetOrCreateValue(cardNode);
		if (!ReferenceEquals(snapshot.TrackedModel, card))
		{
			RestoreSnapshot(cardNode, snapshot);
			ResetSnapshot(snapshot, card);
		}

		if (CardEditorOverrides.SuppressAllOverrides
			|| !CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride overrideData)
			|| !HasAnyVisualHideFlags(overrideData))
		{
			RestoreSnapshot(cardNode, snapshot);
			return;
		}

		CaptureSnapshot(cardNode, snapshot);
		ApplyVisibility(cardNode, _energyIconField, overrideData.HideCosmeticCostOrb == true, snapshot.EnergyIconVisible);
		ApplyVisibility(cardNode, _unplayableEnergyIconField, overrideData.HideCosmeticCostOrb == true, snapshot.UnplayableEnergyIconVisible);
		ApplyVisibility(cardNode, _energyLabelField, overrideData.HideCosmeticCostNumber == true, snapshot.EnergyLabelVisible);
		ApplyVisibility(cardNode, _bannerField, overrideData.HideCosmeticNameBanner == true, snapshot.BannerVisible);
		ApplyVisibility(cardNode, _ancientBannerField, overrideData.HideCosmeticNameBanner == true, snapshot.AncientBannerVisible);
		ApplyVisibility(cardNode, _titleLabelField, overrideData.HideCosmeticNameText == true, snapshot.TitleVisible);
		ApplyVisibility(cardNode, _typePlaqueField, overrideData.HideCosmeticTypeBadge == true, snapshot.TypePlaqueVisible);
		ApplyVisibility(cardNode, _typeLabelField, overrideData.HideCosmeticTypeBadge == true, snapshot.TypeLabelVisible);
		ApplyVisibility(cardNode, _ancientTextBgField, overrideData.HideCosmeticTextBackground == true, snapshot.AncientTextBgVisible);
		ApplyVisibility(cardNode, _ancientBorderGlassOverlayField, overrideData.HideCosmeticAncientInnerBorder == true, snapshot.AncientBorderGlassOverlayVisible);
		ApplyVisibility(cardNode, _descriptionLabelField, overrideData.HideCosmeticBodyText == true, snapshot.DescriptionVisible);
	}

	private static bool HasAnyVisualHideFlags(CardOverride? overrideData)
	{
		if (overrideData == null)
		{
			return false;
		}

		return overrideData.HideCosmeticCostOrb == true
			|| overrideData.HideCosmeticCostNumber == true
			|| overrideData.HideCosmeticNameBanner == true
			|| overrideData.HideCosmeticNameText == true
			|| overrideData.HideCosmeticTypeBadge == true
			|| overrideData.HideCosmeticTextBackground == true
			|| overrideData.HideCosmeticBodyText == true
			|| overrideData.HideCosmeticAncientInnerBorder == true;
	}

	private static void ResetSnapshot(VisibilitySnapshot snapshot, object trackedModel)
	{
		snapshot.TrackedModel = trackedModel;
		snapshot.TitleVisible = null;
		snapshot.DescriptionVisible = null;
		snapshot.BannerVisible = null;
		snapshot.AncientBannerVisible = null;
		snapshot.TypePlaqueVisible = null;
		snapshot.TypeLabelVisible = null;
		snapshot.EnergyIconVisible = null;
		snapshot.EnergyLabelVisible = null;
		snapshot.UnplayableEnergyIconVisible = null;
		snapshot.AncientTextBgVisible = null;
		snapshot.AncientBorderGlassOverlayVisible = null;
		snapshot.IsCaptured = false;
	}

	private static void CaptureSnapshot(NCard cardNode, VisibilitySnapshot snapshot)
	{
		if (snapshot.IsCaptured)
		{
			return;
		}

		snapshot.TitleVisible = GetVisible(cardNode, _titleLabelField);
		snapshot.DescriptionVisible = GetVisible(cardNode, _descriptionLabelField);
		snapshot.BannerVisible = GetVisible(cardNode, _bannerField);
		snapshot.AncientBannerVisible = GetVisible(cardNode, _ancientBannerField);
		snapshot.TypePlaqueVisible = GetVisible(cardNode, _typePlaqueField);
		snapshot.TypeLabelVisible = GetVisible(cardNode, _typeLabelField);
		snapshot.EnergyIconVisible = GetVisible(cardNode, _energyIconField);
		snapshot.EnergyLabelVisible = GetVisible(cardNode, _energyLabelField);
		snapshot.UnplayableEnergyIconVisible = GetVisible(cardNode, _unplayableEnergyIconField);
		snapshot.AncientTextBgVisible = GetVisible(cardNode, _ancientTextBgField);
		snapshot.AncientBorderGlassOverlayVisible = GetVisible(cardNode, _ancientBorderGlassOverlayField);
		NormalizeCostSnapshot(cardNode, snapshot);
		NormalizeTypeSnapshot(cardNode, snapshot);
		snapshot.IsCaptured = true;
	}

	private static void NormalizeCostSnapshot(NCard cardNode, VisibilitySnapshot snapshot)
	{
		CardModel? model = cardNode.Model;
		if (model?.EnergyCost == null)
		{
			return;
		}

		bool shouldShowEnergy = model.EnergyCost.CostsX || model.EnergyCost.GetWithModifiers(CostModifiers.All) >= 0;
		snapshot.EnergyIconVisible = shouldShowEnergy;
		snapshot.EnergyLabelVisible = shouldShowEnergy;
	}

	private static void NormalizeTypeSnapshot(NCard cardNode, VisibilitySnapshot snapshot)
	{
		CardModel? model = cardNode.Model;
		if (model == null || model.Type == CardType.None)
		{
			return;
		}

		snapshot.TypePlaqueVisible = true;
		snapshot.TypeLabelVisible = true;
	}

	private static void RestoreSnapshot(NCard cardNode, VisibilitySnapshot snapshot)
	{
		if (!snapshot.IsCaptured)
		{
			return;
		}

		NormalizeTypeSnapshot(cardNode, snapshot);
		SetVisible(cardNode, _titleLabelField, snapshot.TitleVisible);
		SetVisible(cardNode, _descriptionLabelField, snapshot.DescriptionVisible);
		SetVisible(cardNode, _bannerField, snapshot.BannerVisible);
		SetVisible(cardNode, _ancientBannerField, snapshot.AncientBannerVisible);
		SetVisible(cardNode, _typePlaqueField, snapshot.TypePlaqueVisible);
		SetVisible(cardNode, _typeLabelField, snapshot.TypeLabelVisible);
		SetVisible(cardNode, _energyIconField, snapshot.EnergyIconVisible);
		SetVisible(cardNode, _energyLabelField, snapshot.EnergyLabelVisible);
		SetVisible(cardNode, _unplayableEnergyIconField, snapshot.UnplayableEnergyIconVisible);
		SetVisible(cardNode, _ancientTextBgField, snapshot.AncientTextBgVisible);
		SetVisible(cardNode, _ancientBorderGlassOverlayField, snapshot.AncientBorderGlassOverlayVisible);
		snapshot.IsCaptured = false;
	}

	private static void ApplyVisibility(NCard cardNode, FieldInfo? field, bool hidden, bool? fallbackVisible)
	{
		SetVisible(cardNode, field, hidden ? false : fallbackVisible);
	}

	private static bool? GetVisible(NCard cardNode, FieldInfo? field)
	{
		if (field?.GetValue(cardNode) is CanvasItem item && GodotObject.IsInstanceValid(item))
		{
			return item.Visible;
		}

		return null;
	}

	private static void SetVisible(NCard cardNode, FieldInfo? field, bool? visible)
	{
		if (visible.HasValue && field?.GetValue(cardNode) is CanvasItem item && GodotObject.IsInstanceValid(item))
		{
			item.Visible = visible.Value;
		}
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_CardVisualElement_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorCardVisualElementController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_CardVisualElement_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorCardVisualElementController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
internal static class NCard_UpdateEnergyCostVisuals_CardVisualElement_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorCardVisualElementController.Sync(__instance);
		}
		catch
		{
		}
	}
}
