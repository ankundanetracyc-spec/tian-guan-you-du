extends Control

signal back_requested
signal admission_requested
signal life_action_requested(action_id: String, peak_id: String)
signal exchange_purchase_requested(item_path: String, contribution_cost: int)

const SCENE_ACTION_BUTTON = preload("res://tscn/panel/SceneActionButton.tscn")
const HEGUANG_DUTY_BOARD = preload("res://tscn/world/bai_chuan/YuxuSectDutyBoard.tscn")
const SECT_EXCHANGE_PANEL = preload("res://tscn/world/bai_chuan/YuxuSectExchangePanel.tscn")
const HEGUANG_AFFAIRS_BACKGROUND := "res://textures/sect_overview/peaks/yuxu_heguang_affairs_v1.png"

const PEAKS := {
	"heguang": {
		"title": "和光峰",
		"subtitle": "庶务、杂役与供给",
		"body": "杂役弟子、膳房、库房、修缮与宗门传讯都归于此峰。这里是玉虚仙宗最接近凡俗烟火的一处所在。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_heguang_v4.png"
	},
	"guanfu": {
		"title": "观复峰",
		"subtitle": "外门、内门与日常修持",
		"body": "这里是杂役、外门与内门弟子的日常居所、修习与授业活动之处；静室与休息处按身份供弟子使用，月例在此按月发放。真传入宗满一年后，每半年一期的答疑讲席也在此开设，面向外门与内门弟子。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_guanfu_v4.png"
	},
	"zifu": {
		"title": "紫府峰",
		"subtitle": "真传起居、长老与宗门中枢",
		"body": "天灵根入门的真传弟子在此居住并使用专属静室与休息处；首座、长老与宗主也在此处理宗门中枢事务。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_zifu_v4.png"
	},
	"xuanlan": {
		"title": "玄览峰",
		"subtitle": "藏经、研读与传承",
		"body": "可在此研读当前已有权限的典籍；真传首卷仍按师承流程回紫府峰静室研读并汇报。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_xuanlan_v4.png"
	},
	"tianwen": {
		"title": "天问峰",
		"subtitle": "演法、试炼与宗门比试",
		"body": "斗法台、试炼场和论道台分布于峰间，用来检验术法、应敌和临场取舍。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_tianwen_v4.png"
	},
	"tiangong": {
		"title": "天工峰",
		"subtitle": "丹、器、符、阵诸般技艺",
		"body": "丹房：炼丹与丹方；器作：炼器与法宝；云篆：符箓与符印；阵枢：阵法、阵盘与护宗阵枢。四类技艺在同一峰内分院传承。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_tiangong_v3.png"
	},
	"zhizhong": {
		"title": "执中峰",
		"subtitle": "戒律、执法与宗内裁断",
		"body": "门规、惩戒、执法和宗内争端裁断皆由此峰主持，维持各峰之间的秩序。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_zhizhong_v4.png"
	},
	"yaopu": {
		"title": "瑶圃峰",
		"subtitle": "灵植、灵兽与灵材培育",
		"body": "灵药、灵植、灵兽与各类宗门资源在此培育，所得灵材再送往天工峰炼制。",
		"background": "res://textures/sect_overview/peaks/yuxu_peak_yaopu_v3.png"
	}
}

var _current_peak_id := ""
var _admission_root_name := "无灵根"
var _admission_rank := ""
var _admission_member := false
var _admission_context_initialized := false
var _admission_current_rank := ""
var _has_other_affiliation := false
var _life_member := false
var _life_rank := ""
var _life_monthly_allowance := 0
var _life_unclaimed_allowance := 0
var _life_page_open := false
var _sect_is_member := false
var _sect_active_duty_id := ""
var _sect_active_incident_id := ""
var _sect_incident_summaries: Array = []
var _sect_case_task_summaries: Array = []
var _sect_active_case_task_id := ""
var _sect_duty_ready := false
var _sect_duty_stage := 0
var _sect_has_bone := false
var _sect_root_count := 0
var _lineage_is_true_disciple := false
var _lineage_teacher_name := ""
var _lineage_guide_name := ""
var _lineage_book_name := ""
var _lineage_first_volume_complete := false
var _lineage_reported_first_volume := false
var _lineage_lecture_available := false
var _lineage_lecture_status := ""
var _duty_board_open := false
var _duty_board: Control
var _exchange_board_open := false
var _exchange_board: Control
var _sect_contribution_balance := 0
var _life_action_buttons: Array[Button] = []
var _action_buttons: Dictionary = {}

