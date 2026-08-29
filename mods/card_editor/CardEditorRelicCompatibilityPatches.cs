using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorRelicCompatibility
{
	private const string CombatsKey = "Combats";

	internal static void SyncRuntimeCounters(RelicModel relic, RelicOverride overrideData)
	{
		if (relic == null
			|| overrideData?.DynamicVarBaseValues == null
			|| !overrideData.DynamicVarBaseValues.TryGetValue(CombatsKey, out decimal configuredCombats)
			|| relic is not TeaOfDiscourtesy and not EmberTea)
		{
			return;
		}

		try
		{
			PropertyInfo? property = relic.GetType().GetProperty(
				"CombatsLeft",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property == null)
			{
				Log.Warn($"[CardEditor][Relics] Could not find {relic.GetType().Name}.CombatsLeft; the edited combat count cannot be synchronized.");
				return;
			}

			property.SetValue(relic, Math.Max(0, decimal.ToInt32(decimal.Truncate(configuredCombats))));
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][Relics] Failed to synchronize {relic.GetType().Name}.CombatsLeft: {ex}");
		}
	}

	internal static void ResizeCallingBellRewards(CallingBell relic, List<Reward> rewards)
	{
		if (relic?.DynamicVars == null
			|| rewards == null
			|| !relic.DynamicVars.TryGetValue("Relics", out var relicsVar))
		{
			return;
		}

		int configuredCount = Math.Max(0, relicsVar.IntValue);
		if (rewards.Count > configuredCount)
		{
			rewards.RemoveRange(configuredCount, rewards.Count - configuredCount);
			return;
		}

		RelicRarity[] rarities = { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare };
		while (rewards.Count < configuredCount)
		{
			rewards.Add(new RelicReward(rarities[rewards.Count % rarities.Length], relic.Owner));
		}
	}

	internal static async Task ObtainHeftyTablet(HeftyTablet relic)
	{
		int offerCount = Math.Max(0, relic.DynamicVars.Cards.IntValue);
		List<CardCreationResult> options = new();
		if (offerCount > 0)
		{
			CardCreationOptions creationOptions = new CardCreationOptions(
				new[] { relic.Owner.Character.CardPool },
				CardCreationSource.Other,
				CardRarityOddsType.Uniform,
				card => card.Rarity == CardRarity.Rare)
				.WithFlags(CardCreationFlags.NoUpgradeRoll);
			options = CardFactory.CreateForReward(relic.Owner, offerCount, creationOptions).ToList();
		}

		CardModel? chosenCard = null;
		if (options.Count > 0)
		{
			CardSelectorPrefs prefs = new CardSelectorPrefs(
				new LocString("relics", relic.Id.Entry + ".selectionScreenPrompt"),
				0,
				1)
			{
				Cancelable = true
			};
			chosenCard = (await CardSelectCmd.FromSimpleGridForRewards(
				new BlockingPlayerChoiceContext(),
				options,
				relic.Owner,
				prefs)).FirstOrDefault();
		}

		List<CardModel> cardsToAdd = new() { relic.Owner.RunState.CreateCard<Injury>(relic.Owner) };
		if (chosenCard != null)
		{
			cardsToAdd.Insert(0, chosenCard);
		}
		CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(cardsToAdd, PileType.Deck));

		foreach (CardCreationResult option in options)
		{
			if (option.Card != chosenCard)
			{
				relic.Owner.RunState.CurrentMapPointHistoryEntry?
					.GetEntry(relic.Owner.NetId)
					.CardChoices.Add(new CardChoiceHistoryEntry(option.Card, wasPicked: false));
			}
		}
	}

	internal static async Task ObtainCallingBellWithoutRewards(CallingBell relic)
	{
		await CardPileCmd.AddCurseToDeck<CurseOfTheBell>(relic.Owner);
		await Cmd.Wait(0.75f);
	}
}

[HarmonyPatch(typeof(CallingBell), "GenerateRewards")]
internal static class CallingBell_GenerateRewards_CardEditorRelicCompatibility_Patch
{
	private static void Postfix(CallingBell __instance, List<Reward> __result)
	{
		CardEditorRelicCompatibility.ResizeCallingBellRewards(__instance, __result);
	}
}

[HarmonyPatch(typeof(CallingBell), nameof(CallingBell.AfterObtained))]
internal static class CallingBell_AfterObtained_CardEditorRelicCompatibility_Patch
{
	private static bool Prefix(CallingBell __instance, ref Task __result)
	{
		if (__instance.DynamicVars["Relics"].IntValue > 0)
		{
			return true;
		}

		__result = CardEditorRelicCompatibility.ObtainCallingBellWithoutRewards(__instance);
		return false;
	}
}

[HarmonyPatch(typeof(HeftyTablet), nameof(HeftyTablet.AfterObtained))]
internal static class HeftyTablet_AfterObtained_CardEditorRelicCompatibility_Patch
{
	private static bool Prefix(HeftyTablet __instance, ref Task __result)
	{
		int count = Math.Max(0, __instance.DynamicVars.Cards.IntValue);
		if (count is >= 1 and <= 3)
		{
			return true;
		}

		__result = CardEditorRelicCompatibility.ObtainHeftyTablet(__instance);
		return false;
	}
}
