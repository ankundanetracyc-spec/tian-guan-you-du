from pathlib import Path
from PIL import Image
import cv2,numpy as np,json
R=Path(__file__).resolve().parent.parent;A=R.parent.parent/'ink_three_variants_20261004';L=np.array([.2126,.7152,.0722])
def read(p):return np.array(Image.open(p).convert('RGBA'))
def parts(a):
 rgb=a[:,:,:3].astype(float);y=rgb@L;alpha=a[:,:,3]/255
 base=cv2.GaussianBlur(y*alpha,(0,0),3)/np.maximum(cv2.GaussianBlur(alpha,(0,0),3),1e-6)
 mask=np.zeros(alpha.shape,np.uint8);mask[210:390,410:620]=1
 mask=(mask*cv2.erode((a[:,:,3]>240).astype('uint8'),np.ones((9,9),np.uint8)))>0
 micro=float(np.percentile(np.maximum(y[mask]-base[mask],0)/np.maximum(base[mask],15),95))
 return rgb,y,base,mask,micro
female=[read(A/'female/hair_back'/f'{i:02}.png') for i in range(1,4)]
target=float(np.median([parts(a)[4] for a in female]));records=[]
for i in range(1,4):
 a=read(R/'source'/f'rear0{i}_raw.png');assert a.shape==(1536,1024,4)
 guide=read(R/'source'/f'rear0{i}_guided.png')[:,:,3]
 safe=cv2.dilate((guide>10).astype('uint8'),np.ones((9,9),np.uint8))
 # A spatial boundary around the posterior cap; no horizontal slice across the actual hair.
 envelope=cv2.GaussianBlur(safe.astype(float),(0,0),.6)
 cap_region=np.clip((460-np.arange(1536)[:,None])/30,0,1)
 a[:,:,3]=np.rint(a[:,:,3]*(1-cap_region+cap_region*envelope))
 a[a[:,:,3]<=2,3]=0
 rgb,y,base,mask,micro=parts(a);k=max(.6,min(1.,target/max(micro,1e-8)))
 previous=read(R/'before_male/hair_back'/f'{i:02}.png');prgb,py,_,pm,_=parts(previous)
 oldchroma=np.median(prgb[pm]-py[pm,None],axis=0);newchroma=np.median(rgb[mask]-y[mask,None],axis=0)
 chroma=np.clip(oldchroma-newchroma,-3,3);chroma-=chroma@L
 delta=np.clip(np.median(py[pm])-np.median(y[mask]),-4,4)
 newrgb=rgb+((base+k*(y-base)+delta)-y)[:,:,None]+chroma
 # Keep the original painted accessories and their color/placement.
 if i==1:newrgb[126:187,378:635]=previous[126:187,378:635,:3]
 if i==2:newrgb[124:172,445:575]=previous[124:172,445:575,:3]
 a[:,:,:3]=np.rint(newrgb).clip(0,255);a[a[:,:,3]==0,:3]=0
 p=R/'male/hair_back'/f'{i:02}.png';p.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(a).save(p)
 rows={}
 for row in [180,210,280,330,350,370,400,430]:
  xs=np.where(a[row,:,3]>230)[0];rows[row]=[int(xs[0]),int(xs[-1]),int(xs[-1]-xs[0]+1)] if len(xs) else None
 records.append({'index':i,'canvas':[1024,1536],'resized_or_shifted':False,'source':f'source/rear0{i}_raw.png','posterior_rows':rows,'female_micro_target':target,'micro_before':micro,'micro_factor':k,'micro_after':parts(a)[4]})
(R/'qa/rear_selection_report.json').write_text(json.dumps(records,indent=2),encoding='utf8')
print('Three posterior-only hair sprites selected; existing dark ink palette retained')
