extends Control

signal duty_action_requested(action_id: String)

const SCENE_ACTION_BUTTON = preload("res://tscn/panel/SceneActionButton.tscn")
const ROPE_SCRIPT = preload("res://scripts/ui/SceneActionRope.gd")
const UI_FONT = preload("res://resources/font/LXGWZhenKaiSlabGB-Regular.ttf")
const INCIDENT_DUTY_ID := "east_woods_conflict_review"
const INCIDENTS_PER_ROW := 7

const TAG_SIZE := Vector2(90.0, 150.0)
const TAG_GAP := 82.0
# Unimplemented narrative drafts; never used to populate the board until their world events exist.
const UNIMPLEMENTED_DUTY_DRAFTS := [
	{
		"title": "云息 · 玄基",
		"rows": [
			{
				"top": 0.66,
				"edge": 0.585,
				"realm": "云息境可尝试，玄基境处理更稳妥",
				"tasks": [
					["cloud_east_corridor", "巡查", "外院东廊灯符巡查", "外院东廊", "逐处核对沿廊灯符与符座", "灯符齐全、符座无松脱"],
					["cloud_gate_log", "记录", "山门石阶值守记录", "山门石阶", "核记当值时段与来访事由", "时段与事由逐项登记"],
					["cloud_kitchen_message", "传讯", "膳房与药庐交接传讯", "膳房外廊", "将当日交接文书送至药庐值处", "文书交到当值弟子手中"],
					["cloud_store_count", "清点", "外院器具清点", "外院公用器具间", "按册核对借出与归还的器具", "册上数量与实物相符"],
					["cloud_newcomer_guide", "接引", "新入弟子路线引导", "山门内阶", "带新入弟子认清观复峰与和光峰往返道路", "沿途地点交代清楚且无人走失"],
					["cloud_morning_register", "核对", "晨钟台修习名录核对", "晨钟台", "对照当日到场记录与名录", "缺席与补记情况注明缘由"]
				]
			},
			{
				"top": 0.45,
				"edge": 0.375,
				"realm": "云息后期可尝试，玄基境完成更从容",
				"tasks": [
					["mixed_channel_inspect", "勘验", "云渠石壁阵纹复核", "云渠西侧石壁", "沿石壁检查阵纹衔接与水痕变化", "可见断纹和新生水痕都作记录"],
					["mixed_yaopu_delivery", "护送", "瑶圃峰文书送达", "瑶圃峰外院", "将封存文书交给当值执事", "封口完整并由对方确认收讫"],
					["mixed_hillside_inquiry", "问询", "山腰巡道行旅问询", "山腰巡道", "向沿途行旅核实近来道路异状", "只记录可核实的时间、位置与现象"],
					["mixed_archive_corridor", "巡查", "玄览峰外廊巡查", "玄览峰外藏经廊", "查看廊门、窗扇及封存标记", "发现异常时不擅自入内并及时回报"],
					["mixed_courtyard_record", "登记", "弟子院舍损耗登记", "外院弟子院舍", "逐处记录公用设施的损坏与位置", "损耗描述明确且位置可复查"],
					["mixed_peak_handover", "协调", "三峰事务交接核验", "和光峰传务廊", "核实观复峰、天工峰送来的交接记录", "记录双方经手人与交接时刻"]
				]
			},
			{
				"top": 0.24,
				"edge": 0.165,
				"realm": "玄基境较为合适，云息境不建议独自承办",
				"tasks": [
					["xuji_outer_patrol", "巡防", "护山阵外沿巡防", "护山阵西侧外沿", "沿既定巡线查看阵光与界桩", "巡线全程无遗漏且异样逐一标记"],
					["xuji_registry_review", "复核", "宗务册目交叉复核", "和光峰册房", "交叉核对两份当月事务记录", "差异项目注明原文位置与经手人"],
					["xuji_rock_survey", "勘察", "溪口阵基沉降勘察", "山溪西口阵基", "比较阵基石面、刻线与周边土痕", "判断依据可复查且不移动阵基石"],
					["xuji_outer_inquiry", "调查", "外院争执经过调查", "外院讲习场", "分别核实双方陈述与现场经过", "区分亲见事实和转述内容后回报"],
					["xuji_transfer_audit", "核验", "跨峰封札交接核验", "天工峰与执中峰交界驿廊", "陪同核验封札编号、封口和签收记录", "编号一致、封口无损、签收人明确"],
					["xuji_road_watch", "巡查", "北侧落石风险巡视", "北侧山道转折处", "查看坡面新裂痕与落石堆积方向", "标清风险位置并确认绕行路线可通"]
				]
			}
		]
	},
	{
		"title": "金丹 · 元婴",
		"rows": [
			{
				"top": 0.66,
				"edge": 0.585,
				"realm": "金丹境可尝试，元婴境支援更稳妥",
				"tasks": [
					["jindan_south_route", "巡查", "南线巡道异状查访", "南侧巡山道", "复查近期报来的路面异状与行迹", "区分旧痕、新痕及无法确认的迹象"],
					["jindan_transcript_review", "核对", "外务文书交叉核对", "和光峰传务廊", "核对外务记录与各峰回执", "缺失回执标明经手环节并回报"],
					["jindan_peak_meeting", "议事", "三峰联务议事记录", "和光峰议事院", "记录观复峰、天工峰与执中峰议定事项", "决议与待办分别记清，不擅改原意"],
					["jindan_guest_route", "接引", "外来执事山门接引", "主山门内阶", "接引来访执事至指定峰务会面处", "核明来访身份并完成交接"],
					["jindan_notice_delivery", "通报", "护山阵检修通报", "各峰传讯亭", "向指定峰务处递送检修时段通报", "相关地点与时段无遗漏"],
					["jindan_boundary_visit", "访查", "辖域边界例行访查", "百川辖域北界驿道", "核实沿线驿亭收到的近况记录", "回报仅包含可核验的地点和事件"]
				]
			},
			{
				"top": 0.45,
				"edge": 0.375,
				"realm": "金丹境可担纲，元婴境完成更从容",
				"tasks": [
					["mid_crosspeak_review", "协查", "跨峰事务差异协查", "宗务册房", "沿事务记录追查一处未闭合的交接", "查明最后经手节点并附可复核凭据"],
					["mid_array_review", "核验", "护山阵节点复核", "护山阵东侧节点", "检查节点灵光、刻纹和阵位响应", "按既定阵位记录异常，不擅自调阵"],
					["mid_seal_escort", "护送", "封存卷宗跨峰护送", "执中峰外议事廊", "将封存卷宗护送至指定保管处", "全程交接留痕且封存完整"],
					["mid_remote_patrol", "巡防", "外沿灵脉巡防复核", "灵脉西南巡线", "沿既定界标比对灵脉波动记录", "不离开标定巡线并标出波动位置"],
					["mid_incident_review", "复查", "山道异象后续复查", "北侧山道转折处", "回访此前登记的异象位置与周边变化", "确认变化、未变或无法判断三类结果"],
					["mid_dispatch_audit", "调度", "多峰值守轮次调度", "和光峰传务院", "核对各峰提交的值守轮次与空缺时段", "调整建议不冲突并回呈各峰确认"]
				]
			},
			{
				"top": 0.24,
				"edge": 0.165,
				"realm": "元婴境较为合适，金丹境不建议独自承办",
				"tasks": [
					["yuanying_incident_response", "处置", "辖域急报会同处置", "百川辖域北界驿道", "会同当地驻守修士查明急报来源与影响", "先确认影响范围，处置过程逐项留档"],
					["yuanying_vein_survey", "勘察", "灵脉波动源头勘察", "北界灵脉主支交汇处", "循已有观测记录核查波动起点", "不得单独深入未确认区域并及时回报"],
					["yuanying_dispute_review", "评议", "跨势力通行争议评议", "百川北界会客驿", "核对双方所述约定、时序与现场记录", "只据已核实材料提出处置意见"],
					["yuanying_guard_assignment", "护卫", "重要传承会晤护卫", "玄览峰外会晤道场", "确认进出路线与外围守护交接", "不得接触或擅自查看传承内容"],
					["yuanying_outer_audit", "清查", "辖域驿亭交接清查", "北界三处驿亭", "逐亭核验来往文牒与交接存档", "缺项明确对应驿亭和责任时段"],
					["yuanying_peak_review", "督查", "多峰联务执行督查", "和光峰议事院", "复查联务决议中尚未回报的事项", "仅核对已定事项，不擅自追加新任务"]
				]
			}
		]
	}
]

