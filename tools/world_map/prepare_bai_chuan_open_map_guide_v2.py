"""Compose only a low-density cartographic coordinate guide, not landscape art."""
import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/美术规范/百川整幅山水底图重绘_20261028'
data=json.loads((ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
old=json.loads((ROOT/'textures/world/bai_chuan/acceptance_v1/presentation.json').read_text(encoding='utf-8'))
size=1536
im=Image.new('RGB',(size,size),'#ceb88b'); d=ImageDraw.Draw(im)
def point(p):return (round(p[0]*size/100),round(p[1]*size/100))
mountains=[
 [[6,19],[12,18],[16,30],[14,45],[17,54],[10,62],[6,50],[8,39],[5,30]],
 [[28,6],[43,8],[54,7],[62,4],[71,9],[75,15],[74,20],[69,23],[57,23],[51,27],[42,25],[36,28],[30,19]],
 [[13,62],[22,59],[29,67],[27,76],[32,85],[36,89],[41,95],[30,97],[23,88],[15,80]],
 [[78,29],[86,28],[91,36],[89,45],[91,52],[89,62],[81,65],[78,58],[82,49],[78,42]],
 [[30,45],[37,45],[40,48],[39,54],[34,55],[30,52]],
 [[38,61],[43,60],[48,64],[47,70],[42,71],[38,67]],
 [[57,32],[62,31],[66,34],[68,38],[64,41],[59,40],[56,36]],
 [[59,77],[64,76],[70,78],[71,85],[66,87],[61,85]],
 [[83,83],[89,81],[94,87],[93,94],[87,96],[82,91]]]
mask=Image.new('L',(size,size)); md=ImageDraw.Draw(mask)
for poly in mountains:
    ps=[point(p) for p in poly];d.polygon(ps,fill='#aea17e');md.polygon(ps,fill=255)
for water in data['terrainPlanning']['waters']:
    d.line([point(p) for p in water['points']],fill='#9fa89f',width=16 if water['width']==4 else 10,joint='curve')
for lake in data['terrainPlanning']['lakes']:d.polygon([point(p) for p in lake['polygon']],fill='#9fa89f')
bay=[[99,5],[97.5,10],[98,14],[95.5,16],[95,18.7],[96,21],[97,24],[99,25],[100,27],[100,5]]
d.polygon([point(p) for p in bay],fill='#9fa89f')
forest_count=0
for f in old['forests'][::2]:
    x,y=round(f['center'][0]/4),round(f['center'][1]/4)
    if not(30<x<size-30 and 30<y<size-30):continue
    if any(mask.getpixel((xx,yy)) for xx,yy in [(x,y),(x-20,y),(x+20,y),(x,y-14),(x,y+14)]):continue
    d.polygon([(x-12,y+4),(x-7,y-7),(x+2,y-12),(x+14,y-3),(x+11,y+9),(x-2,y+11)],fill='#80866b')
    forest_count+=1
for n in data['nodes']:
    if n['type']=='wild':continue
    x,y=point(n['pos']);r=20 if n.get('level')==1 else 14
    d.ellipse((x-r,y-r*.65,x+r,y+r*.65),fill='#d8c49c')
for m in data['terrainPlanning']['crossingLandmarks']:
    if m['sourceType']=='water':
        x,y=point(m['pos']);d.ellipse((x-3,y-3,x+3,y+3),fill='#a45f4f')
im.save(DOC/'开阔舆图疏密引导.png')
coverage=sum(v>0 for v in mask.getdata())/(size*size)
print(json.dumps({'mountainCoverage':round(coverage,3),'forestHints':forest_count,'guide':str(DOC/'开阔舆图疏密引导.png')},ensure_ascii=False))
