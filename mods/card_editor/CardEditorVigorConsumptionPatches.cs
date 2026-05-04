using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Creature = MegaCrit.Sts2.Core.Entities.Creatures.Creature;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorVigorConsumptionSnapshot
{
	private readonly Creature _owner;
	private readonly CardModel _card;
	private readonly int _startingAmount;

	private CardEditorVigorConsumptionSnapshot(Creature owner, CardModel card, int startingAmount)
	{
		_owner = owner;
		_card = card;
		_startingAmount = startingAmount;
	}

	public static CardEditorVigorConsumptionSnapshot? TryCreate(AttackCommand command)
	{
		CardModel? playedCard = CardEditorBorrowedEffectSourceDamageHelper.ResolveRuntimePlayedCard(command?.ModelSource as CardModel);
		CardModel? currentCard = CardEditorCardPlayContext.Current?.Card;
		if (playedCard == null || currentCard == null || !ReferenceEquals(playedCard, currentCard))
		{
			return null;
		}

		if (!CardEditorExtraEffects.CardHasRuntimeResourceConsumptionMode(playedCard, CardExtraEffectResourceConsumptionMode.Vigor))
		{
			return null;
		}

		Creature? owner = playedCard.Owner?.Creature;
		int amount = TryGetVigorPower(owner)?.Amount ?? 0;
		if (owner == null || amount <= 0)
		{
			return null;
		}

		return new CardEditorVigorConsumptionSnapshot(owner, playedCard, amount);
	}

	public async Task RestoreIfConsumed()
	{
		PowerModel? current = TryGetVigorPower(_owner);
		int currentAmount = current?.Amount ?? 0;
		if (currentAmount >= _startingAmount)
		{
			return;
		}

		int delta = _startingAmount - currentAmount;
		if (delta <= 0)
		{
			return;
		}

		try
		{
			PowerModel? canonical = TryGetCanonicalVigorPower();
			if (canonical == null)
			{
				return;
			}

			await PowerCmd.Apply(canonical.ToMutable(), _owner, delta, _owner, _card);
		}
		catch
		{
			// ignored
		}
	}

	private static PowerModel? TryGetVigorPower(Creature? owner)
	{
		if (owner == null)
		{
			return null;
		}

		try
		{
			return owner.GetPower<VigorPower>();
		}
		catch
		{
			return null;
		}
	}

	private static PowerModel? TryGetCanonicalVigorPower()
	{
		try
		{
			return ModelDb.Power<VigorPower>();
		}
		catch
		{
			return null;
		}
	}
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class AttackCommand_Execute_PreserveCardVigorPatch
{
	private static bool _warnedPatchFailure;

	public static void Prefix(AttackCommand __instance, out CardEditorVigorConsumptionSnapshot? __state)
	{
		try
		{
			__state = CardEditorVigorConsumptionSnapshot.TryCreate(__instance);
		}
		catch (Exception ex)
		{
			WarnPatchFailure("snapshot", ex);
			__state = null;
		}
	}

	public static void Postfix(ref Task __result, CardEditorVigorConsumptionSnapshot? __state)
	{
		if (__state == null || __result == null)
		{
			return;
		}

		__result = RestoreAfter(__result, __state);
	}

	private static async Task RestoreAfter(Task original, CardEditorVigorConsumptionSnapshot snapshot)
	{
		await original;
		try
		{
			await snapshot.RestoreIfConsumed();
		}
		catch (Exception ex)
		{
			WarnPatchFailure("restore", ex);
		}
	}

	private static void WarnPatchFailure(string phase, Exception ex)
	{
		if (_warnedPatchFailure)
		{
			return;
		}

		_warnedPatchFailure = true;
		Log.Warn($"[CardEditor] Preserve-card-vigor attack patch failed during {phase}; attack will continue without vigor restoration. {ex}");
	}
}
