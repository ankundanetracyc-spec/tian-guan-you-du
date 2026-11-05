"""User-authorized original-pixel separation; fixed common canvas, no feature scaling."""
from pathlib import Path
import json, shutil
import cv2
import numpy as np
from PIL import Image
B=Path(__file__).resolve().parent.parent
ACTIVE=B.parent/'ink_three_variants_20261004'
W,H=1024,1536
def read(p): return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True); a=np.clip(np.rint(a),0,255).astype('uint8');a[a[:,:,3]==0,:3]=0;Image.fromarray(a,'RGBA').save(p)
def poly(points,feather=0):
 a=np.zeros((H,W),np.uint8);cv2.fillPoly(a,[np.array(points,np.int32)],255)
 if feather:a=cv2.GaussianBlur(a,(0,0),feather)
 return a.astype(float)/255
def original_layer(ref,a):
 out=ref.copy();out[:,:,3]=np.rint(a*255);return out
def over(dst,src):
 d=dst.astype(float)/255;s=src.astype(float)/255;da=d[:,:,3:4];sa=s[:,:,3:4];oa=sa+da*(1-sa)
 rgb=(s[:,:,:3]*sa+d[:,:,:3]*da*(1-sa))/np.maximum(oa,1e-10)
 return np.clip(np.rint(np.concatenate([rgb,oa],2)*255),0,255).astype('uint8')
def native_place(name,scale=587/1020,tx=220,ty=50):
 src=read(B/'source'/f'{name}_raw.png');mat=np.array([[scale,0,tx],[0,scale,ty]],float)
 # Premultiplied whole-layer resampling, only for hidden generated underpainting.
 a=src[:,:,3:4].astype(float)/255;prem=np.concatenate([src[:,:,:3]*a,src[:,:,3:4]],2)
 out=cv2.warpAffine(prem,mat,(W,H),flags=cv2.INTER_AREA);alpha=out[:,:,3:4]
 return np.concatenate([out[:,:,:3]/np.maximum(alpha/255,1e-9),alpha],2).clip(0,255).astype('uint8')
