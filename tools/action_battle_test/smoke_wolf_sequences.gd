extends SceneTree

const COUNTS := {"idle":12,"walk":16,"trot":16,"windup":8,"lunge":6,"recover":10,"hurt":6,"death":12}
var checks := 0
var failures: Array[String] = []
var actor: Node2D
var observed := {}

func _initialize() -> void:
	call_deferred("_run")

func _local(path: String) -> String:
	return get_script().resource_path.get_base_dir().path_join(path)

func _check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures.append(label)
		push_error(label)

func _sample(state: String, duration: float, velocity := Vector2.ZERO) -> void:
	var seen := {}
	for tick in range(ceili(duration*120.0)+1):
		var time := minf(duration,tick/120.0)
		actor.global_position += velocity/120.0
		actor.set_motion_context(1.0/120.0,velocity,state,maxf(0,duration-time),1.0,true)
		if not seen.has(actor.current_clip): seen[actor.current_clip] = {}
		seen[actor.current_clip][actor.sprite.frame] = true
	for clip in seen:
		observed[clip] = maxi(int(observed.get(clip,0)),seen[clip].size())

func _run() -> void:
	actor = load(_local("forest_wolf_sequence.gd")).new()
	root.add_child(actor)
	actor.scale = Vector2.ONE*.55
	actor.position = Vector2(600,400)
	_check(actor.build(_local("art/forest_wolf_frames_v2")), "the complete sequence pack loads")
	_check(actor.find_children("*","Bone2D",true,false).is_empty(), "the wolf creates no bones")
	_check(actor.find_children("*","Polygon2D",true,false).is_empty(), "the wolf creates no deforming meshes")
	_check(actor.find_children("*","AnimatedSprite2D",true,false).size()==1, "native AnimatedSprite2D draws the whole animal")
	var unique_regions := {}
	for name in COUNTS:
		_check(actor.frames.has_animation(name), name+" exists")
		_check(actor.frames.get_frame_count(name)==COUNTS[name], name+" has the required source-frame count")
		var scale_before := Vector2.ZERO
		for index in range(COUNTS[name]):
			actor._show_frame(name,index)
			var texture: AtlasTexture = actor.frames.get_frame_texture(name,index)
			_check(texture != null and texture.atlas != null, name+" frame "+str(index)+" has its original atlas pixels")
			_check(texture.region.size.x>0 and texture.region.size.y>0, name+" frame has a valid source region")
			if index==0: scale_before=actor.sprite.scale
			_check(actor.sprite.scale==scale_before, name+" never rescales individual cels")
			var key := str(texture.atlas.get_instance_id())+":"+str(texture.region)
			unique_regions[key]=true
	_check(unique_regions.size()==86, "all 86 source cels are separately addressed")
	for state in ["windup","lunge","recover"]:
		var seconds := .8 if state=="windup" else (.2 if state=="lunge" else 1.5)
		actor.reset_pose()
		_sample(state,seconds,Vector2.ZERO)
		_check(int(observed.get(state,0))==COUNTS[state], state+" reaches every cel within the unchanged gameplay phase")
	actor.reset_pose()
	_sample("chase",1.0,Vector2(32,0))
	_check(int(observed.get("walk",0))==16, "walking displays all16 chronological cels")
	actor.reset_pose()
	_sample("chase",1.0,Vector2(95,0))
	_check(int(observed.get("trot",0))==16, "pursuit displays all16 trot cels")
	var paused_clip: String = actor.current_clip
	var paused_frame: int = actor.sprite.frame
	for tick in range(20):
		actor.set_motion_context(0,Vector2.ZERO,"chase",0,1,true)
	_check(actor.current_clip==paused_clip and actor.sprite.frame==paused_frame, "pause preserves the exact moving cel")
	var before: Vector2 = actor.global_position
	actor.set_motion_context(0,Vector2(95,0),"chase",0,-1,true)
	_check(actor.sprite.flip_h and actor.global_position==before, "facing mirrors the full image without moving the collision root")
	actor.reset_pose()
	_sample("idle",2.4)
	_check(int(observed.get("idle",0))==12, "idle loops through all12 source cels")
	actor.trigger_hurt()
	_sample("chase",.28)
	_check(int(observed.get("hurt",0))==6, "hurt uses six actual painted cels")
	actor.reset_pose()
	var death_frames := {}
	for tick in range(180):
		actor.set_motion_context(1.0/120.0,Vector2.ZERO,"chase",0,1,false)
		death_frames[actor.sprite.frame]=true
	observed["death"] = death_frames.size()
	_check(death_frames.size()==12 and actor.sprite.frame==11, "death reaches every cel and holds the last")
	for tick in range(60): actor.set_motion_context(1.0/60,Vector2.ZERO,"chase",0,1,false)
	_check(actor.sprite.frame==11, "dead wolves do not loop or stand again")
	actor.reset_pose()
	_check(actor.current_clip=="idle" and actor.sprite.frame==0, "restart restores the first neutral cel")
	var scene_path := "res://tscn/scene/ActionBattleOriginalWandererScene.tscn"
	if not ResourceLoader.exists(scene_path): scene_path=_local("ActionBattleFeelTest.tscn")
	var scene: Node2D=load(scene_path).instantiate()
	root.add_child(scene)
	scene.set_process(false)
	scene.set_process_input(false)
	_check(scene.skinned_wolf.get_runtime_summary().backend=="whole_animal_sequence", "the current battle uses whole-animal frames")
	_check(scene.skinned_wolf.find_children("*","Skeleton2D",true,false).is_empty(), "the live wolf no longer uses its old skeleton")
	_check(scene.WOLF_SPEED==95 and scene.WOLF_WINDUP==.8 and scene.WOLF_LUNGE_SECONDS==.2 and scene.WOLF_RECOVERY==1.5, "art replacement preserves wolf movement and attack timings")
	var output:=ProjectSettings.globalize_path(_local("../../output/forest_wolf_sequences_20261104")).simplify_path()
	DirAccess.make_dir_recursive_absolute(output)
	var result := {"passed":failures.is_empty(),"checks":checks,"failures":failures,"source_counts":COUNTS,"observed_cels":observed,"total":86,"runtime":actor.get_runtime_summary()}
	var environment := "main" if _local("").begins_with("res://tools/") else "standalone"
	for file_name in ["smoke_report.json","smoke_report_"+environment+".json"]:
		var report:=FileAccess.open(output.path_join(file_name),FileAccess.WRITE)
		report.store_string(JSON.stringify(result,"\t"))
	print("WOLF SEQUENCES ", "PASS" if failures.is_empty() else "FAIL", " checks=",checks)
	scene.queue_free()
	actor.queue_free()
	await process_frame
	quit(0 if failures.is_empty() else 1)
