"""Prepare native display-size raster assets and deterministic presentation-only placement."""
import hashlib
import json
import math
import random
from pathlib import Path
import numpy as np
from PIL import Image, ImageFont

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/acceptance_v1'
DOC=ROOT/'docs/美术规范/百川整图与节点落位验收_20261028'
SOURCE=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
data=json.loads(SOURCE.read_text(encoding='utf-8'))
S=6144
rng=random.Random(281028)
catalog={}

def image_part(path,col,row,cols,rows,bounds=None):
    im=Image.open(path).convert('RGBA')
    cw,ch=im.width//cols,im.height//rows
    im=im.crop(bounds or (col*cw,row*ch,(col+1)*cw,(row+1)*ch))
    cw,ch=im.size
    box=im.getchannel('A').point(lambda a:255 if a>28 else 0).getbbox()
    assert box
    return im.crop((max(0,box[0]-5),max(0,box[1]-5),min(cw,box[2]+5),min(ch,box[3]+5)))

def asset(key,im,width):
    assert width<=im.width, (key,width,im.size)
    target=(width,round(im.height*width/im.width))
    resized=im.resize(target,Image.Resampling.LANCZOS)
    file=ART/'icons'/f'{key}.png'
    resized.save(file)
    catalog[key]={'file':'res://'+str(file.relative_to(ROOT)).replace('\\','/'),'size':list(target),'sourceSize':list(im.size)}
    return key

approved=ROOT/'docs/美术规范/百川简笔底图与通用节点模板_20261028/通用节点图集_原图_v02.png'
raw={}
for i,key in enumerate(['sect','city','town']): raw[key]=image_part(approved,i,0,3,1)
for i,key in enumerate(['market','village','dock']): raw[key]=image_part(ART/'source/local.png',i,0,3,1)
for i,key in enumerate(['dao','holy','academy']): raw[key]=image_part(ART/'source/sects.png',i,0,3,1)
# Generated atlases have irregular gutters. Crop at the actual empty gutters,
# not y=512 through the top-row stonework, which otherwise leaves fragments
# floating above the bottom-row symbols.
for i,key in enumerate(['peak','cave','ruin','pool','valley','grove']):
    xs=[0,550,1045,1536]; ys=[0,592,1024]; col,row=i%3,i//3
    raw[key]=image_part(ART/'source/wild.png',col,row,3,2,(xs[col],ys[row],xs[col+1],ys[row+1]))
for i,key in enumerate(['hill','shelf','saddle','forest_a','forest_b','forest_c']):
    xs=[0,545,1045,1536] if i<3 else [0,500,995,1536]; ys=[0,520,1024]; col,row=i%3,i//3
    raw[key]=image_part(ART/'source/terrain.png',col,row,3,2,(xs[col],ys[row],xs[col+1],ys[row+1]))
for key,widths in {'sect':[118,164],'dao':[118,164],'holy':[118,164],'academy':[118],'city':[142],'town':[100],'market':[94],'village':[78],'dock':[92],'peak':[76],'cave':[76],'ruin':[72],'pool':[76],'valley':[76],'grove':[80]}.items():
    for w in widths: asset(f'{key}_{w}',raw[key],w)
for key in ['hill','shelf','saddle']:
    for w in [280,340,400]: asset(f'{key}_{w}',raw[key],w)
for key in ['forest_a','forest_b','forest_c']:
    for w in [92,116,140]: asset(f'{key}_{w}',raw[key],w)

def point(p): return np.array(p,dtype=float)*S/100
def rect_intersect(a,b,gap=0): return a[0]<b[0]+b[2]+gap and a[0]+a[2]+gap>b[0] and a[1]<b[1]+b[3]+gap and a[1]+a[3]+gap>b[1]
def dist_segment(p,a,b):
    v=b-a
    t=np.clip(np.dot(p-a,v)/max(np.dot(v,v),1e-8),0,1)
    q=a+t*v
    return float(np.linalg.norm(p-q)),q,float(t)

def catmull(ps,step=12):
    out=[]
    for i in range(len(ps)-1):
        a,b,c,d=np.array(ps[max(0,i-1)]),np.array(ps[i]),np.array(ps[i+1]),np.array(ps[min(len(ps)-1,i+2)])
        count=max(2,round(np.linalg.norm(c-b)/step))
        for j in range(count):
            t=j/count
            q=.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)
            out.append(q)
    out.append(np.array(ps[-1]))
    return np.array(out)

