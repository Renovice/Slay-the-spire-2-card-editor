shader_type canvas_item;

uniform vec4 aurora_color : source_color = vec4(0.84, 0.84, 0.90, 1.0);
uniform vec4 background_color1 : source_color = vec4(0.05, 0.10, 0.20, 1.0);
uniform vec4 background_color2 : source_color = vec4(0.10, 0.05, 0.20, 1.0);
uniform float aurora_intensity : hint_range(0.0, 5.0) = 1.8;
uniform float star_brightness : hint_range(0.0, 1.0) = 0.8;
uniform float star_density : hint_range(0.0, 3.0) = 1.0;
uniform float brightness : hint_range(0.5, 2.0) = 1.0;
uniform float pastel : hint_range(0.0, 1.0) = 0.0;
uniform float saturation : hint_range(0.0, 1.0) = 1.0;
uniform float hue_shift : hint_range(0.0, 1.0) = 0.0;
uniform float speed : hint_range(0.0, 3.0) = 1.0;
uniform float time_offset : hint_range(0.0, 10.0) = 0.0;
uniform float pattern_scale : hint_range(0.1, 4.0) = 1.0;
uniform float horizon : hint_range(-1.0, 1.0) = 0.0;
uniform float reflection_strength : hint_range(0.0, 1.0) = 0.65;
uniform float rotation_strength : hint_range(0.0, 1.0) = 0.2;

#define PI 3.14159265358979323846264
#define LOCAL_TIME (TIME * speed + time_offset)

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
		col2.rgb = aurora_color.rgb * color_variation * rzt;
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
	return mix(background_color1.rgb, background_color2.rgb, sd) * 0.63;
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
	vec2 local_uv = (UV - vec2(0.5)) * max(pattern_scale, 0.05) + vec2(0.5);
	vec2 sphere_uv = clamp(local_uv, vec2(0.0), vec2(1.0));
	float theta = clamp(sphere_uv.y + horizon * 0.15, 0.0, 1.0) * PI;
	float phi = (sphere_uv.x - 0.5) * 2.0 * PI;
	vec3 rd = vec3(sin(theta) * sin(phi), sin(theta) * cos(phi), cos(theta));
	rd.yz = mm2(0.4) * rd.yz;
	rd.xz = mm2(sin(LOCAL_TIME * 0.05) * rotation_strength) * rd.xz;

	vec3 ro = vec3(0.0, 0.0, -6.7);
	vec2 pseudo_frag = sphere_uv * (1.0 / max(SCREEN_PIXEL_SIZE, vec2(0.0001)));
	float fade = smoothstep(0.0, 0.01, abs(rd.y)) * 0.1 + 0.9;
	vec3 col = bg(rd) * fade;

	if (rd.y > 0.0) {
		vec4 aur = smoothstep(vec4(0.0), vec4(1.5), aurora(ro, rd, pseudo_frag)) * fade;
		col += stars(rd);
		col = col * (1.0 - aur.a) + aur.rgb;
	} else {
		vec3 rrd = rd;
		rrd.y = abs(rrd.y);
		col = bg(rrd) * fade * 0.6;
		vec4 aur = smoothstep(vec4(0.0), vec4(2.5), aurora(ro, rrd, pseudo_frag));
		col += stars(rrd) * 0.1;
		col = col * (1.0 - aur.a * reflection_strength) + aur.rgb * reflection_strength;
		vec3 pos = ro + ((0.5 - ro.y) / max(rrd.y, 0.001)) * rrd;
		float nz2 = triNoise2d(pos.xz * vec2(0.5, 0.7), 0.0);
		col += mix(vec3(0.2, 0.25, 0.5) * 0.08, vec3(0.3, 0.3, 0.5) * 0.7, nz2 * 0.4) * reflection_strength;
	}

	col = clamp(col * brightness, vec3(0.0), vec3(1.0));
	col = mix(col, vec3(1.0), clamp(pastel, 0.0, 1.0));
	vec3 hsv = rgb2hsv(col);
	hsv.x = fract(hsv.x + hue_shift);
	hsv.y = clamp(hsv.y * saturation, 0.0, 1.0);
	col = hsv2rgb(hsv);
	COLOR = vec4(col, 1.0);
}
