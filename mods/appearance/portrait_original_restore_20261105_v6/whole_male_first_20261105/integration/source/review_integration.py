from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import shutil,json,hashlib,numpy as np
R=Path(__file__).resolve().parent.parent;P=R.parents[4];A=P/'mods/appearance/ink_three_variants_20261004';Q=R/'qa';font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
for part in ['eyebrows','eyes','nose','mouth']:
 p=R/'face_parts/male'/part/'01.png';q=R/'male'/part/'01.png';q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
for part in ['clothing','clothing_back']:
 p=R/'garment_parts/male'/part/'01.png';q=R/'male'/part/'01.png';q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
def layer(part,i):
 p=R/'male'/part/f'{i:02}.png'
 if not p.exists():p=R/'before/male'/part/f'{i:02}.png'
 return Image.open(p).convert('RGBA')
def compose(c=1,bg='#e8e1d6',hair=True):
 out=Image.new('RGBA',(1024,1536),bg)
 for part,i in [('hair_back',1),('clothing_back',c),('face_shape',1),('clothing',c),('nose',1),('eyes',1),('eyebrows',1),('mouth',1),('hair_front',1)]:
  if not hair and part.startswith('hair_'):continue
  out.alpha_composite(layer(part,i))
 return out
master=Image.open(R.parent/'male_whole_01.png').convert('RGBA');current=compose();current.save(Q/'assembled_light.png');compose(bg='#253136').save(Q/'assembled_dark.png')
out=Image.new('RGB',(1200,930),'#e8e1d6');d=ImageDraw.Draw(out)
for i,(im,label) in enumerate([(master,'认可母版'),(current,'实际独立部件组合')]):
 out.paste(im.crop((260,60,780,840)).resize((600,900)),(i*600,30));d.text((i*600+15,2),label,font=font,fill='#343b35')
out.save(Q/'master_and_parts.png')
for color,name in [('#e8e1d6','light'),('#253136','dark')]:
 out=Image.new('RGB',(1680,900),color);d=ImageDraw.Draw(out)
 for i in range(1,8):
  im=compose(i,bg=color,hair=False).crop((350,485,680,825)).resize((420,430));out.paste(im,(((i-1)%4)*420,((i-1)//4)*450+20));d.text((((i-1)%4)*420+10,((i-1)//4)*450+2),str(i),fill='white' if name=='dark' else 'black')
 out.save(Q/f'seven_collars_{name}.png')
old=np.array(layer('face_shape',1));fixed=np.array(Image.open(R/'before/male/face_shape/01.png'));assert np.array_equal(old[:,:,3],fixed[:,:,3])
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report={'canvas':[1024,1536],'head_neck_alpha_changed_pixels':0,'master_sha256':sha(R.parent/'male_whole_01.png'),'selected_parts':[{ 'part':p.parent.name,'sha256':sha(p),'mode':Image.open(p).mode,'size':list(Image.open(p).size)} for p in (R/'male').glob('*/01.png')],'user_approved_master':True,'user_part_visual_approval':False}
assert len(report['selected_parts'])==9
assert all(r['mode']=='RGBA' and r['size']==[1024,1536] for r in report['selected_parts'])
(Q/'structural_check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('Nine parts assembled and verified; head/neck alpha unchanged; seven collar previews saved')
