shader_type canvas_item;
render_mode unshaded, blend_mix;

// 低成本战斗水墨：一张白色 Sprite + 一个 Shader 覆盖斩击、拳锋、命中和死亡。
// effect_mode: 0 斩击，1 拳锋，2 普通命中，3 拳击命中，4 死亡墨散。
uniform vec4 ink_color : source_color = vec4(0.055, 0.075, 0.07, 0.94);
uniform vec4 accent_color : source_color = vec4(0.30, 0.42, 0.36, 1.0);
uniform float effect_mode : hint_range(0.0, 6.0) = 0.0;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float seed_value = 0.0;
uniform float critical : hint_range(0.0, 1.0) = 0.0;
uniform float evaded : hint_range(0.0, 1.0) = 0.0;

const float INK_PI = 3.14159265359;
const float INK_TAU = 6.28318530718;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.56;
	for (int i = 0; i < 3; i++) {
		value += value_noise(p) * amplitude;
		p = p * 2.03 + vec2(17.1, 9.7);
		amplitude *= 0.5;
	}
	return value;
}

float soft_line(float distance_to_line, float width, float feather) {
	return 1.0 - smoothstep(width, width + feather, distance_to_line);
}

float soft_disc(vec2 point, vec2 center, float radius, float feather) {
	return 1.0 - smoothstep(radius, radius + feather, distance(point, center));
}

float soft_round_box(vec2 point, vec2 center, vec2 half_size, float roundness, float feather) {
	vec2 q = abs(point - center) - half_size + vec2(roundness);
	float distance_to_box = length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - roundness;
	return 1.0 - smoothstep(-feather, feather, distance_to_box);
}

float ease_out_cubic(float value) {
	float inverse = 1.0 - clamp(value, 0.0, 1.0);
	return 1.0 - inverse * inverse * inverse;
}

