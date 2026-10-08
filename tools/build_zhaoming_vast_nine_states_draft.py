"""Build the broad, import-friendly nine-state layout and aligned art layers.

The SVG is the editable planning source.  The review PNG keeps the political
color layer visible, while the runtime PNG intentionally keeps only terrain,
water and landforms; the runtime resource supplies the interactive territory
layer separately.  Travel routes remain data-only.
"""

from __future__ import annotations

import html
import json
import math
import random
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Tuple

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
PRODUCTION = ROOT / "textures" / "world_map" / "production_samples"
OUT_PNG = DOCS / "预览_昭明界九州广袤布局_8000.png"
OUT_SVG = PRODUCTION / "zhaoming_vast_nine_states_layout_master_8000.svg"
OUT_MANIFEST = PRODUCTION / "zhaoming_vast_nine_states_layout_master_8000.json"
OUT_RUNTIME_PNG = ROOT / "textures" / "world_map" / "zhaoming_vast_nine_states_runtime_8000_native.png"
OUT_MASK_DIR = ROOT / "textures" / "world_map" / "territory_masks_vast_nine_states_5x5_8000"

WIDTH, HEIGHT = 2400, 1800
LOGICAL_MAP_SIZE = 8000
RUNTIME_ART_SCALE = 3
RUNTIME_OFFSET = (400, 1300)
RUNTIME_TILE_SIZE = 1600
CORE_SPAN_KM = (24000, 18000)

Point = Tuple[int, int]


SEA = (102, 153, 160, 255)
SEA_DARK = (54, 107, 116, 210)
INK = (66, 62, 52, 255)
MUTED_INK = (91, 88, 76, 220)
PARCHMENT = (230, 211, 168, 255)
GOLD = (189, 142, 63, 235)
ROUTE = (247, 218, 141, 235)


LAND_OUTLINE: List[Point] = [
    (210, 420),
    (315, 255),
    (525, 140),
    (825, 115),
    (1090, 205),
    (1435, 165),
    (1690, 255),
    (1855, 405),
    (1985, 610),
    (1940, 850),
    (2050, 1065),
    (1910, 1210),
    (1640, 1265),
    (1380, 1350),
    (1035, 1305),
    (790, 1370),
    (550, 1280),
    (370, 1140),
    (270, 925),
    (185, 690),
]


STATES: List[Dict[str, object]] = [
    {
        "id": "xuanshuo",
        "name": "玄朔州",
        "subtitle": "北冥雪原",
        "fill": (184, 208, 209, 225),
        "center": (620, 285),
        "polygon": [
            (210, 420), (315, 255), (525, 140), (825, 115),
            (1090, 205), (1035, 405), (815, 485), (585, 430),
            (355, 525), (205, 585),
        ],
    },
    {
        "id": "xiangshan",
        "name": "相山州",
        "subtitle": "相山北海",
        "fill": (134, 181, 184, 220),
        "center": (1260, 290),
        "polygon": [
            (1090, 205), (1435, 165), (1690, 255), (1765, 435),
            (1590, 535), (1350, 515), (1160, 430), (1035, 405),
        ],
    },
    {
        "id": "guanhe",
        "name": "关河州",
        "subtitle": "西陵荒域",
        "fill": (210, 168, 103, 224),
        "center": (475, 650),
        "polygon": [
            (205, 585), (355, 525), (585, 430), (815, 485),
            (750, 735), (565, 860), (330, 790), (195, 690),
        ],
    },
    {
        "id": "yunque",
        "name": "云阙州",
        "subtitle": "中州玉虚",
        "fill": (170, 193, 133, 224),
        "center": (1040, 620),
        "polygon": [
            (815, 485), (1035, 405), (1160, 430), (1350, 515),
            (1330, 745), (1160, 865), (890, 815), (750, 735),
        ],
    },
    {
        "id": "donghua",
        "name": "东华州",
        "subtitle": "东陆机巧",
        "fill": (207, 167, 102, 220),
        "center": (1585, 690),
        "polygon": [
            (1350, 515), (1590, 535), (1765, 435), (1855, 600),
            (1940, 850), (1720, 945), (1470, 835), (1330, 745),
        ],
    },
    {
        "id": "yunxiu",
        "name": "云岫州",
        "subtitle": "百脉山原",
        "fill": (151, 133, 128, 225),
        "center": (600, 990),
        "polygon": [
            (330, 790), (565, 860), (750, 735), (890, 815),
            (865, 1015), (720, 1135), (525, 1090), (370, 1140),
            (270, 925),
        ],
    },
    {
        "id": "cangwu",
        "name": "苍梧州",
        "subtitle": "南荒万山",
        "fill": (106, 148, 104, 224),
        "center": (1110, 1060),
        "polygon": [
            (865, 1015), (890, 815), (1160, 865), (1330, 745),
            (1470, 835), (1510, 1065), (1380, 1215), (1035, 1305),
            (720, 1135),
        ],
    },
    {
        "id": "guichao",
        "name": "归潮州",
        "subtitle": "南冥海疆",
        "fill": (78, 148, 142, 222),
        "center": (1720, 1130),
        "polygon": [
            (1510, 1065), (1720, 945), (1940, 850), (2050, 1065),
            (1910, 1210), (1640, 1265), (1380, 1350), (1380, 1215),
        ],
    },
]