eye=poly([(430,368),(458,360),(479,365),(498,382),(497,398),(470,403),(439,396),(428,382)],2)+poly([(532,381),(550,365),(579,360),(608,366),(610,384),(590,397),(557,402),(532,397)],2)
brow=poly([(431,337),(467,338),(500,348),(503,367),(474,365),(433,357)],1.5)+poly([(531,348),(573,335),(608,337),(609,357),(570,362),(531,368)],1.5)
nose=poly([(506,350),(526,350),(530,407),(538,431),(542,452),(533,469),(498,469),(488,453),(493,431),(502,408)],3)
mouth=poly([(476,472),(497,465),(536,465),(557,476),(558,493),(539,508),(491,508),(473,493)],3)
masks={'eyes':np.minimum(eye,1),'eyebrows':np.minimum(brow,1),'nose':nose,'mouth':mouth}
# Reference skin silhouette and a fixed hidden neck support, shared by every outfit.
neck=poly([(437,491),(597,491),(606,530),(606,585),(580,643),(532,731),(482,654),(429,586),(429,530)],.8)
face_window=poly([(444,272),(516,250),(589,272),(612,338),(626,399),(613,452),(589,491),(553,528),(532,546),(501,548),(478,533),(443,503),(414,457),(405,401),(413,340)],1)
neck_visible=poly([(441,500),(592,500),(589,551),(568,605),(531,692),(482,616),(451,557)],1)
ref=read(B/'source/reference_canvas_03.png')
feature_holes=np.maximum.reduce(list(masks.values()))
hole=(feature_holes>.015).astype('uint8')*255
inpaint=cv2.inpaint(ref[:,:,:3],hole,5,cv2.INPAINT_TELEA)
base=native_place('face',ty=42)
fixed_head=poly([(430,242),(516,218),(603,243),(618,368),(631,397),(632,421),(614,438),(609,454),(598,477),(579,502),(550,529),(531,544),(519,548),(505,547),(492,541),(475,529),(451,507),(432,482),(415,451),(411,434),(399,425),(399,397),(412,375),(415,310)],.65)
base[:,:,3]=np.maximum(base[:,:,3]*(np.arange(H)[:,None]<510),neck*255).astype('uint8')
rgb=ref[:,:,:3].astype(float);skin=np.clip((rgb[:,:,0]-rgb[:,:,2]-29)/10,0,1)*np.clip((rgb.mean(2)-115)/35,0,1)
clean_face=poly([(486,279),(581,279),(598,321),(605,366),(609,413),(603,451),(589,479),(565,510),(534,540),(511,546),(487,534),(459,511),(439,481),(426,449),(425,408),(437,370),(450,338),(467,309)],2)
pure=((rgb[:,:,0]-rgb[:,:,1]>10)&(rgb[:,:,0]-rgb[:,:,2]>30)&(rgb.mean(2)>160)).astype('uint8')
pure=cv2.erode(pure,np.ones((9,9),np.uint8)).astype(float)
pure=cv2.GaussianBlur(pure,(0,0),2)
visible=np.maximum(clean_face,neck_visible)*pure*(1-np.minimum(feature_holes*4,1))
base[:,:,:3]=np.rint(base[:,:,:3]*(1-visible[:,:,None])+ref[:,:,:3]*visible[:,:,None]).astype('uint8')
# Copy the original jaw/under-chin junction as one connected strip, avoiding a second generated jaw line.
jaw_strip=poly([(424,450),(438,478),(457,502),(480,526),(502,541),(518,547),(532,544),(553,530),(578,505),(598,478),(610,451),(607,474),(590,505),(561,534),(535,557),(510,562),(483,548),(456,523),(435,494),(420,469)],1)
jaw_strip*=np.clip((rgb[:,:,0]-rgb[:,:,1]-7)/7,0,1)
base[:,:,:3]=np.rint(base[:,:,:3]*(1-jaw_strip[:,:,None])+ref[:,:,:3]*jaw_strip[:,:,None]).astype('uint8')
# Extend genuine neck skin behind the collar; never expose transparent-source black RGB.
neck_fill=base[:,:,:3].copy()
for y in range(480,733):
 valid=np.where((neck_visible[y]>.9)&(pure[y]>.9))[0]
 if len(valid):
  xs=np.arange(W);j=np.searchsorted(valid,xs).clip(0,len(valid)-1)
  previous=np.maximum(j-1,0);j=np.where(abs(valid[previous]-xs)<abs(valid[j]-xs),previous,j)
  fill=ref[y,valid[j],:3]
  neck_fill[y]=fill
neck_fill=cv2.GaussianBlur(neck_fill,(0,0),3)
weight=neck*(1-neck_visible)*np.clip((np.arange(H)[:,None]-480)/25,0,1)
base[:,:,:3]=np.rint(base[:,:,:3]*(1-weight[:,:,None])+neck_fill*weight[:,:,None])
save(B/'male/face_shape/01.png',base)
for part,ids in [('eyes',[(1,1),(2,3)]),('eyebrows',[(1,1),(2,2),(3,3)]),('nose',[(1,3)]),('mouth',[(1,3)])]:
 for index,reference in ids:
  r=read(B/'source'/f'reference_canvas_0{reference}.png');save(B/'male'/part/f'{index:02}.png',original_layer(r,masks[part]))
