using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorExtraEffectPower : PowerModel
{
	private sealed class PowerEffectEntry
	{
		public required long EntryId { get; init; }
		public required CardModel SourceCard { get; set; }
		public required CardExtraEffect Effect { get; set; }
		public required CardExtraEffect MergeTemplate { get; set; }
		public Dictionary<string, List<CardModel>> SelectedCardsByEffectId { get; init; } = new(StringComparer.Ordinal);
		public string? CustomStatusBehaviorId { get; set; }
		public string? CustomStatusBehaviorKey { get; set; }
		public Creature? RememberedTarget { get; set; }
		public int StackCount { get; set; } = 1;
		public int TriggerCounter { get; set; }
		public int TriggerFireCount { get; set; }
		public int TurnCounter { get; set; }
		public bool AutoPlayLoopLimitMerged { get; set; }
		public string? LastTurnBoundaryExecutionKey { get; set; }
	}

	private static long _nextEntryId;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	protected override object? InitInternalData()
	{
		return new List<PowerEffectEntry>();
	}

	private List<PowerEffectEntry> Entries => GetInternalData<List<PowerEffectEntry>>();

	protected override void DeepCloneFields()
	{
		List<PowerEffectEntry>? snapshot = null;
		try
		{
			if (Entries.Count > 0)
			{
				snapshot = Entries
					.Where(e => e != null && e.SourceCard != null && e.Effect != null)
					.Select(e => new PowerEffectEntry
					{
						EntryId = e.EntryId,
						SourceCard = e.SourceCard,
						RememberedTarget = e.RememberedTarget,
						StackCount = Math.Max(1, e.StackCount),
						TriggerCounter = e.TriggerCounter,
						TriggerFireCount = e.TriggerFireCount,
						TurnCounter = e.TurnCounter,
						AutoPlayLoopLimitMerged = e.AutoPlayLoopLimitMerged,
						LastTurnBoundaryExecutionKey = e.LastTurnBoundaryExecutionKey,
						Effect = CardEditorExtraEffects.CloneEffect(e.Effect),
						MergeTemplate = CardEditorExtraEffects.CloneEffect(e.MergeTemplate ?? e.Effect),
						SelectedCardsByEffectId = CardEditorEffectExecutionAmountContext.CloneSelectedCardsSnapshot(e.SelectedCardsByEffectId, cloneCards: true),
						CustomStatusBehaviorId = e.CustomStatusBehaviorId,
						CustomStatusBehaviorKey = e.CustomStatusBehaviorKey,
					})
					.ToList();
			}
		}
		catch
		{
			snapshot = null;
		}

		base.DeepCloneFields();

		if (snapshot != null)
		{
			Entries.Clear();
			Entries.AddRange(snapshot);
		}
	}

	public bool HasOnPlayTargetEffects()
	{
		try
		{
			return Entries.Any(e => e?.Effect != null
				&& e.Effect.Amount > 0
				&& !e.Effect.GrantToCard
				&& e.Effect.AsPower
				&& e.Effect.Trigger == CardExtraEffectTrigger.OnPlay
				&& e.Effect.Target == CardExtraEffectTarget.Target
				&& e.Effect.PowerTargeting == CardExtraEffectPowerTargeting.TriggerTarget
				&& (e.Effect.Kind != CardExtraEffectKind.OstyAction
					|| e.Effect.OstyAction == CardExtraEffectOstyAction.Attack));
		}
		catch
		{
			return false;
		}
	}

	public async Task AddPowerEffects(CardModel sourceCard, IReadOnlyList<CardExtraEffect> effects, CardPlay? sourcePlay = null)
	{
		AssertMutable();
		if (sourceCard == null || effects == null || effects.Count == 0)
		{
			return;
		}

		foreach (CardExtraEffect effect in effects)
		{
			CardExtraEffect? normalizedEffect = CardEditorExtraEffects.NormalizeSignedEffectAmount(effect);
			if (normalizedEffect == null
				|| !normalizedEffect.AsPower
				|| !CardEditorExtraEffects.SupportsAsPower(normalizedEffect.Kind))
			{
				continue;
			}

			CardExtraEffect stored = sourcePlay != null
				? CardEditorExtraEffects.CloneForDeferredExecution(sourcePlay, normalizedEffect)
				: CardEditorExtraEffects.CloneEffect(normalizedEffect);
			if (!CardEditorExtraEffects.IsValidEffectAmount(stored.Kind, stored.Amount))
			{
				continue;
			}
			if (stored.RepeatCount < 0)
			{
				continue;
			}
			stored.AsPower = true;
			Dictionary<string, List<CardModel>> selectedCardsByEffectId = CardEditorEffectExecutionAmountContext.CaptureCurrentSelectedCards(cloneCards: true);

			PowerEffectEntry? existingEntry = FindMergeTarget(sourceCard, stored, selectedCardsByEffectId);
			if (existingEntry != null)
			{
				MergeIntoEntry(existingEntry, stored);
				continue;
			}

			Entries.Add(new PowerEffectEntry
			{
				EntryId = Interlocked.Increment(ref _nextEntryId),
				SourceCard = sourceCard,
				Effect = stored,
				MergeTemplate = CardEditorExtraEffects.CloneEffect(stored),
				SelectedCardsByEffectId = selectedCardsByEffectId,
				StackCount = 1
			});
		}

		CoalesceMergeEntries();
		await SyncVisibleMirrorPowers();
	}

	public async Task AddCustomStatusBehaviorEffects(CardModel sourceCard, string? customStatusId, IReadOnlyList<CardExtraEffect> effects)
	{
		AssertMutable();
		string normalizedStatusId = customStatusId?.Trim() ?? string.Empty;
		if (sourceCard == null
			|| string.IsNullOrWhiteSpace(normalizedStatusId)
			|| effects == null
			|| effects.Count == 0)
		{
			return;
		}

		HashSet<string> desiredKeys = new(StringComparer.Ordinal);
		for (int i = 0; i < effects.Count; i++)
		{
			CardExtraEffect? normalizedEffect = CardEditorExtraEffects.NormalizeSignedEffectAmount(effects[i]);
			if (normalizedEffect == null
				|| !normalizedEffect.AsPower
				|| !CardEditorExtraEffects.SupportsAsPower(normalizedEffect.Kind))
			{
				continue;
			}

			CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(normalizedEffect);
			if (!CardEditorExtraEffects.IsValidEffectAmount(stored.Kind, stored.Amount) || stored.RepeatCount < 0)
			{
				continue;
			}

			stored.AsPower = true;
			string behaviorKey = !string.IsNullOrWhiteSpace(stored.EffectId)
				? stored.EffectId.Trim()
				: $"{normalizedStatusId}:{i}";
			desiredKeys.Add(behaviorKey);

			PowerEffectEntry? existingEntry = Entries.FirstOrDefault(entry =>
				entry != null
				&& string.Equals(entry.CustomStatusBehaviorId ?? string.Empty, normalizedStatusId, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(entry.CustomStatusBehaviorKey ?? string.Empty, behaviorKey, StringComparison.Ordinal));
			if (existingEntry != null)
			{
				existingEntry.SourceCard = sourceCard;
				existingEntry.Effect = stored;
				existingEntry.MergeTemplate = CardEditorExtraEffects.CloneEffect(stored);
				existingEntry.StackCount = 1;
				existingEntry.LastTurnBoundaryExecutionKey = null;
				continue;
			}

			Entries.Add(new PowerEffectEntry
			{
				EntryId = Interlocked.Increment(ref _nextEntryId),
				SourceCard = sourceCard,
				Effect = stored,
				MergeTemplate = CardEditorExtraEffects.CloneEffect(stored),
				CustomStatusBehaviorId = normalizedStatusId,
				CustomStatusBehaviorKey = behaviorKey,
				StackCount = 1
			});
		}

		Entries.RemoveAll(entry =>
			entry != null
			&& string.Equals(entry.CustomStatusBehaviorId ?? string.Empty, normalizedStatusId, StringComparison.OrdinalIgnoreCase)
			&& !string.IsNullOrWhiteSpace(entry.CustomStatusBehaviorKey)
			&& !desiredKeys.Contains(entry.CustomStatusBehaviorKey));

		await SyncVisibleMirrorPowers();
	}

	private PowerEffectEntry? FindMergeTarget(CardModel sourceCard, CardExtraEffect stored, IReadOnlyDictionary<string, List<CardModel>> selectedCardsByEffectId)
	{
		if (sourceCard == null
			|| stored == null
			|| stored.PowerStackMode != CardExtraEffectPowerStackMode.Merge)
		{
			return null;
		}

		return Entries.FirstOrDefault(entry => CanMergeIntoEntry(entry, sourceCard, stored, selectedCardsByEffectId));
	}

	private static bool CanMergeIntoEntry(PowerEffectEntry? entry, CardModel sourceCard, CardExtraEffect stored, IReadOnlyDictionary<string, List<CardModel>> selectedCardsByEffectId)
	{
		if (entry == null
			|| entry.SourceCard == null
			|| entry.Effect == null
			|| entry.MergeTemplate == null
			|| sourceCard == null
			|| stored == null
			|| entry.Effect.PowerStackMode != CardExtraEffectPowerStackMode.Merge
			|| stored.PowerStackMode != CardExtraEffectPowerStackMode.Merge
			|| !CardEditorExtraEffects.PowerEffectEntriesMatch(entry.MergeTemplate, stored))
		{
			return false;
		}

		if (HasSelectedCardSnapshots(entry.SelectedCardsByEffectId) || HasSelectedCardSnapshots(selectedCardsByEffectId))
		{
			return false;
		}

		return true;
	}

	private void CoalesceMergeEntries()
	{
		for (int i = 0; i < Entries.Count; i++)
		{
			PowerEffectEntry target = Entries[i];
			if (target == null
				|| IsCustomStatusBehaviorEntry(target)
				|| target.SourceCard == null
				|| target.Effect == null
				|| target.Effect.PowerStackMode != CardExtraEffectPowerStackMode.Merge)
			{
				continue;
			}

			for (int j = i + 1; j < Entries.Count; j++)
			{
				PowerEffectEntry candidate = Entries[j];
				if (candidate == null
					|| IsCustomStatusBehaviorEntry(candidate)
					|| candidate.SourceCard == null
					|| candidate.Effect == null
					|| !CanMergeIntoEntry(target, candidate.SourceCard, candidate.Effect, candidate.SelectedCardsByEffectId))
				{
					continue;
				}

				MergeIntoEntry(target, candidate.Effect);
				Entries.RemoveAt(j);
				j--;
			}
		}
	}

	private static bool HasSelectedCardSnapshots(IReadOnlyDictionary<string, List<CardModel>>? selectedCardsByEffectId)
	{
		return selectedCardsByEffectId != null
			&& selectedCardsByEffectId.Values.Any(cards => cards != null && cards.Count > 0);
	}

	private static void MergeIntoEntry(PowerEffectEntry entry, CardExtraEffect incoming)
	{
		MergeAutoPlayLoopLimit(entry, incoming.AutoPlayLoopLimit);
		entry.StackCount = Math.Clamp(entry.StackCount + 1, 1, 999);
		MergeTriggerMaxTurns(entry, incoming.TriggerMaxTurns);
		MergeTriggerMaxFires(entry, incoming.TriggerMaxFires);
	}

	private static void MergeAutoPlayLoopLimit(PowerEffectEntry entry, int incomingLimit)
	{
		if (entry?.Effect == null)
		{
			return;
		}
		if (entry.Effect.AutoPlayLoopLimit <= 0 || incomingLimit <= 0)
		{
			entry.Effect.AutoPlayLoopLimit = 0;
			entry.AutoPlayLoopLimitMerged = true;
			return;
		}

		EnsureAutoPlayLoopLimitMerged(entry);
		entry.Effect.AutoPlayLoopLimit = Math.Clamp(entry.Effect.AutoPlayLoopLimit + incomingLimit, 1, 999);
	}

	private static void EnsureAutoPlayLoopLimitMerged(PowerEffectEntry entry)
	{
		if (entry?.Effect == null
			|| entry.AutoPlayLoopLimitMerged
			|| entry.Effect.AutoPlayLoopLimit <= 0)
		{
			return;
		}

		int stackCount = GetStackCount(entry);
		if (stackCount > 1)
		{
			entry.Effect.AutoPlayLoopLimit = Math.Clamp(entry.Effect.AutoPlayLoopLimit * stackCount, 1, 999);
		}

		entry.AutoPlayLoopLimitMerged = true;
	}

	private static void MergeTriggerMaxTurns(PowerEffectEntry entry, int incomingMaxTurns)
	{
		if (entry?.Effect == null)
		{
			return;
		}
		if (entry.Effect.TriggerMaxTurns <= 0 || incomingMaxTurns <= 0)
		{
			entry.Effect.TriggerMaxTurns = 0;
			entry.TurnCounter = 0;
			return;
		}

		int remaining = Math.Max(0, entry.Effect.TriggerMaxTurns - entry.TurnCounter);
		entry.Effect.TriggerMaxTurns = Math.Clamp(remaining + incomingMaxTurns, 1, 999);
		entry.TurnCounter = 0;
	}

	private static void MergeTriggerMaxFires(PowerEffectEntry entry, int incomingMaxFires)
	{
		if (entry?.Effect == null)
		{
			return;
		}
		if (entry.Effect.TriggerMaxFires <= 0 || incomingMaxFires <= 0)
		{
			entry.Effect.TriggerMaxFires = 0;
			entry.TriggerFireCount = 0;
			return;
		}

		int remaining = Math.Max(0, entry.Effect.TriggerMaxFires - entry.TriggerFireCount);
		entry.Effect.TriggerMaxFires = Math.Clamp(remaining + incomingMaxFires, 1, 999);
		entry.TriggerFireCount = 0;
	}

	private static string GetCardIdText(CardModel? card)
	{
		try
		{
			return card?.Id.ToString() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static int GetStackCount(PowerEffectEntry? entry)
	{
		return Math.Clamp(entry?.StackCount ?? 1, 1, 999);
	}

	private static CardExtraEffect BuildStackedRuntimeEffect(PowerEffectEntry entry)
	{
		CardExtraEffect effect = CardEditorExtraEffects.CloneEffect(entry.Effect);
		int stackCount = GetStackCount(entry);
		if (effect.AutoPlayLoopLimit > 0 && stackCount > 1 && !entry.AutoPlayLoopLimitMerged)
		{
			effect.AutoPlayLoopLimit = Math.Clamp(effect.AutoPlayLoopLimit * stackCount, 1, 999);
		}
		return effect;
	}

	private static int GetEffectiveTriggerMaxFires(PowerEffectEntry entry)
	{
		if (entry?.Effect == null || entry.Effect.TriggerMaxFires <= 0)
		{
			return 0;
		}
		return Math.Clamp(entry.Effect.TriggerMaxFires, 1, 999);
	}

	private static (int VisibleAmount, bool ShowAmountLabel) GetMirrorDisplayState(PowerEffectEntry entry)
	{
		if (entry?.Effect == null)
		{
			return (0, false);
		}

		if (entry.Effect.AutoPlayLoopLimit > 0)
		{
			CardExtraEffect runtimeEffect = BuildStackedRuntimeEffect(entry);
			CombatState? combatState = entry.SourceCard.GetConcreteCombatState()
				?? entry.SourceCard?.Owner?.Creature.GetConcreteCombatState();
			int remaining = CardEditorAutoPlayLoopGuard.GetRemainingEffectUses(
				combatState,
				entry.SourceCard?.Owner,
				entry.SourceCard,
				runtimeEffect,
				CardEditorAutoPlayLoopGuard.BuildPowerUseLimitInstanceKey(entry.EntryId));
			if (remaining >= 0)
			{
				return (remaining, true);
			}
		}

		if (entry.Effect.TriggerMaxTurns > 0)
		{
			return (Math.Max(0, entry.Effect.TriggerMaxTurns - entry.TurnCounter), true);
		}

		int effectiveMaxFires = GetEffectiveTriggerMaxFires(entry);
		if (effectiveMaxFires > 0)
		{
			return (Math.Max(0, effectiveMaxFires - entry.TriggerFireCount), true);
		}

		if (GetStackCount(entry) > 1)
		{
			return (GetStackCount(entry), true);
		}

		return (0, false);
	}

	private static bool IsCustomStatusBehaviorEntry(PowerEffectEntry? entry)
		=> !string.IsNullOrWhiteSpace(entry?.CustomStatusBehaviorId);

	public async Task SyncVisibleMirrorPowers()
	{
		try
		{
			CoalesceMergeEntries();
			Creature? owner = Owner;
			if (owner == null)
			{
				return;
			}

			List<CardEditorVisibleExtraEffectPower> existingMirrors = owner.Powers
				.OfType<CardEditorVisibleExtraEffectPower>()
				.Where(power => power != null)
				.ToList();
			HashSet<long> desiredEntryIds = new HashSet<long>(Entries
				.Where(entry => !IsCustomStatusBehaviorEntry(entry))
				.Select(entry => entry.EntryId));

			foreach (PowerEffectEntry entry in Entries)
			{
				if (entry?.SourceCard == null || entry.Effect == null)
				{
					continue;
				}
				if (IsCustomStatusBehaviorEntry(entry))
				{
					continue;
				}

				(int visibleAmount, bool showAmountLabel) = GetMirrorDisplayState(entry);
				CardExtraEffect visibleEffect = BuildStackedRuntimeEffect(entry);
				CardEditorVisibleExtraEffectPower? mirror = existingMirrors.FirstOrDefault(power => power.EntryId == entry.EntryId);
				if (mirror == null)
				{
					using IDisposable _ = CardEditorVisibleExtraEffectPower.PushPendingPayload(
						entry.EntryId,
						entry.SourceCard,
						visibleEffect,
						visibleAmount,
						showAmountLabel);
					mirror = await PowerCmd.Apply<CardEditorVisibleExtraEffectPower>(owner, 1, owner, entry.SourceCard, silent: true);
				}

				if (mirror != null)
				{
					mirror.SyncFromEntry(entry.EntryId, entry.SourceCard, visibleEffect, visibleAmount, showAmountLabel);
					CardEditorPowerSourceMap.Register(mirror, entry.SourceCard);
				}
			}

			foreach (CardEditorVisibleExtraEffectPower staleMirror in existingMirrors)
			{
				if (staleMirror != null && !desiredEntryIds.Contains(staleMirror.EntryId))
				{
					await PowerCmd.Remove(staleMirror);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed syncing visible extra effect powers: {ex}");
		}
	}

	private static bool IsValidEnemyTarget(Creature? creature, Creature? owner)
	{
		return creature != null
			&& creature.IsAlive
			&& owner != null
			&& !ReferenceEquals(creature, owner)
			&& owner.GetConcreteCombatState()?.GetOpponentsOf(owner).Contains(creature) == true;
	}

	private Creature? ResolveRememberedEnemyTarget(PowerEffectEntry entry, CombatState combatState, CardPlay triggerPlay)
	{
		Creature? owner = Owner;
		if (owner == null)
		{
			return null;
		}

		Creature? currentTarget = triggerPlay.Target;
		bool hasValidCurrentEnemy = IsValidEnemyTarget(currentTarget, owner);
		Creature? remembered = IsValidEnemyTarget(entry.RememberedTarget, owner) ? entry.RememberedTarget : null;
		IEnumerable<Creature> livingOpponents = combatState.GetOpponentsOf(owner).Where(c => c.IsAlive);

		switch (entry.Effect.PowerTargeting)
		{
			case CardExtraEffectPowerTargeting.RememberFirstEnemy:
				if (remembered != null)
				{
					return remembered;
				}
				if (hasValidCurrentEnemy)
				{
					entry.RememberedTarget = currentTarget;
					return currentTarget;
				}
				return null;
			case CardExtraEffectPowerTargeting.RememberLastEnemy:
				if (hasValidCurrentEnemy)
				{
					entry.RememberedTarget = currentTarget;
					return currentTarget;
				}
				return remembered;
			case CardExtraEffectPowerTargeting.RememberEnemyRandomFallback:
				if (hasValidCurrentEnemy)
				{
					entry.RememberedTarget = currentTarget;
					return currentTarget;
				}
				if (remembered != null)
				{
					return remembered;
				}
				return combatState.RunState.Rng.CombatTargets.NextItem(livingOpponents);
			case CardExtraEffectPowerTargeting.RandomEnemy:
				return combatState.RunState.Rng.CombatTargets.NextItem(livingOpponents);
			default:
				return hasValidCurrentEnemy ? currentTarget : remembered;
		}
	}

	private static Creature? GetEventActor(CardModel? triggeringCard, CardPlay? triggeringPlay = null)
	{
		return triggeringPlay?.Card?.Owner?.Creature ?? triggeringCard?.Owner?.Creature;
	}

	private bool WatchesEventActor(CardExtraEffect effect, Creature? eventActor)
	{
		Creature? owner = Owner;
		if (owner == null || effect == null)
		{
			return false;
		}

		Creature effectiveEventActor = eventActor ?? owner;
		return CardEditorExtraEffects.GetEffectivePowerTriggerFrom(effect) switch
		{
			CardExtraEffectPowerTriggerFrom.Self => ReferenceEquals(effectiveEventActor, owner),
			CardExtraEffectPowerTriggerFrom.AnyEnemy => owner.GetConcreteCombatState()?.GetOpponentsOf(owner).Contains(effectiveEventActor) == true,
			CardExtraEffectPowerTriggerFrom.AnyAlly => effectiveEventActor.Side == owner.Side && !ReferenceEquals(effectiveEventActor, owner),
			CardExtraEffectPowerTriggerFrom.Anyone => true,
			_ => ReferenceEquals(effectiveEventActor, owner)
		};
	}

	private CardExtraEffect BuildResolvedPowerEffect(PowerEffectEntry entry)
	{
		CardExtraEffect resolved = BuildStackedRuntimeEffect(entry);
		if (resolved.Target == CardExtraEffectTarget.Target && resolved.PowerTargeting == CardExtraEffectPowerTargeting.AllEnemies)
		{
			resolved.Target = CardExtraEffectTarget.AllEnemies;
		}

		return resolved;
	}

	private async Task<int> ExecuteOrSchedulePowerEffect(
		CombatState combatState,
		PlayerChoiceContext choiceContext,
		CardPlay triggerPlay,
		PowerEffectEntry entry,
		int triggerEventAmount = 1)
	{
		if (entry == null || entry.Effect == null || entry.SourceCard == null || triggerPlay == null)
		{
			return 0;
		}

		using IDisposable selectedSession = CardEditorEffectExecutionAmountContext.PushSessionScoped();
		using IDisposable selectedScope = CardEditorEffectExecutionAmountContext.PushSelectedCardsScoped(entry.SelectedCardsByEffectId);

		CardExtraEffect effect = entry.Effect;
		CardModel sourceCard = entry.SourceCard;
		if (CardEditorAutoPlayLoopGuard.ShouldSuppressEffect(sourceCard, effect))
		{
			return 0;
		}

		string useLimitSourceInstance = CardEditorAutoPlayLoopGuard.BuildPowerUseLimitInstanceKey(entry.EntryId);
		CardExtraEffect resolvedEffect = BuildResolvedPowerEffect(entry);
		int remainingUses = CardEditorAutoPlayLoopGuard.GetRemainingEffectUses(
			combatState,
			sourceCard.Owner ?? Owner?.Player,
			sourceCard,
			resolvedEffect,
			useLimitSourceInstance);
		if (remainingUses == 0)
		{
			return 0;
		}

		Creature? lockedTarget = null;
		if (resolvedEffect.Target == CardExtraEffectTarget.Target)
		{
			lockedTarget = resolvedEffect.PowerTargeting == CardExtraEffectPowerTargeting.TriggerTarget
				? triggerPlay.Target
				: ResolveRememberedEnemyTarget(entry, combatState, triggerPlay);
			if (lockedTarget == null)
			{
				return 0;
			}
		}

		int stackCount = GetStackCount(entry);
		int runCount = remainingUses >= 0 ? Math.Min(stackCount, remainingUses) : stackCount;
		if (runCount <= 0)
		{
			return 0;
		}

		if (resolvedEffect.Timing == CardExtraEffectTiming.Immediate)
		{
			int executed = 0;
			for (int i = 0; i < runCount; i++)
			{
				if (CardEditorAutoPlayLoopGuard.GetRemainingEffectUses(
					combatState,
					sourceCard.Owner ?? Owner?.Player,
					sourceCard,
					resolvedEffect,
					useLimitSourceInstance) == 0)
				{
					break;
				}

				if (!CardEditorAutoPlayLoopGuard.TryEnterAutoPlayEffect(
					combatState,
					sourceCard.Owner ?? Owner?.Player,
					sourceCard,
					resolvedEffect,
					out IDisposable autoScope,
					markPrecountedNestedBatch: true))
				{
					break;
				}

				CardModel executionCard = CardEditorExtraEffects.ResolveImmediatePowerExecutionCard(sourceCard, triggerPlay, resolvedEffect);
				Creature? executionTarget = resolvedEffect.Target == CardExtraEffectTarget.Target
					? lockedTarget
					: triggerPlay.Target;
				CardPlay executionPlay = triggerPlay;
				if (!ReferenceEquals(triggerPlay.Card, executionCard)
					|| !ReferenceEquals(triggerPlay.Target, executionTarget))
				{
					executionPlay = new CardPlay
					{
						Card = executionCard,
						Target = executionTarget,
						ResultPile = triggerPlay.ResultPile,
						Resources = triggerPlay.Resources,
						IsAutoPlay = triggerPlay.IsAutoPlay,
						PlayIndex = triggerPlay.PlayIndex,
						PlayCount = triggerPlay.PlayCount
					};
				}

				CardEditorPowerSourceMap.Register(this, sourceCard);
				using IDisposable ___ = autoScope;
				using IDisposable _ = CardEditorEffectSourceContext.PushScoped(sourceCard);
				using IDisposable __ = CardEditorPowerExecutionHostContext.PushScoped(Owner);
				using IDisposable ____ = CardEditorAutoPlayLoopGuard.PushUseLimitSourceInstance(useLimitSourceInstance);
				await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, executionPlay, resolvedEffect, triggerEventAmount);
				executed++;
			}

			return executed;
		}

		CardPlay schedulingPlay = new CardPlay
		{
			Card = sourceCard,
			Target = lockedTarget,
			ResultPile = triggerPlay.ResultPile,
			Resources = triggerPlay.Resources,
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};

		for (int i = 0; i < runCount; i++)
		{
			CardEditorExtraEffectScheduler.Schedule(
				combatState,
				schedulingPlay,
				resolvedEffect,
				lockedTarget,
				Owner,
				triggerEventAmount,
				useLimitSourceInstance);
		}
		return runCount;
	}

	private void TrackEntryFireCount(PowerEffectEntry entry, int runCount)
	{
		if (entry?.Effect == null || runCount <= 0 || entry.Effect.TriggerMaxFires < 1)
		{
			return;
		}

		entry.TriggerFireCount = Math.Clamp(entry.TriggerFireCount + runCount, 0, 999);
		if (entry.TriggerFireCount >= GetEffectiveTriggerMaxFires(entry))
		{
			Entries.Remove(entry);
		}
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		Creature? eventActor = GetEventActor(cardPlay?.Card, cardPlay);
		await RunTrigger(context, cardPlay?.Card, cardPlay, CardExtraEffectTrigger.OnPlay, eventActor);
		await TriggerCountEvent(context, CardExtraEffectCountEvent.Played, triggeringPlay: cardPlay, triggeringCard: cardPlay?.Card, eventActor: eventActor, amount: 1);
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		Creature? eventActor = GetEventActor(card);
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnDraw, eventActor);
		await TriggerCountEvent(choiceContext, CardExtraEffectCountEvent.Drawn, triggeringCard: card, eventActor: eventActor, amount: 1);
	}

	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		Creature? eventActor = GetEventActor(card);
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnDiscard, eventActor);
		await TriggerCountEvent(choiceContext, CardExtraEffectCountEvent.Discarded, triggeringCard: card, eventActor: eventActor, amount: 1);
	}

	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		Creature? eventActor = GetEventActor(card);
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnExhaust, eventActor);
		await TriggerCountEvent(choiceContext, CardExtraEffectCountEvent.Exhausted, triggeringCard: card, eventActor: eventActor, amount: 1);
	}

	public override async Task AfterOrbChanneled(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
	{
		Creature? eventActor = player?.Creature;
		await RunOrbTrigger(choiceContext, CardExtraEffectTrigger.OnChannel, eventActor);
		await TriggerCountEvent(choiceContext, CardExtraEffectCountEvent.OrbChanneled, eventActor: eventActor, amount: 1);
	}

	public override async Task AfterOrbEvoked(PlayerChoiceContext choiceContext, OrbModel orb, IEnumerable<Creature> targets)
	{
		Creature? eventActor = orb?.Owner?.Creature;
		await RunOrbTrigger(choiceContext, CardExtraEffectTrigger.OnEvoke, eventActor);
		await TriggerCountEvent(choiceContext, CardExtraEffectCountEvent.OrbEvoked, eventActor: eventActor, amount: 1);
	}

	public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		try
		{
			Creature? cardOwner = card.TryGetOwnerCreature();
			if (cardOwner == null || !ReferenceEquals(cardOwner, Owner))
			{
				return Task.CompletedTask;
			}

			CombatState? combatState = this.GetConcreteCombatState();
			Creature? owner = Owner;
			if (combatState == null || owner == null)
			{
				return Task.CompletedTask;
			}

			CardEditorExtraEffects.TriggerPowerCountEvent(combatState, owner, CardExtraEffectCountEvent.Generated, amount: 1);
		}
		catch
		{
			// ignored
		}

		return Task.CompletedTask;
	}

	public async Task TriggerCountEvent(
		PlayerChoiceContext choiceContext,
		CardExtraEffectCountEvent countEvent,
		CardEditorExtraEffects.ResourceCountSource source = CardEditorExtraEffects.ResourceCountSource.Other,
		CardPlay? triggeringPlay = null,
		CardModel? triggeringCard = null,
		PowerModel? triggeringPower = null,
		Creature? eventActor = null,
		int amount = 1)
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState();
			if (combatState == null || choiceContext == null)
			{
				return;
			}

			Creature? owner = Owner;
			if (owner == null)
			{
				return;
			}

			triggeringCard ??= triggeringPlay?.Card;
			eventActor ??= GetEventActor(triggeringCard, triggeringPlay) ?? owner;

			using IDisposable _ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					|| entry.Effect.Trigger != CardExtraEffectTrigger.OnCountEvent
					|| entry.Effect.PowerTriggerCountEvent != countEvent
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (!WatchesEventActor(entry.Effect, eventActor))
				{
					continue;
				}

				if (countEvent == CardExtraEffectCountEvent.BlockLost
					&& source == CardEditorExtraEffects.ResourceCountSource.BetweenTurnsBlockClear
					&& entry.Effect.BlockLostCountingMode != CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns)
				{
					continue;
				}

				Player? filterOwner = entry.SourceCard?.Owner ?? owner.Player;
				if (CardEditorExtraEffects.PowerCountEventUsesCardFilters(countEvent)
					&& !CardEditorExtraEffects.MatchesPowerTriggerCardFilters(filterOwner, triggeringCard, entry.Effect))
				{
					continue;
				}

				if (CardEditorExtraEffects.CountEventUsesEnemyStatus(countEvent))
				{
					if (triggeringPower == null || !CardEditorExtraEffects.PowerMatchesConfiguredStatus(triggeringPower, entry.Effect.PowerTriggerEnemyStatus, entry.Effect.PowerTriggerPowerId))
					{
						continue;
					}
				}

				if (entry.Effect.TriggerEveryN >= 2)
				{
					entry.TriggerCounter++;
					if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
					{
						continue;
					}
				}

				CardPlay? basePlay = triggeringPlay;
				if (basePlay == null)
				{
					CardModel? playCard = triggeringCard ?? entry.SourceCard;
					if (playCard == null)
					{
						continue;
					}
					basePlay = new CardPlay
					{
						Card = playCard,
						Target = eventActor,
						ResultPile = playCard.Pile?.Type ?? PileType.None,
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

				int runCount = 0;
				try
				{
					runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, basePlay, entry, amount);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed (countEvent={countEvent}): {ex}");
				}

				TrackEntryFireCount(entry, runCount);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Count-event power trigger failed ({countEvent}): {ex}");
		}
		finally
		{
			await SyncVisibleMirrorPowers();
		}
	}

	private async Task RunOrbTrigger(PlayerChoiceContext choiceContext, CardExtraEffectTrigger trigger, Creature? eventActor = null)
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState();
			if (combatState == null || choiceContext == null)
			{
				return;
			}

			Creature? owner = Owner;
			if (owner == null || !owner.IsPlayer)
			{
				return;
			}

			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					|| entry.Effect.Trigger != trigger
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (!WatchesEventActor(entry.Effect, eventActor))
				{
					continue;
				}

				if (entry.Effect.TriggerEveryN >= 2)
				{
					entry.TriggerCounter++;
					if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
					{
						continue;
					}
				}

				CardModel sourceCard = entry.SourceCard!;
				Creature? sourceOwner = sourceCard.Owner?.Creature;
				if (sourceOwner == null || !ReferenceEquals(sourceOwner, owner))
				{
					continue;
				}

				CardPlay syntheticPlay = new CardPlay
				{
					Card = sourceCard,
					Target = eventActor,
					ResultPile = sourceCard.Pile?.Type ?? PileType.None,
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
				int runCount = 0;
				try
				{
					runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed ({trigger}): {ex}");
				}

				TrackEntryFireCount(entry, runCount);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] {trigger} power trigger failed: {ex}");
		}
		finally
		{
			await SyncVisibleMirrorPowers();
		}
	}

	private async Task RunLifecycleTriggerWithHookContext(
		CardExtraEffectTrigger trigger,
		CardModel? triggeringCard = null,
		Creature? eventActor = null,
		Creature? target = null)
	{
		ulong? netId = LocalContext.NetId;
		Player? player = Owner?.Player;
		if (!netId.HasValue || player == null)
		{
			return;
		}

		HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(player, netId.Value, GameActionType.Combat);
		Task task = RunLifecycleTrigger(choiceContext, trigger, triggeringCard, eventActor, target);
		bool completed = await choiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
		if (!completed && choiceContext.GameAction != null)
		{
			await choiceContext.GameAction.CompletionTask;
		}
	}

	private async Task RunLifecycleTrigger(
		PlayerChoiceContext choiceContext,
		CardExtraEffectTrigger trigger,
		CardModel? triggeringCard = null,
		Creature? eventActor = null,
		Creature? target = null)
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState();
			if (combatState == null || choiceContext == null)
			{
				return;
			}

			Creature? owner = Owner;
			if (owner == null || !owner.IsPlayer)
			{
				return;
			}

			eventActor ??= owner;
			using IDisposable _ = CardEditorEffectExecutionAmountContext.PushSessionScoped();
			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					|| entry.Effect.Trigger != trigger
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (!WatchesEventActor(entry.Effect, eventActor))
				{
					continue;
				}

				if (triggeringCard != null)
				{
					Player? filterOwner = entry.SourceCard?.Owner ?? owner.Player;
					if (!CardEditorExtraEffects.MatchesPowerTriggerCardFilters(filterOwner, triggeringCard, entry.Effect))
					{
						continue;
					}
				}

				if (entry.Effect.TriggerEveryN >= 2)
				{
					entry.TriggerCounter++;
					if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
					{
						continue;
					}
				}

				CardModel sourceCard = entry.SourceCard;
				if (sourceCard.Owner?.Creature == null || !ReferenceEquals(sourceCard.Owner.Creature, owner))
				{
					continue;
				}

				CardPlay syntheticPlay = new CardPlay
				{
					Card = sourceCard,
					Target = target,
					ResultPile = sourceCard.Pile?.Type ?? PileType.None,
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

				int runCount = 0;
				try
				{
					runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed (lifecycle trigger={trigger}): {ex}");
				}

				TrackEntryFireCount(entry, runCount);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Lifecycle power trigger failed ({trigger}): {ex}");
		}
		finally
		{
			await SyncVisibleMirrorPowers();
		}
	}

	public override async Task AfterCardEnteredCombat(CardModel card)
	{
		Creature? actor = card?.Owner?.Creature;
		await RunLifecycleTriggerWithHookContext(CardExtraEffectTrigger.AfterCardEnteredCombat, card, actor, actor);
	}

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
	{
		await RunLifecycleTrigger(choiceContext, CardExtraEffectTrigger.BeforeHandDraw, eventActor: player?.Creature, target: player?.Creature);
	}

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
	{
		await RunLifecycleTrigger(choiceContext, CardExtraEffectTrigger.AfterDeath, eventActor: creature, target: creature);
	}

	public override async Task AfterCombatEnd(CombatRoom room)
	{
		await RunLifecycleTriggerWithHookContext(CardExtraEffectTrigger.AfterCombatEnd, eventActor: Owner, target: Owner);
	}

	public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		if (choiceContext == null)
		{
			return;
		}

		await RunAfterAttackWithChoiceContext(choiceContext, command);
	}

	private async Task RunAfterAttackWithChoiceContext(PlayerChoiceContext choiceContext, AttackCommand command)
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState();
			if (combatState == null || command == null)
			{
				return;
			}

			Creature? owner = Owner;
			if (owner == null || !owner.IsPlayer)
			{
				return;
			}

			Player? ownerPlayer = owner.Player;
			if (ownerPlayer == null)
			{
				return;
			}

			if (choiceContext != null)
			{
				Creature? attackTarget = CardEditorExtraEffects.FlattenDamageResults(command.GetResultsCompat()).FirstOrDefault()?.Receiver;
				await RunLifecycleTrigger(
					choiceContext,
					CardExtraEffectTrigger.AfterAttack,
					command.ModelSource as CardModel,
					command.Attacker,
					attackTarget);
			}

			Creature? osty = ownerPlayer.Osty;
			if (osty == null || !ReferenceEquals(command.Attacker, osty))
			{
				return;
			}

			if (choiceContext == null)
			{
				return;
			}

			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					|| entry.Effect.Trigger != CardExtraEffectTrigger.OstyDealDamage
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (entry.Effect.TriggerEveryN >= 2)
				{
					entry.TriggerCounter++;
					if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
					{
						continue;
					}
				}

				CardModel sourceCard = entry.SourceCard;
				if (sourceCard.Owner?.Creature == null || !ReferenceEquals(sourceCard.Owner.Creature, owner))
				{
					continue;
				}

				CardPlay syntheticPlay = new CardPlay
				{
					Card = sourceCard,
					Target = null,
					ResultPile = sourceCard.Pile?.Type ?? PileType.None,
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

				int runCount = 0;
				try
				{
					runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed (OstyDealDamage): {ex}");
				}

				TrackEntryFireCount(entry, runCount);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] AfterAttack (OstyDealDamage) failed: {ex}");
		}
		finally
		{
			await SyncVisibleMirrorPowers();
		}
	}

