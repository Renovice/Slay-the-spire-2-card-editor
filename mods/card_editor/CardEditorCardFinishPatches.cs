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

                CardModel? model = card?.Model;
                if (model == null)
                {
                        return;
                }

                Node? overlayContainer = OverlayContainerRef(card!);
                if (overlayContainer == null)
                {
                        return;
                }

                CardEditorVisualFinish desiredFinish = CardEditorCardFinishResolver.GetDesiredFinish(model);
                Dictionary<string, float>? finishParams = CardEditorCardFinishResolver.GetDesiredFinishParams(model);
                CardEditorRainbowFoilOverlay? overlay = FindOverlay(overlayContainer);
				CardEditorPrismaticBandGlareOverlay? prismaticOverlay = FindPrismaticOverlay(overlayContainer);
                
                bool fullArt = CardEditorFullArtRefresh.ShouldBeFullArt(model);

                CardEditorRainbowGlitterArtController.Sync(card, desiredFinish == CardEditorVisualFinish.RainbowGlitterArt, fullArt, finishParams);
                CardEditorPrismaticBandGlareController.Sync(card, desiredFinish == CardEditorVisualFinish.PrismaticBandGlare, fullArt, finishParams);
				CardEditorPurpleWavesOceanController.Sync(card, desiredFinish == CardEditorVisualFinish.PurpleWavesOcean, fullArt, finishParams);

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
