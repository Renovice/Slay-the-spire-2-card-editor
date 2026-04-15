shader_type canvas_item;

uniform float time_speed : hint_range(0.0, 3.0) = 1.0;
uniform bool flip_y = false;

uniform vec4 sea_color_dark : source_color = vec4(0.04, 0.32, 0.55, 1.0);
uniform vec4 sea_color_light : source_color = vec4(0.06, 0.47, 0.60, 1.0);
uniform vec4 refraction_color : source_color = vec4(0.96, 0.98, 0.86, 1.0);
uniform vec4 light_shaft_color : source_color = vec4(0.88, 0.90, 0.78, 1.0);
uniform vec4 bubble_color : source_color = vec4(0.7, 0.85, 0.95, 0.8); // Bubble tint

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
    return mix(mix(hash(p + vec2(0,0)), hash(p + vec2(1,0)), w.x),
               mix(hash(p + vec2(0,1)), hash(p + vec2(1,1)), w.x), w.y);
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

float map(vec3 p) {
    vec2 uv = p.xz + (TIME * time_speed) / 2.0;
    float amp = 0.6, freq = 2.0, val = 0.0;
    for(int i = 0; i < 3; ++i) {
        val += map_octave(uv) * amp;
        amp *= 0.3;
        uv *= freq;
    }
    uv = p.xz - 1000.0 - (TIME * time_speed) / 2.0;
    amp = 0.6; freq = 2.0;
    for(int i = 0; i < 3; ++i) {
        val += map_octave(uv) * amp;
        amp *= 0.3;
        uv *= freq;
    }
    return val + 3.0 - p.y;
}

vec3 getNormal(vec3 p, float eps) {
    vec3 px = p + vec3(eps, 0.0, 0.0);
    vec3 pz = p + vec3(0.0, 0.0, eps);
    return normalize(vec3(map(px), eps, map(pz)));
}

