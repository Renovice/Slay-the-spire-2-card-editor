using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorIgnoreEffectHelpers
{
	internal static bool VerboseDebugEnabled => CardEditorPerformanceSettings.VerboseDamageDebugLogging;

	private static readonly Action<AttackCommand, ValueProp>? _attackCommandDamagePropsSetter =
		AccessTools.PropertySetter(typeof(AttackCommand), nameof(AttackCommand.DamageProps)) is MethodInfo setter
			? (Action<AttackCommand, ValueProp>)Delegate.CreateDelegate(typeof(Action<AttackCommand, ValueProp>), setter)
			: null;

	private static void LogCapDebug(string message)
	{
		if (!VerboseDebugEnabled)
		{
			return;
		}

		Log.Info($"[CardEditor][IgnoreDamageDebug] {message}");
	}

	private static string DescribeCard(CardModel? card)
	{
		if (card == null)
		{
			return "<null>";
		}

		string id = card.Id?.Entry ?? "<no-id>";
		return $"{id}@{RuntimeHelpers.GetHashCode(card)}";
	}

	private static string DescribeCreature(Creature? creature)
	{
		if (creature == null)
		{
			return "<null>";
		}

		return $"{creature.Name}@{RuntimeHelpers.GetHashCode(creature)}";
	}

	private static string DescribeEffects(IReadOnlyList<CardExtraEffect> effects)
	{
		if (effects == null || effects.Count == 0)
		{
			return "<none>";
		}

		return string.Join(", ", effects
			.Where(effect => effect != null)
			.Select(effect => $"{effect.Kind}(amt={effect.Amount},scale={effect.ScaleMode},asPower={effect.AsPower})"));
	}

	private static bool TargetHasRelevantCapPower(Creature? target)
	{
		return target?.GetPower<SlipperyPower>() != null
			|| target?.GetPower<HardToKillPower>() != null
			|| target?.GetPower<HardenedShellPower>() != null
			|| target?.GetPower<IntangiblePower>() != null;
	}

	public static bool ShouldLogCapDebug(CardExtraEffectKind kind, CardModel? cardSource, Creature? target)
	{
		if (!VerboseDebugEnabled)
		{
			return false;
		}

		if (kind != CardExtraEffectKind.IgnoreDamageCaps && kind != CardExtraEffectKind.IgnoreEnemyDamageReductions)
		{
			return false;
		}

		return TargetHasRelevantCapPower(target)
			|| cardSource == null
			|| CardEditorEffectSourceContext.Current != null
			|| CardEditorCardPlayContext.Current?.Card != null
			|| CardEditorHookModelContext.Current is CardModel;
	}

	public static CardModel? ResolveEffectiveSource(CardModel? cardSource)
	{
		if (CardEditorEffectSourceContext.Current is CardModel effectSource)
		{
			return effectSource;
		}

		if (cardSource != null)
		{
			return cardSource;
		}

		if (CardEditorCardPlayContext.Current?.Card is CardModel currentPlayCard)
		{
			return currentPlayCard;
		}

		if (CardEditorHookModelContext.Current is CardModel hookCard)
		{
			return hookCard;
		}

		return null;
	}

	public static bool HasIgnoreEffect(CardModel? card, CardExtraEffectKind kind)
	{
		return HasActiveIgnoreEffect(kind, card, dealer: null, combatState: card?.CombatState.AsCombatState(), target: null);
	}

	public static bool HasActiveIgnoreEffect(
		CardExtraEffectKind kind,
		CardModel? cardSource,
		Creature? dealer,
		CombatState? combatState,
		Creature? target)
	{
		bool debug = ShouldLogCapDebug(kind, cardSource, target);
		CardModel? effectiveSource = ResolveEffectiveSource(cardSource);
		if (effectiveSource == null)
		{
			if (debug)
			{
				LogCapDebug(
					$"HasActiveIgnoreEffect:no-source kind={kind} cardSource={DescribeCard(cardSource)} playCard={DescribeCard(CardEditorCardPlayContext.Current?.Card)} hookCard={DescribeCard(CardEditorHookModelContext.Current as CardModel)} target={DescribeCreature(target)}");
			}
			return false;
		}

		try
		{
			IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetRuntimeEffectsForExecution(combatState, effectiveSource);
			if (effects == null || effects.Count == 0)
			{
				if (debug)
				{
					LogCapDebug(
						$"HasActiveIgnoreEffect:no-effects kind={kind} source={DescribeCard(effectiveSource)} cardSource={DescribeCard(cardSource)} target={DescribeCreature(target)}");
				}
				return false;
			}

			CardPlay? playForConditions = CardEditorCardPlayContext.Current ?? BuildPreviewPlay(effectiveSource, target);
			Creature? ownerCreature = effectiveSource.Owner?.Creature ?? dealer;
			CombatState? resolvedCombatState = combatState
				?? CardEditorCardPlayContext.Current?.Card?.CombatState.AsCombatState()
				?? effectiveSource.CombatState.AsCombatState()
				?? dealer?.CombatState.AsCombatState();

			if (debug)
			{
				LogCapDebug(
					$"HasActiveIgnoreEffect:start kind={kind} source={DescribeCard(effectiveSource)} cardSource={DescribeCard(cardSource)} playCard={DescribeCard(CardEditorCardPlayContext.Current?.Card)} hookCard={DescribeCard(CardEditorHookModelContext.Current as CardModel)} target={DescribeCreature(target)} owner={DescribeCreature(ownerCreature)} effects=[{DescribeEffects(effects)}]");
			}

			foreach (CardExtraEffect effect in effects)
			{
				if (effect == null
					|| effect.Kind != kind
					|| (effect.AsPower && CardEditorExtraEffects.SupportsAsPower(effect.Kind))
					|| !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
				{
					continue;
				}

				if (effect.ScaleMode == CardExtraEffectScaleMode.None)
				{
					if (debug)
					{
						LogCapDebug($"HasActiveIgnoreEffect:match kind={kind} source={DescribeCard(effectiveSource)} mode=None amount={effect.Amount}");
					}
					return true;
				}

				if (resolvedCombatState == null || ownerCreature == null || playForConditions == null)
				{
					if (debug)
					{
						LogCapDebug(
							$"HasActiveIgnoreEffect:missing-context kind={kind} source={DescribeCard(effectiveSource)} combat={(resolvedCombatState != null)} owner={(ownerCreature != null)} play={(playForConditions != null)}");
					}
					continue;
				}

				int multiplier = CardEditorExtraEffects.GetHistoryCountMultiplierForCardPlay(resolvedCombatState, ownerCreature, playForConditions, effect);
				if (!DoesCountConditionPass(effect, multiplier))
				{
					if (debug)
					{
						LogCapDebug(
							$"HasActiveIgnoreEffect:count-failed kind={kind} source={DescribeCard(effectiveSource)} count={multiplier} comparison={effect.CountComparison} threshold={effect.CountConditionAmount}");
					}
					continue;
				}

				if (debug)
				{
					LogCapDebug(
						$"HasActiveIgnoreEffect:scaled-match kind={kind} source={DescribeCard(effectiveSource)} count={multiplier} scale={effect.ScaleMode}");
				}
				return true;
			}

			if (debug)
			{
				LogCapDebug($"HasActiveIgnoreEffect:miss kind={kind} source={DescribeCard(effectiveSource)} target={DescribeCreature(target)}");
			}
			return false;
		}
		catch (Exception ex)
		{
			if (debug)
			{
				LogCapDebug(
					$"HasActiveIgnoreEffect:error kind={kind} source={DescribeCard(effectiveSource)} error={ex.GetType().Name}: {ex.Message}");
			}
			return false;
		}
	}

	public static void ApplyAttackDamageProps(AttackCommand attack)
	{
		if (attack?.ModelSource is not CardModel cardSource)
		{
			return;
		}

		Creature? dealer = attack.Attacker;
		CombatState? combatState = dealer?.CombatState.AsCombatState() ?? cardSource.CombatState.AsCombatState();
		ValueProp props = attack.DamageProps;

		if (HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreBlock, cardSource, dealer, combatState, target: null))
		{
			props |= ValueProp.Unblockable;
		}

		if (HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageModifiers, cardSource, dealer, combatState, target: null))
		{
			props |= ValueProp.Unpowered;
		}

		if (props == attack.DamageProps)
		{
			return;
		}

		_attackCommandDamagePropsSetter?.Invoke(attack, props);
	}

	private static CardPlay BuildPreviewPlay(CardModel card, Creature? target)
	{
		return new CardPlay
		{
			Card = card,
			Target = target,
			ResultPile = card.Pile?.Type ?? PileType.None,
			Resources = new ResourceInfo
			{
				EnergySpent = 0,
				EnergyValue = 0,
				StarsSpent = 0,
				StarValue = 0
			},
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};
	}

	private static bool DoesCountConditionPass(CardExtraEffect effect, int count)
	{
		if (effect == null)
		{
			return true;
		}

		if (effect.CountComparison == CardExtraEffectCountComparison.None)
		{
			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				return CardEditorExtraEffects.ResolveHistoryScalingMultiplier(effect, count) > 0 || effect.HistoryScalingIncludesBase;
			}

			return count > 0 || (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount && effect.HistoryScalingIncludesBase);
		}

		int threshold = Math.Max(0, effect.CountConditionAmount);
		return effect.CountComparison switch
		{
			CardExtraEffectCountComparison.AtLeast => count >= threshold,
			CardExtraEffectCountComparison.AtMost => count <= threshold,
			CardExtraEffectCountComparison.Exactly => count == threshold,
			_ => count > 0
		};
	}
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class AttackCommand_Execute_ApplyIgnoreDamageProps_Patch
{
	public static void Prefix(AttackCommand __instance)
	{
		CardEditorIgnoreEffectHelpers.ApplyAttackDamageProps(__instance);
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

	private static bool ShouldSkipTargetOwnedCap(AbstractModel model, Creature? target, decimal candidate)
	{
		if (target == null)
		{
			return false;
		}

		// Only skip explicit caps that are clearly target-owned. This keeps dealer-side cap logic intact
		// while allowing "ignore enemy damage reductions" to bypass powers like Slippery/Hard to Kill.
		if (candidate >= decimal.MaxValue)
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
		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource, dealer, combatState, target);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreEnemyDamageReductions, effectiveSource, dealer, combatState, target);
		bool debug = CardEditorIgnoreEffectHelpers.ShouldLogCapDebug(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource ?? cardSource, target);
		if (debug)
		{
			Log.Info(
				$"[CardEditor][IgnoreDamageDebug] ModifyDamageInternal:start target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} damage={damage} props={props} ignoreCaps={ignoreCaps} ignoreEnemyReductions={ignoreEnemyReductions} hookType={modifyDamageHookType}");
		}
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
				bool skipAsEnemyReduction = ignoreEnemyReductions && ShouldSkipTargetOwnedCap(item, target, candidate);
				if (debug && candidate < decimal.MaxValue)
				{
					Log.Info(
						$"[CardEditor][IgnoreDamageDebug] ModifyDamageInternal:cap target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} model={item.GetType().Name} candidate={candidate} numBefore={num} skipped={skipAsEnemyReduction}");
				}
				if (skipAsEnemyReduction)
				{
					continue;
				}
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
		if (debug)
		{
			Log.Info(
				$"[CardEditor][IgnoreDamageDebug] ModifyDamageInternal:end target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} result={num} modifiers=[{string.Join(", ", list.Select(item => item.GetType().Name))}]");
		}
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
		if (!CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageNegation, effectiveSource, dealer, combatState, target))
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
		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreEnemyDamageReductions, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		if (CardEditorIgnoreEffectHelpers.VerboseDebugEnabled)
		{
			Log.Info(
				$"[CardEditor][IgnoreDamageDebug] IntangiblePower.ModifyHpLostAfterOsty target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} ignoreCaps={ignoreCaps} ignoreEnemyReductions={ignoreEnemyReductions} props={props}");
		}
		if (!ignoreCaps && !ignoreEnemyReductions)
		{
			return true;
		}

		__result = amount;
		return false;
	}
}

