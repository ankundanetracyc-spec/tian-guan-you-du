"""Compose saved layout positions for presentation; not an engine screenshot."""
from pathlib import Path
import json
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'textures/world_map/bai_chuan/natural_families_20261009'
data=json.loads((ROOT/'resources/world/bai_chuan/generated_nature_layout.json').read_text(encoding='utf-8'))
catalog=json.loads((ROOT/'resources/world/bai_chuan/natural_asset_catalog.json').read_text(encoding='utf-8'))
images={a['path']:Image.open(ROOT/a['path'][6:]).convert('RGBA') for a in catalog['assets']}
visible={path:(im.getbbox(),im.crop(im.getbbox())) for path,im in images.items()}
paper=Image.open(ROOT/catalog['ground'][6:]).convert('RGBA').crop((0,0,256,256))
def render(box,step,name):
    x0,y0,w,h=box;canvas=Image.new('RGBA',(w*step,h*step));tile=paper.resize((step,step),Image.Resampling.LANCZOS)
    for y in range(h):
        for x in range(w):canvas.alpha_composite(tile,(x*step,y*step))
    scaled={}
    for r in sorted(data['records'],key=lambda r:r['position'][1]):
        im=images[r['asset']];s=r['scale']*step/256;px,py=r['position'];ox,oy=r['offset']
        xx=(px/256-x0)*step+(ox-im.width/2)*s;yy=(py/256-y0)*step+(oy-im.height/2)*s
        bounds,cut=visible[r['asset']];xx+=bounds[0]*s;yy+=bounds[1]*s
        iw,ih=max(1,round(cut.width*s)),max(1,round(cut.height*s))
        if xx+iw<0 or yy+ih<0 or xx>canvas.width or yy>canvas.height:continue
        key=(r['asset'],iw,ih)
        if key not in scaled:scaled[key]=cut.resize((iw,ih),Image.Resampling.LANCZOS,reducing_gap=3)
        canvas.alpha_composite(scaled[key],(round(xx),round(yy)))
    lines=Image.new('RGBA',canvas.size,(0,0,0,0));draw=ImageDraw.Draw(lines)
    if step>=20:
        for x in range(w+1):draw.line((x*step,0,x*step,canvas.height),fill=(90,90,80,65))
        for y in range(h+1):draw.line((0,y*step,canvas.width,y*step),fill=(90,90,80,65))
    canvas.alpha_composite(lines)
    canvas.convert('RGB').save(OUT/name)
render((0,0,200,200),8,'template_map_overview.png')
render((8,8,76,54),24,'template_northwest_region.png')
render((16,36,32,26),50,'template_woodland_detail.png')
render((15,13,32,26),50,'template_mountain_detail.png')
print('Saved layout presentation images; original PNGs unchanged.')
