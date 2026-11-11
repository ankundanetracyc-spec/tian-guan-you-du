// Diagnostic collars/shoulders only. Read installed/staged PNGs; write QA outputs only.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const PACK=path.resolve(__dirname,'..'),ROOT=path.resolve(__dirname,'../../../..');
const ACTIVE=path.join(ROOT,'mods/appearance/ink_three_variants_20261004');
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const indices=[6,7,8,9],clothes=[1,2,3,4,5,6,7],W=1024,H=1536;
const crop={left:200,top:470,width:700,height:430};
const part=(slot,index)=>path.join(ACTIVE,'male',slot,String(index).padStart(2,'0')+'.png');
const own=rel=>path.join(PACK,'qa',rel);
(async()=>{
 fs.mkdirSync(own('collars'),{recursive:true});
 const files=new Set();
 for(const i of indices)for(const slot of ['hair_front','hair_back'])files.add(fs.existsSync(part(slot,i))?part(slot,i):path.join(PACK,'male',slot,String(i).padStart(2,'0')+'.png'));
 for(const c of clothes)for(const slot of ['clothing','clothing_back'])files.add(part(slot,c));
 for(const slot of ['face_shape','nose','eyes','eyebrows','mouth'])files.add(part(slot,1));
 const before=Object.fromEntries([...files].map(f=>[f,sha(f)]));
 const buffers=Object.fromEntries([...files].map(f=>[f,fs.readFileSync(f)]));
 const lookup=(slot,index)=>buffers[part(slot,index)]||buffers[path.join(PACK,'male',slot,String(index).padStart(2,'0')+'.png')];
 const tiles=[],records=[],tileW=350,tileH=215,header=24;
 let svg='<svg xmlns="http://www.w3.org/2000/svg" width="2450" height="956"><g fill="#253136" font-size="16" font-family="sans-serif">';
 for(let row=0;row<indices.length;row++)for(let col=0;col<clothes.length;col++){
  const i=indices[row],c=clothes[col];
  const order=[lookup('hair_back',i),lookup('clothing_back',c),lookup('face_shape',1),lookup('clothing',c),lookup('nose',1),lookup('eyes',1),lookup('eyebrows',1),lookup('mouth',1),lookup('hair_front',i)].map(input=>({input,left:0,top:0}));
  const full=await sharp({create:{width:W,height:H,channels:4,background:'#e8e1d6'}}).composite(order).png().toBuffer();
  const close=await sharp(full).extract(crop).png().toBuffer();
  const output=own('collars/hair'+String(i).padStart(2,'0')+'_clothes'+String(c).padStart(2,'0')+'.png');
  fs.writeFileSync(output,close);
  const thumb=await sharp(close).resize(tileW,tileH,{kernel:'lanczos3'}).png().toBuffer();
  tiles.push({input:thumb,left:col*tileW,top:row*(tileH+header)+header});
  svg+='<text x="'+(col*tileW+5)+'" y="'+(row*(tileH+header)+18)+'">Hair'+String(i).padStart(2,'0')+' / Clothes'+String(c).padStart(2,'0')+'</text>';
  records.push({hair_index:i,clothing_index:c,crop_file:path.relative(PACK,output).replaceAll('\\','/'),sha256:sha(output)});
 }
 svg+='</g></svg>';
 await sharp({create:{width:2450,height:956,channels:4,background:'#e8e1d6'}}).composite([...tiles,{input:Buffer.from(svg),left:0,top:0}]).png().toFile(own('four_styles_seven_collars.png'));
 for(const [f,h] of Object.entries(before))if(sha(f)!==h)throw Error('A read source changed while rendering: '+f);
 fs.writeFileSync(own('four_styles_seven_collars_report.json'),JSON.stringify({scope:'4 new matching hair pairs ×7 existing front/back clothing pairs; diagnostic collar/shoulder crops only',crop_original_canvas:crop,rows_hair_indices:indices,columns_clothing_indices:clothes,source_hashes:before,all_read_sources_preserved:true,active_or_candidate_pngs_written:false,records},null,2)+'\n');
 console.log(JSON.stringify({rendered:28,table:'qa/four_styles_seven_collars.png',natural_scale_crops:'qa/collars/hair08_clothes01.png..07.png',production_or_candidate_writes:false}));
})().catch(error=>{console.error(error.stack||String(error));process.exitCode=1;});

