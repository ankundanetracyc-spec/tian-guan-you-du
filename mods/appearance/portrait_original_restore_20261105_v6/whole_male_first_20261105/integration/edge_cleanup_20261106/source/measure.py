from PIL import Image
from pathlib import Path
import numpy as np,json
R=Path(__file__).resolve().parent
records={}
for part in ['eyebrows','eyes']:
 points=[]
 for kind,p in [('old',R.parent/'before'/part/'01.png'),('native',R/f'{part}_raw.png')]:
  a=np.array(Image.open(p).convert('RGBA'));l=a[:,:,:3]@np.array([.2126,.7152,.0722]);al=a[:,:,3]
  if part=='eyebrows':
   ranges=[(420,510,325,355),(510,615,325,355)] if kind=='old' else [(200,530,590,690),(690,1050,590,690)]
   out=[]
   for x0,x1,y0,y1 in ranges:
    aa=al[y0:y1,x0:x1]/255.;ll=l[y0:y1,x0:x1];w=np.maximum(150-ll,0)*aa;yy,xx=np.indices(w.shape);out.append([float((xx*w).sum()/w.sum()+x0),float((yy*w).sum()/w.sum()+y0)])
  else:
   ranges=[(447,475,357,375),(555,581,357,375)] if kind=='old' else [(310,410,608,655),(820,930,608,655)]
   out=[]
   for x0,x1,y0,y1 in ranges:
    ll=l[y0:y1,x0:x1];aa=al[y0:y1,x0:x1];threshold=np.percentile(ll[aa>230],3);yy,xx=np.where((ll<threshold+5)&(aa>230));out.append([float(xx.mean()+x0),float(yy.mean()+y0)])
  points.append((kind,out))
 d=dict(points);s=(d['old'][1][0]-d['old'][0][0])/(d['native'][1][0]-d['native'][0][0]);records[part]={'scale':s,'target':np.mean(d['old'],axis=0).tolist(),'source':np.mean(d['native'],axis=0).tolist(),'landmarks':d}
(R/'registration.json').write_text(json.dumps(records,indent=2),encoding='utf8');print(json.dumps(records))
