from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib
R=Path(__file__).resolve().parent
P=R.parents[4]
A=P/'mods/appearance/ink_three_variants_20261004/female'
layers=[('hair_back',1),('face_shape',4),('clothing',1),('nose',1),('eyes',1),('eyebrows',1),('mouth',1),('hair_front',1)]
out=Image.new('RGBA',(1024,1536),'#e8e1d6')
records=[]
for part,index in layers:
 p=A/part/f'{index:02}.png';im=Image.open(p).convert('RGBA');assert im.size==(1024,1536)
 out.alpha_composite(im);a=np.array(im);ys,xs=np.where(a[:,:,3]>32)
 records.append({'part':part,'index':index,'source':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'alpha32_bbox':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]})
out.save(R/'female_active.png')
head=np.array(Image.open(A/'face_shape/04.png'))
rows=[]
for y in [280,350,379,451,484,530,550,580]:
 xs=np.where(head[y,:,3]>230)[0]
 rows.append({'y':y,'alpha230_x':[int(xs.min()),int(xs.max())] if len(xs) else None})
(R/'female_reference_manifest.json').write_text(json.dumps({'canvas':[1024,1536],'mode':'Unmodified active female layers alpha-composited on warm plain background','layers':records,'face_rows':rows},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'output':str(R/'female_active.png'),'rows':rows,'layers':[(p,i) for p,i in layers]},ensure_ascii=False))
