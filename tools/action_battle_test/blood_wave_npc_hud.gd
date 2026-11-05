extends "human_sparring_hud.gd"


func trial_title() -> String:
	return "水墨血浪 · 人类陪练"


func ready_title() -> String:
	return "血浪 · 人类NPC演练"


func trial_intro() -> String:
	return "以人物为中心，从手部高度向外扩散血浪。"


func trial_goal() -> String:
	return "三名人类陪练，左键连招，T 重新开始。"


func trial_controls() -> String:
	return "WASD 移动  ·  左键连招  ·  V 血浪预览  ·  P 暂停  ·  T 重开  ·  F1 操作"


func wave_button_rect() -> Rect2:
	return Rect2(Vector2(canvas_size.x * 0.5 + 72, 54), Vector2(146, 30))


func pointer_over_ui() -> bool:
	return super.pointer_over_ui() or wave_button_rect().has_point(pointer)


func _input(event: InputEvent) -> void:
	_update_layout()
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		if not help_open and not battle.configuring and wave_button_rect().has_point(pointer):
			battle.preview_blood_wave()
			get_viewport().set_input_as_handled()
			return
	super._input(event)


func _draw() -> void:
	super._draw()
	if help_open or battle.configuring or battle.hp <= 0 or battle.enemy_hp <= 0:
		return
	if not battle.simulation_paused:
		_button(wave_button_rect(), "V  血浪预览")


func _draw_help() -> void:
	var origin := _dialog("血浪演练操作", "从人物中心的手部高度放浪，鼠标指定方向。", 335.0)
	var lines := [
		"WASD / 方向键：移动     左键：拳掌四段     空格：错步",
		"右键：痛击     Q：架招     E：催血     R：聚炁",
		"V / 上方按钮：血浪效果预览，不造成伤害。",
		"P：开始／暂停／继续     T：同组重开",
		"Tab：装配     F2：判定对照     Esc：关闭面板／返回首页",
	]
	for index in range(lines.size()):
		_text(lines[index], origin + Vector2(28, 113 + index * 32), 14)
