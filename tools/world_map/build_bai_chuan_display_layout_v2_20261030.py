"""Display-only spacing/edge registration for the isolated pending-art preview.
Does not modify authoritative node/edge IDs, planning coordinates, or travel rules.
"""
from __future__ import annotations
import hashlib, json, math, heapq
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage, sparse
from scipy.sparse.linalg import spsolve

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/acceptance_20261030'
DATA=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
W=3762

def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,value): p.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
def integral(mask): return np.pad(mask.astype(np.int32).cumsum(0).cumsum(1),((1,0),(1,0)))
def boxsum(ii,x0,y0,x1,y1):
    x0=np.clip(x0,0,W).astype(int);x1=np.clip(x1,0,W).astype(int)
    y0=np.clip(y0,0,W).astype(int);y1=np.clip(y1,0,W).astype(int)
    return ii[y1,x1]-ii[y0,x1]-ii[y1,x0]+ii[y0,x0]

def main():
    before_hash=hashlib.sha256(DATA.read_bytes()).hexdigest()
    data=read(DATA); source=read(BASE/'presentation.json'); rows=source['nodes']
    image=Image.open(BASE/'master_3762.png').convert('RGB'); rgb=np.array(image).astype(np.float32)
    water=(rgb[:,:,2]>rgb[:,:,0]*1.15)&(rgb[:,:,2]>rgb[:,:,1]*1.04)
    wet=ndimage.binary_dilation(water,iterations=5); wet_ii=integral(wet)
    # Dark low-chroma mountain faces only: a soft art-placement cost, not elevation.
    mountain=(rgb.max(2)-rgb.min(2)<36)&(rgb.mean(2)<151)&~water
    mountain_ii=integral(mountain)
    original=np.array([r['drawPos'] for r in rows],float); positions=original.copy()
    built=np.array([r.get('asset') is not None for r in rows]); main=np.array([r.get('level')==1 for r in rows])
    fixed=np.flatnonzero(main); poles=original[fixed]
    region=((original[:,None]-poles[None])**2).sum(2).argmin(1)
    # Maximal movement is a display-only envelope, preserving the three-pole geography.
    radii=np.where(built,340,880)
    gx,gy=np.meshgrid(np.arange(90,W-90,28),np.arange(100,W-100,28))
    grid=np.column_stack((gx.ravel(),gy.ravel())).astype(float)
    candidates=[]; terrain_cost=[]
    for i,row in enumerate(rows):
        options=np.vstack((original[i],grid))
        delta=options-original[i]
        good=(delta**2).sum(1)<=radii[i]**2
        # Preserve the original nearest major faction zone, including neutral interfaces.
        zones=((options[:,None]-poles[None])**2).sum(2).argmin(1)
        good &= zones==region[i]
        options=options[good]
        width=row.get('width',0) or 16; height=row.get('height',0) or 16
        x0=options[:,0]-width*.5-4;y0=options[:,1]-height-3
        x1=options[:,0]+width*.5+4;y1=options[:,1]+4
        dry=boxsum(wet_ii,x0,y0,x1,y1)==0
        options=options[dry]
        if not len(options): raise RuntimeError('No dry placement for '+row['id'])
        # Avoid extensive mountain-face coverage for settlements, retain soft shoulders for sects.
        rock=boxsum(mountain_ii,options[:,0]-width*.5,options[:,1]-height,
                    options[:,0]+width*.5,options[:,1])/(width*height)
        terrain=rock*(8 if row['type']=='sect' else 32 if built[i] else 2)
        candidates.append(options);terrain_cost.append(terrain)
    matrix=np.where(built[:,None]&built[None,:],300.,232.)
    np.fill_diagonal(matrix,0.)
    for iteration in range(10):
        # Named buildings settle first. Wild locations absorb more of the redistribution.
        for i in list(np.flatnonzero(built&~main))+list(np.flatnonzero(~built)):
            options=candidates[i]
            distances=np.sqrt(((options[:,None]-positions[None])**2).sum(2))
            deficits=np.maximum(0,matrix[i][None]-distances)
            deficits[:,i]=0
            repulsion=(deficits**2).sum(1)*.009
            attraction=((options-original[i])**2).sum(1)*(.000065 if built[i] else .000018)
            # A broad crowding cost draws wilderness toward emptier areas without a grid layout.
            density=np.exp(-distances**2/(2*430**2)).sum(1)*3.8
            score=repulsion+attraction+terrain_cost[i]+density
            positions[i]=options[score.argmin()]
    positions=np.round(positions).astype(int)
    named={row['id']:positions[i].astype(float) for i,row in enumerate(rows)}
    old_named={node['id']:np.array(node['pos'],float)*W/100 for node in data['nodes']}
    old_transit={node['id']:np.array(node['pos'],float)*W/100 for node in data['transitNodes']}
    original_all=old_named|old_transit
    edges=data['roadCurveGeometry']['edges']
    # Harmonic extension across the existing adjacency moves shared junctions exactly once.
    transits=list(old_transit); ti={id:i for i,id in enumerate(transits)}
    matrix_l=sparse.lil_matrix((len(transits),len(transits)))
    rhs=np.zeros((len(transits),2))
    for key,edge in edges.items():
        a,b=edge['from'],edge['to']
        weight=1/max(30,np.linalg.norm(original_all[a]-original_all[b]))
        for u,v in [(a,b),(b,a)]:
            if u not in ti: continue
            j=ti[u];matrix_l[j,j]+=weight
            if v in ti: matrix_l[j,ti[v]]-=weight
            else: rhs[j]+=weight*(named[v]-old_named[v])
    displacement=np.column_stack([spsolve(matrix_l.tocsr(),rhs[:,axis]) for axis in range(2)])
    display_transit={id:old_transit[id]+displacement[i] for i,id in enumerate(transits)}
    # Pull submerged passage marks to the nearest dry bank, keeping the same node identity.
    distance,nearest=ndimage.distance_transform_edt(wet,return_indices=True)
    for id,p in display_transit.items():
        x,y=np.clip(np.round(p).astype(int),[4,4],[W-5,W-5])
        if wet[y,x]:
            p=np.array([nearest[1,y,x],nearest[0,y,x]],float)
        display_transit[id]=np.round(p).astype(float)
    all_display=named|display_transit
    display_edges={}
    # The old smooth curve is warped by its two endpoint deltas. Exact endpoint replacement
    # prevents detached gates, orphan marks and mismatches on shared path junctions.
    for key,edge in edges.items():
        a,b=edge['from'],edge['to']
        points=np.array(edge['points'],float)*W/100
        lengths=np.r_[0,np.linalg.norm(np.diff(points,axis=0),axis=1).cumsum()]
        t=lengths/max(lengths[-1],.001)
        points += (all_display[a]-original_all[a])[None]*(1-t[:,None])+(all_display[b]-original_all[b])[None]*t[:,None]
        points[0]=all_display[a];points[-1]=all_display[b]
        display_edges[key]=np.round(points,3).tolist()
    # Reroute the long water/sea excursions left by the endpoint warp. This edits
    # only drawing samples of the same edge, never adjacency or travel costs.
    step=14
    small_water=water[step//2::step,step//2::step]
    small_rock=mountain[step//2::step,step//2::step]
    label_land,nland=ndimage.label(~water)
    land_counts=np.bincount(label_land.ravel());land_counts[0]=0
    mainland=label_land==land_counts.argmax()
    shore=np.array([np.flatnonzero(row)[-1] if row.any() else int(W*.86) for row in mainland])
    shore=ndimage.median_filter(shore,size=45)
    sea=water&(np.arange(W)[None,:]>shore[:,None])
    small_sea=sea[step//2::step,step//2::step]
    cost=1+small_water.astype(float)*11+small_sea.astype(float)*160+small_rock.astype(float)*.8
    hh,ww=cost.shape
    rerouted=0
    directions=[(1,0,1),(-1,0,1),(0,1,1),(0,-1,1),(1,1,1.414),(-1,1,1.414),(1,-1,1.414),(-1,-1,1.414)]
    def route_edge(points):
        nonlocal rerouted
        points=np.array(points,float)
        samples=[]
        for p,q in zip(points[:-1],points[1:]):
            for t in np.linspace(0,1,max(2,int(np.linalg.norm(q-p)/7))):samples.append(p*(1-t)+q*t)
        samples=np.clip(np.array(samples).round().astype(int),0,W-1)
        water_hits=water[samples[:,1],samples[:,0]]
        sea_hits=sea[samples[:,1],samples[:,0]]
        if not sea_hits.any() and water_hits.mean()<.16:return points.tolist()
        start=np.clip((points[0]/step).astype(int),[0,0],[ww-1,hh-1]);end=np.clip((points[-1]/step).astype(int),[0,0],[ww-1,hh-1])
        sx,sy=map(int,start);ex,ey=map(int,end)
        # A broad local envelope allows a road around a cape while avoiding global detours.
        left=max(0,min(sx,ex)-22);right=min(ww-1,max(sx,ex)+22)
        top=max(0,min(sy,ey)-22);bottom=min(hh-1,max(sy,ey)+22)
        goal=(ex,ey);startkey=(sx,sy);queue=[(math.hypot(ex-sx,ey-sy),0,startkey)]
        scores={startkey:0};came={}
        while queue:
            _,g,(x,y)=heapq.heappop(queue)
            if g>scores.get((x,y),float('inf'))+.0001:continue
            if (x,y)==goal:break
            for dx,dy,length in directions:
                nx,ny=x+dx,y+dy
                if nx<left or nx>right or ny<top or ny>bottom:continue
                ng=g+length*(cost[y,x]+cost[ny,nx])*.5
                if ng<scores.get((nx,ny),float('inf')):
                    scores[nx,ny]=ng;came[(nx,ny)]=(x,y)
                    heapq.heappush(queue,(ng+math.hypot(ex-nx,ey-ny),ng,(nx,ny)))
        if goal not in scores:return points.tolist()
        path=[goal]
        while path[-1]!=startkey:path.append(came[path[-1]])
        path.reverse();p=np.array(path,float)*step+step*.5
        # Remove collinear samples, retain a shallow quadratic corner fillet.
        simplified=[p[0]]
        for i in range(1,len(p)-1):
            if abs((p[i,0]-p[i-1,0])*(p[i+1,1]-p[i,1])-(p[i,1]-p[i-1,1])*(p[i+1,0]-p[i,0]))>.01:simplified.append(p[i])
        simplified.append(p[-1]);p=np.array(simplified)
        if len(p)>2:
            smooth=[p[0]]
            for i in range(1,len(p)-1):
                before=p[i]*.7+p[i-1]*.3;after=p[i]*.7+p[i+1]*.3
                smooth.append(before)
                for t in [.25,.5,.75,1.]:smooth.append((1-t)**2*before+2*(1-t)*t*p[i]+t*t*after)
            smooth.append(p[-1]);p=np.array(smooth)
        p[0]=points[0];p[-1]=points[-1]
        rerouted+=1
        return np.round(p,3).tolist()
    display_edges={key:route_edge(points) for key,points in display_edges.items()}
    locations={}
    for i,row in enumerate(rows):
        x,y=positions[i];entry={'drawPos':[int(x),int(y)],'previousDrawPos':row['drawPos'],
            'displayOffsetFromLogical':np.round(positions[i]-np.array(row['logicalPos']),3).tolist()}
        if built[i]:
            w,h=row['width'],row['height'];entry['rect']=[int(x-w/2),int(y-h),int(x-w/2)+w,int(y)]
        locations[row['id']]=entry
    output={'version':'display_layout_v2_20261030','worldSize':W,'displayOnly':True,
        'logicalPlanSha256':before_hash,'note':'Visual redistribution only; no change to route adjacency or travel settlement. Curves are not verified elevation-aware roads.',
        'locations':locations,'transitPositions':{id:p.tolist() for id,p in display_transit.items()},'displayEdges':display_edges}
    write(BASE/'display_layout_v2.json',output)
    # Connectivity/endpoint checks are exact, independent of the art-placement score.
    endpoint_errors=[]
    for key,e in edges.items():
        pts=display_edges[key]
        if np.linalg.norm(np.array(pts[0])-all_display[e['from']])>.01 or np.linalg.norm(np.array(pts[-1])-all_display[e['to']])>.01:endpoint_errors.append(key)
    def spacing(p,mask=None):
        p=p if mask is None else p[mask]
        dist=np.sqrt(((p[:,None]-p[None])**2).sum(2));np.fill_diagonal(dist,np.inf)
        return {'minimum':round(float(dist.min()),2),'medianNearest':round(float(np.median(dist.min(1))),2)}
    def regions(p,mask):
        counts=np.zeros((3,3),int)
        for x,y in p[mask]:counts[min(2,int(y//1254)),min(2,int(x//1254))]+=1
        return counts.tolist()
    samples=[]
    for pts in display_edges.values():
        a=np.array(pts)
        for p,q in zip(a[:-1],a[1:]):
            for t in np.linspace(0,1,max(2,int(np.linalg.norm(q-p)/6))):samples.append(p*(1-t)+q*t)
    samples=np.clip(np.array(samples).round().astype(int),0,W-1)
    water_fraction=float(water[samples[:,1],samples[:,0]].mean())
    report={'displayOnly':True,'sourcePlanUnchanged':hashlib.sha256(DATA.read_bytes()).hexdigest()==before_hash,
        'counts':{'named':len(locations),'built':int(built.sum()),'wild':int((~built).sum()),'transit':len(display_transit),'edges':len(display_edges)},
        'endpointMismatchCount':len(endpoint_errors),'threeMajorAnchorsUnchanged':bool(np.array_equal(positions[fixed],original[fixed])),
        'allLocationsNearestFactionZoneUnchanged':bool(np.array_equal(((positions[:,None]-poles[None])**2).sum(2).argmin(1),region)),
        'spacingBefore':spacing(original),'spacingAfter':spacing(positions),
        'builtSpacingBefore':spacing(original,built),'builtSpacingAfter':spacing(positions,built),
        'wildNineRegionsBefore':regions(original,~built),'wildNineRegionsAfter':regions(positions,~built),
        'maxVisualShiftPixels':round(float(np.linalg.norm(positions-original,axis=1).max()),2),
        'routeWaterSampleFraction':round(water_fraction,4),'routeSeaSampleFraction':round(float(sea[samples[:,1],samples[:,0]].mean()),4),'waterAvoidanceReroutedEdges':rerouted,
        'namedNineRegionsBefore':regions(original,np.ones(len(rows),bool)),'namedNineRegionsAfter':regions(positions,np.ones(len(rows),bool)),
        'transitMarkWaterOverlapCount':sum(bool(water[int(p[1]),int(p[0])]) for p in display_transit.values()),
        'limits':'Route samples may cross river water; no new bridges or travel permissions introduced. Terrain masking is an art check, not a height/flow proof.'}
    write(BASE/'display_layout_v2_checks.json',report)
    # Debug diagram overlays route geometry and correctly sized actual sprites for human review.
    preview=image.copy().convert('RGBA');draw=ImageDraw.Draw(preview)
    for pts in display_edges.values():
        # Dashes at this QA scale are only a diagram of the existing connected curve.
        for i in range(1,len(pts),2):draw.line([tuple(pts[i-1]),tuple(pts[i])],fill=(91,71,40,155),width=2)
    for id,p in display_transit.items():
        x,y=p;draw.ellipse([x-2,y-2,x+2,y+2],fill=(235,218,175,255))
    for i,row in enumerate(rows):
        x,y=positions[i]
        if built[i]:
            icon=Image.open(ROOT/'textures/world/bai_chuan/node_sprites_20261030/icons'/f"{row['asset']}.png").convert('RGBA')
            preview.alpha_composite(icon,(int(x-icon.width/2),int(y-icon.height)))
        else:draw.ellipse([x-4,y-4,x+4,y+4],fill=(236,218,174,255),outline=(85,73,43,255),width=1)
    preview.convert('RGB').resize((1505,1505),Image.Resampling.LANCZOS).save(BASE/'display_layout_v2_preview.jpg',quality=92)
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__': main()
