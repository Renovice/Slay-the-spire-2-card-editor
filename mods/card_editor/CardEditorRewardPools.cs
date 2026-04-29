using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorRewardPoolDefinition
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Group { get; init; }
	public CardRarity? RarityHint { get; init; }
	public HashSet<ModelId> DirectTemplateIds { get; init; } = new();
	public Func<Player, CardCreationOptions, int, IEnumerable<CardModel>, bool>? MatchOptions { get; init; }
}

internal static class CardEditorRewardPoolRegistry
{
	private static readonly IReadOnlyList<CardEditorRewardPoolDefinition> _all = BuildDefinitions();
	private static readonly Dictionary<string, CardEditorRewardPoolDefinition> _byId =
		_all.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

	public static IReadOnlyList<CardEditorRewardPoolDefinition> All => _all;

	public static bool IsKnownPoolId(string? id)
		=> !string.IsNullOrWhiteSpace(id) && _byId.ContainsKey(id.Trim());

	public static IReadOnlyList<CardEditorRewardPoolDefinition> GetDefinitions(IEnumerable<string> ids)
	{
		List<CardEditorRewardPoolDefinition> result = new();
		foreach (string id in ids)
		{
			if (_byId.TryGetValue(id, out CardEditorRewardPoolDefinition? definition))
			{
				result.Add(definition);
			}
		}
		return result;
	}

	public static bool TryChooseDirectReplacement(CardModel canonicalCard, Player owner, out CardModel replacement)
	{
		replacement = null!;
		if (canonicalCard == null || owner == null || CardEditorCreatedCardsStore.IsCreatedCardId(canonicalCard.Id))
		{
			return false;
		}

		List<CardEditorRewardPoolDefinition> matchingPools = _all
			.Where(pool => pool.DirectTemplateIds.Contains(canonicalCard.Id))
			.ToList();
		if (matchingPools.Count == 0)
		{
			return false;
		}

		List<RewardPoolCandidate> candidates = GetEnabledCandidates(matchingPools.Select(p => p.Id), owner, canonicalCard.Rarity)
			.Where(c => c.Template.Id != canonicalCard.Id)
			.ToList();
		if (candidates.Count == 0)
		{
			return false;
		}

		CardEditorRewardPoolInjectionMode mode = GetDominantMode(candidates);
		bool shouldReplace = mode != CardEditorRewardPoolInjectionMode.AddToPool || RollReplacement(owner.PlayerRng.Rewards, candidates.Count, matchingPools.Sum(p => Math.Max(1, p.DirectTemplateIds.Count)));
		if (!shouldReplace)
		{
			return false;
		}

		replacement = ChooseCandidate(candidates, owner.PlayerRng.Rewards).Template;
		Log.Info($"[CardEditor][RewardPools] Direct event reward replaced {canonicalCard.Id} with {replacement.Id}");
		return true;
	}

	public static void ApplyToGeneratedCardReward(Player player, CardCreationOptions options, int optionCount, List<CardCreationResult> cards)
	{
		if (player == null || options == null || cards == null || cards.Count == 0)
		{
			return;
		}

		List<CardModel> possibleCards;
		try
		{
			possibleCards = options.GetPossibleCards(player).Where(c => c != null).ToList();
		}
		catch
		{
			return;
		}

		List<CardEditorRewardPoolDefinition> matchingPools = _all
			.Where(pool => pool.MatchOptions?.Invoke(player, options, optionCount, possibleCards) == true)
			.ToList();
		if (matchingPools.Count == 0)
		{
			return;
		}

		HashSet<ModelId> existingIds = cards.Select(c => c.Card.Id).ToHashSet();
		if (existingIds.Any(CardEditorCreatedCardsStore.IsCreatedCardId))
		{
			return;
		}

		CardRarity? selectedRarity = InferRewardRarity(cards, possibleCards, options);
		List<RewardPoolCandidate> candidates = GetEnabledCandidates(matchingPools.Select(p => p.Id), player, selectedRarity)
			.Where(c => !existingIds.Contains(c.Template.Id))
			.ToList();
		if (candidates.Count == 0)
		{
			return;
		}

		CardEditorRewardPoolInjectionMode mode = GetDominantMode(candidates);
		Rng rng = options.RngOverride ?? player.PlayerRng.Rewards;
		int originalPoolSize = Math.Max(cards.Count, possibleCards.Count);
		if (mode == CardEditorRewardPoolInjectionMode.AddToPool && !RollReplacement(rng, candidates.Count, originalPoolSize))
		{
			return;
		}

		if (mode == CardEditorRewardPoolInjectionMode.ReplacePool)
		{
			cards.Clear();
			int count = Math.Max(1, optionCount);
			for (int i = 0; i < count && candidates.Count > 0; i++)
			{
				RewardPoolCandidate chosen = ChooseCandidate(candidates, rng);
				cards.Add(new CardCreationResult(player.RunState.CreateCard(chosen.Template, player)));
				candidates.Remove(chosen);
			}
			Log.Info($"[CardEditor][RewardPools] Replaced reward pool with {cards.Count} custom card(s) for {string.Join(",", matchingPools.Select(p => p.Id))}");
			return;
		}

		int replaceIndex = rng.NextInt(cards.Count);
		RewardPoolCandidate candidate = ChooseCandidate(candidates, rng);
		cards[replaceIndex] = new CardCreationResult(player.RunState.CreateCard(candidate.Template, player));
		Log.Info($"[CardEditor][RewardPools] Injected {candidate.Template.Id} into reward pool {string.Join(",", matchingPools.Select(p => p.Id))}");
	}

