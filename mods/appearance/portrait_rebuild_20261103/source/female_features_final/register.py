"""Registration only: uniform scaling and translation, preserving generated RGBA."""
from pathlib import Path
from PIL import Image
import numpy as np
import json

ROOT=Path(__file__).resolve().parent
FINAL=ROOT.parents[1]/'final'/'female'
OLD=ROOT.parents[2]/'ink_three_variants_20261004'/'female'

def bbox16(im):
    a=np.array(im.getchannel('A'))
    return Image.fromarray(np.uint8(a>16)*255).getbbox()

def darkcenter(im,threshold=125,roi=None):
    a=np.array(im).astype(float)
    w=np.maximum(threshold-a[:,:,:3].mean(2),0)*(a[:,:,3]>100)
    if roi:
        x1,y1,x2,y2=roi
        mask=np.zeros_like(w);mask[y1:y2,x1:x2]=1;w*=mask
    yy,xx=np.mgrid[:a.shape[0],:a.shape[1]]
    return np.array([(w*xx).sum()/w.sum(),(w*yy).sum()/w.sum()])

records=[]
for v in range(1,4):
    for part in ['eyebrows','eyes','nose','mouth']:
        name=f'{part}_{v:02}'+('_v2' if part=='nose' else '')
        im=Image.open(ROOT/'raw'/f'{name}.png').convert('RGBA')
        b=bbox16(im)
        if part=='eyes':
            p1=darkcenter(im,35,(450,280,750,410))
            p2=darkcenter(im,35,(1450,280,1750,410))
            scale=110/(p2[0]-p1[0]);anchor=(p1+p2)/2;target=np.array([519.,379.])
        elif part=='eyebrows':
            scale=190/(b[2]-b[0]);anchor=np.array([(b[0]+b[2])/2,(b[1]+b[3])/2]);target=np.array([519.,354.])
        elif part=='nose':
            scale=42/(b[2]-b[0]);anchor=darkcenter(im);target=np.array([519.,451.])
        else:
            scale=65/(b[2]-b[0]);anchor=darkcenter(im,95);target=np.array([519.,483.])
        size=(round(im.width*scale),round(im.height*scale))
        resized=im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
        actual=np.array([size[0]/im.width,size[1]/im.height])
        offset=np.rint(target-anchor*actual).astype(int)
        canvas=Image.new('RGBA',(1024,1536));canvas.alpha_composite(resized,tuple(offset))
        path=FINAL/part/f'{v:02}.png';path.parent.mkdir(parents=True,exist_ok=True);canvas.save(path)
        records.append({'part':part,'variant':v,'source':str(ROOT/'raw'/f'{name}.png'),'output':str(path),'uniform_scale':float(scale),'resized_pixels':size,'translation':offset.tolist(),'source_anchor':anchor.tolist(),'target_anchor':target.tolist(),'bbox':canvas.getbbox()})

(ROOT/'registration.json').write_text(json.dumps(records,indent=2),encoding='utf8')
for v in range(1,4):
    canvas=Image.new('RGBA',(1024,1536),(226,222,208,255))
    for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
        p=FINAL/part/f'{v:02}.png' if part in ['eyes','eyebrows','nose','mouth'] else OLD/part/'01.png'
        canvas.alpha_composite(Image.open(p).convert('RGBA'))
    canvas.crop((320,210,720,580)).resize((800,740),Image.Resampling.LANCZOS).convert('RGB').save(ROOT/'qa'/f'face_{v:02}.jpg')
    canvas.convert('RGB').save(ROOT/'qa'/f'portrait_{v:02}.jpg')
print('Exported',len(records),'independent RGBA parts')
