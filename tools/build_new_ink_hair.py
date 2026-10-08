from pathlib import Path
import shutil,json
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parents[1]
R=P/'mods/appearance/new_ink_hair_20261003_v1'
OLD=P/'mods/appearance/selected_ink_portrait_clean_20261003_v1'
G=Path('D:/CodexData/generated_images/01a0fd74-de00-7ab2-9dfc-c15a97009561')
SOURCES={'male_front':'exec-d8a1bc66-0ad1-40b9-8773-0f98e0e8cc0c.png','male_back':'exec-5729e058-bc74-4594-b0a0-854ad42ef028.png','female_front':'exec-3cdff7e0-19ba-46a6-90f4-590e64aba155.png','female_back':'exec-2a5c379c-526b-4a3e-b9b0-275227e535fc.png'}
TRANSFORMS={'male_front':(.65,184,112),'male_back':(.85,77,22),'female_front':(.62,198,102),'female_back':(.85,84,98)}
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
def composite(layers,hidden=''):
    out=Image.new('RGBA',(1024,1536))
    for k in ORDER:
        if k!=hidden:out.alpha_composite(layers[k])
    return out
def light(a):
    b=Image.new('RGBA',a.size,'#e8e3dc');b.alpha_composite(a);return b.convert('RGB')
def main():
    (R/'preview').mkdir(parents=True,exist_ok=True)
    board=Image.new('RGB',(1536,1576),'#e8e3dc');font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
    for row,sex in enumerate(['male','female']):
        (R/sex).mkdir(exist_ok=True);layers={}
        for key in ORDER:
            if key.startswith('hair_'):
                name=sex+'_'+key[5:];src=R/'source'/f'{name}.png';shutil.copy2(G/SOURCES[name],src)
                a=Image.open(src).convert('RGBA');s,x,y=TRANSFORMS[name]
                a=a.convert('RGBa').resize((round(a.width*s),round(a.height*s)),Image.Resampling.LANCZOS).convert('RGBA')
                out=Image.new('RGBA',(1024,1536));out.alpha_composite(a,(x,y));layers[key]=out
            else:layers[key]=Image.open(OLD/sex/f'{key}.png').convert('RGBA')
            layers[key].save(R/sex/f'{key}.png')
        for col,hidden in enumerate(['','hair_back','hair_front']):
            a=composite(layers,hidden);name='full' if not hidden else 'without_'+hidden
            light(a).save(R/'preview'/f'{sex}_{name}.png')
            board.paste(light(a).resize((512,768)),(col*512,row*788+20))
            ImageDraw.Draw(board).text((col*512+10,row*788),['全部显示','仅前发','仅后发'][col],font=font,fill='#302a25')
    board.save(R/'preview/hair_ownership_check.png')
    (R/'registration.json').write_text(json.dumps({'method':'single uniform scale and translation; no local warp','transforms':TRANSFORMS,'status':'visual fitting candidate; not three-point solver certified'},indent=2),encoding='utf-8')
if __name__=='__main__':
    main()
    if (R/'face_revision.json').exists():
        import runpy
        runpy.run_path(str(P/'tools/refine_female_ink_face.py'),run_name='__main__')
