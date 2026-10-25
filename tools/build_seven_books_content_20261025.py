"""同步本轮秘籍主图、文字、卡牌图与资源；图谱字段先生成，资源复用相同内容。"""
from pathlib import Path
import copy
import html
import json
import math
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / 'docs/秘籍词典'
TEMPLATE = DOC / 'drawio/初步设计好的/玄枢含章经.drawio'
ENABLE_RUNTIME = '--enable-runtime' in sys.argv

def quote(v):
    return json.dumps(v, ensure_ascii=False)

def arr(items):
    return 'Array[String]([' + ', '.join(quote(v) for v in items) + '])'

def text(v):
    return re.sub('<[^>]+>', '', v.replace('<br>', '\n').replace('<br/>', '\n'))

def skill(name, cost, mana, target, tags, effect, formula='', style='', limit=0):
    return dict(name=name, cost=cost, mana=mana, target=target, tags=tags,
                effect=effect, formula=formula, style=style, limit=limit)

def skill_text(s, book, resource=True):
    lines=[s['name']]
    if resource: lines += [f"资源路径：res://resources/activeskills/{book}/{s['name']}.tres"]
    lines += ['关键词：' + ('、'.join(s['tags']) or '无'),
              f"耗费：体力 {s['cost']}" + (f"、法力 {s['mana']}" if s['mana'] else ''),
              '目标：' + ('自身' if s['target']==0 else '指定1名敌方')]
    if s['style']: lines += ['出手方式：' + ('近身' if s['style']=='melee' else '远程')]
    lines += ['效果：' + s['effect']]
    if s['formula'] and '计算公式' not in s['effect']: lines += ['计算公式：A=' + s['formula'] + '。']
    if s['limit']: lines += [f"使用次数：每回合{s['limit']}次"]
    lines += ['冷却：无']
    return '\n'.join(lines)

