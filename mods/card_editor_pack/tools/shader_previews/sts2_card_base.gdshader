shader_type canvas_item;

render_mode blend_mix;

uniform vec2 rect_size = vec2(320.0, 446.0);
uniform float corner_radius_px = 32.0;
uniform float corner_softness_px = 1.5;
uniform float inset_px = 1.5;

uniform float border_px = 6.0;
uniform vec3 border_color = vec3(0.20, 0.16, 0.12);
uniform vec3 base_color_top = vec3(0.12, 0.10, 0.11);
uniform vec3 base_color_bottom = vec3(0.09, 0.07, 0.08);
uniform float vignette_strength = 0.35;
uniform float highlight_strength = 0.18;

float sd_round_rect(vec2 p, vec2 he, float r)
{
	vec2 q = abs(p) - he + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

void fragment()
{
	vec2 safe_size = max(rect_size, vec2(1.0));
	vec2 half_size = (safe_size - vec2(inset_px) * 2.0) * 0.5;
	vec2 local = UV * safe_size - safe_size * 0.5;

	float sd_outer = sd_round_rect(local, half_size, corner_radius_px);
	float aa = max(corner_softness_px, fwidth(sd_outer));
	float outer_mask = 1.0 - smoothstep(0.0, aa, sd_outer);
	if (outer_mask <= 0.001)
	{
		discard;
	}

	vec2 inner_half = max(vec2(1.0), half_size - vec2(border_px));
	float inner_r = max(0.0, corner_radius_px - border_px);
	float sd_inner = sd_round_rect(local, inner_half, inner_r);
	float inner_mask = 1.0 - smoothstep(0.0, aa, sd_inner);

	float border_mask = clamp(outer_mask - inner_mask, 0.0, 1.0);

	vec3 body = mix(base_color_top, base_color_bottom, smoothstep(0.0, 1.0, UV.y));

	float d = distance(UV, vec2(0.5, 0.55));
	float vig = smoothstep(0.15, 0.95, d);
	body *= (1.0 - vig * vignette_strength);

	float hl = 1.0 - smoothstep(0.0, 0.75, distance(UV, vec2(0.22, 0.20)));
	body += vec3(1.0) * hl * highlight_strength;

	vec3 col = mix(body, border_color, border_mask);
	COLOR = vec4(clamp(col, vec3(0.0), vec3(1.0)), outer_mask);
}

