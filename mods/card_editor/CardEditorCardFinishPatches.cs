using System;
using System.Collections.Generic;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace SlayTheSpire2Mod.CardEditor;

internal static class CardEditorCardFinishResolver
{
	public static CardEditorVisualFinish GetDesiredFinish(CardModel? model)
	{
		if (model?.Id == null)
		{
			return CardEditorVisualFinish.None;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
		{
			return CardEditorCreatedCardsStore.GetFinish(model.Id);
		}

		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return CardEditorVisualFinish.None;
		}

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
		{
			return draftOverride.Finish ?? CardEditorVisualFinish.None;
		}

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.Finish ?? CardEditorVisualFinish.None
			: CardEditorVisualFinish.None;
	}

	public static Dictionary<string, float>? GetDesiredFinishParams(CardModel? model)
	{
		if (model?.Id == null)
			return null;

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
			return CardEditorCreatedCardsStore.GetFinishParams(model.Id);

		if (CardEditorOverrides.SuppressAllOverrides)
			return null;

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
			return draftOverride.FinishParams;

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.FinishParams
			: null;
	}

	public static string? GetDesiredCustomFinishId(CardModel? model)
	{
		if (model?.Id == null)
		{
			return null;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
		{
			return CardEditorCreatedCardsStore.GetCustomFinishId(model.Id);
		}

		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return null;
		}

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
		{
			return draftOverride.CustomFinishId;
		}

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.CustomFinishId
			: null;
	}

	public static Dictionary<string, string>? GetDesiredCustomFinishParams(CardModel? model)
	{
		if (model?.Id == null)
			return null;

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
			return CardEditorCreatedCardsStore.GetCustomFinishParams(model.Id);

		if (CardEditorOverrides.SuppressAllOverrides)
			return null;

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
			return draftOverride.CustomFinishParams;

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.CustomFinishParams
			: null;
	}

	public static CardEditorVisualFinish GetDesiredBorderFinish(CardModel? model)
	{
		if (model?.Id == null)
		{
			return CardEditorVisualFinish.None;
		}

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
		{
			return CardEditorCreatedCardsStore.GetBorderFinish(model.Id);
		}

		if (CardEditorOverrides.SuppressAllOverrides)
		{
			return CardEditorVisualFinish.None;
		}

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
		{
			return draftOverride.BorderFinish ?? CardEditorVisualFinish.None;
		}

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.BorderFinish ?? CardEditorVisualFinish.None
			: CardEditorVisualFinish.None;
	}

	public static Dictionary<string, float>? GetDesiredBorderFinishParams(CardModel? model)
	{
		if (model?.Id == null)
			return null;

		if (CardEditorCreatedCardsStore.IsCreatedCardId(model.Id))
			return CardEditorCreatedCardsStore.GetBorderFinishParams(model.Id);

		if (CardEditorOverrides.SuppressAllOverrides)
			return null;

		if (CardEditorUiState.IsActive && CardEditorUiState.TryGetDraftOverride(model.Id, out CardOverride draftOverride))
			return draftOverride.BorderFinishParams;

		return CardEditorOverrides.TryGetEffectiveOverride(model.Id, out CardOverride overrideData)
			? overrideData.BorderFinishParams
			: null;
	}
}

internal static class CardEditorArtFinishCardSpace
{
	private static readonly Vector2 FallbackCardSize = new(300f, 422f);
	private const float ReferencePortraitHeightFraction = 190f / 422f;
	private static readonly AccessTools.FieldRef<NCard, TextureRect> FrameRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientBorderRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientBorder");

	public static Vector2 GetCardRectSize(NCard card, Control artControl)
	{
		Resolve(card, artControl, out _, out _, out Vector2 effectSize, out _, out _);
		return effectSize;
	}

	public static Vector2 GetDisplayedArtRectSize(Control artControl)
	{
		Rect2 artRect = SafeGlobalRect(artControl);
		if (HasUsefulSize(artRect.Size))
		{
			return artRect.Size;
		}

		Vector2 size = artControl.Size;
		if (HasUsefulSize(size))
		{
			Vector2 scale = artControl.Scale.Abs();
			if (!IsFinite(scale) || scale.X <= 0f || scale.Y <= 0f)
			{
				scale = Vector2.One;
			}

			Vector2 scaledSize = size * scale;
			if (HasUsefulSize(scaledSize))
			{
				return scaledSize;
			}

			return size;
		}

		return FallbackCardSize;
	}

	public static void Apply(ShaderMaterial material, NCard card, Control artControl)
	{
		if (material == null)
		{
			return;
		}

		Resolve(card, artControl, out Vector2 origin, out Vector2 scale, out Vector2 effectSize, out Vector2 effectScreenOrigin, out float patternScale);
		material.SetShaderParameter("card_effect_uv_origin", origin);
		material.SetShaderParameter("card_effect_uv_scale", scale);
		material.SetShaderParameter("card_effect_rect_size", effectSize);
		material.SetShaderParameter("card_effect_screen_origin", effectScreenOrigin);
		material.SetShaderParameter("card_effect_screen_size", effectSize);
		material.SetShaderParameter("card_effect_pattern_scale", patternScale);
	}

	public static Vector2 ApplyLocalArtSpace(ShaderMaterial material, Control artControl)
	{
		Vector2 effectSize = GetDisplayedArtRectSize(artControl);
		Vector2 screenOrigin = Vector2.Zero;
		Rect2 artRect = SafeGlobalRect(artControl);
		if (HasUsefulSize(artRect.Size))
		{
			screenOrigin = artRect.Position;
		}

		material.SetShaderParameter("card_effect_uv_origin", Vector2.Zero);
		material.SetShaderParameter("card_effect_uv_scale", Vector2.One);
		material.SetShaderParameter("card_effect_rect_size", effectSize);
		material.SetShaderParameter("card_effect_screen_origin", screenOrigin);
		material.SetShaderParameter("card_effect_screen_size", effectSize);
		material.SetShaderParameter("card_effect_pattern_scale", 1.0f);
		material.SetShaderParameter("rect_size", effectSize);
		return effectSize;
	}

	private static void Resolve(NCard card, Control artControl, out Vector2 origin, out Vector2 scale, out Vector2 effectSize, out Vector2 effectScreenOrigin, out float patternScale)
	{
		Rect2 artRect = SafeGlobalRect(artControl);
		Rect2 cardRect = ResolveCardBorderRect(card, artControl, artRect);

		if (!HasUsefulSize(cardRect.Size) || !HasUsefulSize(artRect.Size) || !LooksLikeCardRect(cardRect, artRect))
		{
			cardRect = FindLikelyCardAncestorRect(artControl, artRect);
		}

		if (!HasUsefulSize(cardRect.Size))
		{
			cardRect = new Rect2(Vector2.Zero, HasUsefulSize(artRect.Size) ? artRect.Size : FallbackCardSize);
		}

		if (!HasUsefulSize(artRect.Size))
		{
			artRect = new Rect2(cardRect.Position, cardRect.Size);
		}

		Vector2 cardSize = HasUsefulSize(cardRect.Size) ? cardRect.Size : FallbackCardSize;
		Rect2 drawRect = HasUsefulSize(artRect.Size) ? artRect : new Rect2(cardRect.Position, cardSize);
		effectSize = cardSize;
		effectScreenOrigin = cardRect.Position;
		origin = new Vector2(
			(drawRect.Position.X - cardRect.Position.X) / Mathf.Max(cardSize.X, 1f),
			(drawRect.Position.Y - cardRect.Position.Y) / Mathf.Max(cardSize.Y, 1f));
		scale = new Vector2(
			drawRect.Size.X / Mathf.Max(cardSize.X, 1f),
			drawRect.Size.Y / Mathf.Max(cardSize.Y, 1f));
		patternScale = 1f;

		if (!IsFinite(origin) || !IsFinite(scale) || scale.X <= 0f || scale.Y <= 0f)
		{
			origin = Vector2.Zero;
			scale = Vector2.One;
		}
		if (!IsFinite(effectSize))
		{
			effectSize = FallbackCardSize;
		}
		if (!IsFinite(effectScreenOrigin))
		{
			effectScreenOrigin = Vector2.Zero;
		}
		if (!float.IsFinite(patternScale) || patternScale <= 0f)
		{
			patternScale = 1f;
		}
	}

	private static float ComputePatternScale(Vector2 effectSize, Vector2 cardSize)
	{
		if (!HasUsefulSize(effectSize) || !HasUsefulSize(cardSize))
		{
			return 1f;
		}

		float visibleHeightFraction = effectSize.Y / Mathf.Max(cardSize.Y, 1f);
		float normalized = visibleHeightFraction / ReferencePortraitHeightFraction;
		return Mathf.Clamp(normalized, 0.65f, 2.6f);
	}

	private static Rect2 ResolveCardBorderRect(NCard card, Control artControl, Rect2 artRect)
	{
		TextureRect? ancientPortrait = TryGetAncientPortrait(card);
		if (ReferenceEquals(artControl, ancientPortrait) || (ancientPortrait?.Visible ?? false))
		{
			Rect2 ancientBorderRect = SafeGlobalRect(TryGetAncientBorder(card));
			if (HasUsefulSize(ancientBorderRect.Size))
			{
				return ancientBorderRect;
			}

			Rect2 ancientArtRect = SafeGlobalRect(ancientPortrait);
			if (HasUsefulSize(ancientArtRect.Size))
			{
				return ancientArtRect;
			}
		}

		Rect2 frameRect = SafeGlobalRect(TryGetFrame(card));
		if (HasUsefulSize(frameRect.Size))
		{
			return frameRect;
		}

		Rect2 cardRect = SafeGlobalRect(card as Control);
		if (HasUsefulSize(cardRect.Size))
		{
			return cardRect;
		}

		return default;
	}

	private static TextureRect? TryGetFrame(NCard card)
	{
		try
		{
			return FrameRef(card);
		}
		catch
		{
			return null;
		}
	}

	private static TextureRect? TryGetAncientPortrait(NCard card)
	{
		try
		{
			return AncientPortraitRef(card);
		}
		catch
		{
			return null;
		}
	}

	private static TextureRect? TryGetAncientBorder(NCard card)
	{
		try
		{
			return AncientBorderRef(card);
		}
		catch
		{
			return null;
		}
	}

	private static Rect2 SafeGlobalRect(Control? control)
	{
		if (control == null || !GodotObject.IsInstanceValid(control))
		{
			return default;
		}

		try
		{
			Rect2 rect = control.GetGlobalRect();
			if (HasUsefulSize(rect.Size))
			{
				return rect;
			}
		}
		catch
		{
		}

		Vector2 size = control.Size;
		if (HasUsefulSize(size))
		{
			Vector2 scale = control.Scale.Abs();
			if (!HasUsefulSize(scale))
			{
				scale = Vector2.One;
			}
			return new Rect2(control.GlobalPosition, size * scale);
		}

		return default;
	}

	private static Rect2 FindLikelyCardAncestorRect(Control artControl, Rect2 artRect)
	{
		Node? node = artControl;
		Rect2 best = default;
		float bestArea = float.MaxValue;
		while (node != null)
		{
			if (node is Control control)
			{
				Rect2 rect = SafeGlobalRect(control);
				if (LooksLikeCardRect(rect, artRect))
				{
					float area = rect.Size.X * rect.Size.Y;
					if (area < bestArea)
					{
						best = rect;
						bestArea = area;
					}
				}
			}

			node = node.GetParent();
		}

		return best;
	}

	private static bool LooksLikeCardRect(Rect2 candidate, Rect2 artRect)
	{
		if (!HasUsefulSize(candidate.Size))
		{
			return false;
		}

		if (!HasUsefulSize(artRect.Size))
		{
			return true;
		}

		float aspect = candidate.Size.X / Mathf.Max(candidate.Size.Y, 1f);
		bool cardAspect = aspect >= 0.58f && aspect <= 0.86f;
		bool sameAsArt = Mathf.Abs(candidate.Size.X - artRect.Size.X) <= Mathf.Max(2f, artRect.Size.X * 0.01f)
			&& Mathf.Abs(candidate.Size.Y - artRect.Size.Y) <= Mathf.Max(2f, artRect.Size.Y * 0.01f);
		bool largerThanArt = candidate.Size.X >= artRect.Size.X * 1.01f
			&& candidate.Size.Y >= artRect.Size.Y * 1.01f;
		bool notViewportSized = candidate.Size.X <= artRect.Size.X * 3.0f
			&& candidate.Size.Y <= artRect.Size.Y * 3.0f;
		return cardAspect && (sameAsArt || largerThanArt) && notViewportSized;
	}

	private static bool HasUsefulSize(Vector2 size)
		=> size.X > 1f && size.Y > 1f && IsFinite(size);

	private static bool IsFinite(Vector2 value)
		=> float.IsFinite(value.X) && float.IsFinite(value.Y);
}

internal static class CardEditorArtFinishOverlayNodes
{
	private const string OverlayNodeNamePrefix = "CardEditorArtFinishOverlay_";
	private static readonly AccessTools.FieldRef<NCard, CanvasGroup> PortraitCanvasGroupRef =
		AccessTools.FieldRefAccess<NCard, CanvasGroup>("_portraitCanvasGroup");

	public static ColorRect? SyncOverlay(NCard card, string key, bool enabled, bool fullArt)
	{
		CanvasGroup? group = TryGetPortraitCanvasGroup(card);
		if (group == null || !GodotObject.IsInstanceValid(group))
		{
			return null;
		}

		string overlayName = OverlayNodeNamePrefix + key;
		if (!enabled)
		{
			RemoveOverlay(group, overlayName);
			return null;
		}

		ColorRect overlay = GetOrCreateOverlay(group, overlayName);
		SyncOverlayLayout(overlay, fullArt);
		return overlay;
	}

	public static void ApplyArtSpace(ShaderMaterial material, bool fullArt)
	{
		Vector2 effectSize = fullArt
			? new Vector2(299f, 421f)
			: new Vector2(250f, 190f);

		material.SetShaderParameter("card_effect_uv_origin", Vector2.Zero);
		material.SetShaderParameter("card_effect_uv_scale", Vector2.One);
		material.SetShaderParameter("card_effect_rect_size", effectSize);
		material.SetShaderParameter("card_effect_screen_origin", Vector2.Zero);
		material.SetShaderParameter("card_effect_screen_size", effectSize);
		material.SetShaderParameter("card_effect_pattern_scale", 1.0f);
		material.SetShaderParameter("rect_size", effectSize);
	}

	public static void ClearPortraitMaterial(TextureRect? portrait, Shader shader)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (portrait.Material is ShaderMaterial existing && existing.Shader == shader)
		{
			portrait.Material = null;
		}
	}

	private static CanvasGroup? TryGetPortraitCanvasGroup(NCard card)
	{
		try
		{
			return PortraitCanvasGroupRef(card);
		}
		catch
		{
			return null;
		}
	}

	private static ColorRect GetOrCreateOverlay(CanvasGroup group, string overlayName)
	{
		for (int i = 0; i < group.GetChildCount(); i++)
		{
			if (group.GetChild(i) is ColorRect existing && existing.Name == overlayName)
			{
				return existing;
			}
		}

		ColorRect overlay = new()
		{
			Name = overlayName,
			Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = false
		};
		group.AddChild(overlay);
		return overlay;
	}

	private static void RemoveOverlay(CanvasGroup group, string overlayName)
	{
		for (int i = group.GetChildCount() - 1; i >= 0; i--)
		{
			if (group.GetChild(i) is ColorRect rect && rect.Name == overlayName)
			{
				group.RemoveChild(rect);
				rect.QueueFree();
			}
		}
	}

	private static void SyncOverlayLayout(ColorRect overlay, bool fullArt)
	{
		overlay.Visible = true;
		overlay.Modulate = Colors.White;
		overlay.SelfModulate = Colors.White;
		overlay.LayoutMode = 1;
		overlay.AnchorLeft = 0.5f;
		overlay.AnchorTop = 0.5f;
		overlay.AnchorRight = 0.5f;
		overlay.AnchorBottom = 0.5f;
		overlay.GrowHorizontal = Control.GrowDirection.Both;
		overlay.GrowVertical = Control.GrowDirection.Both;
		overlay.Scale = Vector2.One;
		overlay.Rotation = 0f;

		if (fullArt)
		{
			// Must match the AncientPortrait visual rect exactly: any child extending past it inflates the
			// portrait CanvasGroup's backbuffer union and stretches the vanilla ancient-portrait mask,
			// revealing art below the frame window.
			overlay.OffsetLeft = -153f;
			overlay.OffsetTop = -215f;
			overlay.OffsetRight = 146f;
			overlay.OffsetBottom = 206f;
			overlay.PivotOffset = new Vector2(149.5f, 210.5f);
		}
		else
		{
			overlay.OffsetLeft = -125f;
			overlay.OffsetTop = -168f;
			overlay.OffsetRight = 125f;
			overlay.OffsetBottom = 22f;
			overlay.PivotOffset = new Vector2(125f, 95f);
		}
	}
}

internal sealed class CardEditorRainbowFoilOverlay : Control
{
	private const string ShaderCode = @"shader_type canvas_item;
 render_mode blend_premul_alpha;

 uniform float intensity = 1.0;
 uniform float lower_fade_start = 0.58;
 uniform float lower_fade_end = 0.96;
 uniform float lower_mask_min = 0.2;
 uniform vec2 rect_size = vec2(300.0, 422.0);
 uniform float inset_px = 1.0;
 uniform float corner_radius_px = 30.0;
 uniform float corner_softness_px = 1.5;

 float sd_round_rect(vec2 p, vec2 half_extents, float radius)
 {
	vec2 q = abs(p) - half_extents + vec2(radius);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
 }

 float hash(vec2 p)
 {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
 }

 vec2 hash2(vec2 p)
 {
	vec2 q = vec2(
		dot(p, vec2(127.1, 311.7)),
		dot(p, vec2(269.5, 183.3)));
	return -1.0 + 2.0 * fract(sin(q) * 43758.5453123);
 }

 float noise(vec2 p)
 {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);

