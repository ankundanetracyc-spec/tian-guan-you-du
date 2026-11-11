"""Bake complete mountain blockers and reroute the existing opaque road IDs.

The original water/bridge grid is retained. Old colour-cut mountain cells are
replaced by complete native mountain silhouettes; no roads carve them open.
"""
from pathlib import Path
import heapq, json, math, hashlib
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT=Path(__file__).resolve().parents[2]
RES=ROOT/'resources/world/bai_chuan'
OUT=ROOT/'output/bai_chuan_layered_20261106'
PROFILE='complete_mountains_20261106_v1'

def read(path): return json.loads(path.read_text(encoding='utf-8'))
def write(path,data): path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def main():
    base=read(RES/'free_navigation_3x3_20261104.json')
    layout=read(RES/'free_layout_3x3_20261104.json')
    n=base['columns']; cell=base['cell_size']
    grid=np.array([[int(c) for c in row] for row in base['rows']],dtype=np.uint8)
    grid[grid==1]=0
    alpha=np.asarray(Image.open(ROOT/'textures/world/bai_chuan/layered_20261106/feature_data.png'))[:,:,1]
    mask=ndi.binary_dilation(alpha>=90,iterations=1)
    pad=n*8-mask.shape[0]
    solid=np.pad(mask,((0,pad),(0,pad))).reshape(n,8,n,8).max((1,3))
    grid[solid]=1
    components,_=ndi.label(grid==0)
    sizes=np.bincount(components.ravel());sizes[0]=0
    mainland=components==sizes.argmax()
    valid_y,valid_x=np.nonzero(mainland & (ndi.distance_transform_edt(grid==0)>=1.5))
    valid=np.column_stack(((valid_x+.5)*cell,(valid_y+.5)*cell))
    positions={k:r['drawPos'] for k,r in layout['locations'].items()}
    positions.update(layout['transitPositions'])
    moves=[]
    for ident,point in list(positions.items()):
        x,y=point;gx,gy=int(x/cell),int(y/cell)
        if mainland[gy,gx]: continue
        index=((valid-np.array(point))**2).sum(1).argmin()
        chosen=valid[index].tolist()
        distance=math.dist(point,chosen)
        if distance>650: raise ValueError(f'No local mountain-foot access: {ident} {distance}')
        moves.append({'id':ident,'from':point,'to':chosen,'distance':distance})
        positions[ident]=chosen
        if ident in layout['locations']:
            row=layout['locations'][ident];dx,dy=chosen[0]-x,chosen[1]-y
            row['drawPos']=chosen
            r=row['rect'];row['rect']=[r[0]+dx,r[1]+dy,r[2]+dx,r[3]+dy]
        else: layout['transitPositions'][ident]=chosen

    def free(x,y): return 0<=x<n and 0<=y<n and grid[y,x]==0
    def clear(a,b):
        ax,ay=a[0]/cell,a[1]/cell;bx,by=b[0]/cell,b[1]/cell
        x,y=math.floor(ax),math.floor(ay);ex,ey=math.floor(bx),math.floor(by)
        if not free(x,y) or not free(ex,ey):return False
        dx,dy=bx-ax,by-ay;sx=1 if dx>0 else -1;sy=1 if dy>0 else -1
        tx=((x+1-ax) if sx>0 else (ax-x))/abs(dx) if dx else math.inf
        ty=((y+1-ay) if sy>0 else (ay-y))/abs(dy) if dy else math.inf
        dtx=1/abs(dx) if dx else math.inf;dty=1/abs(dy) if dy else math.inf
        for _ in range(n*2+2):
            if (x,y)==(ex,ey):return True
            if abs(tx-ty)<1e-7:
                if not free(x+sx,y) or not free(x,y+sy):return False
                x+=sx;y+=sy;tx+=dtx;ty+=dty
            elif tx<ty:x+=sx;tx+=dtx
            else:y+=sy;ty+=dty
            if not free(x,y):return False
        return False

    def path(a,b):
        if clear(a,b):return [a,b]
        start=(int(a[0]/cell),int(a[1]/cell));end=(int(b[0]/cell),int(b[1]/cell))
        queue=[(0.,start)];cost={start:0.};previous={}
        while queue:
            _,current=heapq.heappop(queue)
            if current==end:break
            x,y=current
            for sx,sy in [(1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,1),(1,-1),(-1,-1)]:
                nx,ny=x+sx,y+sy
                if not free(nx,ny) or (sx and sy and (not free(nx,y) or not free(x,ny))):continue
                nc=cost[current]+(math.sqrt(2) if sx and sy else 1.)
                if nc>=cost.get((nx,ny),math.inf):continue
                cost[(nx,ny)]=nc;previous[(nx,ny)]=current
                ex,ey=abs(nx-end[0]),abs(ny-end[1])
                heuristic=max(ex,ey)+(math.sqrt(2)-1)*min(ex,ey)
                heapq.heappush(queue,(nc+heuristic,(nx,ny)))
        if end not in previous:raise ValueError(f'Isolated endpoints {a} {b}')
        cells=[end]
        while cells[-1]!=start:cells.append(previous[cells[-1]])
        cells.reverse()
        raw=[a]+[[(x+.5)*cell,(y+.5)*cell] for x,y in cells]+[b]
        # Bounded line-of-sight smoothing with the same corner rule as runtime.
        result=[a];i=0
        while i<len(raw)-1:
            chosen=i+1
            for j in range(i+2,min(len(raw),i+70)):
                if clear(raw[i],raw[j]):chosen=j
            if not clear(result[-1],raw[chosen]):raise ValueError('Invalid smoothed segment')
            if math.dist(result[-1],raw[chosen])>.001:result.append(raw[chosen])
            i=chosen
        return result

    changed=0
    for ident,edge in layout['displayGraph']['edges'].items():
        old=layout['displayEdges'][ident]
        trial=[positions[edge['from']]]+old[1:-1]+[positions[edge['to']]]
        if not all(clear(a,b) for a,b in zip(trial,trial[1:])):
            trial=path(positions[edge['from']],positions[edge['to']]);changed+=1
        layout['displayEdges'][ident]=trial
    base.update(rows=[''.join(map(str,row)) for row in grid.tolist()],navigation_profile=PROFILE,
        mainland_rows=[''.join('1' if c else '0' for c in row) for row in mainland],
        mountain_source='native_complete_silhouettes_20261106',water_source='previous_authored_water_and_bridge_grid',
        mountain_mask_sha256=hashlib.sha256(alpha.tobytes()).hexdigest())
    layout.update(version='free_layout_complete_mountains_20261106',navigation_profile=PROFILE,mountainFootAdjustments=moves)
    write(RES/'free_navigation_mountains_20261106.json',base)
    write(RES/'free_layout_mountains_20261106.json',layout)
    scenery=read(RES/'layered_landscape_20261106.json')
    before=len(scenery['trees'])
    rects=[row['rect'] for row in layout['locations'].values()]
    scenery['trees']=[tree for tree in scenery['trees'] if not any(a-25<=tree['position'][0]<=c+25 and b-15<=tree['position'][1]<=d+32 for a,b,c,d in rects)]
    write(RES/'layered_landscape_20261106.json',scenery)
    decomposition=read(OUT/'decomposition_report.json')
    decomposition['trees_before_place_clearance']=decomposition.get('trees_before_place_clearance',decomposition['trees'])
    decomposition['trees']=len(scenery['trees'])
    decomposition['data_sha256']=hashlib.sha256((RES/'layered_landscape_20261106.json').read_bytes()).hexdigest()
    write(OUT/'decomposition_report.json',decomposition)
    report={'profile':PROFILE,'mountain_cells':int((grid==1).sum()),'water_cells':int((grid==2).sum()),
        'places':len(layout['locations']),'anchors':len(positions),'relocated_anchors':moves,
        'rerouted_edges':changed,'edges':len(layout['displayEdges']),'trees_after_place_clearance':len(scenery['trees']),
        'extra_trees_removed':decomposition['trees_before_place_clearance']-len(scenery['trees']),
        'all_anchors_on_mainland':all(mainland[int(y/cell),int(x/cell)] for x,y in positions.values()),
        'complete_mountain_cells_have_no_road_carve':bool(np.all(grid[solid]==1))}
    write(OUT/'mountain_navigation_report.json',report)
    print(json.dumps({k:v for k,v in report.items() if k!='relocated_anchors'},ensure_ascii=False))
    print('moved_places',[(r['id'],round(r['distance'],1)) for r in moves if r['id'].startswith('bc_place')])

if __name__=='__main__':main()
