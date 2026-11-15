extends SceneTree
## Offline, lossless-source portrait derivatives for encyclopedia catalogue rows.
## Run with --headless --path <project> --script res://tools/BakeEncyclopediaThumbnails.gd.

const CATALOGUE := "res://resources/bestiary/creatures.json"
const WOLF_PORTRAIT := "res://textures/bestiary/static/forest_wolf.png"
const OUTPUT_DIR := "res://textures/ui/encyclopedia/portraits/"
const MANIFEST_PATH := OUTPUT_DIR + "manifest.json"
const MAX_WIDTH := 144
const MAX_HEIGHT := 96

func _initialize() -> void:
	call_deferred("_bake")

func _fail(message: String) -> void:
	push_error(message)
	quit(1)

func _bake() -> void:
	if not FileAccess.file_exists(CATALOGUE):
		_fail("Missing creature catalogue: " + CATALOGUE)
		return
	var catalogue: Variant = JSON.parse_string(FileAccess.get_file_as_string(CATALOGUE))
	if not catalogue is Array:
		_fail("Creature catalogue must be a JSON array")
		return
	var sources: Dictionary = {WOLF_PORTRAIT: true}
	for entry: Variant in catalogue:
		if not entry is Dictionary:
			_fail("Creature catalogue contains a non-object entry")
			return
		var source: String = str(entry.get("portrait", "")).strip_edges()
		if source.is_empty() or not source.begins_with("res://"):
			_fail("Creature entry has no valid portrait path: " + str(entry.get("id", "")))
			return
		sources[source] = true
	var mkdir_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUTPUT_DIR))
	if mkdir_error != OK:
		_fail("Cannot create thumbnail directory: " + error_string(mkdir_error))
		return
	var previous: Dictionary = {}
	if FileAccess.file_exists(MANIFEST_PATH):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
		if parsed is Dictionary:
			previous = parsed
	var manifest: Dictionary = {}
	var source_paths: Array = sources.keys()
	source_paths.sort()
	var generated := 0
	var skipped := 0
	var total_pixels := 0
	var total_bytes := 0
	var source_pixels := 0
	var source_bytes := 0
	for source: String in source_paths:
		if not FileAccess.file_exists(source):
			_fail("Missing portrait source: " + source)
			return
		var source_file := FileAccess.open(source, FileAccess.READ)
		if source_file == null:
			_fail("Cannot open portrait source: " + source)
			return
		var size_bytes := source_file.get_length()
		source_file.close()
		var modified := FileAccess.get_modified_time(source)
		var target := OUTPUT_DIR + source.sha256_text().substr(0, 16) + ".png"
		var old: Dictionary = previous.get(source, {})
		var thumbnail: Image = null
		var unchanged := (str(old.get("path", "")) == target
			and int(old.get("source_mtime", -1)) == modified
			and int(old.get("source_bytes", -1)) == size_bytes
			and int(old.get("max_width", -1)) == MAX_WIDTH
			and int(old.get("max_height", -1)) == MAX_HEIGHT)
		if unchanged and FileAccess.file_exists(target):
			thumbnail = Image.load_from_file(ProjectSettings.globalize_path(target))
			if thumbnail != null and not thumbnail.is_empty() and thumbnail.get_width() <= MAX_WIDTH and thumbnail.get_height() <= MAX_HEIGHT:
				skipped += 1
			else:
				thumbnail = null
		var original_width := int(old.get("source_width", 0))
		var original_height := int(old.get("source_height", 0))
		if thumbnail == null:
			var original := Image.load_from_file(ProjectSettings.globalize_path(source))
			if original == null or original.is_empty():
				_fail("Cannot decode portrait source: " + source)
				return
			original_width = original.get_width()
			original_height = original.get_height()
			var scale_factor := minf(1.0, minf(float(MAX_WIDTH) / original_width, float(MAX_HEIGHT) / original_height))
			var width := maxi(1, roundi(original_width * scale_factor))
			var height := maxi(1, roundi(original_height * scale_factor))
			original.resize(width, height, Image.INTERPOLATE_LANCZOS)
			var save_error := original.save_png(target)
			if save_error != OK:
				_fail("Cannot save thumbnail " + target + ": " + error_string(save_error))
				return
			thumbnail = Image.load_from_file(ProjectSettings.globalize_path(target))
			generated += 1
		if thumbnail == null or thumbnail.is_empty() or thumbnail.get_width() > MAX_WIDTH or thumbnail.get_height() > MAX_HEIGHT:
			_fail("Thumbnail verification failed: " + target)
			return
		var target_file := FileAccess.open(target, FileAccess.READ)
		if target_file == null:
			_fail("Cannot verify thumbnail file: " + target)
			return
		var target_bytes := target_file.get_length()
		target_file.close()
		manifest[source] = {
			"path": target, "source_mtime": modified, "source_bytes": size_bytes,
			"source_width": original_width, "source_height": original_height,
			"max_width": MAX_WIDTH, "max_height": MAX_HEIGHT,
			"width": thumbnail.get_width(), "height": thumbnail.get_height()
		}
		total_pixels += thumbnail.get_width() * thumbnail.get_height()
		total_bytes += target_bytes
		source_pixels += original_width * original_height
		source_bytes += size_bytes
	var manifest_file := FileAccess.open(MANIFEST_PATH, FileAccess.WRITE)
	if manifest_file == null:
		_fail("Cannot write thumbnail manifest: " + MANIFEST_PATH)
		return
	manifest_file.store_string(JSON.stringify(manifest, "\t") + "\n")
	manifest_file.close()
	print("Encyclopedia thumbnails verified: count=%d generated=%d skipped=%d pixels=%d bytes=%d source_pixels=%d source_bytes=%d" % [manifest.size(), generated, skipped, total_pixels, total_bytes, source_pixels, source_bytes])
	quit(0)
