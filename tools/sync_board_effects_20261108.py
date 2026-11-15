"""同步现行五本秘籍、二十招和二十化法的资源文案与词典，不改变数值字段。"""
from pathlib import Path
from layout_five_miji_20261108 import arrange as arrange_readable_book
from layout_five_miji_20261108 import plain as readable_plain
import html
import json
import math
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PLAN = "战斗与构筑/人类招式心法全效果迁移_20261108.md"
BALANCE_PLAN = "战斗与构筑/人类招式首轮平衡与名称标注_20261108.md"
BOOKS = ["玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章"]
NUM = ["一", "二", "三", "四"]
ATTRS = {"physique": "体魄", "intelligence": "灵慧", "soul": "神魂", "body_proficiency": "体道造诣", "magic_proficiency": "术法造诣", "sword_proficiency": "剑道造诣", "max_health_percent": "最大生命", "physical_damage_percent": "物理伤害", "precision_percent": "精准", "critical_percent": "会心", "qi_blood_cap": "气血上限", "cloud_breath_mana_cap": "云息法力上限"}
CHANGED = set()


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def write(path, text):
    path = ROOT / path
    if path.exists() and path.read_text(encoding="utf-8-sig") == text:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")
    CHANGED.add(str(path.relative_to(ROOT)).replace("\\", "/"))


def field(text, key, default=None):
    match = re.search(r"^" + re.escape(key) + r" = (.*)$", text, re.M)
    if not match:
        return default
    value = match[1]
    if value.startswith(('"', '{')):
        return json.loads(value)
    if value.startswith("Vector2i("):
        return tuple(map(int, re.findall(r"-?\d+", value.split("(", 1)[1])))
    if value.startswith("Array[String]"):
        return json.loads(re.search(r"\(\s*(\[.*\])\s*\)", value)[1])
    try:
        return float(value) if "." in value else int(value)
    except ValueError:
        return value


def replace_field(text, key, value):
    line = key + " = " + json.dumps(value, ensure_ascii=False)
    if re.search(r"^" + re.escape(key) + r" = ", text, re.M):
        return re.sub(r"^" + re.escape(key) + r" = .*", lambda _: line, text, flags=re.M)
    return text.rstrip() + "\n" + line + "\n"


def inline(text):
    """将现存字母公式放回对应结果旁，不改表达式。"""
    text = text.replace("计算公式：", "").replace("以D、E概率", "以D概率、E概率")
    # 每次移动后重新定位；不能沿用修改前的字符偏移。
    for _ in range(8):
        candidates = list(re.finditer(r"(?<![A-Za-z（])([A-E])\s*=\s*([^。；\n]+)[。；]?", text))
        movable = None
        for match in candidates:
            result = re.search(r"(?<![A-Za-z])" + match[1] + r"(?:点|层|概率|概率判定|(?=（))", text[:match.start()])
            if result:
                movable = (match, result)
                break
        if movable is None:
            break
        match, result = movable
        text = text[:result.end()] + f"（{match[1]}={match[2].strip()}）" + text[result.end():match.start()] + text[match.end():]
    return re.sub(r"\n{3,}", "\n\n", text).strip()


def static_text(text):
    # 玩家图使用原有详细公式；不向图中代入角色示例。
    return inline(text).replace("带剑关键词的攻伐技能", "带剑关键词的攻击招式")


