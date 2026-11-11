extends Node
## Real map rendered offscreen, using an isolated save directory.
const MapRuntime = preload("res://gdscripts/world/BaiChuanFreeWorldRuntime.gd")
const OUT := "res://output/bai_chuan_ground_details_20261106/"
var map
var viewport: SubViewport
var checks := 0
var failures: Array[String] = []
var samples: Array[Dictionary] = []

func _ready() -> void:
	if not OS.get_environment("APPDATA").replace("\\", "/").contains("bai_chuan_ground_details_20261106/"):
		get_tree().quit(2)
		return
	get_window().size = Vector2i(64,64)
	get_window().position = Vector2i(-16000,-16000)
	get_window().set_flag(Window.FLAG_NO_FOCUS,true)
	_check(_hide_native_window(), "only this preview window is hidden")
	viewport = SubViewport.new()
	viewport.size = Vector2i(1920,1080)
	viewport.world_2d = World2D.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(viewport)
	map = MapRuntime.new()
	map.size = Vector2(1920,1080)
	viewport.add_child(map)
	await _frames(6)
	map.set_process(false)
	_check(map.navigation.ready and map.terrain_travel.ready, "actual production navigation and terrain loaded")
	_check(is_instance_valid(map.landscape.ground_details), "ground detail renderer configured")
	if not is_instance_valid(map.landscape.ground_details):
		_finish()
		return
	_check(map.landscape.ground_details.total > 8000, "real layout loaded without creating individual prop nodes")
	_check(map.place_views.size()==123 and is_equal_approx(map.get_world_size(),22118.4), "places and map scale preserved")
	map._set_zoom_level(2,Vector2(960,540))
	for case: Dictionary in [{"id":"white_stone_town","name":"white_stone"}, {"id":"yuxu_xianzong_core","name":"yuxu"}, {"point":Vector2(10900,19400),"name":"southern_plain"}]:
		if case.has("id"):
			_check(map.set_initial_runtime_id(case.id), "runtime location retained " + str(case.id))
			map.focus_world_point(map.player_mark.position)
		else:
			map.player_mark.position = map.navigation.nearest_walkable(case.point)
			map.player_halo.position = map.player_mark.position
			map.focus_world_point(case.point)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		_set_detail_strength(0.0)
		map.landscape.ground_details.hide()
		await _capture(str(case.name)+"_before.png")
		if str(case.name)=="yuxu":
			_set_detail_strength(1.0)
			await _capture("yuxu_pigment_only.png")
			_set_detail_strength(0.0)
			map.landscape.ground_details.show()
			await _capture("yuxu_decorations_only.png")
		_set_detail_strength(1.0)
		map.landscape.ground_details.show()
		await _capture(str(case.name)+"_after.png")
		var summary: Dictionary = map.landscape.ground_details.get_debug_summary()
		_check(int(summary.visible_instances)>25, "visible clustered decorations " + str(case.name))
		_check(int(summary.atlases)==1 and int(summary.active_chunks)<60, "shared atlas and bounded active chunks " + str(case.name))
		samples.append({"name":case.name,"camera":map.camera_pos,"zoom":map.world_zoom,"details":summary})
	for level in [0,1,2]:
		var before: Vector2 = map.player_mark.position
		map._set_zoom_level(level,Vector2(960,540))
		map.focus_world_point(before)
		map.landscape.refresh(true)
		map.landscape.update(0.0)
		_check(map.player_mark.position==before, "camera changes preserve actor coordinates " + str(level))
		var summary: Dictionary = map.landscape.ground_details.get_debug_summary()
		_check(int(summary.draw_calls)==0 if level==0 else int(summary.draw_calls)>0, "detail LOD batches " + str(level))
	_finish()

func _set_detail_strength(amount: float) -> void:
	for node in map.world.get_children():
		if node is Sprite2D and str(node.name).begins_with("Background"):
			(node.material as ShaderMaterial).set_shader_parameter("ground_detail_strength",amount)

func _frames(count: int) -> void:
	for i in count: await get_tree().process_frame

func _capture(filename: String) -> void:
	await _frames(3)
	await RenderingServer.frame_post_draw
	_check(viewport.get_texture().get_image().save_png(OUT+filename)==OK, "actual render " + filename)

func _hide_native_window() -> bool:
	var handle := DisplayServer.window_get_native_handle(DisplayServer.WINDOW_HANDLE)
	var code := """Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class GroundDetailPreviewWindow { [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int c); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p); }';
$detailWindowHandle = [IntPtr]__HANDLE__;
[uint32]$detailWindowOwner = 0;
[void][GroundDetailPreviewWindow]::GetWindowThreadProcessId($detailWindowHandle,[ref]$detailWindowOwner);
if ($detailWindowOwner -ne __OWNER__) { exit 3 };
[void][GroundDetailPreviewWindow]::ShowWindowAsync($detailWindowHandle,0);
"""
	code=code.replace("__HANDLE__",str(handle)).replace("__OWNER__",str(OS.get_process_id()))
	return OS.execute("powershell.exe",["-NoProfile","-NonInteractive","-WindowStyle","Hidden","-EncodedCommand",Marshalls.raw_to_base64(code.to_utf16_buffer())],[],true,false)==0

func _check(ok: bool, message: String) -> void:
	checks+=1
	if not ok: failures.append(message)
	print(("PASS " if ok else "FAIL ")+message)

func _finish() -> void:
	var report := {"checks":checks,"failures":failures,"samples":samples,"world_size":22118.4,"closest_zoom":0.8}
	var file := FileAccess.open(OUT+"actual_map_visual_report.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t")+"\n")
	print("GROUND_DETAILS_VISUAL_CHECKS ",checks," FAILURES ",failures.size())
	get_tree().quit(0 if failures.is_empty() else 1)
