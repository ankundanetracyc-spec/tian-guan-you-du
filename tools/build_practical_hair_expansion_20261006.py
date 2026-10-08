"""Register six new male hair paintings without locally warping artwork."""
from pathlib import Path
import json,hashlib,sys,shutil
import numpy as np
import cv2
from PIL import Image,ImageDraw,ImageFont
import build_ink_three_variants as b
cv2.setNumThreads(1)
R=b.R;S=R/'source/practical_hair_expansion_20261006';OUT=S/'candidate'
if '--apply' in sys.argv and 'female_practical_hair_expansion' in json.loads((R/'portrait_manifest.json').read_text(encoding='utf-8')):
 raise SystemExit('Female expansion is active; the older male-only snapshot must not overwrite the current pack.')
T={
 'male_hair_front_6':(.35,327,52),
 'male_hair_back_6':(.51,247,142),
 'male_hair_front_7':(.28,341,93),
 'male_hair_back_7':(.33,303,78),
 'male_hair_front_8':(.30,357,66),
 'male_hair_back_8':(.32,350,135),
}
SOURCE_AXES={'male_hair_front_6':543,'male_hair_back_6':530,'male_hair_front_7':628,'male_hair_back_7':650,'male_hair_front_8':533,'male_hair_back_8':523}
J=json.loads((S/'selected_prompts.json').read_text(encoding='utf-8'))['jobs']
def compose(front,back,face=1,bg='#e8e4dd'):
 im=Image.new('RGBA',b.SIZE)
 for slot in b.ORDER:
  p=OUT/'male'/slot/f'{front if slot=="hair_front" else back:02}.png' if slot in ['hair_front','hair_back'] else R/'male'/slot/f'{face if slot=="face_shape" else 1:02}.png'
  if not p.exists():p=R/'male'/slot/f'{front if slot=="hair_front" else back:02}.png'
  im=Image.alpha_composite(im,Image.open(p).convert('RGBA'))
 return Image.alpha_composite(Image.new('RGBA',b.SIZE,bg),im).convert('RGB')
