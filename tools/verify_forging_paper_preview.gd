extends Node

var preview: Control
var render_viewport: SubViewport
var output_dir := "res://output/forging-paper-native/1920"
var target_width := 1920
var checks: Array = []
var captures: Array = []

func verify(label: String, passed: bool, detail = null) -> void:
	checks.append({"name": label, "pass": passed, "detail": detail})
	if not passed:
		push_error("FORGE_PAPER_FAIL " + label + " " + str(detail))
		write_result("failed")
		get_tree().quit(1)
		assert(false, label)

func nodes(parent: Node) -> Array:
	var result: Array = []
	for child in parent.get_children():
		result.append(child)
		result.append_array(nodes(child))
	return result

func find_button(text: String) -> Button:
	for node in nodes(preview):
		if node is Button and node.is_visible_in_tree() and node.text.replace(" ", "").begins_with(text):
			return node
	return null

func verify_material_cards(expected := 3) -> void:
	var icons := nodes(preview).filter(func(node): return node is TextureButton and node.name == "MaterialLargeIcon" and node.is_visible_in_tree())
	verify("材料卡使用原图大图标 " + str(expected), icons.size() == expected)
	for icon in icons:
		var details: VBoxContainer = icon.get_parent().get_child(1)
		var selector: Button = details.get_child(0)
		var quantities: HBoxContainer = details.get_child(1)
		var text_width := selector.get_theme_font("font").get_string_size(selector.text, HORIZONTAL_ALIGNMENT_LEFT, -1, selector.get_theme_font_size("font_size")).x
		var frame := selector.get_theme_stylebox("normal")
		verify("材料资料单行且完整容纳 " + selector.text, not selector.text.contains("\n") and text_width + frame.get_content_margin(SIDE_LEFT) + frame.get_content_margin(SIDE_RIGHT) <= selector.size.x + 1, {"width": selector.size.x, "text": text_width})
		verify("图标跨资料与份数两行 " + selector.text, icon.size.x >= 79 and icon.size.y >= selector.size.y + quantities.size.y - 1 and quantities.get_global_rect().position.x >= icon.get_global_rect().end.x - 1, {"icon": icon.size, "selector": selector.size, "quantities": quantities.size})
		verify("材料卡不超出投料栏 " + selector.text, details.get_global_rect().end.x <= icon.get_parent().get_global_rect().end.x + 1)

