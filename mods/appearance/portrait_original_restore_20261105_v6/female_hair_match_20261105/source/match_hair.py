"""Local temple correction and female-reference highlight matching; fixed canvas."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,cv2,json
R=Path(__file__).resolve().parent.parent
A=R.parent.parent/'ink_three_variants_20261004'
W,H=1024,1536;L=np.array([.2126,.7152,.0722])
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True);a=np.rint(a).clip(0,255).astype('uint8');a[a[:,:,3]==0,:3]=0;Image.fromarray(a).save(p)
def shape(points,feather):
 a=np.zeros((H,W),np.float32);cv2.fillPoly(a,[np.array(points,np.int32)],1)
 return cv2.GaussianBlur(a,(0,0),feather)
def blend(a,b,m):
 a=a.astype(float);b=b.astype(float);aa=a[:,:,3:4]/255;ba=b[:,:,3:4]/255;w=m[:,:,None]
 alpha=aa*(1-w)+ba*w
 rgb=(a[:,:,:3]*aa*(1-w)+b[:,:,:3]*ba*w)/np.maximum(alpha,1e-9)
 return np.concatenate([rgb,alpha*255],2)
def sample_mask(a,crown=False):
 m=np.zeros((H,W),np.uint8)
 if crown:m[185:320,365:670]=1
 else:m[190:590,345:690]=1
 return (cv2.erode((a[:,:,3]>240).astype('uint8'),np.ones((11,11),np.uint8))*m)>0
def base_luma(a):
 rgb=a[:,:,:3].astype(float);y=rgb@L;alpha=a[:,:,3].astype(float)/255
 base=cv2.GaussianBlur(y*alpha,(0,0),3)/np.maximum(cv2.GaussianBlur(alpha,(0,0),3),1e-6)
 return rgb,y,base
def micro(a):
 _,y,b=base_luma(a);m=sample_mask(a,True)
 return float(np.percentile(np.maximum(y[m]-b[m],0)/np.maximum(b[m],15),95))
records=[]
for part in ['hair_front','hair_back']:
 female=[read(A/'female'/part/f'{i:02}.png') for i in range(1,4)]
 # Front styles have intentionally different contours; their shared medium is the target.
 target_micro=float(np.median([micro(a) for a in female]))
 reference_rgb=np.concatenate([a[:,:,:3][sample_mask(a)] for a in female]).astype(float)
 ref_y=reference_rgb@L;ref_chroma=np.median(reference_rgb-ref_y[:,None],axis=0)
 for i in range(1,4):
  original=read(R/'before_male'/part/f'{i:02}.png');a=original.copy()
  if part=='hair_front' and i==3:
   native=read(R/'source/front03_temple_raw.png');assert native.shape==a.shape
   # Only the ear-side problem is selected, retaining the original crown, long locks and all anchors elsewhere.
   region=shape([(590,294),(672,286),(703,343),(697,431),(675,473),(619,470),(592,415)],8)
   # Match the local generated colors to its source before the common reference adjustment.
   common=(region>.1)&(region<.65)&(native[:,:,3]>240)&(a[:,:,3]>240)
   delta=np.median(a[:,:,:3][common].astype(float)-native[:,:,:3][common].astype(float),axis=0)
   native[:,:,:3]=np.rint(native[:,:,:3].astype(float)+delta).clip(0,255)
   a=blend(a,native,region).round().clip(0,255).astype('uint8')
  rgb,y,base=base_luma(a);mask=sample_mask(a)
  before_micro=micro(a)
  strength=min(1.,target_micro/max(before_micro,1e-6))
  # Suppress excessive bright and dark micro-lines together, keeping the diffuse hair mass.
  strength=max(.55,strength)
  ynew=base+strength*(y-base)
  median_shift=float(np.clip(np.median(ref_y)-np.median(y[mask]),-3,3))
  ynew+=median_shift
  own_chroma=np.median(rgb[mask]-y[mask,None],axis=0)
  delta_chroma=np.clip(ref_chroma-own_chroma,-3,3);delta_chroma-=delta_chroma@L
  newrgb=rgb+(ynew-y)[:,:,None]+delta_chroma
  # Wooden pin and black tie retain their existing paint.
  preserve=np.zeros((H,W),np.float32)
  if part=='hair_back' and i==1:preserve[155:188,378:632]=1
  if part=='hair_back' and i==2:preserve[124:172,445:575]=1
  newrgb=newrgb*(1-preserve[:,:,None])+original[:,:,:3]*preserve[:,:,None]
  out=a.copy();out[:,:,:3]=np.rint(newrgb).clip(0,255)
  save(R/'male'/part/f'{i:02}.png',out)
  records.append({'part':part,'index':i,'canvas':[W,H],'rescaled':False,'alpha_changed_pixels':int((out[:,:,3]!=original[:,:,3]).sum()),'highlight_ratio_before':before_micro,'female_target_ratio':target_micro,'micro_contrast_factor':strength,'highlight_ratio_after':micro(out),'median_body_shift':median_shift,'chroma_delta':delta_chroma.tolist()})
(R/'qa/hair_match_report.json').write_text(json.dumps(records,indent=2),encoding='utf8')
# Comparison uses source-scale crops, so color and line-width differences are visible without changing the assets.
for part in ['hair_front','hair_back']:
 out=Image.new('RGB',(1200,1260),'#e8e1d6')
 for row,source in enumerate(['female','before','after']):
  for i in range(1,4):
   p=A/'female'/part/f'{i:02}.png' if source=='female' else R/('before_male' if source=='before' else 'male')/part/f'{i:02}.png'
   a=Image.open(p);b=Image.new('RGBA',a.size,'#e8e1d6');b.alpha_composite(a);out.paste(b.crop((330,170,710,560)).resize((400,411)),((i-1)*400,row*420))
 out.save(R/'qa'/f'{part}_female_before_after.png')
print('Six hair layers matched to female highlight/chroma reference; third temple selected locally')
