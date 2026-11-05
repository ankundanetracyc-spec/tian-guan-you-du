extends RefCounted
## The free world scales authored coordinates without enlarging map markers.

const LEGACY_MAP_PIXELS := 3762.0
const LEGACY_WORLD_KM := 200.0
const PREVIOUS_FREE_MAP_PIXELS := 12288.0
const MAP_PIXELS := 7372.8
const WORLD_KM := 8000.0
const COORDINATE_SCALE := MAP_PIXELS / LEGACY_MAP_PIXELS
const KM_PER_PIXEL := WORLD_KM / MAP_PIXELS
const NAVIGATION_SOURCE_CELL_SIZE := 8.0
const NAVIGATION_CELL_SIZE := NAVIGATION_SOURCE_CELL_SIZE * COORDINATE_SCALE
const NAVIGATION_COLUMNS := 471
const ART_BASE := "res://textures/world/bai_chuan/original_3x3_20261104/"
const MASTER_FILE := "master_8000.png"
const LAYOUT_PATH := "res://resources/world/bai_chuan/free_layout_3x3_20261104.json"
const NAVIGATION_PATH := "res://resources/world/bai_chuan/free_navigation_3x3_20261104.json"
const ART_COLUMNS := 3