func verify_header_buttons(back: Button) -> void:
	var header: Control = preview.find_child("ForgingPaperHeader", true, false)
	var reference: StyleBoxTexture = back.get_theme_stylebox("normal")
	var buttons := nodes(header).filter(func(node): return node is Button and node.is_visible_in_tree())
	buttons.append(back)
	verify("顶栏五个按钮共用一种纸面样式", buttons.size() == 5 and buttons.all(func(button): return button.get_theme_stylebox("normal") is StyleBoxTexture and button.get_theme_stylebox("normal").texture.resource_name == reference.texture.resource_name and absf(button.size.y - 46) < 1))
	var text_controls := nodes(header).filter(func(node): return (node is Button or node is Label) and node.is_visible_in_tree())
	text_controls.append(back)
	verify("顶栏标题造诣数值按钮同字级同行高同位置", text_controls.all(func(control): return control.get_theme_font_size("font_size") == 22 and absf(control.size.y - 46) < 1 and absf(control.get_global_rect().position.y - back.get_global_rect().position.y) < 1 and control.get_theme_font("font") == back.get_theme_font("font")))
	verify("顶栏标签垂直居中与按钮对齐", text_controls.all(func(control): return control is not Label or control.vertical_alignment == VERTICAL_ALIGNMENT_CENTER))
	for left_index in text_controls.size():
		for right_index in range(left_index + 1, text_controls.size()):
			verify("顶栏控件互不交叠 " + str(left_index) + "/" + str(right_index), not text_controls[left_index].get_global_rect().intersects(text_controls[right_index].get_global_rect()))
	var scale := header.get_global_transform_with_canvas().get_scale().x
	verify("横条在独立返回前收口且相隔16设计单位", header.get_parent() == back.get_parent() and not header.is_ancestor_of(back) and absf((back.get_global_rect().position.x - header.get_global_rect().end.x) / scale - 16) < .1)
	for fixed in [["ForgingHelpButton", 14], ["CraftingHeaderMetrics", 926]]:
		verify("工艺信息使用同一固定列 " + fixed[0], absf(header.find_child(fixed[0], true, false).position.x - fixed[1]) < .1)
	var spirit_gap: float = (header.find_child("CraftingHeaderEnergyCaption", true, false).get_global_rect().position.x - header.find_child("CraftingHeaderSpiritValue", true, false).get_global_rect().end.x) / scale
	var energy_gap: float = (header.find_child("CraftingHeaderAttainmentCaption", true, false).get_global_rect().position.x - header.find_child("CraftingHeaderEnergyValue", true, false).get_global_rect().end.x) / scale
	verify("灵识精力造诣三组文字之间等距32单位", absf(spirit_gap - 32) < .1 and absf(energy_gap - spirit_gap) < .1, [spirit_gap, energy_gap])
	verify("顶栏灵识读取原试炼容量", header.find_child("CraftingHeaderSpiritValue", true, false).text == "240/240")
	verify("顶栏补上精力并沿用既有初始容量", header.find_child("CraftingHeaderEnergyValue", true, false).text == "100")
	verify("顶栏不再重复炼器标题", not text_controls.any(func(control): return control is Label and control.text.replace(" ", "") == "炼器"))
	verify("灵识整组向资源栏右侧对齐", absf(header.find_child("CraftingHeaderSpiritValue", true, false).get_global_rect().end.x - header.find_child("CraftingHeaderSpirit", true, false).get_global_rect().end.x) < .1)
	var header_paper: StyleBoxTexture = header.get_theme_stylebox("panel")
	verify("横条与人物面板同用原纸面暖色", header_paper.modulate_color.is_equal_approx(Color.WHITE) and header_paper.texture.resource_name.contains("saturation1"))

func settle(frames := 6) -> void:
	for index in frames:
		await get_tree().process_frame

func press(button: Button) -> void:
	verify("按钮可操作 " + button.text, button.is_visible_in_tree() and not button.disabled)
	var position := button.get_global_transform_with_canvas() * (button.size / 2)
	var motion := InputEventMouseMotion.new()
	motion.position = position
	render_viewport.push_input(motion, true)
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = position
		event.button_index = MOUSE_BUTTON_LEFT
		event.pressed = down
		render_viewport.push_input(event, true)
	await settle()

func shot(name: String, clear_hover := true) -> void:
	if clear_hover:
		var motion := InputEventMouseMotion.new()
		motion.position = Vector2(5, 5)
		render_viewport.push_input(motion, true)
	await settle()
	await RenderingServer.frame_post_draw
	var image := render_viewport.get_texture().get_image()
	verify("原生截图尺寸 " + name, image.get_width() == target_width)
	verify("原生截图写入 " + name, image.save_png(output_dir.path_join(name)) == OK)
	captures.append(name)