	float n00 = dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0));
	float n10 = dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
	float n01 = dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
	float n11 = dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));

	float n = mix(mix(n00, n10, u.x), mix(n01, n11, u.x), u.y);
	return clamp(n * 0.5 + 0.5, 0.0, 1.0);
 }

 float fbm(vec2 p)
 {
	float value = 0.0;
	float amplitude = 0.5;
 
	for (int i = 0; i < 4; i++)
	{
		float footprint = max(fwidth(p).x, fwidth(p).y);
		float octaveWeight = clamp(1.0 - footprint * 1.75, 0.0, 1.0);
		value += noise(p) * amplitude * octaveWeight;
		p *= 2.03;
		p = vec2(0.80 * p.x - 0.60 * p.y, 0.60 * p.x + 0.80 * p.y) + vec2(17.1, 9.2);
		amplitude *= 0.5;
	}
 
	return value;
 }

	void fragment()
	{
		vec2 uv = UV;
		vec2 safeSize = max(rect_size, vec2(1.0));
		vec2 inset = vec2(inset_px);
		vec2 halfSize = (safeSize - inset * 2.0) * 0.5;
		vec2 local = uv * safeSize - safeSize * 0.5;
		float signedDistance = sd_round_rect(local, halfSize, corner_radius_px);
		float cornerAA = max(corner_softness_px, fwidth(signedDistance));
		float cornerMask = 1.0 - smoothstep(0.0, cornerAA, signedDistance);
		if (cornerMask <= 0.001)
		{
			discard;
		}

		vec2 centered = uv - vec2(0.5);
		float t = TIME * 0.33;
		float diagonal = uv.x * 1.18 + uv.y * 0.82;
		float sweepPhase = diagonal * 20.0 - t * 7.2;
		float sweepAA = clamp(1.0 - fwidth(sweepPhase) * 0.35, 0.0, 1.0);
		float sweepA = sin(sweepPhase) * 0.5 + 0.5;
		float sweepB = sin((uv.x * -17.0 + uv.y * 13.0) + t * 9.0) * 0.5 + 0.5;

		float mistScale = 7.2;
		float mistFoot = max(fwidth(uv.x * mistScale), fwidth(uv.y * mistScale));
		float mistLod = clamp(1.0 - mistFoot * 1.6, 0.0, 1.0);
		float mist = fbm(uv * mix(4.8, mistScale, mistLod) + vec2(t * 0.18, -t * 0.16));

		float shardScale = 19.0;
		float shardFoot = max(fwidth(uv.x * shardScale), fwidth(uv.y * shardScale));
		float shardLod = clamp(1.0 - shardFoot * 1.2, 0.0, 1.0);
		float shards = fbm(uv * mix(11.0, shardScale, shardLod) + vec2(-t * 0.5, t * 0.38));
		float crystal = smoothstep(0.56, 0.93, shards + sweepA * 0.34) * mix(0.55, 1.0, shardLod);

		float prismaticLines = pow(smoothstep(0.18, 1.0, sweepA), 3.7) * sweepAA;
		float centerGlow = 1.0 - smoothstep(0.16, 0.76, length(centered * vec2(1.0, 1.35)));
		float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
		float edgeGleam = 1.0 - smoothstep(0.02, 0.14, edgeDistance);
		float border = smoothstep(0.0, 0.035, uv.x) * smoothstep(0.0, 0.035, uv.y) * smoothstep(0.0, 0.035, 1.0 - uv.x) * smoothstep(0.0, 0.035, 1.0 - uv.y);
		border = 1.0 - border;

		vec2 sparkleGrid = vec2(120.0, 196.0);
		vec2 sparkleUv = uv * sparkleGrid;
		vec2 sparkleCell = floor(sparkleUv);
		vec2 sparkleFract = fract(sparkleUv);
		float sparkles = 0.0;
		for (int y = -1; y <= 1; y++)
		{
			for (int x = -1; x <= 1; x++)
			{
				vec2 neighbor = vec2(float(x), float(y));
				vec2 cell = sparkleCell + neighbor;
				vec2 rnd = vec2(hash(cell), hash(cell + vec2(5.7, 11.3)));
				vec2 pos = neighbor + rnd - sparkleFract;

				float dist2 = dot(pos, pos);
				float core = exp(-dist2 * 55.0);
				float envelope = exp(-dist2 * 14.0);

				float angle = rnd.x * 6.28318;
				float c = cos(angle);
				float s = sin(angle);
				vec2 p = vec2(c * pos.x - s * pos.y, s * pos.x + c * pos.y);

				float rayWidth = 0.05;
				float invW = 1.0 / max(rayWidth * rayWidth, 0.0001);
				float rayLen = 7.5;
				float rayH = exp(-p.y * p.y * invW) * exp(-p.x * p.x * rayLen);
				float rayV = exp(-p.x * p.x * invW) * exp(-p.y * p.y * rayLen);
				float rays = max(rayH, rayV) * envelope;

				float shape = clamp(core + rays * 0.85, 0.0, 1.0);

				float pulse = 0.35 + 0.65 * sin(t * 7.0 + hash(cell + vec2(2.1, 9.2)) * 6.28318);
				float gate = smoothstep(0.84, 1.0, hash(cell + vec2(13.2, 3.4)));
				float candidate = shape * pulse * gate;
				sparkles = max(sparkles, candidate);
			}
		}
		sparkles = clamp(pow(sparkles, 0.85) * (0.26 + 0.74 * sweepB), 0.0, 1.0);

		vec3 rainbowA = 0.5 + 0.5 * cos(vec3(0.0, 2.0, 4.0) + (diagonal * 5.2 + mist * 1.4 + t * 1.1) * 6.28318);
		vec3 rainbowB = 0.5 + 0.5 * cos(vec3(1.15, 3.15, 5.15) + ((uv.y * 4.0 - uv.x * 2.2) + sweepB * 0.55 - t * 0.82) * 6.28318);
		vec3 rainbow = mix(rainbowA, rainbowB, 0.5 + crystal * 0.25);
		float lowerMask = 1.0 - smoothstep(lower_fade_start, lower_fade_end, uv.y);
		lowerMask = mix(lower_mask_min, 1.0, lowerMask);
		float alpha = 0.02;
		alpha += prismaticLines * 0.08;
		alpha += crystal * 0.08;
		alpha += centerGlow * 0.05;
		alpha += edgeGleam * (0.07 + 0.05 * sweepA);
		alpha += border * 0.08;
		alpha += sparkles * 0.26;
		alpha *= lowerMask * cornerMask;
		alpha = clamp(alpha * intensity, 0.0, 0.22);
		vec3 color = rainbow * (0.55 + prismaticLines * 0.55 + crystal * 0.35);
		color += vec3(1.0, 0.99, 0.95) * sparkles * 1.05;
		color += rainbowB * edgeGleam * 0.22;
		color += mix(rainbowA, vec3(1.0, 0.95, 0.85), 0.4) * border * 0.18;
		color = clamp(color, vec3(0.0), vec3(1.05));
		COLOR = vec4(color * alpha, alpha);
	}";

	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };
	private readonly ShaderMaterial _material;
	private readonly ColorRect _foilRect;
	private Vector2 _lastKnownRectSize = Vector2.Zero;

	public CardEditorRainbowFoilOverlay()
	{
		Name = "CardEditorFinishOverlay";
		MouseFilter = MouseFilterEnum.Ignore;
		LayoutMode = 3;
		ZIndex = 0;
		ClipContents = false;
		_material = new ShaderMaterial { Shader = SharedShader };
		_material.SetShaderParameter("rect_size", new Vector2(300.0f, 422.0f));
		_foilRect = new ColorRect
		{
			Color = new Color(1, 1, 1, 0),
			Material = _material,
			MouseFilter = MouseFilterEnum.Ignore,
			LayoutMode = 0,
			OffsetLeft = -150.0f,
			OffsetTop = -211.0f,
			OffsetRight = 150.0f,
			OffsetBottom = 211.0f
		};
		AddChild(_foilRect);
		ApplyStyle(fullArt: false);
	}

	public override void _Ready()
	{
		base._Ready();
		UpdateRectSize();
	}

	public override void _Notification(int what)
	{
		base._Notification(what);

		if (what == NotificationResized)
		{
			UpdateRectSize();
		}
	}

	public void ApplyStyle(bool fullArt)
	{
		_material.SetShaderParameter("intensity", fullArt ? 1.0f : 0.85f);
		_material.SetShaderParameter("lower_fade_start", fullArt ? 0.86f : 0.72f);
		_material.SetShaderParameter("lower_fade_end", fullArt ? 0.998f : 0.92f);
		_material.SetShaderParameter("lower_mask_min", fullArt ? 0.35f : 0.22f);
		_material.SetShaderParameter("inset_px", fullArt ? 1.0f : 1.5f);
		_material.SetShaderParameter("corner_radius_px", 30.0f);
		_material.SetShaderParameter("corner_softness_px", 1.5f);
		UpdateRectSize();
	}

	private void UpdateRectSize()
	{
		Vector2 currentSize = Size;
		if (currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			currentSize = _foilRect.Size;
		}

		if (currentSize == _lastKnownRectSize || currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			return;
		}

		_lastKnownRectSize = currentSize;
		_material.SetShaderParameter("rect_size", currentSize);
	}
}

internal static class CardEditorRainbowRareFoilArtController
{
	private const string ShaderCode = @"shader_type canvas_item;

 uniform float intensity = 1.0;
 uniform float lower_fade_start = 0.58;
 uniform float lower_fade_end = 0.96;
 uniform float lower_mask_min = 0.2;
 uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
 uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
 uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);

 float hash(vec2 p)
 {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
 }

 vec2 hash2(vec2 p)
 {
	vec2 q = vec2(
		dot(p, vec2(127.1, 311.7)),
		dot(p, vec2(269.5, 183.3)));
	return -1.0 + 2.0 * fract(sin(q) * 43758.5453123);
 }

 float noise(vec2 p)
 {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);

	float n00 = dot(hash2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0));
	float n10 = dot(hash2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
	float n01 = dot(hash2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
	float n11 = dot(hash2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));

	float n = mix(mix(n00, n10, u.x), mix(n01, n11, u.x), u.y);
	return clamp(n * 0.5 + 0.5, 0.0, 1.0);
 }

 float fbm(vec2 p)
 {
	float value = 0.0;
	float amplitude = 0.5;

	for (int i = 0; i < 4; i++)
	{
		float footprint = max(fwidth(p).x, fwidth(p).y);
		float octaveWeight = clamp(1.0 - footprint * 1.75, 0.0, 1.0);
		value += noise(p) * amplitude * octaveWeight;
		p *= 2.03;
		p = vec2(0.80 * p.x - 0.60 * p.y, 0.60 * p.x + 0.80 * p.y) + vec2(17.1, 9.2);
		amplitude *= 0.5;
	}

	return value;
 }

 void fragment()
 {
	vec2 uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 centered = uv - vec2(0.5);
	float t = TIME * 0.33;
	float diagonal = uv.x * 1.18 + uv.y * 0.82;
	float sweepPhase = diagonal * 20.0 - t * 7.2;
	float sweepAA = clamp(1.0 - fwidth(sweepPhase) * 0.35, 0.0, 1.0);
	float sweepA = sin(sweepPhase) * 0.5 + 0.5;
	float sweepB = sin((uv.x * -17.0 + uv.y * 13.0) + t * 9.0) * 0.5 + 0.5;

	float mistScale = 7.2;
	float mistFoot = max(fwidth(uv.x * mistScale), fwidth(uv.y * mistScale));
	float mistLod = clamp(1.0 - mistFoot * 1.6, 0.0, 1.0);
	float mist = fbm(uv * mix(4.8, mistScale, mistLod) + vec2(t * 0.18, -t * 0.16));

	float shardScale = 19.0;
	float shardFoot = max(fwidth(uv.x * shardScale), fwidth(uv.y * shardScale));
	float shardLod = clamp(1.0 - shardFoot * 1.2, 0.0, 1.0);
	float shards = fbm(uv * mix(11.0, shardScale, shardLod) + vec2(-t * 0.5, t * 0.38));
	float crystal = smoothstep(0.56, 0.93, shards + sweepA * 0.34) * mix(0.55, 1.0, shardLod);

	float prismaticLines = pow(smoothstep(0.18, 1.0, sweepA), 3.7) * sweepAA;
	float centerGlow = 1.0 - smoothstep(0.16, 0.76, length(centered * vec2(1.0, 1.35)));
	float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
	float edgeGleam = 1.0 - smoothstep(0.02, 0.14, edgeDistance);

	vec2 sparkleGrid = max(card_effect_rect_size * vec2(0.42, 0.46), vec2(96.0, 150.0));
	vec2 sparkleUv = uv * sparkleGrid;
	vec2 sparkleCell = floor(sparkleUv);
	vec2 sparkleFract = fract(sparkleUv);
	float sparkles = 0.0;
	for (int y = -1; y <= 1; y++)
	{
		for (int x = -1; x <= 1; x++)
		{
			vec2 neighbor = vec2(float(x), float(y));
			vec2 cell = sparkleCell + neighbor;
			vec2 rnd = vec2(hash(cell), hash(cell + vec2(5.7, 11.3)));
			vec2 pos = neighbor + rnd - sparkleFract;

			float dist2 = dot(pos, pos);
			float core = exp(-dist2 * 55.0);
			float envelope = exp(-dist2 * 14.0);

			float angle = rnd.x * 6.28318;
			float c = cos(angle);
			float s = sin(angle);
			vec2 p = vec2(c * pos.x - s * pos.y, s * pos.x + c * pos.y);

			float rayWidth = 0.05;
			float invW = 1.0 / max(rayWidth * rayWidth, 0.0001);
			float rayLen = 7.5;
			float rayH = exp(-p.y * p.y * invW) * exp(-p.x * p.x * rayLen);
			float rayV = exp(-p.x * p.x * invW) * exp(-p.y * p.y * rayLen);
			float rays = max(rayH, rayV) * envelope;

			float shape = clamp(core + rays * 0.85, 0.0, 1.0);

			float pulse = 0.35 + 0.65 * sin(t * 7.0 + hash(cell + vec2(2.1, 9.2)) * 6.28318);
			float gate = smoothstep(0.84, 1.0, hash(cell + vec2(13.2, 3.4)));
			float candidate = shape * pulse * gate;
			sparkles = max(sparkles, candidate);
		}
	}
	sparkles = clamp(pow(sparkles, 0.85) * (0.26 + 0.74 * sweepB), 0.0, 1.0);

	vec3 rainbowA = 0.5 + 0.5 * cos(vec3(0.0, 2.0, 4.0) + (diagonal * 5.2 + mist * 1.4 + t * 1.1) * 6.28318);
	vec3 rainbowB = 0.5 + 0.5 * cos(vec3(1.15, 3.15, 5.15) + ((uv.y * 4.0 - uv.x * 2.2) + sweepB * 0.55 - t * 0.82) * 6.28318);
	vec3 rainbow = mix(rainbowA, rainbowB, 0.5 + crystal * 0.25);
	float lowerMask = 1.0 - smoothstep(lower_fade_start, lower_fade_end, uv.y);
	lowerMask = mix(lower_mask_min, 1.0, lowerMask);
	float alpha = 0.02;
	alpha += prismaticLines * 0.08;
	alpha += crystal * 0.08;
	alpha += centerGlow * 0.05;
	alpha += edgeGleam * (0.07 + 0.05 * sweepA);
	alpha += sparkles * 0.26;
	alpha *= lowerMask;
	alpha = clamp(alpha * intensity, 0.0, 0.24);
	vec3 foil = rainbow * (0.55 + prismaticLines * 0.55 + crystal * 0.35);
	foil += vec3(1.0, 0.99, 0.95) * sparkles * 1.05;
	foil += rainbowB * edgeGleam * 0.22;
	foil = clamp(foil, vec3(0.0), vec3(1.05));
	COLOR = vec4(clamp(foil, vec3(0.0), vec3(1.0)), alpha);
 }";

	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };

	public static void Sync(NCard card, bool enabled, bool fullArt, Dictionary<string, float>? fp = null)
	{
		if (card == null)
		{
			return;
		}

		TextureRect? portrait = null;
		TextureRect? ancientPortrait = null;
		try
		{
			portrait = PortraitRef(card);
			ancientPortrait = AncientPortraitRef(card);
		}
		catch
		{
			return;
		}

		CardEditorArtFinishOverlayNodes.ClearPortraitMaterial(portrait, SharedShader);
		CardEditorArtFinishOverlayNodes.ClearPortraitMaterial(ancientPortrait, SharedShader);

		ColorRect? overlay = CardEditorArtFinishOverlayNodes.SyncOverlay(card, "RainbowRareFoil", enabled, fullArt);
		if (overlay == null)
		{
			return;
		}

		ShaderMaterial material;
		if (overlay.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == SharedShader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = SharedShader };
			overlay.Material = material;
		}

		material.SetShaderParameter("intensity", CardEditorTextureLoader.P(fp, "strength", fullArt ? 1.0f : 0.86f));
		material.SetShaderParameter("lower_fade_start", fullArt ? 0.86f : 0.72f);
		material.SetShaderParameter("lower_fade_end", fullArt ? 0.998f : 0.92f);
		material.SetShaderParameter("lower_mask_min", fullArt ? 0.35f : 0.22f);
		CardEditorArtFinishOverlayNodes.ApplyArtSpace(material, fullArt);
	}
}

internal sealed class CardEditorPrismaticBandGlareOverlay : Control
{
	internal const string ShaderCode = @"shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec2 rect_size = vec2(300.0, 422.0);
uniform float corner_radius_px = 30.0;
uniform float corner_softness_px = 1.5;
uniform float inset_px = 1.0;

uniform float intensity = 1.0;
uniform float lower_fade_start = 0.62;
uniform float lower_fade_end = 0.97;
uniform float lower_mask_min = 0.18;

uniform float band_strength = 0.22;
uniform float glare_strength = 0.24;
uniform float sweep_speed = 1.0;
uniform float line_count = 22.0;
uniform float line_width = 0.10;

float sd_round_rect(vec2 p, vec2 he, float r)
{
	vec2 q = abs(p) - he + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

vec3 sunpillar_gradient(float coord)
{
	vec3 c1 = vec3(1.0, 0.48, 0.47);
	vec3 c2 = vec3(1.0, 0.89, 0.43);
	vec3 c3 = vec3(0.64, 1.0, 0.46);
	vec3 c4 = vec3(0.55, 1.0, 0.96);
	vec3 c5 = vec3(0.57, 0.68, 1.0);
	vec3 c6 = vec3(0.88, 0.52, 1.0);

	float wrapped = fract(coord);
	float scaled = wrapped * 6.0;
	float idx = floor(scaled);
	float f = fract(scaled);

	vec3 from_col = c1;
	vec3 to_col = c2;
	if (idx < 1.0)
	{
		from_col = c1;
		to_col = c2;
	}
	else if (idx < 2.0)
	{
		from_col = c2;
		to_col = c3;
	}
	else if (idx < 3.0)
	{
		from_col = c3;
		to_col = c4;
	}
	else if (idx < 4.0)
	{
		from_col = c4;
		to_col = c5;
	}
	else if (idx < 5.0)
	{
		from_col = c5;
		to_col = c6;
	}
	else
	{
		from_col = c6;
		to_col = c1;
	}

	return mix(from_col, to_col, smoothstep(0.0, 1.0, f));
}

float metallic_band(vec2 uv, float angle_radians, float frequency, float width)
{
	vec2 dir = vec2(cos(angle_radians), sin(angle_radians));
	float band = abs(fract(dot(uv, dir) * frequency) - 0.5) * 2.0;
	return 1.0 - smoothstep(width, 1.0, band);
}

void fragment()
{
	vec2 safeSize = max(rect_size, vec2(1.0));
	vec2 halfSize = (safeSize - vec2(inset_px) * 2.0) * 0.5;
	vec2 local = UV * safeSize - safeSize * 0.5;
	float sd = sd_round_rect(local, halfSize, corner_radius_px);
	float cornerAA = max(corner_softness_px, fwidth(sd));
	float cornerMask = 1.0 - smoothstep(0.0, cornerAA, sd);
	if (cornerMask <= 0.001)
	{
		discard;
	}

	float t = TIME * sweep_speed;
	vec2 pointer = vec2(
		0.5 + sin(t * 0.83) * 0.24,
		0.34 + cos(t * 0.61) * 0.18
	);
	vec2 backgroundShift = vec2(
		0.44 + sin(t * 0.21) * 0.06,
		0.36 + cos(t * 0.18 + 0.8) * 0.05
	);

	float angle = radians(133.0);
	float bandA = metallic_band(UV + backgroundShift * 0.35, angle, line_count, line_width);
	float bandB = metallic_band(UV - backgroundShift * 0.25, angle, max(1.0, line_count * 0.73), line_width * 1.4);
	float bandSweep = sin(dot(UV, normalize(vec2(1.0, -0.72))) * 15.0 - t * 1.65) * 0.5 + 0.5;
	float bandMix = max(bandA, bandB * 0.72) * (0.72 + 0.28 * bandSweep);

	float sunpillarCoord = UV.y * 2.0 + backgroundShift.y * 0.85;
	vec3 rainbowTint = sunpillar_gradient(sunpillarCoord);
	vec3 bandColor = mix(vec3(0.62, 0.72, 1.0), rainbowTint, 0.75);

	float pointerDist = distance(UV, pointer);
	float glareCore = 1.0 - smoothstep(0.0, 0.28, pointerDist);
	float glareHalo = 1.0 - smoothstep(0.0, 0.78, pointerDist);
	vec3 glareColor = mix(vec3(0.30, 0.34, 0.42), vec3(1.0, 0.98, 0.93), pow(glareHalo, 1.35));

	float lowerMask = 1.0 - smoothstep(lower_fade_start, lower_fade_end, UV.y);
	lowerMask = mix(lower_mask_min, 1.0, lowerMask);

	float alpha = bandMix * band_strength;
	alpha += glareHalo * glare_strength * 0.65;
	alpha += glareCore * glare_strength * 0.35;
	alpha *= lowerMask * cornerMask * intensity;
	alpha = clamp(alpha, 0.0, 0.28);

	vec3 color = bandColor * (0.65 + bandMix * 1.15);
	color += vec3(1.0, 0.99, 0.95) * glareCore * 0.95;
	color += glareColor * glareHalo * 0.42;
	color = clamp(color, vec3(0.0), vec3(2.0));

	COLOR = vec4(color * alpha, alpha);
}";

	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };
	private readonly ShaderMaterial _material;
	private readonly ColorRect _overlayRect;
	private Vector2 _lastKnownRectSize = Vector2.Zero;

	public CardEditorPrismaticBandGlareOverlay()
	{
		Name = "CardEditorPrismaticBandGlareOverlay";
		MouseFilter = MouseFilterEnum.Ignore;
		LayoutMode = 3;
		ZIndex = 0;
		ClipContents = false;
		_material = new ShaderMaterial { Shader = SharedShader };
		_material.SetShaderParameter("rect_size", new Vector2(300.0f, 422.0f));
		_material.SetShaderParameter("card_origin", Vector2.Zero);
		_overlayRect = new ColorRect
		{
			Color = new Color(1, 1, 1, 0),
			Material = _material,
			MouseFilter = MouseFilterEnum.Ignore,
			LayoutMode = 0,
			OffsetLeft = -150.0f,
			OffsetTop = -211.0f,
			OffsetRight = 150.0f,
			OffsetBottom = 211.0f
		};
		AddChild(_overlayRect);
		ApplyStyle(fullArt: false);
	}

	public override void _Ready()
	{
		base._Ready();
		UpdateRectSize();
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
		{
			UpdateRectSize();
		}
	}

	public void ApplyStyle(bool fullArt)
	{
		_material.SetShaderParameter("intensity", fullArt ? 1.0f : 0.92f);
		_material.SetShaderParameter("lower_fade_start", fullArt ? 0.90f : 0.74f);
		_material.SetShaderParameter("lower_fade_end", fullArt ? 0.998f : 0.94f);
		_material.SetShaderParameter("lower_mask_min", fullArt ? 0.40f : 0.24f);
		_material.SetShaderParameter("band_strength", fullArt ? 0.24f : 0.22f);
		_material.SetShaderParameter("glare_strength", fullArt ? 0.28f : 0.24f);
		_material.SetShaderParameter("sweep_speed", 1.0f);
		_material.SetShaderParameter("line_count", 22.0f);
		_material.SetShaderParameter("line_width", 0.10f);
		_material.SetShaderParameter("inset_px", fullArt ? 1.0f : 1.5f);
		_material.SetShaderParameter("corner_radius_px", 30.0f);
		_material.SetShaderParameter("corner_softness_px", 1.5f);
		UpdateRectSize();
	}

	private void UpdateRectSize()
	{
		Vector2 currentSize = Size;
		if (currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			currentSize = _overlayRect.Size;
		}

		if (currentSize == _lastKnownRectSize || currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			return;
		}

		_lastKnownRectSize = currentSize;
		_material.SetShaderParameter("rect_size", currentSize);
	}
}

internal static class CardEditorRainbowGlitterArtController
{
	private const string ShaderCode = @"shader_type canvas_item;

// --- holographic foil shader ---
// Models light diffracting through an iridescent film on top of card art.
// The original art is preserved; rainbow is composited via overlay blend.

uniform float strength = 0.76;
uniform float saturation = 0.60;
uniform float pastel = 0.30;
uniform float hue_spread = 0.72;
uniform float hue_speed = 0.12;
uniform float hue_offset = 0.0;
uniform vec2 gradient_dir = vec2(1.0, 0.7);
uniform float gradient_scale = 0.85;
uniform float contrast_gamma = 0.55;
uniform float brightness_boost = 1.22;
uniform float edge_px = 2.0;
uniform float edge_threshold = 0.05;
uniform float edge_darken = 0.78;
uniform float ink_preserve = 0.82;
uniform float sheen_strength = 0.22;
uniform float sheen_speed = 0.28;
uniform float sheen_width = 0.18;
uniform float grain_strength = 0.005;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);

