extends SceneTree

const Encounters = preload("res://gdscripts/world/BaiChuanRoadEncounters.gd")

func _initialize() -> void:
	var a = Encounters.new()
	var b = Encounters.new()
	a.initialize(4456)
	b.initialize(4456)
	var unchanged: Dictionary = a.persistent_state()
	assert(a.consume_edge(1000.0, false).is_empty())
	assert(a.persistent_state() == unchanged, "safe stretches must not consume the distance clock")
	var first := {}
	for i in 100:
		first = a.consume_edge(1.0, true)
		if not first.is_empty(): break
	var second := {}
	for i in 10:
		second = b.consume_edge(10.0, true)
		if not second.is_empty(): break
	assert(not first.is_empty() and first.id == second.id and first.kind == second.kind, "node subdivision changed the encounter draw")
	var saved: Dictionary = a.persistent_state()
	var restored = Encounters.new()
	assert(restored.restore_state(saved))
	assert(restored.consume_edge(2.0, true) == first, "loading a pending encounter rerolled it")
	a.resolve_encounter()
	restored.resolve_encounter()
	assert(a.persistent_state() == restored.persistent_state(), "next encounter changed after load")
	var total := 0.0
	var wildlife := 0
	var samples := 10000
	var population = Encounters.new()
	population.initialize(117)
	for i in samples:
		total += population.distance_until_encounter
		var event: Dictionary = population.consume_edge(population.distance_until_encounter + 0.000001, true)
		if event.kind == "wildlife": wildlife += 1
		population.resolve_encounter()
	assert(absf(total / samples - 60.0) < 3.0, "unexpected mean distance")
	assert(absf(float(wildlife) / samples - 0.8) < 0.025, "unexpected encounter proportions")
	print("BAICHUAN_ROAD_ENCOUNTERS_OK mean=%.2f wildlife=%.3f" % [total/samples, float(wildlife)/samples])
	quit()
