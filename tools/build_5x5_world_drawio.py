# -*- coding: utf-8 -*-
"""Generate the editable 5x5 Zhaoming world-map layout for draw.io."""
from pathlib import Path
from xml.sax.saxutils import escape
import base64
import json
import re
import xml.etree.ElementTree as ET
from typing import Tuple

from PIL import Image


OUT = Path("docs/plans/世界与探索/历史地图/昭明纪环海大世界地图底稿.drawio")
CELL = 300
LEFT = 50
TOP = 110
TERRAIN_MASTER = Path("textures/world_map/production_samples/zhaoming_continuous_terrain_master_v24_atlas_base.png")
PARTITION_MASTER = Path("textures/world_map/territory_partition/zhaoming_land_partition_id.png")
PARTITION_EDGES = Path("textures/world_map/territory_partition/zhaoming_land_partition_edges.png")
PARTITION_METADATA = Path("textures/world_map/territory_partition/zhaoming_land_partition.json")
DRAWIO_TERRAIN = Path("textures/world_map/territory_partition/zhaoming_drawio_terrain_with_boundaries.png")
_PARTITION_IMAGE = None
_PARTITION_COLOURS = None
_OCCUPIED_NODE_SITES = []

# Old 4x4 production cells map into their closest 5x5 geography.  A source
# cell may straddle states; the irregular state overlay is intentionally
# independent from this mapping.
OLD_TO_NEW = {
    0: (0, 0), 1: (1, 0), 2: (3, 0), 3: (4, 0),
    4: (1, 1), 5: (2, 1), 6: (3, 1), 7: (4, 1),
    8: (1, 3), 9: (2, 2), 10: (2, 3), 11: (3, 3),
    12: (0, 2), 13: (1, 4), 14: (2, 4), 15: (4, 3),
}

CELLS = [
    ("极北冰盖", "玄朔州北境 / 相山州北隅 · 浮冰裂海", "#dce9f2", ["烛阴一脉 L1", "寒渊秘境 L4"]),
    ("北冥雪原", "玄朔州腹地 / 相山州西缘 · 星坠天池", "#e5eef2", ["北极仙宫 L1", "雪月宫 L2", "北原陈家 L2"]),
    ("相山雪岭", "相山州腹地 / 玄朔州南麓 · 相山天池", "#dfe8e7", ["相山遗族 L1", "玄潮书院 L2", "九脉山庄 L2"]),
    ("北海群岛", "相山州东岸 / 沧溟州北屿 · 寒潮湾", "#d5e8ef", ["玄龟岛 L2", "听涛门 L2", "北海雾航 L3"]),
    ("东外海", "沧溟州外洋 / 北海远航带", "#cfe5ed", ["东海航图 L2", "蜃门回航 L4"]),
    ("西北戈壁", "关河州西北 / 玄朔州南缘 · 盐泽", "#eee1ce", ["天狼寨 L2", "黄沙门 L2"]),
    ("西陵北境", "关河州北境 / 玄朔州南麓 · 鬼哭岭", "#ecd9bd", ["冥灯门 L2", "折戟山庄 L2", "苦禅寺 L2"]),
    ("中州北原", "云阙州北原 / 关河州东缘 · 弱水河", "#e2ead7", ["太虚门 L1", "白鹿书院 L2", "大光道 L1"]),
    ("东陆北丘", "东华州北丘 / 云阙州东境 · 古道河谷", "#dfebdc", ["天工阁 L1", "公孙阵府 L2", "百炼谷 L2"]),
    ("东海近岸", "东华州东岸 / 沧溟州近海 · 明湾", "#d5eaf0", ["听潮埠 L2", "扶桑泊 L2"]),
    ("西陵西岸", "关河州西岸 / 云岫州北口 · 西海湾", "#e8dcc7", ["黑水寨 L2", "西海盐埠 L3"]),
    ("西陵荒域", "关河州腹地 / 云阙州西境 / 云岫州北麓", "#ebd5b5", ["白骨宗 L1", "黄泉谷 L2", "阴尸宗 L2"]),
    ("中州玉虚", "云阙州腹地 / 苍梧州北缘 · 青溪湖（小）", "#dcebd7", ["玉虚仙宗 L1", "大夏仙都 L1", "白玉京投影 L4", "青溪里 L3"]),
    ("东陆机巧", "东华州腹地 / 云阙州东境 / 归潮州北路", "#dbe8d4", ["千机城 L1", "紫阳门 L2", "金刚门 L2"]),
    ("东海航道", "沧溟州内航道 / 东华州外海", "#d0e7ef", ["蓬莱外岛 L2", "方丈孤屿 L2", "沉舟残舱 L4"]),
    ("剑南西麓", "云岫州西麓 / 关河州南口 · 剑河谷", "#e3e2dc", ["枕剑庐 L2", "寒江剑派 L2"]),
    ("剑南群山", "云岫州腹地 / 云阙州南界 / 苍梧州北岭", "#dddde2", ["铸剑城 L1", "七剑门 L2", "剑冢残境 L4"]),
    ("南荒万山", "苍梧州北部 / 云岫州南岭 / 归潮州西界", "#d9ead7", ["万妖谷 L1", "百草谷 L2", "五仙教 L2"]),
    ("南冥海疆", "归潮州陆海交错 / 苍梧州东南缘", "#d5e7e4", ["不夜港 L1", "沧海阁 L2", "南冥苏家 L2"]),
    ("南冥群岛", "归潮州东屿 / 沧溟州南航道", "#cce5e8", ["鲛纱阁 L2", "碧游宫 L2", "月潮鲸歌 L4"]),
    ("西南外海", "云岫州外海 / 归潮州西航道", "#d0e4ea", ["西南海图 L2", "归航古碑 L3"]),
    ("南荒南泽", "苍梧州腹地 / 云岫州南缘 · 大泽水网", "#d8ead9", ["药王谷 L2", "青木部 L2", "万蛊秘境 L4"]),
    ("南荒海口", "苍梧州海口 / 归潮州北湾 · 三江入海", "#d5e9dc", ["百花谷 L2", "云梦渡 L2"]),
    ("南冥外岛", "归潮州外岛 / 沧溟州南航线", "#cce5e6", ["紫府岛 L2", "天涯阁 L2"]),
    ("归墟外海", "归潮州远洋 / 沧溟州外环", "#c6dfe8", ["归墟潮眼 L4", "大光海道 L1"]),
]