float hash12(vec2 p)
{
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

vec3 hsv2rgb(vec3 c)
{
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec3 overlay_blend(vec3 base_col, vec3 over)
{
	// Photoshop-style overlay: boosts contrast while tinting.
	// dark base -> multiply, light base -> screen.
	return mix(
		2.0 * base_col * over,
		1.0 - 2.0 * (1.0 - base_col) * (1.0 - over),
		step(0.5, base_col)
	);
}

vec2 art_rect_uv(vec2 frag)
{
	// FRAGCOORD is a stage built-in: it is only visible inside fragment(), so it must be
	// passed in as a parameter or the shader fails to compile and the finish renders nothing.
	vec2 safe_size = max(card_effect_screen_size, vec2(1.0));
	return clamp((frag - card_effect_screen_origin) / safe_size, vec2(0.0), vec2(1.0));
}

void fragment()
{
	vec4 tex = texture(TEXTURE, UV);
	float a = tex.a;
	if (a <= 0.001)
	{
		discard;
	}

	float luma = dot(tex.rgb, vec3(0.2126, 0.7152, 0.0722));
	float lit = clamp(pow(luma, contrast_gamma) * brightness_boost, 0.0, 1.0);
	float t = TIME * motion_speed + time_offset;
	vec2 effect_uv = art_rect_uv(FRAGCOORD.xy);
	vec2 dir = normalize(gradient_dir);
	vec2 crossDir = normalize(vec2(-dir.y, dir.x));
	float phaseA = dot(effect_uv - vec2(0.5), dir) * gradient_scale * hue_spread;
	float phaseB = dot(effect_uv - vec2(0.5), crossDir) * gradient_scale * 0.45;
	float flow = sin((effect_uv.x + effect_uv.y * 0.8) * 2.3561945 + t * 0.22) * 0.035;
	vec3 rainbowA = 0.5 + 0.5 * cos(vec3(0.0, 2.0, 4.0) + (phaseA + flow + t * hue_speed + hue_offset) * 6.2831853);
	vec3 rainbowB = 0.5 + 0.5 * cos(vec3(0.8, 2.8, 4.8) + (phaseB - t * hue_speed * 0.45 + hue_offset) * 6.2831853);
	vec3 rainbow = mix(rainbowA, rainbowB, 0.32);
	rainbow = mix(rainbow, vec3(1.0), pastel);
	float rl = dot(rainbow, vec3(0.2126, 0.7152, 0.0722));
	rainbow = mix(vec3(rl), rainbow, saturation);
	rainbow = mix(rainbow, color_tint, tint_strength);

	vec3 foilDirect = rainbow * lit;
	vec3 foilOverlay = overlay_blend(tex.rgb, rainbow);
	vec3 foilColor = mix(foilDirect, foilOverlay, smoothstep(0.15, 0.55, luma));
	float pearlMask = smoothstep(0.20, 0.95, lit);
	foilColor = mix(foilColor, mix(foilColor, vec3(1.0), 0.35), pastel * pearlMask * 0.55);

	vec2 px = TEXTURE_PIXEL_SIZE * edge_px;
	float lpx = dot(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lnx = dot(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lpy = dot(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lny = dot(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float edge = abs(lpx - lnx) + abs(lpy - lny);
	edge = smoothstep(edge_threshold, edge_threshold + 0.10, edge);
	float inkMask = edge * (1.0 - smoothstep(0.08, 0.42, luma));
	foilColor *= mix(1.0, edge_darken, edge);

	vec3 outColor = mix(tex.rgb, foilColor, strength);
	outColor = mix(outColor, tex.rgb, inkMask * ink_preserve);

	// Animated sweeping sheen highlight (the glossy flash).
	float sheenPhase = fract(t * sheen_speed);
	float sheenCoord = dot(effect_uv, normalize(vec2(1.0, 1.4)));
	float sheenWave = sheenPhase * 2.5 - 0.75;
	float sheenDist = sheenCoord - sheenWave;
	float sheenGlow = exp(-sheenDist * sheenDist / max(0.001, sheen_width * sheen_width));
	outColor += (rainbow * 0.55 + vec3(1.0) * 0.45) * sheenGlow * sheen_strength * lit;

	// Subtle animated grain for physical card feel.
	float grain = hash12(effect_uv * 320.0 + vec2(t * 0.08, 0.0)) - 0.5;
	outColor += vec3(grain) * grain_strength;

	float edgeFade = smoothstep(0.0, 0.05, effect_uv.x)
		* smoothstep(0.0, 0.05, effect_uv.y)
		* smoothstep(0.0, 0.05, 1.0 - effect_uv.x)
		* smoothstep(0.0, 0.05, 1.0 - effect_uv.y);
	outColor = mix(tex.rgb, outColor, edgeFade);
	COLOR = vec4(clamp(outColor, vec3(0.0), vec3(1.0)), a);
}";

	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> TitleBannerRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_banner");

	public static void Sync(NCard card, bool enabled, bool fullArt, Dictionary<string, float>? fp = null)
	{
		if (card == null)
		{
			return;
		}

		TextureRect? portrait = null;
		TextureRect? ancientPortrait = null;
		TextureRect? titleBanner = null;
		try
		{
			portrait = PortraitRef(card);
			ancientPortrait = AncientPortraitRef(card);
			titleBanner = TitleBannerRef(card);
		}
		catch
		{
			return;
		}

		CardEditorArtFinishOverlayNodes.SyncOverlay(card, "RainbowGlitterArt", enabled: false, fullArt);
		SyncPortrait(portrait, enabled, fullArt, fp);
		SyncPortrait(ancientPortrait, enabled, fullArt, fp);
		SyncTitleBanner(titleBanner, false, fullArt, fp);
	}

	private static void SyncPortrait(TextureRect? portrait, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (!enabled)
		{
			if (portrait.Material is ShaderMaterial existing && existing.Shader == SharedShader)
			{
				portrait.Material = null;
			}
			return;
		}

		ShaderMaterial material;
		if (portrait.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == SharedShader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = SharedShader };
			portrait.Material = material;
		}

		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.78f : 0.76f));
		material.SetShaderParameter("saturation", CardEditorTextureLoader.P(fp, "saturation", fullArt ? 0.62f : 0.60f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", fullArt ? 0.28f : 0.30f));
		material.SetShaderParameter("hue_spread", CardEditorTextureLoader.P(fp, "hueSpread", fullArt ? 0.75f : 0.72f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("hue_offset", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("gradient_dir", new Vector2(1.0f, 0.7f));
		material.SetShaderParameter("gradient_scale", CardEditorTextureLoader.P(fp, "patternScale", fullArt ? 0.88f : 0.85f));
		material.SetShaderParameter("contrast_gamma", fullArt ? 0.52f : 0.55f);
		material.SetShaderParameter("brightness_boost", CardEditorTextureLoader.P(fp, "brightness", fullArt ? 1.24f : 1.22f));
		material.SetShaderParameter("edge_px", fullArt ? 2.25f : 2.0f);
		material.SetShaderParameter("edge_threshold", fullArt ? 0.04f : 0.05f);
		material.SetShaderParameter("edge_darken", fullArt ? 0.80f : 0.78f);
		material.SetShaderParameter("ink_preserve", fullArt ? 0.78f : 0.82f);
		material.SetShaderParameter("sheen_strength", CardEditorTextureLoader.P(fp, "glareStrength", fullArt ? 0.24f : 0.22f));
		material.SetShaderParameter("sheen_speed", CardEditorTextureLoader.P(fp, "glareSpeed", 0.28f));
		material.SetShaderParameter("sheen_width", CardEditorTextureLoader.P(fp, "glareWidth", fullArt ? 0.17f : 0.18f));
		material.SetShaderParameter("grain_strength", fullArt ? 0.006f : 0.005f);
		CardEditorArtFinishCardSpace.ApplyLocalArtSpace(material, portrait);
	}

	private static void SyncTitleBanner(TextureRect? titleBanner, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		if (titleBanner == null || !GodotObject.IsInstanceValid(titleBanner))
		{
			return;
		}

		if (!enabled)
		{
			if (titleBanner.Material is ShaderMaterial existing && existing.Shader == SharedShader)
			{
				titleBanner.Material = null;
			}
			return;
		}

		ShaderMaterial material;
		if (titleBanner.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == SharedShader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = SharedShader };
			titleBanner.Material = material;
		}

		material.SetShaderParameter("strength", fullArt ? 0.72f : 0.70f);
		material.SetShaderParameter("saturation", fullArt ? 0.52f : 0.48f);
		material.SetShaderParameter("pastel", fullArt ? 0.36f : 0.40f);
		material.SetShaderParameter("hue_spread", 0.68f);
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("hue_offset", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("gradient_dir", new Vector2(1.0f, 0.7f));
		material.SetShaderParameter("gradient_scale", fullArt ? 0.90f : 0.86f);
		material.SetShaderParameter("contrast_gamma", 0.50f);
		material.SetShaderParameter("brightness_boost", 1.24f);
		material.SetShaderParameter("edge_px", 2.0f);
		material.SetShaderParameter("edge_threshold", 0.06f);
		material.SetShaderParameter("edge_darken", 0.82f);
		material.SetShaderParameter("ink_preserve", 0.88f);
		material.SetShaderParameter("sheen_strength", fullArt ? 0.14f : 0.12f);
		material.SetShaderParameter("sheen_speed", 0.28f);
		material.SetShaderParameter("sheen_width", 0.20f);
		material.SetShaderParameter("grain_strength", fullArt ? 0.004f : 0.003f);
	}
}

internal static class CardEditorPurpleWavesOceanController
{
	private const string OverlayNodeNamePrefix = "CardEditorPurpleWavesOceanOverlay_";

	private const string ShaderCode = @"shader_type canvas_item;

render_mode blend_mix;

// --- Purple Waves (Ocean) ---
// Alpha-based procedural ocean overlay.
// Applied as a separate overlay node so the effect is anchored to the card frame (portrait rect),
// not influenced by the underlying art's resolution/aspect/cropping.

uniform float strength = 1.0; // 0 = off, 1 = full overlay
uniform float brightness = 1.0;
uniform float pastel = 0.15;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform vec2 rect_size = vec2(300.0, 422.0);
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform float y_offset = 0.25;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);

uniform vec4 sea_color_dark : source_color = vec4(0.04, 0.32, 0.55, 1.0);
uniform vec4 sea_color_light : source_color = vec4(0.06, 0.47, 0.60, 1.0);
uniform vec4 refraction_color : source_color = vec4(0.96, 0.98, 0.86, 1.0);
uniform vec4 light_shaft_color : source_color = vec4(0.88, 0.90, 0.78, 1.0);

struct RaymarchResult {
	vec3 hit_pos;
	float hit_t;
	float dist;
};

float hash(vec2 p) {
	return 0.5 * (sin(dot(p, vec2(271.319, 413.975)) + 1217.13 * p.x * p.y)) + 0.5;
}

float noise(vec2 p) {
	vec2 w = fract(p);
	w = w * w * (3.0 - 2.0 * w);
	p = floor(p);
	return mix(mix(hash(p + vec2(0.0,0.0)), hash(p + vec2(1.0,0.0)), w.x),
		   mix(hash(p + vec2(0.0,1.0)), hash(p + vec2(1.0,1.0)), w.x), w.y);
}

float map_octave(vec2 uv) {
	uv = (uv + noise(uv)) / 2.5;
	uv = vec2(uv.x * 0.6 - uv.y * 0.8, uv.x * 0.8 + uv.y * 0.6);
	vec2 uvsin = 1.0 - abs(sin(uv));
	vec2 uvcos = abs(cos(uv));
	uv = mix(uvsin, uvcos, uvsin);
	float val = 1.0 - pow(uv.x * uv.y, 0.65);
	return val;
}

float map(vec3 p, float t) {
	vec2 uv = p.xz + t / 2.0;
	float amp = 0.6, freq = 2.0, val = 0.0;
	for(int i = 0; i < 3; ++i) {
		val += map_octave(uv) * amp;
		amp *= 0.3;
		uv *= freq;
	}
	uv = p.xz - 1000.0 - t / 2.0;
	amp = 0.6; freq = 2.0;
	for(int i = 0; i < 3; ++i) {
		val += map_octave(uv) * amp;
		amp *= 0.3;
		uv *= freq;
	}
	return val + 3.0 - p.y;
}

vec3 getNormal(vec3 p, float eps, float t) {
	vec3 px = p + vec3(eps, 0.0, 0.0);
	vec3 pz = p + vec3(0.0, 0.0, eps);
	return normalize(vec3(map(px, t), eps, map(pz, t)));
}

	RaymarchResult raymarch(vec3 ro, vec3 rd, float eps, float t) {
		RaymarchResult result;
		float l = 0.0, r = 26.0;
		int steps = 20;
		float dist = 1000000.0;
		for(int i = 0; i < steps; ++i) {
			float mid = (r + l) / 2.0;
			float mapmid = map(ro + rd * mid, t);
			dist = min(dist, abs(mapmid));
			if(mapmid > 0.0) {
				l = mid;
			} else {
				r = mid;
			}
		}
		result.hit_pos = ro + rd * l;
		result.hit_t = l;
		result.dist = dist;
		return result;
	}

float fbm(vec2 n) {
	float total = 0.0, amplitude = 1.0;
	for (int i = 0; i < 5; i++) {
		total += noise(n) * amplitude;
		n += n;
		amplitude *= 0.4;
	}
	return total;
}

float lightShafts(vec2 st, float t) {
	float angle = -0.2;
	vec2 _st = st;
	st = vec2(st.x * cos(angle) - st.y * sin(angle),
		  st.x * sin(angle) + st.y * cos(angle));
	float tt = t / 16.0;
	float val = fbm(vec2(st.x * 2.0 + 200.0 + tt, st.y / 4.0));
	val += fbm(vec2(st.x * 2.0 + 200.0 - tt, st.y / 4.0));
	val = val / 3.0;
	float mask = pow(clamp(1.0 - abs(_st.y - 0.15), 0.0, 1.0) * 0.49 + 0.5, 2.0);
	mask *= clamp(1.0 - abs(_st.x + 0.2), 0.0, 1.0) * 0.49 + 0.5;
	return pow(val * mask, 2.0);
}

struct BubbleResult {
	vec2 offset;
	float intensity;
};

BubbleResult bubble_with_color(vec2 uv, float scale, float t) {
	BubbleResult result;
	result.intensity = 0.0;
	result.offset = vec2(0.0);

	if (uv.y > 0.2)
	{
		return result;
	}

	float tt = t / 4.0;
	vec2 st = uv * scale;
	vec2 _st = floor(st);
	vec2 bias = vec2(0.0, 4.0 * sin(_st.x * 128.0 + tt));
	float mask = smoothstep(0.1, 0.2, -cos(_st.x * 128.0 + tt));
	st += bias;
	vec2 _st_ = floor(st);
	st = fract(st);
	float size = noise(_st_) * 0.07 + 0.01;
	vec2 pos = vec2(noise(vec2(tt, _st_.y * 64.1)) * 0.8 + 0.1, 0.5);

	float d = length(st.xy - pos);
	if (d < size)
	{
		result.intensity = (1.0 - d / max(size, 0.0001)) * mask;
		result.offset = (st + pos) * vec2(0.1, 0.2) * mask;
	}

	return result;
}

vec3 rgb2hsv(vec3 c)
{
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 safe_res = max(rect_size, vec2(1.0));
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 frag = effect_uv * safe_res;
	vec2 uv = (-safe_res + 2.0 * frag) / safe_res.y;

	float t = (TIME * motion_speed) + time_offset;

	// Keep the original framing; widening this makes some portraits miss the water surface (you only see flakes/bubbles).
	uv.y *= 0.5;
	uv.x *= 0.45;
	float zoom = max(0.05, pattern_scale * card_effect_pattern_scale);
	uv *= zoom;
	uv.y += y_offset;

	// Keep bubble size stable when zoom changes by compensating the bubble grid scale.
	float inv_zoom = 1.0 / zoom;
	BubbleResult bubble1 = bubble_with_color(uv, 12.0 * inv_zoom, t);
	BubbleResult bubble2 = bubble_with_color(uv, 24.0 * inv_zoom, t);
	float bubble_intensity = clamp(bubble1.intensity + bubble2.intensity, 0.0, 1.0);
	vec2 bubble_offset = bubble1.offset + bubble2.offset;

	vec2 water_uv = uv + bubble_offset;

	float eps = 1.0 / max(1.0, max(safe_res.x, safe_res.y));
	vec3 ro = vec3(0.0, 0.0, 2.0);
	vec3 lightPos = vec3(8.0, 3.0, -3.0);
	vec3 rd = normalize(vec3(water_uv, -1.0));

	RaymarchResult result = raymarch(ro, rd, eps, t);
	float diffuse = dot(getNormal(result.hit_pos, eps, t), rd) * 0.5 + 0.5;
	vec3 color = mix(sea_color_dark.rgb, sea_color_light.rgb, diffuse);
	color += pow(diffuse, 12.0);

	vec3 ref = normalize(refract(result.hit_pos - lightPos, getNormal(result.hit_pos, eps, t), 0.05));
	float refraction = clamp(dot(ref, rd), 0.0, 1.0);
	color += refraction_color.rgb * 0.6 * pow(refraction, 1.5);

	vec3 col = mix(color, sea_color_dark.rgb, pow(clamp(result.dist, 0.0, 1.0), 0.2));
	col += light_shaft_color.rgb * lightShafts(water_uv, t);
	col = (col * col + sin(col)) / vec3(1.8, 1.8, 1.9);

	vec2 q = effect_uv;
	col *= 0.7 + 0.3 * pow(16.0 * q.x * q.y * (1.0 - q.x) * (1.0 - q.y), 0.2);

	// Alpha mask (transparent background) from wave distance + bubbles.
	float wave_intensity = clamp(1.0 - result.dist * 0.5, 0.0, 1.0);
	float effect_alpha = max(wave_intensity, bubble_intensity * 0.9);
	effect_alpha = clamp(effect_alpha, 0.0, 0.95);
	// Make mid alphas more visible on card portraits.
	effect_alpha = smoothstep(0.0, 0.95, effect_alpha);
	effect_alpha = clamp(effect_alpha * 1.15, 0.0, 1.0);

	if (bubble_intensity > 0.01)
	{
		vec3 bubble_highlight = vec3(0.9, 0.95, 1.0) * (0.5 + bubble_intensity * 0.8);
		col = mix(col, bubble_highlight, bubble_intensity * 0.7);
	}

	col = clamp(col * brightness, vec3(0.0), vec3(1.0));
	col = mix(col, vec3(1.0), clamp(pastel, 0.0, 1.0));
	col = pow(col, vec3(0.85));

	vec3 hsv = rgb2hsv(col);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);

	col = mix(col, color_tint, clamp(tint_strength, 0.0, 1.0));

		float a = clamp(strength, 0.0, 1.0) * effect_alpha;
		// Subtle dithering to reduce 8-bit banding in low-gradient areas.
		float dither = hash(frag + vec2(17.0, 31.0)) - 0.5;
		vec3 out_col = clamp(col, vec3(0.0), vec3(1.0)) + dither * (a * 0.003);
		out_col = clamp(out_col, vec3(0.0), vec3(1.0));
		COLOR = vec4(out_col, a);
	}";

	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");

	public static void Sync(NCard card, bool enabled, bool fullArt, Dictionary<string, float>? fp = null)
	{
		if (card == null)
		{
			return;
		}

		TextureRect? portrait = null;
		TextureRect? ancientPortrait = null;
		try
		{
			portrait = PortraitRef(card);
			ancientPortrait = AncientPortraitRef(card);
		}
		catch
		{
			return;
		}

		CardEditorArtFinishOverlayNodes.ClearPortraitMaterial(portrait, SharedShader);
		CardEditorArtFinishOverlayNodes.ClearPortraitMaterial(ancientPortrait, SharedShader);
		RemoveOverlay(portrait);
		RemoveOverlay(ancientPortrait);
		SyncPortrait(card, enabled, fullArt, fp);
	}

	private static void SyncPortrait(NCard card, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		ColorRect? overlay = CardEditorArtFinishOverlayNodes.SyncOverlay(card, "PurpleWavesOcean", enabled, fullArt);
		if (overlay == null)
		{
			return;
		}

		ShaderMaterial material;
		if (overlay.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == SharedShader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = SharedShader };
			overlay.Material = material;
		}

		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", 0.60f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", fullArt ? 0.12f : 0.15f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("y_offset", CardEditorTextureLoader.P(fp, "horizonOffset", 0.25f));

		CardEditorArtFinishOverlayNodes.ApplyArtSpace(material, fullArt);
	}

	private static ColorRect GetOrCreateOverlay(TextureRect portrait)
	{
		Control? parent = portrait.GetParent() as Control;
		Node overlayParent = parent ?? portrait;
		string overlayName = OverlayNodeNamePrefix + portrait.Name;

		for (int i = 0; i < overlayParent.GetChildCount(); i++)
		{
			if (overlayParent.GetChild(i) is ColorRect rect && rect.Name == overlayName)
			{
				return rect;
			}
		}

		ColorRect overlay = new ColorRect
		{
			Name = overlayName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color = Colors.White,
		};
		overlayParent.AddChild(overlay);
		SyncOverlayLayout(overlay, portrait);

		if (overlayParent == parent)
		{
			int portraitIndex = parent!.GetChildren().IndexOf(portrait);
			if (portraitIndex >= 0)
			{
				parent.MoveChild(overlay, Math.Min(parent.GetChildCount() - 1, portraitIndex + 1));
			}
		}

		return overlay;
	}

	private static void RemoveOverlay(TextureRect? portrait)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		Node? parent = portrait.GetParent();
		string overlayName = OverlayNodeNamePrefix + portrait.Name;

		if (parent != null && GodotObject.IsInstanceValid(parent))
		{
			for (int i = parent.GetChildCount() - 1; i >= 0; i--)
			{
				if (parent.GetChild(i) is ColorRect rect && rect.Name == overlayName)
				{
					parent.RemoveChild(rect);
					rect.QueueFree();
				}
			}
		}

		for (int i = portrait.GetChildCount() - 1; i >= 0; i--)
		{
			if (portrait.GetChild(i) is ColorRect rect && rect.Name == overlayName)
			{
				portrait.RemoveChild(rect);
				rect.QueueFree();
			}
		}
	}

	private static void SyncOverlayLayout(ColorRect overlay, TextureRect portrait)
	{
		overlay.LayoutMode = portrait.LayoutMode;
		overlay.AnchorLeft = portrait.AnchorLeft;
		overlay.AnchorTop = portrait.AnchorTop;
		overlay.AnchorRight = portrait.AnchorRight;
		overlay.AnchorBottom = portrait.AnchorBottom;
		overlay.OffsetLeft = portrait.OffsetLeft;
		overlay.OffsetTop = portrait.OffsetTop;
		overlay.OffsetRight = portrait.OffsetRight;
		overlay.OffsetBottom = portrait.OffsetBottom;
		overlay.GrowHorizontal = portrait.GrowHorizontal;
		overlay.GrowVertical = portrait.GrowVertical;
		overlay.Scale = portrait.Scale;
		overlay.Rotation = portrait.Rotation;
		overlay.PivotOffset = portrait.PivotOffset;
	}
}

internal static class CardEditorProceduralArtFinishController
{
	private const string OverlayNodeNamePrefix = "CardEditorProceduralArtFinishOverlay_";
	private const string LegacyOverlayNodeName = "CardEditorProceduralArtFinishOverlay";

	private const string WhirlpoolShaderCode = @"shader_type canvas_item;

uniform float strength = 0.58;
uniform float brightness = 1.0;
uniform float pastel = 0.08;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);
uniform float swirl_strength = 4.7;
uniform float line_count = 6.0;
uniform float line_sharpness = 0.10;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 5; i++) {
		v += a * noise(p);
		p *= 2.1;
		a *= 0.5;
	}
	return v;
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	float t = TIME * motion_speed + time_offset;
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 uv = (effect_uv - vec2(0.5)) * max(pattern_scale * card_effect_pattern_scale, 0.05);
	float radius = length(uv);
	float angle = atan(uv.y, uv.x);

	float swirl = swirl_strength * exp(-radius * 3.2);
	float a2 = angle + swirl - t * 0.22;
	vec2 swirled = vec2(cos(a2), sin(a2)) * radius + vec2(0.5);

	vec2 depth_uv = swirled * 3.0 + vec2(t * 0.018, t * 0.012);
	float depth = fbm(depth_uv);

	float line_pos = fract((a2 / (2.0 * 3.14159)) * line_count);
	float line = 1.0 - abs(line_pos - 0.5) * 2.0;
	line = pow(line, 8.0 * (1.0 + line_sharpness * 12.0));

	float fade = smoothstep(0.0, 0.18, radius) * smoothstep(0.82, 0.45, radius);
	line *= fade;

	float wobble = noise(swirled * 3.0 + t * 0.1) - 0.15 * 0.15;
	float line_pos2 = fract((a2 / (2.0 * 3.14159)) * line_count + wobble);
	float line2 = 1.0 - abs(line_pos2 - 0.5) * 2.0;
	line2 = pow(line2, 8.0 * (1.0 + line_sharpness * 12.0));
	line2 *= fade * 0.1;

	float lines_final = max(line, line2);

	vec3 deep_color = vec3(0.05, 0.12, 0.25);
	vec3 mid_color = vec3(0.30, 0.55, 0.75);
	vec3 line_color = vec3(0.65, 0.85, 1.0);
	vec3 col = mix(deep_color, mid_color, depth * 0.9);
	col += line_color * lines_final * 0.9;
	col *= mix(0.05, 1.0, smoothstep(0.0, 0.15, radius));
	col *= 1.0 - smoothstep(0.35, 0.72, radius) * 0.45;
	col = clamp(col * brightness, vec3(0.0), vec3(1.0));
	col = mix(col, vec3(1.0), clamp(pastel, 0.0, 1.0));

	vec3 hsv = rgb2hsv(col);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);
	col = mix(col, color_tint, clamp(tint_strength, 0.0, 1.0));

	float effect_mask = clamp(0.24 + depth * 0.24 + lines_final * 0.76, 0.0, 1.0);
	effect_mask *= 1.0 - smoothstep(0.72, 1.08, radius);
	float blend_amount = clamp(strength * effect_mask, 0.0, 1.0);
	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), blend_amount);
}";

	private const string MiasmaShaderCode = @"shader_type canvas_item;