	private static List<RewardPoolCandidate> GetEnabledCandidates(IEnumerable<string> poolIds, Player owner, CardRarity? selectedRarity)
	{
		HashSet<string> requestedPoolIds = poolIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
		List<RewardPoolCandidate> result = new();
		foreach (ModelId id in CardEditorCreatedCardsStore.GetAllCreatedCardIds())
		{
			if (!CardEditorCreatedCardsStore.IsEnabled(id) || !CardEditorCreatedCardsStore.AreCustomRewardPoolsEnabled(id))
			{
				continue;
			}

			IReadOnlyList<string> configuredPools = CardEditorCreatedCardsStore.GetCustomRewardPoolIds(id);
			if (!configuredPools.Any(requestedPoolIds.Contains))
			{
				continue;
			}

			CardModel? template = ModelDb.GetByIdOrNull<CardModel>(id);
			if (template == null)
			{
				continue;
			}

			CardEditorRewardPoolBucket bucket = CardEditorCreatedCardsStore.GetRewardPoolBucket(id);
			if (!BucketMatches(template, bucket, selectedRarity))
			{
				continue;
			}

			result.Add(new RewardPoolCandidate(template, bucket, CardEditorCreatedCardsStore.GetRewardPoolInjectionMode(id)));
		}
		return result;
	}

	private static bool BucketMatches(CardModel template, CardEditorRewardPoolBucket bucket, CardRarity? selectedRarity)
	{
		CardRarity effective = bucket switch
		{
			CardEditorRewardPoolBucket.Common => CardRarity.Common,
			CardEditorRewardPoolBucket.Uncommon => CardRarity.Uncommon,
			CardEditorRewardPoolBucket.Rare => CardRarity.Rare,
			CardEditorRewardPoolBucket.Event => CardRarity.Event,
			_ => template.Rarity
		};

		return selectedRarity == null || selectedRarity == CardRarity.None || selectedRarity == effective || template.Rarity == CardRarity.Event;
	}

	private static RewardPoolCandidate ChooseCandidate(List<RewardPoolCandidate> candidates, Rng rng)
		=> candidates[Math.Clamp(rng.NextInt(candidates.Count), 0, candidates.Count - 1)];

	private static CardEditorRewardPoolInjectionMode GetDominantMode(IEnumerable<RewardPoolCandidate> candidates)
	{
		if (candidates.Any(c => c.Mode == CardEditorRewardPoolInjectionMode.ReplacePool))
		{
			return CardEditorRewardPoolInjectionMode.ReplacePool;
		}
		if (candidates.Any(c => c.Mode == CardEditorRewardPoolInjectionMode.ForceInclude))
		{
			return CardEditorRewardPoolInjectionMode.ForceInclude;
		}
		return CardEditorRewardPoolInjectionMode.AddToPool;
	}

	private static bool RollReplacement(Rng rng, int customCount, int originalCount)
	{
		int total = Math.Max(1, customCount + Math.Max(1, originalCount));
		return rng.NextInt(total) < customCount;
	}

	private static CardRarity? InferRewardRarity(List<CardCreationResult> generated, List<CardModel> possibleCards, CardCreationOptions options)
	{
		CardModel? first = generated.FirstOrDefault()?.Card;
		if (first != null)
		{
			return first.Rarity;
		}
		if (options.RarityOdds == CardRarityOddsType.Uniform)
		{
			CardModel? possible = possibleCards.FirstOrDefault();
			if (possible != null && possibleCards.All(c => c.Rarity == possible.Rarity))
			{
				return possible.Rarity;
			}
		}
		return null;
	}