# Only duties with a real start/completion event are published.
const IMPLEMENTED_OFFICIAL_DUTY_PAGES := [
	{
		"title": "云息 · 玄基",
		"rows": [
			{"top": 0.66, "edge": 0.585, "realm": "云息初期可承办；按要求巡看并及时回报", "tasks": [
				["yaopu_watch", "值守", "瑶圃药园值守", "玉虚仙宗·瑶圃峰药园", "巡看灵土、苗圃与药架，按时完成当值", "完成四个游戏时辰的药园值守；正式轮值按数日安排，此时长仅供短测", 5],
				["yaopu_cultivate_mist_bell", "培育", "培育雾铃花", "玉虚仙宗·瑶圃峰雾铃园", "照料宗门苗株并等待生长，采收后大部分交宗门入库", "照料一株并等待二十四个游戏时辰，采收雾铃花四株：三株交宗门、一株自留；此为短测周期", 10]
			]},
			{"top": 0.45, "edge": 0.375, "realm": "云息境可承办；野外行事留意妖兽", "tasks": [
				["east_woods_patrol", "巡查", "东林地巡线", "东林地·林缘至静冠洼地巡线", "沿标定路线巡回并查看沿途有无异常；通常无异常，按时走完全程即可", "沿路线至少推进四个节点，抵达静冠洼地复核后，返回和光峰事务处回报；节点耗时按基础值与当前遁速动态结算，不要求探查、采集或杀怪", 5],
				["beast_bone_submission", "提交", "上缴兽骨", "和光峰·宗门事务处", "将现有妖兽掉落物兽骨交由宗门登记", "提交行囊中的兽骨一份；兽骨须来自现有妖兽掉落，核验后交宗门", 5],
				["bitter_moss_root_submission", "采集", "苦苔根交付", "东林地 → 和光峰", "为宗门筹集苦苔根，可在东林地林地、阴湿地带探查采集", "携带苦苔根两份回和光峰交付；已有存货亦可交付，提交时才扣物并核发贡献", 5]
			]},
			{"top": 0.24, "edge": 0.165, "realm": "云息境可承办；天问峰演法台", "tasks": [
				["tianwen_practice", "演法", "林地凶狼演法", "玉虚仙宗·天问峰演法台", "完成一场现有凶狼演练并回传战斗结果", "完成一次演法并回传实际战果；不要求获胜，不掉落物品", 5]
			]}
		]
	},
	{
		"title": "各境共通",
		"rows": [
			{"top": 0.66, "edge": 0.585, "realm": "不限境界；须有一卷当前可研读的秘籍", "tasks": []},
			{"top": 0.45, "edge": 0.375, "realm": "不限境界；静室按弟子身份确定", "tasks": [
				["study_chapter", "研习", "静室研读一卷", "观复峰静室 / 紫府峰静室", "依弟子身份在所属静室完成一卷当前可研读的秘籍", "秘籍章节研读完成", 5]
			]},
			{"top": 0.24, "edge": 0.165, "realm": "当前仅开放已接入完成事件的宗门事务", "tasks": []}
		]
	}
]

