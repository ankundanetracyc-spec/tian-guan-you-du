"""Crop generated transparent atlas and show unmodified cutouts on the sample background."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/美术规范/百川简笔底图与通用节点模板_20261028'
version = sys.argv[1] if len(sys.argv) > 1 else 'v01'
assert version in ['v01', 'v02']
atlas = Image.open(OUT / f'通用节点图集_原图_{version}.png').convert('RGBA')
assert atlas.getchannel('A').getextrema()[0] == 0
w,h = atlas.size
cell = w // 3
bg = Image.open(OUT / f'通用地貌底图_{version}.png').convert('RGBA')
plate = bg.copy()
records = []
names = ['宗门', '城池', '镇集']
display_widths = [140, 128, 100]
cards = []
overlay = []
for i,name in enumerate(names):
    area = atlas.crop((i*cell, 0, (i+1)*cell, h))
    bbox = area.getchannel('A').point(lambda a: 255 if a > 32 else 0).getbbox()
    assert bbox is not None
    rect = (max(0,bbox[0]-16), max(0,bbox[1]-16), min(cell,bbox[2]+16), min(h,bbox[3]+16))
    icon = area.crop(rect)
    filename = f'{name}通用模板_{version}.png'
    icon.save(OUT / filename)
    width = display_widths[i]
    height = round(icon.height*width/icon.width)
    thumb = icon.resize((width,height),Image.Resampling.LANCZOS)
    x = round(bg.width * [0.26,0.50,0.73][i] - width/2)
    y = round(bg.height*0.62 - height)
    plate.alpha_composite(thumb,(x,y))
    records.append({'type':name,'file':filename,'nativeSize':list(icon.size),'displaySize':[width,height],'position':[x,y],'alphaRange':list(icon.getchannel('A').getextrema()),'sha256':hashlib.sha256((OUT/filename).read_bytes()).hexdigest()})
    cards.append(f'<a class="card" href="{filename}"><img src="{filename}" alt="{name}通用模板"><p>{name}模板 · 透明PNG</p></a>')
    overlay.append(f'<img class="node" src="{filename}" alt="{name}通用模板" style="left:{x}px;top:{y}px;width:{width}px;height:{height}px">')
plate.convert('RGB').save(OUT/f'三个模板叠放效果_{version}.png')
manifest={'backgroundNativeSize':list(bg.size),'atlasNativeSize':list(atlas.size),'templates':records,'processing':'Native AI images; atlas cropping preserves alpha; preview only downsizes cutouts and composites them onto generated terrain. No new painted detail.'}
(OUT/f'模板清单_{version}.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>百川 · 通用节点模板试阅</title><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="icon" href="data:,"><style>*{box-sizing:border-box}body{margin:0;background:#e1d3b8;color:#4f4435;font-family:"Microsoft YaHei",sans-serif}header,section{max-width:1580px;margin:auto;padding:18px 22px}h1{font-family:KaiTi,serif;font-size:30px;font-weight:normal;margin:0 0 8px}p{line-height:1.7}header p{margin:4px 0;font-size:14px}label{display:inline-block;margin:8px 0}.viewer{width:100%;overflow:auto;border:1px solid #ac9876;background:#c3ac83}.stage{position:relative;width:__W__px;height:__H__px}.base{display:block;width:__W__px;height:__H__px}.node{position:absolute;object-fit:contain;pointer-events:none}.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin-top:20px}.card{text-align:center;padding:20px;background:#c9b48b;color:inherit;text-decoration:none}.card img{width:auto;max-width:100%;height:160px;object-fit:contain}.foot{font-size:13px;color:#7d6d56}@media(max-width:700px){.cards{grid-template-columns:1fr}.card img{height:140px}}</style><header><h1>百川 · 简笔底图与通用节点模板</h1><p>仅试画风：左为宗门，中为城池，右为镇集。地形、建筑各为独立图片，未采用参考截图的地图布局、路线、人物或界面。</p><label><input id="nodes" type="checkbox" checked>显示三个节点模板</label><p>底图按原像素显示；窄窗口可横向滚动，无放大操作。</p></header><section><div class="viewer"><div class="stage"><img class="base" src="通用地貌底图_v01.png" alt="通用地貌底图">__NODES__</div></div><div class="cards">__CARDS__</div><p class="foot">单独模板为较大检视图；上方地图叠放宽度依次140、128、100像素。此页是通用样式试阅，不代表正式地点或九块底图已换稿。<a href="README.md">制作说明</a> · <a href="三个模板叠放效果_v01.png">叠放效果PNG</a></p></section><script>document.getElementById('nodes').addEventListener('change',e=>document.querySelectorAll('.node').forEach(n=>n.hidden=!e.target.checked));</script></html>'''
page=page.replace('__W__',str(bg.width)).replace('__H__',str(bg.height)).replace('__NODES__',''.join(overlay)).replace('__CARDS__',''.join(cards)).replace('_v01.png',f'_{version}.png')
if version == 'v02':
    page=page.replace('仅试画风：左为宗门，中为城池，右为镇集。','森林与建筑细化版：左为宗门，中为城池，右为镇集。').replace('地形、建筑各为独立图片，','底图增加少量森林点缀，建筑增加廊院、门楼、角楼和布棚；地形、建筑各为独立图片，')
    page=page.replace('<label><input id="nodes"', '<label><input id="forest" type="checkbox" checked>森林点缀</label>　<label><input id="nodes"')
    page=page.replace('<script>', '<script>document.getElementById("forest").addEventListener("change",e=>document.querySelector(".base").src=e.target.checked?"通用地貌底图_v02.png":"通用地貌底图_v01.png");')
    page=page.replace('制作说明</a>', '制作说明</a> · <a href="通用模板效果_v01.html">上一版对照</a>')
(OUT/f'通用模板效果_{version}.html').write_text(page,encoding='utf-8')
(OUT/'通用模板效果.html').write_text(page,encoding='utf-8')
print(json.dumps({'background':list(bg.size),'atlas':list(atlas.size),'templates':[{k:j[k] for k in ['type','nativeSize','displaySize','alphaRange']} for j in records]},ensure_ascii=False))
