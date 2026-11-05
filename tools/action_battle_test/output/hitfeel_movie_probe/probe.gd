extends SceneTree
var frame := 0
var sound: AudioStreamPlayer
var panel: ColorRect

func _initialize() -> void:
	call_deferred("setup")

func setup() -> void:
	root.size = Vector2i(320,180)
	root.position = Vector2i(-4096,-4096)
	panel = ColorRect.new()
	panel.size = Vector2(320,180)
	panel.color = Color("c5cbbf")
	root.add_child(panel)
	var label := Label.new()
	label.text = "Engine movie/audio probe"
	label.position = Vector2(15,65)
	panel.add_child(label)
	var data := PackedByteArray()
	data.resize(24000*2)
	for index in range(24000):
		var t := float(index)/48000.0
		var envelope := sin(minf(1.0,t/0.02)*PI*0.5)*exp(-t*8.0)
		data.encode_s16(index*2,int(sin(t*TAU*440.0)*envelope*10000.0))
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = 48000
	stream.data = data
	sound = AudioStreamPlayer.new()
	sound.stream = stream
	root.add_child(sound)

func _process(_delta: float) -> bool:
	frame += 1
	if sound != null and frame in [15,60]:
		sound.play()
		print("PROBE_AUDIO frame=",frame)
	if panel != null:
		panel.color = Color("c5cbbf").lerp(Color("6e8a7b"),float(frame%60)/60.0)
	return false