def icon_kind(n):
    t=n['type']; name=n['name']; text=name+n.get('terrain','')
    if t=='sect':
        return 'dao' if name=='玄都天府' else 'holy' if name in ['无相圣宗','枯骨寺','蚀日宫'] else 'academy' if name=='归藏书院' else 'sect'
    if t!='wild': return t
    if any(c in name for c in ['洞','窟']): return 'cave'
    if any(c in text for c in ['林','藤木']): return 'grove'
    if any(c in name for c in ['墟','陵','台','碑']): return 'ruin'
    if any(c in name for c in ['泽','潭','渊','泉','溪','湾','滩','浦','涧']): return 'pool'
    if any(c in name for c in ['山','岭','岗','崖','岩','石']): return 'peak'
    return 'valley'

mountain_names={'玉虚仙宗','无相圣宗','镇岳观','抱朴山','枯骨寺','蚀日宫','丹鼎宗'}
placements=[]
occupied=[]
priority={'sect':0,'city':1,'town':2,'market':3,'village':4,'dock':5,'wild':6}
for n in sorted(data['nodes'],key=lambda n:(priority[n['type']],n['id'])):
    kind=icon_kind(n)
    width=164 if n.get('level')==1 else 118 if n['type']=='sect' else {'city':142,'town':100,'market':94,'village':78,'dock':92,'peak':76,'cave':76,'ruin':72,'pool':76,'valley':76,'grove':80}[kind]
    key=f'{kind}_{width}'; w,h=catalog[key]['size']; p=point(n['pos'])
    pivot=[.39,.95] if kind=='sect' else [.47,.97] if kind=='city' else [.50,.95]
    if kind=='dock': pivot=[.34,.66]
    choices=[(0,0),(-16,0),(16,0),(0,-16),(0,16),(-28,0),(28,0),(0,-28),(0,28)]
    best=None
    for dx,dy in choices:
        box=[round(p[0]+dx-w*pivot[0]),round(p[1]+dy-h*pivot[1]),w,h]
        hits=sum(rect_intersect(box,b,5) for b in occupied)
        score=hits*10000+abs(dx)+abs(dy)
        if best is None or score<best[0]: best=(score,dx,dy,box)
    _,dx,dy,box=best
    occupied.append(box)
    footing='mountain_shelf' if n['name'] in mountain_names else 'shore' if n['type']=='dock' else 'valley' if n['type']=='sect' else 'open_ground'
    if n['type']=='wild': footing='cave_entrance' if kind=='cave' else 'water_landmark' if kind=='pool' else 'landmark'
    placements.append({'id':n['id'],'name':n['name'],'type':n['type'],'kind':kind,'asset':key,'logicalPos':n['pos'],'anchor':p.tolist(),'artOffset':[dx,dy],'rect':box,'pivot':pivot,'footing':footing,'labelSize':22 if n.get('level')==1 else 18 if n['type']=='city' else 16 if n['type']=='sect' else 14})
placements.sort(key=lambda n:n['id'])
byid={p['id']:p for p in placements}

# A bridge and a place can share a planning-scale anchor. Set the building back
# on the dry bank; the graph anchor itself is not moved.
water_marks=[m for m in data['terrainPlanning']['crossingLandmarks'] if m['sourceType']=='water']
bridge_positions=np.array([point(m['pos']) for m in water_marks])
planning_water=[]
for water in data['terrainPlanning']['waters']:
    for a,b in zip(water['points'],water['points'][1:]):
        a,b=point(a),point(b)
        planning_water.extend(np.linspace(a,b,max(2,math.ceil(np.linalg.norm(b-a)/8))))
    planning_water.extend(catmull([point(q) for q in water['points']],step=8))