public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
{
	try
	{
		Creature? owner = Owner;
		if (owner == null)
		{
			return;
		}

		if (side == owner.Side)
		{
			await RunTurnBoundary(choiceContext, CardExtraEffectTurnBoundary.EndAfterDiscard, CardExtraEffectTurnBoundarySide.YourTurn);
			await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.EndOfTurnInHand);
			await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.EndOfTurn);

			// Expire temporary power-mode entries for non-status kinds.
			ExpireNonStatusDurationEntries(CardExtraEffectTurnBoundary.EndAfterDiscard, CardExtraEffectTurnBoundarySide.YourTurn);

			// Increment turn counter and expire entries that exceeded their turn duration.
			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry?.Effect == null)
				{
					continue;
				}
				if (entry.Effect.TriggerMaxTurns >= 1)
				{
					entry.TurnCounter++;
					if (entry.TurnCounter >= entry.Effect.TriggerMaxTurns)
					{
						Entries.Remove(entry);
					}
				}
			}
		}
		else if (side == CombatSide.Enemy)
		{
			await RunTurnBoundary(choiceContext, CardExtraEffectTurnBoundary.EndAfterDiscard, CardExtraEffectTurnBoundarySide.EnemyTurn);
			await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.EndOfEnemyTurn);
			ExpireNonStatusDurationEntries(CardExtraEffectTurnBoundary.EndAfterDiscard, CardExtraEffectTurnBoundarySide.EnemyTurn);
		}
	}
	finally
	{
		await SyncVisibleMirrorPowers();
	}
}

