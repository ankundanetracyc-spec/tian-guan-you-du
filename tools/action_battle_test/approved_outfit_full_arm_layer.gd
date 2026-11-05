extends RefCounted

## Five complete arm drawings registered to the approved fixed-length rig.
## The old arm chart is only geometry/UV reference; no separate hand is drawn.
const DEFAULT_DIR := "res://art/approved_outfit_full_arms_20261105"
const KEYS := ["near_open","far_open","near_fist","far_fist","near_grip"]
const OLD_SHOULDER := Vector2(425.0,435.0)
const OLD_ELBOW := Vector2(466.0,562.0)
const OLD_WRIST := Vector2(609.0,473.0)
const OLD_LOWER := OLD_WRIST-OLD_ELBOW
const SHADER_PATH := "res://approved_outfit_full_arm.gdshader"

var owner_ref: WeakRef
var assets: Dictionary = {}
var registrations: Dictionary = {}
var hands: Dictionary = {}
var errors: Array[String] = []
var visible := true
var pose_override := "auto"
var art_dir := DEFAULT_DIR
var sword_source_sha256 := ""
var target_palm_width := 0.0

func _vec(value: Variant, fallback: Vector2 = Vector2.ZERO) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return fallback

func build(owner_skin: RefCounted, directory: String = DEFAULT_DIR) -> bool:
	if not hands.is_empty():
		errors.append("Full arm layer already built")
		return false
	var resource_dir: String = get_script().resource_path.get_base_dir()
	if directory==DEFAULT_DIR: directory = resource_dir.path_join("art/approved_outfit_full_arms_20261105")
	owner_ref = weakref(owner_skin)
	art_dir = directory
	var config_path := directory.path_join("arm_registration.json")
	if not FileAccess.file_exists(config_path):
		errors.append("Missing measured full arm registration "+config_path)
		return false
	var config: Variant = JSON.parse_string(FileAccess.get_file_as_string(config_path))
	if not config is Dictionary:
		errors.append("Invalid full arm registration")
		return false
	target_palm_width = float(config.get("target_palm_width",0.0))
	if target_palm_width<=0.0:
		errors.append("Full arm physical palm width must be measured and positive")
		return false
	registrations = config.get("arms",config.get("hands",{}))
	for key in KEYS:
		var registration: Dictionary = registrations.get(key,{})
		var valid := true
		for field in ["shoulder_px","elbow_px","wrist_px","grip_px"]:
			var value: Variant = registration.get(field)
			if not value is Array or value.size()!=2:
				errors.append("Missing measured "+str(key)+" "+str(field))
				valid = false
		if float(registration.get("palm_width_px",0.0))<=0.0:
			errors.append("Missing measured "+str(key)+" palm width")
			valid = false
		if not valid: continue
		if _vec(registration.shoulder_px).distance_to(_vec(registration.elbow_px))<1.0 or _vec(registration.elbow_px).distance_to(_vec(registration.wrist_px))<1.0:
			errors.append("Degenerate measured full arm "+str(key))
			continue
		var path := directory.path_join(str(key)+".png")
		var image := Image.new()
		if image.load(ProjectSettings.globalize_path(path))!=OK:
			errors.append("Missing regenerated full arm "+path)
			continue
		var size := image.get_size()
		var digest := FileAccess.get_sha256(path)
		if registration.has("source_sha256") and str(registration.source_sha256)!=digest:
			errors.append("Registration does not match full arm source "+str(key))
			continue
		image.generate_mipmaps()
		assets[key] = {"texture":ImageTexture.create_from_image(image),"path":path,"size":[size.x,size.y],"sha256":digest}
	if not errors.is_empty(): return false
	var skin: Variant = owner_ref.get_ref()
	sword_source_sha256 = FileAccess.get_sha256(resource_dir.path_join("art/yuxu_fullbody_v15/arm_sword_fingers_v21.png"))
	for side in ["near","far"]:
		if not skin.bones.has(side+"_hand") or not skin.arm_materials.has(side):
			errors.append("Missing approved wrist/material "+side)
			continue
		var mesh: Polygon2D
		for record in skin.mesh_records:
			if str(record.name)==side+"_arm": mesh = record.mesh
		if mesh==null:
			errors.append("Missing approved full arm mesh "+side)
			continue
		var material: ShaderMaterial = skin.arm_materials[side]
		material.shader = load(resource_dir.path_join(SHADER_PATH.get_file()))
		material.set_shader_parameter("sword_hand",skin.textures.arm_sword_fingers_v21)
		mesh.material = material
		var cap := mesh.get_node_or_null("ShoulderUnderClothing") as Polygon2D
		if cap!=null: cap.material = material
		# Textureless Sprite2D keeps the existing weapon layer's socket/depth API.
		# The whole hand itself is drawn by the same skinned arm mesh.
		var socket := Sprite2D.new()
		socket.name = "FullArmHandSocket_"+side
		socket.z_as_relative = false
		socket.z_index = 70 if side=="near" else 40
		socket.visible = false
		socket.rotation = OLD_LOWER.angle()+PI*0.5
		skin.bones[side+"_hand"].add_child(socket)
		hands[side] = {"node":socket,"material":material,"mesh":mesh,"cap":cap,"style":"","key":"","last":{}}
	if not errors.is_empty(): return false
	update_pose()
	return true

func set_pose_override(value: String) -> void:
	pose_override = value if value in ["auto","open","fist","sword_near","grip_near"] else "auto"
	update_pose()

func _style_for(side: String, skin: Variant) -> String:
	if pose_override in ["open","fist"]: return pose_override
	if pose_override=="sword_near": return "sword" if side=="near" else "open"
	if pose_override=="grip_near": return "grip" if side=="near" else "open"
	return "fist" if str(skin.last_clip) in ["Run_New1","Run_New2","BackstepWalk"] or float(skin.gait_amount)>0.15 else "open"

