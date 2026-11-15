const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const crypto=require('crypto');
const pack=path.resolve(__dirname,'..');
const base='E:/tmp/CodexData/generated_images/01a1a634-5600-73a3-a938-a8d7b936ea5c/';
const rows=[
 {id:'01',raw:'exec-5e6e9b50-d639-4842-bd0a-eff83b0108ba.png',scale:.97,left:24,top:27,description:'Wooden pin half-up rear knot and long continuous curtain'},
 {id:'02',raw:'exec-9734b6dd-ace4-49bd-b799-8fa7a483b3f2.png',scale:.64,left:189,top:59,description:'High rear bun with black cloth wrap and long continuous curtain'},
 {id:'03',raw:'exec-c9cfa472-a304-4549-bf06-57e0f24be5a8.png',scale:.64,left:184,top:47,description:'Loose rear hair, no bun or ornament'}
];
function sha(file){return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')}
async function main(){
 fs.mkdirSync(path.join(__dirname,'back_raw'),{recursive:true});
 fs.mkdirSync(path.join(pack,'male/hair_back'),{recursive:true});
 const records=[];
 for(const row of rows){
  const raw=path.join(__dirname,'back_raw',row.id+'.png');
  fs.copyFileSync(base+row.raw,raw);
  const meta=await sharp(raw).metadata();
  const width=Math.round(meta.width*row.scale),height=Math.round(meta.height*row.scale);
  const resized=await sharp(raw).resize(width,height,{kernel:'lanczos3'}).png().toBuffer();
  const {data,info}=await sharp({create:{width:1024,height:1536,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:resized,left:row.left,top:row.top}]).raw().toBuffer({resolveWithObject:true});
  for(let k=0;k<data.length;k+=4)if(!data[k+3])data[k]=data[k+1]=data[k+2]=0;
  const output=path.join(pack,'male/hair_back',row.id+'.png');
  await sharp(data,{raw:info}).png().toFile(output);
  await sharp(output).flatten({background:'#eee9dd'}).png().toFile(path.join(__dirname,'back_raw',row.id+'_registered_light.png'));
  await sharp(output).flatten({background:'#24282a'}).png().toFile(path.join(__dirname,'back_raw',row.id+'_registered_dark.png'));
  records.push({...row,native_canvas:[meta.width,meta.height],source_path:'source/back_raw/'+row.id+'.png',output_path:'male/hair_back/'+row.id+'.png',output_canvas:[1024,1536],resized_canvas:[width,height],source_sha256:sha(raw),output_sha256:sha(output),transform:'One whole-layer uniform scale and translation. No local deformation, mask, alpha clipping, or color edits. Zero-alpha RGB cleared only.',certified_three_point_2px:false});
 }
 fs.writeFileSync(path.join(__dirname,'back_registration.json'),JSON.stringify({rig_id:'our_selected_ink_portrait_rig_20261003_v1',gender:'male',part:'hair_back',new_generation:true,built_in_tool:true,records},null,2)+'\n');
 console.log(JSON.stringify(records.map(r=>({id:r.id,output:r.output_path,scale:r.scale,left:r.left,top:r.top}))));
}
main().catch(e=>{console.error(e);process.exitCode=1});