uniform float strength = 0.55;
uniform float brightness = 1.0;
uniform float pastel = 0.06;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);
uniform float contrast = 1.0;

#define iTime ((TIME * motion_speed + time_offset))

float rand(vec4 p) {
	return fract(sin(p.x * 1234.0 + p.y * 2345.0 + p.z * 3456.0 + p.w * 4567.0) * 5678.0);
}

float smoothnoise(vec4 p) {
	const vec2 e = vec2(0.0, 1.0);
	vec4 i = floor(p);
	vec4 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(mix(rand(i + e.xxxx),
		rand(i + e.yxxx), f.x),
		mix(rand(i + e.xyxx),
		rand(i + e.yyxx), f.x), f.y),
		mix(mix(rand(i + e.xxyx),
		rand(i + e.yxyx), f.x),
		mix(rand(i + e.xyyx),
		rand(i + e.yyyx), f.x), f.y), f.z),
		mix(mix(mix(rand(i + e.xxxy),
		rand(i + e.yxxy), f.x),
		mix(rand(i + e.xyxy),
		rand(i + e.yyxy), f.x), f.y),
		mix(mix(rand(i + e.xxyy),
		rand(i + e.yxyy), f.x),
		mix(rand(i + e.xyyy),
		rand(i + e.yyyy), f.x), f.y), f.z), f.w);
}

float fbm(vec3 x) {
	float v = 0.0;
	float a = 0.5;
	vec3 shift = vec3(1.0);
	for (int i = 0; i < 10; ++i) {
		v += a * smoothnoise(vec4(x, cos(iTime * 0.002) * 200.0));
		x = x * 2.0 + shift;
		a *= 0.5;
	}
	return v;
}

vec3 uvTo3D(vec2 uv) {
	float theta = uv.x * 2.0 * 3.14159265359;
	float phi = uv.y * 3.14159265359;
	float x = sin(phi) * cos(theta);
	float y = sin(phi) * sin(theta);
	float z = cos(phi);
	return vec3(x, y, z);
}

float max3(vec3 v) {
	return max(max(v.x, v.y), v.z);
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 centered_uv = (effect_uv - vec2(0.5)) * max(pattern_scale * card_effect_pattern_scale, 0.05) + vec2(0.5);
	vec3 pos = uvTo3D(centered_uv);
	pos.y += sin(iTime / 5.0);
	pos.x += cos(iTime / 5.0);
	pos.z += sin(iTime / 5.0);

	float fbmm = fbm(pos);
	vec3 q = vec3(fbmm, sin(fbmm), cos(fbmm));
	vec3 r = vec3(fbmm, sin(fbmm), cos(fbmm));
	float v = fbm(pos + 5.0 * r + iTime * 0.005);

	vec3 color_a = vec3(0.20, 0.00, 0.10);
	vec3 color_b = vec3(0.70, 0.30, 0.40);
	vec3 color_c = vec3(1.00, 0.20, 0.40);

	vec3 res_color = mix(color_a, color_b, clamp(r, 0.0, 1.0));
	res_color = mix(res_color, color_c, clamp(q, 0.0, 1.0));

	float poss = v * 2.0 - 1.0;
	res_color = mix(res_color, vec3(1.0), clamp(poss, 0.0, 1.0));
	res_color = mix(res_color, vec3(0.0), clamp(-poss, 0.0, 1.0));
	res_color = res_color / max(max3(res_color), 0.001);
	res_color = (clamp((0.4 * pow(v, 3.0) + pow(v, 2.0) + 0.5 * v), 0.0, 1.0) * 0.9 + 0.1) * res_color;
	res_color = pow(clamp(res_color * brightness, vec3(0.0), vec3(1.0)), vec3(max(0.2, contrast)));
	res_color = mix(res_color, vec3(1.0), clamp(pastel, 0.0, 1.0));

	vec3 hsv = rgb2hsv(res_color);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	res_color = hsv2rgb(hsv);
	res_color = mix(res_color, color_tint, clamp(tint_strength, 0.0, 1.0));

	float cloud_mask = smoothstep(0.12, 0.95, v);
	float blend_amount = clamp(strength * (0.24 + cloud_mask * 0.76), 0.0, 1.0);
	COLOR = vec4(clamp(res_color, vec3(0.0), vec3(1.0)), blend_amount);
}";

	private const string AuroraShaderCode = @"shader_type canvas_item;

uniform float strength = 0.62;
uniform vec3 aurora_color = vec3(0.84, 0.84, 0.90);
uniform vec3 background_color1 = vec3(0.05, 0.10, 0.20);
uniform vec3 background_color2 = vec3(0.10, 0.05, 0.20);
uniform float aurora_intensity = 1.8;
uniform float star_brightness = 0.8;
uniform float star_density = 1.0;
uniform float brightness = 1.0;
uniform float pastel = 0.0;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);
uniform float projection_bend = 0.4;
uniform float horizon = 0.0;
uniform float reflection_strength = 0.65;
uniform float rotation_strength = 0.2;

#define PI 3.14159265358979323846264
#define LOCAL_TIME (TIME * motion_speed + time_offset)

mat2 mm2(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, s), vec2(-s, c));
}

const mat2 m2 = mat2(vec2(0.95534, 0.29552), vec2(-0.29552, 0.95534));

float tri(float x) {
	return clamp(abs(fract(x) - 0.5), 0.01, 0.49);
}

vec2 tri2(vec2 p) {
	return vec2(tri(p.x) + tri(p.y), tri(p.y + tri(p.x)));
}

float triNoise2d(vec2 p, float spd) {
	float z = 1.8;
	float z2 = 2.5;
	float rz = 0.0;
	p = mm2(p.x * 0.06) * p;
	vec2 bp = p;
	for (int i = 0; i < 5; i++) {
		vec2 dg = tri2(bp * 1.85) * 0.75;
		dg = mm2(LOCAL_TIME * spd) * dg;
		p -= dg / z2;

		bp *= 1.3;
		z2 *= 0.45;
		z *= 0.42;
		p *= 1.21 + (rz - 1.0) * 0.02;

		rz += tri(p.x + tri(p.y)) * z;
		p = (m2 * -1.0) * p;
	}
	return clamp(1.0 / pow(max(rz * 29.0, 0.0001), 1.3), 0.0, 0.55);
}

float hash21(vec2 n) {
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

vec3 hash33(vec3 p) {
	p = fract(p * vec3(0.1031, 0.11369, 0.13787));
	p += dot(p, p.yxz + 19.19);
	return fract(vec3((p.x + p.y) * p.z, (p.x + p.z) * p.y, (p.y + p.z) * p.x));
}

vec4 aurora(vec3 ro, vec3 rd, vec2 frag_coord) {
	vec4 col = vec4(0.0);
	vec4 avg_col = vec4(0.0);

	for (int j = 0; j < 50; j++) {
		float i = float(j);
		float of = 0.006 * hash21(frag_coord + vec2(LOCAL_TIME * 0.01)) * smoothstep(0.0, 15.0, i);
		float pt = ((0.8 + pow(i, 1.4) * 0.002) - ro.y) / (rd.y * 2.0 + 0.4);
		pt -= of;
		vec3 bpos = ro + pt * rd;
		vec2 p = bpos.zx;
		float rzt = triNoise2d(p, 0.06);
		vec4 col2 = vec4(0.0, 0.0, 0.0, rzt);

		vec3 color_variation = sin(1.0 - vec3(2.15, -0.5, 1.2) + i * 0.043) * 0.5 + 0.5;
		col2.rgb = aurora_color * color_variation * rzt;

		avg_col = mix(avg_col, col2, 0.5);
		col += avg_col * exp2(-i * 0.065 - 2.5) * smoothstep(0.0, 5.0, i);
	}
	col *= clamp(rd.y * 15.0 + 0.4, 0.0, 1.0);
	return col * aurora_intensity;
}

vec3 stars(vec3 p) {
	vec3 c = vec3(0.0);
	float density = max(star_density, 0.0);
	for (int j = 0; j < 4; j++) {
		float i = float(j);
		vec3 q = fract(p * (18.0 + density * 22.0)) - 0.5;
		vec3 id = floor(p * (18.0 + density * 22.0));
		vec2 rn = hash33(id + vec3(i)).xy;
		float c2 = 1.0 - smoothstep(0.0, 0.6, length(q));
		c2 *= step(rn.x, (0.0005 + i * i * 0.001) * max(density, 0.001));
		c += c2 * (mix(vec3(1.0, 0.49, 0.1), vec3(0.75, 0.9, 1.0), rn.y) * 0.1 + 0.9);
		p *= 1.3;
	}
	return c * c * star_brightness;
}

vec3 bg(vec3 rd) {
	float sd = dot(normalize(vec3(-0.5, -0.6, 0.9)), rd) * 0.5 + 0.5;
	sd = pow(sd, 5.0);
	vec3 col = mix(background_color1, background_color2, sd);
	return col * 0.63;
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 local_uv = (effect_uv - vec2(0.5)) * max(pattern_scale * card_effect_pattern_scale, 0.05) + vec2(0.5);
	vec2 sphere_uv = clamp(local_uv, vec2(0.0), vec2(1.0));
	float theta = clamp(sphere_uv.y + horizon * 0.15, 0.0, 1.0) * PI;
	float phi = (sphere_uv.x - 0.5) * 2.0 * PI;
	vec3 sphere_rd = vec3(sin(theta) * sin(phi), sin(theta) * cos(phi), cos(theta));
	vec2 flat_uv = sphere_uv - vec2(0.5);
	vec3 flat_rd = normalize(vec3(flat_uv.x * 1.65, -flat_uv.y * 1.65 + horizon * 0.35, 1.15));
	vec3 rd = normalize(mix(flat_rd, sphere_rd, clamp(projection_bend, 0.0, 1.0)));

	float time_rot = LOCAL_TIME * 0.05;
	rd.yz = mm2(0.4) * rd.yz;
	rd.xz = mm2(sin(time_rot) * rotation_strength) * rd.xz;

	vec3 ro = vec3(0.0, 0.0, -6.7);
	vec2 pseudo_frag = sphere_uv * (1.0 / max(TEXTURE_PIXEL_SIZE, vec2(0.0001)));
	float fade = smoothstep(0.0, 0.01, abs(rd.y)) * 0.1 + 0.9;
	vec3 col = bg(rd) * fade;
	float aurora_mask = 0.0;

	if (rd.y > 0.0) {
		vec4 aur_raw = aurora(ro, rd, pseudo_frag);
		vec4 aur = smoothstep(vec4(0.0), vec4(1.5), aur_raw) * fade;
		col += stars(rd);
		col = col * (1.0 - aur.a) + aur.rgb;
		aurora_mask = aur.a;
	} else {
		vec3 rrd = rd;
		rrd.y = abs(rrd.y);
		col = bg(rrd) * fade * 0.6;
		vec4 aur_raw = aurora(ro, rrd, pseudo_frag);
		vec4 aur = smoothstep(vec4(0.0), vec4(2.5), aur_raw);
		col += stars(rrd) * 0.1;
		col = col * (1.0 - aur.a * reflection_strength) + aur.rgb * reflection_strength;

		vec3 pos = ro + ((0.5 - ro.y) / max(rrd.y, 0.001)) * rrd;
		float nz2 = triNoise2d(pos.xz * vec2(0.5, 0.7), 0.0);
		col += mix(vec3(0.2, 0.25, 0.5) * 0.08, vec3(0.3, 0.3, 0.5) * 0.7, nz2 * 0.4) * reflection_strength;
		aurora_mask = aur.a * reflection_strength;
	}

	col = clamp(col * brightness, vec3(0.0), vec3(1.0));
	col = mix(col, vec3(1.0), clamp(pastel, 0.0, 1.0));
	vec3 hsv = rgb2hsv(col);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);
	col = mix(col, color_tint, clamp(tint_strength, 0.0, 1.0));

	float edge = smoothstep(0.0, 0.08, sphere_uv.x) * smoothstep(1.0, 0.92, sphere_uv.x)
		* smoothstep(0.0, 0.08, sphere_uv.y) * smoothstep(1.0, 0.92, sphere_uv.y);
	float blend_amount = clamp(strength * edge * (0.28 + aurora_mask * 0.72), 0.0, 1.0);
	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), blend_amount);
}";

	private const string ConstellationShaderCode = @"shader_type canvas_item;

uniform float strength = 0.66;
uniform float line_strength = 1.0;
uniform float line_width = 0.012;
uniform float line_softness = 0.004;
uniform float connection_range = 1.3;
uniform float target_length = 0.75;
uniform float target_band = 0.04;
uniform float long_line_strength = 0.5;
uniform float sparkle_strength = 1.0;
uniform float sparkle_power = 1.5;
uniform float sparkle_pulse = 0.4;
uniform float node_jitter = 0.4;
uniform float point_speed = 1.0;
uniform float layer_count = 5.0;
uniform float grid_scale = 1.5;
uniform float layer_min_scale = 0.1;
uniform float layer_max_scale = 15.0;
uniform float layer_speed = 1.0;
uniform float background_glow = 0.18;
uniform float color_speed = 2.0;
uniform float color_spread = 1.0;
uniform float brightness = 1.0;
uniform float pastel = 0.0;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 network_color = vec3(1.0, 1.0, 1.0);
uniform float network_tint_strength = 0.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);

#define LOCAL_TIME (TIME * motion_speed + time_offset)

float distLine(vec2 p, vec2 a, vec2 b) {
	vec2 ap = p - a;
	vec2 ab = b - a;
	float denom = max(dot(ab, ab), 0.0001);
	float a_dot_b = clamp(dot(ap, ab) / denom, 0.0, 1.0);
	return length(ap - ab * a_dot_b);
}

float drawLine(vec2 uv, vec2 a, vec2 b) {
	float d = distLine(uv, a, b);
	float line = 1.0 - smoothstep(line_width, line_width + max(line_softness, 0.0001), d);
	float dist = length(b - a);
	float long_fade = (1.0 - smoothstep(connection_range * 0.62, connection_range, dist)) * long_line_strength;
	float target_fade = 1.0 - smoothstep(target_band * 0.75, target_band, abs(dist - target_length));
	return line * clamp(long_fade + target_fade, 0.0, 2.0) * line_strength;
}

float n21(vec2 i) {
	i += fract(i * vec2(223.64, 823.12));
	i += dot(i, i + 23.14);
	return fract(i.x * i.y);
}

vec2 n22(vec2 i) {
	float x = n21(i);
	return vec2(x, n21(i + x));
}

vec2 getPoint(vec2 id, vec2 offset) {
	return offset + sin(n22(id + offset) * LOCAL_TIME * point_speed) * node_jitter;
}

float layer(vec2 uv) {
	float m = 0.0;
	float t = LOCAL_TIME * 2.0;
	vec2 gv = fract(uv) - 0.5;
	vec2 id = floor(uv) - 0.5;

	vec2 p[9];
	int idx = 0;
	for (int yy = -1; yy <= 1; yy++) {
		for (int xx = -1; xx <= 1; xx++) {
			p[idx] = getPoint(id, vec2(float(xx), float(yy)));
			idx++;
		}
	}

	for (int i = 0; i < 9; i++) {
		m += drawLine(gv, p[4], p[i]);
		float sparkle = sparkle_strength / pow(max(length(gv - p[i]), 0.001), max(sparkle_power, 0.2)) * 0.005;
		m += sparkle * (sin(t + fract(p[i].x) * 12.23) * sparkle_pulse + (1.0 - sparkle_pulse));
	}

	m += drawLine(gv, p[1], p[3]);
	m += drawLine(gv, p[1], p[5]);
	m += drawLine(gv, p[7], p[3]);
	m += drawLine(gv, p[7], p[5]);
	return m;
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	float aspect = card_effect_rect_size.x / max(card_effect_rect_size.y, 1.0);
	vec2 uv = (effect_uv - vec2(0.5)) * vec2(aspect, 1.0) * max(pattern_scale * card_effect_pattern_scale, 0.05);

	vec3 cycle = sin(LOCAL_TIME * color_speed * vec3(0.234, 0.324, 0.768)) * 0.4 + 0.6;
	cycle.x += (uv.x + 0.5) * color_spread;
	cycle = mix(cycle, network_color, clamp(network_tint_strength, 0.0, 1.0));

	vec3 col = vec3(0.0);
	col += pow(max(-uv.y + 0.5, 0.0), 5.0) * background_glow * cycle;

	float m = 0.0;
	for (int li = 0; li < 5; li++) {
		if (float(li) >= layer_count) {
			continue;
		}
		float i = float(li) / 4.0;
		float z = fract(i + LOCAL_TIME * 0.05 * layer_speed);
		float size = mix(layer_max_scale, layer_min_scale, z) * grid_scale;
		float fade = smoothstep(0.0, 1.0, z) * smoothstep(1.0, 0.9, z);
		m += layer(size * uv + i * 10.0) * fade;
	}

	col += m * cycle;
	col = clamp(col * brightness, vec3(0.0), vec3(1.0));
	col = mix(col, vec3(1.0), clamp(pastel, 0.0, 1.0));
	vec3 hsv = rgb2hsv(col);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);
	col = mix(col, color_tint, clamp(tint_strength, 0.0, 1.0));

	float effect_mask = clamp(background_glow * 0.18 + m, 0.0, 1.0);
	float blend_amount = clamp(strength * effect_mask, 0.0, 1.0);
	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), blend_amount);
}";

	private const string RippleShaderCode = @"shader_type canvas_item;

uniform float strength = 0.65;
uniform float ripple_strength = 1.0;
uniform float wave_frequency = 2.0;
uniform float wave_spread = 1.5;
uniform float source_count = 5.0;
uniform float highlight = 1.2;
uniform float noise_strength = 0.005;
uniform float brightness = 1.0;
uniform float color_saturation = 1.0;
uniform float hue_shift = 0.0;
uniform float time_offset = 0.0;
uniform float motion_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);
uniform float debug_mode = 0.0;