def skill_info(path):
    text = read(path)
    tags = field(text, "tags", [])
    if "应对" in field(text, "keywords", []) and "应对" not in tags:
        tags.append("应对")
    name = field(text, "skill_name")
    display_name = name + f"*{field(text, 'board_max_placements', 1)}"
    battle_limit = field(text, "uses_per_battle", 0) or field(text, "max_charges", 0)
    if battle_limit > 0:
        display_name += f" 限{battle_limit}"
    w, h = field(text, "board_size", (1, 1))
    lines = [display_name, "关键词：" + ("、".join(tags) or "无"),
             f"耗费：体力{field(text, 'energy_cost', 1)}、法力{field(text, 'mana_cost', 0)}",
             f"占格：{w}×{h}",
             "目标：" + ("自身" if field(text, "target_type", 0) == 0 else "随机一名合法敌人")]
    if field(text, "skill_type", 0) == 0 and field(text, "attack_style", ""):
        lines.append("出手方式：" + ("远程" if field(text, "attack_style") == "ranged" else "近身"))
    lines += ["", "效果：" + static_text(field(text, "description", ""))]
    if field(text, "uses_per_round", 0) > 0:
        lines.append(f"使用次数：每回合{field(text, 'uses_per_round')}次；同招共享。")
    if field(text, "cooldown_rounds", 0) > 0:
        lines.append(f"冷却：{field(text, 'cooldown_rounds')}回合；同招共享。")
    if name == "寄念":
        lines += ["当前版本暂未启用。"]
    return "\n".join(lines)


def diagram_page(parent, name, text, title="", ident="player", width=1400):
    page = ET.SubElement(parent, "diagram", {"name": name, "id": ident})
    # 足够的行高用于长公式，不以缩小字号挤入旧框。
    lines = sum(max(1, math.ceil(len(line) / (width / 22))) for line in text.splitlines())
    height = max(700, lines * 32 + 140)
    graph = ET.SubElement(page, "mxGraphModel", {"page": "1", "pageWidth": str(width), "pageHeight": str(height + 180), "grid": "1", "gridSize": "10"})
    root = ET.SubElement(graph, "root")
    ET.SubElement(root, "mxCell", {"id": "0"})
    ET.SubElement(root, "mxCell", {"id": "1", "parent": "0"})
    cell = ET.SubElement(root, "mxCell", {"id": "body", "parent": "1", "vertex": "1", "value": html.escape(text).replace("\n", "<br>"), "style": "rounded=1;whiteSpace=wrap;html=1;fillColor=#ebe6d6;strokeColor=#857c61;fontSize=20;align=left;verticalAlign=top;spacing=24;"})
    ET.SubElement(cell, "mxGeometry", {"as": "geometry", "x": "50", "y": "50", "width": str(width - 100), "height": str(height)})
    return page


def xml_write(path, root):
    ET.indent(root, space="  ")
    write(path, '<?xml version="1.0" encoding="utf-8"?>\n' + ET.tostring(root, encoding="unicode") + "\n")


def update_card(path, text, source, icon_source=None, display_title=None):
    path = str(path)
    if (ROOT / path).exists():
        root = ET.fromstring(read(path))
        pages = root.findall("diagram")
        page = pages[0]
        # 保留现有源图标及主图样式，清除旧说明页，重建单一策划说明。
        for stale in pages[1:]:
            root.remove(stale)
        graph = page.find("mxGraphModel")
        container = graph.find("root")
        body = next((cell for cell in container.findall("mxCell") if cell.get("id") in ("body", "main")), None)
        if body is None:
            # 旧卡的正文没有统一ID；以现行卡图为模板。
            if icon_source:
                root = ET.fromstring(read(icon_source))
                page = root.findall("diagram")[0]
                for stale in root.findall("diagram")[1:]:
                    root.remove(stale)
                graph = page.find("mxGraphModel")
                container = graph.find("root")
                body = container.find("mxCell[@id='body']")
            else:
                root = ET.Element("mxfile", {"host": "app.diagrams.net", "agent": "Codex"})
                page = diagram_page(root, "心法", text)
                graph = page.find("mxGraphModel")
                container = graph.find("root")
                body = container.find("mxCell[@id='body']")
        for cell in list(container.findall("mxCell")):
            if cell.get("id") not in ("0", "1", "title", "icon", body.get("id")):
                container.remove(cell)
        body.set("value", html.escape(text).replace("\n", "<br>"))
        if display_title is not None and container.find("mxCell[@id='title']") is not None:
            container.find("mxCell[@id='title']").set("value", html.escape(display_title))
        geo = body.find("mxGeometry")
        width = float(geo.get("width", "850"))
        lines = sum(max(1, math.ceil(len(line) / (width / 20))) for line in text.splitlines())
        height = max(780, lines * 30 + 70)
        geo.set("height", str(height))
        graph.set("pageHeight", str(math.ceil(float(geo.get("y", "100")) + height + 70)))
    else:
        root = ET.Element("mxfile", {"host": "app.diagrams.net", "agent": "Codex"})
        diagram_page(root, "心法", text)
    diagram_page(root, "策划说明", f"现行规则：docs/plans/{PLAN}\n平衡与名称标注：docs/plans/{BALANCE_PLAN}\n实施与时序：docs/plans/战斗与构筑/人类招式盘实施与验收_20261107.md\n资源：{source}\n主图仅为玩家详细文案。\n名称*N为可摆份数；限N只表示整场共享次数，每回合次数另写。\n首次扫到整份检查，失败本轮不重试，下一共同回合重新点亮。\n同招共享原限次和冷却；每项化法只影响实际指向的一份。\n寄念目标、妖兽布阵及新器物参战仍按各自专题，未在本图补造。", ident="notes")
    xml_write(path, root)