var _page_index := 0
var _source_id := "official"
var _active_duty_id := ""
var _active_task_id := ""
var _active_incident_id := ""
var _active_duty_ready := false
var _active_duty_stage := 0
var _has_bone := false
var _root_count := 0
var _incident_summaries: Array = []
var _selected_duty_id := ""
var _hovered_duty_id := ""
var _duty_by_id: Dictionary = {}
var _pages_by_source: Dictionary = {}
var _source_buttons: Dictionary = {}
var _tag_containers: Array[HBoxContainer] = []
var _hover_timer: Timer
var _page_label: Label
var _motto: Label
var _preview: PanelContainer
var _preview_title: Label
var _preview_body: Label
var _detail_shade: ColorRect
var _detail_panel: PanelContainer
var _detail_title: Label
var _detail_realm: Label
var _detail_body: RichTextLabel
var _accept_button: Button
var _cancel_button: Button
var _empty_state: VBoxContainer

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build_task_index()
	_build_source_tabs()
	_build_page_header()
	_build_preview()
	_build_rope_rows()
	_build_empty_state()
	_build_detail_panel()
	_hover_timer = Timer.new()
	_hover_timer.one_shot = true
	_hover_timer.wait_time = 1.5
	_hover_timer.timeout.connect(_on_hover_timer_timeout)
	add_child(_hover_timer)
	hide()

func open_board(active_duty_id: String) -> void:
	_source_id = "official"
	_page_index = 0
	_active_duty_id = active_duty_id
	_selected_duty_id = ""
	_clear_hover()
	_update_source_tabs()
	_detail_panel.hide()
	_update_page()
	show()
	move_to_front()

func set_active_duty(active_duty_id: String, ready: bool = false, stage: int = 0, has_bone: bool = false, root_count: int = 0, active_incident_id: String = "", incident_summaries: Array = []) -> void:
	var previous_incident_ids := _incident_summary_ids(_incident_summaries)
	var next_incident_ids := _incident_summary_ids(incident_summaries)
	_active_duty_id = active_duty_id
	_active_incident_id = active_incident_id
	_active_task_id = INCIDENT_DUTY_ID + ":" + active_incident_id if active_duty_id == INCIDENT_DUTY_ID and not active_incident_id.is_empty() else active_duty_id
	_active_duty_ready = ready
	_active_duty_stage = stage
	_has_bone = has_bone
	_root_count = maxi(0, root_count)
	_incident_summaries = incident_summaries.duplicate(true)
	if previous_incident_ids != next_incident_ids:
		_rebuild_task_index()
		if is_visible_in_tree():
			_update_page()
	if not _selected_duty_id.is_empty():
		_refresh_detail()

func _incident_summary_ids(summaries: Array) -> String:
	var ids: Array[String] = []
	for incident in summaries:
		if incident is Dictionary:
			ids.append(str(incident.get("incident_id", "")))
	return "|".join(ids)

func close_detail() -> bool:
	if not _detail_panel.visible:
		return false
	_detail_panel.hide()
	_detail_shade.hide()
	_selected_duty_id = ""
	return true

func _build_task_index() -> void:
	_rebuild_task_index()

