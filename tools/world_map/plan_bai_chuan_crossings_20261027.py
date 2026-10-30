"""Resolve bridge and mountain-pass positions in the planning graph."""
import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'))
nodes={n['id']:n for n in d['nodes']};transit={n['id']:n for n in d['transitNodes']}
def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def meet(a,b,c,e):
 ab=[b[i]-a[i] for i in (0,1)];ce=[e[i]-c[i] for i in (0,1)];ca=[c[i]-a[i] for i in (0,1)]
 determinant=cross(ab,ce)
 if abs(determinant)<1e-9:return None
 t=cross(ca,ce)/determinant;u=cross(ca,ab)/determinant
 if 1e-6<t<1-1e-6 and 1e-6<u<1-1e-6:return t,[round(a[i]+t*ab[i],4) for i in (0,1)]
 return None
features=[];water_records=[];pass_records=[]
for r in d['routes']:
 a,b=nodes[r['from']]['pos'],nodes[r['to']]['pos'];candidates=[]
 for kind,items in [('water',d['terrainPlanning']['waters']),('ridge',d['terrainPlanning']['ridges'])]:
  for item in items:
   for c,e in zip(item['points'],item['points'][1:]):
    hit=meet(a,b,c,e)
    if not hit:continue
    t,pos=hit
    if any(math.dist(pos,old['pos'])<.12 for old in candidates):continue
    candidates.append({'t':t,'pos':pos,'source':item['name'],'sourceType':kind})
 used=set();allocated=[]
 for candidate in sorted(candidates,key=lambda x:x['t']):
  free=[transit[i] for i in r['via'] if i not in used]
  if free:
   node=min(free,key=lambda n:math.dist(n['pos'],candidate['pos']))
  else:
   ident=r['id'].replace('route','cross')+'_'+str(len(allocated)+1)
   node={'id':ident,'type':'transit','name':None,'pos':candidate['pos'],'routeId':r['id']}
   d['transitNodes'].append(node);transit[ident]=node;r['via'].append(ident)
  used.add(node['id']);node['pos']=candidate['pos']
  feature_kind=('石桥' if r['usage']=='主要通路' else '木桥') if candidate['sourceType']=='water' else '山口'
  feature={'id':f'bc_landmark_{len(features)+1:03}','nodeId':node['id'],'routeId':r['id'],'pos':candidate['pos'],'kind':feature_kind,'source':candidate['source'],'endpoints':[nodes[r['from']]['name'],nodes[r['to']]['name']],'sourceType':candidate['sourceType'],'note':'路线上的普通地形过渡，不新增具名地点、费用、奖励或能力门槛。'}
  node['routeFeature']=feature_kind;node['landmarkId']=feature['id'];features.append(feature);allocated.append(feature['id'])
  (water_records if candidate['sourceType']=='water' else pass_records).append(feature)
 ab=[b[i]-a[i] for i in (0,1)];den=sum(v*v for v in ab)
 r['via'].sort(key=lambda i:sum((transit[i]['pos'][j]-a[j])*ab[j] for j in (0,1))/den)
 r['landmarks']=allocated
 r['journeyNote']={'主要通路':'以城际与跨片区往来为主，保留路口和桥口辨识，不保证无遭遇。','地方支路':'连接镇坊、山门与附近生活地点，和长途通路区分。','野外深入':'连接野外与遗迹，可有尽头；到达后由玩家主动进入探索。'}[r['usage']]
merged=[]
for first in list(water_records):
 if first['id'] in merged:continue
 for second in list(water_records):
  if second['id']==first['id'] or second['id'] in merged:continue
  if first['source']!=second['source'] or math.dist(first['pos'],second['pos'])>=1:continue
  route=next(r for r in d['routes'] if r['id']==second['routeId'])
  old=transit[second['nodeId']];old.pop('routeFeature',None);old.pop('landmarkId',None)
  common=transit[first['nodeId']]
  common['routeIds']=list(dict.fromkeys(common.get('routeIds',[common['routeId']])+[route['id']]))
  if common['id'] not in route['via']:route['via'].append(common['id'])
  near=min([nodes[route['from']],nodes[route['to']]],key=lambda n:math.dist(n['pos'],common['pos']))
  old['pos']=[round(common['pos'][i]+.6*(near['pos'][i]-common['pos'][i]),4) for i in (0,1)]
  a,b=nodes[route['from']]['pos'],nodes[route['to']]['pos'];ab=[b[i]-a[i] for i in (0,1)]
  route['via'].sort(key=lambda ident:sum((transit[ident]['pos'][j]-a[j])*ab[j] for j in (0,1)))
  first_route=next(r for r in d['routes'] if r['id']==first['routeId'])
  if near['id'] in (first_route['from'],first_route['to']):
   if old['id'] not in first_route['via']:first_route['via'].append(old['id'])
   aa,bb=nodes[first_route['from']]['pos'],nodes[first_route['to']]['pos'];direction=[bb[i]-aa[i] for i in (0,1)]
   first_route['via'].sort(key=lambda ident:sum((transit[ident]['pos'][j]-aa[j])*direction[j] for j in (0,1)))
   old['routeIds']=[route['id'],first_route['id']]
  route['landmarks']=[first['id'] if ident==second['id'] else ident for ident in route['landmarks']]
  second['sharedLandmarkId']=first['id'];second['nodeId']=first['nodeId'];second['pos']=first['pos']
  first['sharedRoutes']=list(dict.fromkeys(first.get('sharedRoutes',[first['routeId']])+[route['id']]))
  merged.append(second['id'])
features=[f for f in features if f['id'] not in merged]
for r in d['routes']:
 r['points']=[nodes[r['from']]['pos']]+[transit[i]['pos'] for i in r['via']]+[nodes[r['to']]['pos']]
d['terrainPlanning']['crossingLandmarks']=features
d['terrainPlanning']['resolvedWaterCrossings']=water_records
d['terrainPlanning']['resolvedPasses']=pass_records
for record in d['terrainPlanning']['crossWaterReview']:
 record['status']='已在示意图定位普通桥位；最终河岸与美术线形仍需对齐。'
d['count']['transit']=len(d['transitNodes']);d['count']['graphTotal']=len(d['nodes'])+len(d['transitNodes'])
d['routeAudit']['waterLandmarks']=len(water_records)-len(merged);d['routeAudit']['passLandmarks']=len(pass_records)
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'waterCrossings':len(water_records),'bridgeLocations':len(water_records)-len(merged),'passes':len(pass_records),'transit':len(d['transitNodes']),'graphTotal':d['count']['graphTotal']},ensure_ascii=True))
for f in water_records:print(f['kind']+' '+ '—'.join(f['endpoints'])+' '+f['source']+' '+str(f['pos']))
