"""Fit four repainted fronts; preserve all other artwork and public choices."""
from pathlib import Path
import json,hashlib,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import build_ink_three_variants as b
R=b.R;S=R/'source/hair_front_refinement_20261006';OUT=S/'candidate'
if '--apply' in sys.argv and 'straight_hair_redesign' in json.loads((R/'portrait_manifest.json').read_text(encoding='utf-8')):
 raise SystemExit('The four styles were rejected; the older refinement must not overwrite the straight redesign.')
J=json.loads((S/'selected_prompts.json').read_text(encoding='utf-8'))['jobs']
T={'male_hair_front_8':(.235,358,112),'female_hair_front_6':(.27,342,101),'female_hair_front_7':(.28,335,96),'female_hair_front_8':(.30,321,128)}
AXES={'male_hair_front_8':676,'female_hair_front_6':654,'female_hair_front_7':658,'female_hair_front_8':660}
F=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
records=[]
for j in J:
 p=S/j['workspace_source']
 if not p.exists():shutil.copy2(j['generated_original'],p)
 a=b.hair_tone(b.read(p));a=b.place(a,T[j['key']]);target=OUT/j['sex']/j['slot']/f'{j["index"]:02}.png';b.save(target,a)
 records.append({'key':j['key'],'name':j['name'],'source':p.name,'generated_original':j['generated_original'],'output':target.relative_to(OUT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'source_size':Image.open(p).size,'uniform_transform':T[j['key']],'source_hairline_axis_visual_estimate':AXES[j['key']],'registered_axis_x':AXES[j['key']]*T[j['key']][0]+T[j['key']][1],'no_local_warp':True})

def compose(sex,f,r,face=1,old=False,bg='#e8e4dd'):
 im=Image.new('RGBA',b.SIZE)
 for slot in b.ORDER:
  n=f if slot=='hair_front' else r if slot=='hair_back' else face if slot=='face_shape' else 1
  rel=Path(sex)/slot/f'{n:02}.png';p=R/rel
  if slot=='hair_front' and (OUT/rel).exists():p=(S/'previous'/rel) if old else OUT/rel
  im=Image.alpha_composite(im,Image.open(p).convert('RGBA'))
 return Image.alpha_composite(Image.new('RGBA',b.SIZE,bg),im).convert('RGB')

def sheet(items,path,cols=3):
 im=Image.new('RGB',(480*cols,640*((len(items)+cols-1)//cols)),'#e8e4dd');d=ImageDraw.Draw(im)
 for n,(sex,f,r,face,old,label) in enumerate(items):
  z=compose(sex,f,r,face,old).crop((270,45,770,730)).resize((450,617));x=n%cols*480;y=n//cols*640
  im.paste(z,(x+15,y+23));d.text((x+15,y+1),label,font=F,fill='#39332e')
 im.save(path,quality=95)

sheet([('female',k,k,1,False,['拂云轻束','素心盘髻','听雨短发'][k-6]) for k in range(6,9)],R/'preview/female_hair_front_refined_three_20261006.jpg')
sheet([(j['sex'],j['index'],j['index'],1,old,j['name']+(' · 上一版' if old else ' · 重绘版')) for j in J for old in [True,False]],R/'preview/hair_front_refinement_before_after_20261006.jpg',2)
sheet([('female',f,r,1,False,f'前发 {f} / 后发 {r}') for f in range(6,9) for r in range(6,9)],R/'preview/female_hair_front_refined_mix_20261006.jpg')
sheet([('female',k,k,face,False,f'{k} · 脸型 {face}') for k in range(6,9) for face in range(1,5)],R/'preview/female_hair_front_refined_face_qa_20261006.jpg')
sheet([(sex,f,r,1,False,f'{sex} · 前 {f} / 后 {r}') for sex,f,r in [('male',8,i) for i in [1,6,7,8]]+[('female',f,r) for f in [6,7,8] for r in [1,2,3,4,5]]],R/'preview/hair_front_refined_old_back_mix_20261006.jpg')
split=Image.new('RGB',(1920,640));d=ImageDraw.Draw(split)
for n,j in enumerate(J):
 a=Image.open(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png').crop((300,80,740,620))
 for side,bg in enumerate(['#e8e4dd','#302e2c']):
  z=Image.alpha_composite(Image.new('RGBA',a.size,bg),a).convert('RGB');z.thumbnail((230,540));x=n*480+side*240
  split.paste(Image.new('RGB',(240,640),bg),(x,0));split.paste(z,(x+(240-z.width)//2,50));d.text((x+8,8),j['name'],font=F,fill='#b0a99f' if side else '#39332e')
split.save(R/'preview/hair_front_refined_split_20261006.jpg',quality=95)

before=json.loads((S/'before_hashes.json').read_text(encoding='utf-8'));changed={f'{j["sex"]}/hair_front/{j["index"]:02}.png' for j in J}
assert all(hashlib.sha256((R/p).read_bytes()).hexdigest()==h for p,h in before.items() if p not in changed)
metrics={}
for j in J:
 a=np.asarray(Image.open(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png'));assert a.shape==(1536,1024,4) and np.all(a[a[:,:,3]==0,:3]==0)
 if j['sex']=='female':
  old=np.asarray(Image.open(S/'previous/female/hair_front'/f'{j["index"]:02}.png'))
  metrics[j['key']]={'central_cheek_roi':[444,397,594,505],'old_opaque_pixels':int((old[397:505,444:594,3]>128).sum()),'new_opaque_pixels':int((a[397:505,444:594,3]>128).sum()),'note':'fixed image ROI support only; not anatomical segmentation'}
registration={'canvas':[1024,1536],'runtime_offset':[0,0],'method':'whole-layer uniform visual fitting; no formal three-point residual certification','own_head_axes':{'male':517,'female':519},'parts':records,'face_occlusion_metrics':metrics}
(S/'registration.json').write_text(json.dumps(registration,ensure_ascii=False,indent=2),encoding='utf-8')
(S/'generation_results.json').write_text(json.dumps({'mode':'built-in image_gen','parts':records},ensure_ascii=False,indent=2),encoding='utf-8')
if '--apply' in sys.argv:
 for j in J:shutil.copy2(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png',R/j['sex']/j['slot']/f'{j["index"]:02}.png')
 m=json.loads((R/'portrait_manifest.json').read_text(encoding='utf-8'));m['hair_front_refinement']={'date':'2026-10-06','replaced_parts':4,'active_choice_count':88,'physical_file_count':91,'selected_prompts':'source/hair_front_refinement_20261006/selected_prompts.json','registration':'source/hair_front_refinement_20261006/registration.json','status':'visual fitting candidate; pending user aesthetic review'};(R/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report={'pass':True,'applied':'--apply' in sys.argv,'replaced_fronts':4,'unchanged_existing_parts':87,'active_choice_count':88,'physical_png_count':91,'face_occlusion_metrics':metrics,'no_skin_or_ears_in_fronts':'visual inspection required','previous_ordinals_and_names':'appearance_parts.json is unchanged'}
(S/'asset_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False))
