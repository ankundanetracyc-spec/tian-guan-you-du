"""Register independently painted variants in the project's own portrait frame.

Source PNGs remain untouched. Only matting, color calibration and one uniform
scale/translation per complete part are applied; no face or hair warping.
"""
from pathlib import Path
import json, hashlib
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont

P=Path(__file__).resolve().parents[1]
R=P/'mods/appearance/ink_three_variants_20261004'
B=P/'mods/appearance/new_ink_hair_20261003_v1'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
SLOTS=['hair_front','hair_back','eyebrows','eyes','nose','mouth','face_shape','clothing','face_accessory']
NAMES=['前发','后发','眉毛','眼睛','鼻子','嘴巴','脸型','衣服','面饰']
SIZE=(1024,1536)
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
TRANS={
 'male_hair_front_2':(.44,292,86),'male_hair_front_3':(.65,184,102),
 'male_hair_back_2':(.85,77,0),'male_hair_back_3':(.8,107,82),
 'female_hair_front_2':(.5,260,75),'female_hair_front_3':(.62,198,99),
 'female_hair_back_2':(.85,84,10),'female_hair_back_3':(.85,84,98),
 'male_face_shape_2':(.667,176,59),'male_face_shape_3':(.72,148,51),
 'female_face_shape_2':(.97,16,5),'female_face_shape_3':(1,0,0),
}
# Traced in a 2048-wide viewing frame, converted to native pixels below.
EYES={
 'male_eyes_2':{'pupil':(677,370),'core':[(479,350),(576,337),(678,339),(765,364),(817,400),(855,440),(718,443),(601,436),(536,410)],'keep':[(330,312),(493,287),(611,296),(755,325),(817,366),(880,422),(900,487),(700,508),(475,488),(371,376)]},
 'male_eyes_3':{'pupil':(625,320),'core':[(417,294),(508,282),(623,291),(729,321),(815,365),(864,397),(701,397),(579,390),(490,363)],'keep':None},
 'female_eyes_2':{'pupil':(707,373),'core':[(533,357),(620,337),(716,340),(795,376),(856,422),(883,438),(752,442),(650,433),(580,410)],'keep':[(418,302),(505,271),(606,265),(736,293),(825,340),(886,407),(921,465),(832,498),(660,513),(503,475),(441,380)]},
 'female_eyes_3':{'pupil':(605,346),'core':[(390,328),(493,307),(606,309),(717,340),(786,391),(851,435),(722,435),(595,428),(505,405),(445,374)],'keep':None},
}

def read(p):
 with Image.open(p) as im:return np.array(im.convert('RGBA'),dtype=np.float32)

def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True)
 a=np.clip(np.rint(a),0,255).astype(np.uint8);a[a[:,:,3]<3]=0
 Image.fromarray(a).save(p)

def pigment(a,core=None,strength=1.):
 rgb=a[:,:,:3];old=a[:,:,3]/255
 base=np.array([238,218,202],np.float32)
 coverage=np.clip(np.mean((base-rgb)/np.array([173,165,155]),axis=2),0,1)
 coverage=np.minimum(old,coverage)
 fg=(rgb-(1-coverage[:,:,None])*base)/np.maximum(coverage[:,:,None],.01)
 mask=np.ones(old.shape,bool) if core is None else ~core
 rgb[mask]=np.clip(fg[mask],0,255);a[:,:,3][mask]=coverage[mask]*255*strength
 return a

def hair_tone(a,rear=False):
 rgb=a[:,:,:3];sel=a[:,:,3]>230
 q=np.percentile(rgb[sel],[20,50,80],axis=0)
 target=np.array([[45,40,36],[60,55,51],[79,74,70]] if rear else [[53,48,44],[72,67,63],[95,90,86]])
 # One gentle channel curve preserves the original individual fiber strokes.
 for c in range(3):
  rgb[:,:,c]=np.interp(rgb[:,:,c],[0,*q[:,c],255],[0,*target[:,c],205])
 return a

def place(a,t):
 s,tx,ty=t;h,w=a.shape[:2];alpha=a[:,:,3]/255
 sz=(round(w*s),round(h*s));aa=cv2.resize(alpha,sz,interpolation=cv2.INTER_AREA)
 cc=cv2.resize(a[:,:,:3]*alpha[:,:,None],sz,interpolation=cv2.INTER_AREA)
 small=np.dstack((cc/np.maximum(aa[:,:,None],.0001),aa*255))
 out=np.zeros((1536,1024,4),np.float32);tx,ty=round(tx),round(ty)
 x0=max(0,tx);y0=max(0,ty);x1=min(1024,tx+sz[0]);y1=min(1536,ty+sz[1])
 out[y0:y1,x0:x1]=small[y0-ty:y1-ty,x0-tx:x1-tx]
 return out

