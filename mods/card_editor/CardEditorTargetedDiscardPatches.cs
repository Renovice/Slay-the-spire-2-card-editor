using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorTargetedDiscardSupport
{
	internal const string DiscardCountVarName = "DiscardCount";

	private static readonly ModelId _acrobaticsId = ModelDb.GetId<Acrobatics>();
	private static readonly ModelId _survivorId = ModelDb.GetId<Survivor>();
	private static readonly ModelId _daggerThrowId = ModelDb.GetId<DaggerThrow>();

	internal static bool IsSupportedCard(ModelId cardId)
	{
		return cardId == _acrobaticsId || cardId == _survivorId || cardId == _daggerThrowId;
	}

	internal static int GetDesiredDiscardCount(CardModel card)
	{
		if (card == null)
		{
			return 1;
		}

		int count = 1;
		if (CardEditorOverrides.TryGetEffectiveOverride(card, out CardOverride overrideData)
			&& overrideData.HandDiscardCount.HasValue)
		{
			count = overrideData.HandDiscardCount.Value;
		}

		return Math.Clamp(count, 0, 99);
	}
}

[HarmonyPatch(typeof(CardModel), "AddExtraArgsToDescription")]
internal static class CardModel_AddExtraArgsToDescription_TargetedDiscardCount_Patch
{
	public static void Postfix(CardModel __instance, LocString description)
	{
		if (__instance == null || description == null)
		{
			return;
		}

		if (!CardEditorTargetedDiscardSupport.IsSupportedCard(__instance.Id))
		{
			return;
		}

		int discardCount = CardEditorTargetedDiscardSupport.GetDesiredDiscardCount(__instance);
		try
		{
			description.Add(new DynamicVar(CardEditorTargetedDiscardSupport.DiscardCountVarName, discardCount));
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(Acrobatics), "OnPlay")]
internal static class Acrobatics_OnPlay_TargetedDiscardCount_Patch
{
	public static bool Prefix(Acrobatics __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		int discardCount = CardEditorTargetedDiscardSupport.GetDesiredDiscardCount(__instance);
		if (discardCount == 1)
		{
			return true;
		}

		__result = Run(__instance, choiceContext, cardPlay, discardCount);
		return false;
	}

	private static async Task Run(Acrobatics card, PlayerChoiceContext choiceContext, CardPlay cardPlay, int discardCount)
	{
		await CardPileCmd.Draw(choiceContext, card.DynamicVars.Cards.BaseValue, card.Owner);
		if (discardCount <= 0)
		{
			return;
		}

		IEnumerable<CardModel> selected = await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			card.Owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, discardCount),
			null,
			card);

		await CardCmd.Discard(choiceContext, selected);
	}
}

[HarmonyPatch(typeof(Survivor), "OnPlay")]
internal static class Survivor_OnPlay_TargetedDiscardCount_Patch
{
	public static bool Prefix(Survivor __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		int discardCount = CardEditorTargetedDiscardSupport.GetDesiredDiscardCount(__instance);
		if (discardCount == 1)
		{
			return true;
		}

		__result = Run(__instance, choiceContext, cardPlay, discardCount);
		return false;
	}

	private static async Task Run(Survivor card, PlayerChoiceContext choiceContext, CardPlay cardPlay, int discardCount)
	{
		await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, cardPlay);
		if (discardCount <= 0)
		{
			return;
		}

		IEnumerable<CardModel> selected = await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			card.Owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, discardCount),
			null,
			card);

		await CardCmd.Discard(choiceContext, selected);
	}
}

[HarmonyPatch(typeof(DaggerThrow), "OnPlay")]
internal static class DaggerThrow_OnPlay_TargetedDiscardCount_Patch
{
	public static bool Prefix(DaggerThrow __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		int discardCount = CardEditorTargetedDiscardSupport.GetDesiredDiscardCount(__instance);
		if (discardCount == 1)
		{
			return true;
		}

		__result = Run(__instance, choiceContext, cardPlay, discardCount);
		return false;
	}

	private static async Task Run(DaggerThrow card, PlayerChoiceContext choiceContext, CardPlay cardPlay, int discardCount)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

		await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card, cardPlay).Targeting(cardPlay.Target)
			.WithAttackerFx(() => NDaggerSprayFlurryVfx.Create(card.Owner.Creature, new Color("#b1ccca"), goingRight: true))
			.WithHitVfxNode((Creature t) => NDaggerSprayImpactVfx.Create(t, new Color("#b1ccca"), goingRight: true))
			.Execute(choiceContext);

		await CardPileCmd.Draw(choiceContext, 1m, card.Owner);

		if (discardCount <= 0)
		{
			return;
		}

		IEnumerable<CardModel> selected = await CardSelectCmd.FromHandForDiscard(
			choiceContext,
			card.Owner,
			new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, discardCount),
			null,
			card);

		await CardCmd.Discard(choiceContext, selected);
	}
}
