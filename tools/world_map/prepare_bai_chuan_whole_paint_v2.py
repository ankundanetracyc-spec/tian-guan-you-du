"""Make coordinate guides, not production landscape art, for a whole-map repaint."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/美术规范/百川整幅山水底图重绘_20261028'
ART=ROOT/'textures/world/bai_chuan/whole_paint_v2'
DOC.mkdir(parents=True,exist_ok=True)
(ART/'source').mkdir(parents=True,exist_ok=True)
(ART/'tiles').mkdir(parents=True,exist_ok=True)
data=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
size=1536
im=Image.new('RGB',(size,size),'#e8dfce')
draw=ImageDraw.Draw(im)
font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),18)
def point(p): return (round(p[0]*size/100),round(p[1]*size/100))

for ridge in data['terrainPlanning']['ridges']:
    ps=[point(p) for p in ridge['points']]
    draw.line(ps,fill='#b2a58e',width=48,joint='curve')
for water in data['terrainPlanning']['waters']:
    ps=[point(p) for p in water['points']]
    draw.line(ps,fill='#aebdc1',width=12 if water['width']==4 else 8,joint='curve')
for lake in data['terrainPlanning']['lakes']:
    draw.polygon([point(p) for p in lake['polygon']],fill='#aebdc1')
bay=[[99,5],[97.5,10],[98,14],[95.5,16],[95,18.7],[96,21],[97,24],[99,25],[100,27],[100,5]]
draw.polygon([point(p) for p in bay],fill='#aebdc1')

for node in data['nodes']:
    if node['type']=='wild': continue
    x,y=point(node['pos'])
    width=46 if node.get('level')==1 else 38 if node['type']=='city' else 30
    draw.rounded_rectangle((x-width/2,y-24,x+width/2,y+7),radius=4,fill='#f5eedf',outline='#b0a791',width=1)
    if node.get('level')==1:
        draw.text((x+25,y-15),node['name'],font=font,fill='#7c6452')
for marker in data['terrainPlanning']['crossingLandmarks']:
    if marker['sourceType']!='water': continue
    x,y=point(marker['pos'])
    draw.ellipse((x-3,y-3,x+3,y+3),fill='#a46e60')

im.save(DOC/'整图构图引导.png')
print(json.dumps({'guide':str(DOC/'整图构图引导.png'),'nodes':len(data['nodes']),'majorSects':[(n['name'],n['pos']) for n in data['nodes'] if n.get('level')==1]},ensure_ascii=False))
