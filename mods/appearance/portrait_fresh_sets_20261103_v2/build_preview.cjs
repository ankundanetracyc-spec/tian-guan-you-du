/* Reproducible source-over previews only. Never edits, trims or re-centers source PNGs. */
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const ROOT = __dirname, OUT = path.join(ROOT, 'preview'), QA = path.join(ROOT, 'qa');
const W = 1024, H = 1536;
const GENDERS = ['male', 'female'];
const VARIABLE = ['eyebrows', 'eyes', 'nose', 'mouth', 'hair_front', 'hair_back'];
const ORDER = ['hair_back', 'face_shape', 'clothing', 'eyes', 'eyebrows', 'nose', 'mouth', 'face_accessory', 'hair_front'];
const LABELS = {hair_back:'后发',face_shape:'脸型',clothing:'衣服',eyes:'眼睛',eyebrows:'眉毛',nose:'鼻子',mouth:'嘴巴',face_accessory:'面饰',hair_front:'前发'};
const COLORS = {warm:'#eae4d8',dark:'#252b30'};
const ANCHORS = {
  male:[[517,277,'发际'],[447,310,'左发际'],[590,310,'右发际'],[464,375,'左眼'],[569,375,'右眼'],[441,550,'左颈领'],[594,550,'右颈领']],
  female:[[519,277,'发际'],[442,310,'左发际'],[596,310,'右发际'],[464,379,'左眼'],[574,379,'右眼'],[450,552,'左颈领'],[584,552,'右颈领']],
};
const canvas=(width=W,height=H,background={r:0,g:0,b:0,alpha:0})=>sharp({create:{width,height,channels:4,background}});
const over=(buffers,width=W,height=H,background)=>canvas(width,height,background).composite(buffers.map(input=>({input,left:0,top:0,blend:'over'}))).png().toBuffer();
const sourcePath=(gender,part,index)=>path.join(ROOT,gender,part+(VARIABLE.includes(part)?'/'+String(index+1).padStart(2,'0'):'' )+'.png');