planning_water=np.array(planning_water)
for p in placements:
    if p['kind'] in ['pool','dock']: continue
    x,y,w,h=p['rect']
    by_bridge=any(x-32<b[0]<x+w+32 and y-32<b[1]<y+h+32 for b in bridge_positions)
    near_wild=p['type']=='wild' and np.any((planning_water[:,0]>x-96)&(planning_water[:,0]<x+w+96)&(planning_water[:,1]>y-96)&(planning_water[:,1]<y+h+96))
    if not by_bridge and not near_wild: continue
    def bank_score(offset):
        dx,dy=offset; box=[x+dx,y+dy,w,h]
        collisions=sum(rect_intersect(box,q['rect'],12) for q in placements if q['id']!=p['id'])
        wet=np.count_nonzero((planning_water[:,0]>box[0]-38)&(planning_water[:,0]<box[0]+w+38)&(planning_water[:,1]>box[1]-38)&(planning_water[:,1]<box[1]+h+38))
        return collisions*1000000+wet*10000+math.hypot(dx,dy)
    choices=[(dx,dy) for dx in range(-168,169,24) for dy in range(-144,145,24)]
    dx,dy=min(choices,key=bank_score)
    if dx==0 and dy==0: continue
    p['rect']=[x+dx,y+dy,w,h]
    p['artOffset']=[p['artOffset'][0]+dx,p['artOffset'][1]+dy]
    p['footing']='riverside_setback'
    anchor=np.array(p['anchor']); front=anchor+np.array(p['artOffset'])
    p['approach']=[anchor.tolist(),(anchor*.55+front*.45).tolist(),front.tolist()]

label_boxes=[]
fontfile=ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'
for p in sorted(placements,key=lambda x:(priority[x['type']],x['id'])):
    x,y,w,h=p['rect']; ax,ay=np.array(p['anchor'])+np.array(p['artOffset']); font=ImageFont.truetype(str(fontfile),p['labelSize'])
    tw=math.ceil(font.getlength(p['name']))+10; th=p['labelSize']+10
    candidates=[[round(ax-tw/2),round(ay+9),tw,th],[x+w+7,y+h-th,tw,th],[x-tw-7,y+h-th,tw,th],[round(x+w/2-tw/2),y-th-5,tw,th],[round(ax-tw/2),round(ay+24),tw,th]]
    def score(b):
        return sum(rect_intersect(b,q['rect'],2) for q in placements if q['id']!=p['id'])*100+sum(rect_intersect(b,q,3) for q in label_boxes)*1000+(0 if 0<=b[0] and b[0]+b[2]<=S else 10000)
    box=min(candidates,key=score)
    p['labelRect']=box; p['labelConflicts']=score(box)//100
    label_boxes.append(box)

roads=[]
for key,e in data['roadCurveGeometry']['edges'].items():
    roads.append({'key':key,'from':e['from'],'to':e['to'],'points':[point(q).tolist() for q in e['points']]})
roadpoints=np.array([p for r in roads for p in r['points'][::3]])
reserves=[[p['rect'][0]-18,p['rect'][1]-18,p['rect'][2]+36,p['rect'][3]+40] for p in placements]

waters=[]
for water in data['terrainPlanning']['waters']:
    ps=[point(p) for p in water['points']]; inserts=[]
    for i,(a,b) in enumerate(zip(ps,ps[1:])):
        inserts.append((i,0,a))
        for m in water_marks:
            if m['source']!=water['name']: continue
            p=point(m['pos']); distance,_,t=dist_segment(p,a,b)
            if distance<2 and 0<t<1:
                u=(b-a)/np.linalg.norm(b-a)
                for delta in [-54,0,54]:
                    q=p+u*delta
                    t2=float(np.dot(q-a,b-a)/np.dot(b-a,b-a))
                    if 0<t2<1: inserts.append((i,t2,q))
    inserts.append((len(ps)-1,0,ps[-1]))
    knots=[q for _,_,q in sorted(inserts,key=lambda q:(q[0],q[1]))]
    curve=catmull(knots)
    # Keep civil footprints dry, while fixed bridge anchors remain on the river centerline.
    civil=[p for p in placements if p['type'] not in ['wild','dock']]
    for p in civil:
        x,y,w,h=p['rect']; c=np.array([x+w/2,y+h/2]); radii=np.array([w/2+38,h/2+38])
        original=curve.copy()
        near=np.where(np.all(np.abs(curve-c)<radii+80,axis=1))[0]
        if not len(near): continue
        mid=near[np.argmin(np.linalg.norm(curve[near]-c,axis=1))]
        tangent=original[min(mid+2,len(curve)-1)]-original[max(mid-2,0)]; tangent/=max(np.linalg.norm(tangent),1e-8)
        normal=np.array([-tangent[1],tangent[0]])
        side=1 if np.dot(original[mid]-c,normal)>=0 else -1
        lateral=abs(normal[0])*radii[0]+abs(normal[1])*radii[1]+18
        reach=abs(tangent[0])*radii[0]+abs(tangent[1])*radii[1]+135
        for index in range(max(0,mid-45),min(len(curve),mid+46)):
            q=original[index]-c; along=np.dot(q,tangent)
            if abs(along)>reach: continue
            target=side*lateral*math.sqrt(max(0,1-(along/reach)**2))
            current=np.dot(q,normal)
            if side*current<side*target:
                protect=min(1,float(np.linalg.norm(bridge_positions-original[index],axis=1).min())/100)**2
                curve[index]+=normal*(target-current)*protect
    # Feather presentation bends without shifting protected bridge centers.
    for _ in range(3):
        padded=np.pad(curve,((4,4),(0,0)),mode='edge')
        smooth=sum(padded[j:j+len(curve)]*weight for j,weight in enumerate([1,3,6,10,12,10,6,3,1]))/52
        protect=np.minimum(1,np.linalg.norm(curve[:,None]-bridge_positions[None,:],axis=2).min(axis=1)/65)**2
        curve+=(smooth-curve)*protect[:,None]
    waters.append({'name':water['name'],'points':curve.tolist(),'width':42 if water['width']==4 else 29})

