"""Render a hand-drawing guide for the current 4:3 Zhaoming terrain layout."""

from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "textures" / "world_map" / "production_samples" / "zhaoming_vast_nine_states_terrain_guide_2400.png"
S = 2
W, H = 2400 * S, 1800 * S


def sc(p: tuple[float, float]) -> tuple[int, int]:
    return (round(p[0] * S), round(p[1] * S))


def pts(points: list[tuple[float, float]]) -> list[tuple[int, int]]:
    return [sc(p) for p in points]


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc"
    return ImageFont.truetype(path, size * S)


def line(draw: ImageDraw.ImageDraw, points, fill, width=2, joint="curve"):
    draw.line(pts(points), fill=fill, width=width * S, joint=joint)


def mountain(draw: ImageDraw.ImageDraw, x: float, y: float, size: float = 1.0, snowy: bool = False):
    w, h = 54 * size, 70 * size
    base = [(x - w, y), (x, y - h), (x + w, y)]
    draw.polygon(pts(base), fill="#5f655c", outline="#403f38", width=2 * S)
    draw.polygon(pts([(x, y - h), (x + w, y), (x + 10 * size, y - 7 * size)]), fill="#3f514f")
    draw.polygon(pts([(x, y - h), (x - 18 * size, y - 18 * size), (x - 5 * size, y - 22 * size), (x + 3 * size, y - h + 9 * size)]), fill="#e8eadc" if snowy else "#b4b59e")
    line(draw, [(x - w * .75, y - 2), (x, y - h), (x + w * .55, y - 3)], "#d7d3b8", 2)


def mountain_chain(draw: ImageDraw.ImageDraw, anchors, snowy=False, scale=1.0):
    line(draw, anchors, "#6a6755", 3)
    for i, (x, y) in enumerate(anchors):
        mountain(draw, x, y, (0.85 + (i % 3) * .14) * scale, snowy)


def tree(draw: ImageDraw.ImageDraw, x: float, y: float, size: float = 1.0, autumn=False):
    trunk = "#755a3c"
    crown = "#476d51" if not autumn else "#bd7346"
    line(draw, [(x, y + 15 * size), (x, y + 38 * size)], trunk, max(2, round(4 * size)))
    for dx, dy, r in [(-15, 15, 17), (0, 3, 21), (16, 16, 16), (0, 23, 18)]:
        draw.ellipse((round((x + dx * size - r * size) * S), round((y + dy * size - r * size) * S), round((x + dx * size + r * size) * S), round((y + dy * size + r * size) * S)), fill=crown, outline="#355440", width=2 * S)


def forest(draw: ImageDraw.ImageDraw, x: float, y: float, cols: int, rows: int, spacing: float, autumn=False):
    for row in range(rows):
        for col in range(cols):
            jitter = ((row * 17 + col * 11) % 9) - 4
            tree(draw, x + col * spacing + (row % 2) * spacing * .35, y + row * spacing * .68 + jitter, .60 + ((row + col) % 3) * .08, autumn)


def dune(draw: ImageDraw.ImageDraw, x: float, y: float, w: float):
    line(draw, [(x - w, y), (x - w * .45, y - 15), (x + w * .18, y - 3), (x + w, y - 18)], "#b9894d", 3)
    line(draw, [(x - w * .65, y + 18), (x - w * .1, y + 4), (x + w * .65, y + 18)], "#c99e5e", 3)


def wetland(draw: ImageDraw.ImageDraw, x: float, y: float, w: float, h: float):
    draw.ellipse((round((x - w) * S), round((y - h) * S), round((x + w) * S), round((y + h) * S)), fill="#74a69a", outline="#3d7771", width=2 * S)
    line(draw, [(x - w * .7, y), (x - w * .25, y - h * .25), (x + w * .6, y + h * .15)], "#b1d6c0", 2)


def label(draw: ImageDraw.ImageDraw, x: float, y: float, title: str, subtitle: str, color="#343a35"):
    draw.text(sc((x, y)), title, font=font(34, True), fill=color, anchor="mm", stroke_width=2 * S, stroke_fill="#eadbb7")
    draw.text(sc((x, y + 38)), f"· {subtitle} ·", font=font(19), fill=color, anchor="mm", stroke_width=1 * S, stroke_fill="#eadbb7")


def state_overlay(image: Image.Image, polygon, fill: str):
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    d.polygon(pts(polygon), fill=fill)
    d.line(pts(polygon + [polygon[0]]), fill="#7f725e", width=2 * S, joint="curve")
    image.alpha_composite(overlay)


