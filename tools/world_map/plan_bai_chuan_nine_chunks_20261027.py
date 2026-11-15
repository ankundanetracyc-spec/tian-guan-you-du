"""Create a 3x3 background plan and original guide diagrams from map data."""
import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[2]
DATA_PATH=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
d=json.loads(DATA_PATH.read_text(encoding='utf-8'))
OUT=ROOT/'docs/美术规范/百川九宫格背景规划_20261027';OUT.mkdir(parents=True,exist_ok=True)
SIZE=6144;CORE=2048;BLEED=128
titles=['镇岳山前','玄都台原','沧溟河港','镇岳西岭','归藏涵虚','古寺东陵','玉虚山前','百药溪云','无相南境']
chunks=[]
def tile_for(pos):return min(2,int(pos[1]*3/100))*3+min(2,int(pos[0]*3/100))
for index,title in enumerate(titles):
 row,col=divmod(index,3);x,y=col*CORE,row*CORE
 content=[n for n in d['nodes'] if tile_for(n['pos'])==index]
 chunks.append({'id':f'bc_bg_{row+1}{col+1}','row':row,'col':col,'title':title,'coreRect':[x,y,CORE,CORE],'workingRect':[x-BLEED,y-BLEED,CORE+2*BLEED,CORE+2*BLEED],'workingSize':[2304,2304],'guide':f'guide_{row+1}{col+1}.png','nodeCount':len(content),'majorNodes':[n['name'] for n in content if n['type'] in ('sect','city')],'nodes':[n['id'] for n in content],'status':'构图引导已生成，正式栅格素材未生成'})
d['backgroundPlan']={'grid':[3,3],'worldPixels':[SIZE,SIZE],'corePixels':[CORE,CORE],'generationBleed':BLEED,'generationWorkingPixels':[2304,2304],'baselineViewport':[1920,1080],'cameraZoom':1,'chunks':chunks,'status':'本轮显示尺度工作方案，不改变逻辑旅行距离或运行时','note':'原1254整体图仅为画风草稿，不放大冒充6144正式地图，不直接切九块作为正式资产。'}
DATA_PATH.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(OUT/'九宫格分块清单.json').write_text(json.dumps(d['backgroundPlan'],ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
font=lambda size:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',size)
def draw_guide(bounds,size,labels=True,show_routes=True):
 im=Image.new('RGB',size,'#f5eedf');dr=ImageDraw.Draw(im);x0,y0,w,h=bounds
 def xy(pos):return ((pos[0]*SIZE/100-x0)*size[0]/w,(pos[1]*SIZE/100-y0)*size[1]/h)
 def pts(items):return [xy(p) for p in items]
 for area in d['terrainPlanning']['areas']:dr.polygon(pts(area['polygon']),fill=area['color'])
 for ridge in d['terrainPlanning']['ridges']:dr.line(pts(ridge['points']),fill='#bdb49e',width=3)
 for lake in d['terrainPlanning']['lakes']:dr.polygon(pts(lake['polygon']),fill='#abc6ca')
 for water in d['terrainPlanning']['waters']:dr.line(pts(water['points']),fill='#91b2bf',width=3)
 if show_routes:
  for route in d['routes']:dr.line(pts(route.get('renderPoints',route['points'])),fill='#ac9b7c' if route.get('usage')=='主要通路' else '#cdc3b1',width=2 if route.get('usage')=='主要通路' else 1)
 for n in (d['nodes'] if show_routes else []):
  x,y=xy(n['pos'])
  if not (-20<x<size[0]+20 and -20<y<size[1]+20):continue
  if n['type']=='sect':
   r=8 if n['level']==1 else 5;dr.polygon([(x,y-r),(x+r,y),(x,y+r),(x-r,y)],fill={'玉虚':'#527a66','玄都':'#9a7554','无相':'#816783'}[n['camp']])
  elif n['type']=='city':dr.rectangle((x-4,y-4,x+4,y+4),fill='#9d7431')
  else:dr.ellipse((x-2,y-2,x+2,y+2),fill='#839688')
  if labels and (n['type']=='city' or n['type']=='sect'):
   dr.text((x+9,y-9),n['name'],fill='#485849',font=font(15),stroke_width=2,stroke_fill='#f5eedf')
 return im
overview=draw_guide([0,0,SIZE,SIZE],(1500,1500));dr=ImageDraw.Draw(overview)
for chunk in chunks:
 x,y,w,h=chunk['coreRect'];x,y,w,h=[v*1500/SIZE for v in (x,y,w,h)]
 dr.rectangle((x,y,x+w-1,y+h-1),outline='#8a7761',width=3)
 label=chunk['id']+'  '+chunk['title']+'  '+str(chunk['nodeCount'])+'处'
 dr.rectangle((x+6,y+6,x+350,y+41),fill='#f5eedf')
 dr.text((x+12,y+10),label,fill='#5b5447',font=font(21))
 guide=draw_guide(chunk['workingRect'],(900,900));gd=ImageDraw.Draw(guide)
 margin=BLEED*900/2304;gd.rectangle((margin,margin,900-margin,900-margin),outline='#b77750',width=3)
 gd.rectangle((6,6,894,40),fill='#f5eedf');gd.text((14,10),chunk['id']+' · '+chunk['title']+' · 外圈为接缝参考区',fill='#5b5447',font=font(19))
 guide.save(OUT/chunk['guide'])
overview.save(OUT/'九宫格分块与节点总览.png')
draw_guide(chunks[4]['workingRect'],(1152,1152),labels=False,show_routes=False).save(OUT/'中央块纯地貌引导.png')
assert sum(c['nodeCount'] for c in chunks)==123
assert len({i for c in chunks for i in c['nodes']})==123
print(json.dumps({'world':SIZE,'core':CORE,'chunks':len(chunks),'nodeCounts':[c['nodeCount'] for c in chunks],'viewportCoverage':round(1920*1080/SIZE**2*100,2)},ensure_ascii=True))
