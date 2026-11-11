"""Read-only diagnostic alpha compositions; never edit source/component PNGs."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import argparse, json

PACK=Path(__file__).resolve().parents[1]
OLD=PACK/'reference/current_male'; NEW=PACK/'male'; Q=PACK/'qa'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
SELECTED={'hair_front','hair_back','eyebrows','eyes'}
ORDER=['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','face_accessory','hair_front']

def layer(part,index):
    path=(NEW if part in SELECTED and index==5 else OLD)/part/f'{index:02}.png'
    return Image.open(path).convert('RGBA')

def compose(bg,front=5,back=5,brows=5,eyes=5,clothes=1,nohair=False):
    out=Image.new('RGBA',(1024,1536),bg)
    choices={'hair_front':front,'hair_back':back,'eyebrows':brows,'eyes':eyes,'clothing':clothes,'clothing_back':clothes}
    for part in ORDER:
        if nohair and part.startswith('hair_'):continue
        out.alpha_composite(layer(part,choices.get(part,1)))
    return out

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--hair-only',action='store_true');args=parser.parse_args()
    brows=eyes=1 if args.hair_only else 5
    required={'hair_front','hair_back'} if args.hair_only else SELECTED
    missing=[part for part in required if not (NEW/part/'05.png').is_file()]
    if missing:raise SystemExit('Required new layers missing: '+','.join(missing))
    prefix='hair_only_diagnostic' if args.hair_only else 'candidate_selected'
    for theme,bg in [('light','#e8e1d6'),('dark','#202b31')]:
        color='#eee8dc' if theme=='dark' else '#33362e'
        compose(bg,brows=brows,eyes=eyes).save(Q/f'{prefix}_{theme}.png')
        crop=compose(bg,brows=brows,eyes=eyes).crop((345,210,680,460)).resize((1340,1000),Image.Resampling.LANCZOS)
        crop.save(Q/f'{prefix}_hairline_{theme}_4x.png')
        mixes=Image.new('RGB',(1500,2100),bg);draw=ImageDraw.Draw(mixes)
        for k,(front,back) in enumerate((f,b) for f in (1,4,5) for b in (1,4,5)):
            pic=compose(bg,front=front,back=back,brows=brows,eyes=eyes).crop((245,55,800,800)).resize((500,660),Image.Resampling.LANCZOS)
            pos=((k%3)*500,(k//3)*700)
            mixes.paste(pic,(pos[0],pos[1]+40));draw.text((pos[0]+10,pos[1]+5),f'前发{front:02} / 后发{back:02}',font=FONT,fill=color)
        mixes.save(Q/f'{prefix}_nine_hair_mixes_{theme}.png')
        if not args.hair_only:
            compose(bg,nohair=True).save(Q/f'candidate_nohair_{theme}.png')
            collars=Image.new('RGB',(1400,920),bg);draw=ImageDraw.Draw(collars)
            for k in range(7):
                pic=compose(bg,nohair=True,clothes=k+1).crop((365,495,660,800)).resize((350,360),Image.Resampling.LANCZOS)
                pos=((k%4)*350,(k//4)*460);collars.paste(pic,(pos[0],pos[1]+40));draw.text((pos[0]+10,pos[1]+5),f'衣服{k+1:02}',font=FONT,fill=color)
            collars.save(Q/f'seven_collars_{theme}.png')
    if not args.hair_only:
        geom=json.loads((Q/'geometry_and_dedup.json').read_text(encoding='utf-8-sig'))
        (a,b,tx),(c,d,ty)=geom['joint_source_to_frame']['matrix_2x3'];det=a*d-b*c
        inv=(d/det,-b/det,(b*ty-d*tx)/det,-c/det,a/det,(c*tx-a*ty)/det)
        source=Image.open(PACK/'source/approved_male.png').convert('RGBA').transform((1024,1536),Image.Transform.AFFINE,inv,Image.Resampling.BICUBIC)
        sheet=Image.new('RGB',(1200,800),'#e8e1d6');draw=ImageDraw.Draw(sheet)
        for k,(im,label) in enumerate([(source,'所选原稿·共同定位'),(compose('#e8e1d6'),'实际拼合·鼻嘴沿用原款')]):
            pic=im.crop((360,245,660,625)).resize((600,760),Image.Resampling.LANCZOS)
            sheet.paste(pic,(k*600,40));draw.text((k*600+12,5),label,font=FONT,fill='#33362e')
        sheet.save(Q/'source_vs_selected_face.png')
    print(json.dumps({'mode':prefix,'new_layers':sorted(required),'old_nose_mouth_reused':True}))

if __name__=='__main__':main()
