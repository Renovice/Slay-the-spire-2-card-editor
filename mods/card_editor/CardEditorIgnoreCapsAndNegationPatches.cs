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
		return HasActiveIgnoreEffect(kind, card, dealer: null, combatState: card.GetConcreteCombatState(), target: null);
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

		// Version-cached prefilter: unmodified cards (no override/created/marker/payload and
		// no combat grants) provably have no row of this kind, so skip the full effects
		// rebuild. Bypassed while verbose debug logging is on to keep its output intact.
		ulong kindBit = CardEditorRuntimeCaches.MapKindToBit(kind);
		if (!debug
			&& kindBit != 0UL
			&& !CardEditorRuntimeCaches.CardMightHaveRuntimeEffectKind(combatState, effectiveSource, kindBit))
		{
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

			CardPlay? playForConditions = CardEditorCardPlayContext.Current;
			bool previewPlayBuilt = playForConditions != null;
			Creature? ownerCreature = effectiveSource.Owner?.Creature ?? dealer;
			CombatState? resolvedCombatState = combatState
				?? CardEditorCardPlayContext.Current?.Card.GetConcreteCombatState()
				?? effectiveSource.GetConcreteCombatState()
				?? dealer.GetConcreteCombatState();

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

				// The preview CardPlay is only needed for scale-conditioned rows; build it
				// lazily so the common None-scaled/no-match cases allocate nothing.
				if (!previewPlayBuilt)
				{
					playForConditions = BuildPreviewPlay(effectiveSource, target);
					previewPlayBuilt = true;
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

	// Evaluates two ignore kinds from ONE runtime-effects fetch (the list build is the
	// expensive part; the per-row evaluation is pure and cheap). Falls back to the original
	// per-kind path while verbose debug logging is enabled so its output stays identical.
	public static (bool First, bool Second) HasActiveIgnoreEffectPair(
		CardExtraEffectKind kindA,
		CardExtraEffectKind kindB,
		CardModel? cardSource,
		Creature? dealer,
		CombatState? combatState,
		Creature? target)
	{
		if (VerboseDebugEnabled)
		{
			return (
				HasActiveIgnoreEffect(kindA, cardSource, dealer, combatState, target),
				HasActiveIgnoreEffect(kindB, cardSource, dealer, combatState, target));
		}

		CardModel? effectiveSource = ResolveEffectiveSource(cardSource);
		if (effectiveSource == null)
		{
			return (false, false);
		}

		ulong bitA = CardEditorRuntimeCaches.MapKindToBit(kindA);
		ulong bitB = CardEditorRuntimeCaches.MapKindToBit(kindB);
		bool mightA = bitA == 0UL || CardEditorRuntimeCaches.CardMightHaveRuntimeEffectKind(combatState, effectiveSource, bitA);
		bool mightB = bitB == 0UL || CardEditorRuntimeCaches.CardMightHaveRuntimeEffectKind(combatState, effectiveSource, bitB);
		if (!mightA && !mightB)
		{
			return (false, false);
		}

		try
		{
			IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetRuntimeEffectsForExecution(combatState, effectiveSource);
			if (effects == null || effects.Count == 0)
			{
				return (false, false);
			}

			CardPlay? playForConditions = CardEditorCardPlayContext.Current;
			bool previewPlayBuilt = playForConditions != null;
			Creature? ownerCreature = effectiveSource.Owner?.Creature ?? dealer;
			CombatState? resolvedCombatState = combatState
				?? CardEditorCardPlayContext.Current?.Card.GetConcreteCombatState()
				?? effectiveSource.GetConcreteCombatState()
				?? dealer.GetConcreteCombatState();

			bool first = mightA && EvaluateIgnoreEffectsForKind(kindA, effects, effectiveSource, target, resolvedCombatState, ownerCreature, ref playForConditions, ref previewPlayBuilt);
			bool second = mightB && EvaluateIgnoreEffectsForKind(kindB, effects, effectiveSource, target, resolvedCombatState, ownerCreature, ref playForConditions, ref previewPlayBuilt);
			return (first, second);
		}
		catch
		{
			return (false, false);
		}
	}

	// Mirrors the per-row evaluation of HasActiveIgnoreEffect exactly (minus debug logging,
	// which callers only use on the non-verbose path).
	private static bool EvaluateIgnoreEffectsForKind(
		CardExtraEffectKind kind,
		IReadOnlyList<CardExtraEffect> effects,
		CardModel effectiveSource,
		Creature? target,
		CombatState? resolvedCombatState,
		Creature? ownerCreature,
		ref CardPlay? playForConditions,
		ref bool previewPlayBuilt)
	{
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
				return true;
			}

			if (!previewPlayBuilt)
			{
				playForConditions = BuildPreviewPlay(effectiveSource, target);
				previewPlayBuilt = true;
			}

			if (resolvedCombatState == null || ownerCreature == null || playForConditions == null)
			{
				continue;
			}

			int multiplier = CardEditorExtraEffects.GetHistoryCountMultiplierForCardPlay(resolvedCombatState, ownerCreature, playForConditions, effect);
			if (!DoesCountConditionPass(effect, multiplier))
			{
				continue;
			}

			return true;
		}

		return false;
	}

	public static void ApplyAttackDamageProps(AttackCommand attack)
	{
		if (attack?.ModelSource is not CardModel cardSource)
		{
			return;
		}

		Creature? dealer = attack.Attacker;
		CombatState? combatState = dealer.GetConcreteCombatState() ?? cardSource.GetConcreteCombatState();
		ValueProp props = attack.DamageProps;

		(bool ignoreBlock, bool ignoreDamageModifiers) = HasActiveIgnoreEffectPair(
			CardExtraEffectKind.IgnoreBlock,
			CardExtraEffectKind.IgnoreDamageModifiers,
			cardSource,
			dealer,
			combatState,
			target: null);

		if (ignoreBlock)
		{
			props |= ValueProp.Unblockable;
		}

		if (ignoreDamageModifiers)
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

// Per-damage-computation stash of the ignoreCaps/ignoreEnemyReductions flags computed by the
// ModifyDamageInternal prefix. The per-power ModifyDamageCap/ModifyHpLost prefixes fire inside
// the SAME computation with the same (source, target, dealer), so they can reuse the flags
// instead of rebuilding the effects list. The stash is only consulted when the identity tuple
// reference-matches; otherwise callers fall back to full resolution.
internal static class CardEditorDamageCapFlagScope
{
	internal sealed class Snapshot
	{
		internal bool Active;
		internal CardModel? Source;
		internal Creature? Target;
		internal Creature? Dealer;
		internal bool IgnoreCaps;
		internal bool IgnoreEnemyReductions;
	}

	[ThreadStatic] private static bool _active;
	[ThreadStatic] private static CardModel? _source;
	[ThreadStatic] private static Creature? _target;
	[ThreadStatic] private static Creature? _dealer;
	[ThreadStatic] private static bool _ignoreCaps;
	[ThreadStatic] private static bool _ignoreEnemyReductions;

	internal static Snapshot Open(CardModel? source, Creature? target, Creature? dealer, bool ignoreCaps, bool ignoreEnemyReductions)
	{
		Snapshot previous = new Snapshot
		{
			Active = _active,
			Source = _source,
			Target = _target,
			Dealer = _dealer,
			IgnoreCaps = _ignoreCaps,
			IgnoreEnemyReductions = _ignoreEnemyReductions
		};

		_active = true;
		_source = source;
		_target = target;
		_dealer = dealer;
		_ignoreCaps = ignoreCaps;
		_ignoreEnemyReductions = ignoreEnemyReductions;
		return previous;
	}

	internal static void Close(Snapshot? previous)
	{
		if (previous == null)
		{
			_active = false;
			_source = null;
			_target = null;
			_dealer = null;
			return;
		}

		_active = previous.Active;
		_source = previous.Source;
		_target = previous.Target;
		_dealer = previous.Dealer;
		_ignoreCaps = previous.IgnoreCaps;
		_ignoreEnemyReductions = previous.IgnoreEnemyReductions;
	}

	internal static bool TryGetFlags(CardModel? source, Creature? target, Creature? dealer, out bool ignoreCaps, out bool ignoreEnemyReductions)
	{
		if (_active
			&& ReferenceEquals(_source, source)
			&& ReferenceEquals(_target, target)
			&& ReferenceEquals(_dealer, dealer))
		{
			ignoreCaps = _ignoreCaps;
			ignoreEnemyReductions = _ignoreEnemyReductions;
			return true;
		}

		ignoreCaps = false;
		ignoreEnemyReductions = false;
		return false;
	}
}

// Same idea for the IgnoreDamageNegation flag computed by the Hook.ModifyHpLost prefix and
// re-checked by the BufferPower prefix within the same HP-loss computation.
internal static class CardEditorHpLossNegationScope
{
	internal sealed class Snapshot
	{
		internal bool Active;
		internal CardModel? Source;
		internal Creature? Target;
		internal Creature? Dealer;
		internal bool IgnoreNegation;
	}

	[ThreadStatic] private static bool _active;
	[ThreadStatic] private static CardModel? _source;
	[ThreadStatic] private static Creature? _target;
	[ThreadStatic] private static Creature? _dealer;
	[ThreadStatic] private static bool _ignoreNegation;

	internal static Snapshot Open(CardModel? source, Creature? target, Creature? dealer, bool ignoreNegation)
	{
		Snapshot previous = new Snapshot
		{
			Active = _active,
			Source = _source,
			Target = _target,
			Dealer = _dealer,
			IgnoreNegation = _ignoreNegation
		};

		_active = true;
		_source = source;
		_target = target;
		_dealer = dealer;
		_ignoreNegation = ignoreNegation;
		return previous;
	}

	internal static void Close(Snapshot? previous)
	{
		if (previous == null)
		{
			_active = false;
			_source = null;
			_target = null;
			_dealer = null;
			return;
		}

		_active = previous.Active;
		_source = previous.Source;
		_target = previous.Target;
		_dealer = previous.Dealer;
		_ignoreNegation = previous.IgnoreNegation;
	}

	internal static bool TryGetFlag(CardModel? source, Creature? target, Creature? dealer, out bool ignoreNegation)
	{
		if (_active
			&& ReferenceEquals(_source, source)
			&& ReferenceEquals(_target, target)
			&& ReferenceEquals(_dealer, dealer))
		{
			ignoreNegation = _ignoreNegation;
			return true;
		}

		ignoreNegation = false;
		return false;
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
		ICombatState? combatState,
		Creature? target,
		Creature? dealer,
		decimal damage,
		ValueProp props,
		CardModel? cardSource,
		ModifyDamageHookType modifyDamageHookType,
		ref List<AbstractModel> modifiers,
		ref decimal __result,
		out CardEditorDamageCapFlagScope.Snapshot? __state)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		// Public-build signature gives ICombatState; the pair-fetch (one effects build for both
		// kinds) is the perf-pass optimisation and is kept.
		CombatState? concreteCombatState = combatState.GetConcreteCombatState();
		(bool ignoreCaps, bool ignoreEnemyReductions) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
			CardExtraEffectKind.IgnoreDamageCaps,
			CardExtraEffectKind.IgnoreEnemyDamageReductions,
			effectiveSource,
			dealer,
			concreteCombatState,
			target);

		// Stash the flags for the per-power prefixes that fire inside this same computation
		// (Slippery/HardToKill/Intangible/HardenedShell); closed by the Finalizer below.
		__state = CardEditorDamageCapFlagScope.Open(effectiveSource, target, dealer, ignoreCaps, ignoreEnemyReductions);

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

		// One listener snapshot reused across the phases: the listener set cannot change
		// mid-computation (these hooks are pure value queries) and vanilla iterates the
		// same source for each phase.
		IEnumerable<AbstractModel> hookListeners = runState.IterateHookListenersCompat(combatState);

		if (modifyDamageHookType.HasFlag(ModifyDamageHookType.Additive))
		{
			foreach (AbstractModel item in hookListeners)
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
			foreach (AbstractModel item in hookListeners)
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
			foreach (AbstractModel item in hookListeners)
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

	// Runs even when the patched method throws, so the ThreadStatic scope can never leak.
	// __state is null only if the Prefix did not run to the Open call.
	public static void Finalizer(CardEditorDamageCapFlagScope.Snapshot? __state)
	{
		if (__state != null)
		{
			CardEditorDamageCapFlagScope.Close(__state);
		}
	}
}

// v0.109.0 folded the four HpLost hook statics into Hook.ModifyHpLost(..., HpLossHookPhase, ...).
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class Hook_ModifyHpLost_IgnoreNegation_Patch
{
	public static bool Prefix(
		IRunState runState,
		ICombatState? combatState,
		Creature target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource,
		HpLossHookPhase phases,
		ref IEnumerable<AbstractModel> modifiers,
		ref decimal __result,
		out CardEditorHpLossNegationScope.Snapshot? __state)
	{
		__state = null;
		if (!phases.HasFlag(HpLossHookPhase.AfterOsty))
		{
			return true;
		}

		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		bool ignoreNegation = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageNegation, effectiveSource, dealer, combatState.GetConcreteCombatState(), target);

		// Stash the flag for BufferPower's prefix, which fires inside this same HP-loss
		// computation with the same (source, target, dealer); closed by the Finalizer below.
		__state = CardEditorHpLossNegationScope.Open(effectiveSource, target, dealer, ignoreNegation);

		if (!ignoreNegation)
		{
			return true;
		}

		// "Damage negation" in STS2 is primarily implemented via ModifyHpLostAfterOstyLate (e.g., Buffer -> 0 HP loss).
		// Run the vanilla phases but skip that Late pass, so negation is bypassed without consuming the negating power.
		decimal num = amount;
		List<AbstractModel> list = new List<AbstractModel>();

		// One listener snapshot reused across the phases (pure value-query hooks; vanilla
		// iterates the same source each phase).
		IEnumerable<AbstractModel> hookListeners = runState.IterateHookListenersCompat(combatState);
		if (phases.HasFlag(HpLossHookPhase.BeforeOsty))
		{
			foreach (AbstractModel item in hookListeners)
			{
				decimal before = num;
				num = item.ModifyHpLostBeforeOsty(target, num, props, dealer, cardSource);
				if (decimal.Truncate(before) != decimal.Truncate(num))
				{
					list.Add(item);
				}
			}
			foreach (AbstractModel item in hookListeners)
			{
				decimal before = num;
				num = item.ModifyHpLostBeforeOstyLate(target, num, props, dealer, cardSource);
				if (decimal.Truncate(before) != decimal.Truncate(num))
				{
					list.Add(item);
				}
			}
		}
		foreach (AbstractModel item in hookListeners)
		{
			decimal before = num;
			num = item.ModifyHpLostAfterOsty(target, num, props, dealer, cardSource);
			if (decimal.Truncate(before) != decimal.Truncate(num))
			{
				list.Add(item);
			}
		}

		modifiers = list;
		__result = num;
		return false;
	}

	// Runs even when the patched method throws, so the ThreadStatic scope can never leak.
	public static void Finalizer(CardEditorHpLossNegationScope.Snapshot? __state)
	{
		if (__state != null)
		{
			CardEditorHpLossNegationScope.Close(__state);
		}
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
		if (!CardEditorDamageCapFlagScope.TryGetFlags(effectiveSource, target, dealer, out bool ignoreCaps, out bool ignoreEnemyReductions))
		{
			(ignoreCaps, ignoreEnemyReductions) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
				CardExtraEffectKind.IgnoreDamageCaps,
				CardExtraEffectKind.IgnoreEnemyDamageReductions,
				effectiveSource,
				dealer,
				target.GetConcreteCombatState(),
				target);
		}
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

[HarmonyPatch(typeof(BufferPower), nameof(BufferPower.ModifyHpLostAfterOstyLate))]
internal static class BufferPower_ModifyHpLostAfterOstyLate_IgnoreNegation_Patch
{
	public static bool Prefix(
		BufferPower __instance,
		ref decimal __result,
		Creature target,
		decimal amount,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (target != __instance.Owner)
		{
			return true;
		}

		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		if (!CardEditorHpLossNegationScope.TryGetFlag(effectiveSource, target, dealer, out bool ignoreNegation))
		{
			ignoreNegation = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffect(CardExtraEffectKind.IgnoreDamageNegation, effectiveSource, dealer, target.GetConcreteCombatState(), target);
		}
		if (!ignoreNegation)
		{
			return true;
		}

		__result = amount;
		return false;
	}
}

internal static class SlipperyPower_ModifyDamageCap_IgnoreCaps_Patch
{
	public static bool Prepare()
	{
		MethodInfo? method = AccessTools.Method(typeof(SlipperyPower), nameof(SlipperyPower.ModifyDamageCap));
		return method != null && method.DeclaringType == typeof(SlipperyPower);
	}

	public static bool Prefix(
		ref decimal __result,
		Creature? target,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		if (!CardEditorDamageCapFlagScope.TryGetFlags(effectiveSource, target, dealer, out bool ignoreCaps, out bool ignoreEnemyReductions))
		{
			(ignoreCaps, ignoreEnemyReductions) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
				CardExtraEffectKind.IgnoreDamageCaps,
				CardExtraEffectKind.IgnoreEnemyDamageReductions,
				effectiveSource,
				dealer,
				target.GetConcreteCombatState(),
				target);
		}
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
		if (!CardEditorDamageCapFlagScope.TryGetFlags(effectiveSource, target, dealer, out bool ignoreCaps, out bool ignoreEnemyReductions))
		{
			(ignoreCaps, ignoreEnemyReductions) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
				CardExtraEffectKind.IgnoreDamageCaps,
				CardExtraEffectKind.IgnoreEnemyDamageReductions,
				effectiveSource,
				dealer,
				target.GetConcreteCombatState(),
				target);
		}
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

		if (!CardEditorDamageCapFlagScope.TryGetFlags(effectiveSource, target, dealer, out bool ignoreCaps, out bool ignoreEnemyReductions))
		{
			(ignoreCaps, ignoreEnemyReductions) = CardEditorIgnoreEffectHelpers.HasActiveIgnoreEffectPair(
				CardExtraEffectKind.IgnoreDamageCaps,
				CardExtraEffectKind.IgnoreEnemyDamageReductions,
				effectiveSource,
				dealer,
				target.GetConcreteCombatState(),
				target);
		}

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
