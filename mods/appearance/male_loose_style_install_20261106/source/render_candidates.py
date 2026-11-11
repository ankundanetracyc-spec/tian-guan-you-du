"""Diagnostic composites only; never modify source or active component pixels."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json

PACK = Path(__file__).resolve().parents[1]
OLD = PACK / 'reference/current_male'
Q = PACK / 'qa'
NEW = PACK / 'male'
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 23)
SLOTS = ['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','hair_front']
SELECTED = {'hair_front','hair_back','eyebrows','eyes'}

def image_for(part, index):
    file = NEW / part / f'{index:02}.png' if part in SELECTED and index == 4 else OLD / part / f'{index:02}.png'
    return Image.open(file).convert('RGBA')

def compose(bg, front=4, back=4, brows=4, eyes=4, hide_hair=False, clothes=1):
    out = Image.new('RGBA',(1024,1536),bg)
    choices = {'hair_front':front,'hair_back':back,'eyebrows':brows,'eyes':eyes,'clothing':clothes,'clothing_back':clothes}
    for part in SLOTS:
        if hide_hair and part.startswith('hair_'): continue
        out.alpha_composite(image_for(part,choices.get(part,1)))
    return out

def main():
    missing=[part for part in SELECTED if not (NEW/part/'04.png').is_file()]
    if missing: raise SystemExit('Selected new components missing: '+', '.join(missing))
    Q.mkdir(exist_ok=True)
    for theme,color in [('light','#e8e1d6'),('dark','#202b31')]:
        out=compose(color);out.save(Q/f'candidate_selected_{theme}.png')
        compose(color,hide_hair=True).save(Q/f'candidate_nohair_{theme}.png')
        sheet=Image.new('RGB',(1400,920),color);draw=ImageDraw.Draw(sheet)
        for k,(front,back) in enumerate([(1,1),(4,1),(1,4),(4,4)]):
            small=compose(color,front,back).crop((240,65,800,800)).resize((700,880),Image.Resampling.LANCZOS)
            # Four complete half-body views are a separate 2x2 diagnostic sheet.
            if k==0: mixes=Image.new('RGB',(1400,1840),color);mdraw=ImageDraw.Draw(mixes)
            pos=((k%2)*700,(k//2)*920)
            mixes.paste(small,(pos[0],pos[1]+40));mdraw.text((pos[0]+15,pos[1]+5),f'前发 {front:02} / 后发 {back:02}',font=FONT,fill='white' if theme=='dark' else '#33362e')
        mixes.save(Q/f'hair_mixes_{theme}.png')
        for k in range(7):
            cut=compose(color,hide_hair=True,clothes=k+1).crop((365,495,660,800)).resize((350,360),Image.Resampling.LANCZOS)
            pos=((k%4)*350,(k//4)*460)
            sheet.paste(cut,(pos[0],pos[1]+40));draw.text((pos[0]+10,pos[1]+4),f'衣服{k+1:02}',font=FONT,fill='white' if theme=='dark' else '#33362e')
        sheet.save(Q/f'seven_collars_{theme}.png')
    geom=json.loads((Q/'geometry_and_dedup.json').read_text(encoding='utf-8-sig'))
    m=geom['joint_source_to_frame']['matrix_2x3'];a,b,tx=m[0];c,d,ty=m[1];det=a*d-b*c
    inv=(d/det,-b/det,(b*ty-d*tx)/det,-c/det,a/det,(c*tx-a*ty)/det)
    source=Image.open(PACK/'source/approved_male.png').convert('RGBA').transform((1024,1536),Image.Transform.AFFINE,inv,Image.Resampling.BICUBIC)
    actual=compose('#e8e1d6')
    comparison=Image.new('RGB',(1200,800),'#e8e1d6');draw=ImageDraw.Draw(comparison)
    for k,(im,label) in enumerate([(source,'所选原稿·共同定位'),(actual,'实际拼合·鼻嘴沿用原款')]):
        cut=im.crop((360,245,660,625)).resize((600,760),Image.Resampling.LANCZOS)
        comparison.paste(cut,(k*600,40));draw.text((k*600+12,5),label,font=FONT,fill='#33362e')
    comparison.save(Q/'source_vs_selected_face.png')
    print(json.dumps({'new_parts':sorted(SELECTED),'all_selected_exist':True,'nose_mouth':'existing original parts reused after deduplication'},ensure_ascii=False))

if __name__=='__main__':main()