def parse_book(name):
    path = f"resources/gongfa/{name}.tres"
    text = read(path)
    exts = re.findall(r'\[ext_resource[^\n]*path="([^"]+)" id="([^"]+)"\]', text)
    by_id = {ident: path.removeprefix("res://") for path, ident in exts}
    chapters = []
    for match in re.finditer(r'\[sub_resource[^\n]+\]\n(.*?)(?=\n\[|\Z)', text, re.S):
        chunk = match[1]
        if field(chunk, "chapter_id") is None:
            continue
        learned = re.search(r"^learned_active_skills = (.*)$", chunk, re.M)
        ids = re.findall(r'ExtResource\("([^"]+)"\)', learned[1]) if learned else []
        chapters.append((chunk, [by_id[ident] for ident in ids]))
    heart_id = re.search(r'^heart_method_skill = ExtResource\("([^"]+)"\)', text, re.M)[1]
    return path, text, sorted(chapters, key=lambda entry: field(entry[0], "order")), by_id[heart_id]


def attr_list(values, compare=False):
    return "、".join(f"{ATTRS.get(k,k)}{'≥' if compare else '+'}{v:g}{'%' if k.endswith('_percent') else ''}" for k,v in values.items()) or "无"


def synchronize():
    manifest = json.loads(read("docs/美术规范/人类招式占格审阅_20261107/几何检查结果.json"))
    skills = [entry["resource"].removeprefix("res://") for entry in manifest["skills"]]
    by_name = {field(read(path), "skill_name"): path for path in skills}
    # 该条件是既有真实结算条件，避免把护盾承伤误写成没有造成伤害。
    for path in skills:
        text = read(path)
        description = field(text, "description", "").replace("首段未造成伤害", "首段未造成生命伤害")
        write(path, replace_field(text, "description", description))

    effects = {}
    for file in ["csharp/gongfa/GongfaSkillRewriteRuntime.cs", "csharp/gongfa/GongfaSkillRewriteRuntime.SevenBooks.cs"]:
        for ident, desc in re.findall(r'\["([^"]+)"\]\s*=\s*new\("[^"]+",\s*"[^"]+",\s*("(?:[^"\\]|\\.)*")', read(file)):
            effects[ident] = json.loads(desc)
    heart_versions = {}
    for name in BOOKS:
        path, book, chapters, heart_path = parse_book(name)
        heart_name = field(read(heart_path), "skill_name")
        versions = []
        for i, (chunk, learned) in enumerate(chapters):
            desc = field(chunk, "heart_method_description", "")
            if re.search(r"沿用第[一二三四1234]卷", desc):
                desc = versions[-1]
            if not desc.startswith("心法被动："):
                desc = "心法被动：" + heart_name + "\n" + desc
            desc = static_text(desc)
            if name == "太和感物养元宝册" and i == 3:
                desc = "心法被动：太和感物养元\n效果：每回合首次触发自身应对后，获得气血2层；本回合后续每次触发自身应对后，获得气血1层，均受气血上限限制。"
            if name == "舒卷天章":
                formula = "round(2 + 体魄×0.20 + 剑道造诣×0.30" + (" + max(本次行动开始时剑意层数−6,0)" if i >= 1 else "") + ")"
                count = "追加攻击；本次行动开始时剑意达到10层则追加2段，否则追加1段" if i == 3 else "追加1段攻击"
                desc = f"心法被动：流云剑势\n关键词：剑、剑意\n效果：本次行动开始时剑意达到6层，带剑关键词的攻击招式首段命中后，在原招式攻击段结算完毕时{count}；每段对原目标造成A点（A={formula}）物理伤害。云岚三叠按自身追加段数结算，不另加心法攻击段。\n主修额外效果：触发所需剑意层数减少1层。"
            if "关键词：" not in desc:
                tags = "、".join(field(read(heart_path), "tags", [])) or "无"
                desc = desc.replace("\n效果：", f"\n关键词：{tags}\n效果：", 1)
            versions.append(desc)
            new_chunk = replace_field(chunk, "heart_method_description", desc)
            if field(chunk, "heart_method_upgrade_description") is not None:
                new_chunk = replace_field(new_chunk, "heart_method_upgrade_description", desc)
            book = book.replace(chunk, new_chunk)
        if name == "玄枢含章经":
            book = book.replace("每次周天完成后，自动施展技能槽10中的技能。", "完成周天时，使周天指示所指招式额外施放；每回合最多一次。")
            book = book.replace("以周天为枢机，使技能槽10中的技能自动施展。", "以周天为枢机，使周天指示所指招式额外施放。")
        write(path, book)
        base = read(heart_path)
        write(heart_path, replace_field(base, "description", versions[0]))
        heart_versions[name] = versions

    # 重写资源本身也保留完整的原效果与追加，不只靠某一处面板字符串替换。
    for name in BOOKS:
        for path in (ROOT / f"resources/activeskills/{name}/化法").glob("*.tres"):
            rel = str(path.relative_to(ROOT))
            text = read(rel)
            skill_name = field(text, "skill_name")
            ids = field(text, "gongfa_rewrite_effect_ids", [])
            if skill_name not in by_name or not ids:
                continue
            description = field(read(by_name[skill_name]), "description", "")
            description += "\n" + "\n".join(effects[ident] for ident in ids)
            write(rel, replace_field(text, "description", description))

    for path in skills:
        name = field(read(path), "skill_name")
        card = f"docs/卡牌词典/招式盘/{name}.drawio"
        info = skill_info(path)
        update_card(card, info, "res://" + path, display_title=info.splitlines()[0])
        if name in ("灵击", "云帔"):
            learning = "参悟条件：灵慧≥8、术法造诣≥6，无前置，无额外参悟费用。学会后自行配入招式盘。"
            note = "纳炁面独立节点；资源：res://" + path + "。"
            if name == "云帔":
                note += "旧角色明确已学事实保留；玄枢第二卷保留化法，不再为新角色重复授予云帔。"
            write(f"docs/卡牌词典/基础技能节点/{name}.md", "# " + info.splitlines()[0] + "\n\n" + learning
                  + "\n\n" + "\n\n".join(line for line in info.splitlines()[1:] if line.strip()) + f"\n\n[详细图]({name}.drawio)。\n\n## 策划说明\n\n" + note
                  + f"数值与名称标注见[当前方案](../../plans/{BALANCE_PLAN})。\n")
        # 所有旧入口指向同一份效果，避免从个人节点或旧技能目录读到另一套规则。
        for folder in ["docs/卡牌词典/基础技能节点", "docs/卡牌词典/公共基础技能", "docs/卡牌词典/基础通用"]:
            old = f"{folder}/{name}.drawio"
            if (ROOT / old).exists():
                update_card(old, info, "res://" + path, card, display_title=info.splitlines()[0])
        for book in BOOKS:
            old = f"docs/卡牌词典/秘籍技能/{book}/{name}.drawio"
            if (ROOT / old).exists():
                update_card(old, info, "res://" + path, card, display_title=info.splitlines()[0])

    for name in BOOKS:
        path, book, chapters, heart_path = parse_book(name)
        versions = heart_versions[name]
        heart_name = field(read(heart_path), "skill_name")
        latest_full = 0
        sections = [f"# {name}", f"> 当前招式盘全效果版，2026-11-08。现行规则与迁移边界见[全效果迁移](../../plans/{PLAN})；数值和名称标注见[首轮平衡](../../plans/{BALANCE_PLAN})。学习门槛、永久奖励及修持数值保留。", "可主修、可辅修；主修范围至云息十层。"]
        primary = ET.fromstring(read(f"docs/秘籍词典/drawio/初步设计好的/{name}.drawio"))
        first = primary.findall("diagram")[0]
        for old in primary.findall("diagram")[1:]:
            primary.remove(old)
        cells = {cell.get("id"): cell for cell in first.findall(".//mxCell")}
        container = first.find("mxGraphModel/root")
        for cell in list(container.findall("mxCell[@vertex='1']")):
            ident = cell.get("id", "")
            if ident not in ("title", "overview", "summary") and not re.fullmatch(r"c[1-4](.?:_cards|_left|_right)?", ident):
                container.remove(cell)
        y = 370
        heart_card = []
        for i, (chunk, learned) in enumerate(chapters):
            chapter_name = field(chunk, "chapter_name")
            display_heart = versions[i]
            if i > 0 and versions[i] == versions[i - 1]:
                display_heart = f"心法被动：{heart_name}（沿用第{NUM[latest_full]}卷，无新增效果）"
            else:
                latest_full = i
            heart_card.append(f"第{NUM[i]}卷：{chapter_name}\n{display_heart}")
            attr = attr_list(field(chunk, "required_attributes", {}), True)
            perm = attr_list(field(chunk, "learn_permanent_bonuses", {}))
            temper = attr_list(field(chunk, "combat_tempering_bonuses", {}))
            previous = field(chapters[i - 1][0], "chapter_name") if i else "无"
            sections += [f"## 第{NUM[i]}卷：{chapter_name}", f"累计研读：{field(chunk,'required_study_progress'):g}；境界：{field(chunk,'required_realm','无境界限制')}；前置：{previous}。", f"属性条件：{attr}。", f"永久所得：{perm}。修持（仅战斗内）：{temper}。", display_heart]
            learned_text = [skill_info(item) for item in learned]
            for item, info in zip(learned, learned_text):
                sections += ["### " + info.splitlines()[0], "\n".join(info.splitlines()[1:])]
            # 本次唯一化法对应已批准表，不从任意其他历史资源推断。
            right = cells[f"c{i+1}_right"]
            raw = readable_plain(right.get("value", ""))
            target = re.search(r"适用(?:招式|范围)：([^\n]+)", raw)[1].strip()
            effect_index = raw.index("化法追加：")
            addition = static_text(raw[effect_index:])
            # 同步有歧义的生命伤害条件。
            addition = addition.replace("首段未造成伤害", "首段未造成生命伤害")
            huafa_text = f"化法*1\n适用范围：{target}\n占格：1×1\n\n{addition}"
            sections += ["### 化法", huafa_text.removeprefix("化法*1\n"), f"[详细图](../../卡牌词典/招式盘/化法/{name}·{chapter_name}.drawio)"]
            update_card(f"docs/卡牌词典/招式盘/化法/{name}·{chapter_name}.drawio", f"{name}·{chapter_name}*1\n{huafa_text}", "res://" + path, display_title=f"{name}·{chapter_name}*1")
            for old in (ROOT / f"docs/卡牌词典/秘籍技能/{name}/化法").glob("*.drawio"):
                old_root = ET.fromstring(old.read_text(encoding="utf-8"))
                title_text = " ".join(cell.get("value", "") for cell in old_root.findall("diagram")[0].findall(".//mxCell")
                                      if cell.get("id") == "title")
                if f"{name}·{chapter_name}" in title_text:
                    update_card(str(old.relative_to(ROOT)), f"{name}·{chapter_name}*1\n{huafa_text}", "res://" + path, display_title=f"{name}·{chapter_name}*1")
            cells[f"c{i+1}_cards"].set("value", html.escape(display_heart + "\n\n" + ("习得：\n" + "\n\n".join(learned_text) if learned else "习得：无新增招式。")).replace("\n", "<br>"))
            right.set("value", html.escape(huafa_text).replace("\n", "<br>"))
            chapter_text = readable_plain(cells[f"c{i+1}"].get("value", ""))
            for item, info in zip(learned, learned_text):
                skill_name = field(read(item), "skill_name")
                chapter_text = re.sub(r"(?<=习得：)" + re.escape(skill_name) + r"(?:\*\d+(?: 限\d+)?)?", info.splitlines()[0], chapter_text)
            cells[f"c{i+1}"].set("value", html.escape(chapter_text).replace("\n", "<br>"))
            row_height = 520
            for suffix, x, width in [("_cards", 40, 740), ("_left", 820, 260), ("", 1200, 340), ("_right", 1640, 560)]:
                cell = cells[f"c{i+1}{suffix}"]
                geo = cell.find("mxGeometry")
                plain = re.sub(r"<br\s*/?>", "\n", cell.get("value", ""))
                plain = html.unescape(re.sub(r"<[^>]+>", "", plain))
                lines = sum(max(1, math.ceil(len(line) / (width / 14))) for line in plain.splitlines())
                height = max(160 if suffix == "_left" else 430, lines * 21 + 65)
                geo.set("x", str(x)); geo.set("y", str(y)); geo.set("width", str(width)); geo.set("height", str(height))
                row_height = max(row_height, height)
            y += row_height + 100
        for edge in first.findall(".//mxCell[@edge='1']/mxGeometry"):
            for points in list(edge):
                edge.remove(points)
        for ident, x, width in [("title", 1090, 560), ("overview", 1090, 560), ("summary", 40, 740)]:
            if ident in cells:
                g = cells[ident].find("mxGeometry"); g.set("x", str(x)); g.set("width", str(width))
                if ident == "summary":
                    g.set("y", "140"); g.set("height", "160")
                    cells[ident].set("value", html.escape(field(book.split("[resource]")[-1], "description", "")))
        first.find("mxGraphModel").set("pageWidth", "2280")
        first.find("mxGraphModel").set("pageHeight", str(y + 100))
        diagram_page(primary, "策划说明", f"现行主案：docs/plans/{PLAN}\n首轮平衡：docs/plans/{BALANCE_PLAN}\n资源：res://{path}\n名称*N为可摆份数；限N只表示整场共享次数，每回合次数另写。\n首次扫到整份检查，失败本轮不重试；应对扫到待命，下一共同回合重新点亮。\n取消心法自行自动应对；数值和费用按首轮平衡案，状态、修持与学习奖励保留。\n化法按实际邻接指向生效，不叠加旧十槽绑定；同招各份共享原限次和冷却。\n周天每共同回合额外一次，复命另占原招普通机会；两次付款连续预览。\n舒卷逐卷成长按2026-11-08用户裁定：一卷基础，二卷超额剑意增伤，四卷满剑意两段。\n寄念新目标、妖兽布阵和新器物参战尚未定义，继续各自原边界。", ident="notes")
        arrange_readable_book(primary)
        xml_write(f"docs/秘籍词典/drawio/初步设计好的/{name}.drawio", primary)
        write(f"docs/秘籍词典/md/{name}.md", "\n\n".join(sections) + "\n")
        heart_file = "斫基损契" if name == "斫基损契玄经" else "心法"
        update_card(f"docs/卡牌词典/秘籍技能/{name}/{heart_file}.drawio", "\n\n".join(heart_card), "res://" + heart_path)
    return skills


if __name__ == "__main__":
    synchronize()
    print(json.dumps({"changed": len(CHANGED), "files": sorted(CHANGED)}, ensure_ascii=False, indent=2))
