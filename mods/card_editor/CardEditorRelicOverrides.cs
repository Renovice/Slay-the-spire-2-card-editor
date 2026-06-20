using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Unlocks;

namespace SlayTheSpire2Mod.CardEditor;

// Relic effect triggers. Phase 1 starter set; expanded to the full AbstractModel hook
// surface in Phase 2 (see Notes/RELIC_EFFECTS_PLAN.md).
public enum RelicTriggerKind
{
	OnCombatStart = 0,
	OnTurnStart = 1,
	OnTurnEnd = 2,
	OnCardPlayed = 3,
	OnPickup = 4,
	OnCombatEnd = 5,
	OnEnemyKilled = 6,
	OnDamageTaken = 7,
	// Phase 2 expansion (2026-06-19): reactive in-combat hooks bringing relic triggers closer to the
	// card editor's surface. New values are appended (never renumbered) so saved overrides stay valid.
	OnCombatVictory = 8,
	OnEnemyTurnStart = 9,
	OnEnemyTurnEnd = 10,
	OnCardDrawn = 11,
	OnCardDiscarded = 12,
	OnCardExhausted = 13,
	OnShuffle = 14,
	OnDamageDealt = 15,
	OnBlockGained = 16,
	OnHpLost = 17,
	OnHeal = 18,
	OnEnergyReset = 19,
	OnOrbChanneled = 20,
	OnStarsGained = 21,
	OnHandDraw = 22,
}

// One configured relic effect: a card-editor effect plus the relic trigger that fires it.
public sealed class RelicEffectEntry
{
	public RelicTriggerKind Trigger { get; set; }
	public CardExtraEffect Effect { get; set; } = null!;
}

public sealed class RelicOverride
{
	public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
	public bool? CustomDescriptionEnabled { get; set; }
	public string? CustomDescription { get; set; }
	public HashSet<string>? PoolKeys { get; set; }
	public HashSet<string>? FixedSourceKeys { get; set; }
	public List<RelicEffectEntry>? ExtraEffects { get; set; }

	// Every trigger group the user created, even ones with no effects yet, so an intentionally
	// parked (empty) trigger group survives a reopen instead of silently vanishing.
	public List<RelicTriggerKind>? EffectTriggers { get; set; }

	public bool IsEmpty()
	{
		return (DynamicVarBaseValues == null || DynamicVarBaseValues.Count == 0)
			&& CustomDescriptionEnabled == null
			&& string.IsNullOrWhiteSpace(CustomDescription)
			&& PoolKeys == null
			&& FixedSourceKeys == null
			&& (ExtraEffects == null || ExtraEffects.Count == 0)
			&& (EffectTriggers == null || EffectTriggers.Count == 0);
	}
}

internal static class CardEditorRelicOverrides
{
	private static readonly Dictionary<ModelId, RelicOverride> _overrides = new();

	internal static IReadOnlyDictionary<ModelId, RelicOverride> AllOverrides => _overrides;

	internal static bool HasAnyOverrides => _overrides.Count > 0;
	internal static int Revision { get; private set; }

	internal static void EnsureLoaded()
	{
		CardEditorRelicOverrideStore.EnsureLoaded();
	}

	internal static bool TryGet(ModelId relicId, out RelicOverride overrideData)
	{
		return _overrides.TryGetValue(relicId, out overrideData!);
	}

	internal static RelicOverride? Get(ModelId relicId)
	{
		_overrides.TryGetValue(relicId, out RelicOverride? overrideData);
		return overrideData;
	}

	internal static void Set(ModelId relicId, RelicOverride? overrideData)
	{
		if (overrideData == null || overrideData.IsEmpty())
		{
			if (_overrides.Remove(relicId))
			{
				Revision++;
			}
			return;
		}

		_overrides[relicId] = Clone(overrideData);
		Revision++;
	}

	internal static void SetAndSave(ModelId relicId, RelicOverride? overrideData)
	{
		Set(relicId, overrideData);
		CardEditorRelicOverrideStore.Save();
	}

	internal static void ReplaceAll(IReadOnlyDictionary<ModelId, RelicOverride> overrides)
	{
		_overrides.Clear();
		foreach ((ModelId relicId, RelicOverride overrideData) in overrides)
		{
			if (overrideData != null && !overrideData.IsEmpty())
			{
				_overrides[relicId] = Clone(overrideData);
			}
		}

		Revision++;
	}

	internal static Dictionary<ModelId, RelicOverride> ExportSnapshot()
	{
		EnsureLoaded();
		Dictionary<ModelId, RelicOverride> snapshot = new(_overrides.Count);
		foreach ((ModelId relicId, RelicOverride overrideData) in _overrides)
		{
			if (overrideData == null || overrideData.IsEmpty())
			{
				continue;
			}

			snapshot[relicId] = Clone(overrideData);
		}
		return snapshot;
	}

	internal static RelicOverride Clone(RelicOverride source)
	{
		return new RelicOverride
		{
			DynamicVarBaseValues = source.DynamicVarBaseValues != null
				? new Dictionary<string, decimal>(source.DynamicVarBaseValues, StringComparer.Ordinal)
				: null,
			CustomDescriptionEnabled = source.CustomDescriptionEnabled,
			CustomDescription = source.CustomDescription,
			PoolKeys = source.PoolKeys != null
				? new HashSet<string>(source.PoolKeys, StringComparer.Ordinal)
				: null,
			FixedSourceKeys = source.FixedSourceKeys != null
				? new HashSet<string>(source.FixedSourceKeys, StringComparer.Ordinal)
				: null,
			ExtraEffects = source.ExtraEffects != null
				? source.ExtraEffects
					.Where(e => e?.Effect != null)
					.Select(e => new RelicEffectEntry { Trigger = e.Trigger, Effect = CardEditorExtraEffects.CloneEffect(e.Effect) })
					.ToList()
				: null,
			EffectTriggers = source.EffectTriggers != null
				? new List<RelicTriggerKind>(source.EffectTriggers)
				: null
		};
	}

