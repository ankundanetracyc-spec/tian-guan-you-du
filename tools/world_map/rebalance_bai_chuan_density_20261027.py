"""Spread city locations, occupy the central corridor, and keep alliance detached."""
from pathlib import Path
import json, math, subprocess, sys, re
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'));by={n['name']:n for n in d['nodes']}
def min_city_gap():
 cities=[n for n in d['nodes'] if n['type']=='city']
 return min(math.dist(a['pos'],b['pos']) for i,a in enumerate(cities) for b in cities[i+1:])
before=min_city_gap()
cities={'太和城':[11,68],'白石城':[28,65],'溪云城':[49,74],'青萝城':[30,87],
 '金阙城':[34,10],'望岫城':[20,29],'绛阙城':[54,19],'承天城':[43,37],
 '赤水城':[25,48],'春水府':[49,55],'通济城':[65,51],
 '镇海城':[88,12],'临渊城':[70,26],'归寂城':[86,41],'暮烟城':[72,83],'永夜关':[91,63]}
# Carry dependent settlements with their city, then tune a few river/shore positions.
old_cities={name:by[name]['pos'][:] for name in cities}
for n in d['nodes']:
 if n['type'] in ('town','market','village','dock') and n.get('city') in cities:
  name=n['city'];delta=[cities[name][i]-old_cities[name][i] for i in (0,1)]
  n['pos']=[round(n['pos'][i]+delta[i],2) for i in (0,1)]
for name,pos in cities.items():by[name]['pos']=pos
positions={
 '玄都天府':[43,19],'太微道宫':[35,50],'抱朴山':[43,65],'百药谷':[36,91],
 '丹鼎宗':[61,8],'归藏书院':[62,37],'沧溟水府':[72,17],
 '血河教':[65,82],'蚀日宫':[88,88],
 '含光镇':[43,9],'琼华坊':[47,14],'丹丘原':[33,21],'垂虹岭':[47,25],'断岳山':[51,14],
 '洗心镇':[14,78],'松月坊':[19,70],'坐忘谷':[15,87],'清籁涧':[25,79],'太素原':[24,73],
 '澜浦渡':[95,18],'白蘋里':[91,24],'听风镇':[79,10],
 '夜泊坊':[81,55],'听澜镇':[87,69],
 '石雾原':[50,43],'潜龙原':[51,36],'天碑原':[59,44],'镜月泽':[72,58],'碎星滩':[61,63],
 '东林地':[58,55],'灵草谷':[56,67],'雷泽':[75,44],'落星墟':[68,69],
 '鹿鸣林':[39,83],'长生坞':[37,65],'濯缨泉':[48,68],
 '万毒谷':[35,86],'盘龙涧':[42,93],'不老林':[52,86],
 '天籁长峡':[31,40],'玄霜坞':[28,47],'砺青崖':[58,29],'含章窟':[52,31],
 '鸣玉溪':[74,30],'藏蜕林':[85,31],
 '回澜滩':[44,49],'涵虚泽':[52,49],'蒹葭浦':[55,55],
 '烟柳镇':[53,59],'桑落镇':[43,54],'荷汀镇':[56,49],
 '无生谷':[75,75],'蕴辉岩':[77,78],
}
for name,pos in positions.items():by[name]['pos']=pos
d['politicalGeography']['centralCorridor']={'allianceReservedArea':False,'nodes':['承天城','春水府','通济城','归藏书院','天碑原','东林地','镜月泽','雷泽','碎星滩','灵草谷','落星墟'],'note':'中部由城镇、宗门、野外、产地及过渡路线承接，不为仙盟预留空白。'}
d['politicalGeography']['densityRevision']={'priorMinCityGap':round(before,2),'minCityGap':round(min_city_gap(),2),'note':'城市疏密为全图关系检查，不是游戏内最小距离限制。'}
d['detachedNodes'][0]['note']='单个普通尺度的独立节点，暂不接外界；不占大世界中心空白或政治辖区。'
for n in d['nodes']:
 if n['name']=='太微道宫':n['note']='玉虚北侧内陆联系带的地方强宗；靠近太微的城市不构成第四极。'
