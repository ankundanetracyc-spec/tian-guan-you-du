extends SceneTree
## Load-only validation: no device playback, no game scene or simulation state.
const DIRECTORY := "E:/stacklands/audio/combat_impact_20261105"

func _initialize() -> void:
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(DIRECTORY.path_join("manifest.json")))
	var checked := 0
	var failures := 0
	for filename in manifest.files:
		var stream := AudioStreamWAV.load_from_file(DIRECTORY.path_join(str(filename)))
		var valid := stream != null
		if valid:
			valid = stream.mix_rate==48000 and not stream.stereo and stream.format==AudioStreamWAV.FORMAT_16_BITS
			valid = valid and absf(stream.get_length()-float(manifest.files[filename].seconds))<1.0/48000.0
		checked += 1
		if not valid:
			failures += 1
			push_error("Invalid combat sound: "+str(filename))
	print("COMBAT_AUDIO_LOAD_CHECKS ",checked," failures=",failures)
	quit(1 if failures else 0)
