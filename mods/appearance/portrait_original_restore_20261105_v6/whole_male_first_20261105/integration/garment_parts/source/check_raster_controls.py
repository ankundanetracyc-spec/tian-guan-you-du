from pathlib import Path
from PIL import Image
import numpy as np,json
R=Path(__file__).resolve().parent.parent
p=R/'source/selection_manifest.json';data=json.loads(p.read_text(encoding='utf8'));a=np.array(Image.open(R/'male/clothing/01.png'));points=[]
for x0,x1 in [(400,470),(570,630)]:
 yy,xx=np.where(a[500:600,x0:x1,3]>230);y=yy.min();points.append([float(np.median(xx[yy==y])+x0),int(y+500)])
yy,xx=np.where(a[600:750,490:551,3]<30);y=yy.max();points.append([float(np.median(xx[yy==y])+490),int(y+600)])
target=data['records'][0]['transform']['target_controls'];res=[float(np.linalg.norm(np.array(m)-t)) for m,t in zip(points,target)]
data['records'][0]['raster_controls_alpha_thresholds']={'tips':230,'v_opening':30};data['records'][0]['raster_controls_measured']=points;data['records'][0]['raster_residual_pixels']=res
assert max(res)<=2,res
back=np.array(Image.open(R/'male/clothing_back/01.png'))
yy,xx=np.where(back[:,:,3]>32);data['records'][1]['alpha32_bbox']=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
assert np.max(a[:500,:,3])==0
assert a[600,516,3]==0 and a[650,516,3]==0
p.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps({'raster_controls':points,'raster_residual_pixels':res,'rear_bbox':data['records'][1]['alpha32_bbox']}))
