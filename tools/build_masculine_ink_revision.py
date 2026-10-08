"""Export only the three new male brow/eye pairs in the existing portrait rig."""
import os
os.environ['OPENBLAS_NUM_THREADS'] = '1'
os.environ['OMP_NUM_THREADS'] = '1'
from pathlib import Path
import json, hashlib
import numpy as np
import cv2
cv2.setNumThreads(1)
from PIL import Image, ImageDraw, ImageFont
from build_ink_three_variants import read, save, place, paired_transform, hair_tone

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / 'mods/appearance/ink_three_variants_20261004'
SOURCE = PACK / 'source/masculine_revision_20261005'
SPECS = [
    ('stern_sword', '凝岳', (288, 727), (739, 726), 670,
     [(105,707),(170,702),(245,703),(315,710),(370,733),(411,761),(365,766),(310,772),(240,773),(178,763),(134,742)]),
    ('steady_straight', '磐石', (286, 804), (739, 804), 745,
     [(126,789),(184,783),(251,784),(320,793),(365,818),(409,845),(365,844),(303,849),(240,847),(190,834),(149,810)]),
    ('rising_edge', '凌锋', (309, 799), (715, 799), 767,
     [(156,783),(207,778),(270,779),(332,791),(376,812),(418,834),(376,835),(317,839),(266,838),(215,825),(180,803)]),
]

def export():
    records=[]
    for ordinal,(key,name,left,right,split,outline) in enumerate(SPECS,4):
        raw=read(SOURCE/f'{key}.png'); h,w=raw.shape[:2]
        # Alpha below 3 is an encoder fringe, not painted anatomy.
        raw[raw[:,:,3]<3]=0
        brows=raw.copy(); brows[:500]=0; brows[split:]=0
        brows=hair_tone(brows)
        weight=(brows[:,:,3]/255)**3; weight[brows[:,:,:3].mean(2)>160]=0
        yy,xx=np.indices((h,w));centers=[]
        for lo,hi in [(0,w//2),(w//2,w)]:
            z=weight[:,lo:hi]; centers.append(((xx[:,lo:hi]*z).sum()/z.sum(),(yy[:,lo:hi]*z).sum()/z.sum()))
        bt=paired_transform(*centers,(456,346),(579,345))
        save(PACK/'male/eyebrows'/f'{ordinal:02}.png',place(brows,bt))

        eyes=raw.copy(); eyes[:split]=0; eyes[900:]=0
        # Trace the eye organs. A 1.5 px transition retains ink lid strokes;
        # the surrounding generated complexion is not exported as a skin plate.
        mask=Image.new('L',(w,h)); d=ImageDraw.Draw(mask)
        d.polygon(outline,fill=255)
        mirror_axis=left[0]+right[0]
        d.polygon([(mirror_axis-x,y+(right[1]-left[1])) for x,y in outline],fill=255)
        core=np.array(mask)>0
        dist=cv2.distanceTransform((~core).astype(np.uint8),cv2.DIST_L2,3)
        edge=np.clip(1-dist/7,0,1)
        eyes[:,:,3]*=edge
        # Only the immediate transparent edge carries pigment, never pale skin.
        outside=~core
        base=np.array([238,218,202],np.float32)
        rgb=eyes[:,:,:3]; coverage=np.clip(np.mean((base-rgb)/[173,165,155],axis=2),0,1)
        coverage=np.minimum(eyes[:,:,3]/255,coverage)
        rgb[outside]=np.clip((rgb-(1-coverage[:,:,None])*base)/np.maximum(coverage[:,:,None],.01),0,255)[outside]
        eyes[:,:,3][outside]=coverage[outside]*255*.8
        # Warm grey sclera and small muted highlights, matching current wash.
        bright=core&(rgb.min(2)>210); rgb[bright]=rgb[bright]*.84
        et=paired_transform(left,right,(464,375),(569,375))
        save(PACK/'male/eyes'/f'{ordinal:02}.png',place(eyes,et))
        records.append({'name':name,'source':key+'.png','source_sha256':hashlib.sha256((SOURCE/f'{key}.png').read_bytes()).hexdigest(),
                        'eyes_file':f'male/eyes/{ordinal:02}.png','eyebrows_file':f'male/eyebrows/{ordinal:02}.png',
                        'eye_transform':list(et),'brow_transform':list(bt),'source_pupils':[left,right],
                        'target_pupils':[[464,375],[569,375]],'eye_outline':outline,'skin_plate_removed':True})
    (SOURCE/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    preview()

def composite(eye,brow,face=1,hair=1):
    im=Image.new('RGBA',(1024,1536))
    for part,v in [('hair_back',hair),('face_shape',face),('clothing',1),('eyes',eye),('eyebrows',brow),('nose',1),('mouth',1),('face_accessory',1),('hair_front',min(hair,4))]:
        im.alpha_composite(Image.open(PACK/'male'/part/f'{v:02}.png').convert('RGBA'))
    bg=Image.new('RGBA',im.size,'#e8e3dc'); bg.alpha_composite(im); return bg.convert('RGB')

def preview():
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    board=Image.new('RGB',(1600,1180),'#e8e3dc');d=ImageDraw.Draw(board)
    for col,(key,name,*_) in enumerate(SPECS):
        v=col+4
        for row in range(3):
            im=composite(v,v,row+1,row+1).crop((335,270,700,560)).resize((500,397))
            board.paste(im,(col*530+5,row*390+28));d.text((col*530+20,row*390+2),f'{name} · 脸型 {row+1}',font=font,fill='#352f2b')
        composite(v,v).save(PACK/'preview'/f'male_{key}_20261005.png')
    board.save(PACK/'preview/masculine_three_faces_qa_20261005.jpg',quality=95)
    comparison=Image.new('RGB',(1600,480),'#e8e3dc'); cd=ImageDraw.Draw(comparison)
    for col,(eye,brow,title) in enumerate([(1,1,'原有眉眼'),(4,4,'凝岳 · 剑眉沉目'),(5,5,'磐石 · 浓眉朗目'),(6,6,'凌锋 · 扬眉锐目')]):
        face=composite(eye,brow).crop((345,260,690,635)).resize((390,425))
        comparison.paste(face,(col*400+5,45));cd.text((col*400+12,12),title,font=font,fill='#352f2b')
    comparison.save(PACK/'preview/masculine_three_pairs_20261005.jpg',quality=95)

if __name__=='__main__':
    raise SystemExit('The pointed 2026-10-05 revision was rejected. Use build_soft_masculine_ink_revision.py for the latest revision; do not republish these historical sources.')
