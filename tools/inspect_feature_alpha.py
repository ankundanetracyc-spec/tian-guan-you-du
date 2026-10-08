from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

p=Path(__file__).resolve().parents[1]/'mods/appearance/腰上男修拆件演示_v2'
asset=p/'test_assets_v3'
sheet=Image.new('RGB',(5*240,2*170),(232,224,211))
d=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
for row,kind in enumerate(('brow','eye')):
    for i in range(5):
        im=Image.open(asset/f'{kind}_{i}.png').convert('RGBA')
        crop=im.crop((395,305,635,445))
        x=i*240;y=row*170
        sheet.paste(crop,(x,y+24),crop)
        d.text((x+6,y+3),f'{kind} {i}',font=font,fill=(35,35,35))
        a=im.getchannel('A')
        print(kind,i,[(yy,sum(a.getpixel((xx,yy))>128 for xx in range(410,615))) for yy in range(315,401,5)])
sheet.save(p/'preview/feature_alpha_before_v4.png')
rows=Image.new('RGB',(5*320,360),(230,222,210))
rd=ImageDraw.Draw(rows)
for i in range(5):
    canvas=Image.new('RGBA',(1024,1536))
    for key,n in [('hair_back',0),('face',0),('clothes',0),('eye',i),('brow',0),('nose',0),('mouth',0),('hair_front',0)]:
        canvas.alpha_composite(Image.open(asset/f'{key}_{n}.png').convert('RGBA'))
    crop=canvas.crop((352,265,672,600))
    rows.paste(crop,(i*320,25),crop)
    rd.text((i*320+6,3),f'眼型 {i}',font=font,fill=(35,35,35))
rows.save(p/'preview/eye_brow_gap_v4.png')
rows2=Image.new('RGB',(5*320,360),(230,222,210))
for i in range(5):
    canvas=Image.new('RGBA',(1024,1536))
    for key,n in [('face',0),('clothes',0),('eye',i),('brow',0),('nose',0),('mouth',0)]:
        canvas.alpha_composite(Image.open(asset/f'{key}_{n}.png').convert('RGBA'))
    crop=canvas.crop((352,265,672,600))
    rows2.paste(crop,(i*320,25),crop)
rows2.save(p/'preview/eye_brow_without_hair_v4.png')
