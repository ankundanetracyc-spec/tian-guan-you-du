"""Package independent portrait parts and a self-contained local mix preview."""
from pathlib import Path
import json, base64, hashlib, shutil, zipfile
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'mods/appearance/portrait_rebuild_20261103/three_style_parts'
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004/female'
PARTS=['hair_back','eyes','eyebrows','nose','mouth','hair_front']
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']
LABELS={'hair_back':'后发','hair_front':'前发','eyes':'眼睛','eyebrows':'眉毛','nose':'鼻子','mouth':'嘴巴'}

def run():
    common=OUT/'female/common';common.mkdir(parents=True,exist_ok=True)
    for part,idx in [('face_shape','04'),('clothing','01')]:
        source=OUT/'female/clothing/01.png' if part=='clothing' and (OUT/'female/clothing/01.png').exists() else ACTIVE/part/(idx+'.png')
        shutil.copyfile(source,common/(part+'.png'))
    data={};records=[]
    for part in ORDER:
        indices=range(1,4) if part in PARTS else [0]
        data[part]=[]
        for i in indices:
            p=OUT/'female'/part/f'{i:02}.png' if i else common/(part+'.png')
            im=Image.open(p)
            assert im.size==(1024,1536) and im.mode=='RGBA',str(p)
            data[part].append('data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode())
            records.append({'file':str(p.relative_to(OUT)).replace('\\','/'),'size':list(im.size),'mode':im.mode,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    preview=OUT/'preview';preview.mkdir(exist_ok=True)
    for f in range(1,4):
        for b in range(1,4):
            canvas=Image.new('RGBA',(1024,1536),(235,229,217,255))
            for part in ORDER:
                p=OUT/'female'/part/f'{b if part=="hair_back" else f:02}.png' if part in PARTS else common/(part+'.png')
                canvas.alpha_composite(Image.open(p))
            canvas.convert('RGB').save(preview/f'front_{f:02}_back_{b:02}.jpg',quality=94)
            if f==b:
                canvas.crop((280,35,765,690)).convert('RGB').save(preview/f'set_{f:02}_head.jpg',quality=96)
    html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>女性立绘 · 三组自由组合</title>
<style>*{box-sizing:border-box}body{margin:0;background:#e8e2d3;color:#343d36;font:17px system-ui}main{max-width:1200px;margin:auto;display:flex;gap:36px;padding:24px}canvas{max-width:58vw;max-height:92vh;object-fit:contain;background:#e4ded0}aside{flex:1;min-width:260px}h1{font-size:25px}label{display:flex;align-items:center;gap:12px;margin:18px 0}select,button{font:inherit;padding:9px;background:#f5efdf;border:1px solid #9a9d8b;border-radius:5px}select{flex:1}button{cursor:pointer;margin:4px}p{font-size:14px;line-height:1.7}input{width:18px;height:18px}footer{font-size:13px;margin-top:24px}@media(max-width:700px){main{display:block}canvas{max-width:100%;height:65vh}aside{min-width:0}}</style>
<main><canvas width="1024" height="1536"></canvas><aside><h1>女性立绘 · 三组自由组合</h1><p>沿用原尺寸与柔和卵面。每个部位可独立切换，勾选控制显隐。</p><div id="controls"></div><div id="sets"></div><button id="random">随机组合</button><button id="save">保存当前透明立绘</button><label>背景<select id="bg"><option value="#e4ded0">浅底</option><option value="#353a3b">深底</option></select></label><footer>当前为候选预览，未替换游戏素材。衣服已清理残留发线、领口碎片，并柔化重黑边。</footer></aside></main><script>
const sources=DATA,order=ORDER,labels=LABELS,images={},state={},visible={};
const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');
function draw(){ctx.clearRect(0,0,1024,1536);for(const p of order){if(visible[p]===false)continue;const im=images[p]?.[state[p]||0];if(im?.complete)ctx.drawImage(im,0,0)}}
for(const p of order){images[p]=sources[p].map(s=>{let im=new Image();im.onload=draw;im.src=s;return im});state[p]=0;if(labels[p]){let l=document.createElement('label'),check=document.createElement('input'),sel=document.createElement('select');check.type='checkbox';check.checked=true;check.onchange=()=>{visible[p]=check.checked;draw()};for(let i=0;i<3;i++)sel.add(new Option('第'+(i+1)+'组',i));sel.dataset.part=p;sel.onchange=()=>{state[p]=+sel.value;draw()};l.append(check,document.createTextNode(labels[p]),sel);document.querySelector('#controls').append(l)}}
function sync(){document.querySelectorAll('[data-part]').forEach(s=>s.value=state[s.dataset.part]);draw()}
for(let i=0;i<3;i++){let b=document.createElement('button');b.textContent='整组 '+(i+1);b.onclick=()=>{Object.keys(labels).forEach(p=>state[p]=i);sync()};document.querySelector('#sets').append(b)}
document.querySelector('#random').onclick=()=>{Object.keys(labels).forEach(p=>state[p]=Math.floor(Math.random()*3));sync()};document.querySelector('#bg').onchange=e=>canvas.style.background=e.target.value;document.querySelector('#save').onclick=()=>{let a=document.createElement('a');a.download='女性立绘组合.png';a.href=canvas.toDataURL();a.click()};
</script></html>'''.replace('DATA',json.dumps(data)).replace('ORDER',json.dumps(ORDER)).replace('LABELS',json.dumps(LABELS,ensure_ascii=False))
    (OUT/'自由组合预览.html').write_text(html,encoding='utf-8')
    (OUT/'asset_manifest.json').write_text(json.dumps({'status':'candidate_review_not_installed','canvas':[1024,1536],'runtime_offset':[0,0],'parts':records,'note':'Hair adapted from previous generated candidates; features extracted from reference pixels. See source reports. Clothing polish candidate preserves original canvas and placement.'},ensure_ascii=False,indent=2),encoding='utf-8')
    with zipfile.ZipFile(OUT/'女性三组独立部件.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in (OUT/'female').rglob('*.png'):z.write(p,p.relative_to(OUT))
        for name in ['asset_manifest.json','自由组合预览.html','feature_extraction.json','female_hair_sources_and_limits.json']:
            if (OUT/name).exists():z.write(OUT/name,name)
    print(json.dumps({'independent_parts':18,'shared_base_layers':2,'front_back_previews':9,'output':str(OUT)},ensure_ascii=False))

if __name__=='__main__':run()
