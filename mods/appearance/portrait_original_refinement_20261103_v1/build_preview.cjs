/* Deterministic source-over previews. Source sprites are never altered. */
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');

const ROOT = __dirname;
const OUT = path.join(ROOT, 'preview');
const W = 1024, H = 1536;
const ORDER = ['hair_back', 'face_shape', 'clothing', 'eyes', 'eyebrows', 'nose', 'mouth', 'face_accessory', 'hair_front'];
const LABELS = { hair_back: '后发', face_shape: '脸型', clothing: '衣服', eyes: '眼睛', eyebrows: '眉毛', nose: '鼻子', mouth: '嘴巴', face_accessory: '面饰', hair_front: '前发' };
const COLORS = { warm: '#eae4d8', dark: '#252b30' };
const ANCHORS = {
  male: [[517,277,'发际'], [447,310,'左发际'], [590,310,'右发际'], [464,375,'左眼'], [569,375,'右眼'], [441,550,'左颈领'], [594,550,'右颈领']],
  female: [[519,277,'发际'], [442,310,'左发际'], [596,310,'右发际'], [464,379,'左眼'], [574,379,'右眼'], [450,552,'左颈领'], [584,552,'右颈领']],
};
const canvas = (width = W, height = H, background = {r:0,g:0,b:0,alpha:0}) => sharp({create:{width,height,channels:4,background}});
const over = async (buffers, width = W, height = H, background) => canvas(width,height,background).composite(buffers.map(input => ({input, left:0, top:0, blend:'over'}))).png().toBuffer();
const pngPath = (set, gender, part) => path.join(ROOT, set, gender, part + '.png');

