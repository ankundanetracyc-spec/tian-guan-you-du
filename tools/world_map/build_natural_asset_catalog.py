"""Index original PNGs and compose a presentation sheet without editing source pixels."""
from pathlib import Path
import collections,json
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
OLD=ROOT/'textures/world_map/bai_chuan/natural_scenery_20261009_v2'
NEW=ROOT/'textures/world_map/bai_chuan/natural_families_20261009'
FAMILIES=['peak','hill','broadleaf','pine','willow','bamboo','deadwood','boulder','terrace','tree_rock','shrub','bank','stream','lotus','spring','pond','cliff','cave']
LABELS=['高峰与小山群','低丘与山脊','阔叶乔木','松树','柳树','竹丛','枯木','散石','岩台','树石组合','低灌木','岸景','浅溪','荷叶','泉石','小潭','短崖','洞口']
old_family=['peak','hill','peak','peak','hill','hill','broadleaf','broadleaf','pine','willow','broadleaf','bamboo','boulder','terrace','tree_rock','bank','stream','lotus','spring','bank']
catalog=[]
def index(path,family,name,footprint):
    im=Image.open(path).convert('RGBA')
    bounds=im.getchannel('A').point(lambda a:255 if a>25 else 0).getbbox()
    if bounds is None:raise ValueError(f'Empty image: {path.name}')
    catalog.append(dict(id=path.stem,family=family,name=name,path='res://'+path.relative_to(ROOT).as_posix(),footprint=footprint,size=list(im.size),bounds=list(bounds),weight=1))
for family,item in zip(old_family,json.loads((OLD/'asset_manifest.json').read_text(encoding='utf-8'))['assets']):
    index(OLD/item['file'],family,item['name'],item['suggested_footprint'])
for job in json.loads((NEW/'prompts.json').read_text(encoding='utf-8'))['jobs']:
    index(NEW/(job['id']+'.png'),job['family'],job['name'],job['footprint'])
data=dict(version=1,tool='built-in imagegen',families=[dict(id=f,name=n) for f,n in zip(FAMILIES,LABELS)],assets=catalog,ground='res://textures/world_map/bai_chuan/natural_scenery_20261009_v2/21_warm_paper_tile.png')
(ROOT/'resources/world/bai_chuan/natural_asset_catalog.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
groups=collections.defaultdict(list)
for a in catalog:groups[a['family']].append(a)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',16)
sheet=Image.new('RGB',(1920,1800),'#e3cfb1');draw=ImageDraw.Draw(sheet)
draw.text((25,12),'百川自然地物 · 十八族 / 四十款 · 模板随机布局素材库',font=font,fill='#37403d')
draw.text((25,46),'各族两至三款；展示缩放不代表占格比例；草丛与其他生态暂不加入',font=small,fill='#5c6559')
for i,(family,label) in enumerate(zip(FAMILIES,LABELS)):
    x,y=(i%3)*640,80+(i//3)*285
    draw.text((x+20,y+5),f'{i+1:02d} {label}',font=font,fill='#37403d')
    for j,a in enumerate(groups[family]):
        im=Image.open(ROOT/a['path'][6:]).convert('RGBA').crop(a['bounds'])
        im.thumbnail((187,198),Image.Resampling.LANCZOS)
        sheet.paste(im,(x+j*210+(210-im.width)//2,y+40+(200-im.height)//2),im)
        draw.text((x+j*210+10,y+242),a['name'],font=small,fill='#454d43')
sheet.save(NEW/'family_contact_sheet.png')
print(json.dumps({'assets':len(catalog),'families':{f:len(groups[f]) for f in FAMILIES}},ensure_ascii=False))
