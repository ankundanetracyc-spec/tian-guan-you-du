from pathlib import Path
import json,hashlib,zipfile,shutil
from PIL import Image
R=Path(__file__).resolve().parents[1]
A=R/'mods/appearance/ink_three_variants_20261004'
O=R/'mods/appearance/portrait_rebuild_20261103/alignment_skin_revision'
P=R/'mods/appearance/portrait_rebuild_20261103/three_style_parts'
files=[('face_shape',4)]+[(p,i) for p in ['eyebrows','eyes','nose','mouth','hair_front','hair_back'] for i in range(1,4)]
inputs=[]
for part,i in files:
 src=O/'source_extracted'/part/f'{i:02}.png' if i==1 and part.startswith('hair') else O/'female'/part/f'{i:02}.png'
 im=Image.open(src);assert im.size==(1024,1536) and im.mode=='RGBA',str(src)
 inputs.append((part,i,src))
backup=O/'before_install.zip'
if not backup.exists():
 with zipfile.ZipFile(backup,'w',zipfile.ZIP_DEFLATED) as z:
  for part,i,src in inputs:z.write(A/'female'/part/f'{i:02}.png',f'female/{part}/{i:02}.png')
  z.write(A/'portrait_manifest.json','portrait_manifest.json')
records=[]
for part,i,src in inputs:
 dst=A/'female'/part/f'{i:02}.png';shutil.copyfile(src,dst)
 if part!='face_shape':shutil.copyfile(src,P/'female'/part/f'{i:02}.png')
 records.append({'part':part,'index':i,'source':str(src.relative_to(R)),'sha256':hashlib.sha256(dst.read_bytes()).hexdigest()})
m=json.loads((A/'portrait_manifest.json').read_text(encoding='utf-8'))
m['female_reference_correction_20261103']={'skin':'reference skin matched, alpha/face shape unchanged','hair01':'reference pixel extraction, hidden back fill','hair02_03':'uniform geometry correction; still generated shapes','record':str(O.relative_to(R))}
(A/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(O/'installed_assets.json').write_text(json.dumps({'count':len(inputs),'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
print('Installed',len(inputs),'revised female layers; IDs unchanged.')
