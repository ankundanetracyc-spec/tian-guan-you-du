"""Fit AI edited short temple hair; whole-layer registration only."""
from pathlib import Path
import json,hashlib,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import build_ink_three_variants as b
S=b.R/'source/short_sideburn_revision_20261006';OUT=S/'candidate'
J=json.loads((S/'selected_prompts.json').read_text(encoding='utf-8'))['jobs']
T={'male_hair_front_8':(.26,330,119),'female_hair_front_8':(.30,322,121),'male_hair_back_8':(.32,310,135)}
records=[];checks={}
for j in J:
 sex=j['sex'];p=S/f'{j["key"]}.png';a=b.place(b.hair_tone(b.read(p),j['slot']=='hair_back'),T[j['key']]);rel=Path(sex)/j['slot']/'08.png';b.save(OUT/rel,a)
 records.append({'key':j['key'],'source':p.name,'generated_original':j['generated_original'],'source_size':Image.open(p).size,'uniform_transform':T[j['key']],'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'output':rel.as_posix(),'no_local_warp':True})
 if j['slot']!='hair_front':continue
 skin=np.asarray(Image.open(b.R/sex/'face_shape/01.png'))[:,:,3];hair=np.asarray(Image.open(OUT/rel))[:,:,3];missing=0
 for y in range(180,341):
  xs=np.where(skin[y]>230)[0]
  if xs.size:missing+=sum(hair[y,int(x)]<200 for x in [xs.min(),xs.max()])
 checks[sex]={'uncovered_dense_scalp_boundary_pixels':int(missing),'rows':[180,340]}
def compose(sex,old=False,back=8,face=1,bg='#e8e4dd',front=True):
 im=Image.new('RGBA',b.SIZE)
 for slot in b.ORDER:
  if slot=='hair_front' and not front:continue
  n=8 if slot=='hair_front' else back if slot=='hair_back' else face if slot=='face_shape' else 1;p=b.R/sex/slot/f'{n:02}.png'
  rel=Path(sex)/slot/f'{n:02}.png'
  if (OUT/rel).exists():p=(S/'previous' if old else OUT)/rel
  im=Image.alpha_composite(im,Image.open(p).convert('RGBA'))
 return Image.alpha_composite(Image.new('RGBA',b.SIZE,bg),im).convert('RGB')
F=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
def sheet(items,path,cols=2,crop=(300,90,740,650),tile=(440,560)):
 im=Image.new('RGB',(tile[0]*cols,(tile[1]+32)*((len(items)+cols-1)//cols)),'#e8e4dd');d=ImageDraw.Draw(im)
 for n,(sex,old,back,face,bg,front,label) in enumerate(items):
  z=compose(sex,old,back,face,bg,front).crop(crop).resize(tile);x=n%cols*tile[0];y=n//cols*(tile[1]+32);im.paste(z,(x,y+32));d.text((x+8,y+3),label,font=F,fill='#39332e')
 im.save(path,quality=95)
P=b.R/'preview'
sheet([(s,o,8,1,'#e8e4dd',True,('男' if s=='male' else '女')+(' · 修改前' if o else ' · 鬓角修订')) for s in ['male','female'] for o in [True,False]],P/'short_sideburn_before_after_20261006.jpg')
sheet([(s,False,8,1,'#e8e4dd',True,'行舟短发' if s=='male' else '听雨短发') for s in ['male','female']],P/'short_sideburn_pair_20261006.jpg')
details=Image.new('RGB',(660,1920),'#e8e4dd');dd=ImageDraw.Draw(details)
for row,(s,side) in enumerate([(s,i) for s in ['male','female'] for i in [0,1]]):
 for col,old in enumerate([True,False]):
  box=(340,300,450,450) if side==0 else (590,300,700,450)
  details.paste(compose(s,old).crop(box).resize((330,450)),(col*330,row*480+28));dd.text((col*330+5,row*480+2),('男' if s=='male' else '女')+('左' if side==0 else '右')+(' · 修改前' if old else ' · 修订'),font=F,fill='#39332e')
details.save(P/'short_sideburn_detail_20261006.jpg',quality=95)
sheet([(s,False,8,f,'#e8e4dd',True,f'{s} · 脸{f}') for s in ['male','female'] for f in range(1,4 if s=='male' else 5)],P/'short_sideburn_face_qa_20261006.jpg',3)
sheet([(s,False,r,1,'#e8e4dd',True,f'{s} · 后发{r}') for s in ['male','female'] for r in range(1,9)],P/'short_sideburn_mix_20261006.jpg',4)
sheet([(s,False,8,1,bg,True,s+' · 浅底' if bg=='#e8e4dd' else s+' · 深底') for s in ['male','female'] for bg in ['#e8e4dd','#302e2c']],P/'short_sideburn_light_dark_20261006.jpg')
split=Image.new('RGB',(1320,592));d=ImageDraw.Draw(split)
for n,j in enumerate(J):
 a=Image.open(OUT/j['sex']/j['slot']/'08.png').crop((300,90,740,650));z=Image.alpha_composite(Image.new('RGBA',a.size,'#e8e4dd'),a).convert('RGB');split.paste(z,(n*440,32));d.text((n*440+8,3),j['name'],font=F,fill='#39332e')
split.save(P/'short_sideburn_split_20261006.jpg',quality=95)
before=json.loads((S/'before_hashes.json').read_text(encoding='utf-8'));changed={f'{j["sex"]}/{j["slot"]}/08.png' for j in J}
assert all(hashlib.sha256((b.R/p).read_bytes()).hexdigest()==h for p,h in before.items() if p not in changed)
assert (b.R/'appearance_parts.json').read_bytes()==(S/'previous/appearance_parts.before.json').read_bytes()
fit=all(c['uncovered_dense_scalp_boundary_pixels']==0 for c in checks.values())
reg={'canvas':[1024,1536],'runtime_offset':[0,0],'method':'whole-layer uniform fitting; preserves previous scale/translation where source layout retained; not formal three-point residual certification','parts':records,'base01_scalp_checks':checks}
(S/'registration.json').write_text(json.dumps(reg,ensure_ascii=False,indent=2),encoding='utf-8')
if '--apply' in sys.argv:
 assert fit,'Scalp check failed'
 for j in J:shutil.copy2(OUT/j['sex']/j['slot']/'08.png',b.R/j['sex']/j['slot']/'08.png')
 m=json.loads((b.R/'portrait_manifest.json').read_text(encoding='utf-8'));m['short_sideburn_revision']={'date':'2026-10-06','replaced_parts':3,'active_choice_count':88,'selected_prompts':'source/short_sideburn_revision_20261006/selected_prompts.json','registration':'source/short_sideburn_revision_20261006/registration.json','status':'candidate, pending user visual review'};(b.R/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report={'pass':fit,'applied':'--apply' in sys.argv,'replaced_parts':3,'unchanged_existing_parts':88,'active_choice_count':88,'physical_png_count':91,'previous_ordinals_and_names':True,'base01_scalp_checks':checks,'parts':records,'visual_approved_by_user':False}
(S/'asset_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False))
