shader_type canvas_item;

// Render-only recoil. CPU vertices, bones, grounding and contact heights stay intact.
uniform vec2 foot_world = vec2(0.0);
uniform float body_height = 100.0;
uniform vec2 recoil = vec2(0.0);
uniform float compression = 0.0;
uniform float lean = 0.0;

void vertex() {
	vec2 world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	vec2 local_foot = world - foot_world;
	float upper = smoothstep(0.24, 0.70, -local_foot.y / max(body_height, 1.0));
	// Bend around the waist, keeping the head/shoulders together and the feet planted.
	vec2 waist = vec2(0.0, -body_height * 0.42);
	vec2 torso = local_foot - waist;
	float angle = lean * upper;
	float c = cos(angle);
	float s = sin(angle);
	world = foot_world + waist + vec2(c * torso.x - s * torso.y, s * torso.x + c * torso.y);
	world += recoil * upper;
	world.y += max(-local_foot.y, 0.0) * compression * upper;
	VERTEX = (inverse(MODEL_MATRIX) * vec4(world, 0.0, 1.0)).xy;
}
