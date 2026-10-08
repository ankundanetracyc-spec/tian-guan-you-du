"""Export the fixed L1-L3 Draw.io node layer as Godot runtime data."""
from html import unescape
from collections import deque
from pathlib import Path
import re
from typing import Tuple
import xml.etree.ElementTree as ET

from PIL import Image


SOURCE = Path("docs/昭明纪环海大世界地图底稿.drawio")
OUT = Path("resources/world/zhaoming_node_catalog.txt")
LEFT = 50.0
TOP = 110.0
CANVAS_SIZE = 1500.0
MAP_SIZE = 6400.0
LAND_PARTITION = Path("textures/world_map/territory_partition/zhaoming_land_partition_id.png")
STATIC_NODE_RESOURCE = Path("resources/world/wangyue_lake_nodes.tres")
NODE_SEPARATION = 105.0


def node_type(name: str) -> str:
    if any(word in name for word in ("遗", "秘境", "墓", "碑", "祠", "窟", "残", "裂", "洞")):
        return "WorldNodeData.NodeType.DUNGEON"
    if any(word in name for word in ("城", "都", "港", "镇", "关", "坊", "里", "渡")):
        return "WorldNodeData.NodeType.CITY"
    if any(word in name for word in ("宗", "宫", "门", "阁", "院", "寺", "庄", "府", "堡", "营")):
        return "WorldNodeData.NodeType.SECT"
    return "WorldNodeData.NodeType.RESOURCE"


def display_name(raw_name: str) -> str:
    """Keep the authored landmark title, not the Draw.io level/location note."""
    return re.split(r"\s+L[1-4]\s*(?:·|\\|).*", raw_name, maxsplit=1)[0].strip()


def display_level(raw_name: str, node_id: str) -> int:
    """Read the authored L1-L3 tag; legacy supplemental markers default to L2."""
    match = re.search(r"\sL([1-3])\b", raw_name)
    if match:
        return int(match.group(1))
    return 3 if node_id.startswith("additional") else 2


def build_land_components(land_mask: Image.Image):
    """Return connected land components; all but the largest are islands."""
    width, height = land_mask.size
    pixels = land_mask.load()
    labels = bytearray(width * height)
    component_sizes = []
    component_id = 0
    for y in range(height):
        for x in range(width):
            index = y * width + x
            if labels[index] or pixels[x, y] == (0, 0, 0):
                continue
            component_id += 1
            labels[index] = component_id
            queue = deque([(x, y)])
            size = 0
            while queue:
                px, py = queue.popleft()
                size += 1
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    nindex = ny * width + nx
                    if 0 <= nx < width and 0 <= ny < height and not labels[nindex] and pixels[nx, ny] != (0, 0, 0):
                        labels[nindex] = component_id
                        queue.append((nx, ny))
            component_sizes.append(size)
    mainland = component_sizes.index(max(component_sizes)) + 1
    return labels, {index + 1 for index in range(len(component_sizes)) if index + 1 != mainland}


def snap_to_land(position: Tuple[float, float], land_mask: Image.Image, components, island_components, occupied, require_island: bool) -> Tuple[float, float]:
    """Move a blueprint point in the sea to the nearest actual mainland/island.

    The editable blueprint is a planning layer, while the terrain image is the
    geographic authority.  Using the exact land partition here keeps every
    fixed landmark click target on a painted landmass without altering sea
    borders or territory hit-testing.
    """
    width, height = land_mask.size
    start_x = max(0, min(width - 1, round(position[0] / MAP_SIZE * (width - 1))))
    start_y = max(0, min(height - 1, round(position[1] / MAP_SIZE * (height - 1))))
    pixels = land_mask.load()

    separation_pixels = NODE_SEPARATION / MAP_SIZE * (width - 1)

    def is_land(x: int, y: int) -> bool:
        if pixels[x, y] == (0, 0, 0):
            return False
        if require_island and components[y * width + x] not in island_components:
            return False
        return all((x - ox) ** 2 + (y - oy) ** 2 >= separation_pixels ** 2 for ox, oy in occupied)

    if is_land(start_x, start_y):
        occupied.append((start_x, start_y))
        return position
    for radius in range(1, max(width, height)):
        left, right = start_x - radius, start_x + radius
        top, bottom = start_y - radius, start_y + radius
        for x in range(left, right + 1):
            for y in (top, bottom):
                if 0 <= x < width and 0 <= y < height and is_land(x, y):
                    occupied.append((x, y))
                    return x / (width - 1) * MAP_SIZE, y / (height - 1) * MAP_SIZE
        for y in range(top + 1, bottom):
            for x in (left, right):
                if 0 <= x < width and 0 <= y < height and is_land(x, y):
                    occupied.append((x, y))
                    return x / (width - 1) * MAP_SIZE, y / (height - 1) * MAP_SIZE
    if require_island:
        # A missing island component is a data issue, but a mainland fallback
        # keeps the catalogue usable until that hand-authored landmark is fixed.
        return snap_to_land(position, land_mask, components, island_components, occupied, False)
    raise RuntimeError("No land pixel available for node placement")


