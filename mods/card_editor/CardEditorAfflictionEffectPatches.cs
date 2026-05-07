using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorAfflictionEffects
{
	public static int GetEntangledCostIncrease(CardModel card)
	{
		if (card?.Affliction is not Entangled entangled)
		{
			return 0;
		}

		Creature? owner = card.TryGetOwnerCreature();
		if (owner == null)
		{
			return 0;
		}

		// Avoid double-modifying when the base game's TangledPower is present.
		if (owner.HasPower<TangledPower>())
		{
			return 0;
		}

		return Math.Max(0, entangled.Amount);
	}

	public static bool ShouldPreventPlay(CombatState combatState, CardModel card, AutoPlayType autoPlayType, out AbstractModel? preventer)
	{
		preventer = null;
		if (combatState == null || card == null)
		{
			return false;
		}

		AfflictionModel? affliction = card.Affliction;
		if (affliction == null)
		{
			return false;
		}

		Creature? owner = card.TryGetOwnerCreature();
		if (owner == null)
		{
			return false;
		}

		// Smog: afflicted cards cannot be played.
		if (affliction is Smog && !owner.HasPower<SmoggyPower>())
		{
			preventer = affliction;
			return true;
		}

		// Bound: only one Bound card per turn.
		if (affliction is Bound && !owner.HasPower<ChainsOfBindingPower>())
		{
			bool alreadyPlayedBound = CombatManager.Instance.History.CardPlaysStarted.Any((CardPlayStartedEntry e) =>
				e.HappenedThisTurn(combatState)
				&& e.CardPlay?.Card?.Owner?.Creature == owner
				&& e.CardPlay.Card.Affliction is Bound);

			if (alreadyPlayedBound)
			{
				preventer = affliction;
				return true;
			}
		}

		// Ringing: this card can only be played if you haven't played a card yet this turn.
		if (affliction is Ringing && !owner.HasPower<RingingPower>())
		{
			bool alreadyPlayedAny = CombatManager.Instance.History.CardPlaysStarted.Any((CardPlayStartedEntry e) =>
				e.HappenedThisTurn(combatState)
				&& e.CardPlay?.Card?.Owner?.Creature == owner);

			if (alreadyPlayedAny)
			{
				preventer = affliction;
				return true;
			}
		}

		return false;
	}

	public static async Task RunAfterCardPlayed(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		try
		{
			CardModel? card = cardPlay?.Card;
			if (combatState == null || choiceContext == null || card == null)
			{
				return;
			}

			if (card.Affliction is not Galvanized galvanized)
			{
				return;
			}

			Creature? owner = card.TryGetOwnerCreature();
			if (owner == null)
			{
				return;
			}

			// Avoid double-triggering when the base game's GalvanicPower is present on any creature
			// (e.g. an enemy like GlobeHead that applies Galvanized to player power cards).
			if (owner.HasPower<GalvanicPower>()
				|| combatState.Enemies.Any(e => e != null && e.HasPower<GalvanicPower>()))
			{
				return;
			}

			// Mirror the base game's behavior: Galvanized triggers on Power cards.
			if (card.Type != CardType.Power)
			{
				return;
			}

			int damage = Math.Max(0, galvanized.Amount);
			if (damage <= 0)
			{
				return;
			}

			VfxCmd.PlayOnCreature(owner, "vfx/vfx_attack_lightning");
			await CreatureCmd.Damage(choiceContext, owner, damage, ValueProp.Unpowered | ValueProp.Move, null, null);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Affliction AfterCardPlayed failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class Hook_ModifyEnergyCostInCombat_Afflictions_Patch
{
	public static void Postfix(CombatState combatState, CardModel card, decimal originalCost, ref decimal __result)
	{
		try
		{
			if (__result < 0m)
			{
				return;
			}

			int increase = CardEditorAfflictionEffects.GetEntangledCostIncrease(card);
			if (increase <= 0)
			{
				return;
			}

			__result += increase;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Affliction ModifyEnergyCostInCombat failed: {ex}");
		}
	}
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPlay))]
internal static class Hook_ShouldPlay_Afflictions_Patch
{
	public static bool Prefix(CombatState combatState, CardModel card, ref AbstractModel? preventer, AutoPlayType autoPlayType, ref bool __result)
	{
		try
		{
			if (CardEditorAfflictionEffects.ShouldPreventPlay(combatState, card, autoPlayType, out AbstractModel? p))
			{
				preventer = p;
				__result = false;
				return false;
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Affliction ShouldPlay failed: {ex}");
		}

		return true;
	}
}

// Hexed afflictions clear themselves unless HexPower is present; that makes editor-set Hexed effectively a no-op.
// This patch keeps Hexed afflictions from self-clearing when no HexPower exists.
[HarmonyPatch(typeof(Hexed), nameof(Hexed.AfterCardEnteredCombat))]
internal static class Hexed_AfterCardEnteredCombat_DoNotAutoClear_Patch
{
	public static bool Prefix(Hexed __instance, CardModel card, ref Task __result)
	{
		try
		{
			if (__instance == null || card == null)
			{
				return true;
			}

			if (card != __instance.Card)
			{
				return true;
			}

			Creature? owner = card.TryGetOwnerCreature();
			if (owner == null)
			{
				return true;
			}

			if (!owner.HasPower<HexPower>())
			{
				__result = Task.CompletedTask;
				return false;
			}
		}
		catch
		{
		}

		return true;
	}
}
