"""Independent display-geometry audit; no source graph or layout writes."""
from pathlib import Path
import argparse, hashlib, json, math
import sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/acceptance_20261030'
EPS = .02
CELL = 80

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def cross(a, b):
    return a[0]*b[1]-a[1]*b[0]

def intersect(a, b, c, d):
    r = b-a; s = d-c
    denom = cross(r, s)
    if abs(denom) < 1e-8:
        if abs(cross(c-a, r)) > 1e-5: return None
        length = float(r@r)
        if length < 1e-12: return None
        t0 = float((c-a)@r)/length; t1 = float((d-a)@r)/length
        lo = max(0., min(t0, t1)); hi = min(1., max(t0, t1))
        if hi < lo-1e-9: return None
        return a+r*(lo+hi)*.5, (hi-lo)*math.sqrt(length)
    t = cross(c-a, s)/denom; u = cross(c-a, r)/denom
    if -1e-9 <= t <= 1+1e-9 and -1e-9 <= u <= 1+1e-9:
        return a+t*r, 0.
    return None

def cells(a, b, pad=0):
    lo = np.floor((np.minimum(a,b)-pad)/CELL).astype(int)
    hi = np.floor((np.maximum(a,b)+pad)/CELL).astype(int)
    for y in range(lo[1], hi[1]+1):
        for x in range(lo[0], hi[0]+1): yield x, y