public async Task RunStartOfTurn(PlayerChoiceContext choiceContext)
{
	await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.StartOfTurn);
}

public async Task RunStartOfEnemyTurn(PlayerChoiceContext choiceContext)
{
	await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.StartOfEnemyTurn);
}

public async Task RunTurnBoundary(PlayerChoiceContext choiceContext, CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
{
	try
	{
		CombatState? combatState = this.GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}

		foreach (PowerEffectEntry entry in Entries.ToList())
		{
			if (entry == null
				|| entry.Effect == null
				|| entry.SourceCard == null
				|| !CardEditorExtraEffects.DoesTurnBoundaryMatch(entry.Effect, boundary, side, entry.SourceCard)
				|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
			{
				continue;
			}

			if (!TryMarkTurnBoundaryExecution(entry, combatState, boundary, side))
			{
				continue;
			}

			if (entry.Effect.TriggerEveryN >= 2)
			{
				entry.TriggerCounter++;
				if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
				{
					continue;
				}
			}

			CardModel card = entry.SourceCard;
			CardPlay syntheticPlay = new CardPlay
			{
				Card = card,
				Target = null,
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

			int runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
			TrackEntryFireCount(entry, runCount);
		}
		if (boundary != CardExtraEffectTurnBoundary.EndAfterDiscard)
		{
			ExpireNonStatusDurationEntries(boundary, side);
		}
	}
	finally
	{
		await SyncVisibleMirrorPowers();
	}
}

private static bool TryMarkTurnBoundaryExecution(
	PowerEffectEntry entry,
	CombatState combatState,
	CardExtraEffectTurnBoundary boundary,
	CardExtraEffectTurnBoundarySide side)
{
	if (entry == null || combatState == null)
	{
		return false;
	}

	string key = $"{combatState.RoundNumber}|{combatState.CurrentSide}|{boundary}|{side}";
	if (string.Equals(entry.LastTurnBoundaryExecutionKey, key, StringComparison.Ordinal))
	{
		return false;
	}

	entry.LastTurnBoundaryExecutionKey = key;
	return true;
}

private void ExpireNonStatusDurationEntries(CardExtraEffectTurnBoundary boundary, CardExtraEffectTurnBoundarySide side)
{
	Creature? owner = Owner;
	if (owner == null)
	{
		return;
	}

	Entries.RemoveAll(e => e != null
		&& e.Effect != null
		&& !CardEditorExtraEffects.SupportsDuration(e.Effect.Kind)
		&& side == CardExtraEffectTurnBoundarySide.YourTurn
		&& DoesDurationExpireAt(e.Effect.Duration, boundary));
}

private static bool DoesDurationExpireAt(CardExtraEffectDuration duration, CardExtraEffectTurnBoundary boundary)
{
	return duration switch
	{
		CardExtraEffectDuration.ThisTurn => boundary == CardExtraEffectTurnBoundary.EndAfterDiscard,
		CardExtraEffectDuration.NextTurnStartBeforeDraw => boundary == CardExtraEffectTurnBoundary.Start,
		CardExtraEffectDuration.NextTurnStartAfterDraw => boundary == CardExtraEffectTurnBoundary.StartAfterDraw,
		CardExtraEffectDuration.NextTurnEndBeforeDiscard => boundary == CardExtraEffectTurnBoundary.End,
		CardExtraEffectDuration.NextTurnEndAfterDiscard => boundary == CardExtraEffectTurnBoundary.EndAfterDiscard,
		_ => false
	};
}

private async Task RunStartOrEndTimed(PlayerChoiceContext choiceContext, CardExtraEffectTrigger trigger)
{
	try
	{
		CombatState? combatState = this.GetConcreteCombatState();
		if (combatState == null)
		{
			return;
		}

			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					// Unified Turn Boundary powers have dedicated edge-specific runners.
					// Letting them flow through Start/End-of-turn legacy hooks makes
					// "before draw" / "before discard" fire a second time.
					|| entry.Effect.Trigger == CardExtraEffectTrigger.TurnBoundary
					|| !CardEditorExtraEffects.DoesTriggerMatch(entry.Effect, trigger, entry.SourceCard)
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

			if (entry.Effect.TriggerEveryN >= 2)
			{
				entry.TriggerCounter++;
				if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
				{
					continue;
				}
			}

			CardModel card = entry.SourceCard;
			CardPlay syntheticPlay = new CardPlay
			{
				Card = card,
				Target = null,
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

			int runCount = 0;
			try
			{
				runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Power extra effect failed (timed trigger={trigger}): {ex}");
			}

			TrackEntryFireCount(entry, runCount);
		}
	}
	finally
	{
		await SyncVisibleMirrorPowers();
	}
}

	private async Task RunTrigger(PlayerChoiceContext choiceContext, CardModel? triggeringCard, CardPlay? play, CardExtraEffectTrigger trigger, Creature? eventActor = null)
	{
		try
		{
			CombatState? combatState = this.GetConcreteCombatState();
			if (combatState == null || choiceContext == null)
			{
				return;
			}

			if (triggeringCard == null)
			{
				return;
			}

			CardPlay? basePlay = play;
			if (basePlay == null && triggeringCard != null)
			{
				basePlay = new CardPlay
				{
					Card = triggeringCard,
					Target = eventActor,
					ResultPile = triggeringCard.Pile?.Type ?? PileType.None,
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

			if (basePlay == null)
			{
				return;
			}

			foreach (PowerEffectEntry entry in Entries.ToList())
			{
				if (entry == null
					|| entry.Effect == null
					|| entry.SourceCard == null
					|| entry.Effect.Trigger != trigger
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (!WatchesEventActor(entry.Effect, eventActor))
				{
					continue;
				}

				Player? filterOwner = entry.SourceCard?.Owner ?? triggeringCard.Owner;
				if (!CardEditorExtraEffects.MatchesPowerTriggerCardFilters(filterOwner, triggeringCard, entry.Effect))
				{
					continue;
				}

				if (entry.Effect.TriggerEveryN >= 2)
				{
					entry.TriggerCounter++;
					if (entry.TriggerCounter % entry.Effect.TriggerEveryN != 0)
					{
						continue;
					}
				}

				int runCount = await ExecuteOrSchedulePowerEffect(combatState, choiceContext, basePlay, entry);

				TrackEntryFireCount(entry, runCount);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Power extra effects failed (trigger={trigger}): {ex}");
		}
		finally
		{
			await SyncVisibleMirrorPowers();
		}
	}
}
