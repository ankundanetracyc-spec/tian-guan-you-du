"""Verify the delivered raster package and assemble a native-pixel art review."""
import hashlib
import html
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'textures/world/bai_chuan/acceptance_v1'
DOC = ROOT / 'docs/美术规范/百川整图与节点落位验收_20261028'
SOURCE = ROOT / 'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
layout = json.loads((ART / 'presentation.json').read_text(encoding='utf-8'))
static = json.loads((DOC / '落位检查.json').read_text(encoding='utf-8'))
native = json.loads((DOC / '实机验收.json').read_text(encoding='utf-8'))
master = Image.open(DOC / '百川地表母图_6144.png').convert('RGB')
world = Image.open(DOC / '完整地图_含节点与道路_6144.png').convert('RGB')
assert master.size == world.size == (6144,6144)
tiles = []
for row in range(3):
    for col in range(3):
        name = f'bc_{row+1}{col+1}.png'
        tile = Image.open(ART / 'tiles' / name).convert('RGB')
        assert tile.size == (2048,2048), name
        expected = master.crop((col*2048,row*2048,(col+1)*2048,(row+1)*2048))
        error = max(x[1] for x in ImageChops.difference(tile,expected).getextrema())
        assert error == 0, (name,error)
        tiles.append({'file':name,'pixels':list(tile.size),'maximumPixelError':error,'sha256':hashlib.sha256((ART/'tiles'/name).read_bytes()).hexdigest()})

assets = []
for key,asset in layout['catalog'].items():
    path = ROOT / asset['file'].removeprefix('res://')
    image = Image.open(path)
    assert list(image.size) == asset['size']
    assert all(target <= source for target,source in zip(asset['size'],asset['sourceSize']))
    assets.append({'asset':key,'displayPixels':asset['size'],'sourcePixels':asset['sourceSize'],'enlarged':False})