func write_result(status: String) -> void:
	var result := {"status": status, "checks": checks, "checkCount": checks.size(), "captures": captures, "width": target_width, "gameIntegrated": false}
	var file := FileAccess.open(output_dir.path_join("验收记录.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "  "))

func _ready() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--forge-paper-width="):
			target_width = argument.get_slice("=", 1).to_int()
		if argument.begins_with("--forge-paper-output="):
			output_dir = argument.get_slice("=", 1)
	verify("输出仅在项目output", output_dir.begins_with("res://output/"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_dir))
	get_window().mode = Window.MODE_WINDOWED
	get_window().borderless = true
	get_window().unfocusable = true
	get_window().content_scale_size = Vector2i.ZERO
	get_window().size = Vector2i(target_width, 1080 if target_width == 1920 else 720)
	render_viewport = SubViewport.new()
	render_viewport.name = "NativeCaptureViewport"
	render_viewport.size = Vector2i(target_width, 1080 if target_width == 1920 else 720)
	render_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	render_viewport.handle_input_locally = true
	add_child(render_viewport)
	var display := TextureRect.new()
	display.texture = render_viewport.get_texture()
	display.size = Vector2(render_viewport.size)
	display.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(display)
	preview = load("res://tscn/scene/ForgingPaperPreview.tscn").instantiate()
	render_viewport.add_child(preview)
	await settle(15)
	var state: Dictionary = preview.call("ReviewSnapshot")
	verify("原生控件与只读炉火已就绪", state.fire_ready and state.inputs_visible and state.styled_controls > 30, state.styled_controls)
	var ledger: Dictionary = JSON.parse_string(state.ledger)
	verify("未提交试演账目", ledger.Count == 0 and ledger.Attempts == 0 and ledger.Hours == 0 and ledger.Energy == 0, ledger)
	var back := find_button("返回")
	verify("右上统一返回存在", back != null and back.text == "返回")
	var native_canvas: Control = preview.get_node("NativeForgingRoom").find_child("MaterialCanvas", true, false)
	var scale := native_canvas.get_global_transform_with_canvas().get_scale().x
	var top_left := native_canvas.get_global_transform_with_canvas().origin
	var back_rect := back.get_global_rect()
	var offset := (back_rect.position - top_left) / scale
	verify("返回位置遵守统一边距", absf(offset.x - 1534) < 1 and absf(offset.y - 18) < 1, offset)
	verify_material_cards()
	verify_header_buttons(back)
	var clear := find_button("清空投料")
	var clear_style: StyleBoxTexture = clear.get_theme_stylebox("normal")
	verify("清空投料与顶栏使用同一种普通按钮", clear_style.texture.resource_name.contains("button_regular.png") and clear_style.texture.resource_name == back.get_theme_stylebox("normal").texture.resource_name)
	var ink_states_consistent := true
	for button in nodes(preview):
		if button is not Button or not button.is_visible_in_tree():
			continue
		for ink_state in ["font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
			ink_states_consistent = ink_states_consistent and button.get_theme_color(ink_state) == button.get_theme_color("font_color") and button.get_theme_color("font_shadow_color").a == 0
		for state_name in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
			var selected_suffix := "/selectedTrue" if button.get_meta("forging_paper_selected", false) and state_name != "disabled" else "/selectedFalse"
			verify("按钮各状态都是整面原纸无中央染色 " + button.text.replace("\n", " ") + " " + state_name, button.get_theme_stylebox(state_name) is StyleBoxTexture and button.get_theme_stylebox(state_name).texture.resource_name.ends_with(selected_suffix))
	verify("悬停选中按下文字保持深墨且无暗色文字阴影", ink_states_consistent)
	var selected_tab := find_button("身材")
	verify("分类选中整纸变色且无中央方形染色", selected_tab.get_meta("forging_paper_selected", false) and selected_tab.get_theme_stylebox("normal").texture.resource_name.contains("selectedTrue") and selected_tab.get_theme_stylebox("normal").texture != find_button("质材").get_theme_stylebox("normal").texture and selected_tab.get_theme_color("font_color").get_luminance() < .4)
	await shot("炼器_主界面_" + str(target_width) + ".png")
	var clear_point := clear.get_global_transform_with_canvas() * (clear.size / 2)
	var hover_motion := InputEventMouseMotion.new()
	hover_motion.position = clear_point
	render_viewport.push_input(hover_motion, true)
	await settle()
	await shot("炼器_按钮悬停_" + str(target_width) + ".png", false)
	var held := InputEventMouseButton.new()
	held.position = clear_point
	held.button_index = MOUSE_BUTTON_LEFT
	held.pressed = true
	render_viewport.push_input(held, true)
	await settle()
	verify("真实按下进入反馈状态", [BaseButton.DRAW_PRESSED, BaseButton.DRAW_HOVER_PRESSED].has(clear.get_draw_mode()))
	var pressed_style: StyleBoxTexture = clear.get_theme_stylebox("pressed")
	verify("按下整张纸面变深并下沉一单位", pressed_style.modulate_color.get_luminance() < clear_style.modulate_color.get_luminance() and pressed_style.expand_margin_top == -1 and pressed_style.expand_margin_bottom == 1)
	await shot("炼器_按钮按下_" + str(target_width) + ".png", false)
	var outside_motion := InputEventMouseMotion.new()
	outside_motion.position = Vector2(4, 4)
	render_viewport.push_input(outside_motion, true)
	held = InputEventMouseButton.new()
	held.position = Vector2(4, 4)
	held.button_index = MOUSE_BUTTON_LEFT
	held.pressed = false
	render_viewport.push_input(held, true)
	await settle()
	verify("移出后释放不误触清空", find_button("玄铁") != null)
	var first_icon: TextureButton = nodes(preview).filter(func(node): return node is TextureButton and node.name == "MaterialLargeIcon" and node.is_visible_in_tree())[0]
	var icon_position := first_icon.get_global_transform_with_canvas() * (first_icon.size / 2)
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = icon_position
		event.button_index = MOUSE_BUTTON_LEFT
		event.pressed = down
		render_viewport.push_input(event, true)
	await settle()
	verify("大图标仍打开原生选材", find_button("关闭") != null)
	await press(find_button("关闭"))
	var first_selector := find_button("玄铁")
	var first_quantities: HBoxContainer = first_selector.get_parent().get_child(1)
	var plus: Button = first_quantities.get_children().filter(func(node): return node is Button and node.text == "＋")[0]
	await press(plus)
	var quantity: LineEdit = find_button("玄铁").get_parent().get_child(1).get_children().filter(func(node): return node is LineEdit)[0]
	verify("右侧份数操作仍走原生回调", quantity.text == "4")
	var minus: Button = quantity.get_parent().get_children().filter(func(node): return node is Button and node.text == "－")[0]
	await press(minus)
	verify_material_cards()
	await press(find_button("质材"))
	verify("投料页签可切换", find_button("添加材料") != null)
	verify("切换后仅当前分类带选中纸色", find_button("质材").get_meta("forging_paper_selected", false) and not find_button("身材").get_meta("forging_paper_selected", true))
	verify_material_cards(1)
	await shot("炼器_质材_" + str(target_width) + ".png")
	await press(find_button("铜髓矿"))
	verify("选材弹窗可打开", find_button("关闭") != null)
	await shot("炼器_选材_" + str(target_width) + ".png")
	await press(find_button("关闭"))
	await press(find_button("性材"))
	verify_material_cards(2)
	await shot("炼器_性材_" + str(target_width) + ".png")
	await press(find_button("和材"))
	verify_material_cards(2)
	await shot("炼器_和材_" + str(target_width) + ".png")
	await press(find_button("身材"))
	await press(find_button("器物册"))
	verify("器物册保持空预览", find_button("关闭") != null)
	await shot("炼器_器物册_" + str(target_width) + ".png")
	await press(find_button("关闭"))
	await press(find_button("炼器说明"))
	verify("说明弹窗可打开", find_button("关闭") != null)
	await shot("炼器_说明_" + str(target_width) + ".png")
	await press(find_button("关闭"))
	await press(find_button("开炼"))
	verify("原生只读炉火可进入", not preview.call("ReviewSnapshot").inputs_visible and find_button("跳过演出") != null)
	await press(find_button("跳过演出"))
	verify("原生演出可定器", find_button("返回投料") != null)
	await shot("炼器_炉火定器_" + str(target_width) + ".png")
	await press(find_button("返回投料"))
	verify("演出返回保留原投料", preview.call("ReviewSnapshot").inputs_visible and find_button("玄铁") != null)
	var final: Dictionary = JSON.parse_string(preview.call("ReviewSnapshot").ledger)
	verify("查看与切页不变账目", ledger.Stock == final.Stock and final.Count == 0 and final.Attempts == 0 and final.Hours == 0 and final.Energy == 0)
	await press(find_button("返回"))
	verify("返回只收起独立预览", preview.call("ReviewSnapshot").closed_visible and not preview.call("ReviewSnapshot").preview_visible)
	await press(find_button("继续查看"))
	verify("可继续查看独立样稿", preview.call("ReviewSnapshot").preview_visible)
	write_result("passed")
	preview.call("FlushReviewInterop")
	await settle(2)
	print("FORGE_PAPER_PASS " + str(checks.size()))
	get_tree().quit(0)

