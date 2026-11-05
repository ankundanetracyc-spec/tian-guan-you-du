"""Render three real combinations; retain each independently generated layer."""
from pathlib import Path
import base64,json,hashlib,zipfile
from PIL import Image,ImageDraw,ImageFont
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_three_samples_20261103'
PARTS=['hair_front','hair_back','eyebrows','eyes','nose','mouth']
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']
LABELS={'hair_front':'前发','hair_back':'后发','eyebrows':'眉毛','eyes':'眼睛','nose':'鼻子','mouth':'嘴巴'}
TITLES=['清束半披','素挽低髻','编束披发']
def run():
 records=[];data={}
 for part in ORDER:
  data[part]=[]
  for i in (range(1,4) if part in PARTS else [0]):
   p=O/'female'/part/f'{i:02}.png' if i else O/'common'/(part+'.png')
   im=Image.open(p);assert im.size==(1024,1536) and im.mode=='RGBA',str(p)
   data[part].append('data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode())
   records.append({'file':str(p.relative_to(O)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'canvas':[1024,1536]})
 def combine(front,back=None,hide=(),bg=(235,229,217,255)):
  back=back or front;im=Image.new('RGBA',(1024,1536),bg)
  for part in ORDER:
   if part in hide:continue
   p=O/'female'/part/f'{back if part=="hair_back" else front:02}.png' if part in PARTS else O/'common'/(part+'.png')
   im.alpha_composite(Image.open(p).convert('RGBA'))
  return im
 font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
 board=Image.new('RGB',(1260,780),(235,229,217));draw=ImageDraw.Draw(board)
 for i in range(1,4):
  for color,name in [((235,229,217,255),'light'),((44,48,51,255),'dark')]:
   im=combine(i,bg=color);im.convert('RGB').save(O/'preview'/f'set{i:02}_{name}_portrait.jpg',quality=97)
   im.crop((280,70,765,1020)).convert('RGB').save(O/'preview'/f'set{i:02}_{name}_head_chest.jpg',quality=97)
   im.crop((325,135,715,575)).resize((780,880)).convert('RGB').save(O/'qa'/f'set{i:02}_{name}_face_2x.jpg',quality=96)
  combine(i,bg=(0,0,0,0)).save(O/'preview'/f'set{i:02}_transparent.png')
  head=combine(i).crop((300,95,740,870)).resize((420,740),Image.Resampling.LANCZOS).convert('RGB');board.paste(head,((i-1)*420,40));draw.text(((i-1)*420+16,8),str(i)+' · '+TITLES[i-1],font=font,fill='#354039')
  for part in ['hair_front','hair_back']:
   combine(i,hide=(part,)).crop((280,70,765,1020)).convert('RGB').save(O/'qa'/f'set{i:02}_hide_{part}.jpg',quality=96)
 board.save(O/'preview/three_combinations.jpg',quality=97)
 for f in range(1,4):
  for b in range(1,4):combine(f,b).crop((280,70,765,1020)).convert('RGB').save(O/'qa'/f'front{f:02}_back{b:02}.jpg',quality=95)
 html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>女性立绘 · 三组试拼</title><style>*{box-sizing:border-box}body{background:#e9e3d6;color:#354039;font:16px system-ui;margin:0}main{max-width:1220px;margin:auto;display:flex;gap:30px;padding:20px}canvas{max-width:62vw;max-height:94vh;object-fit:contain;background:#ebe5d9}aside{flex:1;min-width:250px}h1{font-size:24px}button,select{font:inherit;background:#f5efdf;color:inherit;padding:8px;border:1px solid #9b9e8e;border-radius:5px}button{margin:4px;cursor:pointer}label{display:flex;gap:12px;align-items:center;margin:16px 0}label select{flex:1}input{width:17px;height:17px}p,footer{font-size:14px;line-height:1.7}footer{margin-top:24px}@media(max-width:700px){main{display:block}canvas{max-width:100%;height:65vh}aside{min-width:0}}</style><main><canvas width="1024" height="1536"></canvas><aside><h1>女性立绘 · 三组试拼</h1><div id="sets"></div><p>先看整组，也可以分别换前发、后发和五官。</p><div id="controls"></div><button id="random">随机组合</button><button id="save">保存当前透明立绘</button><label>背景<select id="bg"><option value="#ebe5d9">浅底</option><option value="#2c3033">深底</option></select></label><footer>本轮试拼候选，尚未替换游戏素材。</footer></aside></main><script>
const sources=DATA,order=ORDER,labels=LABELS,titles=TITLES,images={},state={},visible={};const canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d');function draw(){ctx.clearRect(0,0,1024,1536);for(const p of order){if(visible[p]===false)continue;const im=images[p]?.[state[p]||0];if(im?.complete)ctx.drawImage(im,0,0)}}
for(const p of order){state[p]=0;images[p]=sources[p].map(s=>{let im=new Image();im.onload=draw;im.src=s;return im});if(labels[p]){let row=document.createElement('label'),check=document.createElement('input'),sel=document.createElement('select');check.type='checkbox';check.checked=true;check.onchange=()=>{visible[p]=check.checked;draw()};for(let i=0;i<3;i++)sel.add(new Option(titles[i],i));sel.dataset.part=p;sel.onchange=()=>{state[p]=+sel.value;draw()};row.append(check,document.createTextNode(labels[p]),sel);document.querySelector('#controls').append(row)}}function sync(){document.querySelectorAll('[data-part]').forEach(s=>s.value=state[s.dataset.part]);draw()}
titles.forEach((name,i)=>{let b=document.createElement('button');b.textContent=(i+1)+' · '+name;b.onclick=()=>{Object.keys(labels).forEach(p=>state[p]=i);sync()};document.querySelector('#sets').append(b)});document.querySelector('#random').onclick=()=>{Object.keys(labels).forEach(p=>state[p]=Math.floor(Math.random()*3));sync()};document.querySelector('#bg').onchange=e=>canvas.style.background=e.target.value;document.querySelector('#save').onclick=()=>{let a=document.createElement('a');a.download='女性立绘试拼.png';a.href=canvas.toDataURL();a.click()};</script></html>'''.replace('DATA',json.dumps(data)).replace('ORDER',json.dumps(ORDER)).replace('LABELS',json.dumps(LABELS,ensure_ascii=False)).replace('TITLES',json.dumps(TITLES,ensure_ascii=False))
 (O/'三组自由试拼.html').write_text(html,encoding='utf-8')
 (O/'manifest.json').write_text(json.dumps({'status':'three_generated_candidates_for_user_review_not_installed','independent_parts':18,'canvas':[1024,1536],'titles':TITLES,'layer_order':ORDER,'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
 with zipfile.ZipFile(O/'女性三组独立PNG.zip','w',zipfile.ZIP_DEFLATED) as z:
  for p in (O/'female').rglob('*.png'):z.write(p,p.relative_to(O))
  for p in (O/'common').glob('*.png'):z.write(p,p.relative_to(O))
  for name in ['manifest.json','三组自由试拼.html']:z.write(O/name,name)
 print('18 independent parts; 3 full previews; 9 front/back mixes; local preview and ZIP ready.')
if __name__=='__main__':run()
