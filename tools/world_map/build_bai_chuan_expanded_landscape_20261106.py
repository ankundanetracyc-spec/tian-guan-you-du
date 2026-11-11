"""Compose native scenery with explicit XYZ on the expanded8000km map.

This tool reads raster alpha to produce placement/collision data; it does not
repaint the generated art. Surface water is separately baked by the water tool.
"""
from pathlib import Path
import json, math, hashlib, heapq, time
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
import cv2

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'resources/world/bai_chuan'
ART=ROOT/'textures/world/bai_chuan/expanded3x_20261106'
OUT=ROOT/'output/bai_chuan_expanded3x_20261106'
WORLD=22118.4
CELL=15.678468899521532
N=1413
SCALE=3.
PROFILE='expanded3x_20261106_v1'

def read(p):return json.loads(p.read_text(encoding='utf-8'))
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rows(a):return [''.join(map(str,row)) for row in a.tolist()]
def rect_intersects(a,b):return a[0]<b[0]+b[2] and b[0]<a[0]+a[2] and a[1]<b[1]+b[3] and b[1]<a[1]+a[3]

def main():
    started=time.perf_counter();OUT.mkdir(parents=True,exist_ok=True)
    old=read(RES/'layered_landscape_20261106.json')
    complete_path=ART/'mountain_catalog_complete.json'
    if complete_path.exists():
        complete=read(complete_path)
        old['mountain_catalog']=complete['mountain_catalog']
        old['mountain_sources']=complete['mountain_sources']
    layout=read(RES/'free_layout_mountains_20261106.json')
    node_catalog=read(ART/'nodes/node_catalog.json')
    water_data=read(RES/'expanded_water_20261106.json')
    water_data['water_distance']=water_data.get('water_distance',water_data['water_sdf'])
    water_data['bridge_atlas']=next(iter(water_data['bridge_catalog'].values()))['atlas']
    water_grid=np.array([[int(c) for c in row] for row in water_data['water_rows']],dtype=np.uint8)==2
    bridge_open=np.array([[c=='1' for c in row] for row in water_data['bridge_open_rows']],dtype=bool)
    grid=np.where(water_grid,2,0).astype(np.uint8);grid[bridge_open]=0
    outside=(np.arange(N)+.5)*CELL>=WORLD
    grid[outside,:]=2;grid[:,outside]=2
    water_grid[outside,:]=True;water_grid[:,outside]=True
    bridge_open[outside,:]=False;bridge_open[:,outside]=False
    source_rgb=np.asarray(Image.open(ROOT/'textures/world/bai_chuan/original_3x3_20261104/master_8000.png').convert('RGB')).astype(np.int16)
    r,g,b=source_rgb.transpose(2,0,1)
    forest=(g>=r*.94)&(g-b>14)&(r-b>16)&(source_rgb.mean(2)<158)&(b<135)
    forest=ndi.binary_fill_holes(ndi.binary_closing(forest,iterations=2))
    # The high-resolution collision grid, not a coarse colour tile, owns access.
    clear=ndi.distance_transform_edt(grid==0)
    safe_y,safe_x=np.nonzero(clear>=2.)
    safe_points=np.column_stack(((safe_x+.5)*CELL,(safe_y+.5)*CELL))
    bindings={r['id']:r for r in node_catalog['location_bindings']}
    positions={k:(np.array(v['drawPos'])*SCALE).tolist() for k,v in layout['locations'].items()}
    positions.update({k:(np.array(v)*SCALE).tolist() for k,v in layout['transitPositions'].items()})
    reserved=[];anchor_moves=[]
    for ident,row in layout['locations'].items():
        original=positions[ident];point=np.array(original)
        binding=bindings.get(ident)
        asset=node_catalog['assets'][binding['asset']] if binding else None
        width=float(binding['world_width']) if binding else 36.
        height=width*asset['native_size'][1]/asset['native_size'][0] if asset else 36.
        pivot=asset['pivot'] if asset else [.5,.5]
        # Buildings and path entries may sit at a bank, but never inside water.
        gx,gy=np.floor(point/CELL).astype(int)
        if clear[gy,gx]<2.:
            index=((safe_points-point)**2).sum(1).argmin();point=safe_points[index].copy()
        x,y=map(float,point)
        if math.dist(original,point)>.01:anchor_moves.append({'id':ident,'from':original,'to':[x,y],'reason':'shore_access'})
        positions[ident]=[x,y]
        rect=[x-width*pivot[0],y-height*pivot[1],width,height]
        row.update(drawPos=[x,y],rect=[rect[0],rect[1],rect[0]+width,rect[1]+height],
            native_asset=binding['asset'] if binding else '',x=x,y=y,z=y)
        # Give the full building and its front approach a real valley.
        reserved.append([rect[0]-45,rect[1]-30,width+90,height+125])
    for bridge in water_data['bridges']:
        x,y,w,h=bridge['bounds'];reserved.append([x-65,y-65,w+130,h+130])

    mountain_bank={key:Image.open(ROOT/entry['path'].removeprefix('res://')).convert('RGBA') for key,entry in old['mountain_sources'].items()}
    mountain_images={}
    for key,entry in old['mountain_catalog'].items():
        x,y,w,h=entry['region'];mountain_images[key]=mountain_bank[entry['bank']].crop((x,y,x+w,y+h))
    mountain_grid=np.zeros((N,N),dtype=bool)
    rng=np.random.default_rng(2026110603)
    mountains=[]
    max_height=0.
    def raster(bounds,alpha):
        x,y,w,h=bounds
        xa,ya=max(0,int(math.floor(x/CELL))-1),max(0,int(math.floor(y/CELL))-1)
        xb,yb=min(N,int(math.ceil((x+w)/CELL))+1),min(N,int(math.ceil((y+h)/CELL))+1)
        if xa>=xb or ya>=yb:return None
        # Rasterise every visible native pixel's actual world footprint. Resizing
        # a thin silhouette to cell dimensions averages away opaque ridge tips,
        # and also changes its alignment when bounds straddle cell boundaries.
        source=np.asarray(alpha)
        sy,sx=np.nonzero(source>=46)  # Shader begins visibility at alpha0.18.
        a=np.zeros((yb-ya,xb-xa),dtype=bool)
        for edge_x,edge_y in [(0.,0.),(1.-1e-7,0.),(0.,1.-1e-7),(1.-1e-7,1.-1e-7)]:
            gx=np.floor((x+(sx+edge_x)*w/alpha.width)/CELL).astype(np.int32)-xa
            gy=np.floor((y+(sy+edge_y)*h/alpha.height)/CELL).astype(np.int32)-ya
            a[gy,gx]=True
        # Include a full neighbouring cell beyond the native bounds as well.
        a=ndi.binary_dilation(a,iterations=1)
        return xa,ya,xb,yb,a
    # Group native massifs along the old geographic ranges. Space them rather
    # than making a single low-resolution painting three times as large.
    for original in old['mountains']:
        ox,oy,ow,oh=original['bounds']
        if ow<75 and oh<55:continue
        region_width=ow*SCALE
        count=max(1,min(5,int(round(region_width/640))))
        span=max(0.,region_width-min(820.,max(130.,ow*1.08)))
        for index in range(count):
            width=min(820.,max(100.,ow*rng.uniform(.98,1.18)))
            height=min(510.,max(65.,oh*rng.uniform(.94,1.1)))
            if width/height<1.05:height=width/1.05
            foot_x=ox*SCALE+region_width*.5 + (index-(count-1)*.5)*(span/max(1,count-1))
            foot_y=(oy+oh)*SCALE + rng.uniform(-55,55)
            target_aspect=width/height
            choices=sorted(mountain_images,key=lambda k:abs(math.log((mountain_images[k].width/mountain_images[k].height)/target_aspect)))
            key=choices[(index+int(original['label']))%2] if width>170 else 'b1'
            image=mountain_images[key]
            aspect=image.width/image.height
            # Native art is scaled uniformly, with a small near-view sampling
            # margin. Narrow old colour fragments must not stretch into spikes.
            width=min(width,image.width*.96/.8,510.*aspect)
            height=width/aspect
            pivot=old['mountain_catalog'][key].get('pivot',[.5,1.])
            alpha=image.getchannel('A')
            flip=bool(rng.integers(0,2))
            if flip:alpha=alpha.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            placed=None
            offsets=[(0,0)]+[(float(rng.uniform(-330,330)),float(rng.uniform(-210,210))) for _ in range(28)]
            for dx,dy in offsets:
                bounds=[foot_x+dx-width*pivot[0],foot_y+dy-height*pivot[1],width,height]
                if bounds[0]<10 or bounds[1]<10 or bounds[0]+width>WORLD-10 or bounds[1]+height>WORLD-10:continue
                if any(rect_intersects(bounds,r) for r in reserved):continue
                result=raster(bounds,alpha)
                xa,ya,xb,yb,a=result
                if (a&(water_grid[ya:yb,xa:xb]|bridge_open[ya:yb,xa:xb])).any():continue
                placed=(bounds,result);break
            if placed is None:continue
            bounds,(xa,ya,xb,yb,a)=placed
            mountain_grid[ya:yb,xa:xb]|=a
            x=bounds[0]+width*pivot[0];y=bounds[1]+height*pivot[1]
            mountains.append({'id':f'mountain_{len(mountains):05d}','kind':'mountain','asset':key,'flip':flip,
                'x':x,'y':y,'z':y,'position':[x,y],'bounds':bounds,'layer':'ground_props','source_region':original['id']})
            max_height=max(max_height,height)
    grid[mountain_grid]=1
    components,_=ndi.label(grid==0)
    comp_sizes=np.bincount(components.ravel());comp_sizes[0]=0
    main_id=int(comp_sizes.argmax());mainland=components==main_id
    landing=mainland&(ndi.distance_transform_edt(grid==0)>=1.5)
    ly,lx=np.nonzero(landing);landing_points=np.column_stack(((lx+.5)*CELL,(ly+.5)*CELL))
    # Invisible compatibility indices retain their IDs but acquire legal feet.
    for ident,point in list(positions.items()):
        x,y=point;gx,gy=int(x/CELL),int(y/CELL)
        if mainland[gy,gx]:continue
        index=((landing_points-np.array(point))**2).sum(1).argmin();chosen=landing_points[index].tolist()
        distance=math.dist(point,chosen)
        if distance>1000:raise ValueError(f'No same-region access for {ident}: {distance}')
        positions[ident]=chosen;anchor_moves.append({'id':ident,'from':point,'to':chosen,'reason':'mountain_or_isolated_access'})
        if ident in layout['locations']:
            row=layout['locations'][ident];dx,dy=chosen[0]-x,chosen[1]-y
            r=row['rect'];row['rect']=[r[0]+dx,r[1]+dy,r[2]+dx,r[3]+dy]
            row.update(drawPos=chosen,x=chosen[0],y=chosen[1],z=chosen[1])
        else:layout['transitPositions'][ident]=chosen
    # Trees are native sprites on geographic woodland patches, with explicit
    # coordinates. The entire canopy rectangle avoids mountains and buildings.
    reserved_final=[]
    for row in layout['locations'].values():
        x0,y0,x1,y1=row['rect'];reserved_final.append([x0-32,y0-20,x1-x0+64,y1-y0+95])
    for bridge in water_data['bridges']:
        x,y,w,h=bridge['bounds'];reserved_final.append([x-35,y-35,w+70,h+70])
    trees=[]
    for y0 in np.arange(80,WORLD-50,84):
        for x0 in np.arange(50,WORLD-50,92):
            x=float(x0+rng.uniform(-19,19));y=float(y0+rng.uniform(-16,16))
            sx,sy=int(x/WORLD*3762),int(y/WORLD*3762)
            if not forest[sy,sx]:continue
            height=float(rng.uniform(90,126));key=str(rng.choice([0,1,2,3],p=[.46,.18,.18,.18]))
            region=old['tree_catalog'][key]['region'];width=height*region[2]/region[3]
            bounds=[x-width*.5,y-height,width,height+3]
            xa,ya=max(0,int(bounds[0]/CELL)),max(0,int(bounds[1]/CELL));xb,yb=min(N,int(math.ceil((bounds[0]+width)/CELL))),min(N,int(math.ceil((y+3)/CELL)))
            if mountain_grid[ya:yb,xa:xb].any() or grid[int(y/CELL),int(x/CELL)]!=0:continue
            if any(rect_intersects(bounds,r) for r in reserved_final):continue
            trees.append({'id':f'tree_{len(trees):05d}','kind':'tree','asset':key,'x':round(x,4),'y':round(y,4),'z':round(y,4),
                'position':[round(x,4),round(y,4)],'height':round(height,4),'flip':bool(rng.integers(0,2)),'bounds':bounds,'layer':'ground_props'})

    def free(x,y):return 0<=x<N and 0<=y<N and grid[y,x]==0
    def clear_segment(a,b):
        # Conservative offline supercover; runtime uses the same corner rule.
        ax,ay=a[0]/CELL,a[1]/CELL;bx,by=b[0]/CELL,b[1]/CELL
        x,y=math.floor(ax),math.floor(ay);ex,ey=math.floor(bx),math.floor(by)
        if not free(x,y) or not free(ex,ey):return False
        dx,dy=bx-ax,by-ay;sx=1 if dx>0 else -1;sy=1 if dy>0 else -1
        tx=((x+1-ax)if sx>0 else(ax-x))/abs(dx)if dx else math.inf
        ty=((y+1-ay)if sy>0 else(ay-y))/abs(dy)if dy else math.inf
        dtx=1/abs(dx)if dx else math.inf;dty=1/abs(dy)if dy else math.inf
        for _ in range(N*2+2):
            if (x,y)==(ex,ey):return True
            if x==ex:tx=math.inf
            if y==ey:ty=math.inf
            if abs(tx-ty)<1e-7:
                if not free(x+sx,y)or not free(x,y+sy):return False
                x+=sx;y+=sy;tx+=dtx;ty+=dty
            elif tx<ty:x+=sx;tx+=dtx
            else:y+=sy;ty+=dty
            if not free(x,y):return False
        return False
    def find_path(a,b):
        if clear_segment(a,b):return[a,b]
        start=(int(a[0]/CELL),int(a[1]/CELL));end=(int(b[0]/CELL),int(b[1]/CELL))
        queue=[(0.,0.,start)];cost={start:0.};previous={}
        while queue:
            _,_,current=heapq.heappop(queue)
            if current==end:break
            x,y=current;g=cost[current]
            for sx,sy in[(1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)]:
                nx,ny=x+sx,y+sy
                if not free(nx,ny)or(sx and sy and(not free(nx,y)or not free(x,ny))):continue
                ng=g+(math.sqrt(2) if sx and sy else 1.)
                if ng>=cost.get((nx,ny),math.inf)-1e-9:continue
                cost[(nx,ny)]=ng;previous[(nx,ny)]=current
                vx,vy=abs(nx-end[0]),abs(ny-end[1]);h=max(vx,vy)+(math.sqrt(2)-1)*min(vx,vy)
                heapq.heappush(queue,(round(ng+h,9),h,(nx,ny)))
        if end not in previous:raise ValueError(f'No path {a}->{b}')
        route=[end]
        while route[-1]!=start:route.append(previous[route[-1]])
        route.reverse();raw=[a]+[[(x+.5)*CELL,(y+.5)*CELL]for x,y in route]+[b]
        result=[a];i=0
        while i<len(raw)-1:
            chosen=i+1
            for j in range(i+2,min(len(raw),i+70)):
                if clear_segment(raw[i],raw[j]):chosen=j
            if not clear_segment(result[-1],raw[chosen]):raise ValueError('Invalid path connector')
            if math.dist(result[-1],raw[chosen])>.001:result.append(raw[chosen])
            i=chosen
        return result
    rerouted=0
    fingerprint=hashlib.sha256(grid.tobytes()+json.dumps(positions,sort_keys=True).encode()).hexdigest()[:16]
    route_cache_path=OUT/f'route_cache_{fingerprint}.json'
    route_cache=read(route_cache_path) if route_cache_path.exists() else {}
    for i,(key,edge) in enumerate(layout['displayGraph']['edges'].items()):
        original=np.asarray(layout['displayEdges'][key])*SCALE
        trial=[positions[edge['from']]]+original[1:-1].tolist()+[positions[edge['to']]]
        if key in route_cache:
            trial=route_cache[key]
            rerouted+=1
        elif not all(clear_segment(a,b)for a,b in zip(trial,trial[1:])):
            trial=find_path(positions[edge['from']],positions[edge['to']]);rerouted+=1
            route_cache[key]=trial
        layout['displayEdges'][key]=trial
        if i%25==0:write(route_cache_path,route_cache)
        if i%100==0:print(f'ROUTES {i}/575',flush=True)
    write(route_cache_path,route_cache)
    layout['transitPositions']={ident:positions[ident] for ident in layout['transitPositions']}
    layout.update(version='free_layout_expanded3x_20261106',worldSize=WORLD,worldKm=8000.,coordinateScale=WORLD/3762,
        navigation_profile=PROFILE,native_node_catalog='res://textures/world/bai_chuan/expanded3x_20261106/nodes/node_catalog.json',
        expandedAnchorAdjustments=anchor_moves)
    nav={'version':2,'world_size':WORLD,'world_km':8000.,'cell_size':CELL,'columns':N,'rows':rows(grid),
        'navigation_profile':PROFILE,'mainland_rows':[''.join('1'if v else'0'for v in row)for row in mainland],
        'terrain_source':'native_xyz_scenery_and_water_20261106'}
    terrain=read(RES/'terrain_travel_20261105.json');terrain.update(world_size=WORLD,cell_size=terrain['cell_size']*SCALE)
    terrain_grid=np.array([list(row) for row in terrain['rows']])
    crossing=bridge_open.reshape(471,3,471,3).any((1,3))
    terrain_grid[crossing]='C'
    terrain['rows']=[''.join(row) for row in terrain_grid.tolist()]
    scene={'version':2,'world_size':WORLD,'world_km':8000.,'grid_size':640.,'source_size':[3762,3762],'source_scale':WORLD/3762,
        'ground_texture':old['ground_texture'],'ground_tone_data':old['ground_tone_data'],'ground_texture_average':old['ground_texture_average'],
        'water_distance':water_data['water_distance'],'water_texture':'res://textures/world/bai_chuan/expanded3x_20261106/surface/water_native.png',
        'tree_atlas':old['tree_atlas'],'tree_catalog':old['tree_catalog'],'mountain_sources':old['mountain_sources'],'mountain_catalog':old['mountain_catalog'],
        'bridge_atlas':water_data['bridge_atlas'],'bridge_catalog':water_data['bridge_catalog'],
        'mountains':mountains,'trees':trees,'bridges':water_data['bridges'],'max_prop_height':max_height,
        'runtime_texture_base':'res://textures/world/bai_chuan/expanded3x_20261106/runtime/',
        'coordinate_rule':'x/y are stable ground foot coordinates; z is explicit logical sorting coordinate. ground_decals use fixed z. Runtime converts ground z per camera without moving world positions.'}
    write(RES/'free_layout_expanded3x_20261106.json',layout)
    write(RES/'free_navigation_expanded3x_20261106.json',nav)
    terrain['source_files'].update({
        'resources/world/bai_chuan/free_layout_expanded3x_20261106.json':hashlib.sha256((RES/'free_layout_expanded3x_20261106.json').read_bytes()).hexdigest(),
        'resources/world/bai_chuan/free_navigation_expanded3x_20261106.json':hashlib.sha256((RES/'free_navigation_expanded3x_20261106.json').read_bytes()).hexdigest()})
    write(RES/'terrain_travel_expanded3x_20261106.json',terrain)
    write(RES/'landscape_expanded3x_20261106.json',scene)
    np.save(OUT/'mountain_collision_grid.npy',mountain_grid)
    report={'world_size':WORLD,'world_km':8000,'closest_zoom':.8,'old_100km_span_now_km':40.,'avatar_world_height':52,
        'places':len(layout['locations']),'anchors':len(positions),'edges':len(layout['displayEdges']),'rerouted_edges':rerouted,
        'mountains':len(mountains),'trees':len(trees),'bridges':len(scene['bridges']),'anchor_adjustments':anchor_moves,
        'columns':N,'cell_size':CELL,'mountain_cells':int(mountain_grid.sum()),'water_cells':int((grid==2).sum()),
        'all_anchors_on_mainland':all(mainland[int(y/CELL),int(x/CELL)]for x,y in positions.values()),
        'elapsed_seconds':time.perf_counter()-started}
    write(OUT/'layout_bake_report.json',report)
    print(json.dumps({k:v for k,v in report.items()if k!='anchor_adjustments'},ensure_ascii=True),flush=True)

if __name__=='__main__':main()
