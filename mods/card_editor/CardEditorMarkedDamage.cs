using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

// "Marked" (Hang-style) debuff as a real, VISIBLE PowerModel. The owner takes +50% attack damage per
// stack, applied multiplicatively exactly like Vulnerable (so it shows in the predicted-damage number
// and is absorbed by Block). It persists for the combat (no duration tick-down). Custom powers have no
// icon resource of their own, so it borrows Vulnerable's icon/look via the patches below.
internal sealed class CardEditorMarkedPower : PowerModel
{
	private const decimal PerStackMultiplier = 0.5m;

	public override LocString Title => CardEditorCustomStatusPower.CreateRuntimeLocString("CARD_EDITOR.MARKED_TITLE.", "Marked");

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override int DisplayAmount => Math.Max(0, Amount);

	protected override bool IsVisibleInternal => true;

	// Mirrors VulnerablePower: every power on the struck creature is asked for a multiplier; gate on
	// "I am the one being hit" (target == Owner) and a powered attack so non-attack damage is untouched.
	public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (target != base.Owner || !props.IsPoweredAttack())
		{
			return 1m;
		}

		return 1m + (PerStackMultiplier * Math.Max(0, base.Amount));
	}

	// Vulnerable's textures are immutable session resources; cache them after the first
	// successful ModelDb lookup so per-frame icon reads stop hitting the database.
	private static Texture2D? _cachedIcon;
	private static Texture2D? _cachedBigIcon;

	internal static Texture2D? ResolveSharedIcon(bool bigIcon)
	{
		Texture2D? cached = bigIcon ? _cachedBigIcon : _cachedIcon;
		if (cached != null)
		{
			return cached;
		}

		try
		{
			PowerModel? vulnerable = ModelDb.Power<VulnerablePower>();
			Texture2D? resolved = bigIcon ? vulnerable?.BigIcon : vulnerable?.Icon;
			if (resolved != null)
			{
				if (bigIcon)
				{
					_cachedBigIcon = resolved;
				}
				else
				{
					_cachedIcon = resolved;
				}
			}
			return resolved;
		}
		catch
		{
			return null;
		}
	}

	internal string TooltipBody()
	{
		int percent = (int)(PerStackMultiplier * 100m);
		decimal current = 1m + (PerStackMultiplier * Math.Max(0, base.Amount));
		return $"Takes {percent}% more attack damage per stack (currently x{current:0.##}).";
	}
}

// Additive: only handles CardEditorMarkedPower instances (returns true -> run original -> for everything
// else, including CardEditorCustomStatusPower which has its own patch). Gives Marked its icon + tooltip.
[HarmonyPatch]
internal static class CardEditorMarkedPowerPatches
{
	[HarmonyPatch(typeof(PowerModel), "get_Icon")]
	[HarmonyPrefix]
	private static bool Icon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorMarkedPower)
		{
			return true;
		}

		__result = CardEditorMarkedPower.ResolveSharedIcon(bigIcon: false);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
	[HarmonyPrefix]
	private static bool BigIcon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorMarkedPower)
		{
			return true;
		}

		__result = CardEditorMarkedPower.ResolveSharedIcon(bigIcon: true);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_HoverTips")]
	[HarmonyPrefix]
	private static bool HoverTips_Prefix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (__instance is not CardEditorMarkedPower marked)
		{
			return true;
		}

		List<IHoverTip> tips = new()
		{
			new HoverTip(
				CardEditorCustomStatusPower.CreateRuntimeLocString("CARD_EDITOR.MARKED_TIP.", "Marked"),
				marked.TooltipBody(),
				CardEditorMarkedPower.ResolveSharedIcon(bigIcon: false))
		};
		__result = IHoverTip.RemoveDupes(tips).ToList();
		return false;
	}
}
