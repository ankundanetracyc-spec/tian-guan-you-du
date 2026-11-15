from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,hashlib
R=Path(__file__).resolve().parent.parent;P=R.parents[3];A=P/'mods/appearance/ink_three_variants_20261004'
def layer(part,i,before=False):
 p=R/('before/male' if before else 'male')/part/f'{i:02}.png'
 if not p.exists():p=R/'before/male'/part/f'{i:02}.png'
 return Image.open(p).convert('RGBA')
def compose(f=1,b=1,c=1,bg='#e8e1d6',before=False,hair=True):
 out=Image.new('RGBA',(1024,1536),bg)
 order=[('hair_back',b),('clothing_back',c),('face_shape',1),('clothing',c),('nose',1),('eyes',2 if f==3 else 1),('eyebrows',f),('mouth',1),('hair_front',f)]
 for part,i in order:
  if not hair and part.startswith('hair_'):continue
  out.alpha_composite(layer(part,i,before))
 return out
for bg,name in [('#e8e1d6','light'),('#253136','dark')]:
 sheet=Image.new('RGB',(1680,940),bg);d=ImageDraw.Draw(sheet)
 for i in range(1,8):
  im=compose(c=i,bg=bg,hair=False).crop((350,480,680,830)).resize((420,445));sheet.paste(im,(((i-1)%4)*420,((i-1)//4)*470+22));d.text((((i-1)%4)*420+8,((i-1)//4)*470+5),str(i),fill='white' if name=='dark' else 'black')
 sheet.save(R/'qa'/f'collars_{name}.png')
 grid=Image.new('RGB',(1080,1710),bg);d=ImageDraw.Draw(grid)
 for f in range(1,4):
  for b in range(1,4):
   im=compose(f,b,bg=bg).crop((270,50,770,810)).resize((360,547));grid.paste(im,((b-1)*360,(f-1)*570+22));d.text(((b-1)*360+8,(f-1)*570+5),f'Front {f} / Back {b}',fill='white' if name=='dark' else 'black')
 grid.save(R/'qa'/f'nine_mixes_{name}.png')
strip=Image.new('RGB',(1080,576),'#e8e1d6')
for i in range(1,4):strip.paste(compose(i,i).crop((270,45,770,845)).resize((360,576)),((i-1)*360,0))
strip.save(R/'qa/three_styles.png')
comparison=Image.new('RGB',(1080,1152),'#e8e1d6')
for i in range(1,4):
 for old in [True,False]:comparison.paste(compose(i,i,before=old).crop((270,45,770,845)).resize((360,576)),((i-1)*360,0 if old else 576))
comparison.save(R/'qa/before_after_styles.png')
metrics=[]
for part in ['hair_front','hair_back']:
 for i in range(1,4):
  a=np.array(layer(part,i));mask=a[:,:,3]>230;lum=a[:,:,:3]@np.array([.2126,.7152,.0722]);ys,xs=np.where(mask)
  row=[]
  for y in [200,250,300,350,400]:
   xx=np.where(mask[y])[0];row.append({'y':y,'left':int(xx.min()),'right':int(xx.max()),'width':int(xx.max()-xx.min()+1)})
  metrics.append({'part':part,'index':i,'core_top':int(ys.min()),'rows':row,'luminance_percentiles':np.percentile(lum[mask],[25,50,75,95]).tolist()})
checks=[]
headpath=A/'male/face_shape/01.png';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();frozen=json.loads((R/'source/frozen_head_neck.json').read_text(encoding='utf8'))
assert sha(headpath)==frozen['sha256'];assert sha(A/'appearance_parts.json')==frozen['configuration_sha256']
for i in range(1,8):
 old=np.array(layer('clothing',i,True));new=np.array(layer('clothing',i));native=np.array(Image.open(R/'source'/f'clothing{i:02}_raw.png'))
 assert np.array_equal(native[930:,:,:3],new[930:,:,:3]);checks.append({'index':i,'source':'continuous generated garment; collar locally registered','generated_body_below_930_rgb_unchanged':True,'front_changed_pixels':int(np.any(old!=new,axis=2).sum())})
(R/'qa/structural_check.json').write_text(json.dumps({'head_sha256':frozen['sha256'],'head_and_neck_unchanged':True,'configuration_unchanged':True,'garments':checks,'hair_measurements':metrics},indent=2),encoding='utf8')
print(json.dumps(metrics))