async function main() {
  await fs.mkdir(OUT, {recursive:true});
  await fs.mkdir(path.join(ROOT, 'qa'), {recursive:true});
  const inputs = {baseline:{male:{},female:{}}, parts:{male:{},female:{}}};
  const metadata = [];
  for (const set of Object.keys(inputs)) {
    for (const gender of ['male','female']) {
      for (const part of ORDER) {
        const file = pngPath(set,gender,part);
        const buffer = await fs.readFile(file);
        const meta = await sharp(buffer).metadata();
        if (meta.width !== W || meta.height !== H || meta.channels !== 4 || !meta.hasAlpha || meta.format !== 'png') {
          throw new Error('尺寸或RGBA格式不符：' + file + ' ' + JSON.stringify(meta));
        }
        inputs[set][gender][part] = buffer;
        metadata.push({set,gender,part,path:path.relative(ROOT,file).replaceAll('\\','/'),width:meta.width,height:meta.height,channels:meta.channels,hasAlpha:meta.hasAlpha,format:meta.format,sha256:crypto.createHash('sha256').update(buffer).digest('hex')});
      }
    }
  }
  const composites = {baseline:{},parts:{}};
  for (const set of Object.keys(inputs)) {
    for (const gender of ['male','female']) {
      const image = await over(ORDER.map(p=>inputs[set][gender][p]));
      composites[set][gender] = image;
      if (set === 'parts') await fs.writeFile(path.join(OUT, gender + '.png'), image);
      for (const [theme,color] of Object.entries(COLORS)) {
        const name = (set === 'baseline' ? 'original_' : '') + gender + '_' + theme + '.png';
        await fs.writeFile(path.join(OUT,name), await over([image],W,H,color));
      }
    }
  }
  const pair = async (images, width, height, name, bg = COLORS.warm) => {
    const tileWidth = width / images.length;
    const tiles = await Promise.all(images.map(input=>sharp(input).resize(Math.round(tileWidth),height,{fit:'contain',background:bg}).png().toBuffer()));
    await canvas(width,height,bg).composite(tiles.map((input,i)=>({input,left:Math.round(i*tileWidth),top:0}))).png().toFile(path.join(OUT,name));
  };
  await pair([composites.parts.male,composites.parts.female],1600,1200,'pair.png');
  await pair([composites.parts.male,composites.parts.female],1600,1200,'pair_dark.png',COLORS.dark);
  const comparison = [composites.baseline.male,composites.parts.male,composites.baseline.female,composites.parts.female];
  await pair(comparison,2048,768,'before_after.png');
  const heads = await Promise.all(comparison.map(input=>sharp(input).extract({left:300,top:90,width:440,height:610}).png().toBuffer()));
  await pair(heads,1760,610,'face_compare.png');
  await pair(heads,1760,610,'face_compare_dark.png',COLORS.dark);
  for (const gender of ['male','female']) {
    const layers = inputs.parts[gender];
    const variants = [[],['hair_front'],['hair_back'],['hair_front','hair_back']];
    const tiles = await Promise.all(variants.map(hidden=>over(ORDER.filter(p=>!hidden.includes(p)).map(p=>layers[p]))));
    await pair(tiles,2048,768,gender+'_visibility.png');
  }
  const encoded = {};
  for (const set of Object.keys(inputs)) {
    encoded[set] = {};
    for (const gender of ['male','female']) {
      encoded[set][gender] = {};
      for (const part of ORDER) encoded[set][gender][part] = 'data:image/png;base64,' + inputs[set][gender][part].toString('base64');
    }
  }
  const html = `<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="icon" href="data:,"><title>原版立绘优化 · 独立候选</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#151b1c;color:#e6e5df;font:15px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1480px;margin:auto;padding:24px}h1{font-size:24px;margin:0 0 5px}p{margin:5px 0 16px;color:#b9c4bf}.toolbar{display:flex;gap:12px;flex-wrap:wrap;align-items:center;padding:14px;background:#202929;border:1px solid #3b4c47;border-radius:9px}button,select{font:inherit;background:#30433b;color:#f0eee8;border:1px solid #627e70;border-radius:5px;padding:6px 12px;cursor:pointer}label{display:inline-flex;gap:6px;align-items:center}.parts{display:flex;gap:12px;flex-wrap:wrap;padding:14px 0}.parts label{background:#202929;padding:4px 10px;border-radius:5px}.portraits{display:grid;grid-template-columns:1fr 1fr;gap:16px}.portrait{min-width:0}h2{font-size:16px;margin:6px 0}canvas{display:block;width:100%;height:auto;border-radius:7px;background:#eae4d8}body[data-theme="dark"] canvas{background:#252b30}body[data-theme="check"] canvas{background:conic-gradient(#c8c8c8 25%,#eee 0 50%,#c8c8c8 0 75%,#eee 0) 0 0/24px 24px}.note{font-size:13px;margin-top:12px}#status{min-height:24px;color:#d3c493}@media(max-width:650px){main{padding:12px}.toolbar{gap:8px}.portraits{gap:8px}h1{font-size:21px}button,select{padding:5px 7px}}
</style><body data-theme="warm"><main>
<h1>原版立绘优化 · 独立候选</h1><p>四张前后发新绘，其余十四张部件沿用原件。男女各一套，保持 1024 × 1536 共同画布；待美术审阅。</p>
<div class="toolbar">
<label>人物 <select id="gender"><option value="male">男修</option><option value="female">女修</option></select></label>
<label>底色 <select id="theme"><option value="warm">浅暖底</option><option value="dark">深底</option><option value="check">透明网格</option></select></label>
<label>前发 <select id="front"><option value="parts">新版</option><option value="baseline">原版</option></select></label>
<label>后发 <select id="back"><option value="parts">新版</option><option value="baseline">原版</option></select></label>
<label><input id="anchors" type="checkbox">显示参考锚点</label>
<button id="reset">恢复基础组合</button><button id="download">导出当前透明 PNG</button>
</div><div class="parts" id="parts"></div><div id="status" role="status"></div>
<div class="portraits"><section class="portrait"><h2>原版基础组合</h2><canvas id="original" width="1024" height="1536" aria-label="原版基础组合"></canvas></section><section class="portrait"><h2>新版实际分件拼合</h2><canvas id="candidate" width="1024" height="1536" aria-label="新版实际分件拼合"></canvas></section></div>
<p class="note">两侧采用相同显示比例；部位显隐同时作用于两侧。前后发选项只影响右侧。锚点为原角色空间的参考位置，不代表新件已通过完整三点配准认证。导出保持透明背景，不包含锚点。</p>
</main><script>
const DATA=${JSON.stringify(encoded)};
const ORDER=${JSON.stringify(ORDER)};
const LABELS=${JSON.stringify(LABELS)};
const ANCHORS=${JSON.stringify(ANCHORS)};
const images={baseline:{male:{},female:{}},parts:{male:{},female:{}}};
const $=id=>document.getElementById(id);
const state={gender:'male',front:'parts',back:'parts',anchors:false,visible:Object.fromEntries(ORDER.map(p=>[p,true]))};
const controls=$('parts');
ORDER.forEach(part=>{const label=document.createElement('label');const input=document.createElement('input');input.type='checkbox';input.checked=true;input.dataset.part=part;input.id='show-'+part;input.addEventListener('change',()=>{state.visible[part]=input.checked;render()});label.append(input,document.createTextNode(LABELS[part]));controls.append(label)});
function draw(ctx,candidate,guides){ctx.clearRect(0,0,1024,1536);for(const part of ORDER){if(!state.visible[part])continue;let set='baseline';if(candidate)set=part==='hair_front'?state.front:part==='hair_back'?state.back:'parts';ctx.drawImage(images[set][state.gender][part],0,0)}if(guides){ctx.save();ctx.lineWidth=2;ctx.font='18px system-ui';for(const [x,y,name]of ANCHORS[state.gender]){ctx.strokeStyle='#21a2ca';ctx.fillStyle='#163a46';ctx.beginPath();ctx.arc(x,y,6,0,Math.PI*2);ctx.stroke();ctx.strokeStyle='#fff9';ctx.lineWidth=4;ctx.strokeText(name,x+9,y-8);ctx.fillText(name,x+9,y-8);ctx.lineWidth=2;}ctx.restore()}}
function render(){draw($('original').getContext('2d'),false,state.anchors);draw($('candidate').getContext('2d'),true,state.anchors);$('status').textContent=(state.gender==='male'?'男修':'女修')+' · 同原点零位移合成 · 前发'+(state.front==='parts'?'新版':'原版')+'／后发'+(state.back==='parts'?'新版':'原版')}
$('gender').addEventListener('change',e=>{state.gender=e.target.value;render()});
$('theme').addEventListener('change',e=>document.body.dataset.theme=e.target.value);
$('front').addEventListener('change',e=>{state.front=e.target.value;render()});
$('back').addEventListener('change',e=>{state.back=e.target.value;render()});
$('anchors').addEventListener('change',e=>{state.anchors=e.target.checked;render()});
$('reset').addEventListener('click',()=>{state.front='parts';state.back='parts';state.anchors=false;ORDER.forEach(p=>state.visible[p]=true);$('front').value='parts';$('back').value='parts';$('anchors').checked=false;document.querySelectorAll('[data-part]').forEach(e=>e.checked=true);render()});
$('download').addEventListener('click',()=>{const output=document.createElement('canvas');output.width=1024;output.height=1536;draw(output.getContext('2d'),true,false);const a=document.createElement('a');a.download=state.gender+'_portrait_candidate.png';a.href=output.toDataURL('image/png');a.click()});
(async()=>{await Promise.all(Object.entries(DATA).flatMap(([set,genders])=>Object.entries(genders).flatMap(([gender,parts])=>Object.entries(parts).map(([part,src])=>new Promise((resolve,reject)=>{const img=new Image();img.onload=()=>{images[set][gender][part]=img;resolve()};img.onerror=()=>reject(new Error('部件加载失败 '+set+'/'+gender+'/'+part));img.src=src})))));render();window.portraitPreviewReady=true})().catch(e=>{$('status').textContent=e.message;console.error(e)});
</script></body></html>`;
  await fs.writeFile(path.join(ROOT,'预览.html'),html,'utf8');
  await fs.writeFile(path.join(ROOT,'qa','preview_sources.json'),JSON.stringify({canvas:[W,H],draw_order:ORDER,source_over:true,source_files:metadata,actual_candidate_parts:metadata.filter(x=>x.set==='parts').length,artistic_approval:'pending user review',registration_certification:'not asserted'},null,2));
  console.log(JSON.stringify({candidate_parts:18,baseline_parts:18,all_sources_rgba_1024x1536:true,preview:path.join(ROOT,'预览.html'),outputs:await fs.readdir(OUT)}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
