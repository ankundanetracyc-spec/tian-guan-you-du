"""Build review-only node placements; never edit logical world coordinates."""
from pathlib import Path
import json, math, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import cv2

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/acceptance_20261030'
DOC=ROOT/'docs/美术规范/百川地图与节点验收_20261030'
DOC.mkdir(parents=True,exist_ok=True)
S=3762
source=ART/'master_3762.png'
if not source.exists():
    shutil.copy2(ROOT/'textures/world/bai_chuan/pale_nine_20261030/bai_chuan_nine_master_3762.png',source)
im=Image.open(source).convert('RGB'); rgb=np.array(im).astype(np.int16)
r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
water=((b-r>55)&(g-r>35)).astype('uint8')
water=cv2.dilate(water,np.ones((5,5),np.uint8))
# Use fixed pale geometry to classify mountains/woods: a palette change must
# not itself erase the mountain mask or redefine placement semantics.
geom=np.array(Image.open(ROOT/'textures/world/bai_chuan/pale_nine_20261030/bai_chuan_nine_master_3762.png').convert('RGB')).astype(np.int16)
gr,gg,gb=geom[:,:,0],geom[:,:,1],geom[:,:,2]
forest=((gg>gr*1.012)&(gr>gb+4)&(geom.max(2)<195)).astype('uint8')
mount=((gb>gr+4)&(gb-gg<27)&(geom.max(2)<220)&(water==0)).astype('uint8')
integrals=[cv2.integral(m) for m in [water,forest,mount]]
def count(ii,box):
    x0,y0,x1,y1=[int(v) for v in box]
    x0=max(0,min(S,x0));x1=max(0,min(S,x1));y0=max(0,min(S,y0));y1=max(0,min(S,y1))
    return int(ii[y1,x1]-ii[y0,x1]-ii[y1,x0]+ii[y0,x0])
def overlap(a,b,gap=8):
    return a[0]<b[2]+gap and b[0]<a[2]+gap and a[1]<b[3]+gap and b[1]<a[3]+gap