	internal static RelicModel BuildPreview(RelicModel canonicalRelic)
	{
		RelicModel preview = canonicalRelic.ToMutable();
		ApplyTo(preview);
		return preview;
	}

	internal static void ApplyTo(RelicModel relic)
	{
		if (relic == null || !relic.IsMutable)
		{
			return;
		}
		if (!_overrides.TryGetValue(relic.Id, out RelicOverride? overrideData))
		{
			return;
		}
		ApplyOverride(relic, overrideData);
	}

	internal static void ApplyOverride(RelicModel relic, RelicOverride overrideData)
	{
		if (overrideData.DynamicVarBaseValues != null)
		{
			foreach ((string key, decimal value) in overrideData.DynamicVarBaseValues)
			{
				if (relic.DynamicVars.TryGetValue(key, out DynamicVar? dynamicVar))
				{
					dynamicVar.BaseValue = value;
				}
			}
		}
	}

	internal static bool TryBuildCustomDynamicDescription(RelicModel relic, out LocString locString)
	{
		locString = null!;
		if (relic == null
			|| !_overrides.TryGetValue(relic.Id, out RelicOverride? overrideData)
			|| overrideData.CustomDescriptionEnabled != true)
		{
			return false;
		}

		string text = overrideData.CustomDescription ?? string.Empty;
		locString = CreateRuntimeRelicLocString("CARD_EDITOR.RELIC_DESCRIPTION.", text);
		relic.DynamicVars.AddTo(locString);
		string prefix = EnergyIconHelper.GetPrefix(relic);
		locString.Add("energyPrefix", prefix);
		locString.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/star_icon.png[/img]");
		foreach (KeyValuePair<string, object> variable in locString.Variables)
		{
			if (variable.Value is EnergyVar energyVar)
			{
				energyVar.ColorPrefix = prefix;
			}
		}
		return true;
	}