BOOKS = [
 dict(name='玉髓回澜养真宝册', heart='玉髓回澜', tags=['气血','护盾'],
      theme='将气血回落回复的溢出部分转为护盾，在显臂、回复和护盾之间取舍。',
      heart_effect='气血层数降低产生回复时，超出生命缺口的回复量的50%转为护盾。',
      main='自身护盾被敌方攻击耗尽后，获得气血1层；每回合限一次。',
      heart_formula='A=floor(max(0,本次气血回落应回复量−本次实际回复量)×0.50)',
      chapters=['涤络','鸣泉','玉振','回澜'], prefix='yusui', attr='physique', proficiency='body_proficiency',
      bonuses=[{'physique':.5,'body_proficiency':.5},{'body_proficiency':.5},{'physique':.5,'body_proficiency':1.},{'body_proficiency':1.}],
      temper=[{'max_health_percent':10.},{'qi_blood_cap':1.},{'physical_damage_percent':4.},{'max_health_percent':10.,'physical_damage_percent':6.}],
      temper_text=['最大生命 +10%','气血上限 +1','物理伤害 +4%','最大生命 +10%\n物理伤害 +6%'],
      skills=[(1,skill('涤髓',1,0,0,['气血'],'气血层数降低1层，触发气血层数降低时的回复效果；至少有1层气血时可施展。')),
              (2,skill('鸣泉',2,0,2,['气血','护盾'],'造成A点物理伤害。施展前自身具有护盾时，命中后获得气血1层。','round(8 + 体魄×0.55 + 体道造诣×0.25)','melee',1))],
      chemicals=[('涤髓','yusui_c1_disui','气血实际降低后，若自身已有护盾，额外获得A点护盾。A=round(2 + 体魄×0.10 + 体道造诣×0.15)。'),
                 ('鸣泉','yusui_c2_mingquan','施展前自身具有护盾时，本次原始攻击伤害提高20%。'),
                 ('架招','yusui_c3_jiazhao','本次来袭结算后，获得A点护盾。A=floor(本次架招实际抵消的物理伤害总点数×0.50)。'),
                 ('涤髓','yusui_c4_disui','本次气血回落回复的溢出护盾转化比例改为100%；替代心法对此次回复的50%转化，不叠加。')],
      boundary='护盾转化只读取气血回落回复，不读取其他治疗或散血。应回复量计入现行受疗修正，实际回复量按生命缺口截断。护盾被穿过、主动消耗或削减不触发主修回气血；敌方攻击实际吸收护盾并使其耗尽后，在该次来袭结算结束时检查。',
      combo='搭配百骸：保留高气血维持显臂，或回落换取护盾；搭配太和：强化架招先回落、再因应对补血。鸣泉读取行动开始前是否具有护盾，不按伤害段重复补血。'),
 dict(name='抱虚养晦通玄录',heart='虚室生白',tags=['周天'],
      theme='法力耗尽后，以纯体力技能继续积炁，完成周天后重启施法。',
      heart_effect='非应对技能支付费用后，若自身法力为0，该技能结算后获得炁1；每回合最多触发两次。',
      main='每回合首次触发本心法时，额外获得炁1。', heart_formula='',
      chapters=['散照','守寂','虚室','生白'],prefix='baoxu',attr='intelligence',proficiency='magic_proficiency',
      bonuses=[{'intelligence':.5,'magic_proficiency':.5},{'magic_proficiency':.5},{'intelligence':.5,'magic_proficiency':1.},{'magic_proficiency':1.}],
      temper=[{'max_mana_yunxi':1.},{'max_health_percent':10.},{'physical_damage_percent':5.},{'physical_damage_percent':5.}],
      temper_text=['云息境界起法力上限 +1','最大生命 +10%','物理伤害 +5%','物理伤害 +5%'],
      skills=[(1,skill('散照',1,1,2,[],'造成A点物理伤害。','round(8 + 灵慧×0.55 + 术法造诣×0.25)','ranged')),
              (2,skill('守寂',1,0,0,['护盾'],'自身获得A点护盾。计算公式：A=round(4 + 灵慧×0.20 + 神魂×0.15 + 术法造诣×0.20)。'))],
      chemicals=[('散照','baoxu_c1_sanzhao','支付费用后法力为0时，本次原始攻击伤害提高20%。'),
                 ('守寂','baoxu_c2_shouji','支付费用后法力为0时，结算后额外获得炁1；每回合限一次。'),
                 ('架招','baoxu_c3_jiazhao','实际抵消物理伤害后，若自身法力为0，获得炁1；每回合限一次。'),
                 ('散照','baoxu_c4_sanzhao','支付费用后法力为0且命中时，获得B点护盾；每回合限一次。B=round(3 + 灵慧×0.15 + 术法造诣×0.20)。')],
      boundary='法力为0的条件在费用支付后、技能效果前记录；后续回法或周天不反向改变本次记录。最后一点法力被支付、燃烧上限补费后法力归零、法力费用已改为体力时均按最终状态判定。未发动、应对技能、休整和服丹不触发心法。先结算技能及化法，再结算心法；主修额外炁与本次基础炁合并为同一次获得事件。',
      combo='玄枢主修＋本经辅修：零法力时继续推动周天与技能槽10；斫基主修＋本经辅修：已转体力支付的招式在零法力时仍可产炁。反向主辅搭配则保留额外产炁，放弃斫基的主修费用转换。'),
]

shu_path=DOC/'drawio/设计提案/舒卷天章.drawio'
if not shu_path.exists(): shu_path=DOC/'drawio/初步设计好的/舒卷天章.drawio'
shu_tree=ET.parse(shu_path)
shu_cells={c.get('id'):c for c in shu_tree.iter('mxCell')}
SHU=dict(name='舒卷天章',heart='流云剑势',tags=['剑','剑意'],theme='剑意达到门槛后，为剑招追加攻击；云岚三叠按递减概率追加至多3段。',
    chapters=['云舒','回岚','逐影','天章'],ids=['yun_shu','hui_lan','zhu_ying','tian_zhang'],prefix='shujuan',attr='physique',proficiency='sword_proficiency',
    bonuses=[{'physique':.5,'sword_proficiency':.5},{'sword_proficiency':.5},{'sword_proficiency':1.},{'physique':.5,'sword_proficiency':1.}],
    temper=[{'max_health_percent':10.},{'precision_percent':8.},{'physical_damage_percent':5.},{'max_health_percent':10.,'physical_damage_percent':7.}],
    temper_text=['最大生命 +10%','精准 +8%','物理伤害 +5%','最大生命 +10%\n物理伤害 +7%'],
    skills=[(3,skill('云岚三叠',2,0,2,['剑','剑意'],'造成A点物理伤害。首段命中后，以C概率追加攻击；追加攻击命中后，依次以D、E概率继续追加，最多追加3段。任一概率判定失败、攻击未命中或目标离场，后续攻击停止。每段追加攻击均攻击原目标，造成B点物理伤害。计算公式：A=round(5 + 体魄×0.40 + 剑道造诣×0.50)；B=round(2 + 体魄×0.20 + 剑道造诣×0.30 + max(本次行动开始时剑意层数−6,0))；C=clamp(40% + 本次行动开始时剑意层数×5% + 剑道造诣×0.5%,40%,80%)；D=max(0%,C−20个百分点)；E=max(0%,C−40个百分点)。','round(5 + 体魄×0.40 + 剑道造诣×0.50)','melee'))],
    chemicals=[], heart_versions=[],boundary=text(shu_cells['status_rules'].get('value')),combo=text(shu_cells['summary'].get('value')))
