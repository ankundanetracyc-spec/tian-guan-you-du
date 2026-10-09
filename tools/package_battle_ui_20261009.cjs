// Package generated artwork into isolated PNG sprites; no artwork is redrawn.
const fs = require('fs/promises');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const muted = process.argv.includes('--muted');
const revision = muted ? 'v2' : 'v1';
const out = path.join(root, `textures/ui/combat/queue_ui_20261009${muted ? '_muted_v2' : ''}`);
const generated = 'D:/CodexData/generated_images/01a11020-5520-7640-a826-848cd695f2e3';
const inputs = {
  background: 'exec-79372ed7-0343-4244-b958-adf1129fbbfe.png',
  wide_frames: 'exec-f1ae1994-08c5-47c2-8480-05c9e02e45ce.png',
  panels: 'exec-ddba4904-fab5-4fbc-aba8-c5c0ab668a53.png',
  icons: 'exec-7179073a-7a24-4bab-bfb3-63229c923e2b.png',
  containers: 'exec-1a4c2eb8-dec4-47e6-bfff-717183107b2f.png',
  approved_reference: 'exec-8286e58f-f62b-4eba-838e-7d4c8166a29d.png',
};
if (muted) Object.assign(inputs, {
  background: 'exec-3d9066e5-d199-4788-8ce0-94a52b405e3e.png',
  wide_frames: 'exec-01ea4f47-9945-4196-9c72-1d50fbe0fe5c.png',
  panels: 'exec-8fe80105-289b-47b9-8b7b-118cb819120f.png',
  icons: 'exec-0455056f-878f-41b3-a08a-c395eee5cdb6.png',
  containers: 'exec-994ca5ae-de61-46b3-ace5-3138fe934f36.png',
});
const entries = [];
async function sprite(id, source, region, display, margins = null, square = false) {
  const sourcePath = path.join(out, 'source', `${source}.png`);
  const raw = await sharp(sourcePath).extract(region).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const {width:w,height:h,channels:c}=raw.info;
  let minX=w,minY=h,maxX=-1,maxY=-1;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    if(raw.data[(y*w+x)*c+3]>8){minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y);}
  }
  if(maxX<0)throw new Error(`Empty sprite ${id}`);
  const bounds={left:region.left+minX,top:region.top+minY,width:maxX-minX+1,height:maxY-minY+1};
  let pipeline=sharp(sourcePath).extract(bounds);
  if(square)pipeline=pipeline.resize(96,96,{fit:'inside'}).extend({top:16,bottom:16,left:16,right:16,background:'#00000000'}).resize(128,128,{fit:'contain',background:'#00000000'});
  else pipeline=pipeline.extend({top:8,bottom:8,left:8,right:8,background:'#00000000'});
  const file=`${id}_${revision}.png`;
  await pipeline.png().toFile(path.join(out,file));
  const m=await sharp(path.join(out,file)).metadata();
  entries.push({id,file,source:`source/${source}.png`,source_bounds:bounds,texture_size:[m.width,m.height],display_size:display,nine_patch_display_margins:margins,alpha:true});
}
async function main(){
  await fs.mkdir(path.join(out,'source'),{recursive:true});
  for(const [key,file]of Object.entries(inputs))await fs.copyFile(path.join(generated,file),path.join(out,'source',`${key}.png`));
  if(muted)await fs.copyFile('C:/Users/34784/AppData/Local/Temp/codex-clipboard-3adf228a-f904-4ccd-81f3-0057e2c62110.png',path.join(out,'source/palette_reference.png'));
  const wide=await sharp(path.join(out,'source/wide_frames.png')).metadata();
  await sprite('skillbar_panel','wide_frames',{left:0,top:0,width:wide.width,height:480},[1344,184],[64,24,64,24]);
  await sprite('timeline_panel','wide_frames',{left:0,top:480,width:wide.width,height:wide.height-480},[720,80],[90,20,90,20]);
  const panelSpecs=[
    ['battle_title',muted?[0,150,460,250]:[0,150,440,250],[200,40],[36,12,36,12]],
    ['control_button',muted?[460,150,350,250]:[440,150,350,250],[140,40],[16,12,16,12]],
    ['resource_chip',muted?[810,150,276,250]:[790,150,296,250],[96,40],[14,12,14,12]],
    ['health_frame',[0,440,440,290],[174,40],[18,12,18,12]],
    ['health_fill_ally',muted?[440,440,340,290]:[440,440,320,290],[158,7],null],
    ['health_fill_enemy',muted?[780,440,306,290]:[760,440,326,290],[158,7],null],
    ['queue_row',[0,730,360,270],[132,62],[14,12,14,12]],
    ['queue_row_active',[360,730,370,270],[132,62],[14,12,14,12]],
    ['queue_row_warning',[730,730,356,270],[132,62],[14,12,14,12]],
    ['skill_card_frame',[0,1000,362,448],[108,136],[12,12,12,28]],
    ['skill_slot_empty',[362,1000,362,448],[108,136],[12,12,12,12]],
    ['target_corners',[724,1000,362,448],[180,216],null],
  ];
  for(const[id,b,display,margin]of panelSpecs)await sprite(id,'panels',{left:b[0],top:b[1],width:b[2],height:b[3]},display,margin);
  const iconNames=['energy','mana','speed','pause','queue_current','queue_down','timeline_current','timeline_ally','timeline_enemy','play','undo','warning'];
  const im=await sharp(path.join(out,'source/icons.png')).metadata();
  for(let i=0;i<iconNames.length;i++){
    const row=Math.floor(i/4),top=[0,405,724][row],height=[405,319,362][row];
    await sprite(`icon_${iconNames[i]}`,'icons',{left:i%4*im.width/4,top,width:im.width/4,height},i<2?[28,28]:[20,20],null,true);
  }
  await sprite('queue_panel','containers',{left:0,top:0,width:370,height:1024},[156,440],[30,40,30,40]);
  await sprite('planning_panel','containers',{left:370,top:0,width:1166,height:1024},[760,300],null);
  // A raised title must not be stretched; export it independently from the body.
  await sprite('planning_title_tab','containers',{left:774,top:244,width:364,height:84},[220,48],null);
  // Brackets are anchored separately around a target rather than scaling their strokes.
  for(const[id,b]of [['tl',[724,1000,181,224]],['tr',[905,1000,181,224]],['bl',[724,1224,181,224]],['br',[905,1224,181,224]]])
    await sprite(`target_corner_${id}`,'panels',{left:b[0],top:b[1],width:b[2],height:b[3]},[28,28],null,true);
  const bgFile=`textures/battle/ink_terrace_multiteam_20261009_${revision}.png`;
  await fs.copyFile(path.join(out,'source/background.png'),path.join(root,bgFile));
  const manifest={version:1,status:'art_generated_not_connected',design_canvas:[1672,941],generator:'built-in imagegen',background:bgFile,palette:{surface:'#062B2C',gold:'#C99B50',text:'#F4F0DF',highlight:'#F1D77A',energy:'jade_green',mana:'cyan_blue',warning:'cinnabar_red'},notes:['Display sizes and margins use design-canvas pixels; convert margins to texture pixels before NinePatchRect use.','Do not stretch icons. Raster text and counts are intentionally absent.','planning_panel raised title must be handled independently for adaptive width; use its extracted title and a plain reusable body.','UI assets are candidates for visual review and not loaded by existing scenes.'],assets:entries};
  if(muted){manifest.version=2;manifest.palette={surface:'#1B3031',trim:'#A9956C',text:'#F4F0DF',highlight:'#C4B58C',energy:'#75A98D',mana:'#6E9EAC',warning:'#A55343'};manifest.notes.push('Muted v2 supersedes bright-gold v1 as user-selected palette direction.');}
  await fs.writeFile(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
  // Contact sheet uses actual exported PNGs, not another generated UI mockup.
  const W=1600,H=1900;
  let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><rect width="${W}" height="${H}" fill="#e8dfcd"/><style>text{font-family:Microsoft YaHei,sans-serif;fill:#263c36}.heading{font-size:28px;font-weight:bold}.label{font-size:17px}</style><text x="40" y="48" class="heading">战斗界面 · 独立素材预览</text><text x="40" y="80" class="label">墨青漆底 / 暖金细边 / 精力绿 / 法力蓝 · 无字底图 · 透明 PNG</text>`;
  const composites=[];
  for(let i=0;i<entries.length;i++){
    const e=entries[i],col=i%4,row=Math.floor(i/4),x=32+col*390,y=110+row*200;
    svg+=`<rect x="${x}" y="${y}" width="374" height="188" rx="10" fill="#d2c8b5"/><text x="${x+12}" y="${y+173}" class="label">${e.id}</text>`;
    const tile=await sharp(path.join(out,e.file)).resize(346,138,{fit:'inside'}).png().toBuffer({resolveWithObject:true});
    composites.push({input:tile.data,left:Math.round(x+187-tile.info.width/2),top:Math.round(y+74-tile.info.height/2)});
  }
  svg+='</svg>';
  if(muted)svg=svg.replace('墨青漆底 / 暖金细边 / 精力绿 / 法力蓝','灰墨青 / 哑光米赭边 / 精力绿 / 法力蓝');
  await sharp(Buffer.from(svg)).composite(composites).png().toFile(path.join(out,`asset_preview_${revision}.png`));
  console.log(JSON.stringify({assets:entries.length,output:out,background:bgFile}));
}
main().catch(e=>{console.error(e);process.exit(1)});
