from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json
R=Path('mods/appearance/portrait_rebuild_20261103/alignment_skin_revision');A=Path('mods/appearance/ink_three_variants_20261004/female');Q=R/'qa';Q.mkdir(exist_ok=True)
for bg,label in [((235,229,217,255),'light'),((38,42,47,255),'dark')]:
 board=Image.new('RGB',(840,1200),bg[:3]);detail=Image.new('RGB',(960,840),bg[:3]);d=ImageDraw.Draw(board)
 for j,(f,b) in enumerate([(2,2),(2,3),(3,2),(3,3)]):
  im=Image.new('RGBA',(1024,1536),bg)
  for k in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
   ix=b if k=='hair_back' else f if k!='face_shape' else 4
   path=A/k/'01.png' if k=='clothing' else R/'female'/k/f'{ix:02}.png'
   im.alpha_composite(Image.open(path).convert('RGBA'))
  im.save(Q/f'corrected02_03_mix_f{f}_b{b}_{label}.png');board.paste(im.crop((310,40,730,640)).convert('RGB'),((j%2)*420,(j//2)*600));d.text(((j%2)*420+3,(j//2)*600+3),f'front{f} back{b}',fill='white' if label=='dark' else 'black');detail.paste(im.crop((400,350,640,560)).resize((480,420)).convert('RGB'),((j%2)*480,(j//2)*420))
 board.save(Q/f'corrected02_03_mix_{label}.jpg',quality=97);detail.save(Q/f'corrected02_03_nose_mouth_{label}.jpg',quality=97)
# all three feature sets for new face color at same native origin, isolated focus.
board=Image.new('RGB',(900,600),(235,229,217))
for i in [1,2,3]:
 im=Image.new('RGBA',(1024,1536),(235,229,217,255));im.alpha_composite(Image.open(R/'female/face_shape/04.png').convert('RGBA'))
 for k in ['eyes','eyebrows','nose','mouth']:im.alpha_composite(Image.open(R/'female'/k/f'{i:02}.png').convert('RGBA'))
 board.paste(im.crop((369,300,669,600)).resize((300,300)).convert('RGB'),((i-1)*300,0));board.paste(im.crop((469,393,569,493)).resize((300,300)).convert('RGB'),((i-1)*300,300))
board.save(Q/'new_skin_all_feature_sets.jpg',quality=97)