	private static LocString CreateRuntimeRelicLocString(string prefix, string text)
	{
		string safeText = text ?? string.Empty;
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

	internal static List<RelicPoolModel> EditablePools()
	{
		return ModelDb.AllRelicPools
			.Where(pool =>
			{
				string name = pool.GetType().Name;
				return !string.Equals(name, "DeprecatedRelicPool", StringComparison.Ordinal)
					&& !string.Equals(name, "FallbackRelicPool", StringComparison.Ordinal);
			})
			.OrderBy(GetPoolLabel, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	internal static string GetPoolKey(RelicPoolModel pool)
	{
		return pool.GetType().FullName ?? pool.GetType().Name;
	}

	internal static string GetPoolLabel(RelicPoolModel pool)
	{
		string name = pool.GetType().Name;
		switch (name)
		{
			case "SharedRelicPool":
				return "Shared Reward Pool";
			case "EventRelicPool":
				return "Event/Special Pool";
			case "IroncladRelicPool":
				return "Ironclad Reward Pool";
			case "SilentRelicPool":
				return "Silent Reward Pool";
			case "DefectRelicPool":
				return "Defect Reward Pool";
			case "NecrobinderRelicPool":
				return "Necrobinder Reward Pool";
			case "RegentRelicPool":
				return "Regent Reward Pool";
			case "DeprecatedRelicPool":
				return "Deprecated Pool";
			case "FallbackRelicPool":
				return "Fallback Pool";
		}

		return name.EndsWith("RelicPool", StringComparison.Ordinal)
			? name[..^"RelicPool".Length]
			: name;
	}

	internal static string GetPoolDescription(RelicPoolModel pool)
	{
		string name = pool.GetType().Name;
		return name switch
		{
			"SharedRelicPool" => "Normal reward pool shared by all characters. Standard relic rewards use Shared plus the current character's pool.",
			"EventRelicPool" => "Broad special/event catalog pool. This is not a per-event source list; many event and ancient relic grants are hard-coded event options.",
			"IroncladRelicPool" => "Normal reward pool added while playing Ironclad.",
			"SilentRelicPool" => "Normal reward pool added while playing Silent.",
			"DefectRelicPool" => "Normal reward pool added while playing Defect.",
			"NecrobinderRelicPool" => "Normal reward pool added while playing Necrobinder.",
			"RegentRelicPool" => "Normal reward pool added while playing Regent.",
			"DeprecatedRelicPool" => "Internal deprecated pool, hidden from the relic editor.",
			"FallbackRelicPool" => "Internal fallback pool, hidden from the relic editor.",
			_ => "Relic pool exposed by the game."
		};
	}

	internal static HashSet<string> GetVanillaPoolKeys(RelicModel relic)
	{
		HashSet<string> keys = new(StringComparer.Ordinal);
		foreach (RelicPoolModel pool in ModelDb.AllRelicPools)
		{
			if (pool.AllRelicIds.Contains(relic.Id))
			{
				keys.Add(GetPoolKey(pool));
			}
		}
		return keys;
	}

	internal static HashSet<string> GetEffectivePoolKeys(RelicModel relic)
	{
		if (_overrides.TryGetValue(relic.Id, out RelicOverride? overrideData)
			&& overrideData.PoolKeys != null)
		{
			return new HashSet<string>(overrideData.PoolKeys, StringComparer.Ordinal);
		}

		return GetVanillaPoolKeys(relic);
	}

	// True only when the user EXPLICITLY emptied a relic's pool membership (an override exists with zero
	// pool keys). The editor only writes PoolKeys when it differs from vanilla (NRelicEditorPopup), so an
	// empty set means "removed from every pool it used to be in" - not a relic that was simply never pooled.
	internal static bool IsRemovedFromAllPools(RelicModel relic)
	{
		return relic != null
			&& _overrides.TryGetValue(relic.Id, out RelicOverride? overrideData)
			&& overrideData.PoolKeys != null
			&& overrideData.PoolKeys.Count == 0;
	}

	internal static IEnumerable<RelicModel> ApplyPoolOverrides(RelicPoolModel pool, IEnumerable<RelicModel> result)
	{
		string poolKey = GetPoolKey(pool);
		List<RelicModel> relics = result?.Where(r => r != null).ToList() ?? new List<RelicModel>();
		if (_overrides.Count == 0)
		{
			return relics;
		}

		relics.RemoveAll(relic =>
			_overrides.TryGetValue(relic.Id, out RelicOverride? overrideData)
			&& overrideData.PoolKeys != null
			&& !overrideData.PoolKeys.Contains(poolKey));

		HashSet<ModelId> existingIds = relics.Select(r => r.Id).ToHashSet();
		foreach ((ModelId relicId, RelicOverride overrideData) in _overrides)
		{
			if (overrideData.PoolKeys == null || !overrideData.PoolKeys.Contains(poolKey) || existingIds.Contains(relicId))
			{
				continue;
			}

			RelicModel? relic = ModelDb.GetByIdOrNull<RelicModel>(relicId);
			if (relic != null)
			{
				relics.Add(relic);
				existingIds.Add(relicId);
			}
		}

		return relics;
	}

	internal static RelicPoolModel? ResolveFirstEffectivePool(RelicModel relic)
	{
		HashSet<string> keys = GetEffectivePoolKeys(relic);
		if (keys.Count == 0)
		{
			return null;
		}

		foreach (RelicPoolModel pool in ModelDb.AllRelicPools)
		{
			if (keys.Contains(GetPoolKey(pool)))
			{
				return pool;
			}
		}

		return null;
	}

	internal static List<RelicSourceSummary> EditableFixedSources()
	{
		Dictionary<string, RelicSourceSummary> summaries = new(StringComparer.Ordinal);
		foreach ((PropertyInfo property, object source) in EnumerateFixedRelicSourceObjects())
		{
			bool isAncient = IsAncientSource(property, source);
			string key = BuildFixedSourceKey(isAncient ? "Ancient" : "Event", source);
			string id = GetSourceIdText(source);
			string label = GetSourceTitleText(source);
			string sourceName = string.IsNullOrWhiteSpace(label) ? id : label;
			summaries.TryAdd(key, new RelicSourceSummary(
				key: key,
				kind: isAncient ? "Ancient" : "Event",
				label: $"{(isAncient ? "Ancient" : "Event")}: {sourceName}",
				description: isAncient
					? "Ancient option source. These choices come from AncientEventModel.AllPossibleOptions, not RelicPoolModel."
					: "Fixed event option source. This is listed for visibility; generic editing is not enabled because event options can have custom scripts.",
				editable: isAncient));
		}

		return summaries.Values
			.OrderByDescending(summary => summary.Editable)
			.ThenBy(summary => summary.Kind, StringComparer.OrdinalIgnoreCase)
			.ThenBy(summary => summary.Label, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	internal static HashSet<string> GetVanillaFixedSourceKeys(RelicModel relic)
	{
		HashSet<string> keys = new(StringComparer.Ordinal);
		foreach (RelicSourceSummary summary in EnumerateFixedRelicSourceSummaries(relic.Id))
		{
			keys.Add(summary.Key);
		}
		return keys;
	}

	internal static HashSet<string> GetEffectiveFixedSourceKeys(RelicModel relic)
	{
		if (_overrides.TryGetValue(relic.Id, out RelicOverride? overrideData)
			&& overrideData.FixedSourceKeys != null)
		{
			return new HashSet<string>(overrideData.FixedSourceKeys, StringComparer.Ordinal);
		}

		return GetVanillaFixedSourceKeys(relic);
	}

	internal static IEnumerable<EventOption> ApplyFixedSourceOverrides(AncientEventModel source, IEnumerable<EventOption> result)
	{
		List<EventOption> options = result?.Where(option => option != null).ToList() ?? new List<EventOption>();
		if (source == null || _overrides.Count == 0)
		{
			return options;
		}

		string sourceKey = BuildFixedSourceKey("Ancient", source);
		options.RemoveAll(option =>
			option.Relic != null
			&& TryGetRelicModelId(option.Relic, out ModelId relicId)
			&& _overrides.TryGetValue(relicId, out RelicOverride? overrideData)
			&& overrideData.FixedSourceKeys != null
			&& !overrideData.FixedSourceKeys.Contains(sourceKey));

		HashSet<ModelId> existingIds = options
			.Select(option => option.Relic)
			.Where(relic => relic != null)
			.Select(relic => relic!.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();

		foreach ((ModelId relicId, RelicOverride overrideData) in _overrides)
		{
			if (overrideData.FixedSourceKeys == null
				|| !overrideData.FixedSourceKeys.Contains(sourceKey)
				|| existingIds.Contains(relicId))
			{
				continue;
			}

			RelicModel? relic = ModelDb.GetByIdOrNull<RelicModel>(relicId);
			if (relic == null || !TryCreateAncientRelicOption(source, relic, out EventOption? option) || option == null)
			{
				continue;
			}

			options.Add(option);
			existingIds.Add(relicId);
		}

		return options;
	}

	internal static List<RelicSourceSummary> GetFixedRelicSourceSummaries(RelicModel relic)
	{
		if (relic == null)
		{
			return new List<RelicSourceSummary>();
		}

		Dictionary<string, RelicSourceSummary> summaries = new(StringComparer.Ordinal);
		foreach (RelicSourceSummary summary in EnumerateFixedRelicSourceSummaries(relic.Id))
		{
			summaries.TryAdd(summary.Key, summary);
		}

		return summaries.Values
			.OrderBy(summary => summary.Kind, StringComparer.OrdinalIgnoreCase)
			.ThenBy(summary => summary.Label, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static IEnumerable<RelicSourceSummary> EnumerateFixedRelicSourceSummaries(ModelId relicId)
	{
		foreach ((PropertyInfo property, object source) in EnumerateFixedRelicSourceObjects())
		{
			if (!SourceContainsRelicOption(source, relicId))
			{
				continue;
			}

			bool isAncient = IsAncientSource(property, source);
			string kind = isAncient ? "Ancient" : "Event";
			string id = GetSourceIdText(source);
			string label = GetSourceTitleText(source);
			string sourceName = string.IsNullOrWhiteSpace(label) ? id : label;
			yield return new RelicSourceSummary(
				key: BuildFixedSourceKey(kind, source),
				kind: kind,
				label: $"{kind}: {sourceName}",
				description: "Fixed source detected from event options. This is not a random relic reward pool.",
				editable: isAncient);
		}
	}

	private static IEnumerable<(PropertyInfo Property, object Source)> EnumerateFixedRelicSourceObjects()
	{
		HashSet<string> seen = new(StringComparer.Ordinal);
		foreach (PropertyInfo property in typeof(ModelDb).GetProperties(BindingFlags.Public | BindingFlags.Static))
		{
			if (property.GetIndexParameters().Length != 0
				|| property.PropertyType == typeof(string)
				|| !typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
			{
				continue;
			}

			IEnumerable? sources;
			try
			{
				sources = property.GetValue(null) as IEnumerable;
			}
			catch
			{
				continue;
			}

			if (sources == null)
			{
				continue;
			}

			foreach (object? source in sources)
			{
				if (source == null)
				{
					continue;
				}
				PropertyInfo? optionsProperty = source.GetType().GetProperty("AllPossibleOptions", BindingFlags.Instance | BindingFlags.Public);
				if (optionsProperty == null || !typeof(IEnumerable).IsAssignableFrom(optionsProperty.PropertyType))
				{
					continue;
				}

				bool isAncient = IsAncientSource(property, source);
				string key = BuildFixedSourceKey(isAncient ? "Ancient" : "Event", source);
				if (seen.Add(key))
				{
					yield return (property, source);
				}
			}
		}
	}

	private static string BuildFixedSourceKey(string kind, object source)
	{
		return $"{kind}:{GetSourceIdText(source)}";
	}

	private static bool SourceContainsRelicOption(object source, ModelId relicId)
	{
		PropertyInfo? optionsProperty = source.GetType().GetProperty("AllPossibleOptions", BindingFlags.Instance | BindingFlags.Public);
		if (optionsProperty == null || !typeof(IEnumerable).IsAssignableFrom(optionsProperty.PropertyType))
		{
			return false;
		}

		IEnumerable? options;
		try
		{
			options = optionsProperty.GetValue(source) as IEnumerable;
		}
		catch
		{
			return false;
		}

		if (options == null)
		{
			return false;
		}

		foreach (object? option in options)
		{
			if (option == null)
			{
				continue;
			}

			PropertyInfo? relicProperty = option.GetType().GetProperty("Relic", BindingFlags.Instance | BindingFlags.Public);
			if (relicProperty == null)
			{
				continue;
			}

			object? optionRelic;
			try
			{
				optionRelic = relicProperty.GetValue(option);
			}
			catch
			{
				continue;
			}

			if (TryGetRelicModelId(optionRelic, out ModelId optionRelicId) && optionRelicId == relicId)
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryGetRelicModelId(object? relicLike, out ModelId relicId)
	{
		relicId = ModelId.none;
		if (relicLike == null)
		{
			return false;
		}

		if (relicLike is RelicModel relic)
		{
			relicId = relic.CanonicalInstance?.Id ?? relic.Id;
			return relicId != ModelId.none;
		}

		try
		{
			PropertyInfo? canonicalProperty = relicLike.GetType().GetProperty("CanonicalInstance", BindingFlags.Instance | BindingFlags.Public);
			if (canonicalProperty?.GetValue(relicLike) is RelicModel canonicalRelic)
			{
				relicId = canonicalRelic.Id;
				return relicId != ModelId.none;
			}
		}
		catch
		{
		}

		try
		{
			PropertyInfo? idProperty = relicLike.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
			if (idProperty?.GetValue(relicLike) is ModelId id)
			{
				relicId = id;
				return relicId != ModelId.none;
			}
		}
		catch
		{
		}

		return false;
	}

	private static string GetSourceIdText(object source)
	{
		try
		{
			object? id = source.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)?.GetValue(source);
			if (id != null)
			{
				return id.ToString() ?? source.GetType().FullName ?? source.GetType().Name;
			}
		}
		catch
		{
		}

		return source.GetType().FullName ?? source.GetType().Name;
	}

	private static string GetSourceTitleText(object source)
	{
		try
		{
			object? title = source.GetType().GetProperty("Title", BindingFlags.Instance | BindingFlags.Public)?.GetValue(source);
			if (title is LocString locString)
			{
				return locString.GetFormattedText();
			}
			if (title != null)
			{
				return title.ToString() ?? string.Empty;
			}
		}
		catch
		{
		}

		return string.Empty;
	}

	private static bool IsAncientSource(PropertyInfo property, object source)
	{
		return property.Name.Contains("Ancient", StringComparison.OrdinalIgnoreCase)
			|| source.GetType().Name.Contains("Ancient", StringComparison.OrdinalIgnoreCase);
	}

	private static bool TryCreateAncientRelicOption(AncientEventModel ancient, RelicModel canonicalRelic, out EventOption? option)
	{
		option = null;
		try
		{
			MethodInfo? method = GetAncientRelicOptionMethod();
			if (method == null)
			{
				return false;
			}

			RelicModel mutableRelic = canonicalRelic.ToMutable();
			option = method.Invoke(ancient, new object?[] { mutableRelic, "INITIAL", null }) as EventOption;
			return option != null;
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed creating Ancient relic option ancient={ancient?.Id} relic={canonicalRelic?.Id}: {ex.Message}");
			return false;
		}
	}

	private static MethodInfo? GetAncientRelicOptionMethod()
	{
		return typeof(AncientEventModel)
			.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
			.FirstOrDefault(method =>
			{
				if (!string.Equals(method.Name, "RelicOption", StringComparison.Ordinal) || method.IsGenericMethod)
				{
					return false;
				}

				ParameterInfo[] parameters = method.GetParameters();
				return parameters.Length == 3
					&& parameters[0].ParameterType == typeof(RelicModel)
					&& parameters[1].ParameterType == typeof(string)
					&& parameters[2].ParameterType == typeof(string);
			});
	}
}

internal sealed class RelicSourceSummary
{
	public RelicSourceSummary(string key, string kind, string label, string description, bool editable = false)
	{
		Key = key;
		Kind = kind;
		Label = label;
		Description = description;
		Editable = editable;
	}

	public string Key { get; }
	public string Kind { get; }
	public string Label { get; }
	public string Description { get; }
	public bool Editable { get; }
}

internal static class CardEditorRelicOverrideStore
{
	private const int CurrentVersion = 2;
	private const string StorePath = "user://card_editor/relic_overrides.json";
	private static bool _loaded;

	internal static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;

		try
		{
			string path = ProjectSettings.GlobalizePath(StorePath);
			if (!File.Exists(path))
			{
				return;
			}

			string json = File.ReadAllText(path);
			RelicOverrideFileDto? data = JsonSerializer.Deserialize<RelicOverrideFileDto>(json, CreateJsonOptions());
			if (data == null || data.Overrides == null)
			{
				return;
			}
			if (data.Version <= 0 || data.Version > CurrentVersion)
			{
				Log.Warn($"[CardEditor][RelicEditor] Unsupported relic override version={data.Version} (current={CurrentVersion})");
				return;
			}

			Dictionary<ModelId, RelicOverride> loaded = new();
			foreach ((string rawRelicId, RelicOverrideDto dto) in data.Overrides)
			{
				if (!TryParseModelId(rawRelicId, out ModelId relicId) || ModelDb.GetByIdOrNull<RelicModel>(relicId) == null)
				{
					continue;
				}

				RelicOverride overrideData = dto.ToOverride();
				if (!overrideData.IsEmpty())
				{
					loaded[relicId] = overrideData;
				}
			}

			CardEditorRelicOverrides.ReplaceAll(loaded);
			CardEditorMod.VerboseLog($"[CardEditor][RelicEditor] Loaded {loaded.Count} relic overrides");
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed loading relic overrides: {ex}");
		}
	}

	internal static void Save()
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(StorePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

			RelicOverrideFileDto data = new()
			{
				Version = CurrentVersion,
				SavedAtUtc = DateTime.UtcNow,
				Overrides = CardEditorRelicOverrides.AllOverrides.ToDictionary(
					kvp => kvp.Key.ToString(),
					kvp => RelicOverrideDto.FromOverride(kvp.Value),
					StringComparer.Ordinal)
			};

			string json = JsonSerializer.Serialize(data, CreateJsonOptions());
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor][RelicEditor] Failed saving relic overrides: {ex}");
		}
	}

	private static bool TryParseModelId(string text, out ModelId id)
	{
		try
		{
			id = ModelId.Deserialize(text);
			return true;
		}
		catch
		{
			id = ModelId.none;
			return false;
		}
	}

	private static JsonSerializerOptions CreateJsonOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true
		};
	}

	private sealed class RelicOverrideFileDto
	{
		public int Version { get; set; }
		public DateTime SavedAtUtc { get; set; }
		public Dictionary<string, RelicOverrideDto>? Overrides { get; set; }
	}

	internal sealed class RelicEffectEntryDto
	{
		public string? Trigger { get; set; }
		public CardEditorPresetStore.CardExtraEffectDto? Effect { get; set; }
	}

	internal sealed class RelicOverrideDto
	{
		public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
		public bool? CustomDescriptionEnabled { get; set; }
		public string? CustomDescription { get; set; }
		public List<string>? PoolKeys { get; set; }
		public List<string>? FixedSourceKeys { get; set; }
		public List<RelicEffectEntryDto>? ExtraEffects { get; set; }
		public List<string>? EffectTriggers { get; set; }

		public RelicOverride ToOverride()
		{
			return new RelicOverride
			{
				EffectTriggers = ParseTriggers(EffectTriggers),
				DynamicVarBaseValues = DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				CustomDescriptionEnabled = CustomDescriptionEnabled,
				CustomDescription = CustomDescription,
				PoolKeys = PoolKeys != null
					? new HashSet<string>(PoolKeys.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.Ordinal)
					: null,
				FixedSourceKeys = FixedSourceKeys != null
					? new HashSet<string>(FixedSourceKeys.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.Ordinal)
					: null,
				ExtraEffects = ParseEffectEntries(ExtraEffects)
			};
		}

		private static List<RelicEffectEntry>? ParseEffectEntries(List<RelicEffectEntryDto>? dtos)
		{
			if (dtos == null)
			{
				return null;
			}
			List<RelicEffectEntry> result = new();
			foreach (RelicEffectEntryDto? dto in dtos)
			{
				if (dto?.Effect == null || !dto.Effect.TryToEffect(out CardExtraEffect effect))
				{
					continue;
				}
				RelicTriggerKind trigger = (Enum.TryParse(dto.Trigger, ignoreCase: true, out RelicTriggerKind parsed)
						&& Enum.IsDefined(typeof(RelicTriggerKind), parsed))
					? parsed
					: RelicTriggerKind.OnCombatStart;
				result.Add(new RelicEffectEntry { Trigger = trigger, Effect = effect });
			}
			return result.Count > 0 ? result : null;
		}

		private static List<RelicTriggerKind>? ParseTriggers(List<string>? names)
		{
			if (names == null)
			{
				return null;
			}
			List<RelicTriggerKind> result = new();
			foreach (string name in names)
			{
				if (Enum.TryParse(name, ignoreCase: true, out RelicTriggerKind parsed)
					&& Enum.IsDefined(typeof(RelicTriggerKind), parsed)
					&& !result.Contains(parsed))
				{
					result.Add(parsed);
				}
			}
			return result.Count > 0 ? result : null;
		}

		public static RelicOverrideDto FromOverride(RelicOverride overrideData)
		{
			return new RelicOverrideDto
			{
				EffectTriggers = overrideData.EffectTriggers != null
					? overrideData.EffectTriggers.Select(t => t.ToString()).ToList()
					: null,
				DynamicVarBaseValues = overrideData.DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(overrideData.DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				CustomDescriptionEnabled = overrideData.CustomDescriptionEnabled,
				CustomDescription = overrideData.CustomDescription,
				PoolKeys = overrideData.PoolKeys != null
					? overrideData.PoolKeys.OrderBy(p => p, StringComparer.Ordinal).ToList()
					: null,
				FixedSourceKeys = overrideData.FixedSourceKeys != null
					? overrideData.FixedSourceKeys.OrderBy(p => p, StringComparer.Ordinal).ToList()
					: null,
				ExtraEffects = overrideData.ExtraEffects != null
					? overrideData.ExtraEffects
						.Where(e => e?.Effect != null)
						.Select(e => new RelicEffectEntryDto { Trigger = e.Trigger.ToString(), Effect = CardEditorPresetStore.CardExtraEffectDto.FromEffect(e.Effect) })
						.ToList()
					: null
			};
		}
	}
}

internal static class CardEditorRelicEditorSession
{
	private const string WiredMetaKey = "CardEditorRelicEditorWired";
	private static ModelId _lastOpenedRelicId = ModelId.none;
	private static ulong _lastOpenTicks;

	internal static bool IsActive => CardEditorUiState.IsRelicEditorActive;

	internal static void Begin()
	{
		NRelicEditorPopup.CloseAnyOpen();
		CardEditorUiState.Mode = CardEditorLibraryMode.Relic;
		_lastOpenedRelicId = ModelId.none;
		_lastOpenTicks = 0;
		Log.Info("[CardEditor][RelicEditor] Session active");
	}

	internal static void End()
	{
		if (CardEditorUiState.IsRelicEditorActive)
		{
			Log.Info("[CardEditor][RelicEditor] Session ended");
			CardEditorUiState.Mode = CardEditorLibraryMode.None;
		}
		NRelicEditorPopup.CloseAnyOpen();
		_lastOpenedRelicId = ModelId.none;
		_lastOpenTicks = 0;
	}

	internal static void WireEntries(Node root)
	{
		if (!IsActive)
		{
			return;
		}

		int wired = 0;
		foreach (NRelicCollectionEntry entry in FindDescendants<NRelicCollectionEntry>(root))
		{
			if (entry.HasMeta(WiredMetaKey))
			{
				continue;
			}

			entry.SetMeta(WiredMetaKey, true);
			wired++;
		}

		Log.Info($"[CardEditor][RelicEditor] Marked {wired} relic collection entries for editor mode");
	}

	internal static void OpenRelicEditorFor(RelicModel relic, string source)
	{
		if (!IsActive || relic == null)
		{
			if (relic != null)
			{
				Log.Info($"[CardEditor][RelicEditor] Ignored popup source={source} relic={relic.Id} active={IsActive}");
			}
			return;
		}

		ulong now = Time.GetTicksMsec();
		if (_lastOpenedRelicId == relic.Id && now - _lastOpenTicks < 250)
		{
			return;
		}

		_lastOpenedRelicId = relic.Id;
		_lastOpenTicks = now;
		Log.Info($"[CardEditor][RelicEditor] Opening popup source={source} relic={relic.Id}");
		Callable.From(() => Callable.From(() => NRelicEditorPopup.Open(relic)).CallDeferred()).CallDeferred();
	}

	private static IEnumerable<T> FindDescendants<T>(Node node)
		where T : Node
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is T typed)
			{
				yield return typed;
			}

			foreach (T nested in FindDescendants<T>(child))
			{
				yield return nested;
			}
		}
	}
}

public partial class NRelicEditorPresetToggleButton : Control
{
	private static readonly string _buttonTexturePath = "res://images/packed/common_ui/settings_tab_selected.png";
	private static readonly string _buttonOutlineTexturePath = "res://images/packed/common_ui/settings_tab_stroke.png";
	private static readonly string _fontPath = "res://themes/kreon_bold_glyph_space_two.tres";
	private static readonly string _labelThemePath = "res://themes/settings_screen_tab.tres";
	private static readonly string _outlineMaterialPath = "res://themes/canvas_item_material_additive_shared.tres";
	private static readonly string _shaderPath = "res://shaders/hsv.gdshader";

	private static Texture2D? _buttonTexture;
	private static Texture2D? _buttonOutlineTexture;
	private static Font? _labelFont;
	private static Theme? _labelTheme;
	private static Material? _outlineMaterial;
	private static Shader? _hsvShader;

	private NRelicCollection _collection = null!;
	private CardEditorBaseDeckActionButton _button = null!;

	public static NRelicEditorPresetToggleButton Create(NRelicCollection collection)
	{
		NRelicEditorPresetToggleButton toggle = new();
		toggle.Initialize(collection);
		toggle.BuildUi();
		return toggle;
	}

	private void Initialize(NRelicCollection collection)
	{
		_collection = collection;
		Name = "CardEditorRelicPresetToggleButton";
		ZIndex = 61;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	private void BuildUi()
	{
		CardEditorGodotResourceCache.Load(ref _buttonTexture, _buttonTexturePath);
		CardEditorGodotResourceCache.Load(ref _buttonOutlineTexture, _buttonOutlineTexturePath);
		CardEditorGodotResourceCache.Load(ref _labelFont, _fontPath);
		CardEditorGodotResourceCache.Load(ref _labelTheme, _labelThemePath);
		CardEditorGodotResourceCache.Load(ref _outlineMaterial, _outlineMaterialPath);
		CardEditorGodotResourceCache.Load(ref _hsvShader, _shaderPath);

		_button = new CardEditorBaseDeckActionButton();
		_button.Initialize(CardEditorLoc.T("button.presetEditor", "Preset Editor"), Colors.White, _buttonTexture!, _buttonOutlineTexture!, _labelFont, _labelTheme, _outlineMaterial, _hsvShader);
		_button.Triggered += OnTriggered;
		AddChild(_button);
	}

	public void RefreshState(bool shouldShow, bool isPanelOpen)
	{
		Visible = shouldShow;
		if (!shouldShow)
		{
			return;
		}

		_button.SetText(CardEditorLoc.T("button.presetEditor", "Preset Editor"));
		_button.TooltipText = isPanelOpen
			? CardEditorLoc.T("tooltip.hidePresets", "Hide Presets")
			: CardEditorLoc.T("tooltip.showPresets", "Show Presets");
		_button.SetButtonSize(CardEditorBaseDeckActionBarTuning.ButtonWidth, CardEditorBaseDeckActionBarTuning.ButtonHeight);
		_button.SetTextSize(CardEditorBaseDeckActionBarTuning.ButtonFontSize);
		_button.SetTextOffsets(
			CardEditorBaseDeckActionBarTuning.BarTextOffsetX,
			CardEditorBaseDeckActionBarTuning.BarTextOffsetY,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetX,
			CardEditorBaseDeckActionBarTuning.ResetTextOffsetY);
		_button.SetSelected(isPanelOpen);
		_button.SetEmphasized(false);
		_button.SetButtonEnabled(true);
		_button.Position = CardEditorPresetButtonTuning.GetBaseDeckButtonPosition();
	}

	private void OnTriggered()
	{
		CardEditorRelicPresetPanelHooks.ToggleOpen(_collection);
		RefreshState(true, CardEditorRelicPresetPanelHooks.IsOpen(_collection));
	}
}

internal static class CardEditorRelicPresetPanelHooks
{
	private const string OpenMetaKey = "card_editor_relic_preset_panel_open";

	public static bool IsOpen(NRelicCollection collection)
	{
		return collection != null
			&& GodotObject.IsInstanceValid(collection)
			&& collection.HasMeta(OpenMetaKey)
			&& collection.GetMeta(OpenMetaKey).AsBool();
	}

	public static void ToggleOpen(NRelicCollection collection)
	{
		if (collection == null || !GodotObject.IsInstanceValid(collection))
		{
			return;
		}

		collection.SetMeta(OpenMetaKey, !IsOpen(collection));
		Sync(collection);
	}

	public static void Sync(NRelicCollection collection)
	{
		if (collection == null || !GodotObject.IsInstanceValid(collection))
		{
			return;
		}

		NCardEditorPresetPanel? panel = collection.GetNodeOrNull<NCardEditorPresetPanel>("CardEditorPresetPanel");
		NRelicEditorPresetToggleButton? button = collection.GetNodeOrNull<NRelicEditorPresetToggleButton>("CardEditorRelicPresetToggleButton");
		if (!CardEditorRelicEditorSession.IsActive)
		{
			collection.SetMeta(OpenMetaKey, false);
			if (panel != null)
			{
				panel.QueueFree();
			}
			if (button != null)
			{
				button.QueueFree();
			}
			return;
		}

		if (button == null)
		{
			button = NRelicEditorPresetToggleButton.Create(collection);
			collection.AddChild(button);
		}

		bool isOpen = IsOpen(collection);
		button.RefreshState(true, isOpen);

		if (panel == null)
		{
			panel = NCardEditorPresetPanel.CreateForRelicEditor(collection);
			collection.AddChild(panel);
		}

		panel.SetCreatorMode(false);
		panel.Visible = isOpen;
		if (isOpen)
		{
			panel.RefreshPresetList();
			collection.MoveChild(panel, collection.GetChildCount() - 1);
		}
		collection.MoveChild(button, collection.GetChildCount() - 1);
		Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(panel))
			{
				panel.ApplyLayoutTuning();
			}
			if (GodotObject.IsInstanceValid(button))
			{
				button.RefreshState(true, IsOpen(collection));
			}
		}).CallDeferred();
	}

	public static void Close(NRelicCollection collection)
	{
		if (collection == null || !GodotObject.IsInstanceValid(collection))
		{
			return;
		}

		foreach (NCardEditorPresetPanel panel in collection.GetChildren().OfType<NCardEditorPresetPanel>().ToArray())
		{
			panel.QueueFree();
		}
		foreach (NRelicEditorPresetToggleButton button in collection.GetChildren().OfType<NRelicEditorPresetToggleButton>().ToArray())
		{
			button.QueueFree();
		}
		collection.SetMeta(OpenMetaKey, false);
	}
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.ToMutable))]
internal static class RelicModel_ToMutable_CardEditorRelicOverrides_Patch
{
	public static void Postfix(ref RelicModel __result)
	{
		CardEditorRelicOverrides.ApplyTo(__result);
	}
}