assert all(0<=v<=100 for n in d['nodes'] for v in n['pos'])
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
subprocess.run([sys.executable,str(ROOT/'tools/world_map/plan_bai_chuan_routes_20261027.py'),'--graph-only'],check=True)
d=json.loads(p.read_text(encoding='utf-8'));T=d['count']['transit'];R=d['count']['routes'];G=d['count']['graphTotal'];by={n['name']:n for n in d['nodes']}
# Synchronize the current authority text, replacing old totals and section prose.
paths=[ROOT/'docs/plans'/name for name in ['百川三极布局与政策地理修订_20261027.md','百川节点分层与过渡路线规划_20261027.md','百川宗门站队与城镇轮值及资源节点规划_20261027.md','百川势力与节点分布规划_20261027.md','百川城镇与野外节点名录草案_20261027.md','百川命名与资源归属修订_20261027.md','百川区域仙盟大势力与天人议盟规划.md','百川知名资源产地与宗门供给规划_20261027.md','百川固定方格世界地图设计.md','大陆格局与百川区域设计.md']]
notice=f'> 最新疏密修订：玄都右移至43／19；西南城市拉开，中部由承天、春水、通济及野外联系带承接。仙盟只是一处图外独立节点，不占中心空地。123个具名地点＋{T}空节点，{R}条候选路线。位置以结构化规划数据与[三极布局主案](百川三极布局与政策地理修订_20261027.md)本轮补充为准。\n\n'
for path in paths:
 s=path.read_text(encoding='utf-8')
 s=s.replace('283',str(T)).replace('168',str(R)).replace('406',str(G))
 s=s.replace('32／19','43／19').replace('39／51','35／50').replace('太微39／51、镇岳12／47、抱朴43／70、百药45／88','太微35／50、镇岳12／47、抱朴43／65、百药36／91')
 s=s.replace('丹鼎55／10、归藏58／35、沧溟67／24','丹鼎61／8、归藏62／37、沧溟72／17').replace('枯骨83／36、血河65／80、蚀日88／86','枯骨83／36、血河65／82、蚀日88／88').replace('沧溟水府位于67／24','沧溟水府位于72／17')
 s=s.replace('三大位置保持，不为调整太微重排全图。','本轮玄都由32／19右移至43／19，玉虚与无相位置保持；同步疏解西南城市，补充中央联系带。')
 s=s.replace('三边长度约56、62、66','三边长度约59、58、62').replace('三边约为图幅的56、62、66个百分点','三边约为图幅的59、58、62个百分点')
 for name,n in by.items():
  if n['type']=='sect':s=re.sub(r'(\| '+re.escape(name)+r' \| )\d+／\d+( \|)',lambda m:m[1]+str(n['pos'][0])+'／'+str(n['pos'][1])+m[2],s)
 head,tail=s.split('\n',1);path.write_text(head+'\n\n'+notice+tail.lstrip('\n'),encoding='utf-8')
for path,prefix in [(ROOT/'docs/README.md','plans/'),(ROOT/'docs/策划案摘要/README.md','../plans/')]:
 lines=path.read_text(encoding='utf-8').splitlines();lines[2]=f'百川最新疏密规划（2026-10-27）：[三极布局与中部修订]({prefix}百川三极布局与政策地理修订_20261027.md)、[节点路线]({prefix}百川节点分层与过渡路线规划_20261027.md)。玄都右移43／19，西南城市拉开，中央以城镇与野外联系带填补；仙盟仅图外独立节点，不预留中心区域。123具名地点＋{T}空节点、{R}条路线；地理反映三方亲疏，非行政隶属。未改运行时。'
 path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({'routes':R,'transit':T,'graphTotal':G,'cityGapBefore':round(before,2),'cityGapAfter':round(min_city_gap(),2)},ensure_ascii=True))
