extends RefCounted
## Optional renderer-only sample. The authored landscape and navigation files
## stay immutable; a valid patch is merged before any scenery is indexed.
const DATA_PATH := "res://resources/world/bai_chuan/forest_grounding_sample_20261106.json"

static func merge(metadata: Dictionary, data_path: String = "") -> Dictionary:
	if data_path.is_empty(): data_path = str(metadata.get("forest_grounding_patch_path", DATA_PATH))
	if not FileAccess.file_exists(data_path): return {"active": false}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(data_path))
	if not parsed is Dictionary: return _reject("invalid sample JSON")
	var patch: Dictionary = parsed
	if int(patch.get("version", 0)) != 1 or not is_equal_approx(float(patch.get("world_size", 0.0)), float(metadata.get("world_size", -1.0))):
		return _reject("sample version or world size differs")
	var sample_bounds := _rect(patch.get("bounds", []))
	if not sample_bounds.has_area(): return _reject("invalid sample bounds")
	var catalog_path := str(patch.get("tree_catalog", ""))
	if not FileAccess.file_exists(catalog_path): return _reject("tree catalog is unavailable")
	var parsed_catalog: Variant = JSON.parse_string(FileAccess.get_file_as_string(catalog_path))
	if not parsed_catalog is Dictionary: return _reject("invalid tree catalog")
	var source_catalog: Dictionary = parsed_catalog
	var catalog_assets: Dictionary = source_catalog.get("assets", {})
	if catalog_assets.is_empty(): return _reject("tree catalog is empty")
	var merged_catalog: Dictionary = metadata.tree_catalog.duplicate()
	for asset_id: String in catalog_assets:
		if merged_catalog.has(asset_id): return _reject("sample tree asset overrides an existing asset")
		var asset: Dictionary = catalog_assets[asset_id].duplicate(true)
		var region := _rect(asset.get("region", []))
		var pivot: Array = asset.get("pivot", [])
		if not region.has_area() or pivot.size() != 2: return _reject("invalid tree region or pivot")
		if not Vector2(float(pivot[0]), float(pivot[1])).is_finite() or float(pivot[0]) < 0.0 or float(pivot[0]) > 1.0 or float(pivot[1]) < 0.0 or float(pivot[1]) > 1.0:
			return _reject("tree pivot is outside its native region")
		var atlas_path := str(asset.get("atlas", source_catalog.get("atlas", "")))
		var runtime_path := str(asset.get("runtime_atlas", source_catalog.get("runtime_atlas", "") if atlas_path == str(source_catalog.get("atlas", "")) else ""))
		if not FileAccess.file_exists(atlas_path) and (runtime_path.is_empty() or not ResourceLoader.exists(runtime_path)):
			return _reject("tree atlas is unavailable")
		asset["atlas"] = atlas_path
		asset["runtime_atlas"] = runtime_path
		merged_catalog[asset_id] = asset
	var removed := {}
	for id: String in patch.get("removed_tree_ids", []): removed[id] = true
	var retained: Array = []
	var known_ids := {}
	var removed_count := 0
	for tree: Dictionary in metadata.trees:
		var id := str(tree.id)
		if removed.has(id):
			if not sample_bounds.intersects(_rect(tree.bounds)): return _reject("removed tree lies outside the sample")
			removed_count += 1
		else:
			retained.append(tree)
			known_ids[id] = true
	if removed_count != removed.size(): return _reject("removed tree id is unknown")
	var mountain_ids := {}
	for mountain: Dictionary in metadata.mountains:
		known_ids[str(mountain.id)] = true
		mountain_ids[str(mountain.id)] = mountain
	for bridge: Dictionary in metadata.get("bridges", []): known_ids[str(bridge.id)] = true
	var grounded := {}
	for id: String in patch.get("grounded_mountain_ids", []):
		if not mountain_ids.has(id) or not sample_bounds.intersects(_rect(mountain_ids[id].bounds)):
			return _reject("grounded mountain lies outside the sample")
		grounded[id] = true
	var additions: Array = patch.get("trees", [])
	if additions.is_empty(): return _reject("sample has no trees")
	var max_height := float(metadata.get("max_prop_height", 900.0))
	for tree: Dictionary in additions:
		var id := str(tree.get("id", ""))
		var asset_id := str(tree.get("asset", ""))
		var point: Array = tree.get("position", [])
		var bounds := _rect(tree.get("bounds", []))
		if id.is_empty() or known_ids.has(id) or not catalog_assets.has(asset_id) or point.size() != 2 or float(tree.get("height", 0.0)) <= 0.0:
			return _reject("invalid sample tree")
		if str(tree.get("kind", "")) != "tree" or str(tree.get("layer", "ground_props")) != "ground_props" or not is_equal_approx(float(tree.get("z", -1.0)), float(point[1])):
			return _reject("sample tree must sort by its fixed ground point")
		if not Vector2(float(point[0]), float(point[1])).is_finite() or not bounds.has_area() or not sample_bounds.encloses(bounds):
			return _reject("sample tree bounds lie outside the sample")
		known_ids[id] = true
		max_height = maxf(max_height, bounds.size.y)
	var details_path := str(patch.get("ground_details_path", ""))
	if not FileAccess.file_exists(details_path): return _reject("sample ground details are unavailable")
	# Commit only after every reference and sample boundary is checked.
	retained.append_array(additions)
	metadata["trees"] = retained
	metadata["tree_catalog"] = merged_catalog
	metadata["grounded_mountain_ids"] = grounded
	metadata["grounding_details_path"] = details_path
	metadata["max_prop_height"] = max_height
	return {"active": true, "path": data_path, "bounds": patch.bounds, "removed_trees": removed_count, "trees": additions.size(), "mountains": grounded.size(), "ground_details_path": details_path, "tree_catalog": catalog_path}

static func _rect(values: Array) -> Rect2:
	if values.size() != 4: return Rect2()
	var result := Rect2(float(values[0]), float(values[1]), float(values[2]), float(values[3]))
	return result if result.position.is_finite() and result.size.is_finite() else Rect2()

static func _reject(reason: String) -> Dictionary:
	push_warning("百川林地样区未启用：" + reason)
	return {"active": false, "reason": reason}
