extends CanvasLayer

const Avatar = preload("res://tools/queue_small_avatar/yuxu_modular_battle_poses.gd")
const POSES := {
	"待机": "idle", "移动": "move", "闪避": "dodge", "架招": "guard",
	"受击": "hurt", "失神": "stagger", "出掌": "attack", "尽锋": "sword_finish",
}
const HAIRSTYLES := {"发带束髻": "ribbon", "木簪束发": "pin"}

func _ready() -> void:
	var backdrop := ColorRect.new()
	add_child(backdrop)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	backdrop.color = Color("#b4b4a6")
	var center := CenterContainer.new()
	add_child(center)
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var layout := VBoxContainer.new()
	center.add_child(layout)
	var toolbar := HBoxContainer.new()
	layout.add_child(toolbar)
	var viewport := Control.new()
	viewport.custom_minimum_size = Vector2(560, 430)
	layout.add_child(viewport)
	var avatar := Avatar.new()
	viewport.add_child(avatar)
	avatar.position = Vector2(245, 390)
	if not avatar.configure(340.0):
		var error := Label.new()
		error.text = "战斗素材加载失败"
		toolbar.add_child(error)
		return
	var hair := _selector(toolbar, "发型", HAIRSTYLES.keys())
	var bodies := _selector(toolbar, "身体姿势", POSES.keys())
	hair.item_selected.connect(func(index: int): avatar.set_hair_style(HAIRSTYLES.values()[index]))
	bodies.item_selected.connect(func(index: int): avatar.show_pose_for_review(POSES.values()[index]))
	var facing := CheckButton.new()
	facing.text = "朝左"
	toolbar.add_child(facing)
	facing.toggled.connect(avatar.set_facing)

func _selector(toolbar: HBoxContainer, caption: String, choices: Array) -> OptionButton:
	var title := Label.new()
	title.text = caption
	title.modulate = Color("#292620")
	toolbar.add_child(title)
	var selector := OptionButton.new()
	toolbar.add_child(selector)
	for choice in choices:
		selector.add_item(choice)
	return selector
