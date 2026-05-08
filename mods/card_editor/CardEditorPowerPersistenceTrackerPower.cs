using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorPowerPersistenceEntry
{
	public string PowerId { get; set; } = string.Empty;
	public int ProtectedAmount { get; set; }
	public CardExtraEffectDuration Duration { get; set; } = CardExtraEffectDuration.Permanent;
	public int AppliedRoundNumber { get; set; }
	public CombatSide AppliedSide { get; set; } = CombatSide.None;
}

internal sealed class CardEditorPowerPersistenceTrackerPower : PowerModel
{
	public List<CardEditorPowerPersistenceEntry> Entries { get; set; } = new();

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public void Track(string? powerId, int protectedAmount, CardExtraEffectDuration duration, CombatState? combatState)
	{
		if (string.IsNullOrWhiteSpace(powerId) || protectedAmount <= 0)
		{
			return;
		}

		string normalizedPowerId = powerId.Trim();
		CardEditorPowerPersistenceEntry? existing = Entries.FirstOrDefault(entry =>
			entry != null
			&& string.Equals(entry.PowerId, normalizedPowerId, StringComparison.Ordinal)
			&& entry.Duration == duration
			&& entry.AppliedRoundNumber == (combatState?.RoundNumber ?? 0)
			&& entry.AppliedSide == (combatState?.CurrentSide ?? CombatSide.None));

		if (existing != null)
		{
			existing.ProtectedAmount = Math.Clamp(existing.ProtectedAmount + protectedAmount, 0, 999999999);
			return;
		}

		Entries.Add(new CardEditorPowerPersistenceEntry
		{
			PowerId = normalizedPowerId,
			ProtectedAmount = protectedAmount,
			Duration = duration,
			AppliedRoundNumber = combatState?.RoundNumber ?? 0,
			AppliedSide = combatState?.CurrentSide ?? CombatSide.None
		});
	}