for i in range(1,5):
    v=text(shu_cells[f'c{i}_cards'].get('value'))
    if i==3: v='心法被动：流云剑势（沿用第二卷，无新增效果）'
    SHU['heart_versions'].append(v)
    chem=text(shu_cells[f'c{i}_right'].get('value'))
    target=re.search('适用范围：([^\n]+)',chem).group(1)
    SHU['chemicals'].append((target,f'shujuan_c{i}',chem.split('化法追加：',1)[1]))

def heart_text(b, i):
    if 'heart_versions' in b: return b['heart_versions'][i-1]
    if i!=1: return f"心法被动：{b['heart']}（沿用第一卷，无新增效果）"
    v=f"心法被动：{b['heart']}\n关键词：{'、'.join(b['tags'])}\n效果：{b['heart_effect']}\n主修额外效果：{b['main']}"
    if b['heart_formula']:
        v=v.replace('50%转为护盾','50%转为A点护盾')+'\n计算公式：'+b['heart_formula']+'。'
    return v

def chapter_id(b,i): return b.get('ids',[f"{b['prefix']}_c{x}" for x in range(1,5)])[i-1]

def block_value(v):
    return '<div style="text-align:left;">'+html.escape(v).replace('\n','<br>')+'</div>'

def fit_height(v,width):
    # 13号字，汉字按双宽；公式允许自动换行，保留正文内边距。
    chars=max(18,int((width-24)/6.5))
    lines=sum(max(1,math.ceil(sum(2 if unicodedata.east_asian_width(c) in 'WF' else 1 for c in line)/chars)) for line in v.splitlines())
    return max(140, lines*20+32)

