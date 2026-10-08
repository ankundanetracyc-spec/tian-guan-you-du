"""Inspect selected installed-game hair sprites; no original art enters MODs."""
from pathlib import Path
import json
import UnityPy
from PIL import Image, ImageDraw, ImageFont

P=Path(__file__).resolve().parents[1]
GAME=Path('E:/SteamLibrary/steamapps/common/鬼谷八荒/guigubahuang_Data')
OUT=P/'tmp/guigu_hair_diversity_20261006'
refs=json.loads((OUT/'hair_resources.json').read_text(encoding='utf-8'))
selected=[]
for sex in ['man','woman']:
 for slot in ['hairfront','hair']:
  group=[r for r in refs if f'/{sex}/{slot}/' in r[0]]
  # Evenly sample the catalogue rather than treating colour repeats as variety.
  selected += [group[round(i*(len(group)-1)/11)] for i in range(12)]
env=UnityPy.Environment();env.path=str(GAME)
env.load_file(str(GAME/'resources.assets.resS'))
asset=env.load_file(str(GAME/'resources.assets'))
def sprites(go_id,seen=None):
 seen=seen or set()
 if go_id in seen:return []
 seen.add(go_id);go=asset.objects[go_id].read();found=[]
 for component in go.m_Component:
  ptr=component.component if hasattr(component,'component') else component[1]
  obj=ptr.deref();data=obj.read()
  if obj.type.name=='SpriteRenderer' and data.m_Sprite.path_id:
   found.append(data.m_Sprite.deref().read())
  elif obj.type.name in ['Transform','RectTransform']:
   for child in data.m_Children:
    found+=sprites(child.deref().read().m_GameObject.path_id,seen)
 return found
records=[]
for key,ptr in selected:
 for i,s in enumerate(sprites(ptr['m_PathID'])):
  im=s.image.convert('RGBA');name=key.replace('/','_')+f'_{i}.png'
  im.save(OUT/name)
  records.append(dict(resource=key,sprite=s.m_Name,file=name,size=list(im.size)))
 print(key,flush=True)
(OUT/'extracted_sources.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
for sex in ['man','woman']:
 r=[x for x in records if f'/{sex}/' in x['resource']]
 sheet=Image.new('RGB',(1200,((len(r)+5)//6)*265),'#d6cfbf');d=ImageDraw.Draw(sheet)
 for n,item in enumerate(r):
  im=Image.open(OUT/item['file']);im.thumbnail((190,225))
  x=n%6*200;y=n//6*265
  sheet.paste(im,(x+(200-im.width)//2,y+28),im)
  d.text((x+5,y+3),'/'.join(item['resource'].split('/')[-2:]),font=font,fill='#34312b')
 sheet.save(OUT/f'original_{sex}_hair.jpg',quality=94)
