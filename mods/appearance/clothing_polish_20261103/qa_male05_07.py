from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib
R=Path('mods/appearance/clothing_polish_20261103');Q=R/'qa/male05_07';Q.mkdir(parents=True,exist_ok=True);records=[]
for i in range(5,8):
 s=Path('mods/appearance/ink_three_variants_20261004/male/clothing')/f'{i:02}.png';p=R/'generated/male'/f'{i:02}.png';a=np.array(Image.open(s).convert('RGBA'));b=np.array(Image.open(p).convert('RGBA'));rec={'index':i,'source':str(s),'output':str(p),'source_sha256':hashlib.sha256(s.read_bytes()).hexdigest(),'output_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'source_size':list(Image.open(s).size),'output_size':list(Image.open(p).size),'output_mode':Image.open(p).mode}
 if a.shape==b.shape:
  x=a[:,:,3]>=128;y=b[:,:,3]>=128;rec.update({'source_alpha128_bbox':list(Image.fromarray(x).getbbox()),'output_alpha128_bbox':list(Image.fromarray(y).getbbox()),'alpha128_iou':float((x&y).sum()/(x|y).sum()),'source_alpha_bbox':list(Image.open(s).getbbox()),'output_alpha_bbox':list(Image.open(p).getbbox()),'output_alpha0_pixels':int((b[:,:,3]==0).sum())})
  rec['collar_top_y_source_output']=[int(np.where(a[:800,:,3]>=128)[0].min()),int(np.where(b[:800,:,3]>=128)[0].min())]
  overlay=Image.new('RGB',(1024,1536),(235,229,217));c=np.array(overlay);c[x&~y]=[230,70,60];c[y&~x]=[50,110,230];c[x&y]=[90,95,90];Image.fromarray(c).crop((0,460,1024,850)).save(Q/f'{i:02}_silhouette_delta.png')
 board=Image.new('RGB',(1024,768),(235,229,217))
 for j,path in enumerate([s,p]):
  im=Image.open(path).convert('RGBA');bg=Image.new('RGBA',im.size,(235,229,217,255));bg.alpha_composite(im);board.paste(bg.resize((512,768)).convert('RGB'),(j*512,0))
 board.save(Q/f'{i:02}_before_after.jpg');records.append(rec)
(Q/'shape_report.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(records,indent=2))