def paired_transform(left,right,target_left,target_right):
 s=(target_right[0]-target_left[0])/(right[0]-left[0])
 return (s,target_left[0]-left[0]*s,(target_left[1]+target_right[1])/2-(left[1]+right[1])/2*s)

def prepare(j):
 key=j['id'];sex=j['sex'];slot=j['slot'];v=j['variant'];a=read(R/'source'/f'{key}.png');h,w=a.shape[:2]
 t=TRANS.get(key,(1,0,0));notes=[]
 if slot.startswith('hair_'):
  a=hair_tone(a,slot=='hair_back')
 elif slot=='eyes':
  spec=EYES[key];ratio=w/2048
  core=Image.new('L',(w,h));d=ImageDraw.Draw(core)
  pts=[(x*ratio,y*ratio) for x,y in spec['core']]
  d.polygon(pts,fill=255);d.polygon([(w-x,y) for x,y in pts],fill=255)
  mask=np.array(core)>0
  if spec['keep']:
   keep=Image.new('L',(w,h));d=ImageDraw.Draw(keep);pts=[(x*ratio,y*ratio) for x,y in spec['keep']]
   d.polygon(pts,fill=255);d.polygon([(w-x,y) for x,y in pts],fill=255)
   a[:,:,3]*=np.array(keep)/255
  a=pigment(a,mask,.7)
  px,py=np.array(spec['pupil'])*ratio
  t=paired_transform((px,py),(w-px,py),(464,375 if sex=='male' else 379),(569 if sex=='male' else 574,375 if sex=='male' else 379))
  # Preserve small matching iris lights, reduce blown-white pixels.
  yy,xx=np.indices((h,w));iris=(((xx-px)/75)**2+((yy-py)/80)**2<1)|(((xx-(w-px))/75)**2+((yy-py)/80)**2<1)
  bright=iris&(a[:,:,:3].min(2)>190);a[:,:,:3][bright]=[191,179,164]
  notes=['paired pupil alignment','eyebrows removed from eye artwork','outer skin unmixed into transparent pigment']
 elif slot=='eyebrows':
  a=hair_tone(a)
  # The two dense brow centers locate the complete pair, retaining its arch.
  weight=(a[:,:,3]/255)**3;weight[a[:,:,:3].mean(2)>160]=0
  yy,xx=np.indices((h,w));centers=[]
  for lo,hi in [(0,w//2),(w//2,w)]:
   z=weight[:,lo:hi];centers.append(((xx[:,lo:hi]*z).sum()/z.sum(),(yy[:,lo:hi]*z).sum()/z.sum()))
  t=paired_transform(*centers,(456,346) if sex=='male' else (457,354),(579,345) if sex=='male' else (582,353))
 elif slot=='nose':
  # Nostrils are the anatomical reference; bridge fades naturally on the face.
  source_anchor={'male_nose_2':(768,710),'male_nose_3':(689,782),'female_nose_2':(689,752),'female_nose_3':(689,747)}[key]
  s={'male_nose_2':.185,'male_nose_3':.18,'female_nose_2':.18,'female_nose_3':.175}[key]
  t=(s,(517 if sex=='male' else 519)-source_anchor[0]*s,(451 if sex=='male' else 451)-source_anchor[1]*s)
  source_alpha=a[:,:,3].copy()
  a=pigment(a,None,.9)
  yy,xx=np.indices((h,w));cx,cy=source_anchor
  # Keep nostrils and their local wash; the long generated bridge must not
  # create a second shaded skin plate between the independently drawn eyes.
  tip=np.exp(-(((xx-cx)/135)**2+((yy-cy)/105)**2)/2)
  below=np.clip((yy-(cy-240))/210,0,1)
  distance=cv2.distanceTransform((source_alpha>90).astype(np.uint8),cv2.DIST_L2,3)
  a[:,:,3]*=(.10+.78*tip)*np.clip(distance/32,0,1)*below
 elif slot=='mouth':
  anchors={'male_mouth_2':(887,437,.1),'male_mouth_3':(887,455,.092),'female_mouth_2':(887,430,.087),'female_mouth_3':(768,510,.105)}
  x,y,s=anchors[key];a=pigment(a,None,1)
  t=(s,(517 if sex=='male' else 519)-x*s,(481 if sex=='male' else 483)-y*s)
 elif slot=='face_accessory':
  if sex=='male':
   a=hair_tone(a)
   x,y,s=(887,380,.105) if v==2 else (701,465,.108)
   t=(s,517-x*s,465-y*s)
  else:
   a=pigment(a,None,.42)
   left=(420,555) if v==2 else (490,460);right=(w-left[0],left[1])
   t=paired_transform(left,right,(455,428),(583,428))
 out=place(a,t)
 if slot=='face_shape':
  rgb=out[:,:,:3];sample=rgb[285:330,480:555];med=np.median(sample.reshape(-1,3),axis=0)
  delta=np.array([233,209,192])-med
  weight=np.clip((rgb.mean(2)-100)/110,0,1);rgb+=weight[:,:,None]*delta
  notes=['forehead tone matched to existing matte warm skin','paper texture retained']
 return out,{'scale':t[0],'translation':[t[1],t[2]],'notes':notes}

def compose(sex,variants,hidden=''):
 out=Image.new('RGBA',SIZE)
 for slot in ORDER:
  if slot==hidden:continue
  with Image.open(R/sex/slot/f'{variants[SLOTS.index(slot)]:02}.png') as im:out.alpha_composite(im.convert('RGBA'))
 bg=Image.new('RGBA',SIZE,'#e8e3dc');bg.alpha_composite(out);return bg.convert('RGB')

def previews():
 p=R/'preview';p.mkdir(exist_ok=True)
 board=Image.new('RGB',(1536,1616),'#e8e3dc');faces=Image.new('RGB',(1536,980),'#e8e3dc')
 for row,sex in enumerate(['male','female']):
  for v in [1,2,3]:
   im=compose(sex,[v]*9);im.save(p/f'{sex}_set_{v}.png');x=(v-1)*512
   board.paste(im.resize((512,768)),(x,row*808+40));ImageDraw.Draw(board).text((x+15,row*808+8),f'{"男" if sex=="male" else "女"} · 第{v}款',font=FONT,fill='#352f2b')
   faces.paste(im.crop((340,240,700,565)).resize((504,455)),(x,row*490+32));ImageDraw.Draw(faces).text((x+15,row*490+5),f'{sex} {v}',font=FONT,fill='#352f2b')
  mixed=Image.new('RGB',(1536,808),'#e8e3dc')
  for k in range(3):
   vs=[1+(i+k)%3 for i in range(9)];im=compose(sex,vs);mixed.paste(im.resize((512,768)),(k*512,40));ImageDraw.Draw(mixed).text((k*512+12,8),str(vs),font=FONT,fill='#352f2b')
  mixed.save(p/f'{sex}_mixed.png')
  # Inspect one varying part against the base assembly, not only matched sets.
  single=Image.new('RGB',(1800,9*280),'#e8e3dc')
  for i,slot in enumerate(SLOTS):
   for v in range(1,4):
    vs=[1]*9;vs[i]=v;im=compose(sex,vs)
    crop=(300,100,735,800) if slot.startswith('hair') else (0,450,1024,1536) if slot=='clothing' else (355,300,675,580)
    im=im.crop(crop);im.thumbnail((550,248));single.paste(im,(v*600-590,i*280+30));ImageDraw.Draw(single).text((v*600-590,i*280+2),f'{NAMES[i]} {v}',font=FONT,fill='#352f2b')
  single.save(p/f'{sex}_slot_comparison.jpg',quality=93)
 board.save(p/'six_sets.jpg',quality=95);faces.save(p/'six_faces.png')

def main():
 jobs=json.loads((R/'source/generation_jobs.json').read_text(encoding='utf-8'))
 records=[]
 for sex in ['male','female']:
  for slot in SLOTS:
   a=read(B/sex/f'{slot}.png')
   if slot.startswith('hair'):a=hair_tone(a,slot=='hair_back')
   save(R/sex/slot/'01.png',a)
 for j in jobs:
  a,t=prepare(j);dst=R/j['sex']/j['slot']/f"{j['variant']:02}.png";save(dst,a)
  t.update(id=j['id'],file=str(dst.relative_to(R)),source_sha256=hashlib.sha256((R/'source'/f"{j['id']}.png").read_bytes()).hexdigest());records.append(t)
 manifest={'id':R.name,'canvas':SIZE,'sexes':['male','female'],'slots':SLOTS,'variants_per_slot':3,'total_pngs':54,'empty_accessory_variant':1,'blend_order':ORDER,'runtime_offset':[0,0],'rig':'../new_ink_hair_20261003_v1/rig/portrait_rig.json','visual_approved_by_user':False,'registrations':records}
 (R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 if (R/'source/eyes_bangs_revision/registration.json').exists():
  from refine_ink_eyes_bangs import apply_revision
  apply_revision()
 if (R/'source/ethereal_revision/registration.json').exists():
  from build_ethereal_portrait_revision import apply_revision as apply_ethereal_revision
  apply_ethereal_revision()
 previews();print('Built 54 registered PNGs and comparison boards.')

if __name__=='__main__':main()
