extends RefCounted

## Independent fitted sword and preview pose. No damage, skill or input rules.
const DEFAULT_DIR := "res://art/approved_outfit_sword_20261105"
const CLEAN_ALPHA_SHADER := """shader_type canvas_item;
render_mode unshaded;
void fragment() {
	vec4 pixel = texture(TEXTURE,UV);
	pixel.a = smoothstep(0.65,0.90,pixel.a);
	COLOR = pixel;
}
"""

var owner_ref: WeakRef
var hands_ref: WeakRef
var errors: Array[String] = []
var loaded := false
var visible := true
var mode := "off"
var swing_loop := true
var swing_duration := 0.72
var swing_elapsed := 0.0
var registration: Dictionary = {}
var sword_sprite: Sprite2D
var image_size := Vector2.ZERO
var source_grip := Vector2.ZERO
var source_tip := Vector2.ZERO
var source_pommel := Vector2.ZERO
var image_scale := 1.0
var grip_to_tip := 55.0
var previous_hand_override := "auto"
var last: Dictionary = {}
var source_sha256 := ""
var asset_dir := DEFAULT_DIR

func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return fallback

func build(skin: RefCounted, hands: RefCounted, directory: String = DEFAULT_DIR) -> bool:
	if loaded:
		errors.append("Sword layer already built")
		return false
	owner_ref = weakref(skin)
	hands_ref = weakref(hands)
	asset_dir = directory
	var path := directory.path_join("sword.png")
	var registration_path := directory.path_join("sword_registration.json")
	if not FileAccess.file_exists(path) or not FileAccess.file_exists(registration_path):
		errors.append("Sword asset/registration not ready: "+directory)
		return false
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(registration_path))
	if not parsed is Dictionary:
		errors.append("Invalid sword registration")
		return false
	registration = parsed.get("sword",parsed)
	var image := Image.new()
	if image.load(ProjectSettings.globalize_path(path))!=OK:
		errors.append("Cannot load sword image")
		return false
	image_size = image.get_size()
	source_grip = _vec(registration.get("grip_px"),Vector2(512,1303))
	source_tip = _vec(registration.get("tip_px"),Vector2(512,50))
	source_pommel = _vec(registration.get("pommel_px"),Vector2(512,1496))
	grip_to_tip = float(registration.get("target_grip_to_tip",55.0))
	if source_grip.distance_to(source_tip)<1.0:
		errors.append("Sword grip and tip must differ")
		return false
	image_scale = grip_to_tip/source_grip.distance_to(source_tip)
	image.generate_mipmaps()
	sword_sprite = Sprite2D.new()
	sword_sprite.name = "ApprovedOutfit_FittedSword"
	sword_sprite.texture = ImageTexture.create_from_image(image)
	sword_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	sword_sprite.centered = false
	sword_sprite.z_as_relative = false
	var material := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = CLEAN_ALPHA_SHADER
	material.shader = shader
	sword_sprite.material = material
	skin.runtime.rig_root_node.add_child(sword_sprite)
	sword_sprite.visible = false
	source_sha256 = FileAccess.get_sha256(path)
	loaded = true
	return true

func set_mode(value: String) -> void:
	var next := value if value in ["off","hold","swing"] else "off"
	var hands: Variant = hands_ref.get_ref() if hands_ref!=null else null
	if next!="off" and mode=="off" and hands!=null:
		previous_hand_override = str(hands.pose_override)
	if next=="swing" and mode!="swing": swing_elapsed = 0.0
	mode = next
	if hands!=null:
		hands.set_pose_override(previous_hand_override if mode=="off" else "grip_near")
	if sword_sprite!=null: sword_sprite.visible = loaded and visible and mode!="off"

func restart_swing(loop: bool = true) -> void:
	swing_loop = loop
	swing_elapsed = 0.0
	set_mode("swing")