TERRITORIES = [
    # Every inner segment below is shared verbatim by its two neighbours.
    # This is a planar land-state partition, not overlapping influence zones.
    ("玄朔州", [(0.05,0.42),(1.65,0.18),(2.15,0.45),(2.02,1.00),(2.45,1.45),(1.70,1.58),(0.80,1.47),(0.08,1.30)], "#7695b0"),
    ("相山州", [(2.15,0.45),(3.72,0.35),(4.05,0.85),(4.45,1.50),(3.55,1.65),(2.45,1.45),(2.02,1.00)], "#5c929b"),
    ("关河州", [(0.08,1.30),(0.80,1.47),(1.70,1.58),(1.55,2.10),(1.80,2.75),(1.65,3.45),(0.75,3.30),(0.10,3.02)], "#bc8748"),
    ("云阙州", [(1.70,1.58),(2.45,1.45),(3.55,1.65),(3.40,2.20),(3.70,2.75),(3.40,3.50),(2.45,3.32),(1.65,3.45),(1.80,2.75),(1.55,2.10)], "#73944d"),
    ("东华州", [(3.55,1.65),(4.45,1.50),(4.55,2.20),(4.35,2.80),(4.45,3.38),(3.40,3.50),(3.70,2.75),(3.40,2.20)], "#b68c43"),
    # 沧溟州只取东岸陆地与近岸岛屿；外海航线不属任何州。
    ("沧溟州", [(4.05,0.85),(4.36,0.72),(4.56,1.08),(4.45,1.50),(4.55,2.20),(4.35,2.80),(4.45,3.38),(4.16,3.15),(4.08,2.42),(3.92,1.62)], "#4d90a2"),
    ("云岫州", [(0.10,3.02),(0.75,3.30),(1.65,3.45),(1.80,4.10),(1.55,4.65),(1.65,4.82),(0.40,4.65),(0.12,4.12)], "#766e93"),
    ("苍梧州", [(1.65,3.45),(2.45,3.32),(3.40,3.50),(3.55,4.15),(3.30,4.60),(3.18,4.82),(1.65,4.82),(1.55,4.65),(1.80,4.10)], "#5e9559"),
    # 归潮州只取南明半岛及其近岸岛链；归墟外海、远航海路保持无主。
    ("归潮州", [(3.40,3.50),(4.45,3.38),(4.30,3.72),(4.48,4.05),(4.26,4.28),(4.42,4.55),(4.10,4.70),(3.72,4.46),(3.48,4.70),(3.18,4.82),(3.30,4.60),(3.55,4.15)], "#438c8b"),
]

# Marine zones are gameplay waters, never 州.  They host routes, offshore
# islands and L4 events, but must not be used by land-territory hit-testing.
MARINE_ZONES = [
    ("北冥外海", [(3.75,0.12),(5.00,0.05),(5.00,1.15),(4.72,1.10),(4.38,0.55)], "#6f9eaf"),
    ("东海航道", [(4.58,1.28),(5.00,1.10),(5.00,3.32),(4.66,3.10),(4.52,2.18)], "#4f9eb4"),
    ("南溟近海", [(3.65,3.95),(4.98,3.55),(5.00,4.92),(3.45,5.00),(3.52,4.58)], "#559bac"),
    ("归墟外环", [(3.65,4.45),(4.88,4.45),(5.00,5.00),(3.78,5.00)], "#3e7d97"),
]

# Administrative borders are the shared inner seams only.  Do not draw the
# outer perimeter of a state: that would close across sea and falsely imply
# that the water belongs to the state.  Coastlines come from the terrain art.
SHARED_BORDERS = [
    [(2.05,0.00),(2.10,0.24),(2.15,0.45),(2.06,0.72),(2.02,1.00),(2.26,1.22),(2.45,1.45)],  # 玄朔 / 相山、云阙
    [(0.00,1.55),(0.38,1.49),(0.80,1.47),(1.25,1.54),(1.70,1.58)],  # 玄朔 / 关河
    [(1.70,1.58),(2.45,1.45)],                          # 玄朔 / 云阙
    [(2.45,1.45),(2.78,1.53),(3.16,1.47),(3.55,1.65)],  # 相山 / 云阙
    [(4.15,0.00),(4.05,0.85),(4.45,1.50)],              # 相山 / 沧溟
    [(3.55,1.65),(3.83,1.57),(4.12,1.63),(4.45,1.50)],  # 相山 / 东华
    [(1.70,1.58),(1.61,1.83),(1.55,2.10),(1.70,2.42),(1.80,2.75),(1.70,3.08),(1.65,3.45)],  # 关河 / 云阙
    [(3.55,1.65),(3.43,1.93),(3.40,2.20),(3.55,2.48),(3.70,2.75),(3.48,3.08),(3.40,3.50)],  # 云阙 / 东华
    [(4.45,1.50),(4.55,2.20),(4.35,2.80),(4.45,3.38)],  # 东华 / 沧溟
    [(0.00,3.40),(0.75,3.30),(1.65,3.45)],              # 关河 / 云岫
    [(1.65,3.45),(1.95,3.38),(2.45,3.32),(2.86,3.42),(3.40,3.50)],  # 云阙 / 苍梧
    [(3.40,3.50),(4.45,3.38)],                          # 东华 / 归潮
    [(4.45,3.38),(5.00,3.40)],                          # 沧溟 / 归潮（仅近岸陆段）
    [(1.65,3.45),(1.80,4.10),(1.55,4.65),(1.65,5.00)],  # 云岫 / 苍梧
    [(3.40,3.50),(3.55,4.15),(3.30,4.60),(3.40,5.00)],  # 苍梧 / 归潮
]

