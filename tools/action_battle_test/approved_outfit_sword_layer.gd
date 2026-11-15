extends RefCounted

## Independent fitted sword and preview pose. No damage, skill or input rules.
const DEFAULT_DIR := "res://art/approved_outfit_sword_20261105"
const CLEAN_ALPHA_SHADER := """shader_type canvas_item;
render_mode unshaded;
uniform bool depth_split = false;
uniform bool near_pass = true;
uniform vec2 source_dimensions = vec2(1024.0,1536.0);
uniform vec2 source_grip_px = vec2(512.0,1303.0);
uniform vec2 depth_gradient = vec2(0.0);
uniform float grip_depth = 0.0;
uniform float body_depth = 0.04;
void fragment() {
	vec4 pixel = texture(TEXTURE,UV);
	pixel.a = smoothstep(0.65,0.90,pixel.a);
	if (depth_split) {
		float depth = grip_depth+dot(UV*source_dimensions-source_grip_px,depth_gradient);
		if ((near_pass && depth>body_depth) || (!near_pass && depth<=body_depth)) discard;
	}
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
var pose_revision := 0
var swing_form := 0
var next_swing_form := 0
var native_pose_helper: RefCounted
var far_sword_sprite: Sprite2D
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
	if directory==DEFAULT_DIR:
		directory = get_script().resource_path.get_base_dir().path_join("art/approved_outfit_sword_20261105")
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
	if skin.has_method("_rotate_source_branch") and hands.has_method("get_weapon_projection"):
		native_pose_helper = load(get_script().resource_path.get_base_dir().path_join("guigu_native_sword_pose_3d.gd")).new()
		native_pose_helper.build(skin)
		sword_sprite.z_as_relative = true
		far_sword_sprite = sword_sprite.duplicate()
		far_sword_sprite.name = "ApprovedOutfit_FittedSword_FarDepth"
		far_sword_sprite.material = material.duplicate()
		skin.runtime.rig_root_node.add_child(far_sword_sprite)
		far_sword_sprite.visible = false
	source_sha256 = FileAccess.get_sha256(path)
	loaded = true
	return true

func set_mode(value: String) -> void:
	var next := value if value in ["off","hold","swing"] else "off"
	if next!=mode: pose_revision += 1
	var hands: Variant = hands_ref.get_ref() if hands_ref!=null else null
	if next!="off" and mode=="off" and hands!=null:
		previous_hand_override = str(hands.pose_override)
	if next=="swing" and mode!="swing": swing_elapsed = 0.0
	mode = next
	if hands!=null:
		hands.set_pose_override(previous_hand_override if mode=="off" else "grip_near")
	if sword_sprite!=null: sword_sprite.visible = loaded and visible and mode!="off"
	if far_sword_sprite!=null: far_sword_sprite.visible = loaded and visible and mode!="off"
	if mode=="off" and native_pose_helper!=null: native_pose_helper.restore_depth()

func restart_swing(loop: bool = true, form_index: int = -1) -> void:
	swing_loop = loop
	swing_elapsed = 0.0
	swing_form = posmod(form_index,4) if form_index>=0 else next_swing_form
	next_swing_form = posmod(swing_form+1,4)
	pose_revision += 1
	set_mode("swing")

func _pose_track(phase: float, held: float, lifted: float, cut: float, lift_start: float, lift_end: float, cut_start: float, cut_end: float, return_start: float) -> float:
	# The three joints start and arrive separately: the shoulder carries the
	# lift, the elbow sends it, and the wrist finishes directing the blade.
	var value := lerpf(held,lifted,smoothstep(lift_start,lift_end,phase))
	value = lerpf(value,cut,smoothstep(cut_start,cut_end,phase))
	return lerpf(value,held,smoothstep(return_start,1.0,phase))

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
	var form := posmod(swing_form+int(floor(swing_elapsed/swing_duration)),4) if swing_loop and mode=="swing" else swing_form
	if native_pose_helper!=null:
		last = native_pose_helper.apply(skin,form,phase,mode=="swing")
		last.mode = mode
		last.clip = clip
		last.phase = phase
		last.swing_section = "hold" if mode!="swing" else ("prepare" if phase<0.29 else ("cut" if phase<0.61 else "recover"))
		last.elbow_hyperextension = false
		hands.pose_override = "grip_near"
		return
	var upper_degrees := 55.0
	var lower_degrees := -15.0
	var wrist_degrees := -6.0
	var swing_section := "hold"
	if mode=="swing":
		swing_section = "prepare" if phase<0.30 else ("cut" if phase<0.56 else "recover")
		upper_degrees = _pose_track(phase,55.0,17.0,66.0,0.0,0.26,0.30,0.49,0.58)
		lower_degrees = _pose_track(phase,-15.0,-65.0,24.0,0.025,0.30,0.325,0.52,0.61)
		wrist_degrees = _pose_track(phase,-6.0,-16.0,16.0,0.07,0.30,0.36,0.56,0.65)
	var upper_angle := deg_to_rad(upper_degrees)
	var lower_angle := deg_to_rad(lower_degrees)
	var wrist_angle := deg_to_rad(wrist_degrees)
	# _aim_arm changes rotations only. Existing rest lengths and wrist mount
	# remain the same while standing, running and swinging.
	skin._aim_arm("near",upper_angle,lower_angle,wrist_angle)
	hands.pose_override = "grip_near"
	var shoulder: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_upper.global_position)
	var elbow: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_forearm.global_position)
	var wrist: Vector2 = skin.runtime.rig_root_node.to_local(skin.bones.near_hand.global_position)
	var flex_degrees := rad_to_deg(upper_angle-lower_angle)
	last = {"mode":mode,"clip":clip,"phase":phase,"swing_section":swing_section,"pose_angles_degrees":Vector3(rad_to_deg(upper_angle),rad_to_deg(lower_angle),rad_to_deg(wrist_angle)),"shoulder":shoulder,"elbow":elbow,"wrist":wrist,"upper_length":shoulder.distance_to(elbow),"lower_length":elbow.distance_to(wrist),"rotations_only":true,"elbow_flex_degrees":flex_degrees,"elbow_hyperextension":flex_degrees<0.0}

func update_attachment() -> void:
	if not loaded or sword_sprite==null: return
	sword_sprite.visible = visible and mode!="off"
	if mode=="off": return
	var skin: Variant = owner_ref.get_ref()
	var hands: Variant = hands_ref.get_ref()
	if skin==null or hands==null: return
	if native_pose_helper!=null:
		_update_native_attachment(skin,hands)
		return
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
	var tip_world := sword_sprite.to_global(source_tip)
	last.attachment_error = grip_world.distance_to(attachment.origin)
	last.grip_global = grip_world
	last.tip_global = tip_world
	last.grip_axis_world = (tip_world-grip_world).normalized()
	last.grip_to_tip_native = grip_to_tip
	last.grip_to_tip_world = grip_world.distance_to(tip_world)
	last.total_length_native = source_pommel.distance_to(source_tip)*image_scale
	last.total_length_world = sword_sprite.to_global(source_pommel).distance_to(tip_world)
	last.z_index = sword_sprite.z_index

func _update_native_attachment(skin: RefCounted, hands: RefCounted) -> void:
	far_sword_sprite.visible = visible and mode!="off"
	var frame: Dictionary = hands.get_weapon_projection()
	var projected: Transform2D = frame.projection
	var root: Transform2D = skin.runtime.rig_root_node.global_transform
	var x := root.basis_xform(projected.x*image_scale)
	var y := root.basis_xform(projected.y*image_scale)
	var grip_world := root*projected.origin
	var transform := Transform2D(x,y,grip_world-x*source_grip.x-y*source_grip.y)
	sword_sprite.global_transform = transform
	far_sword_sprite.global_transform = transform
	sword_sprite.z_index = int(last.near_arm_base_z_index)-1
	far_sword_sprite.z_index = int(last.body_z_index)-2
	var gradients: Vector2 = Vector2((frame.width_axis_3d as Vector3).z,(frame.length_axis_3d as Vector3).z)*image_scale/skin.PIXELS_PER_UNIT
	for item in [sword_sprite,far_sword_sprite]:
		var material: ShaderMaterial = item.material
		material.set_shader_parameter("depth_split",true)
		material.set_shader_parameter("near_pass",item==sword_sprite)
		material.set_shader_parameter("source_dimensions",image_size)
		material.set_shader_parameter("source_grip_px",source_grip)
		material.set_shader_parameter("depth_gradient",gradients)
		material.set_shader_parameter("grip_depth",(frame.grip_3d as Vector3).z)
		material.set_shader_parameter("body_depth",float(last.body_depth))
	var tip_3d: Vector3 = frame.grip_3d+(frame.blade_axis_3d as Vector3)*(grip_to_tip/skin.PIXELS_PER_UNIT)
	last.attachment_error = sword_sprite.to_global(source_grip).distance_to(grip_world)
	last.grip_global = sword_sprite.to_global(source_grip)
	last.tip_global = sword_sprite.to_global(source_tip)
	last.grip_3d = frame.grip_3d
	last.tip_3d = tip_3d
	last.blade_axis_3d = frame.blade_axis_3d
	last.grip_axis_world = (last.tip_global-last.grip_global).normalized()
	last.grip_to_tip_native = (frame.grip_3d as Vector3).distance_to(tip_3d)*skin.PIXELS_PER_UNIT
	last.grip_to_tip_world = (last.tip_global as Vector2).distance_to(last.grip_global)
	last.projected_grip_to_tip_native = last.grip_to_tip_world/root.x.length()
	last.total_length_native = source_pommel.distance_to(source_tip)*image_scale
	last.total_length_world = sword_sprite.to_global(source_pommel).distance_to(last.tip_global)
	last.z_index = sword_sprite.z_index
	last.far_z_index = far_sword_sprite.z_index
	last.depth_split = true

func set_visible(value: bool) -> void:
	visible = value
	if sword_sprite!=null: sword_sprite.visible = value and mode!="off"
	if far_sword_sprite!=null: far_sword_sprite.visible = value and mode!="off"

func get_diagnostics() -> Dictionary:
	return {"loaded":loaded,"errors":errors,"mode":mode,"visible":visible,"swing_loop":swing_loop,"swing_duration":swing_duration,"swing_form":swing_form,"next_swing_form":next_swing_form,"source":asset_dir.path_join("sword.png"),"source_sha256":source_sha256,"native_size":image_size,"registration":registration,"source_image_scale":image_scale,"uniform_image_scale":image_scale,"projection":"true3D orthographic; source length55, screen length foreshortens" if native_pose_helper!=null else "legacy uniform2D","last":last,"grip_hand":"original anatomical right palm; actual wrist basis" if native_pose_helper!=null else "near right back-facing grip only","render_order":"per-pixel native depth split; same-depth original fingers above hilt; actor-relative layers" if native_pose_helper!=null else "sword behind near fingers by one Z layer","alpha_cleanup":"shader0.65..0.90 only; PNG unchanged","scope":"independent pose/weapon preview; no skill, hitbox or damage changes"}
