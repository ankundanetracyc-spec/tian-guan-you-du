"""Place native transparent scenery; derive collision input without repainting source art."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/layered_20261106'
OUT = ROOT / 'resources/world/bai_chuan/layered_landscape_20261106.json'
REVIEW = ROOT / 'output/bai_chuan_layered_20261106'
MASTER = ROOT / 'textures/world/bai_chuan/original_3x3_20261104/master_8000.png'
WORLD = 7372.8

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

def main():
    BASE.mkdir(parents=True, exist_ok=True); REVIEW.mkdir(parents=True, exist_ok=True)
    inputs = [MASTER, ROOT/'resources/world/bai_chuan/free_navigation_3x3_20261104.json', ROOT/'resources/world/bai_chuan/free_layout_3x3_20261104.json']
    hashes = {str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in inputs}
    rgb = np.asarray(Image.open(MASTER).convert('RGB')).astype(np.int16)
    h,w = rgb.shape[:2]; scale = WORLD/w
    r,g,b = rgb.transpose(2,0,1); mean = rgb.mean(2)
    water = (b-r>30)&(b-g>2)
    water_band = ndi.binary_dilation(water, iterations=2)
    forest = (g>=r*.94)&(g-b>14)&(r-b>16)&(mean<158)&(b<135)&~water_band
    forest = ndi.binary_fill_holes(ndi.binary_closing(forest, iterations=2))
    flabel,n = ndi.label(forest)
    fsizes = np.bincount(flabel.ravel()); fsizes[0]=0
    forest = fsizes[flabel]>=65
    # Cool rock surfaces are the strong mountain seeds. Short bounded growth
    # recovers adjacent ochre foothills without turning the ground into a wall.
    rock = (b>=g-13)&(b>=r-17)&(rgb.max(2)-rgb.min(2)<85)&(mean<191)&~water_band&~forest
    rock = ndi.binary_closing(rock, iterations=2)
    labels,n = ndi.label(rock)
    sizes = np.bincount(labels.ravel()); sizes[0]=0
    rock = sizes[labels]>=35
    warm = (r-g>8)&(g-b>12)&(r<193)&(g<173)&~forest&~water_band
    mountain = (ndi.binary_dilation(rock, iterations=5)&(warm|ndi.binary_dilation(rock,iterations=1)))
    mountain = ndi.binary_fill_holes(ndi.binary_closing(mountain, iterations=2))&~water_band&~forest
    labels,n = ndi.label(mountain)
    slices = ndi.find_objects(labels)
    retained = np.zeros_like(labels,dtype=np.uint16)
    mountains=[]
    for old_id,box in enumerate(slices,1):
        if box is None: continue
        yy,xx=box; bw=xx.stop-xx.start; bh=yy.stop-yy.start
        local=labels[box]==old_id; area=int(local.sum())
        if area<65 or bw<8 or bh<7: continue
        ident=len(mountains)+1
        if ident>=65535: raise ValueError('Too many mountain components')
        retained[box][local]=ident
        # Padding allows antialiased mask edges, not a rectangular RGB backing.
        x0,y0=max(0,xx.start-3),max(0,yy.start-3)
        x1,y1=min(w,xx.stop+3),min(h,yy.stop+3)
        mountains.append({'id':f'mountain_{ident:04d}','kind':'mountain','label':ident,
            'source_rect':[x0,y0,x1-x0,y1-y0],
            'position':[(xx.start+xx.stop)*.5*scale,yy.stop*scale],
            'source_foot':[(xx.start+xx.stop)*.5,yy.stop],
            'bounds':[x0*scale,y0*scale,(x1-x0)*scale,(y1-y0)*scale]})
    mountain=retained>0
    # A rock-colour mask is only a location seed, not a complete silhouette.
    # Native complete bare massifs replace the failed cut-out ridge fragments.
    mountain_catalog={}; mountain_images={}; mountain_sources={}
    for bank in ['a','b']:
        path=BASE/f'source/mountains_solid_{bank}.png'
        image=Image.open(path).convert('RGBA'); iw,ih=image.size
        image_alpha=np.asarray(image)[:,:,3]
        mountain_sources[bank]={'path':f'res://textures/world/bai_chuan/layered_20261106/source/{path.name}','size':[iw,ih],'sha256':sha(path)}
        for row in range(2):
            for col in range(2):
                xa,xb=col*iw//2,(col+1)*iw//2;ya,yb=row*ih//2,(row+1)*ih//2
                ys,xs=np.nonzero(image_alpha[ya:yb,xa:xb]>=128)
                ax0=max(xa,int(xs.min())+xa-4);ay0=max(ya,int(ys.min())+ya-4)
                ax1=min(xb,int(xs.max())+xa+5);ay1=min(yb,int(ys.max())+ya+5)
                key=f'{bank}{row*2+col}'
                mountain_catalog[key]={'bank':bank,'region':[ax0,ay0,ax1-ax0,ay1-ay0]}
                mountain_images[key]=image.crop((ax0,ay0,ax1,ay1)).getchannel('A')
    retained=np.zeros((h,w),dtype=np.uint16)
    mountain_alpha=np.zeros((h,w),dtype=np.uint8)
    for entry in mountains:
        x,y,bw,bh=entry['source_rect']
        old=mountain[y:y+bh,x:x+bw]
        wet=water_band[y:y+bh,x:x+bw]
        best=None
        for key,im in mountain_images.items():
            local_alpha=np.asarray(im.resize((bw,bh),Image.Resampling.LANCZOS)).copy()
            # The renderer uses the same alpha threshold/soft edge.
            t=np.clip((local_alpha.astype(np.float32)/255-.18)/(.85-.18),0,1)
            local_alpha=np.rint(t*t*(3-2*t)*255).astype(np.uint8)
            for flip in [False,True]:
                candidate=local_alpha[:,::-1] if flip else local_alpha
                solid=candidate>=100
                score=float((solid&wet).sum())*12+float((old&~solid).sum())*.45
                score+=abs(np.log((bw/bh)/(im.width/im.height)))*bw*bh*.015
                score+=(int(entry['label'])*int(key[-1])+ord(key[0]))%17*.01
                if best is None or score<best[0]: best=(score,key,flip,candidate.copy())
        _,key,flip,local=best
        # Preserve the river rather than clipping holes into a complete massif.
        # Rare old bounding boxes touched a bank; shift the whole small object
        # by the nearest few source pixels when its new solid base needs room.
        original_rect=entry['source_rect'].copy()
        if ((local>=100)&wet).any():
            offsets=sorted(((dx,dy) for dx in range(-32,33) for dy in range(-32,33)),key=lambda p:p[0]*p[0]+p[1]*p[1])
            for dx,dy in offsets:
                nx,ny=x+dx,y+dy
                if nx<0 or ny<0 or nx+bw>w or ny+bh>h:continue
                if not ((local>=100)&water_band[ny:ny+bh,nx:nx+bw]).any():
                    x,y=nx,ny
                    entry['source_rect']=[x,y,bw,bh]
                    entry['bounds']=[x*scale,y*scale,bw*scale,bh*scale]
                    entry['original_source_rect']=original_rect
                    break
        current=mountain_alpha[y:y+bh,x:x+bw]
        take=local>current
        current[take]=local[take]
        retained[y:y+bh,x:x+bw][take]=entry['label']
        entry.update(asset=key,flip=flip,
            position=[(x+bw*.5)*scale,(y+bh)*scale],
            source_foot=[x+bw*.5,y+bh])
    mountain=mountain_alpha>=100
    # This is a data texture, not display art: R=water, G=mountain alpha,
    # B/A=16-bit component identity. Read without colour-space conversion.
    alpha=mountain_alpha.astype(np.float32)/255
    _,nearest=ndi.distance_transform_edt(~mountain,return_indices=True)
    expanded=retained[tuple(nearest)]
    expanded[alpha<.004]=0
    data=np.empty((h,w,4),dtype=np.uint8)
    data[:,:,0]=np.rint(ndi.gaussian_filter(water_band.astype(np.float32),.65)*255).astype(np.uint8)
    data[:,:,1]=np.rint(alpha*255).astype(np.uint8)
    data[:,:,2]=(expanded%256).astype(np.uint8);data[:,:,3]=(expanded//256).astype(np.uint8)
    Image.fromarray(data,'RGBA').save(BASE/'feature_data.png')
    del nearest,expanded,flabel,labels,retained,alpha,data,mountain_alpha
    # Local plain-earth colour reference; the new native ground texture supplies
    # the visible grain. No mountain/tree colour is sampled into this field.
    valid=~water_band&~ndi.binary_dilation(mountain|forest,iterations=5)&(mean>110)
    global_tone=np.median(rgb[valid],axis=0)
    tone=np.zeros((128,128,3),dtype=np.uint8)
    for gy in range(128):
        y0,y1=int(gy*h/128),int((gy+1)*h/128)
        for gx in range(128):
            x0,x1=int(gx*w/128),int((gx+1)*w/128)
            selected=rgb[y0:y1,x0:x1][valid[y0:y1,x0:x1]]
            tone[gy,gx]=np.median(selected,axis=0) if len(selected)>20 else global_tone
    tone=ndi.gaussian_filter(tone.astype(np.float32),(1.2,1.2,0))
    Image.fromarray(np.rint(tone).astype(np.uint8)).save(BASE/'ground_tone_data.png')
    # Exact native atlas regions; generated alpha/RGB are never repainted.
    atlas_path=BASE/'source/trees_native.png'
    atlas=Image.open(atlas_path).convert('RGBA'); aw,ah=atlas.size
    atlas_alpha=np.asarray(atlas)[:,:,3]
    trees_catalog={}
    for row in range(2):
        for col in range(2):
            x0,x1=col*aw//2,(col+1)*aw//2;y0,y1=row*ah//2,(row+1)*ah//2
            ys,xs=np.nonzero(atlas_alpha[y0:y1,x0:x1]>=16)
            bx0=max(x0,int(xs.min())+x0-3);by0=max(y0,int(ys.min())+y0-3)
            bx1=min(x1,int(xs.max())+x0+4);by1=min(y1,int(ys.max())+y0+4)
            trees_catalog[str(row*2+col)]={'region':[bx0,by0,bx1-bx0,by1-by0], 'pivot':[.5,.99]}
    layout=json.loads(inputs[2].read_text(encoding='utf-8'))
    avoid=[]
    for loc in layout['locations'].values():
        x0,y0,x1,y1=loc['rect']; avoid.append((x0-25,y0-20,x1+25,y1+32))
    rng=np.random.default_rng(20261106)
    integral=np.pad(forest.astype(np.int32),((1,0),(1,0))).cumsum(0).cumsum(1)
    def forest_fraction(x0,y0,x1,y1):
        x0=max(0,x0);y0=max(0,y0);x1=min(w,x1);y1=min(h,y1)
        return (integral[y1,x1]-integral[y0,x1]-integral[y1,x0]+integral[y0,x0])/max(1,(x1-x0)*(y1-y0))
    trees=[]
    for y in range(25,h-5,17):
        for x in range(15,w-10,20):
            px=int(x+rng.integers(-5,6));py=int(y+rng.integers(-4,5))
            height=float(rng.uniform(52,76)); source_h=height/scale
            if not forest[max(0,int(py-source_h*.55)),px]: continue
            if forest_fraction(int(px-source_h*.28),int(py-source_h),int(px+source_h*.28)+1,py+1)<.25: continue
            if water_band[py,px] or mountain[py,px]: continue
            position=[round(px*scale,3),round(py*scale,3)]
            if any(a<=position[0]<=c and bb<=position[1]<=dd for a,bb,c,dd in avoid): continue
            asset=int(rng.choice([0,1,2,3],p=[.53,.16,.12,.19]))
            region=trees_catalog[str(asset)]['region'];width=height*region[2]/region[3]
            # Exclude the whole projected tree, not only its trunk point.
            tx0=max(0,int(px-width/scale*.5)-3);tx1=min(w,int(px+width/scale*.5)+4)
            ty0=max(0,int(py-source_h)-3);ty1=min(h,py+4)
            if mountain[ty0:ty1,tx0:tx1].any(): continue
            trees.append({'id':f'tree_{len(trees):05d}','kind':'tree','asset':str(asset),'position':position,'height':round(height,3),'flip':bool(rng.integers(0,2)),
                'bounds':[round(position[0]-width*.5,3),round(position[1]-height,3),round(width,3),round(height+2,3)]})
    result={'version':1,'visual_only':True,'world_size':WORLD,'source_size':[w,h],'source_scale':scale,
        'grid_size':512.0,'source_files':hashes,'feature_data':'res://textures/world/bai_chuan/layered_20261106/feature_data.png',
        'ground_tone_data':'res://textures/world/bai_chuan/layered_20261106/ground_tone_data.png',
        'ground_texture':'res://textures/world/bai_chuan/layered_20261106/source/ground_native.png',
        'tree_atlas':'res://textures/world/bai_chuan/layered_20261106/source/trees_native.png','tree_catalog':trees_catalog,
        'ground_texture_average':np.asarray(Image.open(BASE/'source/ground_native.png').convert('RGB')).mean((0,1)).tolist(),
        'mountain_sources':mountain_sources,'mountain_catalog':mountain_catalog,
        'mountains':mountains,'trees':trees,'limits':'Original source files remain unchanged. Complete native mountain silhouettes also supply the reviewed walking blockers.'}
    write(OUT,result)
    report={'source_files_unchanged':all(sha(ROOT/p)==value for p,value in hashes.items()),'mountains':len(mountains),'trees':len(trees),
        'forest_pixels':int(forest.sum()),'mountain_pixels':int(mountain.sum()),'water_pixels':int(water.sum()),
        'tree_world_height':[52,76],'tree_native_atlas':list(atlas.size),'ground_native':list(Image.open(BASE/'source/ground_native.png').size),
        'largest_mountain_source_rect':max((m['source_rect'] for m in mountains),key=lambda r:r[2]*r[3]),'atlas_sha256':sha(atlas_path),'data_sha256':sha(OUT)}
    write(REVIEW/'decomposition_report.json',report)
    # Diagnostic masks are not replacement map paintings.
    preview=np.zeros((h,w,3),dtype=np.uint8);preview[:]=[208,190,125]
    preview[water]=[70,170,218];preview[forest]=[55,130,60];preview[mountain]=[105,102,120]
    Image.fromarray(preview).resize((1500,1500)).save(REVIEW/'feature_review.png')
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__': main()