# Draw only the landward coastline administrative boundary.  This does not
# claim the sea for any state; it explicitly marks where a state ends and an
# unowned marine gameplay zone begins.
COAST_BOUNDARIES = [
    [(0.09,0.42),(0.52,0.33),(0.98,0.23),(1.42,0.20),(1.69,0.27)],
    [(2.21,0.45),(2.64,0.39),(3.13,0.36),(3.58,0.42),(3.77,0.80)],
    [(3.92,0.82),(4.04,0.98),(4.10,1.22),(4.02,1.43)],
    [(4.02,1.43),(4.11,1.68),(4.04,1.98),(4.14,2.28),(4.00,2.58),(4.06,2.88),(4.00,3.15)],
    [(0.08,1.30),(0.03,1.72),(0.08,2.14),(0.03,2.60),(0.10,3.02)],
    [(0.10,3.02),(0.06,3.42),(0.10,3.78),(0.12,4.12),(0.38,4.52),(0.40,4.65)],
    [(0.40,4.65),(0.78,4.74),(1.18,4.80),(1.65,4.82)],
    [(1.65,4.82),(2.08,4.88),(2.56,4.83),(3.18,4.82)],
    [(3.18,4.82),(3.48,4.70),(3.72,4.46),(4.10,4.70),(4.42,4.55)],
]

# The continental political cluster deliberately occupies the upper-left
# portion of the world.  The right and lower margins remain continuous sea
# for the 东海、南溟、归墟 gameplay layers.  Apply one transform to states and
# their shared seams so they cannot drift apart.
LAND_CLUSTER_SCALE = 0.88
LAND_CLUSTER_OFFSET = (0.05, 0.05)


def _shift_land_cluster(points):
    return [
        (x * LAND_CLUSTER_SCALE + LAND_CLUSTER_OFFSET[0], y * LAND_CLUSTER_SCALE + LAND_CLUSTER_OFFSET[1])
        for x, y in points
    ]


TERRITORIES = [(name, _shift_land_cluster(points), color) for name, points, color in TERRITORIES]
SHARED_BORDERS = [_shift_land_cluster(points) for points in SHARED_BORDERS]
COAST_BOUNDARIES = [_shift_land_cluster(points) for points in COAST_BOUNDARIES]


def _point_in_polygon(point, polygon):
    """Ray casting; boundary points count as contained for node validation."""
    x, y = point
    inside = False
    for index, (x1, y1) in enumerate(polygon):
        x2, y2 = polygon[(index + 1) % len(polygon)]
        if ((y1 > y) != (y2 > y)) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            inside = not inside
    return inside


def _zone_for_drawio_point(x, y):
    """Return exact state ownership from the authoritative pixel partition."""
    if PARTITION_MASTER.exists() and PARTITION_METADATA.exists():
        global _PARTITION_IMAGE, _PARTITION_COLOURS
        if _PARTITION_IMAGE is None:
            _PARTITION_IMAGE = Image.open(PARTITION_MASTER).convert("RGB")
            metadata = json.loads(PARTITION_METADATA.read_text(encoding="utf-8"))
            _PARTITION_COLOURS = {
                tuple(int(zone["color"][offset:offset + 2], 16) for offset in (1, 3, 5)): zone["name"]
                for zone in metadata["zones"]
            }
        image = _PARTITION_IMAGE
        logical_x = (x - LEFT) / (CELL * 5)
        logical_y = (y - TOP) / (CELL * 5)
        pixel_x = max(0, min(image.width - 1, round(logical_x * (image.width - 1))))
        pixel_y = max(0, min(image.height - 1, round(logical_y * (image.height - 1))))
        colour = image.getpixel((pixel_x, pixel_y))
        if colour in _PARTITION_COLOURS:
            return _PARTITION_COLOURS[colour], (logical_x * 5, logical_y * 5)
        # Sea is intentionally outside every state.  Give design nodes a
        # gameplay-water membership instead of forcing them inland.
        if logical_y > 0.72:
            return "南溟近海", (logical_x * 5, logical_y * 5)
        if logical_x > 0.70:
            return "东海外航道", (logical_x * 5, logical_y * 5)
        return "北冥外海", (logical_x * 5, logical_y * 5)
    logical = ((x - LEFT) / CELL, (y - TOP) / CELL)
    for name, polygon, _ in TERRITORIES:
        if _point_in_polygon(logical, polygon):
            return name, logical
    for name, polygon, _ in MARINE_ZONES:
        if _point_in_polygon(logical, polygon):
            return name, logical
    return "", logical


