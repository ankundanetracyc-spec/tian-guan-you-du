from pathlib import Path
from PIL import Image
import numpy as np,json
R=Path(__file__).resolve().parent;M=R.parents[2]/'male_whole_01.png'
def pix(p):return np.array(Image.open(p).convert('RGBA'))
a=pix(M);lum=a[:,:,:3]@np.array([.2126,.7152,.0722]);points=[]
for x0,x1 in [(452,480),(560,586)]:
 area=lum[357:375,x0:x1];yy,xx=np.where(area<50);points.append([float(xx.mean()+x0),float(yy.mean()+357)])
print('Master eyes',points)
raw=pix(R/'eyes_raw.png');l=raw[:,:,:3]@np.array([.2126,.7152,.0722]);native=[]
for x0,x1 in [(310,410),(820,930)]:
 area=l[590:646,x0:x1];aa=raw[590:646,x0:x1,3];threshold=np.percentile(area[aa>230],3);yy,xx=np.where((area<threshold+5)&(aa>230));native.append([float(xx.mean()+x0),float(yy.mean()+590)])
print('Native eyes',native)
result={'master_eye_points':points,'native_eye_points':native}
for part in ['eyebrows','nose','mouth']:
 b=pix(R/f'{part}_raw.png');l=b[:,:,:3]@np.array([.2126,.7152,.0722]);mask=b[:,:,3]>128
 if part=='eyebrows':
  yy,xx=np.where(mask);result[part]={'bbox128':[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]}
 elif part=='mouth':
  score=((l<100)&mask)[:,350:920].sum(axis=1);y=int(np.argmax(score));xx=np.where((l[y]<100)&mask[y])[0];result[part]={'seam':[float(np.mean(xx)),y],'seam_width':int(xx.max()-xx.min()+1)}
 else:
  score=((l<110)&mask)[:,450:810].sum(axis=1);y=int(np.argmax(score));xx=np.where((l[y]<110)&mask[y])[0];result[part]={'nostrils':[float(np.mean(xx)),y],'nostrils_width':int(xx.max()-xx.min()+1)}
(R/'measurements.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps(result,indent=2))
