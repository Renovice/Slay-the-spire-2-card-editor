using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheSpire2Mod.CardEditor;

internal sealed class CardEditorCustomStatusPower : PowerModel
{
	private sealed class PendingPayloadScope(CardEditorCustomStatusDefinition? previous) : IDisposable
	{
		public void Dispose()
		{
			_pendingPayload.Value = previous;
		}
	}

	private static readonly AsyncLocal<CardEditorCustomStatusDefinition?> _pendingPayload = new();

	private string _customStatusId = string.Empty;
	private string _customStatusName = "Custom Status";
	private string? _customStatusDescription;
	private ModelId? _sourceCardId;
	private IReadOnlyList<CardExtraEffect> _behaviorEffects = Array.Empty<CardExtraEffect>();
	private PowerType _customStatusType = PowerType.Buff;
	private CardExtraEffectStatusIconMode _iconMode = CardExtraEffectStatusIconMode.Auto;
	private string? _iconPowerId;
	private string? _customPackedIconPath;
	private string? _customBigIconPath;

	public string CustomStatusId => _customStatusId;

	public string CustomStatusName => _customStatusName;

	public string? CustomStatusDescription => _customStatusDescription;

	public CardExtraEffectStatusIconMode IconMode => _iconMode;

	public string? IconPowerId => _iconPowerId;

	public string? CustomPackedIconPath => _customPackedIconPath;

	public string? CustomBigIconPath => _customBigIconPath;

	public override LocString Title => CreateRuntimeLocString("CARD_EDITOR.CUSTOM_STATUS_TITLE.", _customStatusName);

	// During the first PowerCmd.Apply, the hook chain (Artifact's debuff negation) reads Type
	// BEFORE BeforeApplied copies the definition onto this instance. Consult the pending payload
	// so a Debuff-classified custom status is blocked by Artifact on the first application too.
	public override PowerType Type => string.IsNullOrEmpty(_customStatusId) && _pendingPayload.Value != null
		? _pendingPayload.Value.Type
		: _customStatusType;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

	public override bool ShouldPlayVfx => false;

	public override int DisplayAmount => Math.Max(0, Amount);

	protected override bool IsVisibleInternal => true;

	public static IDisposable PushPendingPayload(CardEditorCustomStatusDefinition definition)
	{
		CardEditorCustomStatusDefinition? previous = _pendingPayload.Value;
		_pendingPayload.Value = definition;
		return new PendingPayloadScope(previous);
	}

