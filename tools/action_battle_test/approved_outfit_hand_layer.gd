extends RefCounted

## Five independent bare-hand images on fixed wrist mounts. The approved
## sleeve/cuff and existing sword-fingers texture remain separate invariants.
const DEFAULT_DIR := "res://art/approved_outfit_hands_20261105"
const KEYS := ["near_open","far_open","near_fist","far_fist","near_grip"]
const WRIST_SOURCE := Vector2(609.0,473.0)
const CUFF_EXIT_SOURCE := Vector2(627.3,458.1)
const FOREARM_SOURCE := Vector2(143.0,-89.0)
const DEFAULT_WRIST := Vector2(512.0,820.0)
const DEFAULT_GRIP := Vector2(512.0,590.0)
const TARGET_PALM_WIDTH := 4.5

var owner_ref: WeakRef
var assets: Dictionary = {}
var registrations: Dictionary = {}
var hands: Dictionary = {}
var errors: Array[String] = []
var visible := true
var pose_override := "auto"
var art_dir := DEFAULT_DIR
var sword_source_sha256 := ""

func _vec(value: Variant, fallback: Vector2) -> Vector2:
	if value is Vector2: return value
	if value is Array and value.size()==2: return Vector2(float(value[0]),float(value[1]))
	return fallback

func build(owner_skin: RefCounted, directory: String = DEFAULT_DIR) -> bool:
	if not hands.is_empty():
		errors.append("Hand layer already built")
		return false
	owner_ref = weakref(owner_skin)
	art_dir = directory
	var config_path := directory.path_join("hand_registration.json")
	if FileAccess.file_exists(config_path):
		var config: Variant = JSON.parse_string(FileAccess.get_file_as_string(config_path))
		if config is Dictionary: registrations = config.get("hands",config)
	for key in KEYS:
		var path := directory.path_join(key+".png")
		var image := Image.new()
		if image.load(ProjectSettings.globalize_path(path))!=OK:
			errors.append("Missing regenerated hand "+path)
			continue
		var size := image.get_size()
		image.generate_mipmaps()
		assets[key] = {"texture":ImageTexture.create_from_image(image),"path":path,"size":[size.x,size.y],"sha256":FileAccess.get_sha256(path)}
	if not errors.is_empty(): return false
	var skin: Variant = owner_ref.get_ref()
	sword_source_sha256 = FileAccess.get_sha256("res://art/yuxu_fullbody_v15/arm_sword_fingers_v21.png")
	for side in ["near","far"]:
		if not skin.bones.has(side+"_hand") or not skin.arm_materials.has(side):
			errors.append("Missing approved wrist/material "+side)
			continue
		var sprite := Sprite2D.new()
		sprite.name = "IndependentBareHand_"+side
		sprite.centered = false
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		var bare_material := ShaderMaterial.new()
		bare_material.shader = load("res://approved_outfit_bare_hand.gdshader")
		sprite.material = bare_material
		sprite.z_as_relative = false
		# The unchanged cuff covers the cutout's wrist base and its straight
		# outline, while the fingers remain rigidly mounted beyond the cuff.
		sprite.z_index = 69 if side=="near" else 39
		skin.bones[side+"_hand"].add_child(sprite)
		var material: ShaderMaterial = skin.arm_materials[side]
		material.shader = load("res://approved_outfit_hand_erase.gdshader")
		material.set_shader_parameter("sword_hand",skin.textures.arm_sword_fingers_v21)
		hands[side] = {"node":sprite,"material":material,"style":"","key":"","last":{}}
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
		var sprite: Sprite2D = state.node
		if sword:
			sprite.visible = false
			state.style = style
			state.key = "original_v21_sword"
			continue
		var key: String = side+"_"+style
		if not assets.has(key): continue
		var registration: Dictionary = registrations.get(key,{})
		var wrist := _vec(registration.get("wrist_px"),DEFAULT_WRIST)
		var palm_width := maxf(1.0,float(registration.get("palm_width_px",260.0)))
		var target_width := float(registration.get("target_palm_width",TARGET_PALM_WIDTH))
		var art_scale := target_width/palm_width*float(registration.get("scale_multiplier",1.0))
		var rotation := FOREARM_SOURCE.angle()+PI*0.5+deg_to_rad(float(registration.get("rotation_degrees",0.0)))
		var attach_source := _vec(registration.get("cuff_exit_source_px"),CUFF_EXIT_SOURCE)
		var mount := (attach_source-WRIST_SOURCE)*float(skin.PIXEL_SCALE)
		sprite.texture = assets[key].texture
		sprite.scale = Vector2.ONE*art_scale
		sprite.rotation = rotation
		sprite.position = mount-(wrist*art_scale).rotated(rotation)
		sprite.visible = visible
		state.style = style
		state.key = key
		state.last = {"wrist_px":[wrist.x,wrist.y],"palm_width_px":palm_width,"target_palm_width":target_width,"scale":art_scale,"rotation":rotation,"cuff_exit_source_px":[attach_source.x,attach_source.y]}

func set_visible(value: bool) -> void:
	visible = value
	for side in hands:
		hands[side].node.visible = value and str(hands[side].style)!="sword"

func get_hand_node(side: String = "near") -> Sprite2D:
	return hands[side].node if hands.has(side) else null

func get_attachment_transform(side: String = "near") -> Transform2D:
	if not hands.has(side): return Transform2D.IDENTITY
	var state: Dictionary = hands[side]
	var sprite: Sprite2D = state.node
	var registration: Dictionary = registrations.get(str(state.key),{})
	var grip := _vec(registration.get("grip_px"),DEFAULT_GRIP)
	var transform := sprite.global_transform
	return Transform2D(transform.x.normalized(),transform.y.normalized(),sprite.to_global(grip))

func get_wrist_transform(side: String = "near") -> Transform2D:
	if not hands.has(side): return Transform2D.IDENTITY
	var state: Dictionary = hands[side]
	var sprite: Sprite2D = state.node
	var registration: Dictionary = registrations.get(str(state.key),{})
	var wrist := _vec(registration.get("wrist_px"),DEFAULT_WRIST)
	var transform := sprite.global_transform
	return Transform2D(transform.x.normalized(),transform.y.normalized(),sprite.to_global(wrist))

func get_diagnostics() -> Dictionary:
	var records := {}
	for side in hands:
		var state: Dictionary = hands[side]
		records[side] = {"style":state.style,"asset":state.key,"registration":state.last,"visible":state.node.visible,"anatomy":"right hand back facing camera" if side=="near" else "left palm facing camera","rigid_wrist_mount":true}
	var source := {}
	for key in assets: source[key] = {"path":assets[key].path,"native_size":assets[key].size,"sha256":assets[key].sha256}
	return {"loaded":errors.is_empty() and hands.size()==2,"errors":errors,"pose_override":pose_override,"hands":records,"assets":source,"distinct_near_far_images":true,"sword_source_sha256":sword_source_sha256,"sword_preserved":"original v21 texture and legacy shader boundary; no generated replacement","clothing":"original sleeve/cuff pixels; old skin only erased beyond measured oblique cuff","mount":"fixed hand Bone2D; one uniform image scale per pose; no finger warping"}