func _rebuild_task_index() -> void:
	var official_pages: Array = IMPLEMENTED_OFFICIAL_DUTY_PAGES.duplicate(true)
	var incidents_per_page := INCIDENTS_PER_ROW * 3
	var incident_page_count := ceili(float(_incident_summaries.size()) / float(incidents_per_page))
	for page_index in range(incident_page_count):
		var rows: Array = []
		for row_index in range(3):
			var tasks: Array = []
			var first_index := page_index * incidents_per_page + row_index * INCIDENTS_PER_ROW
			var last_index := mini(first_index + INCIDENTS_PER_ROW, _incident_summaries.size())
			for incident_index in range(first_index, last_index):
				var incident: Dictionary = _incident_summaries[incident_index]
				var incident_id := str(incident.get("incident_id", ""))
				if incident_id.is_empty():
					continue
				var attacker_name := str(incident.get("attacker_npc_name", incident.get("attacker_npc_id", "发起方")))
				var target_name := str(incident.get("target_npc_name", incident.get("target_npc_id", "对方")))
				var occurred_at := str(incident.get("occurred_at", "时间未载"))
				var route_node_id := str(incident.get("route_node_id", "地点未载"))
				var task_id := INCIDENT_DUTY_ID + ":" + incident_id
				var title := "东林冲突 · %s与%s" % [attacker_name, target_name]
				var place := "东林地·深入林地（%s）" % route_node_id
				var action := "接取这一件已结束冲突的事实核验事务；到执中峰查看来源事实并上呈"
				var requirement := "案卷 %s；发生时间 %s；双方身份 ID 与具体事实在执中峰查看" % [incident_id, occurred_at]
				tasks.append([task_id, "核验", title, place, action, requirement, 5])
			rows.append({
				"top": [0.66, 0.45, 0.24][row_index],
				"edge": [0.585, 0.375, 0.165][row_index],
				"realm": "执中峰事实核验；只核对并上呈，不作责任判断",
				"tasks": tasks
			})
		official_pages.append({"title": "东林地案件 · %d" % (page_index + 1), "rows": rows})
	_pages_by_source["official"] = official_pages
	_pages_by_source["private"] = _make_empty_pages(official_pages)
	_duty_by_id.clear()
	for source_id in _pages_by_source:
		for page in _pages_by_source[source_id]:
			for row in page["rows"]:
				for raw_task in row["tasks"]:
					var task: Dictionary = _task_from_row(raw_task, str(row["realm"]))
					task["source"] = str(source_id)
					_duty_by_id[str(task["id"])] = task

func _make_empty_pages(reference_pages: Array) -> Array:
	var empty_pages: Array = []
	for reference_page in reference_pages:
		var empty_rows: Array = []
		for reference_row in reference_page["rows"]:
			var empty_row: Dictionary = reference_row.duplicate()
			empty_row["tasks"] = []
			empty_rows.append(empty_row)
		empty_pages.append({"title": reference_page["title"], "rows": empty_rows})
	return empty_pages

func _active_pages() -> Array:
	return _pages_by_source.get(_source_id, IMPLEMENTED_OFFICIAL_DUTY_PAGES)

func _task_from_row(raw_task: Array, realm_judgment: String) -> Dictionary:
	var task_id := str(raw_task[0])
	var task_type := str(raw_task[1])
	var title := str(raw_task[2])
	var place := str(raw_task[3])
	var action := str(raw_task[4])
	var requirement := str(raw_task[5])
	var reward := int(raw_task[6]) if raw_task.size() > 6 else 0
	var incident_id := task_id.substr((INCIDENT_DUTY_ID + ":").length()) if task_id.begins_with(INCIDENT_DUTY_ID + ":") else ""
	var detail := "%s。前往[u]%s[/u]，%s；完成要求为[u]%s[/u]。" % [realm_judgment, place, action, requirement]
	if reward > 0:
		detail += "事务核验通过后核发[u]%d点贡献[/u]。" % reward
	return {
		"id": task_id,
		"type": task_type,
		"title": title,
		"place": place,
		"realm": realm_judgment,
		"preview": "%s · %s贡献" % [realm_judgment, reward] if reward > 0 else "%s · %s" % [realm_judgment, place],
		"reward": reward,
		"detail": detail,
		"incident_id": incident_id
	}

func _build_source_tabs() -> void:
	var tabs := HBoxContainer.new()
	tabs.name = "SourceSwitch"
	tabs.anchor_left = 0.5
	tabs.anchor_right = 0.5
	tabs.offset_left = -174.0
	tabs.offset_top = 14.0
	tabs.offset_right = 174.0
	tabs.offset_bottom = 54.0
	tabs.alignment = BoxContainer.ALIGNMENT_CENTER
	tabs.add_theme_constant_override("separation", 12)
	add_child(tabs)

	for source in [
		{"id": "official", "label": "宗门公事"},
		{"id": "private", "label": "同门私委"}
	]:
		var source_id := str(source["id"])
		var button := Button.new()
		button.name = "Source_%s" % source_id.capitalize()
		button.text = str(source["label"])
		button.custom_minimum_size = Vector2(160.0, 40.0)
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(_set_source.bind(source_id))
		tabs.add_child(button)
		_source_buttons[source_id] = button
	_update_source_tabs()