func update_pose(delta: float = 0.0, clip: String = "Idle", _clock: float = 0.0) -> void:
	if not loaded or mode=="off": return
	var skin: Variant = owner_ref.get_ref()
	var hands: Variant = hands_ref.get_ref()
	if skin==null or hands==null: return
	if delta>0.0 and mode=="swing":
		swing_elapsed += delta
		if not swing_loop and swing_elapsed>=swing_duration:
			mode = "hold"
	var phase := fposmod(swing_elapsed/swing_duration,1.0) if mode=="swing" else 0.0
	var upper_angle := deg_to_rad(72.0)
	var lower_angle := deg_to_rad(25.0)
	var wrist_angle := 0.0
	if mode=="swing":
		# One readable preparation, descending cut and recovery, all rotations.
		if phase<0.22:
			var t := smoothstep(0.0,0.22,phase)
			upper_angle = lerpf(deg_to_rad(72),deg_to_rad(-12),t)
			lower_angle = lerpf(deg_to_rad(25),deg_to_rad(-34),t)
			wrist_angle = lerpf(0.0,deg_to_rad(-10),t)
		elif phase<0.62:
			var t := smoothstep(0.22,0.62,phase)
			upper_angle = lerpf(deg_to_rad(-12),deg_to_rad(95),t)
			lower_angle = lerpf(deg_to_rad(-34),deg_to_rad(65),t)
			wrist_angle = lerpf(deg_to_rad(-10),deg_to_rad(25),t)
		else:
			var t := smoothstep(0.62,1.0,phase)
			upper_angle = lerpf(deg_to_rad(95),deg_to_rad(72),t)
			lower_angle = lerpf(deg_to_rad(65),deg_to_rad(25),t)
			wrist_angle = lerpf(deg_to_rad(25),0.0,t)
	# _aim_arm changes rotations only. Existing rest lengths and wrist mount
	# remain the same while standing, running and swinging.
	skin._aim_arm("near",upper_angle,lower_angle,wrist_angle)
	hands.pose_override = "grip_near"
	var shoulder: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_upper.global_position)
	var elbow: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_forearm.global_position)
	var wrist: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_hand.global_position)
	last = {"mode":mode,"clip":clip,"phase":phase,"shoulder":shoulder,"elbow":elbow,"wrist":wrist,"upper_length":shoulder.distance_to(elbow),"lower_length":elbow.distance_to(wrist),"rotations_only":true,"elbow_flex_degrees":rad_to_deg(upper_angle-lower_angle),"elbow_hyperextension":false}

func update_attachment() -> void:
	if not loaded or sword_sprite==null: return
	sword_sprite.visible = visible and mode!="off"
	if mode=="off": return
	var skin: Variant = owner_ref.get_ref()
	var hands: Variant = hands_ref.get_ref()
	if skin==null or hands==null: return
	var attachment: Transform2D = hands.get_attachment_transform("near")
	var root_transform: Transform2D = skin.runtime.rig_root_node.global_transform
	var root_scale := root_transform.x.length()
	var rotate := deg_to_rad(float(registration.get("grip_axis_offset_degrees",-90.0)))
	var basis := attachment*Transform2D(rotate,Vector2.ZERO)
	var scale := image_scale*root_scale
	var origin := attachment.origin-(basis.x*source_grip.x+basis.y*source_grip.y)*scale
	sword_sprite.global_transform = Transform2D(basis.x*scale,basis.y*scale,origin)
	var near_hand: Sprite2D = hands.get_hand_node("near")
	sword_sprite.z_index = near_hand.z_index-1 if near_hand!=null else 68
	var grip_world := sword_sprite.to_global(source_grip)
	last.attachment_error = grip_world.distance_to(attachment.origin)
	last.grip_global = grip_world
	last.tip_global = sword_sprite.to_global(source_tip)
	last.grip_to_tip_world = grip_to_tip
	last.total_length_world = source_pommel.distance_to(source_tip)*image_scale
	last.z_index = sword_sprite.z_index

func set_visible(value: bool) -> void:
	visible = value
	if sword_sprite!=null: sword_sprite.visible = value and mode!="off"

func get_diagnostics() -> Dictionary:
	return {"loaded":loaded,"errors":errors,"mode":mode,"visible":visible,"swing_loop":swing_loop,"swing_duration":swing_duration,"source":asset_dir.path_join("sword.png"),"source_sha256":source_sha256,"native_size":image_size,"registration":registration,"uniform_image_scale":image_scale,"last":last,"grip_hand":"near right back-facing grip only","render_order":"sword behind near fingers by one Z layer","alpha_cleanup":"shader0.65..0.90 only; PNG unchanged","scope":"independent pose/weapon preview; no skill, hitbox or damage changes"}