	public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
	{
		ApplyDefinition(_pendingPayload.Value);
		return Task.CompletedTask;
	}

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		_behaviorEffects = _behaviorEffects
			.Select(CardEditorExtraEffects.CloneEffect)
			.ToList();
	}

	internal void SyncDefinition(CardEditorCustomStatusDefinition definition)
	{
		ApplyDefinition(definition);
		InvokeDisplayAmountChanged();
	}

	internal bool MatchesConfiguredId(string? powerId)
	{
		if (!CardEditorCustomStatusRegistry.TryDecodeId(powerId, out string _))
		{
			return false;
		}

		return string.Equals(_customStatusId, powerId?.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	internal string GetTooltipBody()
	{
		if (!string.IsNullOrWhiteSpace(_customStatusDescription))
		{
			return _customStatusDescription.Trim();
		}

		string? generated = BuildGeneratedTooltipBody();
		if (!string.IsNullOrWhiteSpace(generated))
		{
			return generated.Trim();
		}

		return _customStatusName;
	}

	internal Texture2D? ResolveIcon(bool bigIcon)
	{
		Texture2D? resolved = _iconMode switch
		{
			CardExtraEffectStatusIconMode.BaseGame => ResolveBaseGameIcon(_iconPowerId, bigIcon),
			CardExtraEffectStatusIconMode.Custom => ResolveCustomIcon(bigIcon),
			_ => ResolveBaseGameIcon(_iconPowerId, bigIcon)
		};

		if (resolved != null)
		{
			return resolved;
		}

		return ResolveFallbackIcon(bigIcon);
	}

	private void ApplyDefinition(CardEditorCustomStatusDefinition? definition)
	{
		if (definition == null)
		{
			return;
		}

		_customStatusId = definition.Id ?? string.Empty;
		_customStatusName = string.IsNullOrWhiteSpace(definition.Name) ? "Custom Status" : definition.Name.Trim();
		_customStatusDescription = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim();
		_sourceCardId = definition.SourceCardId;
		_behaviorEffects = definition.BehaviorEffects
			.Where(effect => effect != null)
			.Select(CardEditorExtraEffects.CloneEffect)
			.ToList();
		_customStatusType = definition.Type;
		_iconMode = definition.IconMode;
		_iconPowerId = string.IsNullOrWhiteSpace(definition.IconPowerId) ? null : definition.IconPowerId.Trim();
		_customPackedIconPath = string.IsNullOrWhiteSpace(definition.CustomPackedIconPath) ? null : definition.CustomPackedIconPath.Trim();
		_customBigIconPath = string.IsNullOrWhiteSpace(definition.CustomBigIconPath) ? null : definition.CustomBigIconPath.Trim();
	}

	private string? BuildGeneratedTooltipBody()
	{
		if (_behaviorEffects == null || _behaviorEffects.Count == 0)
		{
			return null;
		}

		CardModel? sourceCard = BuildTooltipSourceCard(_sourceCardId);
		List<string> lines = new();
		foreach (CardExtraEffect effect in _behaviorEffects)
		{
			string? line = null;
			if (sourceCard != null)
			{
				try
				{
					line = CardEditorExtraEffects.FormatSingleEffectLine(sourceCard, effect);
				}
				catch
				{
					line = null;
				}
			}
			if (string.IsNullOrWhiteSpace(line))
			{
				CardExtraEffectDefinition? definition = CardEditorExtraEffects.Definitions.FirstOrDefault(def => def.Kind == effect.Kind);
				line = definition != null
					? CardEditorExtraEffects.DefinitionDisplayLabel(definition)
					: effect.Kind.ToString();
				if (effect.Amount > 0)
				{
					line = $"{line}: {effect.Amount}";
				}
			}

			if (!string.IsNullOrWhiteSpace(line))
			{
				lines.Add(line.Trim());
			}
		}

		return lines.Count == 0 ? null : string.Join("\n", lines);
	}

	private static CardModel? BuildTooltipSourceCard(ModelId? sourceCardId)
	{
		if (sourceCardId == null || sourceCardId == ModelId.none)
		{
			return null;
		}

		try
		{
			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			return canonical == null ? null : CardEditorOverrides.BuildPreview(canonical);
		}
		catch
		{
			return null;
		}
	}

	private Texture2D? ResolveCustomIcon(bool bigIcon)
	{
		string? preferredPath = bigIcon
			? _customBigIconPath ?? _customPackedIconPath
			: _customPackedIconPath ?? _customBigIconPath;
		Texture2D? preferred = CardEditorCustomIconLoader.LoadTexture(preferredPath);
		if (preferred != null)
		{
			return preferred;
		}

		string? fallbackPath = bigIcon ? _customPackedIconPath : _customBigIconPath;
		return CardEditorCustomIconLoader.LoadTexture(fallbackPath);
	}

	private static Texture2D? ResolveBaseGameIcon(string? powerIdText, bool bigIcon)
	{
		if (string.IsNullOrWhiteSpace(powerIdText))
		{
			return null;
		}

		try
		{
			ModelId id = ModelId.Deserialize(powerIdText.Trim());
			PowerModel? power = ModelDb.GetByIdOrNull<PowerModel>(id);
			return power == null ? null : (bigIcon ? power.BigIcon : power.Icon);
		}
		catch
		{
			return null;
		}
	}

	private static Texture2D? ResolveFallbackIcon(bool bigIcon)
	{
		try
		{
			PowerModel? power = ModelDb.Power<ArtifactPower>();
			return bigIcon ? power?.BigIcon : power?.Icon;
		}
		catch
		{
			return null;
		}
	}

	internal static LocString CreateRuntimeLocString(string prefix, string text)
	{
		string safeText = string.IsNullOrWhiteSpace(text) ? "Custom Status" : text.Trim();
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(safeText)));
		string key = prefix + hash;

		if (LocManager.Instance != null)
		{
			try
			{
				LocTable table = LocManager.Instance.GetTable("extensions");
				table.MergeWith(new Dictionary<string, string>
				{
					[key] = safeText
				});
			}
			catch
			{
			}
		}

		return new LocString("extensions", key);
	}
}

[HarmonyPatch]
internal static class CardEditorCustomStatusPowerPatches
{
	[HarmonyPatch(typeof(PowerModel), "get_Icon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_Icon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorCustomStatusPower customStatus)
		{
			return true;
		}

		__result = customStatus.ResolveIcon(bigIcon: false);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
	[HarmonyPrefix]
	private static bool PowerModel_get_BigIcon_Prefix(PowerModel __instance, ref Texture2D? __result)
	{
		if (__instance is not CardEditorCustomStatusPower customStatus)
		{
			return true;
		}

		__result = customStatus.ResolveIcon(bigIcon: true);
		return false;
	}

	[HarmonyPatch(typeof(PowerModel), "get_HoverTips")]
	[HarmonyPrefix]
	private static bool PowerModel_get_HoverTips_Prefix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		if (__instance is not CardEditorCustomStatusPower customStatus)
		{
			return true;
		}

		string title = customStatus.CustomStatusName;
		string body = customStatus.GetTooltipBody();
		List<IHoverTip> tips = new()
		{
			new HoverTip(
				CardEditorCustomStatusPower.CreateRuntimeLocString("CARD_EDITOR.CUSTOM_STATUS_TIP.", title),
				string.IsNullOrWhiteSpace(body) ? title : body,
				customStatus.ResolveIcon(bigIcon: false))
		};
		tips.AddRange(CardEditorVanillaKeywordSupport.InferHoverTips(body));
		__result = IHoverTip.RemoveDupes(tips).ToList();
		return false;
	}
}