def audit(layout_path):
    layout = read(layout_path)
    data = read(ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json')
    graph = layout.get('displayGraph',{}).get('edges',data['roadCurveGeometry']['edges'])
    positions = {k:np.array(v['drawPos'],float) for k,v in layout['locations'].items()}
    positions.update({k:np.array(v,float) for k,v in layout['transitPositions'].items()})
    outside_nodes=[k for k,p in positions.items() if np.any(p<0) or np.any(p>3762)]
    outside_edges=[k for k,p in layout['displayEdges'].items() if np.any(np.array(p)<0) or np.any(np.array(p)>3762)]
    named=np.array([v['drawPos'] for v in layout['locations'].values()],float)
    distances=np.sqrt(((named[:,None]-named[None])**2).sum(2))
    np.fill_diagonal(distances,np.inf)
    segments=[]; grid={}; bad=[]; endpoints=[]; reviewed=set()
    for key, points in layout['displayEdges'].items():
        edge=graph[key]; points=np.array(points,float)
        if np.linalg.norm(points[0]-positions[edge['from']])>EPS or np.linalg.norm(points[-1]-positions[edge['to']])>EPS:
            endpoints.append(key)
        for i,(a,b) in enumerate(zip(points[:-1],points[1:])):
            if np.linalg.norm(b-a)<1e-7: continue
            sid=len(segments)
            current=(key,i,a,b)
            for cell in cells(a,b):
                for other in grid.get(cell,[]):
                    pair=(other,sid)
                    if pair in reviewed: continue
                    reviewed.add(pair)
                    otherkey,j,c,d=segments[other]
                    if key==otherkey and abs(i-j)<=1: continue
                    hit=intersect(a,b,c,d)
                    if hit is None: continue
                    point,length=hit
                    shared=set([edge['from'],edge['to']]) & set([graph[otherkey]['from'],graph[otherkey]['to']]) if key!=otherkey else set()
                    allowed=length<EPS and any(np.linalg.norm(point-positions[n])<=EPS for n in shared)
                    if not allowed:
                        bad.append({'edges':[key,otherkey],'point':np.round(point,3).tolist(),'overlapLength':round(length,3),'selfCrossing':key==otherkey})
            for cell in cells(a,b): grid.setdefault(cell,[]).append(sid)
            segments.append(current)
    wrong_nodes=[]
    for nid,point in positions.items():
        candidates=set()
        for cell in cells(point,point,6): candidates.update(grid.get(cell,[]))
        close={}
        for sid in candidates:
            key,i,a,b=segments[sid]
            if nid in [graph[key]['from'],graph[key]['to']]: continue
            delta=b-a; t=np.clip(float((point-a)@delta)/float(delta@delta),0,1)
            distance=float(np.linalg.norm(a+t*delta-point))
            if distance<5.: close[key]=min(close.get(key,math.inf),distance)
        for key,distance in close.items(): wrong_nodes.append({'node':nid,'edge':key,'distance':round(distance,3),'point':point.tolist()})
    unique_bad={tuple(sorted(b['edges']))+tuple(b['point']) for b in bad}
    # Ink has a width, so mathematically disjoint paths can still appear joined.
    sys.path.insert(0,'E:/tmp/CodexData/world_map_python_libs')
    from shapely.geometry import LineString, Point
    from shapely.strtree import STRtree
    keys=list(layout['displayEdges'])
    lines=[LineString(layout['displayEdges'][k]) for k in keys]
    tree=STRtree(lines); close_pairs=[]; near_mid_nodes=[]
    for nid,xy in positions.items():
        point=Point(xy)
        for j in tree.query(point.buffer(14)):
            edge=graph[keys[j]]
            if nid in [edge['from'],edge['to']]: continue
            along=float(lines[j].project(point))
            distance=float(lines[j].distance(point))
            if distance<14 and along>18 and lines[j].length-along>18:
                near_mid_nodes.append({'node':nid,'edge':keys[j],'distance':round(distance,3)})
    for i,line in enumerate(lines):
        edge=graph[keys[i]]
        for j in tree.query(line.buffer(4)):
            if j<=i: continue
            shared=set([edge['from'],edge['to']]) & set([graph[keys[j]]['from'],graph[keys[j]]['to']])
            a,b=line,lines[j]
            for nid in shared:
                disk=Point(positions[nid]).buffer(8)
                a=a.difference(disk); b=b.difference(disk)
            if a.is_empty or b.is_empty: continue
            distance=float(a.distance(b))
            if distance<3.999:
                close_pairs.append({'edges':[keys[i],keys[j]],'distance':round(distance,4)})
    result={'layout':layout_path.name,'layoutSha256':hashlib.sha256(layout_path.read_bytes()).hexdigest(),'nodeCount':len(positions),'edgeCount':len(layout['displayEdges']),
            'segmentCount':len(segments),'endpointMismatchCount':len(endpoints),'forbiddenContactCount':len(unique_bad),
            'forbiddenPairCount':len({tuple(sorted(b['edges'])) for b in bad}),
            'unrelatedNodePassCount':len(wrong_nodes),'contactExamples':bad[:60],'unrelatedNodeExamples':wrong_nodes[:40],
            'visualClosePairCount':len(close_pairs),'visualCloseExamples':close_pairs[:40],
            'visualClearancePass':not close_pairs,
            'nearNodeMidpassCount':len(near_mid_nodes),'nearNodeMidpassExamples':near_mid_nodes[:40],
            'minimumNamedSpacing':round(float(distances.min()),3),
            'outOfWorldNodeCount':len(outside_nodes),'outOfWorldEdgeCount':len(outside_edges),
            'strictGeometryPass':not bad and not endpoints and not wrong_nodes and not near_mid_nodes and not outside_nodes and not outside_edges,
            'scope':'Complete displayed polylines, before dash gaps or label masks; only exact common graph endpoints may touch. Nonincident node clearance is 5px.'}
    out=BASE/(layout_path.stem+'_independent_audit.json')
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    image=Image.open(BASE/'master_3762.png').convert('RGB')
    draw=ImageDraw.Draw(image)
    for pts in layout['displayEdges'].values():draw.line([tuple(p) for p in pts],fill=(28,25,20),width=2)
    for b in bad:
        x,y=b['point'];draw.ellipse((x-14,y-14,x+14,y+14),outline=(225,25,40),width=4)
    for n,p in positions.items():
        x,y=p;draw.ellipse((x-3,y-3,x+3,y+3),fill=(255,235,160))
    image.resize((1505,1505),Image.Resampling.LANCZOS).save(BASE/(layout_path.stem+'_independent_audit.jpg'),quality=90)
    print(json.dumps({k:v for k,v in result.items() if not k.endswith('Examples')},ensure_ascii=False))
    return result

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('layout',nargs='?',default='display_layout_v3.json')
    args=parser.parse_args();audit(BASE/args.layout)
