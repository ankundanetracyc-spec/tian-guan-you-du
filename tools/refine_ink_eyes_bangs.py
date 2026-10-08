"""Install the user-requested small-eye and fringe redraws in the trial set."""
import json, hashlib, shutil
import numpy as np
from PIL import Image, ImageDraw
import build_ink_three_variants as b

SOURCE=b.R/'source/eyes_bangs_revision'
SPECS={
 1:((423,490),[(259,477),(324,460),(408,458),(487,475),(550,512),(595,548),(479,554),(361,550),(300,526)]),
 2:((430,539),[(269,533),(345,512),(431,510),(519,534),(572,567),(600,587),(485,593),(368,588),(308,566)]),
 3:((443,508),[(303,500),(366,480),(443,484),(515,510),(566,542),(603,566),(490,557),(394,552),(335,530)]),
}

def apply_revision():
 records=[]
 for v,(pupil,poly) in SPECS.items():
  src=SOURCE/f'female_eyes_{v}_small.png';a=b.read(src);h,w=a.shape[:2]
  assert (w,h)==(1536,1024)
  core=Image.new('L',(w,h));d=ImageDraw.Draw(core);d.polygon(poly,fill=255);d.polygon([(w-x,y) for x,y in poly],fill=255)
  a=b.pigment(a,np.array(core)>0,.7)
  px,py=pupil;t=b.paired_transform(pupil,(w-px,py),(464,379),(574,379))
  yy,xx=np.indices((h,w));iris=(((xx-px)/55)**2+((yy-py)/60)**2<1)|(((xx-(w-px))/55)**2+((yy-py)/60)**2<1)
  bright=iris&(a[:,:,:3].min(2)>190);a[:,:,:3][bright]=[191,179,164]
  dst=b.R/'female/eyes'/f'{v:02}.png';b.save(dst,b.place(a,t))
  records.append({'file':str(dst.relative_to(b.R)),'source':str(src.relative_to(b.R)),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'scale':t[0],'translation':list(t[1:]),'pupil_targets':[[464,379],[574,379]],'core_width_px':round((max(x for x,y in poly)-min(x for x,y in poly))*t[0],1)})
 for sex,t in [('male',(.40,312,97)),('female',(.47,278,80))]:
  if (b.R/'source/ethereal_revision/user_face_reference.png').exists():
   shutil.copy2(SOURCE/f'before_{sex}_front_2.png',b.R/sex/'hair_front/02.png')
   continue
  src=SOURCE/f'{sex}_front_bangs.png';a=b.hair_tone(b.read(src));dst=b.R/sex/'hair_front/02.png';b.save(dst,b.place(a,t))
  records.append({'file':str(dst.relative_to(b.R)),'source':str(src.relative_to(b.R)),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'scale':t[0],'translation':list(t[1:]),'style':'forehead-covering broken fringe'})
 manifest=json.loads((b.R/'portrait_manifest.json').read_text(encoding='utf-8'));manifest['latest_revision']={'id':'small_eyes_and_bangs','generation_tool':'built-in imagegen','records':records,'visual_approved_by_user':False}
 changed={r['file'] for r in records}
 for old in manifest['registrations']:
  if old['file'] in changed:old['superseded_by']='latest_revision.records'
 (b.R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 (SOURCE/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

def revision_previews():
 before=Image.open(SOURCE/'before_faces.png');board=Image.new('RGB',(1536,980),'#e8e3dc');board.paste(before.crop((0,490,1536,980)),(0,0))
 for v in [1,2,3]:
  # Same previous hairstyle in all comparison pairs isolates the eye change.
  vs=[v]*9;im=b.compose('female',vs)
  if v==2:
   full=Image.new('RGBA',b.SIZE)
   for slot in b.ORDER:
    path=SOURCE/'before_female_front_2.png' if slot=='hair_front' else b.R/'female'/slot/f'{v:02}.png'
    full.alpha_composite(Image.open(path).convert('RGBA'))
   im=Image.new('RGBA',b.SIZE,'#e8e3dc');im.alpha_composite(full);im=im.convert('RGB')
  board.paste(im.crop((340,240,700,565)).resize((504,455)),((v-1)*512,522))
  ImageDraw.Draw(board).text(((v-1)*512+12,492),f'缩小后 · 第 {v} 款',font=b.FONT,fill='#352f2b')
 board.save(b.R/'preview/female_eyes_before_after.png')
 pair=Image.new('RGB',(1200,920),'#e8e3dc')
 for i,sex in enumerate(['male','female']):
  vs=[1]*9;vs[0]=2;im=b.compose(sex,vs);pair.paste(im.crop((210,75,825,975)).resize((600,878)),(i*600,40));ImageDraw.Draw(pair).text((i*600+20,8),f'{"男" if sex=="male" else "女"} · 第二款前发 / 碎刘海',font=b.FONT,fill='#352f2b')
 pair.save(b.R/'preview/bangs_pair.png')
 board=Image.new('RGB',(1200,2*3*330),'#e8e3dc')
 for si,sex in enumerate(['male','female']):
  for face in range(3):
   for rear in range(3):
    vs=[1]*9;vs[0]=2;vs[1]=rear+1;vs[6]=face+1;im=b.compose(sex,vs).crop((330,70,710,560));im.thumbnail((390,290));x=rear*400;y=(si*3+face)*330;board.paste(im,(x,y+35));ImageDraw.Draw(board).text((x+8,y+4),f'{sex} face {face+1} rear {rear+1}',font=b.FONT,fill='#352f2b')
 board.save(b.R/'preview/bangs_mix_check.jpg',quality=93)

if __name__=='__main__':
 for sex in ['male','female']:
  backup=SOURCE/f'before_{sex}_front_2.png'
  if not backup.exists():shutil.copy2(b.R/sex/'hair_front/02.png',backup)
 for v in [1,2,3]:
  backup=SOURCE/f'before_female_eyes_{v}.png'
  if not backup.exists():shutil.copy2(b.R/'female/eyes'/f'{v:02}.png',backup)
 apply_revision();b.previews();revision_previews();print('Updated three female eye pairs and male/female front hair 02.')
