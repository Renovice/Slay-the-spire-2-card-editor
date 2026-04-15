shader_type canvas_item;

render_mode blend_mix;

uniform sampler2D grain_tex;

uniform vec2 rect_size = vec2(320.0, 446.0);
uniform float corner_radius_px = 32.0;
uniform float corner_softness_px = 1.5;
uniform float inset_px = 1.5;

uniform float motion_speed = 1.0;
uniform float time_offset = 0.0;
uniform float foil_strength = 0.58;
uniform float soft_light_strength = 0.5;
uniform float glare_strength = 0.34;
uniform float grain_strength = 0.22;
uniform float metallic_strength = 0.42;
uniform float shadow_strength = 0.34;
uniform float hue_shift = 0.0;
uniform float color_saturation = 1.0;
uniform vec3 color_tint = vec3(1.0, 1.0, 1.0);
uniform float tint_strength = 0.0;

float sd_round_rect(vec2 p, vec2 he, float r)
{
	vec2 q = abs(p) - he + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

vec3 screen_blend(vec3 base_col, vec3 blend_col)
{
	return 1.0 - (1.0 - base_col) * (1.0 - blend_col);
}

float overlay_channel(float base_col, float blend_col)
{
	return base_col < 0.5 ? 2.0 * base_col * blend_col : 1.0 - 2.0 * (1.0 - base_col) * (1.0 - blend_col);
}

vec3 overlay_blend(vec3 base_col, vec3 blend_col)
{
	return vec3(
		overlay_channel(base_col.r, blend_col.r),
		overlay_channel(base_col.g, blend_col.g),
		overlay_channel(base_col.b, blend_col.b)
	);
}

float hard_light_channel(float base_col, float blend_col)
{
	return blend_col < 0.5 ? 2.0 * base_col * blend_col : 1.0 - 2.0 * (1.0 - base_col) * (1.0 - blend_col);
}

vec3 hard_light_blend(vec3 base_col, vec3 blend_col)
{
	return vec3(
		hard_light_channel(base_col.r, blend_col.r),
		hard_light_channel(base_col.g, blend_col.g),
		hard_light_channel(base_col.b, blend_col.b)
	);
}

float soft_light_channel(float base_col, float blend_col)
{
	if (blend_col <= 0.5)
	{
		return base_col - (1.0 - 2.0 * blend_col) * base_col * (1.0 - base_col);
	}

	float d = base_col <= 0.25 ? ((16.0 * base_col - 12.0) * base_col + 4.0) * base_col : sqrt(base_col);
	return base_col + (2.0 * blend_col - 1.0) * (d - base_col);
}

vec3 soft_light_blend(vec3 base_col, vec3 blend_col)
{
	return vec3(
		soft_light_channel(base_col.r, blend_col.r),
		soft_light_channel(base_col.g, blend_col.g),
		soft_light_channel(base_col.b, blend_col.b)
	);
}

vec3 color_dodge_blend(vec3 base_col, vec3 blend_col)
{
	return clamp(base_col / max(vec3(0.001), 1.0 - blend_col), 0.0, 1.0);
}

vec3 apply_levels(vec3 color, float brightness, float contrast, float saturation)
{
	color *= brightness;
	color = (color - 0.5) * contrast + 0.5;
	float lum = dot(color, vec3(0.299, 0.587, 0.114));
	return mix(vec3(lum), color, saturation);
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
	vec4 base = texture(TEXTURE, UV);
	if (base.a <= 0.001)
	{
		discard;
	}

	vec2 safe_size = max(rect_size, vec2(1.0));
	vec2 half_size = (safe_size - vec2(inset_px) * 2.0) * 0.5;
	vec2 local = UV * safe_size - safe_size * 0.5;
	float sd = sd_round_rect(local, half_size, corner_radius_px);
	float corner_aa = max(corner_softness_px, fwidth(sd));
	float corner_mask = 1.0 - smoothstep(0.0, corner_aa, sd);
	if (corner_mask <= 0.001)
	{
		discard;
	}

	float t = TIME * motion_speed + time_offset;
	vec2 pointer = vec2(
		0.5 + sin(t * 0.83) * 0.24,
		0.34 + cos(t * 0.61) * 0.18
	);
	vec2 background_shift = vec2(
		0.44 + sin(t * 0.53) * 0.14,
		0.36 + cos(t * 0.47 + 0.8) * 0.12
	);

	vec2 grain_uv = UV * vec2(11.5, 8.0) + vec2(t * 0.015, -t * 0.01);
	float grain = texture(grain_tex, grain_uv).r;
	vec3 grain_layer = vec3(grain);

	float sunpillar_coord = UV.y * 2.0 + background_shift.y * 0.85 + hue_shift * 6.0;
	vec3 sunpillar = sunpillar_gradient(sunpillar_coord);
	float sl = dot(sunpillar, vec3(0.299, 0.587, 0.114));
	sunpillar = mix(vec3(sl), sunpillar, color_saturation);

	float angle = radians(133.0);
	float metallic_main = metallic_band(UV + background_shift * 0.35, angle, 22.0, 0.10);
	float metallic_alt = metallic_band(UV - background_shift * 0.25, angle, 16.0, 0.14);
	vec3 metallic_color = mix(vec3(0.055, 0.082, 0.18), vec3(0.055, 0.082, 0.18) + vec3(0.705, 0.818, 0.74) * metallic_strength, metallic_main);
	vec3 metallic_color_alt = mix(vec3(0.04, 0.06, 0.16), vec3(0.04, 0.06, 0.16) + vec3(0.66, 0.82, 0.79) * metallic_strength, metallic_alt);

	float pointer_dist = distance(UV, pointer);
	float radial_shadow = smoothstep(0.10, 1.05, pointer_dist);
	vec3 shadow_layer = vec3(1.0 - radial_shadow * shadow_strength);

	vec3 shine_main = screen_blend(grain_layer * grain_strength + sunpillar, sunpillar);
	shine_main = hard_light_blend(shine_main, metallic_color);
	shine_main = overlay_blend(shine_main, shadow_layer);
	shine_main = apply_levels(shine_main, 0.82, 2.7, 0.72);

	vec3 shine_alt = screen_blend(grain_layer * (grain_strength * 0.8) + sunpillar * 0.85, sunpillar);
	shine_alt = hard_light_blend(shine_alt, metallic_color_alt);
	shine_alt = apply_levels(shine_alt, 1.0, 2.3, 1.4);

	vec3 dodged = color_dodge_blend(base.rgb, shine_main * foil_strength);
	vec3 with_shine = mix(base.rgb, dodged, foil_strength);
	vec3 with_soft = mix(with_shine, soft_light_blend(with_shine, shine_alt), soft_light_strength);

	float glare_falloff = 1.0 - smoothstep(0.0, 0.78, pointer_dist);
	vec3 glare_color = mix(vec3(0.10, 0.11, 0.14), vec3(1.0, 0.98, 0.92), pow(glare_falloff, 1.45));
	vec3 with_glare = mix(with_soft, hard_light_blend(with_soft, glare_color), glare_strength * glare_falloff);

	float foil_mask = clamp((dot(base.rgb, vec3(0.299, 0.587, 0.114)) * 0.55 + 0.35) * corner_mask, 0.0, 1.0);
	vec3 out_color = mix(base.rgb, with_glare, foil_mask);
	out_color = mix(out_color, out_color * color_tint, tint_strength * foil_mask);

	COLOR = vec4(out_color * corner_mask, base.a * corner_mask);
}