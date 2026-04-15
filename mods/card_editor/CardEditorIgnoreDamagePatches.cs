using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), new[]
{
	typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext),
	typeof(IEnumerable<Creature>),
	typeof(decimal),
	typeof(ValueProp),
	typeof(Creature),
	typeof(CardModel)
})]
internal static class CreatureCmd_Damage_IgnoreProps_Patch
{
	public static void Prefix(ref ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (cardSource == null)
		{
			return;
		}

		CardModel? effectiveSource = CardEditorIgnoreEffectHelpers.ResolveEffectiveSource(cardSource);
		if (effectiveSource == null)
		{
			return;
		}

		CardPlay? currentPlay = CardEditorCardPlayContext.Current;
		CombatState? combatState = currentPlay?.Card?.CombatState ?? dealer?.CombatState;

		IReadOnlyList<CardExtraEffect> effects = CardEditorExtraEffects.GetEffectsForDescription(effectiveSource, isUpgradePreview: false);
		if (effects == null || effects.Count == 0)
		{
			return;
		}

		foreach (CardExtraEffect effect in effects)
		{
			if (effect == null
				|| (effect.AsPower && CardEditorExtraEffects.SupportsAsPower(effect.Kind))
				|| !CardEditorExtraEffects.IsValidEffectAmount(effect.Kind, effect.Amount))
			{
				continue;
			}

			if (effect.Kind is not CardExtraEffectKind.IgnoreBlock and not CardExtraEffectKind.IgnoreDamageModifiers)
			{
				continue;
			}

			if (effect.ScaleMode == CardExtraEffectScaleMode.PerHistoryCount)
			{
				if (combatState == null || dealer == null || currentPlay == null)
				{
					continue;
				}

				int multiplier = CardEditorExtraEffects.GetHistoryCountMultiplierForCardPlay(combatState, dealer, currentPlay, effect);
				if (multiplier <= 0)
				{
					continue;
				}
			}

			switch (effect.Kind)
			{
				case CardExtraEffectKind.IgnoreBlock:
					props |= ValueProp.Unblockable;
					break;
				case CardExtraEffectKind.IgnoreDamageModifiers:
					props |= ValueProp.Unpowered;
					break;
			}
		}
	}
}
