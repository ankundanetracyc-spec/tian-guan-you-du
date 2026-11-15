from pathlib import Path
import hashlib,json,shutil,zipfile
from PIL import Image
R=Path(__file__).resolve().parents[1]
A=R/'mods/appearance/ink_three_variants_20261004'
S=R/'mods/appearance/clothing_polish_20261103'
Q=S/'installation';Q.mkdir(exist_ok=True)
files=[Path(g)/'clothing'/f'{i:02}.png' for g in ['male','female'] for i in range(1,8)]
for rel in files:
 im=Image.open(S/'final'/rel);assert im.size==(1024,1536) and im.mode=='RGBA'
config_hash=hashlib.sha256((A/'appearance_parts.json').read_bytes()).hexdigest()
backup=Q/'original_clothes.zip'
if not backup.exists():
 with zipfile.ZipFile(backup,'w',zipfile.ZIP_DEFLATED) as z:
  for rel in files:z.write(A/rel,rel)
  z.write(A/'portrait_manifest.json','portrait_manifest.json')
for rel in files:shutil.copyfile(S/'final'/rel,A/rel)
m=json.loads((A/'portrait_manifest.json').read_text(encoding='utf-8'))
m['clothing_polish_installed_20261103']={'count':14,'source':str(S.relative_to(R)),'canvas':[1024,1536],'style_ids_and_item_paths':'unchanged'}
(A/'portrait_manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert config_hash==hashlib.sha256((A/'appearance_parts.json').read_bytes()).hexdigest()
records=[]
for rel in files:
 assert (A/rel).read_bytes()==(S/'final'/rel).read_bytes()
 records.append({'file':str(rel).replace('\\','/'),'sha256':hashlib.sha256((A/rel).read_bytes()).hexdigest()})
(Q/'installed_assets.json').write_text(json.dumps({'count':14,'appearance_config_unchanged':True,'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
print('14 clothes replaced; appearance config and equipment mappings unchanged.')