#define PI 3.14159265359
#define LOCAL_TIME (TIME * motion_speed + time_offset)

float vmin(vec2 v) { return min(v.x, v.y); }
float range(float vminv, float vmaxv, float value) { return (value - vminv) / (vmaxv - vminv); }
float rangec(float a, float b, float t) { return clamp(range(a, b, t), 0.0, 1.0); }
vec3 pal(float t, vec3 a, vec3 b, vec3 c, vec3 d) { return a + b * cos(6.28318 * (c * t + d)); }
vec3 spectrum(float n) { return pal(n, vec3(0.5), vec3(0.5), vec3(1.0), vec3(0.0, 0.33, 0.67)); }

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void drawHit(inout vec4 col, vec2 p, vec2 hitPos, float hitDist) {
	float d = length(p - hitPos);
	if (debug_mode > 0.5) {
		col = mix(col, vec4(0.0, 1.0, 1.0, 0.0), step(d, 0.1));
		return;
	}
	float freq = max(wave_frequency, 0.1);
	float wavefront = d - hitDist * max(wave_spread, 0.1);
	vec3 spec = 1.0 - spectrum(-wavefront * freq + hitDist * freq);
	float ripple = sin((wavefront * freq) * PI * 2.0 - PI / 2.0);
	float blend = 1.0 - smoothstep(0.0, 3.0, hitDist);
	blend *= 1.0 - smoothstep(-0.5, 0.2, wavefront);
	blend *= rangec(-4.0, 0.0, wavefront);
	blend *= ripple_strength;
	col.rgb *= mix(vec3(1.0), spec, clamp(pow(clamp(blend, 0.0, 1.0), 4.0) * highlight, 0.0, 1.0));
	float height = ripple * blend;
	col.a -= height * 1.9 / freq;
}

void drawReflectedHit(inout vec4 col, vec2 p, vec2 hitPos, float hitDist) {
	col.a += length(p) * 0.0001;
	drawHit(col, p, hitPos, hitDist);
}

void flip(inout vec2 pos) {
	vec2 flip_val = mod(floor(pos), 2.0);
	pos = abs(flip_val - mod(pos, 1.0));
}

vec2 calcHitPos(vec2 move, vec2 dir, vec2 size) {
	vec2 hitPos = mod(move, 1.0);
	vec2 xCross = hitPos - hitPos.x / (size / size.x) * (dir / dir.x);
	vec2 yCross = hitPos - hitPos.y / (size / size.y) * (dir / dir.y);
	hitPos = max(xCross, yCross);
	hitPos += floor(move);
	return hitPos;
}

void fragment() {
	vec2 tex_size = max(card_effect_rect_size, vec2(1.0));
	float aspect = card_effect_rect_size.x / max(card_effect_rect_size.y, 1.0);
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 p = (effect_uv * 2.0 - 1.0) * vec2(aspect, 1.0) * max(pattern_scale * card_effect_pattern_scale, 0.05);
	if (debug_mode > 0.5) {
		p *= 2.0;
	}

	vec2 screen_aspect = vec2(aspect, 1.0) * 2.0;
	vec2 dir = normalize(vec2(9.0, 16.0) * screen_aspect);
	vec2 move = dir * LOCAL_TIME / 1.5;
	vec2 size = max(screen_aspect, vec2(0.1));
	move = move / size + 0.5;

	vec2 lastHitPos = calcHitPos(move, dir, size);
	vec4 col = vec4(1.0, 1.0, 1.0, 0.0);
	vec4 colFx = vec4(1.0, 1.0, 1.0, 0.0);
	vec4 colFy = vec4(1.0, 1.0, 1.0, 0.0);
	vec2 e = vec2(0.8, 0.0) / max(tex_size.y, 1.0);
	if (debug_mode > 0.5) {
		col.rgb = vec3(0.0);
	}

	for (int i = 0; i < 8; i++) {
		if (float(i) < source_count) {
			vec2 hitPos = lastHitPos;
			if (i > 0) {
				hitPos = calcHitPos(hitPos - 0.00001 / size, dir, size);
			}
			lastHitPos = hitPos;
			float hitDist = distance(hitPos, move);
			flip(hitPos);
			hitPos = (hitPos - 0.5) * size;
			drawReflectedHit(col, p, hitPos, hitDist);
			drawReflectedHit(colFx, p + e, hitPos, hitDist);
			drawReflectedHit(colFy, p + e.yx, hitPos, hitDist);
		}
	}

	float bf = 0.1;
	float fx = (col.a - colFx.a) * 2.0;
	float fy = (col.a - colFy.a) * 2.0;
	vec3 nor = normalize(vec3(fx, fy, e.x / bf));
	float ff = length(vec2(fx, fy));
	float ee = rangec(0.0, 10.0 / max(tex_size.y, 1.0), ff);
	nor = normalize(vec3(vec2(fx, fy) * ee, max(ff, 0.0001)));

	col.rgb = clamp(1.0 - col.rgb, vec3(0.0), vec3(1.0));
	col.rgb /= 3.0;

	if (debug_mode <= 0.5) {
		vec3 lig = normalize(vec3(1.0, 2.0, 2.0));
		vec3 rd = normalize(vec3(p, -10.0));
		vec3 hal = normalize(lig - rd);
		float dif = clamp(dot(lig, nor), 0.0, 1.0);
		float spe = pow(clamp(dot(nor, hal), 0.0, 1.0), 16.0) * dif
			* (0.04 + 0.96 * pow(clamp(1.0 + dot(hal, rd), 0.0, 1.0), 5.0));
		vec3 lin = vec3(0.0);
		lin += 5.0 * dif;
		lin += 0.2;
		col.rgb = col.rgb * lin;
		col.rgb += 5.0 * spe;
	}

	if (debug_mode > 0.5) {
		float b = vmin(abs(fract(p / screen_aspect) - 0.5) * 2.0);
		b /= max(fwidth(b) * 2.0, 0.0001);
		b = 1.0 - clamp(b, 0.0, 1.0);
		col.rgb = mix(col.rgb, vec3(0.0), b);
	}

	vec2 px = floor(effect_uv * card_effect_rect_size);
	float n = hash21(px + vec2(floor(LOCAL_TIME * 24.0)));
	col.rgb += (vec3(n) * 2.0 - 1.0) * noise_strength;
	col.rgb = pow(max(col.rgb, vec3(0.0)), vec3(1.0 / 1.5));
	col.rgb *= brightness;

	vec3 hsv = rgb2hsv(max(col.rgb, vec3(0.0)));
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col.rgb = hsv2rgb(hsv);

	float effect_mask = clamp(0.20 + ff * 16.0 + abs(col.a) * 0.18, 0.0, 1.0);
	float overlay_alpha = clamp(strength * effect_mask, 0.0, 1.0);
	COLOR = vec4(clamp(col.rgb, vec3(0.0), vec3(1.0)), overlay_alpha);
}";

	private const string LightningShaderCode = @"shader_type canvas_item;

uniform float strength = 0.68;
uniform vec3 effect_color = vec3(0.2, 0.3, 0.8);
uniform float octave_count = 10.0;
uniform float amp_start = 0.5;
uniform float amp_coeff = 0.5;
uniform float freq_coeff = 2.0;
uniform float motion_speed = 0.5;
uniform float time_offset = 0.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);
uniform float bolt_width = 0.045;
uniform float bolt_count = 1.0;
uniform float glow = 1.4;
uniform float brightness = 1.0;
uniform float flicker = 0.55;
uniform float color_saturation = 1.0;
uniform float hue_shift = 0.0;

#define LOCAL_TIME (TIME * motion_speed + time_offset)

float hash12(vec2 x) {
	return fract(cos(mod(dot(x, vec2(13.9898, 8.141)), 3.14)) * 43758.5453);
}

vec2 hash22(vec2 uv) {
	uv = vec2(dot(uv, vec2(127.1, 311.7)),
		dot(uv, vec2(269.5, 183.3)));
	return 2.0 * fract(sin(uv) * 43758.5453123) - 1.0;
}

float noise(vec2 uv) {
	vec2 iuv = floor(uv);
	vec2 fuv = fract(uv);
	vec2 blur = smoothstep(0.0, 1.0, fuv);
	return mix(mix(dot(hash22(iuv + vec2(0.0, 0.0)), fuv - vec2(0.0, 0.0)),
			dot(hash22(iuv + vec2(1.0, 0.0)), fuv - vec2(1.0, 0.0)), blur.x),
		mix(dot(hash22(iuv + vec2(0.0, 1.0)), fuv - vec2(0.0, 1.0)),
			dot(hash22(iuv + vec2(1.0, 1.0)), fuv - vec2(1.0, 1.0)), blur.x), blur.y) + 0.5;
}

float fbm(vec2 uv) {
	float value = 0.0;
	float amplitude = amp_start;
	for (int i = 0; i < 20; i++) {
		if (float(i) < octave_count) {
			value += amplitude * noise(uv);
			uv *= max(freq_coeff, 1.0);
			amplitude *= amp_coeff;
		}
	}
	return value;
}

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	float aspect = card_effect_rect_size.x / max(card_effect_rect_size.y, 1.0);
	vec2 effect_uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 uv = (2.0 * effect_uv - 1.0) * vec2(aspect, 1.0) * max(pattern_scale * card_effect_pattern_scale, 0.05);

	vec2 uv_base = uv;
	vec3 col = vec3(0.0);
	float mask = 0.0;
	float count = clamp(floor(bolt_count + 0.5), 1.0, 8.0);
	float spacing = mix(0.0, 0.62, clamp((count - 1.0) / 7.0, 0.0, 1.0));
	float bolt_scale = mix(1.0, 0.58, clamp((count - 1.0) / 7.0, 0.0, 1.0));

	for (int j = 0; j < 8; j++) {
		float i = float(j);
		if (i >= count) {
			continue;
		}

		float phase = i * 17.31;
		float lane = i - (count - 1.0) * 0.5;
		vec2 bolt_uv = uv_base;
		bolt_uv.x -= lane * spacing;

		float bend = 2.0 * fbm(bolt_uv + vec2(LOCAL_TIME + phase, LOCAL_TIME * 0.37 - phase * 0.21)) - 1.0;
		bolt_uv.x += bend;
		float dist = abs(bolt_uv.x);
		float width = max(bolt_width, 0.001);
		float core = 1.0 - smoothstep(0.0, width, dist);
		float aura = exp(-dist * 10.0 / max(glow, 0.01));
		float flicker_sample = mix(1.0, 0.25 + hash12(vec2(floor((LOCAL_TIME + phase) * 28.0), floor((LOCAL_TIME + phase) * 7.0))) * 0.95, clamp(flicker, 0.0, 1.0));

		col += effect_color * (core * 2.2 + aura * glow * 0.45) * brightness * flicker_sample * bolt_scale;
		mask = max(mask, clamp(core + aura * 0.65, 0.0, 1.0));
	}

	vec3 hsv = rgb2hsv(max(col, vec3(0.0)));
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);

	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), clamp(strength * mask, 0.0, 1.0));
}";

	private const string FlameShaderCode = @"shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform float strength : hint_range(0.0, 1.0, 0.01) = 0.78;
uniform vec4 cold_color : source_color = vec4(0.055, 0.006, 0.0, 1.0);
uniform vec4 deep_red_color : source_color = vec4(0.55, 0.025, 0.0, 1.0);
uniform vec4 orange_color : source_color = vec4(1.0, 0.25, 0.015, 1.0);
uniform vec4 yellow_color : source_color = vec4(1.0, 0.68, 0.08, 1.0);
uniform vec4 white_core_color : source_color = vec4(1.0, 0.92, 0.55, 1.0);
uniform vec4 smoke_color : source_color = vec4(0.16, 0.14, 0.12, 1.0);

uniform float flame_height : hint_range(0.5, 1.7, 0.01) = 1.08;
uniform float flame_width : hint_range(0.15, 0.8, 0.01) = 0.44;
uniform float flame_top_width : hint_range(0.01, 0.8, 0.01) = 0.035;
uniform float flame_intensity : hint_range(0.1, 2.5, 0.01) = 0.82;
uniform float core_heat : hint_range(0.0, 1.3, 0.01) = 0.38;
uniform float turbulence : hint_range(0.0, 1.5, 0.01) = 0.48;
uniform float noise_scale : hint_range(1.0, 14.0, 0.01) = 7.5;
uniform float rise_speed : hint_range(0.1, 4.0, 0.01) = 1.45;
uniform float time_offset : hint_range(0.0, 10.0, 0.01) = 0.0;
uniform float flicker_strength : hint_range(0.0, 1.0, 0.01) = 0.32;
uniform float edge_softness : hint_range(0.02, 0.5, 0.01) = 0.24;
uniform float glow_strength : hint_range(0.0, 2.0, 0.01) = 0.58;
uniform float ember_density : hint_range(0.0, 1.0, 0.01) = 0.14;
uniform float smoke_amount : hint_range(0.0, 1.0, 0.01) = 0.08;
uniform float wind : hint_range(-1.0, 1.0, 0.01) = 0.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);

#define LOCAL_TIME (TIME + time_offset)

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

vec2 grad2(vec2 p) {
	float a = hash21(p) * 6.28318530718;
	return vec2(cos(a), sin(a));
}

