from pathlib import Path
from PIL import Image
import numpy as np,json
from collections import deque
R=Path(__file__).resolve().parent.parent;P=R.parents[5]
face=Image.open(P/'mods/appearance/ink_three_variants_20261004/male/face_shape/01.png').convert('RGBA')
out=Image.new('RGBA',face.size,(0,0,0,0))
for layer in [Image.open(R/'male/clothing_back/01.png').convert('RGBA'),face,Image.open(R/'male/clothing/01.png').convert('RGBA')]:out.alpha_composite(layer)
a=np.array(out)[500:750,390:650,3];empty=a<32;h,w=empty.shape;seen=np.zeros_like(empty);holes=[]
for yy in range(h):
 for xx in range(w):
  if not empty[yy,xx] or seen[yy,xx]:continue
  q=deque([(xx,yy)]);seen[yy,xx]=True;pixels=[];touch=False
  while q:
   x,y=q.popleft();pixels.append((x,y));touch|=x in [0,w-1] or y in [0,h-1]
   for nx,ny in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
    if 0<=nx<w and 0<=ny<h and empty[ny,nx] and not seen[ny,nx]:seen[ny,nx]=True;q.append((nx,ny))
  if not touch:holes.append({'pixels':len(pixels),'bbox':[min(x for x,y in pixels)+390,min(y for x,y in pixels)+500,max(x for x,y in pixels)+391,max(y for x,y in pixels)+501]})
assert not holes,holes
report={'fixed_head_alpha_used':True,'collar_crop':[390,500,650,750],'alpha_gap_threshold':32,'enclosed_transparent_joint_holes':holes,'visual_review':'Light and dark composites checked: rear band does not protrude as side triangles or horizontal bar; front lapels continuous into fixed neck. Original source and current head shape are different images, so preview is a garment-interface review only.'}
(R/'qa/joint_check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(report))