def sheet(pairs,path,faces=False):
 cols=3;cell=(480,640);im=Image.new('RGB',(cols*cell[0],((len(pairs)+2)//3)*cell[1]),'#e8e4dd');d=ImageDraw.Draw(im)
 font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
 for n,(f,r,face,label) in enumerate(pairs):
  head=compose(f,r,face).crop((270,45,770,730)).resize((450,617))
  x=n%3*480;y=n//3*640;im.paste(head,(x+15,y+23));d.text((x+15,y+2),label,font=font,fill='#39332e')
 im.save(path,quality=95)
records=[]
for j in J:
 a=b.read(S/j['workspace_source']);a=b.hair_tone(a,j['slot']=='hair_back');a=b.place(a,T[j['key']])
 p=OUT/'male'/j['slot']/f'{j["index"]:02}.png';b.save(p,a)
 ar=np.asarray(Image.open(p));opaque=ar[:,:,3]>230
 records.append(dict(key=j['key'],name=j['name'],source=j['workspace_source'],output=str(p.relative_to(R)),transform=T[j['key']],source_size=Image.open(S/j['workspace_source']).size,opaque_rgb_median=np.median(ar[:,:,:3][opaque],axis=0).tolist(),notes=['one uniform scale and translation','project head centre and frontal hairline visual registration','no local warping','complete continuous rear scalp; no face hole'] if j['slot']=='hair_back' else ['one uniform scale and translation','front cap and front-facing short temples/fringe only','no skin, ears or facial features']))
for r in records:
 r['output']='male/'+next(j['slot'] for j in J if j['key']==r['key'])+'/'+r['key'].rsplit('_',1)[1].zfill(2)+'.png'
 r['source_head_axis_x_visual_estimate']=SOURCE_AXES[r['key']]
 r['registered_head_axis_x']=SOURCE_AXES[r['key']]*r['transform'][0]+r['transform'][1]
(S/'registration.json').write_text(json.dumps({'rig_id':'our_selected_ink_male_20261003_v1','canvas':[1024,1536],'runtime_offset':[0,0],'project_head_axis_x':517,'project_central_hairline_reference':[517,271],'reference_points_are_our_own_not_guigu':True,'method':'whole-layer uniform visual fitting; not certified three-point residual registration','parts':records},ensure_ascii=False,indent=2),encoding='utf-8')
sheet([(k,k,1,['束风轻束','守真束髻','行舟短发'][k-6]) for k in range(6,9)],R/'preview/male_practical_hair_three_20261006.jpg')
sheet([(f,r,1,f'前发 {f} / 后发 {r}') for f in range(6,9) for r in range(6,9)],R/'preview/male_practical_hair_mix_20261006.jpg')
sheet([(f,r,1,f'前发 {f} / 后发 {r}') for f,r in [(1,6),(1,7),(1,8),(6,1),(7,1),(8,1)]],R/'preview/male_practical_hair_original_mix_20261006.jpg')
sheet([(k,k,face,f'{k} · 脸型 {face}') for k in range(6,9) for face in range(1,4)],R/'preview/male_practical_hair_face_qa_20261006.jpg')
split=Image.new('RGB',(1440,960));d=ImageDraw.Draw(split);font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
for n,j in enumerate(J):
 a=Image.open(OUT/'male'/j['slot']/f'{j["index"]:02}.png').crop((270,35,770,980))
 for m,bg in enumerate(['#e8e4dd','#302e2c']):
  z=Image.alpha_composite(Image.new('RGBA',a.size,bg),a).convert('RGB');z.thumbnail((230,430))
  x=(n%3)*480+m*240;y=(n//3)*480;split.paste(Image.new('RGB',(240,480),bg),(x,y));split.paste(z,(x+(240-z.width)//2,y+35));d.text((x+8,y+5),j['name'],font=font,fill='#b0a99f' if m else '#39332e')
split.save(R/'preview/male_practical_hair_split_20261006.jpg',quality=95)
before=json.loads((S/'before_hashes.json').read_text())
assert all(hashlib.sha256((R/p).read_bytes()).hexdigest()==h for p,h in before.items()),'Existing artwork changed'
if '--apply' in sys.argv:
 for j in J:shutil.copy2(OUT/'male'/j['slot']/f'{j["index"]:02}.png',R/'male'/j['slot']/f'{j["index"]:02}.png')
 config=json.loads((S/'previous/appearance_parts.before.json').read_text(encoding='utf-8'))
 for slot in ['hair_front','hair_back']:
  entry=next(c for c in config['components'] if c['gender']=='male' and c['part']==slot)
  indices=entry.get('variant_indices',list(range(1,entry['variant_count']+1)))
  extra=[j for j in J if j['slot']==slot]
  entry['variant_indices']=indices+[j['index'] for j in extra]
  entry['variant_names']+=[j['name'] for j in extra];entry['variant_count']+=len(extra)
  assert 5 not in entry['variant_indices'] if slot=='hair_front' else True
 config['rig_id']='ink_three_variants_20261004_active82'
 (R/'appearance_parts.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 manifest=json.loads((S/'previous/portrait_manifest.before.json').read_text(encoding='utf-8'))
 manifest['practical_hair_expansion']={'date':'2026-10-06','gender':'male','new_assets':6,'physical_indices':[6,7,8],'active_choice_count':82,'physical_file_count':85,'registration':'source/practical_hair_expansion_20261006/registration.json','selected_prompts':'source/practical_hair_expansion_20261006/selected_prompts.json','parts':records,'status':'visual fitting candidate; pending user aesthetic review'}
 (R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
files=[p for sex in ['male','female'] for p in (R/sex).glob('*/*.png')]
checks=[]
for j in J:
 p=(R if '--apply' in sys.argv else OUT)/'male'/j['slot']/f'{j["index"]:02}.png';a=np.asarray(Image.open(p));assert a.shape==(1536,1024,4);assert np.all(a[a[:,:,3]==0,:3]==0);assert np.any(a[:,:,3]>230)
 checks.append({'file':str(p.relative_to(R)),'rgba':True,'canvas':[1024,1536],'transparent_rgb_zero':True,'continuous_alpha':bool(np.any((a[:,:,3]>0)&(a[:,:,3]<255))),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
report={'pass':True,'applied':'--apply' in sys.argv,'new_parts':6,'unchanged_existing_parts':len(before),'physical_file_count':len(files),'parts':checks,'new_front_back_mixes':9,'original_front_back_mixes':6,'three_face_trials':9,'geometric_certification':'visual fitting only, no three-point residual certification'}
(S/'asset_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
