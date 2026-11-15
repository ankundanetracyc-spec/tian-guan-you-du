"""Assemble AI-recolored regions and calibrate the palette to user reference."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
import cv2
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/acceptance_20261030'
DOC=ROOT/'docs/美术规范/百川地图与节点验收_20261030'
N=1254;S=N*3
ref=np.array(Image.open(ART/'palette_reference.png').convert('RGB'))
def water(a):
    t=a.astype(np.int16)
    return (t[:,:,2]-t[:,:,0]>55)&(t[:,:,1]-t[:,:,0]>35)
def calibrate(image,target):
    """Global palette calibration only: no spatial deformation or new objects."""
    lab=cv2.cvtColor(image,cv2.COLOR_RGB2LAB).astype(np.float32)
    dst=cv2.cvtColor(target,cv2.COLOR_RGB2LAB).astype(np.float32)
    a_water,b_water=water(image),water(target)
    for cls in [False,True]:
        src_mask=a_water==cls;tgt_mask=b_water==cls
        source=lab[src_mask];sample=dst[tgt_mask]
        q=np.linspace(0,100,101)
        xs=np.percentile(source[:,0],q);ys=np.percentile(sample[:,0],q)
        unique,index=np.unique(xs,return_index=True)
        mapped=np.interp(source[:,0],unique,ys[index])
        source[:,0]=mapped
        for ch in [1,2]:
            ratio=np.clip(sample[:,ch].std()/max(source[:,ch].std(),1),.7,1.3)
            source[:,ch]=(source[:,ch]-source[:,ch].mean())*ratio+sample[:,ch].mean()
        lab[src_mask]=source
    return cv2.cvtColor(np.rint(lab).clip(0,255).astype('uint8'),cv2.COLOR_LAB2RGB)
raw=np.array(Image.open(ART/'raw_color_join.png').convert('RGB'))
corrected=calibrate(raw,ref)
old=Image.open(ROOT/'textures/world/bai_chuan/ink_depth_20261030/bai_chuan_pale_ground_depth_v8.png').convert('RGB').resize((S,S),Image.Resampling.LANCZOS)
guide=calibrate(np.array(old),ref)
canvas=np.empty_like(corrected);manifest=[]
(ART/'tiles').mkdir(exist_ok=True)
for row in range(3):
    for col in range(3):
        yy,xx=np.mgrid[:N,:N];dist=np.full((N,N),N,dtype=np.float32)
        if row:dist=np.minimum(dist,yy)
        if row<2:dist=np.minimum(dist,N-1-yy)
        if col:dist=np.minimum(dist,xx)
        if col<2:dist=np.minimum(dist,N-1-xx)
        t=np.clip((dist-6)/42,0,1);w=t*t*(3-2*t)
        y,x=row*N,col*N;a=corrected[y:y+N,x:x+N];b=guide[y:y+N,x:x+N]
        tile=np.rint(a*w[...,None]+b*(1-w[...,None])).clip(0,255).astype('uint8')
        canvas[y:y+N,x:x+N]=tile
        ident=f'bc_{row+1}{col+1}';Image.fromarray(tile).save(ART/'tiles'/f'{ident}.png')
        manifest.append({'id':ident,'native_size':[N,N],'file':f'tiles/{ident}.png','shared_reference_edge':6,'transition':42})
Image.fromarray(canvas).save(ART/'master_3762.png')
Image.fromarray(canvas).resize((1536,1536),Image.Resampling.LANCZOS).save(DOC/'底图整幅预览.jpg',quality=95)
comps=[]
m=water(canvas).astype('uint8');num,labels,stats,cents=cv2.connectedComponentsWithStats(m,8)
for x,y,w,h,a in stats[1:]:
    if a>150:comps.append({'bounds':list(map(int,[x,y,w,h])),'area':int(a),'left':bool(x<=2)})
report={'grid':[3,3],'tiles':manifest,'world_size':[S,S],'native_2048':False,
 'palette_method':'Built-in AI recolor of all nine regions, then global Lab land/water luminance quantile and chroma calibration to user reference; no spatial deformation.',
 'reference_edge':'6px shared original geometric reference with42px transition; reference limited to original1254 whole-map detail.',
 'water_components':comps,'all_significant_components_left':all(c['left'] for c in comps)}
(DOC/'底图尺寸与水域检查.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'tiles':9,'size':S,'components':comps},ensure_ascii=False))
