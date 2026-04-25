using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using PowerCmd = SlayTheSpire2Mod.CardEditor.CardEditorPowerCmdCompat;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorVigorConsumptionSnapshot
{
	private static readonly ModelId VigorPowerId = ModelId.Deserialize("VigorPower");
	private readonly MegaCrit.Sts2.Core.Entities.Creatures.Creature _owner;
	private readonly CardModel _card;
	private readonly int _startingAmount;

	private CardEditorVigorConsumptionSnapshot(MegaCrit.Sts2.Core.Entities.Creatures.Creature owner, CardModel card, int startingAmount)
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

		MegaCrit.Sts2.Core.Entities.Creatures.Creature? owner = playedCard.Owner?.Creature;
		int amount = owner?.Powers?.FirstOrDefault(power => power != null && Equals(power.Id, VigorPowerId))?.Amount ?? 0;
		if (owner == null || amount <= 0)
		{
			return null;
		}

		return new CardEditorVigorConsumptionSnapshot(owner, playedCard, amount);
	}

	public async Task RestoreIfConsumed()
	{
		PowerModel? current = _owner.Powers?.FirstOrDefault(power => power != null && Equals(power.Id, VigorPowerId));
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
			PowerModel? canonical = ModelDb.GetByIdOrNull<PowerModel>(VigorPowerId);
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
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class AttackCommand_Execute_PreserveCardVigorPatch
{
	public static void Prefix(AttackCommand __instance, out CardEditorVigorConsumptionSnapshot? __state)
	{
		__state = CardEditorVigorConsumptionSnapshot.TryCreate(__instance);
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
		await snapshot.RestoreIfConsumed();
	}
}