	private static IReadOnlyList<CardEditorRewardPoolDefinition> BuildDefinitions()
	{
		bool IsPool<TPool>(CardCreationOptions options) where TPool : CardPoolModel
			=> options.CardPools.Any(p => p is TPool);

		bool IsOwnerPool(Player player, CardCreationOptions options)
			=> options.CardPools.Any(p => p == player.Character.CardPool || p.Id == player.Character.CardPool.Id);

		bool IsSingleRarity(IEnumerable<CardModel> cards, CardRarity rarity)
			=> cards.Any() && cards.All(c => c.Rarity == rarity);

		bool AllCardsAreType(IEnumerable<CardModel> cards, CardType type)
			=> cards.Any() && cards.All(c => c.Type == type);

		return new List<CardEditorRewardPoolDefinition>
		{
			new()
			{
				Id = "TRASH_HEAP.EVENT_CARDS",
				Title = "Trash Heap - Random Event Card",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Caltrops, Clash, Distraction, DualWield, Entrench, HelloWorld, Outmaneuver, Rebound, RipAndTear, Stack>()
			},
			new()
			{
				Id = "BUGSLAYER.EXTERMINATE",
				Title = "Bugslayer - Exterminate Option",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Exterminate>()
			},
			new()
			{
				Id = "BUGSLAYER.SQUASH",
				Title = "Bugslayer - Squash Option",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Squash>()
			},
			new()
			{
				Id = "SPIRIT_GRAFTER.METAMORPHOSIS",
				Title = "Spirit Grafter - Metamorphosis",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Metamorphosis>()
			},
			new()
			{
				Id = "ZEN_WEAVER.ENLIGHTENMENT",
				Title = "Zen Weaver - Enlightenment",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Enlightenment>()
			},
			new()
			{
				Id = "ENDLESS_CONVEYOR.FEEDING_FRENZY",
				Title = "Endless Conveyor - Feeding Frenzy",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<FeedingFrenzy>()
			},
			new()
			{
				Id = "WOOD_CARVINGS.BIRD",
				Title = "Wood Carvings - Bird Card",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<Peck>()
			},
			new()
			{
				Id = "WOOD_CARVINGS.TORUS",
				Title = "Wood Carvings - Torus Card",
				Group = "Direct Event Cards",
				RarityHint = CardRarity.Event,
				DirectTemplateIds = Ids<ToricToughness>()
			},
			new()
			{
				Id = "BRAIN_LEECH.COLORLESS",
				Title = "Brain Leech - Colorless Card Reward",
				Group = "Card Reward Screens",
				MatchOptions = (_, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.RegularEncounter
					&& options.CardPoolFilter == null
					&& IsPool<ColorlessCardPool>(options)
			},
			new()
			{
				Id = "COLORFUL_PHILOSOPHERS.COMMON",
				Title = "Colorful Philosophers - Common Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Common,
				MatchOptions = (_, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.Flags.HasFlag(CardCreationFlags.NoRarityModification)
					&& !options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsSingleRarity(cards, CardRarity.Common)
			},
			new()
			{
				Id = "COLORFUL_PHILOSOPHERS.UNCOMMON",
				Title = "Colorful Philosophers - Uncommon Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Uncommon,
				MatchOptions = (_, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.Flags.HasFlag(CardCreationFlags.NoRarityModification)
					&& !options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsSingleRarity(cards, CardRarity.Uncommon)
			},
			new()
			{
				Id = "COLORFUL_PHILOSOPHERS.RARE",
				Title = "Colorful Philosophers - Rare Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Rare,
				MatchOptions = (_, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.Flags.HasFlag(CardCreationFlags.NoRarityModification)
					&& !options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsSingleRarity(cards, CardRarity.Rare)
			},
			new()
			{
				Id = "CRYSTAL_SPHERE.COMMON",
				Title = "Crystal Sphere - Common Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Common,
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.RngOverride != null
					&& IsOwnerPool(player, options)
					&& IsSingleRarity(cards, CardRarity.Common)
			},
			new()
			{
				Id = "CRYSTAL_SPHERE.UNCOMMON",
				Title = "Crystal Sphere - Uncommon Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Uncommon,
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.RngOverride != null
					&& IsOwnerPool(player, options)
					&& IsSingleRarity(cards, CardRarity.Uncommon)
			},
			new()
			{
				Id = "CRYSTAL_SPHERE.RARE",
				Title = "Crystal Sphere - Rare Card Reward",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Rare,
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.RngOverride != null
					&& IsOwnerPool(player, options)
					&& IsSingleRarity(cards, CardRarity.Rare)
			},
			new()
			{
				Id = "ROOM_FULL_OF_CHEESE.COMMON_GRID",
				Title = "Room Full of Cheese - Common Card Grid",
				Group = "Card Reward Screens",
				RarityHint = CardRarity.Common,
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 8
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.Flags.HasFlag(CardCreationFlags.NoRarityModification)
					&& options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsOwnerPool(player, options)
					&& IsSingleRarity(cards, CardRarity.Common)
			},
			new()
			{
				Id = "INFESTED_AUTOMATON.POWER",
				Title = "Infested Automaton - Power Reward",
				Group = "Card Reward Screens",
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 1
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.RegularEncounter
					&& options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsOwnerPool(player, options)
					&& AllCardsAreType(cards, CardType.Power)
			},
			new()
			{
				Id = "THE_FUTURE_OF_POTIONS.TYPED",
				Title = "The Future of Potions - Typed Card Reward",
				Group = "Card Reward Screens",
				MatchOptions = (player, options, optionCount, cards) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.Uniform
					&& options.Flags.HasFlag(CardCreationFlags.NoRarityModification)
					&& options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& IsOwnerPool(player, options)
					&& cards.Any()
					&& cards.Select(c => c.Type).Distinct().Count() == 1
					&& cards.Select(c => c.Rarity).Distinct().Count() == 1
			},
			new()
			{
				Id = "TRIAL.CARD_REWARD",
				Title = "Trial - Card Reward",
				Group = "Card Reward Screens",
				MatchOptions = (player, options, optionCount, _) =>
					optionCount == 3
					&& options.Source == CardCreationSource.Other
					&& options.RarityOdds == CardRarityOddsType.RegularEncounter
					&& options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll)
					&& options.CardPoolFilter == null
					&& IsOwnerPool(player, options)
			}
		};
	}

	private static HashSet<ModelId> Ids<T>() where T : CardModel
		=> new() { ModelDb.Card<T>().Id };

	private static HashSet<ModelId> Ids<T1, T2>() where T1 : CardModel where T2 : CardModel
		=> new() { ModelDb.Card<T1>().Id, ModelDb.Card<T2>().Id };

	private static HashSet<ModelId> Ids<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
		where T1 : CardModel where T2 : CardModel where T3 : CardModel where T4 : CardModel where T5 : CardModel
		where T6 : CardModel where T7 : CardModel where T8 : CardModel where T9 : CardModel where T10 : CardModel
		=> new()
		{
			ModelDb.Card<T1>().Id,
			ModelDb.Card<T2>().Id,
			ModelDb.Card<T3>().Id,
			ModelDb.Card<T4>().Id,
			ModelDb.Card<T5>().Id,
			ModelDb.Card<T6>().Id,
			ModelDb.Card<T7>().Id,
			ModelDb.Card<T8>().Id,
			ModelDb.Card<T9>().Id,
			ModelDb.Card<T10>().Id
		};

	private sealed record RewardPoolCandidate(CardModel Template, CardEditorRewardPoolBucket Bucket, CardEditorRewardPoolInjectionMode Mode);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Runs.RunState), nameof(MegaCrit.Sts2.Core.Runs.RunState.CreateCard), typeof(CardModel), typeof(Player))]
internal static class RunState_CreateCard_CardEditorRewardPools_Patch
{
	public static void Prefix(ref CardModel canonicalCard, Player owner)
	{
		try
		{
			if (CardEditorRewardPoolRegistry.TryChooseDirectReplacement(canonicalCard, owner, out CardModel replacement))
			{
				canonicalCard = replacement;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RewardPools] Direct replacement failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.Populate))]
internal static class CardReward_Populate_CardEditorRewardPools_Patch
{
	private static readonly System.Reflection.FieldInfo? CardsField = AccessTools.Field(typeof(CardReward), "_cards");
	private static readonly System.Reflection.FieldInfo? OptionsField = AccessTools.Field(typeof(CardReward), "<Options>k__BackingField");
	private static readonly System.Reflection.FieldInfo? OptionCountField = AccessTools.Field(typeof(CardReward), "<OptionCount>k__BackingField");

	public static void Postfix(CardReward __instance)
	{
		try
		{
			if (CardsField?.GetValue(__instance) is not List<CardCreationResult> cards
				|| OptionsField?.GetValue(__instance) is not CardCreationOptions options
				|| OptionCountField?.GetValue(__instance) is not int optionCount)
			{
				return;
			}

			CardEditorRewardPoolRegistry.ApplyToGeneratedCardReward(__instance.Player, options, optionCount, cards);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RewardPools] CardReward injection failed: {ex}");
		}
	}
}