@onready var background: TextureRect = $Background
@onready var info_card: PanelContainer = $InfoCard
@onready var title_label: Label = $InfoCard/Margin/VBox/Title
@onready var subtitle_label: Label = $InfoCard/Margin/VBox/Subtitle
@onready var body_label: Label = $InfoCard/Margin/VBox/Body
@onready var back_button: Button = $BackButton
@onready var admission_button: Button = $ActionRope/Tags/AdmissionButton
@onready var actions: HBoxContainer = $ActionRope/Tags

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	back_button.pressed.connect(_on_back_button_pressed)
	admission_button.pressed.connect(func() -> void: admission_requested.emit())
	_build_life_actions()
	_duty_board = HEGUANG_DUTY_BOARD.instantiate() as Control
	add_child(_duty_board)
	_duty_board.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_duty_board.hide()
	_duty_board.connect("duty_action_requested", Callable(self, "_on_duty_board_action_requested"))
	var exchange_canvas := CanvasLayer.new()
	exchange_canvas.name = "SectExchangeCanvas"
	exchange_canvas.layer = 31
	add_child(exchange_canvas)
	_exchange_board = SECT_EXCHANGE_PANEL.instantiate() as Control
	_exchange_board.name = "YuxuSectExchangePanel"
	exchange_canvas.add_child(_exchange_board)
	_exchange_board.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_exchange_board.hide()
	_exchange_board.connect("back_requested", Callable(self, "_close_exchange_board"))
	_exchange_board.connect("purchase_requested", Callable(self, "_on_exchange_purchase_requested"))
	back_button.z_index = 20
	hide()

func open_peak(peak_id: String) -> void:
	if not PEAKS.has(peak_id):
		return
	_current_peak_id = peak_id
	_life_page_open = false
	_duty_board_open = false
	_duty_board.hide()
	back_button.text = "返回八峰总览"
	info_card.custom_minimum_size = Vector2(760, 190)
	info_card.offset_left = -380.0
	info_card.offset_top = 0.0
	info_card.offset_right = 380.0
	info_card.offset_bottom = 190.0
	var peak: Dictionary = PEAKS[peak_id]
	background.texture = load(str(peak["background"]))
	title_label.text = str(peak["title"])
	subtitle_label.text = str(peak["subtitle"])
	_refresh_content()
	_refresh_admission_button()
	show()
	move_to_front()
	if _can_open_life_page():
		_open_life_page()

func get_peak_journal_entry(peak_id: String) -> Dictionary:
	if not PEAKS.has(peak_id):
		return {}
	var peak: Dictionary = PEAKS[peak_id]
	return {
		"title": str(peak["title"]),
		"subtitle": str(peak["subtitle"]),
		"body": str(peak["body"])
	}

func set_admission_context(root_name: String, rank: String, is_member: bool, current_rank: String, has_other_affiliation: bool) -> void:
	var just_joined := _admission_context_initialized and not _admission_member and is_member
	_admission_root_name = root_name if not root_name.is_empty() else "无灵根"
	_admission_rank = rank
	_admission_member = is_member
	_admission_current_rank = current_rank
	_has_other_affiliation = has_other_affiliation
	_admission_context_initialized = true
	_refresh_content()
	_refresh_admission_button()
	if just_joined:
		var main_scene := get_tree().get_first_node_in_group("main_scene")
		if main_scene != null and main_scene.has_method("on_yuxu_membership_state_changed"):
			main_scene.call("on_yuxu_membership_state_changed")
	if _can_open_life_page():
		_open_life_page()

