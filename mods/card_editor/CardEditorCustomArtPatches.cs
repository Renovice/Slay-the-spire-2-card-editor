using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

[HarmonyPatch]
internal static class CardEditorCustomPortraitLoader
{
	private static readonly object _lock = new();
	private static readonly Dictionary<string, Texture2D> _cache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _warnedFailures = new(StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _loggedLoads = new(StringComparer.OrdinalIgnoreCase);
	private const string CardArtFolderName = "card art";

	[ThreadStatic]
	private static HashSet<ModelId>? _resolvingSourcePortraits;

	[ThreadStatic]
	private static HashSet<ModelId>? _resolvingSourcePortraitPaths;

	internal readonly struct PortraitTransform
	{
		public PortraitTransform(float offsetX, float offsetY, float zoom)
		{
			OffsetX = offsetX;
			OffsetY = offsetY;
			Zoom = zoom <= 0f ? 1f : zoom;
		}

		public float OffsetX { get; }
		public float OffsetY { get; }
		public float Zoom { get; }

		public bool IsDefault =>
			Math.Abs(OffsetX) < 0.01f
			&& Math.Abs(OffsetY) < 0.01f
			&& Math.Abs(Zoom - 1f) < 0.001f;
	}

	public static bool TryGetCustomPortrait(ModelId cardId, out Texture2D texture)
	{
		texture = null!;

		if (cardId == null)
		{
			return false;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(cardId))
		{
			if (CardEditorCreatedCardsStore.TryGetCustomPortraitAbsolutePath(cardId, out string createdAbsolutePath))
			{
				return TryGetOrLoadTexture(createdAbsolutePath, cardId, out texture);
			}

			ModelId? sourceCardId = CardEditorCreatedCardsStore.GetPortraitSourceCardId(cardId);
			if (sourceCardId != null && sourceCardId != ModelId.none)
			{
				return TryGetPortraitFromSourceCard(sourceCardId, out texture);
			}

			return false;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(cardId, out CardOverride overrideData))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile))
		{
			if (!TryGetCustomPortraitAbsolutePath(overrideData.CustomPortraitFile!, out string absolutePath))
			{
				return false;
			}
			return TryGetOrLoadTexture(absolutePath, cardId, out texture);
		}

		if (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none)
		{
			return TryGetPortraitFromSourceCard(overrideData.PortraitSourceCardId, out texture);
		}

