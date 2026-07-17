using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal abstract class CardEditorTempStatTrackerPower<TUnderlying> : PowerModel where TUnderlying : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override bool AllowNegative => true;

	protected override bool IsVisibleInternal => false;

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		Creature? owner = Owner;
		if (owner == null)
		{
			return;
		}
		if (side != owner.Side)
		{
			return;
		}
		// Co-op extra turns end the turn for a subset of creatures only; "this turn" effects on
		// non-participants must not expire during someone else's extra turn.
		if (participants != null && !participants.Contains(owner))
		{
			return;
		}

		int delta = Amount;
		await PowerCmd.Remove(this);
		if (delta == 0)
		{
			return;
		}

		// The underlying power may already be gone (e.g. Weak's own duration tick removed the
		// last stack this same turn end). Applying the negative delta anyway would create a live
		// phantom power with negative stacks that still affects combat. Reduce only what exists.
		if (delta > 0)
		{
			TUnderlying? active = owner.GetPower<TUnderlying>();
			if (active == null || active.Amount <= 0)
			{
				return;
			}

			int toRemove = Math.Min(delta, active.Amount);
			if (toRemove >= active.Amount)
			{
				await PowerCmd.Remove(active);
			}
			else
			{
				await PowerCmd.ModifyAmount(active, -toRemove, owner, null);
			}
			return;
		}

		// Negative tracked delta means the temporary effect REDUCED the stat; restore it.
		await PowerCmd.Apply<TUnderlying>(owner, -delta, owner, null);
	}
}

internal sealed class CardEditorTempStrengthTrackerPower : CardEditorTempStatTrackerPower<StrengthPower>
{
}

internal sealed class CardEditorTempDexterityTrackerPower : CardEditorTempStatTrackerPower<DexterityPower>
{
}

internal sealed class CardEditorTempFocusTrackerPower : CardEditorTempStatTrackerPower<FocusPower>
{
}

internal sealed class CardEditorTempWeakTrackerPower : CardEditorTempStatTrackerPower<WeakPower>
{
}

internal sealed class CardEditorTempFrailTrackerPower : CardEditorTempStatTrackerPower<FrailPower>
{
}

internal sealed class CardEditorTempVulnerableTrackerPower : CardEditorTempStatTrackerPower<VulnerablePower>
{
}

internal sealed class CardEditorTempPoisonTrackerPower : CardEditorTempStatTrackerPower<PoisonPower>
{
}

internal sealed class CardEditorTempDoomTrackerPower : CardEditorTempStatTrackerPower<DoomPower>
{
}

internal sealed class CardEditorTempArtifactTrackerPower : CardEditorTempStatTrackerPower<ArtifactPower>
{
}

internal sealed class CardEditorTempThornsTrackerPower : CardEditorTempStatTrackerPower<ThornsPower>
{
}

internal sealed class CardEditorTempRegenTrackerPower : CardEditorTempStatTrackerPower<RegenPower>
{
}

internal sealed class CardEditorTempPlatingTrackerPower : CardEditorTempStatTrackerPower<PlatingPower>
{
}

internal sealed class CardEditorTempIntangibleTrackerPower : CardEditorTempStatTrackerPower<IntangiblePower>
{
}

internal sealed class CardEditorTempBufferTrackerPower : CardEditorTempStatTrackerPower<BufferPower>
{
}

internal sealed class CardEditorTempVigorTrackerPower : CardEditorTempStatTrackerPower<VigorPower>
{
}

internal sealed class CardEditorTempBlurTrackerPower : CardEditorTempStatTrackerPower<BlurPower>
{
}

internal sealed class CardEditorTempRitualTrackerPower : CardEditorTempStatTrackerPower<RitualPower>
{
}

internal sealed class CardEditorTempConstrictTrackerPower : CardEditorTempStatTrackerPower<ConstrictPower>
{
}

internal sealed class CardEditorPowerDurationTrackerEntry
{
	public string PowerId { get; set; } = string.Empty;
	public int AmountDelta { get; set; }
	public bool TargetHadPowerBefore { get; set; }
	public CardExtraEffectDuration Duration { get; set; } = CardExtraEffectDuration.Permanent;
	public int AppliedRoundNumber { get; set; }
	public CombatSide AppliedSide { get; set; } = CombatSide.None;
}

internal sealed class CardEditorPowerDurationTrackerPower : PowerModel
{
	public List<CardEditorPowerDurationTrackerEntry> Entries { get; set; } = new();

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public void Track(string? powerId, int amountDelta, bool targetHadPowerBefore, CardExtraEffectDuration duration, CombatState? combatState)
	{
		if (string.IsNullOrWhiteSpace(powerId)
			|| amountDelta == 0
			|| !CardEditorExtraEffects.IsTemporaryDuration(duration))
		{
			return;
		}

		string normalizedPowerId = powerId.Trim();
		CardEditorPowerDurationTrackerEntry? existing = Entries.FirstOrDefault(entry =>
			entry != null
			&& string.Equals(entry.PowerId, normalizedPowerId, StringComparison.Ordinal)
			&& entry.TargetHadPowerBefore == targetHadPowerBefore
			&& entry.Duration == duration
			&& entry.AppliedRoundNumber == (combatState?.RoundNumber ?? 0)
			&& entry.AppliedSide == (combatState?.CurrentSide ?? CombatSide.None));

		if (existing != null)
		{
			existing.AmountDelta += amountDelta;
			return;
		}

		Entries.Add(new CardEditorPowerDurationTrackerEntry
		{
			PowerId = normalizedPowerId,
			AmountDelta = amountDelta,
			TargetHadPowerBefore = targetHadPowerBefore,
			Duration = duration,
			AppliedRoundNumber = combatState?.RoundNumber ?? 0,
			AppliedSide = combatState?.CurrentSide ?? CombatSide.None
		});
	}