func refresh_admission_detail() -> void:
	_refresh_content()
	_refresh_admission_button()

func set_life_context(is_member: bool, current_rank: String, monthly_allowance: int, unclaimed_allowance: int) -> void:
	_life_member = is_member
	_life_rank = current_rank
	_life_monthly_allowance = monthly_allowance
	_life_unclaimed_allowance = unclaimed_allowance
	if _life_page_open:
		if _can_open_life_page():
			_refresh_life_page()
		else:
			_close_life_page()
	elif is_visible_in_tree() and _can_open_life_page():
		_open_life_page()
	else:
		_refresh_content()

func set_sect_duty_context(is_member: bool, active_duty_id: String, ready: bool = false, stage: int = 0, has_bone: bool = false, root_count: int = 0, active_incident_id: String = "", incident_summaries: Array = [], case_task_summaries: Array = [], active_case_task_id: String = "") -> void:
	_sect_is_member = is_member
	_sect_active_duty_id = active_duty_id
	_sect_active_incident_id = active_incident_id
	_sect_incident_summaries = incident_summaries.duplicate(true)
	_sect_case_task_summaries = case_task_summaries.duplicate(true)
	_sect_active_case_task_id = active_case_task_id
	_sect_duty_ready = ready
	_sect_duty_stage = stage
	_sect_has_bone = has_bone
	_sect_root_count = root_count
	if is_instance_valid(_duty_board):
		_duty_board.call("set_active_duty", active_duty_id, ready, stage, has_bone, root_count, active_incident_id, _sect_incident_summaries, _sect_case_task_summaries, _sect_active_case_task_id)
	if _duty_board_open and not is_member:
		_close_duty_board()
	elif _life_page_open:
		_refresh_life_page()
	elif is_visible_in_tree():
		_refresh_main_actions()

func set_lineage_context(is_true_disciple: bool, teacher_name: String, guide_name: String, book_name: String, first_volume_complete: bool, reported_first_volume: bool) -> void:
	_lineage_is_true_disciple = is_true_disciple
	_lineage_teacher_name = teacher_name
	_lineage_guide_name = guide_name
	_lineage_book_name = book_name
	_lineage_first_volume_complete = first_volume_complete
	_lineage_reported_first_volume = reported_first_volume
	if _life_page_open:
		_refresh_life_page()
	elif is_visible_in_tree():
		_refresh_main_actions()

func set_lineage_lecture_context(is_available: bool, status: String) -> void:
	_lineage_lecture_available = is_available
	_lineage_lecture_status = status
	if _life_page_open:
		_refresh_life_page()
	elif is_visible_in_tree():
		_refresh_main_actions()

func set_contribution_balance(value: int) -> void:
	_sect_contribution_balance = maxi(0, value)
	if is_instance_valid(_exchange_board):
		_exchange_board.call("set_contribution_balance", _sect_contribution_balance)

func resolve_exchange_purchase(success: bool, current_balance: int, message: String) -> void:
	_sect_contribution_balance = maxi(0, current_balance)
	if is_instance_valid(_exchange_board):
		_exchange_board.call("resolve_purchase", success, _sect_contribution_balance, message)

func _on_exchange_purchase_requested(item_path: String, contribution_cost: int) -> void:
	exchange_purchase_requested.emit(item_path, contribution_cost)

func _on_back_button_pressed() -> void:
	handle_back()

func handle_back() -> bool:
	if _exchange_board_open:
		return bool(_exchange_board.call("handle_back"))
	if _duty_board_open:
		if _duty_board.call("close_detail"):
			return true
		_close_duty_board()
		return true
	if not _current_peak_id.is_empty():
		back_requested.emit()
		return true
	return false

