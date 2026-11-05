extends SceneTree
class Base extends Node2D:
	func read_mouse() -> Vector2:
		return get_global_mouse_position()
class Replay extends Base:
	@warning_ignore("native_method_override")
	func get_global_mouse_position() -> Vector2:
		return Vector2(123,456)
func _initialize() -> void:
	var replay := Replay.new()
	root.add_child(replay)
	print("REPLAY MOUSE: ",replay.read_mouse())
	replay.queue_free()
	quit()
