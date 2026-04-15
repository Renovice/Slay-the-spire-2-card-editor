using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
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
		public required CardModel SourceCard { get; init; }
		public required CardExtraEffect Effect { get; init; }
		public int TriggerCounter { get; set; }
		public int TriggerFireCount { get; set; }
		public int TurnCounter { get; set; }
	}

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
						SourceCard = e.SourceCard,
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

		if (snapshot != null && snapshot.Count > 0)
		{
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
				&& (e.Effect.Kind != CardExtraEffectKind.OstyAction
					|| e.Effect.OstyAction == CardExtraEffectOstyAction.Attack));
		}
		catch
		{
			return false;
		}
	}

	public void AddPowerEffects(CardModel sourceCard, IReadOnlyList<CardExtraEffect> effects)
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

			Entries.Add(new PowerEffectEntry { SourceCard = sourceCard, Effect = stored });
		}
	}

	private async Task ExecuteOrSchedulePowerEffect(
		CombatState combatState,
		PlayerChoiceContext choiceContext,
		CardPlay triggerPlay,
		CardModel sourceCard,
		CardExtraEffect effect)
	{
		if (effect == null || sourceCard == null || triggerPlay == null)
		{
			return;
		}

		if (effect.Timing == CardExtraEffectTiming.Immediate)
		{
			CardEditorPowerSourceMap.Register(this, sourceCard);
			using IDisposable _ = CardEditorEffectSourceContext.PushScoped(sourceCard);
			await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, triggerPlay, effect);
			return;
		}

		CardPlay schedulingPlay = new CardPlay
		{
			Card = sourceCard,
			Target = triggerPlay.Target,
			ResultPile = triggerPlay.ResultPile,
			Resources = triggerPlay.Resources,
			IsAutoPlay = true,
			PlayIndex = 0,
			PlayCount = 1
		};

		Creature? lockedTarget = effect.Target == CardExtraEffectTarget.Target ? triggerPlay.Target : null;
		CardEditorExtraEffectScheduler.Schedule(combatState, schedulingPlay, effect, lockedTarget);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		await RunTrigger(context, cardPlay?.Card, cardPlay, CardExtraEffectTrigger.OnPlay);
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnDraw);
	}

	public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
	{
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnDiscard);
	}

	public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
	{
		await RunTrigger(choiceContext, card, play: null, CardExtraEffectTrigger.OnExhaust);
	}

	public override async Task AfterOrbChanneled(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
	{
		await RunOrbTrigger(choiceContext, CardExtraEffectTrigger.OnChannel);
	}

	public override async Task AfterOrbEvoked(PlayerChoiceContext choiceContext, OrbModel orb, IEnumerable<Creature> targets)
	{
		await RunOrbTrigger(choiceContext, CardExtraEffectTrigger.OnEvoke);
	}

	public async Task TriggerCountEvent(PlayerChoiceContext choiceContext, CardExtraEffectCountEvent countEvent, CardEditorExtraEffects.ResourceCountSource source = CardEditorExtraEffects.ResourceCountSource.Other)
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
					|| entry.Effect.Trigger != CardExtraEffectTrigger.OnCountEvent
					|| entry.Effect.PowerTriggerCountEvent != countEvent
					|| !CardEditorExtraEffects.IsValidEffectAmount(entry.Effect.Kind, entry.Effect.Amount))
				{
					continue;
				}

				if (countEvent == CardExtraEffectCountEvent.BlockLost
					&& source == CardEditorExtraEffects.ResourceCountSource.BetweenTurnsBlockClear
					&& entry.Effect.BlockLostCountingMode != CardExtraEffectBlockLostCountingMode.IncludeBetweenTurns)
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
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, sourceCard, entry.Effect);
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
	}

	private async Task RunOrbTrigger(PlayerChoiceContext choiceContext, CardExtraEffectTrigger trigger)
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
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, sourceCard, entry.Effect);
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
					await ExecuteOrSchedulePowerEffect(combatState, choiceContext, syntheticPlay, sourceCard, entry.Effect);
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
	}

public override async Task AfterTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
{
	Creature? owner = Owner;
	if (owner == null)
	{
		return;
	}

	if (side == owner.Side)
	{
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
		await RunStartOrEndTimed(choiceContext, CardExtraEffectTrigger.EndOfEnemyTurn);
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

private async Task RunStartOrEndTimed(PlayerChoiceContext choiceContext, CardExtraEffectTrigger trigger)
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
				|| (trigger == CardExtraEffectTrigger.EndOfTurnInHand && entry.Effect.Trigger == CardExtraEffectTrigger.TurnBoundary)
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
			if (card.Owner?.Creature == null || !ReferenceEquals(card.Owner.Creature, Owner))
			{
				continue;
			}

			CardEditorPowerSourceMap.Register(this, card);
			using IDisposable _ = CardEditorEffectSourceContext.PushScoped(card);

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
				await CardEditorExtraEffects.ExecuteEffect(combatState, choiceContext, syntheticPlay, entry.Effect);
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

	private async Task RunTrigger(PlayerChoiceContext choiceContext, CardModel? triggeringCard, CardPlay? play, CardExtraEffectTrigger trigger)
	{
		try
		{
			CombatState combatState = CombatState;
			if (combatState == null || choiceContext == null)
			{
				return;
			}

			if (triggeringCard?.Owner?.Creature == null || !ReferenceEquals(triggeringCard.Owner.Creature, Owner))
			{
				return;
			}

			Player ownerPlayer = triggeringCard.Owner;

			CardPlay? basePlay = play;
			if (basePlay == null && triggeringCard != null)
			{
				basePlay = new CardPlay
				{
					Card = triggeringCard,
					Target = null,
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

				if (!CardEditorExtraEffects.MatchesPowerTriggerCardFilters(ownerPlayer, triggeringCard, entry.Effect))
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
				if (sourceCard.Owner?.Creature == null || !ReferenceEquals(sourceCard.Owner.Creature, Owner))
				{
					continue;
				}

				await ExecuteOrSchedulePowerEffect(combatState, choiceContext, basePlay, sourceCard, entry.Effect);

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
	}
}
