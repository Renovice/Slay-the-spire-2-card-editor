using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

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
	internal const string TinkerTimeTypeAttackPoolId = "TINKER_TIME.TYPE_ATTACK";
	internal const string TinkerTimeTypeSkillPoolId = "TINKER_TIME.TYPE_SKILL";
	internal const string TinkerTimeTypePowerPoolId = "TINKER_TIME.TYPE_POWER";
	internal const string TinkerTimeRiderAttackPoolId = "TINKER_TIME.RIDER_ATTACK";
	internal const string TinkerTimeRiderSkillPoolId = "TINKER_TIME.RIDER_SKILL";
	internal const string TinkerTimeRiderPowerPoolId = "TINKER_TIME.RIDER_POWER";

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

	internal static IReadOnlyList<CardEditorRewardPoolTemplateCandidate> GetEnabledTemplateCandidates(
		IEnumerable<string> poolIds,
		Player owner,
		CardRarity? selectedRarity = null)
	{
		if (owner == null)
		{
			return Array.Empty<CardEditorRewardPoolTemplateCandidate>();
		}

		return GetEnabledCandidates(poolIds, owner, selectedRarity)
			.Select(c => new CardEditorRewardPoolTemplateCandidate(c.Template, c.Mode))
			.ToList();
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
				Id = TinkerTimeTypeAttackPoolId,
				Title = "Tinker Time - Attack Base Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
			new()
			{
				Id = TinkerTimeTypeSkillPoolId,
				Title = "Tinker Time - Skill Base Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
			new()
			{
				Id = TinkerTimeTypePowerPoolId,
				Title = "Tinker Time - Power Base Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
			new()
			{
				Id = TinkerTimeRiderAttackPoolId,
				Title = "Tinker Time - Attack Rider Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
			new()
			{
				Id = TinkerTimeRiderSkillPoolId,
				Title = "Tinker Time - Skill Rider Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
			new()
			{
				Id = TinkerTimeRiderPowerPoolId,
				Title = "Tinker Time - Power Rider Option",
				Group = "Tinker Time Builder",
				RarityHint = CardRarity.Event
			},
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

internal readonly record struct CardEditorRewardPoolTemplateCandidate(CardModel Template, CardEditorRewardPoolInjectionMode Mode);

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

internal sealed class CardEditorCardRewardSpec
{
	public CardGeneratedCardPool Pool { get; init; } = CardGeneratedCardPool.Default;
	public CardGeneratedCardType Type { get; init; } = CardGeneratedCardType.Any;
	public CardExtraEffectCardRewardRarityFilter Rarity { get; init; } = CardExtraEffectCardRewardRarityFilter.Any;
	public string? CustomTag { get; init; }

	public bool HasMetadata => Pool != CardGeneratedCardPool.Default
		|| Type != CardGeneratedCardType.Any
		|| Rarity != CardExtraEffectCardRewardRarityFilter.Any
		|| !string.IsNullOrWhiteSpace(CustomTag);

	public bool RequiresPostPopulateFilter => Pool == CardGeneratedCardPool.Ancient
		|| Type != CardGeneratedCardType.Any
		|| Rarity != CardExtraEffectCardRewardRarityFilter.Any
		|| !string.IsNullOrWhiteSpace(CustomTag);
}

internal static class CardEditorCardRewardSpecs
{
	private const string SpecCategory = "CARD_EDITOR_REWARD";
	private static readonly ConditionalWeakTable<CardReward, CardEditorCardRewardSpec> Specs = new();
	private static readonly System.Reflection.FieldInfo? OptionsField = AccessTools.Field(typeof(CardReward), "<Options>k__BackingField");
	private static readonly System.Reflection.FieldInfo? OptionCountField = AccessTools.Field(typeof(CardReward), "<OptionCount>k__BackingField");

	public static CardEditorCardRewardSpec FromEffect(CardExtraEffect effect)
	{
		return new CardEditorCardRewardSpec
		{
			Pool = effect?.GeneratedCardPool ?? CardGeneratedCardPool.Default,
			Type = effect?.GeneratedCardType ?? CardGeneratedCardType.Any,
			Rarity = effect?.CardRewardRarityFilter ?? CardExtraEffectCardRewardRarityFilter.Any,
			CustomTag = string.IsNullOrWhiteSpace(effect?.GeneratedCardCustomTag) ? null : effect.GeneratedCardCustomTag.Trim()
		};
	}

	public static void Attach(CardReward reward, CardEditorCardRewardSpec spec)
	{
		if (reward == null || spec == null || !spec.HasMetadata)
		{
			return;
		}

		Specs.Remove(reward);
		Specs.Add(reward, spec);
	}

	public static bool TryApplyToGeneratedCardReward(CardReward reward, Player player, CardCreationOptions options, int optionCount, List<CardCreationResult> cards)
	{
		if (reward == null
			|| player == null
			|| options == null
			|| cards == null
			|| !Specs.TryGetValue(reward, out CardEditorCardRewardSpec? spec)
			|| !spec.RequiresPostPopulateFilter)
		{
			return false;
		}

		List<CardModel> candidates = options.GetPossibleCards(player)
			.Where(card => MatchesSpec(player, card, spec))
			.GroupBy(card => card.Id)
			.Select(group => group.First())
			.ToList();
		if (candidates.Count == 0)
		{
			// Vanilla never offers an empty card reward (CardFactory throws instead). Keep the
			// vanilla-generated cards rather than presenting a selection screen with nothing in it.
			Log.Warn("[CardEditor][RewardPools] Card reward filter matched no cards; offering unfiltered reward instead.");
			return false;
		}

		int count = Math.Min(Math.Max(optionCount, 1), candidates.Count);
		try
		{
			List<CardCreationResult> generated = GenerateFilteredRewardCards(player, options, spec, candidates, count);
			if (generated.Count == 0)
			{
				Log.Warn("[CardEditor][RewardPools] Card reward filter generated no cards; offering unfiltered reward instead.");
				return false;
			}

			cards.Clear();
			cards.AddRange(generated);
			return true;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RewardPools] Card reward filter generation failed: {ex}");
			return false;
		}
	}

	public static bool TryBuildSerializable(CardReward reward, out SerializableReward result)
	{
		result = null!;
		if (reward == null
			|| !Specs.TryGetValue(reward, out CardEditorCardRewardSpec? spec)
			|| !spec.HasMetadata
			|| OptionsField?.GetValue(reward) is not CardCreationOptions options
			|| OptionCountField?.GetValue(reward) is not int optionCount
			|| options.CardPools.Count <= 0)
		{
			return false;
		}

		result = new SerializableReward
		{
			RewardType = RewardType.Card,
			PredeterminedModelId = Encode(spec),
			Source = options.Source,
			RarityOdds = options.RarityOdds,
			CardPoolIds = options.CardPools.Select(pool => pool.Id).ToList(),
			OptionCount = optionCount
		};
		return true;
	}

	public static bool TryBuildFromSerializable(SerializableReward save, Player player, out Reward reward)
	{
		reward = null!;
		if (save == null
			|| save.RewardType != RewardType.Card
			|| !TryDecode(save.PredeterminedModelId, out CardEditorCardRewardSpec spec))
		{
			return false;
		}

		CardCreationOptions options = new CardCreationOptions(save.CardPoolIds.Select(ModelDb.GetById<CardPoolModel>), save.Source, save.RarityOdds);
		CardReward cardReward = new CardReward(options, save.OptionCount, player);
		Attach(cardReward, spec);
		reward = cardReward;
		return true;
	}

	private static List<CardCreationResult> GenerateFilteredRewardCards(Player player, CardCreationOptions options, CardEditorCardRewardSpec spec, List<CardModel> candidates, int count)
	{
		if (CanUseVanillaRewardFactory(spec, candidates))
		{
			CardRarityOddsType odds = spec.Rarity != CardExtraEffectCardRewardRarityFilter.Any || candidates.Select(card => card.Rarity).Distinct().Count() <= 1
				? CardRarityOddsType.Uniform
				: options.RarityOdds;
			CardCreationOptions filteredOptions = new CardCreationOptions(candidates, options.Source, odds)
				.WithFlags(CardCreationFlags.NoCardPoolModifications);
			if (options.RngOverride != null)
			{
				filteredOptions.WithRngOverride(options.RngOverride);
			}

			return CardFactory.CreateForReward(player, count, filteredOptions).ToList();
		}

		Rng rng = options.RngOverride ?? player.PlayerRng.Rewards;
		List<CardModel> remaining = candidates.ToList();
		List<CardCreationResult> result = new();
		for (int i = 0; i < count && remaining.Count > 0; i++)
		{
			CardModel canonical = rng.NextItem(remaining);
			if (canonical == null)
			{
				break;
			}

			remaining.Remove(canonical);
			result.Add(new CardCreationResult(player.RunState.CreateCard(canonical, player)));
		}

		return result;
	}

	private static bool CanUseVanillaRewardFactory(CardEditorCardRewardSpec spec, List<CardModel> candidates)
	{
		if (spec.Rarity != CardExtraEffectCardRewardRarityFilter.Any)
		{
			return ToCardRarity(spec.Rarity) is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
		}

		return candidates.Any(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
	}

	private static bool MatchesSpec(Player player, CardModel card, CardEditorCardRewardSpec spec)
	{
		if (player == null || card == null)
		{
			return false;
		}

		if (!CardEditorExtraEffects.MatchesCountPool(player, card, spec.Pool))
		{
			return false;
		}

		if (!MatchesType(card, spec.Type))
		{
			return false;
		}

		CardRarity? rarity = ToCardRarity(spec.Rarity);
		if (rarity.HasValue && card.Rarity != rarity.Value)
		{
			return false;
		}

		string tag = spec.CustomTag?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(tag))
		{
			return CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
				&& overrideData.CustomTags != null
				&& overrideData.CustomTags.Contains(tag);
		}

		return true;
	}

	private static bool MatchesType(CardModel card, CardGeneratedCardType type)
	{
		return type switch
		{
			CardGeneratedCardType.Attack => card.Type == CardType.Attack,
			CardGeneratedCardType.Skill => card.Type == CardType.Skill,
			CardGeneratedCardType.Power => card.Type == CardType.Power,
			CardGeneratedCardType.Playable => card.Type is CardType.Attack or CardType.Skill or CardType.Power,
			CardGeneratedCardType.Status => card.Type == CardType.Status,
			CardGeneratedCardType.Curse => card.Type == CardType.Curse,
			CardGeneratedCardType.Quest => card.Type == CardType.Quest,
			_ => true
		};
	}

	private static CardRarity? ToCardRarity(CardExtraEffectCardRewardRarityFilter rarity)
	{
		return rarity switch
		{
			CardExtraEffectCardRewardRarityFilter.Basic => CardRarity.Basic,
			CardExtraEffectCardRewardRarityFilter.Common => CardRarity.Common,
			CardExtraEffectCardRewardRarityFilter.Uncommon => CardRarity.Uncommon,
			CardExtraEffectCardRewardRarityFilter.Rare => CardRarity.Rare,
			CardExtraEffectCardRewardRarityFilter.Ancient => CardRarity.Ancient,
			CardExtraEffectCardRewardRarityFilter.Event => CardRarity.Event,
			CardExtraEffectCardRewardRarityFilter.Token => CardRarity.Token,
			CardExtraEffectCardRewardRarityFilter.Status => CardRarity.Status,
			CardExtraEffectCardRewardRarityFilter.Curse => CardRarity.Curse,
			CardExtraEffectCardRewardRarityFilter.Quest => CardRarity.Quest,
			_ => null
		};
	}

	private static ModelId Encode(CardEditorCardRewardSpec spec)
	{
		string payload = string.Join("|", new[]
		{
			"1",
			((int)spec.Pool).ToString(),
			((int)spec.Type).ToString(),
			((int)spec.Rarity).ToString(),
			ToBase64Url(Encoding.UTF8.GetBytes(spec.CustomTag ?? string.Empty))
		});
		return new ModelId(SpecCategory, ToBase64Url(Encoding.UTF8.GetBytes(payload)));
	}

	private static bool TryDecode(ModelId id, out CardEditorCardRewardSpec spec)
	{
		spec = null!;
		if (id == null || id == ModelId.none || !string.Equals(id.Category, SpecCategory, StringComparison.Ordinal))
		{
			return false;
		}

		try
		{
			string payload = Encoding.UTF8.GetString(FromBase64Url(id.Entry));
			string[] parts = payload.Split('|');
			if (parts.Length < 5 || parts[0] != "1")
			{
				return false;
			}

			spec = new CardEditorCardRewardSpec
			{
				Pool = Enum.TryParse(parts[1], out CardGeneratedCardPool pool) ? pool : CardGeneratedCardPool.Default,
				Type = Enum.TryParse(parts[2], out CardGeneratedCardType type) ? type : CardGeneratedCardType.Any,
				Rarity = Enum.TryParse(parts[3], out CardExtraEffectCardRewardRarityFilter rarity) ? rarity : CardExtraEffectCardRewardRarityFilter.Any,
				CustomTag = string.IsNullOrWhiteSpace(parts[4]) ? null : Encoding.UTF8.GetString(FromBase64Url(parts[4]))
			};
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string ToBase64Url(byte[] bytes)
	{
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	private static byte[] FromBase64Url(string value)
	{
		string text = value.Replace('-', '+').Replace('_', '/');
		switch (text.Length % 4)
		{
			case 2:
				text += "==";
				break;
			case 3:
				text += "=";
				break;
		}
		return Convert.FromBase64String(text);
	}
}

[HarmonyPatch(typeof(CardReward), nameof(CardReward.ToSerializable))]
internal static class CardReward_ToSerializable_CardEditorRewardSpecs_Patch
{
	public static bool Prefix(CardReward __instance, ref SerializableReward __result)
	{
		if (!CardEditorCardRewardSpecs.TryBuildSerializable(__instance, out SerializableReward result))
		{
			return true;
		}

		__result = result;
		return false;
	}
}

[HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable))]
internal static class Reward_FromSerializable_CardEditorRewardSpecs_Patch
{
	public static bool Prefix(SerializableReward save, Player player, ref Reward __result)
	{
		if (!CardEditorCardRewardSpecs.TryBuildFromSerializable(save, player, out Reward reward))
		{
			return true;
		}

		__result = reward;
		return false;
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

			if (CardEditorCardRewardSpecs.TryApplyToGeneratedCardReward(__instance, __instance.Player, options, optionCount, cards))
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