watersegments=[(np.array(a),np.array(b),w['width']) for w in waters for a,b in zip(w['points'],w['points'][1:])]
waterpoints=np.array([p for w in waters for p in w['points']])
def nearest_water(p):
    index=np.argmin(np.linalg.norm(waterpoints-np.array(p),axis=1))
    return waterpoints[index]

terrain=[]
bay=[[99,5],[97.5,10],[98,14],[95.5,16],[95,18.7],[96,21],[97,24],[99,25],[100,27],[100,5]]
bay_world=[point(q).tolist() for q in bay]
# Insets keep the named lake approaches on dry shore. Lake coordinates in the
# planning JSON remain schematic and unchanged.
lake_shapes=[{'name':'涵虚泽湖面','points':[[52.65,50.1],[53.25,49.9],[53.7,50.05],[54.4,49.8],[54.65,50.35],[54.95,50.65],[54.85,51.25],[55.0,51.7],[54.55,52.1],[54.4,52.55],[53.6,53.0],[53.3,52.65],[52.5,52.6],[52.4,52.2],[52.55,51.8],[52.15,51.25],[52.4,50.9]]},
             {'name':'镜月泽湖面','points':[[70.8,58.4],[71.6,58.65],[71.9,59.5],[71.3,60.5],[70.1,60.4],[69.7,59.6],[70,58.8]]}]
lakepolys=[{'name':lake['name'],'points':[point(q).tolist() for q in lake['points']]} for lake in lake_shapes]
lakepolys.append({'name':'东北近海岸','points':bay_world})
def inside_polygon(p,poly):
    inside=False
    for a,b in zip(poly,poly[1:]+poly[:1]):
        if (a[1]>p[1])!=(b[1]>p[1]) and p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside
def sea_overlap(box):
    x,y,w,h=box
    return any(inside_polygon(p,lake['points']) for lake in lakepolys for p in [(x,y),(x+w,y),(x,y+h),(x+w,y+h),(x+w/2,y+h/2)]) or any(x<p[0]<x+w and y<p[1]<y+h for lake in lakepolys for p in lake['points'])
def place_terrain(key,center,flip=False,alpha=1,owner=None):
    w,h=catalog[key]['size']; box=[round(center[0]-w/2),round(center[1]-h*.72),w,h]
    terrain.append({'asset':key,'pos':box[:2],'flip':flip,'alpha':alpha,'owner':owner})
    return box

# Mountains around named religious sites: a natural back slope, a quiet shelf and a short approach.
for p in placements:
    if p['footing']=='mountain_shelf':
        x,y,w,h=p['rect']; center=[x+w*.5,y-5]
        sw,sh=catalog['shelf_340']['size']
        def shoulder_score(shift):
            support=[center[0]-sw/2,center[1]+shift-sh*.72,sw,sh]
            collisions=sum(rect_intersect(support,q['rect'],12) for q in placements if q['id']!=p['id'] and q['type']!='wild')
            return collisions*10000+abs(shift)
        # Try both directions. Always moving upslope can push a shoulder into
        # the settlement above it and leave the mountain gate on bare ground.
        center[1]+=min([0,24,-24,48,-48,72,-72,96,-96],key=shoulder_score)
        place_terrain('shelf_340',center,False,.96,p['id'])
        p['shelf']={'left':x-28,'right':x+w+28,'top':y+h*.60,'bottom':p['anchor'][1]+15,'entrance':p['anchor']}

