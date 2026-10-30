"""Curve planning roads with a shared spatial deformation, preserving anchors/topology."""
import json, math, collections
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
P=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'

def distance_to_segment(p,a,b):
 dx,dy=b[0]-a[0],b[1]-a[1];den=dx*dx+dy*dy
 t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/den)) if den else 0
 return math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy)

def audit_curves(edges):
 # Spatial buckets avoid testing every pair of distant sampled line segments.
 segments=[];buckets=collections.defaultdict(list);pairs=set()
 for key,edge in edges.items():
  for index,(a,b) in enumerate(zip(edge['points'],edge['points'][1:])):
   ident=len(segments);segments.append((key,index,a,b))
   for x in range(math.floor(min(a[0],b[0])/2),math.floor(max(a[0],b[0])/2)+1):
    for y in range(math.floor(min(a[1],b[1])/2),math.floor(max(a[1],b[1])/2)+1):
     for prior in buckets[x,y]:pairs.add((prior,ident))
     buckets[x,y].append(ident)
 def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
 failures=[]
 for i,j in pairs:
  k1,n1,a,b=segments[i];k2,n2,c,e=segments[j]
  if k1==k2 and abs(n1-n2)<=1:continue
  if max(min(a[0],b[0]),min(c[0],e[0]))>min(max(a[0],b[0]),max(c[0],e[0])):continue
  if max(min(a[1],b[1]),min(c[1],e[1]))>min(max(a[1],b[1]),max(c[1],e[1])):continue
  if cross(a,b,c)*cross(a,b,e)<-1e-10 and cross(c,e,a)*cross(c,e,b)<-1e-10:failures.append((k1,k2))
 return failures

def main():
 d=json.loads(P.read_text(encoding='utf-8'));nodes={n['id']:n for n in d['nodes']+d['transitNodes']}
 landmarks={f['nodeId'] for f in d['terrainPlanning']['crossingLandmarks']}
 fixed={n['id'] for n in d['nodes']}|landmarks
 for n in d['transitNodes']:n.setdefault('straightLayoutPos',n['pos'][:])
 original={i:(n['straightLayoutPos'] if i not in {x['id'] for x in d['nodes']} else n['pos']) for i,n in nodes.items()}
 fixed_points=[original[i] for i in fixed]
 terrain_segments=[]
 for feature in d['terrainPlanning']['waters']+d['terrainPlanning']['ridges']:
  terrain_segments.extend(zip(feature['points'],feature['points'][1:]))
 for feature in d['terrainPlanning']['lakes']:
  poly=feature['polygon'];terrain_segments.extend(zip(poly,poly[1:]+poly[:1]))
 edge_keys=set()
 for route in d['routes']:
  chain=[route['from']]+route['via']+[route['to']]
  edge_keys.update(tuple(sorted((a,b))) for a,b in zip(chain,chain[1:]))
 def warp(p,amplitude):
  distance=min([math.dist(p,q) for q in fixed_points]+[distance_to_segment(p,a,b) for a,b in terrain_segments])
  t=min(1,distance/2.8);fade=t*t*(3-2*t)
  x,y=p;dx=.85*math.sin(.24*x+.10*y)+.45*math.cos(.14*y-.15*x)
  dy=.75*math.cos(.18*x-.16*y)-.40*math.sin(.27*y+.10*x)
  return [round(x+amplitude*fade*dx,6),round(y+amplitude*fade*dy,6)]
 for amplitude in [2.0,1.6,1.2,.8]:
  edges={}
  for a,b in sorted(edge_keys):
   start,end=original[a],original[b];steps=max(12,math.ceil(math.dist(start,end)/.18))
   points=[warp([start[j]+(end[j]-start[j])*i/steps for j in (0,1)],amplitude) for i in range(steps+1)]
   points[0]=original[a][:] if a in fixed else warp(original[a],amplitude)
   points[-1]=original[b][:] if b in fixed else warp(original[b],amplitude)
   edges[a+'~'+b]={'from':a,'to':b,'points':points}
  failures=audit_curves(edges)
  if not failures:break
 else:raise AssertionError('Unable to obtain non-crossing curves')
 for ident,n in nodes.items():
  if ident not in fixed:n['pos']=warp(original[ident],amplitude)
 for r in d['routes']:
  chain=[r['from']]+r['via']+[r['to']];r['points']=[nodes[i]['pos'] for i in chain]
  rendered=[];segments=[]
  for a,b in zip(chain,chain[1:]):
   key='~'.join(sorted((a,b)));edge=edges[key];reverse=a!=edge['from']
   points=list(reversed(edge['points'])) if reverse else edge['points']
   rendered.extend(points if not rendered else points[1:]);segments.append({'edge':key,'reverse':reverse})
  r['renderPoints']=rendered;r['movementSegments']=segments
 d['roadCurveGeometry']={'status':'规划显示与未来行旅表现共用，未改变结算或运行时','edges':edges,'anchorRule':'具名地点、桥位、山口与水岸山脊交会固定；共享道路只保留一份曲线','amplitude':amplitude,'uniqueEdges':len(edges),'crossings':0,'maxTransitDisplacement':round(max(math.dist(n['pos'],n['straightLayoutPos']) for n in d['transitNodes']),3)}
 for i in fixed:assert nodes[i]['pos']==original[i]
 assert all(0<=v<=100 for edge in edges.values() for p in edge['points'] for v in p)
 # Curves must not drift through a different named place's marker.
 near=[]
 for key,edge in edges.items():
  for n in d['nodes']:
   if n['id'] in (edge['from'],edge['to']):continue
   if distance_to_segment(n['pos'],original[edge['from']],original[edge['to']])<.12:continue
   if any(distance_to_segment(n['pos'],a,b)<.12 for a,b in zip(edge['points'],edge['points'][1:])):near.append((key,n['name']))
 assert not near,near
 P.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print(json.dumps({k:v for k,v in d['roadCurveGeometry'].items() if k!='edges'},ensure_ascii=True))

if __name__=='__main__':main()
