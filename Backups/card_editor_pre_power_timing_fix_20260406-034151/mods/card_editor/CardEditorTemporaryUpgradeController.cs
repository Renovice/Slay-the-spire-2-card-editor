using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryUpgradeController
{
	private sealed class UpgradeGrant
	{
		public required bool ExpiresAtCombatEnd { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class UpgradeState
	{
		public List<UpgradeGrant> Grants { get; } = new List<UpgradeGrant>();
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, UpgradeState> States { get; } =
			new Dictionary<CardModel, UpgradeState>();
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules =
		new ConditionalWeakTable<CombatState, CombatSchedule>();

	public static void Apply(CombatState combatState, IEnumerable<CardModel> cards, CardExtraEffectCardCostsLessDuration duration, int turns)
	{
		if (combatState == null || cards == null)
		{
			return;
		}

		foreach (CardModel card in cards)
		{
			if (card == null || !card.IsMutable)
			{
				continue;
			}

			UpgradeGrant grant;
			if (duration == CardExtraEffectCardCostsLessDuration.ThisCombat)
			{
				grant = new UpgradeGrant { ExpiresAtCombatEnd = true, RemainingTurns = 0 };
			}
			else if (duration == CardExtraEffectCardCostsLessDuration.Turns)
			{
				grant = new UpgradeGrant { ExpiresAtCombatEnd = false, RemainingTurns = Math.Max(1, turns) };
			}
			else
			{
				// ThisTurn (and anything unrecognised)
				grant = new UpgradeGrant { ExpiresAtCombatEnd = false, RemainingTurns = 1 };
			}

			CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
			if (!schedule.States.TryGetValue(card, out UpgradeState? state))
			{
				state = new UpgradeState();
				schedule.States[card] = state;
			}

			state.Grants.Add(grant);
		}
	}

	public static void OnAfterPlayerTurnEnd(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule) || schedule.States.Count == 0)
		{
			return;
		}

		foreach ((CardModel card, UpgradeState state) in schedule.States)
		{
			if (card == null || state == null || state.Grants.Count == 0)
			{
				continue;
			}
			if (!card.IsMutable || card.HasBeenRemovedFromState)
			{
				continue;
			}

			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				UpgradeGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					continue;
				}
				if (grant.ExpiresAtCombatEnd)
				{
					continue;
				}

				grant.RemainingTurns--;
				if (grant.RemainingTurns <= 0)
				{
					state.Grants.RemoveAt(i);
					CardCmd.Downgrade(card);
				}
			}
		}

		CleanupScheduleIfEmpty(combatState, schedule);
	}

	public static void Clear(CombatState combatState)
	{
		if (combatState == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule))
		{
			return;
		}

		foreach ((CardModel card, UpgradeState state) in schedule.States)
		{
			if (card == null || !card.IsMutable || state == null)
			{
				continue;
			}

			int downgrades = state.Grants.Count;
			for (int i = 0; i < downgrades; i++)
			{
				CardCmd.Downgrade(card);
			}
		}

		_schedules.Remove(combatState);
	}

	private static void CleanupScheduleIfEmpty(CombatState combatState, CombatSchedule schedule)
	{
		bool any = false;
		foreach ((CardModel card, UpgradeState state) in schedule.States)
		{
			if (card == null || state == null)
			{
				continue;
			}
			if (state.Grants.Count > 0)
			{
				any = true;
				break;
			}
		}

		if (!any)
		{
			_schedules.Remove(combatState);
		}
	}
}
