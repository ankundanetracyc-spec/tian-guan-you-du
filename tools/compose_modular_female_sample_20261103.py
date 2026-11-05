from pathlib import Path
import json,zipfile,hashlib
from PIL import Image
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_modular_sample_20261103'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']
def compose(hide=(),background=(235,229,217,255)):
 im=Image.new('RGBA',(1024,1536),background)
 for part in ORDER:
  p=O/'parts'/(part+'.png')
  if p.exists() and part not in hide:im.alpha_composite(Image.open(p).convert('RGBA'))
 return im
def run():
 for color,name in [((235,229,217,255),'light'),((44,48,51,255),'dark')]:
  im=compose(background=color)
  im.convert('RGB').save(O/'preview'/f'combined_{name}.jpg',quality=97)
  im.crop((280,65,765,1025)).convert('RGB').save(O/'preview'/f'head_chest_{name}.jpg',quality=97)
  im.crop((330,130,710,580)).convert('RGB').save(O/'preview'/f'face_{name}.jpg',quality=97)
 for part in ['hair_front','hair_back']:
  compose((part,)).crop((280,65,765,1025)).convert('RGB').save(O/'qa'/f'hide_{part}.jpg',quality=96)
 compose(background=(0,0,0,0)).save(O/'preview/combined_transparent.png')
 records=[]
 for p in (O/'parts').glob('*.png'):
  im=Image.open(p);assert im.size==(1024,1536) and im.mode=='RGBA'
  records.append({'file':str(p.relative_to(O)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'canvas':[1024,1536]})
 (O/'manifest.json').write_text(json.dumps({'status':'single_sample_for_user_review_not_installed','records':records,'layer_order':ORDER},ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(O/'女性单套独立部件.zip','w',zipfile.ZIP_DEFLATED) as z:
  for p in (O/'parts').glob('*.png'):z.write(p,p.relative_to(O))
  z.write(O/'manifest.json','manifest.json')
if __name__=='__main__':run()
