using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorVanillaEffectCards
{
	private static HashSet<ModelId>? _tempStrengthCardIds;
	private static HashSet<ModelId>? _tempDexterityCardIds;
	private static HashSet<ModelId>? _tempFocusCardIds;

	public static bool UsesTemporaryStrength(ModelId cardId)
	{
		return GetTempStrengthCardIds().Contains(cardId);
	}

	public static bool UsesTemporaryDexterity(ModelId cardId)
	{
		return GetTempDexterityCardIds().Contains(cardId);
	}

	public static bool UsesTemporaryFocus(ModelId cardId)
	{
		return GetTempFocusCardIds().Contains(cardId);
	}

	private static HashSet<ModelId> GetTempStrengthCardIds()
	{
		return _tempStrengthCardIds ??= BuildCardIdsFromPowerOrigins<TemporaryStrengthPower>();
	}

	private static HashSet<ModelId> GetTempDexterityCardIds()
	{
		return _tempDexterityCardIds ??= BuildCardIdsFromPowerOrigins<TemporaryDexterityPower>();
	}

	private static HashSet<ModelId> GetTempFocusCardIds()
	{
		return _tempFocusCardIds ??= BuildCardIdsFromPowerOrigins<TemporaryFocusPower>();
	}

	private static HashSet<ModelId> BuildCardIdsFromPowerOrigins<TPower>() where TPower : PowerModel
	{
		HashSet<ModelId> ids = new HashSet<ModelId>();
		foreach (PowerModel power in ModelDb.AllPowers)
		{
			if (power is not TPower typedPower)
			{
				continue;
			}

			try
			{
				AbstractModel origin = typedPower switch
				{
					TemporaryStrengthPower p => p.OriginModel,
					TemporaryDexterityPower p => p.OriginModel,
					TemporaryFocusPower p => p.OriginModel,
					_ => null!
				};

				if (origin is CardModel originCard)
				{
					ids.Add(originCard.Id);
				}
			}
			catch
			{
			}
		}
		return ids;
	}
}

