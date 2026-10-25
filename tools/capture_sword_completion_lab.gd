extends SceneTree
const OUTPUT := "res://docs/美术规范/持剑受击与女修剑招_20261025/"
var checks := 0

func _initialize() -> void:
	call_deferred("_capture")

func _check(condition: bool,label: String) -> bool:
	if not condition:
		push_error(label)
		quit(1)
		return false
	checks+=1
	print("LAB PASS: "+label)
	return true

func _save(name: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT+name+".png")

func _capture() -> void:
	root.position=Vector2i(-4096,-4096)
	root.size=Vector2i(1440,810)
	root.get_node("GameplayInputRouter").set_process_input(false)
	var preview: Node=load("res://tscn/scene/InkVfxShowcase.tscn").instantiate()
	root.add_child(preview)
	current_scene=preview
	await create_timer(.35,false).timeout
	preview.call("SetPreviewCaster",true)
	for pose in ["cut","parry","nurture","finish"]:
		preview.call("PreviewSwordAction",pose,0,0)
		await process_frame
		await process_frame
		preview.call("SeekSwordAction",.82 if pose=="nurture" else .53)
		await process_frame
		var body: Sprite2D=preview.find_child("TwoLayerSwordPose",true,false).get_node("BodyAndFarHand")
		if not _check(body.texture.resource_path.begins_with("res://textures/characters/side_ink_sword_female_20261025_v1/"),"女修剑招入口: "+pose): return
		await _save("lab_female_"+pose)
	preview.call("PreviewSwordHurt",1,1)
	await process_frame
	await process_frame
	var doll: Node2D=preview.find_child("BattleVfxLayer",true,false).get_node("出手/BattlePaperDoll")
	var portrait: Sprite2D=doll.get_node("InkPresentation/Portrait")
	if not _check(portrait.texture.resource_path.ends_with("female_20261025_v1/hurt_body_back.png"),"女修持剑受击入口"): return
	var sword: Sprite2D=doll.find_child("ReplaceableSwordIcon",true,false)
	if not _check(sword.is_visible_in_tree() and sword.texture.resource_path.ends_with("broad_sword.png"),"受击入口可换剑图"): return
	preview.call("SetSwordIdlePaused",true)
	var point := doll.position
	var angle := sword.rotation
	var sequence: AnimatedSprite2D=preview.find_child("InkSequence_hurt",false,false).get_node("SequenceFrames")
	var effect_frame := sequence.frame
	await create_timer(.14,false).timeout
	if not _check(doll.position==point and sword.rotation==angle and sequence.frame==effect_frame and portrait.texture.resource_path.ends_with("hurt_body_back.png"),"测试入口暂停身体、剑与受击序列帧"): return
	preview.call("SetSwordPreviewDark",true)
	if not _check(preview.find_child("SwordDarkBackdrop",true,false).visible,"持剑受击入口可切换深色背景"): return
	await _save("lab_female_hurt")
	preview.call("SetSwordIdlePaused",false)
	await create_timer(1.7,false).timeout
	if not _check(portrait.texture.resource_path.ends_with("idle_body_back.png") and sword.is_visible_in_tree(),"测试入口恢复持剑待机"): return
	preview.call("SetPreviewCaster",false)
	preview.call("PreviewSwordHurt",2,0)
	await process_frame
	await process_frame
	if not _check(portrait.texture.resource_path.ends_with("sword_hurt_20261025_v1/hurt_body_back.png"),"男修持剑受击入口"): return
	await _save("lab_male_hurt")
	print("SWORD COMPLETION LAB: %d CHECKS PASSED"%checks)
	preview.queue_free()
	await process_frame
	quit()