[HarmonyPatch(typeof(RelicModel), "get_DynamicDescription")]
internal static class RelicModel_get_DynamicDescription_CardEditorRelicTextOverrides_Patch
{
	public static bool Prefix(RelicModel __instance, ref LocString __result)
	{
		if (CardEditorRelicOverrides.TryBuildCustomDynamicDescription(__instance, out LocString locString))
		{
			__result = locString;
			return false;
		}

		return true;
	}
}

[HarmonyPatch]
internal static class RelicPoolModel_GetUnlockedRelics_CardEditorRelicPools_Patch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		HashSet<MethodBase> targets = new();
		foreach (Type type in typeof(RelicPoolModel).Assembly.GetTypes())
		{
			if (!typeof(RelicPoolModel).IsAssignableFrom(type))
			{
				continue;
			}

			MethodInfo? method = type.GetMethod(
				nameof(RelicPoolModel.GetUnlockedRelics),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			if (method != null && !method.IsAbstract && targets.Add(method))
			{
				yield return method;
			}
		}
	}

	public static void Postfix(RelicPoolModel __instance, UnlockState unlockState, ref IEnumerable<RelicModel> __result)
	{
		__result = CardEditorRelicOverrides.ApplyPoolOverrides(__instance, __result);
	}
}

[HarmonyPatch]
internal static class AncientEventModel_AllPossibleOptions_CardEditorRelicSources_Patch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		HashSet<MethodBase> targets = new();
		foreach (Type type in typeof(AncientEventModel).Assembly.GetTypes())
		{
			if (!typeof(AncientEventModel).IsAssignableFrom(type) || type.IsAbstract)
			{
				continue;
			}

			PropertyInfo? property = type.GetProperty(
				nameof(AncientEventModel.AllPossibleOptions),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			MethodInfo? getter = property?.GetGetMethod();
			if (getter != null && targets.Add(getter))
			{
				yield return getter;
			}
		}
	}

	public static void Postfix(AncientEventModel __instance, ref IEnumerable<EventOption> __result)
	{
		__result = CardEditorRelicOverrides.ApplyFixedSourceOverrides(__instance, __result);
	}
}