RaymarchResult raymarch(vec3 ro, vec3 rd, float eps) {
    RaymarchResult result;
    float l = 0.0, r = 26.0;
    int steps = 16;
    float dist = 1000000.0;
    for(int i = 0; i < steps; ++i) {
        float mid = (r + l) / 2.0;
        float mapmid = map(ro + rd * mid);
        dist = min(dist, abs(mapmid));
        if(mapmid > 0.0) {
            l = mid;
        } else {
            r = mid;
        }
        if(r - l < eps) break; 
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

float lightShafts(vec2 st) {
    float angle = -0.2;
    vec2 _st = st;
    float t = (TIME * time_speed) / 16.0;
    st = vec2(st.x * cos(angle) - st.y * sin(angle),
              st.x * sin(angle) + st.y * cos(angle));
    float val = fbm(vec2(st.x * 2.0 + 200.0 + t, st.y / 4.0));
    val += fbm(vec2(st.x * 2.0 + 200.0 - t, st.y / 4.0));
    val = val / 3.0;
    float mask = pow(clamp(1.0 - abs(_st.y - 0.15), 0.0, 1.0) * 0.49 + 0.5, 2.0);
    mask *= clamp(1.0 - abs(_st.x + 0.2), 0.0, 1.0) * 0.49 + 0.5;
    return pow(val * mask, 2.0);
}

// Modified bubble function that returns bubble intensity and refraction offset
struct BubbleResult {
    vec2 offset;
    float intensity;
    bool is_bubble;
};

BubbleResult bubble_with_color(vec2 uv, float scale) {
    BubbleResult result;
    result.is_bubble = false;
    result.intensity = 0.0;
    result.offset = vec2(0.0);
    
    if(uv.y > 0.2) return result;
    
    float t = (TIME * time_speed) / 4.0;
    vec2 st = uv * scale;
    vec2 _st = floor(st);
    vec2 bias = vec2(0.0, 4.0 * sin(_st.x * 128.0 + t));
    float mask = smoothstep(0.1, 0.2, -cos(_st.x * 128.0 + t));
    st += bias;
    vec2 _st_ = floor(st);
    st = fract(st);
    float size = noise(_st_) * 0.07 + 0.01;
    vec2 pos = vec2(noise(vec2(t, _st_.y * 64.1)) * 0.8 + 0.1, 0.5);
    
    if(length(st.xy - pos) < size) {
        result.is_bubble = true;
        result.intensity = (1.0 - length(st.xy - pos) / size) * mask;
        result.offset = (st + pos) * vec2(0.1, 0.2) * mask;
    }
    return result;
}

// Function to get water color at a given UV
vec3 get_water_color(vec2 uv, vec3 ro, vec3 rd_base, float eps, float wave_intensity) {
    vec3 rd = normalize(vec3(uv, -1.0));
    RaymarchResult water_result = raymarch(ro, rd, eps);
    
    float diffuse = dot(getNormal(water_result.hit_pos, eps), rd) * 0.5 + 0.5;
    vec3 color = mix(sea_color_dark.rgb, sea_color_light.rgb, diffuse);
    color += pow(diffuse, 12.0);
    
    vec3 lightPos = vec3(8.0, 3.0, -3.0);
    vec3 ref = normalize(refract(water_result.hit_pos - lightPos, getNormal(water_result.hit_pos, eps), 0.05));
    float refraction = clamp(dot(ref, rd), 0.0, 1.0);
    color += refraction_color.rgb * 0.6 * pow(refraction, 1.5);
    
    vec3 col = mix(color, sea_color_dark.rgb, pow(clamp(0.0, 1.0, water_result.dist), 0.2));
    col += light_shaft_color.rgb * lightShafts(uv);
    col = (col * col + sin(col)) / vec3(1.8, 1.8, 1.9);
    
    vec2 q = uv * 2.0; // Adjust for different coordinate space
    col *= 0.7 + 0.3 * pow(16.0 * q.x * q.y * (1.0 - q.x) * (1.0 - q.y), 0.2);
    
    return col;
}

void fragment() {
    float eps = SCREEN_PIXEL_SIZE.x;
    vec3 ro = vec3(0.0, 0.0, 2.0);
    vec3 lightPos = vec3(8.0, 3.0, -3.0);
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 uv = (-res + 2.0 * FRAGCOORD.xy) / res.y;
    
    if (flip_y) {
        uv.y *= -1.0;
    }
    
    uv.y *= 0.5;
    uv.x *= 0.45;
    
    // Check for bubbles before adding their offset
    BubbleResult bubble1 = bubble_with_color(uv, 12.0);
    BubbleResult bubble2 = bubble_with_color(uv, 24.0);
    
    // Store bubble intensity
    float bubble_intensity = clamp(bubble1.intensity + bubble2.intensity, 0.0, 1.0);
    vec2 bubble_offset = bubble1.offset + bubble2.offset;
    
    // Apply bubble offset to UV for water sampling
    vec2 water_uv = uv + bubble_offset;
    
    // Get the water color (this will be refracted by bubbles)
    vec3 rd = normalize(vec3(water_uv, -1.0));
    RaymarchResult result = raymarch(ro, rd, eps);
    
    float wave_intensity = clamp(1.0 - result.dist * 0.5, 0.0, 1.0);
    float alpha = wave_intensity;
    
    float diffuse = dot(getNormal(result.hit_pos, eps), rd) * 0.5 + 0.5;
    vec3 color = mix(sea_color_dark.rgb, sea_color_light.rgb, diffuse);
    color += pow(diffuse, 12.0);
    
    vec3 ref = normalize(refract(result.hit_pos - lightPos, getNormal(result.hit_pos, eps), 0.05));
    float refraction = clamp(dot(ref, rd), 0.0, 1.0);
    color += refraction_color.rgb * 0.6 * pow(refraction, 1.5);
    
    vec3 col = vec3(0.0);
    col = mix(color, sea_color_dark.rgb, pow(clamp(0.0, 1.0, result.dist), 0.2));
    col += light_shaft_color.rgb * lightShafts(water_uv);
    col = (col * col + sin(col)) / vec3(1.8, 1.8, 1.9);
    
    vec2 q = UV;
    col *= 0.7 + 0.3 * pow(16.0 * q.x * q.y * (1.0 - q.x) * (1.0 - q.y), 0.2);
    
    // Apply bubble effect - brighten and add specular highlight where bubbles are
    if(bubble_intensity > 0.01) {
        // Bubbles create a bright, slightly blue-white refraction
        vec3 bubble_highlight = vec3(0.9, 0.95, 1.0) * (0.5 + bubble_intensity * 0.8);
        col = mix(col, bubble_highlight, bubble_intensity * 0.7);
        
        // Add extra alpha for bubble visibility
        alpha = max(alpha, bubble_intensity * 0.9);
    }
    
    alpha = clamp(alpha, 0.0, 0.95);
    
    COLOR = vec4(col, alpha);
}