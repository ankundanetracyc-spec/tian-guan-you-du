"""Visual QA and geometry checks for current independent portrait layers."""
from pathlib import Path
import json,numpy as np
from PIL import Image,ImageDraw,ImageFont
from export_character_skin_ears import ROOT, PACKS, registered_image
from export_character_ink_hair import validate_front_envelope

# Follow the current anatomy contract when an older QA entry is invoked.
# Its historical x425/599 ear fixtures and y140 scalp views no longer apply.
skin_manifest=json.loads((ROOT/'tools/character_skin_ear_sources.json').read_text(encoding='utf-8'))
if skin_manifest.get('skin_contract_version')=='complete_skull_v15':
    import runpy
    runpy.run_path(str(ROOT/'tools/qa_character_portrait_anatomy.py'),run_name='__main__')
    raise SystemExit(0)

font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),18)
hair=json.loads((ROOT/'tools/character_ink_hair_sources.json').read_text(encoding='utf-8'))
old_hair=json.loads((ROOT/'mods/appearance/腰上男修拆件演示_v2/test_sources/before_compact_high_tie/character_ink_hair_sources.json').read_text(encoding='utf-8'))
for sex,(pack,assets) in PACKS.items():
    folder=ROOT/'mods/appearance'/pack;out=folder/'preview/portrait_cleanup_v14';out.mkdir(parents=True,exist_ok=True)
    layers={p:[Image.open(folder/assets/f'{p}_{i}.png').convert('RGBA') for i in range(5)] for p in ['hair_back','face','ear','clothes','eye','brow','nose','mouth','hair_front','beard' if sex=='male' else 'makeup']}
    order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','beard' if sex=='male' else 'makeup','hair_front']
    def compose(color,choice=None,front_override=None,only_skin=False):
        result=Image.new('RGBA',(1024,1536),color);choice=choice or {}
        for p in (['face','ear'] if only_skin else order):
            result.alpha_composite(front_override if p=='hair_front' and front_override else layers[p][choice.get(p,0)])
        return result
    for i in range(5):
        validate_front_envelope(layers['hair_front'][i],sex,f'{sex} front{i}')
        ear=np.asarray(layers['ear'][i].getchannel('A'));y,x=np.where(ear>64)
        assert 64<=y.max()-y.min()+1<=96,(sex,i,'ear height')
        assert y.min()>=348 and y.max()<=451,(sex,i,'ear placement')
        overlaps=[]
        for face in range(5):
            skin=np.asarray(layers['face'][face].getchannel('A'))
            for lo,hi in [(410,443),(581,614)]:
                overlaps.append(int(((skin[383:420,lo:hi]>200)&(ear[383:420,lo:hi]>128)).sum()))
        assert min(overlaps)>8,(sex,i,'floating root',overlaps)
        print(sex,'ear',i,'height',int(y.max()-y.min()+1),'root overlap minimum',min(overlaps))
    for theme,color in [('light','#ccc8bd'),('dark','#202526')]:
        text_color='black' if theme=='light' else 'white'
        sheet=Image.new('RGB',(1500,1530),color)
        for i in range(5):
            top=compose(color,{'hair_front':i,'hair_back':3})
            sheet.paste(top.crop((300,110,724,780)).resize((300,474)),(i*300,30))
            ImageDraw.Draw(sheet).text((i*300+8,5),f'前发{i}',font=font,fill=text_color)
            skin=compose(color,{'ear':i},only_skin=True)
            sheet.paste(skin.crop((365,340,659,457)).resize((294,117)),(i*300,535))
            ImageDraw.Draw(sheet).text((i*300+8,510),['标准耳','小巧耳','长耳','圆耳垂','贴耳'][i],font=font,fill=text_color)
            detail=compose(color,{'ear':i,'hair_front':4,'hair_back':3})
            sheet.paste(detail.crop((365,320,659,570)).resize((294,250)),(i*300,665))
            face=compose(color,{'brow':i,'ear':0,'hair_front':4,'hair_back':3})
            sheet.paste(face.crop((390,320,634,410)).resize((294,108)),(i*300,950))
            ImageDraw.Draw(sheet).text((i*300+8,925),f'眉型{i}',font=font,fill=text_color)
            if sex=='male':
                beard=compose(color,{'beard':i,'hair_front':4,'hair_back':3})
                sheet.paste(beard.crop((390,435,634,720)).resize((294,344)),(i*300,1100))
                ImageDraw.Draw(sheet).text((i*300+8,1075),f'胡须{i}',font=font,fill=text_color)
        sheet.save(out/f'cleanup_overview_{theme}.jpg')
        if sex=='male':
            before=next(a for a in old_hair['assets'] if (a['sex'],a['part'],a['index'])==('male','front',1))
            comparison=Image.new('RGB',(848,720),color)
            for n,front in enumerate([registered_image(before),layers['hair_front'][1]]):
                comp=compose(color,{'hair_back':2,'hair_front':1},front_override=front)
                comparison.paste(comp.crop((300,0,724,690)),(424*n,30))
                ImageDraw.Draw(comparison).text((424*n+12,5),['修改前：双侧长前发','修改后：高束保留高度'][n],font=font,fill=text_color)
            comparison.save(out/f'bilateral_before_after_{theme}.png')
        compose(color,{'hair_back':2,'hair_front':1,'ear':0}).save(out/f'portrait_{sex}_{theme}.png')
print('PORTRAIT_CLEANUP_GEOMETRY_PASS: ten scalp envelopes; ten small attached ears against five faces each.')
