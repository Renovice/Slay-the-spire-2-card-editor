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

internal sealed class CardEditorPrismaticBandGlareOverlay : Control
{
	private const string ShaderCode = @"shader_type canvas_item;
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
	float bandA = metallic_band(UV + backgroundShift * 0.35, angle, 22.0, 0.10);
	float bandB = metallic_band(UV - backgroundShift * 0.25, angle, 16.0, 0.14);
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

void fragment()
{
	vec4 tex = texture(TEXTURE, UV);
	float a = tex.a;
	if (a <= 0.001)
	{
		discard;
	}

	float luma = dot(tex.rgb, vec3(0.2126, 0.7152, 0.0722));

	// Lift midtones so most of the art shows vivid color,
	// while truly dark lines (luma near 0) stay dark naturally.
	float lit = clamp(pow(luma, contrast_gamma) * brightness_boost, 0.0, 1.0);

	float t = TIME * motion_speed + time_offset;
	vec2 dir = normalize(gradient_dir);
	vec2 crossDir = normalize(vec2(-dir.y, dir.x));
	float phaseA = dot(UV - vec2(0.5), dir) * gradient_scale * hue_spread;
	float phaseB = dot(UV - vec2(0.5), crossDir) * gradient_scale * 0.45;
	float flow = sin((UV.x + UV.y * 0.8) * 2.3561945 + t * 0.22) * 0.035;
	vec3 rainbowA = 0.5 + 0.5 * cos(vec3(0.0, 2.0, 4.0) + (phaseA + flow + t * hue_speed + hue_offset) * 6.2831853);
	vec3 rainbowB = 0.5 + 0.5 * cos(vec3(0.8, 2.8, 4.8) + (phaseB - t * hue_speed * 0.45 + hue_offset) * 6.2831853);
	vec3 rainbow = mix(rainbowA, rainbowB, 0.32);
	rainbow = mix(rainbow, vec3(1.0), pastel);
	float rl = dot(rainbow, vec3(0.2126, 0.7152, 0.0722));
	rainbow = mix(vec3(rl), rainbow, saturation);
	rainbow = mix(rainbow, color_tint, tint_strength);

	// Foil color: rainbow modulated by lifted luminance.
	vec3 foilDirect = rainbow * lit;

	// Overlay blend: preserves original art structure while tinting.
	vec3 foilOverlay = overlay_blend(tex.rgb, rainbow);

	// Combine: use overlay blend on midtones/lights, direct mul on darks.
	// This keeps dark lines as dark rainbow tints instead of washed out.
	vec3 foilColor = mix(foilDirect, foilOverlay, smoothstep(0.15, 0.55, luma));

	// Add a soft pearly wash so the foil leans pastel instead of neon.
	float pearlMask = smoothstep(0.20, 0.95, lit);
	foilColor = mix(foilColor, mix(foilColor, vec3(1.0), 0.35), pastel * pearlMask * 0.55);

	// Edge detection to keep ink lines extra crisp.
	vec2 px = TEXTURE_PIXEL_SIZE * edge_px;
	float lpx = dot(texture(TEXTURE, UV + vec2(px.x, 0.0)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lnx = dot(texture(TEXTURE, UV - vec2(px.x, 0.0)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lpy = dot(texture(TEXTURE, UV + vec2(0.0, px.y)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float lny = dot(texture(TEXTURE, UV - vec2(0.0, px.y)).rgb, vec3(0.2126, 0.7152, 0.0722));
	float edge = abs(lpx - lnx) + abs(lpy - lny);
	edge = smoothstep(edge_threshold, edge_threshold + 0.10, edge);
	float inkMask = edge * (1.0 - smoothstep(0.08, 0.42, luma));
	foilColor *= mix(1.0, edge_darken, edge);

	// Final blend between original art and holographic foil.
	vec3 outColor = mix(tex.rgb, foilColor, strength);
	outColor = mix(outColor, tex.rgb, inkMask * ink_preserve);

	// Animated sweeping sheen highlight (the glossy flash).
	float sheenPhase = fract(t * sheen_speed);
	float sheenCoord = dot(UV, normalize(vec2(1.0, 1.4)));
	float sheenWave = sheenPhase * 2.5 - 0.75;
	float sheenDist = sheenCoord - sheenWave;
	float sheenGlow = exp(-sheenDist * sheenDist / max(0.001, sheen_width * sheen_width));
	outColor += (rainbow * 0.55 + vec3(1.0) * 0.45) * sheenGlow * sheen_strength * lit;

	// Subtle animated grain for physical card feel.
	float grain = hash12(UV * 320.0 + vec2(t * 0.08, 0.0)) - 0.5;
	outColor += vec3(grain) * grain_strength;

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

		SyncPortrait(portrait, enabled, fullArt, fp);
		SyncPortrait(ancientPortrait, enabled, fullArt, fp);
		SyncTitleBanner(titleBanner, enabled, fullArt, fp);
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
		material.SetShaderParameter("hue_spread", fullArt ? 0.75f : 0.72f);
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("hue_offset", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		material.SetShaderParameter("gradient_dir", new Vector2(1.0f, 0.7f));
		material.SetShaderParameter("gradient_scale", fullArt ? 0.88f : 0.85f);
		material.SetShaderParameter("contrast_gamma", fullArt ? 0.52f : 0.55f);
		material.SetShaderParameter("brightness_boost", CardEditorTextureLoader.P(fp, "brightness", fullArt ? 1.24f : 1.22f));
		material.SetShaderParameter("edge_px", fullArt ? 2.25f : 2.0f);
		material.SetShaderParameter("edge_threshold", fullArt ? 0.04f : 0.05f);
		material.SetShaderParameter("edge_darken", fullArt ? 0.80f : 0.78f);
		material.SetShaderParameter("ink_preserve", fullArt ? 0.78f : 0.82f);
		material.SetShaderParameter("sheen_strength", fullArt ? 0.24f : 0.22f);
		material.SetShaderParameter("sheen_speed", 0.28f);
		material.SetShaderParameter("sheen_width", fullArt ? 0.17f : 0.18f);
		material.SetShaderParameter("grain_strength", fullArt ? 0.006f : 0.005f);
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
uniform float y_offset = 0.25;

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
	vec2 frag = UV * safe_res;
	vec2 uv = (-safe_res + 2.0 * frag) / safe_res.y;

	float t = (TIME * motion_speed) + time_offset;

	// Keep the original framing; widening this makes some portraits miss the water surface (you only see flakes/bubbles).
	uv.y *= 0.5;
	uv.x *= 0.45;
	float zoom = max(0.05, pattern_scale);
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

	vec2 q = UV;
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

		vec4 base = texture(TEXTURE, UV);
		float a = clamp(strength, 0.0, 1.0) * effect_alpha;
		vec3 out_col = mix(base.rgb, clamp(col, vec3(0.0), vec3(1.0)), a);
		// Subtle dithering to reduce 8-bit banding in low-gradient areas.
		float dither = hash(frag + vec2(17.0, 31.0)) - 0.5;
		out_col += dither * (a * 0.003);
		out_col = clamp(out_col, vec3(0.0), vec3(1.0));
		COLOR = vec4(out_col, base.a);
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

		SyncPortrait(portrait, enabled, fullArt, fp);
		SyncPortrait(ancientPortrait, enabled, fullArt, fp);
	}

	private static void SyncPortrait(TextureRect? portrait, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		// Clean up any old overlay nodes from previous versions.
		RemoveOverlay(portrait);

		if (!enabled)
		{
			if (portrait.Material is ShaderMaterial existingFinishMaterial && existingFinishMaterial.Shader == SharedShader)
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

		Vector2 visual = Vector2.Zero;
		if (portrait.Texture != null)
		{
			visual = portrait.Texture.GetSize();
		}
		if (visual.X <= 0.0f || visual.Y <= 0.0f)
		{
			visual = portrait.GetGlobalRect().Size;
		}
		if (visual.X <= 0.0f || visual.Y <= 0.0f)
		{
			Vector2 sc = portrait.Scale;
			Vector2 sz = portrait.Size;
			visual = new Vector2(
				sz.X * (Mathf.Abs(sc.X) > 0 ? Mathf.Abs(sc.X) : 1f),
				sz.Y * (Mathf.Abs(sc.Y) > 0 ? Mathf.Abs(sc.Y) : 1f));
		}
		if (visual.X <= 0.0f || visual.Y <= 0.0f)
		{
			visual = new Vector2(300f, 422f);
		}
		material.SetShaderParameter("rect_size", visual);
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

	private static void RemoveOverlay(TextureRect portrait)
	{
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
uniform float swirl_strength = 4.7;
uniform float line_count = 6.0;
uniform float line_sharpness = 0.46;

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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	float t = TIME * motion_speed + time_offset;
	vec2 uv = (UV - vec2(0.5)) * max(pattern_scale, 0.05);
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
	vec3 out_col = mix(base.rgb, col, blend_amount);
	COLOR = vec4(clamp(out_col, vec3(0.0), vec3(1.0)), base.a);
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	vec2 centered_uv = (UV - vec2(0.5)) * max(pattern_scale, 0.05) + vec2(0.5);
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
	vec3 out_col = mix(base.rgb, res_color, blend_amount);
	COLOR = vec4(clamp(out_col, vec3(0.0), vec3(1.0)), base.a);
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	vec2 local_uv = (UV - vec2(0.5)) * max(pattern_scale, 0.05) + vec2(0.5);
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
	vec3 out_col = mix(base.rgb, col, blend_amount);
	COLOR = vec4(clamp(out_col, vec3(0.0), vec3(1.0)), base.a);
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	vec2 tex_size = 1.0 / max(TEXTURE_PIXEL_SIZE, vec2(0.0001));
	float aspect = tex_size.x / max(tex_size.y, 1.0);
	vec2 uv = (UV - vec2(0.5)) * vec2(aspect, 1.0) * max(pattern_scale, 0.05);

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
	vec3 out_col = mix(base.rgb, col, blend_amount);
COLOR = vec4(clamp(out_col, vec3(0.0), vec3(1.0)), base.a);
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	vec2 tex_size = 1.0 / max(TEXTURE_PIXEL_SIZE, vec2(0.0001));
	float aspect = tex_size.x / max(tex_size.y, 1.0);
	vec2 p = (UV * 2.0 - 1.0) * vec2(aspect, 1.0) * max(pattern_scale, 0.05);
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

	vec2 px = floor(UV / max(TEXTURE_PIXEL_SIZE, vec2(0.0001)));
	float n = hash21(px + vec2(floor(LOCAL_TIME * 24.0)));
	col.rgb += (vec3(n) * 2.0 - 1.0) * noise_strength;
	col.rgb = pow(max(col.rgb, vec3(0.0)), vec3(1.0 / 1.5));
	col.rgb *= brightness;

	vec3 hsv = rgb2hsv(max(col.rgb, vec3(0.0)));
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col.rgb = hsv2rgb(hsv);

	float effect_mask = clamp(0.20 + ff * 16.0 + abs(col.a) * 0.18, 0.0, 1.0);
	vec3 ripple_col = clamp(base.rgb + col.rgb, vec3(0.0), vec3(1.0));
	vec3 out_col = mix(base.rgb, ripple_col, clamp(strength * effect_mask, 0.0, 1.0));
	COLOR = vec4(clamp(out_col, vec3(0.0), vec3(1.0)), base.a);
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
uniform float bolt_width = 0.045;
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001) {
		discard;
	}

	vec2 tex_size = 1.0 / max(TEXTURE_PIXEL_SIZE, vec2(0.0001));
	float aspect = tex_size.x / max(tex_size.y, 1.0);
	vec2 uv = (2.0 * UV - 1.0) * vec2(aspect, 1.0) * max(pattern_scale, 0.05);

	float bend = 2.0 * fbm(uv + vec2(LOCAL_TIME, LOCAL_TIME * 0.37)) - 1.0;
	uv.x += bend;
	float dist = abs(uv.x);
	float width = max(bolt_width, 0.001);
	float core = 1.0 - smoothstep(0.0, width, dist);
	float aura = exp(-dist * 10.0 / max(glow, 0.01));
	float flicker_sample = mix(1.0, 0.25 + hash12(vec2(floor(LOCAL_TIME * 28.0), floor(LOCAL_TIME * 7.0))) * 0.95, clamp(flicker, 0.0, 1.0));

	vec3 col = effect_color * (core * 2.2 + aura * glow * 0.45) * brightness * flicker_sample;
	vec3 hsv = rgb2hsv(max(col, vec3(0.0)));
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * color_saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);

	float mask = clamp(core + aura * 0.65, 0.0, 1.0);
	vec3 out_col = mix(base.rgb, clamp(base.rgb + col, vec3(0.0), vec3(1.0)), clamp(strength * mask, 0.0, 1.0));
	COLOR = vec4(out_col, base.a);
}";

	private static readonly Shader WhirlpoolShader = new() { Code = WhirlpoolShaderCode };
	private static readonly Shader MiasmaShader = new() { Code = MiasmaShaderCode };
	private static readonly Shader AuroraShader = new() { Code = AuroraShaderCode };
	private static readonly Shader ConstellationShader = new() { Code = ConstellationShaderCode };
	private static readonly Shader RippleShader = new() { Code = RippleShaderCode };
	private static readonly Shader LightningShader = new() { Code = LightningShaderCode };
	private static readonly AccessTools.FieldRef<NCard, TextureRect> PortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> AncientPortraitRef =
		AccessTools.FieldRefAccess<NCard, TextureRect>("_ancientPortrait");

	public static void Sync(NCard card, CardEditorVisualFinish finish, bool fullArt, Dictionary<string, float>? fp = null)
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

		Shader? shader = finish switch
		{
			CardEditorVisualFinish.Whirlpool => WhirlpoolShader,
			CardEditorVisualFinish.Miasma => MiasmaShader,
			CardEditorVisualFinish.Aurora => AuroraShader,
			CardEditorVisualFinish.Constellation => ConstellationShader,
			CardEditorVisualFinish.DvdRipple => RippleShader,
			CardEditorVisualFinish.Lightning => LightningShader,
			_ => null
		};

		SyncPortrait(portrait, shader, finish, fullArt, fp);
		SyncPortrait(ancientPortrait, shader, finish, fullArt, fp);
	}

	private static void SyncPortrait(TextureRect? portrait, Shader? desiredShader, CardEditorVisualFinish finish, bool fullArt, Dictionary<string, float>? fp)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		if (desiredShader == null)
		{
			if (portrait.Material is ShaderMaterial existing && IsManagedShader(existing.Shader))
			{
				portrait.Material = null;
			}
			return;
		}

		ShaderMaterial material;
		if (portrait.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == desiredShader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = desiredShader };
			portrait.Material = material;
		}

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
	}

	private static bool IsManagedShader(Shader? shader)
		=> shader == WhirlpoolShader || shader == MiasmaShader || shader == AuroraShader || shader == ConstellationShader || shader == RippleShader || shader == LightningShader;

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
		material.SetShaderParameter("line_sharpness", CardEditorTextureLoader.P(fp, "lineSharpness", 0.46f));
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
		material.SetShaderParameter("strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.68f : 0.62f));
		material.SetShaderParameter("aurora_color", new Vector3(
			CardEditorTextureLoader.P(fp, "auroraR", 0.84f),
			CardEditorTextureLoader.P(fp, "auroraG", 0.84f),
			CardEditorTextureLoader.P(fp, "auroraB", 0.90f)));
		material.SetShaderParameter("background_color1", new Vector3(
			CardEditorTextureLoader.P(fp, "background1R", 0.05f),
			CardEditorTextureLoader.P(fp, "background1G", 0.10f),
			CardEditorTextureLoader.P(fp, "background1B", 0.20f)));
		material.SetShaderParameter("background_color2", new Vector3(
			CardEditorTextureLoader.P(fp, "background2R", 0.10f),
			CardEditorTextureLoader.P(fp, "background2G", 0.05f),
			CardEditorTextureLoader.P(fp, "background2B", 0.20f)));
		material.SetShaderParameter("aurora_intensity", CardEditorTextureLoader.P(fp, "auroraIntensity", 1.8f));
		material.SetShaderParameter("star_brightness", CardEditorTextureLoader.P(fp, "starBrightness", 0.8f));
		material.SetShaderParameter("star_density", CardEditorTextureLoader.P(fp, "starDensity", 1.0f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("pastel", CardEditorTextureLoader.P(fp, "pastel", 0.0f));
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("pattern_scale", CardEditorTextureLoader.P(fp, "patternScale", 1.0f));
		material.SetShaderParameter("projection_bend", CardEditorTextureLoader.P(fp, "projectionBend", 0.4f));
		material.SetShaderParameter("horizon", CardEditorTextureLoader.P(fp, "horizon", 0.0f));
		material.SetShaderParameter("reflection_strength", CardEditorTextureLoader.P(fp, "reflectionStrength", 0.65f));
		material.SetShaderParameter("rotation_strength", CardEditorTextureLoader.P(fp, "rotationStrength", 0.2f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
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
		material.SetShaderParameter("glow", CardEditorTextureLoader.P(fp, "glow", 1.4f));
		material.SetShaderParameter("brightness", CardEditorTextureLoader.P(fp, "brightness", 1.0f));
		material.SetShaderParameter("flicker", CardEditorTextureLoader.P(fp, "flicker", 0.55f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
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

		SyncPortrait(portrait, definition, fullArt, customParams);
		SyncPortrait(ancientPortrait, definition, fullArt, customParams);
	}

	private static void SyncPortrait(TextureRect? portrait, CardEditorCustomFoilDefinition? definition, bool fullArt, Dictionary<string, string>? customParams)
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

		Vector2 rectSize = portrait.Size;
		CardEditorCustomFoilRegistry.ApplyParameters(material, definition, fullArt, customParams, rectSize);
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
		_shader ??= GD.Load<Shader>("res://mods/card_editor/dev/holo_card_codes_preview.gdshader");
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
		SyncPortrait(portrait, enabled, fullArt, fp);
		SyncPortrait(ancientPortrait, enabled, fullArt, fp);
	}

	private static void SyncPortrait(TextureRect? portrait, bool enabled, bool fullArt, Dictionary<string, float>? fp)
	{
		if (portrait == null || !GodotObject.IsInstanceValid(portrait))
		{
			return;
		}

		Shader? shader = GetShader();

		if (!enabled)
		{
			if (portrait.Material is ShaderMaterial existing && existing.Shader == shader)
			{
				portrait.Material = null;
			}
			return;
		}

		if (shader == null)
		{
			return;
		}

		ShaderMaterial material;
		if (portrait.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == shader)
		{
			material = existingMaterial;
		}
		else
		{
			material = new ShaderMaterial { Shader = shader };
			portrait.Material = material;
		}

		Texture2D? grain = GetGrainTex();
		if (grain != null)
		{
			material.SetShaderParameter("grain_tex", grain);
		}

		material.SetShaderParameter("corner_radius_px", 0.0f);
		material.SetShaderParameter("inset_px", 0.0f);
		material.SetShaderParameter("motion_speed", CardEditorTextureLoader.P(fp, "speed", 1.0f));
		material.SetShaderParameter("time_offset", CardEditorTextureLoader.P(fp, "timeOffset", 0.0f));
		material.SetShaderParameter("foil_strength", CardEditorTextureLoader.P(fp, "strength", fullArt ? 0.62f : 0.58f));
		material.SetShaderParameter("soft_light_strength", 0.5f);
		material.SetShaderParameter("glare_strength", CardEditorTextureLoader.P(fp, "glareStrength", fullArt ? 0.38f : 0.34f));
		material.SetShaderParameter("grain_strength", 0.22f);
		material.SetShaderParameter("metallic_strength", CardEditorTextureLoader.P(fp, "metallicStrength", 0.42f));
		material.SetShaderParameter("shadow_strength", 0.34f);
		material.SetShaderParameter("hue_shift", CardEditorTextureLoader.P(fp, "hueShift", 0.0f));
		material.SetShaderParameter("color_saturation", CardEditorTextureLoader.P(fp, "saturation", 1.0f));
		material.SetShaderParameter("color_tint", new Vector3(
			CardEditorTextureLoader.P(fp, "tintR", 1.0f),
			CardEditorTextureLoader.P(fp, "tintG", 1.0f),
			CardEditorTextureLoader.P(fp, "tintB", 1.0f)));
		material.SetShaderParameter("tint_strength", CardEditorTextureLoader.P(fp, "tintStrength", 0.0f));
		Vector2 size = portrait.Size;
		material.SetShaderParameter("rect_size", size.X > 0 && size.Y > 0 ? size : new Vector2(300f, 422f));
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

                if (desiredFinish != CardEditorVisualFinish.RainbowRareFoil)
                {
                        RemoveOverlay(overlayContainer, overlay);
                        return;
                }

                if (overlay == null)
                {
                        overlay = new CardEditorRainbowFoilOverlay();
                        overlayContainer.AddChild(overlay);
                }

                overlay.ApplyStyle(fullArt);
                overlay.Show();
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
