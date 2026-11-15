"""User-authorized pixel extraction; preserve source pigment relative to skin."""
from pathlib import Path
import json, hashlib
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'mods/appearance/portrait_rebuild_20261103/single_style_sample'
SRC=ROOT/'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_01.png'
rgb=np.array(Image.open(SRC).convert('RGB'))
# One nose's ownership region in the approved left portrait; surrounding skin
# estimates the removed skin base. No generative drawing or recoloring the face.
region=rgb[344:426,270:356].copy()
mask=np.zeros(region.shape[:2],np.uint8)
points=np.array([(303,355),(321,355),(326,381),(337,394),(334,409),(325,417),(308,417),(292,409),(286,398),(297,380)])-[270,344]
cv2.fillPoly(mask,[points],255)
under=cv2.inpaint(region,mask,7,cv2.INPAINT_TELEA).astype(np.float32)
under=cv2.GaussianBlur(under,(0,0),3)
distance=cv2.distanceTransform(mask,cv2.DIST_L2,5)
weight=np.clip(distance/5,0,1);weight=weight*weight*(3-2*weight)
delta=(region.astype(np.float32)-under)*weight[:,:,None]
scale=1.0
matrix=np.array([[scale,0,519-312+270],[0,scale,446-398+344]],np.float32)
paint=cv2.warpAffine(delta,matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)
face=np.array(Image.open(OUT/'parts/face_shape.png').convert('RGBA'))
base=face[:,:,:3].astype(np.float32)
target=np.clip(base+paint,0,255)
d=target-base
denom=np.where(d<0,np.maximum(base,1),np.maximum(255-base,1))
alpha=np.max(np.abs(d)/denom,axis=2)
alpha=np.ceil(np.clip(alpha*1.15,0,1)*255)/255
alpha[np.max(np.abs(paint),axis=2)<.8]=0
foreground=(target-(1-alpha[:,:,None])*base)/np.maximum(alpha[:,:,None],1/255)
result=np.zeros((1536,1024,4),np.uint8)
result[:,:,:3]=np.rint(np.clip(foreground,0,255));result[:,:,3]=np.rint(alpha*255)
result[alpha==0]=0
Image.fromarray(result).save(OUT/'parts/nose.png')
canvas=Image.new('RGBA',(1024,1536),(235,229,217,255))
for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
    p=OUT/'parts'/(part+'.png')
    if not p.exists():p=ROOT/'mods/appearance/ink_three_variants_20261004/female'/part/'01.png'
    canvas.alpha_composite(Image.open(p).convert('RGBA'))
canvas.convert('RGB').save(OUT/'portrait.jpg',quality=96)
canvas.crop((300,35,745,660)).convert('RGB').save(OUT/'face.jpg',quality=97)
record={'method':'source-pixel local pigment extraction against estimated source skin; composite onto unchanged original face','user_authorized':True,'source':str(SRC),'source_sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),'source_nose_tip':[312,398],'target_nose_tip':[519,446],'scale':scale,'canvas':[1024,1536],'opaque_pixels':int((result[:,:,3]>=240).sum()),'limitations':'Underlying source skin is estimated from surrounding skin; not a claim of whole-image pixel identity.'}
(OUT/'revisions/nose_pixel_extraction.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(record,ensure_ascii=False))
