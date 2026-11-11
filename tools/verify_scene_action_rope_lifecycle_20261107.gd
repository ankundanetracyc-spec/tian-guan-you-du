extends SceneTree

const DUTY_BOARD = preload("res://scripts/world/bai_chuan/YuxuSectDutyBoard.gd")
const ACTION_BUTTON = preload("res://tscn/panel/SceneActionButton.tscn")
var _checks := 0

func _initialize() -> void:
	_run.call_deferred()

func _check(condition: bool, reason: String) -> bool:
	_checks += 1
	if not condition:
		push_error(reason)
		quit(1)
	return condition

func _settle() -> void:
	for frame in range(4):
		await process_frame

func _run() -> void:
	var board := Control.new()
	board.set_script(DUTY_BOARD)
	board.size = Vector2(1920, 1080)
	root.add_child(board)
	board.call("open_board", "")
	await _settle()
	var rope := board.get_node("DutyRope1")
	var container := rope.get_node("Tags")
	var tag := ACTION_BUTTON.instantiate() as Button
	container.add_child(tag)
	rope.call("_refresh_tags")
	if not _check(rope.get("_tags").has(tag), "新增吊牌未纳入绳索布局"):
		return
	# Reproduce the same-frame remove/rebuild/layout sequence used by the duty board.
	container.remove_child(tag)
	tag.queue_free()
	rope.call("_layout_tags")
	if not _check(not rope.get("_tags").has(tag), "重新布局仍引用已经离开父节点的旧吊牌"):
		return
	await _settle()
	for cycle in range(4):
		board.call("open_board", "")
		await _settle()
		board.call("_update_page")
		board.call("_set_source", "private")
		board.call("_set_source", "official")
		board.hide()
		board.show()
		await _settle()
		for row in range(1, 4):
			var row_rope := board.get_node("DutyRope%d" % row)
			for cached_tag in row_rope.get("_tags"):
				if not _check(is_instance_valid(cached_tag) and cached_tag.is_inside_tree() and cached_tag.get_parent() == row_rope.get_node("Tags"), "刷新公事后绳索缓存包含失效吊牌"):
					return
	root.remove_child(board)
	board.free()
	await _settle()
	print("SCENE_ACTION_ROPE_LIFECYCLE_OK: %d checks; detach/rebuild in one frame, repeated board open, source switch, hide/show and teardown" % _checks)
	quit()
