"""Uniform registration/export only; generated artwork is not repainted here."""
from pathlib import Path
import json
import hashlib
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parent
PACKAGE = ROOT.parent.parent
PROJECT = PACKAGE.parents[2]
ACTIVE = PROJECT / 'mods/appearance/ink_three_variants_20261004'
FINAL = PACKAGE / 'final/female'
SIZE = (1024,1536)
PARAMS = {
 ('hair_front',1): ('hair_front_01_generated.png',.60,(524,366),(518,257)),
 ('hair_front',2): ('hair_front_02_generated.png',.57,(526,373),(518,257)),
 ('hair_front',3): ('hair_front_03_generated.png',.59,(519,361),(518,257)),
 ('hair_back',1): ('hair_back_01_tips_repaired.png',.64,(512,72),(518,50)),
 ('hair_back',2): ('hair_back_02_generated.png',.56,(514,84),(518,97)),
 ('hair_back',3): ('hair_back_03_generated.png',.60,(510,99),(518,100)),
}

def run():
 records=[]
 for (part,index),(name,scale,source,target) in PARAMS.items():
  path=ROOT/name
  image=Image.open(path).convert('RGBA')
  dx=target[0]-source[0]*scale;dy=target[1]-source[1]*scale
  # Premultiplied-alpha affine transform avoids color fringes in transparent pixels.
  result=image.convert('RGBa').transform(SIZE,Image.Transform.AFFINE,(1/scale,0,-dx/scale,0,1/scale,-dy/scale),resample=Image.Resampling.BICUBIC).convert('RGBA')
  out=FINAL/part/f'{index:02}.png';out.parent.mkdir(parents=True,exist_ok=True);result.save(out)
  a=np.asarray(result)
  ys,xs=np.where(a[:,:,3]>=16)
  records.append({'part':part,'variant':index,'source':str(path),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'uniform_scale':scale,'translation':[dx,dy],'source_control':source,'target_control':target,'output':str(out),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'visible_bbox':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]})
 (ROOT/'registration.json').write_text(json.dumps({'canvas':SIZE,'method':'Single uniform scale and translation per complete layer; premultiplied-alpha affine sampling. No local warp, drawing, color changes, or pixel erasure.','records':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 # The following composites are inspection-only. Pending features use the current active first choice and are labeled as such.
 config=json.loads((ACTIVE/'appearance_parts.json').read_text(encoding='utf-8'))
 preview=ROOT/'registered_review';preview.mkdir(exist_ok=True)
 def get(part,index):
  if part in ('hair_front','hair_back'):
   return Image.open(FINAL/part/f'{index:02}.png').convert('RGBA')
  new=FINAL/part/f'{index:02}.png'
  if new.exists():return Image.open(new).convert('RGBA')
  entry=next(e for e in config['components'] if e['gender']=='female' and f'/{part}/' in e['file'])
  physical=entry.get('variant_indices',[1])[0]
  return Image.open(ACTIVE/'female'/part/f'{physical:02}.png').convert('RGBA')
 def composite(front,back,hide=()):
  out=Image.new('RGBA',SIZE)
  for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']:
   if part not in hide:out.alpha_composite(get(part,back if part=='hair_back' else front))
  bg=Image.new('RGBA',SIZE,(232,227,218,255));bg.alpha_composite(out)
  return bg.convert('RGB')
 for front in range(1,4):
  for back in range(1,4):
   im=composite(front,back);im.save(preview/f'front_{front:02}_back_{back:02}.jpg',quality=94)
   if front==back:
    im.crop((280,40,760,680)).save(preview/f'set_{front:02}_head.jpg',quality=96)
    for hide in ['hair_front','hair_back']:
     composite(front,back,(hide,)).crop((280,40,760,680)).save(preview/f'set_{front:02}_hide_{hide}.jpg',quality=94)
 print(json.dumps(records,ensure_ascii=False))

if __name__=='__main__':run()