void fragment() {
	vec2 point = (UV - vec2(0.5)) * 2.0;
	float t = clamp(progress, 0.0, 1.0);
	float alpha = 0.0;
	vec3 color = ink_color.rgb;
	float seed = seed_value * 0.017;

	if (effect_mode < 0.5) {
		// 斩击：移动的宽墨带 + 干刷中线 + 湿边断墨。
		float sweep = mix(-1.18, 1.18, smoothstep(0.0, 0.70, t));
		float tail = sweep - 1.13;
		float head = sweep + 0.24;
		float along = smoothstep(tail, tail + 0.09, point.x) * (1.0 - smoothstep(head - 0.15, head, point.x));
		float segment_u = clamp((point.x - tail) / max(head - tail, 0.001), 0.0, 1.0);
		float envelope = 0.42 + 0.58 * sin(segment_u * INK_PI);
		float curve = sin(point.x * 4.3 + seed * 7.0) * 0.10 + sin(point.x * 13.0 - TIME * 1.7 + seed) * 0.024;
		float distance_to_ribbon = abs(point.y - curve);
		float grain = fbm(point * vec2(3.8, 6.2) + vec2(TIME * 0.14 + seed, -TIME * 0.08));
		float wet = soft_line(distance_to_ribbon, (0.105 + 0.16 * envelope) * (0.88 + grain * 0.20), 0.055) * along;
		float body = soft_line(distance_to_ribbon, (0.058 + 0.105 * envelope), 0.035) * along;
		float dry_a = soft_line(abs(point.y - curve - 0.052 - sin(point.x * 5.0 + seed) * 0.028), 0.016, 0.018) * along;
		float dry_b = soft_line(abs(point.y - curve + 0.044 + sin(point.x * 7.0 - seed) * 0.018), 0.012, 0.016) * along * (0.65 + grain * 0.35);
		float head_glow = soft_disc(point, vec2(head - 0.07, curve), 0.105, 0.12) * (1.0 - smoothstep(0.68, 0.98, t));

		float droplets = 0.0;
		for (int i = 0; i < 5; i++) {
			float fi = float(i);
			float droplet_u = 0.18 + hash12(vec2(fi + 4.0, seed + 2.0)) * 0.70;
			float droplet_x = mix(tail, head, droplet_u);
			float droplet_y = curve + (hash12(vec2(fi + 9.0, seed + 7.0)) - 0.5) * (0.22 + droplet_u * 0.18);
			float droplet_radius = 0.018 + hash12(vec2(fi + 15.0, seed + 11.0)) * 0.034;
			droplets += soft_disc(point, vec2(droplet_x, droplet_y), droplet_radius, 0.025) * along;
		}

		float fade = (1.0 - smoothstep(0.76, 1.0, t)) * smoothstep(0.0, 0.055, t);
		float edge_mix = clamp(wet * 0.36 + dry_a * 0.34 + dry_b * 0.22 + head_glow * 0.38, 0.0, 1.0);
		color = mix(ink_color.rgb * 0.72, accent_color.rgb, edge_mix);
		alpha = max(wet * 0.70, body * 0.82) + dry_a * 0.72 + dry_b * 0.50 + droplets * 0.68 + head_glow * 0.42;
		alpha *= fade;
	} else if (effect_mode < 1.5) {
		// 拳锋：蓄力、突进、回弹三段式；拳头和拳风拥有不同的速度与摆动。
		float windup = 1.0 - smoothstep(0.0, 0.12, t);
		float launch = smoothstep(0.08, 0.56, t);
		float strike = ease_out_cubic(launch);
		float snap = smoothstep(0.14, 0.26, t) * (1.0 - smoothstep(0.34, 0.58, t));
		float recoil = smoothstep(0.62, 0.90, t);
		float knuckle = -0.93 - windup * 0.10 + strike * 1.70 - recoil * 0.10;
		float tail = knuckle - mix(0.88, 0.70, strike);
		float head = knuckle + 0.22 + snap * 0.10;
		float attack_gate = smoothstep(0.025, 0.13, t);
		float along = smoothstep(tail, tail + 0.10, point.x) * (1.0 - smoothstep(head - 0.14, head, point.x)) * attack_gate;
		float segment_u = clamp((point.x - tail) / max(head - tail, 0.001), 0.0, 1.0);
		float curve = sin(point.x * 5.0 + seed) * 0.045 + sin(TIME * 2.2 + point.x * 3.5 + seed) * 0.018;
		float grain = value_noise(point * vec2(6.0, 8.0) + vec2(seed, -TIME * 0.45));
		float body = soft_line(abs(point.y - curve), 0.10 + 0.11 * sin(segment_u * INK_PI), 0.045) * along;
		float wet = soft_line(abs(point.y - curve), 0.19 + 0.12 * sin(segment_u * INK_PI) + snap * 0.04, 0.065) * along;

		float streaks = 0.0;
		for (int i = 0; i < 3; i++) {
			float fi = float(i) - 1.0;
			float lane_shift = sin(TIME * (1.45 + fi * 0.16) + seed + fi * 1.7) * 0.020;
			float streak_y = curve + fi * (0.105 + snap * 0.035) + lane_shift + sin(point.x * 4.0 + fi + seed) * 0.025;
			float streak_tail = tail + abs(fi) * 0.055 + fi * 0.06;
			float streak_head = head - abs(fi) * 0.08;
			float streak_along = smoothstep(streak_tail, streak_tail + 0.11, point.x) * (1.0 - smoothstep(streak_head - 0.16, streak_head, point.x)) * attack_gate;
			streaks += soft_line(abs(point.y - streak_y), 0.012 + snap * 0.006, 0.018) * streak_along * (0.58 + grain * 0.42);
		}
		float whip = soft_line(abs(point.y - (curve + 0.06 + sin(point.x * 3.2 + seed) * 0.025)), 0.012, 0.022) * along * (0.55 + snap * 0.40);

		// 前臂、掌面、拇指和四个指节：肌肉渐宽，拳面带掌纹与干湿墨边。
		float fist_tilt = -0.055 + snap * 0.20 + sin(TIME * 1.8 + seed) * 0.012;
		vec2 fist_point = point;
		fist_point.y -= (point.x - knuckle) * fist_tilt;
		fist_point.x = knuckle + (fist_point.x - knuckle) / (1.0 + snap * 0.14);
		fist_point.y = (fist_point.y - curve * 0.30) / (1.0 - snap * 0.08) + curve * 0.30;
		float arm_u = clamp((fist_point.x - (knuckle - 0.72)) / 0.64, 0.0, 1.0);
		float arm_half_height = mix(0.045, 0.090, smoothstep(0.0, 1.0, arm_u));
		float arm_span = smoothstep(knuckle - 0.76, knuckle - 0.62, fist_point.x) * (1.0 - smoothstep(knuckle + 0.005, knuckle + 0.11, fist_point.x));
		float forearm = soft_line(abs(fist_point.y - (0.012 + arm_u * 0.008)), arm_half_height, 0.030) * arm_span;
		float wrist = soft_round_box(fist_point, vec2(knuckle - 0.43, 0.018), vec2(0.16, 0.061), 0.040, 0.026);
		float palm = soft_round_box(fist_point, vec2(knuckle - 0.035, 0.0), vec2(0.135, 0.115), 0.072, 0.040);
		float knuckles = 0.0;
		float knuckle_highlights = 0.0;
		for (int i = 0; i < 4; i++) {
			float fi = float(i) - 1.5;
			vec2 knuckle_center = vec2(knuckle + 0.055 + snap * 0.02, fi * 0.055 - 0.018);
			knuckles = max(knuckles, soft_disc(fist_point, knuckle_center, 0.060 + snap * 0.006, 0.032));
			knuckle_highlights = max(knuckle_highlights, soft_line(abs(fist_point.y - (fi * 0.055 - 0.038)), 0.009, 0.018) * soft_disc(fist_point, knuckle_center, 0.058, 0.028));
		}
		float thumb = soft_disc(fist_point, vec2(knuckle - 0.045, 0.082), 0.064, 0.034);
		float fist = max(max(palm, knuckles), thumb);
		fist = max(fist, max(forearm * 0.88, wrist));
		float fist_shadow = soft_round_box(fist_point, vec2(knuckle - 0.02, 0.075), vec2(0.12, 0.026), 0.020, 0.018);
		float palm_fold = soft_line(abs(fist_point.y - (0.032 + sin((fist_point.x - knuckle) * 9.0) * 0.010)), 0.008, 0.018) * soft_round_box(fist_point, vec2(knuckle - 0.10, 0.015), vec2(0.09, 0.075), 0.045, 0.035);
		float forearm_sheen = soft_line(abs(fist_point.y - (0.012 - arm_u * 0.022)), 0.012, 0.028) * forearm;
		float fist_highlight = max(forearm_sheen, knuckle_highlights);
		float fist_edge = clamp(fist - fist_shadow * 0.34, 0.0, 1.0);
		float ink_grain = value_noise(fist_point * vec2(22.0, 30.0) + vec2(seed * 2.0, TIME * 0.22));
		float edge_breakup = smoothstep(0.70, 0.96, fbm(fist_point * vec2(17.0, 23.0) + vec2(seed, -TIME * 0.12)));
		fist *= 0.93 + ink_grain * 0.10 - edge_breakup * 0.11;
		float afterimage = soft_disc(point, vec2(knuckle - 0.18, -0.012), 0.13 + snap * 0.03, 0.065) * snap * (1.0 - smoothstep(0.25, 0.52, t));
		float fade = (1.0 - smoothstep(0.78, 1.0, t)) * smoothstep(0.0, 0.035, t);
		vec3 fist_color = mix(ink_color.rgb * 0.60, accent_color.rgb, clamp(0.36 + ink_grain * 0.20 + fist_highlight * 0.52, 0.0, 1.0));
		fist_color = mix(fist_color, ink_color.rgb * 0.44, fist_shadow * 0.30 + palm_fold * 0.42);
		color = mix(ink_color.rgb * 0.58, accent_color.rgb, clamp(wet * 0.25 + afterimage * 0.18, 0.0, 1.0));
		color = mix(color, fist_color, fist);
		color = mix(color, accent_color.rgb * 1.12, fist_highlight * 0.28);
		color = mix(color, accent_color.rgb * 1.08, whip * 0.26);
		alpha = max(wet * 0.40 + body * 0.56 + streaks * 0.66 + whip * 0.44, fist * 0.92 + fist_highlight * 0.24) * fade;
		alpha += afterimage * 0.20 * fade;
	} else if (effect_mode < 2.5) {
		// 普通命中：圆形冲击环、八向墨线和不规则飞溅。
		float strength = mix(1.0, 0.44, evaded);
		float radius = mix(0.06, 0.84, smoothstep(0.0, 0.70, t));
		float distance_from_center = length(point);
		float ring = soft_line(abs(distance_from_center - radius), 0.028, 0.065) * (1.0 - smoothstep(0.68, 1.0, t));
		float core = soft_disc(point, vec2(0.0), mix(0.15, 0.035, t), 0.08) * (1.0 - smoothstep(0.25, 0.9, t));
		float rays = 0.0;
		for (int i = 0; i < 8; i++) {
			float fi = float(i);
			float angle = fi / 8.0 * INK_TAU + seed * 0.35;
			vec2 direction = vec2(cos(angle), sin(angle));
			vec2 tangent = vec2(-direction.y, direction.x);
			float along = dot(point, direction);
			float across = abs(dot(point, tangent));
			float in_span = smoothstep(0.12, 0.22, along) * (1.0 - smoothstep(radius + 0.06, radius + 0.20, along));
			rays = max(rays, soft_line(across, 0.012 + hash12(vec2(fi, seed)) * 0.016, 0.024) * in_span);
		}
		float splats = 0.0;
		for (int i = 0; i < 7; i++) {
			float fi = float(i);
			float angle = fi / 7.0 * INK_TAU + hash12(vec2(fi + 30.0, seed)) * 0.55;
			vec2 direction = vec2(cos(angle), sin(angle));
			float distance_from_hit = 0.28 + hash12(vec2(fi + 41.0, seed + 5.0)) * radius * 0.82;
			vec2 splat = direction * distance_from_hit;
			float splat_radius = 0.018 + hash12(vec2(fi + 55.0, seed + 8.0)) * 0.040;
			splats += soft_disc(point, splat, splat_radius, 0.030);
		}
		float fade = (1.0 - smoothstep(0.58, 1.0, t)) * strength;
		vec3 hit_color = mix(ink_color.rgb, vec3(0.76, 0.18, 0.08), critical * 0.72);
		color = mix(hit_color * 0.62, accent_color.rgb, clamp(ring * 0.62 + core * 0.50, 0.0, 1.0));
		alpha = (ring * 0.88 + core * 0.58 + rays * 0.74 + splats * 0.72) * fade;
	} else if (effect_mode < 3.5) {
		// 拳击命中：先压缩、再爆开；残留短暂拳印，再被冲击环带走。
		float strength = 1.0 - evaded * 0.36;
		vec2 ellipse_point = point * vec2(0.86, 1.12);
		float burst = ease_out_cubic(smoothstep(0.0, 0.68, t));
		float contact_pulse = 1.0 - smoothstep(0.10, 0.34, t);
		float radius = mix(0.06, 0.70, burst);
		float ring = soft_line(abs(length(ellipse_point) - radius), 0.040, 0.075) * (1.0 - smoothstep(0.68, 1.0, t));
		float core = soft_disc(point, vec2(-0.05, 0.0), mix(0.18, 0.035, t), 0.08) * (1.0 - smoothstep(0.20, 0.78, t));
		float knuckle_lines = 0.0;
		for (int i = 0; i < 5; i++) {
			float fi = float(i) - 2.0;
			float line_y = fi * 0.075;
			float line_x = point.x + point.y * 0.18;
			float segment = smoothstep(0.02, 0.14, line_x) * (1.0 - smoothstep(0.55 + fi * 0.03, 0.82 + fi * 0.03, line_x));
			knuckle_lines = max(knuckle_lines, soft_line(abs(point.y - line_y), 0.012, 0.022) * segment);
		}
		float contact_forearm = soft_round_box(point, vec2(-0.18, 0.02), vec2(0.20, 0.095), 0.060, 0.035);
		float contact_palm = soft_disc(point, vec2(-0.005, 0.0), 0.135, 0.050);
		float contact_knuckles = 0.0;
		for (int i = 0; i < 4; i++) {
			float fi = float(i) - 1.5;
			contact_knuckles = max(contact_knuckles, soft_disc(point, vec2(0.075, fi * 0.052 - 0.020), 0.058, 0.030));
		}
		float contact_fist = max(max(contact_forearm, contact_palm), contact_knuckles);
		float contact_wind = soft_line(abs(point.y - point.x * 0.12), 0.055, 0.035) * (1.0 - smoothstep(0.10, 0.70, point.x));
		float shock_shear = soft_line(abs(point.y + point.x * 0.20), 0.014, 0.026) * (1.0 - smoothstep(0.28, 0.78, t));
		float flare = soft_disc(point, vec2(0.25, 0.0), 0.12 + t * 0.08, 0.10);
		float fade = (1.0 - smoothstep(0.62, 1.0, t)) * strength;
		vec3 hit_color = mix(ink_color.rgb, vec3(0.76, 0.18, 0.08), critical * 0.72);
		color = mix(hit_color * 0.56, accent_color.rgb, clamp(ring * 0.52 + flare * 0.34 + contact_fist * 0.72 + shock_shear * 0.30, 0.0, 1.0));
		color = mix(color, hit_color * 0.42, contact_fist * 0.26);
		alpha = (ring * 0.90 + core * 0.68 + knuckle_lines * 0.66 + contact_fist * 0.82 * contact_pulse + contact_wind * 0.42 + shock_shear * 0.38 + flare * 0.32) * fade;
	} else if (effect_mode < 4.5) {
		// 死亡墨散：噪声溶解主体 + 向外飞散的固定少量墨粒和断线。
		float distance_from_center = length(point);
		float threshold = mix(0.02, 0.86, smoothstep(0.08, 0.82, t));
		float noise = fbm(point * 3.4 + vec2(seed * 1.7, -seed * 0.8));
		float survive = 1.0 - smoothstep(threshold, threshold + 0.13, noise);
		float dissolve_edge = soft_line(abs(noise - threshold), 0.015, 0.085);
		float body = survive * (1.0 - smoothstep(0.76, 1.10, distance_from_center)) * (1.0 - smoothstep(0.18, 0.95, t));
		float particles = 0.0;
		float trails = 0.0;
		for (int i = 0; i < 10; i++) {
			float fi = float(i);
			float angle = fi / 10.0 * INK_TAU + hash12(vec2(fi + 80.0, seed)) * 0.45;
			vec2 direction = vec2(cos(angle), sin(angle));
			float start_distance = 0.10 + hash12(vec2(fi + 92.0, seed + 1.0)) * 0.18;
			float end_distance = start_distance + smoothstep(0.0, 0.75, t) * (0.28 + hash12(vec2(fi + 104.0, seed + 4.0)) * 0.62);
			vec2 particle = direction * end_distance + vec2(0.0, sin(TIME * 1.6 + fi) * 0.018);
			float particle_radius = 0.018 + hash12(vec2(fi + 117.0, seed + 9.0)) * 0.042;
			particles += soft_disc(point, particle, particle_radius, 0.028);
			vec2 tangent = vec2(-direction.y, direction.x);
			float along = dot(point, direction);
			float across = abs(dot(point, tangent));
			float line_span = smoothstep(start_distance, start_distance + 0.08, along) * (1.0 - smoothstep(end_distance, end_distance + 0.10, along));
			trails = max(trails, soft_line(across, 0.010 + particle_radius * 0.20, 0.022) * line_span);
		}
		float fade = smoothstep(0.0, 0.05, t) * (1.0 - smoothstep(0.72, 1.0, t));
		color = mix(ink_color.rgb * 0.72, accent_color.rgb, clamp(dissolve_edge * 0.84 + particles * 0.30, 0.0, 1.0));
		alpha = (body * 0.68 + dissolve_edge * 0.78 + particles * 0.76 + trails * 0.54) * fade;
	} else if (effect_mode < 5.5) {
		// 剑劈斩：含水笔锋扫过纸面，以积染、枯笔和自然毛边代替硬质刀刃。
		float swing = ease_out_cubic(smoothstep(0.0, 0.72, t));
		float blade_head = mix(-1.14, 1.12, swing);
		float blade_tail = blade_head - 0.92;
		float along = smoothstep(blade_tail, blade_tail + 0.10, point.x) * (1.0 - smoothstep(blade_head - 0.16, blade_head, point.x));
		float u = clamp((point.x - blade_tail) / max(blade_head - blade_tail, 0.001), 0.0, 1.0);
		float curve = point.x * 0.52 + sin(point.x * 3.4 + seed) * 0.045 + sin(TIME * 2.0 + seed) * 0.012;
		float grain = fbm(point * vec2(5.2, 7.4) + vec2(seed, -TIME * 0.12));
		float envelope = 0.36 + 0.64 * sin(u * INK_PI);
		float broad_wash = soft_line(abs(point.y - curve), 0.22 * envelope, 0.11) * along;
		float blade_body = soft_line(abs(point.y - curve), 0.075 * envelope, 0.045) * along;
		float blade_edge = soft_line(abs(point.y - curve + 0.048), 0.016, 0.024) * along;
		float dry_edge = soft_line(abs(point.y - curve - 0.070 - sin(point.x * 5.0 + seed) * 0.018), 0.012, 0.023) * along;
		float back_edge = soft_line(abs(point.y - curve + 0.13), 0.018, 0.032) * along * (0.52 + grain * 0.48);
		float tip = soft_disc(point, vec2(blade_head - 0.05, curve), 0.075, 0.09) * (1.0 - smoothstep(0.72, 1.0, t));
		float chips = 0.0;
		for (int i = 0; i < 5; i++) {
			float fi = float(i);
			float chip_u = 0.16 + hash12(vec2(fi + 312.0, seed)) * 0.74;
			float chip_x = mix(blade_tail, blade_head, chip_u);
			float chip_y = chip_x * 0.52 + (hash12(vec2(fi + 321.0, seed + 3.0)) - 0.5) * (0.20 + chip_u * 0.18);
			chips += soft_disc(point, vec2(chip_x, chip_y), 0.014 + hash12(vec2(fi + 330.0, seed + 5.0)) * 0.025, 0.020) * along;
		}
		float fade = (1.0 - smoothstep(0.76, 1.0, t)) * smoothstep(0.0, 0.045, t);
		color = mix(ink_color.rgb * 0.56, accent_color.rgb, clamp(broad_wash * 0.44 + blade_edge * 0.70 + tip * 0.42 + dry_edge * 0.30, 0.0, 1.0));
		color = mix(color, vec3(0.78, 0.88, 0.91), clamp(blade_edge * 0.64 + dry_edge * 0.40, 0.0, 0.92));
		alpha = (broad_wash * 0.72 + blade_body * 0.86 + blade_edge * 0.88 + dry_edge * 0.72 + back_edge * 0.48 + tip * 0.42 + chips * 0.64) * fade;
	} else {
		// 剑斩受击：目标身上留下斜向剑痕，短促冷光后散成青墨飞线。
		float impact = smoothstep(0.0, 0.11, t);
		float settle = 1.0 - smoothstep(0.38, 0.86, t);
		float slash_curve = point.x * 0.18 + sin(point.x * 3.1 + seed) * 0.018;
		float span = mix(0.12, 0.90, ease_out_cubic(impact));
		float in_span = 1.0 - smoothstep(span - 0.08, span, abs(point.x));
		float main_cut = soft_line(abs(point.y - slash_curve), 0.028, 0.035) * in_span;
		float upper_ink = soft_line(abs(point.y - slash_curve - 0.075), 0.052, 0.045) * in_span;
		float lower_dry = soft_line(abs(point.y - slash_curve + 0.070), 0.010, 0.020) * in_span;
		float second_cut = soft_line(abs(point.y - slash_curve + 0.16), 0.012, 0.022) * in_span * 0.72;
		float wound_glow = soft_line(abs(point.y - slash_curve), 0.080, 0.11) * in_span * (1.0 - smoothstep(0.22, 0.62, t));
		float center_flash = soft_disc(point, vec2(0.0), 0.11 + impact * 0.12, 0.12) * (1.0 - smoothstep(0.10, 0.30, t));
		float splashes = 0.0;
		float sparks = 0.0;
		for (int i = 0; i < 7; i++) {
			float fi = float(i);
			float side = (hash12(vec2(fi + 341.0, seed + 1.0)) - 0.5) * 2.0;
			float distance_from_cut = 0.18 + hash12(vec2(fi + 352.0, seed + 2.0)) * 0.52;
			vec2 fleck = vec2(side * distance_from_cut, side * distance_from_cut * 0.18 + slash_curve);
			float radius = 0.012 + hash12(vec2(fi + 363.0, seed + 3.0)) * 0.028;
			vec2 flying = fleck + vec2(side * impact * 0.12, -abs(side) * impact * 0.08);
			splashes += soft_disc(point, flying, radius, 0.024) * impact;
			float spark_y = slash_curve + side * 0.12;
			sparks += soft_line(abs(point.y - spark_y), 0.008, 0.016) * soft_disc(point, vec2(fleck.x, spark_y), 0.11, 0.08) * (1.0 - smoothstep(0.08, 0.24, t));
		}
		float fade = (settle * 0.76 + (1.0 - smoothstep(0.02, 0.20, t)) * 0.24) * (1.0 - smoothstep(0.72, 1.0, t));
		color = mix(ink_color.rgb * 0.50, accent_color.rgb, clamp(upper_ink * 0.58 + wound_glow * 0.42 + splashes * 0.34 + center_flash * 0.44, 0.0, 1.0));
		color = mix(color, vec3(0.86, 0.94, 0.96), clamp(main_cut * 0.68 + lower_dry * 0.60 + sparks * 0.38 + center_flash * 0.52, 0.0, 1.0));
		alpha = (upper_ink * 0.84 + main_cut * 0.92 + lower_dry * 0.68 + second_cut * 0.58 + wound_glow * 0.48 + center_flash * 0.50 + splashes * 0.76 + sparks * 0.56) * fade;
	}

	COLOR = vec4(color, clamp(alpha, 0.0, 0.96));
}