[HarmonyPatch(typeof(RelicModel), "get_Pool")]
internal static class RelicModel_get_Pool_CardEditorRelicPools_Patch
{
	public static void Postfix(RelicModel __instance, ref RelicPoolModel __result)
	{
		RelicPoolModel? pool = CardEditorRelicOverrides.ResolveFirstEffectivePool(__instance);
		if (pool != null)
		{
			__result = pool;
		}
	}
}

// Neow's event offers relics from a HARDCODED list (Neow.GenerateInitialOptions), bypassing the relic
// pools entirely, so removing a relic from the pools never reaches it. Every Neow offering - both option
// lists and NeowsBones' random grant - gates on RelicModel.IsAllowedAtNeow, so force that false for any
// relic the user removed from all pools. TargetMethods patches the base plus per-relic overrides
// (Kaleidoscope/ScrollBoxes) so none slip through their own IsAllowedAtNeow logic.
[HarmonyPatch]
internal static class RelicModel_IsAllowedAtNeow_CardEditorRelicPools_Patch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		HashSet<MethodBase> targets = new();
		foreach (Type type in typeof(RelicModel).Assembly.GetTypes())
		{
			if (!typeof(RelicModel).IsAssignableFrom(type))
			{
				continue;
			}

			MethodInfo? method = type.GetMethod(
				nameof(RelicModel.IsAllowedAtNeow),
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			if (method != null && !method.IsAbstract && targets.Add(method))
			{
				yield return method;
			}
		}
	}

	public static void Postfix(RelicModel __instance, ref bool __result)
	{
		if (__result && CardEditorRelicOverrides.IsRemovedFromAllPools(__instance))
		{
			__result = false;
		}
	}
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "OnRelicEntryPressed")]
internal static class RelicCollectionCategory_OnRelicEntryPressed_CardEditorRelicEditor_Patch
{
	public static bool Prefix(NRelicCollectionEntry entry)
	{
		if (!CardEditorRelicEditorSession.IsActive)
		{
			if (entry?.relic != null)
			{
				Log.Info($"[CardEditor][RelicEditor] Letting vanilla relic entry open because session is inactive relic={entry.relic.Id}");
			}
			return true;
		}

		if (entry?.relic != null)
		{
			Log.Info($"[CardEditor][RelicEditor] Suppressed vanilla relic entry handler relic={entry.relic.Id}");
			CardEditorRelicEditorSession.OpenRelicEditorFor(entry.relic, "vanilla-entry-handler");
		}

		return false;
	}
}

