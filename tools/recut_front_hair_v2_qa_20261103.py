from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'mods/appearance/portrait_rebuild_20261103/alignment_skin_revision/recut_v2/qa'
OUT.mkdir(parents=True, exist_ok=True)
im = Image.open(ROOT / 'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_01.png').convert('RGB')
font = ImageFont.truetype('arial.ttf', 13)
for name, box in {
    'cap_grid': (120, 80, 490, 400),
    'left_grid': (135, 310, 250, 887),
    'right_grid': (380, 310, 480, 887),
    'left_bottom_grid': (198, 780, 240, 887),
    'right_bottom_grid': (392, 780, 438, 887),
}.items():
    scale = 8 if 'bottom' in name else 4
    tile = im.crop(box).resize(((box[2]-box[0])*scale, (box[3]-box[1])*scale))
    draw = ImageDraw.Draw(tile)
    spacing = 2 if 'bottom' in name else 10
    for x in range((box[0]//spacing+1)*spacing,box[2],spacing):
        xx=(x-box[0])*scale
        draw.line([(xx,0),(xx,tile.height)], fill=(0,160,180), width=1)
        for y in range((box[1]//50+1)*50,box[3],50):
            yy=(y-box[1])*scale
            draw.text((xx+1,yy+1),str(x),font=font,fill=(250,250,240),stroke_width=1,stroke_fill=(20,40,40))
    for y in range((box[1]//10+1)*10,box[3],10):
        yy=(y-box[1])*scale
        draw.line([(0,yy),(tile.width,yy)], fill=(0,160,180), width=1)
        draw.text((2,yy+1),str(y),font=font,fill=(250,250,240),stroke_width=1,stroke_fill=(20,40,40))
    tile.save(OUT/(name+'.png'))
