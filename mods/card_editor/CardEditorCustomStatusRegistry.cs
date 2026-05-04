using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorCustomStatusDefinition
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public PowerType Type { get; init; } = PowerType.Buff;
	public CardExtraEffectStatusIconMode IconMode { get; init; } = CardExtraEffectStatusIconMode.Auto;
	public string? IconPowerId { get; init; }
	public string? CustomPackedIconPath { get; init; }
	public string? CustomBigIconPath { get; init; }
}

internal static class CardEditorCustomStatusRegistry
{
	internal const string CustomStatusIdPrefix = "card_editor_custom_status:";

	public static bool IsCustomStatusId(string? powerId)
		=> !string.IsNullOrWhiteSpace(powerId)
			&& powerId.Trim().StartsWith(CustomStatusIdPrefix, StringComparison.Ordinal);

	public static string? BuildId(string? name)
	{
		string normalized = NormalizeName(name);
		return string.IsNullOrWhiteSpace(normalized)
			? null
			: CustomStatusIdPrefix + Uri.EscapeDataString(normalized);
	}

	public static bool TryDecodeId(string? powerId, out string name)
	{
		name = string.Empty;
		if (!IsCustomStatusId(powerId))
		{
			return false;
		}

		try
		{
			string encoded = powerId!.Trim().Substring(CustomStatusIdPrefix.Length);
			name = NormalizeName(Uri.UnescapeDataString(encoded));
		}
		catch
		{
			name = string.Empty;
		}

		return !string.IsNullOrWhiteSpace(name);
	}

	public static IReadOnlyList<CardEditorCustomStatusDefinition> GetDefinitions(ModelId? preferredCardId = null)
	{
		Dictionary<string, CardEditorCustomStatusDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

		if (preferredCardId != null && preferredCardId != ModelId.none
			&& CardEditorOverrides.TryGetEffectiveOverride(preferredCardId, out CardOverride preferredOverride))
		{
			AddDefinitions(definitions, preferredOverride);
		}

		foreach (CardOverride overrideData in CardEditorOverrides.AllOverrides.Values)
		{
			AddDefinitions(definitions, overrideData);
		}

		return definitions.Values
			.OrderBy(def => def.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static CardEditorCustomStatusDefinition Resolve(string? powerId)
	{
		if (TryDecodeId(powerId, out string name))
		{
			string id = BuildId(name) ?? string.Empty;
			CardEditorCustomStatusDefinition? stored = GetDefinitions()
				.FirstOrDefault(def => string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase));
			if (stored != null)
			{
				return stored;
			}

			return new CardEditorCustomStatusDefinition
			{
				Id = id,
				Name = name,
				Description = null,
				Type = PowerType.Buff
			};
		}

		return new CardEditorCustomStatusDefinition
		{
			Id = string.Empty,
			Name = "Custom Status",
			Type = PowerType.Buff
		};
	}

	private static void AddDefinitions(Dictionary<string, CardEditorCustomStatusDefinition> definitions, CardOverride? overrideData)
	{
		if (overrideData == null)
		{
			return;
		}

		AddDefinitions(definitions, overrideData.ExtraEffects);
		AddDefinitions(definitions, overrideData.Upgrade?.ExtraEffects);
	}

	private static void AddDefinitions(Dictionary<string, CardEditorCustomStatusDefinition> definitions, IEnumerable<CardExtraEffect>? effects)
	{
		if (effects == null)
		{
			return;
		}

		foreach (CardExtraEffect effect in effects)
		{
			AddDefinition(definitions, effect);
		}
	}

	private static void AddDefinition(Dictionary<string, CardEditorCustomStatusDefinition> definitions, CardExtraEffect? effect)
	{
		if (effect == null)
		{
			return;
		}

		if (effect.AsPower
			&& CardEditorExtraEffects.SupportsAsPower(effect.Kind)
			&& !string.IsNullOrWhiteSpace(effect.CustomPowerName))
		{
			string name = NormalizeName(effect.CustomPowerName);
			string? id = BuildId(name);
			if (!string.IsNullOrWhiteSpace(id) && !definitions.ContainsKey(id))
			{
				definitions[id] = new CardEditorCustomStatusDefinition
				{
					Id = id,
					Name = name,
					Description = string.IsNullOrWhiteSpace(effect.CustomPowerDescription) ? null : effect.CustomPowerDescription.Trim(),
					Type = InferPowerType(effect),
					IconMode = effect.StatusIconMode,
					IconPowerId = string.IsNullOrWhiteSpace(effect.StatusIconPowerId) ? null : effect.StatusIconPowerId.Trim(),
					CustomPackedIconPath = string.IsNullOrWhiteSpace(effect.StatusCustomPackedIconPath) ? null : effect.StatusCustomPackedIconPath.Trim(),
					CustomBigIconPath = string.IsNullOrWhiteSpace(effect.StatusCustomBigIconPath) ? null : effect.StatusCustomBigIconPath.Trim()
				};
			}
		}

		AddDefinition(definitions, effect.BranchEffect);
	}

	private static PowerType InferPowerType(CardExtraEffect effect)
	{
		PowerType? iconType = TryResolveBaseGamePowerType(effect.StatusIconPowerId);
		if (iconType.HasValue)
		{
			return iconType.Value;
		}

		PowerType? appliedType = TryResolveBaseGamePowerType(effect.PowerId);
		if (appliedType.HasValue)
		{
			return appliedType.Value;
		}

		return effect.Kind switch
		{
			CardExtraEffectKind.ApplyWeak
				or CardExtraEffectKind.ApplyFrail
				or CardExtraEffectKind.ApplyVulnerable
				or CardExtraEffectKind.ApplyPoison
				or CardExtraEffectKind.ApplyDoom
				or CardExtraEffectKind.ApplyConstrict
				or CardExtraEffectKind.LoseStrength
				or CardExtraEffectKind.LoseDexterity
				or CardExtraEffectKind.LoseFocus => PowerType.Debuff,
			_ => PowerType.Buff
		};
	}

	private static PowerType? TryResolveBaseGamePowerType(string? powerIdText)
	{
		if (string.IsNullOrWhiteSpace(powerIdText) || IsCustomStatusId(powerIdText))
		{
			return null;
		}

		try
		{
			ModelId id = ModelId.Deserialize(powerIdText.Trim());
			PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(id);
			return power?.Type;
		}
		catch
		{
			return null;
		}
	}

	private static string NormalizeName(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return string.Empty;
		}

		return name.Trim();
	}
}
