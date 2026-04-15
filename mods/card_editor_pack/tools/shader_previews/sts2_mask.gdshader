shader_type canvas_item;

render_mode blend_mix;

uniform vec2 rect_size = vec2(320.0, 446.0);
uniform float corner_radius_px = 32.0;
uniform float corner_softness_px = 1.5;
uniform float inset_px = 1.5;

float sd_round_rect(vec2 p, vec2 he, float r)
{
	vec2 q = abs(p) - he + vec2(r);
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - r;
}

float card_mask(vec2 uv)
{
	vec2 safe_size = max(rect_size, vec2(1.0));
	vec2 half_size = (safe_size - vec2(inset_px) * 2.0) * 0.5;
	vec2 local = uv * safe_size - safe_size * 0.5;
	float sd = sd_round_rect(local, half_size, corner_radius_px);
	float aa = max(corner_softness_px, fwidth(sd));
	return 1.0 - smoothstep(0.0, aa, sd);
}

void fragment()
{
	float m = card_mask(UV);
	if (m <= 0.001)
	{
		discard;
	}
	COLOR = vec4(1.0, 1.0, 1.0, m);
}