func _update_source_tabs() -> void:
	for source_id in _source_buttons:
		var button := _source_buttons[source_id] as Button
		var selected := str(source_id) == _source_id
		var normal := _make_tab_style(selected)
		var hover := normal.duplicate() as StyleBoxFlat
		hover.bg_color = Color(0.77, 0.68, 0.49, 0.99) if selected else Color(0.24, 0.21, 0.16, 0.98)
		button.add_theme_stylebox_override("normal", normal)
		button.add_theme_stylebox_override("hover", hover)
		button.add_theme_stylebox_override("pressed", hover)
		button.add_theme_color_override("font_color", Color(0.20, 0.17, 0.12, 1.0) if selected else Color(0.94, 0.89, 0.77, 1.0))
		button.add_theme_color_override("font_hover_color", Color(0.16, 0.14, 0.10, 1.0) if selected else Color(0.99, 0.95, 0.85, 1.0))
		_apply_font(button, 18)

func _make_tab_style(selected: bool) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.75, 0.66, 0.47, 0.96) if selected else Color(0.14, 0.13, 0.10, 0.92)
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.border_color = Color(0.78, 0.66, 0.43, 0.95) if selected else Color(0.55, 0.47, 0.32, 0.70)
	style.corner_radius_top_left = 5
	style.corner_radius_top_right = 5
	style.corner_radius_bottom_right = 5
	style.corner_radius_bottom_left = 5
	return style

func _set_source(source_id: String) -> void:
	if source_id == _source_id or not _pages_by_source.has(source_id):
		return
	_source_id = source_id
	_page_index = 0
	_clear_hover()
	close_detail()
	_update_source_tabs()
	_update_page()

func _build_empty_state() -> void:
	_empty_state = VBoxContainer.new()
	_empty_state.name = "PrivateEmptyState"
	_empty_state.anchor_left = 0.5
	_empty_state.anchor_top = 0.5
	_empty_state.anchor_right = 0.5
	_empty_state.anchor_bottom = 0.5
	_empty_state.offset_left = -330.0
	_empty_state.offset_top = -34.0
	_empty_state.offset_right = 330.0
	_empty_state.offset_bottom = 34.0
	_empty_state.alignment = BoxContainer.ALIGNMENT_CENTER
	_empty_state.add_theme_constant_override("separation", 8)
	_empty_state.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_empty_state)

	var title := Label.new()
	title.text = "当前暂无可承接的同门私委"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_apply_font(title, 23)
	title.add_theme_color_override("font_color", Color(0.22, 0.19, 0.15, 0.96))
	title.add_theme_color_override("font_shadow_color", Color(0.93, 0.88, 0.75, 0.90))
	title.add_theme_constant_override("shadow_offset_x", 1)
	title.add_theme_constant_override("shadow_offset_y", 1)
	_empty_state.add_child(title)

	var hint := Label.new()
	hint.text = "此处只刊登同门托付的事务，与宗门公事分开"
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_apply_font(hint, 16)
	hint.add_theme_color_override("font_color", Color(0.34, 0.30, 0.23, 0.92))
	hint.add_theme_color_override("font_shadow_color", Color(0.93, 0.88, 0.75, 0.85))
	hint.add_theme_constant_override("shadow_offset_x", 1)
	hint.add_theme_constant_override("shadow_offset_y", 1)
	_empty_state.add_child(hint)
	_empty_state.hide()

func _source_has_tasks() -> bool:
	for page in _active_pages():
		for row in page["rows"]:
			if not row["tasks"].is_empty():
				return true
	return false

func _build_page_header() -> void:
	var page_bar := HBoxContainer.new()
	page_bar.name = "PageSwitch"
	page_bar.anchor_left = 0.5
	page_bar.anchor_right = 0.5
	page_bar.offset_left = -200.0
	page_bar.offset_top = 62.0
	page_bar.offset_right = 200.0
	page_bar.offset_bottom = 102.0
	page_bar.alignment = BoxContainer.ALIGNMENT_CENTER
	page_bar.add_theme_constant_override("separation", 12)
	add_child(page_bar)

	var previous := _make_small_button("‹")
	previous.custom_minimum_size = Vector2(52, 38)
	previous.pressed.connect(func() -> void: _set_page(_page_index - 1))
	page_bar.add_child(previous)
	_page_label = Label.new()
	_page_label.custom_minimum_size = Vector2(210, 38)
	_page_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_page_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_apply_font(_page_label, 19)
	page_bar.add_child(_page_label)
	var next := _make_small_button("›")
	next.custom_minimum_size = Vector2(52, 38)
	next.pressed.connect(func() -> void: _set_page(_page_index + 1))
	page_bar.add_child(next)

	_motto = Label.new()
	_motto.name = "Motto"
	_motto.anchor_left = 0.5
	_motto.anchor_right = 0.5
	_motto.offset_left = -180.0
	_motto.offset_top = 108.0
	_motto.offset_right = 180.0
	_motto.offset_bottom = 150.0
	_motto.text = "同修共进"
	_motto.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_motto.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_apply_font(_motto, 28)
	_motto.add_theme_color_override("font_color", Color(0.22, 0.20, 0.16, 0.98))
	add_child(_motto)