def make_diagram(b, status, source=None):
    tree=copy.deepcopy(ET.parse(source or TEMPLATE)); root=tree.getroot()
    root.set('modified','2026-10-25T00:00:00Z')
    diagram=root.find('diagram'); diagram.set('name',b['name']); diagram.set('id',b['prefix']+'_20261025')
    cells={c.get('id'):c for c in root.iter('mxCell')}
    values={'title':b['name'],'overview':f"云息秘籍／可主修、可辅修\n灵根：任意；主修可修至：云息十层\n定位：{b['theme']}\n{status}",
            'summary':b['combo'],'status_rules':'结算边界\n'+b['boundary']}
    thresholds=[5,6,8,10] if b['prefix']!='shujuan' else [6,7,8,10]
    for i in range(1,5):
        learned=[s for vol,s in b['skills'] if vol==i]
        bonus='；'.join(attr_name(k)+f" +{v:g}" for k,v in b['bonuses'][i-1].items())
        values[f'c{i}']=f"第{'一二三四'[i-1]}卷：{b['chapters'][i-1]}\n卷章ID：{chapter_id(b,i)}\n顺序：{i}\n\n研读值（累计）：{[24,48,84,128][i-1]}\n学成可修至：云息{['三','六','九','十'][i-1]}层\n\n学习条件：\n灵根：任意\n境界：{['凡人境及以上','云息三层及以上','云息六层及以上','云息九层及以上'][i-1]}\n前置：{chapter_id(b,i-1) if i>1 else '无'}\n研读属性：{attr_name(b['attr'])}≥{thresholds[i-1]}；{attr_name(b['proficiency'])}≥{[0,.5,1,2][i-1]:g}\n\n学会永久属性：{bonus}\n\n习得：{'、'.join(s['name'] for s in learned) or '无'}"
        values[f'c{i}_cards']=heart_text(b,i)+('\n\n'+'\n\n'.join(skill_text(s,b['name'],False) for s in learned) if learned else '')
        values[f'c{i}_left']='修持（仅战斗内）\n战斗属性：\n'+b['temper_text'][i-1]
        target,_,desc=b['chemicals'][i-1]
        values[f'c{i}_right']='化法\n适用范围：'+target+'\n\n化法追加：'+desc
    for key,v in values.items():
        cells[key].set('value',block_value(v))
        st=cells[key].get('style','')
        st=re.sub(r'fontSize=\d+;', 'fontSize=20;' if key=='title' else 'fontSize=13;',st)
        st=re.sub(r'align=[^;]+;', 'align=left;',st)
        if 'verticalAlign=' not in st: st+='verticalAlign=top;'
        cells[key].set('style',st)
    y=370
    for i in range(1,5):
        heights=[]
        for key in [f'c{i}',f'c{i}_cards',f'c{i}_left',f'c{i}_right']:
            g=cells[key].find('mxGeometry');g.set('y',str(y))
            if key.endswith('_right'):g.set('width','440')
            h=fit_height(values[key],float(g.get('width')))
            if key==f'c{i}': h=max(430,h)
            g.set('height',str(h));heights.append(h)
        y+=max(heights)+70
    for key,yp in [('summary',650),('status_rules',950)]:
        g=cells[key].find('mxGeometry');g.set('x','1850');g.set('y',str(yp));g.set('width','550');g.set('height',str(fit_height(values[key],550)))
    model=root.find('.//mxGraphModel');model.set('pageHeight',str(y+70));model.set('pageWidth','2900')
    ET.indent(tree,space='  ')
    return tree

def attr_name(k):
    return {'physique':'体魄','intelligence':'灵慧','body_proficiency':'体道造诣','magic_proficiency':'术法造诣','sword_proficiency':'剑道造诣'}[k]

def card_diagram(title, content, path):
    root=ET.Element('mxfile',host='app.diagrams.net'); d=ET.SubElement(root,'diagram',name=title)
    m=ET.SubElement(d,'mxGraphModel',grid='1',page='1',pageWidth='1400',pageHeight='1500');r=ET.SubElement(m,'root')
    ET.SubElement(r,'mxCell',id='0');ET.SubElement(r,'mxCell',id='1',parent='0')
    c=ET.SubElement(r,'mxCell',id='main',value=block_value(content),style='rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=13;align=left;verticalAlign=top;spacing=16;',vertex='1',parent='1')
    ET.SubElement(c,'mxGeometry',x='100',y='100',width='850',height=str(fit_height(content,850)),**{'as':'geometry'})
    tree=ET.ElementTree(root);ET.indent(tree,space='  ');path.parent.mkdir(parents=True,exist_ok=True);tree.write(path,encoding='utf-8',xml_declaration=True)

def markdown(b,status):
    out=[f"# {b['name']}",f"> {status}。主图：[图谱](../drawio/{'设计提案' if not b.get('runtime',True) else '初步设计好的'}/{b['name']}.drawio)。",'可主修、可辅修；灵根不限；主修可修至云息十层。']
    rows=['| 卷章 | ID | 累计研读 | 境界与研读属性 | 学会永久奖励 | 修持（战斗内） |','| --- | --- | ---: | --- | --- | --- |']
    for i in range(1,5):
        threshold=([6,7,8,10] if b['prefix']=='shujuan' else [5,6,8,10])[i-1]
        requirements=f"{['凡人','云息三层','云息六层','云息九层'][i-1]}；{attr_name(b['attr'])}≥{threshold}；{attr_name(b['proficiency'])}≥{[0,.5,1,2][i-1]:g}"
        rewards='、'.join(attr_name(k)+f'+{v:g}' for k,v in b['bonuses'][i-1].items())
        rows.append(f"| {b['chapters'][i-1]} | {chapter_id(b,i)} | {[24,48,84,128][i-1]} | {requirements} | {rewards} | {b['temper_text'][i-1].replace(chr(10),'、')} |")
    out += ['## 四卷研读与奖励','\n'.join(rows),'卷章依次研读，后卷以前一卷为前置；学会永久奖励与战斗内修持分别结算。']
    for i in range(1,5):
        out += [f"## 第{'一二三四'[i-1]}卷：{b['chapters'][i-1]}",heart_text(b,i),'修持（仅战斗内）：'+b['temper_text'][i-1]]
        for vol,s in b['skills']:
            if vol==i: out += ['### 习得：'+s['name'],skill_text(s,b['name'],b.get('runtime',True))]
        target,_,desc=b['chemicals'][i-1]
        out += ['### 化法','适用范围：'+target,'化法追加：'+desc]
    out += ['## 构筑',b['combo'],'## 结算边界',b['boundary']]
    return '\n\n'.join(out)+'\n'