func _refresh_content() -> void:
	if _current_peak_id.is_empty() or not PEAKS.has(_current_peak_id):
		return
	var peak: Dictionary = PEAKS[_current_peak_id]
	title_label.text = str(peak["title"])
	subtitle_label.text = str(peak["subtitle"])
	body_label.visible = false
	if _duty_board_open:
		info_card.hide()
		return
	info_card.show()
	if _life_page_open:
		_refresh_life_page()
	else:
		_refresh_main_actions()

func _refresh_admission_button() -> void:
	# 入宗资格核验现由大世界玉虚节点的“山门迎真”承接，不再从观复峰重复申请。
	admission_button.hide()

func _build_life_actions() -> void:
	var items := [
		["meditation", "静室"],
		["rest", "休息"],
		["teaching", "授业"],
		["claim_allowance", "领取月例"],
		["promotion", "申请晋升"],
		["heguang_duties", "宗门事务"],
		["heguang_peer_qa", "同门答疑"],
		["heguang_banquet", "举办宗门宴席"],
		["sect_exchange", "善功堂"],
		["yaopu_watch", "药园值守"],
		["yaopu_cultivate", "培护灵植"],
		["tianwen_practice", "演法台"],
		["xuanlan_study", "研读典籍"],
		["lineage_choose_teacher", "择师"],
		["lineage_book_selection", "师门选书"],
		["lineage_report", "首卷汇报"],
		["lineage_lecture_checkin", "开设讲席"],
		["lineage_lecture_leave_request", "讲席请假"],
		["tiangong_baigong", "百工"],
		["tiangong_alchemy", "丹房 · 炼丹"],
		["tiangong_forging", "器作 · 炼器"],
		["zhizhong_case_review", "案件核验"],
		["zhizhong_case_docket", "裁断与申诉复核"],
		["zhizhong_case_enforce", "执行终局裁断"],
		["zhizhong_case_handoff", "案卷当事人交接"],
	]
	for item in items:
		var action_id := str(item[0])
		var button := _make_action_button(str(item[1]))
		button.pressed.connect(_on_action_button_pressed.bind(action_id))
		actions.add_child(button)
		_life_action_buttons.append(button)
		_action_buttons[action_id] = button
	for action_id in _action_buttons.keys():
		var button := _action_buttons[action_id] as Button
		if button != null:
			button.hide()

func _on_action_button_pressed(action_id: String) -> void:
	match action_id:
		"heguang_duties":
			_open_duty_board()
		"sect_exchange":
			_open_exchange_board()
		_:
			_emit_life_action(action_id)

func _on_duty_board_action_requested(action_id: String) -> void:
	life_action_requested.emit(action_id, "heguang")

func _make_action_button(button_text: String) -> Button:
	var button := SCENE_ACTION_BUTTON.instantiate() as Button
	button.call("set_action_label", button_text)
	button.custom_minimum_size = Vector2(84, 190)
	button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	return button

func _layout_actions() -> void:
	actions.add_theme_constant_override("separation", 58)
	for child in actions.get_children():
		if child is Button:
			var button := child as Button
			button.custom_minimum_size = Vector2(84, 190)

func _emit_life_action(action_id: String) -> void:
	life_action_requested.emit(action_id, _current_peak_id)

func _is_true_disciple() -> bool:
	return _life_rank in ["真传", "真传弟子"]

func _can_open_life_page() -> bool:
	if not _life_member:
		return false
	var regular_disciple := _life_rank in ["杂役", "杂役弟子", "外门", "外门弟子", "内门", "内门弟子"]
	return (_current_peak_id == "guanfu" and regular_disciple) or (_current_peak_id == "zifu" and _is_true_disciple())

func _open_life_page() -> void:
	if not _can_open_life_page():
		return
	_life_page_open = true
	_duty_board_open = false
	_duty_board.hide()
	# 起居页是峰内当前场景，不是额外的弹层；返回按钮和右键都直接回八峰总览。
	back_button.text = "返回八峰总览"
	info_card.custom_minimum_size = Vector2(760, 190)
	info_card.offset_left = -380.0
	info_card.offset_top = 0.0
	info_card.offset_right = 380.0
	info_card.offset_bottom = 190.0
	_refresh_life_page()

