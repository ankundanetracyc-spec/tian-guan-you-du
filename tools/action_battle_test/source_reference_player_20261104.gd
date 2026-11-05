extends Node2D

## Independent source-mesh player: all vertex positions come from complete
## located Unity curves and source bindposes, without the old runtime adapter.
var data: Dictionary = {}
var draws: Array[Polygon2D] = []
var clip_name := "Run_New1"
var secondary := false
var zoom := 4.0

func build(source: Dictionary, original_texture_root: String) -> void:
	data = source
	for entry in data.meshes:
		var drawing := Polygon2D.new()
		var original := Image.new()
		assert(original.load(ProjectSettings.globalize_path(original_texture_root.path_join("sprite_%d.png" % int(entry.spriteId)))) == OK)
		drawing.texture = ImageTexture.create_from_image(original)
		drawing.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		var uv := PackedVector2Array()
		for value in entry.uv:
			uv.append(Vector2(value[0],value[1])*Vector2(original.get_size()))
		drawing.uv = uv
		var triangles: Array[PackedInt32Array] = []
		for indices in entry.triangles: triangles.append(PackedInt32Array(indices))
		drawing.polygons = triangles
		add_child(drawing)
		draws.append(drawing)
	set_pose("Run_New1",0.0)

func set_pose(name_value: String, phase: float) -> void:
	clip_name = name_value
	var clip: Dictionary = data.clips[clip_name]
	var normalized := fposmod(phase,1.0)*float(clip.sampleCount)
	var a := int(floor(normalized))%int(clip.sampleCount)
	var b := (a+1)%int(clip.sampleCount)
	var blend: float = normalized-floor(normalized)
	var frames: Array = clip.secondaryFrames if secondary else clip.frames
	for index in range(draws.size()):
		var vertices := PackedVector2Array()
		for vertex_index in range(frames[a][index].size()):
			var av: Array = frames[a][index][vertex_index]
			var bv: Array = frames[b][index][vertex_index]
			vertices.append(Vector2(av[0],av[1]).lerp(Vector2(bv[0],bv[1]),blend)*zoom)
		draws[index].polygon = vertices

func release() -> void:
	for drawing in draws:
		drawing.texture = null
		drawing.queue_free()
	draws.clear()