[HarmonyPatch(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), typeof(IReadOnlyList<RelicModel>), typeof(RelicModel))]
internal static class InspectRelicScreen_Open_CardEditorRelicEditor_Patch
{
	public static bool Prefix(RelicModel __1)
	{
		if (!CardEditorRelicEditorSession.IsActive)
		{
			if (__1 != null)
			{
				Log.Info($"[CardEditor][RelicEditor] Letting vanilla inspect screen open because session is inactive relic={__1.Id}");
			}
			return true;
		}

		if (__1 != null)
		{
			Log.Info($"[CardEditor][RelicEditor] Suppressed vanilla relic inspect relic={__1.Id}");
			CardEditorRelicEditorSession.OpenRelicEditorFor(__1, "inspect-screen-open");
		}

		return false;
	}
}

[HarmonyPatch(typeof(NRelicCollection), nameof(NRelicCollection.OnSubmenuOpened))]
internal static class RelicCollection_OnSubmenuOpened_CardEditorRelicMode_Patch
{
	public static void Postfix(NRelicCollection __instance)
	{
		CardEditorRelicEditorSession.WireEntries(__instance);
		CardEditorRelicPresetPanelHooks.Sync(__instance);
	}
}

[HarmonyPatch(typeof(NRelicCollectionCategory), "LoadRelicNodes")]
internal static class RelicCollectionCategory_LoadRelicNodes_CardEditorRelicMode_Patch
{
	public static void Postfix(NRelicCollectionCategory __instance)
	{
		CardEditorRelicEditorSession.WireEntries(__instance);
	}
}

[HarmonyPatch(typeof(NRelicCollection), nameof(NRelicCollection.OnSubmenuClosed))]
internal static class RelicCollection_OnSubmenuClosed_CardEditorRelicMode_Patch
{
	public static void Postfix(NRelicCollection __instance)
	{
		CardEditorRelicPresetPanelHooks.Close(__instance);
		CardEditorRelicEditorSession.End();
	}
}
