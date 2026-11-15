shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D ink_noise : repeat_enable, filter_linear_mipmap;
uniform vec4 shield_tint : source_color = vec4(.39, .62, .80, 1.0);
uniform vec4 edge_tint : source_color = vec4(.23, .43, .63, 1.0);
uniform vec4 ivory_tint : source_color = vec4(.76, .88, .96, 1.0);
uniform float shield_strength = .9;
uniform float visibility = 1.0;
uniform vec2 impact_origin = vec2(.105, .5);
uniform float impact_progress = -1.0;
uniform float effect_time = 0.0;

void fragment() {
	vec2 p = (UV - .5) * 2.0;
	float r = length(p);
	float angle = atan(p.y, p.x);
	float n = texture(ink_noise, p * .48 + vec2(effect_time * .008, -effect_time * .011)).r;
	float fiber = texture(ink_noise, p * 1.8 + vec2(-effect_time * .006, 2.7)).r;
	float breath = sin(effect_time * 1.18) * .0025;
	float contour = .817 + breath + (n - .5) * .008;
	float edge_distance = abs(r - contour);
	float rim = exp(-pow(edge_distance / .010, 2.0));
	float wash = exp(-pow(edge_distance / .035, 2.0));
	float flow = .5 + .5 * sin(angle * 2.0 - effect_time * .34 + n * 1.1);
	float broken = smoothstep(.23, .56, fiber);
	float inner_film = (1.0 - smoothstep(.55, .81, r)) * .036;
	float grazing_light = pow(max(0.0, dot(normalize(p + vec2(.0001)), normalize(vec2(-.65, -.75)))), 7.0);
	float alpha = inner_film + wash * .115 + rim * (.25 + flow * .13 + broken * .10);
	float inner_r = .765 + .005 * sin(angle * 3.0 + effect_time * .23);
	float eddy = exp(-pow((r - inner_r) / .007, 2.0)) * pow(flow, 6.0) * broken;
	alpha += eddy * .12;
	// Broad, slow light/shadow movement on the ward surface, without additive bloom.
	float passing_light = exp(-pow((p.x * .86 + p.y * .34 - sin(effect_time * .48) * .47) / .19, 2.0));
	passing_light *= smoothstep(.18, .68, r) * (1.0 - smoothstep(.77, .86, r));
	float travelling_arc = pow(.5 + .5 * cos(angle - effect_time * .38), 9.0);
	float grazing_wash = exp(-pow((r - .785) / .052, 2.0)) * travelling_arc;
	alpha += passing_light * .085 + grazing_wash * .15;
	float moving_strands = 0.0;
	for (int i = 0; i < 4; i++) {
		float index = float(i);
		float direction = i % 2 == 0 ? 1.0 : -1.0;
		float phase = angle * direction - effect_time * (.45 + index * .16) + index * 1.71;
		float head = pow(.5 + .5 * cos(phase), 11.0);
		float arc_radius = .66 + index * .039 + .009 * sin(phase * 2.0 + effect_time * .31);
		float band = exp(-pow((r - arc_radius) / (.014 + index * .003), 2.0));
		moving_strands += head * band;
	}
	float crossing_light = exp(-pow((p.x * -.38 + p.y * .91 - sin(effect_time * .63 + 1.8) * .58) / .16, 2.0));
	crossing_light *= smoothstep(.28, .66, r) * (1.0 - smoothstep(.77, .86, r));
	alpha += moving_strands * .22 + crossing_light * .075;
	vec3 color = mix(shield_tint.rgb, edge_tint.rgb, broken * .35);
	color = mix(color, ivory_tint.rgb, clamp(grazing_light * .72 + passing_light * .42 + grazing_wash * .48 + moving_strands * .72 + crossing_light * .35, 0.0, .92));
	alpha += rim * grazing_light * .17;
	if (impact_progress >= 0.0) {
		float t = clamp(impact_progress, 0.0, 1.0);
		float d = length((UV - impact_origin) * 2.0);
		float travel = 1.0 - pow(1.0 - t, 2.0);
		float ripple = exp(-pow((d - (.025 + travel * .44)) / (.012 + t * .010), 2.0));
		float echo = exp(-pow((d - (.012 + travel * .30)) / .007, 2.0)) * .43;
		float along_surface = 1.0 - smoothstep(.025, .105, abs(r - .79));
		float fade = smoothstep(0.0, .055, t) * (1.0 - smoothstep(.25, 1.0, t));
		float crest = (ripple + echo) * along_surface * fade * (.65 + fiber * .35);
		float touch = exp(-d * d / .0028) * (1.0 - smoothstep(.04, .31, t));
		alpha += crest * .57 + touch * .38;
		color = mix(color, ivory_tint.rgb, clamp(crest * .9 + touch, 0.0, .88));
	}
	alpha *= 1.0 - smoothstep(.86, .94, r);
	// Loss of the ward breaks the wash into pigment before its final fade.
	float breakup = smoothstep(0.0, .35, visibility - fiber * .22);
	COLOR = vec4(color, clamp(alpha * shield_strength * visibility * breakup, 0.0, .62));
}
