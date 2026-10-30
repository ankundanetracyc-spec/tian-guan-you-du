"""Read-only image analysis. Save registration vectors and paper-color measurements, not edited images."""
import json
from pathlib import Path
import cv2
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/joint_paint_v4'
OUT=ART/'registration'
OUT.mkdir(exist_ok=True)
plan=json.loads((ART/'plan.json').read_text(encoding='utf-8'))
boxes=json.loads((ART/'architecture_boxes.json').read_text(encoding='utf-8'))
records=[]
def read(p):
    return cv2.cvtColor(cv2.imdecode(np.fromfile(str(p),np.uint8),cv2.IMREAD_COLOR),cv2.COLOR_BGR2RGB)
def paper(img):
    a=img.astype(np.float32)
    y=a[:,:,0]*.299+a[:,:,1]*.587+a[:,:,2]*.114
    return (y>176)&(a[:,:,0]-a[:,:,1]>22)&(a[:,:,1]-a[:,:,2]>32)
for p in plan['patches']:
    ref=read(p['reference']);src=read(ART/'painted'/f"{p['id']}.png")
    engine=cv2.DISOpticalFlow_create(cv2.DISOPTICAL_FLOW_PRESET_MEDIUM)
    a=cv2.GaussianBlur(cv2.cvtColor(ref,cv2.COLOR_RGB2GRAY),(0,0),1.2)
    b=cv2.GaussianBlur(cv2.cvtColor(src,cv2.COLOR_RGB2GRAY),(0,0),1.2)
    flow=engine.calc(a,b,None)
    flow=cv2.GaussianBlur(np.clip(flow,-60,60),(0,0),2)
    yy,xx=np.mgrid[:1254,:1254]
    protect=np.ones((1254,1254),np.float32)
    for site in p['sites']:
        x,y,w,h=boxes[site['id']]
        distance=np.maximum.reduce([x-xx,xx-x-w,y-yy,yy-y-h,np.zeros_like(xx)])
        # Architecture and altered local footing stay at the painted location.
        protect*=np.clip((distance-75)/125,0,1).astype(np.float32)
    flow*=protect[:,:,None]
    flow.astype('<f4').tofile(OUT/f"{p['id']}.flow")
    rp=paper(ref);sp=paper(src)
    # Robust paper-only samples avoid copying removed mountain washes back onto town plains.
    overall=np.median(ref[rp],axis=0)-np.median(src[sp],axis=0)
    grid=[]
    for gy in range(6):
        row=[]
        for gx in range(6):
            sl=(slice(gy*209,(gy+1)*209),slice(gx*209,(gx+1)*209))
            r=ref[sl][rp[sl]];s=src[sl][sp[sl]]
            delta=np.median(r,axis=0)-np.median(s,axis=0) if len(r)>1500 and len(s)>1500 else overall
            row.append(np.clip(delta,-18,18).tolist())
        grid.append(row)
    records.append({'id':p['id'],'paperShift':overall.tolist(),'paperGrid':grid,'flowMax':float(np.abs(flow).max()),'flowP95':float(np.percentile(np.linalg.norm(flow,axis=2),95))})
    print(p['id'],flush=True)
(OUT/'measurements.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
