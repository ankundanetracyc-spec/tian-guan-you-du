"""Refine alpha only for original visible hair; remove detached extraction debris."""
from pathlib import Path
import json,cv2
import numpy as np
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parent.parent
B=R.parent
W,H=1024,1536
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True)
 a=np.rint(a).clip(0,255).astype('uint8');a[a[:,:,3]==0,:3]=0
 Image.fromarray(a).save(p)
def native(name):
 a=read(B/'source'/f'{name}_raw.png').astype(float)
 alpha=a[:,:,3:4]/255;p=np.concatenate([a[:,:,:3]*alpha,a[:,:,3:4]],2)
 p=cv2.warpAffine(p,np.array([[587/1020,0,220],[0,587/1020,50]],float),(W,H),flags=cv2.INTER_AREA)
 return np.concatenate([p[:,:,:3]/np.maximum(p[:,:,3:4]/255,1e-8),p[:,:,3:4]],2).clip(0,255).astype('uint8')
def over(d,s):
 d=d.astype(float)/255;s=s.astype(float)/255
 da=d[:,:,3:4];sa=s[:,:,3:4];oa=sa+da*(1-sa)
 rgb=(s[:,:,:3]*sa+d[:,:,:3]*da*(1-sa))/np.maximum(oa,1e-8)
 return np.rint(np.concatenate([rgb,oa],2)*255).clip(0,255).astype('uint8')
def remove_debris(a,keep_count=1,kernel=3):
 alpha=a[:,:,3]
 # A three-pixel opening finds the body of a lock, rather than treating every extraction speck as hair.
 core=cv2.morphologyEx((alpha>=40).astype('uint8'),cv2.MORPH_OPEN,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(kernel,kernel)))
 n,labels,stats,_=cv2.connectedComponentsWithStats(core,8)
 ids=sorted(range(1,n),key=lambda k:stats[k,4],reverse=True)[:keep_count]
 body=np.isin(labels,ids).astype('uint8')
 distance=cv2.distanceTransform(1-body,cv2.DIST_L2,5)
 gate=np.clip((2.1-distance)/1.1,0,1)
 a=a.copy();a[:,:,3]=np.rint(alpha*gate)
 return a
records=[]
for i in range(1,4):
 before=read(R/'before_male/hair_front'/f'{i:02}.png')
 working=before.copy()
 rgb=working[:,:,:3].astype(float);red,green,blue=rgb.transpose(2,0,1)
 # Warm skin/ivory mixed into the inner neck edge is extraction residue, not a hair highlight.
 inner=np.zeros((H,W),np.float32)
 for x0,x1 in [(418,453),(591,626)]:inner[430:599,x0:x1]=1
 inner=cv2.GaussianBlur(inner,(0,0),2)
 hair_chroma=np.clip((30-(red-blue))/10,0,1)*np.clip((16-(red-green))/6,0,1)
 working[:,:,3]=np.rint(working[:,:,3]*(1-inner*(1-hair_chroma)))
 front=remove_debris(working,kernel=5)
 fine=remove_debris(working,kernel=3)
 front[900:]=fine[900:] # Preserve the tapered unseen tips, while cleaning the neck-side cutout.
 # Remove pin fragments and posterior fragments from the anterior crown without trimming its outline.
 if i==1:
  guide=read(B/'source/front_clean01_raw.png')[:,:,3].astype(float)/255
  front[:230,:,3]=np.minimum(front[:230,:,3],np.rint(np.clip((guide[:230]-.3)/.55,0,1)*255))
 save(R/'male/hair_front'/f'{i:02}.png',front)
 ref=read(B/'source'/f'reference_canvas_{i:02}.png')
 rgb=ref[:,:,:3].astype(float);red,green,blue=rgb.transpose(2,0,1);lum=rgb.mean(2)
 hair=np.clip((190-lum)/22,0,1)*(1-np.clip((red-blue-25)/12,0,1))*(1-np.clip((green-red-2)/6,0,1))*ref[:,:,3]/255
 original_front=read(R/'before_male/hair_front'/f'{i:02}.png')[:,:,3]/255
 original_rear=hair*(1-original_front)
 # Below the head, use the clean continuous posterior painting. No garment pixels enter this layer.
 original_rear*=np.clip((570-np.arange(H)[:,None])/40,0,1)
 original_rear[330:430,420:620]=0
 posterior=native(f'back0{i}')
 posterior[:230,:,3]=0
 original_rear[:230]=0
 if i==1:
  region=np.zeros((H,W),np.uint8)
  cv2.fillPoly(region,[np.array([(383,182),(388,163),(434,162),(447,139),(476,123),(531,123),(559,142),(574,164),(626,164),(626,180),(556,186),(442,186)])],255)
  original_rear=np.maximum(original_rear,hair*region/255)
  # Original wood pin is warm brown, and therefore intentionally exempt from the hair-color criterion.
  pin=np.zeros((H,W),np.uint8);cv2.fillPoly(pin,[np.array([(383,166),(443,168),(573,164),(626,166),(625,175),(574,178),(440,178),(383,187)])],255)
  original_rear=np.maximum(original_rear,pin/255*np.clip((205-lum)/35,0,1))
 elif i==2:
  original_rear[80:171]=hair[80:171]
 original=ref.copy();original[:,:,3]=np.rint(original_rear*255)
 rear=over(posterior,original)
 # The crown/bun is a valid second component, kept explicitly instead of preserving arbitrary tiny islands.
 rear=remove_debris(rear,keep_count=2 if i<3 else 1)
 save(R/'male/hair_back'/f'{i:02}.png',rear)
 for part,after in [('hair_front',front),('hair_back',rear)]:
  old=read(R/'before_male'/part/f'{i:02}.png')
  records.append({'part':part,'index':i,'removed_visible_pixels':int(((old[:,:,3]>20)&(after[:,:,3]<=20)).sum()),'front_preserved_rgb':bool(np.array_equal(old[:,:,:3][after[:,:,3]>0],after[:,:,:3][after[:,:,3]>0])) if part=='hair_front' else None})
(R/'qa/hair_cleanup.json').write_text(json.dumps(records,indent=2),encoding='utf8')
for bg,name in [('#273238','dark'),('#e8e1d6','light')]:
 out=Image.new('RGB',(1536,1536),bg);d=ImageDraw.Draw(out)
 for part,row in [('hair_front',0),('hair_back',1)]:
  for i in range(1,4):
   a=Image.open(R/'male'/part/f'{i:02}.png');b=Image.new('RGBA',a.size,bg);b.alpha_composite(a)
   out.paste(b.resize((512,768)),((i-1)*512,row*768));d.text(((i-1)*512+8,row*768+8),f'{part} {i}',fill='white' if name=='dark' else 'black')
 out.save(R/'qa'/f'six_hairs_{name}.png')
print('Six hair layers cleaned; front RGB and common geometry retained')
