extends RefCounted
## Shared source silhouettes and frame layout for basic force and its blood ink.
const FRAMES := 8
const ATLASES := {
	"拳掌": "art/palm_qi_v4/fist_atlas.png",
	"拳掌_掌": "art/palm_qi_v4/heavy_palm_atlas.png",
	"痛击": "art/palm_qi_v5/heavy_atlas.png",
	"架招": "art/palm_qi_v5/guard_atlas.png",
}

static func frame_rect(texture: Texture2D, frame: int) -> Rect2:
	var cell := Vector2(texture.get_size()) / Vector2(4.0, 2.0)
	var index := clampi(frame, 0, FRAMES - 1)
	return Rect2(Vector2(index % 4, floori(index / 4.0)) * cell + Vector2.ONE, cell - Vector2(2, 2))

static func destination_rect(texture: Texture2D, reach: float, progress: float) -> Rect2:
	var source_size := frame_rect(texture, 0).size
	var width := reach * 1.15
	var height := width * source_size.y / source_size.x
	var push := 8.0 * (1.0 - minf(progress / 0.25, 1.0))
	return Rect2(reach - width - push, -height * 0.5, width, height)

static func frame_index(age: float, duration: float) -> int:
	var hold_end := duration * 0.55
	if age < 0.08:
		return mini(int(maxf(age, 0.0) / 0.08 * 4.0), 3)
	if age < hold_end:
		return 3
	return mini(4 + int(clampf((age - hold_end) / (duration - hold_end), 0.0, 1.0) * 4.0), 7)
