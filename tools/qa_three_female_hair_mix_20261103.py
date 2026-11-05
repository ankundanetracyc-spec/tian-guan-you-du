from pathlib import Path
from PIL import Image,ImageDraw
P=Path('mods/appearance/portrait_rebuild_20261103');O=P/'three_style_parts';Q=O/'qa';face=Image.open('mods/appearance/ink_three_variants_20261004/female/face_shape/04.png').convert('RGBA')
for bg,label in [((235,229,217,255),'light'),((38,42,47,255),'dark')]:
 board=Image.new('RGB',(1260,1800),bg[:3]);d=ImageDraw.Draw(board)
 for f in range(1,4):
  for b in range(1,4):
   im=Image.new('RGBA',(1024,1536),bg);im.alpha_composite(Image.open(O/'female/hair_back'/f'{b:02}.png').convert('RGBA'));im.alpha_composite(face)
   im.alpha_composite(Image.open('mods/appearance/ink_three_variants_20261004/female/clothing/01.png').convert('RGBA'))
   for k in ['eyes','eyebrows','nose','mouth']:im.alpha_composite(Image.open(P/'single_style_sample/parts'/f'{k}.png').convert('RGBA'))
   im.alpha_composite(Image.open(O/'female/hair_front'/f'{f:02}.png').convert('RGBA'));im.save(Q/f'hair_mix_front{f}_back{b}_{label}.png');crop=im.crop((310,40,730,640));board.paste(crop.convert('RGB'),((b-1)*420,(f-1)*600));d.text(((b-1)*420+5,(f-1)*600+5),f'Front{f} Back{b}',fill='white' if label=='dark' else 'black')
 board.save(Q/f'hair_mix_9_heads_{label}.jpg',quality=95)