def resource_skill(s, book, effects=None, template=None):
    if template:
        src=template.read_text(encoding='utf-8')
        def setline(key,value):
            nonlocal src
            pat=r'^'+re.escape(key)+r' = .*?$'
            if re.search(pat,src,re.M):src=re.sub(pat,lambda _:key+' = '+value,src,flags=re.M)
            else: src+='\n'+key+' = '+value+'\n'
        setline('gongfa_rewrite_effect_ids',arr(effects or []))
        return src
    desc=s['effect']
    if s['formula'] and '计算公式' not in desc: desc+='\n计算公式：A='+s['formula']+'。'
    fields={'skill_name':quote(s['name']),'skill_type':'6' if s.get('heart') else '0' if s['style'] else '3',
            'slot_category':quote('passive' if s.get('heart') else 'active'),'source_gongfa_name':quote(book),
            'description':quote(desc),'tags':arr(s['tags']),'energy_cost':str(s['cost']),'mana_cost':str(s['mana']),
            'target_type':str(s['target']),'attack_style':quote(s['style']),'damage_type':quote('physical'),
            'damage_formula':quote(s['formula']),'uses_per_round':str(s['limit']),
            'icon_path':quote('res://textures/ui/combat/status_icons/心法_太和感物养元_v1.png' if s.get('heart') else 'res://textures/ui/combat/skill_icons/剑修基础_斩剑_v1.png' if '剑' in s['tags'] else 'res://textures/ui/combat/skill_icons/玄枢_云帔_v2.png' if s['target']==0 else 'res://textures/ui/combat/skill_icons/凡俗_痛击_v5.png'),
            'gongfa_rewrite_effect_ids':arr(effects or [])}
    return '[gd_resource type="Resource" script_class="ActiveSkillData" format=3]\n\n[ext_resource type="Script" path="res://csharp/gongfa/ActiveSkillData.cs" id="1"]\n\n[resource]\nscript = ExtResource("1")\n'+'\n'.join(k+' = '+v for k,v in fields.items())+'\n'

