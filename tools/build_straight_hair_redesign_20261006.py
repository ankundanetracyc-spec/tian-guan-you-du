"""Fit six new straight hair components against our actual scalp outlines."""
from pathlib import Path
import json,hashlib,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import build_ink_three_variants as b
R=b.R;S=R/'source/straight_hair_redesign_20261006';OUT=S/'candidate'
J=json.loads((S/'selected_prompts.json').read_text(encoding='utf-8'))['jobs']
T={'male_hair_front_8':(.26,330,119),'female_hair_front_6':(.265,316,95),'female_hair_front_7':(.285,300,114),'female_hair_front_8':(.30,322,121),'male_hair_back_8':(.32,310,135),'female_hair_back_8':(.37,285,136)}
AXES={'male_hair_front_8':720,'female_hair_front_6':766,'female_hair_front_7':768,'female_hair_front_8':656,'male_hair_back_8':646,'female_hair_back_8':632}
F=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
records=[];profiles={};boundary_checks={}
for j in J:
 p=S/j['workspace_source'];a=b.hair_tone(b.read(p),j['slot']=='hair_back');a=b.place(a,T[j['key']]);target=OUT/j['sex']/j['slot']/f'{j["index"]:02}.png';b.save(target,a)
 records.append({'key':j['key'],'name':j['name'],'source':p.name,'generated_original':j['generated_original'],'output':target.relative_to(OUT).as_posix(),'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'source_size':Image.open(p).size,'uniform_transform':T[j['key']],'source_axis_visual_estimate':AXES[j['key']],'registered_axis_x':AXES[j['key']]*T[j['key']][0]+T[j['key']][1],'no_local_warp':True})
 if j['slot']=='hair_front':
  skin=np.asarray(Image.open(R/j['sex']/'face_shape/01.png'))[:,:,3];front=np.asarray(Image.open(target))[:,:,3];old=np.asarray(Image.open(S/'rejected'/j['sex']/'hair_front'/f'{j["index"]:02}.png'))[:,:,3];rows=[]
  for y in [180,200,220,240,260,280,300,320,330,340]:
   xs=np.where(skin[y]>230)[0];hs=np.where(front[y]>230)[0];os=np.where(old[y]>230)[0]
   if xs.size and hs.size:
    rows.append({'y':y,'scalp_edges':[int(xs.min()),int(xs.max())],'hair_edges':[int(hs.min()),int(hs.max())],'margins_left_right':[int(xs.min()-hs.min()),int(hs.max()-xs.max())],'rejected_margins_left_right':[int(xs.min()-os.min()),int(os.max()-xs.max())] if os.size else []})
  profiles[j['key']]=rows
  missing_counts=[]
  for layer in [old,front]:
   missing=0
   for y in range(180,341):
    xs=np.where(skin[y]>230)[0]
    if xs.size:
     missing+=sum(layer[y,int(x)]<200 for x in [xs.min(),xs.max()])
   missing_counts.append(int(missing))
  boundary_checks[j['key']]={'scalp_rows':[180,340],'old_uncovered_boundary_pixels':missing_counts[0],'new_uncovered_boundary_pixels':missing_counts[1],'note':'actual dense face01 scalp contour; style-specific hairline and ears excluded below this range'}

def compose(sex,f,r,face=1,old=False,bg='#e8e4dd'):
 im=Image.new('RGBA',b.SIZE)
 for slot in b.ORDER:
  n=f if slot=='hair_front' else r if slot=='hair_back' else face if slot=='face_shape' else 1;rel=Path(sex)/slot/f'{n:02}.png';p=R/rel
  if (OUT/rel).exists():p=S/'rejected'/rel if old else OUT/rel
  im=Image.alpha_composite(im,Image.open(p).convert('RGBA'))
 return Image.alpha_composite(Image.new('RGBA',b.SIZE,bg),im).convert('RGB')
def sheet(items,path,cols=3):
 im=Image.new('RGB',(480*cols,640*((len(items)+cols-1)//cols)),'#e8e4dd');d=ImageDraw.Draw(im)
 for n,(sex,f,r,face,old,label) in enumerate(items):
  z=compose(sex,f,r,face,old).crop((270,45,770,730)).resize((450,617));x=n%cols*480;y=n//cols*640;im.paste(z,(x+15,y+23));d.text((x+15,y+1),label,font=F,fill='#39332e')
 im.save(path,quality=95)
styles=[('male',8,8,1,False,'行舟短发'),('female',6,6,1,False,'拂云轻束'),('female',7,7,1,False,'素心盘髻'),('female',8,8,1,False,'听雨短发')]
sheet(styles,R/'preview/straight_hair_four_20261006.jpg',2)
sheet([(sex,f,r,face,old,name+(' · 废稿' if old else ' · 顺直版')) for sex,f,r,face,_,name in styles for old in [True,False]],R/'preview/straight_hair_before_after_20261006.jpg',2)
sheet([(sex,f,r,face,False,name+f' · 脸型 {face}') for sex,f,r,_,_,name in styles for face in range(1,4 if sex=='male' else 5)],R/'preview/straight_hair_face_qa_20261006.jpg')
sheet([('female',f,r,1,False,f'前 {f} / 后 {r}') for f in [6,7,8] for r in [6,7,8]],R/'preview/straight_female_hair_mix_20261006.jpg')
sheet([('male',8,r,1,False,f'男 前8 / 后{r}') for r in range(1,9)]+[('female',f,r,1,False,f'女 前{f} / 后{r}') for f in [6,7,8] for r in range(1,6)],R/'preview/straight_hair_old_mix_20261006.jpg')
split=Image.new('RGB',(1440,1120));d=ImageDraw.Draw(split)
for n,j in enumerate(J):
 a=Image.open(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png').crop((300,90,740,680))
 for side,bg in enumerate(['#e8e4dd','#302e2c']):
  z=Image.alpha_composite(Image.new('RGBA',a.size,bg),a).convert('RGB');z.thumbnail((230,490));x=n%3*480+side*240;y=n//3*560;split.paste(Image.new('RGB',(240,560),bg),(x,y));split.paste(z,(x+(240-z.width)//2,y+45));d.text((x+8,y+8),j['name'],font=F,fill='#b0a99f' if side else '#39332e')
split.save(R/'preview/straight_hair_split_20261006.jpg',quality=95)
for sex in ['male','female']:
 im=compose(sex,8,8).convert('RGBA');d=ImageDraw.Draw(im);skin=np.asarray(Image.open(R/sex/'face_shape/01.png'))[:,:,3]
 for y in range(174,345,2):
  xs=np.where(skin[y]>230)[0]
  if xs.size:
   for x in [int(xs.min()),int(xs.max())]:d.ellipse((x-1,y-1,x+1,y+1),fill='#ba5a46')
 x=517 if sex=='male' else 519;d.line((x,135,x,550),fill='#44898d',width=1);im.crop((320,100,720,590)).resize((800,980)).convert('RGB').save(R/f'preview/straight_{sex}_skull_overlay_20261006.jpg',quality=95)
changed={f'{j["sex"]}/{j["slot"]}/{j["index"]:02}.png' for j in J};before=json.loads((S/'before_hashes.json').read_text(encoding='utf-8'))
assert len(before)==91 and all(hashlib.sha256((R/p).read_bytes()).hexdigest()==h for p,h in before.items() if p not in changed)
assert (R/'appearance_parts.json').read_bytes()==(S/'previous/appearance_parts.before.json').read_bytes()
for j in J:
 a=np.asarray(Image.open(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png'));assert a.shape==(1536,1024,4) and np.all(a[a[:,:,3]==0,:3]==0)
fit_ok=all(min(row['margins_left_right'])>=0 for rows in profiles.values() for row in rows) and all(row['new_uncovered_boundary_pixels']==0 for row in boundary_checks.values())
registration={'canvas':[1024,1536],'runtime_offset':[0,0],'own_head_axes':{'male':517,'female':519},'method':'whole-layer uniform visual fitting plus actual scalp outline comparison; no formal three-point residual certification','parts':records,'scalp_width_profiles':profiles,'dense_scalp_boundary_checks':boundary_checks,'base01_scanned_widths_pass':fit_ok}
(S/'registration.json').write_text(json.dumps(registration,ensure_ascii=False,indent=2),encoding='utf-8');(S/'generation_results.json').write_text(json.dumps({'mode':'built-in image_gen','parts':records},ensure_ascii=False,indent=2),encoding='utf-8')
if '--apply' in sys.argv:
 assert 'short_sideburn_revision' not in json.loads((R/'portrait_manifest.json').read_text(encoding='utf-8')), 'Old short sideburns were rejected; do not overwrite latest revision'
 assert fit_ok,'Scalp coverage is too narrow; inspect profiles before applying'
 for j in J:shutil.copy2(OUT/j['sex']/j['slot']/f'{j["index"]:02}.png',R/j['sex']/j['slot']/f'{j["index"]:02}.png')
 m=json.loads((R/'portrait_manifest.json').read_text(encoding='utf-8'));m['straight_hair_redesign']={'date':'2026-10-06','replaced_parts':6,'active_choice_count':88,'physical_file_count':91,'selected_prompts':'source/straight_hair_redesign_20261006/selected_prompts.json','registration':'source/straight_hair_redesign_20261006/registration.json','user_direction':'straight and simple, few flyaways','status':'visual fitting candidate; pending user aesthetic review'};(R/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report={'pass':fit_ok,'applied':'--apply' in sys.argv,'replaced_parts':6,'unchanged_existing_parts':85,'active_choice_count':88,'physical_png_count':91,'base01_scanned_widths_pass':fit_ok,'previous_ordinals_and_names':True,'parts':records}
(S/'asset_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'pass':fit_ok,'applied':report['applied'],'negative_widths':{key:[row for row in rows if min(row['margins_left_right'])<0] for key,rows in profiles.items()},'replaced_parts':6},ensure_ascii=False))
