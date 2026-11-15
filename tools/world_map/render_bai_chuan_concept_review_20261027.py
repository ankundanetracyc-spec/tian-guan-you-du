"""Overlay existing map data on AI concept art in HTML; leave source image intact."""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
source=ROOT/'docs/prototypes/百川宗门城镇资源交互规划_20261027.html'
html=source.read_text(encoding='utf-8')
html=html.replace('百川宗门、城镇与资源 · 交互规划稿','百川 · 构图与节点叠加检查')
html=html.replace('<div class="bar">','<div class="bar"><label><input id="art" type="checkbox" checked> 水墨草稿底图</label>',1)
html=html.replace('function drawTerrain(){',"function drawTerrain(){if(document.getElementById('art').checked){svg.append(el('image',{href:'../美术规范/百川整体构图试稿_20261027/整体构图_v01.png',x:0,y:0,width:1000,height:1000,preserveAspectRatio:'none','pointer-events':'none'}));return;}")
html=html.replace('先规划后素材。','草稿用于检查画风与位置，尚非可直接拼接的正式素材。')
html=html.replace('<main><svg','<p style="padding:0 30px">节点与道路位置保持规划数据；可关掉路线查看底图，或关掉水墨草稿返回地貌色块。山体与桥岸冲突应调整底图。</p><main><svg',1)
destination=ROOT/'docs/prototypes/百川构图与节点叠加检查_20261027.html'
destination.write_text(html,encoding='utf-8')
print(str(destination))