float noise21(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);

	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

	float a = dot(grad2(i + vec2(0.0, 0.0)), f - vec2(0.0, 0.0));
	float b = dot(grad2(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
	float c = dot(grad2(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
	float d = dot(grad2(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));

	return 0.5 + 0.5 * mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;

	mat2 rot = mat2(
		vec2(0.80, -0.60),
		vec2(0.60, 0.80)
	);

	for (int i = 0; i < 6; i++) {
		v += a * noise21(p);
		p = rot * p * 2.05 + vec2(17.13, 9.27);
		a *= 0.5;
	}

	return v;
}

float ridge(float x) {
	return 1.0 - abs(x * 2.0 - 1.0);
}

vec3 fire_ramp(float t) {
	t = clamp(t, 0.0, 1.0);

	vec3 c = mix(cold_color.rgb, deep_red_color.rgb, smoothstep(0.00, 0.20, t));
	c = mix(c, orange_color.rgb, smoothstep(0.18, 0.50, t));
	c = mix(c, yellow_color.rgb, smoothstep(0.44, 0.78, t));
	c = mix(c, white_core_color.rgb, smoothstep(0.78, 1.00, t));

	return c;
}

void fragment() {
	vec2 uv = (card_effect_uv_origin + UV * card_effect_uv_scale);

	// p.x = -1 left to 1 right
	// p.y = 0 bottom to 1 top
	vec2 p = vec2((uv.x - 0.5) * 2.0, 1.0 - uv.y);

	float time = LOCAL_TIME * rise_speed;

	float flicker = sin(LOCAL_TIME * 7.3) * 0.05;
	flicker += sin(LOCAL_TIME * 13.9 + 1.7) * 0.035;
	flicker += sin(LOCAL_TIME * 21.1 + 4.4) * 0.018;
	flicker *= flicker_strength;

	vec2 base_p = vec2(
		p.x * noise_scale + wind * p.y * 2.0,
		p.y * noise_scale - time * 1.55
	);

	float warp_a = fbm(base_p * 0.7 + vec2(0.0, -time * 0.25));
	float warp_b = fbm(base_p * 0.9 + vec2(9.2, time * 0.18));

	vec2 warped_p = base_p;
	warped_p.x += (warp_a - 0.5) * turbulence * 2.2;
	warped_p.y += (warp_b - 0.5) * turbulence * 1.2;

	float n1 = fbm(warped_p);
	float n2 = fbm(warped_p * 1.75 + vec2(6.4, -time * 0.65));
	float n3 = fbm(warped_p * 3.4 + vec2(-3.8, time * 0.35));

	float height_noise = (n2 - 0.5) * turbulence * 0.38 + flicker;

	float bottom_fade = smoothstep(0.0, 0.035, p.y);
	float top_fade = 1.0 - smoothstep(
		flame_height + height_noise,
		flame_height + 0.28 + height_noise,
		p.y
	);

	float vertical_mask = bottom_fade * top_fade;

	float taper = pow(clamp(p.y / max(flame_height, 0.001), 0.0, 1.0), 1.42);
	float width = mix(flame_width, flame_top_width, taper);

	width *= 1.0 + (n1 - 0.5) * turbulence * 0.34 + flicker * 0.7;

	float sway = (n1 - 0.5) * turbulence * (0.35 + p.y * 0.9);
	sway += wind * p.y * 0.45;

	float x = p.x + sway;

	float soft_body = 1.0 - smoothstep(width, width + edge_softness, abs(x));

	float licking = ridge(n2);
	licking = smoothstep(0.22, 0.95, licking + n3 * 0.25);

	float broken_edge = smoothstep(0.12, 0.88, n1 + n3 * 0.18);
	float body = soft_body * mix(0.72, 1.18, broken_edge) * vertical_mask;

	float center = 1.0 - smoothstep(0.0, max(width * 0.72, 0.001), abs(x));
	float core = pow(clamp(center, 0.0, 1.0), 1.85);
	core *= smoothstep(0.02, 0.22, p.y);
	core *= 1.0 - smoothstep(0.86, 1.28, p.y);

	float alpha = body * flame_intensity;
	alpha *= mix(0.75, 1.15, licking);
	alpha = clamp(alpha, 0.0, 1.0);

	float heat = 0.0;
	heat += core * 0.58;
	heat += (1.0 - p.y) * 0.30;
	heat += n2 * 0.23;
	heat += n3 * 0.12;
	heat += core_heat * 0.24;
	heat = clamp(heat, 0.0, 1.0);

	vec3 fire_col = fire_ramp(heat);

	float glow_width = width + 0.28 + glow_strength * 0.16;
	float glow = 1.0 - smoothstep(width, glow_width, abs(x));
	glow *= vertical_mask;
	glow *= glow_strength;
	glow *= 1.0 - smoothstep(flame_height + 0.15, flame_height + 0.55, p.y);

	vec3 glow_col = mix(deep_red_color.rgb, orange_color.rgb, 0.55) * glow * 0.52;

	float smoke_noise = fbm(vec2(
		p.x * 2.2 + wind * 0.8,
		p.y * 5.2 - time * 0.42
	));

	float smoke_mask = smoothstep(0.45, 1.0, p.y);
	smoke_mask *= 1.0 - smoothstep(flame_height + 0.18, flame_height + 0.75, p.y);

	float smoke_alpha = smoke_amount;
	smoke_alpha *= smoke_mask;
	smoke_alpha *= smoothstep(0.42, 0.90, smoke_noise);
	smoke_alpha *= 1.0 - soft_body * 0.65;

	vec2 ember_grid = vec2(
		uv.x * 42.0 + wind * LOCAL_TIME * 2.0,
		uv.y * 64.0 - LOCAL_TIME * rise_speed * 5.0
	);

	vec2 ember_cell = floor(ember_grid);
	vec2 ember_local = fract(ember_grid) - 0.5;

	float ember_seed = hash21(ember_cell);
	float ember = smoothstep(0.998 - ember_density * 0.050, 1.0, ember_seed);
	ember *= 1.0 - smoothstep(0.015, 0.075, length(ember_local));
	ember *= smoothstep(0.10, 0.95, p.y);
	ember *= 1.0 - soft_body * 0.55;

	vec3 col = fire_col * alpha;
	col += glow_col;
	col = mix(col, smoke_color.rgb, smoke_alpha * 0.62);
	col += yellow_color.rgb * ember * 1.35;

	float out_alpha = alpha;
	out_alpha += glow * 0.30;
	out_alpha += smoke_alpha;
	out_alpha += ember;
	out_alpha = clamp(out_alpha, 0.0, 1.0);

	float overlay_alpha = clamp(out_alpha * strength, 0.0, 1.0);
	vec3 overlay_col = clamp(col, vec3(0.0), vec3(1.0));
	COLOR = vec4(overlay_col, overlay_alpha);
}";

	private static readonly Shader WhirlpoolShader = new() { Code = WhirlpoolShaderCode };
	private static readonly Shader MiasmaShader = new() { Code = MiasmaShaderCode };
	private const string AuroraShaderCodeFlat = @"shader_type canvas_item;

uniform vec3 background_color : source_color = vec3(0.25, 0.0, 0.2);
uniform float GWM = 2.05;
uniform float TM = 0.25;
uniform float strength : hint_range(0.0, 1.0) = 0.5;
uniform float transparency : hint_range(0.0, 1.0) = 0.0;
uniform float background_transparency : hint_range(0.0, 1.0) = 0.0;
uniform float wave_count : hint_range(1.0, 8.0, 1.0) = 5.0;
uniform float wave_distortion : hint_range(0.0, 3.0) = 1.0;
uniform float brightness : hint_range(0.1, 4.0) = 1.0;
uniform float motion_speed : hint_range(0.0, 5.0) = 1.0;
uniform float time_offset : hint_range(0.0, 10.0) = 0.0;
uniform float pattern_scale : hint_range(0.1, 4.0) = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_screen_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_screen_size = vec2(300.0, 422.0);
uniform vec2 card_effect_rect_size = vec2(300.0, 422.0);

#define LOCAL_TIME ((TIME * motion_speed) + time_offset)

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash(i + vec2(0.0, 0.0)), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
		u.y
	);
}

float getAmp(float frequency) {
	float t = LOCAL_TIME;
	float low = sin(frequency * 0.045 + t * 1.7) * 0.5 + 0.5;
	float mid = sin(frequency * 0.013 - t * 0.9) * 0.5 + 0.5;
	float grain = noise(vec2(frequency * 0.018, t * 0.45));
	return clamp(low * 0.45 + mid * 0.25 + grain * 0.30, 0.0, 1.0);
}

float getWeight(float f) {
	return (getAmp(f - 2.0) + getAmp(f - 1.0) +
			getAmp(f + 2.0) + getAmp(f + 1.0) +
			getAmp(f)) / 5.0;
}

void fragment() {
	float aspect = card_effect_rect_size.x / max(card_effect_rect_size.y, 1.0);
	vec2 uvTrue = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 uv = (2.0 * uvTrue - 1.0) * max(pattern_scale * card_effect_pattern_scale, 0.05);
	uv.x *= aspect;

	vec3 color = vec3(0.0);
	float strongest_wave = 0.0;

	for (int j = 0; j < 8; j++) {
		float i = float(j);
		if (i >= wave_count) {
			continue;
		}

		uv.y += 0.2 * wave_distortion * sin(uv.x + i / 7.0 - LOCAL_TIME * 0.6);
		float column_amp = getAmp(uvTrue.x * 512.0 + i * 23.0);
		float y = uv.y + getWeight(pow(i, 2.0) * 20.0) * (column_amp - 0.5) * wave_distortion;
		float li = 0.4 + pow(1.6 * abs(mod(uvTrue.x + i / 1.1 + LOCAL_TIME, 2.0) - 1.0), 2.0);
		float gw = abs(li / max(150.0 * abs(y), 0.002));
		gw = min(gw, 2.0);

		float ts = gw * (GWM + sin(LOCAL_TIME * TM));
		color += vec3(ts);
		strongest_wave = max(strongest_wave, ts);
	}

	float wave_opacity = 1.0 - clamp(transparency, 0.0, 1.0);
	float background_opacity = 1.0 - clamp(background_transparency, 0.0, 1.0);
	vec3 generated = background_color * background_opacity + color * brightness * wave_opacity;
	generated = clamp(generated, vec3(0.0), vec3(1.0));

	float wave_mask = clamp(strongest_wave * 0.65 * wave_opacity, 0.0, 1.0);
	float layer_alpha = clamp(max(background_opacity * 0.5, wave_mask) * strength, 0.0, 1.0);

	COLOR = vec4(clamp(generated, vec3(0.0), vec3(1.0)), layer_alpha);
}";

	private static readonly Shader AuroraShader = new() { Code = AuroraShaderCodeFlat };
	private static readonly Shader ConstellationShader = new() { Code = ConstellationShaderCode };
	private static readonly Shader RippleShader = new() { Code = RippleShaderCode };
	private static readonly Shader LightningShader = new() { Code = LightningShaderCode };
	private static readonly Shader FlameShader = new() { Code = FlameShaderCode };
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");
	private static readonly AccessTools.FieldRef<NCard, CanvasGroup> PortraitCanvasGroupRef =
		AccessTools.FieldRefAccess<NCard, CanvasGroup>("_portraitCanvasGroup");

	public static void Sync(NCard card, CardEditorVisualFinish finish, bool fullArt, Dictionary<string, float>? fp = null)
	{
		if (card == null)
		{
			return;
		}

		TextureRect? portrait = null;
		TextureRect? ancientPortrait = null;
		CanvasGroup? portraitCanvasGroup = null;
		try
		{
			portrait = PortraitRef(card);
			ancientPortrait = AncientPortraitRef(card);
			portraitCanvasGroup = PortraitCanvasGroupRef(card);
		}
		catch
		{
			return;
		}

		Shader? shader = finish switch
		{
			CardEditorVisualFinish.Whirlpool => WhirlpoolShader,
			CardEditorVisualFinish.Miasma => MiasmaShader,
			CardEditorVisualFinish.Aurora => AuroraShader,
			CardEditorVisualFinish.Constellation => ConstellationShader,
			CardEditorVisualFinish.DvdRipple => RippleShader,
			CardEditorVisualFinish.Lightning => LightningShader,
			CardEditorVisualFinish.Flame => FlameShader,
			_ => null
		};

		ClearLegacyPortraitMaterial(portrait);
		ClearLegacyPortraitMaterial(ancientPortrait);
		RemoveLegacyPortraitOverlay(portrait);
		RemoveLegacyPortraitOverlay(ancientPortrait);

		if (portraitCanvasGroup == null || !GodotObject.IsInstanceValid(portraitCanvasGroup))
		{
			return;
		}

		if (shader == null)
		{
			RemoveOverlay(portraitCanvasGroup);
			return;
		}

		ColorRect overlay = GetOrCreateOverlay(portraitCanvasGroup);
		SyncOverlayLayout(overlay, fullArt);

		ShaderMaterial material;
		if (overlay.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == shader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = shader };
			overlay.Material = material;
		}

		ApplyOverlayArtSpace(material, fullArt);

		if (finish == CardEditorVisualFinish.Whirlpool)
		{
			ApplyWhirlpoolParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.Miasma)
		{
			ApplyMiasmaParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.Aurora)
		{
			ApplyAuroraParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.Constellation)
		{
			ApplyConstellationParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.DvdRipple)
		{
			ApplyRippleParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.Lightning)
		{
			ApplyLightningParams(material, fullArt, fp);
		}
		else if (finish == CardEditorVisualFinish.Flame)
		{
			ApplyFlameParams(material, fullArt, fp);
		}
	}

	private static ColorRect GetOrCreateOverlay(CanvasGroup portraitCanvasGroup)
	{
		string overlayName = OverlayNodeNamePrefix + "CardSpace";

		for (int i = 0; i < portraitCanvasGroup.GetChildCount(); i++)
		{
			if (portraitCanvasGroup.GetChild(i) is ColorRect existing && existing.Name == overlayName)
			{
				return existing;
			}
		}

		ColorRect overlay = new()
		{
			Name = overlayName,
			Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = false
		};
		portraitCanvasGroup.AddChild(overlay);
		return overlay;
	}

	private static void RemoveOverlay(CanvasGroup portraitCanvasGroup)
	{
		string overlayName = OverlayNodeNamePrefix + "CardSpace";
		for (int i = portraitCanvasGroup.GetChildCount() - 1; i >= 0; i--)
		{
			if (portraitCanvasGroup.GetChild(i) is ColorRect rect && rect.Name == overlayName)
			{
				portraitCanvasGroup.RemoveChild(rect);
				rect.QueueFree();
			}
		}
	}

	private static void ClearLegacyPortraitMaterial(TextureRect? portrait)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (portrait.Material is ShaderMaterial existing && IsManagedShader(existing.Shader))
		{
			portrait.Material = null;
		}
	}

	private static void RemoveLegacyPortraitOverlay(TextureRect? portrait)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		Node? parent = portrait.GetParent();
		string overlayName = OverlayNodeNamePrefix + portrait.Name;

		if (parent != null && GodotObject.IsInstanceValid(parent))
		{
			for (int i = parent.GetChildCount() - 1; i >= 0; i--)
			{
				if (parent.GetChild(i) is ColorRect rect && rect.Name == overlayName)
				{
					parent.RemoveChild(rect);
					rect.QueueFree();
				}
			}
		}

		for (int i = portrait.GetChildCount() - 1; i >= 0; i--)
		{
			Node child = portrait.GetChild(i);
			if ((child is TextureRect || child is ColorRect)
				&& (child.Name == overlayName || child.Name == LegacyOverlayNodeName))
			{
				portrait.RemoveChild(child);
				child.QueueFree();
			}
		}
	}

	private static void SyncOverlayLayout(ColorRect overlay, bool fullArt)
	{
		overlay.Visible = true;
		overlay.Modulate = Colors.White;
		overlay.SelfModulate = Colors.White;
		overlay.LayoutMode = 1;
		overlay.AnchorLeft = 0.5f;
		overlay.AnchorTop = 0.5f;
		overlay.AnchorRight = 0.5f;
		overlay.AnchorBottom = 0.5f;
		overlay.GrowHorizontal = Control.GrowDirection.Both;
		overlay.GrowVertical = Control.GrowDirection.Both;
		overlay.Scale = Vector2.One;
		overlay.Rotation = 0f;

		if (fullArt)
		{
			// Must match the AncientPortrait visual rect exactly: any child extending past it inflates the
			// portrait CanvasGroup's backbuffer union and stretches the vanilla ancient-portrait mask,
			// revealing art below the frame window.
			overlay.OffsetLeft = -153f;
			overlay.OffsetTop = -215f;
			overlay.OffsetRight = 146f;
			overlay.OffsetBottom = 206f;
			overlay.PivotOffset = new Vector2(149.5f, 210.5f);
		}
		else
		{
			overlay.OffsetLeft = -125f;
			overlay.OffsetTop = -168f;
			overlay.OffsetRight = 125f;
			overlay.OffsetBottom = 22f;
			overlay.PivotOffset = new Vector2(125f, 95f);
		}
	}

	private static void ApplyOverlayArtSpace(ShaderMaterial material, bool fullArt)
	{
		Vector2 effectSize = fullArt
			? new Vector2(299f, 421f)
			: new Vector2(250f, 190f);

		material.SetShaderParameter("card_effect_uv_origin", Vector2.Zero);
		material.SetShaderParameter("card_effect_uv_scale", Vector2.One);
		material.SetShaderParameter("card_effect_rect_size", effectSize);
		material.SetShaderParameter("card_effect_screen_origin", Vector2.Zero);
		material.SetShaderParameter("card_effect_screen_size", effectSize);
		material.SetShaderParameter("card_effect_pattern_scale", 1.0f);
	}

	private static bool IsManagedShader(Shader? shader)
		=> shader == WhirlpoolShader || shader == MiasmaShader || shader == AuroraShader || shader == ConstellationShader || shader == RippleShader || shader == LightningShader || shader == FlameShader;

	private static void ApplyWhirlpoolParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.64f : 0.58f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", 0.08f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("swirl_strength", CardEditorTextureLoader.P(fp, "swirlStrength", 4.7f));
		material.SetShaderParameter("line_count", CardEditorTextureLoader.P(fp, "lineCount", 6.0f));
		material.SetShaderParameter("line_sharpness", CardEditorTextureLoader.P(fp, "lineSharpness", 0.10f));
	}

	private static void ApplyMiasmaParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.60f : 0.55f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", 0.06f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("contrast", CardEditorTextureLoader.P(fp, "contrast", 1.0f));
	}

	private static void ApplyAuroraParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("background_color", new Vector3(
			CardEditorTextureLoader.P(fp, "backgroundColorR", CardEditorTextureLoader.P(fp, "backgroundColor1R", CardEditorTextureLoader.P(fp, "bgTopR", 0.25f))),
			CardEditorTextureLoader.P(fp, "backgroundColorG", CardEditorTextureLoader.P(fp, "backgroundColor1G", CardEditorTextureLoader.P(fp, "bgTopG", 0.0f))),
			CardEditorTextureLoader.P(fp, "backgroundColorB", CardEditorTextureLoader.P(fp, "backgroundColor1B", CardEditorTextureLoader.P(fp, "bgTopB", 0.2f)))));
		material.SetShaderParameter("GWM", CardEditorTextureLoader.P(fp, "GWM", 2.05f));
		material.SetShaderParameter("TM", CardEditorTextureLoader.P(fp, "TM", 0.25f));
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", 0.5f));
		material.SetShaderParameter("transparency", CardEditorTextureLoader.P(fp, "transparency", 0.0f));
		material.SetShaderParameter("background_transparency", CardEditorTextureLoader.P(fp, "backgroundTransparency", 0.0f));
		material.SetShaderParameter("wave_count", CardEditorTextureLoader.P(fp, "waveCount", 5.0f));
		material.SetShaderParameter("wave_distortion", CardEditorTextureLoader.P(fp, "waveDistortion", 1.0f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
	}

	private static void ApplyConstellationParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.72f : 0.66f));
		material.SetShaderParameter("line_strength", CardEditorTextureLoader.P(fp, "lineStrength", 1.0f));
		material.SetShaderParameter("line_width", CardEditorTextureLoader.P(fp, "lineWidth", 0.012f));
		material.SetShaderParameter("line_softness", CardEditorTextureLoader.P(fp, "lineSoftness", 0.004f));
		material.SetShaderParameter("connection_range", CardEditorTextureLoader.P(fp, "connectionRange", 1.3f));
		material.SetShaderParameter("target_length", CardEditorTextureLoader.P(fp, "targetLength", 0.75f));
		material.SetShaderParameter("target_band", CardEditorTextureLoader.P(fp, "targetBand", 0.04f));
		material.SetShaderParameter("long_line_strength", CardEditorTextureLoader.P(fp, "longLineStrength", 0.5f));
		material.SetShaderParameter("sparkle_strength", CardEditorTextureLoader.P(fp, "sparkleStrength", 1.0f));
		material.SetShaderParameter("sparkle_power", CardEditorTextureLoader.P(fp, "sparklePower", 1.5f));
		material.SetShaderParameter("sparkle_pulse", CardEditorTextureLoader.P(fp, "sparklePulse", 0.4f));
		material.SetShaderParameter("node_jitter", CardEditorTextureLoader.P(fp, "nodeJitter", 0.4f));
		material.SetShaderParameter("point_speed", CardEditorTextureLoader.P(fp, "pointSpeed", 1.0f));
		material.SetShaderParameter("layer_count", CardEditorTextureLoader.P(fp, "layerCount", 5.0f));
		material.SetShaderParameter("grid_scale", CardEditorTextureLoader.P(fp, "gridScale", 1.5f));
		material.SetShaderParameter("layer_min_scale", CardEditorTextureLoader.P(fp, "layerMinScale", 0.1f));
		material.SetShaderParameter("layer_max_scale", CardEditorTextureLoader.P(fp, "layerMaxScale", 15.0f));
		material.SetShaderParameter("layer_speed", CardEditorTextureLoader.P(fp, "layerSpeed", 1.0f));
		material.SetShaderParameter("background_glow", CardEditorTextureLoader.P(fp, "backgroundGlow", 0.18f));
		material.SetShaderParameter("color_speed", CardEditorTextureLoader.P(fp, "colorSpeed", 2.0f));
		material.SetShaderParameter("color_spread", CardEditorTextureLoader.P(fp, "colorSpread", 1.0f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", 0.0f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("network_color", new Vector3(
			CardEditorTextureLoader.P(fp, "networkR", 1.0f),
			CardEditorTextureLoader.P(fp, "networkG", 1.0f),
			CardEditorTextureLoader.P(fp, "networkB", 1.0f)));
		material.SetShaderParameter("network_tint_strength", CardEditorTextureLoader.P(fp, "networkTintStrength", 0.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
	}

	private static void ApplyRippleParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.70f : 0.65f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("ripple_strength", CardEditorTextureLoader.P(fp, "rippleStrength", 1.0f));
		material.SetShaderParameter("wave_frequency", CardEditorTextureLoader.P(fp, "waveFrequency", 2.0f));
		material.SetShaderParameter("wave_spread", CardEditorTextureLoader.P(fp, "waveSpread", 1.5f));
		material.SetShaderParameter("source_count", CardEditorTextureLoader.P(fp, "sourceCount", CardEditorTextureLoader.P(fp, "hitCount", 5.0f)));
		material.SetShaderParameter("highlight", CardEditorTextureLoader.P(fp, "highlight", 1.2f));
		material.SetShaderParameter("noise_strength", CardEditorTextureLoader.P(fp, "noiseStrength", 0.005f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("debug_mode", 0.0f);
	}

	private static void ApplyLightningParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.74f : 0.68f));
		material.SetShaderParameter("effect_color", new Vector3(
			CardEditorTextureLoader.P(fp, "lightningR", 0.2f),
			CardEditorTextureLoader.P(fp, "lightningG", 0.3f),
			CardEditorTextureLoader.P(fp, "lightningB", 0.8f)));
		material.SetShaderParameter("octave_count", CardEditorTextureLoader.P(fp, "octaves", 10.0f));
		material.SetShaderParameter("amp_start", CardEditorTextureLoader.P(fp, "ampStart", 0.5f));
		material.SetShaderParameter("amp_coeff", CardEditorTextureLoader.P(fp, "ampCoeff", 0.5f));
		material.SetShaderParameter("freq_coeff", CardEditorTextureLoader.P(fp, "freqCoeff", 2.0f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 0.5f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("bolt_width", CardEditorTextureLoader.P(fp, "boltWidth", 0.045f));
		material.SetShaderParameter("bolt_count", CardEditorTextureLoader.P(fp, "boltCount", 1.0f));
		material.SetShaderParameter("glow", CardEditorTextureLoader.P(fp, "glow", 1.4f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("flicker", CardEditorTextureLoader.P(fp, "flicker", 0.55f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
	}

	private static void ApplyFlameParams(ShaderMaterial material, bool fullArt, Dictionary<string, float>? fp)
	{
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.84f : 0.78f));
		material.SetShaderParameter("cold_color", new Color(
			CardEditorTextureLoader.P(fp, "coldR", 0.055f),
			CardEditorTextureLoader.P(fp, "coldG", 0.006f),
			CardEditorTextureLoader.P(fp, "coldB", 0.0f),
			1.0f));
		material.SetShaderParameter("deep_red_color", new Color(
			CardEditorTextureLoader.P(fp, "deepRedR", 0.55f),
			CardEditorTextureLoader.P(fp, "deepRedG", 0.025f),
			CardEditorTextureLoader.P(fp, "deepRedB", 0.0f),
			1.0f));
		material.SetShaderParameter("orange_color", new Color(
			CardEditorTextureLoader.P(fp, "orangeR", 1.0f),
			CardEditorTextureLoader.P(fp, "orangeG", 0.25f),
			CardEditorTextureLoader.P(fp, "orangeB", 0.015f),
			1.0f));
		material.SetShaderParameter("yellow_color", new Color(
			CardEditorTextureLoader.P(fp, "yellowR", 1.0f),
			CardEditorTextureLoader.P(fp, "yellowG", 0.68f),
			CardEditorTextureLoader.P(fp, "yellowB", 0.08f),
			1.0f));
		material.SetShaderParameter("white_core_color", new Color(
			CardEditorTextureLoader.P(fp, "whiteCoreR", 1.0f),
			CardEditorTextureLoader.P(fp, "whiteCoreG", 0.92f),
			CardEditorTextureLoader.P(fp, "whiteCoreB", 0.55f),
			1.0f));
		material.SetShaderParameter("smoke_color", new Color(
			CardEditorTextureLoader.P(fp, "smokeR", 0.16f),
			CardEditorTextureLoader.P(fp, "smokeG", 0.14f),
			CardEditorTextureLoader.P(fp, "smokeB", 0.12f),
			1.0f));
		material.SetShaderParameter("flame_height", CardEditorTextureLoader.P(fp, "flameHeight", 1.08f));
		material.SetShaderParameter("flame_width", CardEditorTextureLoader.P(fp, "flameWidth", 0.44f));
		material.SetShaderParameter("flame_top_width", CardEditorTextureLoader.P(fp, "flameTopWidth", 0.035f));
		material.SetShaderParameter("flame_intensity", CardEditorTextureLoader.P(fp, "flameIntensity", 0.82f));
		material.SetShaderParameter("core_heat", CardEditorTextureLoader.P(fp, "coreHeat", 0.38f));
		material.SetShaderParameter("turbulence", CardEditorTextureLoader.P(fp, "turbulence", 0.48f));
		material.SetShaderParameter("noise_scale", CardEditorTextureLoader.P(fp, "noiseScale", 7.5f));
		material.SetShaderParameter("rise_speed", CardEditorTextureLoader.P(fp, "speed", 1.45f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("flicker_strength", CardEditorTextureLoader.P(fp, "flickerStrength", 0.32f));
		material.SetShaderParameter("edge_softness", CardEditorTextureLoader.P(fp, "edgeSoftness", 0.24f));
		material.SetShaderParameter("glow_strength", CardEditorTextureLoader.P(fp, "glowStrength", 0.58f));
		material.SetShaderParameter("ember_density", CardEditorTextureLoader.P(fp, "emberDensity", 0.14f));
		material.SetShaderParameter("smoke_amount", CardEditorTextureLoader.P(fp, "smokeAmount", 0.08f));
		material.SetShaderParameter("wind", CardEditorTextureLoader.P(fp, "wind", 0.0f));
	}
}

internal static class CardEditorCustomShaderFoilController
{
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");

	public static void Sync(NCard card, string? customFinishId, bool fullArt, Dictionary<string, string>? customParams)
	{
		if (card == null)
		{
			return;
		}

		TextureRect? portrait = null;
		TextureRect? ancientPortrait = null;
		try
		{
			portrait = PortraitRef(card);
			ancientPortrait = AncientPortraitRef(card);
		}
		catch
		{
			return;
		}

		CardEditorCustomFoilDefinition? definition = null;
		if (!string.IsNullOrWhiteSpace(customFinishId))
		{
			CardEditorCustomFoilRegistry.TryGet(customFinishId, out definition!);
		}

		SyncPortrait(card, portrait, definition, fullArt, customParams);
		SyncPortrait(card, ancientPortrait, definition, fullArt, customParams);
	}

	private static void SyncPortrait(NCard card, TextureRect? portrait, CardEditorCustomFoilDefinition? definition, bool fullArt, Dictionary<string, string>? customParams)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (definition == null)
		{
			if (portrait.Material is ShaderMaterial existing && CardEditorCustomFoilRegistry.IsCustomShader(existing.Shader))
			{
				portrait.Material = null;
			}
			return;
		}

		ShaderMaterial material;
		if (portrait.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == definition.Shader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = definition.Shader };
			portrait.Material = material;
		}

		Vector2 rectSize = CardEditorArtFinishCardSpace.GetDisplayedArtRectSize(portrait);
		CardEditorCustomFoilRegistry.ApplyParameters(material, definition, fullArt, customParams, rectSize);
		CardEditorArtFinishCardSpace.ApplyLocalArtSpace(material, portrait);
	}
}

internal static class CardEditorBaseGameOverlayFinishController
{
	private const string OverlayNodeNamePrefix = "CardEditorBaseGameFinishOverlay_";
	private static readonly AccessTools.FieldRef<NCard, Node> OverlayContainerRef =
		AccessTools.FieldRefAccess<NCard, Node>("_overlayContainer");

	public static void Sync(NCard card, CardEditorVisualFinish finish)
	{
		if (card == null)
		{
			return;
		}

		Node? overlayContainer = null;
		try
		{
			overlayContainer = OverlayContainerRef(card);
		}
		catch
		{
			return;
		}

		if (overlayContainer == null || !GodotObject.IsInstanceValid(overlayContainer))
		{
			return;
		}

		string? path = GetOverlayScenePath(finish);
		RemoveExisting(overlayContainer, keepFinish: path != null ? finish : null);
		if (path == null || FindExisting(overlayContainer, finish) != null)
		{
			return;
		}

		PackedScene? scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			return;
		}

		Node overlay = scene.Instantiate();
		overlay.Name = OverlayNodeNamePrefix + finish;
		if (overlay is Control control)
		{
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
		overlayContainer.AddChild(overlay);
	}

	private static string? GetOverlayScenePath(CardEditorVisualFinish finish) => finish switch
	{
		CardEditorVisualFinish.BaseGameInfection => "res://scenes/cards/overlays/infection.tscn",
		CardEditorVisualFinish.BaseGameGalvanized => "res://scenes/cards/overlays/afflictions/galvanized.tscn",
		CardEditorVisualFinish.BaseGameBound => "res://scenes/cards/overlays/afflictions/bound.tscn",
		CardEditorVisualFinish.BaseGameEntangled => "res://scenes/cards/overlays/afflictions/entangled.tscn",
		CardEditorVisualFinish.BaseGameHexed => "res://scenes/cards/overlays/afflictions/hexed.tscn",
		CardEditorVisualFinish.BaseGameRinging => "res://scenes/cards/overlays/afflictions/ringing.tscn",
		CardEditorVisualFinish.BaseGameSmog => "res://scenes/cards/overlays/afflictions/smog.tscn",
		_ => null
	};

	private static Node? FindExisting(Node overlayContainer, CardEditorVisualFinish finish)
	{
		string expectedName = OverlayNodeNamePrefix + finish;
		for (int i = 0; i < overlayContainer.GetChildCount(); i++)
		{
			Node child = overlayContainer.GetChild(i);
			if (child.Name == expectedName)
			{
				return child;
			}
		}
		return null;
	}

	private static void RemoveExisting(Node overlayContainer, CardEditorVisualFinish? keepFinish)
	{
		string? keepName = keepFinish.HasValue ? OverlayNodeNamePrefix + keepFinish.Value : null;
		for (int i = overlayContainer.GetChildCount() - 1; i >= 0; i--)
		{
			Node child = overlayContainer.GetChild(i);
			string name = child.Name.ToString();
			if (!name.StartsWith(OverlayNodeNamePrefix, StringComparison.Ordinal) || name == keepName)
			{
				continue;
			}
			overlayContainer.RemoveChild(child);
			child.QueueFree();
		}
	}
}

internal sealed class CardEditorBorderFoilOverlay : Control
{
	private const string LightningCornerScenePath = "res://scenes/vfx/ui/card/afflictions/galvanized/vfx_ui_card_affliction_lightning_corner.tscn";
	private const string ShaderCode = @"shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec2 rect_size = vec2(300.0, 422.0);
uniform vec2 card_origin = vec2(0.0, 0.0);
uniform float border_width_px = 12.0;
uniform float border_inset_px = 0.0;
uniform float corner_radius_px = 28.0;
uniform float corner_softness_px = 1.5;
uniform float opacity = 0.78;
uniform float flow_speed = 1.0;
uniform float pattern_scale = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform float glow_strength = 0.55;
uniform float time_offset = 0.0;
uniform float hue_shift = 0.0;
uniform int border_style = 1;

const float BORDER_TAU = 6.28318530718;
varying vec2 local_px;

void vertex()
{
	local_px = VERTEX;
}

float sd_round_rect(vec2 p, vec2 half_extents, float radius)
{
	vec2 q = abs(p) - half_extents + vec2(radius);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
}

float hash(vec2 p)
{
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float wrapped_delta(float a, float b)
{
	return fract(a - b + 0.5) - 0.5;
}

float lightning_path(float s, float seed, float tick)
{
	float segments = 54.0;
	float scaled = s * segments;
	float segment = floor(scaled);
	float next_segment = mod(segment + 1.0, segments);
	float local = fract(scaled);
	float y0 = hash(vec2(segment, seed + tick * 1.37));
	float y1 = hash(vec2(next_segment, seed + tick * 1.37));
	float line = mix(y0, y1, local);
	float snap = mix(line, y0, 0.28 * (1.0 - smoothstep(0.10, 0.55, local)));
	float jitter = noise(vec2(segment * 0.31 + seed, tick * 0.73)) - 0.5;
	return clamp(0.16 + snap * 0.68 + jitter * 0.16, 0.07, 0.93);
}

float lightning_track(float flow, float ring_factor, float t, float offset, float seed)
{
	float tick = floor(t * 6.0);
	float s = fract(flow + offset + t * 0.052);
	float path = lightning_path(s, seed, tick);
	float dist = abs(ring_factor - path);
	float core = 1.0 - smoothstep(0.000, 0.038, dist);
	float glow = 1.0 - smoothstep(0.018, 0.190, dist);
	float segment = floor(s * 54.0);
	float spark = 0.64 + 0.36 * hash(vec2(segment, seed + tick * 2.13));
	float flicker = mix(0.42, 1.0, hash(vec2(segment + tick, seed * 0.19)));
	float branch_seed = hash(vec2(segment, seed + 41.0 + tick));
	float branch_gate = step(0.66, branch_seed) * smoothstep(0.08, 0.22, fract(s * 54.0)) * (1.0 - smoothstep(0.54, 0.92, fract(s * 54.0)));
	float branch_target = clamp(path + sign(branch_seed - 0.5) * (0.18 + 0.18 * hash(vec2(segment, seed + 83.0))), 0.06, 0.94);
	float branch_line = mix(path, branch_target, fract(s * 54.0));
	float branch = (1.0 - smoothstep(0.0, 0.030, abs(ring_factor - branch_line))) * branch_gate;
	return clamp((core * 1.45 + glow * 0.36 + branch * 0.82) * spark * flicker, 0.0, 1.0);
}

float lightning_bolt(float flow, float ring_factor, float t)
{
	float main = lightning_track(flow, ring_factor, t, 0.00, 11.7);
	float echo = lightning_track(flow, ring_factor, t, 0.47, 29.3) * 0.64;
	float corner_pulse = pow(sin((flow * 4.0 - t * 0.82) * BORDER_TAU) * 0.5 + 0.5, 10.0) * 0.28;
	return clamp(max(main, echo) + corner_pulse, 0.0, 1.0);
}

vec3 hsv2rgb(vec3 c)
{
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

vec3 rgb2hsv(vec3 c)
{
	vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 apply_hue_shift(vec3 color)
{
	vec3 hsv = rgb2hsv(max(color, vec3(0.0)));
	hsv.x = fract(hsv.x + hue_shift);
	return hsv2rgb(hsv);
}

float border_flow(vec2 local, vec2 safe_size, float inset, float width, float radius)
{
	vec2 h = max(vec2(1.0), safe_size * 0.5 - vec2(inset + width * 0.5));
	float r = clamp(radius - width * 0.5, 0.0, min(h.x, h.y) - 0.5);
	float straight_w = max(1.0, (h.x - r) * 2.0);
	float straight_h = max(1.0, (h.y - r) * 2.0);
	float arc_len = r * BORDER_TAU * 0.25;
	float perimeter = max(1.0, 2.0 * (straight_w + straight_h) + 4.0 * arc_len);
	float left_x = -h.x + r;
	float right_x = h.x - r;
	float top_y = -h.y + r;
	float bottom_y = h.y - r;
	float ax = abs(local.x);
	float ay = abs(local.y);
	float dist = 0.0;

	if (r > 0.5 && ax > right_x && ay > bottom_y)
	{
		float sx = local.x < 0.0 ? -1.0 : 1.0;
		float sy = local.y < 0.0 ? -1.0 : 1.0;
		vec2 center = vec2(sx * right_x, sy * bottom_y);
		float a = atan(local.y - center.y, local.x - center.x);
		if (sx > 0.0 && sy < 0.0)
		{
			float f = clamp((a + BORDER_TAU * 0.25) / (BORDER_TAU * 0.25), 0.0, 1.0);
			dist = straight_w + f * arc_len;
		}
		else if (sx > 0.0 && sy > 0.0)
		{
			float f = clamp(a / (BORDER_TAU * 0.25), 0.0, 1.0);
			dist = straight_w + arc_len + straight_h + f * arc_len;
		}
		else if (sx < 0.0 && sy > 0.0)
		{
			float f = clamp((a - BORDER_TAU * 0.25) / (BORDER_TAU * 0.25), 0.0, 1.0);
			dist = straight_w + arc_len + straight_h + arc_len + straight_w + f * arc_len;
		}
		else
		{
			if (a < 0.0)
			{
				a += BORDER_TAU;
			}
			float f = clamp((a - BORDER_TAU * 0.5) / (BORDER_TAU * 0.25), 0.0, 1.0);
			dist = straight_w + arc_len + straight_h + arc_len + straight_w + arc_len + straight_h + f * arc_len;
		}
	}
	else if (local.y < 0.0 && ax <= right_x)
	{
		dist = clamp(local.x, left_x, right_x) - left_x;
	}
	else if (local.x >= 0.0 && ay <= bottom_y)
	{
		dist = straight_w + arc_len + (clamp(local.y, top_y, bottom_y) - top_y);
	}
	else if (local.y >= 0.0 && ax <= right_x)
	{
		dist = straight_w + arc_len + straight_h + arc_len + (right_x - clamp(local.x, left_x, right_x));
	}
	else
	{
		dist = straight_w + arc_len + straight_h + arc_len + straight_w + arc_len + (bottom_y - clamp(local.y, top_y, bottom_y));
	}

	return fract(dist / perimeter);
}

vec2 rot2(vec2 p, float a)
{
	float s = sin(a);
	float c = cos(a);
	return vec2(p.x * c - p.y * s, p.x * s + p.y * c);
}

float object_wave(vec2 p)
{
	float v = 0.0;
	v += sin(p.x * 1.43 + p.y * 0.76);
	v += sin(p.x * -0.91 + p.y * 1.87 + 1.7) * 0.52;
	v += sin(length(p * vec2(0.78, 1.24)) * 2.36 + p.x * 0.38) * 0.34;
	return v / 2.08;
}

float object_noise(vec2 object_uv, float t, float scale)
{
	vec2 p = (object_uv - vec2(0.5)) * vec2(4.0, 5.4) * max(scale, 0.1);
	vec2 p1 = p + vec2(0.0, -t * 0.42);
	vec2 p2 = rot2(p, 0.645) + vec2(0.37, -t * 0.61);
	vec2 p3 = rot2(p * 1.73, -0.918) + vec2(t * 0.16, -t * 0.31);
	vec2 warp = vec2(
		object_wave(p2 * 0.72 + vec2(2.1, -1.4)),
		object_wave(p3 * 0.58 + vec2(-3.8, 4.2))
	);
	float v = 0.0;
	v += object_wave(p1 + warp * 0.62) * 0.50;
	v += object_wave(p2 - warp.yx * 0.48) * 0.31;
	v += object_wave(p3 + warp * 0.35) * 0.19;
	return clamp(v * 0.5 + 0.5, 0.0, 1.0);
}

float object_shine(vec2 object_uv, float t, float n, float scale)
{
	vec2 p = (object_uv - vec2(0.5)) * vec2(3.6, 4.8) * max(scale, 0.1);
	float a = sin((rot2(p, 0.645).x * 1.35 + p.y * 0.42 - t * 0.72 + n * 0.38) * BORDER_TAU) * 0.5 + 0.5;
	float b = sin((rot2(p * 1.51, -0.918).y * 0.92 + p.x * 0.31 - t * 0.47) * BORDER_TAU) * 0.5 + 0.5;
	return pow(clamp(a * 0.62 + b * 0.38, 0.0, 1.0), 5.2);
}

vec2 border_space(vec2 uv, float aspect)
{
	return vec2(uv.x * aspect, uv.y);
}

float segment_distance(vec2 p, vec2 a, vec2 b)
{
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / max(dot(ba, ba), 0.00001), 0.0, 1.0);
	return length(pa - ba * h);
}

float bolt_segment(vec2 p, vec2 a, vec2 b, float core_width, float glow_width)
{
	float d = segment_distance(p, a, b);
	float core = 1.0 - smoothstep(0.0, core_width, d);
	float glow = 1.0 - smoothstep(core_width, glow_width, d);
	return clamp(core * 1.65 + glow * 0.34, 0.0, 1.0);
}

float jagged_bolt(vec2 p, vec2 a, vec2 b, float id, float seed, float core_width, float glow_width, float jag_amount)
{
	vec2 dir = b - a;
	vec2 dir_norm = normalize(dir);
	vec2 normal = normalize(vec2(-dir.y, dir.x));
	vec2 previous = a;
	float bolt = 0.0;

	for (int i = 1; i <= 8; i++)
	{
		float fi = float(i);
		float f = fi / 8.0;
		vec2 next = mix(a, b, f);
		if (i < 8)
		{
			float jag = hash(vec2(id + fi * 7.13, seed + fi * 13.71)) - 0.5;
			float shove = hash(vec2(seed + fi * 11.41, id + fi * 3.79)) - 0.5;
			next += normal * jag * jag_amount * (0.92 + 0.24 * sin(fi * 2.1 + id));
			next += dir_norm * shove * jag_amount * 0.36;
		}
		bolt = max(bolt, bolt_segment(p, previous, next, core_width, glow_width));

		if (i > 1 && i < 8)
		{
			float fork_seed = hash(vec2(id + fi * 31.17, seed + fi * 5.91));
			float fork_gate = step(0.45, fork_seed);
			float side = fork_seed < 0.72 ? -1.0 : 1.0;
			float fork_len = jag_amount * mix(0.72, 1.92, hash(vec2(seed + fi * 17.7, id + 4.2)));
			vec2 fork_dir = normalize(normal * side + dir_norm * mix(-0.28, 0.42, hash(vec2(id + fi, seed + 8.0))));
			vec2 fork_end = next + fork_dir * fork_len;
			float fork = bolt_segment(p, next, fork_end, core_width * 0.62, glow_width * 0.62);
			bolt = max(bolt, fork * fork_gate * 0.82);
		}

		float knot = exp(-length(p - next) / max(glow_width * 0.70, 0.001)) * 0.22;
		bolt = max(bolt, knot);
		previous = next;
	}

	return clamp(bolt, 0.0, 1.35);
}

float zap_envelope(float phase)
{
	return smoothstep(0.00, 0.08, phase) * (1.0 - smoothstep(0.50, 0.92, phase));
}

float edge_zap(vec2 p, vec2 a_uv, vec2 b_uv, float aspect, float t, float seed, float core_width, float glow_width, float jag_amount)
{
	float rate = 2.35;
	float phase = fract(t * rate + seed);
	float id = floor(t * rate + seed);
	float gate = step(0.18, hash(vec2(id, seed * 9.17)));
	vec2 a = border_space(a_uv, aspect);
	vec2 b = border_space(b_uv, aspect);

	float span = mix(0.42, 0.88, hash(vec2(seed + 71.0, id + 3.0)));
	float start = hash(vec2(id + 13.0, seed + 29.0)) * (1.0 - span);
	vec2 s = mix(a, b, start);
	vec2 e = mix(a, b, start + span);
	float bolt = jagged_bolt(p, s, e, id, seed, core_width, glow_width, jag_amount);

	float head = exp(-length(p - e) / max(glow_width * 1.15, 0.001)) * 0.34;
	float tail = exp(-length(p - s) / max(glow_width * 0.82, 0.001)) * 0.18;
	return (bolt + head + tail) * zap_envelope(phase) * gate;
}

float corner_flash(vec2 p, vec2 corner_uv, float aspect, float t, float seed, float glow_width)
{
	float phase = fract(t * 2.1 + seed);
	float id = floor(t * 2.1 + seed);
	float gate = step(0.36, hash(vec2(id + 19.0, seed * 5.31)));
	float d = length(p - border_space(corner_uv, aspect));
	return exp(-d / max(glow_width * 1.35, 0.001)) * zap_envelope(phase) * gate;
}

float object_lightning(vec2 object_uv, float t, float n, float inner_factor, vec2 safe_size, float width, float inset)
{
	float aspect = safe_size.x / max(safe_size.y, 1.0);
	vec2 p = border_space(object_uv, aspect);

	float lane_x = clamp((inset + width * 0.65) / max(safe_size.x, 1.0), 0.045, 0.470);
	float lane_y = clamp((inset + width * 0.65) / max(safe_size.y, 1.0), 0.035, 0.470);
	float left_x = lane_x;
	float right_x = 1.0 - lane_x;
	float top_y = lane_y;
	float bottom_y = 1.0 - lane_y;
	float x_pad = min(0.10, max(0.018, lane_x * 0.30));
	float y_pad = min(0.10, max(0.018, lane_y * 0.30));

	float width_units = clamp(width / max(safe_size.y, 1.0), 0.012, 0.48);
	float core_width = clamp(width_units * 0.16, 0.0055, 0.050);
	float glow_width = clamp(width_units * 0.88, 0.034, 0.210);
	float jag_amount = clamp(width_units * 1.55, 0.060, 0.280);

	float top = edge_zap(p, vec2(left_x + x_pad, top_y), vec2(right_x - x_pad, top_y + lane_y * 0.05), aspect, t, 1.7, core_width, glow_width, jag_amount);
	float right = edge_zap(p, vec2(right_x, top_y + y_pad), vec2(right_x - lane_x * 0.05, bottom_y - y_pad), aspect, t, 5.3, core_width, glow_width, jag_amount);
	float bottom = edge_zap(p, vec2(right_x - x_pad, bottom_y), vec2(left_x + x_pad, bottom_y - lane_y * 0.05), aspect, t, 9.1, core_width, glow_width, jag_amount);
	float left = edge_zap(p, vec2(left_x, bottom_y - y_pad), vec2(left_x + lane_x * 0.05, top_y + y_pad), aspect, t, 12.9, core_width, glow_width, jag_amount);

	float diagonal_a = edge_zap(p, vec2(left_x + x_pad, top_y), vec2(left_x, top_y + y_pad * 3.4), aspect, t, 17.2, core_width * 0.82, glow_width * 0.82, jag_amount * 0.78) * 0.72;
	float diagonal_b = edge_zap(p, vec2(right_x - x_pad, bottom_y), vec2(right_x, bottom_y - y_pad * 3.4), aspect, t, 22.4, core_width * 0.82, glow_width * 0.82, jag_amount * 0.78) * 0.72;

	float corners = 0.0;
	corners = max(corners, corner_flash(p, vec2(left_x, top_y), aspect, t, 2.6, glow_width));
	corners = max(corners, corner_flash(p, vec2(right_x, top_y), aspect, t, 6.8, glow_width));
	corners = max(corners, corner_flash(p, vec2(right_x, bottom_y), aspect, t, 10.4, glow_width));
	corners = max(corners, corner_flash(p, vec2(left_x, bottom_y), aspect, t, 14.2, glow_width));

	float bolt = max(max(top, right), max(bottom, left));
	bolt = max(bolt, max(diagonal_a, diagonal_b));
	bolt += corners * 0.58;

	float ring = 0.72 + 0.28 * smoothstep(0.02, 0.55, inner_factor);
	float flicker = 0.82 + 0.18 * sin(t * 24.0 + n * 8.0);
	return clamp(bolt * ring * flicker, 0.0, 1.0);
}

vec3 style_color(float flow, float t, float n, float inner_factor, vec2 object_uv, vec2 object_p, vec2 safe_size, float width, float inset)
{
	if (border_style == 3)
	{
		vec3 storm = mix(vec3(0.005, 0.025, 0.16), vec3(0.03, 0.22, 0.72), smoothstep(0.10, 0.90, n));
		float pulse = pow(sin((object_uv.x * 1.3 + object_uv.y * 1.9 - t * 0.34 + n * 0.38) * BORDER_TAU) * 0.5 + 0.5, 6.0);
		return storm + vec3(0.03, 0.34, 0.86) * pulse * 0.22;
	}
	if (border_style == 4)
	{
		float lick = sin((object_uv.x * 3.2 + object_uv.y * 6.4 - t * 1.55 + n * 0.9) * BORDER_TAU) * 0.5 + 0.5;
		float heat = clamp(n * 0.68 + lick * 0.30 + object_uv.y * 0.22 + inner_factor * 0.28, 0.0, 1.0);
		vec3 red = vec3(0.58, 0.03, 0.0);
		vec3 orange = vec3(1.0, 0.31, 0.02);
		vec3 yellow = vec3(1.0, 0.78, 0.12);
		return mix(mix(red, orange, smoothstep(0.18, 0.64, heat)), yellow, smoothstep(0.64, 1.0, heat));
	}
	if (border_style == 5)
	{
		float water = sin((object_uv.y * 7.6 + object_uv.x * 2.4 + n * 1.3 + t * 0.62) * BORDER_TAU) * 0.5 + 0.5;
		return mix(vec3(0.04, 0.30, 0.58), vec3(0.32, 0.92, 1.0), water);
	}
	if (border_style == 6)
	{
		float star = pow(smoothstep(0.72, 1.0, n), 4.0);
		return mix(vec3(0.06, 0.07, 0.22), vec3(0.96, 0.92, 0.72), star);
	}
	if (border_style == 7)
	{
		float ripple = sin((length(object_p * vec2(1.0, 1.35)) * 13.0 - t * 1.1 + n * 0.7) * BORDER_TAU) * 0.5 + 0.5;
		return mix(vec3(0.18, 0.15, 0.70), vec3(0.90, 0.75, 1.0), ripple);
	}
	if (border_style == 8)
	{
		float coord = fract(object_uv.x * 0.72 + object_uv.y * 0.48 + n * 0.18 + t * 0.08);
		float band = sin((object_uv.x * 2.3 - object_uv.y * 1.7 + t * 0.55 + n * 0.42) * BORDER_TAU) * 0.5 + 0.5;
		return mix(vec3(0.65, 0.73, 1.0), hsv2rgb(vec3(coord, 0.62, 1.0)), band);
	}
	if (border_style == 2)
	{
		float fog = clamp(n * 1.18 + sin((object_uv.x * 1.8 + object_uv.y * 2.2 - t * 0.38) * BORDER_TAU) * 0.22, 0.0, 1.0);
		return mix(vec3(0.09, 0.45, 0.18), vec3(0.78, 0.30, 0.96), fog);
	}
	float coord = fract(object_uv.x * 0.78 + object_uv.y * 0.42 + n * 0.24 + t * 0.08);
	return hsv2rgb(vec3(coord, 0.76, 1.0));
}

void fragment()
{
	vec2 safe_size = max(rect_size, vec2(1.0));
	float width = max(1.0, border_width_px);
	float inset = max(0.0, border_inset_px);
	float radius = max(0.0, corner_radius_px);
	vec2 local = UV * safe_size - safe_size * 0.5;
	float outer = sd_round_rect(local, safe_size * 0.5 - vec2(inset), radius);
	float inner = sd_round_rect(local, safe_size * 0.5 - vec2(inset + width), max(0.0, radius - width));
	float aa = max(corner_softness_px, max(fwidth(outer), fwidth(inner)) * 1.5);
	float outer_mask = 1.0 - smoothstep(0.0, aa, outer);
	float inner_mask = 1.0 - smoothstep(0.0, aa, inner);
	float mask = clamp(outer_mask - inner_mask, 0.0, 1.0);
	if (mask <= 0.001)
	{
		discard;
	}

	float inner_factor = clamp(inner / max(1.0, width), 0.0, 1.0);
	float flow = border_flow(local, safe_size, inset, width, radius);
	float t = TIME * flow_speed + time_offset;
	float scale = max(0.1, pattern_scale);
	vec2 object_uv = local_px / safe_size;
	vec2 object_p = object_uv - vec2(0.5);
	float n = object_noise(object_uv, t, scale);
	float lightning = 0.0;
	float shine = border_style == 3 ? pow(n, 3.0) * 0.42 : object_shine(object_uv, t, n, scale);
	vec3 color = style_color(flow, t, n, inner_factor, object_uv, object_p, safe_size, width, inset);
	if (border_style == 3)
	{
		color += vec3(0.20, 0.80, 1.0) * lightning * glow_strength * 1.15;
		color += vec3(1.0, 0.96, 0.78) * pow(lightning, 2.4) * glow_strength * 1.35;
		color *= 0.74 + glow_strength * 0.30 + lightning * 0.72;
	}
	else
	{
		color += vec3(1.0, 0.95, 0.80) * shine * glow_strength * 0.62;
		color *= 0.72 + glow_strength * 0.34 + shine * 0.30;
	}
	color = apply_hue_shift(color);
	float alpha = mask * opacity * (0.68 + n * 0.22 + shine * 0.18);
	alpha = max(alpha, mask * opacity * lightning * 1.18);
	alpha = clamp(alpha, 0.0, 0.97);
	COLOR = vec4(color * alpha, alpha);
}";

	private static readonly Shader SharedShader = new Shader { Code = ShaderCode };
	private static PackedScene? _lightningCornerScene;
	private readonly ShaderMaterial _material;
	private readonly ColorRect _overlayRect;
	private readonly Node2D?[] _lightningCorners = new Node2D?[4];
	private Vector2 _lastKnownRectSize = Vector2.Zero;
	private float _lastBorderWidth = 12.0f;
	private float _lastBorderInset = 0.0f;
	private bool _lightningVfxVisible;

	public CardEditorBorderFoilOverlay()
	{
		Name = "CardEditorBorderFoilOverlay";
		MouseFilter = MouseFilterEnum.Ignore;
		LayoutMode = 3;
		ZIndex = 0;
		ClipContents = false;
		_material = new ShaderMaterial { Shader = SharedShader };
		_material.SetShaderParameter("rect_size", new Vector2(300.0f, 422.0f));
		_overlayRect = new ColorRect
		{
			Color = new Color(1, 1, 1, 0),
			Material = _material,
			MouseFilter = MouseFilterEnum.Ignore,
			LayoutMode = 0,
			OffsetLeft = -150.0f,
			OffsetTop = -211.0f,
			OffsetRight = 150.0f,
			OffsetBottom = 211.0f
		};
		AddChild(_overlayRect);
		SetProcess(true);
		ApplyStyle(CardEditorVisualFinish.RainbowRareFoil, fullArt: false, null);
	}

	public override void _Ready()
	{
		base._Ready();
		UpdateRectSize();
		UpdateShaderOrigin();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		UpdateShaderOrigin();
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
		{
			UpdateRectSize();
		}
	}

	public void ApplyStyle(CardEditorVisualFinish finish, bool fullArt, Dictionary<string, float>? fp)
	{
		bool lightning = finish == CardEditorVisualFinish.Lightning;
		_lastBorderWidth = CardEditorTextureLoader.P(fp, "borderWidth", lightning ? (fullArt ? 17.0f : 15.0f) : (fullArt ? 14.0f : 12.0f));
		_lastBorderInset = CardEditorTextureLoader.P(fp, "borderInset", 0.0f);
		_lightningVfxVisible = lightning;
		_material.SetShaderParameter("border_style", ToStyleId(finish));
		_material.SetShaderParameter("border_width_px", _lastBorderWidth);
		_material.SetShaderParameter("border_inset_px", _lastBorderInset);
		_material.SetShaderParameter("corner_radius_px", CardEditorTextureLoader.P(fp, "borderCornerRadius", 28.0f));
		_material.SetShaderParameter("corner_softness_px", 1.5f);
		_material.SetShaderParameter("opacity", CardEditorTextureLoader.P(fp, "borderOpacity", lightning ? 0.86f : (fullArt ? 0.76f : 0.70f)));
		_material.SetShaderParameter("flow_speed", CardEditorTextureLoader.P(fp, "borderFlowSpeed", lightning ? 1.35f : 1.0f));
		_material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "borderPatternScale", lightning ? 0.78f : 1.0f));
		_material.SetShaderParameter("glow_strength", CardEditorTextureLoader.P(fp, "borderGlow", lightning ? 1.10f : 0.55f));
		_material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "borderTimeOffset", 0.0f));
		_material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "borderHueShift", 0.0f));
		UpdateRectSize();
		UpdateShaderOrigin();
		UpdateLightningCornerVfx();
	}

	private static int ToStyleId(CardEditorVisualFinish finish) => finish switch
	{
		CardEditorVisualFinish.Miasma => 2,
		CardEditorVisualFinish.Lightning => 3,
		CardEditorVisualFinish.Flame => 4,
		CardEditorVisualFinish.Whirlpool or CardEditorVisualFinish.PurpleWavesOcean => 5,
		CardEditorVisualFinish.Constellation => 6,
		CardEditorVisualFinish.DvdRipple => 7,
		CardEditorVisualFinish.PrismaticBandGlare => 8,
		_ => 1
	};

	private void UpdateRectSize()
	{
		Vector2 currentSize = Size;
		if (currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			currentSize = _overlayRect.Size;
		}

		if (currentSize == _lastKnownRectSize || currentSize.X <= 0.0f || currentSize.Y <= 0.0f)
		{
			return;
		}

		_lastKnownRectSize = currentSize;
		_material.SetShaderParameter("rect_size", currentSize);
		UpdateShaderOrigin();
		UpdateLightningCornerVfx();
	}

	private void UpdateShaderOrigin()
	{
		if (!GodotObject.IsInstanceValid(_overlayRect))
		{
			return;
		}

		_material.SetShaderParameter("card_origin", _overlayRect.GetGlobalTransformWithCanvas().Origin);
	}

	private void EnsureLightningCornerVfx()
	{
		if (_lightningCorners[0] != null && GodotObject.IsInstanceValid(_lightningCorners[0]))
		{
			return;
		}

		_lightningCornerScene ??= GD.Load<PackedScene>(LightningCornerScenePath);
		if (_lightningCornerScene == null)
		{
			return;
		}

		for (int i = 0; i < _lightningCorners.Length; i++)
		{
			Node2D corner = _lightningCornerScene.Instantiate<Node2D>(PackedScene.GenEditState.Disabled);
				corner.Name = $"CardEditorLightningCorner_{i}";
				corner.Visible = false;
				corner.ZIndex = 2;
				corner.Rotation = i switch
				{
					1 => Mathf.Pi * 0.5f,
				2 => Mathf.Pi,
				3 => Mathf.Pi * 1.5f,
				_ => 0.0f
			};
			AddChild(corner);
			_lightningCorners[i] = corner;
		}
	}

	private void UpdateLightningCornerVfx()
	{
		if (!_lightningVfxVisible)
		{
			SetLightningCornerVfxVisible(false);
			return;
		}

		EnsureLightningCornerVfx();
		if (_lightningCorners[0] == null || !GodotObject.IsInstanceValid(_lightningCorners[0]))
		{
			return;
		}

		Vector2 size = _lastKnownRectSize;
		if (size.X <= 0.0f || size.Y <= 0.0f)
		{
			size = new Vector2(300.0f, 422.0f);
		}

		float laneX = Mathf.Clamp(_lastBorderInset + _lastBorderWidth * 0.5f, 0.0f, Math.Max(0.0f, size.X * 0.5f - 1.0f));
		float laneY = Mathf.Clamp(_lastBorderInset + _lastBorderWidth * 0.5f, 0.0f, Math.Max(0.0f, size.Y * 0.5f - 1.0f));
		Vector2 topLeft = size * -0.5f;
		Vector2[] positions =
		[
			topLeft + new Vector2(laneX, laneY),
			topLeft + new Vector2(size.X - laneX, laneY),
			topLeft + new Vector2(size.X - laneX, size.Y - laneY),
			topLeft + new Vector2(laneX, size.Y - laneY)
		];

		float cardScale = Math.Max(0.1f, (size.X / 300.0f + size.Y / 422.0f) * 0.5f);
		for (int i = 0; i < _lightningCorners.Length; i++)
		{
			Node2D? corner = _lightningCorners[i];
			if (corner == null || !GodotObject.IsInstanceValid(corner))
			{
				continue;
			}

			corner.Position = positions[i];
			corner.Scale = Vector2.One * cardScale;
			corner.Visible = true;
			corner.Set("emitting", true);
		}
	}

	private void SetLightningCornerVfxVisible(bool visible)
	{
		for (int i = 0; i < _lightningCorners.Length; i++)
		{
			Node2D? corner = _lightningCorners[i];
			if (corner == null || !GodotObject.IsInstanceValid(corner))
			{
				continue;
			}

			corner.Visible = visible;
			corner.Set("emitting", visible);
		}
	}
}

