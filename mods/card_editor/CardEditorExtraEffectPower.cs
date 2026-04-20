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

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorExtraEffectPower : PowerModel
{
	private sealed class PowerEffectEntry
	{
		public required long EntryId { get; init; }
		public required CardModel SourceCard { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public Creature? RememberedTarget { get; set; }
		public int TriggerCounter { get; set; }
		public int TriggerFireCount { get; set; }
		public int TurnCounter { get; set; }
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
						TriggerCounter = e.TriggerCounter,
						TriggerFireCount = e.TriggerFireCount,
						TurnCounter = e.TurnCounter,
						Effect = CardEditorExtraEffects.CloneEffect(e.Effect)
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

	public async Task AddPowerEffects(CardModel sourceCard, IReadOnlyList<CardExtraEffect> effects)
	{
		AssertMutable();
		if (sourceCard == null || effects == null || effects.Count == 0)
		{
			return;
		}

		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null
				|| !effect.AsPower
				|| !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount)
				|| !CardEditorExtraEffects.SupportsAsPower(effect.Kind))
			{
				continue;
			}

			CardExtraEffect stored = CardEditorExtraEffects.CloneEffect(effect);
			stored.AsPower = true;

			Entries.Add(new PowerEffectEntry
			{
				EntryId = Interlocked.Increment(ref _nextEntryId),
				SourceCard = sourceCard,
				Effect = stored
			});
		}

		await SyncVisibleMirrorPowers();
	}

	private static (int VisibleAmount, bool ShowAmountLabel) GetMirrorDisplayState(PowerEffectEntry entry)
	{
		if (entry?.Effect == null)
		{
			return (0, false);
		}

		if (entry.Effect.TriggerMaxTurns > 0)
		{
			return (Math.Max(0, entry.Effect.TriggerMaxTurns - entry.TurnCounter), true);
		}

		if (entry.Effect.TriggerMaxFires > 0)
		{
			return (Math.Max(0, entry.Effect.TriggerMaxFires - entry.TriggerFireCount), true);
		}

		return (0, false);
	}

	public async Task SyncVisibleMirrorPowers()
	{
		try
		{
			Creature? owner = Owner;
			if (owner == null)
			{
				return;
			}

			List<CardEditorVisibleExtraEffectPower> existingMirrors = owner.Powers
				.OfType<CardEditorVisibleExtraEffectPower>()
				.Where(power => power != null)
				.ToList();
			HashSet<long> desiredEntryIds = new HashSet<long>(Entries.Select(entry => entry.EntryId));

			foreach (PowerEffectEntry entry in Entries)
			{
				if (entry?.SourceCard == null || entry.Effect == null)
				{
					continue;
				}

				(int visibleAmount, bool showAmountLabel) = GetMirrorDisplayState(entry);
				CardEditorVisibleExtraEffectPower? mirror = existingMirrors.FirstOrDefault(power => power.EntryId == entry.EntryId);
				if (mirror == null)
				{
					using IDisposable _ = CardEditorVisibleExtraEffectPower.PushPendingPayload(
						entry.EntryId,
						entry.SourceCard,
						entry.Effect,
						visibleAmount,
						showAmountLabel);
					mirror = await PowerCmd.Apply<CardEditorVisibleExtraEffectPower>(owner, 1, owner, entry.SourceCard, silent: true);
				}

				if (mirror != null)
				{
					mirror.SyncFromEntry(entry.EntryId, entry.SourceCard, entry.Effect, visibleAmount, showAmountLabel);
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
			&& owner.CombatState?.GetOpponentsOf(owner).Contains(creature) == true;
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
			CardExtraEffectPowerTriggerFrom.AnyEnemy => owner.CombatState?.GetOpponentsOf(owner).Contains(effectiveEventActor) == true,
			CardExtraEffectPowerTriggerFrom.AnyAlly => effectiveEventActor.Side == owner.Side && !ReferenceEquals(effectiveEventActor, owner),
			CardExtraEffectPowerTriggerFrom.Anyone => true,
			_ => ReferenceEquals(effectiveEventActor, owner)
		};
	}

	private CardExtraEffect BuildResolvedPowerEffect(PowerEffectEntry entry)
	{
		CardExtraEffect resolved = CardEditorExtraEffects.CloneEffect(entry.Effect);
		if (resolved.Target == CardExtraEffectTarget.Target && resolved.PowerTargeting == CardExtraEffectPowerTargeting.AllEnemies)
		{
			resolved.Target = CardExtraEffectTarget.AllEnemies;
		}

		return resolved;
	}

	private async Task ExecuteOrSchedulePowerEffect(
		CombatState combatState,
		PlayerChoiceContext choiceContext,
		CardPlay triggerPlay,
		PowerEffectEntry entry,
		int triggerEventAmount = 1)
	{
		CardExtraEffect effect = entry?.Effect;
		CardModel sourceCard = entry?.SourceCard;
		if (effect == null || sourceCard == null || triggerPlay == null)
		{
			return;
		}

		CardExtraEffect resolvedEffect = BuildResolvedPowerEffect(entry);
		Creature? lockedTarget = null;
		if (resolvedEffect.Target == CardExtraEffectTarget.Target)
		{
			lockedTarget = resolvedEffect.PowerTargeting == CardExtraEffectPowerTargeting.TriggerTarget
				? triggerPlay.Target
				: ResolveRememberedEnemyTarget(entry, combatState, triggerPlay);
			if (lockedTarget == null)
			{
				return;
			}
		}

		if (resolvedEffect.Timing == CardExtraEffectTiming.Immediate)
		{
			CardPlay executionPlay = triggerPlay;
			if (resolvedEffect.Target == CardExtraEffectTarget.Target && !ReferenceEquals(triggerPlay.Target, lockedTarget))
			{
				executionPlay = new CardPlay
				{
					Card = triggerPlay.Card,
					Target = lockedTarget,
					ResultPile = triggerPlay.ResultPile,
					Resources = triggerPlay.Resources,
					IsAutoPlay = triggerPlay.IsAutoPlay,
					PlayIndex = triggerPlay.PlayIndex,
					PlayCount = triggerPlay.PlayCount
				};
			}

			CardEditorPowerSourceMap.Register(this, sourceCard);
			using IDisposable _ = CardEditorEffectSourceContext.PushScoped(sourceCard);
			using IDisposable __ = CardEditorPowerExecutionHostContext.PushScoped(Owner);
			await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, executionPlay, resolvedEffect, triggerEventAmount);
			return;
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

		CardEditorExtraEffectScheduler.Schedule(combatState, schedulingPlay, resolvedEffect, lockedTarget, Owner, triggerEventAmount);
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

	public override Task AfterCardGeneratedForCombat(CardModel card, bool addedByPlayer)
	{
		try
		{
			if (card?.Owner?.Creature == null || !ReferenceEquals(card.Owner.Creature, Owner))
			{
				return Task.CompletedTask;
			}

			CombatState? combatState = CombatState;
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
			CombatState combatState = CombatState;
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
				if (triggeringCard != null
					&& CardEditorExtraEffects.CountEventUsesCardFilters(countEvent)
					&& !CardEditorExtraEffects.MatchesPowerTriggerCardFilters(filterOwner, triggeringCard, entry.Effect))
				{
					continue;
				}

				if (CardEditorExtraEffects.CountEventUsesEnemyStatus(countEvent))
				{
					if (triggeringPower == null || !CardEditorExtraEffects.PowerMatchesStatus(triggeringPower, entry.Effect.PowerTriggerEnemyStatus))
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

				CardPlay basePlay = triggeringPlay;
				if (basePlay == null)
				{
					CardModel playCard = triggeringCard ?? entry.SourceCard;
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

				try
				{
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, basePlay, entry, amount);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed (countEvent={countEvent}): {ex}");
				}

				if (entry.Effect.TriggerMaxFires >= 1)
				{
					entry.TriggerFireCount++;
					if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
					{
						Entries.Remove(entry);
					}
				}
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
			CombatState combatState = CombatState;
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

				CardModel sourceCard = entry.SourceCard;
				if (sourceCard.Owner?.Creature == null || !ReferenceEquals(sourceCard.Owner.Creature, owner))
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
				try
				{
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed ({trigger}): {ex}");
				}

				if (entry.Effect.TriggerMaxFires >= 1)
				{
					entry.TriggerFireCount++;
					if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
					{
						Entries.Remove(entry);
					}
				}
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

	public override async Task AfterAttack(AttackCommand command)
	{
		try
		{
			CombatState combatState = CombatState;
			if (combatState == null)
			{
				return;
			}

			Creature? owner = Owner;
			if (owner == null || !owner.IsPlayer)
			{
				return;
			}

			Player ownerPlayer = owner.Player;
			Creature? osty = ownerPlayer.Osty;
			if (osty == null || !ReferenceEquals(command.Attacker, osty))
			{
				return;
			}

			ulong? netId = LocalContext.NetId;
			if (!netId.HasValue)
			{
				return;
			}

			HookPlayerChoiceContext choiceContext = new HookPlayerChoiceContext(this, netId.Value, combatState, GameActionType.Combat);

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

				try
				{
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
				}
				catch (Exception ex)
				{
					Log.Warn($"[CardEditor] Power extra effect failed (OstyDealDamage): {ex}");
				}

				if (entry.Effect.TriggerMaxFires >= 1)
				{
					entry.TriggerFireCount++;
					if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
					{
						Entries.Remove(entry);
					}
				}
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

			// Expire "this turn" power entries for non-stat-buff kinds.
			Entries.RemoveAll(e => e != null
				&& e.Effect != null
				&& !CardEditorExtraEffects.SupportsDuration(e.Effect.Kind)
				&& e.Effect.Duration == CardExtraEffectDuration.ThisTurn);

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
		CombatState combatState = CombatState;
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

			await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);

			if (entry.Effect.TriggerMaxFires >= 1)
			{
				entry.TriggerFireCount++;
				if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
				{
					Entries.Remove(entry);
				}
			}
		}
	}
	finally
	{
		await SyncVisibleMirrorPowers();
	}
}

private async Task RunStartOrEndTimed(PlayerChoiceContext choiceContext, CardExtraEffectTrigger trigger)
{
	try
	{
		CombatState combatState = CombatState;
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

			try
			{
				await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, entry);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Power extra effect failed (timed trigger={trigger}): {ex}");
			}

			// Track fire count and expire entry if max reached.
			if (entry.Effect.TriggerMaxFires >= 1)
			{
				entry.TriggerFireCount++;
				if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
				{
					Entries.Remove(entry);
				}
			}
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
			CombatState combatState = CombatState;
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

				await ExecuteOrSchedulePowerEffect(combatState, choiceContext, basePlay, entry);

				// Track fire count and expire entry if max reached.
				if (entry.Effect.TriggerMaxFires >= 1)
				{
					entry.TriggerFireCount++;
					if (entry.TriggerFireCount >= entry.Effect.TriggerMaxFires)
					{
						Entries.Remove(entry);
					}
				}
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
