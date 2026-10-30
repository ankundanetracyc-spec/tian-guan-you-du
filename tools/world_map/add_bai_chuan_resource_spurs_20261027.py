"""Attach a few short access paths to existing resource places, keeping original routes."""
import json,math
from pathlib import Path
from curve_bai_chuan_roads_20261027 import audit_curves,distance_to_segment
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(p.read_text(encoding='utf-8'))
assert not d.get('resourceSpurs'),'Resource access paths already planned; do not append duplicates.'
nodes={n['id']:n for n in d['nodes']+d['transitNodes']};routes={r['id']:r for r in d['routes']};edges=d['roadCurveGeometry']['edges'];spurs=[]
def intersection(a,b,c,e):
 ab=[b[i]-a[i] for i in (0,1)];ce=[e[i]-c[i] for i in (0,1)];ca=[c[i]-a[i] for i in (0,1)];det=ab[0]*ce[1]-ab[1]*ce[0]
 if abs(det)<1e-10:return False
 t=(ca[0]*ce[1]-ca[1]*ce[0])/det;u=(ca[0]*ab[1]-ca[1]*ab[0])/det
 return 1e-6<t<1-1e-6 and 1e-6<u<1-1e-6
preferred=['太素原','长生坞','万毒谷','断岳山','砺青崖','黑瘴大泽','凝素泽','鸣玉溪']
targets=sorted([n for n in d['nodes'] if n.get('resourceSite')],key=lambda n:preferred.index(n['name']) if n['name'] in preferred else 99)
for target in targets:
 if len(spurs)>=8:break
 candidates=[]
 for node in d['transitNodes']:
  if node.get('routeFeature') or node.get('isJunction') or node['routeId'] not in routes:continue
  host=routes[node['routeId']]
  if target['id'] in (host['from'],host['to']):continue
  length=math.dist(node['pos'],target['pos'])
  if 1<length<7:candidates.append((length,node,host))
 found=None
 for length,source,host in sorted(candidates,key=lambda item:item[0]):
  a,b=source['pos'],target['pos'];dx,dy=b[0]-a[0],b[1]-a[1]
  for bow in [.35,-.35,.7,-.7,.12,-.12]:
   control=[(a[0]+b[0])/2-dy/length*bow,(a[1]+b[1])/2+dx/length*bow]
   points=[[round((1-t)**2*a[j]+2*(1-t)*t*control[j]+t*t*b[j],6) for j in (0,1)] for t in [i/24 for i in range(25)]]
   points[0]=a[:];points[-1]=b[:]
   if any(intersection(x,y,c,e) for x,y in zip(points,points[1:]) for water in d['terrainPlanning']['waters'] for c,e in zip(water['points'],water['points'][1:])):continue
   if any(any(distance_to_segment(n['pos'],x,y)<.2 for x,y in zip(points,points[1:])) for n in d['nodes'] if n['id']!=target['id']):continue
   mid_id=f'bc_spur_mid_{len(spurs)+1:02}'
   new_edges={}
   segments=[]
   for start,end,part in [(source['id'],mid_id,points[:13]),(mid_id,target['id'],points[12:])]:
    first,last=sorted((start,end));key=first+'~'+last;reverse=start!=first
    new_edges[key]={'from':first,'to':last,'points':list(reversed(part)) if reverse else part};segments.append({'edge':key,'reverse':reverse})
   if audit_curves(dict(edges,**new_edges)):continue
   found=(source,host,points,new_edges,segments,mid_id,length);break
  if found:break
 if not found:continue
 source,host,points,new_edges,segments,mid_id,length=found
 route_id=f'bc_resource_spur_{len(spurs)+1:02}';source['isJunction']=True
 middle={'id':mid_id,'type':'transit','name':None,'pos':points[12],'straightLayoutPos':points[12],'routeId':route_id}
 d['transitNodes'].append(middle);nodes[mid_id]=middle
 r={'id':route_id,'from':source['id'],'to':target['id'],'via':[mid_id],'role':'资源便道','usage':'资源便道','points':[source['pos'],middle['pos'],target['pos']],'renderPoints':points,'movementSegments':segments,'isResourceSpur':True,'layoutLength':round(length,2),'journeyNote':'从原路空节点分岔，通往已有知名产地；不在岔路口自动发放资源。'}
 d['routes'].append(r);edges.update(new_edges)
 spurs.append({'routeId':route_id,'hostRoute':host['id'],'forkNode':source['id'],'resourceNode':target['id'],'resourceName':target['name'],'owner':target.get('owner'),'note':'产地已有其他连接，便道是额外近路，不强制把所有产地改成死路。'})
assert len(spurs)==8,len(spurs)
d['resourceSpurs']=spurs;d['count'].update(transit=len(d['transitNodes']),routes=len(d['routes']),graphTotal=len(d['nodes'])+len(d['transitNodes']),resourceSpurs=len(spurs))
d['roadCurveGeometry']['uniqueEdges']=len(edges);d['routeAudit']['roles']['资源便道']=8
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'spurs':[s['resourceName'] for s in spurs],'counts':d['count']},ensure_ascii=True))
