"""Prepare four male/female daily-robes as visual drafts; no acquisition rules."""
from pathlib import Path
import json, hashlib
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
DRAFT=ROOT/'mods/appearance/yuxu_daily_drafts_20261005'
RANKS=[('zayi','杂役常服'),('waimen','外门常服'),('neimen','内门常服'),('zhenchuan','真传常服')]
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']

def main():
    records=[]
    board=Image.new('RGB',(1600,1190),'#e8e3dc');d=ImageDraw.Draw(board)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    for row,sex in enumerate(['male','female']):
        for col,(key,title) in enumerate(RANKS):
            src=DRAFT/'source'/f'{sex}_{key}.png'
            with Image.open(src) as raw:
                im=raw.convert('RGBA')
                assert im.size==(1024,1536)
                # Remove invisible RGB/encoder fringe only; no painted textile changes.
                alpha=im.getchannel('A');alpha=alpha.point(lambda x:0 if x<3 else x)
                im.putalpha(alpha)
                clean=Image.new('RGBA',im.size);clean.paste(im,(0,0))
                pixels=clean.load();aa=alpha.load()
                for y in range(clean.height):
                    for x in range(clean.width):
                        if aa[x,y]==0:pixels[x,y]=(0,0,0,0)
            out=DRAFT/sex/'clothing'/f'{col+1:02}.png';out.parent.mkdir(parents=True,exist_ok=True);clean.save(out)
            composite=Image.new('RGBA',(1024,1536))
            for part in ORDER:
                variant=4 if sex=='female' and part in ['face_shape','eyes','eyebrows','nose','mouth'] else 4 if sex=='male' and part in ['eyes','eyebrows'] else 1
                layer=clean if part=='clothing' else Image.open(PACK/sex/part/f'{variant:02}.png').convert('RGBA')
                composite.alpha_composite(layer)
            bg=Image.new('RGBA',composite.size,'#e8e3dc');bg.alpha_composite(composite)
            preview=DRAFT/'preview';preview.mkdir(exist_ok=True)
            bg.convert('RGB').save(preview/f'{sex}_{key}.png')
            thumb=bg.crop((0,130,1024,1536)).resize((390,536)).convert('RGB')
            board.paste(thumb,(col*400+5,row*595+45));d.text((col*400+15,row*595+12),f'{"男" if sex=="male" else "女"} · 玉虚{title}',font=font,fill='#352f2b')
            records.append({'sex':sex,'rank':key,'name':'玉虚·'+title,'file':f'{sex}/clothing/{col+1:02}.png',
                'source':str(src.relative_to(DRAFT)),'source_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),
                'transform':[1,0,0],'canvas':[1024,1536],'neck_collars':[[441,550],[594,550]] if sex=='male' else [[450,552],[584,552]],
                'frame_basis':'existing own portrait clothing template','visual_approved_by_user':False})
    board.save(DRAFT/'preview/yuxu_four_ranks_20261005.jpg',quality=95)
    manifest={'status':'visual_draft_only','enabled':False,'canvas':[1024,1536],
              'acquisition':'pending user decision','battle_costume':'pending user decision','registrations':records}
    (DRAFT/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (DRAFT/'appearance_parts.json').write_text(json.dumps({'enabled':False,'package_id':DRAFT.name,'components':[]},indent=2)+'\n',encoding='utf-8')
    print('Prepared eight RGBA clothing drafts and assembled previews; runtime remains disabled.')

if __name__=='__main__':main()
