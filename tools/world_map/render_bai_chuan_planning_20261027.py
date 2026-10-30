"""Render the latest 3+10 relations plan as static and interactive diagrams."""
import json, math, html
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs' / 'prototypes'
DATA = json.loads((OUT / '百川宗门城镇资源规划数据_20261027.json').read_text(encoding='utf-8'))
# Presentation-only overlay: resource is a filter within wild places, not another place class.
for node in DATA['nodes']:
    if node.get('resourceSite'):
        node['type'] = 'resource'
CAMP = {'玉虚': '#527a66', '玄都': '#9a7554', '无相': '#816783'}

def font(n):
    return ImageFont.truetype(r'C:\Windows\Fonts\msyh.ttc', n)

def color(n):
    if n['type'] == 'sect':
        return CAMP[n['camp']]
    return {'city': '#a07835', 'resource': '#528d8a', 'town': '#a89b7c',
            'market': '#9c8d6b', 'village': '#aaa392', 'dock': '#71949e',
            'council': '#6f6b5e', 'memory': '#b0afa7'}.get(n['type'], '#a5aaa0')

def render_png(resource_mode=False, terrain_mode=False, crossing_mode=False):
    im = Image.new('RGB', (1900, 1500), '#f5f0e6')
    d = ImageDraw.Draw(im)
    title = '百川 · 二十一处资源地与城市联系' if resource_mode else '百川 · 三大宗门、十地方宗门与十六城'
    if terrain_mode:
        title = '百川 · 地貌与主通路规划'
    if crossing_mode:
        title = '百川 · 桥位、山口与行旅结构'
    d.text((50, 30), title, fill='#293b35', font=font(39))
    d.text((52, 91), '123处世界地点＋独立仙盟 · 知名产地有归属 · 站队不是管理 · 具体方案待审阅', fill='#667368', font=font(22))
    box = (50, 155, 1260, 1365)
    d.rounded_rectangle(box, radius=18, fill='#fffaf0', outline='#c9c4b5', width=2)
    def xy(n):
        return (box[0] + n['pos'][0] * 12.1, box[1] + n['pos'][1] * 12.1)
    if terrain_mode:
        terrain = DATA.get('terrainPlanning', {})
        def points(items):
            return [xy({'pos': p}) for p in items]
        for area in terrain.get('areas', []):
            d.polygon(points(area['polygon']), fill=area['color'])
        for ridge in terrain.get('ridges', []):
            d.line(points(ridge['points']), fill='#c3bca9', width=5, joint='curve')
        for lake in terrain.get('lakes', []):
            d.polygon(points(lake['polygon']), fill='#b9d1d1')
        for water in terrain.get('waters', []):
            d.line(points(water['points']), fill='#a6c3ca', width=water['width']+2, joint='curve')
    by_id = {n['id']: n for n in DATA['nodes'] + DATA.get('transitNodes', [])}
    if not resource_mode:
        for route in DATA.get('routes', []):
            trunk = terrain_mode and route.get('usage') == '主要通路'
            positions = route.get('renderPoints', route.get('points', [by_id[route['from']]['pos'], by_id[route['to']]['pos']]))
            d.line([xy({'pos': pos}) for pos in positions], fill='#a68a60' if trunk else '#cfc5b4', width=3 if trunk else 2)
        for node in DATA.get('transitNodes', []):
            x, y = xy(node)
            d.ellipse((x-2, y-2, x+2, y+2), outline='#9a9383', width=1)
    if crossing_mode:
        for feature in DATA.get('terrainPlanning', {}).get('crossingLandmarks', []):
            x, y = xy(feature)
            if feature['sourceType'] == 'water':
                d.rectangle((x-5,y-5,x+5,y+5),fill='#fffaf0',outline='#8e6944',width=2)
            else:
                d.polygon([(x,y-6),(x+6,y+5),(x-6,y+5)],fill='#fffaf0',outline='#82745e',width=2)
    all_markers = []
    for n in DATA['nodes']:
        x, y = xy(n)
        r = 15 if n['type'] == 'sect' and n['level'] == 1 else 10 if n['type'] in ('sect', 'city') else 5 if n['type'] == 'resource' else 3
        c = color(n)
        if n['type'] == 'sect':
            d.polygon([(x, y-r), (x+r, y), (x, y+r), (x-r, y)], fill=c)
        elif n['type'] == 'city':
            d.rounded_rectangle((x-r, y-r, x+r, y+r), radius=3, fill=c)
        else:
            d.ellipse((x-r, y-r, x+r, y+r), fill=c)
        if n['type'] in ('sect', 'city', 'resource'):
            all_markers.append((x-r-2, y-r-2, x+r+2, y+r+2))
    labels = [n for n in DATA['nodes'] if n['type'] in (('resource', 'city') if resource_mode else ('sect', 'city'))]
    labels.sort(key=lambda n: (0 if n['type']=='sect' and n['level']==1 else 1 if n['type']=='sect' else 2))
    occupied = []
    def intersects(a, b):
        return a[0] < b[2] and a[2] > b[0] and a[1] < b[3] and a[3] > b[1]
    for n in labels:
        x, y = xy(n)
        ft = font(25 if n['type']=='sect' and n['level']==1 else 21)
        bb = d.textbbox((0, 0), n['name'], font=ft)
        w, h = bb[2]-bb[0], bb[3]-bb[1]+8
        options = [(x-w/2, y+20), (x-w/2, y-h-18), (x+20, y-h/2), (x-w-20, y-h/2),
                   (x+22, y+22), (x-w-22, y+22), (x-w/2, y+45), (x-w/2, y-h-45)]
        for tx, ty in options:
            rect = (tx-5, ty-3, tx+w+5, ty+h+4)
            if rect[0] > box[0]+4 and rect[2] < box[2]-4 and rect[1] > box[1]+4 and rect[3] < box[3]-4 and not any(intersects(rect, a) for a in occupied + all_markers):
                break
        else:
            tx, ty = options[0]
            rect = (tx-5, ty-3, tx+w+5, ty+h+4)
        occupied.append(rect)
        d.rounded_rectangle(rect, radius=5, fill='#fffaf0')
        d.text((tx, ty-bb[1]+3), n['name'], fill=color(n), font=ft)
    x = 1310
    d.text((x, 175), '宗门关系', fill='#384f43', font=font(29))
    yy = 225
    for camp in ['玉虚', '玄都', '无相']:
        d.text((x, yy), camp+'：仅表示站队倾向', fill=CAMP[camp], font=font(23)); yy += 40
        names = [s['name'] for s in DATA['sects'] if s['level']==2 and s['camp']==camp]
        for i in range(0, len(names), 2):
            d.text((x, yy), '、'.join(names[i:i+2]), fill='#566257', font=font(22)); yy += 34
        yy += 28
    yy += 25
    legend = ['菱形：宗门山门', '方块：城市', '绿点：野外中的知名产地', '小灰点：空过渡节点']
    if terrain_mode:
        legend = ['深线：主要通路', '浅线：支路与野外深入', '青线：河流构图示意', '灰褐线：山脊构图示意']
    for text in legend:
        d.text((x, yy), text, fill='#606b60', font=font(23)); yy += 44
    yy += 25
    for text in ['十六城：7轮值、7护持、2自治', '轮值工作方案：每届10年', '仙盟：独立节点，暂不相接', '产地归属可依纷争结局改变']:
        d.text((x, yy), text, fill='#797264', font=font(21)); yy += 40
    d.text((52, 1405), '比例坐标只表示空间意向；不是最终格坐标、真实公里或旅行耗时。详细关系可在交互图中点击查看。', fill='#697367', font=font(21))
    name = '百川资源节点相对布局_20261027.png' if resource_mode else '百川势力与节点分布规划_20261027.png'
    if terrain_mode:
        name = '百川地貌与主通路规划_20261027.png'
        d.text((1310, 1285), '色块是地貌，不是势力边界', fill='#657c72', font=font(21))
        d.text((1310, 1320), '桥渡待校准，不改变通行规则', fill='#657c72', font=font(21))
    if crossing_mode:
        name = '百川桥渡与行旅结构规划_20261027.png'
        d.text((1310, 1245), '方框：15桥位　空三角：13山口', fill='#816e53', font=font(21))
        d.rectangle((1300,1315,1880,1360),fill='#f5f0e6')
        d.text((1310, 1320), '沿现有空节点布置，无新地点', fill='#657c72', font=font(21))
    im.save(OUT/name)

