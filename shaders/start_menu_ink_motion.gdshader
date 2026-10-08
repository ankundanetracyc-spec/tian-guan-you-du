shader_type canvas_item;

// 极轻微的纸墨呼吸感：不改变画面的阅读性，只让山岚与笔触保持活性。
uniform float drift_strength : hint_range(0.0, 0.004) = 0.0012;
uniform float drift_speed : hint_range(0.0, 1.0) = 0.11;

void fragment() {
	vec2 uv = UV;
	float slow_time = TIME * drift_speed;
	uv.x += sin(uv.y * 8.0 + slow_time) * drift_strength;
	uv.y += cos(uv.x * 6.0 + slow_time * 0.72) * drift_strength * 0.48;
	vec4 ink = texture(TEXTURE, uv);
	// 晨光在很长的周期里轻微起伏，避免静帧感。
	float breath = 0.985 + sin(TIME * 0.09) * 0.015;
	COLOR = vec4(ink.rgb * breath, ink.a);
}
