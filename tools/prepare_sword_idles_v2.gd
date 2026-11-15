extends SceneTree
const PACK := "res://textures/characters/side_ink_sword_idles_20261023_v2/"
func _initialize() -> void:
	for pose in ["held","orbit"]:
		var source := Image.load_from_file(ProjectSettings.globalize_path(PACK+"source/male_"+pose+"_idle.png"))
		source.convert(Image.FORMAT_RGBA8)
		# Shared production scale and ground registration, never runtime silhouette fitting.
		source.resize(1790,1790,Image.INTERPOLATE_LANCZOS)
		var registered := Image.create(2048,2048,false,Image.FORMAT_RGBA8)
		registered.blit_rect(source,Rect2i(0,0,1790,1790),Vector2i(129,162))
		registered.save_png(PACK+"male_"+pose+"_idle.png")
	print("SWORD_IDLE_V2_REGISTERED")
	quit()
