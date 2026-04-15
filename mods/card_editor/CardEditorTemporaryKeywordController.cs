using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTemporaryKeywordController
{
	private sealed class KeywordGrant
	{
		public required CardKeyword Keyword { get; init; }
		public required CardExtraEffectCardGrantDuration Duration { get; init; }
		public int RemainingTurns { get; set; }
	}

	private sealed class KeywordState
	{
		public bool HadKeywordBefore { get; set; }
		public List<KeywordGrant> Grants { get; } = new List<KeywordGrant>();
	}

	private sealed class CombatSchedule
	{
		public Dictionary<CardModel, Dictionary<CardKeyword, KeywordState>> States { get; } =
			new Dictionary<CardModel, Dictionary<CardKeyword, KeywordState>>();
	}

	private static readonly ConditionalWeakTable<CombatState, CombatSchedule> _schedules =
		new ConditionalWeakTable<CombatState, CombatSchedule>();

	public static void ApplyForTurns(CombatState combatState, CardModel card, CardKeyword keyword, int turns)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (turns <= 0)
		{
			return;
		}

		EnsureKeywordApplied(combatState, card, keyword, new KeywordGrant
		{
			Keyword = keyword,
			Duration = turns == 1 ? CardExtraEffectCardGrantDuration.ThisTurn : CardExtraEffectCardGrantDuration.Turns,
			RemainingTurns = turns
		});
	}

	public static void ApplyThisCombat(CombatState combatState, CardModel card, CardKeyword keyword)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		EnsureKeywordApplied(combatState, card, keyword, new KeywordGrant
		{
			Keyword = keyword,
			Duration = CardExtraEffectCardGrantDuration.ThisCombat,
			RemainingTurns = 0
		});
	}

	public static void ApplyUntilPlayed(CombatState combatState, CardModel card, CardKeyword keyword)
	{
		if (combatState == null || card == null)
		{
			return;
		}

		EnsureKeywordApplied(combatState, card, keyword, new KeywordGrant
		{
			Keyword = keyword,
			Duration = CardExtraEffectCardGrantDuration.UntilPlayed,
			RemainingTurns = 0
		});
	}

	private static void EnsureKeywordApplied(CombatState combatState, CardModel card, CardKeyword keyword, KeywordGrant grant)
	{
		if (!card.IsMutable)
		{
			return;
		}

		CombatSchedule schedule = _schedules.GetOrCreateValue(combatState);
		if (!schedule.States.TryGetValue(card, out Dictionary<CardKeyword, KeywordState>? byKeyword))
		{
			byKeyword = new Dictionary<CardKeyword, KeywordState>();
			schedule.States[card] = byKeyword;
		}

		if (!byKeyword.TryGetValue(keyword, out KeywordState? state))
		{
			state = new KeywordState { HadKeywordBefore = card.Keywords.Contains(keyword) };
			byKeyword[keyword] = state;
		}

		if (!card.Keywords.Contains(keyword))
		{
			CardCmd.ApplyKeyword(card, keyword);
		}

		state.Grants.Add(grant);
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

		foreach ((CardModel card, Dictionary<CardKeyword, KeywordState> byKeyword) in schedule.States)
		{
			if (card == null || byKeyword == null || byKeyword.Count == 0)
			{
				continue;
			}
			if (!card.IsMutable || card.HasBeenRemovedFromState)
			{
				continue;
			}

			foreach ((CardKeyword keyword, KeywordState state) in byKeyword)
			{
				if (state == null || state.Grants.Count == 0)
				{
					continue;
				}

				for (int i = state.Grants.Count - 1; i >= 0; i--)
				{
					KeywordGrant grant = state.Grants[i];
					if (grant == null)
					{
						state.Grants.RemoveAt(i);
						continue;
					}
					if (grant.Duration is CardExtraEffectCardGrantDuration.ThisCombat or CardExtraEffectCardGrantDuration.UntilPlayed)
					{
						continue;
					}

					grant.RemainingTurns--;
					if (grant.RemainingTurns <= 0)
					{
						state.Grants.RemoveAt(i);
					}
				}

				if (state.Grants.Count == 0 && !state.HadKeywordBefore && card.Keywords.Contains(keyword))
				{
					CardCmd.RemoveKeyword(card, keyword);
				}
			}
		}

		CleanupScheduleIfEmpty(combatState, schedule);
	}

	public static void OnAfterCardPlayed(CombatState combatState, CardModel card)
	{
		if (combatState == null || card == null)
		{
			return;
		}
		if (!_schedules.TryGetValue(combatState, out CombatSchedule? schedule)
			|| !schedule.States.TryGetValue(card, out Dictionary<CardKeyword, KeywordState>? byKeyword)
			|| byKeyword == null
			|| byKeyword.Count == 0)
		{
			return;
		}

		foreach ((CardKeyword keyword, KeywordState state) in byKeyword)
		{
			if (state == null || state.Grants.Count == 0)
			{
				continue;
			}

			for (int i = state.Grants.Count - 1; i >= 0; i--)
			{
				KeywordGrant grant = state.Grants[i];
				if (grant == null)
				{
					state.Grants.RemoveAt(i);
					continue;
				}
				if (grant.Duration == CardExtraEffectCardGrantDuration.UntilPlayed)
				{
					state.Grants.RemoveAt(i);
				}
			}

			if (state.Grants.Count == 0 && !state.HadKeywordBefore && card.Keywords.Contains(keyword))
			{
				CardCmd.RemoveKeyword(card, keyword);
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

		foreach ((CardModel card, Dictionary<CardKeyword, KeywordState> byKeyword) in schedule.States)
		{
			if (card == null || byKeyword == null || byKeyword.Count == 0)
			{
				continue;
			}
			if (!card.IsMutable)
			{
				continue;
			}

			foreach ((CardKeyword keyword, KeywordState state) in byKeyword)
			{
				if (state == null)
				{
					continue;
				}
				if (!state.HadKeywordBefore && card.Keywords.Contains(keyword))
				{
					CardCmd.RemoveKeyword(card, keyword);
				}
			}
		}

		_schedules.Remove(combatState);
	}

	private static void CleanupScheduleIfEmpty(CombatState combatState, CombatSchedule schedule)
	{
		bool any = false;
		foreach ((CardModel card, Dictionary<CardKeyword, KeywordState> byKeyword) in schedule.States)
		{
			if (card == null || byKeyword == null)
			{
				continue;
			}
			foreach ((CardKeyword _, KeywordState state) in byKeyword)
			{
				if (state != null && state.Grants.Count > 0)
				{
					any = true;
					break;
				}
			}
			if (any)
			{
				break;
			}
		}

		if (!any)
		{
			_schedules.Remove(combatState);
		}
	}
}