	internal static async Task RunDurationBoundary(CombatState? combatState, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
	{
		if (combatState == null || side == CardExtraEffectTurnBoundarySide.Both)
		{
			return;
		}

		await CardEditorPowerPersistenceTrackerPower.RunDurationBoundary(combatState, boundary, side);

		foreach (Creature creature in SnapshotCreatures(combatState))
		{
			if (creature == null)
			{
				continue;
			}

			List<CardEditorPowerDurationTrackerPower> trackers = creature.Powers?
				.OfType<CardEditorPowerDurationTrackerPower>()
				.ToList() ?? new List<CardEditorPowerDurationTrackerPower>();

			foreach (CardEditorPowerDurationTrackerPower tracker in trackers)
			{
				await tracker.TryExpire(boundary, side, combatState);
			}
		}
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

	private async Task TryExpire(CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side, CombatState combatState)
	{
		Creature? owner = Owner;
		if (owner == null || Entries.Count == 0)
		{
			return;
		}

		List<CardEditorPowerDurationTrackerEntry> expired = Entries
			.Where(entry => ShouldExpire(entry, owner, boundary, side, combatState))
			.ToList();
		if (expired.Count == 0)
		{
			return;
		}

		foreach (CardEditorPowerDurationTrackerEntry entry in expired)
		{
			try
			{
				await RevertEntry(owner, entry);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Temporary power duration cleanup failed for {entry?.PowerId}: {ex}");
			}
			Entries.Remove(entry);
		}

		if (Entries.Count == 0)
		{
			await PowerCmd.Remove(this);
		}
	}

	private static bool ShouldExpire(CardEditorPowerDurationTrackerEntry? entry, Creature owner, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side, CombatState combatState)
	{
		if (entry == null || owner == null || side == CardExtraEffectTurnBoundarySide.Both)
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

	private static async Task RevertEntry(Creature owner, CardEditorPowerDurationTrackerEntry entry)
	{
		if (owner == null || entry == null || entry.AmountDelta == 0)
		{
			return;
		}

		if (CardEditorCustomStatusRegistry.IsCustomStatusId(entry.PowerId))
		{
			await RevertCustomStatus(owner, entry);
			return;
		}

		if (!TryParsePowerId(entry.PowerId, out ModelId powerId))
		{
			return;
		}

		PowerModel? active = GetActivePower(owner, powerId);
		int revertDelta = -entry.AmountDelta;
		if (active == null)
		{
			if (revertDelta <= 0)
			{
				return;
			}
			PowerModel? canonical = ModelDb.GetByIdOrNull<PowerModel>(powerId);
			if (canonical != null)
			{
				await PowerCmd.Apply(canonical.ToMutable(), owner, revertDelta, owner, null);
			}
			return;
		}

		if (active.StackType == PowerStackType.Single)
		{
			if (!entry.TargetHadPowerBefore && entry.AmountDelta > 0)
			{
				await PowerCmd.Remove(active);
			}
			return;
		}

		if (active is ITemporaryPower)
		{
			CardEditorTemporaryPowerCompat.IgnoreNextInternalApplication(active);
		}

		if (entry.AmountDelta > 0 && active.Amount <= entry.AmountDelta)
		{
			await PowerCmd.Remove(active);
			return;
		}

		await PowerCmd.ModifyAmount(active, revertDelta, owner, null);
	}

	private static async Task RevertCustomStatus(Creature owner, CardEditorPowerDurationTrackerEntry entry)
	{
		CardEditorCustomStatusPower? active = owner.Powers?
			.OfType<CardEditorCustomStatusPower>()
			.FirstOrDefault(power => power != null && power.MatchesConfiguredId(entry.PowerId));
		if (active == null)
		{
			return;
		}

		if (entry.AmountDelta > 0 && active.Amount <= entry.AmountDelta)
		{
			await PowerCmd.Remove(active);
			return;
		}

		await PowerCmd.ModifyAmount(active, -entry.AmountDelta, owner, null);
	}

	private static bool TryParsePowerId(string? powerIdText, out ModelId powerId)
	{
		powerId = ModelId.none;
		if (string.IsNullOrWhiteSpace(powerIdText))
		{
			return false;
		}

		try
		{
			powerId = ModelId.Deserialize(powerIdText.Trim());
			return powerId != ModelId.none;
		}
		catch
		{
			return false;
		}
	}

	private static PowerModel? GetActivePower(Creature owner, ModelId powerId)
	{
		try
		{
			return owner.GetPowerById(powerId);
		}
		catch
		{
			return owner.Powers?.FirstOrDefault(power => power != null && Equals(power.Id, powerId));
		}
	}
}
