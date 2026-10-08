"""Reference-face parts and two flowing hair sets; original drawings retained."""
import json, hashlib
import numpy as np
import cv2
from PIL import Image, ImageDraw
import build_ink_three_variants as b

S=b.R/'source/ethereal_revision'
FACE_T=(110/265,464-380*(110/265),379-626*(110/265))
HAIR_T={
 'male_front_half':(.48,271,96),'female_front_half':(.50,263,98),
 'male_front_high':(.47,276,55),'female_front_high':(.48,273,70),
 'male_back_half':(.80,107,135),'female_back_half':(.80,109,135),
 'male_back_high':(.80,107,160),'female_back_high':(.80,109,160),
}

def poly_mask(shape,points,mirror=False,blur=0):
 h,w=shape;im=Image.new('L',(w,h));d=ImageDraw.Draw(im);d.polygon(points,fill=255)
 if mirror:d.polygon([(w-x,y) for x,y in points],fill=255)
 a=np.array(im,dtype=np.float32)/255
 return cv2.GaussianBlur(a,(0,0),blur) if blur else a

def extract(master,blank,region,core=None):
 rgb=master[:,:,:3].copy();bg=blank[:,:,:3]
 coverage=np.clip((np.max(bg-rgb,axis=2)-2)/165,0,1)*region
 if core is not None:coverage=np.maximum(coverage,core)
 foreground=(rgb-(1-coverage[:,:,None])*bg)/np.maximum(coverage[:,:,None],.001)
 return np.dstack((np.clip(foreground,0,255),coverage*255))