internal static class CardEditorBorderFoilOverlayController
{
	public static void Sync(Node overlayContainer, CardEditorVisualFinish finish, bool fullArt, Dictionary<string, float>? fp)
	{
		if (overlayContainer == null || !GodotObject.IsInstanceValid(overlayContainer))
		{
			return;
		}

		CardEditorBorderFoilOverlay? overlay = FindOverlay(overlayContainer);
		if (!IsSupportedBorderFinish(finish))
		{
			RemoveOverlay(overlayContainer, overlay);
			return;
		}

		if (overlay == null)
		{
			overlay = new CardEditorBorderFoilOverlay();
			overlayContainer.AddChild(overlay);
		}

		overlay.ApplyStyle(finish, fullArt, fp);
		overlay.Show();
	}

	private static bool IsSupportedBorderFinish(CardEditorVisualFinish finish) => finish switch
	{
		CardEditorVisualFinish.RainbowRareFoil
		or CardEditorVisualFinish.RainbowGlitterArt
		or CardEditorVisualFinish.PrismaticBandGlare
		or CardEditorVisualFinish.PurpleWavesOcean
		or CardEditorVisualFinish.Whirlpool
		or CardEditorVisualFinish.Miasma
		or CardEditorVisualFinish.Constellation
		or CardEditorVisualFinish.DvdRipple
		or CardEditorVisualFinish.Lightning
		or CardEditorVisualFinish.Flame => true,
		_ => false
	};

