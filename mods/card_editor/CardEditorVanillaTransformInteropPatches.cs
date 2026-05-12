using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorVanillaTransformInterop
{
	private static readonly object TransformingLock = new();
	private static readonly Dictionary<CardModel, int> TransformingOriginals = new();

	internal static bool IsManaged(CardModel? card)
	{
		if (card?.Id == null)
		{
			return false;
		}

		return CardEditorCreatedCardsStore.IsCreatedCardId(card.Id)
			|| CardEditorOverrides.TryGetEffectiveOverride(card, out _);
	}

	internal static void MarkTransforming(IEnumerable<CardTransformation> transformations)
	{
		lock (TransformingLock)
		{
			foreach (CardModel original in transformations.Select(t => t.Original).Where(IsManaged))
			{
				TransformingOriginals.TryGetValue(original, out int count);
				TransformingOriginals[original] = count + 1;
			}
		}
	}

	internal static void UnmarkTransforming(IEnumerable<CardTransformation> transformations)
	{
		lock (TransformingLock)
		{
			foreach (CardModel original in transformations.Select(t => t.Original).Where(IsManaged))
			{
				if (!TransformingOriginals.TryGetValue(original, out int count))
				{
					continue;
				}

				if (count <= 1)
				{
					TransformingOriginals.Remove(original);
				}
				else
				{
					TransformingOriginals[original] = count - 1;
				}
			}
		}
	}

	internal static bool IsTransformingOriginal(CardModel? card)
	{
		if (card == null)
		{
			return false;
		}

		lock (TransformingLock)
		{
			return TransformingOriginals.ContainsKey(card);
		}
	}
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Transform), new[] { typeof(IEnumerable<CardTransformation>), typeof(Rng), typeof(CardPreviewStyle) })]
internal static class CardCmd_Transform_CardEditorTransformInterop_Patch
{
	public static void Prefix(ref IEnumerable<CardTransformation> transformations, out CardTransformation[] __state)
	{
		__state = transformations?.ToArray() ?? Array.Empty<CardTransformation>();
		transformations = __state;
		CardEditorVanillaTransformInterop.MarkTransforming(__state);
	}

	public static void Postfix(CardTransformation[] __state, ref Task<IEnumerable<CardPileAddResult>> __result)
	{
		__result = UnmarkWhenFinished(__result, __state);
	}

	private static async Task<IEnumerable<CardPileAddResult>> UnmarkWhenFinished(
		Task<IEnumerable<CardPileAddResult>> originalTask,
		CardTransformation[] transformations)
	{
		try
		{
			return await originalTask;
		}
		finally
		{
			CardEditorVanillaTransformInterop.UnmarkTransforming(transformations);
		}
	}
}

[HarmonyPatch(typeof(NCardPlayQueue), nameof(NCardPlayQueue.UpdateCardBeforeExecution))]
internal static class NCardPlayQueue_UpdateCardBeforeExecution_CardEditorTransformInterop_Patch
{
	private static readonly FieldInfo? PlayQueueField = AccessTools.Field(typeof(NCardPlayQueue), "_playQueue");

	public static bool Prefix(NCardPlayQueue __instance, PlayCardAction playCardAction)
	{
		CardModel? queuedCard = TryResolveQueuedCard(playCardAction);
		if (!CardEditorVanillaTransformInterop.IsManaged(queuedCard)
			|| !CardEditorVanillaTransformInterop.IsTransformingOriginal(queuedCard))
		{
			return true;
		}

		if (queuedCard?.Pile?.Type == PileType.Hand)
		{
			return true;
		}

		if (TryGetQueuedCardNode(__instance, playCardAction) is NCard queuedNode)
		{
			__instance.RemoveCardFromQueueForCancellation(queuedNode, forceReturnToHand: true);
		}
		else
		{
			__instance.RemoveCardFromQueueForCancellation(playCardAction);
		}

		CardEditorMod.VerboseLog(
			$"[CardEditor][TransformInterop] Returned queued transforming card before visual refresh card={queuedCard?.Id} pile={queuedCard?.Pile?.Type}");
		return false;
	}

	private static CardModel? TryResolveQueuedCard(PlayCardAction playCardAction)
	{
		try
		{
			return playCardAction?.NetCombatCard.ToCardModelOrNull();
		}
		catch
		{
			return null;
		}
	}

	private static NCard? TryGetQueuedCardNode(NCardPlayQueue queue, PlayCardAction action)
	{
		try
		{
			if (PlayQueueField?.GetValue(queue) is not IEnumerable items)
			{
				return null;
			}

			foreach (object item in items)
			{
				Type itemType = item.GetType();
				object? itemAction = itemType.GetField("action", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item);
				if (!ReferenceEquals(itemAction, action))
				{
					continue;
				}

				return itemType.GetField("card", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) as NCard;
			}
		}
		catch
		{
			return null;
		}

		return null;
	}
}

[HarmonyPatch(typeof(NCardTransformVfx), nameof(NCardTransformVfx.PlayAnimOnCardInHand))]
internal static class NCardTransformVfx_PlayAnimOnCardInHand_CardEditorTransformInterop_Patch
{
	public static void Postfix(NCard cardNode, CardModel endCard, ref Task __result)
	{
		CardModel? startCard = TryGetModel(cardNode);
		if (!CardEditorVanillaTransformInterop.IsManaged(startCard)
			&& !CardEditorVanillaTransformInterop.IsManaged(endCard))
		{
			return;
		}

		__result = CompleteWithVanillaFinalState(__result, cardNode, endCard);
	}

	private static async Task CompleteWithVanillaFinalState(Task originalTask, NCard cardNode, CardModel endCard)
	{
		try
		{
			await originalTask;
		}
		finally
		{
			RestoreVanillaFinalState(cardNode, endCard);
		}
	}

	private static void RestoreVanillaFinalState(NCard cardNode, CardModel endCard)
	{
		if (cardNode == null || endCard == null || !GodotObject.IsInstanceValid(cardNode) || !cardNode.IsInsideTree())
		{
			return;
		}

		if (endCard.Pile != null && !ReferenceEquals(cardNode.Model, endCard))
		{
			cardNode.Model = endCard;
			cardNode.UpdateVisuals(endCard.Pile.Type, CardPreviewMode.Normal);
		}

		if (cardNode.Scale != Vector2.One)
		{
			cardNode.Scale = Vector2.One;
		}

		if (endCard.Pile?.Type == PileType.Hand
			&& NPlayerHand.Instance?.GetCardHolder(endCard) is NHandCardHolder holder)
		{
			holder.UpdateCard();
		}
	}

	private static CardModel? TryGetModel(NCard cardNode)
	{
		try
		{
			return cardNode?.Model;
		}
		catch
		{
			return null;
		}
	}
}
