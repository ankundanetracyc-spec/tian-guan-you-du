import json, math, sys
from pathlib import Path
import numpy as np
from PIL import Image
sys.path.insert(0,'E:/tmp/CodexData/world_map_python_libs')
from shapely.geometry import LineString,Point
from shapely.strtree import STRtree
root=Path(__file__).resolve().parents[2]
base=root/'textures/world/bai_chuan/acceptance_20261030'
src=base/'display_layout_v3_candidate.json'
out=base/'display_layout_v3_spacing_candidate.json'
d=json.loads(src.read_text(encoding='utf8'))
W=3762
edges=d['displayGraph']['edges']; coords=d['displayEdges']
pos={k:np.array(v['drawPos'],float) for k,v in d['locations'].items()};pos.update({k:np.array(v,float) for k,v in d['transitPositions'].items()})
ids=list(pos); incident={k:[] for k in ids}
for key,e in edges.items():
 incident[e['from']].append(key);incident[e['to']].append(key)
img=np.array(Image.open(base/'master_3762.png').convert('RGB'))
keys=list(edges); idx={k:i for i,k in enumerate(keys)}
def build():
 lines=[LineString(coords[k]) for k in keys]
 return lines,STRtree(lines)
lines,tree=build()
def local_geometry(nid,p):
 result={}
 for key in incident[nid]:
  q=np.array(coords[key],float).copy(); e=edges[key]
  if e['from']==nid:q[0]=p
  else:q[-1]=p
  result[key]=LineString(q)
 return result
def safe(nid,p):
 if (p<8).any() or (p>W-8).any():return False
 color=img[round(p[1]),round(p[0])].astype(float)
 if color[2]>color[0]*1.15 and color[2]>color[1]*1.04:return False
 local=local_geometry(nid,p)
 near_other=[(n,Point(q)) for n,q in pos.items() if n!=nid and np.linalg.norm(q-p)<170]
 for n,point in near_other:
  if np.linalg.norm(pos[n]-p)<24:return False
 for key,line in local.items():
  if not line.is_simple or line.length<4:return False
  edge=edges[key]
  for n,q in pos.items():
   if n in (edge['from'],edge['to']):continue
   point=Point(q)
   if not line.bounds[0]-14<=q[0]<=line.bounds[2]+14 or not line.bounds[1]-14<=q[1]<=line.bounds[3]+14:continue
   dist=line.distance(point); along=line.project(point)
   if dist<5 or (dist<14 and along>18 and line.length-along>18):return False
  for j in tree.query(line.buffer(4.1)):
   otherkey=keys[j]
   if otherkey==key:continue
   other=local.get(otherkey,lines[j]); oe=edges[otherkey]
   shared={edge['from'],edge['to']}&{oe['from'],oe['to']}
   hit=line.intersection(other)
   for n in shared:hit=hit.difference(Point(p if n==nid else pos[n]).buffer(.02))
   if not hit.is_empty:return False
   a,b=line,other
   for n in shared:
    disk=Point(p if n==nid else pos[n]).buffer(8)
    a=a.difference(disk);b=b.difference(disk)
   if not a.is_empty and not b.is_empty and a.distance(b)<3.999:return False
 # A moved node may not land on an unrelated road.
 point=Point(p)
 for j in tree.query(point.buffer(14)):
  key=keys[j];e=edges[key]
  if nid in (e['from'],e['to']):continue
  line=lines[j];dist=line.distance(point);along=line.project(point)
  if dist<5 or (dist<14 and along>18 and line.length-along>18):return False
 return True
def apply(nid,p):
 global lines,tree
 old=pos[nid].copy();delta=p-old;pos[nid]=p
 if nid in d['locations']:
  v=d['locations'][nid];v['drawPos']=p.tolist()
  if 'rect' in v:v['rect']=[v['rect'][0]+delta[0],v['rect'][1]+delta[1],v['rect'][2]+delta[0],v['rect'][3]+delta[1]]
  v['displayOffsetFromLogical']=[v['displayOffsetFromLogical'][0]+delta[0],v['displayOffsetFromLogical'][1]+delta[1]]
 else:d['transitPositions'][nid]=p.tolist()
 for key in incident[nid]:
  e=edges[key];coord=coords[key]
  if e['from']==nid:coord[0]=p.tolist()
  else:coord[-1]=p.tolist()
 lines,tree=build()
def min_spacing(nid,p):
 return min(np.linalg.norm(q-p) for k,q in pos.items() if k!=nid)
def boundary(p):return min(p[0],p[1],W-p[0],W-p[1])
changes=[]
# Move only nodes immediately at map edge; camera padding handles all other edges.
for nid in sorted(ids,key=lambda n:boundary(pos[n])):
 old=pos[nid].copy(); b=boundary(old)
 if b>=90:break
 target=np.clip(old,90,W-90)
 options=[old+(target-old)*t for t in (1,.75,.5,.25)]
 for p in options:
  if boundary(p)<=b+.5 or min_spacing(nid,p)<24:continue
  if safe(nid,p):apply(nid,p);changes.append((nid,'edge',float(np.linalg.norm(p-old))));break
# Push apart the tightest transit points, without changing which points are joined.
for iteration in range(3):
 pairs=sorted((np.linalg.norm(pos[a]-pos[b]),a,b) for i,a in enumerate(ids) for b in ids[i+1:] if np.linalg.norm(pos[a]-pos[b])<40)
 if not pairs:break
 progress=False
 for distance,a,b in pairs:
  if np.linalg.norm(pos[a]-pos[b])>=40:continue
  choices=[x for x in (a,b) if x in d['transitPositions']]
  for nid in choices:
   other=b if nid==a else a;old=pos[nid].copy();direction=old-pos[other]
   if np.linalg.norm(direction)<.01:direction=np.array([1.,0.])
   angle=math.atan2(direction[1],direction[0]);candidates=[]
   for radius in [8,12,16,20,26,32,40]:
    for offset in [0,.35,-.35,.7,-.7,1.05,-1.05,1.4,-1.4,math.pi]:
     p=old+radius*np.array([math.cos(angle+offset),math.sin(angle+offset)])
     if boundary(p)<min(boundary(old),90):continue
     spacing=min_spacing(nid,p)
     if spacing <= distance+1:continue
     candidates.append((max(0,40-spacing)*100+radius,p))
   for _,p in sorted(candidates,key=lambda x:x[0]):
    if safe(nid,p):apply(nid,p);changes.append((nid,'spacing',float(np.linalg.norm(p-old))));progress=True;break
   if min_spacing(nid,pos[nid])>=40:break
 if not progress:break
print('changes',len(changes),'edge',sum(t=='edge' for _,t,_ in changes),'spacing',sum(t=='spacing' for _,t,_ in changes),flush=True)
remaining=sorted((round(float(np.linalg.norm(pos[a]-pos[b])),1),a,b) for i,a in enumerate(ids) for b in ids[i+1:] if np.linalg.norm(pos[a]-pos[b])<40)
print('remaining',len(remaining),remaining[:25],flush=True)
d['note']='正式百川节点地图：只调整局部节点位置以改善间距与边缘可见性，保留路网连接关系。'
out.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
