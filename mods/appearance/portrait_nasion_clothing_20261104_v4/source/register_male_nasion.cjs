const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),root=path.resolve(pack,'../../..');
const old=path.join(pack,'installation/backup_before/male/nose/01.png');
const raw=path.join(__dirname,'raw/male_nasion_01.png');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
function smooth(t){t=Math.min(1,Math.max(0,t));return t*t*(3-2*t);}
(async()=>{
 const original=await sharp(old).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const scale=.7,left=158,top=102;
 const resized=await sharp(raw).resize(717,1075).png().toBuffer();
 const layer=await sharp({create:{width:1024,height:1536,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:resized,left,top}]).raw().toBuffer();
 const records=[];
 for(const opacity of [.45,.55,.65]){
  const overlay=Buffer.from(layer);
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
   const k=(y*1024+x)*4;
   const support=smooth((x-487)/5)*smooth((544-x)/5)*smooth((y-345)/5)*smooth((415-y)/10);
   overlay[k+3]=Math.round(overlay[k+3]*support*opacity);
   if(!overlay[k+3])overlay[k]=overlay[k+1]=overlay[k+2]=0;
  }
  const overlayPNG=await sharp(overlay,{raw:original.info}).png().toBuffer();
  const combined=await sharp(original.data,{raw:original.info}).composite([{input:overlayPNG}]).raw().toBuffer();
  let changed=0,lowerChanged=0,x0=1024,y0=1536,x1=-1,y1=-1;
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
   const k=(y*1024+x)*4;
   if(overlay[k+3]===0||y>=415)original.data.copy(combined,k,k,k+4);
   if(!combined.subarray(k,k+4).equals(original.data.subarray(k,k+4))){changed++;x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);if(y>=415)lowerChanged++;}
  }
  if(lowerChanged)throw Error('Nose lower region changed');
  const result=await sharp(combined,{raw:original.info}).png().toBuffer();
  const percent=Math.round(opacity*100);
  const baseFace=path.join(__dirname,'reference/male_face_current.png');
  // Face before already contains the old nose. Add only the nasion overlay.
  const composed=await sharp(baseFace).composite([{input:overlayPNG}]).png().toBuffer();
  await sharp(composed).flatten({background:'#e8e1d6'}).extract({left:420,top:330,width:195,height:150}).resize(780,600).png().toFile(path.join(pack,'qa',`male_nasion_after_${percent}.png`));
  records.push({opacity,changed_pixels:changed,lower_changed_pixels:lowerChanged,changed_bbox:[x0,y0,x1,y1]});
  if(opacity===.55){
   fs.writeFileSync(path.join(pack,'male/nose/01.png'),result);
   fs.writeFileSync(path.join(__dirname,'male_nasion_registered.png'),overlayPNG);
  }
 }
 fs.writeFileSync(path.join(__dirname,'male_nasion_registration.json'),JSON.stringify({mode:'built-in image_gen and whole-layer registration',raw:path.relative(pack,raw),raw_sha256:hash(raw),canvas:[1024,1536],uniform_scale:scale,left,top,chosen_opacity:.55,integration:'generated upper-nose shadow overlay composited over old nose; finite upper support tapers at y415; original bytes restored outside painted support',lower_nose_y_from:415,records,final_sha256:hash(path.join(pack,'male/nose/01.png')),full_three_point_2px_certification:false},null,2)+'\n');
 console.log(records);
})().catch(e=>{console.error(e);process.exitCode=1;});
