using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorFullArtRenderContext
{
	[ThreadStatic]
	private static int _depth;

	public static bool IsActive => _depth > 0;

	public static void Enter()
	{
		_depth++;
	}

	public static void Exit()
	{
		if (_depth > 0)
		{
			_depth--;
		}
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
public static class NCard_Reload_CreatedCardFullArtContext_Patch
{
	public static void Prefix(NCard __instance, ref bool __state)
	{
		__state = false;
		CardModel? model = __instance.Model;
		if (model == null)
		{
			return;
		}
		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
		{
			if (!CardEditorCreatedCardsStore.IsFullArt(model.Id))
			{
				return;
			}
		}
		else
		{
			if (CardEditorOverrides.SuppressAllOverrides)
			{
				return;
			}
			if (!CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData))
			{
				return;
			}
			if (overrideData.FullArt != true)
			{
				return;
			}
		}

		CardEditorFullArtRenderContext.Enter();
		__state = true;
	}

	public static void Postfix(bool __state)
	{
		if (__state)
		{
			CardEditorFullArtRenderContext.Exit();
		}
	}
}

internal static class CardEditorFullArtRefresh
{
	private sealed class State
	{
		public bool LastDesiredFullArt { get; set; }
		public bool HasValue { get; set; }
	}

	private static readonly ConditionalWeakTable<NCard, State> _state = new ConditionalWeakTable<NCard, State>();

	[ThreadStatic]
	private static bool _isReloading;

	private static readonly Action<NCard> _reload = AccessTools.MethodDelegate<Action<NCard>>(AccessTools.Method(typeof(NCard), "Reload"));

	public static bool ShouldBeFullArt(CardModel model)
	{
		if (model == null)
		{
			return false;
		}

		if (model.Rarity == CardRarity.Ancient)
		{
			return true;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
		{
			return CardEditorCreatedCardsStore.IsFullArt(model.Id);
		}

		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData) && overrideData.FullArt == true;
	}

	public static void EnsureReloadedIfNeeded(NCard card)
	{
		if (card == null || _isReloading)
		{
			return;
		}

		CardModel? model = card.Model;
		if (model == null)
		{
			return;
		}

		bool desired = ShouldBeFullArt(model);
		State state = _state.GetOrCreateValue(card);
		bool changed = !state.HasValue || state.LastDesiredFullArt != desired;
		state.LastDesiredFullArt = desired;
		state.HasValue = true;

		if (!changed)
		{
			return;
		}

		_isReloading = true;
		try
		{
			_reload(card);
		}
		finally
		{
			_isReloading = false;
		}
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_FullArtRefresh_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorFullArtRefresh.EnsureReloadedIfNeeded(__instance);
		}
		catch
		{
		}
	}
}