def main():
    random.seed(20260913)
    image = Image.new("RGBA", (W, H), "#5c8589")
    draw = ImageDraw.Draw(image)

    # Sea grain and currents make the empty margins readable without adding borders.
    for i in range(28):
        y = 95 + i * 61
        x = 35 + (i % 4) * 37
        line(draw, [(x, y), (520, y - 55), (1120, y + 28), (1750, y - 26), (2365, y + 8)], "#9cc2bd55", 2)
    for i in range(260):
        x = random.randint(15, 2385)
        y = random.randint(15, 1785)
        r = random.choice([1, 1, 2, 3])
        draw.ellipse((round((x - r) * S), round((y - r) * S), round((x + r) * S), round((y + r) * S)), fill="#d2e4d055")

    land = [(210, 420), (315, 255), (525, 140), (825, 115), (1090, 205), (1435, 165), (1690, 255), (1855, 405), (1985, 610), (1940, 850), (2050, 1065), (1910, 1210), (1640, 1265), (1380, 1350), (1035, 1305), (790, 1370), (550, 1280), (370, 1140), (270, 925), (185, 690)]
    draw.polygon(pts(land), fill="#e6d3a8", outline="#4f5c55", width=5 * S)

    states = [
        ([(210, 420), (315, 255), (525, 140), (825, 115), (1090, 205), (1035, 405), (815, 485), (585, 430), (355, 525), (205, 585)], "#b8d0d155"),
        ([(1090, 205), (1435, 165), (1690, 255), (1765, 435), (1590, 535), (1350, 515), (1160, 430), (1035, 405)], "#86b5b855"),
        ([(205, 585), (355, 525), (585, 430), (815, 485), (750, 735), (565, 860), (330, 790), (195, 690)], "#d2a86755"),
        ([(815, 485), (1035, 405), (1160, 430), (1350, 515), (1330, 745), (1160, 865), (890, 815), (750, 735)], "#aac18555"),
        ([(1350, 515), (1590, 535), (1765, 435), (1855, 600), (1940, 850), (1720, 945), (1470, 835), (1330, 745)], "#cfa76655"),
        ([(330, 790), (565, 860), (750, 735), (890, 815), (865, 1015), (720, 1135), (525, 1090), (370, 1140), (270, 925)], "#97858055"),
        ([(865, 1015), (890, 815), (1160, 865), (1330, 745), (1470, 835), (1510, 1065), (1380, 1215), (1035, 1305), (720, 1135)], "#6a946855"),
        ([(1510, 1065), (1720, 945), (1940, 850), (2050, 1065), (1910, 1210), (1640, 1265), (1380, 1350), (1380, 1215)], "#4e948e55"),
    ]
    for polygon, fill in states:
        state_overlay(image, polygon, fill)
    draw = ImageDraw.Draw(image)

    # Terrain anchors: long mountain belts and three river systems are the first strokes.
    mountain_chain(draw, [(450, 350), (630, 300), (820, 330), (1010, 290)], snowy=True, scale=.70)
    mountain_chain(draw, [(1140, 320), (1260, 250), (1390, 315), (1535, 285)], snowy=True, scale=.68)
    mountain_chain(draw, [(410, 820), (555, 760), (700, 790), (820, 900)], scale=.82)
    mountain_chain(draw, [(800, 1040), (950, 950), (1110, 1020), (1250, 930), (1390, 990)], scale=.92)
    mountain_chain(draw, [(1510, 910), (1630, 840), (1770, 930), (1850, 880)], scale=.75)
    mountain_chain(draw, [(460, 1110), (610, 1060), (760, 1130)], scale=.70)

    # West: desert basin, salt flats, and the unclaimed absolute-spirit ravine.
    for x, y, w in [(330, 585, 68), (480, 520, 90), (580, 680, 80), (355, 735, 72), (620, 470, 60)]:
        dune(draw, x, y, w)
    for x, y in [(300, 610), (405, 690), (555, 575), (625, 760)]:
        draw.polygon(pts([(x - 25, y), (x - 14, y - 34), (x + 2, y - 46), (x + 18, y - 12), (x + 34, y)]), fill="#7d654a", outline="#5d513f", width=2 * S)
    wetland(draw, 630, 790, 74, 22)

    # The central basin is the broadest plain: lake, confluences, and old-road space.
    draw.ellipse((round(1190 * S), round(760 * S), round(1360 * S), round(860 * S)), fill="#4d919b", outline="#376b76", width=3 * S)
    for x, y, r in [(1215, 812, 14), (1300, 800, 12), (1260, 845, 10)]:
        draw.ellipse((round((x-r) * S), round((y-r) * S), round((x+r) * S), round((y+r) * S)), fill="#7b9f77", outline="#496c63", width=2 * S)
    rivers = [
        [(915, 420), (945, 555), (1015, 680), (1125, 795), (1210, 965), (1430, 1120), (1630, 1135)],
        [(1140, 470), (1130, 610), (1060, 760), (950, 900), (820, 1010)],
        [(1355, 610), (1470, 670), (1605, 780), (1720, 900), (1805, 1050)],
    ]
    for river in rivers:
        line(draw, river, "#3d818fbb", 13)
        line(draw, river, "#b5ded0dd", 4)

    # South: rain forest, wetland fingers, and mangrove-like coast.
    forest(draw, 900, 1000, 6, 3, 65)
    forest(draw, 1080, 1160, 7, 2, 68)
    forest(draw, 1420, 1060, 5, 3, 70, autumn=True)
    for x, y, w, h in [(890, 1190, 100, 32), (1050, 1260, 115, 37), (1250, 1190, 92, 28), (1510, 1190, 110, 34)]:
        wetland(draw, x, y, w, h)

    # Northeast and southeast are broken sea, reefs, and island stepping-stones.
    islands = [(1900, 270, 45), (2130, 410, 55), (2050, 540, 39), (2100, 1190, 46), (1940, 1335, 36), (2220, 1290, 54), (1760, 1450, 32), (550, 1500, 35)]
    for x, y, r in islands:
        draw.polygon(pts([(x-r, y+12), (x-r*.25, y-r), (x+r*.75, y-r*.45), (x+r, y+r*.6), (x+r*.1, y+r), (x-r*.8, y+r*.55)]), fill="#6a9a8e", outline="#3c6d70", width=2 * S)
        mountain(draw, x, y + 12, .35, False)

    # Routes are dashed guidance lines, not political borders.
    route_color = "#f3d485cc"
    for route in [
        [(850, 390), (1080, 350), (1330, 380), (1570, 465)],
        [(430, 700), (720, 650), (1040, 630), (1340, 690), (1650, 800)],
        [(700, 745), (650, 860), (780, 980), (960, 1050)],
        [(1130, 625), (1145, 800), (1210, 970), (1385, 1100)],
        [(1650, 720), (1840, 650), (2030, 610), (2140, 730), (1920, 1000)],
        [(1850, 1180), (2110, 1085), (2280, 1180), (2300, 1330)],
    ]:
        # Drawing short dashes follows the route without introducing a regular grid.
        for (x1, y1), (x2, y2) in zip(route, route[1:]):
            dx, dy = x2 - x1, y2 - y1
            length = math.hypot(dx, dy)
            step = 30
            for start in range(0, max(1, round(length)), step * 2):
                end = min(length, start + step)
                a, b = start / length, end / length
                line(draw, [(x1 + dx * a, y1 + dy * a), (x1 + dx * b, y1 + dy * b)], route_color, 5)

    # Names keep the hand-drawing sheet tied to the current nine-state plan.
    label(draw, 620, 275, "玄朔州", "冰原 · 雪岭 · 裂隙")
    label(draw, 1260, 280, "相山州", "移动山脉 · 寒海群礁")
    label(draw, 475, 640, "关河州", "荒盆 · 盐泽 · 绝灵渊")
    label(draw, 1040, 610, "云阙州", "大河 · 湖盆 · 平原")
    label(draw, 1585, 680, "东华州", "丘陵 · 古渠 · 河港")
    label(draw, 2050, 210, "沧溟州", "东海群岛 · 季风航路", "#d9e9df")
    label(draw, 600, 980, "云岫州", "高原 · 峡谷 · 百脉河源")
    label(draw, 1110, 1050, "苍梧州", "雨林 · 泽地 · 分汊水网")
    label(draw, 1720, 1120, "归潮州", "潮海 · 群岛 · 归墟外环")

    # Compact instruction legend for the person drawing in YiZhi.
    box = (60, 1480, 650, 1725)
    draw.rounded_rectangle(tuple(v * S for v in box), radius=16 * S, fill="#244d50d9", outline="#ddcfab", width=2 * S)
    draw.text(sc((88, 1515)), "手绘落笔顺序", font=font(23, True), fill="#f7e1a6")
    legend = [
        ("山系", "先画长山脊：北境—云岫—苍梧"),
        ("水系", "再画河源、湖盆与入海分汊"),
        ("大地貌", "最后铺雪原、荒盆、平原、雨林、潮海"),
        ("留白", "城镇/宗门只放少量锚点，别填满总览"),
    ]
    y = 1555
    for key, value in legend:
        draw.ellipse((round(88 * S), round((y - 5) * S), round(100 * S), round((y + 7) * S)), fill="#f0d58e")
        draw.text(sc((116, y)), f"{key}  {value}", font=font(17), fill="#e9eadc")
        y += 38

    draw.text(sc((80, 62)), "昭明界 · 九州地形分布指导图", font=font(40, True), fill="#263b39", stroke_width=2 * S, stroke_fill="#b6d0c9")
    draw.text(sc((82, 105)), "24000×18000 km 核心框架｜4:3 手绘源稿｜地貌边界优先于势力边界", font=font(19), fill="#234341")
    draw.text(sc((1750, 1680)), "北 ↑   5×5 仅为资源加载分块，不是州界", font=font(17), fill="#e5efe5")

    image = image.convert("RGB").resize((2400, 1800), Image.Resampling.LANCZOS)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUT, format="PNG", optimize=True)
    print(OUT)


if __name__ == "__main__":
    main()
