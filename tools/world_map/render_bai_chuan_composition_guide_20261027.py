"""Draw a clean geography diagram to guide concept generation, not game art."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
d=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
size=1600;image=Image.new('RGB',(size,size),'#f5eedf');draw=ImageDraw.Draw(image)
def pts(items):return [(round(x*16),round(y*16)) for x,y in items]
for region in d['terrainPlanning']['areas']:draw.polygon(pts(region['polygon']),fill=region['color'])
for lake in d['terrainPlanning']['lakes']:draw.polygon(pts(lake['polygon']),fill='#aabfc1')
for water in d['terrainPlanning']['waters']:draw.line(pts(water['points']),fill='#9fb9c1',width=water['width']*3,joint='curve')
for ridge in d['terrainPlanning']['ridges']:draw.line(pts(ridge['points']),fill='#b9b09d',width=7,joint='curve')
p=ROOT/'docs/prototypes/百川整体底图构图引导_20261027.png';image.save(p)
print(str(p))