func _close_life_page() -> void:
	_life_page_open = false
	back_button.text = "返回八峰总览"
	info_card.custom_minimum_size = Vector2(760, 190)
	info_card.offset_left = -380.0
	info_card.offset_top = 0.0
	info_card.offset_right = 380.0
	info_card.offset_bottom = 190.0
	_refresh_content()
	_refresh_admission_button()

func _refresh_life_page() -> void:
	if not _life_page_open:
		return
	title_label.text = ("紫府峰" if _current_peak_id == "zifu" else "观复峰") + " · 起居修习"
	subtitle_label.text = "静室、休息与月例"
	body_label.visible = true
	body_label.text = "这里是你在宗门中的基础居所。\n未领取月例：%d 枚灵石；本月月例：%d 枚。未领取部分持续累积，不生利息。" % [_life_unclaimed_allowance, _life_monthly_allowance]
	admission_button.hide()
	var life_actions := ["meditation", "rest", "claim_allowance"]
	if _is_true_disciple():
		life_actions.append("lineage_lecture_leave_request")
		if not _lineage_lecture_status.is_empty():
			body_label.text += "\n真传讲席：%s。" % _lineage_lecture_status
		if _lineage_teacher_name.is_empty():
			life_actions.insert(0, "lineage_choose_teacher")
			body_label.text += "\n师承尚未登记；先选择一位宗内师父。"
		else:
			body_label.text += "\n师父：%s；带教师兄/师姐：%s。" % [_lineage_teacher_name, _lineage_guide_name if not _lineage_guide_name.is_empty() else "待安排"]
			if _lineage_book_name.is_empty():
				body_label.text += "\n可前往玄览峰，在已有门槛允许的首卷中选择一本云息秘籍。"
			elif _lineage_first_volume_complete and not _lineage_reported_first_volume:
				body_label.text += "\n《%s》第一卷已读成，可向带教师兄/师姐汇报。" % _lineage_book_name
				life_actions.append("lineage_report")
			elif _lineage_reported_first_volume:
				body_label.text += "\n《%s》首卷汇报已完成；后续修行由你自行安排。" % _lineage_book_name
			else:
				body_label.text += "\n所选首卷：《%s》。回静室研读，完成第一卷后再来汇报。" % _lineage_book_name
	if _current_peak_id == "guanfu" and _life_rank in ["杂役", "杂役弟子", "外门", "外门弟子", "内门", "内门弟子"]:
		life_actions.insert(2, "teaching")
		body_label.text += "\n授业处可研读已掌握的功法；研读内容与时间消耗沿用现有修行系统。"
		if _life_rank in ["杂役", "杂役弟子", "外门", "外门弟子"]:
			life_actions.append("promotion")
	for action_id in life_actions:
		var button := _action_buttons.get(action_id) as Button
		if button != null:
			button.show()
	_layout_actions()

func _open_duty_board() -> void:
	if _current_peak_id != "heguang" or not _sect_is_member:
		return
	_duty_board_open = true
	_life_page_open = false
	back_button.text = "返回和光峰"
	background.texture = load(HEGUANG_AFFAIRS_BACKGROUND)
	info_card.hide()
	$ActionRope.hide()
	admission_button.hide()
	_duty_board.call("open_board", _sect_active_duty_id)
	_duty_board.call("set_active_duty", _sect_active_duty_id, _sect_duty_ready, _sect_duty_stage, _sect_has_bone, _sect_root_count, _sect_active_incident_id, _sect_incident_summaries, _sect_case_task_summaries, _sect_active_case_task_id)

func _open_exchange_board() -> void:
	if _current_peak_id != "heguang" or not _sect_is_member or _exchange_board_open:
		return
	_exchange_board_open = true
	_duty_board_open = false
	_life_page_open = false
	_duty_board.hide()
	info_card.hide()
	$ActionRope.hide()
	back_button.hide()
	_exchange_board.call("open_panel", _sect_contribution_balance)

