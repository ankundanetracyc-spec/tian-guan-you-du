const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const {render}=require('./resample_rgba.cjs');
const pack=path.resolve(__dirname,'..'),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const id=String(Number(process.argv[2]??3)).padStart(2,'0');
const metadata=JSON.parse(fs.readFileSync(path.join(__dirname,`front${id}_measured_controls.json`),'utf8'));
function fit(source,target){
 const n=source.length,cs=[0,0],ct=[0,0];for(let i=0;i<n;i++)for(let c=0;c<2;c++){cs[c]+=source[i][c]/n;ct[c]+=target[i][c]/n;}
 let re=0,im=0,den=0;for(let i=0;i<n;i++){const x=source[i][0]-cs[0],y=source[i][1]-cs[1],u=target[i][0]-ct[0],v=target[i][1]-ct[1];re+=x*u+y*v;im+=x*v-y*u;den+=x*x+y*y;}
 const a=re/den,b=im/den,tx=ct[0]-a*cs[0]+b*cs[1],ty=ct[1]-b*cs[0]-a*cs[1];
 return{a,b,tx,ty,uniform_scale:Math.hypot(a,b),rotation_degrees:Math.atan2(b,a)*180/Math.PI};
}
(async()=>{
 const source=metadata.source_controls,target=metadata.target_controls;
 if(source.length<3)throw Error('At least three measured controls required.');
 let area=0;for(let i=1;i<source.length-1;i++)area+=Math.abs((source[i][0]-source[0][0])*(source[i+1][1]-source[0][1])-(source[i][1]-source[0][1])*(source[i+1][0]-source[0][0]));
 if(area<=1)throw Error('Measured controls must not be collinear.');
 const t=fit(source,target),residuals=source.map((p,i)=>Math.hypot(t.a*p[0]-t.b*p[1]+t.tx-target[i][0],t.b*p[0]+t.a*p[1]+t.ty-target[i][1]));
 const raw=path.join(__dirname,`front${id}_raw.png`),out=path.join(pack,`male/hair_front/${id}.png`);
 fs.mkdirSync(path.dirname(out),{recursive:true});await render(raw,out,t);
 const report={gender:'male',part:'hair_front',index:Number(id),tool:'built-in image_gen',source:`source/front${id}_raw.png`,source_sha256:hash(raw),output:`male/hair_front/${id}.png`,output_sha256:hash(out),source_control_record:`source/front${id}_measured_controls.json`,target_artwork:metadata.target,target_artwork_sha256:hash(path.join(pack,metadata.target)),source_controls:source,target_controls:target,actual_noncollinear_controls:true,source_control_triangle_double_area:area,transform:t,control_residuals_px:residuals,maximum_control_residual_px:Math.max(...residuals),measured_control_check_pass:Math.max(...residuals)<=2,sampling:'Whole-layer similarity, premultiplied colour source-area integration using resample_rgba.cjs; no local warp, no pigment edits.',scope:'Three selected actual forehead-opening support controls; this does not certify every hair strand or all future mix combinations.',canvas:[1024,1536],source_visual_reference:'new v5 master and original user triptych; rejected male v3/v4 artwork not used as a source'};
 fs.writeFileSync(path.join(__dirname,`front${id}_registration.json`),JSON.stringify(report,null,2)+'\n');
 const missing=[];for(const bg of[['light','#e7e2d6'],['dark','#22292b']]){
  const layers=[];
  for(const part of['hair_back','face_shape','eyes','eyebrows','nose','mouth','hair_front']){
   let n=part==='hair_front'||part==='hair_back'||part==='eyebrows'?id:'01';if(part==='eyes'&&id==='03')n='02';
   const file=path.join(pack,'male',part,n+'.png');if(fs.existsSync(file))layers.push({input:file});else if(!missing.includes(part+'/'+n))missing.push(part+'/'+n);
  }
  const full=await sharp({create:{width:1024,height:1536,channels:4,background:bg[1]}}).composite(layers).png().toBuffer();
  fs.writeFileSync(path.join(pack,`qa/front${id}_trial_${bg[0]}.png`),full);
  const rows=[];
  const original=path.join(__dirname,`front_reference_${id}_placed.png`),master=path.join(__dirname,'master03_registered.png');
  for(const[i,file]of[original,master,null].entries()){
   const bytes=file?await sharp(file).flatten({background:bg[1]}).png().toBuffer():full;
   const tile=await sharp(bytes).extract({left:290,top:100,width:460,height:650}).png().toBuffer();rows.push({input:tile,left:i*460,top:0});
  }
  await sharp({create:{width:1380,height:650,channels:4,background:bg[1]}}).composite(rows).png().toFile(path.join(pack,`qa/front${id}_original_master_trial_${bg[0]}.png`));
 }
 report.provisional_missing_new_parts=missing;fs.writeFileSync(path.join(__dirname,`front${id}_registration.json`),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