font_path = ROOT / 'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'
font = ImageFont.truetype(str(font_path),18)
small = ImageFont.truetype(str(font_path),14)
footings = {'mountain_shelf':'山腰台地','riverside_setback':'临水退岸','shore':'岸边渡口','valley':'谷地院落','open_ground':'平地聚落','cave_entrance':'洞口','water_landmark':'水岸地标','landmark':'野外地标'}
pages = []
for start in range(0,123,18):
    page = Image.new('RGB',(1800,3*262+44),'#eee5d0')
    draw = ImageDraw.Draw(page)
    draw.text((15,10),f'百川地点逐项检查 · {start+1}—{min(start+18,123)} / 123（缩略索引；原像素请看游戏或验收页）',font=font,fill='#514a3d')
    for offset,p in enumerate(layout['placements'][start:start+18]):
        x = (offset%6)*300
        y = (offset//6)*262+44
        rx,ry,rw,rh = p['rect']
        cx,cy = rx+rw/2,ry+rh*.7
        left = max(0,min(6144-600,round(cx-300)))
        top = max(0,min(6144-430,round(cy-215)))
        crop = world.crop((left,top,left+600,top+430)).resize((300,215),Image.Resampling.LANCZOS)
        page.paste(crop,(x,y+42))
        draw.text((x+8,y+2),f'{start+offset+1:03d} {p["name"]}',font=font,fill='#413d32')
        draw.text((x+8,y+23),footings[p['footing']],font=small,fill='#746853')
    name=f'地点检查_{len(pages)+1:02d}.jpg'
    page.save(DOC/name,quality=93,subsampling=0)
    pages.append(name)

preview=world.resize((1536,1536),Image.Resampling.LANCZOS)
preview.save(DOC/'百川全图缩略.jpg',quality=94,subsampling=0)
source_unchanged=hashlib.sha256(SOURCE.read_bytes()).hexdigest()==layout['sourceHash']
assert source_unchanged
assert not static['iconOverlaps'] and not static['labelConflicts'] and not static['civilRiverIntrusions']
assert not static['roadsInsideLakes']
assert not static['dryWildRiverIntrusions']
assert max(b['pixels'] for b in static['bridgeCenterGaps'])<1
assert native['failureCount']==0
report={'worldPixels':[6144,6144],'tiles':tiles,'nativeAssets':assets,'noAssetUpsampling':True,'sourceUnchanged':source_unchanged,'sourceSha256':layout['sourceHash'],'allNamedNodes':len(layout['placements']),'contactSheetPages':pages,'nativeChecks':native['checks'],'nativeFailures':native['failureCount'],'seamRule':'Every tile is an exact pixel region of the single rendered master; no independently generated border or blend.'}
(DOC/'像素与接缝验收.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

options=''.join(f'<option value="{p["id"]}">{html.escape(p["name"])}</option>' for p in layout['placements'])
cards=''.join(f'<a href="{name}" target="_blank"><img src="{name}" loading="lazy" alt="第{i+1}组地点检查"><span>第{i+1}组 · {i*18+1}—{min((i+1)*18,123)}</span></a>' for i,name in enumerate(pages))
shots=['玉虚仙宗','玄都天府','无相圣宗','青萝城','春水府','雁回镇','澜浦渡','问津渡','藏真洞天','镇岳观','丹鼎宗']
screens=''.join(f'<a href="实机_{name}.png" target="_blank">{name}</a>' for name in shots if (DOC/f'实机_{name}.png').exists())
rows=json.dumps([{'id':p['id'],'name':p['name'],'rect':p['rect'],'footing':footings[p['footing']]} for p in layout['placements']],ensure_ascii=False)
review='''<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>百川 · 九图与地点验收</title><link rel="icon" href="data:,">
<style>
*{box-sizing:border-box}body{margin:0;background:#e7dfcd;color:#433e32;font:16px/1.6 "Microsoft YaHei",sans-serif}main{max-width:1500px;margin:auto;padding:28px}h1{font-family:serif;font-weight:500;margin:0}p{margin:10px 0 18px}a{color:#596e60}button,select{font:inherit;border:1px solid #a99d84;background:#f3ecdc;padding:7px 12px;color:inherit;border-radius:4px}.toolbar{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:14px 0}.map{height:72vh;min-height:420px;overflow:auto;border:1px solid #a99d84;background:#cdbb98;cursor:grab;position:relative;overscroll-behavior:contain;touch-action:pan-x pan-y}.map.dragging{cursor:grabbing;user-select:none}.canvas{position:relative;width:6144px;height:6144px}.canvas img{display:block;width:6144px;height:6144px;max-width:none;pointer-events:none}.target{position:absolute;border:2px solid #95704b;box-shadow:0 0 0 4px #f7e8b366;pointer-events:none}.muted{font-size:14px;color:#706b5c}.strip{display:flex;gap:12px;flex-wrap:wrap;margin:15px 0}.strip a{padding:4px 11px;border:1px solid #b2a58e;border-radius:3px}h2{font:24px serif;margin:30px 0 12px}.sheets{display:grid;grid-template-columns:repeat(auto-fit,minmax(420px,1fr));gap:16px}.sheets a{display:block;color:inherit;text-decoration:none}.sheets img{display:block;width:100%;height:auto}.sheets span{display:block;padding:6px;background:#f0e8d7}summary{cursor:pointer}.overview{width:min(100%,800px);height:auto}
</style><main>
<h1>百川舆图 · 整套验收</h1>
<p>游戏首页 → 试演与预览 → <strong>百川九图 · 地图测试（固定倍率）</strong>。可拖动、用方向键浏览，也可从地点索引跳转；滚轮与捏合不会放大地图。</p>
<p>九张2048底图相接，123个具名地点。山门、平地聚落、岸边渡口分别落位；图标均按实际显示尺寸准备。以下地图保留原像素，可拖动查看。</p>
<div class="toolbar"><label>查看地点 <select id="jump">OPTIONS</select></label><button id="clear">隐藏检查框</button><span id="site" class="muted"></span></div>
<div class="map" id="map"><div class="canvas"><img src="完整地图_含节点与道路_6144.png" alt="6144原像素百川全图" draggable="false"><div class="target" id="target" hidden></div></div></div>
<p class="muted">此页为实机地图的静态导出，固定1∶1浏览；游戏内可分别开关道路、空节点与地点。缩略索引用于查找位置。</p>
<details><summary>全图缩略索引</summary><a href="完整地图_含节点与道路_6144.png" target="_blank"><img class="overview" src="百川全图缩略.jpg" alt="全图缩略索引"></a></details>
<h2>游戏内截图</h2><div class="strip">SCREENS</div>
<h2>全部地点检查</h2><div class="sheets">CARDS</div>
<p><a href="README.md">素材与验收记录</a> · <a href="像素与接缝验收.json">像素接缝记录</a> · <a href="节点落位清单.json">逐地点清单</a></p>
</main><script>
const places=ROWS, map=document.getElementById('map'),target=document.getElementById('target'),jump=document.getElementById('jump');
function focus(id){const p=places.find(p=>p.id===id),r=p.rect;map.scrollLeft=r[0]+r[2]/2-map.clientWidth/2;map.scrollTop=r[1]+r[3]/2-map.clientHeight/2;Object.assign(target.style,{left:(r[0]-8)+'px',top:(r[1]-8)+'px',width:(r[2]+16)+'px',height:(r[3]+16)+'px'});target.hidden=false;document.getElementById('site').textContent=p.name+' · '+p.footing;}
jump.onchange=()=>focus(jump.value);document.getElementById('clear').onclick=()=>target.hidden=true;
let drag=null;map.onpointerdown=e=>{if(e.button!==0)return;drag=[e.clientX,e.clientY,map.scrollLeft,map.scrollTop];map.setPointerCapture(e.pointerId);map.classList.add('dragging')};map.onpointermove=e=>{if(!drag)return;map.scrollLeft=drag[2]+drag[0]-e.clientX;map.scrollTop=drag[3]+drag[1]-e.clientY};map.onpointerup=map.onpointercancel=()=>{drag=null;map.classList.remove('dragging')};map.addEventListener('wheel',e=>{if(e.ctrlKey)e.preventDefault()},{passive:false});
requestAnimationFrame(()=>focus(places[0].id));
</script></html>'''
review=review.replace('OPTIONS',options).replace('SCREENS',screens).replace('CARDS',cards).replace('ROWS',rows)
(DOC/'百川整套验收.html').write_text(review,encoding='utf-8')
print(json.dumps({'tiles':len(tiles),'maximumTileError':max(t['maximumPixelError'] for t in tiles),'nativeRasterAssets':len(assets),'contactSheets':len(pages),'nodes':len(layout['placements']),'sourceUnchanged':source_unchanged},ensure_ascii=False))
