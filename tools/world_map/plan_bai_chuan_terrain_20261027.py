"""Add geography and route roles to the planning overlay, without movement rules."""
import json, math, heapq, itertools
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'));by={n['id']:n for n in d['nodes']};names={n['name']:n for n in d['nodes']}
areas=[
 {'name':'镇岳西岭','kind':'山地','color':'#e8e2d5','labelPos':[11,17],'polygon':[[3,16],[15,18],[24,37],[20,57],[9,68],[3,53]]},
 {'name':'玄都台原','kind':'高地','color':'#eee6d5','labelPos':[47,4],'polygon':[[24,3],[66,3],[78,12],[66,29],[45,32],[29,24]]},
 {'name':'归藏盆地','kind':'盆地','color':'#f0eddc','labelPos':[58,34],'polygon':[[26,28],[49,29],[65,29],[71,40],[54,48],[36,44],[25,38]]},
 {'name':'涵虚水乡','kind':'河谷湖泽','color':'#e4ede5','labelPos':[51,47],'polygon':[[24,43],[38,45],[51,42],[70,43],[79,54],[67,67],[45,65],[26,55]]},
 {'name':'沧溟河港','kind':'水岸','color':'#e4ece7','labelPos':[86,7],'polygon':[[72,4],[98,5],[98,32],[85,37],[72,28],[65,18]]},
 {'name':'玉虚叠嶂','kind':'山地','color':'#e1e8d9','labelPos':[11,94],'polygon':[[7,64],[25,57],[39,66],[34,84],[21,94],[8,89]]},
 {'name':'百药南林','kind':'林谷','color':'#dfe8d8','labelPos':[45,96],'polygon':[[26,77],[43,74],[59,79],[66,92],[57,98],[29,97],[22,88]]},
 {'name':'古寺东陵','kind':'丘陵','color':'#e5e1db','labelPos':[95,40],'polygon':[[77,30],[96,31],[98,53],[86,58],[76,50],[74,40]]},
 {'name':'幽泉深谷','kind':'深谷湿地','color':'#e2dfdf','labelPos':[77,94],'polygon':[[58,70],[78,67],[96,73],[97,94],[82,98],[62,95],[56,85]]},
]
ridges=[
 {'name':'西岭山脊','points':[[8,22],[13,31],[9,42],[15,53],[12,61]]},
 {'name':'断岳余脉','points':[[29,25],[40,28],[52,25],[63,20],[75,10]]},
 {'name':'玉虚山脊','points':[[20,61],[25,68],[20,80],[27,90]]},
 {'name':'东陵低脊','points':[[80,33],[87,40],[82,48],[90,55]]},
]
waters=[
 {'name':'百川中流','points':[[25,45],[34,46],[42,43],[51,47],[58,44],[66,46],[74,40],[84,33],[94,25],[99,24]],'width':4},
 {'name':'溪云支流','points':[[28,88],[35,81],[39,74],[45,67],[48,59],[51,47]],'width':3},
 {'name':'暮烟支流','points':[[83,91],[78,82],[75,74],[72,65],[68,56],[66,46]],'width':3},
]
lakes=[{'name':'涵虚泽湖面','polygon':[[49,48],[52,47],[55,49],[54,51],[51,52],[49,50]]},
 {'name':'镜月泽湖面','polygon':[[71,57],[73,56],[75,58],[74,60],[72,60],[70,59]]}]
def inside(point,polygon):
 x,y=point;result=False
 for a,b in zip(polygon,polygon[1:]+polygon[:1]):
  if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:result=not result
 return result
for n in d['nodes']:
 matched=[a for a in areas if inside(n['pos'],a['polygon'])]
 n['terrainRegion']=matched[-1]['name'] if matched else '片区间过渡地带'
d['terrainPlanning']={'status':'示意地貌，非通行遮罩或正式美术','areas':areas,'ridges':ridges,'waters':waters,'lakes':lakes,'note':'河流和山脊是构图意向，不新增水域格、渡河限制、地形耗时或产物。仙盟不占地貌分区。'}
adj={ident:[] for ident in by}
for r in d['routes']:
 w=r['layoutLength'];adj[r['from']].append((r['to'],w,r['id']));adj[r['to']].append((r['from'],w,r['id']))
def path(a,b):
 start=names[a]['id'];target=names[b]['id'];q=[(0,start,[])];seen=set()
 while q:
  cost,x,route=heapq.heappop(q)
  if x in seen:continue
  seen.add(x)
  if x==target:return route
  for y,w,r in adj[x]:
   if y not in seen:heapq.heappush(q,(cost+w,y,route+[r]))
 raise ValueError((a,b))
corridors=[
 {'name':'西南至东部通路','anchors':['玉虚仙宗','白石城','溪云城','春水府','通济城','无相圣宗','永夜关']},
 {'name':'北部通路','anchors':['春水府','承天城','绛阙城','玄都天府','金阙城']},
 {'name':'东北河港通路','anchors':['通济城','归寂城','临渊城','镇海城']},
 {'name':'西部支接','anchors':['太和城','白石城','赤水城','望岫城','承天城']},
 {'name':'南部通路','anchors':['青萝城','溪云城','暮烟城','无相圣宗']},
]
trunks=set()
for corridor in corridors:
 ids=[]
 for a,b in zip(corridor['anchors'],corridor['anchors'][1:]):ids.extend(path(a,b))
 corridor['routes']=list(dict.fromkeys(ids));trunks.update(ids)
for r in d['routes']:
 endpoints=[by[r['from']],by[r['to']]]
 r['usage']='主要通路' if r['id'] in trunks else '野外深入' if any(n['type']=='wild' for n in endpoints) else '地方支路'
d['corridors']=corridors
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
crossings=[]
for a,b in itertools.combinations(d['routes'],2):
 if {a['from'],a['to']}&{b['from'],b['to']}:continue
 x,y,z,w=[by[i]['pos'] for i in [a['from'],a['to'],b['from'],b['to']]]
 if cross(x,y,z)*cross(x,y,w)<-1e-8 and cross(z,w,x)*cross(z,w,y)<-1e-8:crossings.append([a['id'],b['id']])
assert not crossings
# Mark projected river crossings for later bridge/ford design; they are not extra locations.
water_checks=[]
for r in d['routes']:
 a,b=by[r['from']]['pos'],by[r['to']]['pos']
 for water in waters:
  if any(cross(a,b,c)*cross(a,b,e)<-1e-8 and cross(c,e,a)*cross(c,e,b)<-1e-8 for c,e in zip(water['points'],water['points'][1:])):
   water_checks.append({'route':r['id'],'water':water['name'],'status':'待按最终河岸安排桥渡或沿岸绕行，不增具名节点'})
d['terrainPlanning']['crossWaterReview']=water_checks
d['routeAudit']={'properCrossings':len(crossings),'waterReview':len(water_checks),'roles':{role:sum(r['usage']==role for r in d['routes']) for role in ['主要通路','地方支路','野外深入']}}
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(d['routeAudit'],ensure_ascii=True))
