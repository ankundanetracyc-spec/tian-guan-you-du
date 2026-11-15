const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const W=1024,H=1536,sha=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const canvas=(width=W,height=H,background={r:0,g:0,b:0,alpha:0})=>sharp({create:{width,height,channels:4,background}});
async function metrics(file){
 const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const bounds={};let zero=0,opaque=0;
 for(const threshold of [1,20,180]){let bb=[W,H,-1,-1],n=0;for(let y=0;y<H;y++)for(let x=0;x<W;x++){const a=data[(y*W+x)*4+3];if(a>=threshold){n++;bb=[Math.min(bb[0],x),Math.min(bb[1],y),Math.max(bb[2],x),Math.max(bb[3],y)];}}bounds[threshold]={bbox:bb,pixels:n};}
 for(let k=3;k<data.length;k+=4){if(!data[k])zero++;if(data[k]===255)opaque++;}
 return {width:info.width,height:info.height,channels:info.channels,zero_alpha_pixels:zero,opaque_pixels:opaque,bounds};
}
async function transform(raw,output,s,x,y){
 const size=await sharp(raw).metadata(),width=Math.round(size.width*s),height=Math.round(size.height*s);
 let b=await sharp(raw).resize(width,height,{fit:'fill',kernel:'lanczos3'}).png().toBuffer();
 const left=Math.max(0,-x),top=Math.max(0,-y),cw=Math.min(width-left,W-Math.max(0,x)),ch=Math.min(height-top,H-Math.max(0,y));
 if(left||top||cw!==width||ch!==height)b=await sharp(b).extract({left,top,width:cw,height:ch}).png().toBuffer();
 fs.mkdirSync(path.dirname(output),{recursive:true});await canvas().composite([{input:b,left:Math.max(0,x),top:Math.max(0,y)}]).png().toFile(output);
}
const configs=[
 {part:'eyebrows',id:'01',raw_id:'01_refined',scale:123/124.0487740073267,source_centers:[[455.6907853988428,344.7453811175958],[579.7395594061695,345.05854029527086]],target_centers:[[456,346],[579,345]]},
 {part:'eyebrows',id:'02',scale:123/106.09355,source_centers:[[461.1316,360.0788],[567.2251,360.0127]],target_centers:[[456,346],[579,345]]},
 {part:'eyebrows',id:'03',scale:123/114.29347,source_centers:[[455.0505,372.6935],[569.3440,372.6160]],target_centers:[[456,346],[579,345]]},
 {part:'eyes',id:'01',scale:1,source_centers:[[464,410],[568,410]],target_centers:[[464,375],[569,374]]},
 {part:'eyes',id:'02',scale:1,source_centers:[[460,389],[570,389]],target_centers:[[464,375],[569,374]]},
 {part:'nose',id:'01',scale:.70,source_centers:[[513,503]],target_centers:[[517,451]]},
 {part:'mouth',id:'01',scale:.84,source_centers:[[513,581]],target_centers:[[517,481]]}
];
(async()=>{
 fs.mkdirSync(path.join(pack,'qa'),{recursive:true});const records=[];
 for(const c of configs){
 const source_center=c.source_centers.reduce((a,b)=>[a[0]+b[0]/c.source_centers.length,a[1]+b[1]/c.source_centers.length],[0,0]);
 const target_center=c.target_centers.reduce((a,b)=>[a[0]+b[0]/c.target_centers.length,a[1]+b[1]/c.target_centers.length],[0,0]);
 const tx=Math.round(target_center[0]-c.scale*source_center[0]),ty=Math.round(target_center[1]-c.scale*source_center[1]);
 const raw=path.join(__dirname,'raw_features',c.part,(c.raw_id||c.id)+'.png'),out=path.join(pack,'male',c.part,c.id+'.png');await transform(raw,out,c.scale,tx,ty);
 records.push({...c,source:raw,output:out,translation:[tx,ty],whole_layer_uniform_transform:true,three_noncollinear_control_certification:false,source_sha256:sha(raw),sha256:sha(out),metrics:await metrics(out)});
 }
 fs.writeFileSync(path.join(__dirname,'feature_registration.json'),JSON.stringify({rig_id:'our_selected_ink_portrait_rig_20261003_v1',compatibility_family:'our_selected_ink_male_20261003_v1',method:'One uniform whole-layer scale and integer translation per component; no local edits, no Alpha modifications.',records},null,2)+'\n');
 const face=path.join(pack,'male/face_shape.png');
 for(const [theme,bg]of [['light','#ebe5d9'],['dark','#252b30']]){
 const gallery=[];
 for(let eye=1;eye<=2;eye++)for(let brow=1;brow<=3;brow++){
 const files=[face,path.join(pack,'male/eyes',String(eye).padStart(2,'0')+'.png'),path.join(pack,'male/eyebrows',String(brow).padStart(2,'0')+'.png'),path.join(pack,'male/nose/01.png'),path.join(pack,'male/mouth/01.png')];
 const full=await canvas(W,H,bg).composite(files.map(input=>({input}))).png().toBuffer();
 const crop=await sharp(full).extract({left:365,top:270,width:310,height:305}).resize(620,610).png().toBuffer();gallery.push({input:crop,left:(brow-1)*620,top:(eye-1)*610});
 }
 await canvas(1860,1220,bg).composite(gallery).png().toFile(path.join(pack,'qa','six_feature_combinations_'+theme+'.png'));
 }
 const detail=[];for(let i=0;i<records.length;i++){const r=records[i],bb=r.metrics.bounds[20].bbox;const l=Math.max(0,bb[0]-20),t=Math.max(0,bb[1]-20),w=bb[2]-l+21,h=bb[3]-t+21;
 const b=await sharp(r.source).extract({left:Math.max(0,Math.floor((bb[0]-r.translation[0])/r.scale)-30),top:Math.max(0,Math.floor((bb[1]-r.translation[1])/r.scale)-30),width:Math.min(320,Math.ceil(w/r.scale)+60),height:Math.min(240,Math.ceil(h/r.scale)+60)}).flatten({background:'#dfcabc'}).resize(600,360,{fit:'contain',background:'#dfcabc'}).png().toBuffer();detail.push({input:b,left:(i%3)*600,top:Math.floor(i/3)*360});}
 await canvas(1800,1080,'#dfcabc').composite(detail).png().toFile(path.join(pack,'qa/raw_feature_details.png'));
 console.log(JSON.stringify(records.map(r=>({part:r.part,id:r.id,scale:r.scale,translation:r.translation,bbox:r.metrics.bounds[20].bbox}))));
})().catch(e=>{console.error(e);process.exitCode=1;});
