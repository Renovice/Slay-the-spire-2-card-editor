shader_type canvas_item;

uniform float line_strength : hint_range(0.0, 3.0) = 1.0;
uniform float line_width : hint_range(0.001, 0.05) = 0.012;
uniform float line_softness : hint_range(0.0005, 0.03) = 0.004;
uniform float connection_range : hint_range(0.1, 2.0) = 1.3;
uniform float target_length : hint_range(0.0, 1.5) = 0.75;
uniform float target_band : hint_range(0.001, 0.2) = 0.04;
uniform float long_line_strength : hint_range(0.0, 2.0) = 0.5;
uniform float sparkle_strength : hint_range(0.0, 3.0) = 1.0;
uniform float sparkle_power : hint_range(0.2, 4.0) = 1.5;
uniform float sparkle_pulse : hint_range(0.0, 1.0) = 0.4;
uniform float node_jitter : hint_range(0.0, 1.0) = 0.4;
uniform float point_speed : hint_range(0.0, 3.0) = 1.0;
uniform float layer_count : hint_range(1.0, 5.0, 1.0) = 5.0;
uniform float grid_scale : hint_range(0.1, 4.0) = 1.5;
uniform float layer_min_scale : hint_range(0.01, 2.0) = 0.1;
uniform float layer_max_scale : hint_range(1.0, 30.0) = 15.0;
uniform float layer_speed : hint_range(0.0, 3.0) = 1.0;
uniform float background_glow : hint_range(0.0, 1.0) = 0.18;
uniform float color_speed : hint_range(0.0, 5.0) = 2.0;
uniform float color_spread : hint_range(0.0, 2.0) = 1.0;
uniform float brightness : hint_range(0.5, 2.0) = 1.0;
uniform float pastel : hint_range(0.0, 1.0) = 0.0;
uniform float saturation : hint_range(0.0, 1.0) = 1.0;
uniform float hue_shift : hint_range(0.0, 1.0) = 0.0;
uniform float speed : hint_range(0.0, 3.0) = 1.0;
uniform float time_offset : hint_range(0.0, 10.0) = 0.0;
uniform float pattern_scale : hint_range(0.1, 4.0) = 1.0;
uniform vec4 network_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float network_tint_strength : hint_range(0.0, 1.0) = 0.0;

#define LOCAL_TIME (TIME * speed + time_offset)

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
	vec2 uv = (UV - vec2(0.5)) * vec2(320.0 / 446.0, 1.0) * max(pattern_scale, 0.05);
	vec3 cycle = sin(LOCAL_TIME * color_speed * vec3(0.234, 0.324, 0.768)) * 0.4 + 0.6;
	cycle.x += (uv.x + 0.5) * color_spread;
	cycle = mix(cycle, network_color.rgb, clamp(network_tint_strength, 0.0, 1.0));

	vec3 col = pow(max(-uv.y + 0.5, 0.0), 5.0) * background_glow * cycle;
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
	hsv.y = clamp(hsv.y * saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);
	COLOR = vec4(col, 1.0);
}
