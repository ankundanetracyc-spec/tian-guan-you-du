extends "action_battle_feel_test.gd"

## Use the real battle drawing/pose/skill paths, omitting HUD icon allocation for
## repeatable offscreen art captures while the user's full editors remain open.
func _ready() -> void:
	_load_strike_fx()
	skill_vfx = load(_action_test_path("quiet_skill_vfx.gd")).new()
	skill_vfx.setup(self, _action_test_path("quiet_force.gdshader"))
	add_child(skill_vfx)
	var ground_image := Image.new()
	if ground_image.load(ProjectSettings.globalize_path(_action_test_path("art/battle_ground_stone_v1.png"))) == OK:
		battle_ground = ImageTexture.create_from_image(ground_image)
	var wolf_image := Image.new()
	var wolf_path := _resolve_project_art_path("textures/bestiary/static/forest_wolf.png")
	if not wolf_path.is_empty() and wolf_image.load(wolf_path) == OK:
		wolf_idle = ImageTexture.create_from_image(wolf_image)
	_setup_skinned_player()
	trial_camera = Camera2D.new()
	trial_camera_framer = load(_action_test_path("adaptive_combat_camera.gd")).new()
	add_child(trial_camera)
	trial_camera.make_current()
