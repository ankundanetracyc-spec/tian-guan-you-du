"""Make a self-contained offline review package and verify every image path."""
from pathlib import Path
import json,shutil,zipfile,hashlib,re
from PIL import Image
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/acceptance_20261030'
NODES=ROOT/'textures/world/bai_chuan/node_sprites_20261030'
DOC=ROOT/'docs/美术规范/百川地图与节点验收_20261030'
OUT=DOC/'验收包'
for sub in ['背景','节点','节点源图','九块底图','生成记录','检查记录']:(OUT/sub).mkdir(parents=True,exist_ok=True)
shutil.copy2(ART/'master_3762.png',OUT/'背景/master.png')
shutil.copy2(ART/'palette_reference.png',OUT/'背景/reference.png')
master=np.array(Image.open(ART/'master_3762.png').convert('RGB'))
for row in range(3):
    for col in range(3):
        ident=f'bc_{row+1}{col+1}';tile=ART/'tiles'/f'{ident}.png'
        assert Image.open(tile).size==(1254,1254)
        assert np.array_equal(np.array(Image.open(tile)),master[row*1254:(row+1)*1254,col*1254:(col+1)*1254])
        shutil.copy2(tile,OUT/'九块底图'/tile.name)
        shutil.copy2(ART/'recolored'/f'{ident}.record.json',OUT/'生成记录'/f'{ident}.json')
data=json.loads((ART/'presentation.json').read_text(encoding='utf-8'))
data['background']='背景/master.png';data['paletteReference']='背景/reference.png'
for a in data['assets']:
    a['src']='节点/'+a['id']+'.png'
    shutil.copy2(NODES/'icons'/f"{a['id']}.png",OUT/'节点'/f"{a['id']}.png")
    shutil.copy2(NODES/'source'/f"{a['id']}.png",OUT/'节点源图'/f"{a['id']}.png")
    prompt=ROOT/'docs/美术规范/百川节点透明贴图_20261030'/f"{a['id']}_提示词.txt"
    shutil.copy2(prompt,OUT/'生成记录'/prompt.name)
for n in data['nodes']:
    if n['asset']:n['src']='节点/'+n['asset']+'.png'
assert len(data['assets'])==9 and len(data['nodes'])==123
for path in [data['background'],data['paletteReference']]+[a['src'] for a in data['assets']]+[n['src'] for n in data['nodes'] if n['asset']]:assert (OUT/path).is_file(),path
(OUT/'presentation.js').write_text('window.MAP_REVIEW='+json.dumps(data,ensure_ascii=False)+';',encoding='utf-8')
(OUT/'素材清单.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
html=(DOC/'百川地图与节点验收.html').read_text(encoding='utf-8').replace('href="百川地图验收包.zip"','href="素材清单.json"').replace('下载验收包</a>','素材清单</a>')
(OUT/'index.html').write_text(html,encoding='utf-8')
for name in ['底图尺寸与水域检查.json','落位检查.json','浏览器验收.json','交付核验.json']:
    if (DOC/name).exists():shutil.copy2(DOC/name,OUT/'检查记录'/name)
(OUT/'阅读说明.txt').write_text('完整解压后，用浏览器打开 index.html。点击“实际尺寸”和三大宗门检查大小。支持地点筛选、名称/节点/自然地点/九宫格开关与悬停点击。九块各1254，整图3762；独立美术验收预览，未替换游戏。节点源图和生成记录均附带。',encoding='utf-8')
plan=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
record={'grid':[3,3],'tile_pixels':[1254,1254],'world_pixels':[3762,3762],'reassembled_pixels_equal':True,'node_assets':9,'built_nodes':53,'named_nodes':123,'logical_plan_sha256':hashlib.sha256(plan.read_bytes()).hexdigest(),'native_2048_ready':False,'shared_low_resolution_reference_edges':True,'game_runtime_changed':False,'portable_resource_paths_valid':True}
(DOC/'交付核验.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
shutil.copy2(DOC/'交付核验.json',OUT/'检查记录/交付核验.json')
with zipfile.ZipFile(DOC/'百川地图验收包.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in OUT.rglob('*'):
        if p.is_file():z.write(p,p.relative_to(OUT))
with zipfile.ZipFile(DOC/'百川地图验收包.zip') as z:assert z.testzip() is None
print(json.dumps({'files':sum(p.is_file() for p in OUT.rglob('*')),'zip_bytes':(DOC/'百川地图验收包.zip').stat().st_size,'portable_resources':'valid'},ensure_ascii=False))
