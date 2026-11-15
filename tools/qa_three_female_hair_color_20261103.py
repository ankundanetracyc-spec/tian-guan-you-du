from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json
P=Path('mods/appearance/portrait_rebuild_20261103/three_style_parts');Q=P/'qa';records=[];tiles=Image.new('RGB',(1600,600),(235,229,217));d=ImageDraw.Draw(tiles)
paths=[(f'{k}{i}',P/'female'/k/f'{i:02}.png') for k in ['hair_front','hair_back'] for i in range(1,4)]+[(f'original_{k}',Path('mods/appearance/ink_three_variants_20261004/female')/k/'01.png') for k in ['hair_front','hair_back']]
for i,(name,path) in enumerate(paths):
 a=np.array(Image.open(path).convert('RGBA')).astype(float);rgb=a[:,:,:3];lum=rgb@np.array([.2126,.7152,.0722]);yy,xx=np.indices(lum.shape);roi=(xx>=350)&(xx<690)&(yy>=145)&(yy<350)&(a[:,:,3]>=240);v=rgb[roi];l=lum[roi];bins={}
 for lo,hi in [(20,50),(50,80),(80,110),(110,140),(140,180)]:
  m=(l>=lo)&(l<hi);bins[f'{lo}_{hi}']={'count':int(m.sum()),'median_rgb':np.round(np.median(v[m],axis=0),2).tolist() if m.any() else None,'median_R_minus_B':float(np.median(v[m,0]-v[m,2])) if m.any() else None}
 records.append({'name':name,'source':str(path),'alpha_min':240,'roi':[350,145,690,350],'luma_quantiles_05_25_50_75_95':np.round(np.percentile(l,[5,25,50,75,95]),2).tolist(),'conditional_color_bins':bins})
 # Identical 200x200 native crown-left patch; alpha blended on paper, no adjustment.
 im=Image.open(path).convert('RGBA');bg=Image.new('RGBA',im.size,(235,229,217,255));bg.alpha_composite(im);tiles.paste(bg.crop((350,140,550,340)).resize((200,200)).convert('RGB'),(i*200,30));d.text((i*200+3,5),name,fill='black')
 # empirical opaque color patches at matching luminance bins, useful only as color summary
 for j,b in enumerate(bins.values()):
  if b['median_rgb'] is not None:d.rectangle([i*200+5,250+j*60,i*200+195,300+j*60],fill=tuple(map(int,b['median_rgb'])))
tiles.save(Q/'hair_color_native_patches.jpg',quality=97);(Q/'hair_color_measurements.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(records,ensure_ascii=False,indent=2))
