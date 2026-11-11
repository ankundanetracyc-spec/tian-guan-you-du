extends RefCounted
## The free world scales authored coordinates without enlarging map markers.

const LEGACY_MAP_PIXELS := 3762.0
const LEGACY_WORLD_KM := 200.0
const PREVIOUS_FREE_MAP_PIXELS := 12288.0
const PREVIOUS_COMPACT_MAP_PIXELS := 7372.8
const MAP_PIXELS := 22118.4
const WORLD_KM := 8000.0
const COORDINATE_SCALE := MAP_PIXELS / LEGACY_MAP_PIXELS
const KM_PER_PIXEL := WORLD_KM / MAP_PIXELS
const NAVIGATION_SOURCE_CELL_SIZE := 8.0
const NAVIGATION_CELL_SIZE := 15.678468899521532
const NAVIGATION_COLUMNS := 1413
const TERRAIN_COLUMNS := 471
const TERRAIN_CELL_SIZE := NAVIGATION_SOURCE_CELL_SIZE * COORDINATE_SCALE
const TERRAIN_PATH := "res://resources/world/bai_chuan/terrain_travel_expanded3x_20261106.json"
const ART_BASE := "res://textures/world/bai_chuan/original_3x3_20261104/"
const MASTER_FILE := "master_8000.png"
const LAYOUT_PATH := "res://resources/world/bai_chuan/free_layout_expanded3x_20261106.json"
const NAVIGATION_PATH := "res://resources/world/bai_chuan/free_navigation_expanded3x_20261106.json"
const NAVIGATION_PROFILE := "expanded3x_20261106_v1"
const LANDSCAPE_PATH := "res://resources/world/bai_chuan/landscape_original_grounded_20261109.json"
const ART_COLUMNS := 3