for index in [1,2,3]:
 r=read(B/'source'/f'reference_canvas_0{index}.png');rgb=r[:,:,:3].astype(float);red,green,blue=rgb.transpose(2,0,1);lum=rgb.mean(2)
 hair=np.clip((190-lum)/22,0,1)*(1-np.clip((red-blue-25)/12,0,1))*(1-np.clip((green-red-2)/6,0,1))*(r[:,:,3]/255)
 # Face ink and collar ink are not hair. Source foreground separation supplies the semantic support.
 front_native=native_place(f'front0{index}')
 guard=cv2.dilate((front_native[:,:,3]>35).astype('uint8'),np.ones((19,19),np.uint8)).astype(float)
 clean=read(B/'source'/f'front_clean0{index}_raw.png')
 clean_a=np.clip((clean[:,:,3].astype(float)-80)/174,0,1)
 semantic=cv2.dilate((clean_a>.65).astype('uint8'),np.ones((7,7),np.uint8)).astype(float)
 # Keep the original strands across the feature area instead of punching eye-shaped holes.
 front_a=hair*guard
 front_a[:460]*=np.maximum(semantic[:460],1-feature_holes[:460])
 front_a[600:]*=semantic[600:]
 # The posterior bun and pin belong only to the back layer.
 if index==1:
  front_a[:230]*=clean_a[:230]
 if index==2: front_a[:170]=0
 # Keep posterior source silhouette; generated pixels supply only hidden interior.
 back=native_place(f'back0{index}');hidden=poly([(416,220),(610,220),(618,481),(592,622),(441,622),(407,481)],4)
 hidden[230:]=1 # Reconstruct the continuous hair hidden by its original front, face and clothes.
 back[:,:,3]=(back[:,:,3]*hidden).astype('uint8')
 rear_a=hair*(1-front_a)*(1-feature_holes)
 # Fade only the hidden body portion, avoiding a horizontal cut across visible shoulders.
 rear_a*=np.clip((720-np.arange(H)[:,None])/60,0,1)
 if index==1:
  pin=poly([(380,163),(626,163),(626,190),(380,190)],1)
  rear_a=np.maximum(rear_a,pin*np.clip((205-lum)/35,0,1))
 rear=over(back,original_layer(r,rear_a))
 front=original_layer(r,front_a)
 # Only unseen ends use generated RGB. A short overlap avoids a flat original crop edge.
 tail_weight=np.clip((np.arange(H)[:,None]-900)/37,0,1)
 tail=clean.copy();tail[:,:,3]=np.rint(clean_a*tail_weight*255)
 front[:,:,3]=np.rint(front[:,:,3]*(1-tail_weight))
 front=over(front,tail)
 save(B/'male/hair_front'/f'{index:02}.png',front)
 save(B/'male/hair_back'/f'{index:02}.png',rear)
for part in ['clothing','clothing_back']:
 for i in range(1,8):
  p=B/'male'/part/f'{i:02}.png';p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ACTIVE/'male'/part/f'{i:02}.png',p)
def composite(i,background=(232,225,214,255)):
 out=np.zeros((H,W,4),np.uint8);out[:]=background
 for p,n in [('hair_back',i),('clothing_back',1),('face_shape',1),('clothing',1),('nose',1),('eyes',2 if i==3 else 1),('eyebrows',i),('mouth',1),('hair_front',i)]:out=over(out,read(B/'male'/p/f'{n:02}.png'))
 return out
for i in [1,2,3]:save(B/'preview'/f'style0{i}.png',composite(i))
qa=np.concatenate([np.concatenate([read(B/'source'/f'reference_canvas_0{i}.png')[70:940,250:780] for i in [1,2,3]],1),np.concatenate([composite(i)[70:940,250:780] for i in [1,2,3]],1)],0)
save(B/'qa/original_pixel_trial.png',qa)
(B/'source/fixed_canvas.json').write_text(json.dumps({'canvas':[W,H],'runtime_offset':[0,0],'reference_canvas_used_directly':True,'feature_geometry_rescaled':False,'neck_support':'shared polygon in split_original.py; hidden under original clothing','user_authorized':'按原图像素拆层，优先忠实还原'},ensure_ascii=False,indent=2),encoding='utf8')
print('28 candidate layers written; original pixel trial ready')