HTML = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<title>百川宗门、城镇与资源规划</title><link rel="icon" href="data:,">
<style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;background:#f2eee4;color:#31443b;font:15px/1.65 "Microsoft YaHei",sans-serif}header{padding:24px 30px 14px}h1{font-size:26px;margin:0}p{margin:6px 0;color:#6c766c}.bar{display:flex;gap:18px;flex-wrap:wrap;padding:8px 30px 18px}.bar label{cursor:pointer}main{display:grid;grid-template-columns:minmax(600px,1fr) 360px;gap:18px;padding:0 28px 28px}svg{width:100%;background:#fffbf1;border:1px solid #cbc5b8;border-radius:12px}aside{background:#fffbf1;border:1px solid #d7d0c1;border-radius:12px;padding:20px;align-self:start;position:sticky;top:14px}h2{margin:0 0 12px;font-size:23px}.muted{color:#7b8277;font-size:13px}.field{margin-bottom:14px}.field strong{display:block;color:#8a774f;font-size:13px}.node{cursor:pointer;pointer-events:bounding-box}.node text{pointer-events:auto}.node:hover{filter:brightness(.8)}svg text{font-family:"Microsoft YaHei",sans-serif;pointer-events:none;paint-order:stroke;stroke:#fffbf1;stroke-width:3px;stroke-linejoin:round}.tags{padding:5px 9px;background:#e8e7d9;border-radius:6px;display:inline-block}.legend{margin:15px 0;font-size:13px;color:#737b70}@media(max-width:950px){main{grid-template-columns:1fr}aside{position:static}}
</style><header><h1>百川宗门、城镇与资源 · 交互规划稿</h1><p>三大宗门＋十独立地方宗门，十六城、十四镇、二十一资源地；候选总数107。点击地点查看关系。</p><p class="muted">站队不表示行政管理；图中位置是比例意向，不是最终格坐标或旅行耗时。先规划后素材。</p></header>
<div class="bar"><input id="pick" list="node-names" placeholder="查找地点，如白石城" aria-label="查找地点"><datalist id="node-names"></datalist><label><input type="checkbox" data-group="sect" checked> 宗门</label><label><input type="checkbox" data-group="city" checked> 城市</label><label><input type="checkbox" data-group="settlement" checked> 镇坊村渡</label><label><input type="checkbox" data-group="resource" checked> 资源</label><label><input type="checkbox" data-group="wild"> 普通野外</label><label><input id="labels" type="checkbox"> 全部地名</label><label><input id="camps" type="checkbox"> 站队虚线</label></div>
<main><svg id="map" viewBox="0 0 1000 1000" aria-label="百川相对位置规划图"></svg><aside id="detail"><h2>点选一个地点</h2><div class="tags">结构已定 · 具体方案待整体审阅</div><div class="legend">菱形＝宗门　方块＝城市<br>绿点＝资源地　小点＝其他地点<br>颜色表示宗门站队倾向；不表示城市所有权。</div><p>选择城市可查看值守轮序；选择镇或渡口可查看联系城市；选择资源可查看材料候选与经营契。</p></aside></main>
<script id="dataset" type="application/json">__DATA__</script>
<script>
var data=JSON.parse(document.getElementById('dataset').textContent), svg=document.getElementById('map'), NS='http://www.w3.org/2000/svg';
var colors={'玉虚':'#527a66','玄都':'#9a7554','无相':'#816783'},selected=null;
var byName=Object.fromEntries(data.nodes.map(function(n){return[n.name,n]}));
var typeNames={sect:'宗门',city:'城市',town:'镇',market:'坊市',village:'村落',dock:'渡口',resource:'资源地',wild:'野外／遗迹',council:'共同议场',memory:'记忆锚点'};
function group(n){return ['town','market','village','dock'].includes(n.type)?'settlement':['council','memory'].includes(n.type)?'sect':n.type}
function col(n){return n.type==='sect'?colors[n.camp]:{city:'#a07835',resource:'#528d8a',town:'#a89b7c',market:'#9c8d6b',village:'#aaa392',dock:'#71949e',council:'#6f6b5e',memory:'#b0afa7'}[n.type]||'#9ca595'}
function el(tag,attrs){var e=document.createElementNS(NS,tag);Object.entries(attrs||{}).forEach(function(k){e.setAttribute(k[0],k[1])});return e}
function line(a,b,c,dash){if(!a||!b)return;svg.append(el('line',{x1:a.pos[0]*10,y1:a.pos[1]*10,x2:b.pos[0]*10,y2:b.pos[1]*10,stroke:c,'stroke-width':1.4,'stroke-dasharray':dash?'6 5':'','opacity':.55}))}
function render(){svg.replaceChildren();var shown=new Set(Array.from(document.querySelectorAll('[data-group]:checked')).map(function(x){return x.dataset.group}));
 if(document.getElementById('camps').checked){data.sects.filter(function(s){return s.level===2}).forEach(function(s){var major=data.sects.find(function(a){return a.level===1&&a.camp===s.camp});line(s,major,colors[s.camp],true)})}
 if(selected){if(selected.type==='city')(selected.order||[]).forEach(function(a){line(selected,byName[a],'#bb8f37',true)});else if(selected.city)line(selected,byName[selected.city],'#a28f67',true);else if(selected.type==='resource')selected.supply.split('、').forEach(function(a){line(selected,byName[a],'#528d8a',true)})}
 data.nodes.filter(function(n){return shown.has(group(n))}).forEach(function(n){var x=n.pos[0]*10,y=n.pos[1]*10,r=n.type==='sect'?(n.level===1?10:7):n.type==='city'?7:n.type==='resource'?4.5:3.3,g=el('g',{'class':'node',role:'button','aria-label':n.name+' · '+typeNames[n.type],tabindex:0}),c=col(n),shape;
 if(n.type==='sect')shape=el('polygon',{points:x+','+(y-r)+' '+(x+r)+','+y+' '+x+','+(y+r)+' '+(x-r)+','+y,fill:c});
 else if(n.type==='city')shape=el('rect',{x:x-r,y:y-r,width:r*2,height:r*2,rx:2,fill:c});else shape=el('circle',{cx:x,cy:y,r:r,fill:c});
 if(selected&&selected.name===n.name){shape.setAttribute('stroke','#263c31');shape.setAttribute('stroke-width','3')}
 var hitw=Math.max(r+5,(n.name.length*7)/2);g.append(el('rect',{x:x-hitw,y:y-r-4,width:hitw*2,height:r*2+24,fill:'transparent'}));g.append(shape);var title=el('title');title.textContent=n.name+' · '+typeNames[n.type];g.append(title);
 if(document.getElementById('labels').checked||n.type==='sect'||n.type==='city'){var t=el('text',{x:x,y:y+r+15,'text-anchor':'middle','font-size':n.type==='sect'&&n.level===1?15:12,fill:c});t.textContent=n.name;g.append(t)}
 g.addEventListener('click',function(){selected=n;details(n);render()});svg.append(g)});
}
function details(n){var p=document.getElementById('detail');p.replaceChildren();var h=document.createElement('h2');h.textContent=n.name;p.append(h);var tag=document.createElement('div');tag.className='tags';tag.textContent=typeNames[n.type]+' · 工作方案';p.append(tag);
 function row(k,v){if(!v)return;var e=document.createElement('div');e.className='field';var b=document.createElement('strong');b.textContent=k;var s=document.createElement('span');s.textContent=Array.isArray(v)?v.join('、'):v;e.append(b,s);p.append(e)}
 if(n.type==='sect'){row('层次',n.level===1?'一级大宗':'独立地方宗门');row('站队倾向',n.camp+'（不产生行政隶属）');row('地理位置',n.pos.join('／')+'：比例示意，不是格坐标');row('说明',n.note||'山门、资产与内部决定权独立。')}
 else if(n.type==='city'){row('治理',n.mode);row('值守轮序／护持方',n.order.length?n.order.join(' → '):'本地城务会');row('任期方案',n.mode==='轮值'?'每届10年；列首者为首届，三方一轮30年':n.mode==='单宗护持'?'不轮值；每10年复核护持契':'城务会推举城主，每届10年');row('城约保证方',n.guarantors);row('主要作用',n.role);row('交接边界','城务人员与人物关系留存；不转矿脉产权、不刷新资源和任务。')}
 else if(n.type==='resource'){row('来源',n.terrain);row('材料候选',n.products);row('经营／护持契',n.contract);row('开放',n.access);row('联系城市',n.supply);row('状态','未配置产量与储量；城市换届不重置资源点。')}
 else {row('联系城市／护持方',n.city);row('地理片区',n.geography);row('用途',n.note)}
 var note=document.createElement('p');note.className='muted';note.textContent='连线随点选只表示城约、资源联系或站队，不是固定行走道路。';p.append(note)
}
var list=document.getElementById('node-names');data.nodes.forEach(function(n){var o=document.createElement('option');o.value=n.name;list.append(o)});function pick(){var n=byName[document.getElementById('pick').value];if(n){selected=n;details(n);render()}}document.getElementById('pick').addEventListener('change',pick);document.getElementById('pick').addEventListener('keydown',function(e){if(e.key==='Enter')pick()});
document.querySelectorAll('input').forEach(function(x){x.addEventListener('change',render)});render();
</script></html>'''

def render_html():
    output = HTML.replace('__DATA__', json.dumps(DATA, ensure_ascii=False))
    output = output.replace("resource:'资源地'", "resource:'野外·知名产地'")
    output = output.replace(" if(n.type==='sect'){row('层次'", " row('地理联系',n.regionalInfluence?n.regionalInfluence+'近侧联系片区（非行政归属）':null);row('政策方向',n.policyNote);if(n.type==='sect'){row('层次'")
    output = output.replace('> 资源</label>', '> 资源产地（野外子集）</label>')
    output = output.replace('<input id="labels"', '<input id="routes" type="checkbox" checked> 过渡路线</label><label><input id="terrain" type="checkbox" checked> 地貌示意</label><label><input id="labels"')
    output = output.replace('<input id="labels"', '<input id="features" type="checkbox" checked> 桥位与山口</label><label><input id="labels"')
    output = output.replace("var byName=", "var byId=Object.fromEntries(data.nodes.concat(data.transitNodes||[]).map(function(n){return[n.id,n]}));var byName=")
    output = output.replace(" if(document.getElementById('camps').checked)", " if(document.getElementById('routes').checked){data.routes.forEach(function(r){var pts=r.renderPoints||r.points||[byId[r.from].pos,byId[r.to].pos];svg.append(el('polyline',{points:pts.map(function(p){return p[0]*10+','+p[1]*10}).join(' '),fill:'none',stroke:r.isResourceSpur?'#a1a18c':'#8e8778','stroke-width':r.isResourceSpur?.8:1.2,opacity:.65}))});data.transitNodes.forEach(function(n){svg.append(el('circle',{cx:n.pos[0]*10,cy:n.pos[1]*10,r:1.7,fill:'#fffbf1',stroke:'#8e8778','stroke-width':.7}))})}drawFeatures(); if(document.getElementById('camps').checked)")
    terrain_js = r'''function drawTerrain(){if(!document.getElementById('terrain').checked)return;var terrain=data.terrainPlanning||{};
function pts(items){return items.map(function(p){return (p[0]*10)+','+(p[1]*10)}).join(' ')}
(terrain.areas||[]).forEach(function(a){svg.append(el('polygon',{points:pts(a.polygon),fill:a.color,'pointer-events':'none'}))});
(terrain.ridges||[]).forEach(function(a){svg.append(el('polyline',{points:pts(a.points),fill:'none',stroke:'#c3bca9','stroke-width':4,'stroke-linejoin':'round','pointer-events':'none'}))});
(terrain.lakes||[]).forEach(function(a){svg.append(el('polygon',{points:pts(a.polygon),fill:'#b9d1d1','pointer-events':'none'}))});
(terrain.waters||[]).forEach(function(a){svg.append(el('polyline',{points:pts(a.points),fill:'none',stroke:'#a6c3ca','stroke-width':a.width,'stroke-linejoin':'round','pointer-events':'none'}))});}
'''
    feature_js = r'''function drawFeatures(){if(!document.getElementById('features').checked)return;(data.terrainPlanning.crossingLandmarks||[]).forEach(function(f){var x=f.pos[0]*10,y=f.pos[1]*10,g=el('g',{role:'button',tabindex:0,'aria-label':f.kind+' · '+f.endpoints.join('至'),'class':'node'}),shape;
g.append(el('circle',{cx:x,cy:y,r:7,fill:'transparent'}));if(f.sourceType==='water')shape=el('rect',{x:x-3,y:y-3,width:6,height:6,fill:'#fffbf1',stroke:'#8e6944','stroke-width':1.6});else shape=el('polygon',{points:x+','+(y-4)+' '+(x+4)+','+(y+3)+' '+(x-4)+','+(y+3),fill:'#fffbf1',stroke:'#82745e','stroke-width':1.4});g.append(shape);var title=el('title');title.textContent=f.kind+' · '+f.endpoints.join('—');g.append(title);
g.addEventListener('click',function(){var panel=document.getElementById('detail');panel.replaceChildren();var h=document.createElement('h2');h.textContent=f.kind;panel.append(h);[f.endpoints.join('—')+'途中',f.source,'这是空过渡节点上的地形标记，不是新地点。',f.note].forEach(function(s){var p=document.createElement('p');p.textContent=s;panel.append(p)})});svg.append(g)})}
'''
    output = output.replace('function render(){svg.replaceChildren();', terrain_js+feature_js+'function render(){svg.replaceChildren();drawTerrain();')
    output = output.replace("row('地理片区',n.geography)", "row('地理片区',n.terrainRegion||n.geography)")
    output = output.replace('绿点＝资源地　小点＝其他地点', '绿点＝野外产地　小点＝其他地点；地貌色块不表示势力边界')
    output = output.replace('三大宗门＋十独立地方宗门，十六城、十四镇、二十一资源地；候选总数107。点击地点查看关系。', f'123个具名地点，含70个野外（其中21个知名产地）；{len(DATA.get("transitNodes", []))}个空过渡节点，{len(DATA.get("routes", []))}条候选路线。仙盟独立，暂不与外界相接。')
    output = output.replace("row('来源',n.terrain)", "row('归属方案',n.owner);row('规模意向',n.scale);row('成员便利','按身份开放区域、驻地情报及宗门事务；不设未定义倍率');row('供给','宗门按实际经营所得定期补充可兑换或交易资源，周期数量待定');row('纷争','归属依明确事件结局变更，不因一次战斗自动过户');row('来源',n.terrain)")
    (OUT/'百川宗门城镇资源交互规划_20261027.html').write_text(output, encoding='utf-8')

if __name__ == '__main__':
    render_png()
    render_png(True)
    render_png(terrain_mode=True)
    render_png(terrain_mode=True, crossing_mode=True)
    render_html()
    print(f'Rendered {len(DATA["nodes"])} world locations; alliance detached.')