data=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
catalog=json.loads((ROOT/'textures/world/bai_chuan/node_sprites_20261030/catalog.json').read_text(encoding='utf-8'))
assets={a['id']:a for a in catalog['assets']}
mapping={p['id']:p['asset'] for p in catalog['placements']}
types={'sect':'宗门','city':'城市','town':'镇','market':'坊市','village':'村落','dock':'渡口','wild':'野外'}
placements=[];occupied=[]
built=[n for n in data['nodes'] if n['type']!='wild']
built.sort(key=lambda n:(0 if n['type']=='sect' and n.get('level')==1 else 1 if n['type']=='sect' else 2,n['id']))
for n in built:
    asset=mapping[n['id']];w,h=assets[asset]['display_size'];lx,ly=[round(v*S/100) for v in n['pos']]
    choices=[]
    for dy in range(-180,181,12):
        for dx in range(-180,181,12):
            x,y=lx+dx,ly+dy;box=[x-w//2,y-h,x-w//2+w,y]
            if min(box[:2])<8 or max(box[2:])>S-8:continue
            wet=count(integrals[0],box)
            if wet or any(overlap(box,q) for q in occupied):continue
            tree=count(integrals[1],box)/(w*h)
            rocky=count(integrals[2],box)/(w*h)
            back=count(integrals[2],[x-w,y-h-100,x+w,y-h+12])/(2*w*112)
            dist=math.hypot(dx,dy)
            score=dist*.40+tree*550
            if n['type']=='sect':score+=max(0,.085-back)*1400+rocky*900
            else:score+=rocky*600
            if n['type']=='dock':
                near=count(integrals[0],[x-w-24,y-h-24,x+w+24,y+24])
                score+=0 if near>150 else 150
            choices.append((score,x,y,box,tree,back,rocky))
    if not choices:raise RuntimeError('No dry collision-free placement for '+n['name'])
    _,x,y,box,tree,back,rocky=min(choices)
    # Visual review: bring these two entrances up to the existing low slope
    # rather than leaving a large plain gap between gate and mountain.
    lift={'抱朴山':60,'沧溟水府':48}.get(n['name'],0)
    if lift:
        alternatives=[]
        for oy in [-12,0,12]:
            for ox in [-24,-12,0,12,24]:
                nx,ny=x+ox,y-lift+oy;nb=[nx-w//2,ny-h,nx-w//2+w,ny]
                if count(integrals[0],nb) or any(overlap(nb,q) for q in occupied):continue
                nt=count(integrals[1],nb)/(w*h)
                if nt>.03:continue
                alternatives.append((abs(ox)+abs(oy),nx,ny,nb,nt))
        if alternatives:
            _,x,y,box,tree=min(alternatives)
            back=count(integrals[2],[x-w,y-h-100,x+w,y-h+12])/(2*w*112)
            rocky=count(integrals[2],box)/(w*h)
    occupied.append(box)
    placements.append({'id':n['id'],'name':n['name'],'type':n['type'],'typeLabel':types[n['type']],
        'level':n.get('level'), 'logicalPos':[lx,ly],'drawPos':[x,y],'artOffset':[x-lx,y-ly],
        'asset':asset,'width':w,'height':h,'rect':box,'treeRatio':round(tree,4),
        'mountainBackdropRatio':round(back,4),'bodyMountainRatio':round(rocky,4),
        'src':'../../../textures/world/bai_chuan/node_sprites_20261030/icons/'+asset+'.png'})
for n in data['nodes']:
    if n['type']=='wild':
        pos=[round(v*S/100) for v in n['pos']]
        placements.append({'id':n['id'],'name':n['name'],'type':'wild','typeLabel':'野外','logicalPos':pos,'drawPos':pos,'width':0,'height':0,'asset':None})
assert len(placements)==123
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
labels=[]
for p in placements:
    x,y=p['drawPos'];tw=int(font.getlength(p['name']))+12;th=30
    candidates=[[x-tw//2,y+6,x+tw//2,y+6+th],[x+p['width']//2+7,y-p['height']//2-14,x+p['width']//2+7+tw,y-p['height']//2+16],[x-p['width']//2-7-tw,y-p['height']//2-14,x-p['width']//2-7,y-p['height']//2+16]]
    if p['type']=='wild':candidates=[[x-tw//2,y+7,x+tw//2,y+37]]
    def cost(box):
        bounds=10000 if min(box[:2])<0 or max(box[2:])>S else 0
        wet=count(integrals[0],box)/max(1,(box[2]-box[0])*(box[3]-box[1]))
        return bounds+wet*1800+sum(500 for q in occupied if overlap(box,q,1))+sum(700 for q in labels if overlap(box,q,2))
    box=min(candidates,key=cost);labels.append(box);p['labelRect']=box
presentation={'worldSize':S,'nodes':placements,'assets':[{'id':k,'name':v['name'],'width':v['display_size'][0],'height':v['display_size'][1],'src':'../../../textures/world/bai_chuan/node_sprites_20261030/icons/'+k+'.png'} for k,v in assets.items()],
 'background':'../../../textures/world/bai_chuan/acceptance_20261030/master_3762.png',
 'paletteReference':'../../../textures/world/bai_chuan/acceptance_20261030/palette_reference.png'}
(ART/'presentation.json').write_text(json.dumps(presentation,ensure_ascii=False,indent=2),encoding='utf-8')
(DOC/'presentation.js').write_text('window.MAP_REVIEW='+json.dumps(presentation,ensure_ascii=False)+';',encoding='utf-8')
preview=im.convert('RGBA');draw=ImageDraw.Draw(preview)
for p in placements:
    if not p['asset']:continue
    sprite=Image.open(ROOT/'textures/world/bai_chuan/node_sprites_20261030/icons'/f"{p['asset']}.png").convert('RGBA')
    preview.alpha_composite(sprite,tuple(p['rect'][:2]))
    box=p['labelRect'];draw.text((box[0]+6,box[1]+2),p['name'],font=font,fill='#373d34',stroke_width=2,stroke_fill='#eee8ce')
preview.convert('RGB').resize((1536,1536),Image.Resampling.LANCZOS).save(DOC/'完整地图与节点预览.jpg',quality=95)
sects=[p for p in placements if p['type']=='sect']
sheet=Image.new('RGB',(1500,math.ceil(len(sects)/3)*340),'#e5ddbc')
for k,p in enumerate(sects):
    x,y=p['drawPos'];sheet.paste(preview.crop((x-250,y-220,x+250,y+120)).convert('RGB'),((k%3)*500,(k//3)*340))
sheet.save(DOC/'宗门原尺寸落位检查.jpg',quality=95)
checks={'named_nodes':123,'built_nodes':53,'natural_nodes':70,'accepted_assets':9,
 'major_unique':len({p['asset'] for p in placements if p['type']=='sect' and p['level']==1})==3,
 'local_shared':len({p['asset'] for p in placements if p['type']=='sect' and p['level']==2})==1,
 'city_shared':len({p['asset'] for p in placements if p['type']=='city'})==1,
 'town_shared':len({p['asset'] for p in placements if p['type']=='town'})==1,
 'opaque_rect_water_overlaps':sum(count(integrals[0],p['rect'])>0 for p in placements if p['asset']),
 'sprite_rect_overlaps':sum(overlap(a['rect'],b['rect'],0) for i,a in enumerate(placements) if a['asset'] for b in placements[i+1:] if b['asset']),
 'max_art_offset_pixels':max(math.hypot(*p['artOffset']) for p in placements if p['asset']),
 'max_forest_ratio':max(p['treeRatio'] for p in placements if p['asset']),
 'weak_sect_backdrops':[p['name'] for p in sects if p['mountainBackdropRatio']<.02],
 'limitations':'Masks assist review; mountain footing and forest interpretation require visual inspection. Logical positions remain separate from art offsets.'}
(DOC/'落位检查.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(checks,ensure_ascii=False))