func _build_preview() -> void:
	_preview = PanelContainer.new()
	_preview.name = "HoverPreview"
	_preview.anchor_left = 0.5
	_preview.anchor_top = 0.125
	_preview.anchor_right = 0.5
	_preview.anchor_bottom = 0.125
	_preview.offset_left = -385.0
	_preview.offset_top = 0.0
	_preview.offset_right = 385.0
	_preview.offset_bottom = 88.0
	_preview.add_theme_stylebox_override("panel", _make_paper_style(0.93))
	_preview.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_preview)
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 20)
	margin.add_theme_constant_override("margin_right", 20)
	margin.add_theme_constant_override("margin_top", 10)
	margin.add_theme_constant_override("margin_bottom", 10)
	_preview.add_child(margin)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 4)
	margin.add_child(box)
	_preview_title = Label.new()
	_preview_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_apply_font(_preview_title, 19)
	box.add_child(_preview_title)
	_preview_body = Label.new()
	_preview_body.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_preview_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_apply_font(_preview_body, 15)
	_preview_body.add_theme_color_override("font_color", Color(0.31, 0.28, 0.22, 1.0))
	box.add_child(_preview_body)
	_preview.hide()

func _build_rope_rows() -> void:
	for visual_row in range(3):
		var data_row_index := 2 - visual_row
		var row: Dictionary = IMPLEMENTED_OFFICIAL_DUTY_PAGES[0]["rows"][data_row_index]
		var rope := Control.new()
		rope.name = "DutyRope%d" % (visual_row + 1)
		rope.set_script(ROPE_SCRIPT)
		rope.set("action_container_path", NodePath("Tags"))
		rope.set("tag_size", TAG_SIZE)
		rope.set("tag_gap", TAG_GAP)
		rope.set("tag_top_ratio", float(row["top"]))
		rope.set("rope_edge_height_ratio", float(row["edge"]))
		rope.set("rope_sag_ratio", 0.05)
		rope.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		rope.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var tags := HBoxContainer.new()
		tags.name = "Tags"
		tags.alignment = BoxContainer.ALIGNMENT_CENTER
		tags.mouse_filter = Control.MOUSE_FILTER_IGNORE
		rope.add_child(tags)
		add_child(rope)
		_tag_containers.append(tags)

func _build_detail_panel() -> void:
	_detail_shade = ColorRect.new()
	_detail_shade.name = "DetailShade"
	_detail_shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_detail_shade.color = Color(0.10, 0.09, 0.07, 0.20)
	_detail_shade.mouse_filter = Control.MOUSE_FILTER_STOP
	_detail_shade.hide()
	add_child(_detail_shade)

	_detail_panel = PanelContainer.new()
	_detail_panel.name = "DutyDetail"
	_detail_panel.anchor_left = 0.5
	_detail_panel.anchor_top = 0.5
	_detail_panel.anchor_right = 0.5
	_detail_panel.anchor_bottom = 0.5
	_detail_panel.offset_left = -470.0
	_detail_panel.offset_top = -228.0
	_detail_panel.offset_right = 470.0
	_detail_panel.offset_bottom = 228.0
	_detail_panel.add_theme_stylebox_override("panel", _make_paper_style(0.98))
	_detail_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_detail_panel.hide()
	add_child(_detail_panel)

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 36)
	margin.add_theme_constant_override("margin_right", 36)
	margin.add_theme_constant_override("margin_top", 25)
	margin.add_theme_constant_override("margin_bottom", 25)
	_detail_panel.add_child(margin)
	var layout := VBoxContainer.new()
	layout.alignment = BoxContainer.ALIGNMENT_CENTER
	layout.add_theme_constant_override("separation", 14)
	margin.add_child(layout)
	_detail_title = Label.new()
	_detail_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_detail_title.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_apply_font(_detail_title, 28)
	_detail_title.add_theme_color_override("font_color", Color(0.18, 0.16, 0.12, 1.0))
	layout.add_child(_detail_title)
	_detail_realm = Label.new()
	_detail_realm.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_apply_font(_detail_realm, 16)
	_detail_realm.add_theme_color_override("font_color", Color(0.38, 0.30, 0.20, 1.0))
	layout.add_child(_detail_realm)
	_detail_body = RichTextLabel.new()
	_detail_body.bbcode_enabled = true
	_detail_body.fit_content = true
	_detail_body.scroll_active = false
	_detail_body.custom_minimum_size = Vector2(0, 158)
	_detail_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_detail_body.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_detail_body.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_detail_body.add_theme_color_override("default_color", Color(0.18, 0.17, 0.14, 1.0))
	_apply_font(_detail_body, 20)
	layout.add_child(_detail_body)
	var actions := HBoxContainer.new()
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", 24)
	layout.add_child(actions)
	_accept_button = _make_small_button("接取")
	_accept_button.custom_minimum_size = Vector2(150, 46)
	_accept_button.pressed.connect(_on_accept_pressed)
	actions.add_child(_accept_button)
	_cancel_button = _make_small_button("取消")
	_cancel_button.custom_minimum_size = Vector2(150, 46)
	_cancel_button.pressed.connect(_on_cancel_pressed)
	actions.add_child(_cancel_button)