ISLANDS: List[Tuple[str, List[Point], Tuple[int, int, int, int]]] = [
    ("沧溟州·东海群岛", [(1870, 250), (1935, 205), (2010, 225), (2040, 290), (1970, 330), (1895, 315)], (96, 157, 166, 235)),
    ("沧溟州·东海群岛", [(2080, 380), (2160, 330), (2230, 365), (2250, 430), (2185, 480), (2115, 455)], (96, 157, 166, 235)),
    ("沧溟州·东海群岛", [(1990, 520), (2050, 480), (2115, 505), (2130, 565), (2070, 600), (2015, 585)], (96, 157, 166, 235)),
    ("归潮州·南冥海疆", [(2040, 1190), (2110, 1135), (2190, 1165), (2210, 1230), (2160, 1280), (2070, 1260)], (73, 145, 140, 235)),
    ("归潮州·南冥海疆", [(1880, 1320), (1940, 1280), (2005, 1310), (2015, 1370), (1945, 1400), (1885, 1370)], (73, 145, 140, 235)),
]


ANCHORS: List[Dict[str, object]] = [
    {"name": "北极仙宫", "kind": "守界宗门", "point": (650, 230), "offset": (0, -26)},
    {"name": "九脉山庄", "kind": "地师家族", "point": (1200, 350), "offset": (0, 28)},
    {"name": "西陵戍所", "kind": "边地军务", "point": (360, 620), "offset": (-6, -28)},
    {"name": "玉虚仙宗", "kind": "传法宗门", "point": (1050, 565), "offset": (0, -30)},
    {"name": "天衡仙国", "kind": "仙国网络", "point": (1840, 990), "offset": (0, -30)},
    {"name": "千机城", "kind": "城邦工坊", "point": (1590, 650), "offset": (0, -28)},
    {"name": "蓬莱仙盟", "kind": "海上联盟", "point": (1960, 250), "offset": (26, -10)},
    {"name": "铸剑城", "kind": "地方城邦", "point": (620, 925), "offset": (-10, -28)},
    {"name": "万妖谷", "kind": "妖族联盟", "point": (1190, 1010), "offset": (0, -30)},
    {"name": "不夜港", "kind": "自由港", "point": (1690, 1110), "offset": (20, 28)},
    {"name": "青溪里", "kind": "序章起点", "point": (1280, 795), "offset": (18, 28)},
    {"name": "长风镖局", "kind": "跨州商路", "point": (850, 720), "offset": (0, 30)},
]


ROUTES: List[Dict[str, object]] = [
    {"name": "北境雪关线", "points": [(850, 390), (1080, 350), (1330, 380), (1570, 465)]},
    {"name": "人皇旧道", "points": [(430, 700), (720, 650), (1040, 630), (1340, 690), (1650, 800)]},
    {"name": "百脉山口线", "points": [(700, 745), (650, 860), (780, 980), (960, 1050)]},
    {"name": "三江南行线", "points": [(1130, 625), (1145, 800), (1210, 970), (1385, 1100)]},
    {"name": "大光海道", "points": [(1650, 720), (1840, 650), (2030, 610), (2140, 730), (1920, 1000)]},
    {"name": "归墟外环", "points": [(1850, 1180), (2110, 1085), (2280, 1180), (2300, 1330)]},
]


MASK_COLORS = {
    "xuanshuo": (229, 57, 53),
    "xiangshan": (0, 131, 143),
    "guanhe": (196, 127, 39),
    "yunque": (109, 145, 62),
    "donghua": (177, 125, 36),
    "cangming": (22, 131, 154),
    "yunxiu": (111, 99, 139),
    "cangwu": (78, 138, 72),
    "guichao": (38, 125, 120),
}