for ridge in data['terrainPlanning']['ridges']:
    ps=catmull([point(p) for p in ridge['points']],step=195)
    for i,c in enumerate(ps):
        key=rng.choice(['hill_400','hill_340','shelf_340','saddle_340'])
        w,h=catalog[key]['size']; box=[c[0]-w/2,c[1]-h*.72,w,h]
        if not any(rect_intersect(box,q,8) for q in reserves) and not sea_overlap(box): place_terrain(key,c,i%2==0,.94)
for _ in range(260):
    c=np.array([rng.uniform(150,S-150),rng.uniform(120,S-160)])
    if c[0]>S*.30 and c[0]<S*.76 and c[1]>S*.32 and c[1]<S*.70: continue
    if np.linalg.norm(waterpoints-c,axis=1).min()<170: continue
    if np.linalg.norm(roadpoints-c,axis=1).min()<95: continue
    key=rng.choice(['hill_340','saddle_280','shelf_280']); w,h=catalog[key]['size']; box=[c[0]-w/2,c[1]-h*.72,w,h]
    if any(rect_intersect(box,q,20) for q in reserves): continue
    if sea_overlap(box): continue
    if any(abs(t['pos'][0]-box[0])<200 and abs(t['pos'][1]-box[1])<150 for t in terrain): continue
    place_terrain(key,c,rng.random()<.5,.90)
    if len(terrain)>110: break

for m in data['terrainPlanning']['crossingLandmarks']:
    if m['sourceType']=='ridge':
        c=point(m['pos']); w,h=catalog['saddle_280']['size']; box=[c[0]-w/2,c[1]-h*.72,w,h]
        if not any(rect_intersect(box,q,10) for q in reserves): place_terrain('saddle_280',c,False,.75,'pass')

forests=[]
for attempt in range(8000):
    if len(forests)>=155: break
    c=np.array([rng.uniform(100,S-100),rng.uniform(120,S-100)])
    if c[0]>S*.94 and c[1]<S*.28: continue
    if np.linalg.norm(waterpoints-c,axis=1).min()<65: continue
    if np.linalg.norm(roadpoints-c,axis=1).min()<95: continue
    key=rng.choice(['forest_a','forest_b','forest_c'])+'_'+str(rng.choice([92,116,140])); w,h=catalog[key]['size']; box=[c[0]-w/2,c[1]-h*.72,w,h]
    if any(rect_intersect(box,q,25) for q in reserves+label_boxes): continue
    if sea_overlap(box): continue
    if any(np.linalg.norm(c-np.array(f['center']))<165 for f in forests): continue
    forests.append({'asset':key,'pos':[round(box[0]),round(box[1])],'center':c.tolist(),'flip':rng.random()<.5,'alpha':.80})

def crossing(a,b,c,d):
    r=b-a; s=d-c; den=r[0]*s[1]-r[1]*s[0]
    if abs(den)<1e-8:return None
    v=c-a; t=(v[0]*s[1]-v[1]*s[0])/den; u=(v[0]*r[1]-v[1]*r[0])/den
    return a+t*r if 0<=t<=1 and 0<=u<=1 else None
