"""Embed only this UI's public images/font for an offline browser design preview."""
import base64
import json
import re
from io import BytesIO
from pathlib import Path
from fontTools import subset

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / 'docs/美术规范/行囊索引与空间联动_20261029'
html = (DOC / '行囊索引与空间联动_交互样板.html').read_text(encoding='utf-8')
# Keep source CSS/behavior readable; embed them only in the portable preview.
for asset in ['行囊清册专用皮肤.css']:
    html = html.replace(f'<link rel="stylesheet" href="{asset}">', '<style>\n' + (DOC / asset).read_text(encoding='utf-8') + '\n</style>')
for asset in ['行囊索引交互.js', '行囊空间拖拽.js']:
    html = html.replace(f'<script src="{asset}"></script>', '<script>\n' + (DOC / asset).read_text(encoding='utf-8') + '\n</script>')


def data_url(path, mime):
    return f'data:{mime};base64,' + base64.b64encode(path.read_bytes()).decode('ascii')


font_relative = 'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'
options = subset.Options()
font = subset.load_font(str(ROOT / font_relative), options)
subsetter = subset.Subsetter(options=options)
subsetter.populate(text=html + '0123456789×第件种定位全部行囊')
subsetter.subset(font)
font.flavor = 'woff2'
buffer = BytesIO()
font.save(buffer)
html = html.replace('../../../' + font_relative, 'data:font/woff2;base64,' + base64.b64encode(buffer.getvalue()).decode('ascii'))

for relative, mime in [
    ('textures/ui/inventory_20261012/approved_shell_v1.png', 'image/png'),
    ('textures/ui/inventory_index_20261029/index_panel_paper.png', 'image/png'),
    ('textures/ui/inventory_index_20261029/index_row_normal.png', 'image/png'),
    ('textures/ui/inventory_index_20261029/index_row_selected.png', 'image/png'),
    ('textures/ui/forging_20261021/jade_button_v3.png', 'image/png'),
    ('textures/ui/gongfa_back_button_v1.png', 'image/png'),
]:
    html = html.replace('../../../' + relative, data_url(ROOT / relative, mime))

filenames = sorted(set(re.findall(r'"file":\s*"([^"]+\.png)"', html)))
icons = {name: data_url(ROOT / 'textures/items/inventory_density_20261029' / name, 'image/png') for name in filenames}
html = html.replace("const ICON_ROOT='../../../textures/items/inventory_density_20261029/';", 'const INLINE_ICONS=' + json.dumps(icons) + ';')
html = html.replace('image.src=ICON_ROOT+definition(item).file', 'image.src=INLINE_ICONS[definition(item).file]')
(DOC / '行囊索引与空间联动_独立预览.html').write_text(html, encoding='utf-8')
out = ROOT / 'output/playwright'
out.mkdir(parents=True, exist_ok=True)
runner = 'async (page) => { await page.goto("about:blank"); await page.setContent(' + json.dumps(html, ensure_ascii=True) + ', {waitUntil:"load"}); await page.evaluate(() => document.fonts.ready.then(()=>true)); return {title:await page.title(),rows:await page.locator(".index-row").count(),items:await page.locator(".bag-item").count()}; }'
(out / 'load_inventory_index_preview.js').write_text(runner, encoding='utf-8')
print(f'Offline preview assembled: {len(filenames)} item sprites, font subset {len(buffer.getvalue())} bytes, purpose-made paper panels and rows; no listener or game-save access.')
