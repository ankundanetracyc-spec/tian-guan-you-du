"""五本现行秘籍图谱的阅读排版；保留全部文字、节点身份与连线。"""
from pathlib import Path
from html import escape, unescape
from html.parser import HTMLParser
from functools import lru_cache
import json
import math
import re
import xml.etree.ElementTree as ET
from PIL import ImageFont

ROOT = Path(__file__).resolve().parents[1]
BOOKS = ["玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章"]
FONT_FAMILY = "Microsoft YaHei"
BODY = 20
PADDING = 22
LINE_HEIGHT = 1.40
PARAGRAPH_GAP = 12


class PlainText(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts = []

    def handle_starttag(self, tag, attrs):
        if tag == "br":
            self.parts.append("\n")
        elif tag in ("div", "p") and self.parts and not self.parts[-1].endswith("\n"):
            self.parts.append("\n")

    def handle_data(self, data):
        self.parts.append(data)


def plain(value):
    parser = PlainText()
    parser.feed(value)
    return "\n".join(line.strip() for line in "".join(parser.parts).splitlines()).strip()


def words(value):
    return re.sub(r"\s+", "", plain(value))


@lru_cache(maxsize=32)
def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", size)


def styled_lines(text):
    lines = text.splitlines()
    for i, line in enumerate(lines):
        heading = i == 0 or line.startswith("心法被动：") or line == "习得："
        yield line, heading


def rich_text(text, size=BODY, centered=False):
    blocks = []
    for line, heading in styled_lines(text):
        if not line:
            blocks.append(f'<div style="height:{PARAGRAPH_GAP}px;line-height:{PARAGRAPH_GAP}px;"><br></div>')
        else:
            weight = "700" if heading else "400"
            blocks.append(f'<div style="font-weight:{weight};">{escape(line)}</div>')
    align = "center" if centered else "left"
    return f'<div style="font-family:{FONT_FAMILY};font-size:{size}px;line-height:{LINE_HEIGHT};text-align:{align};overflow-wrap:anywhere;">' + "".join(blocks) + "</div>"


def measure(text, width, size=BODY):
    available = max(40, width - PADDING * 2 - 10)
    total = 0
    for line, heading in styled_lines(text):
        if not line:
            total += PARAGRAPH_GAP
            continue
        face = font(size, heading)
        # 和浏览器相同，中文及长公式允许在字间折行；额外保留一小段行宽。
        current = 0
        rows = 1
        for char in line:
            advance = face.getlength(char)
            if current > 0 and current + advance > available:
                rows += 1
                current = 0
            current += advance
        total += rows * math.ceil(size * LINE_HEIGHT)
    return total


def natural_width(text, maximum, minimum=360, size=BODY):
    longest = max((font(size, heading).getlength(line) for line, heading in styled_lines(text)), default=0)
    return min(maximum, max(minimum, math.ceil((longest + PADDING * 2 + 24) / 10) * 10))


def style(cell, updates):
    values = {}
    for part in cell.get("style", "").split(";"):
        if not part:
            continue
        key, _, value = part.partition("=")
        values[key] = value
    values.update({key: str(value) for key, value in updates.items()})
    cell.set("style", ";".join(key + "=" + value for key, value in values.items()) + ";")


def place(cell, x, y, width, text, size=BODY, centered=False):
    height = math.ceil((measure(text, width, size) + PADDING * 2 + 10) / 10) * 10
    height = max(76, height)
    cell.set("value", rich_text(text, size, centered))
    style(cell, {"fontFamily": FONT_FAMILY, "fontSize": size, "fontColor": "#242b2a",
                 "align": "center" if centered else "left", "verticalAlign": "middle",
                 "spacing": PADDING, "spacingTop": 0, "spacingBottom": 0,
                 "spacingLeft": 0, "spacingRight": 0, "whiteSpace": "wrap", "html": 1,
                 "overflow": "hidden", "rounded": 1, "arcSize": 8})
    geo = cell.find("mxGeometry")
    for key, value in {"x": x, "y": y, "width": width, "height": height}.items():
        geo.set(key, str(value))
    return height


def arrange(primary):
    before = {(page.get("id"), cell.get("id")): words(cell.get("value", ""))
              for page in primary.findall("diagram") for cell in page.findall(".//mxCell")}
    first = primary.findall("diagram")[0]
    graph = first.find("mxGraphModel")
    cells = {cell.get("id"): cell for cell in first.findall(".//mxCell")}
    texts = {key: plain(cell.get("value", "")) for key, cell in cells.items()}
    old_area = sum(float(cell.find("mxGeometry").get("width", "0")) * float(cell.find("mxGeometry").get("height", "0"))
                   for cell in first.findall(".//mxCell[@vertex='1']"))
    # 仍是中央卷章主链、左侧心法与修持、右侧化法，各框独立适配内容。
    chapter_x, chapter_w = 1170, 470
    chapter_center = chapter_x + chapter_w / 2
    title_w, overview_w = 640, 680
    title_h = place(cells["title"], chapter_center - title_w / 2, 30, title_w, texts["title"], 34, True)
    overview_y = 30 + title_h + 22
    overview_h = place(cells["overview"], chapter_center - overview_w / 2, overview_y, overview_w, texts["overview"], BODY, True)
    summary_w = natural_width(texts["summary"], 760, 420)
    summary_h = place(cells["summary"], 36 + (760 - summary_w) / 2, 0, summary_w, texts["summary"], BODY, True)
    cells["summary"].find("mxGeometry").set("y", str(overview_y + (overview_h - summary_h) / 2))
    row_y = overview_y + overview_h + 64
    panels = []
    for chapter in range(1, 5):
        prefix = f"c{chapter}"
        heart_w = natural_width(texts[prefix + "_cards"], 760, 420)
        rewrite_w = natural_width(texts[prefix + "_right"], 600, 460)
        columns = [("_cards", 36 + 760 - heart_w, heart_w),
                   ("_left", 836, 270), ("", chapter_x, chapter_w),
                   ("_right", 1724, rewrite_w)]
        heights = {}
        for suffix, x, width in columns:
            key = prefix + suffix
            heights[key] = place(cells[key], x, 0, width, texts[key], BODY, suffix == "_left")
        row_height = max(heights.values())
        for suffix, _, _ in columns:
            key = prefix + suffix
            cell = cells[key]
            cell.find("mxGeometry").set("y", str(row_y + (row_height - heights[key]) / 2))
            panels.append({"id": key, "height": heights[key], "text_height": measure(texts[key], float(cell.find("mxGeometry").get("width")))})
        row_y += row_height + 64
    graph.set("pageWidth", "2360")
    graph.set("pageHeight", str(math.ceil(row_y + 10)))
    # 标签放在线中，不再固定贴在分支末端；增大箭头和分支字级。
    for edge in first.findall(".//mxCell[@edge='1']"):
        style(edge, {"fontFamily": FONT_FAMILY, "fontSize": 17, "fontColor": "#414745",
                     "labelBackgroundColor": "#ffffff", "spacing": 6, "strokeWidth": 1.5,
                     "endSize": 9, "labelPosition": "center", "verticalLabelPosition": "middle"})
        geo = edge.find("mxGeometry")
        if geo is not None:
            geo.set("x", "0"); geo.set("y", "0")
            for child in list(geo):
                geo.remove(child)
        target_id = edge.get("target", "")
        if target_id.endswith("_cards") and geo is not None:
            source = cells[edge.get("source")].find("mxGeometry")
            target = cells[target_id].find("mxGeometry")
            temper = cells[target_id.replace("_cards", "_left")].find("mxGeometry")
            sx, sy, sh = (float(source.get(key)) for key in ["x", "y", "height"])
            ty, th = (float(target.get(key)) for key in ["y", "height"])
            lane_y = max(sy + sh, float(temper.get("y")) + float(temper.get("height"))) + 28
            style(edge, {"edgeStyle": "orthogonalEdgeStyle", "rounded": 1,
                         "exitX": 0, "exitY": .85, "entryX": 1, "entryY": .5,
                         "exitDx": 0, "exitDy": 0, "entryDx": 0, "entryDy": 0})
            points = ET.SubElement(geo, "Array", {"as": "points"})
            for px, py in [(sx - 24, sy + sh * .85), (sx - 24, lane_y), (816, lane_y), (816, ty + th / 2)]:
                ET.SubElement(points, "mxPoint", {"x": str(px), "y": str(py)})
        elif target_id.endswith("_left"):
            style(edge, {"exitX": 0, "exitY": .5, "entryX": 1, "entryY": .5})
        elif target_id.endswith("_right"):
            style(edge, {"exitX": 1, "exitY": .5, "entryX": 0, "entryY": .5})
        else:
            style(edge, {"exitX": .5, "exitY": 1, "entryX": .5, "entryY": 0})
    for page in primary.findall("diagram")[1:]:
        note_graph = page.find("mxGraphModel")
        bottom = 0
        for cell in page.findall(".//mxCell[@vertex='1']"):
            text = plain(cell.get("value", ""))
            height = place(cell, 40, 40, 1600, text, BODY)
            bottom = max(bottom, 40 + height)
        note_graph.set("pageWidth", "1680")
        note_graph.set("pageHeight", str(bottom + 40))
    after = {(page.get("id"), cell.get("id")): words(cell.get("value", ""))
             for page in primary.findall("diagram") for cell in page.findall(".//mxCell")}
    assert before == after, "排版不得更改文字、公式、节点或连线名称。"
    rectangles = []
    for cell in first.findall(".//mxCell[@vertex='1']"):
        geo = cell.find("mxGeometry")
        rect = [float(geo.get(key, "0")) for key in ["x", "y", "width", "height"]]
        assert rect[0] >= 0 and rect[1] >= 0, cell.get("id")
        rectangles.append((cell.get("id"), *rect))
    for i, (name, x, y, w, h) in enumerate(rectangles):
        for other, a, b, c, d in rectangles[i + 1:]:
            assert not (x < a + c and a < x + w and y < b + d and b < y + h), (name, other)
    new_area = sum(rect[3] * rect[4] for rect in rectangles)
    return {"body_font": BODY, "title_font": 34, "previous_panel_area": round(old_area),
            "panel_area": round(new_area), "panel_area_reduction": round(1 - new_area / old_area, 3),
            "page_size": [2360, math.ceil(row_y + 10)], "content_unchanged": True, "panels": panels}


def main():
    out = ROOT / "output/miji_layout_20261108"
    backup = out / "before"
    backup.mkdir(parents=True, exist_ok=True)
    records = {}
    for book in BOOKS:
        path = ROOT / f"docs/秘籍词典/drawio/初步设计好的/{book}.drawio"
        if not (backup / path.name).exists():
            (backup / path.name).write_bytes(path.read_bytes())
        tree = ET.parse(path)
        records[book] = arrange(tree.getroot())
        initial = ET.parse(backup / path.name).getroot().findall("diagram")[0]
        baseline_area = sum(float(cell.find("mxGeometry").get("width", "0")) * float(cell.find("mxGeometry").get("height", "0"))
                            for cell in initial.findall(".//mxCell[@vertex='1']"))
        records[book]["previous_panel_area"] = round(baseline_area)
        records[book]["panel_area_reduction"] = round(1 - records[book]["panel_area"] / baseline_area, 3)
        ET.indent(tree, space="  ")
        tree.write(path, encoding="utf-8", xml_declaration=True)
    (out / "layout_report.json").write_text(json.dumps(records, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for book, record in records.items():
        print(book, "body", record["body_font"], "panel area reduction", f'{record["panel_area_reduction"]:.0%}', "page", record["page_size"])


if __name__ == "__main__":
    main()