def write_runtime(b):
    name=b['name']; base=ROOT/'resources/activeskills'/name;base.mkdir(parents=True,exist_ok=True)
    h=skill(b['heart'],0,0,0,b['tags'],heart_text(b,1).split('\n',1)[1]);h['heart']=True
    (base/'心法.tres').write_text(resource_skill(h,name),encoding='utf-8')
    for _,s in b['skills']:(base/(s['name']+'.tres')).write_text(resource_skill(s,name),encoding='utf-8')
    exts=[('1','Script','res://csharp/gongfa/GongfaResource.cs'),('2','Script','res://csharp/gongfa/GongfaChapterConfig.cs'),('3','Script','res://csharp/gongfa/GongfaDeckRewriteConfig.cs'),('heart','Resource',f'res://resources/activeskills/{name}/心法.tres')]
    for i,(_,s) in enumerate(b['skills']):exts.append((f'skill{i}','Resource',f"res://resources/activeskills/{name}/{s['name']}.tres"))
    for i,(target,effect,_) in enumerate(b['chemicals'],1):
        local=next((s for _,s in b['skills'] if s['name']==target),None)
        original=base/(target+'.tres') if local else ROOT/'resources/activeskills'/('剑修基础' if target in ['养锋','格剑','尽锋','斩剑'] else '')/(target+'.tres')
        # 每项来源只携带自己的效果；多卷化法通过明确绑定合并，不能偷带未绑定来源。
        effects=[effect]
        file=base/'化法'/f'{target}第{i}卷.tres';file.parent.mkdir(exist_ok=True)
        file.write_text(resource_skill(local or {},name,effects,original),encoding='utf-8')
        exts += [(f'original{i}','Resource','res://'+original.relative_to(ROOT).as_posix()),(f'chem{i}','Resource','res://'+file.relative_to(ROOT).as_posix())]
    out=['[gd_resource type="Resource" script_class="GongfaResource" format=3]','']
    out += [f'[ext_resource type="{kind}" path="{path}" id="{key}"]' for key,kind,path in exts]
    for i,(target,_,_) in enumerate(b['chemicals'],1):
        out += ['',f'[sub_resource type="Resource" id="Rewrite{i}"]','script = ExtResource("3")','mode = 1',f'target_scope = {1 if any(s["name"]==target for _,s in b["skills"]) else 0}',f'source_skill = ExtResource("original{i}")','source_skill_name = '+quote(target),f'result_skill = ExtResource("chem{i}")']
    for i in range(1,5):
        full=heart_text(b,i)
        if '沿用' in full:
            prev=2 if b['prefix']=='shujuan' else 1
            full=heart_text(b,prev)
        out += ['',f'[sub_resource type="Resource" id="Chapter{i}"]','script = ExtResource("2")','chapter_id = '+quote(chapter_id(b,i)),
                'chapter_name = '+quote(b['chapters'][i-1]),f'order = {i}',f'required_study_progress = {[24,48,84,128][i-1]}.0',
                'required_realm = '+quote(['凡人','云息三层','云息六层','云息九层'][i-1]),
                'required_attributes = '+quote({b['attr']:[6,7,8,10][i-1] if b['prefix']=='shujuan' else [5,6,8,10][i-1],b['proficiency']:[0,.5,1,2][i-1]}),
                'required_chapter_ids = '+arr([chapter_id(b,i-1)] if i>1 else []),
                'learn_permanent_bonuses = '+quote(b['bonuses'][i-1]),'combat_tempering_bonuses = '+quote(b['temper'][i-1]),
                f'heart_method_upgrade_id = "{b["prefix"]}_c{i}"','heart_method_upgrade_description = '+quote(full),'heart_method_description = '+quote(full),
                f'sub_deck_rewrites = Array[ExtResource("3")]([SubResource("Rewrite{i}")])']
        learned=[f'ExtResource("skill{j}")' for j,(vol,s) in enumerate(b['skills']) if vol==i]
        if learned:out += ['learned_active_skills = Array[Resource](['+', '.join(learned)+'])']
    out += ['','[resource]','script = ExtResource("1")','gameplay_enabled = '+str(ENABLE_RUNTIME or b['prefix']=='shujuan').lower(),'gongfa_name = '+quote(name),'description = '+quote(b['theme']),
            'can_be_main = true','can_be_sub = true','required_study_progress = 24.0','school_tags = '+arr(['cloud_breath']),
            'main_realm_start = "凡人"','main_realm_end = "云息十层"','heart_method_skill = ExtResource("heart")','default_unlocked_chapter_count = 1',
            'chapters = Array[Resource](['+', '.join(f'SubResource("Chapter{i}")' for i in range(1,5))+'])']
    (ROOT/'resources/gongfa'/(name+'.tres')).write_text('\n'.join(out)+'\n',encoding='utf-8')
    item=ROOT/'resources/entities/items'/(name+'秘籍.tres')
    item.write_text('[gd_resource type="Resource" script_class="ItemData" format=3]\n\n[ext_resource type="Script" path="res://csharp/resources/ItemData.cs" id="1"]\n[ext_resource type="Resource" path="res://resources/gongfa/'+name+'.tres" id="2"]\n\n[resource]\nscript = ExtResource("1")\ndefinition_id = &"manual_'+b['prefix']+'"\nentity_name = '+quote(name)+'\nicon_path = "res://textures/items/item_secret_manual_ink_v1.png"\ncan_stack = false\nmax_stack = 1\nitem_category = "秘籍"\nitem_subcategory = "云息境"\ngongfa = ExtResource("2")\n',encoding='utf-8')

