// Read-only composition review. This script never installs or edits source assets.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const pack = path.resolve(__dirname, '..');
const project = path.resolve(pack, '../../..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004');
const preview = path.join(pack, 'preview');
const qa = path.join(pack, 'qa');
const W = 1024, H = 1536;
const specs = {hair_front:3, hair_back:3, eyebrows:3, eyes:2, nose:1, mouth:1, face_shape:1};
const order = ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'];
const names = count => Array.from({length:count}, (_,i) => String(i+1).padStart(2,'0'));
const maleFiles = Object.entries(specs).flatMap(([part,count]) => names(count).map(id => path.join(pack,'male',part,id+'.png')));
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const rel = file => path.relative(project,file).replaceAll('\\','/');

function pngFiles(dir) {
  return fs.readdirSync(dir,{withFileTypes:true}).flatMap(entry => entry.isDirectory() ? pngFiles(path.join(dir,entry.name)) : entry.name.endsWith('.png') ? [path.join(dir,entry.name)] : []);
}

async function measure(file,gender) {
  const metadata = await sharp(file).metadata();
  if (metadata.width !== W || metadata.height !== H || !metadata.hasAlpha || metadata.channels !== 4) throw new Error('Expected 1024x1536 RGBA: '+file);
  const {data,info} = await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let x0=W,y0=H,x1=-1,y1=-1, visible=0, transparent=0;
  for(let y=0;y<H;y++) for(let x=0;x<W;x++) {
    const alpha=data[(y*W+x)*4+3];
    if(!alpha) transparent++;
    if(alpha<40) continue;
    visible++;
    x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);
  }
  const rows = {};
  if(file.includes('hair_')) for(const y of [175,250,300,350,400,700,900,1100]) {
    let left=W,right=-1;
    for(let x=0;x<W;x++) if(data[(y*W+x)*4+3]>=40) {left=Math.min(left,x);right=x;}
    rows[y]=right<left ? null : [left,right+1];
  }
  return {gender,file:rel(file),canvas:[info.width,info.height],rgba:true,sha256:sha(file),alpha_threshold:40,bbox:x1<0?null:[x0,y0,x1+1,y1+1],visible_width:x1<0?0:x1-x0+1,visible_height:y1<0?0:y1-y0+1,visible_pixels:visible,fully_transparent_pixels:transparent,hair_row_intervals:rows};
}

function maleSelection(front=1,back=1,brow=1,eye=1) {
  const selected={hair_front:front,hair_back:back,eyebrows:brow,eyes:eye,nose:1,mouth:1,face_shape:1,clothing:1,face_accessory:1};
  return order.map(part => path.join(['clothing','face_accessory'].includes(part)?active:pack,'male',part,String(selected[part]).padStart(2,'0')+'.png'));
}

function femaleSelection() {
  return order.map(part => path.join(active,'female',part,part==='face_shape'?'04.png':'01.png'));
}

async function composite(inputs) {
  return sharp({create:{width:W,height:H,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite(inputs.map(input=>({input}))).png().toBuffer();
}

async function grid(images,columns,cellWidth,cellHeight,bg,file) {
  const rows=Math.ceil(images.length/columns);
  const cells=await Promise.all(images.map(async(input,i)=>({input:await sharp(input).flatten({background:bg}).resize(cellWidth,cellHeight,{fit:'fill',kernel:'lanczos3'}).png().toBuffer(),left:(i%columns)*cellWidth,top:Math.floor(i/columns)*cellHeight})));
  await sharp({create:{width:columns*cellWidth,height:rows*cellHeight,channels:3,background:bg}}).composite(cells).png().toFile(path.join(preview,file));
}

async function main() {
  const extra=[path.join(active,'male/clothing/01.png'),path.join(active,'male/face_accessory/01.png'),...femaleSelection()];
  const missing=[...maleFiles,...extra].filter(file=>!fs.existsSync(file));
  if(missing.length) throw new Error('New assets are not complete; no preview written. Missing:\n'+missing.map(rel).join('\n'));
  const femaleFiles=pngFiles(path.join(active,'female')).sort();
  if(maleFiles.length!==14 || femaleFiles.length!==22) throw new Error('Expected 14 new male and 22 active female PNGs; got '+maleFiles.length+' / '+femaleFiles.length);
  fs.mkdirSync(preview,{recursive:true});fs.mkdirSync(qa,{recursive:true});
  const measurements=await Promise.all([...maleFiles.map(f=>measure(f,'male_new')),...femaleFiles.map(f=>measure(f,'female_active'))]);
  const report={generated_at:new Date().toISOString(),read_only_sources:true,canvas:[W,H],male_assets_measured:14,female_assets_measured:22,alpha_bbox_threshold:40,bbox_convention:'[left,top,right_exclusive,bottom_exclusive]; null means empty layer',notes:['All source and runtime layers retain the same full canvas; no per-part fit to preview bounds.','All full portrait panels are rendered at one identical uniform scale.','Visible bounding boxes describe content and are not registration controls.','Male01 rear ends are longer than active female01; the report preserves this intentional long-haired silhouette and never crops or locally stretches it.'],comparison_columns:['female active style01','new male style01: brow01 eye01','new male style02: brow02 eye01','new male style03: brow03 eye02'],measurements};
  fs.writeFileSync(path.join(qa,'asset_size_report.json'),JSON.stringify(report,null,2)+'\n');
  const styles=await Promise.all([composite(maleSelection(1,1,1,1)),composite(maleSelection(2,2,2,1)),composite(maleSelection(3,3,3,2))]);
  for(let i=0;i<styles.length;i++) fs.writeFileSync(path.join(preview,'style'+String(i+1).padStart(2,'0')+'_transparent.png'),styles[i]);
  const female=await composite(femaleSelection());
  const mixes=[];
  for(let front=1;front<=3;front++) for(let back=1;back<=3;back++) mixes.push(await composite(maleSelection(front,back,1,1)));
  const six=[];
  for(let brow=1;brow<=3;brow++) for(let eye=1;eye<=2;eye++) {
    const face=await composite(maleSelection(1,1,brow,eye));
    six.push(await sharp(face).extract({left:335,top:255,width:365,height:330}).png().toBuffer());
  }
  for(const [label,bg] of [['light','#eee9dd'],['dark','#24282a']]) {
    await grid(styles,3,640,960,bg,'three_styles_'+label+'.png');
    await grid(mixes,3,384,576,bg,'nine_hair_mixes_'+label+'.png');
    await grid(six,3,548,495,bg,'six_feature_combinations_'+label+'.png');
    await grid([female,...styles],4,512,768,bg,'female_male_same_scale_'+label+'.png');
  }
  console.log(JSON.stringify({ok:true,male_assets:14,female_assets:22,preview:rel(preview),size_report:rel(path.join(qa,'asset_size_report.json')),composite_order:order}));
}

main().catch(error=>{console.error(error.message);process.exitCode=1;});