def apply_revision():
 records=[]
 master=b.read(S/'face_master.png');blank=b.read(S/'face_blank.png');h,w=blank.shape[:2]
 assert (w,h)==(1024,1536)
 # One shared skin curve for both matching drawings, including ears and neck.
 sample=np.median(blank[440:490,450:570,:3].reshape(-1,3),axis=0);delta=np.array([233,209,192])-sample
 for a in [master,blank]:a[:,:,:3]+=np.clip((a[:,:,:3].mean(2)-100)/110,0,1)[:,:,None]*delta
 parts={'face_shape':blank}
 region=poly_mask((h,w),[(270,541),(382,531),(477,554),(480,589),(393,588),(270,575)],True,3)
 parts['eyebrows']=extract(master,blank,region)
 eye_region=poly_mask((h,w),[(277,598),(351,585),(410,591),(454,622),(464,672),(393,686),(319,674),(279,638)],True,4)
 eye_core=poly_mask((h,w),[(310,621),(334,612),(368,609),(393,616),(421,633),(445,649),(401,652),(365,651),(337,639)],True,1)
 parts['eyes']=extract(master,blank,eye_region,eye_core)
 nose_region=poly_mask((h,w),[(470,690),(550,690),(585,770),(576,824),(542,835),(481,835),(446,816),(443,770)],False,12)
 parts['nose']=extract(master,blank,nose_region)
 mouth_region=poly_mask((h,w),[(425,845),(485,825),(546,828),(606,849),(604,916),(545,944),(483,943),(426,912)],False,8)
 mouth_core=poly_mask((h,w),[(437,876),(477,857),(507,850),(541,856),(581,876),(571,896),(538,912),(490,912),(457,896)],False,2)
 parts['mouth']=extract(master,blank,mouth_region,mouth_core)
 for slot,a in parts.items():
  dst=b.R/'female'/slot/'04.png';b.save(dst,b.place(a,FACE_T))
  records.append({'file':dst.relative_to(b.R).as_posix(),'source':'source/ethereal_revision/face_master.png' if slot!='face_shape' else 'source/ethereal_revision/face_blank.png','scale':FACE_T[0],'translation':list(FACE_T[1:]),'method':'shared master anatomy; transparent feature pigments; one whole-part transform'})
 for sex in ['male','female']:
  for style,v in [('half',4),('high',5)]:
   front=b.hair_tone(b.read(S/f'{sex}_front_{style}.png'));rear=b.hair_tone(b.read(S/f'{sex}_back_{style}.png'),True)
   ft=HAIR_T[f'{sex}_front_{style}'];rt=HAIR_T[f'{sex}_back_{style}']
   back=b.place(rear,rt)
   if style=='high':
    # The generator put the rear knot above the front cap. It belongs behind
    # the head and must remain available when the front slot is hidden.
    y=np.indices(front.shape[:2])[0];weight=np.clip((246-y)/8,0,1)
    knot=front.copy();knot[:,:,3]*=weight;front[:,:,3]*=1-weight
    knot_t=(ft[0],ft[1],ft[2]+35)
    upper=b.place(knot,knot_t);a=upper[:,:,3:4]/255;c=back[:,:,3:4]/255;out_a=a+c*(1-a)
    back[:,:,:3]=(upper[:,:,:3]*a+back[:,:,:3]*c*(1-a))/np.maximum(out_a,.0001);back[:,:,3]=out_a[:,:,0]*255
    front=b.hair_tone(b.read(S/f'{sex}_front_high_lowcrown.png'))
    # Newly painted swept-back temples: register the complete cap once.
    # Compact repainted crown and lower hairline; no local warping.
    ft=(.40,312,64) if sex=='male' else (.40,314,22)
   for slot,a,t in [('hair_front',b.place(front,ft),ft),('hair_back',back,rt)]:
    source=f'{sex}_{"front" if slot=="hair_front" else "back"}_{style}'+('_lowcrown' if style=='high' and slot=='hair_front' else '')+'.png'
    dst=b.R/sex/slot/f'{v:02}.png';b.save(dst,a);records.append({'file':dst.relative_to(b.R).as_posix(),'source':f'source/ethereal_revision/{source}','scale':t[0],'translation':list(t[1:]),'rear_knot_separated_from_front':style=='high'})
    if style=='high' and slot=='hair_back':records[-1]['knot']={'source':f'source/ethereal_revision/{sex}_front_high.png','scale':knot_t[0],'translation':list(knot_t[1:])}
 manifest=json.loads((b.R/'portrait_manifest.json').read_text(encoding='utf-8'))
 manifest['variants_per_slot']={'male':[5,5,3,3,3,3,3,3,3],'female':[5,5,4,4,4,4,4,3,3]};manifest['total_pngs']=67
 manifest['ethereal_revision']={'id':'reference_face_and_flowing_hair','rejected':'forehead bangs withdrawn; front 02 restored','records':records,'generation_tool':'built-in imagegen','visual_approved_by_user':False}
 manifest['latest_revision']['hair_bangs_rejected']=True
 manifest['ethereal_revision']['high_front_revision']='Compact lower crown and lower forehead hairline; no dangling locks; rear knot lowered 35 px; built-in imagegen'
 (b.R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 (S/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
 if (b.R/'source/shape_eye_revision/prompts.json').exists():
  from refine_portrait_eye_shape import apply_revision as apply_shape_eye_revision
  apply_shape_eye_revision()

def previews():
 board=Image.new('RGB',(1600,1240),'#e8e3dc');d=ImageDraw.Draw(board)
 for col,(sex,hair) in enumerate([('male',4),('male',5),('female',4),('female',5)]):
  vs=[1]*9;vs[0]=vs[1]=hair
  if sex=='female':vs[2:7]=[4]*5
  im=b.compose(sex,vs);im.save(b.R/'preview'/f'{sex}_ethereal_{hair}.png');board.paste(im.resize((400,600)),(col*400,40));d.text((col*400+10,5),f'{"男" if sex=="male" else "女"} · {"清雅半束" if hair==4 else "高束流风"}',font=b.FONT,fill='#352f2b')
  board.paste(im.crop((350,230,690,650)).resize((390,482)),(col*400,680))
 board.save(b.R/'preview/ethereal_four_sets.jpg',quality=95)
 board=Image.new('RGB',(1200,630),'#e8e3dc');d=ImageDraw.Draw(board)
 ref=Image.open(S/'user_face_reference.png').convert('RGB').crop((60,85,350,455));ref.thumbnail((360,520));board.paste(ref,(10,55));d.text((10,12),'用户参考（红圈为原标记）',font=b.FONT,fill='#352f2b')
 for col,vs in enumerate([[1]*9,[1,1,4,4,4,4,4,1,1]],start=1):
  im=b.compose('female',vs).crop((360,245,678,570)).resize((382,390));board.paste(im,(col*400+8,80));d.text((col*400+10,12),'现有第一款' if col==1 else '新增参考五官 · 第四款',font=b.FONT,fill='#352f2b')
 board.save(b.R/'preview/reference_face_comparison.png')
 for sex in ['male','female']:
  board=Image.new('RGB',(1500,640),'#e8e3dc');d=ImageDraw.Draw(board)
  for row,front in enumerate([4,5]):
   for col,back in enumerate([1,2,3,4,5]):
    vs=[1]*9;vs[0]=front;vs[1]=back
    if sex=='female':vs[2:7]=[4]*5
    im=b.compose(sex,vs).crop((260,0,770,730));im.thumbnail((290,280));board.paste(im,(col*300,row*320+35));d.text((col*300+8,row*320+5),f'前 {front} / 后 {back}',font=b.FONT,fill='#352f2b')
  board.save(b.R/'preview'/f'{sex}_ethereal_mix.jpg',quality=93)

if __name__=='__main__':apply_revision();b.previews();previews();print('Reference face + eight independent hair parts exported.')
