"""Remove hair imported into non-hair layers; keep the fixed canvas and neck alpha."""
from pathlib import Path
import hashlib,json
import cv2
import numpy as np
from scipy.ndimage import distance_transform_edt
from PIL import Image,ImageDraw

B=Path(__file__).resolve().parent.parent
W,H=1024,1536
X,Y=np.meshgrid(np.arange(W),np.arange(H))
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True)
 a=np.clip(np.rint(a),0,255).astype('uint8');a[a[:,:,3]==0,:3]=0
 Image.fromarray(a,'RGBA').save(p)
def poly(points,feather=.65):
 a=np.zeros((H,W),np.uint8);cv2.fillPoly(a,[np.array(points,np.int32)],255)
 if feather:a=cv2.GaussianBlur(a,(0,0),feather)
 return a.astype(float)/255
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
records=[]
# Only the peripheral ends use these masks. The original main brow/eye pixels stay untouched.
brows={
 1:([[(435,350),(443,347),(452,345),(460,346),(460,356),(447,354),(439,353)],[(578,345),(587,346),(596,349),(602,352),(595,353),(583,355),(578,356)]],450,583),
 2:([[(432,349),(440,347),(451,348),(458,351),(458,359),(445,355),(435,352)],[(578,347),(587,347),(596,349),(600,350),(590,353),(578,357)]],442,585),
 3:([[(437,351),(448,347),(459,347),(465,349),(465,355),(452,354),(440,353)],[(578,347),(587,349),(595,351),(600,354),(590,354),(578,354)]],459,585)
}
eyes={
 1:([[(434,376),(441,373),(448,371),(455,370),(455,394),(445,389),(438,382)],[(582,369),(590,371),(599,375),(592,382),(582,391)]],446,584),
 2:([[(433,378),(442,374),(451,370),(460,370),(460,394),(448,391),(441,386)],[(582,369),(590,372),(599,376),(592,382),(582,391)]],454,584)
}
for part,configs in [('eyebrows',brows),('eyes',eyes)]:
 for i,(shapes,left,right) in configs.items():
  before=B/'before_male'/part/f'{i:02}.png';a=read(before);old=a.copy()
  keep=np.maximum(poly(shapes[0]),poly(shapes[1]))
  # A short inward transition avoids a rectangular seam through the skin surround.
  edge=np.maximum(np.clip((left+3-X)/3,0,1),np.clip((X-right+3)/3,0,1))
  newalpha=np.rint(a[:,:,3]*(1-edge*(1-keep))).astype('uint8')
  a[:,:,3]=newalpha
  assert np.array_equal(a[:,:,:3],old[:,:,:3])
  center=((X>=left+3)&(X<=right-3))
  assert np.array_equal(a[center],old[center])
  output=B/'male'/part/f'{i:02}.png';save(output,a)
  records.append({'part':part,'index':i,'before_sha256':sha(before),'after_sha256':sha(output),'rgb_changed':0,'alpha_changed':int(np.count_nonzero(newalpha!=old[:,:,3])),'central_rgba_unchanged':True,'peripheral_x_limits':[left,right]})

before=B/'before_male/face_shape/01.png';a=read(before);old=a.copy()
masks=[
 poly([(419,446),(427,448),(433,464),(437,487),(439,505),(430,501),(422,479)],1),
 poly([(598,480),(615,480),(615,518),(598,518)],1),
 poly([(422,570),(436,570),(436,603),(422,603)],1),
 poly([(500,630),(595,630),(595,740),(500,740)],2),
]
mask=np.maximum.reduce(masks)
rgb=a[:,:,:3].copy().astype(float)
skin=(a[:,:,3]>230)&(mask<.001)&(rgb[:,:,0]-rgb[:,:,1]>12)&(rgb[:,:,0]-rgb[:,:,2]>30)&(rgb.mean(2)>185)
_,indices=distance_transform_edt(~skin,return_indices=True)
nearest=rgb[indices[0],indices[1]]
# Fill the invisible surroundings before local repair, so transparent black cannot bleed in.
working=np.where((a[:,:,3]>230)[:,:,None],rgb,nearest).astype('uint8')
hard=(mask>.01).astype('uint8')*255
working=cv2.inpaint(working,hard,7,cv2.INPAINT_TELEA)
# The two edge-most zones have no real exterior skin neighbours; use interior skin there.
for zone in [masks[0],masks[2],masks[3]]:
 working=np.rint(working*(1-zone[:,:,None])+nearest*zone[:,:,None]).astype('uint8')
a[:,:,:3]=np.rint(rgb*(1-mask[:,:,None])+working*mask[:,:,None])
assert np.array_equal(a[:,:,3],old[:,:,3])
output=B/'male/face_shape/01.png';save(output,a)
records.append({'part':'face_shape','index':1,'before_sha256':sha(before),'after_sha256':sha(output),'alpha_changed':0,'rgb_changed':int(np.count_nonzero(np.any(a[:,:,:3]!=old[:,:,:3],axis=2))),'neck_alpha_and_canvas_unchanged':True,'repair_regions':[[419,446,439,505],[598,480,615,518],[422,570,436,603],[500,630,595,740]]})

Q=B/'qa';Q.mkdir(exist_ok=True)
for part,i in [('face_shape',1),('eyebrows',1),('eyebrows',2),('eyebrows',3),('eyes',1),('eyes',2)]:
 region=(400,430,635,740) if part=='face_shape' else (420,330,620,410)
 for label,color in [('light','#eee5d9'),('dark','#18303c')]:
  images=[]
  for folder in ['before_male','male']:
   asset=Image.open(B/folder/part/f'{i:02}.png').convert('RGBA');bg=Image.new('RGBA',asset.size,color);bg.alpha_composite(asset)
   bg=bg.crop(region).resize(((region[2]-region[0])*3,(region[3]-region[1])*3))
   images.append(bg)
  out=Image.new('RGBA',(images[0].width*2,images[0].height+24),color)
  for j,img in enumerate(images):out.paste(img,(j*img.width,24))
  draw=ImageDraw.Draw(out);draw.text((8,5),'BEFORE',fill='red');draw.text((images[0].width+8,5),'CLEAN',fill='red')
  out.save(Q/f'feature_{part}_{i:02}_{label}.png')
(Q/'feature_cleanup_report.json').write_text(json.dumps({'canvas':[W,H],'unchanged_geometry':True,'records':records},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(records,ensure_ascii=False))
