shader_type canvas_item;

// ============================================================
// 世界地图地形渲染 — 8 地形 无缝纹理世界空间采样
//
// 每个 TileMapLayer tile 的 R 值编码地形类型：
//   R=0.0    → PLAINS_GRASS   平原草地
//   R=0.125  → PLAINS_FOREST  平原森林
//   R=0.25   → PLAINS_WASTELAND  平原荒地
//   R=0.375  → PLAINS_DESERT  平原沙漠
//   R=0.5    → WATER          平原水域
//   R=0.625  → MOUNTAIN       山地
//   R=0.75   → VILLAGE        村庄
//   R=0.875  → SPECIAL        特殊地块
//
// tile 的 alpha 通道定义六边形形状（边缘透明 = 非六边形区域）
// ============================================================

uniform sampler2D tex_grass : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_forest : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_wasteland : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_desert : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_water : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_mountain : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_village : source_color, repeat_enable, filter_linear_mipmap;
uniform sampler2D tex_special : source_color, repeat_enable, filter_linear_mipmap;

uniform float tex_scale = 0.005;
uniform vec2 world_offset = vec2(0.0, 0.0);

varying vec2 world_pos;

// 地形锚点
const float ANCHOR_GRASS     = 0.0;
const float ANCHOR_FOREST    = 0.125;
const float ANCHOR_WASTELAND = 0.25;
const float ANCHOR_DESERT    = 0.375;
const float ANCHOR_WATER     = 0.5;
const float ANCHOR_MOUNTAIN  = 0.625;
const float ANCHOR_VILLAGE   = 0.75;
const float ANCHOR_SPECIAL   = 0.875;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 tile_color = texture(TEXTURE, UV);
	float r_value = tile_color.r;
	float hex_alpha = tile_color.a;

	// 不在六边形区域内的像素直接透明
	if (hex_alpha < 0.01) {
		COLOR = vec4(0.0, 0.0, 0.0, 0.0);
		return;
	}

	vec2 w_uv = (world_pos + world_offset) * tex_scale;

	vec4 tex_color;
	if (r_value < 0.0625) {
		tex_color = texture(tex_grass, w_uv);
	} else if (r_value < 0.1875) {
		tex_color = texture(tex_forest, w_uv);
	} else if (r_value < 0.3125) {
		tex_color = texture(tex_wasteland, w_uv);
	} else if (r_value < 0.4375) {
		tex_color = texture(tex_desert, w_uv);
	} else if (r_value < 0.5625) {
		tex_color = texture(tex_water, w_uv);
	} else if (r_value < 0.6875) {
		tex_color = texture(tex_mountain, w_uv);
	} else if (r_value < 0.8125) {
		tex_color = texture(tex_village, w_uv);
	} else {
		tex_color = texture(tex_special, w_uv);
	}

	COLOR = vec4(tex_color.rgb, hex_alpha);
}
