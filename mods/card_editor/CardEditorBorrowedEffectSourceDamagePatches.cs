using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorBorrowedEffectSourceDamageHelper
{
	internal static CardModel? ResolveRuntimePlayedCard(CardModel? cardSource)
	{
		if (cardSource == null)
		{
			return null;
		}

		CardModel? borrowedSource = CardEditorEffectSourceContext.Current;
		CardModel? playedCard = CardEditorCardPlayContext.Current?.Card;
		if (borrowedSource == null || playedCard == null)
		{
			return cardSource;
		}

		if (!ReferenceEquals(cardSource, borrowedSource) || ReferenceEquals(playedCard, cardSource))
		{
			return cardSource;
		}

		if (cardSource.Owner != null
			&& playedCard.Owner != null
			&& !ReferenceEquals(cardSource.Owner, playedCard.Owner))
		{
			return cardSource;
		}

		return playedCard;
	}
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.FromCard))]
internal static class AttackCommand_FromCard_BorrowedEffectSourceSourcePatch
{
	private static readonly FieldInfo? _modelSourceField = AccessTools.Field(typeof(AttackCommand), "<ModelSource>k__BackingField");

	public static void Postfix(AttackCommand __instance, CardModel card)
	{
		CardModel? runtimePlayedCard = CardEditorBorrowedEffectSourceDamageHelper.ResolveRuntimePlayedCard(card);
		if (runtimePlayedCard == null || ReferenceEquals(runtimePlayedCard, card) || _modelSourceField == null)
		{
			return;
		}

		_modelSourceField.SetValue(__instance, runtimePlayedCard);
	}
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.FromOsty))]
internal static class AttackCommand_FromOsty_BorrowedEffectSourceSourcePatch
{
	private static readonly FieldInfo? _modelSourceField = AccessTools.Field(typeof(AttackCommand), "<ModelSource>k__BackingField");

	public static void Postfix(AttackCommand __instance, Creature osty, CardModel card)
	{
		CardModel? runtimePlayedCard = CardEditorBorrowedEffectSourceDamageHelper.ResolveRuntimePlayedCard(card);
		if (runtimePlayedCard == null || ReferenceEquals(runtimePlayedCard, card) || _modelSourceField == null)
		{
			return;
		}

		_modelSourceField.SetValue(__instance, runtimePlayedCard);
	}
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), new[]
{
	typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
	typeof(IEnumerable<Creature>),
	typeof(decimal),
	typeof(ValueProp),
	typeof(Creature),
	typeof(CardModel)
})]
internal static class CreatureCmd_Damage_BorrowedEffectSourceSourcePatch
{
	public static void Prefix(ref Creature? dealer, ref CardModel? cardSource)
	{
		CardModel? runtimePlayedCard = CardEditorBorrowedEffectSourceDamageHelper.ResolveRuntimePlayedCard(cardSource);
		if (runtimePlayedCard == null || ReferenceEquals(runtimePlayedCard, cardSource))
		{
			return;
		}

		cardSource = runtimePlayedCard;
		dealer ??= runtimePlayedCard.Owner?.Creature;
	}
}
