"""Build the portable artwork review page from the current inventory manifest."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / 'docs/美术规范/行囊全物品图标与占地_20261029'
tasks = json.loads((DOC / '生成任务.json').read_text(encoding='utf-8'))
rows = json.loads((DOC / '物品占地与图标清单.json').read_text(encoding='utf-8'))
items = []
for job in tasks:
    refs = [r for r in rows if r['new_icon'].endswith('/'+job['file'])]
    kind = '衣物' if refs[0]['script'] == 'PortraitClothingData' else '剑器' if 'sword' in job['file'] else '书册与瓷瓶' if job['file'] in ['manual_generic_2x3.png','pill_container_empty_porcelain.png'] else '材料'
    items.append(dict(file=job['file'],name=job['name'],cells=job['cells'],kind=kind,refs=[r['name'] for r in refs]))
html = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>行囊物品图标 · 整套重绘</title>
<style>
:root{--cell:48px;--paper:#eee4cc;--ink:#403d34}*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font:16px/1.6 'Microsoft YaHei',sans-serif}header{padding:24px 32px 12px;position:sticky;top:0;background:var(--paper);border-bottom:1px solid #b9ad92;z-index:1}h1{font-size:26px;margin:0}p{margin:6px 0 14px;color:#716855}.controls{display:flex;flex-wrap:wrap;gap:10px;align-items:center;padding:5px 0 12px}button,input,select{font:inherit;color:inherit;border:1px solid #b9ad92;background:#f5edda;border-radius:4px;padding:6px 12px}button{cursor:pointer}button.active{background:#6c7563;color:#fff}main{display:flex;flex-wrap:wrap;gap:20px;padding:26px 32px;align-items:stretch}.card{background:#f5ecd7;border:1px solid #c5b99e;padding:14px;min-width:210px;display:flex;flex-direction:column;align-items:center;cursor:zoom-in}.card h2{font-size:17px;align-self:flex-start;margin:0 0 3px}.meta{font-size:13px;color:#827661;align-self:flex-start}.art{margin:14px 18px;width:calc(var(--w)*var(--cell));height:calc(var(--h)*var(--cell));flex-shrink:0;position:relative;background-color:var(--artbg,#f5ecd7)}.grid .art{background-image:linear-gradient(to right,#b5a98c55 1px,transparent 1px),linear-gradient(to bottom,#b5a98c55 1px,transparent 1px);background-size:var(--cell) var(--cell);outline:1px solid #b5a98c55}.art img{width:100%;height:100%;object-fit:contain;display:block}dialog{border:1px solid #b9ad92;background:var(--paper);max-width:90vw;max-height:94vh;padding:24px}dialog::backdrop{background:#242720cc}dialog img{display:block;max-width:65vw;max-height:65vh;margin:10px auto}dialog h2{margin:0}dialog p{max-width:650px}footer{padding:12px 32px 32px;color:#827661;font-size:13px}.empty{padding:24px}
</style><header><h1>行囊物品图标 · 整套重绘</h1><p>65张行囊图覆盖96份物品定义。秘籍共用朴素书册，丹药共用瓷瓶。点击物品查看大图与共用范围。</p><div class="controls" id="filters"></div><div class="controls"><label>每格显示 <select id="size"><option value="36">36像素</option><option value="48" selected>48像素</option><option value="64">64像素</option><option value="96">96像素</option></select></label><label><input type="checkbox" id="grid" checked> 显示占格</label><input id="search" type="search" placeholder="搜索物品名称"><label>底色 <select id="bg"><option value="#eee4cc">浅纸色</option><option value="#292e2b">深灰</option></select></label><span id="count"></span></div></header><main id="list" class="grid"></main><footer>每格128源像素；等比缩小、统一留白、透明PNG。占地为可旋转实心矩形。此页为美术审阅，不改变库存、堆叠、用药或戒指规则。另有1张裸药丸仅供动画。</footer><dialog id="detail"><button id="close">关闭</button><h2 id="title"></h2><img id="large"><p id="refs"></p></dialog>
<script>
const items=__ITEMS__,root='../../../textures/items/inventory_density_20261029/';let filter='全部';const $=id=>document.getElementById(id);
function render(){const term=$('search').value.trim();const selected=items.filter(x=>(filter==='全部'||x.kind===filter)&&(!term||x.refs.some(y=>y.includes(term))||x.name.includes(term)));$('list').replaceChildren();$('count').textContent=selected.length+' / 65张';for(const item of selected){const card=document.createElement('article');card.className='card';const title=document.createElement('h2');title.textContent=item.name;const meta=document.createElement('span');meta.className='meta';meta.textContent=item.cells.join(' × ')+' · '+item.cells[0]*item.cells[1]+'格';const art=document.createElement('div');art.className='art';art.style.setProperty('--w',item.cells[0]);art.style.setProperty('--h',item.cells[1]);const img=document.createElement('img');img.src=root+item.file;img.alt=item.name;art.append(img);card.append(title,meta,art);card.onclick=()=>{$('title').textContent=item.name;$('large').src=img.src;$('refs').textContent='共用物品：'+item.refs.join('、');$('detail').showModal()};$('list').append(card)}}
for(const kind of ['全部','材料','衣物','剑器','书册与瓷瓶']){const b=document.createElement('button');b.textContent=kind;b.className=kind==='全部'?'active':'';b.onclick=()=>{filter=kind;document.querySelectorAll('#filters button').forEach(x=>x.classList.toggle('active',x===b));render()};$('filters').append(b)}$('size').onchange=()=>document.documentElement.style.setProperty('--cell',$('size').value+'px');$('grid').onchange=()=>$('list').classList.toggle('grid',$('grid').checked);$('search').oninput=render;$('bg').onchange=()=>document.documentElement.style.setProperty('--artbg',$('bg').value);$('close').onclick=()=>$('detail').close();render();
</script></html>'''
(DOC/'全物品图标审阅.html').write_text(html.replace('__ITEMS__',json.dumps(items,ensure_ascii=False)),encoding='utf-8')
print('Review page created: 65 assets, 96 resource references.')

# A contact sheet helps review the complete palette; the HTML/native pages
# retain the actual per-cell display scale.
from PIL import Image, ImageDraw, ImageFont
asset = ROOT / 'textures/items/inventory_density_20261029'
if all((asset / item['file']).exists() for item in items):
    font_path = 'C:/Windows/Fonts/msyh.ttc'
    font = ImageFont.truetype(font_path, 19)
    small = ImageFont.truetype(font_path, 14)
    title = ImageFont.truetype(font_path, 32)
    cols, cw, ch, top = 8, 224, 274, 112
    canvas = Image.new('RGB', (cols*cw, top + ((len(items)+cols-1)//cols)*ch + 38), '#eee4cc')
    draw = ImageDraw.Draw(canvas)
    draw.text((28,18), '行囊物品图标 · 统一重绘', font=title, fill='#403d34')
    draw.text((30,66), '65张行囊图 / 96份物品定义　·　此图按等框查看画风，实际占格见交互审阅页', font=font, fill='#716855')
    for index, item in enumerate(items):
        image = Image.open(asset/item['file']).convert('RGBA')
        bounds = image.getchannel('A').point(lambda a: 255 if a >= 2 else 0).getbbox()
        image = image.crop(bounds)
        image.thumbnail((194,208), Image.Resampling.LANCZOS)
        x, y = index%cols*cw, top+index//cols*ch
        canvas.paste(image, (x+(cw-image.width)//2,y+8), image)
        name = item['name']
        width = draw.textbbox((0,0),name,font=font)[2]
        draw.text((x+(cw-width)//2,y+224),name,font=font,fill='#403d34')
        w,h=item['cells']; label=f'{w}×{h} · {w*h}格'
        width=draw.textbbox((0,0),label,font=small)[2]
        draw.text((x+(cw-width)//2,y+252),label,font=small,fill='#827661')
    canvas.save(DOC/'全图标画风总览.png')
    print('Full contact sheet created: 65 packed icons.')
else:
    print('Contact sheet pending: packed icons are incomplete.')
