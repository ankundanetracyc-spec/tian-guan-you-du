"""Diagnostic composites from old/candidate layers; component pixels stay untouched."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
PACK=Path(__file__).resolve().parents[1]
ACTIVE=PACK.parents[2]/'mods/appearance/ink_three_variants_20261004/male'
SNAP=PACK/'reference/current_male';Q=PACK/'qa'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
candidate=PACK/'candidate/new_male_eyes04.png'
if not candidate.is_file():raise SystemExit('Final candidate not yet available')
def layer(part,index):
    return Image.open(SNAP/part/f'{index:02}.png').convert('RGBA')
old=layer('eyes',4);new=Image.open(candidate).convert('RGBA')
def compose(eyes,hair,bg):
    out=Image.new('RGBA',(1024,1536),bg)
    sequence=[] if hair is None else [layer('hair_back',4)]
    sequence += [layer('face_shape',1),layer('nose',1),eyes,layer('eyebrows',4),layer('mouth',1)]
    if hair is not None:sequence += [layer('hair_front',hair)]
    for im in sequence:out.alpha_composite(im)
    return out
for theme,bg in [('light','#e8e1d6'),('dark','#202b31')]:
    sheet=Image.new('RGB',(1360,1080),bg);draw=ImageDraw.Draw(sheet)
    for row,hair in enumerate((None,1,4,5)):
        label='不加头发' if hair is None else f'前发{hair:02}'
        for col,(eyes,word) in enumerate(((old,'修正前'),(new,'修正后'))):
            pic=compose(eyes,hair,bg).crop((350,318,690,433)).resize((680,230),Image.Resampling.LANCZOS)
            sheet.paste(pic,(col*680,row*270+40));draw.text((col*680+12,row*270+5),f'{word} · {label}',font=FONT,fill='#eee8dc' if theme=='dark' else '#33362e')
    sheet.save(Q/f'before_after_eye_hair_comparison_{theme}.png')
    sheet.crop((0,540,1360,810)).save(Q/f'before_after_selected_hair04_{theme}.png')
    actual=Image.new('RGB',(362,110),bg);draw=ImageDraw.Draw(actual)
    for col,(eyes,word) in enumerate(((old,'修正前'),(new,'修正后'))):
        roi=compose(eyes,4,bg).crop((280,280,850,430)).resize((262,69),Image.Resampling.BILINEAR)
        pic=roi.crop((37,19,218,59));actual.paste(pic,(col*181,40));draw.text((col*181+5,5),word,font=FONT,fill='#eee8dc' if theme=='dark' else '#33362e')
    actual.save(Q/f'before_after_reported_scale_{theme}.png')
print(json.dumps({'candidate':str(candidate),'qa_sheets':'before_after_eye_hair_comparison_[light,dark].png','reported_scale':.46}))
