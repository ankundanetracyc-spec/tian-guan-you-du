"""Export read-only card text and shapes for the isolated role-panel preview."""
from pathlib import Path
import html
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'docs/美术规范/角色与静室纸墨木石样稿_20261108'


def value(text, name, default=''):
    match = re.search(r'^' + re.escape(name) + r' = (.*)$', text, re.M)
    if not match:
        return default
    raw = match[1]
    return json.loads(raw) if raw.startswith('"') else raw


def face(path):
    diagram = ET.parse(path).getroot().find('diagram')
    body = next(cell for cell in diagram.iter('mxCell') if cell.get('id') == 'body')
    return html.unescape(re.sub(r'<[^>]+>', '', body.get('value', '').replace('<br>', '\n').replace('<br/>', '\n')))


def main():
    fixture = json.loads((DEST / 'fixture.js').read_text(encoding='utf-8').removeprefix('window.PREVIEW_DATA = ').strip().rstrip(';'))
    cards = []
    for original in fixture['skills']:
        path = ROOT / original['path']
        resource = path.read_text(encoding='utf-8').split('[resource]', 1)[1]
        shape = list(map(int, re.search(r'board_size = Vector2i\((\d+), (\d+)\)', resource).groups()))
        name = value(resource, 'skill_name')
        tags = re.search(r'^tags = .*?\((\[.*\])\)', resource, re.M)
        cards.append(dict(key=name, kind='skill', name=name, source=original['source'], shape=shape,
            max=int(value(resource, 'board_max_placements', '1')), rotate=value(resource, 'board_rotation_allowed') == 'true',
            energy=int(value(resource, 'energy_cost', '0')), mana=int(value(resource, 'mana_cost', '0')),
            icon=original['icon'], path=original['path'], tags=json.loads(tags[1]) if tags else [],
            face=face(ROOT / 'docs/卡牌词典/招式盘' / (name + '.drawio')), available=name != '寄念'))

    for book, prefix, chapters, targets in [
        ('玄枢含章经', 'xuanshu', ['蛰雷','云垂','折冲','照影'], ['惊蛰','云帔','架招','复命']),
        ('斫基损契玄经', 'zhuoji', ['秉烛','剪灯','续照','长明'], ['折锋','焚元','架招','折锋']),
        ('百骸衍枝化生宝册', 'baihai', ['养络','生枝','双擎','化生'], ['催血','血臂撼岳','架招','架招']),
        ('太和感物养元宝册', 'taihe', ['听息','回澜','藏锋','返元'], ['乘势再攻','虚怀纳劲','架招','乘势再攻']),
        ('舒卷天章', 'shujuan', ['云舒','回岚','逐影','天章'], ['养锋','格剑','云岚三叠','尽锋']),
    ]:
        text = (ROOT / 'resources/gongfa' / (book + '.tres')).read_text(encoding='utf-8')
        icons = re.findall(r'^board_icon_path = "res://([^\"]+)"', text, re.M)
        assert len(icons) == 4, book
        for i, (chapter, target, icon) in enumerate(zip(chapters, targets, icons), 1):
            name = book + '·' + chapter
            assert (ROOT / icon).is_file(), icon
            cards.append(dict(key=name, kind='rewrite', name=name, source=book, shape=[1,1], max=1,
                rotate=True, target=target, icon='../../../' + icon,
                path='resources/gongfa/' + book + '.tres',
                face=face(ROOT / 'docs/卡牌词典/招式盘/化法' / (name + '.drawio')), available=True))

    cards.append(dict(key='周天指示', name='周天指示', kind='indicator', source='玄枢含章经', shape=[1,1], max=1,
        rotate=True, icon='../../../textures/ui/combat/board_skill_icons/zhoutian_indicator.png', available=True,
        face='占一格，可移动和转向，不能移出。\n指向共边相邻的一份非应对招式；每回合允许其额外施放一次，照常支付费用、检查原次数与冷却。'))
    catalog = (ROOT / 'csharp/systems/AlchemyProductCatalog.cs').read_text(encoding='utf-8')
    for key in ['huichun_1', 'ninggang_1']:
        path = ROOT / 'resources/entities/items/alchemy' / (key + '.tres')
        text = path.read_text(encoding='utf-8').split('[resource]', 1)[1]
        amount_array = 'Healing' if key.startswith('huichun') else 'Shield'
        amount = int(re.search(r'\[([^\]]+)\]', catalog.split('int[] ' + amount_array, 1)[1])[1].split(',')[1])
        effect = f'恢复{amount}点生命值。' if key.startswith('huichun') else f'获得{amount}点护盾。'
        cards.append(dict(key=key, name=value(text, 'entity_name'), kind='pill', source='行囊', shape=[1,2], max=2,
            stock=2, rotate=True, icon=value(text, 'icon_path').replace('res://', '../../../'),
            path=str(path.relative_to(ROOT)).replace('\\', '/'), face=effect + '\n' + value(text, 'description'), available=True))

    data = dict(status='仅用于独立交互样稿；非真实角色已学清单或库存。不运行战斗、不写存档或浏览器存储。', cards=cards,
        sources=['docs/plans/战斗与构筑/人类招式首轮平衡与名称标注_20261108.md', 'docs/plans/战斗与构筑/人类招式盘实施与验收_20261107.md', 'docs/卡牌词典/招式盘/README.md'])
    serialized = json.dumps(data, ensure_ascii=False, indent=2)
    (DEST / 'battle-data.json').write_text(serialized + '\n', encoding='utf-8')
    (DEST / 'battle-data.js').write_text('window.ROLE_BATTLE_DATA = ' + serialized + ';\n', encoding='utf-8')
    print(f'Exported {len(cards)} definitions (20 skills, 20 rewrites, indicator, 2 pills).')


if __name__ == '__main__':
    main()
