using System;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

// Version-keyed per-card caches used as cheap front gates on gameplay hot paths
// (TargetType/ShouldGlowGold/description postfixes, ShouldPlay, damage-ignore checks,
// self-pile auto sweeps, dynamic cost refresh).
//
// Correctness model:
// - Card-level surfaces (stored/draft/instance overrides, created-card type, stateful
//   transform markers, self-scaling payloads, borrowed sources) only change through seams
//   that call CardEditorRuntimeCacheVersion.Bump(), so they are cached per card keyed on
//   the version counter (+ upgrade level, which changes without bumps).
// - Combat-scoped granted/aura effects are pile- and state-dependent, so they are NEVER
//   cached here: whenever the combat has ANY temporary/aura grant the gates conservatively
//   answer "might have" and callers fall through to the full (original) evaluation.
internal static class CardEditorRuntimeCaches
{
	// Bit positions for the effect kinds hot paths query. Only presence is cached; all
	// validity/condition evaluation still runs in the original code when a bit is set.
	internal const ulong IgnoreBlockBit = 1UL << 0;
	internal const ulong IgnoreDamageModifiersBit = 1UL << 1;
	internal const ulong IgnoreDamageCapsBit = 1UL << 2;
	internal const ulong IgnoreEnemyDamageReductionsBit = 1UL << 3;
	internal const ulong IgnoreDamageNegationBit = 1UL << 4;
	internal const ulong HitsAllEnemiesBit = 1UL << 5;
	internal const ulong PlayPermissionBit = 1UL << 6;
	internal const ulong PlayPreventionBit = 1UL << 7;
	internal const ulong SelfPileAutoBits = 1UL << 8;
	internal const ulong CardDealsExtraDamageBit = 1UL << 9;
	internal const ulong CardCostsLessBit = 1UL << 10;
	internal const ulong CardStarCostsLessBit = 1UL << 11;

	private sealed class SurfaceEntry
	{
		internal long Version = long.MinValue;
		internal bool HasSurface;
	}

	private sealed class KindBitsEntry
	{
		internal long VersionNull = long.MinValue;
		internal int UpgradeLevelNull = -1;
		internal ulong BitsNull;

		internal long VersionCombat = long.MinValue;
		internal int UpgradeLevelCombat = -1;
		internal ulong BitsCombat;
		internal WeakReference<CombatState>? Combat;
	}

	private static readonly ConditionalWeakTable<CardModel, SurfaceEntry> _surfaceCache = new();
	private static readonly ConditionalWeakTable<CardModel, KindBitsEntry> _kindBitsCache = new();

	internal static ulong MapKindToBit(CardExtraEffectKind kind)
	{
		return kind switch
		{
			CardExtraEffectKind.IgnoreBlock => IgnoreBlockBit,
			CardExtraEffectKind.IgnoreDamageModifiers => IgnoreDamageModifiersBit,
			CardExtraEffectKind.IgnoreDamageCaps => IgnoreDamageCapsBit,
			CardExtraEffectKind.IgnoreEnemyDamageReductions => IgnoreEnemyDamageReductionsBit,
			CardExtraEffectKind.IgnoreDamageNegation => IgnoreDamageNegationBit,
			CardExtraEffectKind.HitsAllEnemies => HitsAllEnemiesBit,
			CardExtraEffectKind.PlayPermission => PlayPermissionBit,
			CardExtraEffectKind.PlayPrevention => PlayPreventionBit,
			CardExtraEffectKind.AutoPlaySelfFromPile => SelfPileAutoBits,
			CardExtraEffectKind.AutoDrawSelfFromPile => SelfPileAutoBits,
			CardExtraEffectKind.ConditionalAutoPlayFromPile => SelfPileAutoBits,
			CardExtraEffectKind.ConditionalAutoDrawFromPile => SelfPileAutoBits,
			CardExtraEffectKind.ConditionalAutoRunEffects => SelfPileAutoBits,
			CardExtraEffectKind.CardDealsExtraDamage => CardDealsExtraDamageBit,
			CardExtraEffectKind.CardCostsLess => CardCostsLessBit,
			CardExtraEffectKind.CardStarCostsLess => CardStarCostsLessBit,
			_ => 0UL
		};
	}

