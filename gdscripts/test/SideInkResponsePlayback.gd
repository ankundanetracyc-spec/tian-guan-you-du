extends RefCounted

const ART_ROOT := "res://textures/characters/side_ink_responses_20261015_v4/"
const REST_ART_ROOT := "res://textures/characters/side_ink_rest_20261016_v1/"
const DODGE_ART_ROOT := "res://textures/characters/side_ink_dodge_20261016_v1/"
const FX_ROOT := "res://textures/vfx/ink_response_sequences_20261016_v4/"
const FPS := 30.0
const CONTACT := 1.36
const HURT_HOLD := .14
const GUARD_HOLD := .18
const ACTIVE_START := 1.10
const REST_FX_START := .98
const END := 2.70

var preview: Node2D
var frames: SpriteFrames
var phase_text := "待机"
var frame_number := 0
var frame_count := 0
var landmarks: Dictionary = {}

func configure(owner_preview: Node2D) -> void:
	preview=owner_preview
	frames=load(FX_ROOT+"response_effects.tres") as SpriteFrames
	var parsed: Variant=JSON.parse_string(FileAccess.get_file_as_string(ART_ROOT+"response_manifest.json"))
	if parsed is Dictionary:
		landmarks=parsed.get("landmarks",{})

func attach(actor: Dictionary) -> void:
	var root: Node2D=actor["root"]
	for pose in ["hurt","guard"]:
		var path: String=ART_ROOT+str(actor["id"])+"_"+str(pose)+".png"
		var texture: Texture2D=load(path) as Texture2D if ResourceLoader.exists(path) else null
		var sprite: Sprite2D=preview._make_sprite(str(actor["id"])+pose.capitalize(),texture)
		root.add_child(sprite)
		sprite.visible=false
		actor[pose]=sprite
	var rest_path:=REST_ART_ROOT+str(actor["id"])+"_rest.png"
	var rest_texture: Texture2D=load(rest_path) as Texture2D if ResourceLoader.exists(rest_path) else null
	var rest_sprite: Sprite2D=preview._make_sprite(str(actor["id"])+"Rest",rest_texture)
	root.add_child(rest_sprite)
	rest_sprite.visible=false
	actor["rest"]=rest_sprite
	var dodge_path:=DODGE_ART_ROOT+str(actor["id"])+"_dodge.png"
	var dodge_texture: Texture2D=load(dodge_path) as Texture2D if ResourceLoader.exists(dodge_path) else null
	var dodge_sprite: Sprite2D=preview._make_sprite(str(actor["id"])+"Dodge",dodge_texture)
	root.add_child(dodge_sprite)
	dodge_sprite.visible=false
	actor["dodge"]=dodge_sprite
	var points: Dictionary=landmarks.get(actor["id"],{})
	var body_data: Array=points.get("hurt",[110,-860])
	var guard_data: Array=points.get("guard",[350,-900])
	var body:=Vector2(float(body_data[0]),float(body_data[1]))
	var guard:=Vector2(float(guard_data[0]),float(guard_data[1]))
	actor["response_points"]={"hurt":body,"guard":guard}
	actor["response_effects"]={
		"hurt":_effect("hurt",body-Vector2(.35*900.0,0.0),Vector2(900,800)),
		"guard":_effect("guard",guard-Vector2(.25*1000.0,0.0),Vector2(1000,800)),
		"restore":_effect("restore",Vector2(140,-860),Vector2(600,660)),
		"dodge":_effect("dodge",Vector2(160,-850),Vector2(1000,700))
	}
	for effect in actor["response_effects"].values():
		root.add_child(effect)

func _effect(kind: String, at: Vector2, dimensions: Vector2) -> AnimatedSprite2D:
	var effect:=AnimatedSprite2D.new()
	effect.name=kind.capitalize()+"Sequence"
	effect.sprite_frames=frames
	effect.animation=kind
	effect.position=at
	effect.scale=dimensions/768.0
	effect.z_index=5
	effect.texture_filter=CanvasItem.TEXTURE_FILTER_LINEAR
	effect.visible=false
	return effect

func hide_responses() -> void:
	for actor in preview.actors:
		actor["hurt"].visible=false
		actor["guard"].visible=false
		actor["rest"].visible=false
		actor["dodge"].visible=false
		actor["root"].rotation=0.0
		for effect in actor["response_effects"].values():
			effect.visible=false

