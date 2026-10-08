"""Register generated layers; extract original feature pigments without painting new art."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import json, math, sys, hashlib, shutil
import cv2
import numpy as np
from PIL import Image,ImageDraw,ImageFont
cv2.setNumThreads(1)

P=Path(__file__).resolve().parents[1]
R=P/'mods/appearance/ink_recovered_parts_20261004'
S=R/'source'
M=P/'mods/appearance/ink_reference_recovery_20261004/iteration'
OLD=P/'mods/appearance/ink_three_variants_20261004'
SLOTS=['hair_front','hair_back','eyebrows','eyes','nose','mouth','face_shape','clothing','face_accessory']
ORDER=[1,6,7,3,2,4,5,8,0]
NUMBERS={'male':[1,2,3],'female':[5,6,7,8,9,10]}
# Measured on the accepted new masters, independent of historical portrait anatomy.
# pupil pair, nostril base and lip centre. All features travel in one master frame.
POINTS={
 1:((463,313),(569,311),(517,378),(517,412)),
 2:((459,377),(567,375),(516,446),(516,476)),
 3:((462,317),(567,315),(516,379),(516,412)),
 5:((464,401),(570,399),(517,461),(517,494)),
 6:((460,349),(575,348),(518,417),(518,452)),
 7:((450,383),(563,381),(507,449),(507,482)),
 8:((458,344),(575,344),(517,410),(517,448)),
 9:((463,378),(573,376),(518,446),(518,473)),
 10:((457,384),(570,382),(514,450),(514,484)),
}
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
BLANK_POINTS={1:((278,574),(753,574),(510,873)),2:((399,379),(632,379),(516,529)),3:((255,628),(774,628),(520,961)),5:((239,621),(781,621),(511,939)),6:((212,628),(807,628),(512,990)),7:((249,590),(779,590),(509,898)),8:((231,591),(799,591),(512,916)),9:((276,552),(756,552),(512,812)),10:((270,659),(757,659),(516,910))}
MASTER_FACE={1:((401,340),(637,340),(517,474)),2:((405,392),(634,392),(517,540)),3:((400,340),(635,340),(517,476)),5:((402,421),(636,421),(517,552)),6:((398,375),(641,375),(518,510)),7:((391,404),(627,404),(508,548)),8:((390,371),(644,371),(517,520)),9:((394,396),(636,396),(518,533)),10:((401,404),(631,404),(514,545))}

def landmark_fit(source,target):
 a=np.array(source,float);b=np.array(target,float);ac=a.mean(0);bc=b.mean(0)
 z=(a-ac)[:,0]+1j*(a-ac)[:,1];w=(b-bc)[:,0]+1j*(b-bc)[:,1]
 c=(w*z.conj()).sum()/(abs(z)**2).sum();rot=np.array([[c.real,-c.imag],[c.imag,c.real]])
 return np.column_stack((rot,bc-rot@ac))

def read(p):return np.array(Image.open(p).convert('RGBA'),dtype=np.float32)
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True)
 a=np.clip(np.rint(a),0,255).astype('uint8');a[a[:,:,3]<3]=0
 Image.fromarray(a).save(p)
def homo(m):return np.vstack((m,[0,0,1]))
def warp(a,m):
 al=a[:,:,3]/255
 wa=cv2.warpAffine(al,m,(1024,1536),flags=cv2.INTER_LINEAR)
 wc=cv2.warpAffine(a[:,:,:3]*al[:,:,None],m,(1024,1536),flags=cv2.INTER_LINEAR)
 return np.dstack((wc/np.maximum(wa[:,:,None],.0001),wa*255))

def master_matrix(n):
 l,r,*_=POINTS[n];sex='male' if n<5 else 'female'
 tl=np.array((464,375 if sex=='male' else 379),float);tr=np.array((569 if sex=='male' else 574,tl[1]),float)
 v=np.array(r)-l;target=tr-tl
 z=complex(*target)/complex(*v);a=z.real;b=z.imag
 rot=np.array([[a,-b],[b,a]])
 return np.column_stack((rot,tl-rot@np.array(l)))

def registration(src,master,key,kind):
 cache=S/(key+'_registration.json')
 if key in ('06_back','08_back','09_back'):
  scale,tx,ty={'06_back':(.51,257,35),'08_back':(.56,227,50),'09_back':(.49,267,37)}[key]
  mat=np.array([[scale,0,tx],[0,scale,ty]])
  cache.write_text(json.dumps({'matrix':mat.tolist(),'method':'Rear underpainting has newly drawn hidden centre; matched outer crown width and nape to original master; uniform scale only'},indent=2),encoding='utf-8');return mat
 if kind=='blank':
  n=int(key[:2]);mat=landmark_fit(BLANK_POINTS[n],MASTER_FACE[n])
  cache.write_text(json.dumps({'matrix':mat.tolist(),'source_points':BLANK_POINTS[n],'master_points':MASTER_FACE[n],'method':'Measured ear centres and chin; least-squares similarity transform, no nonuniform stretch'},indent=2),encoding='utf-8');return mat
 if cache.exists():return np.array(json.loads(cache.read_text())['matrix'],float)
 yy,xx=np.indices(src.shape[:2],dtype=np.int16);y2,x2=np.indices(master.shape[:2],dtype=np.int16)
 sm=src[:,:,3]>210;tm=master[:,:,3]>210
 if kind in ('front','back'):
  sm&=(yy<950)&(src[:,:,:3].mean(2)<170)
  tm&=(y2<650)&(master[:,:,:3].mean(2)<140)
 elif kind=='blank':
  sm&=(yy<720)&(src[:,:,:3].mean(2)>130)
  tm&=(y2<720)&(master[:,:,:3].mean(2)>130)&(x2>350)&(x2<685)
 else:sm&=yy<850;tm&=y2<850
 sift=cv2.SIFT_create(nfeatures=6000)
 k1,d1=sift.detectAndCompute(cv2.cvtColor(src[:,:,:3].astype('uint8'),cv2.COLOR_RGB2GRAY),sm.astype('uint8')*255)
 k2,d2=sift.detectAndCompute(cv2.cvtColor(master[:,:,:3].astype('uint8'),cv2.COLOR_RGB2GRAY),tm.astype('uint8')*255)
 good=[] if d1 is None or d2 is None else [m for m,n in cv2.BFMatcher().knnMatch(d1,d2,k=2) if m.distance<.78*n.distance]
 if len(good)<4:raise ValueError(f'{key}: insufficient registration matches {len(good)}')
 p1=np.float32([k1[m.queryIdx].pt for m in good]);p2=np.float32([k2[m.trainIdx].pt for m in good])
 mat,inl=cv2.estimateAffinePartial2D(p1,p2,method=cv2.RANSAC,ransacReprojThreshold=2,maxIters=8000,confidence=.999)
 inside=inl[:,0].astype(bool);res=np.linalg.norm(cv2.transform(p1[:,None,:],mat)[:,0]-p2,axis=1)
 scale=np.linalg.norm(mat[:,0]);angle=math.degrees(math.atan2(mat[1,0],mat[0,0]))
 if inside.sum()<4 or not .35<scale<1.8 or abs(angle)>6:raise ValueError(f'{key}: implausible registration {inside.sum()} {scale} {angle}')
 # Record real matched points and residuals, not an invented box-centre pivot.
 record={'matrix':mat.tolist(),'inliers':int(inside.sum()),'scale':float(scale),'rotation_degrees':angle,'median_residual':float(np.median(res[inside])),'max_residual':float(res[inside].max()),'source_points':p1[inside].tolist(),'master_points':p2[inside].tolist(),'method':'SIFT correspondences + RANSAC single similarity transform; visually checked after composition'}
 cache.write_text(json.dumps(record,indent=2),encoding='utf-8');return mat

def skin(a,n):
 l,r,*_=POINTS[n];cx=round((l[0]+r[0])/2);y=round((l[1]+r[1])/2)-60
 sample=a[y-10:y+10,cx-18:cx+18,:3];med=np.median(sample.reshape(-1,3),axis=0)
 delta=np.array([233,209,192])-med
 a=a.copy();weight=np.clip((a[:,:,:3].mean(2)-105)/105,0,1)
 a[:,:,:3]+=delta*weight[:,:,None]
 return a

def hair_tone(a):
 a=a.copy();rgb=a[:,:,:3];sel=(a[:,:,3]>230)&(rgb.max(2)-rgb.min(2)<50)
 q=np.percentile(rgb[sel],[20,50,80],axis=0)
 targets=np.array([[45,40,36],[64,59,55],[89,84,80]])
 # Preserve blue/ivory ties rather than mapping ribbons into black hair.
 hair=(rgb.max(2)-rgb.min(2)<35)&(rgb.mean(2)<175)
 for c in range(3):
  mapped=np.interp(rgb[:,:,c],[0,*q[:,c],255],[0,*targets[:,c],210])
  rgb[:,:,c][hair]=mapped[hair]
 return a

def poly(shape,pts,blur=1):
 im=Image.new('L',(shape[1],shape[0]));ImageDraw.Draw(im).polygon(pts,fill=255)
 a=np.array(im,dtype=np.float32)/255
 return cv2.GaussianBlur(a,(0,0),blur) if blur else a

def feature(a,n,slot):
 h,w=a.shape[:2];region=np.zeros((h,w),np.float32);core=np.zeros((h,w),np.float32);l,r,no,mo=POINTS[n]
 if slot in ('eyes','eyebrows'):
  for idx,(x,y) in enumerate([l,r]):
   direction=1 if idx==0 else -1
   if slot=='eyes':
    top=-13 if n<5 else -17
    pts=[(-30,-8),(-20,top),(0,-15),(22,-8),(34,5),(23,13),(0,15),(-23,8),(-33,-4)]
    cp=[(-27,-6),(-15,-9),(0,-10),(16,-6),(28,1),(16,7),(0,9),(-16,5)]
    region=np.maximum(region,poly((h,w),[(x+direction*px,y+py) for px,py in pts],2))
    core=np.maximum(core,poly((h,w),[(x+direction*px,y+py) for px,py in cp],.6))
   else:
    shift=-4 if n in (6,8) else 0
    pts=[(-24,-35+shift),(-10,-34+shift),(18,-27+shift),(38,-20+shift),(40,-7+shift),(16,-11+shift),(-8,-18+shift),(-24,-24+shift)]
    region=np.maximum(region,poly((h,w),[(x+direction*px,y+py) for px,py in pts],2))
 elif slot=='nose':
  x,y=no;region=poly((h,w),[(x-15,y-43),(x+15,y-43),(x+30,y-12),(x+26,y+13),(x,y+20),(x-27,y+12),(x-30,y-12)],8)
 elif slot=='mouth':
  x,y=mo;region=poly((h,w),[(x-42,y-15),(x,y-24),(x+42,y-15),(x+43,y+12),(x,y+27),(x-43,y+12)],4)
  core=poly((h,w),[(x-30,y-2),(x-12,y-8),(x,y-10),(x+12,y-8),(x+30,y-2),(x+22,y+7),(x,y+11),(x-22,y+7)],1)*.6
 # Estimate a smoothly varying skin plate from the perimeter; subtract its pigment contribution.
 rgb=a[:,:,:3].copy();mask=(region>.015).astype('uint8')*255
 bgmask=cv2.dilate(mask,np.ones((15,15),np.uint8)) if slot=='eyebrows' else mask
 bg=cv2.inpaint(np.clip(rgb,0,255).astype('uint8'),bgmask,7,cv2.INPAINT_TELEA).astype(np.float32)
 bg=cv2.GaussianBlur(bg,(0,0),4)
 delta=np.maximum(bg-rgb,0);alpha=np.clip((delta.max(2)-4)/145,0,1)*region
 alpha=np.maximum(alpha,core);alpha*=a[:,:,3]/255
 fg=(rgb-(1-alpha[:,:,None])*bg)/np.maximum(alpha[:,:,None],.001)
 if slot=='nose':
  # Keep a light ink wash on the bridge, with the original nostril definition.
  bridge=np.clip((np.arange(h,dtype=np.float32)-(no[1]-28))/20,0,1)
  alpha*=.45+.55*bridge[:,None]
 return np.dstack((np.clip(fg,0,255),alpha*255))

def compose(sex,vs,hidden=None,bg='#e8e3dc'):
 out=Image.new('RGBA',(1024,1536))
 for k in ORDER:
  if SLOTS[k]==hidden:continue
  out.alpha_composite(Image.open(R/sex/SLOTS[k]/f'{vs[k]:02}.png').convert('RGBA'))
 if bg is None:return out
 base=Image.new('RGBA',out.size,bg);base.alpha_composite(out);return base.convert('RGB')

def neck_underlay(out,sex):
 # The unpainted neck overlap comes from this iteration's original portrait.
 n=2 if sex=='male' else 6
 a=warp(skin(read(M/f'{n:02}.png'),n),master_matrix(n))
 mask=poly((1536,1024),[(445,510),(586,510),(595,565),(568,638),(520,700),(453,603)],2)
 a[:,:,3]*=mask
 # The generated head owns chin and upper neck; taper only its lower neck.
 taper=np.clip((635-np.arange(1536,dtype=np.float32))/55,0,1)
 out[:,:,3]*=taper[:,None]
 al=out[:,:,3:4]/255;ab=a[:,:,3:4]/255;total=al+ab*(1-al)
 return np.dstack(((out[:,:,:3]*al+a[:,:,:3]*ab*(1-al))/np.maximum(total,.0001),total[:,:,0]*255))

def build(numbers=None):
 records=[]
 for sex,ns in NUMBERS.items():
  for v,n in enumerate(ns,1):
   if numbers is not None and n not in numbers:continue
   master=read(M/f'{n:02}.png');tm=master_matrix(n);toned=skin(master,n)
   for slot in ['eyebrows','eyes','nose','mouth']:
    save(R/sex/slot/f'{v:02}.png',warp(feature(toned,n,slot),tm))
   for part,slot in [('front','hair_front'),('back','hair_back'),('blank','face_shape')]:
    key=f'{n:02}_{part}';p=S/(key+'.png')
    if not p.exists():continue
    a=read(p);fit=registration(a,master,key,part);combined=(homo(tm)@homo(fit))[:2]
    if part=='blank':
     # Calibrate only after registration because generated head scale can drift.
     a=warp(a,fit);a=skin(a,n);out=neck_underlay(warp(a,tm),sex)
    else:out=warp(hair_tone(a),combined)
    save(R/sex/slot/f'{v:02}.png',out)
    records.append({'sex':sex,'slot':slot,'variant':v,'master_number':n,'matrix':combined.tolist(),'source':str(p.relative_to(R))})
  for k in range(1,4):
   key=f'{sex}_extra_{k}';p=S/(key+'.png')
   if not p.exists():continue
   n=2 if sex=='male' else 6;master=read(M/f'{n:02}.png');a=read(p);fit=registration(a,master,key,'features');a=skin(warp(a,fit),n)
   for slot in ['eyes','eyebrows']:save(R/sex/slot/f'{len(ns)+k:02}.png',warp(feature(a,n,slot),master_matrix(n)))
  for slot in ['clothing','face_accessory']:
   for k in range(1,4):
    dst=R/sex/slot/f'{k:02}.png';dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(OLD/sex/slot/f'{k:02}.png',dst)
 (R/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':
 build([int(s) for s in sys.argv[1:]] or None)
