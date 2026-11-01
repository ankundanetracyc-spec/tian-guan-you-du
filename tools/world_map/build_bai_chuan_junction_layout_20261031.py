"""Terrain-first planar preview routing using real existing junction nodes."""
from __future__ import annotations
import sys,json,math,heapq,hashlib
from pathlib import Path
import numpy as np
from scipy import ndimage
from scipy.spatial import Delaunay,cKDTree
from PIL import Image,ImageDraw
sys.path.insert(0,'E:/tmp/CodexData/world_map_python_libs')
import networkx as nx
from shapely.geometry import LineString,Point,Polygon,MultiPoint
from shapely.strtree import STRtree
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/acceptance_20261030'
SOURCE=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
W=3762

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def samples(p,step=4):
    out=[]
    for a,b in zip(p[:-1],p[1:]):
        for t in np.linspace(0,1,max(2,math.ceil(np.linalg.norm(b-a)/step))):out.append(a*(1-t)+b*t)
    return np.array(out)

def main():
    data=read(SOURCE);v2=read(BASE/'display_layout_v2.json');terrain=read(BASE/'terrain_location_adjustments.json')
    nodes=data['nodes']+data['transitNodes'];ids=[n['id'] for n in nodes];index={id:i for i,id in enumerate(ids)}
    positions=np.array([terrain['locations'][n['id']]['drawPos'] if i<123 else v2['transitPositions'][n['id']] for i,n in enumerate(nodes)],float)
    image=Image.open(BASE/'master_3762.png').convert('RGB');rgb=np.array(image).astype(float)
    water=(rgb[:,:,2]>rgb[:,:,0]*1.15)&(rgb[:,:,2]>rgb[:,:,1]*1.04)
    rock=(rgb.max(2)-rgb.min(2)<44)&(rgb.mean(2)<160)&~water
    dry=~ndimage.binary_dilation(water,iterations=5)
    dry_labels,_=ndimage.label(dry)
    dry_counts=np.bincount(dry_labels.ravel());dry_counts[0]=0
    large_dry=dry_counts>80000;large_dry[0]=False
    dry &= large_dry[dry_labels]
    ground=dry&~ndimage.binary_dilation(rock,iterations=4)
    _,near=ndimage.distance_transform_edt(~ground,return_indices=True)
    # Existing ordinary points settle on dry low ground and keep readable spacing.
    accepted=positions[:123].tolist()
    shifts=[]
    for i in range(123,len(positions)):
        old=positions[i].copy();x,y=np.clip(np.round(old).astype(int),0,W-1)
        seed=old if ground[y,x] else np.array([near[1,y,x],near[0,y,x]],float)
        candidates=[seed]
        for radius in [12,24,42,66,96]:
            for angle in np.linspace(0,math.tau,24,endpoint=False):candidates.append(seed+radius*np.array([math.cos(angle),math.sin(angle)]))
        options=np.array(candidates);options=np.clip(np.round(options),[20,20],[W-21,W-21])
        free=ground[options[:,1].astype(int),options[:,0].astype(int)]
        distances=np.linalg.norm(options[:,None]-np.array(accepted)[None],axis=2).min(1)
        score=np.linalg.norm(options-old,axis=1)+np.maximum(0,24-distances)**2*40+(~free)*100000
        chosen=options[np.argmin(score)];positions[i]=chosen;accepted.append(chosen.tolist());shifts.append(float(np.linalg.norm(chosen-old)))
    tri=Delaunay(positions)
    cell_centers=positions[tri.simplices].mean(1)
    pairs={}
    for ti,t in enumerate(tri.simplices):
        for a,b in [(t[0],t[1]),(t[1],t[2]),(t[2],t[0])]:pairs.setdefault(tuple(sorted([int(a),int(b)])),[]).append(ti)
    cost=1+ndimage.gaussian_filter(rock.astype(float),4)*18+water*30
    lab,_=ndimage.label(~water);counts=np.bincount(lab.ravel());counts[0]=0;mainland=lab==counts.argmax()
    shore=np.array([np.flatnonzero(row)[-1] if row.any() else W*.85 for row in mainland]);shore=ndimage.median_filter(shore,size=35)
    sea=water&(np.arange(W)[None]>shore[:,None]);cost+=sea*200
    point_tree=STRtree([Point(p) for p in positions])
    G=nx.Graph();G.add_nodes_from(range(len(ids)))
    straight={};polygons={}
    for pair,faces in pairs.items():
        a,b=pair;line=LineString(positions[[a,b]])
        nearby=[int(j) for j in point_tree.query(line.buffer(9)) if j not in pair]
        if any(line.distance(Point(positions[j]))<8 for j in nearby):continue
        p=positions[[a,b]];s=samples(p);xy=np.clip(s.round().astype(int),0,W-1)
        weight=line.length*(1+float((cost[xy[:,1],xy[:,0]]-1).mean()))
        wet=water[xy[:,1],xy[:,0]]
        starts=np.flatnonzero(np.diff(np.r_[False,wet,False].astype(int))==1)
        ends=np.flatnonzero(np.diff(np.r_[False,wet,False].astype(int))==-1)
        longest_wet=float((ends-starts).max())*line.length/max(1,len(wet)-1) if len(starts) else 0.
        if longest_wet>75:weight+=8000
        G.add_edge(a,b,weight=weight,length=line.length);straight[pair]=p
        verts=[positions[a],positions[b]]+[cell_centers[f] for f in faces]
        if len(faces)==1:
            # Hull roads remain within the painted-world rectangle.
            center=cell_centers[faces[0]];verts.append((positions[a]+positions[b])-center)
        polygons[pair]=MultiPoint(verts).convex_hull.intersection(Polygon([(5,5),(W-5,5),(W-5,W-5),(5,W-5)]))
    print('candidate',G.number_of_edges(),'components',nx.number_connected_components(G),flush=True)
    if not nx.is_connected(G):raise RuntimeError('Candidate graph disconnected')
    original_graph=data['roadCurveGeometry']['edges']
    # Prefer terrain-cheap real shared paths for each old connection.
    def select():
        used=set()
        tree=nx.minimum_spanning_tree(G,weight='weight')
        used.update(tuple(sorted(p)) for p in tree.edges)
        walks={}
        for key,edge in original_graph.items():
            path=nx.shortest_path(G,index[edge['from']],index[edge['to']],weight='weight')
            walks[key]=path;used.update(tuple(sorted(p)) for p in zip(path[:-1],path[1:]))
        return used,walks
    # Remove thin triangle shortcuts; they naturally become a path through its existing node.
    for iteration in range(40):
        used,walks=select();keys=list(used);lines=[LineString(straight[k]) for k in keys];st=STRtree(lines);bad=[]
        for j,line in enumerate(lines):
            for k in st.query(line.buffer(4.2)):
                if k<=j:continue
                shared=set(keys[j])&set(keys[k]);one,two=line,lines[k]
                for n in shared:
                    disk=Point(positions[n]).buffer(8);one=one.difference(disk);two=two.difference(disk)
                if one.is_empty or two.is_empty:continue
                if one.distance(two)<4.1:bad.append((keys[j],keys[k]))
        if not bad:break
        removed=0
        for one,two in bad:
            choice=sorted([one,two],key=lambda k:G.edges[k]['length'] if G.has_edge(*k) else -1,reverse=True)
            for edge in choice:
                if not G.has_edge(*edge):continue
                info=G.edges[edge].copy();G.remove_edge(*edge)
                if nx.is_connected(G):removed+=1;break
                G.add_edge(*edge,**info)
        print('thin shortcuts',iteration,len(bad),'removed',removed,flush=True)
        if not removed:break
    used,walks=select()
    # Road shaping stays in each candidate edge's disjoint triangulation diamond.
    directions=[(1,0,1),(-1,0,1),(0,1,1),(0,-1,1),(1,1,1.414),(-1,1,1.414),(1,-1,1.414),(-1,-1,1.414)]
    def route(pair):
        a,b=pair;p=positions[[a,b]];poly=polygons[pair]
        direct=samples(p);xy=np.clip(direct.round().astype(int),0,W-1)
        if (cost[xy[:,1],xy[:,0]]-1).mean()<.15:return p
        step=5
        left,top,right,bottom=poly.bounds;left=max(0,int(left)-6);top=max(0,int(top)-6)
        right=min(W-1,int(right)+6);bottom=min(W-1,int(bottom)+6)
        xs=np.arange(left,right+1,step);ys=np.arange(top,bottom+1,step)
        if len(xs)<2 or len(ys)<2:return p
        xx,yy=np.meshgrid(xs,ys);points=np.column_stack([xx.ravel(),yy.ravel()])
        import shapely
        safe=poly.buffer(-3.3)
        allowed=shapely.contains_xy(safe,xx,yy)
        for endpoint in p:allowed|=(xx-endpoint[0])**2+(yy-endpoint[1])**2<9**2
        local_cost=cost[yy,xx]
        sx,sy=np.clip(np.round((p[0]-[left,top])/step).astype(int),[0,0],[len(xs)-1,len(ys)-1]);ex,ey=np.clip(np.round((p[1]-[left,top])/step).astype(int),[0,0],[len(xs)-1,len(ys)-1])
        allowed[sy,sx]=True;allowed[ey,ex]=True
        queue=[(math.hypot(ex-sx,ey-sy),0,(sx,sy))];scores={(sx,sy):0};came={};goal=(ex,ey)
        while queue:
            _,g,(x,y)=heapq.heappop(queue)
            if g>scores.get((x,y),math.inf)+1e-9:continue
            if (x,y)==goal:break
            for dx,dy,length in directions:
                nx_,ny_=x+dx,y+dy
                if nx_<0 or ny_<0 or nx_>=len(xs) or ny_>=len(ys) or not allowed[ny_,nx_]:continue
                ng=g+length*(local_cost[y,x]+local_cost[ny_,nx_])*.5
                if ng<scores.get((nx_,ny_),math.inf):scores[nx_,ny_]=ng;came[(nx_,ny_)]=(x,y);heapq.heappush(queue,(ng+math.hypot(ex-nx_,ey-ny_),ng,(nx_,ny_)))
        if goal not in scores:return p
        path=[goal]
        while path[-1]!=(sx,sy):path.append(came[path[-1]])
        path.reverse();q=np.array([[xs[x],ys[y]] for x,y in path],float);q[0]=p[0];q[-1]=p[1]
        # Local simplification never leaves the independent corridor.
        line=LineString(q).simplify(2,preserve_topology=True)
        q=np.array(line.coords)
        if not poly.buffer(.01).covers(line):return p
        return q
    geometry={pair:route(pair) for pair in used}
    print('selected',len(used),'shaped',sum(len(p)>2 for p in geometry.values()),flush=True)
    # Exact geometric acceptance: a rejected bend falls back to the planar chord.
    for iteration in range(20):
        keys=list(geometry);lines=[LineString(geometry[k]) for k in keys];st=STRtree(lines);rejected=set()
        for i,line in enumerate(lines):
            if not line.is_simple:rejected.add(keys[i])
            nearby=point_tree.query(line.buffer(5.2))
            if any(j not in keys[i] and line.distance(Point(positions[j]))<5.05 for j in nearby):rejected.add(keys[i])
            for j in st.query(line.buffer(4.1)):
                if j<=i:continue
                shared=set(keys[i])&set(keys[j]);one,two=line,lines[j]
                hit=line.intersection(lines[j])
                for n in shared:hit=hit.difference(Point(positions[n]).buffer(.01))
                if not hit.is_empty:
                    if len(geometry[keys[i]])>2:rejected.add(keys[i])
                    if len(geometry[keys[j]])>2:rejected.add(keys[j])
                for n in shared:
                    disk=Point(positions[n]).buffer(8);one=one.difference(disk);two=two.difference(disk)
                if not one.is_empty and not two.is_empty and one.distance(two)<4.01:
                    if len(geometry[keys[i]])>2:rejected.add(keys[i])
                    if len(geometry[keys[j]])>2:rejected.add(keys[j])
        if not rejected:break
        for pair in rejected:geometry[pair]=straight[pair]
        print('unsafe bends reset',len(rejected),flush=True)
    locations={}
    for i,n in enumerate(data['nodes']):
        id=n['id'];entry=v2['locations'][id].copy();entry['drawPos']=positions[i].tolist();entry['terrainClass']=terrain['locations'][id]['terrainClass'];entry['terrainReason']=terrain['locations'][id]['reason']
        logical=np.array(n['pos'])*W/100;entry['displayOffsetFromLogical']=np.round(positions[i]-logical,3).tolist()
        row=next(r for r in read(BASE/'presentation.json')['nodes'] if r['id']==id)
        if row['asset'] is not None:
            x,y=positions[i];width,height=row['width'],row['height'];entry['rect']=[x-width/2,y-height,x+width/2,y]
        locations[id]=entry
    def edgekey(pair):return ids[pair[0]]+'~'+ids[pair[1]]
    display_graph={edgekey(pair):{'from':ids[pair[0]],'to':ids[pair[1]],'sourceEdges':[]} for pair in used}
    source_walks={}
    for oldkey,walk in walks.items():
        edge_ids=[edgekey(tuple(sorted(p))) for p in zip(walk[:-1],walk[1:])]
        source_walks[oldkey]=[ids[i] for i in walk]
        for key in edge_ids:display_graph[key]['sourceEdges'].append(oldkey)
    result={'version':'display_layout_v3_20261031','worldSize':W,'displayOnly':True,'logicalPlanSha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
            'locations':locations,'transitPositions':{ids[i]:positions[i].tolist() for i in range(123,len(ids))},
            'displayEdges':{edgekey(pair):np.round(points,4).tolist() for pair,points in geometry.items()},
            'displayGraph':{'edges':display_graph,'nodeIds':ids},'sourceEdgeWalks':source_walks,
            'note':'Independent preview graph: near existing nodes becomes a real shared connection. Formal travel graph unchanged.'}
    final_keys=list(result['displayEdges']);final_lines=[LineString(result['displayEdges'][k]) for k in final_keys]
    tree=STRtree(final_lines);contact=[];close=[];wrong=[];midpass=[]
    id_positions={id:positions[i] for i,id in enumerate(ids)}
    for i,line in enumerate(final_lines):
        edge=display_graph[final_keys[i]]
        if not line.is_simple:contact.append([final_keys[i],"self"])
        for j in tree.query(line.buffer(4.1)):
            if j<=i:continue
            other=display_graph[final_keys[j]]
            shared=set([edge['from'],edge['to']])&set([other['from'],other['to']])
            hit=line.intersection(final_lines[j]);one,two=line,final_lines[j]
            for nid in shared:
                hit=hit.difference(Point(id_positions[nid]).buffer(.01))
                disk=Point(id_positions[nid]).buffer(8);one=one.difference(disk);two=two.difference(disk)
            if not hit.is_empty:contact.append([final_keys[i],final_keys[j]])
            if not one.is_empty and not two.is_empty and one.distance(two)<3.999:close.append([final_keys[i],final_keys[j]])
    for nid,p in id_positions.items():
        point=Point(p)
        for j in tree.query(point.buffer(14)):
            edge=display_graph[final_keys[j]]
            if nid in [edge['from'],edge['to']]:continue
            distance=point.distance(final_lines[j]);along=final_lines[j].project(point)
            if distance<4.999:wrong.append([nid,final_keys[j]])
            if distance<14 and along>18 and final_lines[j].length-along>18:midpass.append([nid,final_keys[j]])
    source_walks_pass=all(walk[0]==original_graph[key]['from'] and walk[-1]==original_graph[key]['to'] and all(edgekey(tuple(sorted([index[u],index[v]]))) in display_graph for u,v in zip(walk[:-1],walk[1:])) for key,walk in source_walks.items())
    graph=nx.Graph();graph.add_nodes_from(ids)
    graph.add_edges_from((e['from'],e['to']) for e in display_graph.values())
    report={'version':result['version'],'counts':{'named':123,'built':53,'wild':70,'transit':321,'nodes':len(ids),'displayEdges':len(display_graph),'originalEdgeWalks':len(source_walks)},
        'connectedComponents':nx.number_connected_components(graph),'cycleRank':len(display_graph)-len(ids)+1,'forbiddenContactCount':len(contact),'visualClosePairCount':len(close),
        'unrelatedNodePassCount':len(wrong),'nearNodeMidpassCount':len(midpass),'allSourceEdgeWalksValid':source_walks_pass,
        'transitPointWaterOverlapCount':int(sum(water[int(p[1]),int(p[0])] for p in positions[123:])),
        'maxTransitShiftPixels':round(max(shifts),3),'formalPlanSha256':result['logicalPlanSha256'],'strictPass':not contact and not close and not wrong and not midpass and source_walks_pass and nx.is_connected(graph),
        'limits':'Display preview graph changed at real existing nodes; formal travel graph, permissions and time costs unchanged. Colour terrain costs are not height or bridge validation.'}
    write(BASE/'display_layout_v3_checks.json',report)
    write(BASE/'display_layout_v3_candidate.json',result)
    if not report['strictPass']:raise RuntimeError('Final junction graph failed geometry validation: '+json.dumps(report))
    write(BASE/'display_layout_v3.json',result)
    # One sheet shows all actual road polylines and named assets.
    preview=image.copy().convert('RGBA');draw=ImageDraw.Draw(preview)
    for points in geometry.values():draw.line([tuple(p) for p in points],fill=(25,23,19),width=2)
    for i in range(123,len(ids)):
        x,y=positions[i];draw.ellipse((x-3,y-3,x+3,y+3),fill=(238,219,177),outline=(35,31,24))
    rows=read(BASE/'presentation.json')['nodes']
    for i,row in enumerate(rows):
        x,y=positions[i]
        if row['asset']:
            icon=Image.open(ROOT/'textures/world/bai_chuan/node_sprites_20261030/icons'/f"{row['asset']}.png").convert('RGBA');preview.alpha_composite(icon,(int(x-icon.width/2),int(y-icon.height)))
        else:draw.ellipse((x-4,y-4,x+4,y+4),fill=(238,219,177),outline=(35,31,24))
    preview.convert('RGB').resize((1505,1505),Image.Resampling.LANCZOS).save(BASE/'display_layout_v3_candidate_preview.jpg',quality=92)
    print('saved candidate',flush=True)

if __name__=='__main__':main()