func apply(kind: String, time: float) -> void:
	var elapsed:=0.0
	var visible:=false
	var pose:="idle"
	var offset:=0.0
	var tilt:=0.0
	var zoom:=1.0
	var shake:=Vector2.ZERO
	var rest_blend:=0.0
	var playback_fps: float=30.0 if kind=="restore" else (21.0 if kind=="hurt" else 24.0)
	phase_text="待机"
	preview.phase_name="idle"
	if kind=="hurt":
		elapsed=time-CONTACT
		if elapsed>=2.0/playback_fps:
			elapsed=2.0/playback_fps+maxf(0.0,time-CONTACT-2.0/playback_fps-HURT_HOLD)
		visible=time>=CONTACT
		if time>=CONTACT and time<2.65:
			pose="hurt"
			var release:=smoothstep(1.86,2.65,time)
			var recoil:=1.0-pow(1.0-clampf((time-CONTACT)/.055,0.0,1.0),3.0)
			offset=-68.0*recoil*(1.0-release)
			tilt=-.025*recoil*(1.0-release)
			phase_text="受击定格" if time<1.57 else "受击回稳"
			preview.phase_name="impact" if time<1.57 else "recover"
			zoom=1.0+.23*(1.0-release)
			var kick:=time-CONTACT
			if kick<.06:
				shake=Vector2(sin(kick*180.0)*8.0,cos(kick*210.0)*3.0)*(1.0-kick/.06)
	elif kind=="guard":
		elapsed=time-ACTIVE_START
		if time>=CONTACT:
			elapsed=8.0/playback_fps+maxf(0.0,time-CONTACT-GUARD_HOLD)
		visible=time>=ACTIVE_START
		if time>=ACTIVE_START and time<2.60:
			pose="guard"
			var release:=smoothstep(1.90,2.60,time)
			offset=-20.0*smoothstep(CONTACT,CONTACT+.05,time)*(1.0-release)
			zoom=1.0+.22*smoothstep(ACTIVE_START,CONTACT,time)*(1.0-release)
			phase_text="架势迎击" if time<CONTACT else ("截击定格" if time<CONTACT+GUARD_HOLD else "余劲拨散")
			preview.phase_name="windup" if time<CONTACT else ("impact" if time<CONTACT+GUARD_HOLD else "recover")
	elif kind=="restore":
		elapsed=time-REST_FX_START
		visible=time>=REST_FX_START
		if time>=ACTIVE_START and time<END:
			rest_blend=smoothstep(ACTIVE_START,1.26,time)*(1.0-smoothstep(2.35,2.62,time))
			pose="rest" if rest_blend>0.0 else "idle"
			phase_text="轻息回拢" if time<1.65 else ("休整回气" if time<2.30 else "余光渐息")
			preview.phase_name="restore"
			zoom=1.0
	elif kind=="dodge":
		elapsed=time-CONTACT
		visible=time>=CONTACT
		if time>=CONTACT and time<2.55:
			pose="dodge"
			var release:=smoothstep(1.78,2.55,time)
			offset=-130.0*smoothstep(CONTACT,CONTACT+.10,time)*(1.0-release)
			zoom=1.0+.16*(1.0-release)
			phase_text="撤身让锋" if time<1.80 else "收步回位"
			preview.phase_name="dodge"
	frame_count=frames.get_frame_count(kind)
	frame_number=clampi(floori(elapsed*playback_fps+.0001),0,frame_count-1)
	visible=visible and frame_number<frame_count-1 and preview.effects_enabled and kind!="guard"
	if preview.display_size_mode!=0 or not preview.effects_enabled:
		zoom=1.0
		shake=Vector2.ZERO
	if preview.comparison:
		zoom=1.0+(zoom-1.0)*.65
	preview.cinematic_zoom=zoom
	preview.camera_shake=shake
	for actor in preview.actors:
		actor["slash"].visible=false
		actor["impact"].visible=false
		for pose_key in ["idle","chop","hurt","guard","rest","dodge"]:
			actor[pose_key].visible=false
			actor[pose_key].modulate=Color.WHITE
		var chosen: Sprite2D=actor[pose]
		if pose!="idle" and (chosen.texture==null or preview.simulate_missing_action):
			chosen=actor["idle"]
		chosen.visible=true
		if kind=="restore" and chosen==actor["rest"]:
			chosen.modulate.a=rest_blend
			actor["idle"].visible=rest_blend<1.0
			actor["idle"].modulate.a=1.0-rest_blend
		var direction: float=-1.0 if preview.mirrored else 1.0
		var base_scale: float=actor["base_scale"]
		var root: Node2D=actor["root"]
		root.position=actor["base_position"]+Vector2(offset*direction,0.0)*base_scale
		root.scale=Vector2(direction*base_scale,base_scale)
		root.rotation=tilt*direction
		for effect_kind in actor["response_effects"]:
			var effect: AnimatedSprite2D=actor["response_effects"][effect_kind]
			effect.visible=effect_kind==kind and visible
			if effect_kind==kind:
				effect.frame=frame_number
	preview._apply_local_focus()
