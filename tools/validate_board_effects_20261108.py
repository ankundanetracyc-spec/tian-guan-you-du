"""检查全效果迁移的字段保全、图谱一致性与主图边界。"""
from pathlib import Path
import hashlib
import html
import json
import re
import xml.etree.ElementTree as ET
from layout_five_miji_20261108 import words

ROOT = Path(__file__).resolve().parents[1]
checks = []


def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)


def read(p):
    return (ROOT / p).read_text(encoding="utf-8-sig")


def rule_hash(text):
    lines = [line for line in text.splitlines() if not re.match(r"^(description|heart_method_description|heart_method_upgrade_description) =", line)]
    return hashlib.sha256("\n".join(lines).encode()).hexdigest()


def text_of(page):
    return "\n".join(html.unescape(re.sub(r"<[^>]+>", " ", cell.get("value", ""))) for cell in page.findall(".//mxCell"))


def run():
    baseline = json.loads(read("docs/plans/战斗与构筑/人类招式效果字段基线_20261108.json"))["resources"]
    balance = json.loads(read("docs/plans/战斗与构筑/人类招式首轮平衡字段变更_20261108.json"))["resources"]
    check(len(baseline) == 50, "20招、5心法、20化法、5秘籍共50份资源")
    check(len(balance) == 12, "六招及六份化法副本的授权数值差异单列")
    for path, digest in baseline.items():
        text = read(path)
        if path in balance:
            entry = balance[path]
            check(entry["before_hash"] == digest, path + "仍引用原始迁移基线")
            check(rule_hash(text) == entry["after_hash"], path + "符合当前平衡指纹")
            for key, change in entry["fields"].items():
                match = re.search(r"^" + re.escape(key) + r" = (.*)$", text, re.M)
                check(match and match[1] == change["after"], path + ":" + key + "符合平衡定稿")
                text = re.sub(r"^" + re.escape(key) + r" = .*", lambda _: key + " = " + change["before"], text, flags=re.M)
            check(rule_hash(text) == digest, path + "除逐项授权字段及文案外全部保全")
        else:
            check(rule_hash(text) == digest, path + "费用、公式、目标、状态、次数、学习、占格及引用全部保全")

    books = ["玄枢含章经", "斫基损契玄经", "百骸衍枝化生宝册", "太和感物养元宝册", "舒卷天章"]
    cards = list((ROOT / "docs/卡牌词典/招式盘").rglob("*.drawio"))
    check(len(cards) == 40, "二十招与二十化法图齐全")
    extras = []
    for book in books:
        extras += list((ROOT / f"docs/卡牌词典/秘籍技能/{book}").rglob("*.drawio"))
    extras += list((ROOT / "docs/卡牌词典/基础技能节点").glob("*.drawio"))
    primary = [ROOT / f"docs/秘籍词典/drawio/初步设计好的/{book}.drawio" for book in books]
    for path in cards + extras + primary:
        tree = ET.parse(path).getroot()
        pages = tree.findall("diagram")
        check(len(pages) == 2 and pages[1].get("name") == "策划说明", str(path.relative_to(ROOT)) + "单一内部说明页")
        player = text_of(pages[0])
        check(not any(term in player for term in ["技能槽10", "行动队列", "当前槽", "队首", "攻伐", "res://", "本图为详细版", "物理伤)", "最多摆", "首次扫到即", "本轮失败不重试", "扫到后变暗并待命"]), str(path.relative_to(ROOT)) + "主图无旧模式或内部文字")
        check(not re.search(r"(?:体魄|神魂|灵慧|造诣)\[\d", player), str(path.relative_to(ROOT)) + "静态公式无角色样例代入")
        if path in primary:
            rectangles = []
            for cell in pages[0].findall(".//mxCell[@vertex='1']"):
                geo = cell.find("mxGeometry")
                x, y, w, h = (float(geo.get(key, "0")) for key in ["x", "y", "width", "height"])
                check(x >= 0 and y >= 0, path.stem + " " + cell.get("id") + "位于页内")
                rectangles.append((cell.get("id"), x, y, w, h))
            for i, (ident, x, y, w, h) in enumerate(rectangles):
                for other, a, b, c, d in rectangles[i + 1:]:
                    check(not (x < a + c and a < x + w and y < b + d and b < y + h), path.stem + " " + ident + "与" + other + "无重叠")

    for path in cards:
        player = text_of(ET.parse(path).getroot().findall("diagram")[0])
        if path.parent.name != "化法":
            check("关键词：" in player and "目标：" in player and "耗费：" in player, path.stem + "具备完整玩家字段")
            check("占格：" in player and re.search(re.escape(path.stem) + r"\*[1-9]\d*", player), path.stem + "占格保留且份数紧随名称")
        else:
            check("适用范围：" in player and "化法追加：" in player, path.stem + "追加与原效果分开")

    # 正文须与当前实际资源一致，不能只检查文件存在或沿用旧描述。
    from sync_board_effects_20261108 import skill_info, parse_book
    manifest = json.loads(read("docs/美术规范/人类招式占格审阅_20261107/几何检查结果.json"))
    for item in manifest["skills"]:
        source = item["resource"].removeprefix("res://")
        expected = skill_info(source)
        title = expected.splitlines()[0].split("*")[0]
        page = ET.parse(ROOT / f"docs/卡牌词典/招式盘/{title}.drawio").getroot().findall("diagram")[0]
        body = page.find(".//mxCell[@id='body']")
        check(body is not None and words(expected) == words(body.get("value", "")), title + "图内完整效果、费用、公式与当前资源一致")
    for book in books:
        page = ET.parse(ROOT / f"docs/秘籍词典/drawio/初步设计好的/{book}.drawio").getroot().findall("diagram")[0]
        for index, (_, learned) in enumerate(parse_book(book)[2], 1):
            body = page.find(f".//mxCell[@id='c{index}_cards']")
            for source in learned:
                check(words(skill_info(source)) in words(body.get("value", "")), book + "第" + str(index) + "卷招式完整文案与当前资源一致")
    for name in ("灵击", "云帔"):
        md = read(f"docs/卡牌词典/基础技能节点/{name}.md").split("## 策划说明")[0]
        check(not any(term in md for term in ["首次扫到", "变暗", "最多摆"]), name + "文字入口不残留底层流程与旧份数格式")

    for book in books:
        md = read(f"docs/秘籍词典/md/{book}.md")
        check(md.count("## 第") == 4 and md.count("### 化法") == 4, book + "四卷文字及化法完整")
        for target in re.findall(r"\]\(([^)]+)\)", md):
            check((ROOT / "docs/秘籍词典/md" / target.split("#")[0]).exists(), book + "链接可用 " + target)
        path = ROOT / f"docs/秘籍词典/预览/{book}.png"
        check(path.exists() and path.stat().st_size > 50000, book + "draw.io原生预览存在")

    output = {"date": "2026-11-08", "checks": len(checks), "passed": True, "rule_resources": len(baseline),
              "authorized_balance_resources": len(balance), "original_baseline_preserved": True,
              "primary_books": len(primary), "skill_cards": 20, "rewrite_cards": 20,
              "diagrams_checked": len(cards + extras + primary), "checks_detail": checks}
    (ROOT / "docs/plans/战斗与构筑/招式首轮平衡图文验收_20261108.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"BOARD_EFFECTS_STATIC_OK checks={len(checks)} resources={len(baseline)} diagrams={len(cards + extras + primary)}")


if __name__ == "__main__":
    run()