async function main(){
  await fs.mkdir(OUT,{recursive:true});await fs.mkdir(QA,{recursive:true});
  const inputs={male:{},female:{}},metadata=[],missing=[];
  for(const gender of GENDERS)for(const part of ORDER){inputs[gender][part]=[];for(let index=0;index<(VARIABLE.includes(part)?3:1);index++){
    const file=sourcePath(gender,part,index);let buffer;try{buffer=await fs.readFile(file)}catch(error){if(error.code==='ENOENT'){missing.push(path.relative(ROOT,file));continue}throw error}
    const meta=await sharp(buffer).metadata();
    if(meta.width!==W||meta.height!==H||meta.channels!==4||!meta.hasAlpha||meta.format!=='png')throw new Error('素材必须为独立1024×1536 RGBA PNG：'+file+' '+JSON.stringify(meta));
    inputs[gender][part].push(buffer);metadata.push({gender,part,index:VARIABLE.includes(part)?index+1:null,kind:VARIABLE.includes(part)?'new_independent_generated':'reused_fixed',path:path.relative(ROOT,file).replaceAll('\\','/'),width:W,height:H,channels:meta.channels,hasAlpha:meta.hasAlpha,format:meta.format,sha256:crypto.createHash('sha256').update(buffer).digest('hex')});
  }}
  if(missing.length)throw new Error('源素材未齐，共缺'+missing.length+'件：\n'+missing.join('\n'));
  const render=(gender,choices={},hidden=[])=>over(ORDER.filter(part=>!hidden.includes(part)).map(part=>inputs[gender][part][VARIABLE.includes(part)?(choices[part]??0):0]));
  async function sheet(images,cols,tileWidth,tileHeight,name,bg=COLORS.warm){
    const rows=Math.ceil(images.length/cols),tiles=await Promise.all(images.map(input=>sharp(input).resize(tileWidth,tileHeight,{fit:'contain',background:bg}).png().toBuffer()));
    await canvas(cols*tileWidth,rows*tileHeight,bg).composite(tiles.map((input,index)=>({input,left:(index%cols)*tileWidth,top:Math.floor(index/cols)*tileHeight}))).png().toFile(path.join(OUT,name));
  }
  const composites={male:[],female:[]};
  for(const gender of GENDERS){
    for(let index=0;index<3;index++){
      const id=String(index+1).padStart(2,'0'),choices=Object.fromEntries(VARIABLE.map(part=>[part,index])),image=await render(gender,choices);
      composites[gender].push(image);await fs.writeFile(path.join(OUT,gender+'_set'+id+'.png'),image);
      for(const [theme,color]of Object.entries(COLORS))await fs.writeFile(path.join(OUT,gender+'_set'+id+'_'+theme+'.png'),await over([image],W,H,color));
      const visibility=await Promise.all([[],['hair_front'],['hair_back'],['hair_front','hair_back']].map(hidden=>render(gender,choices,hidden)));
      await sheet(visibility,4,512,768,gender+'_set'+id+'_visibility.png');
      await sheet(visibility,4,512,768,gender+'_set'+id+'_visibility_dark.png',COLORS.dark);
    }
    await sheet(composites[gender],3,684,1026,gender+'_three_sets.png');
    await sheet(composites[gender],3,684,1026,gender+'_three_sets_dark.png',COLORS.dark);
    const mixes=[];for(let front=0;front<3;front++)for(let back=0;back<3;back++)mixes.push(await render(gender,{hair_front:front,hair_back:back}));
    await sheet(mixes,3,384,576,gender+'_hair_mix_9.png');
    await sheet(mixes,3,384,576,gender+'_hair_mix_9_dark.png',COLORS.dark);
  }
  const six=GENDERS.flatMap(gender=>composites[gender]);
  const heads=await Promise.all(six.map(input=>sharp(input).extract({left:300,top:90,width:440,height:610}).png().toBuffer()));
  await sheet(heads,6,440,610,'six_faces.png');await sheet(heads,6,440,610,'six_faces_dark.png',COLORS.dark);
  await sheet(six,3,512,768,'all_six_sets.png');
  const encoded={male:{},female:{}};for(const gender of GENDERS)for(const part of ORDER)encoded[gender][part]=inputs[gender][part].map(buffer=>'data:image/png;base64,'+buffer.toString('base64'));
  const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="icon" href="data:,"><title>全新三组立绘 · 独立部件预览</title>
<style>*{box-sizing:border-box}body{margin:0;background:#151b1c;color:#e6e5df;font:15px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1360px;margin:auto;padding:24px}h1{font-size:25px;margin:0 0 6px}p{margin:5px 0 16px;color:#b9c4bf}.toolbar,.selectors,.visibility{display:flex;gap:12px;flex-wrap:wrap;align-items:center}.toolbar{padding:14px;background:#202929;border:1px solid #3b4c47;border-radius:9px}.selectors{padding:14px 0}.visibility{padding:0 0 14px}.visibility label{background:#202929;padding:4px 9px;border-radius:5px}label{display:inline-flex;gap:6px;align-items:center}button,select{font:inherit;background:#30433b;color:#f0eee8;border:1px solid #627e70;border-radius:5px;padding:6px 10px;cursor:pointer}canvas{display:block;width:min(100%,740px);height:auto;margin:auto;border-radius:7px;background:#eae4d8}body[data-theme="dark"] canvas{background:#252b30}body[data-theme="check"] canvas{background:conic-gradient(#c8c8c8 25%,#eee 0 50%,#c8c8c8 0 75%,#eee 0) 0 0/24px 24px}.note{font-size:13px;margin-top:14px}#status{min-height:27px;color:#d3c493}@media(max-width:650px){main{padding:12px}.toolbar,.selectors,.visibility{gap:8px}h1{font-size:21px}button,select{padding:5px 7px}}</style>
<body data-theme="warm"><main><h1>全新三组立绘 · 独立部件预览</h1><p>男女各三组：眉、眼、鼻、嘴、前发和后发共36件全新独立生成；脸耳底板和衣服沿用。每件保持1024 × 1536共同画布，待美术审阅。</p>
<div class="toolbar"><label>人物<select id="gender"><option value="male">男修</option><option value="female">女修</option></select></label><label>整组<select id="set"><option value="0">第一组</option><option value="1">第二组</option><option value="2">第三组</option></select></label><label>底色<select id="theme"><option value="warm">浅暖底</option><option value="dark">深底</option><option value="check">透明网格</option></select></label><label><input id="anchors" type="checkbox">参考锚点</label><button id="random">随机组合</button><button id="reset">恢复第一组</button><button id="download">导出透明PNG</button></div>
<div class="selectors" id="selectors"></div><div class="visibility" id="visibility"></div><div id="status" role="status">正在加载部件…</div><canvas id="portrait" width="1024" height="1536" aria-label="当前独立部件实际拼合"></canvas><p class="note">各部件按原点叠加，不按可见边界居中。参考锚点用于比较发际、眼位与颈领位置，不代表已经通过三点配准认证。透明导出保持1024 × 1536，不包含背景和锚点。</p></main>
<script>const DATA=${JSON.stringify(encoded)},ORDER=${JSON.stringify(ORDER)},VARIABLE=${JSON.stringify(VARIABLE)},LABELS=${JSON.stringify(LABELS)},ANCHORS=${JSON.stringify(ANCHORS)};
const images={male:{},female:{}},$=id=>document.getElementById(id),fresh=()=>Object.fromEntries(VARIABLE.map(part=>[part,0]));
const state={gender:'male',choices:{male:fresh(),female:fresh()},visible:Object.fromEntries(ORDER.map(part=>[part,true])),anchors:false,ready:false};
VARIABLE.forEach(part=>{const label=document.createElement('label'),select=document.createElement('select');select.id='choice-'+part;select.dataset.choice=part;for(let i=0;i<3;i++){const option=document.createElement('option');option.value=String(i);option.textContent='第'+(i+1)+'款';select.append(option)}select.addEventListener('change',()=>{state.choices[state.gender][part]=Number(select.value);syncSet();render()});label.append(document.createTextNode(LABELS[part]),select);$('selectors').append(label)});
ORDER.forEach(part=>{const label=document.createElement('label'),input=document.createElement('input');input.type='checkbox';input.checked=true;input.id='show-'+part;input.dataset.part=part;input.addEventListener('change',()=>{state.visible[part]=input.checked;render()});label.append(input,document.createTextNode(LABELS[part]));$('visibility').append(label)});
function syncSet(){const chosen=VARIABLE.map(part=>state.choices[state.gender][part]);$('set').value=chosen.every(value=>value===chosen[0])?String(chosen[0]):''}
function syncControls(){VARIABLE.forEach(part=>$('choice-'+part).value=String(state.choices[state.gender][part]));syncSet()}
function draw(ctx,guides){ctx.clearRect(0,0,1024,1536);if(!state.ready)return;for(const part of ORDER){if(state.visible[part])ctx.drawImage(images[state.gender][part][VARIABLE.includes(part)?state.choices[state.gender][part]:0],0,0)}if(guides){ctx.save();ctx.lineWidth=2;ctx.font='18px system-ui';for(const[x,y,name]of ANCHORS[state.gender]){ctx.strokeStyle='#21a2ca';ctx.fillStyle='#163a46';ctx.beginPath();ctx.arc(x,y,6,0,Math.PI*2);ctx.stroke();ctx.strokeStyle='#fff9';ctx.lineWidth=4;ctx.strokeText(name,x+9,y-8);ctx.fillText(name,x+9,y-8);ctx.lineWidth=2}ctx.restore()}}
function render(){if(!state.ready)return;draw($('portrait').getContext('2d'),state.anchors);$('status').textContent=(state.gender==='male'?'男修':'女修')+' · '+VARIABLE.map(part=>LABELS[part]+String(state.choices[state.gender][part]+1).padStart(2,'0')).join('／')+' · 独立部件实际拼合'}
$('gender').addEventListener('change',event=>{state.gender=event.target.value;syncControls();render()});$('set').addEventListener('change',event=>{if(event.target.value==='')return;VARIABLE.forEach(part=>state.choices[state.gender][part]=Number(event.target.value));syncControls();render()});$('theme').addEventListener('change',event=>document.body.dataset.theme=event.target.value);$('anchors').addEventListener('change',event=>{state.anchors=event.target.checked;render()});$('random').addEventListener('click',()=>{VARIABLE.forEach(part=>state.choices[state.gender][part]=Math.floor(Math.random()*3));syncControls();render()});$('reset').addEventListener('click',()=>{state.choices[state.gender]=fresh();state.anchors=false;$('anchors').checked=false;ORDER.forEach(part=>{state.visible[part]=true;$('show-'+part).checked=true});syncControls();render()});$('download').addEventListener('click',()=>{if(!state.ready)return;const output=document.createElement('canvas');output.width=1024;output.height=1536;draw(output.getContext('2d'),false);const link=document.createElement('a');link.download=state.gender+'_fresh_portrait.png';link.href=output.toDataURL('image/png');link.click()});
(async()=>{await Promise.all(Object.entries(DATA).flatMap(([gender,parts])=>Object.entries(parts).flatMap(([part,sources])=>{images[gender][part]=[];return sources.map((src,index)=>new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>{if(image.naturalWidth!==1024||image.naturalHeight!==1536){reject(new Error('部件尺寸错误 '+gender+'/'+part));return}images[gender][part][index]=image;resolve()};image.onerror=()=>reject(new Error('加载失败 '+gender+'/'+part+'/'+index));image.src=src}))})));state.ready=true;syncControls();render();window.portraitPreviewReady=true})().catch(error=>{$('status').textContent=error.message;console.error(error)});
</script></body></html>`;
  await fs.writeFile(path.join(ROOT,'预览.html'),html,'utf8');
  const record={canvas:[W,H],draw_order:ORDER,source_over:true,source_mutation:false,source_files:metadata,new_independent_parts:metadata.filter(item=>item.kind==='new_independent_generated').length,reused_fixed_parts:metadata.filter(item=>item.kind==='reused_fixed').length,artistic_approval:'pending user review',registration_certification:'reference anchors only; no three-point certification asserted',hair_mix_layout:'rows:front01..03; columns:back01..03; other parts use set01',visibility_layout:'full / no front / no back / no front or back',six_faces_layout:'male01 male02 male03 female01 female02 female03'};
  await fs.writeFile(path.join(QA,'preview_sources.json'),JSON.stringify(record,null,2));
  console.log(JSON.stringify({new_parts:record.new_independent_parts,fixed_parts:record.reused_fixed_parts,all_sources_rgba_1024x1536:true,preview:path.join(ROOT,'预览.html'),outputs:await fs.readdir(OUT)}));
}
main().catch(error=>{console.error(error);process.exitCode=1});
