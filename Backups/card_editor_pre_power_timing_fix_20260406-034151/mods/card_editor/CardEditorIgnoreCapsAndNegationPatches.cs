using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorIgnoreEffectHelpers
{
	public static CardModel? ResolveEffectiveSource(CardModel? cardSource)
	{
		return CardEditorEffectSourceContext.Current ?? cardSource;
	}

	public static bool HasIgnoreEffect(CardModel? card, CardExtraEffectKind kind)
	{
		if (card == null)
		{
			return false;
		}

		try
		{
			return CardEditorExtraEffects
				.GetEffectsForDescription(card, isUpgradePreview: false)
				.Any(e => e != null && e.Amount > 0 && e.Kind == kind);
		}
		catch
		{
			return false;
		}
	}
}

[HarmonyPatch]
internal static class Hook_ModifyDamageInternal_IgnoreCaps_Patch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(Hook), "ModifyDamageInternal")!;
	}

	private static bool ShouldSkipTargetOwnedReduction(AbstractModel model, Creature? target, decimal multiplier)
	{
		if (target == null)
		{
			return false;
		}

		// Only skip reductions that are clearly target-owned. This preserves dealer buffs (Strength/Vigor/etc)
		// and still allows target-owned damage increases (e.g. Vulnerable) to apply.
		if (multiplier >= 1m)
		{
			return false;
		}

		if (model is PowerModel power && power.Owner == target)
		{
			return true;
		}

		return false;
	}

	public static bool Prefix(
		IRunState runState,
		CombatState? combatState,
		Creature? target,
		Creature? dealer,
		decimal damage,
		ValueProp props,
		CardModel? cardSource,
		ModifyDamageHookType modifyDamageHookType,
		ref List<AbstractModel> modifiers,
		ref decimal __result)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasIgnoreEffect(effectiveSource, CardExtraEffectKind.IgnoreDamageCaps);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasIgnoreEffect(effectiveSource, CardExtraEffectKind.IgnoreEnemyDamageReductions);
		if (!ignoreCaps && !ignoreEnemyReductions)
		{
			return true;
		}

		decimal num = damage;
		List<AbstractModel> list = new List<AbstractModel>();

		if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
		{
			foreach (AbstractModel item in runState.IterateHookListeners(combatState))
			{
				decimal delta = item.ModifyDamageAdditive(target, num, props, dealer, cardSource);
				num += delta;
				if (delta != 0m)
				{
					list.Add(item);
				}
			}
		}

		if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Multiplicative))
		{
			foreach (AbstractModel item in runState.IterateHookListeners(combatState))
			{
				decimal mult = item.ModifyDamageMultiplicative(target, num, props, dealer, cardSource);
				if (ignoreEnemyReductions && ShouldSkipTargetOwnedReduction(item, target, mult))
				{
					continue;
				}
				num *= mult;
				if (mult != 1m)
				{
					list.Add(item);
				}
			}
		}

		if (!ignoreCaps)
		{
			decimal cap = decimal.MaxValue;
			foreach (AbstractModel item in runState.IterateHookListeners(combatState))
			{
				decimal candidate = item.ModifyDamageCap(target, props, dealer, cardSource);
				if (candidate < cap)
				{
					cap = candidate;
					if (num > candidate)
					{
						num = candidate;
						list.Add(item);
					}
				}
			}
		}

		modifiers = list;
		__result = num;
		return false;
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLostAfterOsty))]
internal static class Hook_ModifyHpLostAfterOsty_IgnoreNegation_Patch
{
	public static bool Prefix(
		IRunState runState,
		CombatState? combatState,
		Creature target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource,
		ref IEnumerable<AbstractModel> modifiers,
		ref decimal __result)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		if (!CardEditorIgnoreEffectHelpers.HasIgnoreEffect(effectiveSource, CardExtraEffectKind.IgnoreDamageNegation))
		{
			return true;
		}

		// "Damage negation" in STS2 is primarily implemented via ModifyHpLostAfterOstyLate (e.g., Buffer -> 0 HP loss).
		// Skipping the Late phase avoids negation without consuming the negating power.
		decimal num = amount;
		List<AbstractModel> list = new List<AbstractModel>();
		foreach (AbstractModel item in runState.IterateHookListeners(combatState))
		{
			decimal before = num;
			num = item.ModifyHpLostAfterOsty(target, num, props, dealer, cardSource);
			if ((int)before != (int)num)
			{
				list.Add(item);
			}
		}

		modifiers = list;
		__result = num;
		return false;
	}
}

[HarmonyPatch(typeof(IntangiblePower), nameof(IntangiblePower.ModifyHpLostAfterOsty))]
internal static class IntangiblePower_ModifyHpLostAfterOsty_IgnoreCaps_Patch
{
	public static bool Prefix(
		ref decimal __result,
		Creature target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		if (!CardEditorIgnoreEffectHelpers.HasIgnoreEffect(effectiveSource, CardExtraEffectKind.IgnoreDamageCaps))
		{
			return true;
		}

		__result = amount;
		return false;
	}
}

[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate))]
internal static class HardenedShellPower_ModifyHpLostBeforeOstyLate_IgnoreCaps_Patch
{
	private static readonly FieldInfo? _powerModelInternalDataField = AccessTools.Field(typeof(PowerModel), "_internalData");
	private static readonly ConditionalWeakTable<HardenedShellPower, FieldInfo> _damageReceivedFieldCache = new ConditionalWeakTable<HardenedShellPower, FieldInfo>();

	public static bool Prefix(
		HardenedShellPower __instance,
		ref decimal __result,
		Creature target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);

		// Mirror vanilla behavior for non-owner targets / zero damage.
		if (target != __instance.Owner || amount == 0m)
		{
			__result = amount;
			return false;
		}

		// Ignore-caps bypasses Hardened Shell's cap.
		if (CardEditorIgnoreEffectHelpers.HasIgnoreEffect(effectiveSource, CardExtraEffectKind.IgnoreDamageCaps))
		{
			__result = amount;
			return false;
		}

		// Vanilla assumes Hardened Shell's internal "damageReceivedThisTurn" never exceeds Amount (because it caps).
		// When we bypass the cap, that internal value can exceed Amount, and vanilla would start returning negative
		// HP loss (i.e., healing) on subsequent hits. Clamp to zero to prevent that.
		decimal received = TryGetDamageReceivedThisTurn(__instance);
		decimal remaining = (decimal)__instance.Amount - received;
		if (remaining <= 0m)
		{
			__result = 0m;
			return false;
		}

		__result = Math.Min(amount, remaining);
		return false;
	}

	private static decimal TryGetDamageReceivedThisTurn(HardenedShellPower power)
	{
		try
		{
			if (_powerModelInternalDataField == null)
			{
				return 0m;
			}

			object? internalData = _powerModelInternalDataField.GetValue(power);
			if (internalData == null)
			{
				return 0m;
			}

			FieldInfo? receivedField;
			if (!_damageReceivedFieldCache.TryGetValue(power, out receivedField))
			{
				receivedField = AccessTools.Field(internalData.GetType(), "damageReceivedThisTurn");
				if (receivedField == null)
				{
					return 0m;
				}
				_damageReceivedFieldCache.Add(power, receivedField);
			}

			object? value = receivedField.GetValue(internalData);
			return value is decimal dec ? dec : 0m;
		}
		catch
		{
			return 0m;
		}
	}
}
