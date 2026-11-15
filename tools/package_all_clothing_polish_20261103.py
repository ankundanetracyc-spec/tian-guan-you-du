from pathlib import Path
import json,base64,zipfile,hashlib
import numpy as np
import cv2
from PIL import Image
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/clothing_polish_20261103';A=R/'mods/appearance/ink_three_variants_20261004'
records=[];items=[]
for gender in ['male','female']:
 for i in range(1,8):
  src=A/gender/'clothing'/f'{i:02}.png';gen=O/'generated'/gender/f'{i:02}.png'
  old=np.array(Image.open(src).convert('RGBA'));new=np.array(Image.open(gen).convert('RGBA'))
  assert old.shape==new.shape==(1536,1024,4),str(gen)
  mask=(old[:,:,3]>=128).astype(np.uint8)*255
  if i==1:
   left,right,bottom=((450,552),(584,552),(520,690)) if gender=='female' else ((435,532),(598,532),(531,694))
   cv2.fillPoly(mask,[np.array([(0,0),(1023,0),(1023,right[1]-5),(right[0],right[1]-5),right,bottom,left,(left[0],left[1]-5),(0,left[1]-5)],np.int32)],0)
  mask=cv2.morphologyEx(mask,cv2.MORPH_OPEN,np.ones((3,3),np.uint8))
  n,lab,stats,_=cv2.connectedComponentsWithStats(mask)
  mask=np.where(lab==1+np.argmax(stats[1:,cv2.CC_STAT_AREA]),mask,0).astype(np.uint8)
  mask=cv2.GaussianBlur(mask,(3,3),.55)
  new[:,:,3]=np.minimum(new[:,:,3],mask);new[new[:,:,3]==0]=0
  out=O/'final'/gender/'clothing'/f'{i:02}.png';out.parent.mkdir(parents=True,exist_ok=True)
  Image.fromarray(new).save(out)
  def uri(p):return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
  label=('男装' if gender=='male' else '女装')+f' {i:02}'
  items.append({'label':label,'before':uri(src),'after':uri(out)})
  preview=O/'preview'/gender;preview.mkdir(parents=True,exist_ok=True)
  for name,arr in [('before',old),('after',new)]:
   bg=Image.new('RGBA',(1024,1536),(235,229,217,255));bg.alpha_composite(Image.fromarray(arr))
   bg.crop((0,500,1024,1536)).convert('RGB').save(preview/f'{i:02}_{name}.jpg',quality=95)
  records.append({'gender':gender,'variant':i,'file':str(out.relative_to(O)).replace('\\','/'),'canvas':[1024,1536],'scale':1,'translation':[0,0],'source_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'transparent_rgb_nonzero':int(np.count_nonzero(new[new[:,:,3]==0,:3]))})
(O/'manifest.json').write_text(json.dumps({'status':'review_not_installed','count':14,'method':'Generated polish, identity placement, original silhouette alpha constraint. Small brushwork details may differ.','records':records},ensure_ascii=False,indent=2),encoding='utf-8')
html='''<!doctype html><meta charset="utf-8"><title>男女14款衣服润色对照</title><style>body{background:#ebe5d9;color:#354039;font:16px system-ui;margin:20px}header{position:sticky;top:0;background:#ebe5d9;padding:12px}button,select{font:inherit;padding:8px;margin:5px}main{display:flex;gap:20px}figure{margin:0;flex:1}img{width:100%;background:#e5dfd2}h1{font-size:24px}figcaption{padding:10px}p{font-size:14px}</style><header><h1>男女14款衣服 · 润色对照</h1><select id="pick"></select><button id="prev">上一款</button><button id="next">下一款</button><select id="bg"><option value="#e5dfd2">浅底</option><option value="#34383a">深底</option></select><p>每款独立1024×1536透明PNG，保持原尺寸和位置。左侧原版，右侧润色版。当前为候选，尚未替换游戏。</p></header><main><figure><figcaption>原版</figcaption><img id="old"></figure><figure><figcaption>润色版</figcaption><img id="new"></figure></main><script>const items=DATA;const pick=document.querySelector('#pick');items.forEach((x,i)=>pick.add(new Option(x.label,i)));function draw(){const v=items[+pick.value];document.querySelector('#old').src=v.before;document.querySelector('#new').src=v.after}pick.onchange=draw;document.querySelector('#prev').onclick=()=>{pick.value=(+pick.value+13)%14;draw()};document.querySelector('#next').onclick=()=>{pick.value=(+pick.value+1)%14;draw()};document.querySelector('#bg').onchange=e=>document.querySelectorAll('img').forEach(im=>im.style.background=e.target.value);draw();</script>'''.replace('DATA',json.dumps(items,ensure_ascii=False))
(O/'全部衣服前后对照.html').write_text(html,encoding='utf-8')
with zipfile.ZipFile(O/'男女14款衣服独立PNG.zip','w',zipfile.ZIP_DEFLATED) as z:
 for p in (O/'final').rglob('*.png'):z.write(p,p.relative_to(O/'final'))
 z.write(O/'manifest.json','manifest.json')
print('Packaged 14 independent garments and comparison viewer.')