def main() -> None:
    root = ET.parse(SOURCE).getroot()
    if not LAND_PARTITION.exists():
        raise RuntimeError(f"Land partition not found: {LAND_PARTITION}")
    land_mask = Image.open(LAND_PARTITION).convert("RGB")
    components, island_components = build_land_components(land_mask)
    occupied = []
    # Keep blueprint nodes away from authored runtime landmarks as well.  The
    # resource also contains L4 templates; reserving their positions is harmless
    # and prevents a spawned event from covering a permanent landmark.
    if STATIC_NODE_RESOURCE.exists():
        static_text = STATIC_NODE_RESOURCE.read_text(encoding="utf-8")
        for x_text, y_text in re.findall(r"map_position = Vector2\\(([0-9.]+), ([0-9.]+)\\)", static_text):
            px = max(0, min(land_mask.width - 1, round(float(x_text) / MAP_SIZE * (land_mask.width - 1))))
            py = max(0, min(land_mask.height - 1, round(float(y_text) / MAP_SIZE * (land_mask.height - 1))))
            occupied.append((px, py))
    entries = []
    seen_titles = set()
    for cell in root.findall(".//mxCell"):
        node_id = cell.get("id", "")
        if not node_id.startswith(("migrated", "supplemental", "additional")):
            continue
        style = cell.get("style", "")
        # Dashed red markers are L4 templates/events, never permanent nodes.
        if "dashed=1" in style:
            continue
        geometry = cell.find("mxGeometry")
        raw_name = unescape(cell.get("value", "")).strip()
        if geometry is None or not raw_name:
            continue
        title = display_name(raw_name)
        if not title:
            continue
        # A blueprint may repeat a landmark in its historical / regional
        # annotation layers.  One named landmark equals one runtime site.
        if title in seen_titles:
            continue
        level = display_level(raw_name, node_id)
        x = (float(geometry.get("x", "0")) - LEFT) / CANVAS_SIZE * MAP_SIZE
        y = (float(geometry.get("y", "0")) - TOP) / CANVAS_SIZE * MAP_SIZE
        safe_id = re.sub(r"[^a-z0-9_]+", "_", f"zhaoming_{node_id}")
        x, y = snap_to_land((x, y), land_mask, components, island_components, occupied, any(word in title for word in ("岛", "礁")))
        entries.append((safe_id, title, x, y, level, node_type(title)))
        seen_titles.add(title)

    lines = [
        "extends RefCounted",
        "class_name ZhaomingNodeCatalog",
        "",
        "# Generated from the editable Draw.io blueprint. L4 event markers are excluded.",
        "const ENTRIES := [",
    ]
    for item in entries:
        node_id, name, x, y, level, kind = item
        lines.append('    {"id": "%s", "name": "%s", "x": %.1f, "y": %.1f, "level": %d, "type": %s},' % (node_id, name.replace('"', '\\"'), x, y, level, kind))
    lines += [
        "]",
        "",
        "static func populate(map_data: WorldNodeMapData) -> void:",
        "    if not map_data or map_data.has_meta(\"zhaoming_catalog_loaded\"):",
        "        return",
        "    # Preserve authored landmarks in the base resource and use this",
        "    # catalogue only to fill missing fixed L1-L3 world sites.",
        "    var known_ids: Dictionary = {}",
        "    var known_names: Dictionary = {}",
        "    for existing in map_data.nodes:",
        "        known_ids[existing.node_id] = true",
        "        known_names[existing.node_name] = true",
        "    for entry in ENTRIES:",
        "        if known_ids.has(entry.id) or known_names.has(entry.name):",
        "            continue",
        "        var node := WorldNodeData.new()",
        "        node.node_id = entry.id",
        "        node.node_name = entry.name",
        "        node.node_type = entry.type",
        "        node.display_level = entry.level as WorldNodeData.DisplayLevel",
        "        node.map_position = Vector2(entry.x, entry.y)",
        "        node.min_zoom = 0.0 if entry.level == WorldNodeData.DisplayLevel.WORLD else (1.12 if entry.level == WorldNodeData.DisplayLevel.REGION else 1.85)",
        "        node.marker_size = 72.0 if entry.level == WorldNodeData.DisplayLevel.WORLD else (42.0 if entry.level == WorldNodeData.DisplayLevel.REGION else 22.0)",
        "        node.terrain = \"待按地貌落位校正\"",
        "        node.description = \"昭明纪大世界固定节点；L4 探索事件不会在此常驻显示。\"",
        "        map_data.nodes.append(node)",
        "        known_ids[node.node_id] = true",
        "        known_names[node.node_name] = true",
        "    map_data.set_meta(\"zhaoming_catalog_loaded\", true)",
    ]
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Exported {len(entries)} L1-L3 runtime nodes to {OUT}")


if __name__ == "__main__":
    main()