[HarmonyPatch(typeof(SlipperyPower), nameof(SlipperyPower.ModifyDamageCap))]
internal static class SlipperyPower_ModifyDamageCap_IgnoreCaps_Patch
{
	public static bool Prefix(
		ref decimal __result,
		Creature? target,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreEnemyDamageReductions, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		if (CardEditorIgnoreEffectHelpers.VerboseDebugEnabled)
		{
			Log.Info(
				$"[CardEditor][IgnoreDamageDebug] SlipperyPower.ModifyDamageCap target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} ignoreCaps={ignoreCaps} ignoreEnemyReductions={ignoreEnemyReductions} props={props}");
		}
		if (!ignoreCaps && !ignoreEnemyReductions)
		{
			return true;
		}

		__result = decimal.MaxValue;
		return false;
	}
}

[HarmonyPatch(typeof(HardToKillPower), nameof(HardToKillPower.ModifyDamageCap))]
internal static class HardToKillPower_ModifyDamageCap_IgnoreCaps_Patch
{
	public static bool Prefix(
		ref decimal __result,
		Creature? target,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreEnemyDamageReductions, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		if (CardEditorIgnoreEffectHelpers.VerboseDebugEnabled)
		{
			Log.Info(
				$"[CardEditor][IgnoreDamageDebug] HardToKillPower.ModifyDamageCap target={(target?.Name ?? "<null>")} source={(effectiveSource?.Id?.Entry ?? cardSource?.Id?.Entry ?? "<null>")} ignoreCaps={ignoreCaps} ignoreEnemyReductions={ignoreEnemyReductions} props={props}");
		}
		if (!ignoreCaps && !ignoreEnemyReductions)
		{
			return true;
		}

		__result = decimal.MaxValue;
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

		bool ignoreCaps = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageCaps, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);
		bool ignoreEnemyReductions = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreEnemyDamageReductions, effectiveSource, dealer, target?.CombatState.AsCombatState(), target);

		// Ignore-caps and ignore-enemy-damage-reductions both bypass Hardened Shell's target-owned cap.
		if (ignoreCaps || ignoreEnemyReductions)
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