def constrain_node_to_zone(x, y):
    """Return a point guaranteed to be in a named state or marine zone."""
    zone, _ = _zone_for_drawio_point(x, y)
    if zone:
        return x, y, zone
    # With the authoritative raster online, a land point only needs a tiny
    # deterministic nudge to find the nearest valid state pixel.  This keeps
    # a misplaced node geographically local instead of teleporting it to a
    # state centroid.
    if PARTITION_MASTER.exists():
        for radius in range(1, 220, 3):
            for dx, dy in ((radius, 0), (-radius, 0), (0, radius), (0, -radius), (radius, radius), (-radius, radius)):
                candidate_x, candidate_y = x + dx, y + dy
                zone, _ = _zone_for_drawio_point(candidate_x, candidate_y)
                if zone not in ("北冥外海", "东海外航道", "南溟近海"):
                    return candidate_x, candidate_y, zone
        return x, y, _zone_for_drawio_point(x, y)[0]
    zones = TERRITORIES + MARINE_ZONES
    logical = ((x - LEFT) / CELL, (y - TOP) / CELL)
    # A node outside the partition is invalid input. Put it at the centre of
    # the nearest legal zone rather than silently leaving an unowned marker.
    name, polygon, _ = min(
        zones,
        key=lambda zone: (sum(px for px, _ in zone[1]) / len(zone[1]) - logical[0]) ** 2
        + (sum(py for _, py in zone[1]) / len(zone[1]) - logical[1]) ** 2,
    )
    cx = sum(px for px, _ in polygon) / len(polygon)
    cy = sum(py for _, py in polygon) / len(polygon)
    return LEFT + cx * CELL, TOP + cy * CELL, name


