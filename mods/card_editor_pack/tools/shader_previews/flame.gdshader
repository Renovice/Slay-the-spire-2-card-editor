shader_type canvas_item;

uniform float speed : hint_range(0.0, 3.0) = 0.35;
uniform float swirl_strength : hint_range(0.0, 15.0) = 4.5;
uniform float line_count : hint_range(1.0, 30.0) = 8.0;
uniform float line_sharpness : hint_range(0.0, 1.0) = 0.85;
uniform vec4 deep_color : source_color = vec4(0.01, 0.06, 0.15, 1.0);
uniform vec4 mid_color : source_color = vec4(0.1, 0.28, 0.48, 1.0);
uniform vec4 line_color : source_color = vec4(0.65, 0.85, 1.0, 1.0);

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),u.x),
               mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),u.x),u.y);
}

float fbm(vec2 p) {
    float v=0.0; float a=0.5;
    for(int i=0;i<5;i++){ v+=a*noise(p); p*=2.1; a*=0.5; }
    return v;
}

void fragment() {
    vec2 uv = UV - 0.5;
    float radius = length(uv);
    float angle = atan(uv.y, uv.x);

    // whirlpool swirl
    float swirl = swirl_strength * exp(-radius * 3.2);
    float a2 = angle + swirl - TIME * speed * 0.22;
    vec2 swirled = vec2(cos(a2), sin(a2)) * radius + 0.5;

    // base water depth
    vec2 depth_uv = swirled * 3.0 + vec2(TIME*speed*0.018, TIME*speed*0.012);
    float depth = fbm(depth_uv);

    // --- clean spiral lines ---
    // work in polar space AFTER swirl so lines follow the spiral
    float swirl_angle = a2; // already has swirl baked in

    // wrap angle 0..1 and multiply by line count
    float line_pos = fract((swirl_angle / (2.0 * 3.14159)) * line_count);

    // sharp thin lines: spike at 0 and 1
    float line = 1.0 - abs(line_pos - 0.5) * 2.0; // triangle wave
    line = pow(line, 8.0 * (1.0 + line_sharpness * 12.0)); // sharpen

    // fade lines out at center drain and at edges
    float fade = smoothstep(0.0, 0.18, radius) * smoothstep(0.72, 0.45, radius);
    line *= fade;

    // subtle noise wobble on the lines so they dont look perfectly mechanical
    float wobble = noise(swirled * 3.0 + TIME * speed * 0.1) - 0.15 * 0.15;
    float line_pos2 = fract((swirl_angle / (2.0 * 3.14159)) * line_count + wobble);
    float line2 = 1.0 - abs(line_pos2 - 0.5) * 2.0;
    line2 = pow(line2, 8.0 * (1.0 + line_sharpness * 12.0));
    line2 *= fade * 0.1; // secondary lines dimmer

    float lines_final = max(line, line2);

    // compose
    vec3 col = mix(deep_color.rgb, mid_color.rgb, depth * 0.9);
    col += line_color.rgb * lines_final * 0.9;

    // dark drain
    col *= mix(0.05, 1.0, smoothstep(0.0, 0.15, radius));

    // vignette
    col *= 1.0 - smoothstep(0.35, 0.72, radius) * 0.45;

    COLOR = vec4(col, 1.0);
}