using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SlayTheSpire2Mod.CardEditor;

// Installed quest-reward passives. Combat-scope installs live per CombatState only; run-scope
// installs are re-derived on demand from the persistent quest completion counters (deck cards)
// and the run's save-persistent CompletedQuests/CardsRemoved map history (removed cards), so
// they survive save/load without any storage of their own. Installs are binary per
// (owner, quest card id, quest effect, reward row): repeat completions refresh, never stack.
internal static class CardEditorQuestPassiveStore
{
	private static readonly ConditionalWeakTable<CombatState, CombatPassiveState> _combatStates = new ConditionalWeakTable<CombatState, CombatPassiveState>();

	internal sealed class InstalledPassive
	{
		public InstalledPassive(Player owner, CardModel questCard, CardExtraEffect questEffect, CardExtraEffect rewardRow)
		{
			Owner = owner;
			QuestCard = questCard;
			QuestEffect = questEffect;
			RewardRow = rewardRow;
			Key = BuildInstallKey(owner, questCard, questEffect, rewardRow);
		}

		public Player Owner { get; }
		public CardModel QuestCard { get; }
		public CardExtraEffect QuestEffect { get; }
		public CardExtraEffect RewardRow { get; }
		public string Key { get; }
	}

	public static void InstallCombat(CombatState combatState, Player owner, CardModel questCard, CardExtraEffect questEffect, CardExtraEffect rewardRow)
	{
		if (combatState == null || owner == null || questCard == null || questEffect == null || rewardRow == null)
		{
			return;
		}

		try
		{
			CombatPassiveState state = _combatStates.GetValue(combatState, _ => new CombatPassiveState());
			InstalledPassive install = new InstalledPassive(owner, questCard, questEffect, CardEditorExtraEffects.CloneEffect(rewardRow));
			if (state.CombatInstalls.Any(existing => existing != null && string.Equals(existing.Key, install.Key, StringComparison.Ordinal)))
			{
				return;
			}

			state.CombatInstalls.Add(install);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to install combat quest passive for {questCard?.Id}: {ex}");
		}
	}

	public static IReadOnlyList<InstalledPassive> GetCombatInstalls(CombatState combatState)
	{
		if (combatState != null && _combatStates.TryGetValue(combatState, out CombatPassiveState? state))
		{
			return state.CombatInstalls;
		}

		return Array.Empty<InstalledPassive>();
	}

	// Combat installs win over run-derived entries with the same key so a quest that completed
	// during deck-passive resolution (before the run cache was built) can never fire twice.
	internal static List<InstalledPassive> GetActiveInstalls(CombatState combatState)
	{
		List<InstalledPassive> result = new List<InstalledPassive>();
		if (combatState == null)
		{
			return result;
		}

		CombatPassiveState state = _combatStates.GetValue(combatState, _ => new CombatPassiveState());
		HashSet<string> combatKeys = new HashSet<string>(StringComparer.Ordinal);
		foreach (InstalledPassive install in state.CombatInstalls)
		{
			if (install != null && combatKeys.Add(install.Key))
			{
				result.Add(install);
			}
		}

		state.RunInstallsCache ??= BuildRunInstallsForCombat(combatState);
		foreach (InstalledPassive install in state.RunInstallsCache)
		{
			if (install != null && !combatKeys.Contains(install.Key))
			{
				result.Add(install);
			}
		}

		return result;
	}

	internal static bool TryMarkCombatStartFired(CombatState combatState, InstalledPassive install)
	{
		if (combatState == null || install == null)
		{
			return false;
		}

		CombatPassiveState state = _combatStates.GetValue(combatState, _ => new CombatPassiveState());
		return state.CombatStartFired.Add(install.Key);
	}

	public static List<InstalledPassive> EnumerateRunInstalls(Player? player)
	{
		List<InstalledPassive> result = new List<InstalledPassive>();
		if (player == null)
		{
			return result;
		}

		HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
		try
		{
			foreach (CardModel card in player.Deck?.Cards?.ToList() ?? new List<CardModel>())
			{
				if (card != null)
				{
					CollectInstallsFromCard(player, card, requireCompletionCounter: true, result, seenKeys);
				}
			}

			CollectRemovedQuestInstalls(player, result, seenKeys);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to enumerate run-scoped quest passives: {ex}");
		}

		return result;
	}