func _close_exchange_board() -> void:
	if not _exchange_board_open:
		return
	_exchange_board_open = false
	_exchange_board.hide()
	$ActionRope.show()
	back_button.text = "返回八峰总览"
	back_button.show()
	_refresh_content()
	_refresh_admission_button()


func _close_duty_board() -> void:
	_duty_board_open = false
	_duty_board.hide()
	back_button.text = "返回八峰总览"
	background.texture = load(str(PEAKS[_current_peak_id]["background"]))
	$ActionRope.show()
	info_card.show()
	_refresh_content()
	_refresh_admission_button()

func _refresh_main_actions() -> void:
	for button in _life_action_buttons:
		button.hide()
	if not _current_peak_id.is_empty() and PEAKS.has(_current_peak_id):
		subtitle_label.text = str(PEAKS[_current_peak_id]["subtitle"])
		if _current_peak_id == "guanfu" and _lineage_is_true_disciple and not _lineage_lecture_status.is_empty():
			subtitle_label.text += " · " + _lineage_lecture_status
	if _current_peak_id == "heguang" and _sect_is_member:
		for action_id in ["heguang_duties", "heguang_peer_qa", "heguang_banquet", "sect_exchange"]:
			var button := _action_buttons.get(action_id) as Button
			if button != null:
				button.show()
	if _current_peak_id == "tianwen" and _sect_is_member:
		var practice_button := _action_buttons.get("tianwen_practice") as Button
		if practice_button != null:
			practice_button.show()
	if _current_peak_id == "tiangong" and _sect_is_member:
		for action_id in ["tiangong_baigong", "tiangong_alchemy", "tiangong_forging"]:
			var workshop_button := _action_buttons.get(action_id) as Button
			if workshop_button != null:
				workshop_button.show()
	if _current_peak_id == "zhizhong" and _sect_is_member:
		var case_review_button := _action_buttons.get("zhizhong_case_review") as Button
		if case_review_button != null and _sect_active_duty_id == "east_woods_conflict_review" and not _sect_active_incident_id.is_empty():
			case_review_button.show()
		for action_id in ["zhizhong_case_docket", "zhizhong_case_enforce"]:
			var button := _action_buttons.get(action_id) as Button
			if button != null:
				button.show()
		var handoff_button := _action_buttons.get("zhizhong_case_handoff") as Button
		if handoff_button != null and _has_arrived_case_transport():
			handoff_button.show()
	if _current_peak_id == "yaopu" and _sect_is_member:
		for action_id in ["yaopu_watch", "yaopu_cultivate"]:
			var button := _action_buttons.get(action_id) as Button
			if button != null:
				button.show()
	if _current_peak_id == "xuanlan" and _sect_is_member and _lineage_is_true_disciple and not _lineage_teacher_name.is_empty() and _lineage_book_name.is_empty():
		var lineage_button := _action_buttons.get("lineage_book_selection") as Button
		if lineage_button != null:
			lineage_button.show()
	if _current_peak_id == "guanfu" and _sect_is_member and _lineage_is_true_disciple and _lineage_lecture_available:
		var lecture_button := _action_buttons.get("lineage_lecture_checkin") as Button
		if lecture_button != null:
			lecture_button.show()
	if _current_peak_id == "xuanlan" and _sect_is_member and (not _lineage_is_true_disciple or _lineage_reported_first_volume):
		var study_button := _action_buttons.get("xuanlan_study") as Button
		if study_button != null:
			study_button.show()
	_refresh_admission_button()

func _has_arrived_case_transport() -> bool:
	if _sect_active_case_task_id.is_empty():
		return false
	for task in _sect_case_task_summaries:
		if str(task.get("task_id", "")) == _sect_active_case_task_id and \
			str(task.get("task_type", "")) == "appeal_transport" and \
			str(task.get("status", "")) == "assigned" and \
			str(task.get("journey_status", "")) == "arrived" and \
			bool(task.get("temporary_party_member_active", false)):
			return true
	return false
