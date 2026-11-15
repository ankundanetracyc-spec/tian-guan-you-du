"""Preserve the existing planar embedding while fitting pending map artwork.
All movement is display-only. Full polylines are checked before rendering masks.
"""
from __future__ import annotations
import sys,json,math,time,hashlib
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
sys.path.insert(0,'E:/tmp/CodexData/world_map_python_libs')
import shapely
from shapely.geometry import LineString,Point,box
from shapely.ops import polygonize,unary_union
from shapely.strtree import STRtree
from scipy import ndimage
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/acceptance_20261030'
SOURCE=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
W=3762

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def dense(points,spacing=99999.):
    result=[]
    for a,b in zip(points[:-1],points[1:]):
        n=max(1,math.ceil(np.linalg.norm(b-a)/spacing))
        for t in np.arange(n)/n:result.append(a*(1-t)+b*t)
    result.append(points[-1]);return np.array(result)

def audit(edges,graph,positions):
    keys=list(edges);lines=[LineString(edges[k]) for k in keys];tree=STRtree(lines);bad=[]
    for i,line in enumerate(lines):
        if not line.is_simple:bad.append(('self',keys[i]))
        edge=graph[keys[i]];ids={edge['from'],edge['to']}
        for j in tree.query(line):
            if j<=i:continue
            hit=line.intersection(lines[j])
            if hit.is_empty:continue
            shared=ids&{graph[keys[j]]['from'],graph[keys[j]]['to']}
            for n in shared:hit=hit.difference(Point(positions[n]).buffer(.015))
            if not hit.is_empty:bad.append((keys[i],keys[j]))
    wrong=[]
    for id,p in positions.items():
        point=Point(p)
        for j in tree.query(point.buffer(5)):
            if id in [graph[keys[j]]['from'],graph[keys[j]]['to']]:continue
            distance=point.distance(lines[j])
            if distance<5-1e-6:wrong.append((id,keys[j],round(distance,3)))
    return bad,wrong