	// True when this combat has any temporary/aura grant state. Grants are pile/state
	// dependent, so gates must treat every card as "might be modified" while any exist.
	internal static bool CombatHasAnyGrantedEffects(CombatState? combatState)
	{
		if (combatState == null)
		{
			return false;
		}

		try
		{
			return CardEditorTemporaryExtraEffectController.HasAny(combatState);
		}
		catch
		{
			return true;
		}
	}

	// Cheap "is this card touched by the mod in any way" prefilter. Must never return
	// false for a card whose runtime/description effect list could be non-empty.
	internal static bool HasAnyModSurface(CardModel? card, CombatState? combatState)
	{
		if (card == null)
		{
			return false;
		}

		if (CombatHasAnyGrantedEffects(combatState))
		{
			return true;
		}

		return HasCardLevelModSurface(card);
	}

	internal static bool HasCardLevelModSurface(CardModel? card)
	{
		if (card == null)
		{
			return false;
		}

		if (card is CardEditorCreatedCardBase)
		{
			return true;
		}

		SurfaceEntry entry = _surfaceCache.GetOrCreateValue(card);
		long version = CardEditorRuntimeCacheVersion.Current;
		if (entry.Version != version)
		{
			entry.HasSurface = ComputeCardLevelModSurface(card);
			entry.Version = version;
		}

		return entry.HasSurface;
	}

	private static bool ComputeCardLevelModSurface(CardModel card)
	{
		try
		{
			if (card.Id != null && CardEditorUiState.TryGetDraftOverride(card.Id, out _))
			{
				return true;
			}

			if (CardEditorOverrides.TryGetEffectiveOverride(card, out _))
			{
				return true;
			}

			if (CardEditorExtraEffects.HasStatefulTransformMarker(card))
			{
				return true;
			}

			if (CardEditorExtraEffects.HasDynamicTransformMarker(card))
			{
				return true;
			}

			if (CardEditorRunSelfScalingState.HasStoredSelfScalingPayload(card))
			{
				return true;
			}

			return false;
		}
		catch
		{
			// Conservative: if the surface probe fails, treat the card as modified.
			return true;
		}
	}

	// True when the card's runtime effect list might contain a row whose top-level Kind
	// maps into kindMask. False is only returned when it provably cannot (no combat
	// grants + version-cached card-level rows contain no matching kind).
	internal static bool CardMightHaveRuntimeEffectKind(CombatState? combatState, CardModel? card, ulong kindMask)
	{
		if (card == null)
		{
			return false;
		}

		if (CombatHasAnyGrantedEffects(combatState))
		{
			return true;
		}

		if (!HasCardLevelModSurface(card))
		{
			return false;
		}

		KindBitsEntry entry = _kindBitsCache.GetOrCreateValue(card);
		long version = CardEditorRuntimeCacheVersion.Current;
		int upgradeLevel = card.GetSafeCurrentUpgradeLevel();

		if (combatState == null)
		{
			if (entry.VersionNull != version || entry.UpgradeLevelNull != upgradeLevel)
			{
				entry.BitsNull = ComputeKindBits(null, card);
				entry.UpgradeLevelNull = upgradeLevel;
				entry.VersionNull = version;
			}

			return (entry.BitsNull & kindMask) != 0UL;
		}

		bool sameCombat = entry.Combat != null
			&& entry.Combat.TryGetTarget(out CombatState? cachedCombat)
			&& ReferenceEquals(cachedCombat, combatState);
		if (!sameCombat || entry.VersionCombat != version || entry.UpgradeLevelCombat != upgradeLevel)
		{
			entry.BitsCombat = ComputeKindBits(combatState, card);
			entry.UpgradeLevelCombat = upgradeLevel;
			entry.VersionCombat = version;
			entry.Combat = new WeakReference<CombatState>(combatState);
		}

		return (entry.BitsCombat & kindMask) != 0UL;
	}

	private static ulong ComputeKindBits(CombatState? combatState, CardModel card)
	{
		try
		{
			ulong bits = 0UL;
			foreach (CardExtraEffect effect in CardEditorExtraEffects.GetRuntimeEffectsForExecution(combatState, card))
			{
				if (effect == null)
				{
					continue;
				}

				bits |= MapKindToBit(effect.Kind);
			}

			return bits;
		}
		catch
		{
			// Conservative: on failure assume every kind might be present.
			return ulong.MaxValue;
		}
	}
}