func update_pose() -> void:
	var skin: Variant = owner_ref.get_ref() if owner_ref!=null else null
	if skin==null: return
	for side in hands:
		var state: Dictionary = hands[side]
		var style := _style_for(side,skin)
		var sword := style=="sword"
		state.material.set_shader_parameter("keep_original_sword",sword)
		state.material.set_shader_parameter("open_amount",0.0)
		state.material.set_shader_parameter("sword_amount",1.0 if sword else 0.0)
		var upper: Bone2D = skin.bones[side+"_upper"]
		var elbow: Bone2D = skin.bones[side+"_forearm"]
		var wrist_bone: Bone2D = skin.bones[side+"_hand"]
		var flex := absf((elbow.global_position-upper.global_position).angle_to(wrist_bone.global_position-elbow.global_position))
		# Only deep folds need a same-source contour margin. The rest silhouette
		# and normal elbow opening retain the generated complete drawing.
		state.material.set_shader_parameter("inner_elbow_coverage",smoothstep(deg_to_rad(112.0),deg_to_rad(148.0),flex))
		state.mesh.visible = visible
		if state.cap!=null: state.cap.visible = visible
		state.style = style
		if sword:
			state.key = "original_v21_sword"
			state.last = {"legacy_source":true,"wrist_px":[OLD_WRIST.x,OLD_WRIST.y]}
			state.node.position = Vector2.ZERO
			continue
		var key: String = side+"_"+style
		if not assets.has(key): continue
		var registration: Dictionary = registrations[key]
		if state.key!=key:
			state.material.set_shader_parameter("full_arm",assets[key].texture)
			state.material.set_shader_parameter("full_arm_size",Vector2(assets[key].size[0],assets[key].size[1]))
			state.material.set_shader_parameter("source_shoulder",_vec(registration.shoulder_px))
			state.material.set_shader_parameter("source_elbow",_vec(registration.elbow_px))
			state.material.set_shader_parameter("source_wrist",_vec(registration.wrist_px))
			state.material.set_shader_parameter("source_palm_width",float(registration.palm_width_px))
			state.material.set_shader_parameter("target_palm_width_px",target_palm_width/float(skin.PIXEL_SCALE))
		state.key = key
		state.last = registration.duplicate(true)
		state.last.target_palm_width = target_palm_width
		state.last.hand_source_scale = float(registration.palm_width_px)*float(skin.PIXEL_SCALE)/target_palm_width
		state.last.wrist_mount_source_px = [OLD_WRIST.x,OLD_WRIST.y]
		state.node.position = _hand_point_local(key,_vec(registration.grip_px))

func _hand_point_local(key: String, source_point: Vector2) -> Vector2:
	var skin: Variant = owner_ref.get_ref()
	if skin==null or not registrations.has(key): return Vector2.ZERO
	var registration: Dictionary = registrations[key]
	var wrist := _vec(registration.wrist_px)
	var direction := wrist-_vec(registration.elbow_px)
	var scale := target_palm_width/float(registration.palm_width_px)
	return (source_point-wrist).rotated(OLD_LOWER.angle()-direction.angle())*scale

func get_hand_point_global(side: String, source_point: Vector2) -> Vector2:
	if not hands.has(side): return Vector2.ZERO
	var state: Dictionary = hands[side]
	var skin: Variant = owner_ref.get_ref()
	return skin.bones[side+"_hand"].to_global(_hand_point_local(str(state.key),source_point))

func set_visible(value: bool) -> void:
	visible = value
	for side in hands:
		hands[side].mesh.visible = value
		if hands[side].cap!=null: hands[side].cap.visible = value

func get_hand_node(side: String = "near") -> Sprite2D:
	return hands[side].node if hands.has(side) else null

func get_attachment_transform(side: String = "near") -> Transform2D:
	if not hands.has(side): return Transform2D.IDENTITY
	var transform: Transform2D = hands[side].node.global_transform
	return Transform2D(transform.x.normalized(),transform.y.normalized(),transform.origin)

func get_wrist_transform(side: String = "near") -> Transform2D:
	if not hands.has(side): return Transform2D.IDENTITY
	var skin: Variant = owner_ref.get_ref()
	var transform: Transform2D = hands[side].node.global_transform
	return Transform2D(transform.x.normalized(),transform.y.normalized(),skin.bones[side+"_hand"].global_position)

func get_diagnostics() -> Dictionary:
	var records := {}
	for side in hands:
		var state: Dictionary = hands[side]
		records[side] = {"style":state.style,"asset":state.key,"registration":state.last,"visible":state.mesh.visible,"anatomy":"right hand back facing camera" if side=="near" else "left palm facing camera","mesh":str(state.mesh.name),"shader":SHADER_PATH,"whole_arm_source":true,"socket_textureless":state.node.texture==null,"rigid_wrist_mount":true}
	var source := {}
	for key in assets: source[key] = {"path":assets[key].path,"native_size":assets[key].size,"sha256":assets[key].sha256}
	return {"loaded":errors.is_empty() and hands.size()==2,"errors":errors,"pose_override":pose_override,"hands":records,"assets":source,"distinct_near_far_images":true,"sword_source_sha256":sword_source_sha256,"sword_preserved":"exact original v21 source and legacy blend boundary","clothing":"new complete shoulder-to-fingertips drawings on approved fixed arm rig","target_palm_width":target_palm_width,"mount":"registered wrist directly on hand Bone2D; one measured physical palm width shared across poses; no dynamic bone scaling","sampling":"single continuous full-arm source per variant; static similarity maps at shoulder/elbow/wrist; local same-source contour margin in deep elbow folds only"}
