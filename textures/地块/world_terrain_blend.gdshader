shader_type canvas_item;

// ============================================================
// 世界空间纹理投射 — 平原 + 水域简化版
//
// 所有陆地统一为"平原"（丰饶度由 VegetationLayer 独立控制）。
// 水域保留浅水→深水过渡。
//
// 地形锚点（与 HexWorldMap.TERRAIN_ANCHOR_R 一致）:
//   R=0.0 → 纯平原 (plains)
//   R=0.5 → 纯浅水 (shallow water)
//   R=1.0 → 纯深水 (deep water)
//
// 过渡带:
//   0.0 ~ 0.5: 平原 ↔ 浅水 平滑过渡
//   0.5 ~ 1.0: 浅水 ↔ 深水 平滑过渡
// ============================================================

uniform sampler2D tex_land   : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_land2  : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_shallow: source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_deep   : source_color, repeat_enable, filter_linear_mipmap;

uniform float tex_scale = 0.005;
uniform vec2 world_offset = vec2(0.0, 0.0);

varying vec2 world_pos;

const float SHALLOW_ANCHOR = 0.5;  // 浅水锚点
const float DEEP_ANCHOR    = 1.0;  // 深水锚点

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 tile_color = texture(TEXTURE, UV);
	float r_value = tile_color.r;
	float hex_alpha = tile_color.a;

	vec2 w_uv = (world_pos + world_offset) * tex_scale;

	float w_land    = 0.0;
	float w_shallow = 0.0;
	float w_deep    = 0.0;

	if (r_value < SHALLOW_ANCHOR) {
		// 平原 → 浅水: R=0.0 纯平原, R=0.5 纯浅水
		float t = r_value / SHALLOW_ANCHOR;
		w_land    = 1.0 - t;
		w_shallow = t;
	} else {
		// 浅水 → 深水: R=0.5 纯浅水, R=1.0 纯深水
		float t = (r_value - SHALLOW_ANCHOR) / (DEEP_ANCHOR - SHALLOW_ANCHOR);
		t = clamp(t, 0.0, 1.0);
		w_shallow = 1.0 - t;
		w_deep    = t;
	}

	vec4 color_land    = texture(tex_land,   w_uv);
	vec4 color_shallow = texture(tex_shallow, w_uv);
	vec4 color_deep    = texture(tex_deep,    w_uv);

	vec3 blended = color_land.rgb    * w_land
	             + color_shallow.rgb * w_shallow
	             + color_deep.rgb    * w_deep;

	COLOR = vec4(blended, hex_alpha);
}
