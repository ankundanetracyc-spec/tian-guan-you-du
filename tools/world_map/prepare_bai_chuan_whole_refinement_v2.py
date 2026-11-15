"""Prepare geometry-registered reference crops for integrated AI landscape detail.

The low-resolution reference and its coordinate guides are not delivery art.
Only whole-scene AI refinements supply the final high-resolution pixels.
"""
import json,math
from pathlib import Path
import cv2
import numpy as np
from PIL import Image,ImageDraw
from scipy.interpolate import RBFInterpolator

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/whole_paint_v2'
DOC=ROOT/'docs/美术规范/百川整幅山水底图重绘_20261028'
REF=ART/'reference'
REF.mkdir(exist_ok=True)
(ART/'refined').mkdir(exist_ok=True)
data=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
old=json.loads((ROOT/'textures/world/bai_chuan/acceptance_v1/presentation.json').read_text(encoding='utf-8'))
master=Image.open(ART/'source/master_composition_08.png').convert('RGB')
array=np.array(master)
h,w=array.shape[:2]
rgb=array.astype(np.int16)
water=((rgb[:,:,2]-rgb[:,:,0]>-15)&(rgb[:,:,1]-rgb[:,:,0]>1)&(rgb.mean(axis=2)>110)).astype(np.uint8)*255
water=cv2.morphologyEx(water,cv2.MORPH_CLOSE,np.ones((9,9),np.uint8))
components,labels,stats,_=cv2.connectedComponentsWithStats(water)
water=np.where(np.isin(labels,[i for i in range(1,components) if stats[i,cv2.CC_STAT_AREA]>150]),255,0).astype(np.uint8)
distance=cv2.distanceTransform(water,cv2.DIST_L2,5)
ridge=(distance>=cv2.dilate(distance,np.ones((5,5),np.uint8))-.03)&(distance>1.7)
ys,xs=np.where(ridge)
samples=np.stack([xs,ys],axis=1)
targets=[];sources=[];registration=[]
marks=[m for m in data['terrainPlanning']['crossingLandmarks'] if m['sourceType']=='water']
for m in marks:
    target=np.array(m['pos'])*np.array([w,h])/100
    dist=np.linalg.norm(samples-target,axis=1)
    source=samples[np.argmin(dist)].astype(float)
    targets.append(target); sources.append(source)
    registration.append({'id':m['id'],'target':target.tolist(),'paintedWaterCenter':source.tolist(),'referenceShiftPixels':round(float(np.linalg.norm(target-source)),2)})
# Boundary anchors retain the complete composition and natural coast.
for x,y in [(0,0),(w/2,0),(w-1,0),(0,h/2),(w-1,h/2),(0,h-1),(w/2,h-1),(w-1,h-1)]:
    targets.append([x,y]);sources.append([x,y])
targets=np.array(targets);sources=np.array(sources)
mapping=RBFInterpolator(targets,sources-targets,kernel='thin_plate_spline',smoothing=0.2)
yy,xx=np.mgrid[:h,:w]; points=np.stack([xx.ravel(),yy.ravel()],axis=1)
uv=points+mapping(points)
warped=cv2.remap(array,uv[:,0].reshape(h,w).astype(np.float32),uv[:,1].reshape(h,w).astype(np.float32),cv2.INTER_CUBIC,borderMode=cv2.BORDER_REFLECT_101)
guide=Image.fromarray(warped)
guide.save(REF/'registered_composition_guide.png')
guide.resize((6144,6144),Image.Resampling.LANCZOS).save(REF/'layout_reference_6144_NOT_FINAL.png')
canvas=guide.resize((6144,6144),Image.Resampling.LANCZOS)
expanded=Image.fromarray(np.pad(np.array(canvas),((24,24),(24,24),(0,0)),mode='reflect'))
edges=[round(i*6144/5) for i in range(6)]
patches=[]
for row in range(5):
    for col in range(5):
        x0,x1=edges[col:col+2];y0,y1=edges[row:row+2]
        ox=round((x0+x1)/2-627);oy=round((y0+y1)/2-627)
        name=f'r{row+1}c{col+1}'
        crop=expanded.crop((ox+24,oy+24,ox+1278,oy+1278))
        crop.save(REF/f'{name}_scene.png')
        marked=crop.copy();d=ImageDraw.Draw(marked)
        places=[]
        for n in data['nodes']:
            if n['type']=='wild':continue
            p=next(p for p in old['placements'] if p['id']==n['id'])
            ax,ay=np.array(n['pos'])*6144/100+np.array(p['artOffset'])
            if not (ox+15<ax<ox+1239 and oy+15<ay<oy+1239):continue
            rw,rh=p['rect'][2:]
            rx=round(ax-rw*p['pivot'][0]-ox);ry=round(ay-rh*p['pivot'][1]-oy)
            d.rectangle((rx-12,ry-10,rx+rw+12,ry+rh+8),outline='#b75280',width=2)
            places.append({'name':n['name'],'x':round(ax-ox),'y':round(ay-oy),'footing':'mountain_terrace_or_valley' if n['type']=='sect' else p['footing']})
        bridges=[]
        for m in marks:
            ax,ay=np.array(m['pos'])*6144/100
            if not (ox+8<ax<ox+1246 and oy+8<ay<oy+1246):continue
            lx,ly=round(ax-ox),round(ay-oy)
            d.ellipse((lx-12,ly-12,lx+12,ly+12),outline='#bd5e54',width=3)
            old_bridge=next((b for b in old['bridges'] if b.get('sourceId')==m['id']),None)
            angle=old_bridge['angle'] if old_bridge else 0
            dx,dy=math.cos(angle)*60,math.sin(angle)*60
            d.line((lx-dx,ly-dy,lx+dx,ly+dy),fill='#bd5e54',width=4)
            bridges.append({'id':m['id'],'x':lx,'y':ly,'angle':angle})
        marked.save(REF/f'{name}_placement_guide.png')
        patches.append({'id':name,'row':row,'col':col,'worldOrigin':[ox,oy],'coreRect':[x0,y0,x1-x0,y1-y0],'reference':str(REF/f'{name}_scene.png'),'placementGuide':str(REF/f'{name}_placement_guide.png'),'places':places,'bridges':bridges})
(ART/'refinement_plan.json').write_text(json.dumps({'worldSize':6144,'grid':[5,5],'patchFrame':1254,'sourceMaster':'master_composition_08.png','palette':'warm_ochre_gray_olive','terrainDensity':'open_cartographic_map','trees':'small_conifers_on_flat_ground_only','patches':patches,'registration':registration,'sourceCompositionPixels':[w,h],'deliveryTiles':[2048,2048,9]},ensure_ascii=False,indent=2),encoding='utf-8')
(DOC/'母图坐标校准.json').write_text(json.dumps(registration,ensure_ascii=False,indent=2),encoding='utf-8')
# Contact sheet is a review aid for the reference crops, not an art atlas.
contact=Image.new('RGB',(1250,1250))
for p in patches:
    im=Image.open(p['reference']).resize((250,250),Image.Resampling.LANCZOS)
    contact.paste(im,(p['col']*250,p['row']*250))
contact.save(DOC/'整体分区细化引导.jpg',quality=94)
print(json.dumps({'composition':master.size,'refinementRegions':len(patches),'framePixels':1254,'worldPixels':6144,'largestReferenceRegistrationShift':max(q['referenceShiftPixels'] for q in registration)},ensure_ascii=False))