def main():
    started=time.time();data=read(SOURCE);v2=read(BASE/'display_layout_v2.json');graph=data['roadCurveGeometry']['edges']
    target_data=read(BASE/'terrain_location_adjustments.json') if (BASE/'terrain_location_adjustments.json').exists() else v2
    named=data['nodes'];transit=data['transitNodes'];ids=[n['id'] for n in named+transit]
    idx={id:i for i,id in enumerate(ids)}
    source_nodes=np.array([n['pos'] for n in named+transit],float)*W/100
    targets=np.array([target_data['locations'][n['id']]['drawPos'] for n in named],float)
    originals=np.array([v2['locations'][n['id']]['drawPos'] for n in named],float)
    buffers=[source_nodes];slices={};cursor=len(source_nodes)
    for key,e in graph.items():
        points=dense(np.array(e['points'],float)*W/100)
        slices[key]=slice(cursor,cursor+len(points));cursor+=len(points);buffers.append(points)
    mesh_raw=np.array(read(BASE/'route_original_mesh.json'),float)
    mesh_vertices=[];mesh_index={};triangles=[]
    for triangle in mesh_raw:
        ids_t=[]
        for vertex in triangle:
            key=tuple(np.round(vertex,9))
            if key not in mesh_index:mesh_index[key]=len(mesh_vertices);mesh_vertices.append(vertex)
            ids_t.append(mesh_index[key])
        triangles.append(ids_t)
    mesh_offset=cursor
    buffers.append(np.array(mesh_vertices));cloud=np.vstack(buffers)
    triangles=np.array(triangles,int)+mesh_offset
    def areas(points):
        t=points[triangles]
        return (t[:,1,0]-t[:,0,0])*(t[:,2,1]-t[:,0,1])-(t[:,1,1]-t[:,0,1])*(t[:,2,0]-t[:,0,0])
    a0=areas(cloud);triangles=triangles[np.abs(a0)>1e-12];a0=areas(cloud);sign=np.sign(a0);floor=np.abs(a0)*.005
    # Compact smooth point drags are diffeomorphisms when |d|*max|grad(w)|<1.
    # The local derivative bound is <0.4; dense path samples track each deformation.
    fixed=[i for i,n in enumerate(named) if n.get('level')==1]
    print('points',len(cloud),'anchors',len(named),flush=True)
    history=[]
    waypoints={idx['bc_place_050']:[np.array([2250.,800.]),np.array([2250.,420.]),targets[idx['bc_place_050']]],idx['bc_place_103']:[np.array([1430.,3490.]),np.array([1210.,3650.]),np.array([850.,3650.]),targets[idx['bc_place_103']]]}
    for iteration in range(160):
        for i in list(range(len(named)))+fixed*2:
            center=cloud[i].copy()
            target=targets[i]
            if i in waypoints:
                while len(waypoints[i])>1 and np.linalg.norm(center-waypoints[i][0])<28:waypoints[i].pop(0)
                target=waypoints[i][0]
            delta=target-center;distance=np.linalg.norm(delta)
            if distance<.6:continue
            other_distance=np.linalg.norm(cloud[:len(named)]-center,axis=1);other_distance[i]=np.inf
            radius=max(4.,min(310.,distance*2+100,float(other_distance.min())*.46))
            step=min(distance,18.,radius*.14)
            movement=delta/distance*step
            r2=((cloud-center)**2).sum(1);active=r2<radius*radius
            weight=(1-r2[active]/radius**2)**3
            update=np.zeros_like(cloud);update[active]=weight[:,None]*movement
            factor=1.
            proposal=cloud+update
            for backtrack in range(14):
                if np.all(areas(proposal)*sign>=floor):break
                factor*=.5;proposal=cloud+update*factor
            if factor>1/8192:cloud=proposal
        errors=np.linalg.norm(cloud[:len(named)]-targets,axis=1)
        if iteration%10==0:print(iteration,'maxerror',round(errors.max(),1),'median',round(np.median(errors),1),flush=True)
        history.append([iteration,round(float(errors.max()),3),round(float(np.median(errors)),3)])
        if errors.max()<1.5:break
        if iteration%5==4:
            road_lines=[LineString(cloud[section]) for section in slices.values()]
            road_lines.append(LineString(box(0,0,W,W).exterior.coords))
            polygons=list(polygonize(unary_union(road_lines)))
            mesh=shapely.constrained_delaunay_triangles(shapely.GeometryCollection(polygons))
            fresh_tri=[]
            def walk(geometry):
                if geometry.geom_type=='Polygon':fresh_tri.append(list(geometry.exterior.coords)[:3])
                elif hasattr(geometry,'geoms'):
                    for child in geometry.geoms:walk(child)
            walk(mesh)
            mv=[];mi={};tt=[]
            for triangle in fresh_tri:
                triple=[]
                for vertex in triangle:
                    key=tuple(np.round(vertex,8))
                    if key not in mi:mi[key]=len(mv);mv.append(vertex)
                    triple.append(mi[key])
                tt.append(triple)
            cloud=np.vstack((cloud[:mesh_offset],np.array(mv)))
            triangles=np.array(tt,int)+mesh_offset
            a0=areas(cloud);triangles=triangles[np.abs(a0)>1e-12];a0=areas(cloud)
            sign=np.sign(a0);floor=np.abs(a0)*.005
    positions={id:cloud[i].copy() for i,id in enumerate(ids)}
    edges={k:cloud[s].copy() for k,s in slices.items()}
    for k,e in graph.items():edges[k][0]=positions[e['from']];edges[k][-1]=positions[e['to']]
    bad,wrong=audit(edges,graph,positions)
    print('flow audit',len(bad),len(wrong),flush=True)
    output={'version':'display_layout_v3_20261030','worldSize':W,'displayOnly':True,'logicalPlanSha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'locations':{},'transitPositions':{n['id']:np.round(positions[n['id']],4).tolist() for n in transit},
        'displayEdges':{k:np.round(v,4).tolist() for k,v in edges.items()}}
    for n in named:
        id=n['id'];entry=v2['locations'][id].copy();entry['drawPos']=np.round(positions[id],4).tolist()
        entry['targetDrawPos']=target_data['locations'][id]['drawPos'];output['locations'][id]=entry
    write(BASE/'display_layout_v3_flow_candidate.json',output)
    write(BASE/'display_layout_v3_flow_checks.json',{'history':history,'bad':bad,'wrong':wrong,'seconds':time.time()-started})
    print('done',time.time()-started,flush=True)

if __name__=='__main__':main()