func _update_page() -> void:
	if _page_label == null:
		return
	var pages := _active_pages()
	_page_index = clampi(_page_index, 0, pages.size() - 1)
	var page: Dictionary = pages[_page_index]
	_page_label.text = str(page["title"])
	var page_bar := get_node_or_null("PageSwitch")
	if page_bar != null:
		var previous := page_bar.get_child(0) as Button
		var next := page_bar.get_child(2) as Button
		previous.disabled = _page_index <= 0
		next.disabled = _page_index >= pages.size() - 1
	_clear_rows()
	for visual_row in range(3):
		var row: Dictionary = page["rows"][2 - visual_row]
		var container := _tag_containers[visual_row]
		var rope := container.get_parent()
		rope.set("tag_top_ratio", float(row["top"]))
		rope.set("rope_edge_height_ratio", float(row["edge"]))
		for raw_task in row["tasks"]:
			var task: Dictionary = _task_from_row(raw_task, str(row["realm"]))
			task["source"] = _source_id
			var button := SCENE_ACTION_BUTTON.instantiate() as Button
			button.custom_minimum_size = TAG_SIZE
			button.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
			button.call("set_action_label", str(task["type"]))
			button.pressed.connect(_on_duty_pressed.bind(str(task["id"])))
			button.mouse_entered.connect(_on_tag_hovered.bind(str(task["id"])))
			button.mouse_exited.connect(_on_tag_unhovered.bind(str(task["id"])))
			container.add_child(button)
	_preview.hide()
	_empty_state.visible = _source_id == "private" and not _source_has_tasks()
	_detail_panel.hide()
	_detail_shade.hide()
	_selected_duty_id = ""
	_refresh_ropes()

func _clear_rows() -> void:
	for container in _tag_containers:
		for child in container.get_children():
			container.remove_child(child)
			child.queue_free()

func _refresh_ropes() -> void:
	for container in _tag_containers:
		var rope := container.get_parent()
		if is_instance_valid(rope):
			rope.call("_layout_tags")
			rope.queue_redraw()

func _set_page(index: int) -> void:
	var pages := _active_pages()
	var next_index := clampi(index, 0, pages.size() - 1)
	if next_index == _page_index:
		return
	_page_index = next_index
	_clear_hover()
	_update_page()

func _on_tag_hovered(task_id: String) -> void:
	_hovered_duty_id = task_id
	_preview.hide()
	_hover_timer.start(1.5)

func _on_tag_unhovered(task_id: String) -> void:
	if _hovered_duty_id != task_id:
		return
	_clear_hover()

func _on_hover_timer_timeout() -> void:
	if not _duty_by_id.has(_hovered_duty_id) or not is_visible_in_tree():
		return
	var task: Dictionary = _duty_by_id[_hovered_duty_id]
	_preview_title.text = str(task["title"])
	_preview_body.text = str(task["preview"])
	_preview.show()
	_motto.hide()

func _on_duty_pressed(task_id: String) -> void:
	_clear_hover()
	if not _duty_by_id.has(task_id) or str(_duty_by_id[task_id].get("source", "")) != _source_id:
		return
	_selected_duty_id = task_id
	_detail_shade.show()
	_detail_panel.show()
	_refresh_detail()

func _refresh_detail() -> void:
	if _selected_duty_id.is_empty() or not _duty_by_id.has(_selected_duty_id):
		return
	var task: Dictionary = _duty_by_id[_selected_duty_id]
	_detail_title.text = str(task["title"])
	_detail_realm.text = "境界判断 · " + str(task["realm"])
	_detail_body.text = str(task["detail"])
	if _active_task_id == _selected_duty_id:
		if _selected_duty_id == "beast_bone_submission":
			_detail_body.text += "\n\n当前行囊：兽骨 %s；交付将扣除一份，贡献仅结算一次。" % ("已备齐" if _has_bone else "尚缺一份")
			_accept_button.text = "提交兽骨"
			_accept_button.disabled = not _has_bone
		elif _selected_duty_id == "bitter_moss_root_submission":
			_detail_body.text += "\n\n当前行囊：苦苔根 %d/2；交付扣除两份，贡献仅结算一次。" % _root_count
			_accept_button.text = "提交苦苔根"
			_accept_button.disabled = _root_count < 2
		elif _selected_duty_id == "east_woods_patrol":
			_detail_body.text += "\n\n" + ("巡查已复核，可在此回报领取贡献。" if _active_duty_ready else "巡查进度：%d/4；仍需抵达静冠洼地复核，然后返回此处。" % mini(_active_duty_stage, 4))
			_accept_button.text = "回报巡查" if _active_duty_ready else "巡查进行中"
			_accept_button.disabled = not _active_duty_ready
		elif _selected_duty_id == "tianwen_practice":
			_detail_body.text += "\n\n已接取。前往天问峰演法台完成一场林地凶狼演练并回传结果；不要求获胜，结果返回后核发 5 点贡献，不掉落物品。"
			_accept_button.text = "已接取"
			_accept_button.disabled = true
		elif not str(task.get("incident_id", "")).is_empty():
			_detail_body.text += "\n\n已接取案卷 %s。前往执中峰查看来源事实；核验并上呈后事务结束，状态为已上呈、待裁断。" % _active_incident_id
			_accept_button.text = "前往执中峰核验"
			_accept_button.disabled = true
		else:
			_accept_button.text = "已接取"
			_accept_button.disabled = true
		_cancel_button.text = "取消接取"
		_cancel_button.disabled = false
	elif not _active_duty_id.is_empty():
		_accept_button.text = "已有事务"
		_accept_button.disabled = true
		_cancel_button.text = "取消"
		_cancel_button.disabled = false
	else:
		_accept_button.text = "接取"
		_accept_button.disabled = false
		_cancel_button.text = "取消"
		_cancel_button.disabled = false

