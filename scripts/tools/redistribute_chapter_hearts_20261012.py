"""按卷展示完整心法版本；保留原战斗机制标记与所有技能内容。"""
from pathlib import Path
import html, json, re, math, unicodedata
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'tmp/manual-rules-20261012/chapter-hearts-source.json'
entries = json.loads(SOURCE.read_text(encoding='utf-8'))
stages = {}

def plain(value):
    return re.sub(r'<[^>]*>', '', value).strip()

for entry in entries:
    name = Path(entry['path']).stem
    v = entry['cells']['c1_cards']
    heart = re.search(r'心法被动：([^<]+)', v).group(1)
    tags = re.search(r'类型：心法<br>关键词：([^<]+)', v).group(1)
    base = re.search(r'(?:<br>)效果：([^<]+)', v).group(1)
    main = re.search(r'主修额外追加：([^<]+)', v).group(1)
    changes = {}
    for number, sentence in re.findall(r'第([二三四])卷追加：([^<]+)', v):
        changes[{'二':2,'三':3,'四':4}[number]] = sentence
    for order in [2,3,4]:
        m = re.search(r'心法追加（第[二三四]卷）：([^<]+)', entry['cells'][f'c{order}_cards'])
        if m and order not in changes: changes[order] = m.group(1)
    effects = {1:base}
    for order in range(2,5):
        effects[order] = effects[order-1] + ((' ' + changes[order]) if order in changes else '')

    # Merge numeric changes into their final values, instead of requiring the reader to add them.
    if name == '黄庭养形篇':
        effects[2] = effects[3] = '每回合首次自身主动使气血层数降低后，获得身法2层；第二次主动使气血层数降低后，获得身法1层。'
        effects[4] = '每回合首次自身主动使气血层数降低后，获得身法3层；第二次主动使气血层数降低后，获得身法1层。'
    elif name == '乘物游骸真经':
        for order, count in [(2,1),(3,1),(4,2)]:
            effects[order] = f'每回合首次由自身主动使气血层数降低后，获得游骸1层和身法{count}层；同次效果至多触发一次。'
    elif name == '赤阳参同经':
        for order, count in [(2,1),(3,1),(4,2)]:
            effects[order] = base + f' 每次赤阳化法实际消耗至少1点血炉后，获得身法{count}层。'
    elif name == '内景损益篇':
        effects[2] = base + ' 每回合首次受到实际生命伤害后，获得外伤1层。'
        effects[3] = effects[4] = effects[2].replace('提高3%', '提高4%')
        main = '每层伤势提供的上述伤害倍率再提高1个百分点。'
    elif name == '阴符应物经':
        effects[3] = effects[4] = base.replace('每满4点', '每满3点')
        # The main effect is always a reduction by one, including after chapter three.
        main = '上述借力转化门槛降低1点。'
    elif name == '补虚行血诀':
        effects[3] = effects[4] = base + ' 每次上述气血回落后，获得身法1层。'
    elif name == '玄匣藏真候天诀':
        effects[4] = effects[3].replace('获得炁1；每回合最多一次', '获得炁2；每回合最多一次')
    elif name == '璇玑移星易序篇':
        effects[4] = effects[3].replace('获得炁1；每轮最多触发一次', '获得炁2；每轮最多触发一次')
    elif name == '神室易质秘录':
        # Preserve the existing six-layer stop exactly; this task changes presentation only.
        effects[2] = effects[3] = base + ' 灵台照万象每轮首次成功施展后获得念1；每轮最多赋念1层，念达到6后本心法不再赋念。'
        effects[4] = effects[2] + ' 灵台照万象各念形态的基础护盾倍率提高10%；目标形态、数量和念层门槛沿用技能规则。'
    elif name == '抱朴归元篇':
        effects[4] = effects[3].replace('获得归元1层，最多1层', '获得归元1层；若该次周天所用的炁获得事件溢出至少1层炁，改为获得归元2层，最多2层')
        effects[4] += ' 每层归元分别用于后续一项技能，不叠加用于同一技能；溢炁不保留，也不重复触发周天。'
    elif name == '沧浪心印录':
        effects[4] = ('当一项自身技能实际获得炁并触发周天，计算本次溢炁：周天前炁总量减去当前阈值，最低为0。'
            '溢炁每满2层，使来源技能获得念1，单次周天最多赋念2；若来源技能在本次赋念前已有念6层，改为给后方最多两项未结算术法各赋念1。'
            '炁来源无法追溯到技能时不赋念。先清空炁、将法力回满，再赋念；念附着于技能，整场累积，施展和触发均不消耗。'
            '来源技能首次达到念3后的下一次成功结算，令后方第一项未结算术法获得念1，每项来源技能每场最多触发一次。')
    elif name == '玄籁返真秘录':
        common = ('检查本角色锁定的行动队列。连续五项行动槽中的主动技能须为A→B→C→B→A；A、B、C为三个不同术法槽实例，镜像位置严格引用同一实例。'
            '五项均按序成功结算后，C获得念2；每回合只处理最早完成的一组，不再处理三项短回文。'
            '休整、空位、跳过或未成功结算均打断序列；赋念不复制行动，也不重新检查回文。')
        effects[2] = common
        effects[3] = common + ' 完成回文时，B同时获得念1；B重复出现仍只赋予同一实例。'
        effects[4] = effects[3] + (' 若同一组A、B、C连续两回合均完成上述回文，第二回合完成时，A、B、C各额外获得念1，每回合最多一次。'
            '任何一回合未完成、实例变化或顺序不符，连续记录清零。')
    elif name == '谷神应籁篇':
        common = ('战斗开始时，在2至9号术法槽中按序寻找首组满足条件的三连：中心槽装配念为0的主动技能，左右相邻槽均装配主动技能；中心槽记为空谷位。'
            '无符合条件的三连则本心法本场无效。中心技能成功结算时，两侧念均不少于3层则获得炁1，均不少于4层则改为炁2，两档不叠加，每回合最多一次；两侧念无需相等，也不因本效果消耗。'
            '中心技能被任何来源赋念后，本场不再满足空谷位条件，不能移除念恢复。')
        effects[2] = common
        effects[3] = effects[4] = common + (' 触发心法后，获得炁，再给两侧念较低的一侧赋念1；相同时给左侧，每回合最多一次。'
            '若该次炁完成周天，先结算周天，再给空谷位之后首个未结算主动术法赋念1；赋念不施展技能或额外周天。')
    stages[name] = {'heart':heart, 'tags':tags, 'effects':effects, 'main':main, 'changes':sorted(changes)}

    def block(order):
        return (f'心法被动：{heart}<br><br>{heart}：<br>类型：心法<br>关键词：{tags}<br>'
            f'效果：{effects[order]}<br>主修额外效果：{main}<br><br>')

    path = ROOT / entry['path']
    raw = path.read_text(encoding='utf-8')
    root = ET.fromstring(raw)
    cells = {c.get('id'):c for c in root.iter('mxCell')}
    def set_value(key,value):
        nonlocal_raw[0] = re.sub(r'(<mxCell\b[^>]*\bid="'+re.escape(key)+r'"[^>]*\bvalue=")[^"]*(")',
            lambda m:m[1]+html.escape(value,quote=True)+m[2],nonlocal_raw[0],count=1)
    nonlocal_raw = [raw]
    v = html.unescape(cells['c1_cards'].get('value',''))
    v = re.sub(r'<br>第[二三四]卷追加：[^<]+','',v)
    v = v.replace('主修额外追加：','主修额外效果：')
    # Use the same initial main wording as later versions.
    v = re.sub(r'主修额外效果：[^<]+',lambda m:'主修额外效果：'+main,v)
    set_value('c1_cards',v)
    for order in range(2,5):
        key=f'c{order}_cards';v=html.unescape(cells[key].get('value',''))
        v=re.sub(r'心法追加（第[二三四]卷）：[^<]+(?:<br>)*','',v)
        v=re.sub(r'(?:心法)?本卷不追加(?:心法)?效果[^<]*(?:<br>)*','',v)
        if order in changes:
            v=block(order)+v
        set_value(key,v)
    # The status appendix references the versions, rather than merging future levels into the first heart.
    if 'status_rules' in cells:
        v=html.unescape(cells['status_rules'].get('value',''))
        pattern=re.escape(heart)+r'<br>本经唯一心法Buff[^<]*'
        v=re.sub(pattern,lambda m:heart+'<br>本经唯一心法Buff，持续至本场战斗结束；心法效果按已学卷章的对应版本生效，完整内容见各卷心法条目。',v)
        set_value('status_rules',v)
    for key,c in cells.items():
        if key.startswith('e') and c.get('value')=='心法追加与习得':set_value(key,'心法与习得')
    raw=nonlocal_raw[0]
    # Preserve colors, widths, horizontal structure and connectors. Grow notes only when needed;
    # shift lower chapter rows together so the original four-chapter layout does not overlap.
    root=ET.fromstring(raw);cells={c.get('id'):c for c in root.iter('mxCell')}
    adjustments={}
    for order in range(1,5):
        c=cells[f'c{order}_cards'];g=c.find('mxGeometry')
        text=html.unescape(c.get('value','')).replace('<br>','\n')
        text=re.sub('<[^>]*>','',text)
        # Font 13, usable width 720: conservatively 53 Chinese full-width characters per line.
        lines=sum(max(1,math.ceil(sum(1 if unicodedata.east_asian_width(ch) in 'WF' else .55 for ch in line)/53)) for line in text.split('\n'))
        need=max(float(g.get('height')),lines*17+28)
        delta=need-float(g.get('height'))
        if delta<=0:continue
        y=float(g.get('y'));g.set('height',str(int(need)))
        adjustments[c.get('id')]=dict(g.attrib)
        for other in cells.values():
            geom=other.find('mxGeometry')
            if geom is not None and geom.get('y') is not None and float(geom.get('y'))>y:
                geom.set('y',str(float(geom.get('y'))+delta));adjustments[other.get('id')]=dict(geom.attrib)
    for key,attrs in adjustments.items():
        def adjust(m):
            geo=m[2]
            for attr in ['height','y']:
                if attr in attrs:geo=re.sub(r'\b'+attr+r'="[^"]*"',attr+'="'+attrs[attr]+'"',geo)
            return m[1]+geo
        raw=re.sub(r'(<mxCell\b[^>]*\bid="'+re.escape(key)+r'"[^>]*>\s*)(<mxGeometry\b[^>]*>)',adjust,raw,count=1)
    ET.fromstring(raw)
    path.write_text(raw,encoding='utf-8')

    resource=ROOT / 'resources/gongfa' / (name+'.tres')
    if resource.exists():
        data=resource.read_text(encoding='utf-8')
        def chapter(match):
            part=match[0];m=re.search(r'^order\s*=\s*(\d+)$',part,re.M)
            if not m:return part
            order=int(m[1]);desc='效果：'+effects[order]+'\n主修额外效果：'+main
            part=re.sub(r'^heart_method_description = .*\n?','',part,flags=re.M)
            return part.rstrip()+'\nheart_method_description = '+json.dumps(desc,ensure_ascii=False)+'\n\n'
        data=re.sub(r'\[sub_resource[^\n]+\]\n(?:(?!\n\[).)*',chapter,data,flags=re.S)
        resource.write_text(data,encoding='utf-8')

out=ROOT / 'tmp/manual-rules-20261012/chapter-hearts-final.json'
out.write_text(json.dumps(stages,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'已按卷分布完整心法版本：{len(stages)}本')