	private static CardEditorBorderFoilOverlay? FindOverlay(Node overlayContainer)
	{
		for (int i = 0; i < overlayContainer.GetChildCount(); i++)
		{
			if (overlayContainer.GetChild(i) is CardEditorBorderFoilOverlay overlay)
			{
				return overlay;
			}
		}
		return null;
	}

	private static void RemoveOverlay(Node overlayContainer, CardEditorBorderFoilOverlay? overlay)
	{
		if (overlay == null)
		{
			return;
		}

		if (overlay.GetParent() == overlayContainer)
		{
			overlayContainer.RemoveChild(overlay);
		}

		overlay.QueueFree();
	}
}

internal static class CardEditorTextureLoader
{
	public static ImageTexture? LoadFromPck(string resPath)
	{
		Image img = Image.LoadFromFile(resPath);
		if (img == null || img.IsEmpty())
			return null;
		return ImageTexture.CreateFromImage(img);
	}

	public static float P(Dictionary<string, float>? p, string key, float def)
	{
		return p != null && p.TryGetValue(key, out float v) ? v : def;
	}

	// Returns a tile-scale factor so that holo mask patterns maintain the same
	// physical pixel density regardless of which portrait node is used.
	// _portrait is scale=(1,1) at ~250x190px.
	// _ancientPortrait is scale=(0.5,0.5) with large offsets → visual ~449x632px.
	private const float RefPortraitW = 250f;
	private const float RefPortraitH = 190f;
	public static float HoloTileScale(TextureRect portrait)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
			return 1f;

		Vector2 visual = portrait.GetGlobalRect().Size;
		float vw = visual.X;
		float vh = visual.Y;
		if (vw <= 0 || vh <= 0)
		{
			Vector2 sc = portrait.Scale;
			Vector2 sz = portrait.Size;
			vw = sz.X * (Mathf.Abs(sc.X) > 0 ? Mathf.Abs(sc.X) : 1f);
			vh = sz.Y * (Mathf.Abs(sc.Y) > 0 ? Mathf.Abs(sc.Y) : 1f);
		}

		if (vw <= 0 || vh <= 0) return 1f;

		float sx = vw / RefPortraitW;
		float sy = vh / RefPortraitH;
		if (sx <= 0 || sy <= 0) return 1f;

		return Mathf.Sqrt(sx * sy);
	}
}


internal static class CardEditorPrismaticBandGlareController
{
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");

	private static Shader? _shader;
	private static Texture2D? _grainTex;

	private static Shader? GetShader()
	{
		_shader ??= new Shader { Code = CardEditorPrismaticBandGlareOverlay.ShaderCode };
		return _shader;
	}

	private static Texture2D? GetGrainTex()
	{
		_grainTex ??= CardEditorTextureLoader.LoadFromPck("res://mods/card_editor/dev/pokemon_holo_grain.webp");
		return _grainTex;
	}

	public static void Sync(NCard card, bool enabled, bool fullArt, Dictionary<string, float>? fp = null)
	{
		TextureRect? portrait = PortraitRef(card);
		TextureRect? ancientPortrait = AncientPortraitRef(card);
		Shader? shader = GetShader();
		if (shader == null)
		{
			return;
		}

		ClearPortraitMaterial(portrait, shader);
		ClearPortraitMaterial(ancientPortrait, shader);
		SyncPortrait(card, shader, enabled, fullArt, fp);
	}

	private static void SyncPortrait(NCard card, Shader shader, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		ColorRect? overlay = CardEditorArtFinishOverlayNodes.SyncOverlay(card, "PrismaticBandGlare", enabled, fullArt);
		if (overlay == null)
		{
			return;
		}

		ShaderMaterial material;
		if (overlay.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == shader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = shader };
			overlay.Material = material;
		}

		material.SetShaderParameter("intensity", CardEditorTextureLoader.P(fp, "strength", fullArt ? 1.0f : 0.92f));
		material.SetShaderParameter("lower_fade_start", fullArt ? 0.90f : 0.74f);
		material.SetShaderParameter("lower_fade_end", fullArt ? 0.998f : 0.94f);
		material.SetShaderParameter("lower_mask_min", fullArt ? 0.40f : 0.24f);
		material.SetShaderParameter("band_strength", fullArt ? 0.24f : 0.22f);
		material.SetShaderParameter("glare_strength", CardEditorTextureLoader.P(fp, "glareStrength", fullArt ? 0.28f : 0.24f));
		material.SetShaderParameter("sweep_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("line_count", CardEditorTextureLoader.P(fp, "lineCount", 22.0f));
		material.SetShaderParameter("line_width", CardEditorTextureLoader.P(fp, "lineWidth", 0.10f));
		material.SetShaderParameter("inset_px", fullArt ? 1.0f : 1.5f);
		material.SetShaderParameter("corner_radius_px", 0.0f);
		material.SetShaderParameter("corner_softness_px", 1.5f);
		CardEditorArtFinishOverlayNodes.ApplyArtSpace(material, fullArt);
	}

	private static void ClearPortraitMaterial(TextureRect? portrait, Shader shader)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (portrait.Material is not ShaderMaterial existing)
		{
			return;
		}

		Shader? existingShader = existing.Shader;
		if (existingShader == shader
			|| string.Equals(existingShader?.ResourcePath, "res://mods/card_editor/dev/holo_card_codes_preview.gdshader", StringComparison.Ordinal))
		{
			portrait.Material = null;
		}
	}
}


internal static class CardEditorCardFinishOverlayController
{
	private static readonly AccessTools.FieldRef<NCard, Node> OverlayContainerRef =
		AccessTools.FieldRefAccess<NCard, Node>("_overlayContainer");

	public static void Sync(NCard card)
        {
                if (card == null)
                {
                        return;
                }

                CardModel? model = card.Model;
                if (model == null)
                {
                        return;
                }

                Node? overlayContainer = OverlayContainerRef(card);
                if (overlayContainer == null)
                {
                        return;
                }

                CardEditorVisualFinish desiredFinish = CardEditorCardFinishResolver.GetDesiredFinish(model);
                Dictionary<string, float>? finishParams = CardEditorCardFinishResolver.GetDesiredFinishParams(model);
                string? customFinishId = CardEditorCardFinishResolver.GetDesiredCustomFinishId(model);
                Dictionary<string, string>? customFinishParams = CardEditorCardFinishResolver.GetDesiredCustomFinishParams(model);
                CardEditorVisualFinish borderFinish = CardEditorCardFinishResolver.GetDesiredBorderFinish(model);
                Dictionary<string, float>? borderFinishParams = CardEditorCardFinishResolver.GetDesiredBorderFinishParams(model);
				bool hasCustomFinish = !string.IsNullOrWhiteSpace(customFinishId) && CardEditorCustomFoilRegistry.TryGet(customFinishId, out _);
				if (hasCustomFinish)
				{
					desiredFinish = CardEditorVisualFinish.None;
					finishParams = null;
				}
                CardEditorRainbowFoilOverlay? overlay = FindOverlay(overlayContainer);
				CardEditorPrismaticBandGlareOverlay? prismaticOverlay = FindPrismaticOverlay(overlayContainer);
                
                bool fullArt = CardEditorFullArtRefresh.ShouldBeFullArt(model);

                CardEditorRainbowGlitterArtController.Sync(card, desiredFinish == CardEditorVisualFinish.RainbowGlitterArt, fullArt, finishParams);
                CardEditorPrismaticBandGlareController.Sync(card, desiredFinish == CardEditorVisualFinish.PrismaticBandGlare, fullArt, finishParams);
				CardEditorPurpleWavesOceanController.Sync(card, desiredFinish == CardEditorVisualFinish.PurpleWavesOcean, fullArt, finishParams);
				CardEditorProceduralArtFinishController.Sync(card, desiredFinish, fullArt, finishParams);
				CardEditorBaseGameOverlayFinishController.Sync(card, desiredFinish);
				CardEditorCustomShaderFoilController.Sync(card, hasCustomFinish ? customFinishId : null, fullArt, customFinishParams);
				CardEditorRainbowRareFoilArtController.Sync(card, desiredFinish == CardEditorVisualFinish.RainbowRareFoil, fullArt, finishParams);
                CardEditorBorderFoilOverlayController.Sync(overlayContainer, borderFinish, fullArt, borderFinishParams);

                if (desiredFinish == CardEditorVisualFinish.None)
                {
                        RemoveOverlay(overlayContainer, overlay);
						RemovePrismaticOverlay(overlayContainer, prismaticOverlay);
                        return;
                }

					if (desiredFinish == CardEditorVisualFinish.PrismaticBandGlare)
					{
						RemoveOverlay(overlayContainer, overlay);
						RemovePrismaticOverlay(overlayContainer, prismaticOverlay);
						return;
				}

				if (desiredFinish == CardEditorVisualFinish.RainbowRareFoil)
				{
						RemoveOverlay(overlayContainer, overlay);
						RemovePrismaticOverlay(overlayContainer, prismaticOverlay);
						return;
				}

				RemoveOverlay(overlayContainer, overlay);
        }

                private static CardEditorRainbowFoilOverlay? FindOverlay(Node overlayContainer)
        {
                for (int i = 0; i < overlayContainer.GetChildCount(); i++)
                {
                        if (overlayContainer.GetChild(i) is CardEditorRainbowFoilOverlay overlay)
                        {
                                return overlay;
                        }
                }

                return null;
        }

			private static CardEditorPrismaticBandGlareOverlay? FindPrismaticOverlay(Node overlayContainer)
			{
				for (int i = 0; i < overlayContainer.GetChildCount(); i++)
				{
					if (overlayContainer.GetChild(i) is CardEditorPrismaticBandGlareOverlay overlay)
					{
						return overlay;
					}
				}

				return null;
			}

	private static void RemoveOverlay(Node overlayContainer, CardEditorRainbowFoilOverlay? overlay)
	{
		if (overlay == null)
		{
			return;
		}

		if (overlay.GetParent() == overlayContainer)
		{
			overlayContainer.RemoveChild(overlay);
		}

		overlay.QueueFree();
	}

	private static void RemovePrismaticOverlay(Node overlayContainer, CardEditorPrismaticBandGlareOverlay? overlay)
	{
		if (overlay == null)
		{
			return;
		}

		if (overlay.GetParent() == overlayContainer)
		{
			overlayContainer.RemoveChild(overlay);
		}

		overlay.QueueFree();
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class NCard_Reload_CardFinishOverlay_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorCardFinishOverlayController.Sync(__instance);
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class NCard_UpdateVisuals_CardFinishOverlay_Patch
{
	public static void Postfix(NCard __instance)
	{
		try
		{
			CardEditorCardFinishOverlayController.Sync(__instance);
		}
		catch
		{
		}
	}
}
