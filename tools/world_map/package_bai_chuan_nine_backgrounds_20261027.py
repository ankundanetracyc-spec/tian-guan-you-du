"""Package original raster tiles without resampling originals or hiding seams."""
import hashlib
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/美术规范/百川九张底图_20261027'
manifest_path = OUT / '制作清单.json'
m = json.loads(manifest_path.read_text(encoding='utf-8'))
jobs = sorted(m['jobs'], key=lambda j: j['key'])
assert len(jobs) == 9 and all(j['status'] == 'generated-v01' for j in jobs)
sizes = {tuple(j['nativeSize']) for j in jobs}
assert len(sizes) == 1
w, h = next(iter(sizes))
joined = Image.new('RGB', (w * 3, h * 3))
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 23)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
sheet = Image.new('RGB', (1800, 1950), '#ede6d8')
draw = ImageDraw.Draw(sheet)
cards = []
for j in jobs:
    p = Path(j['destination'])
    raw = p.read_bytes()
    j['sha256'] = hashlib.sha256(raw).hexdigest()
    with Image.open(p) as im:
        im.verify()
    with Image.open(p) as im:
        im = im.convert('RGB')
        col, row = int(j['key'][1]) - 1, int(j['key'][0]) - 1
        joined.paste(im, (col * w, row * h))
        preview = im.resize((584, 584), Image.Resampling.LANCZOS)
        x, y = col * 600 + 8, row * 650 + 8
        sheet.paste(preview, (x, y))
        draw.text((x + 5, y + 590), j['key'] + ' · ' + j['title'], font=font, fill='#514d43')
        draw.text((x + 5, y + 620), f'{w} × {h} 原始底图', font=small, fill='#7b7366')
    name = p.name
    cards.append(f'<a class="card" href="{html.escape(name)}"><img src="{html.escape(name)}" loading="lazy" alt="百川 {j["title"]}"><span>{j["key"]} · {j["title"]}</span></a>')
assert len({j['sha256'] for j in jobs}) == 9, 'Repeated images are not nine unique tiles'
joined.save(OUT / '百川九图原尺寸拼接_v01.png')
sheet.save(OUT / '百川九张底图总览_v01.jpg', quality=94)
m['nativeWorldSize'] = [w * 3, h * 3]
m['nativeTileSize'] = [w, h]
m['deliveryStage'] = 'Nine independently generated original terrain backgrounds; v01 composition review, not runtime final'
manifest_path.write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding='utf-8')
page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>百川 · 九张底图</title><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="icon" href="data:,"><style>*{box-sizing:border-box}body{margin:0;background:#eee8dc;color:#514b40;font-family:"Microsoft YaHei",sans-serif}main{max-width:1440px;margin:auto;padding:28px}h1{font-family:KaiTi,serif;font-size:36px;letter-spacing:8px;font-weight:normal;margin:0 0 12px}p{line-height:1.8;color:#827768}a{color:inherit}.overview{display:block;width:100%;height:auto;border:1px solid #c9bfaa}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:18px;margin-top:24px}.card{display:block;text-decoration:none;background:#e5ddce}.card img{display:block;width:100%;height:auto}.card span{display:block;padding:12px}footer{margin:26px 0;font-size:13px;color:#8c806e}@media(max-width:700px){main{padding:16px}.grid{gap:8px}.card span{font-size:12px;padding:7px}}</style><main><h1>百川 · 九张底图</h1><p>西岭发源，汇入中部河湖，向东北展开河港；西南山前与南部林川相接，东南渐入旧盐泽。<br>以下先看九张直接拼合的地貌，不叠加城镇、宗门和道路。点击图片可打开原尺寸。</p><a href="百川九图原尺寸拼接_v01.png"><img class="overview" src="百川九图原尺寸拼接_v01.png" alt="百川九张底图直接拼接"></a><div class="grid">__CARDS__</div><footer>初版构图检查 · 九张均为独立生成的原图，每张 __SIZE__，原尺寸合图 __WORLD__。当前为底图初版，接缝、河岸与既定桥位尚需精修；未替换正式游戏素材。<a href="README.md">制作说明</a></footer></main></html>'''
(OUT / '百川九张底图展示.html').write_text(page.replace('__CARDS__', ''.join(cards)).replace('__SIZE__', f'{w}×{h}').replace('__WORLD__', f'{w*3}×{h*3}'), encoding='utf-8')
print(json.dumps({'count': len(jobs), 'unique': len({j['sha256'] for j in jobs}), 'tile': [w,h], 'world': [w*3,h*3]}, ensure_ascii=False))