grid={}
for i,(a,b,w) in enumerate(watersegments):
    for gx in range(int(min(a[0],b[0])//128),int(max(a[0],b[0])//128)+1):
        for gy in range(int(min(a[1],b[1])//128),int(max(a[1],b[1])//128)+1): grid.setdefault((gx,gy),[]).append(i)
bridges=[]
for road in roads:
    for a0,b0 in zip(road['points'],road['points'][1:]):
        a,b=np.array(a0),np.array(b0); candidates=set()
        for gx in range(int(min(a[0],b[0])//128),int(max(a[0],b[0])//128)+1):
            for gy in range(int(min(a[1],b[1])//128),int(max(a[1],b[1])//128)+1):candidates.update(grid.get((gx,gy),[]))
        for i in candidates:
            c,d,width=watersegments[i]; p=crossing(a,b,c,d)
            if p is None or any(np.linalg.norm(p-np.array(q['pos']))<30 for q in bridges):continue
            near=min(water_marks,key=lambda m:np.linalg.norm(point(m['pos'])-p))
            known=np.linalg.norm(point(near['pos'])-p)<60
            road_unit=(b-a)/np.linalg.norm(b-a); water_unit=(d-c)/np.linalg.norm(d-c)
            crossing_sine=abs(road_unit[0]*water_unit[1]-road_unit[1]*water_unit[0])
            bridges.append({'pos':p.tolist(),'angle':math.atan2((b-a)[1],(b-a)[0]),'length':width/max(.18,crossing_sine)+26,'kind':near['kind'] if known else '木桥','sourceId':near['id'] if known else None,'presentationOnly':not known})
for m in water_marks:
    if not any(b['sourceId']==m['id'] for b in bridges):
        p=point(m['pos']); nodeid=m['nodeId']; road=next(r for r in roads if r['from']==nodeid or r['to']==nodeid); ps=np.array(road['points']); vec=ps[-1]-ps[0]
        bridges.append({'pos':p.tolist(),'angle':math.atan2(vec[1],vec[0]),'length':68,'kind':m['kind'],'sourceId':m['id'],'presentationOnly':False})

source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
layout={'worldSize':S,'tileSize':2048,'catalog':catalog,'placements':placements,'terrain':terrain,'forests':forests,'waters':waters,'lakes':lakepolys,'bridges':bridges,'sourceHash':source_hash,'sourceGraphCounts':{'named':len(data['nodes']),'transit':len(data['transitNodes']),'edges':len(roads),'routes':len(data['routes'])},'originalBridgeIds':[m['id'] for m in water_marks],'supplementalBridgeArt':sum(b['presentationOnly'] for b in bridges)}
(ART/'presentation.json').write_text(json.dumps(layout,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
(DOC/'节点落位清单.json').write_text(json.dumps(placements,ensure_ascii=False,indent=2),encoding='utf-8')
issues=[]
for i,p in enumerate(placements):
    for q in placements[i+1:]:
        if rect_intersect(p['rect'],q['rect']): issues.append([p['name'],q['name']])
audit={'nodeCount':len(placements),'classificationCount':len({p['id'] for p in placements}),'mountainShelves':[p['name'] for p in placements if p['footing']=='mountain_shelf'],'iconOverlaps':issues,'labelConflicts':[p['name'] for p in placements if p['labelConflicts']],'forestCount':len(forests),'terrainGroups':len(terrain),'originalBridgesRepresented':len({b['sourceId'] for b in bridges if b['sourceId']}),'supplementalBridgeArt':layout['supplementalBridgeArt'],'sourceUnchanged':hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_hash}
wet_civil=[]
for p in placements:
    if p['type'] in ['wild','dock']: continue
    x,y,w,h=p['rect']; pad=8
    if any(np.any((np.array(water['points'])[:,0]>x-pad)&(np.array(water['points'])[:,0]<x+w+pad)&(np.array(water['points'])[:,1]>y-pad)&(np.array(water['points'])[:,1]<y+h+pad)) for water in waters): wet_civil.append(p['name'])
audit['civilRiverIntrusions']=wet_civil
audit['dryWildRiverIntrusions']=[]
for p in placements:
    if p['type']!='wild' or p['kind']=='pool': continue
    x,y,w,h=p['rect']; pad=8
    if any(np.any((np.array(water['points'])[:,0]>x-pad)&(np.array(water['points'])[:,0]<x+w+pad)&(np.array(water['points'])[:,1]>y-pad)&(np.array(water['points'])[:,1]<y+h+pad)) for water in waters): audit['dryWildRiverIntrusions'].append(p['name'])
audit['shoreSetbacks']=[{'name':p['name'],'offset':p['artOffset']} for p in placements if p['footing']=='riverside_setback']
audit['bridgeCenterGaps']=[{'id':m['id'],'pixels':round(min(dist_segment(point(m['pos']),a,b)[0] for a,b,_ in watersegments),3)} for m in water_marks]
audit['roadsInsideLakes']=[]
for lake in lakepolys[:2]:
    poly=lake['points']; closed=catmull([poly[-1]]+poly+[poly[0],poly[1]],step=5).tolist()
    for r in roads:
        if any(inside_polygon(q,closed) for q in r['points']): audit['roadsInsideLakes'].append({'lake':lake['name'],'edge':r['key']})
(DOC/'落位检查.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(audit,ensure_ascii=False))
