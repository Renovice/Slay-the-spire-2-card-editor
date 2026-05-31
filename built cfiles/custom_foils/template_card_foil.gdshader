shader_type canvas_item;

uniform float intensity : hint_range(0.0, 3.0, 0.05) = 1.0;
uniform float opacity : hint_range(0.0, 1.0, 0.01) = 0.65;
uniform float speed : hint_range(0.0, 5.0, 0.05) = 1.0;
uniform float pattern_scale : hint_range(0.25, 8.0, 0.05) = 1.0;
uniform float card_effect_pattern_scale = 1.0;
uniform vec4 tint : source_color = vec4(0.9, 0.95, 1.0, 1.0);
uniform vec2 foil_size = vec2(320.0, 446.0);
uniform vec2 card_effect_uv_origin = vec2(0.0, 0.0);
uniform vec2 card_effect_uv_scale = vec2(1.0, 1.0);
uniform vec2 card_effect_rect_size = vec2(320.0, 446.0);

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

void fragment() {
	vec2 uv = (card_effect_uv_origin + UV * card_effect_uv_scale);
	vec2 centered = uv - 0.5;
	centered.x *= card_effect_rect_size.x / max(card_effect_rect_size.y, 1.0);

	float t = TIME * speed;
	vec2 p = centered * (18.0 * pattern_scale * card_effect_pattern_scale);

	float bands = sin((p.x + p.y) * 2.4 + t * 2.0) * 0.5 + 0.5;
	float glint_seed = hash21(floor(p * 2.0));
	float glint = smoothstep(0.92, 1.0, bands) * step(0.72, glint_seed);

	float vignette = smoothstep(0.75, 0.15, length(centered));
	float alpha = clamp((0.12 + glint * 0.88) * vignette * opacity * intensity, 0.0, 1.0);

	COLOR = vec4(tint.rgb * (0.35 + glint * 0.9) * intensity, alpha);
}
