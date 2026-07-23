using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorQuestEffects
{
	private const bool QuestCountersArePersistent = true;
	private static readonly HashSet<string> _completing = new(StringComparer.Ordinal);
	private static readonly Regex _unsafeOptionIdChars = new("[^A-Z0-9_]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	// Per-player cache of "does the deck contain ANY RunProgress quest row" (scope/act
	// filters deliberately ignored -> superset of the active quests). Keyed on the runtime
	// cache version (covers override/draft/created/grant changes) plus a reference stamp of
	// the deck contents (covers cards added/removed/transformed mid-run). Lets the per-event
	// quest recorders skip the full deck rescan when the run has no quests at all, which is
	// exactly behavior-preserving because GetActiveQuests would have yielded nothing.
	private sealed class QuestPresenceEntry
	{
		internal long Version = long.MinValue;
		internal int DeckStamp;
		internal bool HasAny;
	}

	private static readonly ConditionalWeakTable<Player, QuestPresenceEntry> _questPresenceCache = new();

	private static int ComputeDeckStamp(Player player)
	{
		IReadOnlyList<CardModel>? deckCards = player?.Deck?.Cards;
		if (deckCards == null)
		{
			return 0;
		}

		int stamp = 17;
		unchecked
		{
			stamp = (stamp * 31) + deckCards.Count;
			for (int i = 0; i < deckCards.Count; i++)
			{
				CardModel? card = deckCards[i];
				stamp = (stamp * 31) + (card == null ? 0 : RuntimeHelpers.GetHashCode(card));
				// Upgrade level changes in place with no version bump, and upgraded overrides can
				// carry quest rows the base list lacks - it must be part of the stamp.
				stamp = (stamp * 31) + (card == null ? 0 : card.GetSafeCurrentUpgradeLevel());
			}
		}

		return stamp;
	}

	private static bool HasAnyRunProgressQuestRows(Player? owner)
	{
		if (owner?.Deck?.Cards == null)
		{
			return false;
		}

		try
		{
			QuestPresenceEntry entry = _questPresenceCache.GetOrCreateValue(owner);
			long version = CardEditorRuntimeCacheVersion.Current;
			int stamp = ComputeDeckStamp(owner);
			if (entry.Version != version || entry.DeckStamp != stamp)
			{
				entry.HasAny = ComputeHasAnyRunProgressQuestRows(owner);
				entry.DeckStamp = stamp;
				entry.Version = version;
			}

			return entry.HasAny;
		}
		catch
		{
			// Conservative: on failure fall through to the full quest scan.
			return true;
		}
	}

	private static bool ComputeHasAnyRunProgressQuestRows(Player owner)
	{
		foreach (CardModel card in owner.Deck.Cards.ToList())
		{
			if (card == null)
			{
				continue;
			}

			foreach (CardExtraEffect effect in CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false))
			{
				if (effect?.Kind == CardExtraEffectKind.Quest && effect.QuestMode == CardExtraEffectQuestMode.RunProgress)
				{
					return true;
				}
			}
		}

		return false;
	}

	internal static async Task RecordCardPlayed(CombatState combatState, CardPlay cardPlay)
	{
		CardModel? playedCard = cardPlay?.Card;
		Player? owner = playedCard?.Owner;
		await RecordCardProgress(
			combatState,
			playedCard,
			owner,
			CardExtraEffectCountEvent.Played,
			CardExtraEffectCountEvent.ThisCardPlayed);
	}

	internal static Task RecordCardDrawn(CombatState combatState, CardModel card)
	{
		return RecordCardProgress(
			combatState,
			card,
			card?.Owner,
			CardExtraEffectCountEvent.Drawn,
			CardExtraEffectCountEvent.ThisCardDrawn);
	}

	internal static Task RecordCardDiscarded(CombatState combatState, CardModel card)
	{
		return RecordCardProgress(
			combatState,
			card,
			card?.Owner,
			CardExtraEffectCountEvent.Discarded,
			CardExtraEffectCountEvent.ThisCardDiscarded);
	}

	internal static Task RecordCardExhausted(CombatState combatState, CardModel card)
	{
		return RecordCardProgress(
			combatState,
			card,
			card?.Owner,
			CardExtraEffectCountEvent.Exhausted,
			CardExtraEffectCountEvent.ThisCardExhausted);
	}

	internal static Task RecordCardGenerated(CombatState combatState, CardModel card)
	{
		return RecordCardProgress(
			combatState,
			card,
			card?.Owner,
			CardExtraEffectCountEvent.Generated,
			null);
	}

	internal static bool SupportsRunProgressEvent(CardExtraEffectCountEvent countEvent)
	{
		return countEvent is CardExtraEffectCountEvent.Played
			or CardExtraEffectCountEvent.Drawn
			or CardExtraEffectCountEvent.Discarded
			or CardExtraEffectCountEvent.Exhausted
			or CardExtraEffectCountEvent.Generated
			or CardExtraEffectCountEvent.ThisCardPlayed
			or CardExtraEffectCountEvent.ThisCardDrawn
			or CardExtraEffectCountEvent.ThisCardDiscarded
			or CardExtraEffectCountEvent.ThisCardExhausted
			or CardExtraEffectCountEvent.StarsGained
			or CardExtraEffectCountEvent.StarsLost
			or CardExtraEffectCountEvent.EnergyGained
			or CardExtraEffectCountEvent.EnergyLost
			or CardExtraEffectCountEvent.EnergyUsed
			or CardExtraEffectCountEvent.StarsSpent
			or CardExtraEffectCountEvent.BlockGained
			or CardExtraEffectCountEvent.BlockLost
			or CardExtraEffectCountEvent.StatusGained
			or CardExtraEffectCountEvent.StatusLost
			or CardExtraEffectCountEvent.DamageDealt
			or CardExtraEffectCountEvent.DamageTaken
			or CardExtraEffectCountEvent.HealingReceived
			or CardExtraEffectCountEvent.Summoned
			or CardExtraEffectCountEvent.GoldGained
			or CardExtraEffectCountEvent.GoldLost
			or CardExtraEffectCountEvent.EnemyKilled
			or CardExtraEffectCountEvent.RoomEntered
			or CardExtraEffectCountEvent.CombatEntered
			or CardExtraEffectCountEvent.ShopEntered
			or CardExtraEffectCountEvent.RestSiteEntered
			or CardExtraEffectCountEvent.PotionUsed
			or CardExtraEffectCountEvent.CombatWon;
	}

	internal static Task RecordRunProgress(Creature? actor, CardExtraEffectCountEvent countEvent, int amount = 1, CombatState? combatState = null, PowerModel? triggeringPower = null)
	{
		return RecordRunProgress(GetQuestProgressOwner(actor), countEvent, amount, combatState, triggeringPower);
	}

	internal static async Task RecordRunProgress(IRunState? runState, CardExtraEffectCountEvent countEvent, int amount = 1, CombatState? combatState = null, PowerModel? triggeringPower = null)
	{
		if (runState?.Players == null || amount <= 0 || !SupportsRunProgressEvent(countEvent))
		{
			return;
		}

		foreach (Player player in runState.Players.ToList())
		{
			await RecordRunProgress(player, countEvent, amount, combatState, triggeringPower);
		}
	}

	internal static async Task RecordRunProgress(Player? owner, CardExtraEffectCountEvent countEvent, int amount = 1, CombatState? combatState = null, PowerModel? triggeringPower = null)
	{
		if (owner == null || amount <= 0 || !SupportsRunProgressEvent(countEvent))
		{
			return;
		}

		// No RunProgress quest rows anywhere in the deck -> GetActiveQuests below would
		// yield nothing; skip the per-event full deck rescan.
		if (!HasAnyRunProgressQuestRows(owner))
		{
			return;
		}

		foreach (QuestRuntime quest in GetActiveQuests(owner, CardExtraEffectQuestMode.RunProgress).ToList())
		{
			CardExtraEffect effect = quest.Effect;
			if (!MatchesRunProgressEvent(effect, countEvent, triggeringPower))
			{
				continue;
			}

			string key = BuildQuestCounterKey(effect);
			int progress = CardEditorRunCardCounterState.Add(combatState, quest.Card, key, amount, QuestCountersArePersistent);
			int target = GetQuestTarget(effect);
			if (progress >= target)
			{
				await CompleteQuest(owner, quest.Card, effect, combatState);
			}
		}
	}

	private static Player? GetQuestProgressOwner(Creature? actor)
	{
		if (actor == null)
		{
			return null;
		}

		return actor.Player ?? actor.PetOwner;
	}

	private static bool MatchesRunProgressEvent(CardExtraEffect effect, CardExtraEffectCountEvent countEvent, PowerModel? triggeringPower)
	{
		if (effect == null || effect.CountEvent != countEvent)
		{
			return false;
		}

		if (countEvent is CardExtraEffectCountEvent.StatusGained or CardExtraEffectCountEvent.StatusLost)
		{
			return CardEditorExtraEffects.PowerMatchesConfiguredStatus(triggeringPower, effect.CountEnemyStatus, effect.CountPowerId);
		}

		return true;
	}

	private static async Task RecordCardProgress(
		CombatState combatState,
		CardModel? eventCard,
		Player? owner,
		CardExtraEffectCountEvent normalEvent,
		CardExtraEffectCountEvent? thisCardEvent)
	{
		if (combatState == null || eventCard == null || owner == null)
		{
			return;
		}

		// See RecordRunProgress: skip the deck rescan when no quest rows exist at all.
		if (!HasAnyRunProgressQuestRows(owner))
		{
			return;
		}

		foreach (QuestRuntime quest in GetActiveQuests(owner, CardExtraEffectQuestMode.RunProgress).ToList())
		{
			CardExtraEffect effect = quest.Effect;
			if (thisCardEvent.HasValue && effect.CountEvent == thisCardEvent.Value)
			{
				if (!IsSameDeckCard(eventCard, quest.Card))
				{
					continue;
				}
			}
			else if (effect.CountEvent == normalEvent)
			{
				if (!CardEditorExtraEffects.MatchesQuestObjectiveCard(owner, eventCard, effect))
				{
					continue;
				}
			}
			else
			{
				continue;
			}

			string key = BuildQuestCounterKey(effect);
			int progress = CardEditorRunCardCounterState.Increment(combatState, quest.Card, key, QuestCountersArePersistent);
			int target = GetQuestTarget(effect);
			if (progress >= target)
			{
				await CompleteQuest(owner, quest.Card, effect, combatState);
			}
		}
	}

	internal static bool TryGetQuestProgress(CardModel? card, CardExtraEffect? effect, out int progress, out int target, out int remaining)
	{
		progress = 0;
		target = GetQuestTarget(effect);
		remaining = target;
		if (card == null || effect == null || effect.Kind != CardExtraEffectKind.Quest || effect.QuestMode != CardExtraEffectQuestMode.RunProgress)
		{
			return false;
		}

		progress = Math.Min(target, CardEditorRunCardCounterState.Get(null, card, BuildQuestCounterKey(effect), QuestCountersArePersistent));
		remaining = Math.Max(0, target - progress);
		return true;
	}

	internal static string BuildQuestCounterKey(CardExtraEffect effect)
	{
		if (!string.IsNullOrWhiteSpace(effect?.EffectId))
		{
			return $"quest:{effect.EffectId.Trim()}";
		}

		return string.Join(
			":",
			"quest",
			effect?.QuestMode.ToString() ?? CardExtraEffectQuestMode.RunProgress.ToString(),
			effect?.CountEvent.ToString() ?? CardExtraEffectCountEvent.Played.ToString(),
			effect?.CountEnemyStatus.ToString() ?? CardExtraEffectEnemyStatus.AnyPowerStatus.ToString(),
			effect?.CountPowerId?.Trim() ?? string.Empty,
			effect?.CountCardPool.ToString() ?? CardGeneratedCardPool.Default.ToString(),
			effect?.CountCardType.ToString() ?? CardGeneratedCardType.Any.ToString(),
			effect?.CountCardRarity.ToString() ?? CardExtraEffectCardRarityFilter.Any.ToString(),
			effect?.CountCardFilter.ToString() ?? CardExtraEffectCountCardFilter.Any.ToString(),
			effect?.CardMatchMode.ToString() ?? CardExtraEffectCardMatchMode.Any.ToString(),
			effect?.MatchCardId?.Trim() ?? string.Empty,
			effect?.MatchTagKind.ToString() ?? CardExtraEffectCardMatchTagKind.Vanilla.ToString(),
			effect?.MatchVanillaTag.ToString() ?? CardTag.None.ToString(),
			effect?.MatchCustomTag?.Trim() ?? string.Empty,
			effect?.MatchCustomKeyword?.Trim() ?? string.Empty,
			effect?.NameFilterEnabled == true ? "name" : string.Empty,
			effect?.NameFilterText?.Trim() ?? string.Empty,
			effect?.CostFilterEnabled == true ? "cost" : string.Empty,
			effect?.CostFilterMode.ToString() ?? CardExtraEffectCostFilterMode.Exactly.ToString(),
			(effect?.CostFilterMax ?? 0).ToString(CultureInfo.InvariantCulture));
	}

	internal static async Task<int> CompleteQuest(Player owner, CardModel questCard, CardExtraEffect questEffect, CombatState? combatState = null)
	{
		if (owner == null || questCard == null || questEffect == null || questEffect.Kind != CardExtraEffectKind.Quest)
		{
			return 0;
		}

		string completionKey = BuildCompletionKey(owner, questCard, questEffect);
		lock (_completing)
		{
			if (!_completing.Add(completionKey))
			{
				return 0;
			}
		}

		try
		{
			List<CardExtraEffect> rewardEffects = ResolveQuestRewardEffects(
				questCard,
				questEffect,
				keepTriggers: questEffect.QuestRewardStyle != CardExtraEffectQuestRewardStyle.Instant);
			CardEditorRunCardCounterState.Clear(combatState, questCard, BuildQuestCounterKey(questEffect), QuestCountersArePersistent);
			int completionCount = CardEditorRunCardCounterState.Add(combatState, questCard, BuildQuestCompletionCounterKey(questEffect), 1, QuestCountersArePersistent);
			bool finalCompletion = IsFinalQuestCompletion(questEffect, completionCount);
			int rewardValue = await ExecuteQuestRewards(owner, rewardEffects, combatState, questCard, questEffect);
			if (finalCompletion)
			{
				PlayerCmd.CompleteQuest(questCard);
				await TryRemoveCompletedQuestDeckCards(owner, questCard);
			}
			return rewardValue;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Quest completion failed for {questCard.Id}: {ex}");
			return 0;
		}
		finally
		{
			lock (_completing)
			{
				_completing.Remove(completionKey);
			}

			// Quest completions rewrite persistent counters; make sure the batched store
			// hits disk at this milestone even mid-combat.
			CardEditorRunCardCounterState.FlushIfDirty();
		}
	}

	private static async Task TryRemoveCompletedQuestDeckCards(Player owner, CardModel questCard)
	{
		try
		{
			List<CardModel> deckCards = owner?.Deck?.Cards?
				.Where(card => IsSameRuntimeQuestCard(card, questCard))
				.ToList() ?? new List<CardModel>();
			foreach (CardModel deckCard in deckCards)
			{
				if (deckCard?.Pile?.Type == PileType.Deck)
				{
					await CardPileCmd.RemoveFromDeck(deckCard);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed to remove completed quest card {questCard.Id} from deck: {ex}");
		}
	}

	private static async Task<int> ExecuteQuestRewards(Player owner, IReadOnlyList<CardExtraEffect> rewardEffects, CombatState? combatState, CardModel questCard, CardExtraEffect questEffect)
	{
		if (owner == null || rewardEffects == null || rewardEffects.Count == 0)
		{
			return 0;
		}

		int visibleGold = 0;
		List<Reward> outOfCombatRewards = new();
		bool inCombat = combatState != null;
		foreach (CardExtraEffect rewardEffect in rewardEffects)
		{
			int amount = Math.Max(0, rewardEffect.Amount);
			if (amount <= 0 && rewardEffect.Kind is not CardExtraEffectKind.LoseGold)
			{
				continue;
			}

			// Persisted reward rows keep their recurring trigger instead of firing once; combat-scope
			// installs live for this combat only while run-scope installs are re-derived from the
			// persistent completion counters, so only the current combat needs an explicit install.
			if (questEffect.QuestRewardStyle != CardExtraEffectQuestRewardStyle.Instant
				&& IsRecurringRewardTrigger(rewardEffect.Trigger))
			{
				if (combatState != null)
				{
					CardEditorQuestPassiveStore.InstallCombat(combatState, owner, questCard, questEffect, rewardEffect);
				}
				continue;
			}

			if (inCombat)
			{
				await ExecuteQuestRewardInCombat(owner, combatState!, questCard, questEffect, rewardEffect);
				if (rewardEffect.Kind == CardExtraEffectKind.GainGold)
				{
					visibleGold += amount;
				}
				continue;
			}

			switch (rewardEffect.Kind)
			{
				case CardExtraEffectKind.GainGold:
					outOfCombatRewards.Add(new GoldReward(amount, owner));
					visibleGold += amount;
					break;
				case CardExtraEffectKind.LoseGold:
					await PlayerCmd.LoseGold(amount, owner);
					break;
				case CardExtraEffectKind.CreateRandomPotion:
					AddPotionRewards(owner, rewardEffect, amount, outOfCombatRewards);
					break;
				case CardExtraEffectKind.AddCardReward:
					AddCardReward(owner, rewardEffect, amount, outOfCombatRewards);
					break;
				case CardExtraEffectKind.AddRelicReward:
					AddRelicRewards(owner, amount, outOfCombatRewards);
					break;
				case CardExtraEffectKind.Heal:
					if (owner.Creature != null)
					{
						await CreatureCmd.Heal(owner.Creature, amount);
					}
					break;
				case CardExtraEffectKind.GainMaxHp:
					if (owner.Creature != null)
					{
						await CreatureCmd.GainMaxHp(owner.Creature, amount);
					}
					break;
				case CardExtraEffectKind.UpgradeDeckCards:
					await CardEditorExtraEffects.UpgradeDeckCards(owner, amount, rewardEffect);
					break;
				case CardExtraEffectKind.RemoveCardsFromDeck:
					await CardEditorExtraEffects.RemoveCardsFromDeck(new BlockingPlayerChoiceContext(), owner, amount, rewardEffect, questCard);
					break;
				case CardExtraEffectKind.TransformCards:
					await CardEditorExtraEffects.TransformCards(new BlockingPlayerChoiceContext(), owner, amount, rewardEffect, questCard);
					break;
				default:
					Log.Warn($"[CardEditor] Quest reward effect '{rewardEffect.Kind}' is not supported outside normal card play yet.");
					break;
			}
		}

		if (!inCombat && outOfCombatRewards.Count > 0)
		{
			await RewardsCmd.OfferCustom(owner, outOfCombatRewards);
		}

		return visibleGold;
	}

	private static async Task ExecuteQuestRewardInCombat(Player owner, CombatState combatState, CardModel questCard, CardExtraEffect questEffect, CardExtraEffect rewardEffect)
	{
		if (owner == null || combatState == null || questCard == null || rewardEffect == null)
		{
			return;
		}

		if (!CardEditorAutoPlayLoopGuard.TryEnterAutoPlayEffect(combatState, owner, questCard, questEffect, out IDisposable rewardScope))
		{
			return;
		}

		using (rewardScope)
		{
			CardPlay syntheticPlay = new CardPlay
			{
				Card = questCard,
				Player = questCard.Owner,
				Target = null,
				ResultPile = questCard.Pile?.Type ?? PileType.None,
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

			if (!CardEditorAutoPlayLoopGuard.ShouldSuppressEffect(questCard, rewardEffect))
			{
				await CardEditorExtraEffects.ExecuteEffect(combatState, new BlockingPlayerChoiceContext(), syntheticPlay, rewardEffect);
			}
		}
	}

	internal static async Task RunInstalledPassivesForCombatStart(CombatState combatState, PlayerChoiceContext? choiceContext)
	{
		_ = choiceContext;
		if (combatState == null)
		{
			return;
		}

		try
		{
			foreach (CardEditorQuestPassiveStore.InstalledPassive install in CardEditorQuestPassiveStore.GetActiveInstalls(combatState))
			{
				if (install?.RewardRow?.Trigger != CardExtraEffectTrigger.DeckPassiveCombatStart)
				{
					continue;
				}
				if (!CardEditorQuestPassiveStore.TryMarkCombatStartFired(combatState, install))
				{
					continue;
				}

				await FireInstalledPassive(combatState, install);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Installed quest passive combat-start dispatch failed: {ex}");
		}
	}

	internal static async Task RunInstalledPassivesForTurnBoundary(CombatState combatState, CardExtraEffectTurnBoundary edge, CardExtraEffectTurnBoundarySide side)
	{
		if (combatState == null)
		{
			return;
		}

		try
		{
			foreach (CardEditorQuestPassiveStore.InstalledPassive install in CardEditorQuestPassiveStore.GetActiveInstalls(combatState))
			{
				if (install?.RewardRow == null || !MatchesTurnBoundary(install.RewardRow, edge, side))
				{
					continue;
				}

				await FireInstalledPassive(combatState, install);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Installed quest passive turn-boundary dispatch failed: {ex}");
		}
	}

	private static bool MatchesTurnBoundary(CardExtraEffect rewardRow, CardExtraEffectTurnBoundary edge, CardExtraEffectTurnBoundarySide side)
	{
		return rewardRow.Trigger switch
		{
			CardExtraEffectTrigger.TurnBoundary => rewardRow.TurnBoundary == edge
				&& (rewardRow.TurnBoundarySide == CardExtraEffectTurnBoundarySide.Both || rewardRow.TurnBoundarySide == side),
			CardExtraEffectTrigger.StartOfTurn =>
				edge == CardExtraEffectTurnBoundary.Start && side == CardExtraEffectTurnBoundarySide.YourTurn,
			CardExtraEffectTrigger.EndOfTurn or CardExtraEffectTrigger.EndOfTurnInHand =>
				edge == CardExtraEffectTurnBoundary.End && side == CardExtraEffectTurnBoundarySide.YourTurn,
			CardExtraEffectTrigger.StartOfEnemyTurn =>
				edge == CardExtraEffectTurnBoundary.Start && side == CardExtraEffectTurnBoundarySide.EnemyTurn,
			CardExtraEffectTrigger.EndOfEnemyTurn =>
				edge == CardExtraEffectTurnBoundary.End && side == CardExtraEffectTurnBoundarySide.EnemyTurn,
			_ => false
		};
	}

	private static async Task FireInstalledPassive(CombatState combatState, CardEditorQuestPassiveStore.InstalledPassive install)
	{
		try
		{
			Player? owner = install.Owner ?? install.QuestCard?.Owner;
			CardModel? questCard = install.QuestCard;
			if (combatState == null || owner == null || questCard == null || install.QuestEffect == null || install.RewardRow == null)
			{
				return;
			}

			if (!CardEditorAutoPlayLoopGuard.TryEnterAutoPlayEffect(combatState, owner, questCard, install.QuestEffect, out IDisposable rewardScope))
			{
				return;
			}

			using (rewardScope)
			{
				if (CardEditorAutoPlayLoopGuard.ShouldSuppressEffect(questCard, install.RewardRow))
				{
					return;
				}

				CardPlay syntheticPlay = new CardPlay
				{
					Card = questCard,
					Player = questCard.Owner ?? owner,
					Target = null,
					ResultPile = questCard.Pile?.Type ?? PileType.None,
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

				// The installed row keeps its recurring trigger for matching; flatten a per-fire clone
				// so ExecuteEffect treats it as an immediate effect.
				CardExtraEffect fired = CardEditorExtraEffects.CloneEffect(install.RewardRow);
				fired.Trigger = CardExtraEffectTrigger.OnPlay;
				fired.Timing = CardExtraEffectTiming.Immediate;
				fired.Turns = 0;
				await CardEditorExtraEffects.ExecuteEffect(combatState, new BlockingPlayerChoiceContext(), syntheticPlay, fired);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Installed quest passive failed for {install?.QuestCard?.Id}: {ex}");
		}
	}

	private static void AddPotionRewards(Player owner, CardExtraEffect effect, int amount, List<Reward> rewards)
	{
		if (amount <= 0)
		{
			return;
		}

		if (effect.PotionMode == CardExtraEffectPotionMode.Specific)
		{
			PotionModel? potion = CardEditorExtraEffects.ResolveSpecificPotionModel(effect.SpecificPotionId);
			if (potion == null)
			{
				Log.Warn($"[CardEditor] Failed to create quest potion reward: '{effect.SpecificPotionId}' was not found.");
				return;
			}

			for (int i = 0; i < amount; i++)
			{
				rewards.Add(new PotionReward(potion.ToMutable(), owner));
			}
			return;
		}

		HashSet<ModelId> chosenIds = new();
		for (int i = 0; i < amount; i++)
		{
			// Honor the effect's rarity/pool filters out of combat too, like the in-combat path.
			PotionModel? potion = CardEditorExtraEffects.CreateFilteredPotionForReward(owner, effect, chosenIds);
			if (potion == null)
			{
				rewards.Add(new PotionReward(owner));
				continue;
			}

			rewards.Add(new PotionReward(potion.ToMutable(), owner));
		}
	}

	private static void AddRelicRewards(Player owner, int amount, List<Reward> rewards)
	{
		if (owner == null || amount <= 0)
		{
			return;
		}

		for (int i = 0; i < Math.Clamp(amount, 1, 99); i++)
		{
			rewards.Add(new RelicReward(owner));
		}
	}

	private static void AddCardReward(Player owner, CardExtraEffect effect, int optionCount, List<Reward> rewards)
	{
		if (optionCount <= 0)
		{
			return;
		}

		int count = Math.Clamp(optionCount, 1, 99);
		// Vanilla out-of-combat card rewards (Dream Catcher, Prayer Wheel) use ForRoom(Monster)
		// so the rare-pity timer applies and advances; RestSite maps to Source=Other which
		// rolls flat base odds and never touches the pity counter.
		CardCreationOptions options = CardEditorExtraEffects.BuildCardRewardCreationOptions(owner, RoomType.Monster, effect);
		CardReward reward = new(options, count, owner);
		CardEditorCardRewardSpecs.Attach(reward, CardEditorCardRewardSpecs.FromEffect(effect));
		rewards.Add(reward);
	}

	internal static List<CardExtraEffect> ResolveQuestRewardEffects(CardModel questCard, CardExtraEffect questEffect, bool keepTriggers = false)
	{
		List<string> selectedIds = ParseEffectIds(questEffect.AutoActionEffectIds);
		if (selectedIds.Count == 0)
		{
			return new List<CardExtraEffect>();
		}

		List<CardExtraEffect> rows = CardEditorExtraEffects.GetEffectsForDescription(questCard, isUpgradePreview: false).ToList();
		List<CardExtraEffect> result = new(selectedIds.Count);
		foreach (string id in selectedIds)
		{
			CardExtraEffect? row = rows.FirstOrDefault(effect => string.Equals(effect?.EffectId, id, StringComparison.Ordinal));
			if (row == null || row.Kind == CardExtraEffectKind.Quest)
			{
				continue;
			}

			CardExtraEffect clone = CardEditorExtraEffects.CloneEffect(row);
			clone.PayloadOnly = false;
			clone.AsPower = false;
			if (!keepTriggers || !IsRecurringRewardTrigger(clone.Trigger))
			{
				clone.Trigger = CardExtraEffectTrigger.OnPlay;
				clone.Timing = CardExtraEffectTiming.Immediate;
				clone.Turns = 0;
			}
			result.Add(clone);
		}

		return result;
	}

	internal static bool IsRecurringRewardTrigger(CardExtraEffectTrigger trigger)
	{
		return trigger is CardExtraEffectTrigger.DeckPassiveCombatStart
			or CardExtraEffectTrigger.TurnBoundary
			or CardExtraEffectTrigger.StartOfTurn
			or CardExtraEffectTrigger.EndOfTurn
			or CardExtraEffectTrigger.StartOfEnemyTurn
			or CardExtraEffectTrigger.EndOfEnemyTurn
			or CardExtraEffectTrigger.EndOfTurnInHand;
	}

	private static List<string> ParseEffectIds(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return new List<string>();
		}

		return text
			.Split(new[] { ';', ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(id => id.Trim())
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	private static IEnumerable<QuestRuntime> GetActiveQuests(IRunState? runState, CardExtraEffectQuestMode? mode = null)
	{
		if (runState?.Players == null)
		{
			yield break;
		}

		foreach (Player player in runState.Players)
		{
			foreach (QuestRuntime quest in GetActiveQuests(player, mode))
			{
				yield return quest;
			}
		}
	}

	private static IEnumerable<QuestRuntime> GetActiveQuests(Player? player, CardExtraEffectQuestMode? mode = null)
	{
		if (player?.Deck?.Cards == null)
		{
			yield break;
		}

		foreach (CardModel card in player.Deck.Cards.ToList())
		{
			if (card == null)
			{
				continue;
			}

			List<CardExtraEffect> effects = CardEditorExtraEffects.GetEffectsForDescription(card, isUpgradePreview: false).ToList();
			for (int i = 0; i < effects.Count; i++)
			{
				CardExtraEffect effect = effects[i];
				if (effect?.Kind != CardExtraEffectKind.Quest)
				{
					continue;
				}

				if (mode.HasValue && effect.QuestMode != mode.Value)
				{
					continue;
				}
				if (!IsQuestActiveForScope(player, card, effect))
				{
					continue;
				}

				yield return new QuestRuntime(player, card, effect, i);
			}
		}
	}

	private static bool IsQuestActiveForScope(Player player, CardModel card, CardExtraEffect effect)
	{
		if (player == null || card == null || effect == null)
		{
			return false;
		}

		return effect.QuestActiveScope switch
		{
			CardExtraEffectQuestActiveScope.AnyPile => card.Pile?.Type == PileType.Deck || HasCombatQuestInstance(player, card, null),
			CardExtraEffectQuestActiveScope.Hand => HasCombatQuestInstance(player, card, PileType.Hand),
			CardExtraEffectQuestActiveScope.DrawPile => HasCombatQuestInstance(player, card, PileType.Draw),
			CardExtraEffectQuestActiveScope.DiscardPile => HasCombatQuestInstance(player, card, PileType.Discard),
			CardExtraEffectQuestActiveScope.ExhaustPile => HasCombatQuestInstance(player, card, PileType.Exhaust),
			CardExtraEffectQuestActiveScope.CombatPiles => HasCombatQuestInstance(player, card, null),
			_ => card.Pile?.Type == PileType.Deck
		};
	}

	private static bool HasCombatQuestInstance(Player player, CardModel deckCard, PileType? pileType)
	{
		if (player?.PlayerCombatState?.AllCards == null || deckCard == null)
		{
			return false;
		}

		foreach (CardModel card in player.PlayerCombatState.AllCards)
		{
			if (card == null)
			{
				continue;
			}
			if (pileType.HasValue && card.Pile?.Type != pileType.Value)
			{
				continue;
			}
			if (IsSameRuntimeQuestCard(card, deckCard))
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasActiveQuest(IRunState? runState, CardExtraEffectQuestMode mode, int? actIndex = null)
	{
		return GetActiveQuests(runState, mode).Any(quest => MatchesAct(quest.Effect, runState, actIndex));
	}

	private static bool MatchesAct(CardExtraEffect effect, IRunState? runState, int? actIndex = null)
	{
		int configured = effect?.QuestActIndex ?? -1;
		if (configured < 0)
		{
			return true;
		}

		int current = actIndex ?? runState?.CurrentActIndex ?? -1;
		return current == configured;
	}

	private static int GetQuestTarget(CardExtraEffect? effect)
	{
		return Math.Max(1, effect?.Amount ?? 1);
	}

	private static bool IsSameDeckCard(CardModel playedCard, CardModel deckCard)
	{
		if (ReferenceEquals(playedCard, deckCard))
		{
			return true;
		}

		try
		{
			if (ReferenceEquals(playedCard.DeckVersion, deckCard))
			{
				return true;
			}
		}
		catch
		{
		}

		return playedCard.Id != null && playedCard.Id.Equals(deckCard.Id);
	}

	private static bool IsSameRuntimeQuestCard(CardModel? candidate, CardModel? questCard)
	{
		if (candidate == null || questCard == null)
		{
			return false;
		}
		if (ReferenceEquals(candidate, questCard))
		{
			return true;
		}

		try
		{
			if (ReferenceEquals(candidate.DeckVersion, questCard)
				|| ReferenceEquals(questCard.DeckVersion, candidate)
				|| (candidate.DeckVersion != null && ReferenceEquals(candidate.DeckVersion, questCard.DeckVersion)))
			{
				return true;
			}
		}
		catch
		{
		}

		return false;
	}

	internal static string BuildQuestCompletionCounterKey(CardExtraEffect effect)
	{
		return $"{BuildQuestCounterKey(effect)}:completed";
	}

	private static bool IsFinalQuestCompletion(CardExtraEffect effect, int completionCount)
	{
		int limit = Math.Clamp(effect?.QuestCompletionLimit ?? 1, 0, 999);
		return limit > 0 && completionCount >= limit;
	}

	private static string BuildCompletionKey(Player owner, CardModel questCard, CardExtraEffect questEffect)
	{
		return string.Join(
			":",
			owner.NetId.ToString(CultureInfo.InvariantCulture),
			questCard.Id?.ToString() ?? RuntimeHelpers.GetHashCode(questCard).ToString(CultureInfo.InvariantCulture),
			RuntimeHelpers.GetHashCode(questCard).ToString(CultureInfo.InvariantCulture),
			questEffect.EffectId?.Trim() ?? RuntimeHelpers.GetHashCode(questEffect).ToString(CultureInfo.InvariantCulture),
			RuntimeHelpers.GetHashCode(questEffect).ToString(CultureInfo.InvariantCulture),
			questEffect.QuestMode.ToString());
	}

	private static string BuildOptionId(CardModel questCard, CardExtraEffect questEffect, int index)
	{
		string raw = $"{questCard.Id}_{questEffect.EffectId ?? index.ToString(CultureInfo.InvariantCulture)}";
		string sanitized = _unsafeOptionIdChars.Replace(raw.ToUpperInvariant(), "_").Trim('_');
		return string.IsNullOrWhiteSpace(sanitized)
			? "CARD_EDITOR_QUEST"
			: $"CARD_EDITOR_QUEST_{sanitized}";
	}

	private static bool TryResolveEvent(string? text, out EventModel eventModel)
	{
		eventModel = null!;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		string trimmed = text.Trim();
		foreach (ModelId candidate in BuildEventIdCandidates(trimmed))
		{
			EventModel? resolved = ModelDb.GetByIdOrNull<EventModel>(candidate);
			if (resolved != null)
			{
				eventModel = resolved;
				return true;
			}
		}

		string slug = StringHelper.Slugify(trimmed);
		foreach (EventModel candidate in ModelDb.AllEvents.Concat<EventModel>(ModelDb.AllAncients))
		{
			if (candidate == null)
			{
				continue;
			}

			if (string.Equals(candidate.Id.ToString(), trimmed, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(candidate.Id.Entry, trimmed, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(candidate.Id.Entry, slug, StringComparison.OrdinalIgnoreCase))
			{
				eventModel = candidate;
				return true;
			}

			try
			{
				if (string.Equals(candidate.Title?.GetFormattedText(), trimmed, StringComparison.OrdinalIgnoreCase))
				{
					eventModel = candidate;
					return true;
				}
			}
			catch
			{
			}
		}

		return false;
	}

	private static IEnumerable<ModelId> BuildEventIdCandidates(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			yield break;
		}

		if (text.Contains('.', StringComparison.Ordinal))
		{
			ModelId parsed = ModelId.none;
			try
			{
				parsed = ModelId.Deserialize(text);
			}
			catch
			{
			}

			if (parsed != ModelId.none)
			{
				yield return parsed;
			}
		}

		string slug = StringHelper.Slugify(text);
		string category = ModelDb.GetCategory(typeof(EventModel));
		yield return new ModelId(category, slug);
		yield return new ModelId(category, slug.ToUpperInvariant());
		yield return new ModelId("EVENT", slug);
		yield return new ModelId("EVENT", slug.ToUpperInvariant());
	}

	private static bool TryGetQuestIcon(CardModel questCard, out Texture2D texture)
	{
		if (CardEditorCustomPortraitLoader.TryGetPortrait(questCard, out texture))
		{
			return true;
		}

		texture = null!;
		string path = string.Empty;
		try
		{
			path = questCard.PortraitPath;
		}
		catch
		{
		}

		if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
		{
			return false;
		}

		texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
		return texture != null;
	}

	private static string GetCardTitle(CardModel card)
	{
		try
		{
			string? title = card?.Title?.ToString();
			if (!string.IsNullOrWhiteSpace(title))
			{
				return title.Trim();
			}
		}
		catch
		{
		}

		return card?.Id?.Entry ?? "Quest";
	}

	private readonly record struct QuestRuntime(Player Owner, CardModel Card, CardExtraEffect Effect, int Index);

	private sealed class CardEditorQuestRestSiteOption : RestSiteOption
	{
		public CardModel QuestCard { get; }
		public CardExtraEffect QuestEffect { get; }
		public string TitleText { get; }
		public string DescriptionText { get; }
		public Texture2D? IconTexture { get; }
		public override string OptionId { get; }
		public override IEnumerable<string> AssetPaths => Array.Empty<string>();

		public CardEditorQuestRestSiteOption(Player owner, CardModel questCard, CardExtraEffect questEffect, int index)
			: base(owner)
		{
			QuestCard = questCard;
			QuestEffect = questEffect;
			TitleText = GetCardTitle(questCard);
			DescriptionText = $"Complete quest: {TitleText}.";
			OptionId = BuildOptionId(questCard, questEffect, index);
			if (TryGetQuestIcon(questCard, out Texture2D texture))
			{
				IconTexture = texture;
			}
		}

		public override async Task<bool> OnSelect()
		{
			await CompleteQuest(QuestCard.Owner, QuestCard, QuestEffect);
			return true;
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRestSiteOptions))]
	private static class Hook_ModifyRestSiteOptions_QuestPatch
	{
		public static void Postfix(IRunState runState, Player player, ICollection<RestSiteOption> options)
		{
			if (player == null || options == null)
			{
				return;
			}

			foreach (QuestRuntime quest in GetActiveQuests(player, CardExtraEffectQuestMode.RestSiteOption))
			{
				CardEditorQuestRestSiteOption option = new(player, quest.Card, quest.Effect, quest.Index);
				if (options.Any(existing => existing != null && existing.OptionId == option.OptionId))
				{
					continue;
				}

				options.Add(option);
			}
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyUnknownMapPointRoomTypes))]
	private static class Hook_ModifyUnknownMapPointRoomTypes_QuestPatch
	{
		public static void Postfix(IRunState runState, ref IReadOnlySet<RoomType> __result)
		{
			if (HasActiveQuest(runState, CardExtraEffectQuestMode.ForceNextEvent))
			{
				__result = new HashSet<RoomType> { RoomType.Event };
			}
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
	private static class Hook_ModifyNextEvent_QuestPatch
	{
		public static void Postfix(IRunState runState, ref EventModel __result)
		{
			foreach (QuestRuntime quest in GetActiveQuests(runState, CardExtraEffectQuestMode.ForceNextEvent))
			{
				if (!MatchesAct(quest.Effect, runState)
					|| !TryResolveEvent(quest.Effect.QuestEventId, out EventModel eventModel))
				{
					continue;
				}

				__result = eventModel;
				return;
			}
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMap))]
	private static class Hook_ModifyGeneratedMap_QuestPatch
	{
		public static void Postfix(IRunState runState, ActMap map, int actIndex, ref ActMap __result)
		{
			if (HasActiveQuest(runState, CardExtraEffectQuestMode.SpoilsMap, actIndex))
			{
				if (TryCreateSpoilsActMap(runState, out ActMap? spoilsMap))
				{
					__result = spoilsMap;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMapLate))]
	private static class Hook_ModifyGeneratedMapLate_QuestPatch
	{
		public static void Postfix(IRunState runState, ActMap map, int actIndex, ref ActMap __result)
		{
			if (__result is SpoilsActMap)
			{
				return;
			}

			if (HasActiveQuest(runState, CardExtraEffectQuestMode.SpoilsMap, actIndex))
			{
				if (TryCreateSpoilsActMap(runState, out ActMap? spoilsMap))
				{
					__result = spoilsMap;
				}
			}
		}
	}

	private static bool TryCreateSpoilsActMap(IRunState runState, [NotNullWhen(true)] out ActMap? spoilsMap)
	{
		spoilsMap = null;
		if (runState == null)
		{
			return false;
		}

		try
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			Type spoilsMapType = typeof(SpoilsActMap);
			ConstructorInfo? constructor = spoilsMapType.GetConstructor(
				flags,
				binder: null,
				types: new[] { typeof(IRunState), typeof(MapPointTypeCounts) },
				modifiers: null);

			if (constructor != null)
			{
				spoilsMap = constructor.Invoke(new object?[] { runState, null }) as ActMap;
				return spoilsMap != null;
			}

			constructor = spoilsMapType.GetConstructor(
				flags,
				binder: null,
				types: new[] { typeof(IRunState) },
				modifiers: null);

			if (constructor != null)
			{
				spoilsMap = constructor.Invoke(new object?[] { runState }) as ActMap;
				return spoilsMap != null;
			}

			Log.Warn("[CardEditor] SpoilsMap quest skipped: compatible SpoilsActMap constructor was not found.");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] SpoilsMap quest skipped: failed creating SpoilsActMap: {ex}");
		}

		return false;
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.AfterMapGenerated))]
	private static class Hook_AfterMapGenerated_QuestPatch
	{
		public static void Postfix(IRunState runState, ActMap map, int actIndex, ref Task __result)
		{
			__result = RunAfter(__result ?? Task.CompletedTask, runState, map, actIndex);
		}

		private static async Task RunAfter(Task original, IRunState runState, ActMap map, int actIndex)
		{
			await original;
			if (runState == null || map == null)
			{
				return;
			}

			MapPoint? treasure = map.GetAllMapPoints().FirstOrDefault(point => point?.PointType == MapPointType.Treasure);
			if (treasure == null)
			{
				return;
			}

			foreach (QuestRuntime quest in GetActiveQuests(runState, CardExtraEffectQuestMode.SpoilsMap))
			{
				if (!MatchesAct(quest.Effect, runState, actIndex)
					|| treasure.Quests.Any(model => ReferenceEquals(model, quest.Card)))
				{
					continue;
				}

				treasure.AddQuest(quest.Card);
			}
		}
	}

	[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardRemoved))]
	private static class Hook_BeforeCardRemoved_QuestPatch
	{
		public static void Postfix(IRunState runState, CardModel card, ref Task __result)
		{
			__result = RunAfter(__result ?? Task.CompletedTask, runState, card);
		}

		private static async Task RunAfter(Task original, IRunState runState, CardModel card)
		{
			await original;
			if (runState?.Map == null || card == null)
			{
				return;
			}

			bool isSpoilsQuest = CardEditorExtraEffects
				.GetEffectsForDescription(card, isUpgradePreview: false)
				.Any(effect => effect?.Kind == CardExtraEffectKind.Quest && effect.QuestMode == CardExtraEffectQuestMode.SpoilsMap);
			if (!isSpoilsQuest)
			{
				return;
			}

			foreach (MapPoint point in runState.Map.GetAllMapPoints())
			{
				point?.RemoveQuest(card);
			}
		}
	}

	[HarmonyPatch]
	private static class OneOffSynchronizer_TryHandleSpoilsMap_QuestPatch
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(OneOffSynchronizer), "TryHandleSpoilsMap");
		}

		public static void Postfix(Player player, ref Task<int> __result)
		{
			__result = RunAfter(__result ?? Task.FromResult(0), player);
		}

		private static async Task<int> RunAfter(Task<int> original, Player player)
		{
			int gold = await original;
			try
			{
				gold += await TryHandleCustomSpoilsMap(player);
			}
			catch (Exception ex)
			{
				Log.Warn($"[CardEditor] Custom SpoilsMap quest completion failed: {ex}");
			}

			return gold;
		}

		private static async Task<int> TryHandleCustomSpoilsMap(Player player)
		{
			MapPoint? mapPoint = player?.RunState?.CurrentMapCoord.HasValue == true
				? player.RunState.Map.GetPoint(player.RunState.CurrentMapCoord.Value)
				: null;
			if (player == null || mapPoint == null || mapPoint.Quests.Count == 0)
			{
				return 0;
			}

			int gold = 0;
			foreach (QuestRuntime quest in GetActiveQuests(player, CardExtraEffectQuestMode.SpoilsMap).ToList())
			{
				if (!mapPoint.Quests.Any(model => ReferenceEquals(model, quest.Card)))
				{
					continue;
				}

				gold += await CompleteQuest(player, quest.Card, quest.Effect);
			}

			return gold;
		}
	}

	[HarmonyPatch(typeof(NRestSiteButton), "Reload")]
	private static class NRestSiteButton_Reload_QuestPatch
	{
		private static readonly FieldInfo OptionField = AccessTools.Field(typeof(NRestSiteButton), "_option");
		private static readonly FieldInfo IconField = AccessTools.Field(typeof(NRestSiteButton), "_icon");
		private static readonly FieldInfo LabelField = AccessTools.Field(typeof(NRestSiteButton), "_label");
		private static readonly FieldInfo HsvField = AccessTools.Field(typeof(NRestSiteButton), "_hsv");
		private static readonly StringName S = new("s");
		private static readonly StringName V = new("v");

		public static bool Prefix(NRestSiteButton __instance)
		{
			if (OptionField.GetValue(__instance) is not CardEditorQuestRestSiteOption option)
			{
				return true;
			}

			if (!__instance.IsNodeReady())
			{
				return false;
			}

			if (IconField.GetValue(__instance) is TextureRect icon)
			{
				icon.Texture = option.IconTexture;
			}

			if (LabelField.GetValue(__instance) is MegaLabel label)
			{
				label.SetTextAutoSize(option.TitleText);
			}

			if (HsvField.GetValue(__instance) is ShaderMaterial hsv)
			{
				hsv.SetShaderParameter(S, option.IsEnabled ? 1f : 0f);
				hsv.SetShaderParameter(V, option.IsEnabled ? 1f : 0.6f);
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton.RefreshTextState))]
	private static class NRestSiteButton_RefreshTextState_QuestPatch
	{
		private static readonly FieldInfo OptionField = AccessTools.Field(typeof(NRestSiteButton), "_option");
		private static readonly FieldInfo ExecutingOptionField = AccessTools.Field(typeof(NRestSiteButton), "_executingOption");
		private static readonly FieldInfo? IsFocusedField = AccessTools.Field(typeof(NClickableControl), "<IsFocused>k__BackingField");

		public static bool Prefix(NRestSiteButton __instance)
		{
			if (OptionField.GetValue(__instance) is not CardEditorQuestRestSiteOption option)
			{
				return true;
			}

			NRestSiteRoom? instance = NRestSiteRoom.Instance;
			if (instance == null)
			{
				return false;
			}

			bool executing = ExecutingOptionField.GetValue(__instance) is true;
			bool focused = IsFocusedField?.GetValue(__instance) is true;
			if (focused || executing)
			{
				instance.SetText(option.DescriptionText);
			}
			else
			{
				instance.FadeOutOptionDescription();
			}

			return false;
		}
	}
}