		return false;
	}

	public static bool TryGetPortrait(CardModel? card, out Texture2D texture)
	{
		texture = null!;

		if (card?.Id == null)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			if (TryGetPortrait(identitySource, out texture))
			{
				return true;
			}

			string portraitPath = identitySource.PortraitPath;
			if (!string.IsNullOrWhiteSpace(portraitPath) && ResourceLoader.Exists(portraitPath))
			{
				texture = ResourceLoader.Load<Texture2D>(portraitPath, null, ResourceLoader.CacheMode.Reuse);
				return texture != null;
			}
		}

		if (TryGetCustomPortrait(card.Id, out texture))
		{
			return true;
		}

		return TryGetPoolOverridePortraitFallback(card, out texture);
	}

	public static bool TryGetPortraitTransform(CardModel? card, out PortraitTransform transform)
	{
		transform = default;

		if (card?.Id == null || CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			card = identitySource;
		}

		if (!CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData))
		{
			return false;
		}

		float x = overrideData.PortraitOffsetX ?? 0f;
		float y = overrideData.PortraitOffsetY ?? 0f;
		float zoom = overrideData.PortraitZoom ?? 1f;
		transform = new PortraitTransform(x, y, Math.Clamp(zoom, 0.25f, 4f));
		return !transform.IsDefault;
	}

	public static bool TryGetPortraitPath(CardModel? card, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;

		if (card?.Id == null
			|| CardEditorOverrides.SuppressAllOverrides)
		{
			return false;
		}

		if (CardEditorExtraEffects.TryGetDynamicIdentitySource(card, out CardModel identitySource))
		{
			portraitPath = beta ? identitySource.BetaPortraitPath : identitySource.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(card.Id)
			|| !CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData))
		{
			return false;
		}

		if (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none)
		{
			return TryGetPortraitPathFromSourceCard(overrideData.PortraitSourceCardId, beta, out portraitPath);
		}

		if (string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile)
			&& string.IsNullOrWhiteSpace(overrideData.PoolTitle))
		{
			return false;
		}

		return TryGetOriginalPortraitPath(card.Id, beta, out portraitPath);
	}

	private static bool TryGetOrLoadTexture(string absolutePath, ModelId cardId, out Texture2D texture)
	{
		texture = null!;

		Texture2D? cached;
		lock (_lock)
		{
			_cache.TryGetValue(absolutePath, out cached);
		}

		if (cached == null)
		{
			cached = TryLoadTextureFromFile(absolutePath);
			if (cached == null)
			{
				WarnOnce($"Failed loading custom art for {cardId} from '{absolutePath}'.");
				return false;
			}

			lock (_lock)
			{
				_cache[absolutePath] = cached;
			}

			InfoOnce($"Loaded custom art for {cardId} from '{absolutePath}' ({cached.GetWidth()}x{cached.GetHeight()}).");
		}

		texture = cached;
		return true;
	}

	private static bool TryGetPortraitFromSourceCard(ModelId sourceCardId, out Texture2D texture)
	{
		texture = null!;
		try
		{
			_resolvingSourcePortraits ??= new HashSet<ModelId>();
			if (!_resolvingSourcePortraits.Add(sourceCardId))
			{
				return false;
			}

			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			if (source == null)
			{
				return false;
			}

			texture = source.Portrait;
			return texture != null;
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				_resolvingSourcePortraits?.Remove(sourceCardId);
			}
			catch
			{
			}
		}
	}

	private static bool TryGetPortraitPathFromSourceCard(ModelId sourceCardId, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;
		try
		{
			_resolvingSourcePortraitPaths ??= new HashSet<ModelId>();
			if (!_resolvingSourcePortraitPaths.Add(sourceCardId))
			{
				return false;
			}

			CardModel? source = ModelDb.GetByIdOrNull<CardModel>(sourceCardId);
			if (source == null)
			{
				return false;
			}

			portraitPath = beta ? source.BetaPortraitPath : source.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				_resolvingSourcePortraitPaths?.Remove(sourceCardId);
			}
			catch
			{
			}
		}
	}

	private static bool TryGetPoolOverridePortraitFallback(CardModel card, out Texture2D texture)
	{
		texture = null!;

		if (CardEditorOverrides.SuppressAllOverrides
			|| CardEditorCreatedCardsStore.IsCreatedCardId(card.Id)
			|| !CardEditorOverrides.TryGetEffectiveOverride(card.Id, out CardOverride overrideData)
			|| string.IsNullOrWhiteSpace(overrideData.PoolTitle)
			|| !string.IsNullOrWhiteSpace(overrideData.CustomPortraitFile)
			|| (overrideData.PortraitSourceCardId != null && overrideData.PortraitSourceCardId != ModelId.none))
		{
			return false;
		}

		try
		{
			string currentPortraitPath = card.PortraitPath;
			if (!string.IsNullOrWhiteSpace(currentPortraitPath) && ResourceLoader.Exists(currentPortraitPath))
			{
				return false;
			}
		}
		catch
		{
		}

		return TryGetOriginalPortrait(card, out texture);
	}

	private static bool TryGetOriginalPortrait(CardModel card, out Texture2D texture)
	{
		texture = null!;
		try
		{
			if (!TryGetOriginalPortraitPath(card.Id, beta: false, out string originalPortraitPath))
			{
				return false;
			}

			Texture2D? originalTexture = ResourceLoader.Load<Texture2D>(originalPortraitPath, null, ResourceLoader.CacheMode.Reuse);
			if (originalTexture == null)
			{
				return false;
			}

			texture = originalTexture;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryGetOriginalPortraitPath(ModelId cardId, bool beta, out string portraitPath)
	{
		portraitPath = string.Empty;
		bool previousSuppressAllOverrides = CardEditorOverrides.SuppressAllOverrides;
		try
		{
			CardEditorOverrides.SuppressAllOverrides = true;
			CardModel? canonical = ModelDb.GetByIdOrNull<CardModel>(cardId);
			if (canonical == null)
			{
				return false;
			}

			portraitPath = beta ? canonical.BetaPortraitPath : canonical.PortraitPath;
			return !string.IsNullOrWhiteSpace(portraitPath);
		}
		catch
		{
			return false;
		}
		finally
		{
			CardEditorOverrides.SuppressAllOverrides = previousSuppressAllOverrides;
		}
	}

	private static bool TryGetCustomPortraitAbsolutePath(string fileName, out string absolutePath)
	{
		absolutePath = string.Empty;
		try
		{
			string dir = GetCustomArtDirectory();
			if (string.IsNullOrWhiteSpace(dir))
			{
				return false;
			}

			string candidate = Path.Combine(dir, fileName.Trim());
			if (!File.Exists(candidate))
			{
				return false;
			}

			absolutePath = candidate;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string GetCustomArtDirectory()
	{
		try
		{
			string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (string.IsNullOrWhiteSpace(modDir))
			{
				return string.Empty;
			}

			string dir = Path.Combine(modDir, CardArtFolderName);
			if (Directory.Exists(dir))
			{
				return dir;
			}

			string alt = Path.Combine(modDir, "card_art");
			return Directory.Exists(alt) ? alt : dir;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static Texture2D? TryLoadTextureFromFile(string absolutePath)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
			{
				return null;
			}

			string extension = Path.GetExtension(absolutePath) ?? string.Empty;
			string extLower = extension.ToLowerInvariant();

			byte[] bytes = File.ReadAllBytes(absolutePath);
			if (bytes.Length == 0)
			{
				return null;
			}

			Image image = new Image();
			Error err = Error.Failed;

			if (extLower == ".png")
			{
				err = image.LoadPngFromBuffer(bytes);
			}
			else if (extLower == ".jpg" || extLower == ".jpeg")
			{
				err = image.LoadJpgFromBuffer(bytes);
			}
			else if (extLower == ".webp")
			{
				err = image.LoadWebpFromBuffer(bytes);
			}

			// Some exported PNGs can fail buffer-decoding on certain Godot builds; fall back to file loader.
			if (err != Error.Ok || image.IsEmpty())
			{
				Image? loadedFromFile = TryLoadImageFromFile(absolutePath);
				if (loadedFromFile == null || loadedFromFile.IsEmpty())
				{
					WarnOnce($"Failed decoding custom art '{absolutePath}' (ext='{extension}', bytes={bytes.Length}, err={err}).");
					return null;
				}
				image = loadedFromFile;
			}

			return ImageTexture.CreateFromImage(image);
		}
		catch (Exception ex)
		{
			WarnOnce($"Exception loading custom art '{absolutePath}': {ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	private static Image? TryLoadImageFromFile(string absolutePath)
	{
		try
		{
			string normalizedPath = absolutePath.Replace('\\', '/');
			return Image.LoadFromFile(normalizedPath);
		}
		catch
		{
			return null;
		}
	}

	private static void WarnOnce(string message)
	{
		lock (_lock)
		{
			if (!_warnedFailures.Add(message))
			{
				return;
			}
		}
		Log.Warn($"[CardEditor] {message}");
	}

	private static void InfoOnce(string message)
	{
		lock (_lock)
		{
			if (!_loggedLoads.Add(message))
			{
				return;
			}
		}
		CardEditorMod.VerboseLog($"[CardEditor] {message}");
	}
}

[HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
internal static class CardModel_get_PortraitPath_CustomArt_Patch
{
	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (CardEditorCustomPortraitLoader.TryGetPortraitPath(__instance, beta: false, out string portraitPath))
		{
			__result = portraitPath;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_BetaPortraitPath")]
internal static class CardModel_get_BetaPortraitPath_CustomArt_Patch
{
	public static void Postfix(CardModel __instance, ref string __result)
	{
		if (CardEditorCustomPortraitLoader.TryGetPortraitPath(__instance, beta: true, out string portraitPath))
		{
			__result = portraitPath;
		}
	}
}

[HarmonyPatch(typeof(CardModel), "get_Portrait")]
internal static class CardModel_get_Portrait_CustomArt_Patch
{
	public static bool Prefix(CardModel __instance, ref Texture2D __result)
	{
		if (__instance?.Id == null)
		{
			return true;
		}

		// This can be inlined in some call-sites, so we also patch NCard.Reload. This patch remains as a best-effort.
		if (!CardEditorCustomPortraitLoader.TryGetPortrait(__instance, out Texture2D texture))
		{
			return true;
		}

		__result = texture;
		return false;
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_CustomPortrait_Patch
{
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private const string OriginalPositionMeta = "card_editor_original_portrait_position";
	private const string OriginalScaleMeta = "card_editor_original_portrait_scale";
	private const string OriginalPivotMeta = "card_editor_original_portrait_pivot";
	private const string TransformAppliedMeta = "card_editor_portrait_transform_applied";

	public static void Postfix(NCard __instance)
	{
		CardModel? model = __instance?.Model;
		if (model?.Id == null)
		{
			return;
		}

		bool hasCustomPortrait = CardEditorCustomPortraitLoader.TryGetPortrait(model, out Texture2D texture);
		bool hasTransform = CardEditorCustomPortraitLoader.TryGetPortraitTransform(model, out CardEditorCustomPortraitLoader.PortraitTransform transform);

		try
		{
			TextureRect portrait = PortraitRef(__instance);
			if (portrait != null)
			{
				if (hasCustomPortrait)
				{
					portrait.Texture = texture;
				}
				SyncPortraitTransform(portrait, hasTransform && portrait.Visible, transform);
			}

			TextureRect ancientPortrait = AncientPortraitRef(__instance);
			if (ancientPortrait != null)
			{
				if (hasCustomPortrait)
				{
					ancientPortrait.Texture = texture;
				}
				SyncPortraitTransform(ancientPortrait, hasTransform && ancientPortrait.Visible, transform);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[CardEditor] Failed applying custom portrait to NCard: {ex.GetType().Name}: {ex.Message}");
		}
	}

	private static void SyncPortraitTransform(TextureRect portrait, bool hasTransform, CardEditorCustomPortraitLoader.PortraitTransform transform)
	{
		if (hasTransform)
		{
			ApplyPortraitTransform(portrait, transform);
			return;
		}

		ResetPortraitTransformIfNeeded(portrait);
	}

	private static void ApplyPortraitTransform(TextureRect portrait, CardEditorCustomPortraitLoader.PortraitTransform transform)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		CacheOriginalPortraitLayout(portrait);
		Vector2 originalPosition = portrait.GetMeta(OriginalPositionMeta).AsVector2();
		Vector2 originalScale = portrait.GetMeta(OriginalScaleMeta).AsVector2();
		Vector2 originalPivot = portrait.GetMeta(OriginalPivotMeta).AsVector2();

		portrait.Position = originalPosition;
		portrait.Scale = originalScale;
		portrait.PivotOffset = originalPivot;

		if (transform.IsDefault)
		{
			return;
		}

		portrait.Position = originalPosition + new Vector2(transform.OffsetX, transform.OffsetY);
		portrait.Scale = originalScale * transform.Zoom;
		portrait.SetMeta(TransformAppliedMeta, true);
	}

	private static void ResetPortraitTransformIfNeeded(TextureRect portrait)
	{
		if (portrait == null
			|| !GodotObject.IsInstanceValid(portrait)
			|| !portrait.HasMeta(TransformAppliedMeta)
			|| !portrait.GetMeta(TransformAppliedMeta).AsBool())
		{
			return;
		}

		if (portrait.HasMeta(OriginalPositionMeta))
		{
			portrait.Position = portrait.GetMeta(OriginalPositionMeta).AsVector2();
		}
		if (portrait.HasMeta(OriginalScaleMeta))
		{
			portrait.Scale = portrait.GetMeta(OriginalScaleMeta).AsVector2();
		}
		if (portrait.HasMeta(OriginalPivotMeta))
		{
			portrait.PivotOffset = portrait.GetMeta(OriginalPivotMeta).AsVector2();
		}
		portrait.SetMeta(TransformAppliedMeta, false);
	}

	private static void CacheOriginalPortraitLayout(TextureRect portrait)
	{
		if (!portrait.HasMeta(OriginalPositionMeta))
		{
			portrait.SetMeta(OriginalPositionMeta, portrait.Position);
		}
		if (!portrait.HasMeta(OriginalScaleMeta))
		{
			portrait.SetMeta(OriginalScaleMeta, portrait.Scale);
		}
		if (!portrait.HasMeta(OriginalPivotMeta))
		{
			portrait.SetMeta(OriginalPivotMeta, portrait.PivotOffset);
		}
	}
}
