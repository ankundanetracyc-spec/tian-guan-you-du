// Read-only7x7 compatibility render of the existing and staged male front/back hair.
// Every written image/report is QA only; no production PNG or catalog is modified.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const PACK=path.resolve(__dirname,'..'),ROOT=path.resolve(__dirname,'../../../..');
const ACTIVE=path.join(ROOT,'mods/appearance/ink_three_variants_20261004');
const OUT=path.join(PACK,'qa/mix49'),W=1024,H=1536,INDEX=[1,4,5,6,7,8,9];
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const rel=f=>path.relative(PACK,f).replaceAll('\\','/');
function png(part,index){return path.join(index>=6?PACK:ACTIVE,'male',part,String(index).padStart(2,'0')+'.png');}
(async()=>{
 fs.mkdirSync(OUT,{recursive:true});
 const inputs=[...INDEX.map(i=>png('hair_front',i)),...INDEX.map(i=>png('hair_back',i)),
 ...['clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth'].map(p=>path.join(ACTIVE,'male',p,'01.png'))];
 const snapshots=Object.fromEntries([...new Set(inputs)].map(f=>[f,sha(f)]));
 const buffers=Object.fromEntries(Object.keys(snapshots).map(f=>[f,fs.readFileSync(f)]));
 const fixed=['clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth'].map(p=>({input:buffers[path.join(ACTIVE,'male',p,'01.png')],left:0,top:0}));
 const records=[];
 for(const kind of ['light','dark']){
  const color=kind==='dark'?'#253136':'#e8e1d6',thumbs=[];
  const tileW=234,tileH=300,header=22;
  let svg='<svg xmlns="http://www.w3.org/2000/svg" width="'+(7*tileW)+'" height="'+(7*(tileH+header))+'"><g fill="'+(kind==='dark'?'#f2eade':'#253136')+'" font-family="sans-serif" font-size="15">';
  for(let row=0;row<INDEX.length;row++)for(let col=0;col<INDEX.length;col++){
   const front=INDEX[col],back=INDEX[row],name='front'+String(front).padStart(2,'0')+'_back'+String(back).padStart(2,'0')+'_'+kind+'.png';
   const full=await sharp({create:{width:W,height:H,channels:4,background:color}})
    .composite([{input:buffers[png('hair_back',back)],left:0,top:0},...fixed,{input:buffers[png('hair_front',front)],left:0,top:0}]).png().toBuffer();
   const dest=path.join(OUT,name);fs.writeFileSync(dest,full);
   const thumb=await sharp(full).extract({left:260,top:40,width:585,height:750}).resize(tileW,tileH,{kernel:'lanczos3'}).png().toBuffer();
   thumbs.push({input:thumb,left:col*tileW,top:row*(tileH+header)+header});
   svg+='<text x="'+(col*tileW+5)+'" y="'+(row*(tileH+header)+17)+'">F'+String(front).padStart(2,'0')+' / B'+String(back).padStart(2,'0')+'</text>';
   records.push({front,back,background:kind,file:rel(dest),sha256:sha(dest)});
  }
  svg+='</g></svg>';
  await sharp({create:{width:7*tileW,height:7*(tileH+header),channels:4,background:color}})
   .composite([...thumbs,{input:Buffer.from(svg),left:0,top:0}]).png().toFile(path.join(PACK,'qa/mix49_'+kind+'.png'));
 }
 for(const [file,digest] of Object.entries(snapshots))if(sha(file)!==digest)throw Error('A source changed while rendering QA: '+file);
 const report={status:'49 front/back combinations rendered on each of light/dark QA backgrounds',
  front_column_order:INDEX,back_row_order:INDEX,total_unique_combinations:49,rendered_backgrounds:2,
  reused_fixed_components:'Current male01 face/skin/neck, nose, eyes, eyebrows, mouth and first clothing pair.',
  all_source_files_preserved:true,active_writes:false,
  input_hashes:Object.fromEntries(Object.entries(snapshots).map(([p,h])=>[rel(p),h])),records,
  limitation:'Static QA composition only; view full-size cells for hair/skin coverage. This does not replace actual game/import/save verification.'};
 fs.writeFileSync(path.join(PACK,'qa/mix49_report.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({unique_combinations:49,backgrounds:2,qa_images:records.length,sheets:['qa/mix49_light.png','qa/mix49_dark.png'],active_writes:false}));
})().catch(e=>{console.error(e.stack||String(e));process.exitCode=1});

