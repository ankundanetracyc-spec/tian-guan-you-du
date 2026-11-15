from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json
R=Path(__file__).resolve().parent.parent
head=Image.open(R/'before/male/face_shape/01.png').convert('RGBA')
sheet=Image.new('RGB',(1680,900),'#e8e1d6');d=ImageDraw.Draw(sheet);rows=[]
for i in range(1,8):
 n=f'{i:02}';front=Image.open(R/'source'/f'clothing{n}_raw.png').convert('RGBA');back=Image.open(R/'source'/f'collarback{n}_raw.png').convert('RGBA')
 a=np.array(front);b=np.array(back)
 tips=[]
 for x0,x1 in [(380,470),(570,650)]:
  ys,xs=np.where(a[450:650,x0:x1,3]>230);y=ys.min()+450;x=float(np.median(xs[ys==ys.min()]))+x0;tips.append([x,int(y)])
 yy,xx=np.where(b[:,:,3]>230);bbox=[int(xx.min()),int(yy.min()),int(xx.max()+1),int(yy.max()+1)]
 points={}
 for key,p in [('before',R/'before/male/clothing'/f'{n}.png'),('raw',R/'source'/f'clothing{n}_raw.png')]:
  aa=np.array(Image.open(p))[:,:,3];yy,xx=np.where(aa[600:810,480:561]<30);y=yy.max()+600;x=float(np.median(xx[yy==yy.max()]))+480;points[key]=[x,int(y)]
 rows.append({'index':i,'front_tips':tips,'rear_core_bbox':bbox,'v_points':points})
 out=Image.new('RGBA',head.size,'#e8e1d6')
 for layer in [back,head,front]:out.alpha_composite(layer)
 sheet.paste(out.crop((350,480,680,820)).resize((420,430)),(((i-1)%4)*420,((i-1)//4)*450+20));d.text((((i-1)%4)*420+5,((i-1)//4)*450+4),n,fill='black')
sheet.save(R/'qa/raw_collar_composites.png')
(R/'source/collar_measured.json').write_text(json.dumps(rows,indent=2),encoding='utf8');print(json.dumps(rows))
sheet=Image.new('RGB',(1400,600),'#e8e1d6')
for j,(name,p) in enumerate([('female',R.parents[0]/'../ink_three_variants_20261004/female/hair_front/01.png')]+[(f'front{i:02}',R/'source'/f'front{i:02}volume_raw.png') for i in range(1,4)]):
 im=Image.open(p.resolve()).convert('RGBA');a=np.array(im);mask=(a[:,:,3]>230);lum=a[:,:,:3]@np.array([.2126,.7152,.0722]);print(name,np.percentile(lum[mask],[25,50,75,95]).tolist())
 bg=Image.new('RGBA',im.size,'#e8e1d6');bg.alpha_composite(im);sheet.paste(bg.crop((280,70,760,1070)).resize((280,584)),(j*350,0))
sheet.save(R/'qa/hair_material_sources.png')
