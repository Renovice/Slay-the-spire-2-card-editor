using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Unlocks;

namespace SlayTheSpire2Mod.CardEditor;

public sealed class RelicOverride
{
	public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
	public HashSet<string>? PoolKeys { get; set; }

	public bool IsEmpty()
	{
		return (DynamicVarBaseValues == null || DynamicVarBaseValues.Count == 0)
			&& (PoolKeys == null || PoolKeys.Count == 0);
	}
}

internal static class CardEditorRelicOverrides
{
	private static readonly Dictionary<ModelId, RelicOverride> _overrides = new();

	internal static IReadOnlyDictionary<ModelId, RelicOverride> AllOverrides => _overrides;

	internal static bool HasAnyOverrides => _overrides.Count > 0;

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
			_overrides.Remove(relicId);
			return;
		}

		_overrides[relicId] = Clone(overrideData);
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
	}

	internal static RelicOverride Clone(RelicOverride source)
	{
		return new RelicOverride
		{
			DynamicVarBaseValues = source.DynamicVarBaseValues != null
				? new Dictionary<string, decimal>(source.DynamicVarBaseValues, StringComparer.Ordinal)
				: null,
			PoolKeys = source.PoolKeys != null
				? new HashSet<string>(source.PoolKeys, StringComparer.Ordinal)
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
		if (overrideData.DynamicVarBaseValues == null)
		{
			return;
		}

		foreach ((string key, decimal value) in overrideData.DynamicVarBaseValues)
		{
			if (relic.DynamicVars.TryGetValue(key, out DynamicVar? dynamicVar))
			{
				dynamicVar.BaseValue = value;
			}
		}
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
		return name.EndsWith("RelicPool", StringComparison.Ordinal)
			? name[..^"RelicPool".Length]
			: name;
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
}

internal static class CardEditorRelicOverrideStore
{
	private const int CurrentVersion = 1;
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

	private sealed class RelicOverrideDto
	{
		public Dictionary<string, decimal>? DynamicVarBaseValues { get; set; }
		public List<string>? PoolKeys { get; set; }

		public RelicOverride ToOverride()
		{
			return new RelicOverride
			{
				DynamicVarBaseValues = DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				PoolKeys = PoolKeys != null
					? new HashSet<string>(PoolKeys.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.Ordinal)
					: null
			};
		}

		public static RelicOverrideDto FromOverride(RelicOverride overrideData)
		{
			return new RelicOverrideDto
			{
				DynamicVarBaseValues = overrideData.DynamicVarBaseValues != null
					? new Dictionary<string, decimal>(overrideData.DynamicVarBaseValues, StringComparer.Ordinal)
					: null,
				PoolKeys = overrideData.PoolKeys != null
					? overrideData.PoolKeys.OrderBy(p => p, StringComparer.Ordinal).ToList()
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
			entry.Connect(NClickableControl.SignalName.Released, Callable.From<NRelicCollectionEntry>(OnRelicEntryReleasedFallback));
			wired++;
		}

		Log.Info($"[CardEditor][RelicEditor] Wired {wired} relic collection entries");
	}

	internal static void OpenRelicEditorFor(RelicModel relic, string source)
	{
		if (!IsActive || relic == null)
		{
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
		Callable.From(() => NRelicEditorPopup.Open(relic)).CallDeferred();
	}

	private static void OnRelicEntryReleasedFallback(NRelicCollectionEntry entry)
	{
		if (entry?.relic != null)
		{
			OpenRelicEditorFor(entry.relic, "fallback-signal");
		}
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

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.ToMutable))]
internal static class RelicModel_ToMutable_CardEditorRelicOverrides_Patch
{
	public static void Postfix(ref RelicModel __result)
	{
		CardEditorRelicOverrides.ApplyTo(__result);
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

[HarmonyPatch(typeof(NRelicCollectionCategory), "OnRelicEntryPressed")]
internal static class RelicCollectionCategory_OnRelicEntryPressed_CardEditorRelicEditor_Patch
{
	public static bool Prefix(NRelicCollectionEntry entry)
	{
		if (!CardEditorRelicEditorSession.IsActive)
		{
			return true;
		}

		if (entry?.relic != null)
		{
			CardEditorRelicEditorSession.OpenRelicEditorFor(entry.relic, "vanilla-entry-handler");
		}

		return false;
	}
}

[HarmonyPatch(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.Open), typeof(IReadOnlyList<RelicModel>), typeof(RelicModel))]
internal static class InspectRelicScreen_Open_CardEditorRelicEditor_Patch
{
	public static bool Prefix(RelicModel relic)
	{
		if (!CardEditorRelicEditorSession.IsActive)
		{
			return true;
		}

		if (relic != null)
		{
			Log.Info($"[CardEditor][RelicEditor] Suppressed vanilla relic inspect relic={relic.Id}");
			CardEditorRelicEditorSession.OpenRelicEditorFor(relic, "inspect-screen-open");
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
	public static void Postfix()
	{
		CardEditorRelicEditorSession.End();
	}
}
