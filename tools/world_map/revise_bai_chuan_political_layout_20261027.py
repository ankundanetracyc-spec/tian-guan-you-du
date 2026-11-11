"""Revise political proximity in planning artifacts, leaving runtime untouched."""
import json, subprocess, sys, re, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'))
positions={
 '太微道宫':[39,51],'抱朴山':[43,70],'百药谷':[45,88],
 '归藏书院':[58,35],'沧溟水府':[67,24],
 '血河教':[65,80],'蚀日宫':[88,86],'枯骨寺':[83,36],
 '镇海城':[79,12],'临渊城':[73,29],'春水府':[47,60],'青萝城':[42,81],'归寂城':[84,43],
 '听风镇':[76,7],'白蘋里':[87,18],'澜浦渡':[91,13],
 '烟柳镇':[44,64],'桑落镇':[40,59],'荷汀镇':[51,62],'采薇里':[38,84],
 '天籁长峡':[36,41],'玄霜坞':[29,46],'砺青崖':[54,28],'含章窟':[48,30],
 '长生坞':[39,67],'濯缨泉':[48,73],
 '万毒谷':[50,84],'盘龙涧':[40,91],'不老林':[51,92],'鸣玉溪':[70,31],
 '藏蜕林':[85,31],'蕴辉岩':[76,77],
 '东林地':[50,66],'灵草谷':[49,79],'鹿鸣林':[36,86],'雷泽':[55,75],'落星墟':[51,89],
 '埋骨原':[79,39],'玄荒战墟':[87,47],'寂照林':[91,35],'无名陵':[80,46],
 '潮生泽':[81,22],'沧溟滩':[90,25],'沉舟湾':[79,29],
 '回澜滩':[40,57],'涵虚泽':[51,54],'蒹葭浦':[50,59],
}
by={n['name']:n for n in d['nodes']}
for name,pos in positions.items():by[name]['pos']=pos
orders={
 '镇海城':['沧溟水府','玄都天府','归藏书院'],
 '春水府':['太微道宫','抱朴山','百药谷'],
 '溪云城':['抱朴山','百药谷','太微道宫'],
}
for name,order in orders.items():
 d['governance'][name]['order']=order;by[name]['order']=order
profiles={
 '玉虚':{'policy':'以师承、驻地护持和本地往来为主；开放区与封育区分别登记，不凭站队征税。','core':['太和城','白石城','溪云城','春水府','青萝城']},
 '玄都':{'policy':'重契约与经营许可；矿产、港务和市集以明确约定往来，不自动封锁异阵营。','core':['金阙城','望岫城','绛阙城','镇海城']},
 '无相':{'policy':'强调本门庇护与产地边界；成员和来客权限分明，不把外人默认视为敌人。','core':['永夜关','暮烟城','归寂城']},
}
for n in d['nodes']:
 if n['type']=='sect':
  n['geographicLean']=n['camp'];n['policyNote']=profiles[n['camp']]['policy']
  if n['name']=='太微道宫':n['note']='移至玉虚北侧内陆联系带；古老地方强宗，无独立第四极。与玉虚亲近但保留自主权。'
for camp,profile in profiles.items():
 for city in profile['core']:
  by[city]['regionalInfluence']=camp;by[city]['policyNote']=profile['policy']
  d['governance'][city]['regionalInfluence']=camp