	public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		RestoreProtectedFloor(power);
		return Task.CompletedTask;
	}

	internal bool ShouldProtectRemoval(PowerModel? power)
	{
		if (power == null || IsInternalTrackerPower(power))
		{
			return false;
		}

		Creature? owner = TryGetOwner();
		Creature? powerOwner = TryGetOwner(power);
		return owner != null
			&& ReferenceEquals(owner, powerOwner)
			&& GetProtectedFloor(power) > 0;
	}

	internal Task RestoreProtectedPowerBeforeRemoval(PowerModel? power)
	{
		RestoreProtectedFloor(power, forceToFloor: true);
		return Task.CompletedTask;
	}

	internal static async Task RunDurationBoundary(CombatState? combatState, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
	{
		if (combatState == null || side == CardExtraEffectTurnBoundarySide.Both)
		{
			return;
		}

		foreach (Creature creature in SnapshotCreatures(combatState))
		{
			List<CardEditorPowerPersistenceTrackerPower> trackers = creature.Powers?
				.OfType<CardEditorPowerPersistenceTrackerPower>()
				.ToList() ?? new List<CardEditorPowerPersistenceTrackerPower>();

			foreach (CardEditorPowerPersistenceTrackerPower tracker in trackers)
			{
				await tracker.TryExpire(boundary, side, combatState);
			}
		}
	}

	internal static bool TargetHasConfiguredPower(Creature? target, string? powerId)
	{
		return FindConfiguredPower(target, powerId) != null;
	}

	private void RestoreProtectedFloor(PowerModel? power, bool forceToFloor = false)
	{
		if (power == null || IsInternalTrackerPower(power))
		{
			return;
		}

		Creature? owner = TryGetOwner();
		Creature? powerOwner = TryGetOwner(power);
		if (owner == null || !ReferenceEquals(owner, powerOwner))
		{
			return;
		}

		int floor = GetProtectedFloor(power);
		if (floor <= 0)
		{
			return;
		}

		try
		{
			if (forceToFloor || power.Amount < floor)
			{
				power.SetAmount(floor, silent: true);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to restore protected power floor for {power?.Id}: {ex}");
		}
	}

	private int GetProtectedFloor(PowerModel? power)
	{
		if (power == null || Entries.Count == 0)
		{
			return 0;
		}

		long floor = 0;
		foreach (CardEditorPowerPersistenceEntry entry in Entries)
		{
			if (entry == null || entry.ProtectedAmount <= 0)
			{
				continue;
			}
			if (PowerMatchesConfiguredId(power, entry.PowerId))
			{
				floor += entry.ProtectedAmount;
			}
		}

		return floor <= 0 ? 0 : floor >= int.MaxValue ? int.MaxValue : (int)floor;
	}

	private async Task TryExpire(CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side, CombatState combatState)
	{
		Creature? owner = TryGetOwner();
		if (owner == null || Entries.Count == 0)
		{
			return;
		}

		List<CardEditorPowerPersistenceEntry> expired = Entries
			.Where(entry => ShouldExpire(entry, owner, boundary, side, combatState))
			.ToList();
		if (expired.Count == 0)
		{
			return;
		}

		foreach (CardEditorPowerPersistenceEntry entry in expired)
		{
			Entries.Remove(entry);
		}

		if (Entries.Count == 0)
		{
			await PowerCmd.Remove(this);
		}
	}

	private static bool ShouldExpire(CardEditorPowerPersistenceEntry? entry, Creature owner, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side, CombatState combatState)
	{
		if (entry == null
			|| owner == null
			|| side == CardExtraEffectTurnBoundarySide.Both
			|| entry.Duration == CardExtraEffectDuration.Permanent)
		{
			return false;
		}

		CombatSide ownerSide = owner.Side;
		CardExtraEffectTurnBoundarySide expectedSide = ownerSide == CombatSide.Enemy
			? CardExtraEffectTurnBoundarySide.EnemyTurn
			: CardExtraEffectTurnBoundarySide.YourTurn;
		if (side != expectedSide)
		{
			return false;
		}

		CardExtraEffectTurnBoundary expectedBoundary = entry.Duration switch
		{
			CardExtraEffectDuration.ThisTurn => CardExtraEffectTurnBoundary.EndAfterDiscard,
			CardExtraEffectDuration.NextTurnStartBeforeDraw => CardExtraEffectTurnBoundary.Start,
			CardExtraEffectDuration.NextTurnStartAfterDraw => CardExtraEffectTurnBoundary.StartAfterDraw,
			CardExtraEffectDuration.NextTurnEndBeforeDiscard => CardExtraEffectTurnBoundary.End,
			CardExtraEffectDuration.NextTurnEndAfterDiscard => CardExtraEffectTurnBoundary.EndAfterDiscard,
			_ => CardExtraEffectTurnBoundary.EndAfterDiscard
		};
		if (boundary != expectedBoundary)
		{
			return false;
		}

		if (CardEditorExtraEffects.IsBoundaryDuration(entry.Duration)
			&& combatState.RoundNumber == entry.AppliedRoundNumber
			&& combatState.CurrentSide == entry.AppliedSide
			&& ownerSide == entry.AppliedSide)
		{
			return false;
		}

		return true;
	}

	private static IEnumerable<Creature> SnapshotCreatures(CombatState combatState)
	{
		foreach (Creature creature in combatState.PlayerCreatures ?? Enumerable.Empty<Creature>())
		{
			if (creature != null)
			{
				yield return creature;
			}
		}

		foreach (Creature creature in combatState.Enemies ?? Enumerable.Empty<Creature>())
		{
			if (creature != null)
			{
				yield return creature;
			}
		}
	}

	private Creature? TryGetOwner()
	{
		return TryGetOwner(this);
	}

	private static Creature? TryGetOwner(PowerModel? power)
	{
		try
		{
			return power?.Owner;
		}
		catch
		{
			return null;
		}
	}

	private static PowerModel? FindConfiguredPower(Creature? target, string? powerId)
	{
		if (target == null || string.IsNullOrWhiteSpace(powerId))
		{
			return null;
		}

		return target.Powers?.FirstOrDefault(power => PowerMatchesConfiguredId(power, powerId));
	}

	private static bool PowerMatchesConfiguredId(PowerModel? power, string? powerId)
	{
		if (power == null || string.IsNullOrWhiteSpace(powerId))
		{
			return false;
		}

		if (power is CardEditorCustomStatusPower customStatus)
		{
			return customStatus.MatchesConfiguredId(powerId);
		}

		try
		{
			ModelId parsed = ModelId.Deserialize(powerId.Trim());
			return power.Id != null && power.Id.Equals(parsed);
		}
		catch
		{
			return string.Equals(power.Id?.ToString() ?? string.Empty, powerId.Trim(), StringComparison.Ordinal);
		}
	}

	private static bool IsInternalTrackerPower(PowerModel? power)
	{
		return power is CardEditorPowerPersistenceTrackerPower
			or CardEditorPowerDurationTrackerPower
			or CardEditorTempStrengthTrackerPower
			or CardEditorTempDexterityTrackerPower
			or CardEditorTempFocusTrackerPower
			or CardEditorTempWeakTrackerPower
			or CardEditorTempFrailTrackerPower
			or CardEditorTempVulnerableTrackerPower
			or CardEditorTempPoisonTrackerPower
			or CardEditorTempDoomTrackerPower
			or CardEditorTempArtifactTrackerPower
			or CardEditorTempThornsTrackerPower
			or CardEditorTempRegenTrackerPower
			or CardEditorTempPlatingTrackerPower
			or CardEditorTempIntangibleTrackerPower
			or CardEditorTempBufferTrackerPower
			or CardEditorTempVigorTrackerPower
			or CardEditorTempBlurTrackerPower
			or CardEditorTempRitualTrackerPower
			or CardEditorTempConstrictTrackerPower;
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.PowerCmd), nameof(MegaCrit.Sts2.Core.Commands.PowerCmd.Remove), new[] { typeof(PowerModel) })]
internal static class PowerCmd_Remove_CardEditorPowerPersistence_Patch
{
	private static bool Prefix(PowerModel? power, ref Task __result)
	{
		if (power == null)
		{
			return true;
		}

		Creature? owner = null;
		try
		{
			owner = power.Owner;
		}
		catch
		{
		}

		CardEditorPowerPersistenceTrackerPower? tracker = owner?.Powers?
			.OfType<CardEditorPowerPersistenceTrackerPower>()
			.FirstOrDefault(candidate => candidate != null && candidate.ShouldProtectRemoval(power));
		if (tracker == null)
		{
			return true;
		}

		__result = tracker.RestoreProtectedPowerBeforeRemoval(power);
		return false;
	}
}