# The sea-state envelope is clickable without falsely assigning every ocean
# pixel to a political border.  The actual island ownership remains visible
# in the terrain layer and can later be split into route/harbour data.
CANGMING_POLYGON: List[Point] = [
    (1710, 110), (2390, 110), (2390, 760), (1840, 760),
    (1710, 510),
]


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    candidates = [
        Path("C:/Windows/Fonts/simhei.ttf"),
        Path("C:/Windows/Fonts/HYZhongHeiTi-197.ttf"),
        Path("C:/Windows/Fonts/simsunb.ttf") if bold else Path("C:/Windows/Fonts/simsun.ttc"),
        Path("C:/Windows/Fonts/STKAITI.TTF"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def center_text(draw: ImageDraw.ImageDraw, xy: Point, text: str, text_font: ImageFont.FreeTypeFont, fill, stroke_fill=None, stroke_width=0) -> None:
    box = draw.textbbox((0, 0), text, font=text_font, stroke_width=stroke_width)
    x = xy[0] - (box[2] - box[0]) / 2
    y = xy[1] - (box[3] - box[1]) / 2
    draw.text((x, y), text, font=text_font, fill=fill, stroke_width=stroke_width, stroke_fill=stroke_fill)


def draw_dashed(draw: ImageDraw.ImageDraw, points: Sequence[Point], fill, width: int = 4, dash: int = 18, gap: int = 12) -> None:
    for start, end in zip(points, points[1:]):
        x1, y1 = start
        x2, y2 = end
        distance = math.hypot(x2 - x1, y2 - y1)
        if distance == 0:
            continue
        ux, uy = (x2 - x1) / distance, (y2 - y1) / distance
        travelled = 0.0
        while travelled < distance:
            length = min(dash, distance - travelled)
            a = (int(x1 + ux * travelled), int(y1 + uy * travelled))
            b = (int(x1 + ux * (travelled + length)), int(y1 + uy * (travelled + length)))
            draw.line((a, b), fill=fill, width=width)
            travelled += dash + gap


def draw_mountain_chain(draw: ImageDraw.ImageDraw, points: Sequence[Point], color=(90, 86, 70, 205), scale: int = 1) -> None:
    draw.line(points, fill=color, width=5 * scale, joint="curve")
    for idx, (x, y) in enumerate(points[::2]):
        height = 24 * scale + (idx % 3) * 7 * scale
        width = 34 * scale
        peak = (x, y - height)
        draw.polygon([(x - width, y), peak, (x + width, y)], fill=(112, 107, 88, 110), outline=(87, 82, 67, 175))
        draw.line([(x - width // 3, y - 2), peak, (x + width // 3, y - 2)], fill=(236, 225, 190, 210), width=3 * scale)


def draw_leaf_cluster(draw: ImageDraw.ImageDraw, x: int, y: int, scale: int = 1) -> None:
    for dx, dy, r in [(-18, 4, 18), (0, -8, 23), (20, 5, 17), (4, 16, 15)]:
        draw.ellipse((x + (dx - r) * scale, y + (dy - r) * scale, x + (dx + r) * scale, y + (dy + r) * scale), fill=(59, 111, 73, 105), outline=(57, 101, 65, 150), width=2 * scale)


def scale_point(point: Point, scale: int) -> Point:
    return (int(round(point[0] * scale)), int(round(point[1] * scale)))


def scale_points(points: Sequence[Point], scale: int) -> List[Point]:
    return [scale_point(point, scale) for point in points]


def scale_box(box: Tuple[int, int, int, int], scale: int) -> Tuple[int, int, int, int]:
    return tuple(int(round(value * scale)) for value in box)  # type: ignore[return-value]


def runtime_point(point: Point) -> Point:
    return (int(round(point[0] * RUNTIME_ART_SCALE + RUNTIME_OFFSET[0])), int(round(point[1] * RUNTIME_ART_SCALE + RUNTIME_OFFSET[1])))


def build_native_terrain(scale: int = RUNTIME_ART_SCALE) -> Image.Image:
    """Render terrain at native resolution instead of enlarging the review PNG."""
    image = Image.new("RGBA", (WIDTH * scale, HEIGHT * scale), SEA)
    draw = ImageDraw.Draw(image, "RGBA")
    rng = random.Random(20260912)

    # Broad water field: large quiet bands and sparse wave marks keep the
    # empty sea readable instead of filling it with decorative noise.
    for y in range(120, HEIGHT, 90):
        draw.arc(scale_box((80, y, WIDTH - 90, y + 170), scale), 186, 350, fill=(215, 232, 224, 35), width=4 * scale)
    for _ in range(360):
        x = rng.randint(80, WIDTH - 80)
        y = rng.randint(120, HEIGHT - 80)
        length = rng.randint(10, 34)
        dy = rng.randint(0, 3)
        draw.line((x * scale, y * scale, (x + length) * scale, (y - dy) * scale), fill=(224, 238, 229, rng.randint(20, 65)), width=2 * scale)

    draw.polygon(scale_points(LAND_OUTLINE, scale), fill=PARCHMENT, outline=(89, 85, 70, 255))
    for _label, polygon, fill in ISLANDS:
        draw.polygon(scale_points(polygon, scale), fill=fill, outline=(61, 105, 112, 230))

    # Keep texture sampling in source coordinates so preview and runtime share
    # the same land mask, while the marks themselves are drawn natively.
    land_mask = Image.new("1", (WIDTH * scale, HEIGHT * scale), 0)
    land_mask_draw = ImageDraw.Draw(land_mask)
    land_mask_draw.polygon(scale_points(LAND_OUTLINE, scale), fill=1)
    for _ in range(360):
        x = rng.randint(210, 1980)
        y = rng.randint(150, 1360)
        if not land_mask.getpixel((x * scale, y * scale)):
            continue
        radius = rng.randint(1, 5)
        tone = rng.choice([(214, 198, 160, 255), (238, 220, 181, 255), (204, 190, 154, 255)])
        draw.ellipse(scale_box((x - radius, y - radius, x + radius, y + radius), scale), fill=tone)
    for row in range(340, 1240, 118):
        relief_points: List[Point] = []
        for x in range(230, 1980, 26):
            y = row + int(18 * math.sin(x / 125.0 + row / 210.0))
            if land_mask.getpixel((x * scale, y * scale)):
                relief_points.append((x * scale, y * scale))
            elif len(relief_points) > 1:
                draw.line(relief_points, fill=(198, 182, 143, 255), width=1 * scale)
                relief_points = []
            else:
                relief_points = []
        if len(relief_points) > 1:
            draw.line(relief_points, fill=(198, 182, 143, 255), width=1 * scale)

    mountain_chains = [
        [(450, 350), (630, 300), (820, 330), (1010, 290)],
        [(1140, 320), (1260, 250), (1390, 315), (1535, 285)],
        [(410, 820), (555, 760), (700, 790), (820, 900)],
        [(800, 1040), (950, 950), (1110, 1020), (1250, 930), (1390, 990)],
        [(1510, 910), (1630, 840), (1770, 930), (1850, 880)],
    ]
    for chain in mountain_chains:
        draw_mountain_chain(draw, scale_points(chain, scale), scale=scale)

    for x, y in [(290, 620), (390, 565), (480, 720), (600, 620), (520, 780), (350, 745)]:
        draw.arc(scale_box((x - 70, y - 20, x + 80, y + 42), scale), 190, 350, fill=(132, 96, 52, 125), width=4 * scale)
        draw.arc(scale_box((x - 48, y + 2, x + 100, y + 62), scale), 190, 350, fill=(247, 215, 144, 130), width=3 * scale)

    rivers = [
        [(915, 420), (945, 555), (1015, 680), (1125, 795), (1210, 965), (1430, 1120), (1630, 1135)],
        [(1140, 470), (1130, 610), (1060, 760), (950, 900), (820, 1010)],
        [(1355, 610), (1470, 670), (1605, 780), (1720, 900), (1805, 1050)],
    ]
    river_widths = [(13, 4), (10, 3), (9, 3)]
    for river, (outer_width, inner_width) in zip(rivers, river_widths):
        points = scale_points(river, scale)
        draw.line(points, fill=(71, 133, 151, 195), width=outer_width * scale, joint="curve")
        draw.line(points, fill=(177, 224, 216, 170), width=inner_width * scale, joint="curve")
    draw.ellipse(scale_box((1190, 760, 1360, 860), scale), fill=(71, 142, 157, 135), outline=(58, 111, 123, 175), width=4 * scale)

    for x, y in [(920, 1040), (1010, 1120), (1115, 1085), (1235, 1150), (1320, 1050), (1410, 1125), (1080, 1230), (850, 1170)]:
        draw_leaf_cluster(draw, x * scale, y * scale, scale)
    for x, y in [(1080, 1180), (1160, 1210), (1280, 1240), (1440, 1170), (980, 1260)]:
        draw.arc(scale_box((x - 55, y - 15, x + 70, y + 35), scale), 180, 360, fill=(59, 105, 76, 150), width=4 * scale)

    return image


def build_runtime_assets(native_terrain: Image.Image) -> None:
    """Create the square in-game background and aligned 5×5 ID masks."""
    expected_size = (WIDTH * RUNTIME_ART_SCALE, HEIGHT * RUNTIME_ART_SCALE)
    if native_terrain.size != expected_size:
        raise ValueError(f"native terrain size {native_terrain.size} does not match {expected_size}")
    runtime = Image.new("RGBA", (LOGICAL_MAP_SIZE, LOGICAL_MAP_SIZE), SEA)
    runtime.alpha_composite(native_terrain, RUNTIME_OFFSET)
    OUT_RUNTIME_PNG.parent.mkdir(parents=True, exist_ok=True)
    runtime.convert("RGB").save(OUT_RUNTIME_PNG, compress_level=6)

    mask = Image.new("RGB", (LOGICAL_MAP_SIZE, LOGICAL_MAP_SIZE), (0, 0, 0))
    mask_draw = ImageDraw.Draw(mask)
    for state in STATES:
        points = [runtime_point(point) for point in state["polygon"]]
        mask_draw.polygon(points, fill=MASK_COLORS[state["id"]])
    mask_draw.polygon([runtime_point(point) for point in CANGMING_POLYGON], fill=MASK_COLORS["cangming"])
    for label, polygon, _ in ISLANDS:
        state_id = "cangming" if label.startswith("沧溟州") else "guichao"
        mask_draw.polygon([runtime_point(point) for point in polygon], fill=MASK_COLORS[state_id])

    OUT_MASK_DIR.mkdir(parents=True, exist_ok=True)
    tile_size = RUNTIME_TILE_SIZE
    for row in range(5):
        for col in range(5):
            name = f"zhaoming_territory_id_r{row}{col}.png"
            tile = mask.crop((col * tile_size, row * tile_size, (col + 1) * tile_size, (row + 1) * tile_size))
            tile.save(OUT_MASK_DIR / name, compress_level=6)


def build_png() -> None:
    random.seed(20260912)
    image = Image.new("RGBA", (WIDTH, HEIGHT), SEA)
    draw = ImageDraw.Draw(image, "RGBA")

    # Broad water field: large quiet bands and sparse wave marks keep the
    # empty sea readable instead of filling it with decorative noise.
    for y in range(120, HEIGHT, 90):
        draw.arc((80, y, WIDTH - 90, y + 170), 186, 350, fill=(215, 232, 224, 35), width=4)
    for _ in range(360):
        x = random.randint(80, WIDTH - 80)
        y = random.randint(120, HEIGHT - 80)
        length = random.randint(10, 34)
        draw.line((x, y, x + length, y - random.randint(0, 3)), fill=(224, 238, 229, random.randint(20, 65)), width=2)

    # The land silhouette is intentionally oversized with a broken southern
    # edge; the large water margins are part of the world-scale read.
    draw.polygon(LAND_OUTLINE, fill=PARCHMENT, outline=(89, 85, 70, 255))
    for label, polygon, fill in ISLANDS:
        draw.polygon(polygon, fill=fill, outline=(61, 105, 112, 230))

    # A restrained paper-and-relief texture keeps the runtime layer from
    # reading as a flat polygon while remaining quiet behind map markers.
    land_mask = Image.new("1", (WIDTH, HEIGHT), 0)
    land_mask_draw = ImageDraw.Draw(land_mask)
    land_mask_draw.polygon(LAND_OUTLINE, fill=1)
    for _ in range(360):
        x = random.randint(210, 1980)
        y = random.randint(150, 1360)
        if not land_mask.getpixel((x, y)):
            continue
        radius = random.randint(1, 5)
        # Keep the pixels close to parchment because the runtime export is
        # flattened to RGB after the terrain layer is composed.
        tone = random.choice([(214, 198, 160, 255), (238, 220, 181, 255), (204, 190, 154, 255)])
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=tone)
    for row in range(340, 1240, 118):
        relief_points: List[Point] = []
        for x in range(230, 1980, 26):
            y = row + int(18 * math.sin(x / 125.0 + row / 210.0))
            if land_mask.getpixel((x, y)):
                relief_points.append((x, y))
            elif len(relief_points) > 1:
                draw.line(relief_points, fill=(198, 182, 143, 255), width=1)
                relief_points = []
            else:
                relief_points = []
        if len(relief_points) > 1:
            draw.line(relief_points, fill=(198, 182, 143, 255), width=1)

    # Macro terrain anchors.  They cross political boundaries sparingly and
    # therefore communicate that terrain, not sect identity, shapes the map.
    draw_mountain_chain(draw, [(450, 350), (630, 300), (820, 330), (1010, 290)], scale=1)
    draw_mountain_chain(draw, [(1140, 320), (1260, 250), (1390, 315), (1535, 285)], scale=1)
    draw_mountain_chain(draw, [(410, 820), (555, 760), (700, 790), (820, 900)], scale=1)
    draw_mountain_chain(draw, [(800, 1040), (950, 950), (1110, 1020), (1250, 930), (1390, 990)], scale=1)
    draw_mountain_chain(draw, [(1510, 910), (1630, 840), (1770, 930), (1850, 880)], scale=1)

    # Desert dunes and the absolute/relative divide of the western basin.
    for x, y in [(290, 620), (390, 565), (480, 720), (600, 620), (520, 780), (350, 745)]:
        draw.arc((x - 70, y - 20, x + 80, y + 42), 190, 350, fill=(132, 96, 52, 125), width=4)
        draw.arc((x - 48, y + 2, x + 100, y + 62), 190, 350, fill=(247, 215, 144, 130), width=3)

    # Three long rivers and the delayed southern delta.
    river = [(915, 420), (945, 555), (1015, 680), (1125, 795), (1210, 965), (1430, 1120), (1630, 1135)]
    draw.line(river, fill=(71, 133, 151, 195), width=13, joint="curve")
    draw.line(river, fill=(177, 224, 216, 170), width=4, joint="curve")
    river2 = [(1140, 470), (1130, 610), (1060, 760), (950, 900), (820, 1010)]
    draw.line(river2, fill=(79, 143, 160, 180), width=10, joint="curve")
    draw.line(river2, fill=(184, 222, 210, 150), width=3, joint="curve")
    river3 = [(1355, 610), (1470, 670), (1605, 780), (1720, 900), (1805, 1050)]
    draw.line(river3, fill=(73, 133, 151, 180), width=9, joint="curve")
    draw.line(river3, fill=(182, 222, 208, 145), width=3, joint="curve")
    draw.ellipse((1190, 760, 1360, 860), fill=(71, 142, 157, 135), outline=(58, 111, 123, 175), width=4)

    # Southern forest and wetland texture.
    for x, y in [(920, 1040), (1010, 1120), (1115, 1085), (1235, 1150), (1320, 1050), (1410, 1125), (1080, 1230), (850, 1170)]:
        draw_leaf_cluster(draw, x, y)
    for x, y in [(1080, 1180), (1160, 1210), (1280, 1240), (1440, 1170), (980, 1260)]:
        draw.arc((x - 55, y - 15, x + 70, y + 35), 180, 360, fill=(59, 105, 76, 150), width=4)

    # State colors belong to the design review layer.  The runtime copy above
    # deliberately excludes them so that the game reads as a detailed map
    # first, with political boundaries supplied by the interactive overlay.
    for state in STATES:
        polygon = state["polygon"]
        draw.polygon(polygon, fill=state["fill"], outline=(94, 84, 65, 210))

    title_font = font(42, bold=True)
    subtitle_font = font(21)
    state_font = font(37, bold=True)
    state_subtitle_font = font(22)
    anchor_font = font(21, bold=True)
    small_font = font(17)

    # Header and the intentionally sparse cartographic legend.
    draw.text((100, 62), "昭明界 · 九州广袤布局草图", font=title_font, fill=(48, 55, 53, 255), stroke_width=1, stroke_fill=(224, 213, 181, 170))
    draw.text((102, 112), "序章后半自由修炼世界｜自然边界先于势力边界｜本图为 L0/L1 总览", font=subtitle_font, fill=(47, 70, 69, 235))

    for state in STATES:
        cx, cy = state["center"]
        center_text(draw, (cx, cy - 17), state["name"], state_font, (55, 56, 47, 255), (232, 220, 183, 225), 3)
        center_text(draw, (cx, cy + 28), f"· {state['subtitle']} ·", state_subtitle_font, (65, 67, 55, 235), (232, 220, 183, 170), 2)
    center_text(draw, (2050, 210), "沧溟州", state_font, (58, 74, 73, 255), (174, 211, 204, 180), 3)
    center_text(draw, (2050, 258), "· 东海群岛 ·", state_subtitle_font, (57, 82, 81, 235), (174, 211, 204, 170), 2)

    # Anchor marks; labels show a cross-section of the larger faction pool.
    for anchor in ANCHORS:
        x, y = anchor["point"]
        ox, oy = anchor["offset"]
        draw.ellipse((x - 10, y - 10, x + 10, y + 10), fill=(246, 218, 147, 255), outline=(97, 70, 38, 255), width=3)
        draw.line((x, y, x + ox, y + oy), fill=(107, 79, 45, 190), width=2)
        center_text(draw, (x + ox, y + oy), anchor["name"], anchor_font, (62, 57, 46, 255), (224, 211, 170, 210), 2)

    # Compass and design notes.
    draw.line((2220, 160, 2220, 270), fill=(236, 223, 187, 220), width=4)
    draw.polygon([(2220, 135), (2207, 176), (2233, 176)], fill=(236, 223, 187, 235))
    draw.text((2207, 278), "北", font=small_font, fill=(232, 223, 195, 230))
    draw.rounded_rectangle((86, 1480, 690, 1695), radius=16, fill=(42, 81, 83, 180), outline=(220, 208, 175, 190), width=2)
    draw.text((112, 1506), "设计裁定", font=anchor_font, fill=(247, 225, 166, 255))
    notes = [
        "• 五乘五只保留为资源加载分块，不画成州界",
        "• 核心框架约 24000×18000 km，边缘海域不等同州界",
        "• 每州有多种组织，但总览只标少量 L1 锚点",
        "• 云岫州是百脉山原，不再等同于剑修流派",
        "• 玩家可散修、入宗、入仙国或成为家族客卿",
    ]
    for idx, note in enumerate(notes):
        draw.text((112, 1543 + idx * 25), note, font=small_font, fill=(232, 231, 211, 235))

    draw.text((1740, 1680), "地图外沿保留未定外洋，作为后期航图与异象空间", font=small_font, fill=(229, 239, 229, 225))
    image.convert("RGB").save(OUT_PNG, quality=95)
    build_runtime_assets(build_native_terrain())


def svg_points(points: Sequence[Point]) -> str:
    return " ".join(f"{x},{y}" for x, y in points)


def svg_text(x: int, y: int, text: str, size: int, fill: str, anchor: str = "middle", weight: int = 400, opacity: float = 1.0) -> str:
    return (
        f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="Microsoft YaHei,SimHei,sans-serif" '
        f'font-size="{size}px" font-weight="{weight}" fill="{fill}" fill-opacity="{opacity}">{html.escape(text)}</text>'
    )


def svg_line(points: Sequence[Point], stroke: str, width: int, dash: str = "", opacity: float = 1.0) -> str:
    dash_attr = f' stroke-dasharray="{dash}"' if dash else ""
    return f'<polyline points="{svg_points(points)}" fill="none" stroke="{stroke}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" stroke-opacity="{opacity}"{dash_attr}/>'


def build_svg() -> None:
    parts: List[str] = [
        f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" width="{LOGICAL_MAP_SIZE}" height="{LOGICAL_MAP_SIZE}" viewBox="0 0 {LOGICAL_MAP_SIZE} {LOGICAL_MAP_SIZE}">',
        "<title>昭明界九州广袤布局草图</title>",
        "<desc>8000×8000逻辑地图；核心版图标尺约24000×18000公里。源稿以2400×1800绘制后按统一变换放置。五乘五仅为资源加载分块。</desc>",
        f'<rect width="{LOGICAL_MAP_SIZE}" height="{LOGICAL_MAP_SIZE}" fill="#6699a0"/>',
        f'<g id="source-art" transform="translate({RUNTIME_OFFSET[0]} {RUNTIME_OFFSET[1]}) scale({RUNTIME_ART_SCALE})">',
        '<g id="sea" inkscape:groupmode="layer" inkscape:label="海域背景">',
        f'<rect width="{WIDTH}" height="{HEIGHT}" fill="#6699a0"/>',
        '<path d="M80 250 C650 120 1250 260 2320 150 M40 520 C780 370 1370 610 2390 420 M30 820 C680 650 1420 930 2390 720 M40 1120 C720 930 1530 1200 2390 980" fill="none" stroke="#d7e9df" stroke-opacity=".18" stroke-width="7"/>',
        '</g>',
        '<g id="land" inkscape:groupmode="layer" inkscape:label="大陆轮廓">',
        f'<polygon points="{svg_points(LAND_OUTLINE)}" fill="#e6d3a8" stroke="#595546" stroke-width="5"/>',
        '</g>',
        '<g id="territories" inkscape:groupmode="layer" inkscape:label="九州自然分区">',
    ]
    for state in STATES:
        r, g, b, a = state["fill"]
        opacity = round(a / 255, 3)
        parts.append(
            f'<polygon id="{state["id"]}" points="{svg_points(state["polygon"])}" fill="rgb({r},{g},{b})" fill-opacity="{opacity}" stroke="#71664f" stroke-width="3" stroke-opacity=".82"/>'
        )
    for idx, (label, polygon, fill) in enumerate(ISLANDS):
        r, g, b, a = fill
        parts.append(f'<polygon id="island-{idx}" points="{svg_points(polygon)}" fill="rgb({r},{g},{b})" fill-opacity="{a / 255:.3f}" stroke="#3d6970" stroke-width="3"/>')
    parts.append('</g>')

    parts.append('<g id="terrain" inkscape:groupmode="layer" inkscape:label="山河与地貌锚点">')
    mountain_chains = [
        [(450, 350), (630, 300), (820, 330), (1010, 290)],
        [(1140, 320), (1260, 250), (1390, 315), (1535, 285)],
        [(410, 820), (555, 760), (700, 790), (820, 900)],
        [(800, 1040), (950, 950), (1110, 1020), (1250, 930), (1390, 990)],
        [(1510, 910), (1630, 840), (1770, 930), (1850, 880)],
    ]
    for chain in mountain_chains:
        parts.append(svg_line(chain, "#5a5646", 5, opacity=.76))
        for idx, (x, y) in enumerate(chain[::2]):
            h = 24 + (idx % 3) * 7
            parts.append(f'<path d="M{x-34} {y} L{x} {y-h} L{x+34} {y} Z" fill="#706b58" fill-opacity=".35" stroke="#574f43" stroke-opacity=".65" stroke-width="2"/>')
            parts.append(f'<path d="M{x-12} {y-2} L{x} {y-h} L{x+12} {y-2}" fill="none" stroke="#eee2c1" stroke-opacity=".78" stroke-width="3"/>')
    rivers = [
        [(915, 420), (945, 555), (1015, 680), (1125, 795), (1210, 965), (1430, 1120), (1630, 1135)],
        [(1140, 470), (1130, 610), (1060, 760), (950, 900), (820, 1010)],
        [(1355, 610), (1470, 670), (1605, 780), (1720, 900), (1805, 1050)],
    ]
    for river in rivers:
        parts.append(svg_line(river, "#478797", 12, opacity=.72))
        parts.append(svg_line(river, "#b7dfd2", 3, opacity=.72))
    parts.append('<ellipse cx="1275" cy="810" rx="85" ry="50" fill="#478e9d" fill-opacity=".5" stroke="#3a6e7a" stroke-width="3"/>')
    for x, y in [(920, 1040), (1010, 1120), (1115, 1085), (1235, 1150), (1320, 1050), (1410, 1125), (1080, 1230), (850, 1170)]:
        parts.append(f'<circle cx="{x}" cy="{y}" r="26" fill="#4f8b5d" fill-opacity=".34" stroke="#47784d" stroke-opacity=".52" stroke-width="2"/>')
    parts.append('</g>')

    parts.append('<g id="labels" inkscape:groupmode="layer" inkscape:label="州名标注">')
    parts.append(svg_text(100, 82, "昭明界 · 九州广袤布局草图", 42, "#303732", anchor="start", weight=700))
    parts.append(svg_text(102, 122, "序章后半自由修炼世界｜自然边界先于势力边界｜本图为 L0/L1 总览", 21, "#2f4645", anchor="start"))
    for state in STATES:
        cx, cy = state["center"]
        parts.append(svg_text(cx, cy - 10, str(state["name"]), 37, "#37382f", weight=700))
        parts.append(svg_text(cx, cy + 32, f"· {state['subtitle']} ·", 22, "#414337"))
    parts.append(svg_text(2050, 210, "沧溟州", 37, "#3a4a49", weight=700))
    parts.append(svg_text(2050, 248, "· 东海群岛 ·", 22, "#3c5654"))
    parts.append('</g>')

    parts.append('<g id="anchors" inkscape:groupmode="layer" inkscape:label="L1势力锚点">')
    for idx, anchor in enumerate(ANCHORS):
        x, y = anchor["point"]
        ox, oy = anchor["offset"]
        parts.append(f'<circle id="anchor-{idx}" cx="{x}" cy="{y}" r="10" fill="#f6da93" stroke="#614626" stroke-width="3"/>')
        parts.append(svg_line([(x, y), (x + ox, y + oy)], "#6b4f2d", 2, opacity=.76))
        parts.append(svg_text(x + ox, y + oy + 7, str(anchor["name"]), 21, "#3e392e", weight=700))
    parts.append('</g>')

    parts.extend([
        '<g id="notes" inkscape:groupmode="layer" inkscape:label="设计说明">',
        '<rect x="86" y="1480" width="604" height="215" rx="16" fill="#2a5153" fill-opacity=".78" stroke="#dccfaf" stroke-opacity=".8" stroke-width="2"/>',
        svg_text(112, 1506, "设计裁定", 21, "#f7e1a6", anchor="start", weight=700),
        svg_text(112, 1543, "• 五乘五只保留为资源加载分块，不画成州界", 17, "#e8e7d3", anchor="start"),
        svg_text(112, 1568, "• 核心框架约 24000×18000 km，边缘海域不等同州界", 17, "#e8e7d3", anchor="start"),
        svg_text(112, 1593, "• 每州有多种组织，但总览只标少量 L1 锚点", 17, "#e8e7d3", anchor="start"),
        svg_text(112, 1618, "• 云岫州是百脉山原，不再等同于剑修流派", 17, "#e8e7d3", anchor="start"),
        svg_text(112, 1643, "• 玩家可散修、入宗、入仙国或成为家族客卿", 17, "#e8e7d3", anchor="start"),
        svg_text(1740, 1680, "地图外沿保留未定外洋，作为后期航图与异象空间", 17, "#e5efe5", anchor="start"),
        '<path d="M2220 135 L2207 176 L2233 176 Z" fill="#ecdfbb"/><line x1="2220" y1="160" x2="2220" y2="270" stroke="#ecdfbb" stroke-width="4"/><text x="2207" y="295" font-family="Microsoft YaHei,SimHei,sans-serif" font-size="17px" fill="#e8dfc3">北</text>',
        '</g>',
        '</g>',
        '</svg>',
    ])
    OUT_SVG.write_text("\n".join(parts), encoding="utf-8")


def build_manifest() -> None:
    manifest = {
        "version": 2,
        "purpose": "九州核心版图的可编辑规划源与运行时坐标索引",
        "canvas": [LOGICAL_MAP_SIZE, LOGICAL_MAP_SIZE],
        "source_canvas": [WIDTH, HEIGHT],
        "runtime_transform": {"scale": RUNTIME_ART_SCALE, "offset": list(RUNTIME_OFFSET)},
        "effective_core_span_km": list(CORE_SPAN_KM),
        "physical_scale_km_per_logical_unit": 3.0,
        "resource_chunk_size": [RUNTIME_TILE_SIZE, RUNTIME_TILE_SIZE],
        "resource_chunk_grid": [5, 5],
        "resource_chunk_note": "5×5 仅为加载与资源分块，不是政治疆域网格",
        "route_note": "路线关系保留在运行时数据中供地点面板发起旅行，不在地图视觉层常驻显示",
        "states": [
            {"id": state["id"], "name": state["name"], "subtitle": state["subtitle"], "center": state["center"], "runtime_center": runtime_point(state["center"])}
            for state in STATES
        ] + [{"id": "cangming", "name": "沧溟州", "subtitle": "东海群岛", "center": [2050, 210], "runtime_center": runtime_point((2050, 210))}],
        "routes": [{"name": route["name"], "points": route["points"], "runtime_points": [runtime_point(point) for point in route["points"]]} for route in ROUTES],
        "anchors": [{"name": a["name"], "kind": a["kind"], "point": a["point"], "runtime_point": runtime_point(a["point"])} for a in ANCHORS],
        "faction_note": "总览只标 L1 锚点；可加入宗门目标为 10–14 个，另有家族客卿、仙国身份与散修路线。",
        "cloud_note": "云岫州使用百脉山原命名；铸剑城仅为地方城邦，不代表整州剑修化。",
        "outer_world_note": "九州是昭明界的核心版图，不代表完整世界；外洋、界外大陆与上界通道留作后续地图层。",
    }
    OUT_MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


def main() -> None:
    DOCS.mkdir(parents=True, exist_ok=True)
    PRODUCTION.mkdir(parents=True, exist_ok=True)
    build_png()
    build_svg()
    build_manifest()
    print(OUT_PNG)
    print(OUT_SVG)
    print(OUT_MANIFEST)


if __name__ == "__main__":
    main()
