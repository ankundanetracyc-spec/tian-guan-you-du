"""Stitch integrated whole-scene refinements, then crop nine delivery textures.

No mountain, forest, water or bridge sprites are composed by this script.
Preview mode cannot write production tiles or mark incomplete art complete.
"""
import argparse,copy,hashlib,json,math,shutil
from pathlib import Path
import cv2
import numpy as np
from PIL import Image,ImageFont

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/whole_paint_v2'
DOC=ROOT/'docs/美术规范/百川整幅山水底图重绘_20261028'
OUT=ROOT/'output/bai_chuan_whole_v2'
OUT.mkdir(parents=True,exist_ok=True)
S=6144

def intersects(a,b,gap=0):
    return a[0]<b[0]+b[2]+gap and a[0]+a[2]+gap>b[0] and a[1]<b[1]+b[3]+gap and a[1]+a[3]+gap>b[1]

def stitch(preview=False):
    plan=json.loads((ART/'refinement_plan.json').read_text(encoding='utf-8'))
    missing=[p['id'] for p in plan['patches'] if not ((ART/'refined'/f"{p['id']}.png").exists() and (ART/'refined'/f"{p['id']}_generation.json").exists())]
    if missing and not preview: raise RuntimeError('Incomplete refinement regions: '+','.join(missing))
    accum=np.zeros((S,S,3),np.float32);total=np.zeros((S,S),np.float32)
    manifest=[]
    for p in plan['patches']:
        source=ART/'refined'/f"{p['id']}.png"
        reference=np.array(Image.open(p['reference']).convert('RGB'))
        is_native=p['id'] not in missing
        if is_native:
            record=json.loads(source.with_name(source.stem+'_generation.json').read_text(encoding='utf-8-sig'))
            assert record['sourceMaster']==plan['sourceMaster'],p['id']
            native=Image.open(source).convert('RGB')
            assert native.width>=1254 and native.height>=1254 and native.width==native.height,(p['id'],native.size)
            source_size=list(native.size)
            if native.size!=(1254,1254):native=native.resize((1254,1254),Image.Resampling.LANCZOS)
            pixels=np.array(native)
            # Registration matches low-frequency composition, not generated detail.
            flow_engine=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_FAST)
            ref_gray=cv2.GaussianBlur(cv2.cvtColor(reference,cv2.COLOR_RGB2GRAY),(0,0),4)
            art_gray=cv2.GaussianBlur(cv2.cvtColor(pixels,cv2.COLOR_RGB2GRAY),(0,0),4)
            flow=flow_engine.calc(ref_gray,art_gray,None)
            flow=cv2.GaussianBlur(flow,(0,0),12)
            mag=np.linalg.norm(flow,axis=2)
            flow*=np.minimum(1,32/np.maximum(mag,1e-6))[:,:,None]
            # Retain new natural clearings supplied for separate building sprites.
            yy,xx=np.mgrid[:1254,:1254].astype(np.float32)
            protect=np.ones((1254,1254),np.float32)
            for q in p['places']:
                protect*=1-.9*np.exp(-((xx-q['x'])**2+(yy-q['y'])**2)/(2*85**2))
            flow*=protect[:,:,None]
            pixels=cv2.remap(pixels,xx+flow[:,:,0],yy+flow[:,:,1],cv2.INTER_CUBIC,borderMode=cv2.BORDER_REFLECT_101)
            # Common broad paper/wash tones avoid patch exposure differences;
            # newly generated ink, water texture and paper detail stay native.
            target_low=cv2.GaussianBlur(reference.astype(np.float32),(0,0),48)
            actual_low=cv2.GaussianBlur(pixels.astype(np.float32),(0,0),48)
            pixels=np.clip(pixels.astype(np.float32)+.8*(target_low-actual_low),0,255)
        else:
            pixels=reference.astype(np.float32);source_size=None
        h,w=pixels.shape[:2]
        ox,oy=p['worldOrigin'];x0=max(0,ox);y0=max(0,oy);x1=min(S,ox+w);y1=min(S,oy+h)
        px0,py0=x0-ox,y0-oy;px1,py1=x1-ox,y1-oy
        wx=np.minimum(1,np.minimum(np.arange(w)+1,w-np.arange(w))/25).astype(np.float32)
        wy=np.minimum(1,np.minimum(np.arange(h)+1,h-np.arange(h))/25).astype(np.float32)
        weight=wy[py0:py1,None]*wx[None,px0:px1]
        accum[y0:y1,x0:x1]+=pixels[py0:py1,px0:px1]*weight[:,:,None]
        total[y0:y1,x0:x1]+=weight
        manifest.append({'id':p['id'],'nativeRefinement':is_native,'sourcePixels':source_size,'framePixels':[1254,1254],'coreRect':p['coreRect'],'source':str(source) if is_native else 'REFERENCE_ONLY'})
    assert total.min()>0
    image=Image.fromarray(np.rint(np.clip(accum/total[:,:,None],0,255)).astype(np.uint8))
    del accum,total
    if preview:
        image.save(OUT/'composition_preview_NOT_FINAL.png')
        image.resize((1536,1536),Image.Resampling.LANCZOS).save(OUT/'preview_small.jpg',quality=94)
        (OUT/'preview_status.json').write_text(json.dumps({'completeNativeRegions':25-len(missing),'missing':missing,'productionReady':False},ensure_ascii=False,indent=2),encoding='utf-8')
        return image,manifest
    image.save(ART/'master_6144.png')
    image.save(DOC/'百川整幅底图_6144.png')
    for row in range(3):
        for col in range(3):
            image.crop((col*2048,row*2048,(col+1)*2048,(row+1)*2048)).save(ART/'tiles'/f'bc_{row+1}{col+1}.png')
    image.resize((1024,1024),Image.Resampling.LANCZOS).save(ART/'overview.png')
    (DOC/'整幅细化像素清单.json').write_text(json.dumps({'method':'single_composition_integrated_scene_refinement','worldPixels':[S,S],'sourceCompositionPixels':plan['sourceCompositionPixels'],'sourceMaster':plan['sourceMaster'],'nativeRegions':manifest,'productionTiles':[2048,2048,9],'standaloneTerrainSprites':0},ensure_ascii=False,indent=2),encoding='utf-8')
    return image,manifest

