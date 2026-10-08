"""Read original installed-game eye resources; research outputs never enter MODs."""
from pathlib import Path
import json,sys
import UnityPy

GAME=Path('E:/SteamLibrary/steamapps/common/鬼谷八荒/guigubahuang_Data')
OUT=Path('E:/stacklands/tmp/guigu_eye_diversity_20261006')
OUT.mkdir(parents=True,exist_ok=True)
if not (OUT/'eye_resources.json').exists() or '--refresh' in sys.argv:
    mapping_env=UnityPy.Environment()
    mapping=mapping_env.load_file(str(GAME/'globalgamemanagers'))
    manager=next(o for o in mapping.objects.values() if o.type.name=='ResourceManager')
    data=manager.read_typetree()
    refs=[(key,pointer) for key,pointer in data['m_Container'] if '/portrait/' in key.lower() and '/eyes/' in key.lower()]
    for _,ptr in refs:
        assert mapping.externals[ptr['m_FileID']-1].path.endswith('resources.assets')
    (OUT/'eye_resources.json').write_text(json.dumps(refs,ensure_ascii=False,indent=2),encoding='utf-8')
    del data,mapping,mapping_env
refs=json.loads((OUT/'eye_resources.json').read_text(encoding='utf-8'))
print('mapped eye resources',len(refs),flush=True)
if '--extract' not in sys.argv:raise SystemExit()
env=UnityPy.Environment()
env.path=str(GAME)
env.load_file(str(GAME/'resources.assets.resS'))
asset=env.load_file(str(GAME/'resources.assets'))
print('objects',len(asset.objects),flush=True)
if '--extract' in sys.argv:
    from PIL import Image,ImageDraw,ImageFont
    refs=[x for x in refs if '/man/' in x[0]][:30]
    record=[]
    def sprites(go_id,seen=None):
        seen=seen or set()
        if go_id in seen:return []
        seen.add(go_id)
        go=asset.objects[go_id].read()
        found=[]
        for component in go.m_Component:
            ptr=component.component if hasattr(component,'component') else component[1]
            obj=ptr.deref();data=obj.read()
            if obj.type.name=='SpriteRenderer' and data.m_Sprite.path_id:
                found.append(data.m_Sprite.deref().read())
            elif obj.type.name in ['Transform','RectTransform']:
                for child in data.m_Children:
                    found+=sprites(child.deref().read().m_GameObject.path_id,seen)
        return found
    for key,ptr in refs:
        try:
            found=sprites(ptr['m_PathID'])
            for i,sprite in enumerate(found):
                im=sprite.image.convert('RGBA')
                name=key.rsplit('/',1)[1]+f'_{i}'
                im.save(OUT/(name+'.png'))
                record.append({'resource':key,'go_path_id':ptr['m_PathID'],'sprite':sprite.m_Name,'file':name+'.png','size':list(im.size)})
            print(key,len(found),flush=True)
        except Exception as exc:print(key,type(exc).__name__,str(exc)[:160],flush=True)
    (OUT/'extracted_sources.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
    assert record,'No eye sprites were extracted'
    sheet=Image.new('RGB',(1200,((len(record)+3)//4)*115),'#d9c1ad');draw=ImageDraw.Draw(sheet)
    for n,r in enumerate(record):
        im=Image.open(OUT/r['file']).convert('RGBA');im.thumbnail((278,68))
        x=n%4*300;y=n//4*115
        sheet.paste(im,(x+(300-im.width)//2,y+35),im)
        draw.text((x+8,y+5),r['resource'].rsplit('/',1)[1]+' · '+r['sprite'],font=font,fill='#352f2b')
    sheet.save(OUT/'original_male_eye_diversity.jpg',quality=95)
    raise SystemExit()
