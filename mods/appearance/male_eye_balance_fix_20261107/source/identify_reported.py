"""Read-only screenshot-to-active-portrait crop matching."""
from pathlib import Path
from PIL import Image
import cv2,numpy as np,json,itertools
ROOT=Path('E:/stacklands');PACK=ROOT/'mods/appearance/male_eye_balance_fix_20261107'
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004/male'
ROI=(280,280,850,430)
image=lambda part,i:Image.open(ACTIVE/part/f'{i:02}.png').convert('RGBA').crop(ROI)
layers={(part,i):image(part,i) for part in ('hair_front','hair_back','eyes','eyebrows') for i in (1,4,5)}
fixed={part:image(part,1) for part in ('face_shape','nose','mouth')}
target=np.array(Image.open(PACK/'reference/reported_eye_crop.png').convert('RGB'))
h,w=target.shape[:2];results=[]
for front,back,brow,eye in itertools.product((1,4,5),repeat=4):
 out=Image.new('RGBA',(570,150),'#c9caba')
 for layer in (layers['hair_back',back],fixed['face_shape'],fixed['nose'],layers['eyes',eye],layers['eyebrows',brow],fixed['mouth'],layers['hair_front',front]):out.alpha_composite(layer)
 rgb=np.array(out.convert('RGB'));best=None
 for scale in np.arange(.400,.806,.005):
  scaled=cv2.resize(rgb,None,fx=float(scale),fy=float(scale),interpolation=cv2.INTER_LINEAR)
  if scaled.shape[1]<w or scaled.shape[0]<h:continue
  match=cv2.matchTemplate(scaled,target,cv2.TM_SQDIFF_NORMED);low,_,pos,_=cv2.minMaxLoc(match)
  if best is None or low<best['score']:
   best={'score':float(low),'scale':float(scale),'crop_top_left_in_scaled_roi':list(pos),'parts':{'hair_front':front,'hair_back':back,'eyebrows':brow,'eyes':eye}}
 results.append(best)
results.sort(key=lambda v:v['score']);best=results[0]
(PACK/'qa/screenshot_identification.json').write_text(json.dumps({'method':'Active81 male combinations; native RGBA ROI x280..850,y280..430, bilinear display scales .400..805; all offsets searched','reported_crop_size':[w,h],'ranked_matches':results[:12]},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(results[:4],ensure_ascii=False))
