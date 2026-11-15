"""Build a reviewable planning graph; no runtime data or travel rules are changed."""
import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
P=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(P.read_text(encoding='utf-8'))
renames={'玉虚道宗':'玉虚仙宗','玄都仙府':'玄都天府','断岳矿山':'断岳山','长生药圃':'长生坞','古木禁林':'不老林','盘龙藤海':'盘龙涧','玉髓原':'太素原','青芝圃':'长生坞'}
def rename(s):
 for a,b in renames.items():s=s.replace(a,b)
 return s
d=json.loads(rename(json.dumps(d,ensure_ascii=False)))
nodes=d['nodes']
for n in nodes:
 if n['type']=='resource':n['type']='wild';n['resourceSite']=True
for i,n in enumerate(nodes):
 n['id']=f'bc_place_{i+1:03}'
 # Resolve coincident planning pins, e.g. a cavern and its nearby named mountain.
 while any(math.dist(n['pos'],p['pos'])<1.4 for p in nodes[:i]):
  n['pos']=[round(n['pos'][0]+1.2,2),round(n['pos'][1]+.9,2)]
pos={n['name']:n['pos'] for n in nodes}
for s in d['sects']:s['pos']=pos[s['name']]
for name,g in d['governance'].items():g['pos']=pos[name]
for r in d['resources']:r[-1]=pos[r[0]]
for g in d['neighborhoods']:
 for k in ('city','town','market'):g[k+'Pos']=pos[g[k]]
 for w in g['wild']:w[1]=pos[w[0]]
 g['res'][-1]=pos[g['res'][0]]
N=len(nodes)
def distance(a,b):return math.dist(nodes[a]['pos'],nodes[b]['pos'])
pairs=sorted((distance(a,b),a,b) for a in range(N) for b in range(a+1,N))
parent=list(range(N))
def root(a):
 while parent[a]!=a:parent[a]=parent[parent[a]];a=parent[a]
 return a
edges={}
# A connected sparse skeleton, then nearby alternatives weighted toward lived-in areas.
for length,a,b in pairs:
 x,y=root(a),root(b)
 if x!=y:parent[x]=y;edges[a,b]='骨架'
degree=[0]*N
for a,b in edges:degree[a]+=1;degree[b]+=1
def nearby_path(a,b):
 adj={i:[] for i in range(N)}
 for x,y in edges:adj[x].append((y,distance(x,y)));adj[y].append((x,distance(x,y)))
 import heapq
 q=[(0,a)];seen={}
 while q:
  cost,x=heapq.heappop(q)
  if x in seen:continue
  seen[x]=cost
  if x==b:return cost
  for y,w in adj[x]:
   if y not in seen:heapq.heappush(q,(cost+w,y))
 return float('inf')
for length,a,b in pairs:
 if (a,b) in edges or length>14:continue
 cap_a=4 if nodes[a]['type']=='city' else 3
 cap_b=4 if nodes[b]['type']=='city' else 3
 if degree[a]>=cap_a or degree[b]>=cap_b:continue
 ratio=1.45 if 'city' in [nodes[a]['type'],nodes[b]['type']] else 1.9
 if nearby_path(a,b)>length*ratio:
  edges[a,b]='支路';degree[a]+=1;degree[b]+=1
routes=[];transit=[]
for i,((a,b),role) in enumerate(edges.items(),1):
 na,nb=nodes[a],nodes[b];length=distance(a,b)
 # Close settled locations are tighter; remote stretches contain more stepping stones.
 steps=max(1,round(length/(3.3 if na['type']=='city' or nb['type']=='city' else 4.2)))
 mids=[]
 for k in range(1,steps+1):
  t=k/(steps+1);p=[round(na['pos'][j]*(1-t)+nb['pos'][j]*t,3) for j in (0,1)]
  ident=f'bc_transit_{i:03}_{k:02}'
  transit.append({'id':ident,'type':'transit','name':None,'pos':p,'routeId':f'bc_route_{i:03}'})
  mids.append(ident)
 routes.append({'id':f'bc_route_{i:03}','from':na['id'],'to':nb['id'],'via':mids,'role':role,'layoutLength':round(length,2)})
d['routes']=routes;d['transitNodes']=transit
d['count'].update(wildAndRuins=70,resourceSites=21,resourceSitesIncludedInWild=True,transit=len(transit),routes=len(routes),graphTotal=N+len(transit))
d['routeNote']='规划连线，未按最终山河校正；相交不自动连通，空节点无常驻内容，不把边数解释为旅行时间。'
P.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
import sys
if '--graph-only' in sys.argv:
 print(json.dumps({'places':N,'routes':len(routes),'transit':len(transit)}))
 sys.exit(0)
files=[ROOT/'docs/plans'/n for n in ['百川宗门站队与城镇轮值及资源节点规划_20261027.md','百川区域仙盟大势力与天人议盟规划.md','百川势力与节点分布规划_20261027.md','百川城镇与野外节点名录草案_20261027.md','大陆格局与百川区域设计.md','百川固定方格世界地图设计.md','百川知名资源产地与宗门供给规划_20261027.md','百川命名与资源归属修订_20261027.md']]
notice=f'> 本轮最新裁定：[节点与过渡路线规划](百川节点分层与过渡路线规划_20261027.md)优先。一级为玉虚仙宗、玄都天府、无相圣宗。123个具名世界地点，其中70个野外（含21个有资源归属的产地）；另有{len(transit)}个空过渡节点、{len(routes)}条候选路线。仙盟独立不接外界。旧资源分类为功能视图，不额外加21个地点；旧改名理由与分类统计已替代。未改运行时。\n\n'
for p in files:
 s=rename(p.read_text(encoding='utf-8'));head,tail=s.split('\n',1)
 p.write_text(head+'\n\n'+notice+tail.lstrip('\n'),encoding='utf-8')
for p,prefix in [(ROOT/'docs/README.md','plans/'),(ROOT/'docs/策划案摘要/README.md','../plans/')]:
 lines=p.read_text(encoding='utf-8').splitlines();lines[2]=f'百川最新地图规划（2026-10-27）：[节点与过渡路线]({prefix}百川节点分层与过渡路线规划_20261027.md)。一级恢复玉虚仙宗、玄都天府、无相圣宗；十地方宗门独立站队。资源产地属于野外节点，以归属与供给功能区分，不铺独立小采集点。123具名地点（70野外含21产地）＋{len(transit)}空过渡节点，{len(routes)}条候选路线；另1仙盟暂不相接，取消青溪里世界节点。先规划后素材，未改运行时。'
 p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
assert len({root(i) for i in range(N)})==1
assert all(r['via'] for r in routes)
assert len({n['name'] for n in nodes})==123
assert len(d['detachedNodes'])==1 and not d['detachedNodes'][0]['connections']
print(json.dumps({'places':N,'wild':sum(n['type']=='wild' for n in nodes),'resourceWild':sum(bool(n.get('resourceSite')) for n in nodes),'routes':len(routes),'transit':len(transit),'connected':True}))
