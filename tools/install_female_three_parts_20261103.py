from pathlib import Path
import json,shutil,zipfile,hashlib
from PIL import Image
R=Path(__file__).resolve().parents[1]
A=R/'mods/appearance/ink_three_variants_20261004'
S=R/'mods/appearance/portrait_rebuild_20261103/three_style_parts'
Q=S/'installation';Q.mkdir(exist_ok=True)
names={'hair_front':['清簪垂鬓','素挽分鬓','编云垂鬓'],'hair_back':['清簪半束','素挽低髻','编云长披'],'eyebrows':['清簪柔眉','素挽弯眉','编云秀眉'],'eyes':['清簪杏目','素挽圆目','编云柔目'],'nose':['清簪秀鼻','素挽柔鼻','编云小鼻'],'mouth':['清簪浅唇','素挽柔唇','编云淡唇']}
for part in names:
 for i in range(1,4):
  im=Image.open(S/'female'/part/f'{i:02}.png');assert im.size==(1024,1536) and im.mode=='RGBA'
backup=Q/'before_install.zip'
if not backup.exists():
 with zipfile.ZipFile(backup,'w',zipfile.ZIP_DEFLATED) as z:
  for part in names:
   for p in (A/'female'/part).glob('*'):z.write(p,p.relative_to(A))
  for f in ['appearance_parts.json','portrait_manifest.json']:z.write(A/f,f)
config=json.loads((A/'appearance_parts.json').read_text(encoding='utf-8'))
for entry in config['components']:
 if entry['gender']!='female':continue
 part=entry['file'].split('/')[1]
 if part not in names:continue
 entry.update(variant_count=3,variant_names=names[part],variant_indices=[1,2,3],variant_style_ids=[0,1,2])
 for i in range(1,4):shutil.copyfile(S/'female'/part/f'{i:02}.png',A/'female'/part/f'{i:02}.png')
(A/'appearance_parts.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
m=json.loads((A/'portrait_manifest.json').read_text(encoding='utf-8'))
for gender in ['male','female']:
 m['variants_per_slot'][gender]=[next(e['variant_count'] for e in config['components'] if e['gender']==gender and '/'+slot+'/' in e['file']) for slot in m['slots']]
total=sum(e['variant_count'] for e in config['components']);m['total_pngs']=total;m['active_variant_count']=total
m['female_three_replacement_20261103']={'parts':list(names),'variants_per_part':3,'source':str(S.relative_to(R)),'face':'female/face_shape/04.png','legacy_styles':'0..2 now use new styles; unavailable IDs use existing deterministic catalog fallback'}
(A/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
records=[{'part':p,'index':i,'sha256':hashlib.sha256((A/'female'/p/f'{i:02}.png').read_bytes()).hexdigest()} for p in names for i in range(1,4)]
(Q/'installed_assets.json').write_text(json.dumps({'count':18,'active_count':total,'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
print('Installed 18 assets; active options',total)
