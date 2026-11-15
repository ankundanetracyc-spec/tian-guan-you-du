"""Read-only layer diagnostic at the exact user-reported crop."""
from pathlib import Path
from PIL import Image,ImageFont,ImageDraw
P=Path(__file__).resolve().parents[1];R=P/'reference/current_male';Q=P/'qa'
old=Image.open(R/'eyes/04.png').convert('RGBA')
new=Image.open(P/'candidate/new_male_eyes04.png').convert('RGBA')
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
def compose(eyes,front):
    out=Image.new('RGBA',(1024,1536),'#e8e1d6')
    for part,index in [('hair_back',4),('face_shape',1),('nose',1),('eyes',4),('eyebrows',4),('mouth',1)]+([('hair_front',front)] if front else []):
        layer=eyes if part=='eyes' else Image.open(R/part/f'{index:02}.png').convert('RGBA')
        out.alpha_composite(layer)
    return out
exact=Image.new('RGB',(432,336),'#e8e1d6');draw=ImageDraw.Draw(exact)
full=Image.new('RGB',(680,440),'#e8e1d6');fd=ImageDraw.Draw(full)
for col,(eyes,label) in enumerate([(old,'修复前'),(new,'修复后')]):
    im=compose(eyes,4)
    crop=im.crop((395,315,495,420)).resize((176,185),Image.Resampling.BILINEAR).crop((47,54,101,129))
    exact.paste(crop.resize((216,300),Image.Resampling.NEAREST),(col*216,36));draw.text((col*216+12,6),label,font=font,fill='#33362e')
    for row,front in enumerate((None,4,5)):
        pic=compose(eyes,front).crop((350,318,690,433)).convert('RGB')
        full.paste(pic,(col*340,row*145+30));fd.text((col*340+8,row*145+3),label+(' · 无前发' if not front else f' · 前发{front:02}'),font=font,fill='#33362e')
exact.save(Q/'reported_seam_before_after.png');full.save(Q/'eye_area_before_after_1x.png')
print('Saved exact reported crop and native-size before/after diagnostics.')
