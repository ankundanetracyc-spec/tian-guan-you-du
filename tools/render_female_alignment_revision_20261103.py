from pathlib import Path
import json
from PIL import Image
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_rebuild_20261103/alignment_skin_revision'
A=R/'mods/appearance/ink_three_variants_20261004/female'
records=[]
for i in range(1,4):
 for version in ['before','after']:
  canvas=Image.new('RGBA',(1024,1536),(235,229,217,255));sources={}
  for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
   idx=4 if part=='face_shape' else 1 if part=='clothing' else i
   p=A/part/f'{idx:02}.png'
   if version=='after' and part!='clothing':
    choices=[O/'source_extracted'/part/f'{idx:02}.png',O/'female'/part/f'{idx:02}.png']
    p=next((x for x in choices if x.exists()),p)
   sources[part]=str(p.relative_to(R))
   canvas.alpha_composite(Image.open(p).convert('RGBA'))
  canvas.convert('RGB').save(O/f'{version}_{i:02}_portrait.jpg',quality=96)
  canvas.crop((280,35,765,720)).convert('RGB').save(O/f'{version}_{i:02}_head.jpg',quality=97)
  records.append({'variant':i,'version':version,'sources':sources})
(O/'preview_sources.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
