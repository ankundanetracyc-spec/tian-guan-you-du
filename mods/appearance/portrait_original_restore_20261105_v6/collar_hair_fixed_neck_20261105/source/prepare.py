from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,shutil
R=Path(__file__).resolve().parent.parent; P=R.parents[3]; A=P/'mods/appearance/ink_three_variants_20261004'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
previous=json.loads((R.parent/'skull_partition_fix_20261105/hair_set/installation/install_report.json').read_text(encoding='utf8'))
baseline={**previous['baseline_sha256'],**{r['resource_path']:r['sha256'] for r in previous['records']}}
for path,digest in baseline.items():
 p=P/path[6:];assert sha(p)==digest,path
 q=R/'before'/p.relative_to(A);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
(R/'installation/baseline.json').write_text(json.dumps(baseline,indent=2),encoding='utf8')
for n in ['appearance_parts.json','portrait_manifest.json']:shutil.copy2(A/n,R/'installation'/n.replace('.json','.before.json'))
head=Image.open(A/'male/face_shape/01.png').convert('RGBA')
rows=[]
for y in range(490,741):
 xs=[x for x in range(350,680) if head.getpixel((x,y))[3]>=230]
 if xs:rows.append({'y':y,'left':min(xs),'right':max(xs)})
(R/'source/frozen_head_neck.json').write_text(json.dumps({'sha256':sha(A/'male/face_shape/01.png'),'canvas':[1024,1536],'origin':[0,0],'alpha230_rows':rows,'configuration_sha256':sha(A/'appearance_parts.json')},indent=2),encoding='utf8')
sheet=Image.new('RGB',(1400,1440),'#e8e1d6');d=ImageDraw.Draw(sheet)
for i in range(1,8):
 front=Image.open(A/'male/clothing'/f'{i:02}.png').convert('RGBA');back=Image.open(A/'male/clothing_back'/f'{i:02}.png').convert('RGBA')
 composite=Image.new('RGBA',head.size,(0,0,0,0))
 for layer in [back,head,front]:composite.alpha_composite(layer)
 composite.save(R/'source'/f'neck_clothing_reference_{i:02}.png')
 preview=Image.new('RGBA',head.size,'#e8e1d6');preview.alpha_composite(composite)
 sheet.paste(preview.crop((200,480,824,1200)).resize((350,404)),(((i-1)%4)*350,((i-1)//4)*720+20))
 b=Image.new('RGBA',head.size,'#e8e1d6');b.alpha_composite(back)
 sheet.paste(b.crop((380,480,660,640)).resize((350,200)),(((i-1)%4)*350,((i-1)//4)*720+430));d.text((((i-1)%4)*350+8,((i-1)//4)*720+2),str(i),fill='black')
sheet.save(R/'qa/seven_sources.png')
print('51 source textures snapshotted; head/neck canvas, alpha rows and configuration frozen')
