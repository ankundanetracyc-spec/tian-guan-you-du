from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1] / 'mods/appearance/腰上男修拆件演示_v2'
src = root / 'test_sources'
names = ['blank_full'] + [f'{kind}_full_{i}' for kind in ('brow','eye','nose','mouth','beard') for i in range(1,5)]
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
sheet = Image.new('RGB',(5*280,5*330),(225,222,215))
draw = ImageDraw.Draw(sheet)
for j,name in enumerate(names):
    im=Image.open(src/(name+'.png')).convert('RGBA')
    crop=im.crop((372,245,652,555))
    x=(j%5)*280; y=(j//5)*330
    sheet.paste(crop,(x,y+20),crop)
    draw.text((x+4,y+1),name,font=font,fill=(25,25,25))
sheet.save(root/'preview'/'feature_source_sheet.png')
cutnames=[f'hair_cut_{i}' for i in range(5)]+[f'clothes_cut_{i}' for i in (0,2,3,4)]
cuts=Image.new('RGB',(5*300,2*470),(218,216,211))
cd=ImageDraw.Draw(cuts)
for j,name in enumerate(cutnames):
    im=Image.open(src/(name+'.png')).convert('RGBA')
    im.thumbnail((300,440))
    x=(j%5)*300;y=(j//5)*470
    cuts.paste(im,(x,y+25),im)
    cd.text((x+4,y+1),name,font=font,fill=(25,25,25))
cuts.save(root/'preview'/'cutout_source_sheet.png')
for name in cutnames:
    a=Image.open(src/(name+'.png')).convert('RGBA').getchannel('A')
    print(name, a.getbbox())