func _on_accept_pressed() -> void:
	if _selected_duty_id.is_empty():
		return
	if _active_duty_id == _selected_duty_id and (_selected_duty_id in ["beast_bone_submission", "bitter_moss_root_submission"] or (_selected_duty_id == "east_woods_patrol" and _active_duty_ready)):
		duty_action_requested.emit("heguang_submit_duty:" + _selected_duty_id)
		return
	if not _active_duty_id.is_empty():
		return
	if _selected_duty_id.begins_with(INCIDENT_DUTY_ID + ":"):
		_active_duty_id = INCIDENT_DUTY_ID
		_active_incident_id = _selected_duty_id.substr((INCIDENT_DUTY_ID + ":").length())
		_active_task_id = _selected_duty_id
	else:
		_active_duty_id = _selected_duty_id
		_active_incident_id = ""
		_active_task_id = _selected_duty_id
	_refresh_detail()
	duty_action_requested.emit("heguang_accept_duty:" + _selected_duty_id)

func _on_cancel_pressed() -> void:
	if not _selected_duty_id.is_empty() and _active_task_id == _selected_duty_id:
		var canceled_id := _selected_duty_id
		_active_duty_id = ""
		_active_task_id = ""
		_active_incident_id = ""
		close_detail()
		duty_action_requested.emit("heguang_cancel_duty:" + canceled_id)
		return
	close_detail()

func _clear_hover() -> void:
	if _hover_timer != null:
		_hover_timer.stop()
	if _preview != null:
		_preview.hide()
	if _motto != null:
		_motto.show()
	_hovered_duty_id = ""

func _make_small_button(label_text: String) -> Button:
	var button := Button.new()
	button.text = label_text
	button.focus_mode = Control.FOCUS_NONE
	_apply_font(button, 17)
	button.add_theme_color_override("font_color", Color(0.94, 0.89, 0.77, 1.0))
	var normal := StyleBoxFlat.new()
	normal.bg_color = Color(0.14, 0.13, 0.10, 0.92)
	normal.border_width_left = 1
	normal.border_width_top = 1
	normal.border_width_right = 1
	normal.border_width_bottom = 1
	normal.border_color = Color(0.69, 0.58, 0.39, 0.78)
	normal.corner_radius_top_left = 5
	normal.corner_radius_top_right = 5
	normal.corner_radius_bottom_right = 5
	normal.corner_radius_bottom_left = 5
	button.add_theme_stylebox_override("normal", normal)
	var hover := normal.duplicate() as StyleBoxFlat
	hover.bg_color = Color(0.22, 0.19, 0.14, 0.96)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", normal)
	var disabled := normal.duplicate() as StyleBoxFlat
	disabled.bg_color = Color(0.30, 0.28, 0.23, 0.82)
	disabled.border_color = Color(0.46, 0.42, 0.34, 0.55)
	button.add_theme_stylebox_override("disabled", disabled)
	return button

func _make_paper_style(alpha: float) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.88, 0.84, 0.74, alpha)
	style.border_width_left = 2
	style.border_width_top = 2
	style.border_width_right = 2
	style.border_width_bottom = 2
	style.border_color = Color(0.36, 0.31, 0.22, 0.86)
	style.corner_radius_top_left = 10
	style.corner_radius_top_right = 10
	style.corner_radius_bottom_right = 10
	style.corner_radius_bottom_left = 10
	style.shadow_color = Color(0.08, 0.06, 0.03, 0.20)
	style.shadow_size = 8
	return style

func _apply_font(control: Control, size: int) -> void:
	control.add_theme_font_override("font", UI_FONT)
	control.add_theme_font_size_override("font_size", size)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE and _detail_panel.visible:
		close_detail()
		get_viewport().set_input_as_handled()
