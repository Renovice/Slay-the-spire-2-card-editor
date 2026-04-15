using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
public static class CardPoolModel_GetUnlockedCards_CreatedCardsFilter_Patch
{
	public static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
	{
		if (__result == null)
		{
			return;
		}

		static bool ShouldRemove(CardPoolModel pool, CardModel card)
		{
			if (card?.Id == null)
			{
				return false;
			}

			if (card is not CardEditorCreatedCardBase created)
			{
				if (!CardEditorOverrides.TryGet(card.Id, out CardOverride overrideData))
				{
					return false;
				}

				if (overrideData.Enabled.HasValue && !overrideData.Enabled.Value)
				{
					return true;
				}

				if (string.IsNullOrWhiteSpace(overrideData.PoolTitle))
				{
					return false;
				}

				return !string.Equals(pool.Title, overrideData.PoolTitle.Trim(), System.StringComparison.OrdinalIgnoreCase);
			}

			if (!CardEditorCreatedCardsStore.IsEnabled(created.Id))
			{
				return true;
			}

			return created.Pool.Id != pool.Id;
		}

		static IEnumerable<ModelId> GetVanillaCardsThatShouldBeInPool(CardPoolModel pool)
		{
			if (pool == null)
			{
				yield break;
			}

			foreach ((ModelId id, CardOverride overrideData) in CardEditorOverrides.AllOverrides)
			{
				if (overrideData == null)
				{
					continue;
				}
				if (overrideData.Enabled.HasValue && !overrideData.Enabled.Value)
				{
					continue;
				}
				if (string.IsNullOrWhiteSpace(overrideData.PoolTitle))
				{
					continue;
				}
				if (!string.Equals(pool.Title, overrideData.PoolTitle.Trim(), System.StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if (CardEditorCreatedCardsStore.IsCreatedCardId(id))
				{
					continue;
				}
				yield return id;
			}
		}

		if (__result is List<CardModel> list)
		{
			list.RemoveAll(c => ShouldRemove(__instance, c));

			HashSet<ModelId> present = new HashSet<ModelId>(list.Select(c => c.Id));
			foreach (ModelId id in GetVanillaCardsThatShouldBeInPool(__instance))
			{
				if (!present.Add(id))
				{
					continue;
				}
				CardModel? card = ModelDb.GetByIdOrNull<CardModel>(id);
				if (card != null)
				{
					list.Add(card);
				}
			}

			__result = list;
			return;
		}

		List<CardModel> filtered = __result.Where(c => !ShouldRemove(__instance, c)).ToList();
		HashSet<ModelId> filteredIds = new HashSet<ModelId>(filtered.Select(c => c.Id));
		foreach (ModelId id in GetVanillaCardsThatShouldBeInPool(__instance))
		{
			if (!filteredIds.Add(id))
			{
				continue;
			}
			CardModel? card = ModelDb.GetByIdOrNull<CardModel>(id);
			if (card != null)
			{
				filtered.Add(card);
			}
		}
		__result = filtered;
	}
}
