using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class CardEditorVisibleExtraEffectPowerPatches
{
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

		__result = BuildHoverTips(mirror);
		return false;
	}

	private static IReadOnlyList<IHoverTip> BuildHoverTips(CardEditorVisibleExtraEffectPower mirror)
	{
		string title = GetTooltipTitle(mirror);
		string body = mirror.GetTooltipBody();
		if (string.IsNullOrWhiteSpace(body))
		{
			body = title;
		}

		List<IHoverTip> tips = new List<IHoverTip>
		{
			CreateRuntimeHoverTip(title, body, ResolveIcon(mirror, bigIcon: false))
		};
		tips.AddRange(CardEditorVanillaKeywordSupport.InferHoverTips(body));
		return IHoverTip.RemoveDupes(tips).ToList();
	}

	private static string GetTooltipTitle(CardEditorVisibleExtraEffectPower mirror)
	{
		CardExtraEffect? effect = mirror.SourceEffect;
		if (!string.IsNullOrWhiteSpace(effect?.CustomPowerName))
		{
			return effect.CustomPowerName.Trim();
		}

		if (!string.IsNullOrWhiteSpace(effect?.CustomKeywordName))
		{
			return effect.CustomKeywordName.Trim();
		}

		try
		{
			string? powerTitle = ResolveTooltipPower(effect)?.Title.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(powerTitle))
			{
				return powerTitle.Trim();
			}
		}
		catch
		{
		}

		if (effect != null)
		{
			string? effectLabel = CardEditorExtraEffects.Definitions
				.FirstOrDefault(def => def.Kind == effect.Kind)
				?.Label;
			if (!string.IsNullOrWhiteSpace(effectLabel))
			{
				return effectLabel.Trim();
			}
		}

		if (!string.IsNullOrWhiteSpace(mirror.SourceCard?.Title))
		{
			return mirror.SourceCard.Title.Trim();
		}

		try
		{
			string fallback = mirror.Title.GetFormattedText();
			if (!string.IsNullOrWhiteSpace(fallback))
			{
				return fallback.Trim();
			}
		}
		catch
		{
		}

		return "Card Editor Power";
	}

	private static IHoverTip CreateRuntimeHoverTip(string title, string description, Texture2D? icon)
	{
		string safeTitle = string.IsNullOrWhiteSpace(title) ? "Card Editor Power" : title.Trim();
		string safeDescription = string.IsNullOrWhiteSpace(description) ? safeTitle : description.Trim();
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(safeTitle)));
		string titleKey = "CARD_EDITOR.RUNTIME_POWER_TIP." + hash + ".title";

		if (LocManager.Instance != null)
		{
			try
			{
				LocTable table = LocManager.Instance.GetTable("extensions");
				table.MergeWith(new Dictionary<string, string>
				{
					[titleKey] = safeTitle
				});
			}
			catch
			{
			}
		}

		return new HoverTip(new LocString("extensions", titleKey), safeDescription, icon);
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

	private static PowerModel? ResolveTooltipPower(CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(effect.PowerId))
		{
			PowerModel? appliedPower = ResolveBaseGamePower(effect.PowerId);
			if (appliedPower != null)
			{
				return appliedPower;
			}
		}

		if (!string.IsNullOrWhiteSpace(effect.StatusIconPowerId))
		{
			PowerModel? statusIconPower = ResolveBaseGamePower(effect.StatusIconPowerId);
			if (statusIconPower != null)
			{
				return statusIconPower;
			}
		}

		return GetAutoMappedPower(effect);
	}

	private static PowerModel? ResolveBaseGamePower(string? powerIdText)
	{
		if (string.IsNullOrWhiteSpace(powerIdText))
		{
			return null;
		}

		try
		{
			ModelId powerId = ModelId.Deserialize(powerIdText.Trim());
			return ModelDb.GetByIdOrNull<PowerModel>(powerId);
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
		return CardEditorCustomIconLoader.LoadTexture(path);
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
