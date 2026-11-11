from PIL import Image
from pathlib import Path
import numpy as np,json
R=Path(__file__).resolve().parent;a=np.array(Image.open(R/'eyes_clean_raw.png'));lum=a[:,:,:3]@np.array([.2126,.7152,.0722]);points=[]
for x0,x1 in [(410,490),(535,610)]:
 area=lum[350:500,x0:x1];alpha=a[350:500,x0:x1,3];minimum=np.percentile(area[alpha>230],1);yy,xx=np.where((area<minimum+7)&(alpha>230));points.append([float(np.mean(xx)+x0),float(np.mean(yy)+350)])
s=102/(points[1][0]-points[0][0]);result={'pupils':points,'s':s,'source':np.mean(points,axis=0).tolist(),'target':[515,379]}
(R/'eye_registration.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps(result));print('alpha32 bounds',Image.fromarray((a[:,:,3]>32).astype('uint8')*255).getbbox())
