using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class CardEditorVisibleExtraEffectPowerPatches
{
	private static readonly Dictionary<string, Texture2D?> _textureCache = new Dictionary<string, Texture2D?>(StringComparer.Ordinal);

	[HarmonyPatch(typeof(PowerModel), "get_Icon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_Icon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorVisibleExtraEffectPower mirror)
		{
			return true;
		}

		__result = ResolveIcon(mirror, bigIcon: false);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_BigIcon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorVisibleExtraEffectPower mirror)
		{
			return true;
		}

		__result = ResolveIcon(mirror, bigIcon: true);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_HoverTips")]
	[HarmonyPrefix]
	private static bool PowerModel_get_HoverTips_Prefix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (__instance is not CardEditorVisibleExtraEffectPower mirror)
		{
			return true;
		}

		string body = mirror.GetTooltipBody();
		List<IHoverTip> tips = new List<IHoverTip>
		{
			new HoverTip(mirror, body, false)
		};
		tips.AddRange(CardEditorVanillaKeywordSupport.InferHoverTips(body));
		__result = tips;
		return false;
	}

	private static Texture2D? ResolveIcon(CardEditorVisibleExtraEffectPower mirror, bool bigIcon)
	{
		CardExtraEffect? effect = mirror.SourceEffect;
		if (effect == null)
		{
			PowerModel? fallbackWithoutEffect = GetFallbackPower();
			return bigIcon ? fallbackWithoutEffect?.BigIcon : fallbackWithoutEffect?.Icon;
		}

		Texture2D? resolved = effect.StatusIconMode switch
		{
			CardExtraEffectStatusIconMode.BaseGame => ResolveBaseGameIcon(effect.StatusIconPowerId, bigIcon),
			CardExtraEffectStatusIconMode.Custom => ResolveCustomIcon(effect, bigIcon),
			_ => ResolveAutoIcon(effect, bigIcon)
		};

		if (resolved != null)
		{
			return resolved;
		}

		PowerModel? fallback = GetFallbackPower();
		return bigIcon ? fallback?.BigIcon : fallback?.Icon;
	}

	private static Texture2D? ResolveAutoIcon(CardExtraEffect effect, bool bigIcon)
	{
		if (!string.IsNullOrWhiteSpace(effect.PowerId))
		{
			Texture2D? appliedPowerIcon = ResolveBaseGameIcon(effect.PowerId, bigIcon);
			if (appliedPowerIcon != null)
			{
				return appliedPowerIcon;
			}
		}

		PowerModel? mappedPower = GetAutoMappedPower(effect);
		if (mappedPower != null)
		{
			return bigIcon ? mappedPower.BigIcon : mappedPower.Icon;
		}

		return null;
	}

	private static Texture2D? ResolveBaseGameIcon(string? powerIdText, bool bigIcon)
	{
		if (string.IsNullOrWhiteSpace(powerIdText))
		{
			return null;
		}

		try
		{
			ModelId powerId = ModelId.Deserialize(powerIdText.Trim());
			PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(powerId);
			return power == null ? null : (bigIcon ? power.BigIcon : power.Icon);
		}
		catch
		{
			return null;
		}
	}

	private static Texture2D? ResolveCustomIcon(CardExtraEffect effect, bool bigIcon)
	{
		string? preferredPath = bigIcon
			? effect.StatusCustomBigIconPath ?? effect.StatusCustomPackedIconPath
			: effect.StatusCustomPackedIconPath ?? effect.StatusCustomBigIconPath;
		Texture2D? preferred = LoadTexture(preferredPath);
		if (preferred != null)
		{
			return preferred;
		}

		string? fallbackPath = bigIcon ? effect.StatusCustomPackedIconPath : effect.StatusCustomBigIconPath;
		return LoadTexture(fallbackPath);
	}

	private static Texture2D? LoadTexture(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}

		string normalized = path.Trim();
		if (_textureCache.TryGetValue(normalized, out Texture2D? cached))
		{
			return cached;
		}

		Texture2D? loaded = null;
		try
		{
			if (ResourceLoader.Exists(normalized))
			{
				loaded = ResourceLoader.Load<Texture2D>(normalized);
			}
		}
		catch
		{
			loaded = null;
		}

		_textureCache[normalized] = loaded;
		return loaded;
	}

	private static PowerModel? GetAutoMappedPower(CardExtraEffect effect)
	{
		return effect.Kind switch
		{
			CardExtraEffectKind.GainStrength or CardExtraEffectKind.LoseStrength => TryGetPower<StrengthPower>(),
			CardExtraEffectKind.GainDexterity or CardExtraEffectKind.LoseDexterity => TryGetPower<DexterityPower>(),
			CardExtraEffectKind.GainFocus or CardExtraEffectKind.LoseFocus => TryGetPower<FocusPower>(),
			CardExtraEffectKind.ApplyWeak => TryGetPower<WeakPower>(),
			CardExtraEffectKind.ApplyFrail => TryGetPower<FrailPower>(),
			CardExtraEffectKind.ApplyVulnerable => TryGetPower<VulnerablePower>(),
			CardExtraEffectKind.ApplyPoison => TryGetPower<PoisonPower>(),
			CardExtraEffectKind.ApplyDoom => TryGetPower<DoomPower>(),
			CardExtraEffectKind.ApplyConstrict => TryGetPower<ConstrictPower>(),
			CardExtraEffectKind.GainArtifact or CardExtraEffectKind.RemoveArtifact => TryGetPower<ArtifactPower>(),
			CardExtraEffectKind.GainThorns => TryGetPower<ThornsPower>(),
			CardExtraEffectKind.GainRegen => TryGetPower<RegenPower>(),
			CardExtraEffectKind.GainPlating => TryGetPower<PlatingPower>(),
			CardExtraEffectKind.GainIntangible => TryGetPower<IntangiblePower>(),
			CardExtraEffectKind.GainBuffer => TryGetPower<BufferPower>(),
			CardExtraEffectKind.GainVigor => TryGetPower<VigorPower>(),
			CardExtraEffectKind.GainBlur => TryGetPower<BlurPower>(),
			CardExtraEffectKind.GainRitual => TryGetPower<RitualPower>(),
			_ => GetFallbackPower()
		};
	}

	private static PowerModel? GetFallbackPower()
	{
		return TryGetPower<ArtifactPower>();
	}

	private static PowerModel? TryGetPower<T>() where T : PowerModel
	{
		try
		{
			return ModelDb.Power<T>();
		}
		catch
		{
			return null;
		}
	}
}
