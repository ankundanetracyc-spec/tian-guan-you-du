"""Local resampling of original hair pixels; no whole-layer movement."""
from pathlib import Path
import json,hashlib
import cv2
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]/'mods/appearance/portrait_rebuild_20261103/single_style_sample'
source=ROOT/'revisions/hair_front_before_hairline.png'
im=np.array(Image.open(source).convert('RGBA'))
y,x=np.indices(im.shape[:2],dtype=np.float32)
def smooth(a,b,v):
    t=np.clip((v-a)/(b-a),0,1);return t*t*(3-2*t)
# The crown (above175), outer silhouette, ear roots and chest locks are fixed.
vertical=smooth(175,245,y)*(1-smooth(285,355,y))
horizontal=1-smooth(80,155,np.abs(x-518))
displacement=22*vertical*horizontal
alpha=im[:,:,3].astype(np.float32)/255
premul=im[:,:,:3].astype(np.float32)*alpha[:,:,None]
newa=cv2.remap(alpha,x,y-displacement,cv2.INTER_CUBIC,borderMode=cv2.BORDER_CONSTANT)
newrgb=cv2.remap(premul,x,y-displacement,cv2.INTER_CUBIC,borderMode=cv2.BORDER_CONSTANT)
out=im.copy();changed=displacement>.001
newa=np.clip(newa,0,1)
out[changed,:3]=np.rint(np.clip(newrgb/np.maximum(newa[:,:,None],1/255),0,255))[changed]
out[changed,3]=np.rint(newa*255)[changed]
out[out[:,:,3]==0,:3]=0
Image.fromarray(out).save(ROOT/'parts/hair_front.png')
def edge(arr):
    ys=np.where(arr[180:340,516:521,3].mean(1)>128)[0];return int(ys.max()+180)
record={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'method':'Localized existing-pixel resampling near inner forehead edge; no whole-layer scale or translation','canvas':[1024,1536],'center_hairline_before_y':edge(im),'center_hairline_after_y':edge(out),'displacement_max_px':22,'fixed_regions':'crown above175, below355 including ear roots and all chest hair; face and features untouched','rejected_generation':'exec-0ba80a20-3cd5-4545-b38e-310ba34ecf84.png scaled whole hairstyle, not used'}
(ROOT/'revisions/hairline_lowering.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(record,ensure_ascii=False))
