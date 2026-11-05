shader_type canvas_item;
render_mode unshaded;

// The mesh keeps the approved arm's common rest chart and fixed bone lengths.
// Every sample below comes from one complete shoulder-to-fingertip drawing.
uniform sampler2D full_arm : source_color, filter_linear_mipmap, repeat_disable;
uniform vec2 full_arm_size = vec2(1024.0);
uniform vec2 source_shoulder = vec2(320.0,160.0);
uniform vec2 source_elbow = vec2(424.0,482.0);
uniform vec2 source_wrist = vec2(786.0,257.0);
uniform float source_palm_width = 90.0;
uniform float target_palm_width_px = 52.3125;
uniform sampler2D open_hand : source_color, filter_linear_mipmap;
uniform sampler2D sword_hand : source_color, filter_linear_mipmap;
// Compatibility with the parent pose writer. Variant selection is explicit.
uniform float open_amount = 0.0;
uniform float sword_amount = 0.0;
uniform bool keep_original_sword = false;
uniform float inner_elbow_coverage = 0.0;

const vec2 OLD_A = vec2(425.0,435.0);
const vec2 OLD_B = vec2(466.0,562.0);
const vec2 OLD_C = vec2(609.0,473.0);

vec2 rotate_between(vec2 value, vec2 from, vec2 to) {
	vec2 a = normalize(from);
	vec2 b = normalize(to);
	float cosine = dot(a,b);
	float sine = a.x*b.y-a.y*b.x;
	return vec2(cosine*value.x-sine*value.y,sine*value.x+cosine*value.y);
}

void fragment() {
	if (keep_original_sword) {
		// Exact legacy v21 branch: the protected source and blend boundary stay.
		vec4 rest = texture(TEXTURE,UV);
		vec4 sword = texture(sword_hand,UV);
		float blend = smoothstep(0.574,0.612,UV.x);
		float opacity = mix(rest.a,sword.a,blend);
		COLOR = vec4((rest.rgb*rest.a*(1.0-blend)+sword.rgb*sword.a*blend)/max(opacity,0.00001),opacity);
	} else {
		vec2 point = UV/TEXTURE_PIXEL_SIZE;
		vec2 old_upper = OLD_B-OLD_A;
		vec2 old_lower = OLD_C-OLD_B;
		vec2 new_upper = source_elbow-source_shoulder;
		vec2 new_lower = source_wrist-source_elbow;
		vec2 upper_uv = source_shoulder+rotate_between(point-OLD_A,old_upper,new_upper)*length(new_upper)/length(old_upper);
		vec2 lower_uv = source_elbow+rotate_between(point-OLD_B,old_lower,new_lower)*length(new_lower)/length(old_lower);
		vec2 hand_uv = source_wrist+rotate_between(point-OLD_C,old_lower,new_lower)*source_palm_width/target_palm_width_px;
		vec2 elbow_axis = normalize(normalize(old_upper)+normalize(old_lower));
		float elbow = smoothstep(-8.0,8.0,dot(point-OLD_B,elbow_axis));
		float wrist = smoothstep(-4.0,4.0,dot(point-OLD_C,normalize(old_lower)));
		vec2 source_point = mix(mix(upper_uv,lower_uv,elbow),hand_uv,wrist);
		vec2 source_uv = source_point/full_arm_size;
		vec4 pixel = texture(full_arm,source_uv);
		// The new drawing's inner-elbow contour is slightly tighter than the
		// legacy skinned chart. Deep folds cover its seam with an adjacent
		// contour from this same complete source. Fixed sample offsets stay
		// local to the elbow; normal poses keep the original source silhouette.
		vec2 old_normal = vec2(-normalize(old_lower).y,normalize(old_lower).x);
		vec2 new_normal = vec2(-normalize(new_lower).y,normalize(new_lower).x);
		float inner_elbow = (1.0-smoothstep(-8.0,8.0,dot(point-OLD_B,old_normal)))*(1.0-smoothstep(80.0,110.0,length(point-OLD_B)))*(1.0-smoothstep(36.0,48.0,abs(dot(point-OLD_B,elbow_axis))));
		vec4 adjacent = texture(full_arm,(source_point+new_normal*40.0*inner_elbow*inner_elbow_coverage)/full_arm_size);
		if (pixel.a<0.9 && adjacent.a>pixel.a) pixel = adjacent;
		if (any(lessThan(source_uv,vec2(0.0))) || any(greaterThan(source_uv,vec2(1.0)))) pixel = vec4(0.0);
		pixel.a = smoothstep(0.08,0.9,pixel.a);
		COLOR = pixel;
	}
}
