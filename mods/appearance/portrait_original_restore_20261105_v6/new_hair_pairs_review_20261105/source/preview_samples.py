from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,numpy as np
R=Path(__file__).resolve().parent.parent;P=R.parents[3];A=P/'mods/appearance/ink_three_variants_20261004';Q=R/'qa';Q.mkdir(exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
fixed=json.loads((R.parent/'collar_hair_fixed_neck_20261105/source/frozen_head_neck.json').read_text(encoding='utf8'))
assert sha(A/'male/face_shape/01.png')==fixed['sha256'];assert sha(A/'appearance_parts.json')==fixed['configuration_sha256']
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',23)
names=['疏朗半束','清劲高束','松风低束']
def layer(part,i=1):
 if part.startswith('hair_'):p=R/'male'/part/f'{i:02}.png'
 elif part in ['eyebrows','eyes','nose','mouth']:p=R/'features/male'/part/'01.png'
 else:p=A/'male'/part/f'{i:02}.png'
 return Image.open(p).convert('RGBA')
def composite(i,b=None,c=1,bg='#e8e1d6',hair=True):
 out=Image.new('RGBA',(1024,1536),bg)
 for part,n in [('hair_back',b or i),('clothing_back',c),('face_shape',1),('clothing',c),('nose',1),('eyes',1),('eyebrows',1),('mouth',1),('hair_front',i)]:
  if not hair and part.startswith('hair_'):continue
  out.alpha_composite(layer(part,n))
 return out
for bg,label in [('#e8e1d6','light'),('#253136','dark')]:
 strip=Image.new('RGB',(1200,680),bg);d=ImageDraw.Draw(strip)
 for i in range(1,4):
  im=composite(i,bg=bg);im.save(Q/f'pair{i:02}_{label}_full.png')
  strip.paste(im.crop((265,60,775,875)).resize((400,639)),((i-1)*400,40));d.text(((i-1)*400+110,6),f'{i:02}  {names[i-1]}',font=font,fill='#ede5d9' if label=='dark' else '#363b37')
 strip.save(Q/f'three_new_pairs_{label}.png')
parts=Image.new('RGB',(1200,1140),'#e8e1d6');d=ImageDraw.Draw(parts)
for i in range(1,4):
 for j,part in enumerate(['hair_front','hair_back']):
  out=Image.new('RGBA',(1024,1536),'#e8e1d6');out.alpha_composite(layer(part,i));parts.paste(out.crop((280,70,760,1120)).resize((260,569)),((i-1)*400+70,j*570));d.text(((i-1)*400+8,j*570+6),f'{i:02} '+('前发' if j==0 else '后发'),font=font,fill='#363b37')
parts.save(Q/'paired_layers.png')
face=composite(1,hair=False).crop((375,285,660,555)).resize((855,810));face.save(Q/'new_features_closeup.png')
features=Image.new('RGB',(960,800),'#e8e1d6');d=ImageDraw.Draw(features)
for j,(part,title,box) in enumerate([('eyebrows','眉',(420,328,616,375)),('eyes','眼',(410,349,626,414)),('nose','鼻与山根',(467,329,565,480)),('mouth','嘴',(465,455,567,519))]):
 out=Image.new('RGBA',(1024,1536),'#e8e1d6');out.alpha_composite(layer(part));crop=out.crop(box);crop.thumbnail((420,330));features.paste(crop.resize((min(crop.width*2,420),min(crop.height*2,330))),((j%2)*480+30,(j//2)*400+50));d.text(((j%2)*480+30,(j//2)*400+12),title,font=font,fill='#363b37')
features.save(Q/'four_feature_samples.png')
widths=[]
for i in range(1,4):
 f=np.array(layer('hair_front',i))[:,:,3];b=np.array(layer('hair_back',i))[:,:,3];rows=[]
 for y in [250,300,350,400]:
  xx=np.where(f[y]>128)[0];yy=np.where(b[y]>128)[0];rows.append({'y':y,'front_width':int(xx.max()-xx.min()+1),'back_width':int(yy.max()-yy.min()+1)})
 widths.append({'pair':i,'rows':rows})
assert all(r['back_width']<r['front_width'] for w in widths for r in w['rows']),widths
selection=json.loads((R/'source/selected_assets.json').read_text(encoding='utf8'))
for r in selection['records']:assert sha(R/r['file'])==r['sha256']
(Q/'review_check.json').write_text(json.dumps({'status':'preview only; not installed','head_neck_unchanged':True,'head_sha256':fixed['sha256'],'configuration_unchanged':True,'canvas':[1024,1536],'hair_width_checks':widths,'layer_order':['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','hair_front'],'new_features_each':1,'new_pairs':3,'old_feature_pixels_used':False},ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(widths))
