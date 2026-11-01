"""Create an editable provisional price snapshot for the review UI, not game balance."""
from pathlib import Path
import re,json,math
ROOT=Path(__file__).resolve().parents[1]
TARGET=ROOT/'docs/plans/善功堂临时贡献价_20261030.json'
defaults={'材料':10,'秘籍':15,'器物':30,'丹药':10,'装备':20,'阵盘':60,'器具':10,'符箓':8,'灵膳':5}
pill_prices=[5,8,12,18,25]
def text(path):return path.read_text(encoding='utf-8-sig')
def field(source,key):
    m=re.search(r'^'+key+r' = &?"([^"]*)"',source,re.M)
    return m.group(1) if m else ''
def linked(source,key):
    m=re.search(r'^'+key+r' = ExtResource\("([^"]+)"\)',source,re.M)
    if not m:return None
    line=next((x for x in source.splitlines() if x.startswith('[ext_resource ') and f'id="{m.group(1)}"' in x),'')
    p=re.search(r'path="res://([^"]+)"',line)
    return ROOT/p.group(1) if p else None
entries={}
for path in sorted((ROOT/'resources/entities').rglob('*.tres')):
    source=text(path);identity=field(source,'definition_id');name=field(source,'entity_name');cat=field(source,'item_category')
    if '/inventory_density_20261029/' not in field(source,'icon_path') or not identity:continue
    if cat=='丹药' and not name.startswith('回春丹'):continue
    display_cat='器物' if 'equipment/' in path.as_posix() and '剑' in name else cat
    cost=defaults.get(display_cat,10);origin='临时拟价'
    if cat=='材料' and (craft:=linked(source,'craft_data')):
        craft_text=text(craft);utility_line=next((x for x in craft_text.splitlines() if x.startswith('utilities = ')),'')
        grades=[]
        for uid in re.findall(r'SubResource\("([^"]+)"\)',utility_line):
            block=re.search(r'\[sub_resource[^\n]*id="'+re.escape(uid)+r'"[^\n]*\]\n(.*?)(?=\n\[|\Z)',craft_text,re.S)
            if not block or not field(block.group(1),'utility_id'):continue
            g=re.search(r'^grade = (\d+)',block.group(1),re.M);grades.append(max(1,min(9,int(g.group(1)) if g else 1)))
        grades.sort(reverse=True);score=grades[0]*2 if grades else 0
        if len(grades)>1:score+=grades[1]*.5
        score+=sum(grades[2:])*.25
        grade=max(0,min(27,math.ceil(score)));cost=5+((grade+2)//3)*5;origin='现行材料基础价'
    elif cat=='秘籍' and (gongfa:=linked(source,'gongfa')):
        content=text(gongfa);tags=next((x for x in content.splitlines() if x.startswith('school_tags = ')),'')
        for tag,price in [('cloud_breath',15),('sword',10),('fist',5),('movement',5)]:
            if '"'+tag+'"' in tags:cost=price;origin='现行秘籍基础价';break
    elif identity in ['sect_sword_suxing','sect_sword_chenglu','sect_sword_hanzhang','sect_artifact_qinghe']:
        cost=0;origin='现行器物原型价'
    elif cat=='丹药':
        m=re.search(r'^efficacy_tier = (\d+)',source,re.M);tier=int(m.group(1)) if m else 0;cost=pill_prices[min(tier,4)]
    entries[identity]={'name':name,'category':display_cat,'contribution':cost,'source':origin}
config={'date':'2026-10-30','status':'用户授权临时拟价，可后续修改','scope':'仅独立界面样板，未修改正式兑换或玩家贡献','existing_rule_sources':['csharp/system/YuxuSectPrototypeBalance.cs','csharp/resources/MaterialCraftData.cs'],'category_defaults':defaults,'huichun_tiers':dict(zip(['薄效','良效','优效','极效','满效'],pill_prices)),'items':entries}
TARGET.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'items':len(entries),'examples':{x:entries[x] for x in ['mat_xuantie','manual_huangting_yangxing','alchemy_huichun_9_1','sect_sword_suxing']}},ensure_ascii=False))