exceptions={
 '承天城':'玉虚与玄都接触带：太微、书院及玄都共同城约，承担跨圈交涉，不因靠近哪宗就单方改约。',
 '临渊城':'玄都倾向的沧溟水府所护持，地理上贴近无相北侧，是少量跨圈通商接口。',
 '通济城':'三方接触带自治城市；交通便利，不因此自动成为最近大宗属城。',
 '赤水城':'玉虚近侧自治河谷城，日常往来偏向西南但保持自己的城约。',
}
for name,note in exceptions.items():by[name]['policyNote']=note;d['governance'][name]['policyNote']=note
d['politicalGeography']={'principle':'同倾向宗门总体更近，道路、产地与城市政策共同表达影响；独立站队不等于行政管辖。','profiles':profiles,'exceptions':exceptions,'fourthPole':False}
for s in d['sects']:s['pos']=by[s['name']]['pos']
for name,g in d['governance'].items():g['pos']=by[name]['pos']
for row in d['resources']:row[-1]=by[row[0]]['pos']
d['geographyGroups'].update({'太微道宫':['太微山前联系带',[39,47]],'沧溟水府':['沧溟河港',[77,24]],'百药谷':['百药林谷',[45,84]],'枯骨寺':['古寺东陵',[84,40]],'望舒观':['涵虚湖区',[47,59]],'归藏书院':['归藏盆地',[55,34]]})
for n in d['nodes']:
 if n['name'] in ['回澜滩','涵虚泽','蒹葭浦']:n['geography']='涵虚湖区'
 if n['name'] in ['埋骨原','玄荒战墟','寂照林','无名陵']:n['geography']='古寺东陵'
 if n['name'] in ['潮生泽','沧溟滩','沉舟湾']:n['geography']='沧溟河港'
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
subprocess.run([sys.executable,str(ROOT/'tools/world_map/plan_bai_chuan_routes_20261027.py'),'--graph-only'],check=True)
d=json.loads(p.read_text(encoding='utf-8'));T=d['count']['transit'];R=d['count']['routes'];G=d['count']['graphTotal']
current=ROOT/'docs/plans/世界与探索/百川节点分层与过渡路线规划_20261027.md'
s=current.read_text(encoding='utf-8')
s=s.replace('287',str(T)).replace('169',str(R)).replace('410',str(G))
s=s.replace('例如太微倾向玉虚，不直接跨图连线至玉虚。','太微已移入玉虚北侧联系带，通过沿途城市往来，不另建无视地理的直达线。')
s=s.replace('中部由城市、书院、河谷与渡口串接，保留跨片区绕行；东北太微、南部血河教和蚀日宫等偏远山门周边更疏，沿途野外与空节点承担远行感。地方宗门距最近一级山门约至少图幅短边五分之一，不做十个二级围着三大平均排队。','太微从东北角移至玉虚北侧内陆联系带，不再形成第四极。地方宗门总体靠近所倾向的大宗，城镇、产地和经营往来随之调整；临渊城等少量跨圈接口保留。远近表达影响与协作成本，不自动改变产权或城约。详见[三极与政策地理修订](百川三极布局与政策地理修订_20261027.md)。')
s=s.replace('本轮最新主入口，覆盖此前改名与资源分类表。','本轮地图主入口；最新空间与政策关系以[三极与政策地理修订](百川三极布局与政策地理修订_20261027.md)为准，覆盖此前刻意打散站队的排布。')
current.write_text(s,encoding='utf-8')
# Correct coordinate tables and replace stale route totals in the current authority set.
for pdoc in (ROOT/'docs/plans').glob('*.md'):
 if pdoc.name not in ['百川宗门站队与城镇轮值及资源节点规划_20261027.md','百川区域仙盟大势力与天人议盟规划.md','百川势力与节点分布规划_20261027.md','百川城镇与野外节点名录草案_20261027.md','百川命名与资源归属修订_20261027.md','百川知名资源产地与宗门供给规划_20261027.md','大陆格局与百川区域设计.md','百川固定方格世界地图设计.md']:continue
 text=pdoc.read_text(encoding='utf-8')
 for name,node in by.items():
  if node['type']=='sect':text=re.sub(r'(\| '+re.escape(name)+r' \| )\d+／\d+( \|)',lambda m:m[1]+str(node['pos'][0])+'／'+str(node['pos'][1])+m[2],text)
 text=text.replace('287个空过渡节点、169条候选路线',f'{T}个空过渡节点、{R}条候选路线')
 header,tail=text.split('\n',1)
 note='> 最新空间关系：[三极与政策地理修订](百川三极布局与政策地理修订_20261027.md)优先。太微移入玉虚北侧联系带；站队、产地与城镇政策总体遵循地理邻近，保留少量跨圈协议。旧稿“同阵营刻意分散、东北太微”等排布理由已替代，独立宗门非行政隶属的规则保持。\n\n'
 pdoc.write_text(header+'\n\n'+note+tail.lstrip('\n'),encoding='utf-8')
for path,prefix in [(ROOT/'docs/README.md','plans/'),(ROOT/'docs/策划案摘要/README.md','../plans/')]:
 lines=path.read_text(encoding='utf-8').splitlines();lines[2]=f'百川最新三极布局（2026-10-27）：[空间与政策修订]({prefix}百川三极布局与政策地理修订_20261027.md)、[节点与路线]({prefix}百川节点分层与过渡路线规划_20261027.md)。玉虚、玄都、无相为三极，太微移入西部内陆联系带；地方宗门总体邻近所倾向大宗，资源、城镇与政策一起表达影响，保留少量跨圈接口，独立站队不等于行政隶属。123具名地点＋{T}空节点，{R}条候选路线；仙盟独立。未改运行时。'
 path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
majors={s['camp']:s for s in d['sects'] if s['level']==1}
audit=[]
for s in d['sects']:
 if s['level']==1:continue
 nearest=min(majors,key=lambda c:math.dist(s['pos'],majors[c]['pos']))
 assert nearest==s['camp'],(s['name'],nearest,s['camp'])
 audit.append((s['name'],nearest,round(math.dist(s['pos'],majors[nearest]['pos']),1)))
print(json.dumps({'routes':R,'transit':T,'graphTotal':G,'minorProximity':audit},ensure_ascii=True))
