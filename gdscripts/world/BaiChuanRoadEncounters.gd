extends RefCounted
## Road encounters use travelled kilometres, independent of the number of route nodes.

const MEAN_DISTANCE_KM := 60.0
const WILDLIFE_SHARE := 0.8

var rng := RandomNumberGenerator.new()
var journey_seed := 0
var distance_until_encounter := 0.0
var sequence := 0
var pending: Dictionary = {}

func initialize(seed_value: int) -> void:
	journey_seed = seed_value
	rng.seed = seed_value
	sequence = 0
	pending.clear()
	distance_until_encounter = _draw_distance()

func _draw_distance() -> float:
	# An exponential distance clock gives a mean of 60 eligible kilometres, rather
	# than making long and short route segments equally likely to produce an event.
	return -MEAN_DISTANCE_KM * log(maxf(1.0 - rng.randf(), 0.0000001))

func consume_edge(kilometres: float, eligible: bool) -> Dictionary:
	if not pending.is_empty(): return pending.duplicate(true)
	if not eligible or not is_finite(kilometres) or kilometres <= 0.0: return {}
	distance_until_encounter -= kilometres
	if distance_until_encounter > 0.0: return {}
	sequence += 1
	pending = {
		"id": "road_%s_%d" % [str(journey_seed), sequence],
		"kind": "wildlife" if rng.randf() < WILDLIFE_SHARE else "bandit",
		"overshoot_km": -distance_until_encounter,
	}
	return pending.duplicate(true)

func resolve_encounter() -> void:
	if pending.is_empty(): return
	var overshoot := float(pending.get("overshoot_km", 0.0))
	pending.clear()
	distance_until_encounter = _draw_distance() - overshoot

func persistent_state() -> Dictionary:
	return {
		"version": 1,
		"seed": journey_seed,
		"rng_state": rng.state,
		"distance_until_encounter": distance_until_encounter,
		"sequence": sequence,
		"pending": pending.duplicate(true),
	}

func restore_state(state: Dictionary) -> bool:
	if int(state.get("version", 0)) != 1: return false
	var distance := float(state.get("distance_until_encounter", NAN))
	var saved_pending = state.get("pending", null)
	if not is_finite(distance) or not saved_pending is Dictionary: return false
	if not state.has("seed") or not state.has("rng_state") or not state.has("sequence"): return false
	if not saved_pending.is_empty() and (not saved_pending.has("id") or not saved_pending.get("kind", "") in ["wildlife", "bandit"] or not saved_pending.has("overshoot_km")): return false
	journey_seed = int(state.seed)
	rng.seed = journey_seed
	rng.state = int(state.rng_state)
	distance_until_encounter = distance
	sequence = maxi(0, int(state.sequence))
	pending = saved_pending.duplicate(true)
	return true
