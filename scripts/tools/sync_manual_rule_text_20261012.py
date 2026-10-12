"""同步图谱旁注、参考文案及Excel定点改写清单，不改图面坐标。"""
from pathlib import Path
import html,json,re,sys
import xml.etree.ElementTree as E
from revise_manual_heart_rules_20261012 import HEARTS, public_fall_text

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'tmp/manual-rules-20261012'
OUT.mkdir(parents=True,exist_ok=True)

def common(s):
    s=public_fall_text(s)
    s=re.sub(r'气血下降(?:触发|按)公共规则[，]?(?:回复|恢复)最大生命×3%', '触发气血层数降低时的回复效果',s)
    s=s.replace('主修研读第四卷后倍率','主修心法生效时倍率')
    s=s.replace('每主动下降1层，按公共规则回复最大生命的3%', '层数降低时，按当前气血回落回复规则结算')
    return s

all_hearts={}
for p in (ROOT/'docs/秘籍词典/drawio').rglob('*.drawio'):
    if p.stem.startswith('_'):continue
    def cell(m):
        v=common(html.unescape(m.group(2)));cid=re.search(r'\bid="([^"]+)"',m.group(1))[1]
        if cid=='status_rules':
            v=v.replace('每次下降1层回复最大生命×3%','气血层数降低时按当前回落回复率结算回复效果')
            v=v.replace('每次主动下降1层回复最大生命×3%','气血层数主动降低时按当前回落回复率结算回复效果')
            if p.stem in HEARTS:
                # 旧旁注中的唯一心法段落全部替换为当前规则，其他状态段保留。
                v=re.sub(r'本经同名唯一心法Buff[^<]*',
                    '本经唯一心法Buff，持续至本场战斗结束。'+''.join(HEARTS[p.stem]),v)
            if p.stem=='灵枢经筋篇':
                v=re.sub(r'本经同名唯一心法Buff[^<]*','本经唯一心法Buff；每次主动效果成功消耗伤势，获得借力1层；主修额外获得借力1层。第二卷起，同次消耗至少2层额外获得身法1层；第四卷起，同次消耗至少3层额外获得气血1层。',v)
            if p.stem=='补虚行血诀':
                v=re.sub(r'本诀同名唯一心法Buff[^<]*','本诀唯一心法Buff；每次主动使气血由1层降至0层，强化下一次催血：少耗精力1点并额外获得气血1层，强化不叠加。主修时，该次催血额外获得身法2层；第三卷起，触发强化时额外获得身法1层。',v)
            if p.stem=='玄匣藏真候天诀':
                v=v.replace('行动槽1至3依次安排匣底余炁、匣中惊霆、匣底余炁','行动槽1至3依次安排匣底余炁、吐纳、匣底余炁')
                v=v.replace('第四卷的产炁','第三、四卷的产炁')
            if p.stem=='抱朴归元篇':
                v=re.sub(r'行动槽1安排炉中微明[^<]*','法力上限降至1时，周天阈值为2。主修每回合补给后先获炁1；随后归炉照骨实际支付法力并完成结算，首个付法事件再获炁1，触发周天。第三卷起，首次周天获得归元1层，供后续技能减费。',v)
            if p.stem=='坎离错综周流参同秘录':v=v.replace('第三卷返还法力','第二卷返还法力')
            if p.stem=='神室易质秘录':
                v=re.sub(r'战斗开始时，心法为术法槽[^<]*','每回合开始时，心法给已装配的灵台照万象赋念1；第二卷起，该技能每回合首次成功施展后再获念1（达到6念后停发此项）。护盾分配随念层改变；主修首次跨入3念、6念时触发额外护盾与精力回复。',v)
        if cid=='c1_cards' and '类型：心法' in v:
            part=re.search(r'类型：心法<br>关键词：[^<]*<br>(.*?)(?=<br><br>|$)',v)[1]
            all_hearts[p.stem]='\n'.join(part.split('<br>'))
        return m.group(1)+html.escape(v,quote=True)+m.group(3)
    s=p.read_text('utf-8-sig');s=re.sub(r'(<mxCell\b[^>]*?\bvalue=")([^"]*)(")',cell,s)
    p.write_text(s,encoding='utf-8')
for p in (ROOT/'resources/activeskills').rglob('*.tres'):
    s=p.read_text('utf-8-sig');v=common(s)
    if v!=s:p.write_text(v,encoding='utf-8')
p=ROOT/'resources/card_reference.json';data=json.loads(p.read_text('utf-8-sig'))
def walk(x):
    if isinstance(x,str):return common(x)
    if isinstance(x,list):return [walk(v) for v in x]
    if isinstance(x,dict):return {k:walk(v) for k,v in x.items()}
    return x
data=walk(data)
for p in (ROOT/'resources/activeskills').rglob('*.tres'):
    s=p.read_text('utf-8-sig')
    if 'skill_type = 6' not in s:continue
    name=re.search(r'^skill_name = "([^"]+)"',s,re.M)
    desc=re.search(r'^description = (".*")',s,re.M)
    if name and desc and name[1] in data['cards']:data['cards'][name[1]]['text']=json.loads(desc[1])
data['terms']['气血']='公共战斗状态，基础上限3层。层数降低时触发当前回落回复效果，读取公共基准、当前回复率修正及心法替代规则；无自动回落，战斗结束清空。'
p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'heart-rules.json').write_text(json.dumps(all_hearts,ensure_ascii=False,indent=2),encoding='utf-8')
if '--excel-manifest' in sys.argv:
    import openpyxl
    manifests=[]
    for p in (ROOT/'docs').glob('*气血体系*.xlsx'):
        wb=openpyxl.load_workbook(p);updates=[]
        for ws in wb:
            for row in ws:
                for c in row:
                    if not isinstance(c.value,str):continue
                    v=common(c.value)
                    if ws.title=='技能总表' and c.column==5:
                        name=ws.cell(c.row,1).value
                        book=ws.cell(c.row,2).value or ''
                        for b,desc in all_hearts.items():
                            if b in book and '心法' in book:v=desc
                    if v!=c.value:updates.append({'sheet':ws.title,'cell':c.coordinate,'before':c.value,'after':v})
        manifests.append({'path':str(p),'updates':updates})
    (OUT/'excel-updates.json').write_text(json.dumps(manifests,ensure_ascii=False,indent=2),encoding='utf-8')
    print([(Path(m['path']).name,len(m['updates'])) for m in manifests])
