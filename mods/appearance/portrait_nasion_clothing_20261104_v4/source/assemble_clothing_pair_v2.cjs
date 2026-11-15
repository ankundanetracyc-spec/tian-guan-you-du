const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const param=name=>{const n=process.argv.indexOf(name);return n<0?null:process.argv[n+1]};
const index=String(Number(param('--index')??1)).padStart(2,'0');
const bandFile=path.resolve(pack,param('--band')??`source/clothing${index}_back_v2_band_registered.png`);
const snapshot=path.join(pack,`installation/backup_before/male/clothing/${index}.png`);
const face=path.join(pack,'installation/backup_before/male/face_shape/01.png');
const frontFile=path.join(pack,`male/clothing/${index}.png`),rearFile=path.join(pack,`male/clothing_back/${index}.png`);
(async()=>{
 const old=await sharp(snapshot).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const bm=await sharp(bandFile).metadata();if(bm.width!==1024||bm.height!==1536||!bm.hasAlpha)throw Error('Rear band must be registered in common canvas');
 const splitY=550,cut=splitY*1024*4,front=Buffer.from(old.data),upper=Buffer.alloc(old.data.length);
 front.fill(0,0,cut);old.data.copy(upper,0,0,cut);
 const upperPNG=await sharp(upper,{raw:old.info}).png().toBuffer();
 const rear=await sharp(bandFile).composite([{input:upperPNG}]).png().toBuffer();
 const frontPNG=await sharp(front,{raw:old.info}).png().toBuffer();
 fs.mkdirSync(path.dirname(frontFile),{recursive:true});fs.mkdirSync(path.dirname(rearFile),{recursive:true});
 fs.writeFileSync(frontFile,frontPNG);fs.writeFileSync(rearFile,rear);
 const rearBytes=await sharp(rear).ensureAlpha().raw().toBuffer();
 const support=[[516,530],[516,550],[516,570]].map(([x,y])=>({px:[x,y],alpha:rearBytes[(y*1024+x)*4+3]}));
 if(support.some(p=>p.alpha<230))throw Error('Continuous hidden rear support is missing');
 for(const [bg,label]of [['#e8e1d6','light'],['#222a2b','dark']]){
  const composed=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:rear},{input:face},{input:frontPNG}]).png().toBuffer();
  await sharp(composed).extract({left:395,top:500,width:250,height:130}).resize(1000,520).png().toFile(path.join(pack,`qa/clothing${index}_selected_pair_v2_neck_${label}.png`));
 }
 const geometry=JSON.parse(fs.readFileSync(path.join(__dirname,'clothing01_back_v2_registration.json'),'utf8'));
 const report={index:Number(index),status:'paired_structural_redo_ready_for_independent_review',canvas:[1024,1536],face_sha256:hash(face),snapshot:`installation/backup_before/male/clothing/${index}.png`,snapshot_sha256:hash(snapshot),pure_band:path.relative(pack,bandFile).replaceAll('\\','/'),pure_band_sha256:hash(bandFile),shared_geometry_record:'source/clothing01_back_v2_registration.json',shared_geometry_record_sha256:hash(path.join(__dirname,'clothing01_back_v2_registration.json')),geometry_maximum_residual_px:geometry.maximum_residual_px,front:{file:`male/clothing/${index}.png`,sha256:hash(frontFile),source:'unchanged original front/V/body at y>=550; upper rear-facing surface reassigned behind skin',identity_frame:{scale:1,rotation_degrees:0,translation:[0,0]},rgba_preserved_from_y:550},rear:{file:`male/clothing_back/${index}.png`,sha256:hash(rearFile),sources:['pure registered continuous AI back band','this garment snapshot upper surface y<550'],center_support:support},part_ownership_split_y:splitY,local_warp:false,geometry:'Original pixels keep their common-canvas coordinates; only depth ownership changes. Continuous new rear pigment is behind both the original upper surface and face. No skin-boundary clipping or bright gap patch.',user_visual_approval:false};
 fs.writeFileSync(path.join(__dirname,`clothing${index}_pair_v2_registration.json`),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({index,front:report.front.file,rear:report.rear.file,geometry_maximum_residual_px:geometry.maximum_residual_px,hidden_support:support}));
})().catch(e=>{console.error(e);process.exitCode=1;});