def place_node_without_overlap(x, y, zone, serial):
    """Keep small planning markers readable without changing their region.

    The prior cell-local offsets were applied separately to three node pools,
    so different pools could silently land on the same point.  Search a broad
    deterministic spiral in the node's own state / sea zone instead.
    """
    minimum_x, maximum_x = LEFT + 10, LEFT + CELL * 5 - 10
    minimum_y, maximum_y = TOP + 10, TOP + CELL * 5 - 10
    for attempt in range(260):
        if attempt == 0:
            candidate_x, candidate_y = x, y
        else:
            ring = 14 + (attempt // 12) * 15
            angle = (serial * 2.399963 + attempt * 0.897598) % 6.283185
            candidate_x = x + ring * __import__("math").cos(angle)
            candidate_y = y + ring * __import__("math").sin(angle)
        if not (minimum_x <= candidate_x <= maximum_x and minimum_y <= candidate_y <= maximum_y):
            continue
        candidate_zone, _ = _zone_for_drawio_point(candidate_x, candidate_y)
        if candidate_zone != zone:
            continue
        if all((candidate_x - used_x) ** 2 / 30 ** 2 + (candidate_y - used_y) ** 2 / 16 ** 2 >= 1 for used_x, used_y in _OCCUPIED_NODE_SITES):
            _OCCUPIED_NODE_SITES.append((candidate_x, candidate_y))
            return candidate_x, candidate_y
    # A compact map should never reach this fallback; retain a valid marker
    # rather than discarding design content if a deliberately dense region is
    # later added.
    _OCCUPIED_NODE_SITES.append((x, y))
    return x, y

# Cells that gained no old nodes after the 4×4 → 5×5 remap.  These are
# concrete, fixed L2/L3 locations, not generic random-event placeholders.
SUPPLEMENTAL_NODES = {
    (0, 2): ["北斗台 L2", "天池别院 L2", "望雪楼 L3", "寒河渡 L3", "冰蚕谷 L3", "北辰碑林 L3", "雪鹤岭 L3"],
    (1, 0): ["玉门观 L2", "落日台 L3", "大漠寺 L2", "风蚀驿 L3", "赤沙药圃 L3", "盐泽渡 L3", "胡杨林 L3"],
    (2, 1): ["白骨宗 L1", "黄泉谷 L2", "阴尸宗 L2", "黑风集 L3", "埋骨盐湖 L3", "断碑烽燧 L3", "流沙古渠 L3"],
    (2, 3): ["千机城 L1", "紫阳门 L2", "金刚门 L2", "火种谷 L3", "千机河渡 L3", "铜炉镇 L3", "偃师遗窟 L3"],
    (2, 4): ["蓬莱外岛 L2", "方丈孤屿 L2", "瀛洲旧庭 L2", "明湾港 L3", "断帆礁 L3", "珊瑚浅滩 L3", "沉舟残舱 L4"],
    (3, 0): ["寒江剑派 L2", "枕剑庐 L2", "剑河驿 L3", "石门关 L3", "青锋坪 L3", "听剑谷 L3", "西麓药谷 L3"],
    (4, 0): ["海风观 L2", "归航古碑 L3", "风暴礁 L3", "西南泊地 L3", "鲛人旧渡 L3", "沉香孤岛 L3", "雾隐航标 L3"],
    (4, 3): ["紫府岛 L2", "天涯阁 L2", "鲸落湾 L3", "碧波宫 L3", "珊瑚谷 L3", "云帆泊地 L3", "潮生门 L3"],
    (4, 4): ["归墟潮眼 L4", "大光海道 L1", "沧浪台 L2", "玄冥礁 L3", "海市残宫 L3", "外环航标 L3", "蜃雾海门 L4"],
}

# A second, deliberately denser layer of fixed local factions and named
# landmarks.  These keep the world from reading as a handful of capitals.
# They are L2/L3 only: L4 discoveries belong to the runtime event pool and
# must not be baked into the always-visible world layer.
ADDITIONAL_NODES = {
    (0, 0): ["冰极殿 L2", "寒灯台 L3", "玄冰药圃 L3"],
    (0, 1): ["玄枵院 L2", "风雪关 L2", "冰原巡营 L3"],
    (0, 2): ["烛阴祠 L2", "寒潮观 L2", "星坠驿 L3"],
    (0, 3): ["北斗渔门 L2", "玄潮书院 L2", "听涛埠 L3"],
    (0, 4): ["寒潮关 L2", "观海楼 L2", "雁回礁 L3"],
    (1, 0): ["流云观 L2", "铁砂门 L2", "赤沙古城 L3"],
    (1, 1): ["西陵戍所 L2", "鬼哭岭哨堡 L2", "绝灵渊渡 L3"],
    (1, 2): ["大夏灵部行台 L2", "长风镖局 L2", "白鹿县坊 L3"],
    (1, 3): ["公孙氏阵府 L2", "丹鼎会别院 L2", "机关驿 L3"],
    (1, 4): ["不夜港 L2", "蓬莱商馆 L2", "天机阁书坊 L2", "望潮台 L3"],
    (2, 0): ["黄沙门 L2", "玄风谷 L2", "西陵古驿 L3"],
    (2, 1): ["阴煞宗 L2", "血河门 L2", "鬼哭渡 L3"],
    (2, 2): ["青云观 L2", "澄心碑林 L2", "姜氏行宫 L2"],
    (2, 3): ["百炼谷 L2", "赤炉山 L2", "火种原 L2"],
    (2, 4): ["沧海阁 L2", "瀛洲剑派 L2", "瀛洲月港 L3"],
    (3, 0): ["落霞山庄 L2", "青锋门 L2", "云栈关 L3"],
    (3, 1): ["七剑门 L2", "剑池山庄 L2", "云阶渡 L3"],
    (3, 2): ["万兽部 L2", "建木祠 L2", "边境妖市 L3"],
    (3, 3): ["青木部 L2", "药王谷 L2", "图腾柱林 L3"],
    (3, 4): ["鲛纱阁 L2", "扶桑岛 L2", "扶桑晓屿 L3"],
    (4, 0): ["沉香门 L2", "西海散修坞 L2", "回风湾 L3"],
    (4, 1): ["五仙教 L2", "百花谷 L2", "南泽药庐 L3"],
    (4, 2): ["万蛊门 L2", "金雕部 L2", "三江渡 L3"],
    (4, 3): ["南冥苏家 L2", "碧游宫 L2", "鲲鹏遗祠 L3"],
    (4, 4): ["方丈仙岛 L2", "归墟观星台 L2", "大光海道碑 L3"],
}

# 地图只承载玩家可抵达、可定位的真实地点与地理区域。坊、市、商馆等
# 是所属城镇/宗门内部的功能入口，保留在玩法数据里，但不应再画成一个
# 与城镇并列的世界地图节点。
FUNCTIONAL_FACILITY_NODE_NAMES = {
    "白鹿县坊",
    "蓬莱商馆",
    "天机阁书坊",
    "边境妖市",
}

# 路线不是“地点”。在大世界图中仅保留为无底色的线性标注；实际入口
# 应由渡口、关隘、港口等实体地点承载。
ROUTE_NODE_NAMES = {
    "大光道",
    "大光海道",
    "大光海道碑",
}

# 这些是具备明确地理范围的野外区域。它们使用半透明区域标识，避免和
# 城镇、宗门、洞府被误读为同一种可点击建筑点。
WILDERNESS_REGION_NODE_NAMES = {
    "冰蚕谷", "胡杨林", "黄泉谷", "埋骨盐湖", "火种谷", "偃师遗窟",
    "西麓药谷", "风暴礁", "鲸落湾", "珊瑚谷", "玄风谷", "绝灵渊渡",
    "鬼哭岭哨堡", "澄心碑林", "百炼谷", "赤炉山", "火种原", "建木祠",
    "百花谷", "万毒泽", "十万大山", "镜月泽", "东林地", "灵草谷",
}


def node_name(value: str) -> str:
    return value.replace("&#10;", " ").split(" L")[0].strip()


def node_level(value: str) -> str:
    return "L4" if "L4" in value else "L3" if "L3" in value else "L2" if "L2" in value else "L1"


def node_presentation(name: str, level: str) -> str:
    if level == "L4":
        return "event"
    if name in ROUTE_NODE_NAMES:
        return "route"
    if name in WILDERNESS_REGION_NODE_NAMES:
        return "region"
    return "place"


def node_style(presentation: str, level: str) -> str:
    if presentation == "event":
        return "shape=rhombus;whiteSpace=wrap;html=1;fillColor=#f4deda;fillOpacity=45;strokeColor=#a36862;fontSize=5;dashed=1;"
    if presentation == "route":
        return "text;html=1;fontSize=5;fontStyle=2;fontColor=#557d9c;align=center;verticalAlign=middle;"
    if presentation == "region":
        return "shape=cloud;whiteSpace=wrap;html=1;fillColor=#b9c99c;fillOpacity=32;strokeColor=#70804f;fontSize=5;dashed=1;"
    if level == "L1":
        return "shape=rhombus;whiteSpace=wrap;html=1;fillColor=#9ab9b1;strokeColor=#4d766c;fontSize=5;"
    if level == "L2":
        return "shape=hexagon;whiteSpace=wrap;html=1;fillColor=#f3ead4;strokeColor=#81775f;fontSize=5;"
    return "shape=ellipse;whiteSpace=wrap;html=1;fillColor=#f3ead4;strokeColor=#81775f;fontSize=5;"


def node_geometry(presentation: str) -> Tuple[int, int]:
    if presentation == "region":
        return 32, 12
    if presentation == "route":
        return 34, 8
    return 21, 8


def cell_xml(index: int, name: str, terrain: str, color: str, nodes) -> str:
    row, col = divmod(index, 5)
    x, y = LEFT + col * CELL, TOP + row * CELL
    # The 5×5 cells are production/loading references only.  Keep them in
    # the editable file but hide their fill, stroke and label: they must not
    # read as geographic borders over the continuous terrain.
    result = [f'<mxCell id="cell{index}" value="" style="rounded=0;whiteSpace=wrap;html=1;fillOpacity=0;strokeOpacity=0;fontSize=0;" vertex="1" parent="1"><mxGeometry x="{x}" y="{y}" width="{CELL}" height="{CELL}" as="geometry"/></mxCell>']
    for n, node in enumerate(nodes):
        nx = x + 18 + (n % 2) * 138
        ny = y + 78 + (n // 2) * 45
        style = "shape=ellipse;whiteSpace=wrap;html=1;fillColor=#f3ead4;strokeColor=#81775f;fontSize=9;" if "L4" not in node else "shape=ellipse;whiteSpace=wrap;html=1;fillColor=#f4deda;strokeColor=#a36862;fontSize=9;dashed=1;"
        result.append(f'<mxCell id="node{index}_{n}" value="{escape(node)}" style="{style}" vertex="1" parent="1"><mxGeometry x="{nx}" y="{ny}" width="122" height="28" as="geometry"/></mxCell>')
    return "".join(result)


def terrain_master_xml() -> str:
    """Embed terrain with boundaries precomposed for reliable Draw.io display."""
    if not TERRAIN_MASTER.exists():
        raise RuntimeError(f"Terrain master not found: {TERRAIN_MASTER}")
    if not PARTITION_EDGES.exists():
        raise RuntimeError("Build zhaoming_land_partition before generating Draw.io")
    terrain = Image.open(TERRAIN_MASTER).convert("RGBA")
    edges = Image.open(PARTITION_EDGES).convert("RGBA")
    if edges.size != terrain.size:
        edges = edges.resize(terrain.size, Image.Resampling.NEAREST)
    Image.alpha_composite(terrain, edges).convert("RGB").save(DRAWIO_TERRAIN, optimize=True)
    encoded = base64.b64encode(DRAWIO_TERRAIN.read_bytes()).decode("ascii")
    style = (
        "shape=image;imageAspect=0;aspect=fixed;"
        f"image=data:image/png;base64,{encoded};"
        "movable=0;resizable=0;editable=0;deletable=0;"
    )
    return f'<mxCell id="terrain_master" value="" style="{style}" vertex="1" parent="1"><mxGeometry x="{LEFT}" y="{TOP}" width="{CELL * 5}" height="{CELL * 5}" as="geometry"/></mxCell>'


def partition_overlay_xml() -> str:
    """Embed exact shared state and coast boundaries from the common source."""
    if not PARTITION_EDGES.exists():
        raise RuntimeError("Build zhaoming_land_partition before generating Draw.io")
    encoded = base64.b64encode(PARTITION_EDGES.read_bytes()).decode("ascii")
    style = (
        "shape=image;imageAspect=0;aspect=fixed;"
        f"image=data:image/png;base64,{encoded};"
        "movable=0;resizable=0;editable=0;deletable=0;"
    )
    return f'<mxCell id="authoritative_partition_edges" value="" style="{style}" vertex="1" parent="1"><mxGeometry x="{LEFT}" y="{TOP}" width="{CELL * 5}" height="{CELL * 5}" as="geometry"/></mxCell>'


def partition_labels_xml() -> str:
    if not PARTITION_METADATA.exists():
        raise RuntimeError("Build zhaoming_land_partition before generating Draw.io")
    metadata = json.loads(PARTITION_METADATA.read_text(encoding="utf-8"))
    width, height = metadata["size"]
    labels = []
    for index, zone in enumerate(metadata["zones"]):
        centre_x, centre_y = zone["centre"]
        x = LEFT + centre_x / width * CELL * 5 - 70
        y = TOP + centre_y / height * CELL * 5 + 16
        labels.append(
            f'<mxCell id="territorylabel{index}" value="{escape(zone["name"])}" '
            f'style="text;html=1;fontSize=14;fontStyle=1;fontColor={zone["color"]};align=center;verticalAlign=middle;" '
            f'vertex="1" parent="1"><mxGeometry x="{x:.0f}" y="{y:.0f}" width="140" height="24" as="geometry"/></mxCell>'
        )
    return "".join(labels)


def territory_xml(index: int, name: str, points, color: str) -> str:
    mapped = [(LEFT + x * CELL, TOP + y * CELL) for x, y in points]
    source = mapped[0]
    target = mapped[0]
    mids = "".join(f'<mxPoint x="{x:.0f}" y="{y:.0f}"/>' for x, y in mapped[1:])
    return f'<mxCell id="territory{index}" value="" style="edgeStyle=none;html=1;rounded=0;strokeColor={color};strokeWidth=3;dashed=1;startArrow=none;endArrow=none;opacity=70;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="{source[0]:.0f}" y="{source[1]:.0f}" as="sourcePoint"/><mxPoint x="{target[0]:.0f}" y="{target[1]:.0f}" as="targetPoint"/><Array as="points">{mids}</Array></mxGeometry></mxCell>'


def shared_border_xml(index: int, points) -> str:
    mapped = [(LEFT + x * CELL, TOP + y * CELL) for x, y in points]
    source, target = mapped[0], mapped[-1]
    mids = "".join(f'<mxPoint x="{x:.0f}" y="{y:.0f}"/>' for x, y in mapped[1:-1])
    return f'<mxCell id="sharedborder{index}" value="" style="edgeStyle=none;html=1;rounded=0;strokeColor=#c49a5b;strokeWidth=2;dashed=1;startArrow=none;endArrow=none;opacity=72;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="{source[0]:.0f}" y="{source[1]:.0f}" as="sourcePoint"/><mxPoint x="{target[0]:.0f}" y="{target[1]:.0f}" as="targetPoint"/><Array as="points">{mids}</Array></mxGeometry></mxCell>'


def coast_boundary_xml(index: int, points) -> str:
    mapped = [(LEFT + x * CELL, TOP + y * CELL) for x, y in points]
    source, target = mapped[0], mapped[-1]
    mids = "".join(f'<mxPoint x="{x:.0f}" y="{y:.0f}"/>' for x, y in mapped[1:-1])
    return f'<mxCell id="coastborder{index}" value="" style="edgeStyle=none;html=1;rounded=0;strokeColor=#5d91a7;strokeWidth=2;dashed=1;dashPattern=7 4;startArrow=none;endArrow=none;opacity=72;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="{source[0]:.0f}" y="{source[1]:.0f}" as="sourcePoint"/><mxPoint x="{target[0]:.0f}" y="{target[1]:.0f}" as="targetPoint"/><Array as="points">{mids}</Array></mxGeometry></mxCell>'


def marine_zone_xml(index: int, name: str, points, color: str) -> str:
    mapped = [(LEFT + x * CELL, TOP + y * CELL) for x, y in points]
    source = mapped[0]
    mids = "".join(f'<mxPoint x="{x:.0f}" y="{y:.0f}"/>' for x, y in mapped[1:])
    return f'<mxCell id="marine{index}" value="" style="edgeStyle=none;html=1;rounded=0;strokeColor={color};strokeWidth=2;dashed=1;dashPattern=2 6;startArrow=none;endArrow=none;opacity=58;" edge="1" parent="1"><mxGeometry relative="1" as="geometry"><mxPoint x="{source[0]:.0f}" y="{source[1]:.0f}" as="sourcePoint"/><mxPoint x="{source[0]:.0f}" y="{source[1]:.0f}" as="targetPoint"/><Array as="points">{mids}</Array></mxGeometry></mxCell>'


def migrated_nodes_xml() -> str:
    """Carry every old node forward at the same relative location in its new cell."""
    # This is the pre-5×5 nine-state blueprint; the generated current file
    # must never become its own input on a subsequent rebuild.
    candidates = [Path("docs/plans/世界与探索/历史地图/世界地图九域功能底稿.drawio")]
    if not candidates:
        raise RuntimeError("旧九域功能底稿未找到")
    if not candidates[0].exists():
        raise RuntimeError("旧九域功能底稿未找到")
    root = ET.parse(str(candidates[0])).getroot()
    result = []
    serial = 0
    for cell in root.findall(".//mxCell"):
        match = re.fullmatch(r"node(\d+)_(\d+)", cell.get("id", ""))
        geometry = cell.find("mxGeometry")
        value = cell.get("value", "")
        node_id = cell.get("id", "")
        if geometry is None or not value:
            continue
        if match:
            old_index = int(match.group(1))
        elif node_id.startswith(("new", "expand_", "epoch_")) and "L" in value:
            # Later additions in the old blueprint were placed freely instead
            # of using node<cell>_<index>.  Infer their source production cell.
            raw_x = float(geometry.get("x", "0"))
            raw_y = float(geometry.get("y", "0"))
            old_col = max(0, min(3, int((raw_x - 35) / 527)))
            old_row = max(0, min(3, int((raw_y - 280) / 527)))
            old_index = old_row * 4 + old_col
        else:
            continue
        if old_index not in OLD_TO_NEW:
            continue
        value = value.replace("青溪里", "青溪里").replace("青溪里坊正", "青溪里坊正")
        old_col, old_row = old_index % 4, old_index // 4
        old_x = float(geometry.get("x", "0")) - (35 + old_col * 527)
        old_y = float(geometry.get("y", "0")) - (280 + old_row * 527)
        new_col, new_row = OLD_TO_NEW[old_index]
        # Retain source ordering but disperse sites across each new production
        # cell.  The former centre-weighted remap made every state look like a
        # single crowded capital.  Fine terrain placement is adjusted later.
        spread_x = ((serial * 73 + old_index * 41) % 244) + 26
        spread_y = ((serial * 47 + old_index * 29) % 218) + 50
        x = LEFT + new_col * CELL + spread_x
        y = TOP + new_row * CELL + spread_y
        x, y, zone = constrain_node_to_zone(x, y)
        x, y = place_node_without_overlap(x, y, zone, serial)
        name = node_name(value)
        if name in FUNCTIONAL_FACILITY_NODE_NAMES:
            continue
        level = node_level(value)
        presentation = node_presentation(name, level)
        style = node_style(presentation, level)
        width, height = node_geometry(presentation)
        # One-line boxes are intentionally tiny; level remains in the tooltip
        # text inside draw.io rather than consuming map space.
        label = escape(name)
        result.append(f'<mxCell id="migrated{serial}" value="{label}" style="{style}" data-zone="{escape(zone)}" vertex="1" parent="1"><mxGeometry x="{x:.0f}" y="{y:.0f}" width="{width}" height="{height}" as="geometry"/></mxCell>')
        serial += 1
    return "".join(result)


def supplemental_nodes_xml() -> str:
    result = []
    serial = 0
    offsets = [(22, 96), (114, 82), (206, 104), (38, 148), (132, 140), (222, 154), (92, 202)]
    for (row, col), nodes in SUPPLEMENTAL_NODES.items():
        for index, value in enumerate(nodes):
            x = LEFT + col * CELL + offsets[index][0]
            y = TOP + row * CELL + offsets[index][1]
            x, y, zone = constrain_node_to_zone(x, y)
            x, y = place_node_without_overlap(x, y, zone, serial + 1000)
            name = node_name(value)
            if name in FUNCTIONAL_FACILITY_NODE_NAMES:
                continue
            level = node_level(value)
            presentation = node_presentation(name, level)
            style = node_style(presentation, level)
            width, height = node_geometry(presentation)
            label = escape(name)
            result.append(f'<mxCell id="supplemental{serial}" value="{label}" style="{style}" data-zone="{escape(zone)}" vertex="1" parent="1"><mxGeometry x="{x:.0f}" y="{y:.0f}" width="{width}" height="{height}" as="geometry"/></mxCell>')
            serial += 1
    return "".join(result)


def additional_nodes_xml() -> str:
    """Place minor fixed factions in the lower half of each production cell."""
    result = []
    serial = 0
    offsets = [(26, 232), (137, 248), (222, 222), (82, 274)]
    for (row, col), nodes in ADDITIONAL_NODES.items():
        for index, value in enumerate(nodes):
            x = LEFT + col * CELL + offsets[index][0]
            y = TOP + row * CELL + offsets[index][1]
            x, y, zone = constrain_node_to_zone(x, y)
            x, y = place_node_without_overlap(x, y, zone, serial + 2000)
            name = node_name(value)
            if name in FUNCTIONAL_FACILITY_NODE_NAMES:
                continue
            level = node_level(value)
            presentation = node_presentation(name, level)
            style = node_style(presentation, level)
            width, height = node_geometry(presentation)
            label = escape(name)
            result.append(f'<mxCell id="additional{serial}" value="{label}" style="{style}" data-zone="{escape(zone)}" vertex="1" parent="1"><mxGeometry x="{x:.0f}" y="{y:.0f}" width="{width}" height="{height}" as="geometry"/></mxCell>')
            serial += 1
    return "".join(result)


def main() -> None:
    # The old blueprint supplies the detailed node layer.  The cells are only
    # invisible production references beneath the continuous terrain now.
    _OCCUPIED_NODE_SITES.clear()
    # Draw.io can mishandle a wholly transparent image as an invisible layer.
    # Bake the state/coast pixels into the terrain presentation instead.
    terrain_master = terrain_master_xml()
    cells = terrain_master + "".join(cell_xml(i, value[0], value[1], value[2], []) for i, value in enumerate(CELLS))
    migrated_nodes = migrated_nodes_xml()
    supplemental_nodes = supplemental_nodes_xml()
    additional_nodes = additional_nodes_xml()
    # Territory labels are intentionally a transparent overlay.  Runtime
    # state boundaries will be supplied by their ID masks.
    # Boundary and coast geometry comes from one land-only partition raster,
    # shared with runtime hit masks.  Never re-draw a separate hand polygon.
    territory_edges = ""
    coast_edges = ""
    # Marine areas remain gameplay metadata only.  Do not draw the old broad
    # dotted polylines here: they read as ugly long-distance node links.
    marine_edges = ""
    territory_labels = partition_labels_xml()
    marine_labels = "".join(
        f'<mxCell id="marinelabel{i}" value="{escape(name)}" style="text;html=1;fontSize=12;fontStyle=1;fontColor={color};align=center;verticalAlign=middle;" vertex="1" parent="1"><mxGeometry x="{LEFT + (sum(x for x, _ in points) / len(points)) * CELL - 70:.0f}" y="{TOP + (sum(y for _, y in points) / len(points)) * CELL:.0f}" width="140" height="22" as="geometry"/></mxCell>'
        for i, (name, points, color) in enumerate(MARINE_ZONES)
    )
    content = f'''<mxfile host="app.diagrams.net" modified="2026-08-25T00:00:00.000Z" agent="Codex" version="26.0.16"><diagram id="zhaoming-5x5" name="昭明纪环海大世界"><mxGraphModel dx="1900" dy="1900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1700" pageHeight="1800" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/><mxCell id="title" value="昭明纪 5×5 环海大世界地点表现底稿" style="text;html=1;fontSize=26;fontStyle=1;align=left;verticalAlign=middle;" vertex="1" parent="1"><mxGeometry x="50" y="20" width="900" height="36" as="geometry"/></mxCell><mxCell id="note" value="25格仅为美术／加载格。地图只标可定位的实体地点与野外区域：坊市、客栈、委托等归入所属地点内部；道路与航路以线性标注呈现；临时异象不占永久节点。金色为州际共边，蓝色为州域止于海岸。" style="text;html=1;fontSize=13;fontColor=#555555;whiteSpace=wrap;" vertex="1" parent="1"><mxGeometry x="50" y="60" width="1500" height="35" as="geometry"/></mxCell>{cells}{territory_edges}{coast_edges}{marine_edges}{territory_labels}{marine_labels}{migrated_nodes}{supplemental_nodes}{additional_nodes}<mxCell id="legend" value="边界：金色＝州与州共边；蓝色＝州域陆地止于海岸。节点：菱形＝L1 州级势力／主城；六边形＝L2 宗门、家族、城镇；椭圆＝L3 渡口、村落、洞府；半透明云形＝野外区域；蓝色斜体＝路线／航道；红色虚线菱形＝L4 临时秘境／航行异象。坊、市、商馆等功能不单独落点。" style="text;html=1;fontSize=13;fontColor=#555555;whiteSpace=wrap;" vertex="1" parent="1"><mxGeometry x="50" y="1635" width="1500" height="32" as="geometry"/></mxCell></root></mxGraphModel></diagram></mxfile>'''
    OUT.write_text(content, encoding="utf-8")


if __name__ == "__main__":
    main()