	private static List<InstalledPassive> BuildRunInstallsForCombat(CombatState combatState)
	{
		List<InstalledPassive> result = new List<InstalledPassive>();
		try
		{
			foreach (Player player in combatState.Players ?? Enumerable.Empty<Player>())
			{
				result.AddRange(EnumerateRunInstalls(player));
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to build run-scoped quest passives for combat: {ex}");
		}

		return result;
	}

	private static void CollectInstallsFromCard(Player player, CardModel card, bool requireCompletionCounter, List<InstalledPassive> result, HashSet<string> seenKeys)
	{
		List<CardExtraEffect> effects = CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false).ToList();
		foreach (CardExtraEffect effect in effects)
		{
			if (effect?.Kind != CardExtraEffectKind.Quest
				|| effect.QuestRewardStyle != CardExtraEffectQuestRewardStyle.KeepTriggerRun)
			{
				continue;
			}

			if (requireCompletionCounter
				&& CardEditorRunCardCounterState.Get(null, card, CardEditorQuestEffects.BuildQuestCompletionCounterKey(effect), persistent: true) < 1)
			{
				continue;
			}

			foreach (CardExtraEffect row in CardEditorQuestEffects.ResolveQuestRewardEffects(card, effect, keepTriggers: true))
			{
				if (row == null || !CardEditorQuestEffects.IsRecurringRewardTrigger(row.Trigger))
				{
					continue;
				}

				InstalledPassive install = new InstalledPassive(player, card, effect, row);
				if (seenKeys.Add(install.Key))
				{
					result.Add(install);
				}
			}
		}
	}

	// Final quest completions remove the quest card from the deck, but the game records the
	// completion (CompletedQuests) and the removed card snapshot (CardsRemoved) in its own
	// save-persistent map point history, so run-scoped installs can be rebuilt from there.
	private static void CollectRemovedQuestInstalls(Player player, List<InstalledPassive> result, HashSet<string> seenKeys)
	{
		IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>>? history = player.RunState?.MapPointHistory;
		if (history == null)
		{
			return;
		}

		List<ModelId> completedIds = new List<ModelId>();
		List<SerializableCard> removedCards = new List<SerializableCard>();
		foreach (IReadOnlyList<MapPointHistoryEntry> actEntries in history)
		{
			foreach (MapPointHistoryEntry entry in actEntries ?? Enumerable.Empty<MapPointHistoryEntry>())
			{
				PlayerMapPointHistoryEntry? stats = entry?.PlayerStats?.FirstOrDefault(playerEntry => playerEntry != null && playerEntry.PlayerId == player.NetId);
				if (stats == null)
				{
					continue;
				}

				completedIds.AddRange((stats.CompletedQuests ?? new List<ModelId>()).Where(id => id != null && id != ModelId.none));
				removedCards.AddRange((stats.CardsRemoved ?? new List<SerializableCard>()).Where(saved => saved?.Id != null && saved.Id != ModelId.none));
			}
		}

		HashSet<string> processedIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (ModelId completedId in completedIds)
		{
			if (!processedIds.Add(completedId.ToString()))
			{
				continue;
			}
			if (!CardEditorOverrides.TryGet(completedId, out _))
			{
				continue;
			}

			try
			{
				SerializableCard saved = removedCards.FirstOrDefault(candidate => candidate?.Id != null && candidate.Id.Equals(completedId))
					?? new SerializableCard { Id = completedId };
				CardModel questCard = CardModel.FromSerializable(saved);
				if (questCard != null)
				{
					CollectInstallsFromCard(player, questCard, requireCompletionCounter: false, result, seenKeys);
				}
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Failed to derive installed quest passives for removed card {completedId}: {ex}");
			}
		}
	}

	private static string BuildInstallKey(Player owner, CardModel questCard, CardExtraEffect questEffect, CardExtraEffect rewardRow)
	{
		string cardKey = questCard?.Id?.ToString()
			?? RuntimeHelpers.GetHashCode(questCard).ToString(CultureInfo.InvariantCulture);
		string effectKey = !string.IsNullOrWhiteSpace(questEffect?.EffectId)
			? questEffect.EffectId.Trim()
			: CardEditorQuestEffects.BuildQuestCounterKey(questEffect ?? new CardExtraEffect());
		string rowKey = !string.IsNullOrWhiteSpace(rewardRow?.EffectId)
			? rewardRow.EffectId.Trim()
			: $"{rewardRow?.Kind}|{rewardRow?.Trigger}";
		return string.Join(
			"|",
			(owner?.NetId ?? 0UL).ToString(CultureInfo.InvariantCulture),
			cardKey,
			effectKey,
			rowKey);
	}

	private sealed class CombatPassiveState
	{
		public List<InstalledPassive> CombatInstalls { get; } = new List<InstalledPassive>();
		public List<InstalledPassive>? RunInstallsCache { get; set; }
		public HashSet<string> CombatStartFired { get; } = new HashSet<string>(StringComparer.Ordinal);
	}
}
