from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json
R=Path(__file__).resolve().parent.parent;P=R.parents[4];A=P/'mods/appearance/ink_three_variants_20261004/male';V=R/'axis_alignment_20261106';Q=V/'qa';Q.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
def compose(before=False,front=False):
 out=Image.new('RGBA',(1024,1536),'#cfcdbe')
 for part in ['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','hair_front']:
  if part=='hair_front' and not front:continue
  p=V/'before'/part/'01.png' if before and (V/'before'/part/'01.png').exists() else A/part/'01.png';out.alpha_composite(Image.open(p).convert('RGBA'))
 return out
for front,label in [(False,'front_hidden'),(True,'full')]:
 sheet=Image.new('RGB',(1000,790),'#e8e1d6');d=ImageDraw.Draw(sheet)
 for i,old in enumerate([True,False]):
  im=compose(old,front).crop((345,165 if not front else 70,685,680)).resize((500,757));sheet.paste(im,(i*500,32));d.text((i*500+10,4),'调整前' if old else '同一中线校准后',font=font,fill='#343a35')
 sheet.save(Q/f'before_after_{label}.png')
compose(front=True).save(Q/'aligned_full_portrait.png');compose().crop((340,170,690,680)).resize((700,1020)).save(Q/'aligned_head_without_front.png')
print('Actual active-layer before/after previews saved')
