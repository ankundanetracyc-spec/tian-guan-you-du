"""Repainted eye shape and oval hair silhouette, using shared portrait anchors."""
import json,hashlib
import numpy as np
import cv2
from PIL import Image,ImageDraw
import build_ink_three_variants as b

S=b.R/'source/shape_eye_revision'
HAIR_T={'male':(.355,335,74),'female':(.355,337,39)}

def polygon(shape,points,blur=0):
 h,w=shape;im=Image.new('L',(w,h));d=ImageDraw.Draw(im)
 d.polygon(points,fill=255);d.polygon([(w-x,y) for x,y in points],fill=255)
 a=np.array(im,dtype=np.float32)/255
 return cv2.GaussianBlur(a,(0,0),blur) if blur else a

def apply_revision():
 if (S/'rejected.json').exists():
  path=b.R/'portrait_manifest.json'
  manifest=json.loads(path.read_text(encoding='utf-8'))
  rejection=json.loads((S/'rejected.json').read_text(encoding='utf-8'))
  manifest['rejected_revision']=rejection
  manifest['available_variants_per_slot']={'male':[4,5,3,2,3,3,3,3,3],'female':[4,5,4,4,4,4,4,3,3]}
  manifest['available_total']=64
  manifest.setdefault('shape_eye_revision',{})['status']='rejected_by_user'
  path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
  return
 records=[]
 for sex in ['male','female']:
  src=S/f'{sex}_front_high_oval.png';t=HAIR_T[sex]
  dst=b.R/sex/'hair_front/05.png';b.save(dst,b.place(b.hair_tone(b.read(src)),t))
  records.append({'file':dst.relative_to(b.R).as_posix(),'source':src.relative_to(b.R).as_posix(),'scale':t[0],'translation':list(t[1:]),'method':'repainted rounded silhouette; single uniform registration'})
 src=S/'male_eyes_3_refined.png';a=b.read(src);h,w=a.shape[:2]
 assert (w,h)==(1536,1024)
 core=polygon((h,w),[(211,497),(292,486),(364,480),(433,484),(489,499),(549,529),(621,589),(560,590),(480,599),(384,596),(302,574),(244,535)],1)
 # Preserve iris/sclera; convert only surrounding watercolor into ink alpha.
 a=b.pigment(a,core>.4,.65)
 region=polygon((h,w),[(173,477),(282,460),(407,461),(509,486),(578,535),(637,597),(622,616),(535,625),(420,630),(325,614),(239,575),(193,530)],3)
 a[:,:,3]*=region
 # Suppress bright specular dots in the otherwise matte, shared palette.
 bright=(a[:,:,:3].min(2)>200)&(core>.5)
 a[:,:,:3][bright]=[197,181,164]
 t=b.paired_transform((414,529),(1122,529),(464,375),(569,375))
 dst=b.R/'male/eyes/03.png';b.save(dst,b.place(a,t))
 records.append({'file':dst.relative_to(b.R).as_posix(),'source':src.relative_to(b.R).as_posix(),'scale':t[0],'translation':list(t[1:]),'pupil_targets':[[464,375],[569,375]],'method':'paired eye transform; transparent outer ink; no opaque skin plate'})
 manifest=json.loads((b.R/'portrait_manifest.json').read_text(encoding='utf-8'))
 manifest['shape_eye_revision']={'id':'natural_eye_and_oval_hair','records':records,'generation_tool':'built-in imagegen','visual_approved_by_user':False}
 (b.R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 (S/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

def compose(sex,vs,before=False):
 full=Image.new('RGBA',b.SIZE)
 overrides={'hair_front':S/f'before_{sex}_front_05.png'}
 if sex=='male':overrides['eyes']=S/'before_male_eyes_03.png'
 for slot in b.ORDER:
  v=vs[b.SLOTS.index(slot)];p=b.R/sex/slot/f'{v:02}.png'
  if before and slot in overrides and ((slot=='eyes' and v==3) or (slot=='hair_front' and v==5)):p=overrides[slot]
  full.alpha_composite(Image.open(p).convert('RGBA'))
 bg=Image.new('RGBA',b.SIZE,'#e8e3dc');bg.alpha_composite(full);return bg.convert('RGB')

def previews():
 board=Image.new('RGB',(1440,1000),'#e8e3dc');d=ImageDraw.Draw(board)
 for i,(sex,before) in enumerate([('male',True),('male',False),('female',True),('female',False)]):
  vs=[5,5,1,3,1,1,1,1,1] if sex=='male' else [5,5,4,4,4,4,4,1,1]
  im=compose(sex,vs,before).crop((300,80,735,660)).resize((355,473))
  board.paste(im,(i*360,42));d.text((i*360+8,8),('男' if sex=='male' else '女')+(' · 调整前' if before else ' · 调整后'),font=b.FONT,fill='#352f2b')
 for col,before in enumerate([True,False]):
  # Only the eye changes here; same first hairstyle, brows and face.
  im=compose('male',[1,1,1,3,1,1,1,1,1],before)
  board.paste(im.crop((407,330,632,423)).resize((670,277)),(col*720+20,575))
  d.text((col*720+20,535),'眼睛第三款 · '+('调整前' if before else '调整后'),font=b.FONT,fill='#352f2b')
 board.save(b.R/'preview/shape_eye_before_after.jpg',quality=95)
 pair=Image.new('RGB',(1000,1050),'#e8e3dc');d=ImageDraw.Draw(pair)
 for i,sex in enumerate(['male','female']):
  vs=[5,5,1,3,1,1,1,1,1] if sex=='male' else [5,5,4,4,4,4,4,1,1]
  im=compose(sex,vs).crop((260,40,780,1100));im.thumbnail((490,1000));pair.paste(im,(i*500+5,45));d.text((i*500+20,8),'男 · 新眼形／新发型' if sex=='male' else '女 · 新发型',font=b.FONT,fill='#352f2b')
 pair.save(b.R/'preview/shape_eye_pair.png')
 for sex in ['male','female']:
  board=Image.new('RGB',(1500,600),'#e8e3dc');d=ImageDraw.Draw(board)
  for row in range(2):
   for col in range(5):
    vs=[5,col+1,1,3,1,1,1,1,1] if sex=='male' else [5,col+1,4,4,4,4,4,1,1]
    if row:vs[6]=(col%(3 if sex=='male' else 4))+1
    im=compose(sex,vs).crop((310,60,735,645));im.thumbnail((285,267));board.paste(im,(col*300+35,row*300+30));d.text((col*300+8,row*300+3),f'后发{col+1} / 脸型{vs[6]}',font=b.FONT,fill='#352f2b')
  board.save(b.R/f'preview/{sex}_shape_eye_mix.jpg',quality=94)

if __name__=='__main__':
 apply_revision();previews();print('Updated male eye 03 and both front hair 05.')