def main():
    status='候选接入' if ENABLE_RUNTIME else '设计提案，待审；游戏入口已关闭'
    for b in BOOKS+[SHU]:
        b['runtime']=ENABLE_RUNTIME or b['prefix']=='shujuan'
        book_status='已确认第五本，沿用既定流云剑势设计' if b['prefix']=='shujuan' else status
        tree=make_diagram(b,book_status)
        dest=DOC/'drawio'/('初步设计好的' if b['runtime'] else '设计提案')/(b['name']+'.drawio');tree.write(dest,encoding='utf-8',xml_declaration=True)
        (DOC/'md'/(b['name']+'.md')).write_text(markdown(b,book_status),encoding='utf-8')
        cardroot=ROOT/'docs/卡牌词典/秘籍技能'/b['name']
        card_diagram(b['heart'],'\n\n'.join(f"第{'一二三四'[i-1]}卷\n"+heart_text(b,i) for i in range(1,5)),cardroot/'心法.drawio')
        for _,s in b['skills']:card_diagram(s['name'],skill_text(s,b['name']),cardroot/(s['name']+'.drawio'))
        for i,(target,_,desc) in enumerate(b['chemicals'],1):card_diagram(target+'化法','化法\n适用范围：'+target+'\n\n化法追加：'+desc,cardroot/'化法'/f'{target}第{i}卷.drawio')
        write_runtime(b)
    # 单独的剑修设计稿，不生成资源、不加入开放名单。
    b=dict(name='敛锋归藏剑典',heart='归藏剑势',tags=['剑','剑意','护盾'],theme='以消耗剑意结束剑势，将放锋所得护盾再转为下一轮蓄势。',
           heart_effect='技能开始时剑意达到6层，归藏剑势生效。本次剑招实际消耗剑意后，获得A点护盾。',main='每回合首次在剑势生效时消耗剑意后，获得剑意1层。',
           heart_formula='A=round(本次实际消耗剑意层数×(1 + 体魄×0.05 + 剑道造诣×0.10))',
           chapters=['敛锋','抱刃','垂光','归藏'],prefix='guizang',attr='physique',proficiency='sword_proficiency',
           bonuses=SHU['bonuses'],temper=SHU['temper'],temper_text=SHU['temper_text'],runtime=False,
           skills=[(1,skill('启匣',1,0,0,['剑','剑意','护盾'],'消耗护盾4点，获得剑意2层；护盾至少4点时可施展。',limit=1)),
                   (3,skill('垂锋',1,0,0,['剑','剑意','化解','应对'],'应对敌方物理攻击时，消耗剑意2层，获得A层化解；剑意至少2层时可触发。计算公式：A=round(4 + 体魄×0.25 + 剑道造诣×0.40)。'))],
           chemicals=[('启匣','guizang_c1','护盾消耗由4点改为3点。'),('架招','guizang_c2','实际抵消物理伤害后，获得剑意1层；每回合限一次。'),('垂锋','guizang_c3','本次化解层数提高25%。'),('启匣','guizang_c4','施展前剑意为0时，改为获得剑意3层。')],
           boundary='设计提案，未实装。剑意沿用全局10层上限。剑势门槛按费用支付前的剑意读取；本次消耗后获得护盾，再结算主修返还，不让返还量反复计作消耗。尽锋原有命中条件与费用保留。',
           combo='养锋、格剑积蓄剑意；尽锋消耗剑意换取护盾；启匣再将一部分护盾换回剑意。与舒卷天章搭配时，需要选择继续保留高剑意追加攻击，还是放锋转入护持。')
    tree=make_diagram(b,'待审设计，未实装');tree.write(DOC/'drawio/设计提案/敛锋归藏剑典.drawio',encoding='utf-8',xml_declaration=True)
    (DOC/'md/敛锋归藏剑典.md').write_text(markdown(b,'待审设计，未实装'),encoding='utf-8')
    print('Generated 3 runtime book graphs, texts, resources and card dictionaries; 1 sword proposal.')

if __name__=='__main__':main()