def prepare_presentation(image):
    source=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
    data=json.loads(source.read_text(encoding='utf-8'))
    old=json.loads((ROOT/'textures/world/bai_chuan/acceptance_v1/presentation.json').read_text(encoding='utf-8'))
    nodes={n['id']:n for n in data['nodes']}
    rows=copy.deepcopy(old['placements'])
    entrance_path=ROOT/'textures/world/bai_chuan/entrance_v1/nodes.json'
    entrances=json.loads(entrance_path.read_text(encoding='utf-8')) if entrance_path.exists() and ART.name=='whole_paint_v3' else {'catalog':{},'placements':[]}
    entrance_by_id={p['id']:p for p in entrances['placements']}
    catalog={}
    (ART/'icons').mkdir(exist_ok=True)
    for p in rows:
        if p['type']=='wild':continue
        if p['id'] in entrance_by_id:
            p['asset']=entrance_by_id[p['id']]['asset']
        key=p['asset']
        if key not in catalog:
            item=copy.deepcopy(entrances['catalog'].get(key,old['catalog'].get(key)))
            old_file=ROOT/item['file'].removeprefix('res://')
            new_file=ART/'icons'/old_file.name
            shutil.copy2(old_file,new_file)
            item['file']='res://'+str(new_file.relative_to(ROOT)).replace('\\','/')
            catalog[key]=item
    rgb=np.array(image).astype(np.int16)
    wet=((rgb[:,:,2]-rgb[:,:,0]>-16)&(rgb[:,:,1]-rgb[:,:,0]>1)&(rgb.mean(axis=2)>100)).astype(np.uint8)
    dark=(rgb.mean(axis=2)<95).astype(np.uint8)
    wet_sum=cv2.integral(wet);dark_sum=cv2.integral(dark)
    def fraction(integral,box):
        x,y,w,h=map(round,box);x0=max(0,x);y0=max(0,y);x1=min(S,x+w);y1=min(S,y+h)
        return 1 if x1<=x0 or y1<=y0 else float(integral[y1,x1]-integral[y0,x1]-integral[y1,x0]+integral[y0,x0])/((x1-x0)*(y1-y0))
    priority={'sect':0,'city':1,'town':2,'market':3,'village':4,'dock':5,'wild':6}
    occupied=[];problems=[]
    for p in sorted(rows,key=lambda p:(priority[p['type']],p['id'])):
        n=nodes[p['id']]
        ax,ay=np.array(n['pos'])*S/100
        p['anchor']=[float(ax),float(ay)]
        p.pop('shelf',None);p.pop('approach',None)
        if p['type']=='wild':
            p.update(drawIcon=False,artOffset=[0,0],rect=[round(ax-6),round(ay-6),12,12],footing='integrated_natural_landmark')
            occupied.append(p['rect']);continue
        p['drawIcon']=True
        width,height=catalog[p['asset']]['size']
        pivot=p['pivot']
        base=[round(ax-width*pivot[0]),round(ay-height*pivot[1]),width,height]
        choices=[]
        for dx in range(-120,121,24):
            for dy in range(-120,121,24):
                box=[base[0]+dx,base[1]+dy,width,height]
                collisions=sum(intersects(box,q,7) for q in occupied)
                water=fraction(wet_sum,box)
                trees=fraction(dark_sum,box)
                if p['type']=='dock':
                    water=fraction(wet_sum,[box[0],box[1],width*.42,height])
                score=collisions*1e7+water*1e6+max(0,trees-.12)*4000+math.hypot(dx,dy)
                choices.append((score,dx,dy,box,water))
        _,dx,dy,box,water=min(choices,key=lambda q:q[0])
        p['rect']=box;p['artOffset']=[dx,dy]
        p['footing']='mountain_terrace' if p['type']=='sect' and n.get('level')==1 else 'mountain_valley' if p['type']=='sect' else 'shore' if p['type']=='dock' else 'open_ground'
        if (dx or dy) and p['type']!='sect':
            front=[float(ax+dx),float(ay+dy)]
            p['approach']=[[float(ax),float(ay)],[(float(ax)+front[0])/2,(float(ay)+front[1])/2],front]
        if water>.04:problems.append({'name':p['name'],'waterFraction':round(water,4)})
        occupied.append(box)
    fontfile=ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'
    label_boxes=[]
    for p in sorted(rows,key=lambda p:(priority[p['type']],p['id'])):
        x,y,w,h=p['rect'];ax,ay=np.array(p['anchor'])+np.array(p['artOffset'])
        font=ImageFont.truetype(str(fontfile),p['labelSize'])
        tw=math.ceil(font.getlength(p['name']))+10;th=p['labelSize']+10
        options=[[round(ax-tw/2),round(ay+9),tw,th],[x+w+7,y+h-th,tw,th],[x-tw-7,y+h-th,tw,th],[round(x+w/2-tw/2),y-th-5,tw,th],[round(ax-tw/2),round(ay+26),tw,th]]
        def score(box):
            return sum(intersects(box,q['rect'],2) for q in rows if q['id']!=p['id'])*100+sum(intersects(box,q,3) for q in label_boxes)*1000
        box=min(options,key=score);p['labelRect']=box;p['labelConflicts']=score(box)//100
        label_boxes.append(box)
    rows.sort(key=lambda p:p['id'])
    audit={'nodes':len(rows),'buildingSprites':sum(p['drawIcon'] for p in rows),'naturalLandmarkMarkers':sum(not p['drawIcon'] for p in rows),'mountainSects':[p['name'] for p in rows if p['type']=='sect'],'wetBuildings':problems,'labelConflicts':[p['name'] for p in rows if p['labelConflicts']],'iconOverlaps':[[p['name'],q['name']] for i,p in enumerate(rows) for q in rows[i+1:] if intersects(p['rect'],q['rect'])],'sourceUnchanged':hashlib.sha256(source.read_bytes()).hexdigest()==old['sourceHash']}
    presentation={'worldSize':S,'tileSize':2048,'catalog':catalog,'placements':rows,'terrain':[],'forests':[],'waters':[],'bridges':[],'backgroundMethod':'single_whole_painting_with_integrated_refinement','sourceHash':old['sourceHash'],'sourceGraphCounts':old['sourceGraphCounts'],'originalBridgeIds':old['originalBridgeIds']}
    (ART/'presentation.json').write_text(json.dumps(presentation,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    (DOC/'节点落位清单.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    (DOC/'落位检查.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
    return audit

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--preview',action='store_true')
    parser.add_argument('--art-dir',default='textures/world/bai_chuan/whole_paint_v2')
    parser.add_argument('--doc-dir',default='docs/美术规范/百川整幅山水底图重绘_20261028')
    args=parser.parse_args()
    ART=ROOT/args.art_dir
    DOC=ROOT/args.doc_dir
    DOC.mkdir(parents=True,exist_ok=True)
    image,manifest=stitch(args.preview)
    report={'preview':args.preview,'nativeRegions':sum(p['nativeRefinement'] for p in manifest),'totalRegions':len(manifest)}
    if not args.preview:report['placement']=prepare_presentation(image)
    print(json.dumps(report,ensure_ascii=False))
