"""Restore learned world spells and keep manual diagrams text-only and consistently laid out."""
from pathlib import Path
import re, json, html, math, unicodedata
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
world_spells = {'乘物游骸真经':'游骸行遁', '内景损益篇':'观照山河', '补虚行血诀':'温脉归元'}
changed = []
for path in (root/'docs/秘籍词典').rglob('*.drawio'):
    raw = path.read_text(encoding='utf-8-sig')
    diagram = ET.fromstring(raw)
    cells = {c.get('id'):c for c in diagram.iter('mxCell')}
    before = raw
    def set_value(key, text):
        global raw
        raw = re.sub(r'(<mxCell\b[^>]*\bid="'+re.escape(key)+r'"[^>]*\bvalue=")[^"]*(")',
            lambda m:m[1]+html.escape(text,quote=True)+m[2], raw, count=1)
    for key, cell in cells.items():
        value = cell.get('value','')
        if '<img' in value.lower():
            value = re.sub(r'<img\b[^>]*>', '', value, flags=re.I)
            set_value(key,value)
        if not re.fullmatch(r'c[1-4]_cards',key or ''): continue
        if key == 'c1_cards' and path.stem in world_spells:
            # Existing world-spell sections were appended at the end of chapter one.
            value = value.split('大世界术法',1)[0].rstrip()
            name = world_spells[path.stem]
            resource = (root/'resources/world_spells'/(name+'.tres')).read_text(encoding='utf-8-sig')
            data={m[1]:json.loads(m[2]) for m in re.finditer(r'^(\w+)\s*=\s*("(?:[^"\\]|\\.)*")\s*$', resource, re.M)}
            if '体力' not in data['resource_cost']:
                data['resource_cost']=data['resource_cost'].replace('1点','1点大世界体力',1)
            status='已确认；地图施放尚未接入。' if name=='游骸行遁' else '候选术法；地图施放尚未接入。'
            fields=[('大世界术法',name),('类型',data['use_type']),('施展方式',data['cast_mode']),
                ('习得条件','学会本卷后永久解锁；无需装配来源秘籍。'),('使用场景',data['scene']),
                ('目标',data['target_rule']),('耗费',data['resource_cost']),('时间',data['world_time']),
                ('效果',data['effect']),('使用次数',data['usage_limit']),('中止与失败',data['stop_and_failure']),
                ('实施状态',status)]
            value += '<br><br>'+'<br>'.join(k+'：'+v for k,v in fields)
        # A single block avoids escaped/malformed layout fragments becoming visible text.
        value = re.sub(r'</(?:div|p)\s*>','<br>',value,flags=re.I)
        value = re.sub(r'</?(?:div|p|span|font|b|strong|i)\b[^>]*>','',value,flags=re.I)
        value = re.sub(r'(?:<br\s*/?>\s*){3,}','<br><br>',value)
        value = re.sub(r'^(?:<br\s*/?>)+|(?:<br\s*/?>)+$','',value).strip()
        set_value(key,'<div style="text-align:left;">'+value+'</div>')
        # Match the established chapter-note typeface, alignment, and wrapping.
        style=cell.get('style','')
        for field,setting in [('fontSize','13'),('align','left'),('verticalAlign','top'),('whiteSpace','wrap'),('html','1')]:
            style=re.sub(r'(?:^|;)'+field+r'=[^;]*','',style)
            style=style.rstrip(';')+';'+field+'='+setting+';'
        raw=re.sub(r'(<mxCell\b[^>]*\bid="'+re.escape(key)+r'"[^>]*\bstyle=")[^"]*(")',lambda m:m[1]+style+m[2],raw,count=1)
    diagram=ET.fromstring(raw);cells={c.get('id'):c for c in diagram.iter('mxCell')}
    adjusted={}
    for order in range(1,5):
        c=cells.get(f'c{order}_cards')
        if c is None:continue
        g=c.find('mxGeometry')
        if g is None:continue
        value=c.get('value','').replace('<br>','\n')
        text=html.unescape(re.sub(r'<[^>]*>','',value))
        capacity=(float(g.get('width','740'))-36)/13
        lines=sum(max(1,math.ceil(sum(1 if unicodedata.east_asian_width(ch) in 'WF' else .55 for ch in line)/capacity)) for line in text.split('\n'))
        needed=max(float(g.get('height','0')),lines*18+32)
        delta=needed-float(g.get('height','0'))
        if delta<=0:continue
        top=float(g.get('y','0'));g.set('height',str(int(needed)));adjusted[c.get('id')]=dict(g.attrib)
        for lower in cells.values():
            geometry=lower.find('mxGeometry')
            if geometry is not None and geometry.get('y') is not None and float(geometry.get('y'))>top:
                geometry.set('y',str(float(geometry.get('y'))+delta));adjusted[lower.get('id')]=dict(geometry.attrib)
    for key,attrs in adjusted.items():
        def update(m):
            geometry=m[2]
            for attr in ['height','y']:
                if attr in attrs:geometry=re.sub(r'\b'+attr+r'="[^"]*"',attr+'="'+attrs[attr]+'"',geometry)
            return m[1]+geometry
        raw=re.sub(r'(<mxCell\b[^>]*\bid="'+re.escape(key)+r'"[^>]*>\s*)(<mxGeometry\b[^>]*>)',update,raw,count=1)
    ET.fromstring(raw)
    if raw!=before:
        path.write_text(raw,encoding='utf-8');changed.append(path.name)
print('已整理：',', '.join(changed))
